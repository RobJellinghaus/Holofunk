////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;

namespace Holofunk
{
    /// <summary>A scene graph for Holofunk itself, with all sorts of customized accessors, events, etc.</summary>
    class HolofunkSceneGraph : SceneGraph
    {
        /// <summary>The color of silence.</summary>
        public static readonly Color MuteColor = new Color((byte)50, (byte)50, (byte)50, (byte)255);
        
        readonly HolofunkBass m_audio;
        readonly HolofunkTextureContent m_content;
        readonly Clock m_clock;

        /// <summary>The pulsing rainbow background.</summary>
        readonly SpriteNode m_background;

        /// <summary>
        /// The slide (as in, Powerpoint slide -- a switchable background teture in front
        /// of the pulsing background).
        /// </summary>
        readonly SpriteNode m_slide;

        /// <summary>The status text.</summary>
        readonly TextNode m_statusText;

        /// <summary>A little tiddly current-beat indicator in bottom center.</summary>
        readonly BeatNode m_beatNode;

        /// <summary>The layer into which all tracks go.</summary>
        readonly GroupNode m_trackGroupNode;

        /// <summary>How large is our canvas?</summary>
        readonly Vector2 m_canvasSize;

        /// <summary>current frame count; used to determine when to update status text</summary>
        int m_frameCount;

        /// <summary>number of ticks in a second; a tick = 100 nanoseconds</summary>
        const long TicksPerSecond = 10 * 1000 * 1000;

        readonly Stopwatch m_paintStopwatch = new Stopwatch();
        readonly FloatAverager m_paintMsecAverager = new FloatAverager(150); // ~5 secs

        internal HolofunkSceneGraph(
            GraphicsDevice graphicsDevice,
            Vector2 canvasSize,
            Texture2D depthTexture,
            HolofunkTextureContent holofunkContent,
            HolofunkBass audio,
            Clock clock)
            : base()
        {
            m_content = holofunkContent;
            m_clock = clock;

            RootNode = new GroupNode(null, Transform.Identity, "Root");
            m_canvasSize = canvasSize;

            m_background = new SpriteNode(
                RootNode,
                "Background",
                TextureFactory.ShadedCornerColor(
                    graphicsDevice,
                    canvasSize,
                    Color.Black,
                    new Color(0x10, 0x10, 0x10, 0x10),
                    new Color(0x20, 0x20, 0x20, 0x20),
                    new Color(0x20, 0x20, 0x20, 0x20)));
            m_background.LocalTransform = Transform.Identity;

            m_slide = new SpriteNode(
                RootNode,
                "Slide",
                Content.Slides[0]);
            m_slide.LocalTransform = new Transform(new Vector2(canvasSize.X - (int)(Content.Slides[0].Width * 1.1f),
                (canvasSize.Y - (int)Content.Slides[0].Height) / 2));
            m_slide.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored | SecondaryViewOption.TextureMirrored);

            // constructing the nodes adds them as children of the parent, in first-at-bottom Z order.

            SpriteNode depthNode = new SpriteNode(
                RootNode,
                "DepthImage",
                depthTexture);
            depthNode.LocalTransform = new Transform(
                Vector2.Zero,
                new Vector2((float)canvasSize.X / depthTexture.Width, (float)canvasSize.Y / depthTexture.Height));

            // we want the depth node texture (only) to be mirrored about the center of the viewport
            depthNode.SetSecondaryViewOption(SecondaryViewOption.TextureMirrored); // B4CR: should this also be | PositionMirrored?

            m_audio = audio;

            // Center the textures.
            Vector2 origin = new Vector2(0.5f);

            m_statusText = new TextNode(RootNode, "StatusText");
            m_statusText.SetSecondaryViewOption(SecondaryViewOption.Hidden);
            m_statusText.LocalTransform = new Transform(new Vector2(30f, 20f), new Vector2(MagicNumbers.StatusTextScale));

            // make sure that first update pushes status text
            m_frameCount = MagicNumbers.StatusTextUpdateInterval - 1;

