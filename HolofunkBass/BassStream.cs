////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

namespace Holofunk
{
    /// <summary>
    /// A BASS push stream together with the effects affecting it.
    /// </summary>
    /// <remarks>
    /// Holofunk precreates and pools these in order to avoid expensive BASS_VST_ChannelSetDSP calls
    /// from the BASS thread.
    /// </remarks>
    public class BassStream
    {
        readonly int m_idUniqueWithinPool;

        /// <summary>BASS HSTREAM to the stream of this track</summary>
        readonly StreamHandle m_streamHandle;

        /// <summary>The effects applied to this track's push stream</summary>
        readonly EffectSet m_effects;

        public BassStream(int idUniqueWithinPool, Form baseForm)
        {
            m_idUniqueWithinPool = idUniqueWithinPool;

            m_streamHandle = (StreamHandle)Bass.BASS_StreamCreatePush(
                Clock.TimepointRateHz,
                HolofunkBassAsio.InputChannelCount,
                BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_DECODE,
                new IntPtr(m_idUniqueWithinPool));

            m_effects = AllEffects.CreateLoopEffectSet(m_streamHandle, baseForm);
        }

        public int IdUniqueWithinPool { get { return m_idUniqueWithinPool; } }
        public StreamHandle PushStream { get { return m_streamHandle; } }
        public EffectSet Effects { get { return m_effects; } }
    }
}
