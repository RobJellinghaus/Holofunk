////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    using HolofunkMachine = StateMachineInstance<LoopieEvent>;
    using Model = Holofunk.StateMachines.Model;

    /// <summary>The state of a single Holofunk player.</summary>
    class PlayerModel : Model, ITwoHandedEventSink
    {
        #region Fields

        /// <summary>Our player index.</summary>
        readonly int m_playerIndex;

        /// <summary>The ASIO channel for this player.</summary>
        /// <remarks>In practice this may be equal to the player index, but we don't wish to require this.</remarks>
        readonly int m_asioChannel;

        /// <summary>The parent state of which we are a component.</summary>
        readonly HolofunkModel m_parent;

        readonly PlayerHandModel m_leftHandModel; 
        readonly PlayerHandModel m_rightHandModel;
        
        /// <summary>The sound effect parameters currently defined for this player's sound input.</summary>
        ParameterMap m_microphoneParameters;

        /// <summary>Is the microphone selected in parameter mode?</summary>
        bool m_microphoneSelected;

        /// <summary>This player's scene graph.</summary>
        PlayerSceneGraph m_playerSceneGraph;

        /// <summary>Video recordees.</summary>
        List<TimedRecorder<Sample, byte>> m_recorders = new List<TimedRecorder<Sample, byte>>();

        #endregion

        internal PlayerModel(
            int playerIndex,
            int asioChannel,
            HolofunkModel holofunkModel)
        {
            m_playerIndex = playerIndex;
            m_asioChannel = asioChannel;

            m_parent = holofunkModel;

            // TODO: EVIL INIT ORDER DEPENDENCY: hand models contain state machines, which update scene graphs on initialization!
            m_playerSceneGraph = new PlayerSceneGraph(holofunkModel.SceneGraph, playerIndex, asioChannel);

            m_leftHandModel = new PlayerHandModel(this, false);
            m_rightHandModel = new PlayerHandModel(this, true);

            // the microphone has only per-loop parameters
            m_microphoneParameters = AllEffects.CreateParameterMap();
        }

        #region Properties

        internal HolofunkModel HolofunkModel { get { return m_parent; } }

        internal int PlayerIndex { get { return m_playerIndex; } }
        internal int AsioChannel { get { return m_asioChannel; } }

        internal PlayerSceneGraph PlayerSceneGraph { get { return m_playerSceneGraph; } }

        internal Color PlayerColor { get { return PlayerIndex == 0 ? Color.LightBlue : Color.LightGreen; } }

        // The sound effect parameters currently being applied to the microphone.
        internal ParameterMap MicrophoneParameters { get { return m_microphoneParameters; } }

        /// <summary>Is the microphone selected?</summary>
        internal bool MicrophoneSelected { get { return m_microphoneSelected; } set { m_microphoneSelected = value; } }

        internal PlayerHandModel LeftHandModel { get { return m_leftHandModel; } }
        internal PlayerHandModel RightHandModel { get { return m_rightHandModel; } }

        internal bool IsRecordingWAV { get { return m_parent.IsRecordingWAV; } }

        #endregion

        public void StartRecordingWAV()
        {
            m_parent.StartRecordingWAV();
        }

        public void StopRecordingWAV()
        {
            m_parent.StopRecordingWAV();
        }

        public void RemoveLoopie(Loopie loopie)
        {
            m_parent.RemoveLoopie(loopie);
        }

        internal void AddTimedRecorder(TimedRecorder<Sample, byte> recorder)
        {
            lock (m_recorders) {
                m_recorders.Add(recorder);
            }
        }

        internal void RemoveTimedRecorder(TimedRecorder<Sample, byte> recorder)
        {
            lock (m_recorders) {
                m_recorders.Remove(recorder);
            }
        }

        public void OnLeftHand(HandPose transition)
        {
            Moment now = HolofunkModel.Clock.Now;
            lock (m_leftHandModel.StateMachine) {
                Spam.Model.WriteLine("PlayerModel.OnLeftHand: received transition " + transition);
                m_leftHandModel.StateMachine.OnNext(LoopieEvent.FromHandPose(transition), now);
            }
        }

        public void OnRightHand(HandPose transition)
        {
            Moment now = HolofunkModel.Clock.Now;
            lock (m_rightHandModel.StateMachine) {
                Spam.Model.WriteLine("PlayerModel.OnRightHand: received transition " + transition);
                m_rightHandModel.StateMachine.OnNext(LoopieEvent.FromHandPose(transition), now);
            }
        }

        public void OnLeftArm(ArmPose transition)
        {
            Moment now = HolofunkModel.Clock.Now;
            // fire Other* event at the *other* hand model
            lock (m_rightHandModel.StateMachine) {
                Spam.Model.WriteLine("PlayerModel.OnLeftArm: received transition " + transition);
                m_rightHandModel.StateMachine.OnNext(LoopieEvent.FromArmPose(transition), now);
            }
        }

        public void OnRightArm(ArmPose transition)
        {
            Moment now = HolofunkModel.Clock.Now;
            lock (m_leftHandModel.StateMachine) {
                Spam.Model.WriteLine("PlayerModel.OnRightArm: received transition " + transition);
                m_leftHandModel.StateMachine.OnNext(LoopieEvent.FromArmPose(transition), now);
            }
        }

        public void BodyFrameUpdate(HolofunKinect kinect)
        {
            // thread-safe operation: snapshot current sample time
            Moment now = HolofunkModel.Clock.Now;

            // get the head position
            Vector2 headPosition = kinect.GetJointViewportPosition(PlayerIndex, Microsoft.Kinect.JointType.Head);

            // need to find a rectangle centered on headPosition that doesn't cross the viewport edge
            Rectangle rect = new Rectangle(
                (int)headPosition.X - MagicNumbers.HeadCaptureSize / 2,
                (int)headPosition.Y - MagicNumbers.HeadCaptureSize / 2,
                MagicNumbers.HeadCaptureSize,
                MagicNumbers.HeadCaptureSize);

            if (rect.X < 0) {
                rect.Offset(new Point(-rect.X, 0));
            }
            if (rect.Y < 0) {
                rect.Offset(new Point(0, -rect.Y));
            }
            if (rect.Right > kinect.ViewportSize.X) {
                rect.Offset(new Point((int)kinect.ViewportSize.X - rect.Right, 0));
            }
            if (rect.Bottom > kinect.ViewportSize.Y) {
                rect.Offset(new Point(0, (int)kinect.ViewportSize.Y - rect.Bottom));
            }

            int startOffset = rect.X * 4;
            if (rect.Y > 0) {
                startOffset += (rect.Y - 1) * kinect.DisplayTexture.Width * 4;
            }

            // if we are recording, get the head position
            // this method and the regular hand event handling are both called on Kinect thread,
            // so no need to worry about races on this field
            lock (m_recorders) {
                if (m_recorders.Count > 0) {
                    lock (kinect.DisplayTextureBuffer) {
                        // loop from end to start, so we can remove recorders in mid-iteration
                        for (int i = m_recorders.Count - 1; i >= 0; i--) {
                            bool done = m_recorders[i].Record(
                                now.Time,
                                kinect.DisplayTextureBuffer,
                                startOffset,
                                rect.Width * 4, // * 4 because RGBA
                                kinect.DisplayTexture.Width * 4,
                                rect.Height);

                            if (done) {
                                m_recorders.RemoveAt(i);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Update the state as appropriate for "loopie mode" (the default, in which you are
        /// selecting and recording loopies).</summary>
        public override void GameUpdate(Moment now)
        {
            // Push the current microphone parameters to BASS.
            HolofunkModel.Bass.UpdateMicrophoneParameters(m_asioChannel, m_microphoneParameters, now);

            // We update the model of the state machine of each hand's model.
            // Each hand's state machine may have some layered model in place right now.
            lock (m_leftHandModel.StateMachine) { // [GameThread] ensure KinectThread doesn't concurrently mung the machine
                m_leftHandModel.StateMachine.GameUpdate(now);
            }
            lock (m_rightHandModel.StateMachine) { // [GameThread] ensure KinectThread doesn't concurrently mung the machine
                m_rightHandModel.StateMachine.GameUpdate(now);
            }

            PlayerSceneGraph.Update(this, m_parent.Kinect, now);
        }

        void CheckBeatEvent(Moment now, HolofunkMachine machine)
        {
            lock (machine) {
                Duration<Sample> sinceLastTransition = now.Time - machine.LastTransitionMoment.Time;
                if (sinceLastTransition > (long)HolofunkModel.Clock.ContinuousBeatDuration) {
                    machine.OnNext(LoopieEvent.Beat, now);
                    // TODO: suspicious rounding here... this is probably liable to lose fractional beats...
                    // not clear how much beat events will wind up being used though.
                    machine.LastTransitionMoment = machine.LastTransitionMoment.Clock.Time(machine.LastTransitionMoment.Time + (long)HolofunkModel.Clock.ContinuousBeatDuration);
                }
            }
        }

        /// <summary>
        /// Start recording a new loopie; return null if we have no free streams.
        /// </summary>
        internal Loopie StartRecording(Moment now, Transform handPosition, bool isRight)
        {
            Loopie ret = m_parent.StartRecordingNewLoopie(now, m_playerIndex, isRight, AsioChannel, handPosition);
            return ret;
        }
    }
}
