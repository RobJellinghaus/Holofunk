////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A singleton static class which tracks all defined Effects and their ParameterDescriptions.</summary>
    /// <remarks></remarks>
    public class AllEffects
    {
        struct EffectDescription
        {
            internal readonly Func<StreamHandle, Form, HolofunkEffect> ConstructionFunc;
            internal readonly List<ParameterDescription> Parameters;

            internal EffectDescription(Func<StreamHandle, Form, HolofunkEffect> constructionFunc, List<ParameterDescription> parameters)
            {
                ConstructionFunc = constructionFunc;
                Parameters = parameters;
            }
        }

        static Dictionary<Type, EffectDescription> s_effectMap = new Dictionary<Type, EffectDescription>();
        static List<ParameterDescription> s_parameters = new List<ParameterDescription>();

        public static List<ParameterDescription> AllParameters { get { return s_parameters; } }

        public static void Register(
            Type effectType,
            Func<StreamHandle, Form, HolofunkEffect> constructionFunc, 
            List<ParameterDescription> parameters)
        {
            s_effectMap.Add(effectType, new EffectDescription(constructionFunc, parameters));
            s_parameters.AddRange(parameters);
        }

        public static EffectSet CreateLoopEffectSet(StreamHandle streamHandle, Form form)
        {
            EffectSet set = new EffectSet();
            foreach (EffectDescription desc in s_effectMap.Values) {
                set.Add(desc.ConstructionFunc(streamHandle, form));
            }
            return set;
        }

        /// <summary>Create a set of parameters covering all known effects, with default values across the board.</summary>
        public static ParameterMap CreateParameterMap()
        {
            ParameterMap set = new ParameterMap();
            foreach (ParameterDescription desc in s_parameters) {
                set.Add(new ConstantParameter(desc));
            }
            return set;
        }
    }
}
