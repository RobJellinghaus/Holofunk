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
    /// <summary>A node class represents a slider control along some rotation from a center point.</summary>
    /// <remarks>The slider's value is in the interval [0, 1] and is mapped to some Parameter.</remarks>
    public class SliderNode : GroupNode
    {
        /// <summary>The thin line along the slider's whole extent.</summary>
        readonly LineNode m_fullLine;

        /// <summary>The thick line showing the slider's selected extent.</summary>
        readonly LineNode m_sliderLine;

        /// <summary>The text.</summary>
        readonly TextNode m_label;

        /// <summary>The parameter modified by this Slider.</summary>
        readonly Parameter m_parameter;

        readonly float m_rotation;
        readonly int m_screenLength;
        readonly int m_originScreenOffset;

        /// <summary>The endpoints -- m_zeroEnd maps to 0 value, m_oneEnd maps to 1 value.</summary>
        /// <remarks>These are in local coordinates, i.e. relative to the origin of the slider.</remarks>
        readonly Vector2 m_zeroEnd;
        readonly Vector2 m_oneEnd;

        /// <summary>The value, in the interval [0, 1].</summary>
        float m_value;

        /// <summary>Highlight this particular slider?</summary>
        bool m_highlight;

        public SliderNode(
            AParentSceneNode parent,
            Transform localTransform,
            // what parameter does this slider drag?
            Parameter parameter,
            // at what angle is the slider oriented?
            float rotation,
            // how long is the slider in screen space?
            int screenLength,
            // how far is the slider's origin from the transform's translation, in screen space?
            int originScreenOffset,
            // what is the slider's label?
            string label)
            : base(parent, localTransform, label)
        {
            m_parameter = parameter;
            m_rotation = rotation;
            m_screenLength = screenLength;
            m_originScreenOffset = originScreenOffset;

            // calculate the endpoints
            m_zeroEnd = ValueToLocal(0);
            m_oneEnd = ValueToLocal(1);

            double absRotation = Math.IEEERemainder(rotation, Math.PI * 2);
            if (absRotation < 0) {
                absRotation += Math.PI * 2;
            }

            m_label = new TextNode(this, label + "_text");

            bool overPi = absRotation > Math.PI;

            m_label.Alignment = overPi ? Alignment.TopLeft : Alignment.TopRight;
            m_label.Text.Append(label);
            m_label.LocalTransform = new Transform(m_oneEnd, new Vector2(0.7f));
            // seems the line rotation and label rotation go in opposite directions, feh!
            m_label.Rotation = -rotation + (float)(Math.PI / 2) + (overPi ? (float)Math.PI : 0); 

            m_fullLine = new LineNode(this, "fullLine");
            m_fullLine.SetEndpoints(m_zeroEnd, m_oneEnd);

            m_sliderLine = new LineNode(this, "sliderLine");
            m_sliderLine.LocalTransform = new Transform(Vector2.Zero, new Vector2(3f)); // 3-pixel wide line

            Value = m_parameter[0];
        }

        internal float Value
        {
            get { return m_value; }
            set { m_value = value; }
        }

        internal bool Highlight
        {
            get { return m_highlight; }
            set { m_highlight = value; UpdateHighlight(); }
        }

        void UpdateHighlight()
        {
            m_fullLine.Color = m_highlight ? Color.White : new Color(0x20, 0x20, 0x20, 0x20);
            m_sliderLine.Color = m_highlight ? Color.White : new Color(0x20, 0x20, 0x20, 0x20);
            m_label.Color = m_highlight ? Color.White : new Color(0x20, 0x20, 0x20, 0x20);
        }

        internal void UpdateValue()
        {
            Value = m_parameter[0];
        }

        internal void Drag(Moment now, Vector2 userPosition)
        {
            float distanceSquared;
            float value = ScreenToValue(userPosition, out distanceSquared);
            Value = Math.Max(0, Math.Min(1, value));
            m_parameter[now.Time] = Value;
        }

        internal void Update(Moment now)
        {
            float interpolatedBaseValue = 
                (m_parameter.Description.Base - m_parameter.Description.Min) 
                / (m_parameter.Description.Max - m_parameter.Description.Min);

            float value = Value;
            if (Math.Abs(value - interpolatedBaseValue) < 0.02f) {
                // set some minimum value so default values are still visible on the slider
                value = interpolatedBaseValue + 0.02f;
            }

            m_sliderLine.SetEndpoints(ValueToLocal(interpolatedBaseValue), ValueToLocal(value));
        }

        static Vector2 Line(float angle, int length)
        {
            return new Vector2((float)(length * Math.Sin(angle)),
                (float)(length * Math.Cos(angle)));
        }

        /// <summary>Convert a value (in our [0, 1] value interval) to a local-space point.</summary>
        internal Vector2 ValueToLocal(float value)
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= 1);

            return Line(m_rotation, m_originScreenOffset)
                + value * Line(m_rotation, m_screenLength);
        }

        internal float ScreenToValue(Vector2 userLocalPoint, out float distanceToLineSquared)
        {
            // TODO: get real WorldToLocal, this one is crap
            userLocalPoint -= Parent.LocalTransform.Translation;

            Vector2 zeroToUser = userLocalPoint - m_zeroEnd;
            Vector2 line = m_oneEnd - m_zeroEnd;
            Vector2 normalized = line;
            normalized.Normalize();

            float length = line.Length();

            // The non-normalized dot product of the normalized direction and the vector to the user's point.
            float dotProduct = Vector2.Dot(normalized, zeroToUser);

            if (dotProduct < 0) {
                distanceToLineSquared = (userLocalPoint - m_zeroEnd).LengthSquared();
            }
            else if (dotProduct > length) {
                distanceToLineSquared = (userLocalPoint - m_oneEnd).LengthSquared();
            }
            else {
                Vector2 projectedPoint = m_zeroEnd + normalized * dotProduct;
                Vector2 projectedPointToUserPoint = userLocalPoint - projectedPoint;

                distanceToLineSquared = projectedPointToUserPoint.LengthSquared();
            }

            // return the normalized value
            return dotProduct / length;
        }
    }
}

