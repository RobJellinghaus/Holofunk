////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Vst;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A particular variety of sound effect that can apply to a Track.</summary>
    /// <remarks>Effects are instantiated per-Track (since some Effects have BASS-related state associated
    /// with them).</remarks>
    public abstract class HolofunkEffect
    {
        /// <summary>Apply this effect's parameters at the given moment to the given track.</summary>
        public abstract void Apply(ParameterMap parameters, Moment now);
    }

    /// <summary>Effects that are implemented by Bass.</summary>
    public abstract class BassEffect : HolofunkEffect
    {
        // The BASS stream handle to which the effect applies.
        readonly StreamHandle m_streamHandle;

        protected BassEffect(StreamHandle streamHandle)
        {
            m_streamHandle = streamHandle;
        }

        protected StreamHandle StreamHandle { get { return m_streamHandle; } }
    }

    public abstract class SimpleBassEffect : BassEffect
    {
        protected SimpleBassEffect(StreamHandle streamHandle)
            : base(streamHandle) 
        {
        }

        protected abstract BASSAttribute Attribute { get; }

        protected abstract ParameterDescription Parameter { get; }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            float f = parameters[Parameter].GetInterpolatedValue(now.Time);
            Bass.BASS_ChannelSetAttribute((int)StreamHandle, Attribute, f);
        }
    }

    public class PanEffect : SimpleBassEffect
    {
        static ParameterDescription s_parameter = new ParameterDescription(typeof(PanEffect), "balance", -1, 0, 0, 1, absolute: true);

        public static ParameterDescription Pan { get { return s_parameter; } }

        protected override ParameterDescription Parameter { get { return s_parameter; } }

        protected override BASSAttribute Attribute { get { return BASSAttribute.BASS_ATTRIB_PAN; } }

        PanEffect(StreamHandle streamHandle) : base(streamHandle) { }

        public static void Register()
        {
            AllEffects.Register(
                typeof(PanEffect),
                (streamHandle, form) => new PanEffect(streamHandle),
                new List<ParameterDescription>(new[] { s_parameter }));
        }
    }

    public class VolumeEffect : SimpleBassEffect
    {
        static ParameterDescription s_parameter = new ParameterDescription(typeof(VolumeEffect), "volume", 0, 0.5f, 0.7f, 1, absolute: true);

        public static ParameterDescription Volume { get { return s_parameter; } }

        protected override ParameterDescription Parameter { get { return s_parameter; } }

        protected override BASSAttribute Attribute { get { return BASSAttribute.BASS_ATTRIB_VOL; } }

        VolumeEffect(StreamHandle streamHandle) : base(streamHandle) { }

        public static void Register()
        {
            AllEffects.Register(
                typeof(VolumeEffect),
                (streamHandle, form) => new VolumeEffect(streamHandle), 
                new List<ParameterDescription>(new[] { s_parameter }));
        }
    }

    public abstract class BassFxEffect<TEffectArgs> : BassEffect
        where TEffectArgs : class
    {
        TEffectArgs m_fxArgs;
        FxHandle m_fxHandle;

        protected BassFxEffect(StreamHandle streamHandle, BASSFXType fxType, TEffectArgs effectArgs)
            : base(streamHandle)
        {
            m_fxArgs = effectArgs;

            m_fxHandle = (FxHandle)Bass.BASS_ChannelSetFX((int)streamHandle, fxType, 0);
            BASSError error = Bass.BASS_ErrorGetCode();
            HoloDebug.Assert(m_fxHandle != 0);

            Bass.BASS_FXSetParameters((int)m_fxHandle, (object)m_fxArgs);
        }

        protected void Apply()
        {
            Bass.BASS_FXSetParameters((int)m_fxHandle, (object)m_fxArgs);
        }

        protected TEffectArgs EffectArgs { get { return m_fxArgs; } }
    }

    public class EchoEffect : BassFxEffect<BASS_BFX_ECHO4>
    {
        public static ParameterDescription Wet = new ParameterDescription(typeof(EchoEffect), "echo wet", 0, 0, 0, 1f);
        //public static ParameterDescription Feedback = new ParameterDescription(typeof(EchoEffect), "echo feedback", 0, 0, 50, 100);

        EchoEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_BFX_ECHO4, new BASS_BFX_ECHO4(1, 0, 0.5f, 0.3f, false))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetMix = parameters[Wet].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(EchoEffect),
                (streamHandle, form) => new EchoEffect(streamHandle),
                new List<ParameterDescription>(new[] { Wet }));
        }
    }

    public class ReverbEffect : BassFxEffect<BASS_DX8_REVERB>
    {
        public static ParameterDescription Time = new ParameterDescription(typeof(ReverbEffect), "reverb time", 0.001f, 0.001f, 500f, 3000f);
        public static ParameterDescription Mix = new ParameterDescription(typeof(ReverbEffect), "reverb mix", -96f, -96f, -96f, 0f);

        ReverbEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_REVERB, new BASS_DX8_REVERB(0f, -96f, 500f, 0.001f))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fReverbTime = parameters[Time].GetInterpolatedValue(now.Time);
            EffectArgs.fReverbMix = (int)parameters[Mix].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(ReverbEffect),
                (streamHandle, form) => new ReverbEffect(streamHandle),
                new List<ParameterDescription>(new[] { Time, Mix }));
        }
    }

