////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Windows.Forms;

namespace Holofunk
{
    static class Program
    {
        /// <summary>The main entry point for the application.</summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Force FNA to not load audio.
            // set FNA_AUDIO_DISABLE_SOUND=1
            Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HolofunkForm(false));
        }
    }
}

