////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.SceneGraphs
{
    /// <summary>Extension methods of general utility.</summary>
    public static class ExtensionMethods
    {
        public static string FormatToString(this Rectangle rect)
        {
            return string.Format("[{0},{1} - {2},{3}]", rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        public static Vector2 Clamp(this Rectangle rect, Vector2 point)
        {
            Vector2 ret = new Vector2(
                Math.Max(rect.Left, Math.Min(rect.Right, point.X)),
                Math.Max(rect.Top, Math.Min(rect.Bottom, point.Y)));
            return ret;
        }
    }
}
