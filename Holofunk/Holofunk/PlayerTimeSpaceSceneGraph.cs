////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;

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
