////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.BassAsio;

namespace Holofunk
{
    static class Program
    {
        /// <summary>The main entry point for the application.</summary>
        static void Main(string[] args)
        {
            // Force FNA to not load audio.
            // set FNA_AUDIO_DISABLE_SOUND=1
            Environment.SetEnvironmentVariable("FNA_AUDIO_DISABLE_SOUND", "1");

            using (HolofunkGame game = new HolofunkGame())
            {
                //var secondary = new HolofunkRenderer(game);
                //game.GameSystems.Add(secondary);

                game.Run();
            }
        }
    }
}

