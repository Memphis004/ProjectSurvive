using UnityEngine;

namespace ProjectSurvive.Game.Presentation.GenericCute
{
    /// <summary>
    /// What the Unity 2D Animation backend draws the player with: one prefab, one controller, and the
    /// state names the three poses use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Names are written here by the tool (<c>ProjectSurvive/Player Figure/1. Create GenericCute
    /// profiles</c>), never guessed at runtime: this backend finds a state by the exact name in this
    /// asset, so a profile that names something the controller does not have fails loudly when the
    /// figure is built (the Editor tool refuses to write such a profile at all).
    /// </para>
    /// <para>
    /// <b>Hurt and death are optional and say so.</b> With <see cref="HurtState"/> empty,
    /// <c>PlayHurt</c> does nothing; with <see cref="DeathState"/> empty, the dead pose keeps drawing
    /// the pose that was on screen. That is the same documented option the Spine profile has, so both
    /// backends answer a missing animation the same way rather than falling back to something else.
    /// </para>
    /// <para>
    /// <b>The controller is on the profile, not on the prefab.</b> The pack's own
    /// <c>001 Student 1 Character.prefab</c> carries an Animator with no controller assigned; the
    /// backend assigns this one and disables the Animator so only <c>Tick</c> can advance it.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "GenericCutePlayerProfile", menuName = "ProjectSurvive/GenericCute Player Profile")]
    public sealed class GenericCutePlayerProfile : ScriptableObject
    {
        [Header("What to draw")]
        [Tooltip("The rigged prefab (SpriteSkin bones). Instantiated once in Begin.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("The AnimatorController whose states this profile names. Assigned to the instance's Animator.")]
        [SerializeField] private RuntimeAnimatorController controller;

        [Header("State names (exact — no keywords)")]
        [Tooltip("Standing still. The clip must loop.")]
        [SerializeField] private string idleState = "idle";

        [Tooltip("Moving. The clip must loop.")]
        [SerializeField] private string runState = "run";

        [Tooltip("One-shot hurt reaction. Leave empty when the rig has none: PlayHurt then does nothing.")]
        [SerializeField] private string hurtState = "hit light";

        [Tooltip("Death, held on its last frame. Leave empty when the rig has none: the dead pose keeps drawing the previous pose.")]
        [SerializeField] private string deathState = "";

        [Header("Size and facing")]
        [Tooltip("Height the figure is scaled to, in world units — the same number the Spine profile uses.")]
        [SerializeField] private float targetHeightWorld = 1.6f;

        [Tooltip("Above this speed the caller draws the run pose, in world units a second.")]
        [SerializeField] private float runSpeedThreshold = 0.1f;

        [Tooltip("True when the art is drawn facing the viewer's right. The mirror is then the leftward facing.")]
        [SerializeField] private bool artFacesRight = true;

        /// <summary>The rigged prefab this profile draws.</summary>
        public GameObject Prefab
        {
            get { return prefab; }
            set { prefab = value; }
        }

        /// <summary>The controller whose states the names below refer to.</summary>
        public RuntimeAnimatorController Controller
        {
            get { return controller; }
            set { controller = value; }
        }

        /// <summary>The idle state's exact name (empty means "the rig has no idle", which Begin refuses).</summary>
        public string IdleState
        {
            get { return idleState; }
            set { idleState = value ?? string.Empty; }
        }

        /// <summary>The run state's exact name.</summary>
        public string RunState
        {
            get { return runState; }
            set { runState = value ?? string.Empty; }
        }

        /// <summary>The one-shot hurt state's name, or empty when the rig has none.</summary>
        public string HurtState
        {
            get { return hurtState; }
            set { hurtState = value ?? string.Empty; }
        }

        /// <summary>The death state's name, or empty when the rig has none.</summary>
        public string DeathState
        {
            get { return deathState; }
            set { deathState = value ?? string.Empty; }
        }

        /// <summary>Height the figure is drawn at, in world units.</summary>
        public float TargetHeightWorld
        {
            get { return targetHeightWorld; }
            set { targetHeightWorld = value; }
        }

        /// <summary>The speed above which the caller draws <c>PlayerPose.Run</c>.</summary>
        public float RunSpeedThreshold
        {
            get { return runSpeedThreshold; }
            set { runSpeedThreshold = value; }
        }

        /// <summary>Whether the art is drawn facing the viewer's right.</summary>
        public bool ArtFacesRight
        {
            get { return artFacesRight; }
            set { artFacesRight = value; }
        }
    }
}
