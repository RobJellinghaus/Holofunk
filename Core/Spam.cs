////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace Holofunk.Core
{
    public class SpamCategory
    {
        readonly List<string> Output = new List<string>();

        internal void WriteLine(string s)
        {
            return;

            const int chunkSize = 100;
            const int numChunks = 20;

            lock (Output) {
                if (Output.Count() > (chunkSize * numChunks)) {
                    Output.RemoveRange(0, chunkSize);
                }

                 Output.Add(s);
            }
        }

    }

    /// <summary>Internal text logging, for maximal speed while still allowing viewing under debugger.</summary>
    public static class Spam
    {
        // Mutate this to print something persistent on the screen.
        // Spam is too hard to view interactively.
        public static string TopLine1;
        public static string TopLine2;

        static readonly SpamCategory s_graphics = new SpamCategory();
        static readonly SpamCategory s_audio = new SpamCategory();
        static readonly SpamCategory s_model = new SpamCategory();
        static readonly SpamCategory s_all = new SpamCategory();

        // can look at this in the debugger; Console output hoses us completely (debugger too slow)
        static readonly List<string> s_output = new List<string>();

        // These inner classes let us comment in or out whole categories of spam by making local
        // edits here.
        public static class Graphics
        {
            [Conditional("SPAMGRAPHICS")]
            public static void WriteLine(string s)
            {
                s_graphics.WriteLine(s);
                s_all.WriteLine(s);
            }

            [Conditional("SPAMGRAPHICS")]
            public static void WriteLine()
            {
                WriteLine("");
            }
        }

        // These inner classes let us comment in or out whole categories of spam by making local
        // edits here.
        public static class Audio
        {
            [Conditional("SPAMAUDIO")]
            public static void WriteLine(string s)
            {
                s_audio.WriteLine(s);
                s_all.WriteLine(s);
            }

            [Conditional("SPAMAUDIO")]
            public static void WriteLine()
            {
                WriteLine("");
            }
        }

        public static class Model
        {
            [Conditional("SPAMMODEL")]
            public static void WriteLine(string s)
            {
                s_model.WriteLine(s);
                s_all.WriteLine(s);
            }

            [Conditional("SPAMMODEL")]
            public static void WriteLine()
            {
                WriteLine("");
            }
        }
    }
}