#if false
    public class EchoReverbEffect : BassFxEffect<BASS_BFX_ECHO4>
    {
        public static ParameterDescription Feedback = new ParameterDescription(typeof(EchoReverbEffect), "feedback", 0, 0, 0, 0.5f);
        public static ParameterDescription Delay = new ParameterDescription(typeof(EchoReverbEffect), "delay", 0.00001f, 0.0001f, 0.0001f, 1);

        EchoReverbEffect(StreamHandle streamHandle)
            // NOTE!!!!!  Delay of 0 becomes delay of 1 effectively!  TODO: REPORT THIS AS ECHO4 BUG
            : base(streamHandle, BASSFXType.BASS_FX_BFX_ECHO4, new BASS_BFX_ECHO4(1, 0, 0.0001f, 0, false))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fFeedback = parameters[Feedback].GetInterpolatedValue(now.Time);
            EffectArgs.fDelay = parameters[Delay].GetInterpolatedValue(now.Time);
            EffectArgs.fWetMix = (parameters[Feedback][now.Time] + parameters[Delay][now.Time]) / 2;

            Spam.Audio.WriteLine("Applying echo4 to stream " + (int)StreamHandle + ", feedback " + EffectArgs.fFeedback + " delay " + EffectArgs.fDelay + " wetmix " + EffectArgs.fWetMix);
            Apply();
        }

        static EchoReverbEffect()
        {
            AllEffects.Register(
                typeof(EchoReverbEffect),
                (streamHandle, form) => new EchoReverbEffect(streamHandle),
                new List<ParameterDescription>(new[] { Feedback, Delay }));
        }
    }
