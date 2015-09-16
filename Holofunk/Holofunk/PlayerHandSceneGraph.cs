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
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to one of the player's hands.</summary>
    class PlayerHandSceneGraph : SceneGraph
    {
        #region Fields

        readonly PlayerSceneGraph m_parent;

        /// <summary>
        /// right-handed?
        /// </summary>
        readonly bool m_isRight;

        // The hand group node.
        readonly GroupNode m_handGroup;

        // The hand sprite.
        readonly SpriteNode m_handNode;

        // Stack of hand sprite textures, to enable clean push/pop on enter/exit, without needing mutable capture
        // state in state machine actions (not going to clone real well...).
        readonly Stack<Texture2D> m_handTextureStack = new Stack<Texture2D>();

        /// <summary>A group for the labels so we can reuse them when dragging the effect.</summary>
        readonly GroupNode m_effectLabelGroup;

        /// <summary>The labels for the current effect preset.</summary>
        readonly TextNode[] m_effectLabels;

        /// <summary>Debugging: a label, positioned at the elbow, for that side's arm pose.</summary>
        readonly TextNode m_armPoseLabel;

        /// <summary>The red circle that tracks the current mike signal, and that sticks to this hand
        /// when recording.</summary>
        readonly TrackNode m_handMikeSignal;

        Color m_handMikeSignalColor = new Color(0);

        /// <summary>Is there a moment in the recent past when we showed the effect labels?</summary>
        /// <remarks>If so, we are progressively fading them out and we want to update their color.</remarks>
        Option<Moment> m_effectLabelShownMoment;

        #endregion

        public PlayerHandSceneGraph(PlayerSceneGraph parent, bool isRight)
        {
            m_parent = parent;
            m_isRight = isRight;

            RootNode = new GroupNode(parent.RootNode, Transform.Identity, isRight ? "Left Hand" : "Right Hand");

            m_handGroup = new GroupNode(RootNode, Transform.Identity, isRight ? "Right Group" : "Left Group");

            m_handMikeSignal = new TrackNode(
                m_handGroup,
                new Transform(Vector2.Zero, new Vector2(MagicNumbers.LoopieScale)),
                "MikeSignal",
                parent.Content,
                -1,
                null,
                true,
                () => parent.Audio.LevelRatio(parent.Channel) * 0.5f,
                () => m_handMikeSignalColor,
                () => Color.White,
                // TODO: revive beat meter on current recording
                () => new Duration<Sample>(0),
                () => 0,
                () => m_handMikeSignalColor);

            m_handNode = new SpriteNode(m_handGroup, isRight ? "Right Hand" : "Left Hand", null);
            m_handNode.Origin = new Vector2(0.5f);
            m_handNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored | SecondaryViewOption.TextureMirrored);

            m_effectLabelGroup = new GroupNode(m_handGroup, Transform.Identity, "Effect label group");

            m_effectLabels = MakeEffectLabels(m_effectLabelGroup);

            m_armPoseLabel = new TextNode(RootNode, "Elbow label");
            m_armPoseLabel.Alignment = isRight ? Alignment.TopLeft : Alignment.TopRight;
            m_armPoseLabel.SetSecondaryViewOption(SecondaryViewOption.Hidden);
        }

        #region Properties

        internal TextureContent Content { get { return m_parent.Content; } }

        internal void PushHandTexture(Texture2D value)
        {
            m_handTextureStack.Push(m_handNode.Texture);
            m_handNode.Texture = value;
        }

        internal void PopHandTexture()
        {
            m_handNode.Texture = m_handTextureStack.Pop();
        }
 
        internal Color HandColor { get { return m_handNode.Color; } set { m_handNode.Color = value; } }

        internal Color MikeSignalColor { set { m_handMikeSignalColor = value; } }

        internal Vector2 HandPosition { get { return m_handGroup.LocalTransform.Translation; } }

        internal int TextureRadius { get { return m_parent.TextureRadius; } }

        internal Vector2 ViewportSize { get { return m_parent.ViewportSize; } }

        internal int HandDiameter { get { return m_parent.HandDiameter; } }

        #endregion

        TextNode[] MakeEffectLabels(AParentSceneNode group)
        {
            TextNode[] ret = new TextNode[4];
            ret[0] = MakeEffectLabel(group, m_parent.HandDiameter / 2, 0, 0, false);
            ret[1] = MakeEffectLabel(group, 0, -m_parent.HandDiameter / 2, Math.PI * 3 / 2, false);
            ret[2] = MakeEffectLabel(group, -m_parent.HandDiameter / 2, 0, 0, true);
            ret[3] = MakeEffectLabel(group, 0, m_parent.HandDiameter / 2, Math.PI / 2, false);
            return ret;
        }

        TextNode MakeEffectLabel(AParentSceneNode group, float x, float y, double rotation, bool rightJustified)
        {
            TextNode ret = new TextNode(group, "");
            ret.LocalTransform = new Transform(new Vector2(x, y), new Vector2(MagicNumbers.EffectTextScale));
            ret.Rotation = (float)rotation;
            if (rightJustified) {
                ret.Alignment = Alignment.TopRight;
            }
            return ret;
        }

        internal void ShowEffectLabels(EffectSettings settings, Moment now)
        {
            m_effectLabels[0].Text.Clear();
            m_effectLabels[0].Text.Append(settings.RightLabel);
            m_effectLabels[1].Text.Clear();
            m_effectLabels[1].Text.Append(settings.UpLabel);
            m_effectLabels[2].Text.Clear();
            m_effectLabels[2].Text.Append(settings.LeftLabel);
            m_effectLabels[3].Text.Clear();
            m_effectLabels[3].Text.Append(settings.DownLabel);

            m_effectLabelShownMoment = now;
        }

        internal void HideEffectLabels()
        {
            m_effectLabelShownMoment = Option<Moment>.None;
        }

        internal void Update(PlayerHandModel playerHandModel, HolofunKinect kinect, Moment now)
        {
            // The position adjustment here is purely ad hoc -- the depth image still
            // doesn't line up well with the skeleton-to-depth-mapped hand positions.
            m_handGroup.LocalTransform = new Transform(
                kinect.GetJointViewportPosition(
                    playerHandModel.PlayerModel.PlayerIndex,
                    playerHandModel.IsRightHand ? JointType.HandRight : JointType.HandLeft) + MagicNumbers.ScreenHandAdjustment,
                new Vector2(MagicNumbers.LoopieScale));

            // and make the mike signal update appropriately
            m_handMikeSignal.Update(now, false, playerHandModel.PlayerModel.PlayerColor);

            if (m_effectLabelShownMoment.HasValue) {
                Duration<Sample> elapsed = now.Time - m_effectLabelShownMoment.Value.Time;
                Color color = new Color(0);
                if (elapsed > MagicNumbers.EffectLabelFadeDuration) {
                    m_effectLabelShownMoment = Option<Moment>.None;
                }
                else {
                    float fraction = 1f - ((float)(long)elapsed / MagicNumbers.EffectLabelFadeDuration);
                    color = Alpha(fraction);
                }
                m_effectLabels[0].Color = color;
                m_effectLabels[1].Color = color;
                m_effectLabels[2].Color = color;
                m_effectLabels[3].Color = color;
            }

            // Debugging elbow arm pose label.
            ArmPose armPose = kinect.GetArmPose(m_parent.PlayerIndex, m_isRight ? Side.Right : Side.Left);
            m_armPoseLabel.Text.Clear();
            m_armPoseLabel.Text.Append(
                armPose == ArmPose.AtChest ? "Chest"
                : armPose == ArmPose.AtMouth ? "Mouth"
                : armPose == ArmPose.OnHead ? "Head"
                : "");
            m_armPoseLabel.LocalTransform = new Transform(
                kinect.GetJointViewportPosition(m_parent.PlayerIndex, m_isRight ? JointType.HandRight : JointType.HandLeft)
                    + new Vector2(0, 50),
                new Vector2(0.7f));
        }

        Color Alpha(float fraction)
        {
            Debug.Assert(fraction >= 0);
            Debug.Assert(fraction <= 1);

            byte b = (byte)(255 * fraction);
            return new Color(b, b, b, b);
        }
    }
}
