////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Holofunk.Tests
{
    /// <summary>Mocking class that logs drawing messages.</summary>
    public class MockSpriteBatch // : ISpriteBatch
    {
        Logger m_logger;

        public MockSpriteBatch(Logger logger) { m_logger = logger; }

        #region ISpriteBatch Members

        public void Draw(Texture2D texture, Rectangle rect, Color color)
        {
            m_logger.Log("[Draw " + texture.Width + "x" + texture.Height 
                + " @ (" + rect.Left + "," + rect.Top + ")-(" + rect.Right + "," + rect.Bottom + 
                ") in " + color.ToString() + "]");
        }

        #endregion
    }
}
