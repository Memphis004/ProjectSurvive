using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.SpineActors;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// The status panel's figure: the doctor's half-body bust, drawn once through a
    /// <see cref="SpineUiActor"/> and then never advanced.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing here is logic.</b> It does not know what a hit is, how much health the doctor has,
    /// or which floor this is: the panel's numbers are the HUD presenter's, and the portrait only
    /// shows who the numbers belong to.
    /// </para>
    /// <para>
    /// <b>It stays a SkeletonGraphic on purpose.</b> The bust has to keep its parts separate — head,
    /// hair, body, hat — so a future customisation screen can swap them; a baked Image would throw
    /// that away. The parts come from the profile's combined skin, exactly like the camp and the
    /// arena, and no skin or slot name is written here.
    /// </para>
    /// <para>
    /// <b>One build, one still frame.</b> <see cref="Begin"/> loads the profile's skeleton, composes
    /// the skin, plays the profile's still animation at <b>time 0 with speed 0</b> (or, for a profile
    /// with no still animation, leaves the setup pose) and fits the mesh into this object's
    /// rectangle. There is no per-frame update: a still picture that is re-evaluated every frame is
    /// cost with no picture to show for it, and the test suite counts the plays
    /// (<see cref="PlayCalls"/>) to prove the count does not grow with the frame count.
    /// </para>
    /// <para>
    /// <b>Materials are serialized references, never a shader name.</b> The drawing material is the
    /// project UI material from <c>ProjectSurvive/Spine/3. Create UI materials</c>; the atlas page's
    /// own texture is loaded through the page material
    /// <c>ProjectSurvive/Spine/4. Create portrait profile</c> writes. <c>Shader.Find</c> returns null
    /// in a stripped player build, so nothing here looks a shader up by name.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleHudPortrait : MonoBehaviour
    {
        /// <summary>How far below the status panel's top edge the figure starts, in UI pixels.</summary>
        /// <remarks>
        /// The status panel's caption ("สถานะ") has its own 24 px line at the panel's top, so the
        /// figure starts under it. Stage 08g-2 brought this down from 46 px when the panel became the
        /// small corner frame of 380 × 212: at 46 the box would have hung off the bottom edge.
        /// </remarks>
        public const float TopInset = 28f;

        /// <summary>
        /// Width of the portrait's column, in UI pixels. The HP/MP captions start 92 px from the
        /// panel's left edge, so an 80 px box leaves them a 2 px gutter at both sizes.
        /// </summary>
        public const float BoxWidth = 80f;

        /// <summary>
        /// Height of the portrait's box, in UI pixels. The bust is wider than it is tall, so the fit
        /// is width-bound and the box is what the picture is clipped to.
        /// </summary>
        public const float BoxHeight = 110f;

        /// <summary>No profile was assigned, so there is no skeleton to draw.</summary>
        public const string ProfileMissingCode = "spine.portrait_profile_missing";

        /// <summary>No UI material was assigned. There is no shader-name fallback on purpose.</summary>
        public const string MaterialMissingCode = "spine.portrait_material_missing";

        /// <summary>The actor did not build from the skeleton, skin and material.</summary>
        public const string BuildFailedCode = "spine.portrait_build_failed";

        /// <summary>The built mesh has no size, so it cannot be fitted into the box.</summary>
        public const string BoundsEmptyCode = "spine.portrait_bounds_empty";

        /// <summary>The GenericCute backend is selected but no portrait sprite was assigned.</summary>
        public const string SpriteMissingCode = "generic_cute.portrait_sprite_missing";

        [SerializeField] private SpinePortraitProfile profile;
        [SerializeField] private Material material;

        [Tooltip("Which backend draws the doctor. The status panel follows the arena's own choice (Stage 17f).")]
        [SerializeField] private PlayerFigureBackend backend;

        [Tooltip("The 2D Animation backend's portrait: the pack's portrait sprite, drawn as one Image in the same box.")]
        [SerializeField] private Sprite spritePortrait;

        private SpineUiActor m_Actor;
        private Image m_Sprite;
        private int m_BuildCount;
        private int m_PlayCalls;
        private string m_ErrorCode;

        /// <summary>The profile the figure is drawn from (the serialized asset).</summary>
        public SpinePortraitProfile Profile => profile;

        /// <summary>The built actor, or null before <see cref="Begin"/> succeeded.</summary>
        public SpineUiActor Actor => m_Actor;

        /// <summary>Which backend this portrait draws with.</summary>
        public PlayerFigureBackend Backend => backend;

        /// <summary>The sprite the GenericCute backend draws, or null for a Spine portrait.</summary>
        public Sprite SpritePortrait => spritePortrait;

        /// <summary>The built sprite, or null when the Spine bust is what was built.</summary>
        public Image SpriteImage => m_Sprite;

        /// <summary>True once the figure is built and drawable — as a Spine bust or as one sprite.</summary>
        public bool IsBuilt =>
            m_Sprite != null || (m_Actor != null && m_Actor.IsBuilt);

        /// <summary>How many actors this portrait has created (one per successful build).</summary>
        public int BuildCount => m_BuildCount;

        /// <summary>
        /// Actors created <b>after</b> the first one. A portrait that is rebuilt while a floor runs
        /// would report more than 0; the acceptance is that it never does.
        /// </summary>
        public int CreatedAfterBuild => m_BuildCount > 1 ? m_BuildCount - 1 : 0;

        /// <summary>How many animations this portrait has started (0 for a setup pose, else 1).</summary>
        public int PlayCalls => m_PlayCalls;

        /// <summary>The code that made <see cref="Begin"/> fail, or null after a success.</summary>
        public string ErrorCode => m_ErrorCode;

        /// <summary>The skeleton asset the built actor was made from, or null.</summary>
        public string SkeletonDataName => m_Actor == null || m_Actor.Data == null ? null : m_Actor.Data.name;

        /// <summary>The animation playing on the skeleton's one track, or null for a setup pose.</summary>
        public string Animation
        {
            get
            {
                if (m_Actor == null || !m_Actor.IsBuilt)
                {
                    return null;
                }

                TrackEntry entry = m_Actor.AnimationState.GetCurrent(0);
                return entry == null || entry.Animation == null ? null : entry.Animation.Name;
            }
        }

        /// <summary>
        /// Seconds the still animation has played. Held at 0 by construction: a still frame that
        /// moved would not be still, and the PlayMode case reads this across frames to prove it.
        /// </summary>
        public float AnimationTime
        {
            get
            {
                if (m_Actor == null || !m_Actor.IsBuilt)
                {
                    return 0f;
                }

                TrackEntry entry = m_Actor.AnimationState.GetCurrent(0);
                return entry == null ? 0f : entry.TrackTime;
            }
        }

        /// <summary>
        /// Builds the figure once and fits it into this object's rectangle. Idempotent: a second
        /// call after a success returns true without creating or playing anything.
        /// </summary>
        public bool Begin()
        {
            if (IsBuilt)
            {
                return true;
            }

            m_ErrorCode = null;

            if (backend == PlayerFigureBackend.GenericCute)
            {
                return BeginSprite();
            }

            return BeginSpine();
        }

        /// <summary>
        /// The 2D Animation backend's bust: the pack's portrait sprite, drawn as one <see cref="Image"/> in
        /// the same box the Spine bust is fitted into.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The two backends draw the same doctor, so the panel must not show a Spine half-body beside a
        /// 2D Animation figure. A sprite portrait is a single picture rather than separated parts — the
        /// pack has no part-separated half-body.
        /// </para>
        /// <para>
        /// <b>The picture covers the column, it does not sit inside it.</b> The column is 80 × 110 and the
        /// art is square, so fitting it whole left 15 px of empty panel above and below the face and made
        /// the bust read as a small square stuck to the top of the status panel. The picture now scales to
        /// the column's <b>height</b> and the sides that no longer fit are cropped by this object's own
        /// <see cref="RectMask2D"/> — the aspect is kept, so the face is not squashed, and the crop is the
        /// box the panel always declared.
        /// </para>
        /// <para>
        /// It is built once and never re-evaluated: a still picture has no per-frame cost beyond the
        /// canvas, and the mask is a UI clip, not a per-frame texture copy.
        /// </para>
        /// </remarks>
        private bool BeginSprite()
        {
            if (spritePortrait == null)
            {
                Fail(
                    SpriteMissingCode,
                    "the GenericCute backend is selected but no portrait sprite was assigned; " +
                    "ProjectSurvive/Player Figure/3 writes it");
                return false;
            }

            // The box clips: the cover-sized picture is wider than the column by construction.
            if (GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }

            var spriteObject = new GameObject("HudPortraitSprite", typeof(RectTransform));
            var spriteRect = (RectTransform)spriteObject.transform;
            spriteRect.SetParent(transform, false);
            spriteRect.anchorMin = new Vector2(0.5f, 0.5f);
            spriteRect.anchorMax = new Vector2(0.5f, 0.5f);
            spriteRect.pivot = new Vector2(0.5f, 0.5f);
            spriteRect.anchoredPosition = Vector2.zero;

            var image = spriteObject.AddComponent<Image>();
            image.sprite = spritePortrait;
            image.preserveAspect = true;
            image.raycastTarget = false;

            spriteRect.sizeDelta = CoverSize(spritePortrait, ColumnSize());

            m_Sprite = image;
            m_BuildCount++;
            return true;
        }

        /// <summary>This object's own rectangle, or the authored column before a canvas has laid it out.</summary>
        private Vector2 ColumnSize()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            return size.x > 0f && size.y > 0f ? size : new Vector2(BoxWidth, BoxHeight);
        }

        /// <summary>
        /// The smallest rectangle, with the sprite's own aspect, that covers <paramref name="box"/>: one
        /// dimension is the box's and the other is at least as large, so the picture fills the column and
        /// the overflow is what the box's mask crops.
        /// </summary>
        private static Vector2 CoverSize(Sprite sprite, Vector2 box)
        {
            Vector2 art = sprite.rect.size;
            if (art.x <= 0f || art.y <= 0f)
            {
                return box;
            }

            float artAspect = art.x / art.y;
            float boxAspect = box.x / box.y;

            // A relatively wider picture is bound by the height, a relatively taller one by the width.
            return artAspect > boxAspect
                ? new Vector2(box.y * artAspect, box.y)
                : new Vector2(box.x, box.x / artAspect);
        }

        private bool BeginSpine()
        {
            if (profile == null)
            {
                Fail(ProfileMissingCode, "no SpinePortraitProfile was assigned to the portrait");
                return false;
            }

            if (material == null)
            {
                Fail(
                    MaterialMissingCode,
                    "no UI material was assigned; ProjectSurvive/Spine/3. Create UI materials writes the " +
                    "SpineGraphic-Normal asset this view references");
                return false;
            }

            SkeletonDataAsset data;
            string loadCode;
            if (!SpineSkeletonLoader.TryLoad(profile.SkeletonResourcePath, out data, out loadCode))
            {
                Fail(loadCode, "resource path '" + profile.SkeletonResourcePath + "'");
                return false;
            }

            SkeletonData skeleton = data.GetSkeletonData(true);
            string code;
            string detail;
            if (!profile.TryValidate(skeleton, out code, out detail))
            {
                Fail(code, detail);
                return false;
            }

            // Built through the one factory, with no animation yet: the still pose is applied after
            // the skin, so the mesh that gets measured is the one that will be drawn.
            SpineUiActor actor = SpineActorFactory.CreateUi(
                transform, data, skin: null, animation: null, loop: true,
                actorName: "HudPortraitActor", build: false);

            actor.SetMaterials(material);
            actor.SetSkinParts(profile.SkinPartArray());
            actor.SetExpectedEmptySlots(profile.ExpectedEmptySlotArray());

            // The box is the clip: a bust taller than its column is cut at the panel's edge rather
            // than drawn over the HP rows.
            actor.UseClipping = true;
            actor.Build();

            if (!actor.IsBuilt)
            {
                Fail(BuildFailedCode, "the actor did not build from '" + data.name + "'");
                DestroyActor(actor);
                return false;
            }

            m_BuildCount++;
            m_Actor = actor;

            if (!string.IsNullOrEmpty(profile.StillAnimation))
            {
                // Speed 0 holds the animation at its first frame. The SkeletonGraphic's own clock is
                // stopped too, so nothing can advance it behind this class's back.
                if (actor.Play(profile.StillAnimation, true, 0f) == null)
                {
                    Fail(
                        BuildFailedCode,
                        "still animation '" + profile.StillAnimation + "' did not play on '" + data.name + "'");
                    StopActor();
                    return false;
                }

                m_PlayCalls++;
            }

            if (actor.Graphic != null)
            {
                actor.Graphic.timeScale = 0f;
            }

            actor.Tick(0f);

            if (!FitIntoBox(actor))
            {
                Fail(BoundsEmptyCode, "the skeleton's mesh has no size to fit the portrait's box");
                StopActor();
                return false;
            }

            return true;
        }

        private void OnDestroy()
        {
            StopActor();
            StopSprite();
        }

        /// <summary>
        /// Scales the actor so its own mesh fills this object's rectangle and centres it there.
        /// </summary>
        /// <remarks>
        /// The graphic's rect is matched to the pose first, so the canvas has a culling rectangle
        /// around what is actually drawn; the actor's root then carries one uniform scale and the
        /// offset that puts the mesh's own centre at the box's centre (a skeleton's origin is at
        /// the feet, not at the middle of the figure).
        /// </remarks>
        private bool FitIntoBox(SpineUiActor actor)
        {
            if (actor.Graphic == null)
            {
                return false;
            }

            actor.MatchRectTransformToBounds();

            Mesh mesh = actor.Graphic.GetLastMesh();
            if (mesh == null)
            {
                return false;
            }

            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            {
                return false;
            }

            var box = (RectTransform)transform;
            Vector2 size = box.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                // A canvas that has not been laid out yet still has the authored box.
                size = new Vector2(BoxWidth, BoxHeight);
            }

            float scale = Mathf.Min(size.x / bounds.size.x, size.y / bounds.size.y);
            if (scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale))
            {
                return false;
            }

            var actorRect = (RectTransform)actor.transform;
            actorRect.anchorMin = new Vector2(0.5f, 0.5f);
            actorRect.anchorMax = new Vector2(0.5f, 0.5f);
            actorRect.pivot = new Vector2(0.5f, 0.5f);
            actorRect.sizeDelta = size;
            actorRect.anchoredPosition = new Vector2(-bounds.center.x * scale, -bounds.center.y * scale);
            actorRect.localScale = new Vector3(scale, scale, 1f);
            return true;
        }

        private void Fail(string code, string detail)
        {
            m_ErrorCode = code;
            Debug.LogError("[BattleHudPortrait] " + code + ": " + detail, this);
        }

        /// <summary>Releases the built sprite; the portrait can be begun again afterwards.</summary>
        private void StopSprite()
        {
            if (m_Sprite == null)
            {
                return;
            }

            GameObject built = m_Sprite.gameObject;
            m_Sprite = null;

            if (Application.isPlaying)
            {
                Destroy(built);
            }
            else
            {
                DestroyImmediate(built);
            }
        }

        /// <summary>Releases the built actor; the portrait can be begun again afterwards.</summary>
        private void StopActor()
        {
            if (m_Actor == null)
            {
                return;
            }

            m_Actor.Release();
            DestroyActor(m_Actor);
            m_Actor = null;
            m_PlayCalls = 0;
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
