////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Holofunk.Core
{
    /// <summary>
    /// A stream that buffers some amount of data in memory.
    /// </summary>
    public abstract class BufferedSliceStream<TTime, TValue> : DenseSliceStream<TTime, TValue>
        where TValue : struct
    {
        /// <summary>
        /// Allocator for buffer management.
        /// </summary>
        readonly BufferAllocator<TValue> m_allocator;

        /// <summary>
        /// The slices making up the buffered data itself.
        /// </summary>
        /// <remarks>
        /// The InitialTime of each entry in this list must exactly equal the InitialTime + Duration of the
        /// previous entry; in other words, these are densely arranged in time.
        /// </remarks>
        readonly List<TimedSlice<TTime, TValue>> m_data = new List<TimedSlice<TTime, TValue>>();

        /// <summary>
        /// The maximum amount that this stream will buffer while it is open; more appends will cause
        /// earlier data to be dropped.  If 0, no buffering limit will be enforced.
        /// </summary>
        readonly Duration<TTime> m_maxBufferedDuration;

        /// <summary>
        /// Temporary space for, e.g., the IntPtr Append method.
        /// </summary>
        readonly Buf<TValue> m_tempBuffer = new Buf<TValue>(-1, new TValue[1024]); // -1 id = temp buf

        /// <summary>
        /// This stream holds onto an entire buffer and copies data into it when appending.
        /// </summary>
        Slice<TTime, TValue> m_remainingFreeBuffer;

        /// <summary>
        /// The mapper that converts absolute time into relative time for this stream.
        /// </summary>
        IntervalMapper<TTime> m_intervalMapper;

        /// <summary>
        /// Action to copy IntPtr data into a Slice.
        /// </summary>
        /// <remarks>
        /// Since .NET offers no way to marshal into an array of generic type, we can't express this
        /// function cleanly except in a specialized method defined in a subclass.
        /// </remarks>
        readonly Action<IntPtr, Slice<TTime, TValue>> m_copyIntPtrToSliceAction;

        /// <summary>
        /// Action to copy Slice data into an IntPtr.
        /// </summary>
        /// <remarks>
        /// Since .NET offers no way to marshal into an array of generic type, we can't express this
        /// function cleanly except in a specialized method defined in a subclass.
        /// </remarks>
        readonly Action<Slice<TTime, TValue>, IntPtr> m_copySliceToIntPtrAction;

        /// <summary>
        /// Action to obtain an IntPtr directly on a Slice's data, and invoke another action with that IntPtr.
        /// </summary>
        /// <remarks>
        /// Again, since .NET does not allow taking the address of a generic array, we must use a
        /// specialized implementation wrapped in this generic signature.
        /// </remarks>
        readonly Action<Slice<TTime, TValue>, Action<IntPtr, int>> m_rawSliceAccessAction;

        readonly bool m_useContinuousLoopingMapper = false;

        public BufferedSliceStream(
            Time<TTime> initialTime,
            BufferAllocator<TValue> allocator,
            int sliverSize,
            Action<IntPtr, Slice<TTime, TValue>> copyIntPtrToSliceAction,
            Action<Slice<TTime, TValue>, IntPtr> copySliceToIntPtrAction,
            Action<Slice<TTime, TValue>, Action<IntPtr, int>> rawSliceAccessAction,
            Duration<TTime> maxBufferedDuration = default(Duration<TTime>),
            bool useContinuousLoopingMapper = false)
            : base(initialTime, sliverSize)
        {
            m_allocator = allocator;
            m_copyIntPtrToSliceAction = copyIntPtrToSliceAction;
            m_copySliceToIntPtrAction = copySliceToIntPtrAction;
            m_rawSliceAccessAction = rawSliceAccessAction;
            m_maxBufferedDuration = maxBufferedDuration;
            m_useContinuousLoopingMapper = useContinuousLoopingMapper;

            // as long as we are appending, we use the identity mapping
            // TODO: support delay mapping
            m_intervalMapper = new IdentityIntervalMapper<TTime, TValue>(this);
        }

        public override string ToString()
        {
            return "BufferedSliceStream[" + InitialTime + ", " + DiscreteDuration + "]";
        }

        void EnsureFreeBuffer()
        {
            if (m_remainingFreeBuffer.IsEmpty()) {
                Buf<TValue> chunk = m_allocator.Allocate();
                m_remainingFreeBuffer = new Slice<TTime, TValue>(
                    chunk,
                    0,
                    (chunk.Data.Length / SliverSize),
                    SliverSize);
            }
        }

        public override void Shut(ContinuousDuration finalDuration)
        {
            base.Shut(finalDuration);
            // swap out our mappers, we're looping now
            if (m_useContinuousLoopingMapper) {
                m_intervalMapper = new LoopingIntervalMapper<TTime, TValue>(this);
            }
            else {
                m_intervalMapper = new SimpleLoopingIntervalMapper<TTime, TValue>(this);
            }

#if SPAMAUDIO
            foreach (TimedSlice<TTime, TValue> timedSlice in m_data) {
                Spam.Audio.WriteLine("BufferedSliceStream.Shut: next slice time " + timedSlice.InitialTime + ", slice " + timedSlice.Slice);
            }
#endif
        }

        /// <summary>
        /// Return a temporary buffer slice of the given duration or the max temp buffer size, whichever is lower.
        /// </summary>
        /// <param name="duration"></param>
        /// <returns></returns>
        Slice<TTime, TValue> TempSlice(Duration<TTime> duration)
        {
            Duration<TTime> maxDuration = m_tempBuffer.Data.Length / SliverSize;
            return new Slice<TTime, TValue>(
                m_tempBuffer,
                0,
                duration > maxDuration ? maxDuration : duration,
                SliverSize);
        }

        /// <summary>
        /// Append the given amount of data marshalled from the pointer P.
        /// </summary>
        public override void Append(Duration<TTime> duration, IntPtr p)
        {
            HoloDebug.Assert(!IsShut);

            while (duration > 0) {
                Slice<TTime, TValue> tempSlice = TempSlice(duration);

                m_copyIntPtrToSliceAction(p, tempSlice);
                Append(tempSlice);
                duration -= tempSlice.Duration;
            }
            m_discreteDuration += duration;
        }

        /// <summary>
        /// Append this slice's data, by copying it into this stream's private buffers.
        /// </summary>
        public override void Append(Slice<TTime, TValue> source)
        {
            HoloDebug.Assert(!IsShut);

            // Try to keep copying source into m_remainingFreeBuffer
            while (!source.IsEmpty()) {
                EnsureFreeBuffer();

                // if source is larger than available free buffer, then we'll iterate
                Slice<TTime, TValue> originalSource = source;
                if (source.Duration > m_remainingFreeBuffer.Duration) {
                    source = source.Subslice(0, m_remainingFreeBuffer.Duration);
                }

                // now we know source can fit
                Slice<TTime, TValue> dest = m_remainingFreeBuffer.SubsliceOfDuration(source.Duration);
                source.CopyTo(dest);

                // dest may well be adjacent to the previous slice, if there is one, since we may
                // be appending onto an open chunk.  So here is where we coalesce this, if so.
                dest = InternalAppend(dest);

                // and update our loop variables
                source = originalSource.SubsliceStartingAt(source.Duration);

                Trim();
            }
        }

        /// <summary>
        /// Internally append this slice (which must be allocated from our free buffer); this does the work
        /// of coalescing, updating m_data and other fields, etc.
        /// </summary>
        Slice<TTime, TValue> InternalAppend(Slice<TTime, TValue> dest)
        {
            // dest must be from our free buffer
            HoloDebug.Assert(dest.Buffer.Data == m_remainingFreeBuffer.Buffer.Data);

            if (m_data.Count == 0) {
                m_data.Add(new TimedSlice<TTime, TValue>(InitialTime, dest));
            }
            else {
                TimedSlice<TTime, TValue> last = m_data[m_data.Count - 1];
                if (last.Slice.Precedes(dest)) {
                    m_data[m_data.Count - 1] = new TimedSlice<TTime, TValue>(last.InitialTime, last.Slice.UnionWith(dest));
                }
                else {
                    Spam.Audio.WriteLine("BufferedSliceStream.InternalAppend: last did not precede; last slice is " + last.Slice + ", last slice time " + last.InitialTime + ", dest is " + dest);
                    m_data.Add(new TimedSlice<TTime, TValue>(last.InitialTime + last.Slice.Duration, dest));
                }
            }

            m_discreteDuration += dest.Duration;
            m_remainingFreeBuffer = m_remainingFreeBuffer.SubsliceStartingAt(dest.Duration);
            
            return dest;
        }

        /// <summary>
        /// Copy strided data from a source array into a single destination sliver.
        /// </summary>
        public override void AppendSliver(TValue[] source, int startOffset, int width, int stride, int height)
        {
            HoloDebug.Assert(source != null);
            int neededLength = startOffset + stride * (height - 1) + width;
            HoloDebug.Assert(source.Length >= neededLength);
            HoloDebug.Assert(SliverSize == width * height);
            HoloDebug.Assert(stride >= width);

            EnsureFreeBuffer();

            Slice<TTime, TValue> destination = m_remainingFreeBuffer.SubsliceOfDuration(1);

            int sourceOffset = startOffset;
            int destinationOffset = 0;
            for (int h = 0; h < height; h++) {
                destination.CopyFrom(source, sourceOffset, destinationOffset, width);

                sourceOffset += stride;
                destinationOffset += width;
            }

            InternalAppend(destination);

            Trim();
        }

        /// <summary>
        /// Trim off any content beyond the maximum allowed to be buffered.
        /// </summary>
        /// <remarks>
        /// Internal because wrapper streams want to delegate to this when they are themselves Trimmed.</remarks>
        void Trim()
        {
            if (m_maxBufferedDuration == 0 || m_discreteDuration <= m_maxBufferedDuration) {
                return;
            }

            while (DiscreteDuration > m_maxBufferedDuration) {
                Duration<TTime> toTrim = DiscreteDuration - m_maxBufferedDuration;
                // get the first slice
                TimedSlice<TTime, TValue> firstSlice = m_data[0];
                if (firstSlice.Slice.Duration <= toTrim) {
                    m_data.RemoveAt(0);
#if DEBUG
                    // check to make sure our later stream data doesn't reference this one we're about to free
                    foreach (TimedSlice<TTime, TValue> slice in m_data) {
                        HoloDebug.Assert(slice.Slice.Buffer.Data != firstSlice.Slice.Buffer.Data);
                    }
#endif
                    m_allocator.Free(firstSlice.Slice.Buffer);
                    m_discreteDuration -= firstSlice.Slice.Duration;
                    m_initialTime += firstSlice.Slice.Duration;
                }
                else {
                    TimedSlice<TTime, TValue> newFirstSlice = new TimedSlice<TTime, TValue>(
                        firstSlice.InitialTime + toTrim,
                        new Slice<TTime, TValue>(
                                firstSlice.Slice.Buffer,
                                firstSlice.Slice.Offset + toTrim,
                                firstSlice.Slice.Duration - toTrim,
                                SliverSize));
                    m_data[0] = newFirstSlice;
                    m_discreteDuration -= toTrim;
                    m_initialTime += toTrim;
                }
            }
        }

        public override void CopyTo(Interval<TTime> sourceInterval, IntPtr p)
        {
            while (!sourceInterval.IsEmpty) {
                Slice<TTime, TValue> source = GetNextSliceAt(sourceInterval);
                m_copySliceToIntPtrAction(source, p);
                sourceInterval = sourceInterval.SubintervalStartingAt(source.Duration);
            }
        }

        public override void CopyTo(Interval<TTime> sourceInterval, DenseSliceStream<TTime, TValue> destinationStream)
        {
            while (!sourceInterval.IsEmpty) {
                Slice<TTime, TValue> source = GetNextSliceAt(sourceInterval);
                destinationStream.Append(source);
                sourceInterval = sourceInterval.SubintervalStartingAt(source.Duration);
            }
        }

        /// <summary>
        /// Map the interval time to stream local time, and get the next slice of it.
        /// </summary>
        public override Slice<TTime, TValue> GetNextSliceAt(Interval<TTime> interval)
        {
            Interval<TTime> firstMappedInterval = m_intervalMapper.MapNextSubInterval(interval);

            if (firstMappedInterval.IsEmpty) {
                return Slice<TTime, TValue>.Empty;
            }

            HoloDebug.Assert(firstMappedInterval.InitialTime >= InitialTime);
            HoloDebug.Assert(firstMappedInterval.InitialTime + firstMappedInterval.Duration <= InitialTime + DiscreteDuration);

            TimedSlice<TTime, TValue> foundTimedSlice = GetInitialTimedSlice(firstMappedInterval);
            Interval<TTime> intersection = foundTimedSlice.Interval.Intersect(firstMappedInterval);
            HoloDebug.Assert(!intersection.IsEmpty);
            Slice<TTime, TValue> ret = foundTimedSlice.Slice.Subslice(
                intersection.InitialTime - foundTimedSlice.InitialTime,
                intersection.Duration);

            return ret;
        }

        TimedSlice<TTime, TValue> GetInitialTimedSlice(Interval<TTime> firstMappedInterval)
        {
            // we must overlap somewhere
            HoloDebug.Assert(!firstMappedInterval.Intersect(new Interval<TTime>(InitialTime, DiscreteDuration)).IsEmpty);

            // Get the biggest available slice at firstMappedInterval.InitialTime.
            // First, get the index of the slice just after the one we want.
            TimedSlice<TTime, TValue> target = new TimedSlice<TTime, TValue>(firstMappedInterval.InitialTime, Slice<TTime, TValue>.Empty);
            int originalIndex = m_data.BinarySearch(target, TimedSlice<TTime, TValue>.Comparer.Instance);
            int index = originalIndex;

            if (index < 0) {
                // index is then the index of the next larger element
                // -- we know there is a smaller element because we know firstMappedInterval fits inside stream interval
                index = (~index) - 1;
                HoloDebug.Assert(index >= 0);
            }

            TimedSlice<TTime, TValue> foundTimedSlice = m_data[index];
            return foundTimedSlice;
        }

        public override void Dispose()
        {
            // release each T[] back to the buffer
            foreach (TimedSlice<TTime, TValue> slice in m_data) {
                // this requires that Free be idempotent; in general we don't expect
                // many slices per buffer, since each Stream allocates from a private
                // buffer and coalesces aggressively
                m_allocator.Free(slice.Slice.Buffer);
            }
        }
    }

    public static class SliceFloatExtension
    {
        /// <summary>
        /// Copy data from IntPtr to Slice.
        /// </summary>
        public static void CopyToSlice<TTime>(this IntPtr src, Slice<TTime, float> dest)
        {
            Marshal.Copy(src, dest.Buffer.Data, (int)dest.Offset * dest.SliverSize, (int)dest.Duration * dest.SliverSize);
        }
        /// <summary>
        /// Copy data from Slice to IntPtr.
        /// </summary>
        public static void CopyToIntPtr<TTime>(this Slice<TTime, float> src, IntPtr dest)
        {
            Marshal.Copy(src.Buffer.Data, (int)src.Offset * src.SliverSize, dest, (int)src.Duration * src.SliverSize);
        }
        /// <summary>
        /// Invoke some underlying action with an IntPtr directly to a Slice's data.
        /// </summary>
        /// <remarks>
        /// The arguments to the action are an IntPtr to the fixed data, and the number of BYTES to act on.
        /// </remarks>
        public static unsafe void RawAccess<TTime>(this Slice<TTime, float> src, Action<IntPtr, int> action)
        {
            // per http://www.un4seen.com/forum/?topic=12912.msg89978#msg89978
            fixed (float* p = &src.Buffer.Data[src.Offset * src.SliverSize]) {
                byte* b = (byte*)p;

                action(new IntPtr(p), (int)src.Duration * src.SliverSize * sizeof(float));
            }
        }
    }

    public static class SliceByteExtension
    {
        /// <summary>
        /// Copy data from IntPtr to Slice.
        /// </summary>
        public static void CopyToSlice<TTime>(this IntPtr src, Slice<TTime, byte> dest)
        {
            Marshal.Copy(src, dest.Buffer.Data, (int)dest.Offset * dest.SliverSize, (int)dest.Duration * dest.SliverSize);
        }
        /// <summary>
        /// Copy data from Slice to IntPtr.
        /// </summary>
        public static void CopyToIntPtr<TTime>(this Slice<TTime, byte> src, IntPtr dest)
        {
            Marshal.Copy(src.Buffer.Data, (int)src.Offset * src.SliverSize, dest, (int)src.Duration * src.SliverSize);
        }
        /// <summary>
        /// Invoke some underlying action with an IntPtr directly to a Slice's data.
        /// </summary>
        /// <remarks>
        /// The action receives an IntPtr to the data, and an int that is a count of bytes.
        /// </remarks>
        public static unsafe void RawAccess<TTime>(this Slice<TTime, byte> src, Action<IntPtr, int> action)
        {
            // per http://www.un4seen.com/forum/?topic=12912.msg89978#msg89978
            fixed (byte* p = &src.Buffer.Data[src.Offset * src.SliverSize]) {

                action(new IntPtr(p), (int)src.Duration * src.SliverSize);
            }
        }
        /// <summary>
        /// Invoke some underlying action with a T[] and offset.
        /// </summary>
        /// <remarks>
        /// The action receives the underlying backing T[], an offset from the start of the T[], and a count of elements.
        /// </remarks>
        public static unsafe void RawAccess<TTime>(this Slice<TTime, byte> src, Action<byte[], Duration<TTime>, Duration<TTime>> action)
        {
            action(src.Buffer.Data, src.Offset, src.Duration);
        }
    }

    public class DenseSampleFloatStream : BufferedSliceStream<Sample, float>
    {
        public DenseSampleFloatStream(
            Time<Sample> initialTime,
            BufferAllocator<float> allocator,
            int sliverSize,
            Duration<Sample> maxBufferedDuration = default(Duration<Sample>),
            bool useContinuousLoopingMapper = false)
            : base(initialTime,
                allocator,
                sliverSize,
                SliceFloatExtension.CopyToSlice,
                SliceFloatExtension.CopyToIntPtr,
                SliceFloatExtension.RawAccess,
                maxBufferedDuration,
                useContinuousLoopingMapper)
        {
        }

        public override int SizeofValue()
        {
            return sizeof(float);
        }
    }

    public class DenseFrameByteStream : BufferedSliceStream<Frame, byte>
    {
        public DenseFrameByteStream(
            BufferAllocator<byte> allocator,
            int sliverSize,
            Duration<Frame> maxBufferedDuration = default(Duration<Frame>),
            bool useContinuousLoopingMapper = false)
            : base(0,
                allocator,
                sliverSize,
                SliceByteExtension.CopyToSlice,
                SliceByteExtension.CopyToIntPtr,
                SliceByteExtension.RawAccess,
                maxBufferedDuration,
                useContinuousLoopingMapper)
        {
        }

        public override int SizeofValue()
        {
            return sizeof(byte);
        }
    }

    public class SparseSampleByteStream : SparseSliceStream<Sample, byte>
    {
        public SparseSampleByteStream(
            Time<Sample> initialTime,
            BufferAllocator<byte> allocator,
            int sliverSize,
            int maxBufferedFrameCount = 0)
            : base(initialTime, 
                new DenseFrameByteStream(allocator, sliverSize, maxBufferedFrameCount), 
                maxBufferedFrameCount)
        {
        }

        public override int SizeofValue()
        {
            return sizeof(byte);
        }
    }
}
