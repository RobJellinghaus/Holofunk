////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    /// <summary>Rolling buffer which can average a number of T's.</summary>
    /// <remarks>Parameterized with methods to handle summing / dividing the T's in question.</remarks>
    public abstract class Averager<T>
    {
        // the buffer of T's
        readonly T[] m_storage;

        // have we filled the current storage?
        bool m_storageFull;

        // what's the next index to be overwritten with the next datum?
        int m_index;

        // the total
        T m_total;

        // the current average, so we don't have race conditions about it
        T m_average;

        public Averager(int capacity)
        {
            m_storage = new T[capacity];
        }

        /// <summary>Has this Averager got no data?</summary>
        public bool IsEmpty { get { return m_index == 0 && !m_storageFull; } }

        /// <summary>Update this Averager with another data point.</summary>
        public void Update(T nextT)
        {
            if (!IsValid(nextT)) {
                return;
            }

            if (m_index == m_storage.Length) {
                // might as well unconditionally set it, branching is more expensive
                m_storageFull = true;
                m_index = 0;
            }

            if (m_storageFull) {
                m_total = Subtract(m_total, m_storage[m_index]);
            }
            m_total = Add(m_total, nextT);
            m_storage[m_index] = nextT;
            m_index++;
            m_average = Divide(m_total, m_storageFull ? m_storage.Length : m_index);
        }

        /// <summary>Get the average; invalid if Average.IsEmpty.</summary>
        public T Average 
        { 
            get 
            {
                return m_average;
            } 
        }

        protected abstract bool IsValid(T t);
        protected abstract T Subtract(T total, T nextT);
        protected abstract T Add(T total, T nextT);
        protected abstract T Divide(T total, int count);
    }

    public class FloatAverager : Averager<float>
    {
        public FloatAverager(int capacity)
            : base(capacity)
        {
        }

        protected override bool IsValid(float t)
        {
            // semi-arbitrary, but intended to filter out infinities and other extreme bogosities
            return -100 < t && t < 2000;
        }

        protected override float Add(float total, float nextT)
        {
            return total + nextT;
        }

        protected override float Subtract(float total, float nextT)
        {
            return total - nextT;
        }

        protected override float Divide(float total, int count)
        {
            return total / count;
        }
    }

    public class Vector2Averager : Averager<Vector2>
    {
        public Vector2Averager(int capacity)
            : base(capacity)
        {
        }

        protected override bool IsValid(Vector2 t)
        {
            // semi-arbitrary, but intended to filter out infinities and other extreme bogosities
            return -100 < t.X && t.X < 2000 && -100 < t.Y && t.Y < 2000;
        }

        protected override Vector2 Add(Vector2 total, Vector2 nextT)
        {
            return total + nextT;
        }

        protected override Vector2 Subtract(Vector2 total, Vector2 nextT)
        {
            return total - nextT;
        }

        protected override Vector2 Divide(Vector2 total, int count)
        {
            return new Vector2(total.X / count, total.Y / count);
        }
    }
}
