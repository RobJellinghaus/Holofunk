////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2013 by Rob Jellinghaus.                        //
// Licensed under the Microsoft Public License (MS-PL).               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WiimoteLib;

namespace Holofunk
{
    /// <summary>Immutable struct describing a state of a Wii controller.</summary>
    public struct WiiState
    {
        // True if button is down, false if up.
        public readonly bool ButtonA;
        public readonly bool ButtonB;
        public readonly bool Minus;
        public readonly bool Plus;
        public readonly bool Home;
        public readonly bool Down;
        public readonly bool Up;
        public readonly bool Left;
        public readonly bool Right;
        public readonly bool One;
        public readonly bool Two;
        public readonly float BatteryLevel;

        internal WiiState(WiimoteState ws)
        {
            ButtonA = ws.ButtonState.A;
            ButtonB = ws.ButtonState.B;
            Minus = ws.ButtonState.Minus;
            Plus = ws.ButtonState.Plus;
            Home = ws.ButtonState.Home;
            Down = ws.ButtonState.Down;
            Up = ws.ButtonState.Up;
            Left = ws.ButtonState.Left;
            Right = ws.ButtonState.Right;
            One = ws.ButtonState.One;
            Two = ws.ButtonState.Two;
            BatteryLevel = ws.Battery;
        }
    }

    /// <summary>Controls a Wiimote, and translates its internal raw state updates to
    /// button press events.</summary>
    public class WiimoteController
    {
        Wiimote m_wiimote;

        WiiState m_lastState;

        public WiimoteController(Wiimote wiimote)
        {
            m_wiimote = wiimote;

            m_wiimote.WiimoteChanged += new EventHandler<WiimoteChangedEventArgs>(m_wiimote_WiimoteChanged);
        }

        // A delegate type for hooking up button change notifications.
        public delegate void ButtonEventHandler(bool state);

        // A delegate type for hooking up changes to any state.
        public delegate void StateEventHandler(WiiState oldState, WiiState newState);

        public event ButtonEventHandler ButtonAChanged;
        public event ButtonEventHandler ButtonBChanged;
        public event ButtonEventHandler MinusChanged;
        public event ButtonEventHandler PlusChanged;
        public event ButtonEventHandler HomeChanged;
        public event ButtonEventHandler DownChanged;
        public event ButtonEventHandler UpChanged;
        public event ButtonEventHandler LeftChanged;
        public event ButtonEventHandler RightChanged;
        public event ButtonEventHandler OneChanged;
        public event ButtonEventHandler TwoChanged;
        public event StateEventHandler StateChanged;

        public float BatteryLevel
        {
            get { return m_lastState.BatteryLevel; }
        }

        void m_wiimote_WiimoteChanged(object sender, WiimoteChangedEventArgs e)
        {
            WiiState newState = new WiiState(e.WiimoteState);

            if (StateChanged != null) {
                StateChanged(m_lastState, newState);
            }

            if (newState.ButtonA != m_lastState.ButtonA && ButtonAChanged != null) {
                ButtonAChanged(newState.ButtonA);
            }
            if (newState.ButtonB != m_lastState.ButtonB && ButtonBChanged != null) {
                ButtonBChanged(newState.ButtonB);
            }
            if (newState.Minus != m_lastState.Minus && MinusChanged != null) {
                MinusChanged(newState.Minus);
            }
            if (newState.Plus != m_lastState.Plus && PlusChanged != null) {
                PlusChanged(newState.Plus);
            }
            if (newState.Home != m_lastState.Home && HomeChanged != null) {
                HomeChanged(newState.Home);
            }
            if (newState.Up != m_lastState.Up && UpChanged != null) {
                UpChanged(newState.Up);
            }
            if (newState.Down != m_lastState.Down && DownChanged != null) {
                DownChanged(newState.Down);
            }
            if (newState.Left != m_lastState.Left && LeftChanged != null) {
                LeftChanged(newState.Left);
            }
            if (newState.Right != m_lastState.Right && RightChanged != null) {
                RightChanged(newState.Right);
            }
            if (newState.One != m_lastState.One && OneChanged != null) {
                OneChanged(newState.One);
            }
            if (newState.Two != m_lastState.Two && TwoChanged != null) {
                TwoChanged(newState.Two);
            }
            m_lastState = newState;
        }

        internal WiiState State { get { return m_lastState; } }
    }
}
