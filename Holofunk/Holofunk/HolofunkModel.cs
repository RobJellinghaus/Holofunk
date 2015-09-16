////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>The status of a given Loopie.</summary>
    /// <remarks>All Loopies start out in Record condition, then move to Loop
    /// condition, and from there may go to Mute and then back to Loop indefinitely.</remarks>
    [Flags]
    enum LoopieCondition
    {
        Record = 0x1,
        Loop = 0x2,
        Mute = 0x4,
    }

    /// <summary>The state of Holofunk as a whole, viewed from the LoopieStateMachine.</summary>
    /// <remarks>This is really mostly a passive container class with some helper methods on it.
    /// External code currently manages the state quite imperatively.  This contains one PlayerState
    /// per player, among lots of other content.</remarks>
    class HolofunkModel : StateMachines.Model
    {
        // basic component access
        readonly Clock m_clock;
        readonly HolofunkSceneGraph m_sceneGraph;
        readonly List<Loopie> m_loopies = new List<Loopie>();
        readonly List<Loopie> m_loopiesToRemove = new List<Loopie>();

        readonly HolofunkBass m_bass;
        readonly HolofunKinect m_kinect;

        readonly BufferAllocator<float> m_audioAllocator;
        readonly BufferAllocator<byte> m_videoAllocator;

        readonly HolofunkTextureContent m_holofunkContent;
        readonly Vector2 m_viewportSize;

        readonly List<PlayerModel> m_players;

        /// <summary>
        /// How often is GameUpdate being called?
        /// </summary>
        readonly Stopwatch m_frameRateStopwatch = new Stopwatch();
        readonly FloatAverager m_frameRateMsecAverager = new FloatAverager(60); // 60 frames ~= 2 seconds = OK


        // new requested BPM value, if any -- the Wiimote thread updates this, and
        // the XNA update thread actually changes the clock (to avoid racing against
        // the main metronome BeatNode)
        float m_requestedBPM;

        /// <summary>
        /// The index of the current slide.
        /// </summary>
        int m_slideIndex;

        /// <summary>
        /// Is the slide visible?
        /// </summary>
        bool m_slideVisible;

        /// <summary>Are we showing the secondary view in the secondary window?  (If not, the primary view is shown.)</summary>
        HolofunkView m_secondaryView = HolofunkView.Secondary;

        internal HolofunkModel(
            GraphicsDevice graphicsDevice,
            Clock clock,
            HolofunkBass bass, 
            HolofunKinect kinect,
            HolofunkTextureContent content,
            Vector2 viewportSize,
            float initialBPM,
            BufferAllocator<float> audioAllocator,
            BufferAllocator<byte> videoAllocator)
        {
            m_clock = clock;
            m_bass = bass;
            m_kinect = kinect;
            m_viewportSize = viewportSize;
            m_holofunkContent = content;
            m_audioAllocator = audioAllocator;
            m_videoAllocator = videoAllocator;

            m_requestedBPM = initialBPM;

            m_sceneGraph = new HolofunkSceneGraph(
                graphicsDevice,
                m_viewportSize,
                m_kinect.DisplayTexture,
                m_holofunkContent,
                bass,
                m_clock);

            m_players = new List<PlayerModel>();
            m_players.Add(new PlayerModel(0, HolofunkBassAsio.AsioInputChannelId0, this));
            m_players.Add(new PlayerModel(1, HolofunkBassAsio.AsioInputChannelId1, this));

            Kinect.RegisterPlayerEventSink(0, m_players[0]);
            Kinect.RegisterPlayerEventSink(1, m_players[1]);
        }

        #region Properties

        internal Clock Clock { get { return m_clock; } }
        internal HolofunkSceneGraph SceneGraph { get { return m_sceneGraph; } }
        internal HolofunkBass Bass { get { return m_bass; } }
        internal BufferAllocator<byte> VideoAllocator { get { return m_videoAllocator; } }
        internal HolofunKinect Kinect { get { return m_kinect; } }
        internal HolofunkTextureContent Content { get { return m_holofunkContent; } }

        internal PlayerModel this[int i]
        {
            get { return m_players[i]; } 
        }

        internal HolofunkView SecondaryView { get { return m_secondaryView; } set { m_secondaryView = value; } }

        // The requested BPM.
        internal float RequestedBPM { get { return m_requestedBPM; } set { m_requestedBPM = value < 10 ? 10 : value; } }

        internal List<Loopie> Loopies { get { return m_loopies; } }

        internal bool IsRecordingWAV { get { return m_bass.IsRecordingWAV; } }

        internal bool SlideVisible { get { return m_slideVisible; } set { m_slideVisible = value; } }
        internal int SlideIndex { get { return m_slideIndex; } }

        #endregion

        internal void AdvanceSlide(int direction)
        {
            Debug.Assert(direction == 1 || direction == -1);
            m_slideIndex += direction;
            m_slideIndex %= Content.Slides.Length;
            if (m_slideIndex < 0) 
            {
                m_slideIndex = Content.Slides.Length - 1;
            }
        }

        public void StartRecordingWAV()
        {
            m_bass.StartRecordingWAV();
        }

        public void StopRecordingWAV()
        {
            m_bass.StopRecordingWAV();
        }

        public void RemoveLoopie(Loopie loopie)
        {
            lock (m_loopiesToRemove) { // [KinectThread] avoid racing
                m_loopiesToRemove.Add(loopie);
            }
        }

        public Loopie StartRecordingNewLoopie(Moment now, int playerId, bool isRight, int channel, Transform handPosition)
        {
            lock (m_bass) {
                if (m_bass.StreamPoolFreeCount == 0) {
                    // do not start recording, we have no free streams
                    return null;
                }

                DenseSampleFloatStream newAudioStream =
                    new DenseSampleFloatStream(now.Time - MagicNumbers.LatencyCompensationDuration, m_audioAllocator, 1);

                SparseSampleByteStream newVideoStream =
                    new SparseSampleByteStream(now.Time, m_videoAllocator, MagicNumbers.HeadCaptureBytes);

                // This both creates the new loopie, and adds it to all the various audio and video recorder lists;
                // after this call, data is already flowing into the loopie.
                Loopie newLoopie = new Loopie(now, this, Content, newAudioStream, newVideoStream, handPosition, playerId, isRight, channel);

                // This AddRecorder method is internally synchronized to avoid problems with ASIO thread races.
                Bass.AddRecorder(now, channel, newLoopie);

                // This AddTimedRecorder method is likewise synchronized to avoid problems with Kinect thread races.
                this[playerId].AddTimedRecorder(newLoopie);

                return newLoopie;
            }
        }

        /// <summary>
        /// Update all models, on the game thread.
        /// </summary>
        /// <remarks>
        /// [GameThread]
        /// </remarks>
        /// <param name="now"></param>
        public override void GameUpdate(Moment now)
        {
            if (m_frameRateStopwatch.IsRunning) {
                m_frameRateMsecAverager.Update(m_frameRateStopwatch.ElapsedMilliseconds);
                m_frameRateStopwatch.Restart();
            }
            else {
                m_frameRateStopwatch.Start();
            }

            // and handle any requested removals
            lock (m_loopiesToRemove) { // [GameThread] avoid racing
                while (m_loopiesToRemove.Count > 0) {
                    Loopie toRemove = m_loopiesToRemove[m_loopiesToRemove.Count - 1];
                    toRemove.Dispose(now);
                    m_loopiesToRemove.RemoveAt(m_loopiesToRemove.Count - 1);
                    Loopies.Remove(toRemove);
                }

                m_loopiesToRemove.Clear();
            }

            foreach (PlayerModel pModel in m_players) {
                pModel.GameUpdate(now);
            }

            // Now have the loopies update to the current time.
            lock (Loopies) {
                foreach (Loopie loopie in Loopies) {
                    loopie.GameUpdate(now);
                }
            }

            m_sceneGraph.Update(this, Kinect, now, m_frameRateMsecAverager.Average);
        }

        /// <summary>
        /// Update the current body frame for this player.
        /// </summary>
        /// <remarks>
        /// [KinectThread]
        /// </remarks>
        public void BodyFrameUpdate(HolofunKinect kinect)
        {
            foreach (PlayerModel pModel in m_players) {
                pModel.BodyFrameUpdate(kinect);
            }
        }
    }
}
