////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>This class manages state related to a single input channel.</summary>
    public class HolofunkBassAsioInput
    {
        #region Fields

        /// <summary>Which ASIO channel does this input object track?</summary>
        readonly int m_asioChannel;

        /// <summary>Our parent ASIO object.</summary>
        readonly HolofunkBassAsio m_bassAsio;

        /// <summary>Bounded stream for recording the last N samples</summary>
        DenseSliceStream<Sample, float> m_recentPastStream;

        /// <summary>Measuring peak levels</summary>
        DSP_PeakLevelMeter m_plmRec;
        int m_levelL;
        int m_levelR;

        /// <summary>ASIOPROC to feed ASIO input channel data to input push stream</summary>
        ASIOPROC m_inputToInputPushStreamAsioProc;

        // Push stream for input data
        StreamHandle m_inputPushStream;

        // set of effects we are applying to the input push stream
        EffectSet m_inputPushEffects;

        /// <summary>Hook for processing incoming audio from ASIO; this copies it into m_currentRecordingTrack.</summary>
        /// <remarks>Only accessed by [AsioThread]</remarks>
        DSPPROC m_inputDspProc;

        /// <summary>The list of current recorders (if any).</summary>
        List<Recorder<Sample, float>> m_recorders = new List<Recorder<Sample, float>>();

        #endregion

        internal HolofunkBassAsioInput(HolofunkBassAsio bassAsio, int asioChannel, BufferAllocator<float> audioAllocator)
        {
            m_bassAsio = bassAsio;
            m_asioChannel = asioChannel;

            // buffer one second's worth of audio; that will always be more than we need to look at
            m_recentPastStream = new DenseSampleFloatStream(
                default(Time<Sample>), 
                audioAllocator, 
                1, // input channels are mono
                maxBufferedDuration: Clock.TimepointRateHz);

            m_inputToInputPushStreamAsioProc = new ASIOPROC(InputToInputPushStreamAsioProc);

            // create input push stream; this receives data pushed from ASIO's input, and feeds the mixer
            m_inputPushStream = (StreamHandle)Bass.BASS_StreamCreatePush(
                Clock.TimepointRateHz,
                HolofunkBassAsio.InputChannelCount,
                BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT,
                new IntPtr(m_asioChannel));

            // connect to ASIO input channel
            CheckError(BassAsio.BASS_ASIO_ChannelEnable(
                HolofunkBassAsio.IsInputChannel,
                m_asioChannel,
                m_inputToInputPushStreamAsioProc,
                new IntPtr(m_asioChannel)));

            // join right channel if we have more than one input channel
            // (this is not generalized for >stereo)
            if (HolofunkBassAsio.InputChannelCount == 2) {
                CheckError(BassAsio.BASS_ASIO_ChannelJoin(HolofunkBassAsio.IsInputChannel, 1, m_asioChannel));
            }

            // set format and rate of input channel
            CheckError(BassAsio.BASS_ASIO_ChannelSetFormat(HolofunkBassAsio.IsInputChannel, m_asioChannel, BASSASIOFormat.BASS_ASIO_FORMAT_FLOAT));
            CheckError(BassAsio.BASS_ASIO_ChannelSetRate(HolofunkBassAsio.IsInputChannel, m_asioChannel, Clock.TimepointRateHz));

            // add input push stream to mixer
            CheckError(BassMix.BASS_Mixer_StreamAddChannel(
                (int)m_bassAsio.MixerHStream,
                (int)m_inputPushStream,
                BASSFlag.BASS_MIXER_DOWNMIX | BASSFlag.BASS_MIXER_NORAMPIN));

            // set up the input effects (aka microphone effects)
            m_inputPushEffects = AllEffects.CreateLoopEffectSet(m_inputPushStream, m_bassAsio.BaseForm);
            
            // connect peak level meter to input push stream
            m_plmRec = new DSP_PeakLevelMeter((int)m_inputPushStream, 0);
            m_plmRec.Notification += new EventHandler(Plm_Rec_Notification);

            // Register DSPPROC handler for input channel.  Make sure to hold the DSPPROC itself.
            // See documentation for BassAsioHandler.InputChannel
            m_inputDspProc = new DSPPROC(InputDspProc);

            // set up our recording DSP -- priority 10 hopefully means "run first first first!"
            CheckError(Bass.BASS_ChannelSetDSP((int)m_inputPushStream, m_inputDspProc, new IntPtr(0), 10) != 0);
        }

        #region Properties

        Clock Clock { get { return m_bassAsio.Clock; } }

        internal HolofunkBassAsio HolofunkBassAsio { get { return m_bassAsio; } }

        internal int AsioChannel { get { return m_asioChannel; } }

        public int InputLevelL { get { return m_levelL; } }
        public int InputLevelR { get { return m_levelR; } }

        #endregion

        void CheckError(bool ok)
        {
            m_bassAsio.CheckError(ok);
        }

        // ASIOPROC to feed ASIO input data to input push stream.
        // [AsioThread]
        int InputToInputPushStreamAsioProc(bool input, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {            
            HoloDebug.Assert(input == HolofunkBassAsio.IsInputChannel);
            HoloDebug.Assert(channel == m_asioChannel);

            // Make sure we got a multiple of four, just for sanity's sake.
            // Note that we sometimes do NOT get a multiple of EIGHT -- in other words,
            // a stereo channel may not contain an even number of samples on each push.
            // Go figure.
            HoloDebug.Assert((lengthBytes & 0x3) == 0);

            int ret = Bass.BASS_StreamPutData((int)m_inputPushStream, buffer, lengthBytes);
            HoloDebug.Assert(ret != -1);

            return lengthBytes;
        }


        /// <summary>Apply these parameters to the input (microphone) channel.</summary>
        internal void UpdateMicrophoneParameters(ParameterMap set, Moment now)
        {
            m_inputPushEffects.Apply(set, now);
        }

        /// <summary>Consume incoming audio via our DSP function, and copy it into our recorders (if any).</summary>
        /// <remarks>[AsioThread], reads m_sampleTarget written from main thread.
        /// 
        /// Note that this iterates backwards over the recorders, as the only thing which can cause a recorder to stop
        /// recording is to reach its time limit, and that only happens as a result of calling Record in this exact
        /// method.  So a Recorder may remove itself while this list is being iterated; this is considered fine.</remarks>
        void InputDspProc(int handle, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            if (lengthBytes == 0 || buffer == IntPtr.Zero) {
                return;
            }

            // should be multiple of 4 since it's a float stream
            HoloDebug.Assert((lengthBytes & 0x3) == 0);

            Moment now = Clock.Now;

            // always keep the recycled sample target up to date, may start recording with a new hand at any instant
            lock (this) { // [AsioThread] ensure no races on m_recentPaststream, etc., when receiving incoming data
                m_recentPastStream.Append(lengthBytes >> 2, buffer);

                int recorderCount = m_recorders.Count;
                for (int i = recorderCount - 1; i >= 0; i--) {
                    lock (m_recorders[i]) { // [AsioThread] ensure m_recorders.Record doesn't race with concurrent KinectThread StopRecordingAtNextBeat
                        bool done = m_recorders[i].Record(now, lengthBytes >> 2, buffer);
                        if (done) {
                            // lock it to prevent collision with a concurrent add
                            m_recorders.RemoveAt(i);
                        }
                    }
                }
            }
        }

        // [AsioThread], writes state variables read from main thread
        void Plm_Rec_Notification(object sender, EventArgs e)
        {
            if (m_plmRec != null) {
                m_levelL = m_plmRec.LevelL;
                m_levelR = m_plmRec.LevelR;
            }
        }

        /// <summary>
        /// Add a new recorder.
        /// </summary>
        /// <remarks>
        /// [KinectThread]
        /// </remarks>
        internal void AddRecorder(Moment now, Recorder<Sample, float> recorder)
        {
            // We need to copy the data out of the recent past, and add the new recorder, atomically
            // with respect to the racing AsioThread running its InputDspProc.
            lock (this) { // [KinectThread] ensure consistent state update, consistent pulling from m_recentPastStream
                m_recentPastStream.CopyTo(new Interval<Sample>(now.Time - m_bassAsio.EarlierDuration, m_bassAsio.EarlierDuration), recorder.Stream);
                m_recorders.Add(recorder);
            }
        }
    }
}
