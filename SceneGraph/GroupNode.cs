////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;

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
