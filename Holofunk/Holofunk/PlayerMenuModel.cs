////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using SharpDX;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Base class for menu items; exposes only the label.</summary>
    /// <remarks>This lets the scene graph avoid having to deal with polymorphic Action types.
    /// (In other words, the scene graph only cares what the menu items look like, not what
    /// they do.)</remarks>
    class MenuItem
    {
        internal readonly string Label;

        internal MenuItem(string label)
        {
            Label = label;
        }
    }

    class MenuItem<TModel> : MenuItem
        where TModel : Model
    {
        internal readonly Action<TModel> Action;
        internal readonly List<MenuItem<TModel>> SubItems;
        internal readonly Func<TModel, bool> EnabledFunc;

        /// <summary>Construct a menu item which takes this action when picked.</summary>
        internal MenuItem(string label, Action<TModel> action, Func<TModel, bool> enabledFunc = null)
            : base(label)
        {
            Action = action;
            SubItems = new List<MenuItem<TModel>>();
            EnabledFunc = enabledFunc;
        }

        /// <summary>Menu item with sub-items; gets a no-op action.</summary>
        internal MenuItem(string label, MenuItem<TModel>[] subItems)
            : this(label, model => { })
        {
            SubItems.AddRange(subItems);
        }

        internal bool IsEnabled(TModel model)
        {
            return EnabledFunc == null || EnabledFunc(model);
        }
    }

    /// <summary>Base class for player menu models:  allow access to the menu items without
    /// exposing the specific TBaseModel type.</summary>
    abstract class PlayerMenuModel : Model
    {
        internal abstract int MenuItemCount { get; }
        internal abstract MenuItem this[int menuItemIndex] { get; }
        internal abstract Option<int> SelectedMenuItem { get; }
    }

    /// <summary>The loopie-related state of a player's open menu.</summary>
    /// <remarks>Eventually hierarchical, but not at first.
    /// 
    /// This is intended to be an "extension model" that can be used on top of any "base model" to
    /// define actions specific to that base mode.  For example, this lets an effects model have a menu
    /// that knows how to update that specific type of effects model.</remarks>
    class PlayerMenuModel<TBaseModel> : PlayerMenuModel
        where TBaseModel : Model
    {
        readonly TBaseModel m_baseModel;
        readonly PlayerHandModel m_playerHandModel;
        readonly List<MenuItem<TBaseModel>> m_menuItems = new List<MenuItem<TBaseModel>>();
        readonly PlayerMenuSceneGraph<TBaseModel> m_sceneGraph;
        // The currently selected menu item, if any.
        Option<int> m_selectedMenuItem;

        /// <summary>Construct a PlayerMenuModel that extends the given base model, located at the given center
        /// position, with the given items.</summary>
        /// <param name="baseModel">The base model that will be passed to the menu item actions.</param>
        /// <param name="playerHandModel">The player hand model which this menu will use for tracking interaction.</param>
        /// <param name="parentPlayerHandSceneGraph">The player hand scene graph from which this depends.</param>
        /// <param name="center">The location the menu is being popped up at.</param>
        /// <param name="items">The menu items (with their actions).</param>
        internal PlayerMenuModel(
            TBaseModel baseModel,
            PlayerHandModel playerHandModel,
            PlayerHandSceneGraph parentPlayerHandSceneGraph, 
            Vector2 center, 
            params MenuItem<TBaseModel>[] items)
        {
            HoloDebug.Assert(items.Length > 0);
            m_baseModel = baseModel;
            m_playerHandModel = playerHandModel;
            foreach (MenuItem<TBaseModel> item in items) {
                m_menuItems.Add(item);
            }
            
            m_selectedMenuItem = Option<int>.None;

            m_sceneGraph = new PlayerMenuSceneGraph<TBaseModel>(parentPlayerHandSceneGraph, this, center);
        }

        internal List<MenuItem<TBaseModel>> MenuItems { get { return m_menuItems; } }

        internal TBaseModel BaseModel { get { return m_baseModel; } }

        internal TBaseModel ExtractAndDetach()
        {
            m_sceneGraph.RootNode.Detach();
            return m_baseModel;
        }

        internal override Option<int> SelectedMenuItem { get { return m_selectedMenuItem; } }
        internal override int MenuItemCount { get { return m_menuItems.Count; } }
        internal override MenuItem this[int menuItemIndex] { get { return m_menuItems[menuItemIndex]; } }

        public override void GameUpdate(Moment now)
        {
            m_playerHandModel.UpdateFromChildState(now);

            string topLine = "";

            // first, need to query the scene graph to see what menu item the remote hand is over
            Vector2 handLocation = m_playerHandModel.HandPosition;

            // topLine = topLine + "Hand location: " + (int)handLocation.X + "," + (int)handLocation.Y;

            // see if it is within TextureRadius of any of the menu items; if so, that one's selected
            float minDist = float.MaxValue;
            for (int i = 0; i < MenuItemCount; i++) {
                Vector2 menuItemLocation = m_sceneGraph.GetLocation(i);
                float distSquared = (menuItemLocation - handLocation).LengthSquared();

                // topLine = topLine + "\nMenu item " + i + ": " + (int)menuItemLocation.X + "," + (int)menuItemLocation.Y + " -- distSq " + (int)distSquared;

                if (distSquared < minDist) {
                    minDist = distSquared;
                    m_selectedMenuItem = i;
                    // topLine = topLine + " [HIT]";
                }
            }

            float radiusSquared = m_playerHandModel.SceneGraph.TextureRadius * m_playerHandModel.SceneGraph.TextureRadius;

            // If we are too far from the closest item, or the closest item is disabled, then nothing is selected.
            if (minDist > radiusSquared
                || (m_menuItems[m_selectedMenuItem.Value].EnabledFunc != null 
                    && !m_menuItems[m_selectedMenuItem.Value].EnabledFunc(m_baseModel))) {
                m_selectedMenuItem = Option<int>.None;

                // topLine = topLine + "\nCANCELED DUE TO RADIUS";
            }

            // now update the scene graph since the selected item is set properly
            m_sceneGraph.Update(m_playerHandModel.Kinect, now);
        }

        /// <summary>The user is leaving menu state; run the action of the selected item (if any item is selected).</summary>
        internal void RunSelectedMenuItem()
        {
            if (m_selectedMenuItem.HasValue) {
                m_menuItems[m_selectedMenuItem.Value].Action(m_baseModel);
            }
        }
    }
}
