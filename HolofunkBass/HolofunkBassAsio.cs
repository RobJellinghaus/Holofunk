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
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.AddOn.Vst;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>This class manages all interaction with BASS ASIO, including all ASIO and other callback procedures.</summary>
    /// <remarks>Communication between HolofunkBass and HolofunkBassAsio happens by means of the two
    /// synchronized queues.
    /// 
    /// All methods in this class, except for the constructor and StartAsio(), are intended to
    /// be called from the ASIO thread only.</remarks>
    public class HolofunkBassAsio : IDisposable
    {
        // Constants that work with my personal sound hardware.  TODO: channel config UI
        internal const int AsioDeviceId = 0;
        public const int AsioInputChannelId0 = 0;
        public const int AsioInputChannelId1 = 1;
        internal const int AsioOutputChannelId = 0;
        internal const bool IsInputChannel = true;
        internal const bool IsOutputChannel = false;

        // Are we using VST support?
        public const bool UseVst = true;

        // Are we using Turnado?
        public const bool UseTurnado = true;

        // Are we using CamelCrusher?
        public const bool UseCamelCrusher = false;

        /// <summary>
        /// minimal size of ASIO buffer that can keep up on Dell XPS12 + M-Audio Fast Track Pro,
        /// 24bit stereo @ 48Khz
        /// </summary>
        internal const int AsioBufferSize = 128;

        /// <summary>How many channels is stereo?</summary>
        internal const int StereoChannels = 2;

        // How many channels coming from our input?
        // (This is admittedly hardcoded to my personal Fast Track Pro setup with a mono
        // microphone.)
        // Also, this is static rather than const, as making it const produces all kinds of
        // unreachable code warnings.
        // It is public because it needs to be visible from the main Holofunk assembly, which
        // constructs the Clock, which keeps time by converting total ASIO input sample count 
        // into total timepoint count.
        public static readonly int InputChannelCount = 1;

        /// <summary>The HolofunkBass with which we communicate; we get its queues from here.</summary>
        readonly HolofunkBass m_bass;

        /// <summary>The pool of preallocated push streams and associated (maybe VST) effects</summary>
        BassStreamPool m_streamPool;

        /// <summary>The form to use as the initial handle for new VST embedded editors.</summary>
        Form m_baseForm;

        /// <summary>Mixer stream (HSTREAM)... yes, it's a bit Hungarian... consider wrapping BASS API to distinguish int-based types....</summary>
        /// <remarks>Only accessed by [AsioThread]</remarks>
        StreamHandle m_mixerHStream;

        /// <summary>ASIOPROC to feed mixer stream data to ASIO output</summary>
        ASIOPROC m_mixerToOutputAsioProc;

        /// <summary>newly added stream volume ratio</summary>
        internal const float TopMixVolume = 1f; // MAGIC NUMBER

        /// <summary>friggin' wav encoder!  want something done right, got to have BASS do it :-)</summary>
        EncoderWAV m_wavEncoder;

        int m_asioBufferPreferredSize;

        /// <summary>Input for channel 0</summary>
        HolofunkBassAsioInput m_input0;

        /// <summary>Input for channel 1</summary>
        HolofunkBassAsioInput m_input1;

        /// <summary>
        /// Average stopwatch-measured msec latency of MixerToOutputAsioProc.
        /// </summary>
        FloatAverager m_outputAsioProcAverageLatency;

        Stopwatch m_outputAsioProcStopwatch;

        internal HolofunkBassAsio(HolofunkBass bass)
        {
            m_bass = bass;

            m_outputAsioProcAverageLatency = new FloatAverager(20);
            m_outputAsioProcStopwatch = new Stopwatch();
            m_mixerToOutputAsioProc = new ASIOPROC(MixerToOutputAsioProc);
        }

        internal Duration<Sample> EarlierDuration { get { return HolofunkBass.EarlierDuration; } }

        internal Clock Clock { get { return m_bass.Clock; } }

        internal StreamHandle MixerHStream { get { return m_mixerHStream; } }

        internal BassStreamPool StreamPool { get { return m_streamPool; } }

        internal int StreamPoolFreeCount { get { return m_streamPool.FreeCount; } }

        /// <summary>ASIOPROC to feed mixer stream data to ASIO output buffer.</summary>
        /// <remarks>[AsioThread]</remarks>
        int MixerToOutputAsioProc(bool input, int channel, IntPtr buffer, int lengthBytes, IntPtr user)
        {
            if (m_outputAsioProcStopwatch.IsRunning) {
                m_outputAsioProcAverageLatency.Update(m_outputAsioProcStopwatch.ElapsedMilliseconds);
                m_outputAsioProcStopwatch.Restart();
            }
            else {
                m_outputAsioProcStopwatch.Start();
            }

            /*
            m_outputAsioProcCounter++;
            if (m_outputAsioProcCounter > 100) {
                Spam.Audio.WriteLine("Average output ASIO latency (msec): " + m_outputAsioProcAverageLatency.Average);
            }
             */

            HoloDebug.Assert(input == IsOutputChannel);
            HoloDebug.Assert(channel == AsioOutputChannelId);

            // ChannelGetData here is populating the output buffer for us.            
            // Stereo sample pairs x 4 bytes/sample = shift by 3.
            Clock.Advance(lengthBytes >> 3);

            int bytesAvailable = Bass.BASS_ChannelGetData((int)m_mixerHStream, buffer, lengthBytes);

            return bytesAvailable;
        }

        /// <summary>Initialize the base ASIO streams, and actually start ASIO running.</summary>
        /// <remarks>[MainThread]</remarks>
        internal void StartASIO()
        {
            // not playing anything via BASS, so don't need an update thread
            CheckError(Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATEPERIOD, 0));

            // setup BASS - "no sound" device but SampleFrequencyHz (default for ASIO)
            CheckError(Bass.BASS_Init(0, Clock.TimepointRateHz, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero));
            CheckError(BassAsio.BASS_ASIO_Init(AsioDeviceId, BASSASIOInit.BASS_ASIO_THREAD));

            CheckError(BassFx.LoadMe());

            if (UseVst) { 
                CheckError(BassVst.LoadMe());

                // testing scaffolding; retain for reference
                TempFrobBassVst();
            }

            // register all effects once VST is set up
            EffectRegistrar.RegisterAll();
            
            ////////////////////// DEVICE SETUP

            CheckError(BassAsio.BASS_ASIO_SetDevice(AsioDeviceId));

            BASS_ASIO_DEVICEINFO info = new BASS_ASIO_DEVICEINFO();
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (int n = 0; BassAsio.BASS_ASIO_GetDeviceInfo(n, info); n++) {
                b.AppendLine("device #" + n + ": " + info.ToString());
            }
            b.AppendLine("done");
            string all = b.ToString();
            b.Clear();

            for (int chan = 0; chan < 16; chan++) {
                BASS_ASIO_CHANNELINFO cinfo = BassAsio.BASS_ASIO_ChannelGetInfo(true, chan);
                if (cinfo != null) {
                    b.AppendLine(cinfo.ToString());
                }
                cinfo = BassAsio.BASS_ASIO_ChannelGetInfo(false, chan);
                if (cinfo != null) {
                    b.AppendLine(cinfo.ToString());
                }
            }
            all = b.ToString();

            CheckError(BassAsio.BASS_ASIO_SetRate(Clock.TimepointRateHz));

            BASS_ASIO_INFO asioInfo = BassAsio.BASS_ASIO_GetInfo();
            int inputLatency = BassAsio.BASS_ASIO_GetLatency(IsInputChannel);
            int outputLatency = BassAsio.BASS_ASIO_GetLatency(IsOutputChannel);

            m_asioBufferPreferredSize = 128; // B4CKIN: asioInfo.bufpref;

            ////////////////////// OUTPUT SETUP

            // converted away from BassAsioHandler, to enable better viewing of intermediate data
            // (and full control over API use, allocation, etc.)

            m_mixerHStream = (StreamHandle)BassMix.BASS_Mixer_StreamCreate(
                Clock.TimepointRateHz,
                StereoChannels,
                BASSFlag.BASS_MIXER_RESUME | BASSFlag.BASS_MIXER_NONSTOP | BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT);

            BASS_CHANNELINFO mixerInfo = new BASS_CHANNELINFO();
            CheckError(Bass.BASS_ChannelGetInfo((int)m_mixerHStream, mixerInfo));

            // connect to ASIO output channel
            CheckError(BassAsio.BASS_ASIO_ChannelEnable(IsOutputChannel, AsioOutputChannelId, m_mixerToOutputAsioProc, new IntPtr((int)m_mixerHStream)));

            // Join second mixer channel (right stereo channel).
            CheckError(BassAsio.BASS_ASIO_ChannelJoin(IsOutputChannel, 1, AsioOutputChannelId));

            CheckError(BassAsio.BASS_ASIO_ChannelSetFormat(IsOutputChannel, AsioOutputChannelId, BASSASIOFormat.BASS_ASIO_FORMAT_FLOAT));
            CheckError(BassAsio.BASS_ASIO_ChannelSetRate(IsOutputChannel, AsioOutputChannelId, Clock.TimepointRateHz));

            ////////////////////// INPUT SETUP

            CheckError(BassAsio.BASS_ASIO_SetDevice(HolofunkBassAsio.AsioDeviceId));

            m_input0 = new HolofunkBassAsioInput(this, 0, m_bass.AudioAllocator);
            m_input1 = new HolofunkBassAsioInput(this, 1, m_bass.AudioAllocator);

            ////////////////////// ASIO LAUNCH

            CheckError(BassAsio.BASS_ASIO_Start(m_asioBufferPreferredSize));

            // get the info again, see if latency has changed
            asioInfo = BassAsio.BASS_ASIO_GetInfo();
            inputLatency = BassAsio.BASS_ASIO_GetLatency(IsInputChannel);
            outputLatency = BassAsio.BASS_ASIO_GetLatency(IsOutputChannel);

        }

        internal bool IsRecordingWAV
        {
            get { return m_wavEncoder != null; }
        }

        internal void StartRecordingWAV()
        {
            HoloDebug.Assert(!IsRecordingWAV);

            m_wavEncoder = new EncoderWAV((int)m_mixerHStream);
            m_wavEncoder.InputFile = null; // use STDIN (the above channel)
            DateTime now = DateTime.Now;
            string date = string.Format("{0:D4}{1:D2}{2:D2}",
                now.Year,
                now.Month,
                now.Day);
            string dateTime = string.Format("{0}_{1:D2}{2:D2}{3:D2}",
                date,
                now.Hour,
                now.Minute,
                now.Second);
            string directory = Path.Combine(Directory.GetCurrentDirectory(), "Recordings", date);
            Directory.CreateDirectory(directory);
            m_wavEncoder.OutputFile = Path.Combine(directory, "holofunk_" + dateTime + ".wav");
            m_wavEncoder.Start(null, IntPtr.Zero, false);
        }

        internal void StopRecordingWAV()
        {
            HoloDebug.Assert(IsRecordingWAV);
            m_wavEncoder.Stop();
            m_wavEncoder.Dispose();
            m_wavEncoder = null;
        }

        internal void SetBaseForm(Form baseForm, int streamPoolCapacity)
        {
            m_streamPool = new BassStreamPool(streamPoolCapacity, baseForm);
            m_baseForm = baseForm;
        }

        internal Form BaseForm { get { return m_baseForm; } }

        // Purely test scaffolding for BassVst; retain for future reference.
        void TempFrobBassVst()
        {
            if (HolofunkBassAsio.UseTurnado) {
                StreamHandle handle = LoadVstPlugin("Turnado.dll");

                int programCount;
                while (true) {
                    programCount = BassVst.BASS_VST_GetProgramCount((int)handle);
                    Debug.WriteLine("Program count is {0}", programCount);

                    string[] programs = BassVst.BASS_VST_GetProgramNames((int)handle);
                    int index;
                    if ((index = ArrayIndexOf(programs, "AAA1")) != -1) {
                        BassVst.BASS_VST_SetProgram((int)handle, index);
                        break;
                    }
                    else {
                        BassVst.BASS_VST_SetProgram((int)handle, -1); // Sugar Bytes tech support says this swaps to prior bank
                    }
                }
            }

            if (HolofunkBassAsio.UseCamelCrusher) {
                LoadVstPlugin("CamelCrusher.dll");
            }
        }

        int ArrayIndexOf(string[] array, string s)
        {
            for (int i = 0; i < array.Length; i++) {
                if (array[i] == s) {
                    return i;
                }
            }
            return -1; 
        }

        private StreamHandle LoadVstPlugin(string dllName)
        {
            StreamHandle vstStream = (StreamHandle)BassVst.BASS_VST_ChannelSetDSP(
                0,
                Path.Combine(System.Environment.CurrentDirectory.ToString(), dllName),
                BASSVSTDsp.BASS_VST_DEFAULT,
                0);

            CheckError(Bass.BASS_ErrorGetCode() == BASSError.BASS_OK);

            int paramCount = BassVst.BASS_VST_GetParamCount((int)vstStream);
            BASS_VST_PARAM_INFO[] paramInfos = new BASS_VST_PARAM_INFO[paramCount];

            for (int paramIndex = 0; paramIndex < paramCount; paramIndex++) {
                paramInfos[paramIndex] = BassVst.BASS_VST_GetParamInfo((int)vstStream, paramIndex);
            }

            /*
            BASS_VST_INFO vstInfo = new BASS_VST_INFO();
            if (BassVst.BASS_VST_GetInfo((int)vstStream, vstInfo) && vstInfo.hasEditor) {
                // create a new System.Windows.Forms.Form
                Form f = new Form();
                f.Width = vstInfo.editorWidth + 4;
                f.Height = vstInfo.editorHeight + 34;
                f.Closing += (sender, e) => f_Closing(sender, e, vstStream);
                f.Text = vstInfo.effectName;
                f.Show();
                BassVst.BASS_VST_EmbedEditor((int)vstStream, f.Handle);
            }
             */
            return vstStream;
        }

        void f_Closing(object sender, System.ComponentModel.CancelEventArgs e, StreamHandle vstStream)
        {
            // unembed the VST editor
            BassVst.BASS_VST_EmbedEditor((int)vstStream, IntPtr.Zero);
        }

        internal void CheckError(bool ok)
        {
            if (!ok) {
                BASSError error = BassAsio.BASS_ASIO_ErrorGetCode();
                string str = error.ToString();

                BASSError error2 = Bass.BASS_ErrorGetCode();
                string str2 = error2.ToString();

                string s = str + str2;
            }
        }

        internal HolofunkBassAsioInput GetInput(int channel)
        {
            switch (channel) {
                case 0: return m_input0;
                case 1: return m_input1;
                default: HoloDebug.Assert(false, "Invalid channel"); return null;
            }
        }

        /// <summary>Add track to mixer.</summary>
        /// <remarks>[MainThread]
        /// 
        /// This is safe provided that m_samplePool is thread-safe (which it is) and 
        /// provided that trackSyncProc is prepared to be called immediately.</remarks>
        /// <param name="trackHStream">HSTREAM of the track to add.</param>
        /// <param name="trackUserData">Track's user data.</param>
        /// <param name="trackSync">the syncproc that will push more track data</param>
        internal void AddStreamToMixer(int trackHStream)
        {
            bool ok;

            BASS_CHANNELINFO trackInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo(trackHStream, trackInfo);
            BASS_CHANNELINFO mixerInfo = new BASS_CHANNELINFO();
            Bass.BASS_ChannelGetInfo((int)m_mixerHStream, mixerInfo);

            ok = BassMix.BASS_Mixer_StreamAddChannel(
                (int)m_mixerHStream,
                trackHStream,
                BASSFlag.BASS_MIXER_DOWNMIX | BASSFlag.BASS_MIXER_NORAMPIN);

            // try setting to 40% volume to reduce over-leveling
            ok = Bass.BASS_ChannelSetAttribute(trackHStream, BASSAttribute.BASS_ATTRIB_VOL, (float)TopMixVolume);

            ok = BassMix.BASS_Mixer_ChannelPlay(trackHStream);
        }

        /// <summary>Remove this stream from the mixer's inputs.</summary>
        /// <remarks>[MainThread] but hard to see how this could be racy given proper BASS multithread handling.
        /// Since no evidence of any issues there, will leave this alone.</remarks>
        internal void RemoveStreamFromMixer(StreamHandle trackHStream)
        {
            bool ok = BassMix.BASS_Mixer_ChannelRemove((int)trackHStream);
        }


        /// <summary>Given a beat amount, how many timepoints is it?</summary>
        int BeatsToTimepoints(double beats)
        {
            return (int)(beats / Clock.BeatsPerSecond * Clock.TimepointRateHz);
        }

        public float CpuUsage { get { return BassAsio.BASS_ASIO_GetCPU() + Bass.BASS_GetCPU(); } }

        #region IDisposable Members

        public void Dispose()
        {
            if (m_wavEncoder != null) {
                m_wavEncoder.Stop();
            }

            BassAsio.BASS_ASIO_Stop();
            Bass.BASS_Stop();

            // close bass
            if (UseVst) 
            {
                BassVst.FreeMe();
            }

            BassFx.FreeMe();

            BassAsio.BASS_ASIO_Free();
            Bass.BASS_Free();
        }

        #endregion
    }
}
