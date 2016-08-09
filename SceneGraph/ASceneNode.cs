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
    /// <summary>Abstract superclass of all SceneGraph nodes.</summary>
    /// <remarks>Defines common operations such as obtaining the local transform, rendering,
    /// and picking.
    /// 
    /// Heavily based on _Essential Mathematics for Games and Interactive Applications:
    /// A Programmer's Guide_, James M. Van Verth and Lars M. Bishop, 2004.</remarks>
    public abstract class ASceneNode
    {
        /// <summary>The parent SceneNode, if any.</summary>
        /// <remarks>The root has no parent, and disconnected subtrees may have no parent.</remarks>
        AParentSceneNode m_parent;

        /// <summary>Local transformation owned by this node, used for calculating parent-visible coordinates.</summary>
        /// <remarks>This transform applies to the bounding box exposed to the parent.</remarks>
        Transform m_localTransform = Transform.Identity;

        /// <summary>Should this be hidden in the secondary view?</summary>
        SecondaryViewOption m_secondaryViewOption;

        /// <summary>Diagnostic label, very helpful in examining scene graph dumps.</summary>
        readonly string m_label;

        public ASceneNode(AParentSceneNode parent, Transform localTransform, string label)
        {
            m_parent = parent;
            m_localTransform = localTransform;
            m_label = label;

            if (parent != null) {
                parent.AttachChild(this);
            }
        }

        public void SetSecondaryViewOption(SecondaryViewOption option)
        {
            m_secondaryViewOption = option;
        }

        internal SecondaryViewOption SecondaryViewOption
        {
            get { return m_secondaryViewOption; }
        }

        internal bool IsHidden(HolofunkView view)
        {
            return ((m_secondaryViewOption & SecondaryViewOption.Hidden) != 0) && view == HolofunkView.Secondary;
        }

        /// <summary>Render this node and all its children.  </summary>
        /// <remarks>This is called on every Holofunk.Tick() cycle.</remarks>
        public void Render(
            Moment now,
            GraphicsDevice graphicsDevice,
            ISpriteBatch spriteBatch,
            TextureContent content,
            HolofunkView view,
            Transform parentTransform,
            int depth)
        {
            if (IsHidden(view)) {
                return;
            }

            DoRender(now, graphicsDevice, spriteBatch, content, view, parentTransform, depth);
        }

        /// <summary>Do the actual work of rendering, if this node is visible in this view.</summary>
        protected abstract void DoRender(
            Moment now,
            GraphicsDevice graphicsDevice,
            ISpriteBatch spriteBatch,
            TextureContent content,
            HolofunkView view,
            Transform parentTransform,
            int depth);

        /// <summary>The local transform managed by this node, that it exposes to the parent.</summary>
        public Transform LocalTransform
        {
            get { return m_localTransform; }
            set { m_localTransform = value; }
        }

        public AParentSceneNode Parent { get { return m_parent; } }

        public string Label { get { return m_label; } }

        public void AttachTo(AParentSceneNode parent)
        {
            if (m_parent != null) {
                m_parent.DetachChild(this);
            }
            m_parent = parent;
            if (parent != null) {
                parent.AttachChild(this);
            }
        }

        /// <summary>
        /// Detach this node from its parent.
        /// </summary>
        public void Detach()
        {
            AttachTo(null);
        }
    }
}
