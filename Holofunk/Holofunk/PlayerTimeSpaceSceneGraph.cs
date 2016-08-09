﻿////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to a menu invoked by an individual player.</summary>
    class PlayerTimeSpaceSceneGraph
    {
        /// <summary>The parent player scene graph.</summary>
        readonly PlayerSceneGraph m_parent;

        internal PlayerTimeSpaceSceneGraph(PlayerSceneGraph parent)
            : base()
        {
            m_parent = parent;
        }

        internal void Update(
            PlayerModel playerState,
            HolofunKinect kinect,
            Moment now)
        {
        }
    }
}
