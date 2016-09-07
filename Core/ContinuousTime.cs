////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.Core
{
    /// <summary>
    /// A continous distance between two Times.
    /// </summary>
    /// <typeparam name="TTime"></typeparam>
    public struct ContinuousDuration
    {
        readonly double m_duration;

        public ContinuousDuration(double duration)
        {
            m_duration = duration;
        }

        public static explicit operator double(ContinuousDuration duration)
        {
            return duration.m_duration;
        }

        public static explicit operator ContinuousDuration(double value)
        {
            return new ContinuousDuration(value);
        }
    }
}
