using ProjectSurvive.Game.Presentation.Figure;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSurvive.Game.Presentation.GenericCute
{
    /// <summary>
    /// The player figure drawn by Unity 2D Animation: one rigged prefab instantiated in
    /// <see cref="Begin"/>, animated state by state from the profile, and advanced only by
    /// <see cref="Tick"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the second implementation of <see cref="IPlayerFigure"/></b> and it answers the same
    /// contract as <c>SpinePlayerView</c>: no clock of its own (the Animator is disabled, so only
    /// <see cref="Tick"/> can move it and <c>Time.timeScale</c> cannot animate a paused game),
    /// <see cref="Present"/> writes nothing when the pose and the facing are already drawn,
    /// <see cref="Begin"/> builds once and can be retried after a failure, hurt is a one-shot that
    /// returns to the presented pose, and the facing is one mirror in one place.
    /// </para>
    /// <para>
    /// <b>Animator rules instead of a key-word search.</b> A state is played by the exact name in the
    /// profile (<c>Animator.Play(hash, 0, 0)</c>), never through parameters or transitions: the pack's
    /// demo controller has no transitions at all, so the figure cannot steer itself. Whether a clip
    /// loops is a property of the clip, which is why the Editor tool checks the loop flag of
    /// <c>idle</c>/<c>run</c> (must loop) and of <c>hurt</c>/<c>death</c> (must not) before writing
    /// the profile.
    /// </para>
    /// <para>
    /// <b>Two transforms do the sizing and the mirroring.</b> The root child is scaled uniformly to
    /// <see cref="GenericCutePlayerProfile.TargetHeightWorld"/>, and a child under it carries only the
    /// sign of <c>localScale.x</c> — the same split the Spine actor uses (scaled root, flip on the
    /// child that must keep a unit scale), so a mirror can never make the doctor taller.
    /// </para>
    /// <para>
    /// <b>SpriteSkin deforms in its own LateUpdate.</b> A caller that steps <see cref="Tick"/> by hand
    /// in a test sees bones move immediately but the mesh on the next real frame, which is why a test
    /// that reads bounds or takes a picture waits one frame.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class GenericCutePlayerView : MonoBehaviour, IPlayerFigure
    {
        /// <summary>No profile was configured.</summary>
        public const string ProfileMissingCode = "generic_cute.player_profile_missing";

        /// <summary>The profile's prefab, controller or Animator is missing, or nothing was built.</summary>
        public const string BuildFailedCode = "generic_cute.player_build_failed";

        /// <summary>The deformed sprites have no measurable height.</summary>
        public const string BoundsEmptyCode = "generic_cute.player_bounds_empty";

        /// <summary>The prefab draws no sprite at all.</summary>
        public const string NotVisibleCode = "generic_cute.player_not_visible";

        /// <summary>A state the profile names is not in the controller, or would not play.</summary>
        public const string AnimationFailedCode = "generic_cute.player_animation_failed";

        [SerializeField] private GenericCutePlayerProfile profile;
        [SerializeField] private string figureName = "PlayerFigure";
        [SerializeField] private int sortingOrder;

        private GenericCutePlayerProfile m_Profile;
        private GameObject m_Instance;
        private Transform m_ScaleRoot;
        private Transform m_FlipRoot;
        private Animator m_Animator;
        private SortingGroup m_Sorting;
        private PlayerPose m_Pose = PlayerPose.Idle;
        private int m_Facing = 1;
        private float m_Speed = 1f;
        private bool m_Paused;
        private bool m_Hurt;
        private int m_PlayCalls;
        private int m_BuildCount;
        private float m_AnimationTime;
        private string m_CurrentAnimation;
        private string m_ErrorCode;
        private float m_DrawnHeightWorld;
        private int m_HurtHash;

        /// <summary>The profile in use (the serialized field unless <see cref="Configure"/> replaced it).</summary>
        public GenericCutePlayerProfile Profile
        {
            get { return m_Profile != null ? m_Profile : profile; }
        }

        /// <summary>Assigns the profile before <see cref="Begin"/> (a scene builder or a test does this).</summary>
        public void Configure(GenericCutePlayerProfile playerProfile)
        {
            m_Profile = playerProfile;
            profile = playerProfile;
        }

        /// <summary>True once <see cref="Begin"/> produced a drawable figure.</summary>
        public bool IsBegun
        {
            get { return m_Instance != null && m_Animator != null; }
        }

        /// <summary>The code that made <see cref="Begin"/> fail, or null after a success.</summary>
        public string ErrorCode
        {
            get { return m_ErrorCode; }
        }

        /// <summary>The pose currently drawn.</summary>
        public PlayerPose Pose
        {
            get { return m_Pose; }
        }

        /// <summary>+1 right, -1 left.</summary>
        public int FaceDirection
        {
            get { return m_Facing; }
        }

        /// <summary>True while the one-shot hurt state is playing.</summary>
        public bool IsHurt
        {
            get { return m_Hurt; }
        }

        /// <summary>How many states this figure has started (Begin's idle is the first).</summary>
        public int PlayCalls
        {
            get { return m_PlayCalls; }
        }

        /// <summary>How many figures this view has built (one per successful <see cref="Begin"/>).</summary>
        public int BuildCount
        {
            get { return m_BuildCount; }
        }

        /// <summary>The state name last played, or null before <see cref="Begin"/>.</summary>
        public string CurrentAnimation
        {
            get { return m_CurrentAnimation; }
        }

        /// <summary>Seconds the current state has been playing; frozen while paused.</summary>
        public float AnimationTime
        {
            get { return m_AnimationTime; }
        }

        /// <summary>The height the figure was drawn at, in world units (0 before Begin).</summary>
        public float DrawnHeightWorld
        {
            get { return m_DrawnHeightWorld; }
        }

        /// <summary>The playback speed the last <see cref="SetSpeed"/> or <see cref="SetPaused"/> left: 0 while paused.</summary>
        public float CurrentTimeScale
        {
            get { return m_Paused ? 0f : m_Speed; }
        }

        /// <summary>The draw order of the figure's SortingGroup (and of everything under it).</summary>
        public int SortingOrder
        {
            get { return sortingOrder; }
        }

        /// <summary>The profile's run threshold, or 0.1 when no profile is configured.</summary>
        public float RunSpeedThreshold
        {
            get
            {
                GenericCutePlayerProfile current = Profile;
                return current != null ? current.RunSpeedThreshold : 0.1f;
            }
        }

        /// <summary>
        /// Instantiates the rig once, validates every state name the profile uses, scales it to the
        /// profile's height and plays the idle state. Idempotent; a failure releases what it built so
        /// a corrected profile can be tried again.
        /// </summary>
        public bool Begin()
        {
            if (IsBegun)
            {
                return true;
            }

            m_ErrorCode = null;

            GenericCutePlayerProfile current = Profile;
            if (current == null)
            {
                Fail(ProfileMissingCode, "no GenericCutePlayerProfile was configured");
                return false;
            }

            if (current.Prefab == null)
            {
                Fail(BuildFailedCode, "the profile has no prefab");
                return false;
            }

            if (current.Controller == null)
            {
                Fail(BuildFailedCode, "the profile has no AnimatorController");
                return false;
            }

            if (string.IsNullOrEmpty(current.IdleState))
            {
                Fail(AnimationFailedCode, "the profile has no idle state");
                return false;
            }

            m_ScaleRoot = new GameObject(figureName + "Scale").transform;
            m_ScaleRoot.SetParent(transform, false);
            m_FlipRoot = new GameObject(figureName + "Flip").transform;
            m_FlipRoot.SetParent(m_ScaleRoot, false);

            m_Instance = Instantiate(current.Prefab, m_FlipRoot, false);
            m_Instance.name = figureName;

            m_Animator = m_Instance.GetComponentInChildren<Animator>(true);
            if (m_Animator == null)
            {
                Fail(BuildFailedCode, "the prefab has no Animator");
                Release();
                return false;
            }

            // The Animator is the one thing allowed to move the rig, and it is only ever stepped by
            // Tick: disabled here, so a frame — or Time.timeScale — can never advance it.
            m_Animator.enabled = false;
            m_Animator.runtimeAnimatorController = current.Controller;
            m_Animator.Rebind();
            m_Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (!TryValidateStates(current, out string missing))
            {
                Fail(AnimationFailedCode, missing);
                Release();
                return false;
            }

            m_Sorting = m_Instance.GetComponent<SortingGroup>();
            if (m_Sorting == null)
            {
                // The pack ships one at the root; a rig without it still gets a single draw order,
                // so SetSortingOrder keeps meaning "the whole figure" and the inner order is kept.
                m_Sorting = m_Instance.AddComponent<SortingGroup>();
            }

            m_Sorting.sortingOrder = sortingOrder;

            if (!TryMeasureHeight(m_Instance, out float worldHeight))
            {
                Fail(BoundsEmptyCode, "the prefab's sprites have no height");
                Release();
                return false;
            }

            if (!HasVisibleSprite(m_Instance))
            {
                Fail(NotVisibleCode, "the prefab draws no sprite");
                Release();
                return false;
            }

            // Scale the root only; the flip child keeps a unit scale so the proportions never change.
            float parentScale = Mathf.Abs(transform.lossyScale.y);
            if (parentScale <= 0f)
            {
                parentScale = 1f;
            }

            float localHeight = worldHeight / parentScale;
            float scale = current.TargetHeightWorld / (localHeight * parentScale);
            m_ScaleRoot.localScale = new Vector3(scale, scale, 1f);
            m_DrawnHeightWorld = localHeight * parentScale * scale;

            m_BuildCount++;

            if (!PlayState(PlayerPose.Idle, current.IdleState))
            {
                Fail(AnimationFailedCode, "idle state '" + current.IdleState + "' did not play");
                Release();
                return false;
            }

            ApplyFacing();

            return true;
        }

        /// <summary>
        /// Draws this pose and facing. Cheap by contract: nothing is played, no string built and no
        /// mesh touched unless the pose or the facing actually changed.
        /// </summary>
        public void Present(PlayerPose pose, int facing)
        {
            if (!IsBegun)
            {
                return;
            }

            int face = facing >= 0 ? 1 : -1;

            if (pose != m_Pose)
            {
                SwitchToPose(pose);
            }

            if (face != m_Facing)
            {
                m_Facing = face;
                ApplyFacing();
            }
        }

        /// <summary>
        /// Plays the hurt state once, then returns to the pose being presented. A profile with no hurt
        /// state does nothing — that is its documented option, not a silent fallback.
        /// </summary>
        public void PlayHurt()
        {
            if (!IsBegun)
            {
                return;
            }

            GenericCutePlayerProfile current = Profile;
            string state = current != null ? current.HurtState : null;
            if (string.IsNullOrEmpty(state))
            {
                return;
            }

            if (!PlayState(m_Pose, state))
            {
                Debug.LogError(
                    "[GenericCutePlayerView] Hurt state '" + state + "' is not in '" +
                    current.Controller.name + "'.", this);
                return;
            }

            m_Hurt = true;
        }

        /// <summary>Freezes or resumes the rig (never <c>Time.timeScale</c>).</summary>
        public void SetPaused(bool paused)
        {
            m_Paused = paused;
        }

        /// <summary>Playback speed of the rig; 0 or less freezes it.</summary>
        public void SetSpeed(float speed)
        {
            m_Speed = speed > 0f ? speed : 0f;
        }

        /// <summary>Draw order of the whole figure, applied to its SortingGroup.</summary>
        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
            if (m_Sorting != null)
            {
                m_Sorting.sortingOrder = order;
            }
        }

        /// <summary>
        /// Advances the rig by <paramref name="deltaTime"/>: the only clock this view has. Also the
        /// place a finished hurt state is noticed and the presented pose restored.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsBegun || m_Paused || deltaTime <= 0f)
            {
                return;
            }

            float step = deltaTime * m_Speed;
            m_Animator.Update(step);
            m_AnimationTime += step;

            RestoreAfterHurt();
        }

        private void LateUpdate()
        {
            RestoreAfterHurt();
        }

        private void OnDestroy()
        {
            Release();
        }

        // ------------------------------------------------------------------
        // State plumbing
        // ------------------------------------------------------------------

        /// <summary>
        /// Plays a state by name on layer 0 and records what is playing. A name the controller does not
        /// have is refused rather than logged and swallowed.
        /// </summary>
        private bool PlayState(PlayerPose pose, string state)
        {
            if (m_Animator == null || string.IsNullOrEmpty(state))
            {
                return false;
            }

            int hash = Animator.StringToHash(state);
            if (!m_Animator.HasState(0, hash))
            {
                return false;
            }

            m_Animator.Play(hash, 0, 0f);

            // A queued Play only takes effect on the next evaluation, and that evaluation resets the
            // state's clock to the normalised time just handed in. Applying it here — one zero-length
            // step, on a state change only — means the caller's next Tick advances the state it just
            // asked for, which is what the Spine backend does too.
            m_Animator.Update(0f);

            m_Pose = pose;
            m_PlayCalls++;
            m_AnimationTime = 0f;
            m_CurrentAnimation = state;
            m_Hurt = false;
            return true;
        }

        private void SwitchToPose(PlayerPose pose)
        {
            GenericCutePlayerProfile current = Profile;
            string state = StateFor(current, pose);

            if (string.IsNullOrEmpty(state))
            {
                // The profile documents this rig as having no death: the figure keeps drawing what it
                // was drawing, and the pose is recorded so the caller sees what it asked for.
                m_Pose = pose;
                m_Hurt = false;
                return;
            }

            // A new pose ends a hurt reaction: one state plays at a time.
            m_Hurt = false;
            PlayState(pose, state);
        }

        private static string StateFor(GenericCutePlayerProfile current, PlayerPose pose)
        {
            if (current == null)
            {
                return null;
            }

            switch (pose)
            {
                case PlayerPose.Run:
                    return current.RunState;
                case PlayerPose.Dead:
                    return current.DeathState;
                default:
                    return current.IdleState;
            }
        }

        /// <summary>
        /// Puts the presented pose back once the hurt state reached its last frame. The Animator has no
        /// completion event, so the state's own normalised time is what says "it ended"; this runs from
        /// <see cref="Tick"/> and <see cref="LateUpdate"/> so a hand-stepped view and a frame-stepped
        /// one behave the same.
        /// </summary>
        private void RestoreAfterHurt()
        {
            if (!m_Hurt || m_Animator == null)
            {
                return;
            }

            AnimatorStateInfo info = m_Animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != m_HurtHash || info.normalizedTime < 1f)
            {
                return;
            }

            m_Hurt = false;
            PlayState(m_Pose, StateFor(Profile, m_Pose));
        }

        private void ApplyFacing()
        {
            GenericCutePlayerProfile current = Profile;
            if (m_FlipRoot == null || current == null)
            {
                return;
            }

            bool flip = current.ArtFacesRight ? m_Facing < 0 : m_Facing > 0;
            m_FlipRoot.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        }

        // ------------------------------------------------------------------
        // Validation, measurement and teardown
        // ------------------------------------------------------------------

        private bool TryValidateStates(GenericCutePlayerProfile current, out string detail)
        {
            detail = null;
            m_HurtHash = Animator.StringToHash(current.HurtState ?? string.Empty);

            string[] states = { current.IdleState, current.RunState, current.HurtState, current.DeathState };
            for (int i = 0; i < states.Length; i++)
            {
                string state = states[i];
                if (string.IsNullOrEmpty(state))
                {
                    continue;
                }

                if (!m_Animator.HasState(0, Animator.StringToHash(state)))
                {
                    detail = "state '" + state + "' is not in '" + current.Controller.name + "'";
                    return false;
                }
            }

            return true;
        }

        private static bool TryMeasureHeight(GameObject instance, out float height)
        {
            height = 0f;

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            float low = float.MaxValue;
            float high = float.MinValue;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                if (bounds.size.y <= 0f)
                {
                    continue;
                }

                low = Mathf.Min(low, bounds.min.y);
                high = Mathf.Max(high, bounds.max.y);
            }

            if (high <= low)
            {
                return false;
            }

            height = high - low;
            return true;
        }

        private static bool HasVisibleSprite(GameObject instance)
        {
            SpriteRenderer[] sprites = instance.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].sprite != null && sprites[i].enabled)
                {
                    return true;
                }
            }

            return false;
        }

        private void Fail(string code, string detail)
        {
            m_ErrorCode = code;
            Debug.LogError("[GenericCutePlayerView] " + code + ": " + detail, this);
        }

        /// <summary>Destroys the built rig; the view can be begun again afterwards.</summary>
        private void Release()
        {
            if (m_ScaleRoot != null)
            {
                GameObject built = m_ScaleRoot.gameObject;
                m_ScaleRoot = null;

                if (Application.isPlaying)
                {
                    Destroy(built);
                }
                else
                {
                    DestroyImmediate(built);
                }
            }

            m_Instance = null;
            m_FlipRoot = null;
            m_Animator = null;
            m_Sorting = null;
            m_DrawnHeightWorld = 0f;
            m_CurrentAnimation = null;
            m_AnimationTime = 0f;
            m_Hurt = false;
        }
    }
}