            m_beatNode = new BeatNode(
                RootNode,
                new Transform(new Vector2(m_canvasSize.X / 2, m_canvasSize.Y / 8 * 7)),
                "Root Beater",
                false,
                MagicNumbers.MeasureCircleScale,
                () => (long)((float)clock.ContinuousBeatDuration * 4),
                () => 0,
                () => Color.White);

            m_trackGroupNode = new GroupNode(RootNode, Transform.Identity, "Track group");
        }

        internal HolofunkTextureContent Content { get { return m_content; } }

        internal Vector2 ViewportSize { get { return m_canvasSize; } }
        internal int TextureRadius { get { return m_content.HollowCircle.Width; } }

        internal HolofunkBass Audio { get { return m_audio; } }

        internal Clock Clock { get { return m_clock; } }

        internal GroupNode TrackGroupNode { get { return m_trackGroupNode; } }

        /// <summary>Update the scene's background based on the current beat, and the positions of
        /// the two hands based on the latest data polled from Kinect.</summary>
        /// <remarks>We pass in the current value of "now" to ensure consistent
        /// timing between the PlayerState update and the scene graph udpate.</remarks>
        internal void Update(
            HolofunkModel holofunkModel,
            HolofunKinect kinect,
            Moment now,
            float frameRateMsec)
        {
            // should do this once a second or so, to reduce garbage...
            if (++m_frameCount == MagicNumbers.StatusTextUpdateInterval) {
                m_frameCount = 0;

                m_statusText.Text.Clear();

                long usedAudioMB = Audio.AudioAllocator.TotalReservedSpace / 1024 / 1024;
                long freeAudioMB = Audio.AudioAllocator.TotalFreeListSpace / 1024 / 1024;
                long usedVideoMB = holofunkModel.VideoAllocator.TotalReservedSpace / 1024 / 1024;
                long freeVideoMB = holofunkModel.VideoAllocator.TotalFreeListSpace / 1024 / 1024;

                m_statusText.Text.AppendFormat(
                    "Time: {10}:{11:D2} | BPM: {0} | Update FPS: {1} | Kinect FPS: {2} | CPU: {3}%\nAudio: {4}/{5}MB | Video: {6}/{7}MB | Free streams: {8}\n{9}\n",
                    Clock.BPM,
                    frameRateMsec == 0 ? 0 : Math.Floor((1000f / frameRateMsec) * 10) / 10,
                    kinect.m_totalFrames / Clock.Now.Seconds,
                    Math.Floor(Audio.CpuUsage * 10) / 10,
                    usedAudioMB - freeAudioMB,
                    usedAudioMB,
                    usedVideoMB - freeVideoMB,
                    usedVideoMB,
                    Audio.StreamPoolFreeCount,
                    (Spam.TopLine1 == null ? "" : Spam.TopLine1)
                    + "\n" + (Spam.TopLine2 == null ? "" : Spam.TopLine2),
                    DateTime.Now.Hour,
                    DateTime.Now.Minute);
            }

            // Scale to byte and use for all RGBA components (premultiplied alpha, don't you know)
            Color backgroundColor = FloatScaleToRGBAColor(1 - (now.FractionalBeat / 1.2f));
            m_background.Color = backgroundColor;
            m_background.SecondaryColor = backgroundColor;

            m_beatNode.Update(now);

            Texture2D currentSlide = Content.Slides[holofunkModel.SlideIndex];
            m_slide.Texture = currentSlide;
            m_slide.LocalTransform = new Transform(new Vector2(
                m_canvasSize.X - (int)(currentSlide.Width),
                currentSlide.Height / 8));
            m_slide.Color = holofunkModel.SlideVisible ? Color.White : new Color(0, 0, 0, 0);

            Spam.Graphics.WriteLine("EndUpdate");
        }

        static Color FloatScaleToRGBAColor(double scale)
        {
            scale *= 255;
            byte byteScale = (byte)scale;
            Color scaleColor = new Color(byteScale, byteScale, byteScale, byteScale);
            return scaleColor;
        }
    }
}
