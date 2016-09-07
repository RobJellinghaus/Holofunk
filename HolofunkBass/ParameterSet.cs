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
    /// <summary>A set of Parameters, which may or may not cover all Parameters in all Effects.</summary>
    /// <remarks>A ParameterMap is a map from ParameterDescriptions to Parameters.
    /// 
    /// The usage model of ParameterMaps is that they fundamentally support sharing Parameter
    /// instances, one per ParameterDescription.  The typical model is:
    /// 
    /// - The parameter UI(s) have ParameterMaps that contain mutable Parameter instances, which are
    /// mutated by the user's interaction with the parameter UI(s).
    /// 
    /// - The microphone, and each loopie, have ParameterMaps of their own.  During the time the user
    /// is in a parameter UI, these other ParameterMaps (for the microphone, if the microphone is
    /// being affected, and for all touched loopies) actually share the mutable Parameter instances
    /// from the UI's ParameterMap.  
    /// 
    /// - When the user switches out of parameter UI, or changes the parameter dimensions, the UI's 
    /// ParameterMap is copied into a new immutable ParameterMap; this ParameterMap's Parameters are
    /// then shared by the microphone's ParameterMap (if applicable) and the ParameterMaps of all
    /// touched loopies.
    /// 
    /// The overall goals are: 
    /// - Zero-allocation update of multiple loopies and microphone(s) during parameter manipulation.
    /// - Minimal copying of parameter data when the parameter UI is switched.
    /// - Maximal sharing of either mutable or immutable parameter instances, to minimize the number
    /// of objects that need to be touched when switching UI or editing parameter values.
    /// 
    /// All of this applies whether the parameters are time-varying or not.</remarks>
    public class ParameterMap : IEnumerable<Parameter>
    {
        readonly Dictionary<ParameterDescription, Parameter> m_parameters = new Dictionary<ParameterDescription, Parameter>();

        /// <summary>Construct an initially empty ParameterMap.</summary>
        public ParameterMap()
        {
        }

        public void WriteLabelTo(StringBuilder builder)
        {
            builder.Clear();
            foreach (Parameter p in this) {
                builder.Append(p.Description.Name);
                builder.Append(" ");
            }
        }

        /// <summary>Add this parameter.</summary>
        /// <returns>The map itself, to allow method chaining.</returns>
        public ParameterMap Add(Parameter parameter)
        {
            HoloDebug.Assert(!m_parameters.ContainsKey(parameter.Description));

            m_parameters.Add(parameter.Description, parameter);

            return this;
        }

        /// <summary>This new parameter is to be used until further notice.</summary>
        /// <remarks>When the parameter UI is dragging, this is called to point at the mutable Parameter being directly
        /// manipulated by the user.
        /// 
        /// After the user stops manipulating, this gets called with a static (no-longer-mutable) parameter
        /// instance.</remarks>
        /// <returns>The map itself, to allow method chaining.</returns>
        public ParameterMap Share(Parameter newParameter)
        {
            HoloDebug.Assert(m_parameters.ContainsKey(newParameter.Description));

            m_parameters[newParameter.Description] = newParameter;

            return this;
        }

        /// <summary>Update all parameters in this set from these others.</summary>
        /// <returns>The map itself, to allow method chaining.</returns>
        public ParameterMap ShareAll(ParameterMap newParameterMap)
        {
            foreach (Parameter p in newParameterMap.m_parameters.Values) {
                Share(p);
            }

            return this;
        }

        /// <summary>Return a new ParameterMap containing a complete set of copied, unchanging Parameters.</summary>
        /// <remarks>This is called at the end of parameter recording, to fix the parameters being used by
        /// the relevant tracks.</remarks>
        public ParameterMap Copy(bool forceMutable = false)
        {
            ParameterMap ret = new ParameterMap();
            foreach (Parameter p in m_parameters.Values) {
                ret.Add(p.Copy(forceMutable: forceMutable));
            }
            return ret;
        }

        /// <summary>Get the parameter matching this description, or null if none.</summary>
        public Parameter this[ParameterDescription desc]
        {
            get
            {
                Parameter parameter;
                if (m_parameters.TryGetValue(desc, out parameter)) {
                    return parameter;
                }
                else {
                    return null;
                }
            }
        }

        public bool Contains(ParameterDescription desc)
        {
            return m_parameters.ContainsKey(desc);
        }

        public void SetAll(Moment now, float value)
        {
            HoloDebug.Assert(0 <= value);
            HoloDebug.Assert(value <= 1);

            foreach (Parameter p in m_parameters.Values) {
                p[now.Time] = value;
            }
        }

        /// <summary>
        /// Set the values of each parameter in this map to be the average of all values of
        /// the corresponding parameter in any or all of otherMaps, or the default parameter
        /// value if no such parameters exist in otherMaps.
        /// </summary>
        /// <param name="otherMaps"></param>
        public void SetFromAverage(Moment now, IEnumerable<ParameterMap> otherMaps)
        {
            // Make a count map.
            CounterMap counts = new CounterMap(this);

            // Set all our values to 0 as well.
            SetAll(now, 0f);

            foreach (ParameterMap otherMap in otherMaps) {
                foreach (Parameter p in m_parameters.Values) {
                    Parameter other = otherMap[p.Description];
                    if (other != null) {
                        counts.Increment(p.Description);
                        int count = counts[p.Description];

                        // This effectively adds other[now] into p[now] in the appropriate
                        // proportion to preserve the property that p[now] is the average of
                        // all values added in, without violating the constraint that all
                        // parameter values are in the interval [0, 1] inclusive.
                        // In other words: this lets us keep a running average without needing
                        // to traverse twice (either for counting or for averaging purposes).
                        p[now.Time] = (p[now.Time] * (count - 1) / count) + (other[now.Time] / count);
                    }
                }
            }
        }

        /// <summary>Reset all (modified) parameters to default.</summary>
        public void ResetToDefault()
        {
            foreach (ParameterDescription desc in AllEffects.AllParameters) {
                if (m_parameters.ContainsKey(desc)) {
                    m_parameters[desc] = new ConstantParameter(desc);
                }
            }
        }

        #region IEnumerable<Parameter> Members

        public IEnumerator<Parameter> GetEnumerator()
        {
            return m_parameters.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_parameters.Values.GetEnumerator();
        }

        #endregion
    }
}
