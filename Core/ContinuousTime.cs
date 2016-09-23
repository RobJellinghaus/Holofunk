////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////


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
