//////////////////////////////////////////////////////////
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
    /// <summary>The varieties of events in our per-hand Loopie machine.</summary>
    /// <remarks>These events correspond to transitions of hand pose or of other-hand pose.</remarks>
    enum LoopieEventType
    {
        None,       // uninitialized value; to catch inadvertent defaults

        Opened,     // Hand just opened
        Closed,     // Hand just closed
        Pointing,   // Hand started pointing
        Unknown,    // Hand unrecognizable

        OtherNeutral,   // Other hand went into neutral / untracked pose
        OtherChest,     // Other hand went to chest
        OtherMouth,     // Other hand went to mouth
        OtherHead,      // Other hand went to top of / above head

        Beat,       // a timer event fired once per beat
    }

    /// <summary>An event in a Loopie machine.</summary>
    struct LoopieEvent
    {
        readonly LoopieEventType m_type;

        /// <summary>The type of event.</summary>
        internal LoopieEventType Type { get { return m_type; } }

        internal LoopieEvent(LoopieEventType type) { m_type = type; }

        internal static LoopieEvent Opened { get { return new LoopieEvent(LoopieEventType.Opened); } }
        internal static LoopieEvent Closed { get { return new LoopieEvent(LoopieEventType.Closed); } }
        internal static LoopieEvent Pointing { get { return new LoopieEvent(LoopieEventType.Pointing); } }
        internal static LoopieEvent Unknown { get { return new LoopieEvent(LoopieEventType.Unknown); } }
        internal static LoopieEvent OtherNeutral { get { return new LoopieEvent(LoopieEventType.OtherNeutral); } }
        internal static LoopieEvent OtherChest { get { return new LoopieEvent(LoopieEventType.OtherChest); } }
        internal static LoopieEvent OtherMouth { get { return new LoopieEvent(LoopieEventType.OtherMouth); } }
        internal static LoopieEvent OtherHead { get { return new LoopieEvent(LoopieEventType.OtherHead); } }

        internal static LoopieEvent Beat { get { return new LoopieEvent(LoopieEventType.Beat); } }

        public static LoopieEvent FromHandPose(HandPose transition)
        {
            switch (transition) {
                case HandPose.Closed: return Closed;
                case HandPose.Opened: return Opened;
                case HandPose.Pointing: return Pointing;
                case HandPose.Unknown:
                default: return Unknown;
            }
        }

        public static LoopieEvent FromArmPose(ArmPose transition)
        {
            switch (transition) {
                case ArmPose.Unknown: return OtherNeutral;
                case ArmPose.AtChest: return OtherChest;
                case ArmPose.AtMouth: return OtherMouth;
                case ArmPose.OnHead: return OtherHead;
                default: return OtherNeutral;
            }
        }

        public override string ToString()
        {
            return Type.ToString();
        }
    }

    class LoopieEventComparer : IComparer<LoopieEvent>
    {
        internal static readonly LoopieEventComparer Instance = new LoopieEventComparer();

        public int Compare(LoopieEvent x, LoopieEvent y)
        {
            int delta = (int)x.Type - (int)y.Type;
            return delta;
        }
    }
}
