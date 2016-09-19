////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.SceneGraphs;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Holofunk
{
    /// <summary>Container for resources defined in HolofunkContent project.</summary>
    public class HolofunkTextureContent : TextureContent
    {
        const string EXT = ".png";

        Texture2D m_bigDot;
        Texture2D m_dot;
        Texture2D m_effectCircle;
        Texture2D m_filledCircle;
        Texture2D m_filledSquare;
        Texture2D m_handCircle;
        Texture2D m_hollowCircle;
        Texture2D m_hollowFace0;
        Texture2D m_hollowFace1;
        Texture2D m_hollowFace2;
        Texture2D m_hollowOneOval;
        Texture2D m_hollowOval;
        Texture2D m_hollowSquare;
        Texture2D m_hollowTwoOval;
        Texture2D m_leftHand;
        Texture2D m_lessHollowSquare;
        Texture2D m_microphone;
        Texture2D m_microphoneHighlighted;
        Texture2D m_muteCircle;
        Texture2D m_pointer;
        Texture2D m_pointingCircle;
        Texture2D m_quarterFilledCircle;
        Texture2D m_quarterHollowCircle;
        Texture2D m_recordCircle;
        Texture2D m_rewindCircle;
        Texture2D m_rightHand;
        Texture2D m_slide_1989_tuib;
        Texture2D m_slide_beardyandme;
        Texture2D m_slide_BeardymanTED;
        Texture2D m_slide_beardy_monkey_jazz;
        Texture2D m_slide_castle_looping;
        Texture2D m_slide_environment;
        Texture2D m_slide_holofunkcom;
        Texture2D m_slide_holofunkdesign;
        Texture2D m_slide_holofunkfnf;
        Texture2D m_slide_loopfest;
        Texture2D m_slide_reggietoys;
        Texture2D m_tinyDot;
        Texture2D m_unmuteCircle;

        SpriteFont m_spriteFont;

        GraphicsDevice m_device;

        Texture2D[] m_slides;

        public HolofunkTextureContent(ContentManager content, GraphicsDevice device)
        {
            m_bigDot = content.Load<Texture2D>("20x20_big_dot" + EXT);
            m_dot = content.Load<Texture2D>("20x20_dot" + EXT);
            m_effectCircle = content.Load<Texture2D>("EffectCircle" + EXT);
            m_filledCircle = content.Load<Texture2D>("FilledCircle" + EXT);
            m_filledSquare = content.Load<Texture2D>("20x20_filled_square" + EXT);
            m_handCircle = content.Load<Texture2D>("HandCircle" + EXT);
            m_hollowCircle = content.Load<Texture2D>("HollowCircle" + EXT);
            m_hollowFace0 = content.Load<Texture2D>("HollowFace0" + EXT);
            m_hollowFace1 = content.Load<Texture2D>("HollowFace1" + EXT);
            m_hollowFace2 = content.Load<Texture2D>("HollowFace2" + EXT);
            m_hollowOneOval = content.Load<Texture2D>("HollowOneOval" + EXT);
            m_hollowOval = content.Load<Texture2D>("HollowOval" + EXT);
            m_hollowSquare = content.Load<Texture2D>("20x20_hollow_square" + EXT);
            m_hollowTwoOval = content.Load<Texture2D>("HollowTwoOval" + EXT);
            m_leftHand = content.Load<Texture2D>("LeftHand" + EXT);
            m_lessHollowSquare = content.Load<Texture2D>("20x20_less_hollow_square" + EXT);
            m_microphone = content.Load<Texture2D>("Microphone" + EXT);
            m_microphoneHighlighted = content.Load<Texture2D>("MicrophoneHighlighted" + EXT);
            m_muteCircle = content.Load<Texture2D>("MuteCircle" + EXT);
            m_pointer = content.Load<Texture2D>("Pointer" + EXT);
            m_pointingCircle = content.Load<Texture2D>("PointingCircle" + EXT);
            m_quarterFilledCircle = content.Load<Texture2D>("QuarterFilledCircle" + EXT);
            m_quarterHollowCircle = content.Load<Texture2D>("QuarterHollowCircle" + EXT);
            m_rightHand = content.Load<Texture2D>("RightHand" + EXT);
            m_recordCircle = content.Load<Texture2D>("RecCircle" + EXT);
            m_rewindCircle = content.Load<Texture2D>("RewindCircle" + EXT);
            m_slide_1989_tuib = content.Load<Texture2D>("slide_1989_tuib" + EXT);
            m_slide_beardyandme = content.Load<Texture2D>("slide_beardyandme" + EXT);
            m_slide_BeardymanTED = content.Load<Texture2D>("slide_BeardymanTED" + EXT);
            m_slide_beardy_monkey_jazz = content.Load<Texture2D>("slide_beardy_monkey_jazz" + EXT);
            m_slide_castle_looping = content.Load<Texture2D>("slide_castle_looping" + EXT);
            m_slide_environment = content.Load<Texture2D>("slide_environment" + EXT);
            m_slide_holofunkcom = content.Load<Texture2D>("slide_holofunkcom" + EXT);
            m_slide_holofunkdesign = content.Load<Texture2D>("slide_holofunkdesign" + EXT);
            m_slide_holofunkfnf = content.Load<Texture2D>("slide_holofunkfnf" + EXT);
            m_slide_loopfest = content.Load<Texture2D>("slide_loopfest" + EXT);
            m_slide_reggietoys = content.Load<Texture2D>("slide_reggietoys" + EXT);
            m_tinyDot = content.Load<Texture2D>("2x2_filled_square" + EXT);
            m_unmuteCircle = content.Load<Texture2D>("UnmuteCircle" + EXT);

            m_spriteFont = content.Load<SpriteFont>("MoireBold14.xnb");

            m_device = device;

            m_slides = new Texture2D[] 
            { 
                m_slide_1989_tuib, 
                m_slide_BeardymanTED, 
                //m_slide_reggietoys, 
                //m_slide_castle_looping, 
                m_slide_beardy_monkey_jazz, 
                m_slide_beardyandme, 
                m_slide_holofunkdesign, 
                m_slide_holofunkfnf,
                //m_slide_loopfest,
                //m_slide_environment,
                m_slide_holofunkcom,
            };
        }

        public override Texture2D BigDot { get { return m_bigDot; } }
        public override Texture2D Dot { get { return m_dot; } }
        public override Texture2D EffectCircle { get { return m_effectCircle; } }
        public override Texture2D FilledCircle { get { return m_filledCircle; } }
        public override Texture2D FilledSquare { get { return m_filledSquare; } }
        public override Texture2D HandCircle { get { return m_handCircle; } }
        public override Texture2D HollowCircle { get { return m_hollowCircle; } }
        public override Texture2D HollowFace0 { get { return m_hollowFace0; } }
        public override Texture2D HollowFace1 { get { return m_hollowFace1; } }
        public override Texture2D HollowFace2 { get { return m_hollowFace2; } }
        public override Texture2D HollowOneOval { get { return m_hollowOneOval; } }
        public override Texture2D HollowOval { get { return m_hollowOval; } }
        public override Texture2D HollowSquare { get { return m_hollowSquare; } }
        public override Texture2D HollowTwoOval { get { return m_hollowTwoOval; } }
        public override Texture2D LeftHand { get { return m_leftHand; } }
        public override Texture2D LessHollowSquare { get { return m_lessHollowSquare; } }
        public override Texture2D Microphone { get { return m_microphone; } }
        public override Texture2D MicrophoneHighlighted { get { return m_microphoneHighlighted; } }
        public override Texture2D MuteCircle { get { return m_muteCircle; } }
        public override Texture2D Pointer { get { return m_pointer; } }
        public override Texture2D PointingCircle { get { return m_pointingCircle; } }
        public override Texture2D QuarterFilledCircle { get { return m_quarterFilledCircle; } }
        public override Texture2D QuarterHollowCircle { get { return m_quarterHollowCircle; } }
        public override Texture2D RecordCircle { get { return m_recordCircle; } }
        public override Texture2D RewindCircle { get { return m_rewindCircle; } }
        public override Texture2D RightHand { get { return m_rightHand; } }
        public override Texture2D TinyDot { get { return m_tinyDot; } }
        public override Texture2D UnmuteCircle { get { return m_unmuteCircle; } }

        public override SpriteFont SpriteFont { get { return m_spriteFont; } }

        public override Texture2D[] Slides { get { return m_slides; } }

        public override Texture2D NewDynamicTexture(int width, int height)
        {
            return new Texture2D(m_device, width, height, mipMap: true, format: SurfaceFormat.Color);
        }
    }
}
