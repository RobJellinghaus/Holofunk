////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace Holofunk.StateMachines
{
    public static class ListExtensions
    {
        public static void AddFrom<T>(this List<T> thiz, List<T> other, int start, int count)
        {
            HoloDebug.Assert(thiz != null && other != null);
            HoloDebug.Assert(start >= 0 && count >= 0);
            HoloDebug.Assert(start + count <= other.Count);

            for (int i = start; i < start + count; i++) {
                thiz.Add(other[i]);
            }
        }
    }

    /// <summary>A running instantiation of a particular StateMachine.</summary>
    public class StateMachineInstance<TEvent> 
    {
        readonly StateMachine<TEvent> m_machine;
        State<TEvent> m_machineState;

        // The model is dependent on where in the hierarchy we are; it may be transformed on
        // entry or exit.
        Model m_model;

        // Reused stacks for finding common parents.
        readonly List<State<TEvent>> m_startList = new List<State<TEvent>>();
        readonly List<State<TEvent>> m_endList = new List<State<TEvent>>();
        readonly List<State<TEvent>> m_pathDownList = new List<State<TEvent>>();

        Moment m_lastTransitionMoment;

        public StateMachineInstance(TEvent initial, StateMachine<TEvent> machine, Model initialModel)
        {
            m_machine = machine;
            m_machineState = machine.RootState;
            m_model = initialModel;

            MoveTo(initial, machine.InitialState);
        }

        public Moment LastTransitionMoment { get { return m_lastTransitionMoment; } set { m_lastTransitionMoment = value; } }

        // We are in state start.  We need to get to state end.
        // Do so by performing all the exit actions necessary to get up to the common parent of start and end,
        // and then all the enter actions necessary to get down to end from that common parent.
        void MoveTo(TEvent evt, State<TEvent> end)
        {
            // if already there, do nothing
            if (m_machineState == end) {
                return;
            }

            // Get the common parent of start and end.
            // This will be null if they have no common parent.
            State<TEvent> commonParent = GetCommonParent(m_machineState, end, m_pathDownList);

            ExitUpTo(evt, m_machineState, commonParent);
            EnterDownTo(evt, m_pathDownList);

            m_machineState = end;
        }


        void ExitUpTo(TEvent evt, State<TEvent> state, State<TEvent> commonParent)
        {
            while (state != commonParent) {
                m_model = state.Exit(evt, m_model);
                state = state.Parent;
            }
        }

        void EnterDownTo(TEvent evt, List<State<TEvent>> pathToEnd)
        {
            for (int i = 0; i < pathToEnd.Count; i++) {
                m_model = pathToEnd[i].Enter(evt, m_model);
            }
        }

        State<TEvent> GetCommonParent(
            State<TEvent> start,
            State<TEvent> end,
            List<State<TEvent>> pathDownToEnd)
        {
            // we don't handle this case!
            HoloDebug.Assert(start != end);

            if (start == null || end == null) {
                return null;
            }

            // make a list of all states to root.
            // (actually, the lists wind up being ordered from root to the leaf state.)
            ListToRoot(start, m_startList);
            ListToRoot(end, m_endList);

            // now the common parent is the end of the longest common prefix.
            pathDownToEnd.Clear();
            for (int i = 0; i < Math.Min(m_startList.Count, m_endList.Count); i++) {
                if (m_startList[i] != m_endList[i]) {
                    if (i == 0) {
                        pathDownToEnd.AddFrom(m_endList, 0, m_endList.Count);
                        return null;
                    }
                    else {
                        pathDownToEnd.AddFrom(m_endList, i - 1, m_endList.Count - i + 1);
                        return m_startList[i - 1];
                    }
                }
            }

            // If we got to here, then one list is a prefix of the other.

            if (m_startList.Count > m_endList.Count) {
                // The start list is longer, so end contains (hierarchically speaking) start.
                // So there IS no pathDownToEnd, and the end of endList is the common parent.
                return m_endList[m_endList.Count - 1];
            }
            else {
                // m_endList is longer.
                pathDownToEnd.AddFrom(m_endList, m_startList.Count, m_endList.Count - m_startList.Count);
                return m_startList[m_startList.Count - 1];
            }
        }

        // Clear list and replace it with the ancestor chain of state, with the root at index 0.
        void ListToRoot(State<TEvent> state, List<State<TEvent>> list)
        {
            list.Clear();

            while (state != null) {
                list.Add(state);
                state = state.Parent;
            }

            list.Reverse();
        }

        public void OnCompleted()
        {
            // we don't do nothin' (yet)
        }

        public void OnError(Exception exception)
        {
            Debug.WriteLine(exception.ToString());
            Debug.WriteLine(exception.StackTrace);
            HoloDebug.Assert(false);
        }

        public void OnNext(TEvent value, Moment now)
        {
            // Find transition if any.
            var destination = m_machine.TransitionFrom(m_machineState, value, m_model);
            if (destination != null) {
                MoveTo(value, destination);
            }

            m_lastTransitionMoment = now;
        }

        public void GameUpdate(Moment now)
        {
            m_model.GameUpdate(now);
        }
    }
}
