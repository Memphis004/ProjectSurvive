using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Battle.Pause;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// The pause overlay (Stage 13c-2b): judged on the scene the game ships, then driven through its
    /// presenter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two halves, because the overlay is two things. Its <b>layout</b> is measured from the authored
    /// scene with the same <see cref="UiLayoutCheck"/> verdicts the จัดย่าม screen and the summary use:
    /// the card's controls must not land on one another and none of them may escape the card. Its
    /// <b>behaviour</b> is driven without play mode — the presenter is plain C# over a real
    /// <see cref="RunSession"/> on the shipped tables, so หยุด, เล่นต่อ and กลับศาลา are checkable in the
    /// Fast suite.
    /// </para>
    /// <para>
    /// The scene is opened additively and closed again, so the test never disturbs the scene the Editor
    /// has open.
    /// </para>
    /// </remarks>
    public sealed class BattlePauseTests
    {
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";

        private const uint Seed = 20261014u;

        private IGameConfig _config;
        private Scene _scene;
        private BattlePauseView _view;
        private BattleHudView _hud;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();

            _scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(_scene);

            _view = Object.FindFirstObjectByType<BattlePauseView>(FindObjectsInactive.Include);
            _hud = Object.FindFirstObjectByType<BattleHudView>(FindObjectsInactive.Include);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }

            _view = null;
            _hud = null;
        }

        [Test]
        public void The_scene_authors_the_overlay_closed_with_both_controls_wired()
        {
            Assert.IsNotNull(_view, "the Battle scene has no BattlePauseView");
            Assert.IsNotNull(_hud, "the Battle scene has no BattleHudView");

            var fields = new SerializedObject(_view);
            var panel = fields.FindProperty("_panel")?.objectReferenceValue as GameObject;

            Assert.IsNotNull(panel, "the pause view does not serialize its overlay root");
            Assert.IsFalse(panel.activeSelf, "the overlay must be authored closed");

            Assert.IsNotNull(ButtonField(fields, "_resumeButton"), "the overlay has no เล่นต่อ control");
            Assert.IsNotNull(ButtonField(fields, "_leaveButton"), "the overlay has no กลับศาลา control");
            Assert.IsNotNull(ButtonField(fields, "_openButton"), "the overlay has no HUD หยุด control");

            // The two labels are authored empty and filled from the localization table at runtime,
            // exactly like every other label in the project (AGENTS rule 5).
            Assert.IsNotNull(LabelField(fields, "_titleLabel"));
            Assert.IsNotNull(LabelField(fields, "_warningLabel"));
        }

        [Test]
        public void The_overlay_cannot_be_read_through_and_its_controls_stay_inside_the_card()
        {
            var fields = new SerializedObject(_view);
            var panel = fields.FindProperty("_panel")?.objectReferenceValue as GameObject;
            Assert.IsNotNull(panel);

            RectTransform card = Child(panel.transform, UiLayoutCheck.PausePanel);
            RectTransform dim = Child(panel.transform, UiLayoutCheck.PauseOverlayDim);

            Assert.IsNotNull(card, "the overlay has no card");
            Assert.IsNotNull(dim, "the overlay has no dim sheet");

            // Every rectangle is measured in the card's own space: a RectTransform's <c>rect</c> is
            // local to its pivot, so comparing a child's rect with its sibling's would compare two
            // different origins and call any two labels disjoint.
            var regions = new List<UiRegion>
            {
                new UiRegion(UiLayoutCheck.PausePanel, card.rect, UiRegionKind.Overlay, Alpha(card)),
                new UiRegion(UiLayoutCheck.PauseOverlayDim, InSpaceOf(card, dim), UiRegionKind.Overlay, Alpha(dim)),
                new UiRegion(UiLayoutCheck.PauseTitle, InSpaceOf(card, LabelField(fields, "_titleLabel").rectTransform), UiRegionKind.ModalControl),
                new UiRegion(UiLayoutCheck.PauseWarning, InSpaceOf(card, LabelField(fields, "_warningLabel").rectTransform), UiRegionKind.ModalControl),
                new UiRegion(UiLayoutCheck.PauseResume, InSpaceOf(card, TransformOf(fields, "_resumeButton")), UiRegionKind.ModalControl),
                new UiRegion(UiLayoutCheck.PauseLeave, InSpaceOf(card, TransformOf(fields, "_leaveButton")), UiRegionKind.ModalControl),
            };

            foreach (string violation in UiLayoutCheck.FindViolations(regions))
            {
                Assert.Fail(violation);
            }

            foreach (string escapee in UiLayoutCheck.FindModalEscapees(regions, UiLayoutCheck.PausePanel))
            {
                Assert.Fail(escapee);
            }

            foreach (string opacity in UiLayoutCheck.FindOpacityViolations(regions))
            {
                Assert.Fail(opacity);
            }

            Assert.IsTrue(
                UiLayoutCheck.Contains(dim.rect, InSpaceOf(dim, card)),
                "the card is drawn outside the sheet that takes the screen out of play");
        }

        [Test]
        public void The_HUDs_pause_control_is_drawn_inside_the_floor_panel()
        {
            var hudFields = new SerializedObject(_hud);
            var viewFields = new SerializedObject(_view);

            TMP_Text floorLabel = LabelField(hudFields, "_floorLabel");
            Assert.IsNotNull(floorLabel, "the HUD has no floor label");

            var floorPanel = floorLabel.transform.parent as RectTransform;
            Assert.IsNotNull(floorPanel, "the floor label is not inside a panel");

            Rect pauseButton = InSpaceOf(floorPanel, TransformOf(viewFields, "_openButton"));
            Assert.IsTrue(
                UiLayoutCheck.Contains(floorPanel.rect, pauseButton),
                "หยุด " + pauseButton + " is not inside the floor panel " + floorPanel.rect);
        }

        [Test]
        public void Pausing_holds_the_floor_and_resuming_lets_it_run_again()
        {
            using var fixture = new Fixture(_config);

            Assert.IsFalse(fixture.Presenter.IsOpen, "the overlay starts closed");
            Assert.IsFalse(fixture.Runner.IsPaused, "the floor starts running");

            fixture.Presenter.Open();

            Assert.IsTrue(fixture.Presenter.IsOpen, "หยุด did not open the overlay");
            Assert.IsTrue(fixture.Runner.IsPaused, "the floor was not held still");

            fixture.Presenter.Toggle();

            Assert.IsFalse(fixture.Presenter.IsOpen);
            Assert.IsFalse(fixture.Runner.IsPaused, "เล่นต่อ did not let the floor run");

            // The five strings are looked up when the overlay opens, so the labels the player reads
            // are the table's rows rather than the empty strings the scene was authored with.
            fixture.Presenter.Open();
            Assert.AreEqual(fixture.Text.Get("ui.pause.title"), fixture.State.Title.Value);
            Assert.AreEqual(fixture.Text.Get("ui.pause.warning"), fixture.State.Warning.Value);
            Assert.AreEqual(fixture.Text.Get("ui.pause.resume"), fixture.State.ResumeLabel.Value);
            Assert.AreEqual(fixture.Text.Get("ui.pause.leave"), fixture.State.LeaveLabel.Value);
            Assert.AreEqual(fixture.Text.Get("ui.pause.open"), fixture.State.OpenLabel.Value);

            StringAssert.DoesNotContain("ui.pause", fixture.State.Title.Value);
        }

        [Test]
        public void Leaving_for_the_camp_abandons_the_floor_without_ending_the_run()
        {
            using var fixture = new Fixture(_config);

            int biaBefore = fixture.Run.Wallet.Bia;
            int floorBefore = fixture.Run.FloorIndex;

            fixture.Presenter.Open();
            Result leaving = fixture.Presenter.LeaveForCamp();

            Assert.IsTrue(leaving.IsSuccess);
            Assert.AreEqual(RunFlowState.Camp, fixture.Run.FlowState, "the run did not return to the camp");
            Assert.That(
                fixture.Presenter.RequestedScene, Is.EqualTo(SceneId.Camp),
                "the scene change was not requested");

            // A retreat is not a clear: nothing was paid, the purse is untouched and the floor is
            // still the one the run is on, so the player can depart for it again.
            Assert.AreEqual(0, fixture.Run.FloorsCleared);
            Assert.AreEqual(biaBefore, fixture.Run.Wallet.Bia);
            Assert.AreEqual(floorBefore, fixture.Run.FloorIndex);
            Assert.IsFalse(fixture.Run.IsBagLocked, "the bag stayed locked outside a floor");

            // Once the camp is the run's phase there is nothing left to abandon, and the request the
            // director already holds is not thrown away by the refusal.
            Result second = fixture.Presenter.LeaveForCamp();
            Assert.IsFalse(second.IsSuccess);
            Assert.AreEqual(RunFlowState.Camp, fixture.Run.FlowState);
            Assert.That(fixture.Presenter.RequestedScene, Is.EqualTo(SceneId.Camp));
            Assert.IsNotEmpty(fixture.Presenter.LastError);

            fixture.Presenter.DepartureAccepted();
            Assert.IsNull(fixture.Presenter.RequestedScene, "the accepted departure was not cleared");
        }

        private static RectTransform Child(Transform parent, string name) =>
            parent.Find(name) as RectTransform;

        private static float Alpha(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            return image is null ? 1f : image.color.a;
        }

        private static TMP_Text LabelField(SerializedObject fields, string field) =>
            fields.FindProperty(field)?.objectReferenceValue as TMP_Text;

        private static Button ButtonField(SerializedObject fields, string field) =>
            fields.FindProperty(field)?.objectReferenceValue as Button;

        private static RectTransform TransformOf(SerializedObject fields, string field) =>
            ButtonField(fields, field)?.transform as RectTransform;

        /// <summary>
        /// One rectangle, converted into <paramref name="space"/>'s own local space.
        /// </summary>
        /// <remarks>
        /// The four world corners and an inverse transform, because the regions compared here have
        /// three different pivots: the card is centred, the labels are top-left anchored and the
        /// dim sheet is stretched over the canvas.
        /// </remarks>
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

        /// <summary>One run, one floor and the overlay's real presenter over them.</summary>
        private sealed class Fixture : System.IDisposable
        {
            private readonly GameObject _runnerObject;

            public Fixture(IGameConfig config)
            {
                Text = new LocalizationService(config);
                State = new BattlePauseState();
                Presenter = new BattlePausePresenter(State, Text);

                Run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), Seed);
                Assert.IsTrue(Run.StartRun().IsSuccess);

                Result<BattleSetup> departure = Run.StartFloor();
                Assert.IsTrue(departure.IsSuccess);

                // A runner with no view: the fixture drives the pause state, not pixels, and the
                // presenter falls back to the run's own HP when no frame has been copied.
                _runnerObject = new GameObject("BattlePauseFixtureRunner");
                Runner = _runnerObject.AddComponent<BattleSimulationRunner>();

                Presenter.Attach(Runner, Run);
            }

            public LocalizationService Text { get; }

            public BattlePauseState State { get; }

            public BattlePausePresenter Presenter { get; }

            public RunSession Run { get; }

            public BattleSimulationRunner Runner { get; }

            public void Dispose()
            {
                Presenter.Dispose();
                State.Dispose();
                Object.DestroyImmediate(_runnerObject);
            }
        }
    }
}
