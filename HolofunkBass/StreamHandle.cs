////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Mix;
using Un4seen.Bass.Misc;
using Un4seen.BassAsio;

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
