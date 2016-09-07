////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A set of instantiated Effects.</summary>
    /// <remarks>Effects are instantiated either per-Loop or per-Region; this collects
    /// all the Effects defined for a particular granularity.</remarks>
    public class EffectSet
    {
        readonly List<HolofunkEffect> m_effects = new List<HolofunkEffect>();

        public EffectSet()
        {
        }

        public void Add(HolofunkEffect effect)
        {
            m_effects.Add(effect);
        }

        public void Apply(ParameterMap parameters, Moment now)
        {
            foreach (HolofunkEffect effect in m_effects) {
                effect.Apply(parameters, now);
            }
        }
    }
}
