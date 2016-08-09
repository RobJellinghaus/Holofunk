////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2014 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Holofunk.SceneGraphs
{
    /// <summary>A node class that draws little circles composed of quarters, one quarter per beat.</summary>
    /// <remarks>The node renders its drawing horizontally centered and below its target transform
    /// position.</remarks>
    public class BeatNode : ASceneNode
    {
        // how long is the track we purport to be rendering?
        readonly Func<Duration<Sample>> m_trackLengthFunc;

        // on what beat (since the beginning of time) did the "track" begin?
        // This is necessary to get the phase right for short tracks.
        readonly Func<int> m_initialBeatFunc;

        // what color is our base color?
        readonly Func<Color> m_colorFunc;

        // are we filling in every beat, or just the current beat?
        readonly bool m_fillInEveryBeat;

        // Scale factor for our textures (controls size of measure circles)
        readonly float m_textureScale;

        // what beat are we on now?  (mutated only by Update method)
        long m_currentBeat;
        // of how many beats?
        long m_totalBeats;
        // what fractional beat is it now?
        double m_fractionalBeat;
        
        public BeatNode(
            AParentSceneNode parent,
            Transform localTransform,
            string label,
            // are we filling in every beat, or just the current beat?
            bool fillInEveryBeat,
            // by how much do we scale down our texture when rendering a quarter note?
            float textureScale,
            Func<Duration<Sample>> trackLengthFunc,
            Func<int> initialBeatFunc,
            Func<Color> colorFunc)
            : base(parent, localTransform, label)
        {
            m_fillInEveryBeat = fillInEveryBeat;
            m_textureScale = textureScale;
            m_trackLengthFunc = trackLengthFunc;
            m_initialBeatFunc = initialBeatFunc;
            m_colorFunc = colorFunc;
        }

        public void Update(Moment now)
        {
            // what is our track length?
            Duration<Sample> duration = m_trackLengthFunc();
            Moment length = now.Clock.Time((int)duration);
            m_totalBeats = length.CompleteBeats;

            if (m_totalBeats == 0) {
                // there is no actual track duration here; we do nothing
                return;
            }

            // must be an exact number of beats
            //HoloDebug.Assert(length.TimepointsSinceLastBeat == 0);

            // what beat are we actually on now?
            long nowBeat = now.CompleteBeats;
            // what beat did we start at?
            int initialBeat = m_initialBeatFunc();

            // if we got a -1 for initial beat, we also don't really exist
            // (this is a bit of a sleazy way to handle the ASIO race condition that exists
            // because ASIO may change state between m_trackLengthFunc() and m_initialBeatFunc())
            if (initialBeat == -1) {
                m_totalBeats = 0;
                return;
            }

            // how many beats is that from when we started recording?
            long beatsSinceTrackStart = nowBeat - initialBeat;

            // what is that modulo our number of beats?
            m_currentBeat = beatsSinceTrackStart % m_totalBeats;
            m_fractionalBeat = now.FractionalBeat;
            if (m_fractionalBeat < 0) {
                m_fractionalBeat = 0; // within epsilon
            }
            HoloDebug.Assert(m_fractionalBeat < 1);
        }

        protected override void DoRender(
            Moment now,
            GraphicsDevice graphicsDevice,
            ISpriteBatch spriteBatch,
            TextureContent content,
            HolofunkView view,
            Transform parentTransform,
            int depth)
        {
            if (m_totalBeats == 0) {
                // there is no actual track here; we do not render
                return;
            }

            Transform combinedTransform = parentTransform.CombineWith(LocalTransform);

            // How many measures?
            long measureCount = (m_totalBeats + 3) / 4;

            // note that the quarter-circle only takes up one quarter of quarterCircleRect; this is deliberate
            Rectangle quarterCircleRect = TextureRect(content.QuarterHollowCircle, combinedTransform.Scale * m_textureScale);

            Vector2 measuresOrigin = combinedTransform.Translation;
            measuresOrigin.X -= ((Math.Min(4, measureCount) * quarterCircleRect.Width) / 2);

            Color color = m_colorFunc();

            Spam.Graphics.WriteLine(new string(' ', depth * 4) + Label + ": parentTransform " + parentTransform + ", localTransform " + LocalTransform + ", combinedTransform " + combinedTransform + ", color " + color.ToString());

            for (int b = 0; b < m_totalBeats; b++) {
                float filledness;
                if (m_fillInEveryBeat) {
                    if (b < m_currentBeat) {
                        filledness = 1;
                    }
                    else if (b == m_currentBeat) {
                        filledness = (float)m_fractionalBeat;
                    }
                    else {
                        filledness = 0;
                    }
                }
                else {
                    if (b == m_currentBeat) {
                        filledness = (float)(1 - m_fractionalBeat);
                    }
                    else {
                        filledness = 0;
                    }
                }

                DrawQuarterCircle(spriteBatch, content, quarterCircleRect, measuresOrigin, b, color, filledness, depth);
            }
        }

        // Draw one of the squares at a grid coordinate.
        void DrawQuarterCircle(ISpriteBatch spriteBatch, TextureContent content, Rectangle rect, Vector2 gridOrigin, int beat, Color color, float filledness, int depth)
        {
            // we prefer beats to start at upper left, but left to this logic, they start at lower left

            // position of this measure
            Vector2 position = gridOrigin + new Vector2(((beat / 4) % 4) * rect.Width, (beat / 16) * rect.Height);

            Vector2 offset;
            switch (beat % 4)
            {
                case 0: offset = new Vector2(1, 1); break;
                case 1: offset = new Vector2(0, 1); break;
                case 2: offset = new Vector2(0, 0); break;
                case 3: offset = new Vector2(1, 0); break;
                default: offset = Vector2.Zero; break; // NOTREACHED
            }
            position += offset * new Vector2(rect.Width, rect.Height);

            Rectangle destRect = new Rectangle(
                rect.Left + (int)position.X,
                rect.Top + (int)position.Y,
                rect.Width,
                rect.Height);            

            Spam.Graphics.WriteLine(new string(' ', depth * 4 + 4) + Label + ": beat " + beat + ", filledness " + filledness + ", destRect " + destRect.ToString());

            // Use NonPremultiplied, as our sprite textures are not premultiplied
            spriteBatch.Begin(SpriteSortMode.Deferred, spriteBatch.GraphicsDevice.BlendStates.NonPremultiplied);

            Vector2 origin = new Vector2(0);

            // always draw a hollow quarter circle
            spriteBatch.Draw(
                content.QuarterHollowCircle,
                destRect,
                null,
                color,
                (float)((beat % 4 + 2) * Math.PI / 2),
                origin,
                SpriteEffects.FlipBoth,
                0);

            // now maybe draw a filled circle
            Vector4 v = color.ToVector4();
            v *= filledness;
            color = new Color(v);

            spriteBatch.Draw(
                content.QuarterFilledCircle,
                destRect,
                null,
                color,
                (float)((beat % 4 + 2) * Math.PI / 2),
                origin,
                SpriteEffects.FlipBoth,
                0);

            spriteBatch.End();
        }

        static Rectangle TextureRect(Texture2D texture, Vector2 scale)
        {
            Rectangle rect = new Rectangle(0, 0, (int)(texture.Width * scale.X), (int)(texture.Height * scale.Y));
            return rect;
        }
    }
}

