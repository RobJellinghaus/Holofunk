////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WiimoteLib;

namespace Holofunk
{
    /// <summary>Manages a connection to the (process-wide, if not system-wide) WiimoteLib.</summary>
    class WiimoteLib
    {
        readonly WiimoteCollection m_wiimoteCollection;
        readonly List<WiimoteController> m_wiimotes = new List<WiimoteController>();

        public WiimoteLib()
        {
            // find all wiimotes connected to the system
            m_wiimoteCollection = new WiimoteCollection();

            try {
                m_wiimoteCollection.FindAllWiimotes();
            }
            catch (WiimoteNotFoundException ex) {
                // this stops complaints about ex being unused, so we can look at it in debugger
                HoloDebug.Assert(ex == null);
            }
            catch (WiimoteException ex) {
                HoloDebug.Assert(ex == null);
            }
            catch (Exception ex) {
                HoloDebug.Assert(ex == null);
            }

            int index = 1;
            foreach (Wiimote wm in m_wiimoteCollection) {
                WiimoteController wi = new WiimoteController(wm);
                wm.Connect();
                wm.SetLEDs(index++);
                m_wiimotes.Add(wi);
            }
        }

        public List<WiimoteController> Wiimotes { get { return m_wiimotes; } }
    }
}
