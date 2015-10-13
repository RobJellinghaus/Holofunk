////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Kinect;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to effect space invoked by an individual player's hand.</summary>
    class PlayerEffectSpaceSceneGraph : SceneGraph
    {
        /// <summary>The parent player hand scene graph.</summary>
        readonly PlayerHandSceneGraph m_parent;

        readonly PlayerEffectSpaceModel m_model;

        readonly SpriteNode m_boundingCircleNode;
        readonly LineNode m_effectKnobLineNode;
        readonly SpriteNode m_effectKnobNode;

        internal PlayerEffectSpaceSceneGraph(PlayerHandSceneGraph parent, PlayerEffectSpaceModel model)
            : base()
        {
            m_parent = parent;
            m_model = model;

            RootNode = new GroupNode(parent.RootNode, Transform.Identity, "EffectSpace");
            // TODO: this should really use the starting drag location as its translation, and the rest of this too
            RootNode.LocalTransform = Transform.Identity;

            // We preallocate our big circle.
            m_boundingCircleNode = new SpriteNode(RootNode, "bounding circle", parent.Content.HollowCircle);
            m_boundingCircleNode.LocalTransform = new Transform(
                Vector2.Zero, new Vector2(MagicNumbers.EffectSpaceBoundingCircleMultiple * (1 / MagicNumbers.EffectSpaceBoundingCircleSize)));
            m_boundingCircleNode.Color = new Color(0, 0, 0, 0);
            m_boundingCircleNode.Origin = new Vector2(0.5f);
            m_boundingCircleNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

            m_effectKnobLineNode = new LineNode(RootNode, "effect line");
            m_effectKnobLineNode.Color = new Color(0, 0, 0, 0);
            m_effectKnobLineNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

            m_effectKnobNode = new SpriteNode(RootNode, "effect knob", parent.Content.FilledCircle);
            m_effectKnobNode.Origin = new Vector2(0.5f);
            m_effectKnobNode.LocalTransform = new Transform(Vector2.Zero, new Vector2(MagicNumbers.EffectSpaceKnobMultiple));
            m_effectKnobNode.Color = new Color(0, 0, 0, 0);
            m_effectKnobNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);
        }

        internal ASceneNode BoundingCircleNode { get { return m_boundingCircleNode; } }

        internal void Update(
            PlayerHandModel playerModel,
            HolofunKinect kinect,
            Moment now)
        {
            m_parent.Update(playerModel, kinect, now);

            bool isDragging = m_model.DragStartLocation.HasValue;
            Color color = isDragging ? Color.White : new Color(0);

            // cut alpha of bounding circle
            m_boundingCircleNode.Color = new Color(color.ToRgba() & 0x60606060);
            m_effectKnobLineNode.Color = color;
            m_effectKnobNode.Color = color;

            if (isDragging) {
                m_boundingCircleNode.LocalTransform = new Transform(m_model.DragStartLocation.Value, m_boundingCircleNode.LocalTransform.Scale);
                m_effectKnobLineNode.SetEndpoints(m_model.DragStartLocation.Value, m_model.CurrentKnobLocation.Value);
                m_effectKnobNode.LocalTransform = new Transform(m_model.CurrentKnobLocation.Value, m_effectKnobNode.LocalTransform.Scale);

                // TODO: this is a bit inefficient and hacky
                m_parent.ShowEffectLabels(PlayerEffectSpaceModel.EffectSettings[playerModel.EffectPresetIndex], now);
            }
        }
    }
}
