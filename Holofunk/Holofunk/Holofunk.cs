////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Windows.Forms;

namespace Holofunk
{
    // sort out our WinForms vs. XNA Name Battle
    using Color = Microsoft.Xna.Framework.Color;
    using HolofunkMachine = StateMachineInstance<LoopieEvent>;

    /// <summary>
    /// Centralize all compile-time numeric tuning knobs.
    /// </summary>
    static class MagicNumbers
    {
        #region Body constants

        internal const int BodyPositionSampleCount = 7;
        internal const int HandTrackerSampleCount = 7;

        // Initial K4W2 can only track the hands of two players; here is where we encode this.
        internal const int PlayerCount = HolofunKinect.PlayerCount;

        #endregion

        #region Timing constants

        /// <summary>update status text every 10 frames, to conserve on garbage</summary>
        internal const int StatusTextUpdateInterval = 20;

        #endregion

        #region Display-space constants

        // adjust the position of skeleton sprites by this much in screen space
        // TODO: eliminate this?  K4W2 gets it right :-D
        internal static Vector2 ScreenHandAdjustment = new Vector2(0, 0);

        // Length of slider nodes in starfish mode.
        internal const int SliderLength = 120;

        // How big is the bounding circle, in multiples of the base circle texture width?
        internal const float EffectSpaceBoundingCircleMultiple = 2f;

        // How much smaller is the circle than its texture width?
        internal const float EffectSpaceBoundingCircleSize = 1.5f;

        // How much smaller is the effect knob, in multiples of the base circle texture width?
        internal const float EffectSpaceKnobMultiple = 0.25f;

        /// <summary>scale factor to apply to track nodes and hand cursors</summary>
        internal const float LoopieScale = 1.8f;
        /// <summary>scale factor to apply to the distance of menu nodes</summary>
        internal const float MenuScale = 1.2f;
        /// <summary>Scale of a menu node.</summary>
        internal const float MenuNodeScale = 1.5f;
        /// <summary>Scale of status text.</summary>
        internal const float StatusTextScale = 0.9f;
        /// <summary>Scale of effect label text.</summary>
        internal const float EffectTextScale = 0.8f;
        /// <summary>Scale of menu text.</summary>
        internal const float MenuTextScale = 0.6f;

        /// <summary>How big is a measure circle, proportional to its source texture?</summary>
        internal const float MeasureCircleScale = 0.5f;

        /// <summary>Number of pixels square that we capture for the head</summary>
        internal const int HeadCaptureSize = 200;

        /// <summary>Number of total bytes in an RGBA head capture</summary>
        internal const int HeadCaptureBytes = HeadCaptureSize * HeadCaptureSize * 4;

        /// <summary>
        ///  Amount to shrink the head by
        /// </summary>
        internal const float HeadRatio = 1.3f;

        #endregion

        #region Musical constants

        // Max number of streams.
        internal const int MaxStreamCount = 20;

        // 4/4 time (actually, 4/_ time, we don't care about the note duration)
        internal const int BeatsPerMeasure = 4;

        // what tempo do we start at?
        // turnado sync: 130.612f (120bpm x 48000/44100)
        internal const float InitialBpm = 90; 

        /// <summary>How many samples back do we go when recording a new track?  (Latency compensation, basically.)</summary>
        internal static Duration<Sample> LatencyCompensationDuration = Clock.TimepointRateHz / 6; // 48000 / 6 

        /// <summary>Fade effect labels over this amount of time (in audio sample time)</summary>
        internal static Duration<Sample> EffectLabelFadeDuration = Clock.TimepointRateHz;

        #endregion

        /// <summary>
        /// Static constructor injects magic numbers into dependent assemblies.
        /// </summary>
        public static void Initialize()
        {
            HolofunkBody.BodyPositionSampleCount = BodyPositionSampleCount;
            HandTracker.SampleCount = HandTrackerSampleCount;
            HolofunkBass.EarlierDuration = LatencyCompensationDuration;
            TextSpriteNode.TextScale = MenuTextScale;
        }
    }

    /// <summary>The Holofunk, incarnate.</summary>
    /// <remarks>Implements all the main game logic, coordinates all major components, and basically
    /// gets the job done.</remarks>
    public class HolofunkGame : Game
    {
        static HolofunkGame()
        {
            MagicNumbers.Initialize();
        }

        readonly Clock m_clock;

        readonly GraphicsDeviceManager m_graphicsDeviceManager;

        readonly HolofunkForm m_primaryForm;

        struct EventEntry
        {
            public readonly LoopieEvent Event;
            public readonly HolofunkMachine Machine;
            public EventEntry(LoopieEvent evt, HolofunkMachine machine)
            {
                HoloDebug.Assert(machine != null);
                Event = evt;
                Machine = machine;
            }
            public bool IsInitialized { get { return Machine != null; } }
        }

        BufferAllocator<float> m_audioAllocator;
        BufferAllocator<byte> m_videoAllocator;

        HolofunKinect m_kinect;
        HolofunkBass m_holofunkBass;
        HolofunkModel m_model;

        ISpriteBatch m_spriteBatch;

        // diagnosis: when was last collection?
        int[] m_lastCollectionCounts;

        // how large is our viewport
        Vector2 m_viewportSize;

        public HolofunkGame(HolofunkForm primaryForm) 
        {
            m_clock = new Clock(MagicNumbers.InitialBpm, MagicNumbers.BeatsPerMeasure, HolofunkBassAsio.InputChannelCount);
            m_primaryForm = primaryForm;

            // Setup the relative directory to the executable directory
            // for loading contents with the ContentManager
            Content.RootDirectory = "TextureContent";

            m_graphicsDeviceManager = new GraphicsDeviceManager(this);
            //m_graphicsDeviceManager.IsFullScreen = true;
            //m_graphicsDeviceManager.PreferredFullScreenOutputIndex = 0;
            m_graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;
            
            m_graphicsDeviceManager.PreferredBackBufferWidth = 1920;
            m_graphicsDeviceManager.PreferredBackBufferHeight = 1080;

            m_lastCollectionCounts = new int[GC.MaxGeneration + 1];

            base.IsFixedTimeStep = true;
        }

