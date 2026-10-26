using System.Collections.Generic;
using ProjectSurvive.Game.Presentation.GenericCute;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Checks a <see cref="GenericCutePlayerProfile"/> against the controller it names, before a scene
    /// or a capture is allowed to use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is Editor code.</b> State names, clip loop flags and transitions live on
    /// <see cref="AnimatorController"/>, which only exists in the Editor. The runtime view can only ask
    /// the built Animator <c>HasState</c>; the facts the stage asks to be proven — that the idle and
    /// run clips loop, that the hurt and death clips do not, that death has no way out — are read here.
    /// </para>
    /// <para>
    /// <b>It never edits the pack.</b> A clip whose loop flag is wrong is a failure to report, not
    /// something to flip silently: the art is a third party's, and a changed clip is a changed asset.
    /// </para>
    /// </remarks>
    public static class GenericCuteProfileValidator
    {
        /// <summary>The profile is missing.</summary>
        public const string ProfileMissingCode = "generic_cute.profile_missing";

        /// <summary>The profile names no prefab.</summary>
        public const string PrefabMissingCode = "generic_cute.profile_prefab_missing";

        /// <summary>The profile names no AnimatorController (or a controller that is not an AnimatorController).</summary>
        public const string ControllerMissingCode = "generic_cute.profile_controller_missing";

        /// <summary>The idle or run state name is empty.</summary>
        public const string StateEmptyCode = "generic_cute.profile_state_empty";

        /// <summary>A named state is not in the controller (the detail lists every state it has).</summary>
        public const string StateMissingCode = "generic_cute.profile_state_missing";

        /// <summary>A clip loops when it must not, or does not loop when it must.</summary>
        public const string LoopMismatchCode = "generic_cute.profile_loop_mismatch";

        /// <summary>The death state has a transition out, so it cannot hold its last frame.</summary>
        public const string DeathTransitionsCode = "generic_cute.profile_death_transitions";

        /// <summary>
        /// Validates <paramref name="profile"/>. False comes with a code and a detail that names what
        /// the controller actually offers; <paramref name="warning"/> carries the one thing that is
        /// reported but not fatal — a hurt state with a transition out of it, which the view overrides.
        /// </summary>
        public static bool TryValidate(
            GenericCutePlayerProfile profile, out string code, out string detail, out string warning)
        {
            code = null;
            detail = null;
            warning = null;

            if (profile == null)
            {
                code = ProfileMissingCode;
                detail = "no GenericCutePlayerProfile";
                return false;
            }

            if (profile.Prefab == null)
            {
                code = PrefabMissingCode;
                detail = "the profile has no prefab";
                return false;
            }

            if (string.IsNullOrEmpty(profile.IdleState) || string.IsNullOrEmpty(profile.RunState))
            {
                code = StateEmptyCode;
                detail = "idle='" + profile.IdleState + "' run='" + profile.RunState +
                    "': both are required (a rig with no run cannot be walked)";
                return false;
            }

            var controller = profile.Controller as AnimatorController;
            if (controller == null)
            {
                code = ControllerMissingCode;
                detail = profile.Controller == null
                    ? "the profile has no AnimatorController"
                    : "'" + profile.Controller.name + "' is not an AnimatorController";
                return false;
            }

            AnimatorStateMachine machine = controller.layers.Length > 0 ? controller.layers[0].stateMachine : null;
            if (machine == null)
            {
                code = StateMissingCode;
                detail = "'" + controller.name + "' has no layer 0 state machine";
                return false;
            }

            Dictionary<string, AnimatorState> states = StatesOf(machine);

            string[] required = { profile.IdleState, profile.RunState };
            string[] optional = { profile.HurtState, profile.DeathState };

            for (int i = 0; i < required.Length; i++)
            {
                if (!states.ContainsKey(required[i]))
                {
                    code = StateMissingCode;
                    detail = "state '" + required[i] + "' is not in '" + controller.name + "'; states: " +
                        Names(controller);
                    return false;
                }
            }

            for (int i = 0; i < optional.Length; i++)
            {
                string name = optional[i];
                if (!string.IsNullOrEmpty(name) && !states.ContainsKey(name))
                {
                    code = StateMissingCode;
                    detail = "state '" + name + "' is not in '" + controller.name + "'; states: " +
                        Names(controller);
                    return false;
                }
            }

            AnimatorState idle = states[profile.IdleState];
            AnimatorState run = states[profile.RunState];
            if (!Loops(idle))
            {
                code = LoopMismatchCode;
                detail = "idle state '" + profile.IdleState + "' plays '" + MotionName(idle) +
                    "', whose clip does not loop";
                return false;
            }

            if (!Loops(run))
            {
                code = LoopMismatchCode;
                detail = "run state '" + profile.RunState + "' plays '" + MotionName(run) +
                    "', whose clip does not loop";
                return false;
            }

            if (!string.IsNullOrEmpty(profile.HurtState))
            {
                AnimatorState hurt = states[profile.HurtState];
                if (Loops(hurt))
                {
                    code = LoopMismatchCode;
                    detail = "hurt state '" + profile.HurtState + "' plays '" + MotionName(hurt) +
                        "', whose clip loops; a one-shot must not";
                    return false;
                }

                if (hurt.transitions.Length > 0)
                {
                    warning = "hurt state '" + profile.HurtState + "' has " + hurt.transitions.Length +
                        " transition(s) out; the view decides when the pose returns, so they are unused";
                }
            }

            if (!string.IsNullOrEmpty(profile.DeathState))
            {
                AnimatorState death = states[profile.DeathState];
                if (Loops(death))
                {
                    code = LoopMismatchCode;
                    detail = "death state '" + profile.DeathState + "' plays '" + MotionName(death) +
                        "', whose clip loops; the last frame must hold";
                    return false;
                }

                if (death.transitions.Length > 0)
                {
                    code = DeathTransitionsCode;
                    detail = "death state '" + profile.DeathState + "' has " + death.transitions.Length +
                        " transition(s) out, so it cannot hold its last frame";
                    return false;
                }
            }

            return true;
        }

        /// <summary>Every state name in the controller's first layer, in the order the Animator lists them.</summary>
        public static string Names(AnimatorController controller)
        {
            if (controller == null || controller.layers.Length == 0)
            {
                return "(none)";
            }

            var names = new List<string>();
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                names.Add(states[i].state.name);
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names.ToArray());
        }

        private static Dictionary<string, AnimatorState> StatesOf(AnimatorStateMachine machine)
        {
            var states = new Dictionary<string, AnimatorState>();
            ChildAnimatorState[] children = machine.states;
            for (int i = 0; i < children.Length; i++)
            {
                AnimatorState state = children[i].state;
                if (state != null && !states.ContainsKey(state.name))
                {
                    states.Add(state.name, state);
                }
            }

            return states;
        }

        private static bool Loops(AnimatorState state)
        {
            var clip = state.motion as AnimationClip;
            if (clip == null)
            {
                return false;
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            return settings.loopTime;
        }

        private static string MotionName(AnimatorState state)
        {
            return state.motion != null ? state.motion.name : "(no motion)";
        }
    }
}
