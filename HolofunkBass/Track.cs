////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>An object that couples a stream of audio data with a BASS track.</summary>
    public class Track 
    {
        /// <summary>Audio stream.</summary>
        /// <remarks>Not readonly because this is nulled out on disposal.</remarks>
        DenseSampleFloatStream m_audioStream;

        /// <summary>HolofunkBass we were created on</summary>
        HolofunkBass m_holofunkBass;

        /// <summary>ID of the track (also gets used as user data in track callback)</summary>
        readonly int m_id;

        /// <summary>SYNCPROC to push more data into this stream</summary>
        readonly SYNCPROC m_syncProc;

        /// <summary>The BassStream we are using (and reserving).</summary>
        BassStream m_bassStream;

        /// <summary>handle of the synchronizer attached to m_trackHStream</summary>
        StreamHandle m_trackHSync;

        /// <summary>
        /// Local time is based on the Now when the track started playing, and advances strictly
        /// based on what the Track has pushed.
        /// </summary>
        /// <remarks>
        /// This is because BASS doesn't have rock-solid consistency between the MixerToOutputAsioProc
        /// and the Track's SyncProc.  We would expect that if at time T we push N samples from this
        /// track, that the SyncProc would then be called back at exactly time T+N.  However, this is not
        /// in fact the case -- there is some nontrivial variability of +/- one sample buffer.  So we
        /// use a local time to avoid requiring this exact timing behavior from BASS.
        /// 
        /// The previous code just pushed one sample buffer after another based on their indices; it paid
        /// no attention to "global time" at all.
        /// </remarks>
        Time<Sample> m_localTime;

        /// <summary>Peak level meter.</summary>
        DSP_PeakLevelMeter m_plmTrack;
        int m_levelL;
        int m_levelR;

        Action<IntPtr, int> m_putDataAction;

        /// <summary>Parameters of the track's effects</summary>
        ParameterMap m_parameters;

        /// <summary>Is the track muted?</summary>
        bool m_isMuted;

        /// <summary>Create a Track.</summary>
        /// <param name="bass">The BASS helper object; for coordination on disposal.</param>
        /// <param name="id">Unique ID of this track.</param>
        /// <param name="clock">ASIO-driven clock.</param>
        /// <param name="now">ASIO time.</param>
        /// <param name="startingParameters">A starting set of parameters, which must have been already copied (unshared).</param>
        public Track(HolofunkBass bass, int id, ParameterMap startingParameters, DenseSampleFloatStream audioStream)
        {
            m_holofunkBass = bass;
            m_id = id;
            m_audioStream = audioStream;
            m_bassStream = m_holofunkBass.StreamPoolReserve();

            m_syncProc = new SYNCPROC(SyncProc);

            // cache this delegate to avoid delegate creation on each slice push
            m_putDataAction = PutData;

            m_parameters = AllEffects.CreateParameterMap();
            m_parameters.ShareAll(startingParameters);
        }

        /// <summary>The Parameters of this track.</summary>
        public ParameterMap Parameters { get { return m_parameters; } }

        /// <summary>ASIO sync callback.</summary>
        /// <remarks>[AsioThread]</remarks>
        void SyncProc(int handle, int channel, int data, IntPtr user)
        {
            // push the next sample to the stream
            if (data == 0) { // means "stalled"
                PushNextSliceToAsioMixerStream();
            }
        }

        void PushNextSliceToAsioMixerStream()
        {
            lock (this) { // [AsioThread] protect against concurrent disposal
                if (m_audioStream == null) {
                    return;
                }

                // get up to one second
                Slice<Sample, float> longest = m_audioStream.GetNextSliceAt(new Interval<Sample>(m_localTime, Clock.TimepointRateHz));

                Spam.Audio.WriteLine("Track #" + m_id + " PushNextSliceToAsioMixer: at " + m_localTime + ", pushing " + longest);

                // per http://www.un4seen.com/forum/?topic=12912.msg89978#msg89978
                // we ignore the return value from StreamPutData since it queues anything in excess,
                // so we don't need to track any underflows
                longest.RawAccess(m_putDataAction);

                m_localTime += longest.Duration;
            }
        }

        void PutData(IntPtr intptr, int byteCount)
        {
            // reset stream position so no longer ended.
            // this is as per http://www.un4seen.com/forum/?topic=12965.msg90332#msg90332
            Bass.BASS_ChannelSetPosition((int)m_bassStream.PushStream, 0, BASSMode.BASS_POS_BYTES);

            Bass.BASS_StreamPutData((int)m_bassStream.PushStream, intptr, byteCount | (int)BASSStreamProc.BASS_STREAMPROC_END);
        }

        /// <summary>Start playing this track.</summary>
        /// <remarks>[MainThread] but we have seen no troubles from the cross-thread operations here --
        /// the key point is that m_syncProc is ready to go as soon as the ChannelSetSync happens.</remarks>
        public void StartPlaying(Moment now)
        {
            HoloDebug.Assert(m_audioStream.IsShut); 

            // BUGBUG: This next line was observed in one apparently hung stack.  Need to ask whether is safe to 
            // BUGBUG: re-entrantly call into ASIO from an input proc.  Also need to research which thread
            // BUGBUG: StartPlaying was called on in the prior code.
            // BUGBUG: m_bassStream.Effects.Apply(Parameters, now);

            m_holofunkBass.AddStreamToMixer(StreamHandle);

            m_trackHSync = (StreamHandle)BassMix.BASS_Mixer_ChannelSetSync(
                (int)StreamHandle,
                BASSSync.BASS_SYNC_MIXTIME | BASSSync.BASS_SYNC_END, 
                0, // ignored
                m_syncProc,
                new IntPtr(0));

            // connect peak level meter to input push stream
            m_plmTrack = new DSP_PeakLevelMeter((int)StreamHandle, 0);
            m_plmTrack.Notification += new EventHandler(Plm_Track_Notification);

            m_localTime = now.Time;

            PushNextSliceToAsioMixerStream();
        }

        /// <summary>Effects use this property to invoke the appropriate channel attribute setting, etc.</summary>
        internal StreamHandle StreamHandle { get { return m_bassStream.PushStream; } }

        /// <summary>Set whether the track is muted or not.</summary>
        /// <param name="vol"></param>
        public void SetMuted(bool isMuted)
        {
            m_isMuted = isMuted;

            // TODO: make this handle time properly in the muting; Moment.Start is pure hack here
            Bass.BASS_ChannelSetAttribute(
                (int)StreamHandle, 
                BASSAttribute.BASS_ATTRIB_VOL, 
                isMuted ? 0f : m_parameters[VolumeEffect.Volume].GetInterpolatedValue(0));
        }

        void Plm_Track_Notification(object sender, EventArgs e)
        {
            if (m_plmTrack != null) {
                m_levelL = m_plmTrack.LevelL;
                m_levelR = m_plmTrack.LevelR;
            }
        }

        public float LevelRatio 
        { 
            get { return m_holofunkBass.CalculateLevelRatio(m_levelL, m_levelR); } 
        }

        /// <summary>[MainThread] Update all effects applied to this track.</summary>
        /// <remarks>This should arguably be on the ASIO thread, but it doesn't seem likely that we can afford
        /// the ASIO speed hit.</remarks>
        public void UpdateEffects(Moment now)
        {
            m_bassStream.Effects.Apply(Parameters, now);
        }

        public void ResetEffects(Moment now)
        {
            Parameters.ResetToDefault();
            UpdateEffects(now);
        }

        /// <summary>
        /// [KinectThread] Dispose this
        /// </summary>
        /// <param name="now"></param>
        public void Dispose(Moment now)
        {
            // TODO: free the audio stream, once we are confident we're not going to collide with a syncproc

            Parameters.ResetToDefault();
            m_bassStream.Effects.Apply(Parameters, now);

            m_holofunkBass.RemoveStreamFromMixer(StreamHandle);

            m_holofunkBass.Free(m_bassStream);

            lock (this) { // [KinectThread] ensure we don't stomp m_audioStream out from under the SYNCPROC
                m_audioStream.Dispose();
                m_audioStream = null;
            }
        }
    }
}
