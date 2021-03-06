﻿////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A map from ParameterDescriptions to integer values.</summary>
    public class CounterMap
    {
        readonly Dictionary<ParameterDescription, int> m_counters = new Dictionary<ParameterDescription, int>();

        public CounterMap(ParameterMap other)
        {
            foreach (Parameter p in other) {
                m_counters.Add(p.Description, 0);
            }
        }

        public int this[ParameterDescription d]
        {
            get { return m_counters[d]; }
        }

        public void Increment(ParameterDescription d)
        {
            m_counters[d] = m_counters[d] + 1;
        }
    }
}
