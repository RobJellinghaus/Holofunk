////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////


// This is in the Holofunk namespace rather than Holofunk.Bass, as the latter's Bass suffix
// collides with the Bass.NET's Bass namespace.
namespace Holofunk
{
    /// <summary>Simple wrapper enum for int, to keep our handles straight.</summary>
    /// <remarks>Yes, this causes downcasting to int everywhere that we pass this to a BASS API.
    /// But that is far outweighed by the readability increase in the C# code.</remarks>
    public enum StreamHandle : int
    {
    }

    /// <summary>Simple wrapper enum for int, to keep our handles straight.</summary>
    /// <remarks>Yes, this causes downcasting to int everywhere that we pass this to a BASS API.
    /// But that is far outweighed by the readability increase in the C# code.</remarks>
    public enum FxHandle : int
    {
    }
}
