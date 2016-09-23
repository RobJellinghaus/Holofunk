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
using System.Text;

namespace Holofunk.SceneGraphs
{
    /// <summary>Wrapper for a sprite batch that scales coordinates (and scale factors).</summary>
    public class SpriteBatchWrapper : ISpriteBatch
    {
        readonly SpriteBatch m_spriteBatch;
        readonly Vector2 m_viewport;
        readonly Transform m_transform;

        public SpriteBatchWrapper(SpriteBatch spriteBatch, Vector2 viewport, Transform transform)
        {
            m_spriteBatch = spriteBatch;
            m_viewport = viewport;
            m_transform = transform;
        }

        public Vector2 Viewport { get { return m_viewport; } }

        public void Begin()
        {
            m_spriteBatch.Begin();
        }

        public void Begin(SpriteSortMode spriteSortMode, BlendState blendState)
        {
            m_spriteBatch.Begin(spriteSortMode, blendState);
        }

        public void Draw(
            SharpDX.Direct3D11.ShaderResourceView texture,
            Vector2 position,
            Rectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            Vector2 scale,
            SpriteEffects spriteEffects,
            float layerDepth)
        {
            m_spriteBatch.Draw(
                texture,
                position * m_transform.Scale + m_transform.Translation,
                sourceRectangle,
                color,
                rotation,
                origin,
                scale * m_transform.Scale,
                spriteEffects,
                layerDepth);
        }

        public void Draw(
            SharpDX.Direct3D11.ShaderResourceView texture,
            Rectangle destRectangle,
            Rectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            SpriteEffects spriteEffects,
            float layerDepth)
        {
            m_spriteBatch.Draw(
                texture,
                new RectangleF(
                    (int)(destRectangle.X * m_transform.Scale.X + m_transform.Translation.X),
                    (int)(destRectangle.Y * m_transform.Scale.Y + m_transform.Translation.Y),
                    (int)(destRectangle.Width * m_transform.Scale.X),
                    (int)(destRectangle.Height * m_transform.Scale.Y)),
                sourceRectangle,
                color,
                rotation,
                origin,
                spriteEffects,
                layerDepth);
        }

        public void DrawString(
            SpriteFont spriteFont,
            StringBuilder text,
            Vector2 position,
            Color color,
            float rotation,
            Vector2 origin,
            float scale,
            SpriteEffects spriteEffects,
            float layerDepth)
        {
            m_spriteBatch.DrawString(
                spriteFont,
                text,
                position * m_transform.Scale + m_transform.Translation,
                color,
                rotation,
                origin,
                scale * m_transform.Scale,
                spriteEffects,
                layerDepth);
        }

        public void End()
        {
            m_spriteBatch.End();
        }

        public GraphicsDevice GraphicsDevice { get { return m_spriteBatch.GraphicsDevice; } }
    }
}
