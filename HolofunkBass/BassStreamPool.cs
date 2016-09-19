////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Holofunk
{
    /// <summary>
    /// A pool of precreated BassStreams.
    /// </summary>
    /// <remarks>
    /// Instantiating VST plugins can be quite expensive, so precreating these streams can greatly reduce
    /// latency at runtime.
    /// </remarks>
    public class BassStreamPool
    {
        /// <summary>How many streams do we want in here anyway?</summary>
        readonly int m_capacity;

        /// <summary>The free and currently unused streams.</summary>
        readonly Dictionary<int, BassStream> m_freeStreams = new Dictionary<int, BassStream>();

        readonly Stack<BassStream> m_freeStreamStack = new Stack<BassStream>();

        /// <summary>The reserved streams currently being used by Holofunk.</summary>
        readonly Dictionary<int, BassStream> m_reservedStreams = new Dictionary<int, BassStream>();

        public BassStreamPool(int capacity, Form baseForm)
        {
            m_capacity = capacity;
            for (int i = 0; i < capacity; i++) {
                BassStream stream = new BassStream(i, baseForm);
                m_freeStreams.Add(i, stream);
                m_freeStreamStack.Push(stream);
            }
        }

        public int FreeCount { get { return m_freeStreams.Count; } }

        /// <summary>
        /// Reserve a stream from the free set.
        /// </summary>
        /// <remarks>Throws an exception if no stream is available.</remarks>
        public BassStream Reserve()
        {
            lock (this) { // [?Thread] ensure local invariants when altering pool contents
                BassStream next = m_freeStreamStack.Pop();
                m_freeStreams.Remove(next.IdUniqueWithinPool);
                HoloDebug.Assert(!m_reservedStreams.ContainsKey(next.IdUniqueWithinPool));

                m_reservedStreams.Add(next.IdUniqueWithinPool, next);

                return next;
            }
        }

        public void Free(BassStream stream)
        {
            lock (this) { // [?Thread] ensure local invariants when altering pool contents
                HoloDebug.Assert(!m_freeStreams.ContainsKey(stream.IdUniqueWithinPool));
                HoloDebug.Assert(m_reservedStreams.ContainsKey(stream.IdUniqueWithinPool));
                m_reservedStreams.Remove(stream.IdUniqueWithinPool);
                // BUGBUG: failure to re-add existing stream here
                m_freeStreams.Add(stream.IdUniqueWithinPool, stream);
                m_freeStreamStack.Push(stream);
            }
        }
    }
}
