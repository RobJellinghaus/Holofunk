////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.SceneGraphs
{
    /// <summary>A simple container node rendering children sequentially.</summary>
    public class GroupNode : AParentSceneNode
    {
        public GroupNode(AParentSceneNode parent, Transform localTransform, string label)
            : base(parent, localTransform, label)
        {
        }
    }
}
