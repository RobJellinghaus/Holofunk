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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>An abstract "widget" that allows control of a Loop.</summary>
    /// <remarks>The top-level object that is created when a user starts recording a new loop.
    /// Audio and video data is routed to this Loopie as long as it remains in the Holofunk class's
    /// active list of Recorders.</remarks>
    class Loopie : Recorder<Sample, float>, TimedRecorder<Sample, byte>
    {
        static int s_nextId;

        readonly int m_id;
        
        LoopieCondition m_condition;

        // Is this loopie currently touched by the cursor?
        bool m_touched;
        // color of the player touching it (or white if both players)
        Color m_touchedColor;

        /// <summary>Index of the creating player.</summary>
        readonly int m_playerIndex;

        readonly HolofunkModel m_model;
        readonly TrackNode m_loopieNode;
        readonly DenseSampleFloatStream m_audioStream;
        readonly SparseSampleByteStream m_videoStream;

        /// <summary>
        /// The track gets created when the Loopie finishes being recorded.
        /// </summary>
        Track m_track;

        /// <summary>Track sample count that we want to stop at;
        /// set by StopRecordingAtNextBeat; if -1, we are not waiting for a track to finish</summary>
        ContinuousDuration m_currentRecordingContinuousDuration = (ContinuousDuration)(-1);

        /// <summary>The number of beats long we consider the currently recorded track to be.</summary>
        int m_currentRecordingBeatCount;

        readonly static Color[] s_colors = new[] {
            Color.Blue,
            Color.Purple,
            Color.SeaGreen,
            Color.DarkOrchid,
            Color.Aqua,
            Color.Magenta,
            Color.SteelBlue,
            Color.Tomato,
            Color.Turquoise,
            Color.RoyalBlue,
            Color.MediumVioletRed,
            Color.Maroon,
            Color.LimeGreen,
            Color.HotPink
        };

        /// <summary>
        /// Create a new Loopie, initially in Record mode; add it to the relevant Recorder lists so data will start
        /// flowing immediately.
        /// </summary>
        internal Loopie(
            Moment now,
            HolofunkModel model, 
            TextureContent content,
            DenseSampleFloatStream audioStream,
            SparseSampleByteStream videoStream,
            Transform transform,
            int playerIndex, 
            bool isRightHand,
            int inputChannel)
        {
            m_id = s_nextId++;
            m_playerIndex = playerIndex;
            m_audioStream = audioStream;
            m_videoStream = videoStream;

            SetCondition(LoopieCondition.Record);

            m_model = model;

            m_loopieNode = new TrackNode(
                m_model.SceneGraph.TrackGroupNode,
                transform,
                "Track #" + m_id,
                content,
                m_id,
                videoStream,
                false,
                // set the scale proportionately to the maximum level (on both channels)
                () => {
                    if (m_condition == LoopieCondition.Record) {
                        return m_model.Bass.LevelRatio(inputChannel) * 0.5f; // B4CK remove this 0.5f
                    }
                    else {
                        return m_track.LevelRatio;
                    }
                },
                // set the color: gray if muted, otherwise based on our unique ID
                () => m_condition == LoopieCondition.Record
                    ? new Color((byte)0x80, (byte)0, (byte)0, (byte)0x80)
                    : m_condition == LoopieCondition.Mute
                    ? HolofunkSceneGraph.MuteColor
                    : s_colors[m_id % s_colors.Length],
                () => m_condition == LoopieCondition.Mute
                    ? new Color((byte)0x80) // half transparent
                    : Color.White,
                () => {
                    if (m_condition == LoopieCondition.Record) {
                        return (long)(m_currentRecordingBeatCount * (int)m_model.Clock.ContinuousBeatDuration);
                    }
                    else {
                        return m_audioStream.DiscreteDuration;
                    }
                },
                () => now.CompleteBeats,
                () => m_condition == LoopieCondition.Record
                    ? new Color((byte)0xFF, (byte)0, (byte)0, (byte)0xFF)
                    : m_condition == LoopieCondition.Mute
                    ? HolofunkSceneGraph.MuteColor
                    : s_colors[m_id % s_colors.Length]);

            m_track = new Track(m_model.Bass, m_id, m_model[m_playerIndex].MicrophoneParameters, m_audioStream);
        }

        #region Properties

        internal int Id { get { return m_id; } }
        internal LoopieCondition Condition { get { return m_condition; } }
        internal Transform Transform 
        { 
            get { return m_loopieNode.LocalTransform; }
            set { m_loopieNode.LocalTransform = value; }
        }
        internal int PlayerIndex { get { return m_playerIndex; } }
        internal bool Touched 
        { 
            get { return m_touched; } 
            set { m_touched = value; } 
        }
        internal Color TouchedColor 
        { 
            get { return m_touchedColor; } 
            set { m_touchedColor = value; } 
        }
        internal Track Track { get { return m_track; } }
        public DenseSampleFloatStream Stream { get { return m_audioStream; } }

        #endregion

        /// <summary>How long should this track be, given how much has been recorded so far?</summary>
        /// <remarks>The goal of this method is to round up the current track length to one of
        /// the following values:  1 beat, 2 beats, or a multiple of 4 beats.</remarks>
        int TrackBeatLength(Duration<Sample> trackDuration)
        {
            float continuousBeatDuration = (float)m_model.Clock.ContinuousBeatDuration;

            // How many full or fractional beats elapsed so far?            
            Moment trackLengthMoment = m_model.Clock.Time((Time<Sample>)(long)trackDuration);
            
            float fbeats = ((float)(long)trackDuration) / continuousBeatDuration;
            int floorBeats = (int)Math.Floor(fbeats);
            //HoloDebug.Assert(trackLengthMoment.CompleteBeats == floorBeats);

            int beats = trackLengthMoment.CompleteBeats + 1;

            //HoloDebug.Assert(trackDuration <= beats * continuousBeatDuration);

            switch (beats) {
                case 1:
                case 2:
                    return beats;
                default:
                    return (beats + 0x3) & ~0x3;
                /*
            case 3:
            case 4:
                return 4;
            default:
                // round up to next multiple of 8
                return (beats + 0x7) & ~0x7;
                */
            }
        }

        /// <summary>
        /// Record some audio data.
        /// </summary>
        /// <remarks>
        /// [AsioThread]
        /// 
        /// This method is responsible for detecting when the audio stream is long enough, and shutting down the 
        /// recording for that stream in that case.
        /// </remarks>
        public bool Record(Moment now, Duration<Sample> duration, IntPtr audioBuffer)
        {
            int finalDurationCeiling = (int)Math.Ceiling((float)m_currentRecordingContinuousDuration);

            if (finalDurationCeiling > -1) {
                HoloDebug.Assert(m_audioStream.DiscreteDuration <= finalDurationCeiling);
            }

            int prevBeatCount = m_currentRecordingBeatCount;
            m_currentRecordingBeatCount = TrackBeatLength(m_audioStream.DiscreteDuration);
            if (prevBeatCount != m_currentRecordingBeatCount) {
                // breakpoint here
                int foo = 0;
            }

            bool done = false;

            if (finalDurationCeiling > -1
                && (m_audioStream.DiscreteDuration + duration) > finalDurationCeiling) {
                duration = finalDurationCeiling - m_audioStream.DiscreteDuration;
                done = true;
            }

            // Only the one ASIO stream will ever call into this method, so we don't need to lock m_audioStream here,
            // as there are no other objects concurrently mutating it.
            m_audioStream.Append(duration, audioBuffer);

            if (done) {
                // we are done!
                m_audioStream.Shut(m_currentRecordingContinuousDuration);
                m_videoStream.Shut(m_currentRecordingContinuousDuration);

                m_condition = LoopieCondition.Loop;

                lock (m_model.Loopies) {
                    m_model.Loopies.Add(this);
                }

                m_track.StartPlaying(now);

                // all done!
                return true;
            }
            else {
                return false;
            }
        }

        public bool Record(Time<Sample> time, byte[] videoBuffer, int offset, int width, int stride, int height)
        {
            if (m_condition != LoopieCondition.Record) {
                return true; // done
            }
            else {
                lock (m_videoStream) { // [KinectThread] to avoid racing writes and reads from m_videoStream
                    m_videoStream.AppendSliver(time, videoBuffer, offset, width, stride, height);
                }
                return false;
            }
        }

        public void StopRecordingAtNextBeat(Moment now, ParameterMap microphoneParameters)
        {
            Duration<Sample> originalDuration = m_audioStream.DiscreteDuration;
            int beatLength = TrackBeatLength(originalDuration);

            HoloDebug.Assert(beatLength > 0);
            m_currentRecordingContinuousDuration = (ContinuousDuration)(beatLength * (float)now.Clock.ContinuousBeatDuration);

            Duration<Sample> finalDurationCeiling = (long)Math.Ceiling((float)m_currentRecordingContinuousDuration);
            Duration<Sample> endDuration = m_audioStream.DiscreteDuration;
            // debugging apparent race here....
            HoloDebug.Assert(originalDuration == endDuration);
            HoloDebug.Assert(endDuration <= finalDurationCeiling);

            m_track.Parameters.ShareAll(microphoneParameters.Copy(false));
            m_track.UpdateEffects(now);
        }

        internal void SetCondition(LoopieCondition condition)
        {
            m_condition = condition;

            switch (m_condition) {
                case LoopieCondition.Record: return;
                case LoopieCondition.Mute: m_track.SetMuted(true); break;
                case LoopieCondition.Loop: m_track.SetMuted(false); break;
                default: HoloDebug.Assert(false, "Impossible LoopieCondition"); break;
            }
        }

        internal void GameUpdate(Moment now)
        {
            m_loopieNode.Update(now, Touched, TouchedColor);
        }

        public void Dispose(Moment now)
        {
            m_track.SetMuted(true);
            m_track.Dispose(now);

            m_videoStream.Dispose();

            m_loopieNode.Detach();
        }
    }
}
