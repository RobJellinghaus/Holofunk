////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    /// <summary>
    /// Sample identifies Times based on audio sample counts. 
    /// </summary>
    public class Sample 
    {
    }

    /// <summary>
    /// Frame identifies Times based on video frame counts. 
    /// </summary>
    public class Frame 
    {
    }

    /// <summary>
    /// Time parameterized on some underlying measurement.
    /// </summary>
    public struct Time<TTime>
    {
        readonly long m_time;

        public Time(long time)
        {
            m_time = time;
        }

        public override string ToString()
        {
            return "T[" + (long)this + "]";
        }

        public static Time<TTime> Min(Time<TTime> first, Time<TTime> second)
        {
            return new Time<TTime>(Math.Min(first, second));
        }

        public static Time<TTime> Max(Time<TTime> first, Time<TTime> second)
        {
            return new Time<TTime>(Math.Max(first, second));
        }

        public static implicit operator long(Time<TTime> time)
        {
            return time.m_time;
        }

        public static implicit operator Time<TTime>(long time)
        {
            return new Time<TTime>(time);
        }
        public static bool operator <(Time<TTime> first, Time<TTime> second)
        {
            return (long)first < (long)second;
        }

        public static bool operator >(Time<TTime> first, Time<TTime> second)
        {
            return (long)first > (long)second;
        }

        public static bool operator ==(Time<TTime> first, Time<TTime> second)
        {
            return (long)first == (long)second;
        }

        public override bool Equals(object obj)
        {
            return obj is Time<TTime>
                && ((Time<TTime>)obj) == this;
        }

        public override int GetHashCode()
        {
            return (int)m_time;
        }

        public static bool operator !=(Time<TTime> first, Time<TTime> second)
        {
            return (long)first != (long)second;
        }

        public static bool operator <=(Time<TTime> first, Time<TTime> second)
        {
            return (long)first <= (long)second;
        }

        public static bool operator >=(Time<TTime> first, Time<TTime> second)
        {
            return (long)first >= (long)second;
        }

        public static Duration<TTime> operator -(Time<TTime> first, Time<TTime> second)
        {
            return new Duration<TTime>((long)first - (long)second);
        }

        public static Time<TTime> operator -(Time<TTime> first, Duration<TTime> second)
        {
            return new Time<TTime>((long)first - (long)second);
        }
    }

    /// <summary>
    /// A distance between two Times.
    /// </summary>
    /// <typeparam name="TTime"></typeparam>
    public struct Duration<TTime>
    {
        readonly long m_count;

        public Duration(long count)
        {
            HoloDebug.Assert(count >= 0);
            m_count = count;
        }

        public override string ToString()
        {
            return "D[" + (long)this + "]";
        }

        public static implicit operator long(Duration<TTime> offset)
        {
            return offset.m_count;
        }

        public static implicit operator Duration<TTime>(long value)
        {
            return new Duration<TTime>(value);
        }

        public static Duration<TTime> Min(Duration<TTime> first, Duration<TTime> second)
        {
            return new Duration<TTime>(Math.Min(first, second));
        }

        public static Duration<TTime> operator +(Duration<TTime> first, Duration<TTime> second)
        {
            return new Duration<TTime>((long)first + (long)second);
        }

        public static Duration<TTime> operator -(Duration<TTime> first, Duration<TTime> second)
        {
            return new Duration<TTime>((long)first - (long)second);
        }

        public static Duration<TTime> operator /(Duration<TTime> first, int second)
        {
            return new Duration<TTime>((long)first / second);
        }

        public static bool operator <(Duration<TTime> first, Duration<TTime> second)
        {
            return (long)first < (long)second;
        }

        public static bool operator >(Duration<TTime> first, Duration<TTime> second)
        {
            return (long)first > (long)second;
        }

        public static bool operator <=(Duration<TTime> first, Duration<TTime> second)
        {
            return (long)first <= (long)second;
        }

        public static bool operator >=(Duration<TTime> first, Duration<TTime> second)
        {
            return (long)first >= (long)second;
        }

        public static bool operator ==(Duration<TTime> first, Duration<TTime> second)
        {
            return (long)first == (long)second;
        }

        public static bool operator !=(Duration<TTime> first, Duration<TTime> second)
        {
            return (long)first != (long)second;
        }

        public static Time<TTime> operator +(Time<TTime> first, Duration<TTime> second)
        {
            return new Time<TTime>((long)first + (long)second);
        }

        public override bool Equals(object obj)
        {
            return obj is Duration<TTime>
                && ((Duration<TTime>)obj) == this;
        }

        public override int GetHashCode()
        {
            return (int)m_count;
        }
    }

    /// <summary>
    /// An interval, defined as a start time and a duration (aka length).
    /// </summary>
    /// <remarks>
    /// Empty intervals semantically have no InitialTime, and no distinction should be made between empty
    /// intervals based on InitialTime.</remarks>
    /// <typeparam name="TTime"></typeparam>
    public struct Interval<TTime>
    {
        public readonly Time<TTime> InitialTime;
        public readonly Duration<TTime> Duration;
        readonly bool m_isInitialized;

        public Interval(Time<TTime> initialTime, Duration<TTime> duration)
        {
            HoloDebug.Assert(duration >= 0);

            InitialTime = initialTime;
            Duration = duration;
            m_isInitialized = true;
        }

        public override string ToString()
        {
            return "I[" + InitialTime + ", " + Duration + "]";
        }

        public static Interval<TTime> Empty { get { return new Interval<TTime>(0, 0); } }

        public bool IsInitialized { get { return m_isInitialized; } }

        public bool IsEmpty
        {
            get { return Duration == 0; }
        }

        public Interval<TTime> SubintervalStartingAt(Duration<TTime> offset)
        {
            Debug.Assert(offset <= Duration);
            return new Interval<TTime>(InitialTime + offset, Duration - offset);
        }

        public Interval<TTime> SubintervalOfDuration(Duration<TTime> duration)
        {
            Debug.Assert(duration <= Duration);
            return new Interval<TTime>(InitialTime, duration);
        }

        public Interval<TTime> Intersect(Interval<TTime> other)
        {
            Time<TTime> intersectionStart = Time<TTime>.Max(InitialTime, other.InitialTime);
            Time<TTime> intersectionEnd = Time<TTime>.Min(InitialTime + Duration, other.InitialTime + other.Duration);

            if (intersectionEnd < intersectionStart) {
                return Interval<TTime>.Empty;
            }
            else {
                return new Interval<TTime>(intersectionStart, intersectionEnd - intersectionStart);
            }
        }

        public bool Contains(Time<TTime> time)
        {
            if (IsEmpty) {
                return false;
            }

            return InitialTime <= time
                && (InitialTime + Duration) > time;
        }
    }
}
