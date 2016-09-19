////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;

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

