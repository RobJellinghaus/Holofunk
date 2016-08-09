////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Kinect;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk
{
    /// <summary>Scene graph elements specific to a menu invoked by an individual player.</summary>
    class PlayerMenuSceneGraph<TBaseModel> : SceneGraph
        where TBaseModel : Model
    {
        /// <summary>The parent player hand scene graph.</summary>
        readonly PlayerHandSceneGraph m_parent;

        /// <summary>The menu items for this menu.</summary>
        readonly PlayerMenuModel m_model;

        /// <summary>The center of the popup menu, e.g. the location at which the user clicked it open.</summary>
        readonly Vector2 m_center;

        readonly List<TextSpriteNode> m_menuItemNodes;

        internal PlayerMenuSceneGraph(PlayerHandSceneGraph parent, PlayerMenuModel<TBaseModel> menuModel, Vector2 center)
            : base()
        {
            m_parent = parent;
            m_model = menuModel;
            m_center = center;

            RootNode = new GroupNode(null, Transform.Identity, "PlayerMenuSceneGraph");

            m_menuItemNodes = new List<TextSpriteNode>();
            for (int i = 0; i < menuModel.MenuItemCount; i++) {
                Transform menuItemLocation = new Transform(GetLocation(i), new Vector2(MagicNumbers.MenuNodeScale)); // TODO: make this a constant
                TextSpriteNode menuItemNode = new TextSpriteNode(
                    RootNode,
                    menuItemLocation,
                    m_model[i].Label,
                    m_parent.Content.FilledCircle,
                    m_parent.Content.HollowCircle);

                menuItemNode.Enabled = menuModel.MenuItems[i].IsEnabled(menuModel.BaseModel);
                menuItemNode.SetSecondaryViewOption(SecondaryViewOption.PositionMirrored);

                m_menuItemNodes.Add(menuItemNode);
            }

            // attach the child atomically, so we don't see partly constructed menus
            RootNode.AttachTo(m_parent.RootNode);
        }

        /// <summary>
        /// Get the screen location of the menu item with the given index.
        /// </summary>
        internal Vector2 GetLocation(int index)
        {
            int menuItemCount = m_model.MenuItemCount;
            int textureRadius = (int)(m_parent.TextureRadius * MagicNumbers.MenuScale);
            HoloDebug.Assert(index < menuItemCount);

            if (menuItemCount == 1) {
                return m_center;
            }
            else if (menuItemCount == 2) {
                return m_center - new Vector2(0, - textureRadius / 2) + (index * new Vector2(0, textureRadius));
            }
            else {
                // we know how many items we have.  we want to position them along an equilateral polygon
                // with as many sides as items, such that each side of the polygon is as long as TextureRadius;
                // this ensures all the circular items will just touch each other, packed optimally along the
                // circumference.
                // The core problem is finding the radius of the polygon.  This reduces to finding the hypotenuse
                // of a right triangle with angle that's half of the circle divided by the number of sides,
                // and base that's half of the TextureRadius.
                double angle = 2 * Math.PI / menuItemCount / 2; // sure to be less than Math.PI / 4
                // Math.Sin(angle) = opp/hyp; 1 / Math.Sin(angle) = hyp/opp; well-defined in [0, PI/4]
                double polygonRadius = (1 / Math.Sin(angle)) * textureRadius / 2;
                
                // now the direction of the menu item in radians, with 0 being along positive x and the desired
                // zero menu item location being along negative Y
                double direction = (angle * 2) * index;
                Vector2 menuItemVector = new Vector2(
                    (float)(Math.Sin(direction) * polygonRadius), 
                    (float)(Math.Cos(direction) * polygonRadius));
                return m_center + menuItemVector;
            }
        }

        internal void Update(
            HolofunKinect kinect,
            Moment now)
        {
            for (int i = 0; i < m_menuItemNodes.Count; i++) {
                m_menuItemNodes[i].Selected = false;
            }
            if (m_model.SelectedMenuItem.HasValue) {
                m_menuItemNodes[m_model.SelectedMenuItem.Value].Selected = true;
            }
            for (int i = 0; i < m_menuItemNodes.Count; i++) {
                m_menuItemNodes[i].Update();
            }
        }
    }
}
