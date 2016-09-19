////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Holofunk.Kinect
{
    /// <summary>
    /// The recognized poses of one arm.
    /// </summary>
    public enum ArmPose
    {
        /// <summary>The default: no specifically recognized arm pose.</summary>
        Unknown,
        /// <summary>This arm has hand touching chest.</summary>
        AtChest,
        /// <summary>This arm has hand at mouth.</summary>
        AtMouth,
        /// <summary>This arm has hand on top of head.</summary>
        OnHead,
    }

    /// <summary>
    /// The hand poses we recognize.
    /// </summary>
    public enum HandPose
    {
        /// <summary>The hand just transitioned into Open state.</summary>
        Opened,
        /// <summary>The hand just transitioned into Closed state.</summary>
        Closed,
        /// <summary>The hand just transitioned into Pointing state.</summary>
        Pointing,
        /// <summary>The hand just entered an unknown state.</summary>
        Unknown
    }

    /// <summary>
    /// Which side of the body is an arm on?
    /// </summary>
    public enum Side
    {
        Left,
        Right
    }

    /// <summary>
    /// Tracks the state of a hand, smoothing transient glitches.
    /// </summary>
    public class HandTracker
    {
        /// <summary>Number of hand states.  K4W2DO: why no HandState.Count?</summary>
        const int HandStateCount = 5; 

        /// <summary>Number of samples to track; initialized statically.</summary>
        public static int SampleCount;

        /// <summary>HandState history.</summary>
        readonly Queue<HandState> m_samples = new Queue<HandState>(SampleCount);

        /// <summary>The current HandState, if known (to avoid recalculation).</summary>
        Option<HandState> m_currentHandState;

        /// <summary>Cached array for recomputing sample counts.</summary>
        int[] m_stateCounts = new int[HandStateCount]; 

        public HandTracker() { }

        public void Update(HandState state)
        {
            if (m_samples.Count == SampleCount) {
                m_samples.Dequeue();
            }
            m_samples.Enqueue(state);
            m_currentHandState = Option<HandState>.None;
        }

        public HandState HandState
        {
            get
            {
                if (!m_currentHandState.HasValue) {
                    Array.Clear(m_stateCounts, 0, HandStateCount);

                    // Look at the samples.  Majority state wins.
                    foreach (HandState state in m_samples) {
                        m_stateCounts [(int)state]++;
                    }

                    // Get the largest value.
                    int biggestCount = -1;
                    HandState biggest = HandState.Unknown;
                    for (int i = 0; i < m_stateCounts.Length; i++) {
                        if (m_stateCounts[i] > biggestCount) {
                            // unambiguous new winner
                            biggestCount = m_stateCounts[i];
                            biggest = (HandState)i;
                        }
                        else if (m_stateCounts[i] == biggestCount) {
                            // ambiguous; prefer larger value since Unknown and NotTracked are both losers
                            biggest = (HandState)i;
                        }
                    }

                    m_currentHandState = biggest;
                }
                return m_currentHandState.Value;
            }
        }
    }

    /// <summary>
    /// State relating to one arm of one body.
    /// </summary>
    public class HolofunkArm
    {
        readonly Side m_side;

        /// <summary>Current pose of this arm.</summary>
        ArmPose m_pose;

        /// <summary>
        /// The tracked hand state.
        /// </summary>
        HandTracker m_handTracker = new HandTracker();

        public HolofunkArm(Side whichSide)
        {
            m_side = whichSide;
        }

        public ArmPose ArmPose { get { return m_pose; } }

        /// <summary>Update this arm, using state from the appropriate joints of this body.</summary>
        /// <returns>The HandTransition that just happened, if in fact any did.</returns>
        public Option<HandPose> Update(HolofunkBody holofunkBody, Body body)
        {
            // first calculate the arm pose
            JointType hand = m_side == Side.Left ? JointType.HandLeft : JointType.HandRight;

            Vector2 handToHead = holofunkBody[hand] - holofunkBody[JointType.Head];
            handToHead.X = (float)Math.Round(handToHead.X);
            handToHead.Y = (float)Math.Round(handToHead.Y);

            Vector2 headToUpperChest = holofunkBody[JointType.Head] - holofunkBody[JointType.SpineShoulder];

            Vector2 handToUpperChest = holofunkBody[hand] - holofunkBody[JointType.SpineShoulder];

            Vector2 handToChest = holofunkBody[hand] - holofunkBody[JointType.SpineMid];

            Vector2 headToChest = holofunkBody[JointType.SpineMid] - holofunkBody[JointType.Head];

            /*
            string topLine = null;
            if (m_side == Side.Left) {
                // B4CR: TONS OF GARBAGE: TEMP ONLY
                topLine = "";
                topLine = topLine + "\nhandToHead " + handToHead + ", handToHead.L " + handToHead.Length();
                topLine = topLine + "\nheadToUpperChest " + headToUpperChest + ", headToUpperChest.L " + headToUpperChest.Length();
                topLine = topLine + "\nhandToChest " + handToChest + ", handToChest.L " + handToChest.Length();
                topLine = topLine + "\nchestToUpperChest " + chestToUpperChest + ", chestToUpperChest.L " + chestToUpperChest.Length();
            }
            if (topLine != null) {
                Spam.TopLine2 = topLine;
            }
             */

            if (holofunkBody[hand].X > holofunkBody[JointType.ShoulderLeft].X
                && holofunkBody[hand].X < holofunkBody[JointType.ShoulderRight].X
                && handToHead.Y < 0) {
                m_pose = ArmPose.OnHead;
            }
            else if (handToHead.LengthSquared() < headToUpperChest.LengthSquared()
                && handToHead.LengthSquared() < handToChest.LengthSquared()) {
                m_pose = ArmPose.AtMouth;
            }
            else if (handToUpperChest.LengthSquared() < headToUpperChest.LengthSquared() * 1.5f) {
                m_pose = ArmPose.AtChest;
            }
            else {
                m_pose = ArmPose.Unknown;
            }

            // then calculate the old & new (smoothed) hand states, and fire event if it changed
            HandState handState = m_side == Side.Left ? body.HandLeftState : body.HandRightState;
            HandState priorTrackerState = m_handTracker.HandState;
            m_handTracker.Update(handState);
            HandState postTrackerState = m_handTracker.HandState;
            if (priorTrackerState != postTrackerState) {
                switch (postTrackerState) {
                    case HandState.Open:
                        return HandPose.Opened;
                    case HandState.Closed:
                        return HandPose.Closed;
                    case HandState.Lasso:
                        return HandPose.Pointing;
                    case HandState.NotTracked:
                    case HandState.Unknown:
                        return HandPose.Unknown;
                }
            }

            return Option<HandPose>.None;
        }
    }

    /// <summary>Holds body data.</summary>
    /// <remarks>Supports copying data from a Kinect SkeletonFrame.  This allows us to double-buffer or
    /// otherwise recycle Skeletons to avoid per-frame allocation.</remarks>
    public class HolofunkBody
    {
        /// <summary>Externally injected number of frames by which to average body positions.</summary>
        public static int BodyPositionSampleCount;

        // WARNING: FRAGILE if more joints are added!  Seems to be no JointType.Max....
        const int JointCount = (int)JointType.ThumbRight + 1;

        // Was the corresponding Body actually being tracked?
        bool m_isTracked;

        // What is the tracking ID of this HolofunkBody's body?
        ulong m_trackingId;

        // Array of joint positions, normalized to [0, 1]
        Vector2Averager[] m_joints = new Vector2Averager[JointCount];

        // The Body currently associated with this one, if any (may be none if player not tracked)
        Body m_body;

        // The two arms
        HolofunkArm m_leftArm = new HolofunkArm(Side.Left);
        HolofunkArm m_rightArm = new HolofunkArm(Side.Right);

        public HolofunkBody() 
        {
            for (int i = 0; i < JointCount; i++) {
                // five data points is enough to get some smoothing without toooo much lag
                m_joints[i] = new Vector2Averager(BodyPositionSampleCount);
            }
        }

        /// <summary>
        /// The Kinect-tracked body currently associated with this player's body.
        /// </summary>
        public Body Body
        {
            get { return m_body; }
            set { m_body = value; }
        }

        /// <summary>
        /// Update this HolofunkBody using the Body state just obtained from the sensor.
        /// </summary>
        /// <returns>A tuple of left-hand/right-hand </returns>
        internal void Update(HolofunKinect kinect, 
            Action<HandPose> leftHandAction, 
            Action<ArmPose> leftArmAction,
            Action<HandPose> rightHandAction,
            Action<ArmPose> rightArmAction)
        {
            if (m_body == null) {
                return;
            }

            // Save it as it may switch out from under us in response to some actions (e.g. swapping players)
            Body body = m_body;

            m_isTracked = body.IsTracked && body.HandLeftState != HandState.NotTracked;
            m_trackingId = body.TrackingId;

            if (body.IsTracked) {
                for (int i = 0; i < JointCount; i++) {
                    JointType id = (JointType)i;
                    Vector2 vp = kinect.GetDisplayPosition(body.Joints[id].Position);
                    m_joints[i].Update(vp);
                }
            }

            // Update the arms.
            ArmPose leftArmPose = m_leftArm.ArmPose;
            Option<HandPose> left = m_leftArm.Update(this, body);
            if (left.HasValue) {
                leftHandAction(left.Value);
            }
            ArmPose rightArmPose = m_rightArm.ArmPose;
            Option<HandPose> right = m_rightArm.Update(this, body);
            if (right.HasValue) {
                rightHandAction(right.Value);
            }

            if (leftArmPose != m_leftArm.ArmPose) {
                leftArmAction(m_leftArm.ArmPose);
            }
            if (rightArmPose != m_rightArm.ArmPose) {
                rightArmAction(m_rightArm.ArmPose);
            }
        }

        public bool IsTracked { get { return m_isTracked; } }
        public ulong TrackingId { get { return m_trackingId; } }

        public Vector2 this[JointType id]
        {
            get
            {
                if (m_joints[(int)id].IsEmpty) {
                    return Vector2.Zero;
                }
                else {
                    Vector2 averagePos = m_joints[(int)id].Average;
                    // It appears that the K4W SDK actually delivers joint positions in depth coordinate space,
                    // whereas the beta SDK evidently delivered them as float values in the range [0, 1].
                    // So, leave out the viewport scaling that we evidently no longer need.
                    return averagePos;
                }
            }
        }

        JointType Hand(bool rightHanded)
        {
            return rightHanded ? JointType.HandRight : JointType.HandLeft;
        }

        public ArmPose GetArmPose(Side side)
        {
            return side == Side.Left ? m_leftArm.ArmPose : m_rightArm.ArmPose;
        }

        /// <summary>Is the microphone "close" to the mouth?</summary>
        /// <remarks>Returns None if there is no body.</remarks>
        public bool IsMikeCloseToMouth(int distance, bool rightHanded)
        {
            Vector2 hand = this[Hand(rightHanded)];
            Vector2 head = this[JointType.Head];

            // if distance is less than, oh, say, HandDiameter, then yes
            float distSquared;
            Vector2.DistanceSquared(ref hand, ref head, out distSquared);

            return distSquared < (distance * distance);
        }
    }
}

