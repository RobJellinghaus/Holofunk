////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.StateMachines
{
    public class Transition<TEvent>
    {
        readonly TEvent m_event;

        public Transition(TEvent evt)
        {
            m_event = evt;
        }

        public TEvent Event { get { return m_event; } }

    }

    /// <summary>A transition in a StateMachine.</summary>
    /// <remarks>Is labeled with an event, and contains a means to compute a destination state.</remarks>
    public class Transition<TEvent, TModel> : Transition<TEvent>
    {
        readonly Func<TEvent, TModel, State<TEvent>> m_destinationFunc;

        public Transition(
            TEvent evt,
            Func<TEvent, TModel, State<TEvent>> destinationFunc)
            : base(evt)
        {
            m_destinationFunc = destinationFunc;
        }

        public Transition(
            TEvent evt,
            State<TEvent> destinationState)
            : this(evt, (ignoreModel, ignoreEvent) => destinationState)
        {
        }

        /// <summary>
        /// compute the destination state
        /// </summary>
        public State<TEvent> ComputeDestination(TEvent evt, TModel model)
        {
            return m_destinationFunc(evt, model);
        }
    }
}
