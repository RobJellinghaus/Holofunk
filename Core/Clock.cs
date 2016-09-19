////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////


namespace Holofunk.Core
{
    /// <summary>Tracks the current time (driven from the ASIO input sample DSP), and 
    /// converts it to seconds and beats.</summary>
    /// <remarks>Since the ASIO thread is fundamentally driving the time, the current clock
    /// reading is subject to change out from under the UI thread.  So the Clock
    /// hands out immutable Moment instances, which represent the time at the moment the
    /// clock was asked.  Moments in turn can be converted to timepoint-counts, 
    /// seconds, and beats, consistently and without racing.</remarks>
    public class Clock
    {
        /// <summary>The rate of sound measurements (individual sample data points) per second.</summary>
        public const int TimepointRateHz = 44100;

        // The current BPM of this Clock.
        float m_beatsPerMinute;

        // The beats per MEASURE.  e.g. 3/4 time = 3 beats per measure.
        // TODO: make this actually mean something; it is only partly implemented right now.
        readonly int m_beatsPerMeasure;

        /// <summary>
        /// The number of samples since the beginning of Holofunk.
        /// </summary>
        Time<Sample> m_time;

        /// <summary>
        /// How many input channels are there?
        /// </summary>
        readonly int m_inputChannelCount;

        /// <summary>What is the floating-point duration of a beat, in samples?</summary>
        // This will be a non-integer value if the BPM does not exactly divide the sample rate.
        ContinuousDuration m_continuousBeatDuration;

        const double TicksPerSecond = 10 * 1000 * 1000;

        public Clock(float beatsPerMinute, int beatsPerMeasure, int inputChannelCount)
        {
            m_beatsPerMinute = beatsPerMinute;
            m_beatsPerMeasure = beatsPerMeasure;
            m_inputChannelCount = inputChannelCount;

            CalculateBeatDuration();
        }

        void CalculateBeatDuration()
        {
            m_continuousBeatDuration = (ContinuousDuration)(((float)TimepointRateHz * 60f) / m_beatsPerMinute);
        }

        /// <summary>Advance this clock.</summary>
        public void Advance(Duration<Sample> duration)
        {
            m_time += duration;
        }

        /// <summary>The beats per minute of this clock.</summary>
        /// <remarks>This is the most useful value for humans to control and see, and in fact pretty much all 
        /// time in the system is derived from this.  This value can only currently be changed when
        /// no tracks exist.</remarks>
        public float BPM 
        { 
            get 
            { 
                return m_beatsPerMinute; 
            }
            set 
            { 
                m_beatsPerMinute = value;
                CalculateBeatDuration();
            } 
        }

        public Moment Now
        {
            // Stereo channel, hence twice as many samples as timepoints.
            get 
            { 
                return Time(m_time); 
            }
        }

        public Moment Time(Time<Sample> time)
        {
            return new Moment(time, this);
        }

        public double BeatsPerSecond
        {
            get { return ((double)m_beatsPerMinute) / 60.0; }
        }

        public ContinuousDuration ContinuousBeatDuration
        {
            get { return m_continuousBeatDuration; }
        }

        public int BeatsPerMeasure
        {
            get { return m_beatsPerMeasure; }
        }
    }

    /// <summary>Moments are immutable points in time, that can be converted to various
    /// time measurements (timepoint-count, second, beat).</summary>
    public struct Moment
    {
        public readonly Time<Sample> Time;

        public readonly Clock Clock;

        internal Moment(Time<Sample> time, Clock clock)
        {
            Time = time;
            Clock = clock;
        }

        /// <summary>Approximately how many seconds?</summary>
        public double Seconds { get { return ((double)Time) / Clock.TimepointRateHz; } }

        /// <summary>Exactly how many beats?</summary>
        public double Beats { get { return ((double)Time) / ((double)Clock.ContinuousBeatDuration); } }

        /// <summary>Exactly how many complete beats?</summary>
        /// <remarks>Beats are represented by ints as it's hard to justify longs; 2G beats = VERY LONG TRACK</remarks>
        public int CompleteBeats { get { return (int)(Beats + Epsilon); } }

        private const double Epsilon = 0.0001; // empirically seen some Beats values come too close to this

        /// <summary>What fraction of a beat?</summary>
        public double FractionalBeat { get { return Beats - CompleteBeats; } }

        public override string ToString()
        {
            return "Moment[" + Time + "]";
        }
    }
}
