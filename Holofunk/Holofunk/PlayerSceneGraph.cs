////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using System.Diagnostics;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to an individual player.</summary>
    class PlayerSceneGraph : SceneGraph
    {
        #region Fields

        /// <summary>The parent scene graph in which this player participates</summary>
        readonly HolofunkSceneGraph m_parent;

        /// <summary>The recently averaged volume level ratio (for making funny faces).</summary>
        readonly FloatAverager m_averageLevelRatio;

        readonly int m_playerIndex;
        readonly int m_channel;

        readonly PlayerHandSceneGraph m_leftHandSceneGraph;
        readonly PlayerHandSceneGraph m_rightHandSceneGraph;

        readonly GroupNode m_headGroup;

        /// <summary>Head node.</summary>
        readonly SpriteNode m_headNode;

        /// <summary>
        /// Faded mike signal overlaid over the head.
        /// </summary>
        readonly TrackNode m_headMikeSignal;
        
        #endregion

        internal PlayerSceneGraph(HolofunkSceneGraph parent, int playerIndex, int channel)
            : base()
        {
            m_parent = parent;

            m_playerIndex = playerIndex;
            m_channel = channel;

            m_averageLevelRatio = new FloatAverager(15); // don't flicker face changes too fast
            m_averageLevelRatio.Update(0); // make sure it's initially zero

            // Center the textures.
            Vector2 origin = new Vector2(0.5f);

            RootNode = new GroupNode(parent.RootNode, Transform.Identity, "Player #" + playerIndex);

            m_headGroup = new GroupNode(RootNode, Transform.Identity, "Head group");

            m_headNode = new SpriteNode(
                m_headGroup,
                "Head",
                PlayerIndex == 0 ? parent.Content.HollowOneOval : parent.Content.HollowTwoOval);

            m_headNode.Origin = new Vector2(0.5f, 0.5f);
            // semi-transparent heads, hopefully this will make them seem "less interactive"
            m_headNode.Color = new Color(0.7f, 0.7f, 0.7f, 0.7f);
            m_headNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored | SecondaryViewOption.SecondTexture);
            m_headNode.SecondaryTexture = parent.Content.Dot;

            // Make a faded mike signal that sticks to the head.
            m_headMikeSignal = new TrackNode(
                m_headGroup,
                new Transform(Vector2.Zero, new Vector2(MagicNumbers.LoopieScale)),
                "MikeSignal",
                parent.Content,
                -1,
                null, 
                true,
                () => Audio.LevelRatio(Channel),
                () => new Color(63, 0, 0, 63),
                () => Color.White,
                () => 0,
                () => 0,
                () => new Color(127, 0, 0, 127),
                () => 1f);

            m_leftHandSceneGraph = new PlayerHandSceneGraph(this, false);
            m_rightHandSceneGraph = new PlayerHandSceneGraph(this, true);
        }

        #region Properties

        internal int PlayerIndex { get { return m_playerIndex; } }
        internal int Channel { get { return m_channel; } }
        internal TextureContent Content { get { return m_parent.Content; } }
        internal HolofunkBass Audio { get { return m_parent.Audio; } }
        internal Clock Clock { get { return m_parent.Clock; } }
        internal FloatAverager AverageLevelRatio { get { return m_averageLevelRatio; } }

        // TODO: abstract over this; use real world space coordinates instead of screen space calculations everywher
        internal Vector2 ViewportSize { get { return m_parent.ViewportSize; } }
        // TODO: abstract over this; use real world space coordinates instead of screen space calculations everywher
        internal int TextureRadius { get { return m_parent.TextureRadius; } }

        internal Transform HeadTransform { get { return m_headGroup.LocalTransform; } }
        internal int HandDiameter { get { return m_parent.Content.HollowCircle.Width; } }

        internal PlayerHandSceneGraph LeftHandSceneGraph { get { return m_leftHandSceneGraph; } }
        internal PlayerHandSceneGraph RightHandSceneGraph { get { return m_rightHandSceneGraph; } }

        #endregion

        /// <summary>Update the scene's background based on the current beat, and the positions of
        /// the two hands based on the latest data polled from Kinect.</summary>
        /// <remarks>We pass in the current value of "now" to ensure consistent
        /// timing between the PlayerState update and the scene graph udpate.</remarks>
        internal void Update(
            PlayerModel playerState,
            HolofunKinect kinect,
            Moment now)
        {
            m_headGroup.LocalTransform = new Transform(
                kinect.GetJointViewportPosition(PlayerIndex, JointType.Head) + MagicNumbers.ScreenHandAdjustment,
                new Vector2(1f));

            m_headMikeSignal.Update(now, false, playerState.PlayerColor);

            m_leftHandSceneGraph.Update(playerState.LeftHandModel, kinect, now);
            m_rightHandSceneGraph.Update(playerState.RightHandModel, kinect, now);
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
