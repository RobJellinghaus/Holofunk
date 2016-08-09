////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
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

    class PlayerHandModel : Model
    {
        #region Fields 

        readonly PlayerModel m_parent;
        readonly bool m_isRightHand;

        /// <summary>The state machine for this hand.</summary>
        readonly HolofunkMachine m_stateMachine;

        /// <summary>
        /// If we are recording, and we are "holding" a loopie, this is that loopie.
        /// </summary>
        Option<Loopie> m_heldLoopie;

        /// <summary>
        /// The single loopie that is closest to the hand; None if we have a held Loopie.
        /// </summary>
        Option<Loopie> m_closestLoopie;
        
        /// <summary>What effect are we applying to loopies we touch?</summary>
        // Default: nada.
        // This must be idempotent since right now we apply it like mad on every update!
        Action<Loopie> m_loopieTouchEffect = loopie => { };

        /// <summary>The loopies currently touched by this player.</summary>
        readonly List<Loopie> m_touchedLoopies = new List<Loopie>();

        /// <summary>Index of the current effect preset.</summary>
        int m_effectPresetIndex;

        #endregion Fields

        public PlayerHandModel(PlayerModel parent, bool isRight)
        {
            m_parent = parent;
            m_isRightHand = isRight;
            m_stateMachine = new HolofunkMachine(new LoopieEvent(), LoopieStateMachine.Instance, this);
        }

        #region Properties


        /// <summary>The effect applied to loopies being touched.</summary>
        internal Action<Loopie> LoopieTouchEffect { get { return m_loopieTouchEffect; } set { m_loopieTouchEffect = value; } }

        /// <summary>Get the Loopie that is closest to the Wii hand and within grabbing distance.</summary>
        internal Option<Loopie> ClosestLoopie
        {
            get
            {
                return m_closestLoopie;
            }
        }

        internal List<Loopie> TouchedLoopies
        {
            get
            {
                return m_touchedLoopies;
            }
        }

        internal HolofunkMachine StateMachine { get { return m_stateMachine; } }

        internal PlayerModel PlayerModel { get { return m_parent; } }
        internal HolofunkModel HolofunkModel { get { return m_parent.HolofunkModel; } }

        internal bool IsRightHand { get { return m_isRightHand; } }

        internal PlayerHandSceneGraph SceneGraph { get { return m_isRightHand ? m_parent.PlayerSceneGraph.RightHandSceneGraph : m_parent.PlayerSceneGraph.LeftHandSceneGraph; } }

        internal Vector2 HandPosition { get { return SceneGraph.HandPosition; } }

        internal int EffectPresetIndex { get { return m_effectPresetIndex; } set { m_effectPresetIndex = value; } }

        internal ArmPose OtherArmPose { get { return HolofunkModel.Kinect.GetArmPose(PlayerModel.PlayerIndex, IsRightHand ? Side.Left : Side.Right); } }

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

        internal void UpdateTouchedLoopies(List<Loopie> loopies, Vector2 handPosition, float handDiameter, Color playerColor, Vector2 screenSize)
        {
            HoloDebug.Assert(m_touchedLoopies.Count == 0);
            HoloDebug.Assert(!m_closestLoopie.HasValue);
            HoloDebug.Assert(!m_closestLoopie.HasValue);

            // Reset state variables
            m_closestLoopie = Option<Loopie>.None;

            if (loopies.Count == 0) {
                return;
            }

            // Transform handPosition = PlayerSceneGraph.WiiHandNode.LocalTransform;

            Loopie closest = null;
            double minDistSquared = double.MaxValue;
            double handSize = handDiameter / 1.5; // hand radius too small, hand diameter too large
            double handSizeSquared = handSize * handSize;

            foreach (Loopie loopie in loopies) {
                Transform loopiePosition = loopie.Transform;
                double xDist = loopiePosition.Translation.X - handPosition.X;
                double yDist = loopiePosition.Translation.Y - handPosition.Y;
                double distSquared = xDist * xDist + yDist * yDist;
                if (distSquared < handSizeSquared) {
                    loopie.Touched = true;
                    loopie.TouchedColor = playerColor;
                    m_touchedLoopies.Add(loopie);
                    if (distSquared < minDistSquared) {
                        closest = loopie;
                        minDistSquared = distSquared;
                    }
                }
            }

            if (closest != null) {
                m_closestLoopie = new Option<Loopie>(closest);
            }

        }

        /// <summary>Recalculate the loopies touched by this player.</summary>
        // Call this once after a frame change, before querying GrabbedLoopie or TouchedLoopies;
        // otherwise their cached values will be reused without checking for new positions of
        // anything.
        internal void InvalidateTouchedLoopies()
        {
            m_closestLoopie = Option<Loopie>.None;

            foreach (Loopie loopie in m_touchedLoopies) {
                loopie.Touched = false;
                loopie.TouchedColor = new Color(0);
            }

            m_touchedLoopies.Clear();
        }

        /// <summary>
        /// Update on [GameThread].
        /// </summary>
        /// <param name="now"></param>
        public override void GameUpdate(Moment now)
        {
            InvalidateTouchedLoopies();

            UpdateTouchedLoopies(HolofunkModel.Loopies,
                HandPosition,
                SceneGraph.HandDiameter * MagicNumbers.LoopieScale,
                PlayerModel.PlayerColor,
                HolofunkModel.Kinect.ViewportSize);

            List<Loopie> touched = TouchedLoopies;
            foreach (Loopie loopie in touched) {
                LoopieTouchEffect(loopie);
                loopie.Touched = true;
            }

            lock (this) {
                if (m_heldLoopie.HasValue) {
                    m_heldLoopie.Value.Transform = new Transform(HandPosition);
                    m_heldLoopie.Value.GameUpdate(now);
                }
            }
        }

        internal void UpdateFromChildState(Moment now)
        {
            foreach (Loopie loopie in TouchedLoopies) {
                // Loopies that are still being recorded can't be TouchedLoopies.
                // So it is safe to assume that loopie.Track is non-null here.
                loopie.Track.UpdateEffects(now);
            }
        }

        /// <summary>Initialize the destination map with the average of the parameter values of all touched
        /// loopies (and the microphone if applicable); then have all touched loopies (and the microphone if
        /// applicable) share those newly initialized parameters.</summary>
        internal void UpdateParameterMapFromTouchedLoopieValues(ParameterMap dest)
        {
            // initialize the parameters from the average of the values in the loopies & mike
            int count = TouchedLoopies.Count + 1;

            IEnumerable<ParameterMap> touchedParameterMaps =
                TouchedLoopies.Select(loopie => loopie.Track.Parameters);

            /* MIKEFFECTS:
            if (MicrophoneSelected) {
                touchedParameterMaps = touchedParameterMaps.Concat(new ParameterMap[] { MicrophoneParameters });
            }
             */
            
            dest.SetFromAverage(HolofunkModel.Clock.Time(0), touchedParameterMaps);
        }

        /// <summary>
        /// Start recording a new loopie; the result will be put into m_heldLoopie.
        /// </summary>
        /// <remarks>
        /// [KinectThread]
        /// </remarks>
        internal void StartRecording(Moment now)
        {
            lock (this) { // [KinectThread] to avoid torn writes to m_heldLoopie visible from game thread
                HoloDebug.Assert(!m_heldLoopie.HasValue);

                // Note that this may return null if we have no free streams and can't start recording.
                Loopie newLoopie = m_parent.StartRecording(now, new Transform(HandPosition), IsRightHand);
                m_heldLoopie = newLoopie ?? Option<Loopie>.None;
            }
        }

        /// <summary>Stop recording at end of proper number of beats.</summary>
        /// <remarks>
        /// [KinectThread]
        /// </remarks>
        internal void StopRecordingAtCurrentBeat(Moment now)
        {
            lock (this) { // [KinectThread] to avoid torn writes to m_heldLoopie visible from game thread
                // May be no held loopie if we weren't able to start recording due to lack of free streams
                if (m_heldLoopie.HasValue) {
                    lock (m_heldLoopie.Value) { // [KinectThread] to ensure atomic m_heldLoopie.StopRecordingAtNextBeat
                        m_heldLoopie.Value.StopRecordingAtNextBeat(now, PlayerModel.MicrophoneParameters);
                        m_heldLoopie = Option<Loopie>.None;
                    }
                }
            }
        }

        internal void ShareLoopParameters(ParameterMap parameters)
        {
            foreach (Loopie loopie in TouchedLoopies) {
                loopie.Track.Parameters.ShareAll(parameters);
                // TODO: figure out how to handle these moments outside of time... Time(0) is terrible
                loopie.Track.UpdateEffects(HolofunkModel.Clock.Time(0));
            }
        }
    }
}
