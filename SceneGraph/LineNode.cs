////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Holofunk.SceneGraphs
{
    /// <summary>Line from point to point.</summary>
    /// <remarks>
    /// The endpoints are relative to the parent transform.
    /// 
    /// The width of the line is set by the parent transform's Y scale.
    /// </remarks>
    public class LineNode : ASceneNode
    {
        Vector2 m_p0, m_p1;

        Color m_color = Color.White;

        public LineNode(AParentSceneNode parent, string label)
            : base(parent, Transform.Identity, label)
        {
        }

        /// <summary>Set the endpoints of the line.</summary>
        public void SetEndpoints(Vector2 p0, Vector2 p1)
        {
            m_p0 = p0;
            m_p1 = p1;
        }

        /// <summary>The color to tint when rendering.</summary>
        public Color Color
        {
            get { return m_color; }
            set { m_color = value; }
        }

        protected override void DoRender(
            Moment now,
            GraphicsDevice graphicsDevice,
            ISpriteBatch spriteBatch,
            TextureContent content,
            HolofunkView view,
            Transform parentTransform,
            int depth)
        {
            bool positionMirrored =
                SecondaryViewOption == SecondaryViewOption.PositionMirrored
                && view == HolofunkView.Secondary;

            Vector2 p0 = m_p0 + parentTransform.Translation;
            Vector2 p1 = m_p1 + parentTransform.Translation;

            if (positionMirrored) {
                p0 = new Vector2(spriteBatch.Viewport.X - p0.X, p0.Y);
                p1 = new Vector2(spriteBatch.Viewport.X - p1.X, p1.Y);
            }

            Vector2 diff = Vector2.Subtract(p1, p0);
            float angleRadians = (float)Math.Atan2(diff.Y, diff.X);
            float length = (float)Math.Sqrt(diff.X * diff.X + diff.Y * diff.Y) / 2;

            // Use NonPremultiplied, as our sprite textures are not premultiplied
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            spriteBatch.Draw(
                content.TinyDot,
                p0,
                null,
                Color,
                angleRadians,
                new Vector2(0f, 1f), // we pivot around the center of the left edge of the 2x2 square
                new Vector2(length, LocalTransform.Scale.Y),
                SpriteEffects.None,
                0);

            spriteBatch.End();            
        }
    }
}
