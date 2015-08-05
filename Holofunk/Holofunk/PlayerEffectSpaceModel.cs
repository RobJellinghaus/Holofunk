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
    /// <summary>The parameters for each axis in effect space.</summary>
    /// <remarks>Clockwise from Up.</remarks>
    class EffectSettings
    {
        internal readonly ParameterMap Up, Right, Down, Left;
        internal readonly string UpLabel, RightLabel, DownLabel, LeftLabel;

        internal EffectSettings(string[] labels, ParameterMap[] parameterMaps)
        {
            UpLabel = labels[0];
            RightLabel = labels[1];
            DownLabel = labels[2];
            LeftLabel = labels[3];

            Up = parameterMaps[0];
            Right = parameterMaps[1];
            Down = parameterMaps[2];
            Left = parameterMaps[3];
        }
    }

    /// <summary>The effect space state.</summary>
    /// <remarks>This is written to support being in effect space without dragging, though we don't
    /// currently use it that way.</remarks>
    class PlayerEffectSpaceModel : Model
    {
        #region Fields

        static ParameterMap Map(ParameterDescription description, float value)
        {
            return new ParameterMap().Add(new ConstantParameter(description, value, false));
        }

        static ParameterMap Map2(ParameterDescription description1, float value1, ParameterDescription description2, float value2)
        {
            return new ParameterMap().Add(new ConstantParameter(description1, value1, false))
                .Add(new ConstantParameter(description2, value2, false));
        }

        public static EffectSettings[] EffectSettings = new[] {
            new EffectSettings(
                new[] { "Loud", "Pan R", "Soft", "Pan L" },
                new[] { 
                    Map(VolumeEffect.Volume, 1),
                    Map(PanEffect.Pan, 1),
                    Map(VolumeEffect.Volume, 0),
                    Map(PanEffect.Pan, 0)
                }),
            new EffectSettings(
                new[] { 
                    TurnadoAAA1Effect.Parameters[0].Name,
                    TurnadoAAA1Effect.Parameters[1].Name,
                    TurnadoAAA1Effect.Parameters[2].Name,
                    TurnadoAAA1Effect.Parameters[3].Name,
                },
                new[] { 
                    Map(TurnadoAAA1Effect.Parameters[0], 1),
                    Map(TurnadoAAA1Effect.Parameters[1], 1),
                    Map(TurnadoAAA1Effect.Parameters[2], 1),
                    Map(TurnadoAAA1Effect.Parameters[3], 1)
                }),
            new EffectSettings(
                new[] {
                    TurnadoAAA1Effect.Parameters[4].Name,
                    TurnadoAAA1Effect.Parameters[5].Name,
                    TurnadoAAA1Effect.Parameters[6].Name,
                    TurnadoAAA1Effect.Parameters[7].Name,
                },
                new[] { 
                    Map(TurnadoAAA1Effect.Parameters[4], 1),
                    Map(TurnadoAAA1Effect.Parameters[5], 1),
                    Map(TurnadoAAA1Effect.Parameters[6], 1),
                    Map(TurnadoAAA1Effect.Parameters[7], 1)
                }),
            new EffectSettings(
                new[] { "Flanger", "Echo", "Reverb", "Chorus" },
                new[] { 
                    Map(FlangerEffect.WetDry, 1),
                    Map(EchoEffect.Wet, 1),
                    Map(ReverbEffect.Mix, 1),
                    Map(ChorusEffect.Wet, 1)
                }),
            new EffectSettings(
                new[] { "HPF", "", "LPF", "" },
                new[] { 
                    Map(HPFEffect.Frequency, 1),
                    Map(DistortionEffect.Wet, 1),
                    Map(LPFEffect.Frequency, 1),
                    Map(CompressionEffect.Threshold, 1)
                })

        };

        readonly PlayerHandModel m_parent;

        readonly PlayerEffectSpaceSceneGraph m_sceneGraph;

        /// <summary>The base parameter values when we began dragging.</summary>
        ParameterMap m_baseParameters;

        /// <summary>The parameter map mutated by this interface while dragging.</summary>
        ParameterMap m_parameters;

        /// <summary>The effect settings currently in effect.</summary>
        EffectSettings m_effectSettings;

        /// <summary>If we are dragging, this is where we started.</summary>
        Option<Vector2> m_dragStartLocation;

        /// <summary>If we are dragging, this is where the effect knob is currently located.</summary>
        Option<Vector2> m_currentKnobLocation;

        bool m_microphoneSelected;

        #endregion

        internal PlayerEffectSpaceModel(PlayerHandModel playerHandModel)
        {
            m_parent = playerHandModel;
            m_effectSettings = EffectSettings[playerHandModel.EffectPresetIndex];

            m_sceneGraph = new PlayerEffectSpaceSceneGraph(m_parent.SceneGraph, this);

            // m_parent.SceneGraph.SetEffectLabelNode(m_sceneGraph.BoundingCircleNode);

            m_microphoneSelected = playerHandModel.OtherArmPose == ArmPose.AtMouth;
        }

        #region Properties

        internal Option<Vector2> DragStartLocation
        {
            get { return m_dragStartLocation; }
            set { m_dragStartLocation = value; }
        }

        internal Option<Vector2> CurrentKnobLocation
        {
            get { return m_currentKnobLocation; }
            set { m_currentKnobLocation = value; }
        }

        internal bool MicrophoneSelected
        {
            get { return m_microphoneSelected; }
            set { m_microphoneSelected = value; }
        }

        internal PlayerHandModel ExtractAndDispose()
        {
            m_sceneGraph.RootNode.Detach();
            return m_parent;
        }

        #endregion

        public void InitializeParametersFromLoops()
        {
            m_parameters = AllEffects.CreateParameterMap();
            m_parent.UpdateParameterMapFromTouchedLoopieValues(m_parameters);
            m_baseParameters = m_parameters.Copy();
        }

        public void InitializeParametersFromMicrophone()
        {
            m_parameters = m_parent.MicrophoneParameters.Copy(forceMutable: true);
            m_baseParameters = m_parameters.Copy();
        }

        public void ShareLoopParameters()
        {
            m_parent.ShareLoopParameters(m_parameters);
        }

        internal void FlushLoopParameters()
        {
            m_parent.ShareLoopParameters(m_parameters.Copy());
        }

        internal void ShareMicrophoneParameters()
        {
            m_parent.MicrophoneParameters.ShareAll(m_parameters);
        }

        internal void FlushMicrophoneParameters()
        {
            m_parent.MicrophoneParameters.ShareAll(m_parameters.Copy());
        }

        public override void GameUpdate(Moment now)
        {
            if (!m_dragStartLocation.HasValue) {
                HoloDebug.Assert(!m_currentKnobLocation.HasValue);

                // when not dragging, we handle all selection, etc. as usual; 
                // e.g. we delegate to the usual player code
                m_parent.GameUpdate(now);
                m_parent.SceneGraph.Update(m_parent, m_parent.Kinect, now);
            }
            else {
                if (MicrophoneSelected) {
                    // when the mike is being dragged, we don't have any touched loopies
                    m_parent.TouchedLoopies.Clear();
                }
            
                HoloDebug.Assert(m_currentKnobLocation.HasValue);

                Vector2 knobDelta = GetKnobDelta(m_parent.HandPosition);
                m_currentKnobLocation = m_dragStartLocation.Value + knobDelta;

                RecalculateDragEffect(now, knobDelta);

                m_parent.UpdateFromChildState(now);
            }

            m_sceneGraph.Update(m_parent, m_parent.Kinect, now);
        }

        /// <summary>Get the vector from the drag start location to the knob, given the current hand position.</summary>
        Vector2 GetKnobDelta(Vector2 currentDragLocation)
        {
            Vector2 delta = currentDragLocation - m_dragStartLocation.Value;

            // Now what we want is to keep the angle of the delta, but clamp its length.
            if (delta.Length() > BoundingCircleRadius) {
                // we want to normalize delta and then clamp it at boundingCircleRadius
                delta.Normalize();
                delta *= BoundingCircleRadius;
            }

            return delta;
        }

        float BoundingCircleRadius
        {
            get
            {
                float boundingCircleRadius = m_parent.SceneGraph.Content.HollowCircle.Width
                    * MagicNumbers.EffectSpaceBoundingCircleMultiple
                    / 2;
                return boundingCircleRadius;
            }
        }

        internal void StartDragging()
        {
            Rectangle boundingRect = new Rectangle(
                (int)BoundingCircleRadius,
                (int)BoundingCircleRadius,
                (int)(m_parent.SceneGraph.ViewportSize.X - BoundingCircleRadius),
                (int)(m_parent.SceneGraph.ViewportSize.Y - BoundingCircleRadius));

            Vector2 withinBounds = boundingRect.Clamp(m_parent.HandPosition);

            DragStartLocation = withinBounds;
            CurrentKnobLocation = withinBounds;
        }

        internal void StopDragging()
        {
            DragStartLocation = Option<Vector2>.None;
            CurrentKnobLocation = Option<Vector2>.None;
        }

        /// <summary>The user's dragged the knob; update the effects appropriately.</summary>
        void RecalculateDragEffect(Moment now, Vector2 knobDelta)
        {
            // First, we want to map knobDelta -- that is effectively a vector to the bounding circle --
            // to be a vector to the unit square.
            Vector2 normalizedDelta = knobDelta;
            normalizedDelta.Normalize();

            // Now we want to find the longest dimension of normalizedDelta, and increase it to 1.
            float longestDimension = Math.Max(Math.Abs(normalizedDelta.X), Math.Abs(normalizedDelta.Y));

            if (longestDimension < 0.0001f) {
                // might as well just be zero, so leave normalizedDelta alone
            }
            else {
                float longestDimensionMultiplier = 1 / longestDimension;

                // Scaling a vector does not change its angle.
                normalizedDelta *= longestDimensionMultiplier;
            }

            // Now normalizedDelta is effectively the vector to the unit square!  Leave a little epsilon at the limit...
            HoloDebug.Assert(Math.Abs(normalizedDelta.X) <= 1.0001f);
            HoloDebug.Assert(Math.Abs(normalizedDelta.Y) <= 1.0001f);

            // Finally, the vector we really want is normalizedDelta multiplied by 
            // knobDelta's length divided by the circle's radius.
            float lengthFraction = knobDelta.Length() / BoundingCircleRadius;
            Vector2 actualDelta = normalizedDelta * lengthFraction;

            Spam.Model.WriteLine("normalizedDelta: (" + normalizedDelta.X + ", " + normalizedDelta.Y + "); lengthFraction " + lengthFraction + "; actualDelta: (" + actualDelta.X + ", " + actualDelta.Y + ")");

            // OK, now we have our X and Y values.  Figure out which we are going to apply.
            ParameterMap vertical = normalizedDelta.Y < 0 ? m_effectSettings.Up : m_effectSettings.Down;
            ParameterMap horizontal = normalizedDelta.X < 0 ? m_effectSettings.Left : m_effectSettings.Right;

            foreach (Parameter p in vertical) {
                DragParameter(now, horizontal, p, Math.Abs(actualDelta.Y));
            }

            foreach (Parameter p in horizontal) {
                DragParameter(now, null, p, Math.Abs(actualDelta.X));
            }
        }

        void DragParameter(Moment now, ParameterMap other, Parameter p, float value)
        {
            // We have a tiny gutter around 0, since some effects have a sharp step from 0 to anything else and
            // this sounds more jarring if it happens with no tolerance.
            if (value < 0.1f) {
                value = 0;
            }
            // We deliberately use SSA-like structure here to be able to see all the intermediate values in the debugger.
            // First, get the endpoint value of p
            float pValue = p[now.Time];
            // Next, get the base value of p, in normalized terms
            ParameterDescription pDesc = p.Description;
            float pBaseValueNormalized = pDesc.Min == pDesc.Max ? pDesc.Base : (pDesc.Base - pDesc.Min) / (pDesc.Max - pDesc.Min);

            // Now we want to move from pBaseValueNormalized towards pValue, by an amount proportional to dragValue
            float newDestValue = pBaseValueNormalized + ((pValue - pBaseValueNormalized) * value);

            Parameter dest = m_parameters[p.Description];
            if (m_baseParameters.Contains(p.Description)) {
                float baseValue = m_baseParameters[p.Description][now.Time];

                float averagedDestValue = newDestValue;
                if (other != null && other.Contains(p.Description)) {
                    averagedDestValue = (averagedDestValue + other[p.Description][now.Time]) / 2;
                }

                float adjustedDestValue = averagedDestValue;
                if (!p.Description.Absolute) {
                    // only grow upwards from the base value, proportionately to how big the base value already is
                    adjustedDestValue = baseValue + (averagedDestValue * (1 - baseValue));
                }

                Spam.Model.WriteLine("parameter " + p.Description.Name + ": pValue " + pValue + ", pBVN " + pBaseValueNormalized + ", baseValue " + baseValue + ", adjustedDestValue " + adjustedDestValue);

                // clamp to [0, 1] because we may overshoot a bit with this algorithm
                dest[now.Time] = Math.Max(0, Math.Min(1f, adjustedDestValue));
            }
        }
    }
}
