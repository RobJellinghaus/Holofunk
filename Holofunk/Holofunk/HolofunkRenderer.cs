////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Holofunk
{
    /// <summary>Renders the secondary view.</summary>
    public class HolofunkRenderer
    {
        SpriteBatch m_spriteBatch;

        public HolofunkRenderer(HolofunkGame game)
        {
            BackgroundColor = Color.Black;
            ForegroundColor = Color.Red;

            /*
            PreferredBackBufferFormat = PixelFormat.R8G8B8A8.UNorm;
            PreferredBackBufferWidth = (int)(game.ViewportSize.X);
            PreferredBackBufferHeight = (int)(game.ViewportSize.Y);
             */
        }

        /*
        /// <summary>Initializes a new instance of the <see cref="MiniTriRenderer" /> class.</summary>
        protected override void LoadContent()
        {
            object nativeWindow = Window.NativeWindow;
            System.Windows.Forms.Form asForm = nativeWindow as System.Windows.Forms.Form;

            Window.Title = "Holofunk Alpha";
            Window.AllowUserResizing = true;
        }
        */

        public Color BackgroundColor { get; set; }

        public Color ForegroundColor { get; set; }

        /*
        public override void Draw(GameTime gameTime)
        {
            if (m_spriteBatch == null) {
                m_spriteBatch = new SpriteBatch(GraphicsDevice);
            }

            Vector2 viewport = ((Holofunk)Game).ViewportSize;

            float scale = GraphicsDevice.BackBuffer.Height / viewport.Y;
            float scaledViewportWidth = viewport.X * scale;
            float scaledViewportOffset = (GraphicsDevice.BackBuffer.Width - scaledViewportWidth) / 2;
            Transform transform = new Transform(new Vector2(scaledViewportOffset, 0), new Vector2(scale));

            SpriteBatchWrapper wrapper = new SpriteBatchWrapper(m_spriteBatch, ((Holofunk)Game).ViewportSize, transform);

            ((Holofunk)Game).Render(
                ((Holofunk)Game).Now,
                GraphicsDevice, 
                wrapper, 
                gameTime, 
                ((Holofunk)Game).SecondaryView, 
                BackgroundColor);
        }
        */
    }
}
