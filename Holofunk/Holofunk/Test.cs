////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Holofunk.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Holofunk
{
    class Test : TestSuite
    {
        readonly GraphicsDevice m_device;
        // We have to have a GraphicsDevice to get even a basic Texture2D, and we do not want to mock
        // every damn XNA type in existence.
        internal Test(GraphicsDevice device) { m_device = device; }

        public void TestSceneGraph()
        {
            MockSpriteBatch batch = new MockSpriteBatch(Log);

            Texture2D tex = new Texture2D(m_device, 10, 10, mipMap: false, format: SurfaceFormat.Color);

            batch.Draw(tex, new Rectangle(20, 30, 20, 20), Color.AliceBlue);

            Log.CheckOnly("[Draw 10x10 @ (20,30)-(40,50) in {R:240 G:248 B:255 A:255}]");
        }

    }
}
