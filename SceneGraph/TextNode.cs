////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Text;

namespace Holofunk.SceneGraphs
{
    public enum Alignment
    {
        TopLeft, // Top left corner of text is positioned at transform location
        TopRight, // Top right corner at transform location
        Centered, // Center of text (both horizontally and vertically) is positioned at transform location
    }

    /// <summary>Simple node class containing a square texture (e.g. a sprite).</summary>
    /// <remarks>This always renders at a fixed position on screen; it has no real Transform of its own.
    /// (In other words, it is a hack.)</remarks>
    public class TextNode : ASceneNode
    {
        StringBuilder m_text;

        Alignment m_alignment = Alignment.TopLeft;
        Color m_color = Color.White;
        float m_rotation;

        public TextNode(AParentSceneNode parent, string label)
            : base(parent, Transform.Identity, label)
        {
            m_text = new StringBuilder();
        }

        /// <summary>The color to tint when rendering.</summary>
        public Color Color
        {
            get { return m_color; }
            set { m_color = value; }
        }

        public Alignment Alignment { get { return m_alignment; } set { m_alignment = value; } }

        public float Rotation { get { return m_rotation; } set { m_rotation = value; } }

        public StringBuilder Text
        {
            get { return m_text; }
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
            Transform combinedTransform = parentTransform.CombineWith(LocalTransform);

            spriteBatch.Begin();

            Vector2 textSize = content.SpriteFont.MeasureString(m_text);
            Vector2 origin = Vector2.Zero;

            if (Alignment == Alignment.Centered) {
                origin = textSize / 2;
            }
            else if (Alignment == Alignment.TopRight) {
                origin = new Vector2(textSize.X, 0);
            }
            
            spriteBatch.DrawString(
                content.SpriteFont, 
                m_text, 
                combinedTransform.Translation, 
                Color, 
                Rotation,
                origin, 
                combinedTransform.Scale.X, 
                SpriteEffects.None, 
                0);

            spriteBatch.End();
        }
    }
}
