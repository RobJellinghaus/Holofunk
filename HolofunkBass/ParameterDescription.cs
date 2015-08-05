////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A description of a single parameter of a single effect.</summary>
    /// <remarks>This serves as parameter-level metadata.
    /// 
    /// Note that we DO NOT override GetHashCode or Equals -- we use reference hashing and
    /// reference equality for ParameterDescriptions, since we use a singleton discipline
    /// for constructing them on a per-Effect-subclass basis.
    /// 
    /// Note also that the default values here are used only by Parameter.GetInterpolatedValue
    /// -- the base Parameter.GetValue and Parameter.SetValue methods work over a uniform
    /// closed interval [0, 1].</remarks>
    public class ParameterDescription
    {
        /// <summary>The type of effect this applies to.</summary>
        readonly Type m_effectType;

        /// <summary>The name, for debuggability.</summary>
        readonly string m_name;

        /// <summary>The min, max, base, and default values.</summary>
        /// <remarks>Min = smallest possible value; Max = biggest; Base = the "origin" value
        /// (for slider purposes); Default = the initial value.</remarks>
        readonly float m_min, m_max, m_base, m_default;

        /// <summary>Is this parameter absolute?</summary>
        /// <remarks>For non-additive parameters in effect space, the base value is ignored,
        /// and only the drag value is used.  Non-absolute parameters (the large majority)
        /// are additive; effect space dragging can only increase the base value towards
        /// the maximum.</remarks>
        readonly bool m_absolute;

        public ParameterDescription(
            Type effectType, 
            string name, 
            float min, 
            float baseValue,
            float defaultValue,
            float max,
            bool absolute = false)
        {
            HoloDebug.Assert((min < max && min <= defaultValue) || (min > max && min >= defaultValue) || (min == max && min == defaultValue));
            HoloDebug.Assert((min < max && defaultValue <= max) || (min > max && defaultValue >= max) || (min == max && min == defaultValue));

            m_effectType = effectType;
            m_name = name;
            m_min = min;
            m_base = baseValue;
            m_default = defaultValue;
            m_max = max;
            m_absolute = absolute;
        }

        public Type EffectType { get { return m_effectType; } }
        public string Name { get { return m_name; } }

        public float Min { get { return m_min; } }
        public float Max { get { return m_max; } }
        public float Base { get { return m_base; } }
        public float Default { get { return m_default; } }
        public bool Absolute { get { return m_absolute; } }
    }
}
