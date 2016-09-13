using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
/* Good nice FNA Stuff */
using SDL2;
using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

namespace FNATest
{
    public class FNATestGame : Game
    {
        GraphicsDeviceManager _manager;
        Texture2D _texture;
        SpriteFont _font;

        public FNATestGame()
        {
            _manager = new GraphicsDeviceManager(this);
            _manager.PreferredBackBufferWidth = 800;
            _manager.PreferredBackBufferHeight = 600;
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            _texture = Content.Load<Texture2D>("HollowFace0.png");
            _font = Content.Load<SpriteFont>("MoireBold14.xnb");
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(128, 128, 0, 255));

            using (SpriteBatch batch = new SpriteBatch(GraphicsDevice))
            {
                batch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);
                batch.Draw(
                    _texture,
                    new Vector2(20f, 20f),
                    new Microsoft.Xna.Framework.Rectangle(0, 0, _texture.Width, _texture.Height),
                    Color.White,
                    0.1f, // rotation
                    new Vector2(0, 0),
                    2.0f, // scale
                    SpriteEffects.None,
                    0);

                batch.DrawString(_font, "WOOOPEEE DOOOPEEE", new Vector2(100, 100), Color.Tomato);

                batch.End();
            }

            
        }
    }

    public partial class Form1 : Form
    {
        // thanks to https://gist.github.com/flibitijibibo/cf282bfccc1eaeb47550

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowPos(
            IntPtr handle,
            IntPtr handleAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags
        );

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

        [DllImport("user32.dll")]
        private static extern IntPtr ShowWindow(IntPtr handle, int command);

        public Form1()
        {
            Size = new Size(800, 600);

            FormClosing += new FormClosingEventHandler(WindowClosing);

            InitializeComponent();

            // start game
            {
                new Thread(GameThread).Start();

                while (!_gameStarted)
                {
                    Thread.Sleep(10);
                }
            }

            Panel gamePanel = new Panel();
            gamePanel.Bounds = System.Drawing.Rectangle.FromLTRB(0, 0, ClientSize.Width, ClientSize.Height);
            gamePanel.Dock = DockStyle.Fill;
            Controls.Add(gamePanel);

            // set window
            {
                _game.Window.IsBorderlessEXT = true;

                SDL.SDL_SysWMinfo info = new SDL.SDL_SysWMinfo();
                SDL.SDL_GetWindowWMInfo(_game.Window.Handle, ref info);

                IntPtr winHandle = info.info.win.window;

                SetWindowPos(
                    winHandle,
                    Handle,
                    0,
                    0,
                    0,
                    0,
                    0x0401 // NOSIZE | SHOWWINDOW

                );

                SetParent(winHandle, gamePanel.Handle);

                ShowWindow(winHandle, 1); // SHOWNORMAL
            }
        }

        private static FNATestGame _game;
        private static bool _gameStarted;

        private void WindowClosing(object sender, FormClosingEventArgs e)
        {

            _game.Exit();
        }

        private static void GameThread()
        {
            using (_game = new FNATestGame())
            {
                _gameStarted = true;
                _game.Run();
            }

        }
    }
}
