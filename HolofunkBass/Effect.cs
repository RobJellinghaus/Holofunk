////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011 by Rob Jellinghaus.                             //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>The extent of an effect's application.</summary>
    public enum EffectGranularity
    {
        /// <summary>This effect applies to individual loops (likely because it is implemented directly
        /// in BASS, with low per-loop overhead).</summary>
        Loop,

        /// <summary>This effect applies to an entire region of the Holofunk interface (likely because
        /// it is implemented in an intense VST that we can't instantiate arbitrary numbers of).</summary>
        Region
    }

    /// <summary>A particular variety of sound effect that can apply to a Track.</summary>
    /// <remarks>Effects are instantiated per-Track (since some Effects have BASS-related state associated
    /// with them).</remarks>
    public abstract class Effect
    {
        /// <summary>Apply this effect's parameters at the given moment to the given track.</summary>
        public abstract void Apply(ParameterMap parameters, Moment now);

        public abstract EffectGranularity Granularity { get; }
    }

    /// <summary>Effects that are implemented by Bass.</summary>
    public abstract class BassEffect : Effect
    {
        // The BASS stream handle to which the effect applies.
        readonly StreamHandle m_streamHandle;

        protected const EffectGranularity GRANULARITY = EffectGranularity.Loop;

        protected BassEffect(StreamHandle streamHandle)
        {
            m_streamHandle = streamHandle;
        }

        protected StreamHandle StreamHandle { get { return m_streamHandle; } }

        public override EffectGranularity Granularity
        {
            // since BASS implements this, it's trivial to set it per-loop
            get { return GRANULARITY; }
        }
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
            float f = parameters.Get(Parameter).GetInterpolatedValue(now);
            Bass.BASS_ChannelSetAttribute((int)StreamHandle, Attribute, f);
        }
    }

    public class PanEffect : SimpleBassEffect
    {
        static ParameterDescription s_parameter = new ParameterDescription(typeof(PanEffect), GRANULARITY, "balance", -1, 0, 1);

        public static ParameterDescription Pan { get { return s_parameter; } }

        protected override ParameterDescription Parameter { get { return s_parameter; } }

        protected override BASSAttribute Attribute { get { return BASSAttribute.BASS_ATTRIB_PAN; } }

        PanEffect(StreamHandle streamHandle) : base(streamHandle) { }

        static PanEffect()
        {
            AllEffects.Register(
                typeof(PanEffect),
                streamHandle => new PanEffect(streamHandle),
                new List<ParameterDescription>(new[] { s_parameter }));
        }
    }

    public class VolumeEffect : SimpleBassEffect
    {
        static ParameterDescription s_parameter = new ParameterDescription(typeof(VolumeEffect), GRANULARITY, "volume", 0, 1, 1);

        public static ParameterDescription Volume { get { return s_parameter; } }

        protected override ParameterDescription Parameter { get { return s_parameter; } }

        protected override BASSAttribute Attribute { get { return BASSAttribute.BASS_ATTRIB_VOL; } }

        VolumeEffect(StreamHandle streamHandle) : base(streamHandle) { }

        static VolumeEffect()
        {
            AllEffects.Register(
                typeof(VolumeEffect), 
                streamHandle => new VolumeEffect(streamHandle), 
                new List<ParameterDescription>(new[] { s_parameter }));
        }
    }

    public abstract class BassDX8Effect<TEffectArgs> : BassEffect
        where TEffectArgs : class
    {
        TEffectArgs m_fxArgs;
        FxHandle m_fxHandle;

        protected BassDX8Effect(StreamHandle streamHandle, BASSFXType fxType, TEffectArgs effectArgs)
            : base(streamHandle)
        {
            m_fxArgs = effectArgs;

            m_fxHandle = (FxHandle)Bass.BASS_ChannelSetFX((int)streamHandle, fxType, 0);
            BASSError error = Bass.BASS_ErrorGetCode();
            Debug.Assert(m_fxHandle != 0);

            Bass.BASS_FXSetParameters((int)m_fxHandle, (object)m_fxArgs);
        }

        protected void Apply()
        {
            Bass.BASS_FXSetParameters((int)m_fxHandle, (object)m_fxArgs);
        }

        protected TEffectArgs EffectArgs { get { return m_fxArgs; } }
    }

    public class EchoEffect : BassDX8Effect<BASS_DX8_ECHO>
    {
        public static ParameterDescription WetDry = new ParameterDescription(typeof(EchoEffect), GRANULARITY, "echo wet/dry", 0, 0, 100);
        public static ParameterDescription Feedback = new ParameterDescription(typeof(EchoEffect), GRANULARITY, "echo feedback", 0, 50, 100);

        EchoEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_ECHO, new BASS_DX8_ECHO(0f, 50f, 333f, 333f, false))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetDryMix = (int)parameters.Get(WetDry).GetInterpolatedValue(now);
            EffectArgs.fFeedback = parameters.Get(Feedback).GetInterpolatedValue(now);
            Apply();
        }

        static EchoEffect()
        {
            AllEffects.Register(
                typeof(EchoEffect),
                streamHandle => new EchoEffect(streamHandle),
                new List<ParameterDescription>(new[] { WetDry, Feedback }));
        }
    }

    public class ReverbEffect : BassDX8Effect<BASS_DX8_REVERB>
    {
        public static ParameterDescription Time = new ParameterDescription(typeof(ReverbEffect), GRANULARITY, "reverb time", 0.001f, 500f, 3000f);
        public static ParameterDescription Mix = new ParameterDescription(typeof(ReverbEffect), GRANULARITY, "reverb mix", -96f, -96f, 0f);

        ReverbEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_REVERB, new BASS_DX8_REVERB(0f, -96f, 500f, 0.001f))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fReverbTime = parameters.Get(Time).GetInterpolatedValue(now);
            EffectArgs.fReverbMix = (int)parameters.Get(Mix).GetInterpolatedValue(now);
            Apply();
        }

        static ReverbEffect()
        {
            AllEffects.Register(
                typeof(ReverbEffect),
                streamHandle => new ReverbEffect(streamHandle),
                new List<ParameterDescription>(new[] { Time, Mix }));
        }
    }

    public class FlangerEffect : BassDX8Effect<BASS_DX8_FLANGER>
    {
        public static ParameterDescription WetDry = new ParameterDescription(typeof(FlangerEffect), GRANULARITY, "flanger wet/dry", 0, 0, 100);
        public static ParameterDescription Depth = new ParameterDescription(typeof(ReverbEffect), GRANULARITY, "flanger depth", 0, 25, 100);

        FlangerEffect(StreamHandle streamHandle)
            : base(streamHandle, BASSFXType.BASS_FX_DX8_FLANGER, new BASS_DX8_FLANGER(0f, 25f, 80f, 8f, 1, 30f, BASSFXPhase.BASS_FX_PHASE_NEG_90))
        {
        }

        public override void Apply(ParameterMap parameters, Moment now)
        {
            EffectArgs.fWetDryMix = parameters.Get(WetDry).GetInterpolatedValue(now);
            EffectArgs.fDepth = (int)parameters.Get(Depth).GetInterpolatedValue(now);
            Apply();
        }

        static FlangerEffect()
        {
            AllEffects.Register(
                typeof(FlangerEffect),
                streamHandle => new FlangerEffect(streamHandle),
                new List<ParameterDescription>(new[] { WetDry, Depth }));
        }
    }
}
