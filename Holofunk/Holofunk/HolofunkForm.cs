////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using SDL2;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Holofunk
{
    class HolofunkForm : Form
    {
        readonly HolofunkGame _holofunkGame;

        /// <summary>
        /// Is this the secondary form?
        /// </summary>
        /// <remarks>
        /// There are two HolofunkForms: the primary and the secondary.  The primary form is the performer-
        /// facing form; the secondary form is the secondary-monitor, audience-facing form.
        /// </remarks>
        readonly bool _isSecondaryForm;

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

        public HolofunkForm(HolofunkGame holofunkGame, bool isSecondaryForm)
        {
            _holofunkGame = holofunkGame;
            _isSecondaryForm = isSecondaryForm;

            // Hardcode to 1080p size of Kinect color buffer for now
            Size = new Size(1920, 1080);

            FormClosing += new FormClosingEventHandler(WindowClosing);

            InitializeComponent();

            Panel gamePanel = new Panel();
            gamePanel.Bounds = System.Drawing.Rectangle.FromLTRB(0, 0, ClientSize.Width, ClientSize.Height);
            gamePanel.Dock = DockStyle.Fill;
            Controls.Add(gamePanel);

            // set window
            {
                _holofunkGame.Window.IsBorderlessEXT = true;

                SDL.SDL_SysWMinfo info = new SDL.SDL_SysWMinfo();
                SDL.SDL_GetWindowWMInfo(_holofunkGame.Window.Handle, ref info);

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

        private void WindowClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isSecondaryForm)
            {
                _holofunkGame.Exit();
            }
        }

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Text = "Form1";
        }

        #endregion
    }
}
