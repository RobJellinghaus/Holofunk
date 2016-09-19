////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using System;
using System.Collections.Generic;

namespace Holofunk.StateMachines
{
    public abstract class State<TEvent>
    {
        readonly string m_label;

        protected State(string label)
        {
            m_label = label;
        }

        protected String Label { get { return m_label; } }

        /// <summary>
        /// Enter this state, passing in the model from the parent, and obtaining the new model.
        /// </summary>
        public abstract Model Enter(TEvent evt, Model parentModel);

        /// <summary>
        /// Exit this state, passing in the current model, and obtaining the model for the parent state.
        /// </summary>
        public abstract Model Exit(TEvent evt, Model parentModel);

        public abstract State<TEvent> Parent { get; }

        /// <summary>
        /// Compute where this transition goes.  Must use the dynamic Model type to avoid issues with finding transitions from
        /// parent states whose TParentModel is unknowable.
        /// </summary>
        public abstract State<TEvent> ComputeDestination(Transition<TEvent> transition, TEvent evt, Model model);


        /// <summary>
        /// Get the parent model of this State, given the current model.
        /// </summary>
        /// <returns></returns>
        public abstract Model GetParentModel(Model thisModel);
    }

    public abstract class State<TEvent, TModel> : State<TEvent>
        where TModel : Model
    {
        protected State(string label)
            : base(label)
        {
        }
    }

    /// <summary>A state in a StateMachine.</summary>
    /// <remarks>Contains entry and exit actions that reference the model.
    /// 
    /// The transitions are kept at the StateMachine level.
    /// 
    /// Note that the State has no idea that transitions even exist, nor that the
    /// StateMachine itself exists!  This lets States be constructed independently
    /// (modulo parent states being created before children).  Then the 
    /// StateMachine can be created with the full set of available states.</remarks>
    public class State<TEvent, TModel, TParentModel> : State<TEvent, TModel>
        where TModel : Model
        where TParentModel : Model
    {
        readonly State<TEvent, TParentModel> m_parent;

        readonly List<Action<TEvent, TModel>> m_entryActions;
        readonly List<Action<TEvent, TModel>> m_exitActions;

        readonly Func<TParentModel, TModel> m_entryConversionFunc;
        readonly Func<TModel, TParentModel> m_exitConversionFunc;

        public State(
            string label,
            State<TEvent, TParentModel> parent,
            Action<TEvent, TModel> entryAction,
            Action<TEvent, TModel> exitAction,
            Func<TParentModel, TModel> entryConversionFunc = null,
            Func<TModel, TParentModel> exitConversionFunc = null)
            : this(label, parent, new[] { entryAction }, new[] { exitAction }, entryConversionFunc, exitConversionFunc)
        {
        }

        public State(
            string label,
            State<TEvent, TParentModel> parent,
            Action<TEvent, TModel> entryAction,   
            Func<TParentModel, TModel> entryConversionFunc = null,
            Func<TModel, TParentModel> exitConversionFunc = null)
            : this(label, parent, new[] { entryAction }, new Action<TEvent, TModel>[0], entryConversionFunc, exitConversionFunc)
        {
        }

        public State(
            string label,
            State<TEvent, TParentModel> parent,
            Action<TEvent, TModel>[] entryActions, 
            Action<TEvent, TModel>[] exitActions,
            Func<TParentModel, TModel> entryConversionFunc = null,
            Func<TModel, TParentModel> exitConversionFunc = null)
            : base(label)
        {
            m_parent = parent;
            m_entryActions = new List<Action<TEvent, TModel>>(entryActions);
            m_exitActions = new List<Action<TEvent, TModel>>(exitActions);

            m_entryConversionFunc = entryConversionFunc;
            m_exitConversionFunc = exitConversionFunc;
        }

        public override State<TEvent> ComputeDestination(Transition<TEvent> transition, TEvent evt, Model model)
        {
            // Now we know our TModel and we can use it to regain strong typing on the Transition's destination computation.
            Transition<TEvent, TModel> modelTransition = (Transition<TEvent, TModel>)transition;
            return modelTransition.ComputeDestination(evt, (TModel)model);
        }

        public override Model Enter(TEvent evt, Model parentModel)
        {
            Spam.Model.WriteLine("State.Enter: state " + Label + ", event: " + evt + ", parentModel: " + parentModel.GetType());
            TModel thisState;
            if (m_entryConversionFunc != null) {
                thisState = m_entryConversionFunc((TParentModel)parentModel);
            }
            else {
                // had better be the same type!
                thisState = (TModel)parentModel;
            }
            for (int i = 0; i < m_entryActions.Count; i++) {
                m_entryActions[i](evt, thisState);
            }
            return thisState;
        }

        public override Model Exit(TEvent evt, Model model)
        {
            Spam.Model.WriteLine("State.Exit: state " + Label + ", event type: " + evt.GetType() + ", model.GetType(): " + model.GetType());
            TModel thisModel = (TModel)model;
            for (int i = 0; i < m_exitActions.Count; i++) {
                m_exitActions[i](evt, thisModel);
            }
            TParentModel parentModel;
            if (m_exitConversionFunc != null) {
                parentModel = m_exitConversionFunc(thisModel);
            }
            else {
                // Terrible, but meets the expectation: these must be dynamically the same type.
                parentModel = (TParentModel)(object)thisModel;
            }
            return parentModel;
        }

        public override State<TEvent> Parent { get { return m_parent; } }

        public override Model GetParentModel(Model thisModel)
        {
            if (m_exitConversionFunc != null) {
                return m_exitConversionFunc((TModel)thisModel);
            }
            else {
                return thisModel;
            }
        }
    }
}
