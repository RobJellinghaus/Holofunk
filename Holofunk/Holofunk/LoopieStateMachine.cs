////////////////////////////////////////////////////////////////////////
// Copyright (c) 2011-2016 by Rob Jellinghaus.                        //
// All Rights Reserved.                                               //
////////////////////////////////////////////////////////////////////////

using Holofunk;
using Holofunk.Core;
using Holofunk.Kinect;
using Holofunk.SceneGraphs;
using Holofunk.StateMachines;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace Holofunk

{
    using Model = Holofunk.StateMachines.Model;

    // Some states use the same model as their parents.  Other states change the model relative to
    // their parent states.
    // Either way, the State<TEvent, TModel, TBaseModel> class suffices, but is verbose.
    // These type synonyms make it much more readable to use either kind of state in the machine
    // definition below.

    using PlayerState = State<LoopieEvent, PlayerHandModel, PlayerHandModel>;
    using PlayerToPlayerMenuState = State<LoopieEvent, PlayerMenuModel<PlayerHandModel>, PlayerHandModel>;
    using PlayerToEffectState = State<LoopieEvent, PlayerEffectSpaceModel, PlayerHandModel>;
    using EffectState = State<LoopieEvent, PlayerEffectSpaceModel, PlayerEffectSpaceModel>;

    using PlayerAction = Action<LoopieEvent, PlayerHandModel>;

    /// <summary>The loopie state machine for an individual player.</summary>
    /// <remarks>The actions here are invoked from the Kinect thread.
    /// 
    /// This becomes tricky, as the various transition handlers definitely do call into both
    /// ASIO and the scene graph, resulting in cross-thread interactions with the ASIO thread
    /// and XNA thread, respectively.  Yet we *want* this, especially for the ASIO thread, as
    /// any lost time here hurts our latency.
    /// 
    /// Currently the ASIO operations invoked here actually go via m_holofunkBass, which already
    /// handles cross-thread communication with the ASIO thread.  However, the scene graph
    /// operations are touchier; most of them pertain to updating color, etc., which is safe to
    /// do cross-thread, but there does seem room for collision between the code which adds a
    /// track that just finished recording (on the XNA thread), and the code which deletes a
    /// loopie (on the WiimoteLib thread).
    /// 
    /// Note that the scene graph currently has immutable parents; this actually helps
    /// significantly with avoiding truly breaking renderer-versus-state-machine-update state
    /// collisions.</remarks>
    class LoopieStateMachine : StateMachine<LoopieEvent>
    {
        static LoopieStateMachine s_instance;

        internal static LoopieStateMachine Instance
        {
            get
            {
                // on-demand initialization ensures no weirdness about static initializer ordering
                if (s_instance == null) {
                    s_instance = MakeLoopieStateMachine();
                }
                return s_instance;
            }
        }

        static void AddTransition<TModel>(LoopieStateMachine ret, State<LoopieEvent, TModel> from, LoopieEvent evt, State<LoopieEvent> to)
            where TModel : Model
        {
            ret.AddTransition<TModel>(from, new Transition<LoopieEvent, TModel>(evt, to));
        }

        static void AddTransition<TModel>(LoopieStateMachine ret, State<LoopieEvent, TModel> from, LoopieEvent evt, Func<LoopieEvent, TModel, State<LoopieEvent>> computeTransitionFunc)
            where TModel : Model
        {
            ret.AddTransition<TModel>(from, new Transition<LoopieEvent, TModel>(evt, computeTransitionFunc));
        }

        // Set up the state machine we want for our dear little Loopies.
        static LoopieStateMachine MakeLoopieStateMachine()
        {
            PlayerState root = new PlayerState("root", null, new PlayerAction[0], new PlayerAction[0]);

            // Base state: just playing along.  ("Unselected" implies that something *can* be
            // selected, which will be true someday, but not yet.)
            var initial = new PlayerState(
                "initial",
                root,
                (evt, model) => {
                    model.SceneGraph.HandColor = model.PlayerModel.PlayerColor;
                });

            var ret = new LoopieStateMachine(initial, LoopieEventComparer.Instance);

            #region Recording

            PlayerState armed = new PlayerState(
                "armed",
                root,
                (evt, model) => {
                    model.SceneGraph.PushHandTexture(
                        model.IsRightHand ? model.SceneGraph.Content.RightHand : model.SceneGraph.Content.LeftHand);
                },
                (evt, model) => {
                    model.SceneGraph.PopHandTexture();
                });
            
            AddTransition(ret, initial, LoopieEvent.Opened, armed);
            AddTransition(ret, armed, LoopieEvent.Unknown, initial);

            // We're holding down the trigger and recording.
            PlayerState recording = new PlayerState(
                "recording",
                armed,
                (evt, model) => {
                    model.StartRecording(model.HolofunkModel.Clock.Now);

                    model.SceneGraph.HandColor = new Color((byte)0x80, (byte)0, (byte)0, (byte)0x80);
                    model.SceneGraph.MikeSignalColor = new Color((byte)0x80, (byte)0, (byte)0, (byte)0x80);
                    model.SceneGraph.PushHandTexture(model.SceneGraph.Content.HollowCircle);
                },
                (evt, model) => {
                    model.StopRecordingAtCurrentBeat(model.HolofunkModel.Clock.Now);

                    model.SceneGraph.HandColor = model.PlayerModel.PlayerColor;
                    model.SceneGraph.MikeSignalColor = new Color(0, 0, 0, 0);
                    model.SceneGraph.PopHandTexture();
                });

            // closing an open (armed) hand initiates new recording
            AddTransition(ret, armed, LoopieEvent.Closed, recording);

            AddTransition(ret, recording, LoopieEvent.Opened, armed);
            
            #endregion

            // Super-state of all pointing states.  Exists to provide a single place for "unknown" hand pose to
            // be handled.
            PlayerState pointing = new PlayerState(
                "pointing",
                armed,
                (evt, model) => {
                    model.SceneGraph.PushHandTexture(model.SceneGraph.Content.Pointer);
                },
                (evt, model) => {
                    model.SceneGraph.PopHandTexture();
                });

            AddTransition(ret, pointing, LoopieEvent.Unknown, initial);

            #region Mute/unmute

            // we're pointing, and about to possibly mute/unmute
            PlayerState pointingMuteUnmute = new PlayerState(
                "pointingMuteUnmute",
                pointing,
                (evt, model) => { },
                (evt, model) => { });

            PlayerState mute = new PlayerState(
                "mute",
                pointingMuteUnmute,
                (evt, model) => {
                    Dictionary<Loopie, Loopie> toggledLoopies = new Dictionary<Loopie, Loopie>();
                    Option<bool> deletingTouchedLoopies = Option<bool>.None;

                    model.LoopieTouchEffect = loopie => {
                        // the first loopie touched, if it's a double-mute, puts us into delete mode
                        if (!deletingTouchedLoopies.HasValue) {
                            deletingTouchedLoopies = loopie.Condition == LoopieCondition.Mute;
                        }

                        if (!toggledLoopies.ContainsKey(loopie)) {
                            toggledLoopies.Add(loopie, loopie); // loopity doo, I've got another puzzle for you

                            if (deletingTouchedLoopies.Value) {
                                if (loopie.Condition == LoopieCondition.Mute) {
                                    model.RemoveLoopie(loopie);
                                }
                            }
                            else if (loopie.Condition == LoopieCondition.Loop) {
                                loopie.SetCondition(LoopieCondition.Mute);
                            }
                        }
                    };
                    model.SceneGraph.PushHandTexture(model.SceneGraph.Content.MuteCircle);
                },
                (evt, model) => {
                    model.LoopieTouchEffect = loopie => { };
                    model.SceneGraph.PopHandTexture();
                });

            AddTransition(ret, pointingMuteUnmute, LoopieEvent.Closed, mute);
            AddTransition(ret, mute, LoopieEvent.Opened, armed);
            AddTransition(ret, mute, LoopieEvent.Pointing, pointingMuteUnmute);

            PlayerState unmute = new PlayerState(
                "unmute",
                pointingMuteUnmute,
                (evt, model) => {
                    Dictionary<Loopie, Loopie> toggledLoopies = new Dictionary<Loopie, Loopie>();

                    model.LoopieTouchEffect = loopie => {
                        if (!toggledLoopies.ContainsKey(loopie)) {
                            toggledLoopies.Add(loopie, loopie); // loopity doo, I've got another puzzle for you
                            loopie.SetCondition(LoopieCondition.Loop);
                        }
                    };

                    model.SceneGraph.PushHandTexture(model.SceneGraph.Content.UnmuteCircle);
                },
                (evt, model) => {
                    model.LoopieTouchEffect = loopie => { };
                    model.SceneGraph.PopHandTexture();
                });

            AddTransition(ret, pointingMuteUnmute, LoopieEvent.Opened, unmute);
            AddTransition(ret, unmute, LoopieEvent.Closed, armed);
            AddTransition(ret, unmute, LoopieEvent.Pointing, pointingMuteUnmute);

            #endregion

            #region Effect mode

            var pointingEffectDragging = new PlayerToEffectState(
                "pointingEffectDragging",
                pointing,
                (evt, model) => {
                    // SceneGraph.HandTexture = m_parent.SceneGraph.Content.EffectCircle;

                    if (model.MicrophoneSelected) {
                        model.InitializeParametersFromMicrophone();
                        model.ShareMicrophoneParameters();
                    }
                    else {
                        model.InitializeParametersFromLoops();
                        model.ShareLoopParameters();
                    }

                    model.StartDragging();
                },
                (evt, model) => {
                    if (model.MicrophoneSelected) {
                        model.FlushMicrophoneParameters();
                    }
                    else {
                        model.FlushLoopParameters();
                    }

                    model.StopDragging();
                    model.DragStartLocation = Option<Vector2>.None;
                    model.CurrentKnobLocation = Option<Vector2>.None;

                    model.MicrophoneSelected = false;
                },
                entryConversionFunc: playerModel => new PlayerEffectSpaceModel(playerModel),
                exitConversionFunc: playerEffectModel => playerEffectModel.ExtractAndDispose()
                );

            AddTransition(ret, pointingEffectDragging, LoopieEvent.Opened, armed);
            // TODO: make this actually start recording a new track in realtime!!!
            AddTransition(ret, pointingEffectDragging, LoopieEvent.Closed, initial);

            #endregion

            #region Effect popup menus

            var effectPopupMenu = new PlayerToPlayerMenuState(
                "effectPopupMenu",
                pointing,
                (evt, model) => {},
                (evt, model) => model.RunSelectedMenuItem(),
                entryConversionFunc: playerModel => new PlayerMenuModel<PlayerHandModel>(
                    playerModel,
                    playerModel,
                    playerModel.SceneGraph,
                    playerModel.HandPosition,
                    new MenuItem<PlayerHandModel>(
                        "Vol Pan", 
                        model => model.EffectPresetIndex = 0),
                    new MenuItem<PlayerHandModel>(
                        TurnadoAAA1Effect.Parameters[0].Name + " " + TurnadoAAA1Effect.Parameters[1].Name + "\n"
                        + TurnadoAAA1Effect.Parameters[2].Name + " " + TurnadoAAA1Effect.Parameters[3].Name,
                        model => model.EffectPresetIndex = 1),
                    new MenuItem<PlayerHandModel>(
                        TurnadoAAA1Effect.Parameters[4].Name + " " + TurnadoAAA1Effect.Parameters[5].Name + "\n"
                        + TurnadoAAA1Effect.Parameters[6].Name + " " + TurnadoAAA1Effect.Parameters[7].Name,
                        model => model.EffectPresetIndex = 2),
                    new MenuItem<PlayerHandModel>(
                        "Flange Chorus\nEcho Reverb",
                        model => model.EffectPresetIndex = 3),
                    new MenuItem<PlayerHandModel>(
                        "HPF LPF", 
                        model => model.EffectPresetIndex = 4)
                    ),
                exitConversionFunc: model => model.ExtractAndDetach()
                );

            AddTransition(ret, effectPopupMenu, LoopieEvent.Opened, armed);
            AddTransition(ret, effectPopupMenu, LoopieEvent.Closed, pointingEffectDragging);

            AddTransition(ret, pointingMuteUnmute, LoopieEvent.OtherChest, effectPopupMenu);
            // Once we are effect dragging, we want to stay effect dragging, as it turns out.
            // AddTransition(ret, effectPopupMenu, LoopieEvent.OtherNeutral, pointingMuteUnmute);

            #endregion
 
            #region System popup menu

            var systemPopupMenu = new PlayerToPlayerMenuState(
                "systemPopupMenu",
                pointing,
                (evt, model) => { },
                (evt, model) => model.RunSelectedMenuItem(),
                entryConversionFunc: playerHandModel => {
                    bool areAnyLoopiesMine = false;
                    foreach (Loopie loopie in playerHandModel.HolofunkModel.Loopies) {
                        if (loopie.PlayerIndex == playerHandModel.PlayerModel.PlayerIndex) {
                            areAnyLoopiesMine = true;
                            break;
                        }
                    }

                    return new PlayerMenuModel<PlayerHandModel>(
                        playerHandModel,
                        playerHandModel,
                        playerHandModel.SceneGraph,
                        playerHandModel.HandPosition,
                        new MenuItem<PlayerHandModel>(
                            areAnyLoopiesMine ? "Delete\nmy sounds" : "Delete\nALL sounds", 
                            model => {
                                foreach (Loopie loopie in model.HolofunkModel.Loopies) {
                                    if (!areAnyLoopiesMine || loopie.PlayerIndex == model.PlayerModel.PlayerIndex) {
                                        model.RemoveLoopie(loopie);
                                    }
                                }
                            }),
                        new MenuItem<PlayerHandModel>(
                            playerHandModel.PlayerModel.IsRecordingWAV ? "Stop WAV\nrecording" : "Start WAV\nrecording",
                            model => {
                                if (model.PlayerModel.IsRecordingWAV) {
                                    model.StopRecordingWAV();
                                }
                                else {
                                    model.StartRecordingWAV();
                                }
                            }),
                        new MenuItem<PlayerHandModel>("Switch\naudience\nview",
                            model => model.HolofunkModel.SecondaryView = (model.HolofunkModel.SecondaryView == HolofunkView.Secondary
                                    ? HolofunkView.Primary
                                    : HolofunkView.Secondary)),
                        new MenuItem<PlayerHandModel>("Clear mike\neffects",
                            model => model.PlayerModel.MicrophoneParameters.ShareAll(AllEffects.CreateParameterMap())),
                        new MenuItem<PlayerHandModel>("Clear loop\neffects",
                            model => model.ShareLoopParameters(AllEffects.CreateParameterMap())),
                        new MenuItem<PlayerHandModel>("Advance\nslide",
                            model => model.HolofunkModel.AdvanceSlide(+1)),
                        new MenuItem<PlayerHandModel>(playerHandModel.HolofunkModel.SlideVisible ? "Hide\nslide" : "Show\nslide",
                            model => model.HolofunkModel.SlideVisible = !model.HolofunkModel.SlideVisible),
                        new MenuItem<PlayerHandModel>("Rewind\nslide",
                            model => model.HolofunkModel.AdvanceSlide(-1)),
                        new MenuItem<PlayerHandModel>("Swap\nplayers",
                            model => model.HolofunkModel.Kinect.SwapPlayers()),
                        new MenuItem<PlayerHandModel>("+10 BPM", model => model.HolofunkModel.RequestedBPM += 10,
                            enabledFunc: model => model.HolofunkModel.Loopies.Count == 0),
                        new MenuItem<PlayerHandModel>("-10 BPM", model => model.HolofunkModel.RequestedBPM -= 10,
                            enabledFunc: model => model.HolofunkModel.Loopies.Count == 0)
                            /*
                        new MenuItem<PlayerHandModel>("+1 BPM", model => model.RequestedBPM += 1,
                            enabledFunc: model => model.Loopies.Count == 0),
                        new MenuItem<PlayerHandModel>("-1 BPM", model => model.RequestedBPM -= 1,
                            enabledFunc: model => model.Loopies.Count == 0)
                             */
                        );
                },
                exitConversionFunc: model => model.ExtractAndDetach()
                );

            AddTransition(ret, systemPopupMenu, LoopieEvent.Opened, armed);
            AddTransition(ret, systemPopupMenu, LoopieEvent.Closed, initial);

            AddTransition(ret, pointingMuteUnmute, LoopieEvent.OtherHead, systemPopupMenu);
            AddTransition(ret, effectPopupMenu, LoopieEvent.OtherHead, systemPopupMenu);

            AddTransition(ret, systemPopupMenu, LoopieEvent.OtherNeutral, pointingMuteUnmute);
            AddTransition(ret, systemPopupMenu, LoopieEvent.OtherChest, effectPopupMenu);

            #endregion
 
            // COMPUTED TRANSITION TIME!!!
            AddTransition(ret, root, LoopieEvent.Pointing,
                (evt, model) => {
                    if (model.OtherArmPose == ArmPose.AtChest) {
                        return effectPopupMenu;
                    }
                    else if (model.OtherArmPose == ArmPose.AtMouth) {
                        return effectPopupMenu;
                    }
                    else if (model.OtherArmPose == ArmPose.OnHead) {
                        return systemPopupMenu;
                    }
                    else {
                        return pointingMuteUnmute;
                    }
                });

            return ret;
        }

        LoopieStateMachine(PlayerState initialState, IComparer<LoopieEvent> comparer)
            : base(initialState, comparer)
        {
        }
    }
}
