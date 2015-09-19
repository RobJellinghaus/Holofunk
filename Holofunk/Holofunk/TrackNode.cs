////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>The visual representation of a Loopie, which may or may not still be being recorded.</summary>
    /// <remarks>This class is parameterized by functions which it polls to get the state of the model
    /// (e.g. track) underlying it.
    /// 
    /// This is actually </remarks>
    class TrackNode : AParentSceneNode
    {
        // same as loopie's ID
        readonly int m_id;

        // The node representing our sound
        readonly SpriteNode m_soundNode;

        // The node representing our video
        readonly SpriteNode m_videoNode;

        // The video stream. May be attached and detached for temporary track nodes.
        // TODO: GET RID OF TEMPORARY TRACK NODES, instead make a real Track that is being recorded into.
        SparseSampleByteStream m_videoStream;
        
        // The node representing our highlight when touched
        readonly SpriteNode m_selectNode;

        // The function we poll for our volume level ratio
        readonly Func<float> m_levelRatioFunc;

        // The function we poll for our circle's color
        readonly Func<Color> m_circleColorFunc;

        // The function we poll for the video color
        readonly Func<Color> m_videoColorFunc;

        // Ditto for the beat color
        readonly Func<Color> m_beatColorFunc;

        // The node representing our beats
        BeatNode m_beatNode;

        // The last video frame Slice we displayed
        Slice<Frame, byte> m_lastVideoFrame;

        readonly static Color[] s_colors = new[] {
            Color.Blue,
            Color.Purple,
            Color.SeaGreen,
            Color.Honeydew,
            Color.DarkOrchid,
            Color.Aqua,
            Color.Magenta,
            Color.SteelBlue,
            Color.Tomato
        };

        readonly static byte[] BlankTextureData = new byte[MagicNumbers.HeadCaptureBytes];

        internal TrackNode(
            AParentSceneNode parent,
            Transform transform,
            string label,
            TextureContent content,
            int id,
            SparseSampleByteStream videoStream,
            bool fillInEveryBeat,
            Func<float> levelRatioFunc,
            Func<Color> circleColorFunc,
            Func<Color> videoColorFunc,
            Func<Duration<Sample>> trackDurationFunc,
            Func<int> initialBeatFunc,
            Func<Color> beatColorFunc)
            : base(parent, transform, label)
        {
            m_id = id;

            m_levelRatioFunc = levelRatioFunc;
            m_circleColorFunc = circleColorFunc;
            m_videoColorFunc = videoColorFunc;
            m_beatColorFunc = beatColorFunc;

            // create this first so it is Z-ordered behind m_soundNode
            m_selectNode = new SpriteNode(this, "TrackHighlight", content.FilledCircle);
            m_selectNode.Color = new Color((byte)0x80, (byte)0x80, (byte)0x80, (byte)0x80);
            m_selectNode.Origin = new Vector2(0.5f);

            m_soundNode = new SpriteNode(this, "TrackSound", content.FilledCircle);
            m_soundNode.Color = Color.Blue;
            m_soundNode.Origin = new Vector2(0.5f);

            m_videoNode = new SpriteNode(this, "Headshot", content.NewDynamicTexture(MagicNumbers.HeadCaptureSize, MagicNumbers.HeadCaptureSize));
            m_videoNode.Origin = new Vector2(0.5f);
            m_videoNode.LocalTransform = new Transform(new Vector2(0), new Vector2(MagicNumbers.HeadRatio));
            m_videoNode.SetSecondaryViewOption(SecondaryViewOption.TextureMirrored);

            m_videoStream = videoStream;

            m_beatNode = new BeatNode(
                this,
                // move it down a bit from the sprite node
                new Transform(new Vector2(0, 75)),
                "TrackBeats",
                fillInEveryBeat,
                MagicNumbers.MeasureCircleScale,
                trackDurationFunc,
                initialBeatFunc,
                beatColorFunc);

            m_beatNode.SetSecondaryViewOption(SecondaryViewOption.Hidden);

            // we always mirror track node position
            SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

            m_lastVideoFrame = default(Slice<Frame, byte>);
        }

        internal int Id { get { return m_id; } }

        internal SparseSampleByteStream VideoStream { get { return m_videoStream; } set { lock (this) { m_videoStream = value; } } }

        internal void Update(Moment now, bool touched, Color playerColor)
        {
            m_soundNode.LocalTransform = new Transform(
                m_soundNode.LocalTransform.Translation,
                new Vector2(MagicNumbers.LoopieScale) * m_levelRatioFunc());
            m_soundNode.Color = m_circleColorFunc();

            m_videoNode.LocalTransform = new Transform(
                m_videoNode.LocalTransform.Translation,
                new Vector2(MagicNumbers.HeadRatio) * (0.6f + m_levelRatioFunc() * 0.15f));
            m_videoNode.Color = touched ? (playerColor * (m_videoColorFunc().A / 0xFF)) : m_videoColorFunc();

            m_selectNode.LocalTransform = new Transform(
                m_soundNode.LocalTransform.Translation,
                new Vector2(MagicNumbers.LoopieScale));
            m_selectNode.Color = touched ? new Color(playerColor.R >> 1, playerColor.G >> 1, playerColor.B >> 1, (byte)0xA0) : new Color(0);

            m_beatNode.Update(now);
        }

        static int s_totalRenders = 0;
        static int s_redundantSetDatas = 0;

        protected override void DoRender(Moment now, SharpDX.Toolkit.Graphics.GraphicsDevice graphicsDevice, ISpriteBatch spriteBatch, TextureContent content, HolofunkView view, Transform parentTransform, int depth)
        {
            lock (this) {
                if (VideoStream != null) {
                    s_totalRenders++;

                    Slice<Frame, byte> videoImage = VideoStream.GetClosestSliver(now.Time + MagicNumbers.LatencyCompensationDuration);

                    if (!videoImage.IsEmpty()) {
                        if (videoImage.Equals(m_lastVideoFrame)) {
                            // skip the setData
                            s_redundantSetDatas++;
                        }
                        else {
                            // blast the data in there with a single pointer-based memory copy
                            videoImage.RawAccess((intptr, size) => m_videoNode.Texture.SetData(graphicsDevice, new DataPointer(intptr, size), arraySlice: 0, mipSlice: 0));
                            m_lastVideoFrame = videoImage;
                        }
                    }
                }
                else {
                    // ain't nothing to show
                    m_videoNode.Texture.SetData(graphicsDevice, BlankTextureData);
                }
            }

            base.DoRender(now, graphicsDevice, spriteBatch, content, view, parentTransform, depth);
        }        
    }
}
