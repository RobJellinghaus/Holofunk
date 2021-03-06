﻿////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Xna.Framework;

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
