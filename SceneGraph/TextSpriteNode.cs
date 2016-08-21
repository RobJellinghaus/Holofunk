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
    /// <summary>A node class that displays white text over a semi-transparent black circle, all centered.</summary>
    public class TextSpriteNode : GroupNode
    {
        public static float TextScale = 0.6f;

        TextNode m_textNode;
        SpriteNode m_spriteNode;
        SpriteNode m_highlightSpriteNode;

        bool m_enabled;
        bool m_selected;

        public TextSpriteNode(
            AParentSceneNode parent,
            Transform localTransform,
            string label,
            Texture2D background,
            Texture2D highlight)
            : base(parent, localTransform, label)
        {
            m_spriteNode = new SpriteNode(this, label + "_sprite", background);
            m_spriteNode.Origin = new Vector2(0.5f);

            m_textNode = new TextNode(this, label + "_text");
            m_textNode.Alignment = Alignment.Centered;
            m_textNode.Text.Append(label);
            m_textNode.LocalTransform = new Transform(Vector2.Zero, new Vector2(0.6f));

            m_highlightSpriteNode = new SpriteNode(this, label + "_highlight", highlight);
            m_highlightSpriteNode.Origin = new Vector2(0.5f);

        }

        public bool Enabled { get { return m_enabled; } set { m_enabled = value; } }
        public bool Selected { get { return m_selected; } set { m_selected = value; } }

        public void Update()
        {
            if (Selected) {
                // white text, highlighted
                m_textNode.Color = Color.White;
                m_spriteNode.Color = new Color((byte)0, (byte)0, (byte)0, (byte)128);
                m_highlightSpriteNode.Color = Color.White;
            }
            else if (Enabled) {
                m_textNode.Color = Color.White;
                m_spriteNode.Color = new Color((byte)0, (byte)0, (byte)0, (byte)128);
                m_highlightSpriteNode.Color = new Color(0, 0, 0, 0);
            }
            else {
                // dim text, no highlight
                m_textNode.Color = new Color((byte)0x40, (byte)0x40, (byte)0x40, (byte)0x40);
                m_spriteNode.Color = new Color((byte)0, (byte)0, (byte)0, (byte)128);
                m_highlightSpriteNode.Color = new Color(0, 0, 0, 0);
            }
        }  
    }
}

