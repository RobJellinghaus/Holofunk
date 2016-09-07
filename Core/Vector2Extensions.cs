////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Holofunk.Core
{
    public static class Vector2Extensions
    {
        public static bool CloserThan(this Vector2 thiz, Vector2 other, float distance)
        {
            Vector2 delta = other - thiz;
            return delta.X * delta.X + delta.Y * delta.Y < distance * distance;
        }
    }
}
