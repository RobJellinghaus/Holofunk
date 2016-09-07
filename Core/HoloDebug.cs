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
    class HoloDebugException : Exception
    {
    }

    public class HoloDebug
    {
        /// <summary>Assertion dialogs can hose Holofunk; this trivial wrapper lets us breakpoint just before we dialog.</summary>
        /// <param name="value"></param>
        public static void Assert(bool value)
        {
            if (!value) {
                throw new HoloDebugException();
                // Debug.Assert(value);
            }
        }

        /// <summary>Assertion dialogs can hose Holofunk; this trivial wrapper lets us breakpoint just before we dialog.</summary>
        /// <param name="value"></param>
        public static void Assert(bool value, string message)
        {
            if (!value) {
                throw new HoloDebugException();
                // Debug.Assert(value, message);
            }
        }
    }
}
