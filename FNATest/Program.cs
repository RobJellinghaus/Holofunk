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
            // Force FNA to not load audio.
            // set FNA_AUDIO_DISABLE_SOUND=1
            Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
