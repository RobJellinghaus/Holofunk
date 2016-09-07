////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
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
    // The two possible views of the Holofunk space: the primary view
    // (for the performer), and the secondary view (for the audience).
    public enum HolofunkView
    {
        Primary,
        Secondary
    }

    /// <summary>The possible ways to render oneself in the secondary view.</summary>
    /// <remarks>Only one of the first four options may be selected.</remarks>
    [Flags]
    public enum SecondaryViewOption
    {
        SameOrientation  = 0x0, // just like primary

        PositionMirrored = 0x1, // position only mirrored, textures rendered normally

        TextureMirrored  = 0x2, // texture mirrored / flipped

        SecondTexture    = 0x4, // different texture in secondary view

        Hidden           = 0x8, // rendered transparently in secondary view
    }

    /// <summary>A hierarchy of ASceneNodes, supporting render and pick operations.</summary>
    /// <remarks>Note that a SceneGraph contains only spatial state; it describes an instantaneous
    /// snapshot of a scene (specifically, *the* instantaneous snapshot next to be rendered).
    /// 
    /// TBD exactly how we "layer animation behavior" onto a scene graph as such....</remarks>
    public class SceneGraph
    {
        AParentSceneNode m_rootNode;

        public SceneGraph()
        {
        }

        public AParentSceneNode RootNode
        {
            get { return m_rootNode; }
            set { m_rootNode = value; }
        }

        public void Render(Moment now, GraphicsDevice graphicsDevice, ISpriteBatch spriteBatch, TextureContent content, HolofunkView view)
        {
            if (RootNode != null) {
                RootNode.Render(now, graphicsDevice, spriteBatch, content, view, Transform.Identity, 0);
            }

            Spam.Graphics.WriteLine("End Render");           
        }
    }
}
