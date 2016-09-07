////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Holofunk.Core
{
    /// <summary>
    /// A stream that contains sparsely located slices, each with a time.
    /// </summary>
    public abstract class SparseSliceStream<TTime, TValue> : SliceStream<TTime, TValue>
        where TValue : struct
    {
        /// <summary>
        /// Underlying stream that stores the actual data.
        /// </summary>
        /// <remarks>
        /// Indexed by Frame, regardless of this sparse stream's TTime dimension.
        /// </remarks>
        readonly DenseSliceStream<Frame, TValue> m_innerStream;

        /// <summary>
        /// The slices making up the buffered data itself.
        /// </summary>
        /// <remarks>
        /// The Duration of each TimedSlice here is exactly 1.  This denotes that each TimedSlice here is
        /// really a sparse point containing exactly one Slice, not a dense interval of multiple slices.
        /// </remarks>
        readonly List<Time<TTime>> m_times = new List<Time<TTime>>();

        /// <summary>
        /// Temporary space for, e.g., the IntPtr Append method.
        /// </summary>
        readonly TValue[] m_tempBuffer;

        readonly int m_maxBufferedFrameCount;

        public SparseSliceStream(
            Time<TTime> initialTime,
            DenseSliceStream<Frame, TValue> innerStream,
            int maxBufferedFrameCount = 0)
            : base(initialTime, innerStream.SliverSize)
        {
            m_innerStream = innerStream;
            m_maxBufferedFrameCount = maxBufferedFrameCount;
            m_tempBuffer = new TValue[SliverSize];
        }

        /// <summary>
        /// Append the given amount of data marshalled from the pointer P.
        /// </summary>
        public void Append(Time<TTime> absoluteTime, IntPtr p)
        {
            HoloDebug.Assert(!IsShut);
            HoloDebug.Assert(TimeIsAfterLast(absoluteTime));

            m_innerStream.Append(1, p);
            m_times.Add(absoluteTime);

            Trim();
        }



        bool TimeIsAfterLast(Time<TTime> absoluteTime)
        {
            // all appended times must be after initial time
            if (absoluteTime < InitialTime) {
                return false;
            }

            if (m_times.Count == 0) {
                return true;
            }
            else {
                return m_times[m_times.Count - 1] < absoluteTime;
            }
        }

        /// <summary>
        /// Append this slice's data, by copying it into this stream's private buffers.
        /// </summary>
        public void Append(Time<TTime> absoluteTime, Slice<Frame, TValue> source)
        {
            HoloDebug.Assert(!IsShut);
            HoloDebug.Assert(source.Duration == 1);
            HoloDebug.Assert(TimeIsAfterLast(absoluteTime));

            m_innerStream.Append(source);
            m_times.Add(absoluteTime);

            Trim();
        }

        public void AppendSliver(Time<TTime> absoluteTime, TValue[] source, int startOffset, int width, int stride, int height)
        {
            HoloDebug.Assert(!IsShut);

            if (m_times.Count > 0 && m_times[m_times.Count - 1] == absoluteTime) {
                // BUGBUG? Is this a sign of major slowdown?
                return;
            }

            HoloDebug.Assert(TimeIsAfterLast(absoluteTime));

            m_innerStream.AppendSliver(source, startOffset, width, stride, height);
            m_times.Add(absoluteTime);

            Trim();
        }

        /// <summary>
        /// Trim off any times prior to the ones we want to keep.
        /// </summary>
        /// <remarks>
        /// The inner stream needs to do its own trimming.
        /// </remarks>
        void Trim()
        {
            if (m_maxBufferedFrameCount > 0) {
                while (m_times.Count > m_maxBufferedFrameCount) {
                    m_times.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// Copy a single Sliver selected (by some policy) from the sourceInterval.
        /// </summary>
        /// <remarks>
        /// Returns true if a copy was made, false if there were no frames in sourceInterval.
        /// </remarks>
        public bool CopyTo(Time<TTime> sourceTime, IntPtr p)
        {
            sourceTime = MapTime(sourceTime);

            for (int i = m_times.Count - 1; i >= 0; i--) {
                if (m_times[i] < sourceTime) { 
                    m_innerStream.CopyTo(new Interval<Frame>(i, 1), p);
                    return true;
                }
            }

            // wrap around if not found
            if (m_times.Count > 0) {
                m_innerStream.CopyTo(new Interval<Frame>(m_times.Count - 1, 1), p);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the closest sliver to this time.
        /// </summary>
        /// <remarks>
        /// Will be returned as a Slice with duration 1.
        /// </remarks>
        public Slice<Frame, TValue> GetClosestSliver(Time<TTime> sourceTime)
        {
            sourceTime = MapTime(sourceTime);

            for (int i = m_times.Count - 1; i >= 0; i--) {
                if (m_times[i] < sourceTime) {
                    return m_innerStream.GetNextSliceAt(new Interval<Frame>(i, 1));
                }
            }

            // if didn't find closest by time going backwards, just return the last available one
            // (since this means you need to wrap around the loop)
            if (m_times.Count > 0) {
                return m_innerStream.GetNextSliceAt(new Interval<Frame>(m_times.Count - 1, 1));
            }
            else {
                return Slice<Frame, TValue>.Empty;
            }
        }

        Time<TTime> MapTime(Time<TTime> sourceTime)
        {
            if (IsShut) {
                int numLoops = (int)((sourceTime - InitialTime) / (float)ContinuousDuration);
                sourceTime = sourceTime - (Duration<TTime>)(numLoops * (float)ContinuousDuration);
            }

            return sourceTime;
        }

        public override void Dispose()
        {
            m_innerStream.Dispose();
        }
    }
}
