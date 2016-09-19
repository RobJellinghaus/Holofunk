////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;

namespace Holofunk
{
    /// <summary>
    /// Interface which can consume slice data.
    /// </summary>
    public interface Recorder<TTime, TValue>
        where TValue : struct
    {
        /// <summary>
        /// Record the given data; return true if this recorder is done after recording that data.
        /// </summary>
        bool Record(Moment now, Duration<TTime> duration, IntPtr data);

        /// <summary>
        /// Get the underlying stream, so it can be directly appended.
        /// </summary>
        DenseSampleFloatStream Stream { get; }
    }

    /// <summary>
    /// Interface which can consume slice data with an associated time.
    /// </summary>
    public interface TimedRecorder<TTime, TValue>
        where TValue : struct
    {
        /// <summary>
        /// Record a sliver from the given source at the given time; return true if this recorder is done.
        /// </summary>
        bool Record(Time<TTime> time, TValue[] source, int offset, int width, int stride, int height);
    }
}
