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
    /// <summary>Simple node class containing an assignable square texture (e.g. a sprite).</summary>
    /// <remarks>The extent of the texture is considered to be its entire area, regardless of transparency.
    /// 
    /// The origin of the texture is the texture's center.</remarks>
    public class SpriteNode : ASceneNode
    {
        Texture2D m_texture;
        Texture2D m_secondaryTexture;
        Vector2 m_origin;
        Color m_color = Color.White;
        Option<Color> m_secondaryColor = Option<Color>.None;

        public SpriteNode(AParentSceneNode parent, string label, Texture2D texture)
            : base(parent, Transform.Identity, label)
        {
            m_texture = texture;
        }

        /// <summary>The sprite texture.</summary>
        public Texture2D Texture
        {
            get { return m_texture; }
            set { m_texture = value; }
        }

        /// <summary>The secondary sprite texture, if any.</summary>
        public Texture2D SecondaryTexture
        {
            get { return m_secondaryTexture; }
            set { m_secondaryTexture = value; }
        }

        /// <summary>The color to tint when rendering.</summary>
        public Color Color
        {
            get { return m_color; }
            set { m_color = value; }
        }

        /// <summary>The color to tint when rendering in secondary view.</summary>
        /// <remarks>If None, use Color.</remarks>
        public Option<Color> SecondaryColor
        {
            get { return m_secondaryColor; }
            set { m_secondaryColor = value; }
        }


        /// <summary>The origin of the sprite texture; 0,0 is upper left, 1,1 is lower right.</summary>
        public Vector2 Origin
        {
            get 
            { 
                return m_origin; 
            }
            set 
            {
                HoloDebug.Assert(value.X >= 0f && value.X <= 1f);
                HoloDebug.Assert(value.Y >= 0f && value.Y <= 1f);
                m_origin = value; 
            }
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
            // no texture = no-op
            if (m_texture == null) {
                return;
            }

            int left = -(int)((float)m_texture.Width * m_origin.X);
            int top = -(int)((float)m_texture.Height * m_origin.Y);
            Rectangle rect = new Rectangle(left, top, m_texture.Width, m_texture.Height);

            Transform combinedTransform = parentTransform.CombineWith(LocalTransform);

            Rectangle transformedRect = rect * combinedTransform;

            Spam.Graphics.WriteLine(new string(' ', depth * 4) + Label + ": parentTransform " + parentTransform + ", localTransform " + LocalTransform + ", combinedTransform " + combinedTransform + "; start rect " + rect.FormatToString() + "; transformedRect " + transformedRect.FormatToString());

            Texture2D texture = m_texture;
            SpriteEffects effects = SpriteEffects.None;

            if (view == HolofunkView.Secondary) {
                if ((SecondaryViewOption & SecondaryViewOption.TextureMirrored) != 0) {
                    effects = SpriteEffects.FlipHorizontally;
                }
                
                if ((SecondaryViewOption & SecondaryViewOption.PositionMirrored) != 0) {
                    // need to flip transformedRect around center of viewport
                    int newLeft = (int)spriteBatch.Viewport.X - transformedRect.Right;

                    transformedRect = new Rectangle(newLeft, transformedRect.Y, transformedRect.Width, transformedRect.Height);
                }

                if ((SecondaryViewOption & SecondaryViewOption.SecondTexture) != 0) {
                    HoloDebug.Assert(m_secondaryTexture != null);
                    texture = m_secondaryTexture;
                }
            }

            Color color = m_color;
            if (view == HolofunkView.Secondary && m_secondaryColor.HasValue) {
                color = m_secondaryColor.Value;
            }

            // Use NonPremultiplied, as our sprite textures are not premultiplied
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied);

            spriteBatch.Draw(
                texture,
                transformedRect,
                null,
                color,
                0,
                m_origin,
                effects,
                0);

            spriteBatch.End();
        }
    }
}