#endif


    public class FlangerEffect : BassFxEffect<BASS_BFX_FLANGER>
    {
        public static ParameterDescription WetDry = new ParameterDescription(typeof(FlangerEffect), "flanger wet/dry", 0, 0, 0, 1);

        FlangerEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_BFX_FLANGER, new BASS_BFX_FLANGER(0f, 0.02f))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetDry = parameters[WetDry].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(FlangerEffect),
                (streamHandle, form) => new FlangerEffect(streamHandle),
                new List<ParameterDescription>(new[] { WetDry }));
        }
    }
    
    public class ChorusFlangerEffect : BassFxEffect<BASS_BFX_CHORUS>
    {
        public static ParameterDescription Wet = new ParameterDescription(typeof(FlangerEffect), "wet mix", 0, 0, 0, 0);

        static BASS_BFX_CHORUS Flanger
        {
            get { BASS_BFX_CHORUS flanger = new BASS_BFX_CHORUS(); flanger.Preset_Flanger(); flanger.fDryMix = 1; flanger.fWetMix = 0; return flanger; }
        }

        ChorusFlangerEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_BFX_CHORUS, Flanger)
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetMix = parameters[Wet].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            /*
            AllEffects.Register(
                typeof(ChorusFlangerEffect),
                (streamHandle, form) => new ChorusFlangerEffect(streamHandle),
                new List<ParameterDescription>(new[] { Wet }));
             */
        }
    }

    public class ChorusEffect : BassFxEffect<BASS_BFX_CHORUS>
    {
        public static ParameterDescription Wet = new ParameterDescription(typeof(ChorusEffect), "chorus wet", 0, 0, 0, 0.25f);

        public static BASS_BFX_CHORUS Buhhhh
        {
            get { BASS_BFX_CHORUS chorus = new BASS_BFX_CHORUS(); chorus.Preset_WhoSayTTNManyVoices(); chorus.fWetMix = 0; chorus.fDryMix = 1; return chorus; }
        }

        ChorusEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_BFX_CHORUS, Buhhhh)
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetMix = parameters[Wet].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(ChorusEffect),
                (streamHandle, form) => new ChorusEffect(streamHandle),
                new List<ParameterDescription>(new[] { Wet }));
        }
    }

    public class HPFEffect : BassFxEffect<BASS_BFX_BQF>
    {
        public static ParameterDescription Frequency = new ParameterDescription(typeof(HPFEffect), "frequency cutoff", 10, 10, 10, 1500);

        HPFEffect(StreamHandle streamHandle) : base(streamHandle, BASSFXType.BASS_FX_BFX_BQF, 
            new BASS_BFX_BQF(BASSBFXBQF.BASS_BFX_BQF_HIGHPASS, 10, 0, 1, 0, 0, BASSFXChan.BASS_BFX_CHANALL))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fCenter = parameters[Frequency].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(HPFEffect),
                (streamHandle, form) => new HPFEffect(streamHandle),
                new List<ParameterDescription>(new[] { Frequency }));
        }
    }

    public class LPFEffect : BassFxEffect<BASS_BFX_BQF>
    {
        public static ParameterDescription Frequency = new ParameterDescription(typeof(LPFEffect), "frequency cutoff", 2000, 2000, 2000, 50);

        LPFEffect(StreamHandle streamHandle) : base(streamHandle, BASSFXType.BASS_FX_BFX_BQF, 
            new BASS_BFX_BQF(BASSBFXBQF.BASS_BFX_BQF_LOWPASS, 2000, 0, 1, 0, 0, BASSFXChan.BASS_BFX_CHANALL))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fCenter = parameters[Frequency].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(LPFEffect),
                (streamHandle, form) => new LPFEffect(streamHandle),
                new List<ParameterDescription>(new[] { Frequency }));
        }
    }

    public class CompressionEffect : BassFxEffect<BASS_BFX_COMPRESSOR2>
    {
        public static ParameterDescription Threshold = new ParameterDescription(typeof(CompressionEffect), "threshold", 0, 0, 0, -20);

        public static BASS_BFX_COMPRESSOR2 Hard
        {
            get { BASS_BFX_COMPRESSOR2 fx = new BASS_BFX_COMPRESSOR2(); fx.Preset_Default(); fx.fThreshold = 0f; return fx; }
        }

        CompressionEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_BFX_COMPRESSOR2, Hard)
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fThreshold = parameters[Threshold].GetInterpolatedValue(now.Time);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(CompressionEffect),
                (streamHandle, form) => new CompressionEffect(streamHandle),
                new List<ParameterDescription>(new[] { Threshold }));
        }
    }

    public class DistortionEffect : BassFxEffect<BASS_BFX_DISTORTION>
    {
        public static ParameterDescription Wet = new ParameterDescription(typeof(DistortionEffect), "wet mix", 0, 0, 0, 0f);

        public static BASS_BFX_DISTORTION Fuzzz
        {
            get { BASS_BFX_DISTORTION fuzzz = new BASS_BFX_DISTORTION(); fuzzz.Preset_HardDistortion(); fuzzz.fWetMix = 0; fuzzz.fDryMix = 0f; return fuzzz; }
        }

        DistortionEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_BFX_DISTORTION, Fuzzz)
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetMix = parameters[Wet].GetInterpolatedValue(now.Time);
            EffectArgs.fDryMix = 1f - (2f * EffectArgs.fWetMix);
            Apply();
        }

        public static void Register()
        {
            AllEffects.Register(
                typeof(DistortionEffect),
                (streamHandle, form) => new DistortionEffect(streamHandle),
                new List<ParameterDescription>(new[] { Wet }));
        }
    }

    public abstract class VstEffect : BassEffect
    {
        readonly StreamHandle m_vstStream;
        readonly Form m_baseForm;

        protected VstEffect(StreamHandle streamHandle, Form baseForm, string vstDllName)
            : base(streamHandle)
        {
            string path = Path.Combine(System.Environment.CurrentDirectory.ToString(), vstDllName);

            m_vstStream = (StreamHandle)BassVst.BASS_VST_ChannelSetDSP(
                (int)streamHandle,
                path,
                BASSVSTDsp.BASS_VST_DEFAULT,
                0);            

            BASSError err = Bass.BASS_ErrorGetCode();
            HoloDebug.Assert(err == BASSError.BASS_OK);

            m_baseForm = baseForm;
        }

        protected StreamHandle VstStream { get { return m_vstStream; } }

        protected void PopUI()
        {
            Action a = () => {
                BASS_VST_INFO vstInfo = new BASS_VST_INFO();
                if (BassVst.BASS_VST_GetInfo((int)m_vstStream, vstInfo) && vstInfo.hasEditor) {
                    // create a new System.Windows.Forms.Form
                    Form f = new Form();
                    f.Width = vstInfo.editorWidth + 4;
                    f.Height = vstInfo.editorHeight + 34;
                    f.Closing += (sender, e) => f_Closing(sender, e, m_vstStream);
                    f.Text = vstInfo.effectName;
                    f.Show();
                    BassVst.BASS_VST_EmbedEditor((int)m_vstStream, f.Handle);
                }
            };

            if (m_baseForm != null) {
                m_baseForm.BeginInvoke(a);
            }
        }

        void f_Closing(object sender, System.ComponentModel.CancelEventArgs e, StreamHandle vstStream)
        {
            // unembed the VST editor
            BassVst.BASS_VST_EmbedEditor((int)vstStream, IntPtr.Zero);
        }

        protected void SetParam(ParameterMap parameters, Moment now, ParameterDescription desc, int paramIndex)
        {
            float initialValue = BassVst.BASS_VST_GetParam((int)m_vstStream, paramIndex);

            float value = parameters[desc].GetInterpolatedValue(now.Time);
            if (Math.Abs(initialValue - value) > 0.001f) {
                bool ok = BassVst.BASS_VST_SetParam((int)m_vstStream, paramIndex, value);
                HoloDebug.Assert(ok);
                BASSError error = Bass.BASS_ErrorGetCode();
                HoloDebug.Assert(error == BASSError.BASS_OK);

                float rereadValue = BassVst.BASS_VST_GetParam((int)m_vstStream, paramIndex);
                HoloDebug.Assert(Math.Abs(rereadValue - value) < 0.001f);
            }
        }
    }

    public class TurnadoAAA1Effect : VstEffect
    {
        public static ParameterDescription Addverb = new ParameterDescription(typeof(TurnadoAAA1Effect), "Addverb", 0, 0, 0, 1);
        public static ParameterDescription Backgroundbreak = new ParameterDescription(typeof(TurnadoAAA1Effect), "BackBrk", 0, 0, 0, 1);
        public static ParameterDescription Ventilator = new ParameterDescription(typeof(TurnadoAAA1Effect), "Ventlatr", 0, 0, 0, 1);
        public static ParameterDescription Kompressor = new ParameterDescription(typeof(TurnadoAAA1Effect), "Kompress", 0, 0, 0, 1);
        public static ParameterDescription VowelFilter = new ParameterDescription(typeof(TurnadoAAA1Effect), "VowelFlt", 0, 0, 0, 1);
        public static ParameterDescription SliceWarz = new ParameterDescription(typeof(TurnadoAAA1Effect), "SliceWarz", 0, 0, 0, 1);
        public static ParameterDescription RingModulator = new ParameterDescription(typeof(TurnadoAAA1Effect), "RingMod", 0, 0, 0, 1);
        public static ParameterDescription Underwater = new ParameterDescription(typeof(TurnadoAAA1Effect), "Underwtr", 0, 0, 0, 1);

        public static ParameterDescription[] Parameters = new[] { Addverb, Backgroundbreak, Ventilator, Kompressor, VowelFilter, SliceWarz, RingModulator, Underwater };

        public TurnadoAAA1Effect(StreamHandle streamHandle, Form baseForm)
            : base(streamHandle, baseForm, "Turnado.dll")
        {
            int programCount;
            while (true) {
                programCount = BassVst.BASS_VST_GetProgramCount((int)VstStream);
                Debug.WriteLine("Program count is {0}", programCount);

                string program0 = BassVst.BASS_VST_GetProgramName((int)VstStream, 0);
                if (program0 == "AAA1") {
                    BassVst.BASS_VST_SetProgram((int)VstStream, 0);
                    break;
                }
                else {
                    BassVst.BASS_VST_SetProgram((int)VstStream, -1); // Sugar Bytes tech support says this swaps to prior bank
                }
            }

            int currentProgramIndex = BassVst.BASS_VST_GetProgram((int)VstStream);
            HoloDebug.Assert(currentProgramIndex == 0);
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            for (int i = 0; i < Parameters.Length; i++) {
                SetParam(parameters, now, Parameters[i], i);
            }

#if DEBUG
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Parameters.Length; i++) {
                float f = parameters[Parameters[i]].GetInterpolatedValue(now.Time);
                if (f > 0) {
                    sb.AppendFormat("{0}: {1} / ", Parameters[i].Name, f);
                }
            }
            if (sb.Length > 0) {
                //Spam.TopLine2 = sb.ToString();
            }
#endif
        }

        public static void Register()
        {
            if (HolofunkBassAsio.UseVst) {
                AllEffects.Register(
                    typeof(TurnadoAAA1Effect),
                    (streamHandle, form) => new TurnadoAAA1Effect(streamHandle, form),
                    new List<ParameterDescription>(Parameters));
            }
        }
    }

    public static class EffectRegistrar
    {
        public static void RegisterAll()
        {
            PanEffect.Register();
            VolumeEffect.Register();
            EchoEffect.Register();
            ReverbEffect.Register();
            FlangerEffect.Register();
            ChorusEffect.Register();
            HPFEffect.Register();
            LPFEffect.Register();
            CompressionEffect.Register();
            DistortionEffect.Register();
            if (HolofunkBassAsio.UseTurnado) {
                TurnadoAAA1Effect.Register();
            }
        }
    }

#if NO_ROUGH_RIDER
    public class RoughRiderEffect : VstEffect
    {
        public static ParameterDescription Sensitivity = new ParameterDescription(typeof(RoughRiderEffect), "Sensitivity", 0, 0, 0, 1);

        public RoughRiderEffect(StreamHandle streamHandle, Form baseForm)
            : base(streamHandle, baseForm, "RoughRider.dll")
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            SetParam(parameters, now, Sensitivity, 0);
        }

        static RoughRiderEffect()
        {
            if (HolofunkBassAsio.UseVst) { 
                AllEffects.Register(
                    typeof(RoughRiderEffect),
                    (streamHandle, form) => new RoughRiderEffect(streamHandle, form),
                    new List<ParameterDescription>(new[] { Sensitivity }));
            }
        }
    }
#endif
}
