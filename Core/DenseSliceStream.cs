////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;

namespace Holofunk.Core
{
    /// <summary>
    /// A stream of data, accessed through consecutive, densely sequenced Slices.
    /// </summary>
    /// <remarks>
    /// The methods which take IntPtr arguments cannot be implemented generically in .NET; it is not possible to
    /// take the address of a generic T[] array.  The type must be specialized to some known primitive type such
    /// as float or byte.  This is done in the leaf subclasses in the Stream hierarchy.  All other operations are
    /// generically implemented.
    /// </remarks>
    public abstract class DenseSliceStream<TTime, TValue> : SliceStream<TTime, TValue>
        where TValue : struct
    {
        /// <summary>
        /// The discrete duration of this stream; always exactly equal to the sum of the durations of all contained slices.
        /// </summary>
        protected Duration<TTime> m_discreteDuration;

        /// <summary>
        /// The mapper that converts absolute time into relative time for this stream.
        /// </summary>
        IntervalMapper<TTime> m_intervalMapper;

        protected DenseSliceStream(Time<TTime> initialTime, int sliverSize)
            : base(initialTime, sliverSize)
        {
        }

        /// <summary>
        /// The discrete duration of this stream; always exactly equal to the number of timepoints appended.
        /// </summary>
        public Duration<TTime> DiscreteDuration { get { return m_discreteDuration; } }

        public Interval<TTime> DiscreteInterval { get { return new Interval<TTime>(InitialTime, DiscreteDuration); } }

        IntervalMapper<TTime> IntervalMapper
        {
            get { return m_intervalMapper; }
            set { m_intervalMapper = value; }
        }

        /// <summary>
        /// Shut the stream; no further appends may be accepted.
        /// </summary>
        /// <param name="finalDuration">The possibly fractional duration to be associated with the stream;
        /// must be strictly equal to, or less than one sample smaller than, the discrete duration.</param>
        public override void Shut(ContinuousDuration finalDuration)
        {
            HoloDebug.Assert(!IsShut);
            // Should always have as many samples as the rounded-up finalDuration.
            // The precise time matching behavior is that a loop will play either Math.Floor(finalDuration)
            // or Math.Ceiling(finalDuration) samples on each iteration, such that it remains perfectly in
            // time with finalDuration's fractional value.  So, a shut loop should have DiscreteDuration
            // equal to rounded-up ContinuousDuration.
            HoloDebug.Assert((int)Math.Ceiling((double)finalDuration) == (int)DiscreteDuration);
            base.Shut(finalDuration);
        }

        /// <summary>
        /// Get a reference to the next slice at the given time, up to the given duration if possible, or the
        /// largest available slice if not.
        /// </summary>
        /// <remarks>
        /// If the interval IsEmpty, return an empty slice.
        /// </remarks>
        public abstract Slice<TTime, TValue> GetNextSliceAt(Interval<TTime> sourceInterval);


        /// <summary>
        /// Append contiguous data; this must not be shut yet.
        /// </summary>
        public abstract void Append(Slice<TTime, TValue> source);

        /// <summary>
        /// Append a rectangular, strided region of the source array.
        /// </summary>
        /// <remarks>
        /// The width * height must together equal the sliverSize.
        /// </remarks>
        public abstract void AppendSliver(TValue[] source, int startOffset, int width, int stride, int height);

        /// <summary>
        /// Append the given duration's worth of slices from the given pointer.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="duration"></param>
        public abstract void Append(Duration<TTime> duration, IntPtr p);

        /// <summary>
        /// Copy the given interval of this stream to the destination.
        /// </summary>
        public abstract void CopyTo(Interval<TTime> sourceInterval, DenseSliceStream<TTime, TValue> destination);

        /// <summary>
        /// Copy the given interval of this stream to the destination.
        /// </summary>
        public abstract void CopyTo(Interval<TTime> sourceInterval, IntPtr destination);
    }
}
