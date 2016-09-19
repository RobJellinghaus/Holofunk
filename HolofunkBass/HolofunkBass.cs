////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Windows.Forms;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>Manager object for almost all interaction with the BASS library.</summary>
    /// <remarks>Manages recording, track creation, mixing, and generally all other top-level functions.
    /// 
    /// The term "timepoint" is used here to mean "a point in time at which a sample was taken."
    /// Since our channels are stereo, there are technically two mono samples per timepoint.
    /// Therefore "sample count" is a bit ambiguous -- are those mono samples or stereo samples?
    /// To avoid confusion, we use "timepoint" analogously to "sample" whenever we are calculating
    /// purely based on time.
    /// 
    /// This object is created by the main thread, and manages communication from the main thread
    /// to the ASIO thread, as well as exposing responses from the ASIO thread.  There are two
    /// communication queues: one from the main thread to ASIO, and one the other way.  This
    /// ensures that the main Holofunk game thread can communicate with the ASIO thread in an
    /// orderly and synchronized manner; the ASIO thread, via the HolofunkBassAsio object,
    /// manages essentially all the ASIO state.</remarks>
    public class HolofunkBass : IDisposable
    {
        public static Duration<Sample> EarlierDuration;

        readonly HolofunkBassAsio m_asio;
        readonly Clock m_clock;
        readonly BufferAllocator<float> m_audioAllocator;

        public HolofunkBass(Clock clock, BufferAllocator<float> audioAllocator)
        {
            m_clock = clock;
            m_audioAllocator = audioAllocator;
            m_asio = new HolofunkBassAsio(this);
        }

        #region Properties

        public Clock Clock { get { return m_clock; } }

        public int StreamPoolFreeCount { get { return m_asio.StreamPoolFreeCount; } }

        public BufferAllocator<float> AudioAllocator { get { return m_audioAllocator; } }

        public bool IsRecordingWAV { get { return m_asio.IsRecordingWAV; } }

        #endregion

        public void SetBaseForm(Form baseForm, int streamPoolCapacity)
        {
            m_asio.SetBaseForm(baseForm, streamPoolCapacity);
        }

        /// <summary>Set up and start the ASIO subsystem running.</summary>
        /// <remarks>[MainThread], obviously.</remarks>
        public void StartASIO()
        {
            m_asio.StartASIO();
        }

        // Add this HSTREAM to our mixer.  (Called by Track.)
        internal void AddStreamToMixer(StreamHandle streamHandle)
        {
            m_asio.AddStreamToMixer((int)streamHandle);
        }

        // Remove this HSTREAM from our mixer.  (Called by Track.)
        internal void RemoveStreamFromMixer(StreamHandle streamHandle)
        {
            m_asio.RemoveStreamFromMixer(streamHandle);
        }

        public void StartRecordingWAV()
        {
            m_asio.StartRecordingWAV();
        }

        public void StopRecordingWAV()
        {
            m_asio.StopRecordingWAV();
        }

        public BassStream Reserve()
        {
            return m_asio.StreamPool.Reserve();
        }

        public void Free(BassStream bassStream)
        {
            m_asio.StreamPool.Free(bassStream);
        }

        /// <summary>The level value we show as maximum volume when rendering.</summary>
        /// <remarks>Chosen purely by feel with Rob Jellinghaus's specific hardware....</remarks>
        const int MaxLevel = 7000;

        internal float CalculateLevelRatio(int inputLevelL, int inputLevelR)
        {
            int maxLevel = Math.Max(inputLevelL, inputLevelR) / 3; 

            return (float)Math.Min(1f, (Math.Log(maxLevel, 2) / Math.Log(MaxLevel, 2)));
        }

        /// <summary>Normalize this level to the interval [0, 1], clamping at MaxLevel.</summary>
        public float LevelRatio(int channel)
        {
            HolofunkBassAsioInput input = m_asio.GetInput(channel);
            return CalculateLevelRatio(input.InputLevelL, input.InputLevelR);
        }

        public float CpuUsage { get { return m_asio.CpuUsage; } }

        public void AddRecorder(Moment now, int channel, Recorder<Sample, float> recorder)
        {
            m_asio.GetInput(channel).AddRecorder(now, recorder);
        }

        /// <summary>Update the effect parameters on the input channel.</summary>
        /// <remarks>[MainThread]
        /// 
        /// This calls directly through to m_asio WITHOUT queueing.  This is because the
        /// effect parameter setting is thread-safe, and the set of parameters and effects
        /// will not change during this operation.</remarks>
        public void UpdateMicrophoneParameters(int channel, ParameterMap set, Moment now)
        {
            m_asio.GetInput(channel).UpdateMicrophoneParameters(set, now);
        }

        public BassStream StreamPoolReserve()
        {
            return m_asio.StreamPool.Reserve();
        }

        public void StreamPoolFree(BassStream bs)
        {
            m_asio.StreamPool.Free(bs);
        }

        public void Dispose()
        {
            m_asio.Dispose();
        }
    }
}
