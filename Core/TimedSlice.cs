using System.Collections.Generic;

namespace Holofunk.Core
{
    /// <summary>
    /// A slice with an absolute initial time associated with it.
    /// </summary>
    /// <remarks>
    /// In the case of BufferedStreams, the first TimedSlice's InitialTime will be the InitialTime
    /// of the stream itself.
    /// </remarks>
    struct TimedSlice<TTime, TValue>
        where TValue : struct
    {
        internal readonly Time<TTime> InitialTime;
        internal readonly Slice<TTime, TValue> Slice;

        internal TimedSlice(Time<TTime> startTime, Slice<TTime, TValue> slice)
        {
            InitialTime = startTime;
            Slice = slice;
        }

        internal Interval<TTime> Interval { get { return new Interval<TTime>(InitialTime, Slice.Duration); } }

        internal class Comparer : IComparer<TimedSlice<TTime, TValue>>
        {
            internal static Comparer Instance = new Comparer();

            public int Compare(TimedSlice<TTime, TValue> x, TimedSlice<TTime, TValue> y)
            {
                if (x.InitialTime < y.InitialTime) {
                    return -1;
                }
                else if (x.InitialTime > y.InitialTime) {
                    return 1;
                }
                else {
                    return 0;
                }
            }
        }

    }
}
