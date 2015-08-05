////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.SceneGraphs
{
    /// <summary>Abstract superclass of all SceneGraph nodes containing children.</summary>
    public abstract class AParentSceneNode : ASceneNode
    {
        // For now, we use a simple linear list.  Eventually perhaps some kind of
        // spatial data structure....
        // m_children must only be enumerated / mutated under lock (this)
        readonly List<ASceneNode> m_children = new List<ASceneNode>();

        /// <summary>A cached copy of m_children used during rendering.</summary>
        /// <remarks>Rendering happens on a separate thread, so changes to the child list can
        /// cause cross-thread interference; this snapshot isolates the render loop from
        /// the game thread in this regard.</remarks>
        readonly List<ASceneNode> m_childSnapshot = new List<ASceneNode>();

        protected AParentSceneNode(AParentSceneNode parent, Transform localTransform, string label) 
            : base(parent, localTransform, label)
        {
        }

        protected List<ASceneNode> Children
        {
            get { return m_children; }
        }

        /// <summary>Attach this child to this parent, returning a function that will provide the
        /// parent's local-to-world matrix for this child on demand.</summary>
        /// <remarks>The function allows the child to obtain its transform matrix without exposing
        /// any details of how the parent maintains or represents it.</remarks>
        internal void AttachChild(ASceneNode child)
        {
            lock (this) {
                m_children.Add(child);
            }
        }

        /// <summary>Remove this child.</summary>
        /// <remarks>O(N) but N is expected to be relatively tiny.</remarks>
        internal void DetachChild(ASceneNode child)
        {
            lock (this) {
                HoloDebug.Assert(m_children.Contains(child));

                m_children.Remove(child);
            }
        }

        /// <summary>Render this AParentSceneNode by rendering all its children.</summary>
        protected override void DoRender(
            Moment now,
            GraphicsDevice graphicsDevice, 
            ISpriteBatch spriteBatch,
            TextureContent content,
            HolofunkView view,
            Transform parentTransform, 
            int depth)
        {
            Transform combinedTransform = parentTransform.CombineWith(LocalTransform);

            if (view == HolofunkView.Secondary && SecondaryViewOption == SecondaryViewOption.PositionMirrored) {
                combinedTransform = new Transform(
                    new Vector2(
                        spriteBatch.Viewport.X - combinedTransform.Translation.X,
                        combinedTransform.Translation.Y),
                    combinedTransform.Scale);
            }

            Spam.Graphics.WriteLine(new string(' ', depth * 4) + Label + ": parentTransform " + parentTransform + ", localTransform " + LocalTransform + ", combinedTransform " + combinedTransform);

            // m_children must only be enumerated / mutated under lock (this)
            lock (this) {
                m_childSnapshot.AddRange(m_children);
            }

            for (int i = 0; i < m_childSnapshot.Count; i++) {
                m_childSnapshot[i].Render(now, graphicsDevice, spriteBatch, content, view, combinedTransform, depth + 1);
            }

            // note that clearing preserves the capacity in the list, so no reallocation on next render
            m_childSnapshot.Clear();
        }
    }
}
