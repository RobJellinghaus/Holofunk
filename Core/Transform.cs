////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Xna.Framework;

namespace Holofunk.Core
{
    /// <summary>Simple scale-translate transform.</summary>
    /// <remarks>This represents the scale and translation explicitly, to facilitate the mathematically constrained
    /// amongst us.  </remarks>
    public struct Transform
    {
        readonly Vector2 m_translation;
        readonly Vector2 m_scale;

        public Transform(Vector2 translation, Vector2 scale)
        {
            m_translation = translation;
            m_scale = scale;
        }

        public Transform(Vector2 translation)
            : this(translation, new Vector2(1))
        {
        }

        public Vector2 Translation 
        { 
            get { return m_translation; } 
        }

        public Vector2 Scale 
        { 
            get { return m_scale; } 
        }

        /// <summary>Combine the two transforms by adding their translations and multiplying their scales,
        /// WITHOUT scaling their translations in any way.</summary>
        public Transform CombineWith(Transform other)
        {
            return new Transform(Translation + other.Translation, Scale * other.Scale);
        }

        public static Vector2 operator *(Vector2 vector, Transform xform)
        {
            return vector * xform.Scale + xform.Translation;
        }

        public static Rectangle operator *(Rectangle rect, Transform xform)
        {
            Vector2 upperCorner = new Vector2(rect.Left, rect.Top);
            Vector2 lowerCorner = new Vector2(rect.Right, rect.Bottom);
            Vector2 upperXformCorner = upperCorner * xform;
            Vector2 lowerXformCorner = lowerCorner * xform;
            // xywh constructor!!!
            return new Rectangle(
                (int)upperXformCorner.X, 
                (int)upperXformCorner.Y, 
                (int)lowerXformCorner.X - (int)upperXformCorner.X, 
                (int)lowerXformCorner.Y - (int)upperXformCorner.Y);
        }

        public static Transform operator +(Transform xform, Vector2 offset)
        {
            return new Transform(xform.Translation + offset, xform.Scale);
        }

        public static Transform Identity
        {
            get { return new Transform(Vector2.Zero, Vector2.One); }
        }

        public override string ToString()
        {
            return "[" + m_translation.X + "," + m_translation.Y + "]x(" + m_scale.X + "," + m_scale.Y + ")";
        }
    }
}
