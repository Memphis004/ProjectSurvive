using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.SpineActors;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 17d: the status panel's figure is the doctor's Spine skeleton, drawn through the UI
    /// material the 17a tool writes, and it lands nowhere near the HP/MP rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Measured from the <b>shipped</b> Battle scene, like the pause overlay's layout: the thing
    /// under test is the rectangle the player sees, wired by <c>BattleSceneBuilder</c>. The build
    /// case then begins the real portrait against the real skeleton and material, so \"the actor is
    /// built once and plays the idle\" is a fact about the asset rather than about a stub.
    /// </para>
    /// <para>
    /// The two sizes the acceptance names are covered twice: this suite measures the authored
    /// geometry in the canvas' reference pixels (the CanvasScaler is 1920×1080, match 0.5, so both
    /// 16:9 screens are the same arrangement), and <c>ProjectSurvive/Tools/1. Capture UI</c> measures
    /// and photographs the real rectangles at 1920×1080 and 1280×720.
    /// </para>
    /// </remarks>
    public sealed class BattleHudPortraitTests
    {
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";

        private Scene _scene;
        private BattleHudPortrait _portrait;
        private BattleHudView _hud;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);

            _portrait = Object.FindFirstObjectByType<BattleHudPortrait>(FindObjectsInactive.Include);
            _hud = Object.FindFirstObjectByType<BattleHudView>(FindObjectsInactive.Include);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }

            _portrait = null;
            _hud = null;
        }

        // -------------------------------------------------------------------
        // What the scene authors
        // -------------------------------------------------------------------

        [Test]
        public void The_scene_authors_the_portrait_from_the_project_ui_material_and_the_portrait_profile()
        {
            Assert.That(_portrait, Is.Not.Null, "the Battle scene has no BattleHudPortrait");
            Assert.That(_hud, Is.Not.Null, "the Battle scene has no HUD");

            // The placeholder Image is gone: the panel's figure is the skeleton, not a generated bust.
            Assert.That(
                _portrait.GetComponent<Image>(), Is.Null,
                "the portrait still carries the placeholder Image");

            var fields = new SerializedObject(_portrait);

            var material = fields.FindProperty("material")?.objectReferenceValue as Material;
            Assert.That(material, Is.Not.Null, "the portrait serializes no UI material");
            Assert.That(
                material.shader, Is.Not.Null, "the portrait's material has no shader");
            Assert.That(
                material.shader.name, Is.EqualTo("Spine/SkeletonGraphic"),
                "the portrait must draw with the SkeletonGraphic material ProjectSurvive/Spine/3 writes, "
                + "not a world shader and not a runtime Shader.Find");
            Assert.That(
                AssetDatabase.GetAssetPath(material),
                Does.StartWith(SpineMaterialTool.MaterialFolder),
                "the portrait's material did not come from the 17a material tool's folder");

            var profile = fields.FindProperty("profile")?.objectReferenceValue as SpinePortraitProfile;
            Assert.That(profile, Is.Not.Null, "the portrait serializes no SpinePortraitProfile");
            Assert.That(
                AssetDatabase.GetAssetPath(profile), Is.EqualTo(SpinePortraitTool.PortraitProfileAssetPath),
                "the portrait must draw the halfbody profile, not the walker's");
            Assert.That(profile.StillAnimation, Is.Not.Empty, "the profile names no still animation");

            // The box is anchored to the status panel's top-left corner — the canvas' own bottom-left
            // corner band — rather than placed in the canvas' middle somewhere.
            var rect = (RectTransform)_portrait.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), "the portrait is not anchored to the panel's top-left corner");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.rect.size.x, Is.EqualTo(BattleHudPortrait.BoxWidth).Within(0.5f));
            Assert.That(rect.rect.size.y, Is.EqualTo(BattleHudPortrait.BoxHeight).Within(0.5f));

            var panel = rect.parent as RectTransform;
            Assert.That(panel, Is.Not.Null, "the portrait is not inside a panel");
            Assert.That(
                rect.anchoredPosition.y, Is.EqualTo(-BattleHudPortrait.TopInset).Within(0.5f),
                "the portrait is not under the status caption");
            Assert.That(
                panel.anchorMin, Is.EqualTo(Vector2.zero),
                "the status panel itself must be anchored to the canvas' corner");
        }

        // -------------------------------------------------------------------
        // The layout rule, on the scene the player gets
        // -------------------------------------------------------------------

        [Test]
        public void The_portrait_box_clears_the_HP_and_MP_rows_and_stays_inside_the_status_panel()
        {
            Assert.That(_portrait, Is.Not.Null);
            Assert.That(_hud, Is.Not.Null);

            Canvas.ForceUpdateCanvases();

            var hudFields = new SerializedObject(_hud);
            var portraitRect = (RectTransform)_portrait.transform;
            var panel = portraitRect.parent as RectTransform;
            Assert.That(panel, Is.Not.Null);

            // Every rectangle is measured in the status panel's own space, because a RectTransform's
            // `rect` is local to its pivot and the labels, the bars and the portrait have different
            // pivots. Comparing raw `rect`s would compare different origins and call everything
            // disjoint.
            var regions = new List<UiRegion>
            {
                new UiRegion(UiLayoutCheck.StatusPanel, panel.rect, UiRegionKind.Surface),
                new UiRegion(UiLayoutCheck.Portrait, InSpaceOf(panel, portraitRect), UiRegionKind.Control),
                new UiRegion(UiLayoutCheck.HpBar, InSpaceOf(panel, TransformOf(hudFields, "_hpBar")), UiRegionKind.Control),
                new UiRegion(UiLayoutCheck.ManaBar, InSpaceOf(panel, TransformOf(hudFields, "_manaBar")), UiRegionKind.Control),
                new UiRegion("HpCaption", InSpaceOf(panel, LabelRect(hudFields, "_hpCaption")), UiRegionKind.Control),
                new UiRegion("HpValue", InSpaceOf(panel, LabelRect(hudFields, "_hpValue")), UiRegionKind.Control),
                new UiRegion("ManaCaption", InSpaceOf(panel, LabelRect(hudFields, "_manaCaption")), UiRegionKind.Control),
                new UiRegion("ManaValue", InSpaceOf(panel, LabelRect(hudFields, "_manaValue")), UiRegionKind.Control),
                new UiRegion("StatusCaption", InSpaceOf(panel, LabelRect(hudFields, "_statusCaption")), UiRegionKind.Control),
                new UiRegion("PossessionLabel", InSpaceOf(panel, LabelRect(hudFields, "_possessionLabel")), UiRegionKind.Control),
            };

            foreach (string violation in UiLayoutCheck.FindViolations(regions))
            {
                Assert.Fail(violation);
            }

            // The rule above already refuses a control half outside its surface; spelled out here so
            // the report can say the picture is inside the box it is drawn in.
            Assert.That(
                UiLayoutCheck.Contains(panel.rect, InSpaceOf(panel, portraitRect)), Is.True,
                "the portrait escaped the status panel");
        }

        // -------------------------------------------------------------------
        // The actor: one build, one still frame, inside the box
        // -------------------------------------------------------------------

        [Test]
        public void Building_the_portrait_draws_one_still_halfbody_inside_its_box_and_never_rebuilds()
        {
            Assert.That(_portrait, Is.Not.Null);

            Assert.That(_portrait.IsBuilt, Is.False, "the scene must author the portrait unbuilt");

            Assert.That(_portrait.Begin(), Is.True, "the portrait refused to build: " + _portrait.ErrorCode);
            Assert.That(_portrait.ErrorCode, Is.Null);
            Assert.That(_portrait.BuildCount, Is.EqualTo(1), "the portrait built more than one actor");
            Assert.That(_portrait.CreatedAfterBuild, Is.Zero, "an actor was created after the build");

            Assert.That(
                _portrait.SkeletonDataName, Is.EqualTo("2110101_SkeletonData"),
                "the portrait is not drawing the halfbody bust from Resources/Portrait");
            Assert.That(
                _portrait.Animation, Is.EqualTo(_portrait.Profile.StillAnimation),
                "the portrait does not hold the profile's still animation");
            Assert.That(_portrait.PlayCalls, Is.EqualTo(1), "the portrait played more than one still frame");

            SpineUiActor actor = _portrait.Actor;
            Assert.That(actor, Is.Not.Null);
            Assert.That(actor.Graphic, Is.Not.Null, "the actor has no SkeletonGraphic");
            Assert.That(
                actor.Graphic.timeScale, Is.Zero,
                "the SkeletonGraphic keeps its own clock: the portrait must tick the actor itself");

            // The drawn mesh fills the box without escaping it, and its own centre is the box's.
            Mesh mesh = actor.Graphic.GetLastMesh();
            Assert.That(mesh, Is.Not.Null, "the actor's mesh is missing");
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;

            var actorRect = (RectTransform)actor.transform;
            float drawnWidth = bounds.size.x * actorRect.localScale.x;
            float drawnHeight = bounds.size.y * actorRect.localScale.y;

            Assert.That(
                drawnWidth, Is.LessThanOrEqualTo(BattleHudPortrait.BoxWidth + 0.5f),
                "the figure is wider than the portrait's box");
            Assert.That(
                drawnHeight, Is.LessThanOrEqualTo(BattleHudPortrait.BoxHeight + 0.5f),
                "the figure is taller than the portrait's box");
            Assert.That(
                actorRect.anchoredPosition.x, Is.EqualTo(-bounds.center.x * actorRect.localScale.x).Within(0.5f),
                "the figure is not horizontally centred in the box");
            Assert.That(
                actorRect.anchoredPosition.y, Is.EqualTo(-bounds.center.y * actorRect.localScale.y).Within(0.5f),
                "the figure is not vertically centred in the box");

            // The still frame must not move: the actor's own time scale is 0 and nothing ticks it.
            float held = _portrait.AnimationTime;
            actor.Tick(0.5f);
            Assert.That(
                _portrait.AnimationTime, Is.EqualTo(held).Within(0.0001f),
                "the portrait's still animation advanced: it must be held at time 0");

            // A second Begin is a no-op: the panel never rebuilds its figure while a floor runs.
            Assert.That(_portrait.Begin(), Is.True);
            Assert.That(_portrait.BuildCount, Is.EqualTo(1), "a second Begin built another actor");
            Assert.That(_portrait.CreatedAfterBuild, Is.Zero);
        }

        // -------------------------------------------------------------------
        // The portrait profile is the halfbody's own data
        // -------------------------------------------------------------------

        /// <summary>
        /// The portrait profile validates against the real halfbody skeleton, and it refuses a name
        /// the skeleton does not have with the full list beside the failure.
        /// </summary>
        [Test]
        public void The_portrait_profile_validates_against_the_halfbody_and_refuses_a_wrong_name()
        {
            var profile = AssetDatabase.LoadAssetAtPath<SpinePortraitProfile>(
                SpinePortraitTool.PortraitProfileAssetPath);
            Assert.That(profile, Is.Not.Null, SpinePortraitTool.PortraitProfileAssetPath + " is missing");

            SkeletonDataAsset asset;
            string loadCode;
            Assert.That(
                SpineSkeletonLoader.TryLoad(SpineAssetPaths.PortraitSkeletonData, out asset, out loadCode),
                Is.True, loadCode);

            SkeletonData data = asset.GetSkeletonData(true);

            string code;
            string detail;
            Assert.That(profile.TryValidate(data, out code, out detail), Is.True, code + ": " + detail);
            Assert.That(profile.StillAnimation, Is.EqualTo("stand"), "the halfbody's still frame is `stand`");
            Assert.That(profile.SkinParts, Is.Not.Empty, "the profile picks no parts, so the bust would draw nothing");

            foreach (SpineActorBase.SkinPart part in profile.SkinParts)
            {
                Assert.That(
                    data.FindSkin(part.skin), Is.Not.Null,
                    "skin part '" + part.part + "' -> '" + part.skin + "' is not in the skeleton");
            }

            // Every declared-empty slot is a real slot; a typo must fail here, not at build time.
            foreach (string slot in profile.ExpectedEmptySlots)
            {
                Assert.That(data.FindSlot(slot), Is.Not.Null, "expected empty slot '" + slot + "' is not in the skeleton");
            }

            // The skeleton's own parts: one body, one face, a face decoration, hair and a hat.
            Assert.That(profile.SkinParts.Count, Is.GreaterThanOrEqualTo(4));

            var broken = ScriptableObject.CreateInstance<SpinePortraitProfile>();
            broken.SkeletonResourcePath = SpineAssetPaths.PortraitSkeletonData;
            broken.StillAnimation = "no_such_pose";
            broken.SetSkinParts(profile.SkinPartArray());
            broken.SetExpectedEmptySlots(profile.ExpectedEmptySlotArray());
            try
            {
                Assert.That(broken.TryValidate(data, out code, out detail), Is.False);
                Assert.That(code, Is.EqualTo(SpinePortraitProfile.AnimationMissingCode));
                Assert.That(detail, Does.Contain("stand"), "the failure must list the animations the skeleton has");
            }
            finally
            {
                Object.DestroyImmediate(broken);
            }
        }

        // -------------------------------------------------------------------
        // The canvas' shader channels: only a tint-black actor asks for them
        // -------------------------------------------------------------------

        [Test]
        public void The_canvas_gets_the_tint_black_channels_only_when_tint_black_is_on()
        {
            var canvasObject = new GameObject("PortraitChannelCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            SkeletonDataAsset data;
            string loadCode;
            Assert.That(
                SpineSkeletonLoader.TryLoad(SpineAssetPaths.PlayerSkeletonData, out data, out loadCode),
                Is.True, loadCode);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                SpineMaterialTool.MaterialFolder + "/SpineGraphic-Normal.mat");
            Assert.That(material, Is.Not.Null);

            SpineUiActor plain = null;
            SpineUiActor tinted = null;

            try
            {
                AdditionalCanvasShaderChannels before = canvasObject.GetComponent<Canvas>().additionalShaderChannels;

                plain = SpineActorFactory.CreateUi(canvasObject.transform, data, build: false);
                plain.SetMaterials(material);
                plain.Build();

                Assert.That(plain.IsBuilt, Is.True, "the plain UI actor did not build");
                Assert.That(
                    canvasObject.GetComponent<Canvas>().additionalShaderChannels, Is.EqualTo(before),
                    "a portrait without tint black enabled the canvas' shader channels");

                tinted = SpineActorFactory.CreateUi(canvasObject.transform, data, build: false);
                tinted.TintBlack = true;
                tinted.SetMaterials(material);
                tinted.Build();

                Assert.That(tinted.IsBuilt, Is.True, "the tint-black actor did not build");
                Assert.That(
                    canvasObject.GetComponent<Canvas>().additionalShaderChannels,
                    Is.EqualTo(before | AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2),
                    "a tint-black actor must add TexCoord1/TexCoord2 to the canvas");
            }
            finally
            {
                if (plain != null)
                {
                    plain.Release();
                    Object.DestroyImmediate(plain.gameObject);
                }

                if (tinted != null)
                {
                    tinted.Release();
                    Object.DestroyImmediate(tinted.gameObject);
                }

                Object.DestroyImmediate(canvasObject);
            }
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static RectTransform LabelRect(SerializedObject fields, string field) =>
            (fields.FindProperty(field)?.objectReferenceValue as Component)?.transform as RectTransform;

        private static RectTransform TransformOf(SerializedObject fields, string field) =>
            fields.FindProperty(field)?.objectReferenceValue as RectTransform;

        /// <summary>One rectangle, converted into <paramref name="space"/>'s own local space.</summary>
        private static Rect InSpaceOf(RectTransform space, RectTransform target)
        {
            if (target is null)
            {
                return default;
            }

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 point = space.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
