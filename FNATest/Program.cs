using Microsoft.Xna.Framework;
using Color = Microsoft.Xna.Framework.Color;
using System;
using System.Windows.Forms;

namespace FNATest
{
    class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
