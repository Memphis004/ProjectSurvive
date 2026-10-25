using Spine.Unity;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// The player figure on screen: one <see cref="SpineWorldActor"/> built once in
    /// <see cref="Begin"/>, drawn from the pose its owner presents, and advanced only by
    /// <see cref="Tick"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Presentation only — it decides no rule. It never reads the simulation, does not know what a
    /// hurt costs, and owns no clock: the skeleton's own component runs with a zero time scale so
    /// that Unity's frame time (and <c>Time.timeScale</c>) cannot animate a paused game, and
    /// <see cref="Tick"/> is the one thing that moves the animation state forward. Pause and speed
    /// go through the actor's own <c>Pause</c>/<c>SetSpeed</c> and never through
    /// <c>Time.timeScale</c>.
    /// </para>
    /// <para>
    /// <see cref="Present"/> is called every frame, so it does nothing when the pose and the
    /// facing are the ones already drawn: 10 000 presents of the same pose play one animation and
    /// allocate nothing.
    /// </para>
    /// </remarks>
    public sealed class SpinePlayerView : MonoBehaviour
    {
        /// <summary>No profile was configured.</summary>
        public const string ProfileMissingCode = "spine.player_profile_missing";

        /// <summary>Building the actor produced nothing that can be drawn.</summary>
        public const string BuildFailedCode = "spine.player_build_failed";

        /// <summary>The skeleton and skin produced an empty mesh bounds, so it cannot be scaled to a height.</summary>
        public const string BoundsEmptyCode = "spine.player_bounds_empty";

        /// <summary>The skeleton is built but has no vertices: a skin that renders nothing.</summary>
        public const string NotVisibleCode = "spine.player_not_visible";

        /// <summary>The required animation would not play on the built actor.</summary>
        public const string AnimationFailedCode = "spine.player_animation_failed";

        /// <summary>The profile's single world material cannot cover every atlas page.</summary>
        public const string AtlasMultiPageCode = "spine.player_atlas_multi_page";

        [SerializeField] private SpinePlayerProfile profile;
        [SerializeField] private string actorName = "PlayerFigure";
        [SerializeField] private int sortingOrder;

        private SpinePlayerProfile m_Profile;
        private SpineWorldActor m_Actor;
        private PlayerPose m_Pose = PlayerPose.Idle;
        private int m_Facing = 1;
        private float m_Speed = 1f;
        private bool m_Paused;
        private bool m_Hurt;
        private bool m_RestoreAfterHurt;
        private int m_PlayCalls;
        private int m_BuildCount;
        private string m_ErrorCode;

        /// <summary>The profile in use (the serialized field unless <see cref="Configure"/> replaced it).</summary>
        public SpinePlayerProfile Profile
        {
            get { return m_Profile != null ? m_Profile : profile; }
        }

        /// <summary>The built actor, or null before <see cref="Begin"/> succeeded.</summary>
        public SpineWorldActor Actor
        {
            get { return m_Actor; }
        }

        /// <summary>True once <see cref="Begin"/> produced a drawable figure.</summary>
        public bool IsBegun
        {
            get { return m_Actor != null && m_Actor.IsBuilt; }
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

        /// <summary>How many animations this view has started (Begin plays the idle pose once).</summary>
        public int PlayCalls
        {
            get { return m_PlayCalls; }
        }

        /// <summary>How many actors this view has created (one per successful Begin).</summary>
        public int BuildCount
        {
            get { return m_BuildCount; }
        }

        /// <summary>True while a one-shot hurt animation is playing.</summary>
        public bool IsHurt
        {
            get { return m_Hurt; }
        }

        /// <summary>The height the figure was drawn at, in world units (0 before Begin).</summary>
        public float DrawnHeightWorld { get; private set; }

        /// <summary>Assigns the profile before <see cref="Begin"/> (a test or a scene builder does this).</summary>
        public void Configure(SpinePlayerProfile playerProfile)
        {
            m_Profile = playerProfile;
            profile = playerProfile;
        }

        /// <summary>
        /// Builds the figure once: loads the skeleton, validates the profile against it, composes the
        /// skin, scales the root to the profile's height and plays the idle pose. Idempotent —
        /// a second call after a success returns true without building anything.
        /// </summary>
        public bool Begin()
        {
            if (IsBegun)
            {
                return true;
            }

            m_ErrorCode = null;

            SpinePlayerProfile current = Profile;
            if (current == null)
            {
                Fail(ProfileMissingCode, "no SpinePlayerProfile was configured");
                return false;
            }

            SkeletonDataAsset data;
            string loadCode;
            if (!SpineSkeletonLoader.TryLoad(current.SkeletonResourcePath, out data, out loadCode))
            {
                Fail(loadCode, "resource path '" + current.SkeletonResourcePath + "'");
                return false;
            }

            string code;
            string detail;
            if (!current.TryValidate(data.GetSkeletonData(true), out code, out detail))
            {
                Fail(code, detail);
                return false;
            }

            var actor = SpineActorFactory.CreateWorld(
                transform, data, null, null, true, actorName, false);
            m_BuildCount++;

            actor.SetSkinParts(current.SkinPartArray());
            actor.Build();

            if (!actor.IsBuilt)
            {
                Fail(BuildFailedCode, "the actor did not build from '" + data.name + "'");
                DestroyActor(actor);
                return false;
            }

            m_Actor = actor;

            // One material draws the mesh, so a multi-page atlas would draw every page with the
            // last page's texture. Refuse instead of showing the wrong art.
            int pages = AtlasPageCount(data);
            if (pages > 1)
            {
                Fail(AtlasMultiPageCode, "the atlas has " + pages + " pages; one world material cannot cover them");
                StopActor();
                return false;
            }

            actor.SetMaterial(current.WorldMaterial);

            // The actor advances only through Tick: stop the spine component's own Update from
            // consuming Time.deltaTime (and therefore Time.timeScale) behind the caller's back.
            if (actor.Animation != null)
            {
                actor.Animation.timeScale = 0f;
            }

            actor.AnimationComplete += HandleAnimationComplete;
            actor.SortingOrder = sortingOrder;

            if (!TryMeasureHeight(actor, out float meshHeight))
            {
                Fail(BoundsEmptyCode, "the skeleton's mesh bounds have no height");
                StopActor();
                return false;
            }

            // Scale the actor's root only: its SkeletonTransform carries the flip and must keep
            // a unit local scale for that. A scaled parent is compensated so the figure still lands
            // at the profile's height in the world.
            float parentScale = Mathf.Abs(transform.lossyScale.y);
            if (parentScale <= 0f)
            {
                parentScale = 1f;
            }

            float scale = current.TargetHeightWorld / (meshHeight * parentScale);
            actor.transform.localScale = new Vector3(scale, scale, 1f);
            DrawnHeightWorld = meshHeight * parentScale * scale;

            if (!PlayPose(PlayerPose.Idle, current.IdleAnimation, true))
            {
                Fail(AnimationFailedCode, "idle animation '" + current.IdleAnimation + "' did not play");
                StopActor();
                return false;
            }

            // One zero-length tick applies the state and regenerates the mesh, so the visibility
            // check below looks at what will actually be drawn.
            actor.Tick(0f);

            if (!TryCountVertices(actor, out int vertices))
            {
                Fail(NotVisibleCode, "the skeleton's mesh is missing");
                StopActor();
                return false;
            }

            if (vertices <= 0)
            {
                Fail(NotVisibleCode, "the composed skin draws no vertices; check the profile's skin parts");
                StopActor();
                return false;
            }

            return true;
        }

        /// <summary>
        /// Draws this pose and facing. Cheap by contract: nothing is played, no material touched and
        /// no string built unless the pose or the facing actually changed.
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
        /// Plays the hurt reaction once, then returns to the pose being presented. Without a hurt
        /// animation in the profile this does nothing (that is the profile's documented option, not
        /// a silent fallback: the asset says which animations this figure has).
        /// </summary>
        public void PlayHurt()
        {
            if (!IsBegun)
            {
                return;
            }

            SpinePlayerProfile current = Profile;
            string animation = current != null ? current.HurtAnimation : null;
            if (string.IsNullOrEmpty(animation))
            {
                return;
            }

            if (m_Actor.Play(animation, false, CurrentSpeed()) == null)
            {
                Debug.LogError(
                    "[SpinePlayerView] Hurt animation '" + animation + "' did not play; check the profile against the skeleton.",
                    this);
                return;
            }

            m_PlayCalls++;
            m_Hurt = true;
            m_RestoreAfterHurt = false;
        }

        /// <summary>Freezes or resumes the animation state (never <c>Time.timeScale</c>).</summary>
        public void SetPaused(bool paused)
        {
            m_Paused = paused;
            if (!IsBegun)
            {
                return;
            }

            if (paused)
            {
                m_Actor.Pause();
            }
            else
            {
                m_Actor.SetSpeed(m_Speed);
            }
        }

        /// <summary>Playback speed of the animation state; 0 or less freezes it.</summary>
        public void SetSpeed(float speed)
        {
            m_Speed = speed > 0f ? speed : 0f;
            if (IsBegun && !m_Paused)
            {
                m_Actor.SetSpeed(m_Speed);
            }
        }

        /// <summary>Sorting order of the figure's renderer (and of the mount, if one is added).</summary>
        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
            if (IsBegun)
            {
                m_Actor.SortingOrder = order;
            }
        }

        /// <summary>
        /// Advances the animation state by <paramref name="deltaTime"/>. The only clock this view
        /// has: a paused or zero-length step moves nothing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsBegun || m_Paused || deltaTime <= 0f)
            {
                return;
            }

            m_Actor.Tick(deltaTime);

            // A hurt animation that ended during this step is restored here too, not only in
            // LateUpdate, so a caller that steps the view by hand gets the same frames as one that
            // lets the frame loop do it.
            ApplyPendingRestore();
        }

        private void LateUpdate()
        {
            ApplyPendingRestore();
        }

        private void OnDestroy()
        {
            StopActor();
        }

        // ------------------------------------------------------------------
        // Pose plumbing
        // ------------------------------------------------------------------

        private void SwitchToPose(PlayerPose pose)
        {
            SpinePlayerProfile current = Profile;
            string animation = AnimationFor(current, pose);
            if (string.IsNullOrEmpty(animation))
            {
                // An optional animation the profile leaves empty: the figure keeps drawing what it
                // was drawing, and the pose is recorded so the caller sees the state it asked for.
                m_Pose = pose;
                m_Hurt = false;
                m_RestoreAfterHurt = false;
                return;
            }

            // A new pose ends a hurt reaction; there is one track, so a one-shot cannot outlive it.
            m_Hurt = false;
            m_RestoreAfterHurt = false;

            if (PlayPose(pose, animation, pose != PlayerPose.Dead))
            {
                return;
            }

            Debug.LogError(
                "[SpinePlayerView] Pose " + pose + " asked for animation '" + animation +
                "', which the built skeleton would not play.", this);
            m_Pose = pose;
        }

        private static string AnimationFor(SpinePlayerProfile current, PlayerPose pose)
        {
            if (current == null)
            {
                return null;
            }

            switch (pose)
            {
                case PlayerPose.Run:
                    return current.RunAnimation;
                case PlayerPose.Dead:
                    return current.DeathAnimation;
                default:
                    return current.IdleAnimation;
            }
        }

        private bool PlayPose(PlayerPose pose, string animation, bool loop)
        {
            if (string.IsNullOrEmpty(animation))
            {
                return true;
            }

            if (m_Actor == null)
            {
                return false;
            }

            // Play resets the animation state's time scale to the value handed in, so the current
            // speed (0 while paused) has to be passed every time.
            if (m_Actor.Play(animation, loop, CurrentSpeed()) == null)
            {
                return false;
            }

            m_PlayCalls++;
            m_Pose = pose;
            ApplyFacing();
            return true;
        }

        private void ApplyFacing()
        {
            SpinePlayerProfile current = Profile;
            if (!IsBegun || current == null)
            {
                return;
            }

            bool flip = current.ArtFacesRight ? m_Facing < 0 : m_Facing > 0;
            m_Actor.SetFlipX(flip);
        }

        private float CurrentSpeed()
        {
            return m_Paused ? 0f : m_Speed;
        }

        /// <summary>
        /// Called from inside Spine's track update when a non-looping animation ends. Nothing may
        /// play here — the track list is being walked — so only the flag is set and LateUpdate (or
        /// the end of a Tick) does the work.
        /// </summary>
        private void HandleAnimationComplete(string animation)
        {
            SpinePlayerProfile current = Profile;
            if (!m_Hurt || current == null || animation != current.HurtAnimation)
            {
                return;
            }

            m_RestoreAfterHurt = true;
        }

        private void ApplyPendingRestore()
        {
            if (!m_RestoreAfterHurt)
            {
                return;
            }

            m_RestoreAfterHurt = false;
            m_Hurt = false;

            if (!IsBegun)
            {
                return;
            }

            string animation = AnimationFor(Profile, m_Pose);
            PlayPose(m_Pose, animation, m_Pose != PlayerPose.Dead);
        }

        // ------------------------------------------------------------------
        // Measurement and teardown
        // ------------------------------------------------------------------

        private static int AtlasPageCount(SkeletonDataAsset data)
        {
            int pages = 0;
            if (data.atlasAssets == null)
            {
                return 0;
            }

            for (int i = 0; i < data.atlasAssets.Length; i++)
            {
                if (data.atlasAssets[i] != null)
                {
                    pages += data.atlasAssets[i].MaterialCount;
                }
            }

            return pages;
        }

        private static bool TryMeasureHeight(SpineWorldActor actor, out float height)
        {
            height = 0f;

            SkeletonAnimation animation = actor.Animation;
            if (animation == null)
            {
                return false;
            }

            MeshFilter filter = animation.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                return false;
            }

            Vector3 size = mesh.bounds.size;
            if (size.y <= 0f || float.IsNaN(size.y) || float.IsInfinity(size.y))
            {
                return false;
            }

            height = size.y;
            return true;
        }

        private static bool TryCountVertices(SpineWorldActor actor, out int vertices)
        {
            vertices = 0;

            SkeletonAnimation animation = actor.Animation;
            if (animation == null)
            {
                return false;
            }

            MeshFilter filter = animation.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
            {
                return false;
            }

            vertices = mesh.vertexCount;
            return true;
        }

        private void Fail(string code, string detail)
        {
            m_ErrorCode = code;
            Debug.LogError("[SpinePlayerView] " + code + ": " + detail, this);
        }

        /// <summary>Releases the built actor; the view can be begun again afterwards.</summary>
        private void StopActor()
        {
            if (m_Actor == null)
            {
                return;
            }

            m_Actor.AnimationComplete -= HandleAnimationComplete;
            m_Actor.Release();
            DestroyActor(m_Actor);
            m_Actor = null;
            DrawnHeightWorld = 0f;
        }

        private static void DestroyActor(SpineActorBase actor)
        {
            if (actor == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(actor.gameObject);
            }
            else
            {
                Object.DestroyImmediate(actor.gameObject);
            }
        }
    }
}
