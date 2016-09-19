////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Holofunk.SceneGraphs
{
    /// <summary>Factory for creating various textures.</summary>
    public static class TextureFactory
    {
        /// <summary>Create a texture which has C0 in the upper left corner,
        /// C1 in the upper right corner, C2 in the lower left corner,
        /// and C3 in the lower right corner, with a smooth shading 
        /// between all points.</summary>
        public static Texture2D ShadedCornerColor(
            GraphicsDevice graphicsDevice, 
            Vector2 size, 
            Color c0, 
            Color c1, 
            Color c2, 
            Color c3)
        {
            HoloDebug.Assert(size.X > 0);
            HoloDebug.Assert(size.Y > 0);

            Vector3 v0 = c0.ToVector3();
            Vector3 v1 = c1.ToVector3();
            Vector3 v2 = c2.ToVector3();
            Vector3 v3 = c3.ToVector3();

            // The maximum sum of all distances is 2 + (2 ^ 0.5) (any corner).
            float sqrt2 = (float)Math.Sqrt(2);

            // RGBA32 format
            Color[] data = new Color[(int)size.X * (int)size.Y];

            for (int j = 0; j < size.Y; j++) {
                for (int i = 0; i < size.X; i++) {
                    float fi = (float)i / (float)(size.X - 1);
                    float fj = (float)j / (float)(size.Y - 1);

                    Vector2 currentPoint = new Vector2(fi, fj);

                    float dist0 = Math.Max(0, 1 - Vector2.Distance(new Vector2(0, 0), currentPoint));
                    float dist1 = Math.Max(0, 1 - Vector2.Distance(new Vector2(1, 0), currentPoint));
                    float dist2 = Math.Max(0, 1 - Vector2.Distance(new Vector2(0, 1), currentPoint));
                    float dist3 = Math.Max(0, 1 - Vector2.Distance(new Vector2(1, 1), currentPoint));

                    Vector3 vectorSum = (v0 * dist0) + (v1 * dist1) + (v2 * dist2) + (v3 * dist3);
                    Vector4 vectorSum1 = new Vector4(vectorSum, 1.0f);
                    Color color = new Color(vectorSum1);

                    data[i + (j * (int)size.X)] = color;
                }
            }

            Texture2D ret = new Texture2D(graphicsDevice, (int)size.X, (int)size.Y, mipMap: true, format: SurfaceFormat.Color);
            ret.SetData(data);
            return ret;
        }
    }
}