        internal Moment Now { get { return m_clock.Now; } }

        internal Vector2 ViewportSize { get { return m_viewportSize; } }

        internal HolofunkView SecondaryView { get { return m_model.SecondaryView; } }

        /// <summary>Allows the game to perform any initialization it needs to before starting to run.</summary>
        /// <remarks>This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.</remarks>
        protected override void Initialize()
        {
            new Test(GraphicsDevice).RunAllTests();

            // HORRIBLE HACK: just ensure the statics are initialized
            string s = PlayerEffectSpaceModel.EffectSettings[0].LeftLabel;

            m_audioAllocator = new BufferAllocator<float>(2 * 4 * Clock.TimepointRateHz, 128, sizeof(float));
            
            m_holofunkBass = new HolofunkBass(m_clock, m_audioAllocator); 
            m_holofunkBass.StartASIO();

            m_kinect = new HolofunKinect(GraphicsDevice, BodyFrameUpdate);

            m_viewportSize = m_kinect.ViewportSize;

            m_videoAllocator = new BufferAllocator<byte>(64 * MagicNumbers.HeadCaptureBytes, 128, 1);

            base.Initialize();

            // oh dear

            m_holofunkBass.SetBaseForm(m_primaryForm, MagicNumbers.MaxStreamCount);

            Window.Title = "Holofunk Alpha";
            Window.AllowUserResizing = true;

            /*
            object nativeWindow = Window.NativeWindow;
            System.Windows.Forms.Form asForm = nativeWindow as System.Windows.Forms.Form;
            asForm.SetDesktopBounds(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            //asForm.SetDesktopBounds(100, 100, (int)(m_viewportSize.X * 2), (int)(m_viewportSize.Y * 2));
            */
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            float scale = GraphicsDevice.PresentationParameters.BackBufferHeight / m_viewportSize.Y;
            float scaledViewportWidth = m_viewportSize.X * scale;
            float scaledViewportOffset = (GraphicsDevice.PresentationParameters.BackBufferWidth - scaledViewportWidth) / 2;
            Transform transform = new Transform(new Vector2(scaledViewportOffset, 0), new Vector2(scale)); 
            
            m_spriteBatch = new SpriteBatchWrapper(new SpriteBatch(GraphicsDevice), ViewportSize, transform);

            var holofunkContent = new HolofunkTextureContent(Content, GraphicsDevice);

            m_model = new HolofunkModel(
                GraphicsDevice,
                m_clock,
                m_holofunkBass,
                m_kinect,
                holofunkContent,
                m_viewportSize,
                m_clock.BPM,
                m_audioAllocator, 
                m_videoAllocator);                
        }

        /// <summary>Dispose this and all its state.</summary>
        /// <remarks>This seems to be called twice... so making it robust to that.</remarks>
        protected override void Dispose(bool disposeManagedResources)
        {
            if (m_kinect != null) {
                m_kinect.Dispose();
                m_kinect = null;
            }

            if (m_holofunkBass != null) {
                m_holofunkBass.Dispose();
                m_holofunkBass = null;
            }

            base.Dispose(disposeManagedResources);
        }

        // [MainThread]
        protected override void Update(GameTime gameTime)
        {
            for (int i = 0; i < GC.MaxGeneration; i++) {
                int thisCount = GC.CollectionCount(i);
                if (thisCount != m_lastCollectionCounts[i]) {
                    m_lastCollectionCounts[i] = thisCount;
                    Spam.Model.WriteLine("Holofunk.Update: updated collection count for gen" + i + " to " + thisCount + "; gen0 " + m_lastCollectionCounts[0] + ", gen1 " + m_lastCollectionCounts[1] + ", gen2 " + m_lastCollectionCounts[2]);
                }
            }

            GameUpdate();
        }

        void BodyFrameUpdate(HolofunKinect kinect)
        {
            // call into the model to propagate this
            if (m_model != null) {
                m_model.BodyFrameUpdate(kinect);
            }

        }

        int ChannelToPlayerIndex(int channel)
        {
            // trivial now, but may change someday, who knows
            return channel;
        }

        /// <summary>Allows the form to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.</summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        void GameUpdate()
        {
            if (m_kinect == null) {
                // we've been disposed, do nothing
                return;
            }

            // update the tempo.  This ensures clock consistency from the point of view
            // of the scene graph (which is updated and rendered from the XNA thread).
            // We don't yet handle updating existing tracks, so don't change BPM if there are any.
            // TODO: add tempo shifting that works perfectly throughout the whole system.... EEECH
            if (m_model.RequestedBPM != m_clock.BPM && m_model.Loopies.Count == 0) {
                m_clock.BPM = m_model.RequestedBPM;
            }

            Moment now = m_clock.Now;

            // now update the Holofunk model
            m_model.GameUpdate(now);
        }

        protected override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            Render(Now, GraphicsDevice, m_spriteBatch, gameTime, HolofunkView.Primary, Color.Black);
        }

        internal void Render(Moment now, GraphicsDevice graphicsDevice, ISpriteBatch spriteBatch, GameTime gameTime, HolofunkView view, Color backgroundColor)
        {
            graphicsDevice.Clear(backgroundColor);

            m_model.SceneGraph.Render(now, graphicsDevice, spriteBatch, m_model.Content, view);
        }
    }
}
