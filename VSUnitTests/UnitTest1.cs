////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////
using Holofunk.Core;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VSUnitTests
{
    [TestClass]
    public class UnitTest1
    {
        const int FloatSliverSize = 2;
        const int FloatNumSlices = 128;

        [TestMethod]
        public void TestBufferAllocator()
        {
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(FloatNumSlices * 2048, 1, sizeof(float));
            Buf<float> f = bufferAllocator.Allocate();
            HoloDebug.Assert(f.Data.Length == FloatSliverSize * 1024 * FloatNumSlices);

            Buf<float> f2 = bufferAllocator.Allocate();
            HoloDebug.Assert(f.Data.Length == f2.Data.Length);
            bufferAllocator.Free(f2);
            Buf<float> f3 = bufferAllocator.Allocate();
            HoloDebug.Assert(f2.Data == f3.Data); // need to pull from free list first
        }

        [TestMethod]
        public void TestSlice()
        {
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(FloatNumSlices * 2048, 1, sizeof(float));
            Buf<float> buffer = bufferAllocator.Allocate();
            Slice<Sample, float> slice = new Slice<Sample, float>(buffer, 0, FloatNumSlices, FloatSliverSize);
            HoloDebug.Assert(slice.Duration == FloatNumSlices);
            HoloDebug.Assert(slice.IsEmpty() == false);
            HoloDebug.Assert(slice.SliverSize == FloatSliverSize);
            var halfDuration = (FloatNumSlices / 2);
            Slice<Sample, float> prefixSlice = slice.Subslice(0, halfDuration);
            Slice<Sample, float> prefixSlice2 = slice.SubsliceOfDuration(halfDuration);
            Slice<Sample, float> suffixSlice = slice.Subslice(halfDuration, halfDuration);
            Slice<Sample, float> suffixSlice2 = slice.SubsliceStartingAt(halfDuration);
            HoloDebug.Assert(prefixSlice.Precedes(suffixSlice));
            HoloDebug.Assert(prefixSlice.Precedes(suffixSlice2));
            HoloDebug.Assert(prefixSlice2.Precedes(suffixSlice));
            HoloDebug.Assert(prefixSlice2.Precedes(suffixSlice2));

            PopulateFloatSlice(slice);

            Buf<float> buffer2 = bufferAllocator.Allocate();
            Slice<Sample, float> slice2 = new Slice<Sample, float>(buffer2, 0, FloatNumSlices, FloatSliverSize);

            slice.CopyTo(slice2);

            VerifySlice(slice2);
        }

        static void PopulateFloatSlice(Slice<Sample, float> slice)
        {
            for (int i = 0; i < (int)slice.Duration; i++) {
                slice[i, 0] = i;
                slice[i, 1] = i + 0.5f;
            }
        }

        static void VerifySlice(Slice<Sample, float> slice)
        {
            for (int i = 0; i < (int)slice.Duration; i++) {
                HoloDebug.Assert(slice[i, 0] == i);
                HoloDebug.Assert(slice[i, 1] == i + 0.5);
            }
        }

        /// <summary>
        /// Simple basic stream test: make one, append two slices to it, ensure they get merged.
        /// </summary>
        [TestMethod]
        public void TestStream()
        {
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(FloatNumSlices * 2048, 1, sizeof(float));

            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, FloatSliverSize);

            HoloDebug.Assert(stream.DiscreteDuration == 0);

            var interval = new Interval<Sample>(0, 10);
            Slice<Sample, float> firstSlice = stream.GetNextSliceAt(interval);
            HoloDebug.Assert(firstSlice.IsEmpty());

            // Now let's fill a float array...
            float[] buffer = new float[FloatNumSlices * FloatSliverSize];
            Duration<Sample> floatNumSlicesDuration = FloatNumSlices;
            Slice<Sample, float> tempSlice = new Slice<Sample, float>(new Buf<float>(-1, buffer), FloatSliverSize);
            PopulateFloatSlice(tempSlice);

            // now append in chunks
            stream.Append(tempSlice.SubsliceOfDuration(tempSlice.Duration / 2));
            stream.Append(tempSlice.SubsliceStartingAt(tempSlice.Duration / 2));

            HoloDebug.Assert(stream.InitialTime == 0);
            HoloDebug.Assert(stream.DiscreteDuration == FloatNumSlices);

            Slice<Sample, float> theSlice = stream.GetNextSliceAt(stream.DiscreteInterval);

            VerifySlice(theSlice);
            HoloDebug.Assert(theSlice.Duration == floatNumSlicesDuration);
        }

        [TestMethod]
        public void TestStreamChunky()
        {
            const int sliverSize = 4; // 4 floats = 16 bytes
            const int floatNumSlices = 11; // 11 slices per buffer, to test various cases
            const int biggestChunk = 5; // max size of slice to copy in middle loop
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(sliverSize * floatNumSlices, 1, sizeof(float));

            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, sliverSize);

            HoloDebug.Assert(stream.DiscreteDuration == 0);

            float f = 0;
            float[] tinyBuffer = new float[biggestChunk * sliverSize];
            for (int i = 0; i < 100; i++) {
                for (int c = 1; c <= 5; c++) {
                    for (int j = 0; j < c; j++) {
                        tinyBuffer[j * sliverSize] = f;
                        tinyBuffer[j * sliverSize + 1] = f + 0.25f;
                        tinyBuffer[j * sliverSize + 2] = f + 0.5f;
                        tinyBuffer[j * sliverSize + 3] = f + 0.75f;
                        f++;
                    }
                    Slice<Sample, float> tempSlice = new Slice<Sample, float>(
                        new Buf<float>(-2, tinyBuffer), 0, c, sliverSize);
                    stream.Append(tempSlice);
                }
            }

            // Now after this we will need a verification loop.
            BufferAllocator<float> bigBufferAllocator = new BufferAllocator<float>(sliverSize * 1024, 1, sizeof(float));
            DenseSampleFloatStream bigStream = new DenseSampleFloatStream(0, bigBufferAllocator, sliverSize);

            stream.CopyTo(stream.DiscreteInterval, bigStream);

            HoloDebug.Assert(Verify4SliceFloatStream(stream, 0) == 1500);
            HoloDebug.Assert(Verify4SliceFloatStream(bigStream, 0) == 1500);

            DenseSampleFloatStream stream2 = new DenseSampleFloatStream(0, bufferAllocator, sliverSize);
            bigStream.CopyTo(bigStream.DiscreteInterval, stream2);

            HoloDebug.Assert(Verify4SliceFloatStream(stream2, 0) == 1500);
        }

        static float Verify4SliceFloatStream(DenseSampleFloatStream stream, float f)
        {
            Interval<Sample> interval = stream.DiscreteInterval;
            while (!interval.IsEmpty) {
                Slice<Sample, float> nextSlice = stream.GetNextSliceAt(interval);
                for (int i = 0; i < (int)nextSlice.Duration; i++) {
                    HoloDebug.Assert(nextSlice[i, 0] == f);
                    HoloDebug.Assert(nextSlice[i, 1] == f + 0.25f);
                    HoloDebug.Assert(nextSlice[i, 2] == f + 0.5f);
                    HoloDebug.Assert(nextSlice[i, 3] == f + 0.75f);
                    f++;
                }
                interval = interval.SubintervalStartingAt(nextSlice.Duration);
            }
            return f;
        }

        static float[] AllocateSmall4FloatArray(int numSlices, int sliverSize)
        {
            float[] tinyBuffer = new float[numSlices * sliverSize];
            float f = 0;
            for (int i = 0; i < numSlices; i++) {
                tinyBuffer[i * sliverSize] = f;
                tinyBuffer[i * sliverSize + 1] = f + 0.25f;
                tinyBuffer[i * sliverSize + 2] = f + 0.5f;
                tinyBuffer[i * sliverSize + 3] = f + 0.75f;
                f++;
            }
            return tinyBuffer;
        }

        [TestMethod]
        public void TestStreamAppending()
        {
            const int sliverSize = 4; // 4 floats = 16 bytes
            const int floatNumSlices = 11; // 11 slices per buffer, to test various cases
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(sliverSize * floatNumSlices, 1, sizeof(float));

            float[] buffer = AllocateSmall4FloatArray(floatNumSlices, sliverSize);

            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, sliverSize);

            unsafe {
                fixed (float* f = buffer) {
                    IntPtr pf = new IntPtr(f);

                    stream.Append(floatNumSlices, pf);
                }
            }

            HoloDebug.Assert(stream.DiscreteDuration == floatNumSlices);

            HoloDebug.Assert(Verify4SliceFloatStream(stream, 0) == 11);

            // clear original buffer to test copying back into it
            for (int i = 0; i < buffer.Length; i++) {
                buffer[i] = 0;
            }

            unsafe {
                fixed (float* f = buffer) {
                    IntPtr pf = new IntPtr(f);
                    stream.CopyTo(stream.DiscreteInterval, pf);
                }
            }

            DenseSampleFloatStream stream2 = new DenseSampleFloatStream(0, bufferAllocator, sliverSize);
            stream2.Append(new Slice<Sample, float>(new Buf<float>(-3, buffer), sliverSize));

            HoloDebug.Assert(Verify4SliceFloatStream(stream2, 0) == 11);
        }

        [TestMethod]
        public void TestStreamSlicing()
        {
            const int sliverSize = 4; // 4 floats = 16 bytes
            const int floatNumSlices = 11; // 11 slices per buffer, to test various cases
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(sliverSize * floatNumSlices, 1, sizeof(float));

            float[] buffer = AllocateSmall4FloatArray(floatNumSlices * 2, sliverSize);

            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, sliverSize);
            stream.Append(new Slice<Sample, float>(new Buf<float>(-4, buffer), sliverSize));

            // test getting slices from existing stream
            Slice<Sample, float> beforeFirst = stream.GetNextSliceAt(new Interval<Sample>((-2), 4));
            // should return slice with duration 2
            HoloDebug.Assert(beforeFirst.Duration == 2);

            Slice<Sample, float> afterLast = stream.GetNextSliceAt(new Interval<Sample>(19, 5));
            HoloDebug.Assert(afterLast.Duration == 3);

            // now get slice across the buffer boundary, verify it is split as expected
            Interval<Sample> splitInterval = new Interval<Sample>(7, 8);
            Slice<Sample, float> beforeSplit = stream.GetNextSliceAt(splitInterval);
            HoloDebug.Assert(beforeSplit.Duration == 4);

            Slice<Sample, float> afterSplit = stream.GetNextSliceAt(splitInterval.SubintervalStartingAt(beforeSplit.Duration));
            HoloDebug.Assert(afterSplit.Duration == beforeSplit.Duration);
            float lastBefore = beforeSplit[3, 0];
            float firstAfter = afterSplit[0, 0];
            HoloDebug.Assert(lastBefore + 1 == firstAfter);

            float[] testStrideCopy = new float[] { 
                0, 0, 1, 1, 0, 0, 
                0, 0, 2, 2, 0, 0,
            };

            stream.AppendSliver(testStrideCopy, 2, 2, 6, 2);

            Slice<Sample, float> lastSliver = stream.GetNextSliceAt(new Interval<Sample>(22, 1));
            HoloDebug.Assert(lastSliver.Duration == 1);
            HoloDebug.Assert(lastSliver[0, 0] == 1f);
            HoloDebug.Assert(lastSliver[0, 1] == 1f);
            HoloDebug.Assert(lastSliver[0, 2] == 2f);
            HoloDebug.Assert(lastSliver[0, 3] == 2f);

            Slice<Sample, float> firstSlice = stream.GetNextSliceAt(new Interval<Sample>(-2, 100));
            HoloDebug.Assert(firstSlice.Duration == 11);
        }

        [TestMethod]
        public void TestStreamShutting()
        {
            const int sliverSize = 4; // 4 floats = 16 bytes
            const int floatNumSlices = 11; // 11 slices per buffer, to test various cases
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(sliverSize * floatNumSlices, 1, sizeof(float));

            float continuousDuration = 2.4f;
            int discreteDuration = (int)Math.Round(continuousDuration + 1);
            float[] buffer = AllocateSmall4FloatArray(discreteDuration, sliverSize);
            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, sliverSize, useContinuousLoopingMapper: true);
            stream.Append(new Slice<Sample, float>(new Buf<float>(-5, buffer), sliverSize));

            // OK, time to get this fractional business right.
            stream.Shut((ContinuousDuration)continuousDuration);
            HoloDebug.Assert(stream.IsShut);

            // now test looping
            Interval<Sample> interval = new Interval<Sample>(0, 10);
            // we expect this to be [0, 1, 2, 0, 1, 0, 1, 2, 0, 1]
            // or rather, [0>3], [0>2], [0>3], [0>2]
            Slice<Sample, float> slice = stream.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 3);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[2, 0] == 2f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            slice = stream.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 2);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[1, 0] == 1f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            slice = stream.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 3);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[2, 0] == 2f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            slice = stream.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 2);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[1, 0] == 1f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            HoloDebug.Assert(interval.IsEmpty);

            DenseSampleFloatStream stream2 = new DenseSampleFloatStream(0, bufferAllocator, sliverSize, useContinuousLoopingMapper: false);
            stream2.Append(new Slice<Sample, float>(new Buf<float>(-5, buffer), sliverSize));
            stream2.Shut((ContinuousDuration)continuousDuration);
            interval = new Interval<Sample>(0, 10);
            slice = stream2.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 3);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[2, 0] == 2f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            slice = stream2.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 3);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[1, 0] == 1f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            slice = stream2.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 3);
            HoloDebug.Assert(slice[0, 0] == 0f);
            HoloDebug.Assert(slice[2, 0] == 2f);

            interval = interval.SubintervalStartingAt(slice.Duration);
            slice = stream2.GetNextSliceAt(interval);
            HoloDebug.Assert(slice.Duration == 1);
            HoloDebug.Assert(slice[0, 0] == 0f);
        }

        [TestMethod]
        public void TestDispose()
        {
            const int sliverSize = 4; // 4 floats = 16 bytes
            const int floatNumSlices = 11; // 11 slices per buffer, to test various cases
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(sliverSize * floatNumSlices, 1, sizeof(float));

            float continuousDuration = 2.4f;
            int discreteDuration = (int)Math.Round(continuousDuration + 1);
            float[] tempBuffer = AllocateSmall4FloatArray(discreteDuration, sliverSize);

            // check that allocated, then freed, buffers are used first for next allocation
            Buf<float> buffer = bufferAllocator.Allocate();
            bufferAllocator.Free(buffer);
            Buf<float> buffer2 = bufferAllocator.Allocate();
            HoloDebug.Assert(buffer.Data == buffer2.Data);

            // free it again so stream can get it
            bufferAllocator.Free(buffer);

            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, sliverSize);
            stream.Append(new Slice<Sample, float>(new Buf<float>(-6, tempBuffer), sliverSize));

            Verify4SliceFloatStream(stream, 0);

            // have stream drop it; should free buffer
            stream.Dispose();

            // make sure we get it back again
            buffer2 = bufferAllocator.Allocate();
            HoloDebug.Assert(buffer.Data == buffer2.Data);            
        }

        [TestMethod]
        public void TestLimitedBufferingStream()
        {
            const int sliverSize = 4; // 4 floats = 16 bytes
            const int floatNumSlices = 11; // 11 slices per buffer, to test various cases
            BufferAllocator<float> bufferAllocator = new BufferAllocator<float>(sliverSize * floatNumSlices, 1, sizeof(float));

            float[] tempBuffer = AllocateSmall4FloatArray(20, sliverSize);

            DenseSampleFloatStream stream = new DenseSampleFloatStream(0, bufferAllocator, sliverSize, 5);
            stream.Append(new Slice<Sample, float>(new Buf<float>(-7, tempBuffer), 0, 11, sliverSize));
            HoloDebug.Assert(stream.DiscreteDuration == 5);
            Slice<Sample, float> slice = stream.GetNextSliceAt(stream.DiscreteInterval);
            HoloDebug.Assert(slice[0, 0] == 6f);

            stream.Append(new Slice<Sample, float>(new Buf<float>(-8, tempBuffer), 11, 5, sliverSize));
            HoloDebug.Assert(stream.DiscreteDuration == 5);
            HoloDebug.Assert(stream.InitialTime == 11);
            slice = stream.GetNextSliceAt(stream.DiscreteInterval);
            HoloDebug.Assert(slice[0, 0] == 11f);
        }

        [TestMethod]
        public void TestSparseSampleByteStream()
        {
            const int sliverSize = 2 * 2 * 4; // uncompressed 2x2 RGBA image... worst case
            const int bufferSlivers = 10;
            BufferAllocator<byte> allocator = new BufferAllocator<byte>(sliverSize * bufferSlivers, 1, sizeof(float));

            byte[] appendBuffer = new byte[sliverSize];
            for (int i = 0; i < sliverSize; i++) {
                appendBuffer[i] = (byte)i;
            }

            SparseSampleByteStream stream = new SparseSampleByteStream(10, allocator, sliverSize);
            stream.Append(11, new Slice<Frame, byte>(new Buf<byte>(-9, appendBuffer), sliverSize));

            // now let's get it back out
            Slice<Frame, byte> slice = stream.GetClosestSliver(11);
            HoloDebug.Assert(slice.Duration == 1);
            HoloDebug.Assert(slice.SliverSize == sliverSize);
            for (int i = 0; i < sliverSize; i++) {
                HoloDebug.Assert(slice[0, i] == (byte)i);
            }

            // now let's copy it to intptr
            byte[] target = new byte[sliverSize];
            unsafe {
                fixed (byte* p = target) {
                    IntPtr pp = new IntPtr(p);
                    stream.CopyTo(11, pp);
                }
            }

            for (int i = 0; i < sliverSize; i++) {
                HoloDebug.Assert(target[i] == (byte)i);
            }

            SparseSampleByteStream stream2 = new SparseSampleByteStream(10, allocator, sliverSize);
            unsafe {
                fixed (byte* p = target) {
                    IntPtr pp = new IntPtr(p);
                    stream2.Append(11, pp);
                }
            }

            Slice<Frame, byte> slice2 = stream2.GetClosestSliver(12);
            HoloDebug.Assert(slice2.Duration == 1);
            HoloDebug.Assert(slice2.SliverSize == sliverSize);
            for (int i = 0; i < sliverSize; i++) {
                HoloDebug.Assert(slice2[0, i] == (byte)i);
            }

            // now verify looping and shutting work as expected
            for (int i = 0; i < appendBuffer.Length; i++) {
                appendBuffer[i] += (byte)appendBuffer.Length;
            }
            stream2.Append(21, new Slice<Frame, byte>(new Buf<byte>(-10, appendBuffer), sliverSize));

            Slice<Frame, byte> slice3 = stream2.GetClosestSliver(12);
            HoloDebug.Assert(slice3.Duration == 1);
            HoloDebug.Assert(slice3.SliverSize == sliverSize);
            HoloDebug.Assert(slice3[0, 0] == (byte)0);
            Slice<Frame, byte> slice4 = stream2.GetClosestSliver(22);
            HoloDebug.Assert(slice4.Duration == 1);
            HoloDebug.Assert(slice4.SliverSize == sliverSize);
            HoloDebug.Assert(slice4[0, 0] == (byte)sliverSize);

            stream2.Shut((ContinuousDuration)20);

            // now the closest sliver to 32 should be the first sliver
            Slice<Frame, byte> slice5 = stream2.GetClosestSliver(32);
            HoloDebug.Assert(slice5.Duration == 1);
            HoloDebug.Assert(slice5.SliverSize == sliverSize);
            HoloDebug.Assert(slice5[0, 0] == (byte)0);
            // and 42, the second
            Slice<Frame, byte> slice6 = stream2.GetClosestSliver(42);
            HoloDebug.Assert(slice6.Duration == 1);
            HoloDebug.Assert(slice6.SliverSize == sliverSize);
            HoloDebug.Assert(slice6[0, 0] == (byte)sliverSize);
        }
    }
}
