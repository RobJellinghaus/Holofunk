////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;

// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>A single float parameter applied to control a sound effect; sound effects have one
    /// or more Parameters.</summary>
    /// <remarks>Subclasses define policy around time (e.g. constant or time-varying).  Parameters are
    /// effectively functions from Moment to float.
    /// 
    /// Note that all Parameters store and expose their value as a float in the range [0, 1]
    /// (inclusive on both ends).  The InterpolatedValue property interpolates the base Value
    /// to the min-max range defined in the ParameterDescription.
    /// </remarks
    public abstract class Parameter
    {
        readonly ParameterDescription m_description;

        protected Parameter(ParameterDescription description)
        {
            m_description = description;
        }

        /// <summary>The parameter's description metadata (singleton, by construction).</summary>
        public ParameterDescription Description { get { return m_description; } }

        /// <summary>Get the parameter's value at the given moment; must be in the range [0, 1] inclusive.</summary>
        public abstract float this[Time<Sample> now] { get; set; }

        /// <summary>Get the interpolated value of the parameter, by using the current value to interpolate
        /// to the interval [Description.Min, Description.Max].</summary>
        public float GetInterpolatedValue(Time<Sample> now)
        {
            float baseValue = this[now];
            return Description.Min + ((Description.Max - Description.Min) * baseValue);
        }

        /// <summary>Copy this Parameter; the copied Parameter will not support SetValue.</summary>
        /// <remarks>This is used at the end of parameter recording, to duplicate the recorded data
        /// into an immutable and sharable form.</remarks>
        public abstract Parameter Copy(bool forceMutable = false);
    }

    /// <summary>A parameter which has only one value for all time.</summary>
    /// <remarks>The value may be mutated externally (for all time).</remarks>
    public sealed class ConstantParameter : Parameter
    {
        float m_value;
        readonly bool m_immutable;

        public ConstantParameter(ParameterDescription description, float value, bool immutable)
            : base(description)
        {
            HoloDebug.Assert(0 <= value);
            HoloDebug.Assert(value <= 1);

            m_value = value;
            m_immutable = immutable;
        }

        /// <summary>Create a constant parameter with the default value.</summary>
        /// <param name="description"></param>
        public ConstantParameter(ParameterDescription description) 
            : this(
                description, 
                // have to map the default value in the description to our internal [0, 1] range
                (float)description.Max == description.Min 
                    ? description.Default 
                    : ((description.Default - description.Min) / (description.Max - description.Min)), 
                false)
        {
        }

        public override float this[Time<Sample> now]
        {
            get 
            { 
                return m_value; 
            }
            set 
            {
                HoloDebug.Assert(!m_immutable);
                HoloDebug.Assert(0 <= value);
                HoloDebug.Assert(value <= 1);

                m_value = value; 
            }
        }

        public override Parameter Copy(bool forceMutable = false)
        {
            return new ConstantParameter(Description, m_value, !forceMutable);
        }
    }
}
