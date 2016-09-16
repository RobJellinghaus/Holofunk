////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

// This file contains code based on the Kinect SDK SkeletonViewer sample,
// which is licensed under the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU

//#define COPY_ALL_ZERO_NON_PLAYER // copy all pixels, zero out the non-player
#define COPY_PLAYER // copy only the player pixels

//#define GREEN_SCREEN_MAPPING_DEPTH_TO_COLOR_RESOLUTION
#define GREEN_SCREEN_MAPPING_DEPTH_TO_COLOR_SPLATS

#if COPY_ALL_ZERO_NON_PLAYER
#elif COPY_PLAYER
#else
#error Must define one of COPY_ALL_ZERO_NON_PLAYER and COPY_PLAYER
#endif

#if GREEN_SCREEN_MAPPING_DEPTH_TO_COLOR_RESOLUTION
#elif GREEN_SCREEN_MAPPING_DEPTH_TO_COLOR_SPLATS
#else
#error Must define one of GREEN_SCREEN_MAPPING_DEPTH_TO_COLOR_RESOLUTION and GREEN_SCREEN_MAPPING_DEPTH_TO_COLOR_SPLATS
#endif

using Holofunk.Core;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Holofunk.Kinect
{
    /// <summary>Interface for passing in hand transitions.</summary>
    public interface ITwoHandedEventSink
    {
        void OnLeftHand(HandPose transition);
        void OnRightHand(HandPose transition);
        void OnLeftArm(ArmPose transition);
        void OnRightArm(ArmPose transition);
    }

    /// <summary>Provides access to Kinect data streams and manages connection to Kinect device.</summary>
    /// <remarks>This class tracks two distinct players, Player0 and Player1.  The default tracking
    /// algorithm is "sticky" -- the first recognized player becomes Player0, and the second becomes
    /// Player1.  If two are recognized simultaneously, the leftmost is Player0.  If one player goes
    /// out of view, that player slot becomes available, and the next recognized player gets it.
    /// But as long as a player is in view, they will be persistently tracked.
    /// 
    /// This deliberately does not support multiple Kinect sensors.</remarks>
    public class HolofunKinect : IDisposable
    {
        public const int PlayerCount = 2;

        /// <summary>
        /// Size of the RGBA pixel in the bitmap
        /// </summary>
        static readonly int BytesPerColorPixel = 4;

        KinectSensor m_kinect;
        CoordinateMapper m_coordinateMapper;
        public int m_totalFrames = 0;
        int m_lastFrames = 0;
        DateTime m_lastTime = DateTime.MaxValue;

        /// <summary>The event sink for each player; indexed by player index, must be initialized before events start flowing.</summary>
        ITwoHandedEventSink[] m_eventSinkArray = new ITwoHandedEventSink[PlayerCount];

        /// <summary>The texture to be displayed as the backdrop.</summary>
        Texture2D m_displayTexture;

        /// <summary>The latest skeletal data received.</summary>
        List<Body> m_bodies;

        /// <summary>The HolofunkBodies tracking the raw skeletal data; one per supported tracked skeleton.</summary>
        List<HolofunkBody> m_holofunkBodies;

        /// <summary>How big is our viewport?</summary>
        /// <remarks>  Used when calculating skeleton -> viewport translation, and sized by depth buffer,
        /// since we don't get any player/bone/depth data outside the depth area.</remarks>
        Vector2 m_viewportSize;

        /// <summary>The graphics device from which we can create a player texture.</summary>
        GraphicsDevice m_graphicsDevice;

        /// <summary>A reusable, temporary list of HolofunkSkeletons (kept as a field to avoid churning the GC).</summary>
        List<HolofunkBody> m_tempSkeletonList = new List<HolofunkBody>();

        /// <summary>
        /// Reader for depth/color/body index frames
        /// </summary>
        MultiSourceFrameReader m_reader;

        /// <summary>
        /// The depth space points corresponding to each color space point; used in GreenScreenMappingDepthToColorResolution.
        /// </summary>
        DepthSpacePoint[] m_colorToDepthSpacePoints;

        /// <summary>
        /// The color space points corresponding to each depth space point; used in GreenScreenMappingDepthToColorSplats.
        /// </summary>
        ColorSpacePoint[] m_depthToColorSpacePoints;

        /// <summary>
        /// Intermediate storage for converting sensor YUY2 data to BGRA data
        /// </summary>
        byte[] m_colorFrameData;

        /// <summary>
        /// Intermediate storage for frame data converted to color
        /// </summary>
        byte[] m_displayPixels;

        Stopwatch m_stopwatch = new Stopwatch();
        const int TimerSamples = 20;
        public FloatAverager m_depthMapTimer = new FloatAverager(TimerSamples);
        public FloatAverager m_colorCopyTimer = new FloatAverager(TimerSamples);
        public FloatAverager m_colorScanTimer = new FloatAverager(TimerSamples);
        public FloatAverager m_textureSetDataTimer = new FloatAverager(TimerSamples);

        /// <summary>
        /// Called once per body frame update, after the Bodies themselves have been updated with latest joint positions.
        /// </summary>
        Action<HolofunKinect> m_bodyFrameUpdateAction;

        public HolofunKinect(GraphicsDevice graphicsDevice, Action<HolofunKinect> bodyFrameUpdateAction)
        {
            m_graphicsDevice = graphicsDevice;
            KinectStart();
            m_bodyFrameUpdateAction = bodyFrameUpdateAction;
        }

        public void RegisterPlayerEventSink(int playerId, ITwoHandedEventSink sink)
        {
            m_eventSinkArray[playerId] = sink;
        }

        void KinectStart()
        {
            m_kinect = KinectSensor.GetDefault();
            m_coordinateMapper = m_kinect.CoordinateMapper;

            m_kinect.Open();

            try {
                FrameDescription depthFrameDescription = m_kinect.DepthFrameSource.FrameDescription;

                int depthWidth = depthFrameDescription.Width;
                int depthHeight = depthFrameDescription.Height;

                /*
                // create the bitmap to display
                m_bitmap = new WriteableBitmap(depthWidth, depthHeight, 96.0, 96.0, PixelFormats.Bgra32, null);
                 */

                FrameDescription colorFrameDescription = m_kinect.ColorFrameSource.FrameDescription;

                int colorWidth = colorFrameDescription.Width;
                int colorHeight = colorFrameDescription.Height;

                m_viewportSize = new Vector2(colorWidth, colorHeight);

                m_colorFrameData = new byte[colorWidth * colorHeight * BytesPerColorPixel];

                m_displayPixels = new byte[colorWidth * colorHeight * BytesPerColorPixel];
                m_displayTexture = new Texture2D(m_graphicsDevice, colorWidth, colorHeight, mipMap: true, format: SurfaceFormat.Color);

                m_colorToDepthSpacePoints = new DepthSpacePoint[colorWidth * colorHeight];
                m_depthToColorSpacePoints = new ColorSpacePoint[depthWidth * depthHeight];

                m_reader = m_kinect.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Color | FrameSourceTypes.BodyIndex | FrameSourceTypes.Body);
                m_reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

                if (m_bodies == null) {
                    int bodyCount = 6; //  m_kinect.BodyFrameSource.BodyCount;
                    m_bodies = new List<Body>(bodyCount);
                    for (int i = 0; i < bodyCount; i++) {
                        m_bodies.Add(null);
                    }

                    m_holofunkBodies = new List<HolofunkBody>(PlayerCount);
                    for (int i = 0; i < PlayerCount; i++) {
                        m_holofunkBodies.Add(new HolofunkBody());
                    }
                }

            }
            catch (InvalidOperationException) {
                HoloDebug.Assert(false);
                return;
            }

            m_lastTime = DateTime.Now;
        }

        /// <summary>
        /// Handles the depth/color/body index frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrameReference frameReference = e.FrameReference;

            // If you hit an E_FAIL here and the Kinect is starting up and shutting down repeatedly,
            // check your "USB Suspend" and "Link state power management" advanced power settings:
            // see https://social.msdn.microsoft.com/Forums/en-US/fb5d5590-4cb9-4c99-918a-4af18017b86f/kinect-service-shutting-down?forum=kinectv2sdk&prof=required
            MultiSourceFrame multiSourceFrame = frameReference.AcquireFrame();
            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            BodyIndexFrame bodyIndexFrame = null;
            BodyFrame bodyFrame = null;

            try
            {
                if (multiSourceFrame != null) {
                    DepthFrameReference depthFrameReference = multiSourceFrame.DepthFrameReference;
                    ColorFrameReference colorFrameReference = multiSourceFrame.ColorFrameReference;
                    BodyIndexFrameReference bodyIndexFrameReference = multiSourceFrame.BodyIndexFrameReference;
                    BodyFrameReference bodyFrameReference = multiSourceFrame.BodyFrameReference;

                    depthFrame = depthFrameReference.AcquireFrame();
                    colorFrame = colorFrameReference.AcquireFrame();
                    bodyIndexFrame = bodyIndexFrameReference.AcquireFrame();
                    bodyFrame = bodyFrameReference.AcquireFrame();

                    if ((depthFrame != null) && (colorFrame != null) && (bodyFrame != null) && (bodyIndexFrame != null)) {
                        FrameDescription depthFrameDescription = depthFrame.FrameDescription;
                        FrameDescription colorFrameDescription = colorFrame.FrameDescription;
                        FrameDescription bodyIndexFrameDescription = bodyIndexFrame.FrameDescription;

                        int depthWidth = depthFrameDescription.Width;
                        int depthHeight = depthFrameDescription.Height;

                        int colorWidth = colorFrameDescription.Width;
                        int colorHeight = colorFrameDescription.Height;

                        int bodyIndexWidth = bodyIndexFrameDescription.Width;
                        int bodyIndexHeight = bodyIndexFrameDescription.Height;

                        ++m_totalFrames;

                        DateTime cur = DateTime.Now;
                        if (cur.Subtract(m_lastTime) > TimeSpan.FromSeconds(1)) {
                            int frameDiff = m_totalFrames - m_lastFrames;
                            m_lastFrames = m_totalFrames;
                            m_lastTime = cur;
                            // Title = frameDiff.ToString() + " fps";
                        }

                        BodyFrameReady(bodyFrame);

                        // Done with bodyFrame
                        bodyFrame.Dispose();
                        bodyFrame = null;                

                        GreenScreenMappingDepthToColorSplats(ref depthFrame, ref colorFrame, ref bodyIndexFrame, depthWidth, depthHeight, colorWidth, colorHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                HoloDebug.Assert(false, ex.Message);
            }
            finally
            {
                // MultiSourceFrame, DepthFrame, ColorFrame, BodyIndexFrame are IDispoable
                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                    depthFrame = null;
                }

                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                    colorFrame = null;
                }

                if (bodyIndexFrame != null)
                {
                    bodyIndexFrame.Dispose();
                    bodyIndexFrame = null;
                }

                if (bodyFrame != null) 
                {
                    bodyFrame.Dispose();
                    bodyFrame = null;
                }
            }
        }

        void GreenScreenMappingDepthToColorSplats(ref DepthFrame depthFrame, ref ColorFrame colorFrame, ref BodyIndexFrame bodyIndexFrame, int depthWidth, int depthHeight, int colorWidth, int colorHeight)
        {
            m_stopwatch.Restart();

            using (KinectBuffer depthFrameData = depthFrame.LockImageBuffer()) {
                // Need to know the color space point for each depth space point, but this is much less data
                // and much faster to compute than mapping the other way
                m_coordinateMapper.MapDepthFrameToColorSpaceUsingIntPtr(
                    depthFrameData.UnderlyingBuffer,
                    depthFrameData.Size,
                    m_depthToColorSpacePoints);
            }

            m_depthMapTimer.Update(m_stopwatch.ElapsedMilliseconds);
            m_stopwatch.Restart();

            // We're done with the DepthFrame 
            depthFrame.Dispose();
            depthFrame = null;

            lock (m_displayPixels) { // [KinectThread] avoid racing display buffer refresh with render (can cause missing images)

                // have to clear the display pixels so we can copy only the BGRA image of the player(s)
                Array.Clear(m_displayPixels, 0, m_displayPixels.Length);

                unsafe {
                    fixed (byte* colorFrameDataPtr = &m_colorFrameData[0]) {
                        colorFrame.CopyConvertedFrameDataToIntPtr(new IntPtr(colorFrameDataPtr), (uint)m_colorFrameData.Length, ColorImageFormat.Bgra);
                    }
                }

                // done with the colorFrame
                colorFrame.Dispose();
                colorFrame = null;

                m_colorCopyTimer.Update(m_stopwatch.ElapsedMilliseconds);
                m_stopwatch.Restart();

                // We'll access the body index data directly to avoid a copy
                using (KinectBuffer bodyIndexData = bodyIndexFrame.LockImageBuffer()) {
                    unsafe {
                        byte* bodyIndexDataPointer = (byte*)bodyIndexData.UnderlyingBuffer;
                        uint bodyIndexDataLength = bodyIndexData.Size;

                        int colorMappedToDepthPointCount = m_colorToDepthSpacePoints.Length;

                        fixed (ColorSpacePoint* depthMappedToColorPointsPointer = m_depthToColorSpacePoints) {
                            fixed (byte* bitmapPixelsBytePointer = &m_displayPixels[0]) {
                                fixed (byte* sourcePixelsBytePointer = &m_colorFrameData[0]) {
                                    uint* bitmapPixelsPointer = (uint*)bitmapPixelsBytePointer;
                                    uint* sourcePixelsPointer = (uint*)sourcePixelsBytePointer;

                                    // We don't go all the way to the edge of the depth buffer, to eliminate a chance
                                    // that a splat will go outside the edge of the color buffer when mapped to color
                                    // space.  In the x direction this will never happen anyway since the depth FOV
                                    // is so much narrower than the color FOV.
                                    const int Margin = 2;
                                    for (int y = Margin; y < depthHeight - Margin; y++) {
                                        for (int x = 0; x < depthWidth; x++) {
                                            // Scan forwards until we find a non-0xff value in the body index data.
                                            int depthIndex = y * depthWidth + x;
                                            if (bodyIndexDataPointer[depthIndex] != 0xff) {
                                                int depthIndex2 = depthIndex;
                                                // We found the beginning of a horizontal run of player pixels.
                                                // Scan to the end.
                                                int runWidth;
                                                for (runWidth = 1; runWidth + x < depthWidth; runWidth++) {
                                                    depthIndex2++;
                                                    if (bodyIndexDataPointer[depthIndex2] == 0xff) {
                                                        break;
                                                    }
                                                }
                                                
                                                // Now splat from (x, y) to (x + runWidth, y)
                                                float depthMappedToColorLeftX = depthMappedToColorPointsPointer[depthIndex].X;
                                                float depthMappedToColorLeftY = depthMappedToColorPointsPointer[depthIndex].Y;
                                                float depthMappedToColorRightX = depthMappedToColorPointsPointer[depthIndex2 - 1].X;
                                                float depthMappedToColorRightY = depthMappedToColorPointsPointer[depthIndex2 - 1].Y;

                                                // Now copy color pixels along that rectangle.
                                                const int splatHMargin = 2; // X margin of splat rectangle in color pixels
                                                const int splatVMargin = 3; // Y margin of splat rectangle in color pixels
                                                int minX = (int)Math.Min(depthMappedToColorLeftX, depthMappedToColorRightX) - splatHMargin;
                                                int minY = (int)Math.Min(depthMappedToColorLeftY, depthMappedToColorRightY) - splatVMargin;
                                                int maxX = (int)Math.Max(depthMappedToColorLeftX, depthMappedToColorRightX) + splatHMargin;
                                                int maxY = (int)Math.Max(depthMappedToColorLeftY, depthMappedToColorRightY) + splatVMargin;

                                                // Some edge of screen situations can result in color space points that are negative or otherwise
                                                // actually outside the color space coordinate range.
                                                Clamp(ref minX, colorWidth - 1);
                                                Clamp(ref minY, colorHeight - 1);
                                                Clamp(ref maxX, colorWidth - 1);
                                                Clamp(ref maxY, colorHeight - 1);

                                                for (int colorY = minY; colorY < maxY; colorY++) {
                                                    int colorIndex = colorY * colorWidth + minX;
                                                    for (int colorX = minX; colorX < maxX; colorX++) {
                                                        bitmapPixelsPointer[colorIndex] = sourcePixelsPointer[colorIndex];
                                                        colorIndex++;
                                                    }
                                                }

                                                x += runWidth;                          
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Done with bodyIndexFrame
                bodyIndexFrame.Dispose();
                bodyIndexFrame = null;                
            }

            m_colorScanTimer.Update(m_stopwatch.ElapsedMilliseconds);
            m_stopwatch.Restart();

            m_displayTexture.SetData(m_displayPixels);

            m_textureSetDataTimer.Update(m_stopwatch.ElapsedMilliseconds);
            m_stopwatch.Restart();

            Spam.TopLine1 = string.Format("depth map: {0} msec; color copy: {1} msec; color scan: {2} msec; texture set: {3} msec",
                m_depthMapTimer.Average,
                m_colorCopyTimer.Average,
                m_colorScanTimer.Average,
                m_textureSetDataTimer.Average);
        }

        void Clamp(ref int clampee, int max)
        {
            if (clampee < 0) {
                clampee = 0;
                return;
            }
            if (clampee > max) {
                clampee = max;
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct LongConverter
        {
            internal static LongConverter Get(byte[] bytes)
            {
                LongConverter converter = new LongConverter();
                converter.Bytes = bytes;
                return converter;
            }

            [FieldOffset(0)]
            internal byte[] Bytes;
            [FieldOffset(0)]
            internal long[] Longs;
        }

        public void Dispose()
        {
            if (m_kinect != null) {
                m_kinect.Close();
                m_kinect = null;
            }
        }
        
        /// <summary>The colorized, depth-based display texture.</summary>
        public Texture2D DisplayTexture
        {
            get { return m_displayTexture; }
        }

        public byte[] DisplayTextureBuffer
        {
            get { return m_displayPixels; }
        }

        /// <summary>very useful to know</summary>
        public Vector2 ViewportSize
        {
            get { return m_viewportSize; }
        }

        HolofunkBody GetBody(int playerId)
        {
            return m_holofunkBodies[playerId];
        }

        /// <summary>Get the position of the given joint in viewport coordinates.</summary>
        /// <remarks>If there is no first skeleton, returns the identity transform.  Should rearrange
        /// all this to support persistent skeleton identification....</remarks>
        public Vector2 GetJointViewportPosition(int playerId, JointType joint)
        {
            HolofunkBody skeleton = GetBody(playerId);
            // if no skeleton, all joints are waaaay offscreen (to the upper left)
            return skeleton == null ? new Vector2(-1000) : skeleton[joint];
        }

        public ArmPose GetArmPose(int playerId, Side side)
        {
            HolofunkBody body = GetBody(playerId);
            return body.GetArmPose(side);
        }

        // Get the coordinates in color-pixel space.
        internal Vector2 GetDisplayPosition(CameraSpacePoint jointPosition)
        {
            ColorSpacePoint point = m_coordinateMapper.MapCameraPointToColorSpace(jointPosition);
            return new Vector2(point.X, point.Y);
        }

        void BodyFrameReady(BodyFrame frame)
        {
            frame.GetAndRefreshBodyData(m_bodies);

            // now update the player-index-to-body mapping
            for (int i = 0; i < PlayerCount; i++) {
                UpdatePlayerMapping(i);
            }

            for (int i = 0; i < PlayerCount; i++) {
                // Update the bodies with the latest pose state.  This may fire events and state machine actions.
                m_holofunkBodies[i].Update(this,
                    m_eventSinkArray[i].OnLeftHand,
                    m_eventSinkArray[i].OnLeftArm,
                    m_eventSinkArray[i].OnRightHand,
                    m_eventSinkArray[i].OnRightArm);
            }

            if (m_bodyFrameUpdateAction != null) {
                m_bodyFrameUpdateAction(this);
            }
        }

        void UpdatePlayerMapping(int playerIndex)
        {
            // If there is a body for this player index, then either it's tracked or it's not; if it's not, then
            // wipe out the body's entry.
            if (m_holofunkBodies[playerIndex].Body != null) {
                if (m_holofunkBodies[playerIndex].Body.IsTracked) {
                    return;
                }
                else {
                    m_holofunkBodies[playerIndex].Body = null;
                }
            }

            // We need a not-yet-associated-with-a-player body for this player index, if one is available.
            // Walk through the bodies and find the first one that is 1) tracked and 2) not already assigned.
            for (int i = 0; i < m_bodies.Count; i++) {
                if (m_bodies[i].IsTracked && m_bodies[i].HandLeftState != HandState.NotTracked) {
                    bool found = false;
                    for (int j = 0; j < PlayerCount; j++) {
                        if (j == playerIndex) {
                            continue;
                        }
                        else if (m_holofunkBodies[j].Body == m_bodies[i]) {
                            found = true;
                            break;
                        }
                    }

                    if (found) {
                        continue;
                    }
                    else {
                        // this is a tracked body not associated with any player
                        // so use it!
                        m_holofunkBodies[playerIndex].Body = m_bodies[i];
                    }
                }
            }
        }

        public void SwapPlayers()
        {
            Body body0 = m_holofunkBodies[0].Body;
            m_holofunkBodies[0].Body = m_holofunkBodies[1].Body;
            m_holofunkBodies[1].Body = body0;
        }
    }
}
