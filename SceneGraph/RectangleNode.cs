////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Xna.Framework;

namespace Holofunk.SceneGraphs
{
    /// <summary>Rectangle.</summary>
    public class RectangleNode : AParentSceneNode
    {
        LineNode m_top;
        LineNode m_left;
        LineNode m_right;
        LineNode m_bottom;

        public RectangleNode(AParentSceneNode parent, string label)
            : base(parent, Transform.Identity, label)
        {
            m_top = new LineNode(this, label + " top line");
            m_left = new LineNode(this, label + " left line");
            m_right = new LineNode(this, label + " right line");
            m_bottom = new LineNode(this, label + " bottom line");
        }

        /// <summary>Set the endpoints of the line.</summary>
        public void SetCorners(Vector2 p0, Vector2 p1)
        {
            m_top.SetEndpoints(p0, new Vector2(p1.X, p0.Y));
            m_left.SetEndpoints(p0, new Vector2(p0.X, p1.Y));
            m_right.SetEndpoints(p1, new Vector2(p1.X, p0.Y));
            m_bottom.SetEndpoints(p1, new Vector2(p0.X, p1.Y));
        }

        /// <summary>The color to tint when rendering.</summary>
        public Color Color
        {
            get 
            { 
                return m_top.Color; 
            }
            set 
            {
                m_top.Color = value;
                m_left.Color = value;
                m_right.Color = value;
                m_bottom.Color = value;
            }
        }
    }
}
