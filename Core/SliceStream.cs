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
    /// A stream of data, which can be Shut, at which point it acquires a floating-point ContinuousDuration.
    /// </summary>
    /// <remarks>
    /// Streams may be Open (in which case more data may be appended to them), or Shut (in which case they will
    /// not change again).
    /// 
    /// Streams may have varying internal policies for mapping time to underlying data, and may form hierarchies
    /// internally.
    /// 
    /// Streams have a SliverSize which denotes a larger granularity within the Stream's data.
    /// A SliverSize of N represents that each element in the Stream logically consists of N contiguous
    /// TValue entries in the stream's backing store; such a contiguous group is called a sliver.  
    /// A Stream with duration 1 has exactly one sliver of data. 
    /// </remarks>
    public abstract class SliceStream<TTime, TValue> : IDisposable
        where TValue : struct
    {
        /// <summary>
        /// The initial time of this Stream.
        /// </summary>
        /// <remarks>
        /// Note that this is discrete.  We don't consider a sub-sample's worth of error in the start time to be
        /// significant.  The loop duration, on the other hand, is iterated so often that error can and does
        /// accumulate; hence, ContinuousDuration, defined only once shut.
        /// </remarks>
        protected Time<TTime> m_initialTime;

        /// <summary>
        /// The floating-point duration of this stream; only valid once shut.
        /// </summary>
        /// <remarks>
        /// This allows streams to have lengths measured in fractional samples, which prevents roundoff error from
        /// causing clock drift when using odd BPM values and looping for long periods.
        /// </remarks>
        ContinuousDuration m_continuousDuration;

        /// <summary>
        /// As with Slice<typeparam name="TValue"></typeparam>, this defines the number of T values in an
        /// individual slice.
        /// </summary>
        public readonly int SliverSize;

        /// <summary>
        /// Is this stream shut?
        /// </summary>
        bool m_isShut;

        protected SliceStream(Time<TTime> initialTime, int sliverSize)
        {
            m_initialTime = initialTime;
            SliverSize = sliverSize;
        }

        public bool IsShut { get { return m_isShut; } }

        /// <summary>
        /// The starting time of this Stream.
        /// </summary>
        public Time<TTime> InitialTime { get { return m_initialTime; } }

        /// <summary>
        /// The floating-point-accurate duration of this stream; only valid once shut.
        /// </summary>
        public ContinuousDuration ContinuousDuration { get { return m_continuousDuration; } }

        /// <summary>
        /// Shut the stream; no further appends may be accepted.
        /// </summary>
        /// <param name="finalDuration">The possibly fractional duration to be associated with the stream;
        /// must be strictly equal to, or less than one sample smaller than, the discrete duration.</param>
        public virtual void Shut(ContinuousDuration finalDuration)
        {
            HoloDebug.Assert(!IsShut);
            m_isShut = true;
            m_continuousDuration = finalDuration;
        }

        /// <summary>
        /// Drop this stream and all its owned data.
        /// </summary>
        /// <remarks>
        /// This MAY need to become a ref counting structure if we want stream dependencies.
        /// </remarks>
        public abstract void Dispose();

        /// <summary>
        /// The sizeof(TValue) -- sadly this is not expressible in C# 4.5.
        /// </summary>
        /// <returns></returns>
        public abstract int SizeofValue();
    }
}
