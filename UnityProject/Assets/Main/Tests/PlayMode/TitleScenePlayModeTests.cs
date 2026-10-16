using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.ResultScene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 10d in PlayMode: the game's first screen, and the control that leaves it — driven by
    /// <b>real pointer and keyboard events</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 10's loop was Boot → Title → Camp → … without ever saying what moved the title along, so a
    /// player (and the browser suite) that opened the game sat on the first screen. This fixture is
    /// about the screen that was missing a control: the press is a real click on the point the view
    /// publishes — resolved by the scene's own <c>InputSystemUIInputModule</c> and the canvas'
    /// <c>GraphicRaycaster</c> — or a real Enter/Space, and the scene change is the real
    /// <c>SceneLoader</c>. Nothing here calls <c>RequestStart</c> and then claims to have tested the
    /// screen: a use case called directly can never see a button that is not where the pointer aims, a
    /// key that was never focused, or a second transition that quietly stacked on the first.
    /// </para>
    /// <para>
    /// The shell is booted the way the game boots it (<see cref="SceneFlowHarness.BootShell"/>), which is
    /// what makes \"Boot still stops at the title on purpose\" an assertion rather than a claim: the
    /// scene marker, the absent run and the screen's own report are all read after the handover the boot
    /// scene performed by itself.
    /// </para>
    /// </remarks>
    public sealed class TitleScenePlayModeTests : InputTestFixture
    {
        /// <summary>How long a floor may take to start before the test calls it stuck.</summary>
        private const float FloorStartBudgetSeconds = 30f;

        private Mouse _mouse;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        // ------------------------------------------------------------------
        // the stage's headline case: Boot stops at the title, the control leaves it
        // ------------------------------------------------------------------

        /// <summary>
        /// Boot hands over to the title and stops there; pressing เริ่มเล่น with the mouse starts a run
        /// and walks into the camp — one run, one scene scope, floor 1.
        /// </summary>
        [UnityTest]
        public IEnumerator Boot_stops_at_the_title_and_the_start_control_walks_into_the_camp()
        {
            // The page reports are process-wide mirrors, so a stale document from an earlier fixture
            // must not be able to satisfy the assertions below.
            TitleReport.Reset();
            CampReport.Reset();

            yield return SceneFlowHarness.BootShell();

            // Boot stops here by design: its own handover is the only move it makes.
            Assert.That(
                SceneManager.GetActiveScene().name, Is.EqualTo(SceneFlowHarness.TitleSceneName),
                "the boot flow did not stop at the title");
            Assert.That(
                SceneBoot.CompletedBootCount, Is.GreaterThan(0), "the boot suite did not run");
            Assert.That(SceneBoot.LastReport, Is.Not.Null);
            Assert.That(SceneBoot.LastReport.AllPassed, Is.True, SceneBoot.LastReport.ToLog());

            var director = SceneFlowHarness.Find<TitleSceneDirector>();
            Assert.That(director, Is.Not.Null, "the Title scene has no TitleSceneDirector");
            Assert.That(director.Started, Is.True, "the title screen did not start: " + director.StartError);
            Assert.That(director.Departed, Is.False, "the title screen left before the player asked");

            RunScopeOwner owner = SceneFlowHarness.Owner();
            Assert.That(owner.HasRun, Is.False, "the title built a run before the control was pressed");
            Assert.That(
                SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(0),
                "there was no run scope on the title screen, so nothing could have been carried into one");

            // What the screen says, from the scope that shows it.
            RootLifetimeScope scope = SceneFlowHarness.Find<RootLifetimeScope>();
            var state = scope.Container.Resolve<UiState>();
            var text = scope.Container.Resolve<LocalizationService>();
            IGameConfig config = scope.Container.Resolve<IGameConfig>();

            Assert.That(state.Title.Value, Is.EqualTo(text.Get(TitleKeys.Title)));
            Assert.That(state.StartLabel.Value, Is.EqualTo(text.Get(TitleKeys.Start)));
            Assert.That(
                state.StartLabel.Value, Does.Match("[\\u0E00-\\u0E7F]"),
                "the start caption is not the localized Thai text: " + state.StartLabel.Value);

            // The page's own document: it is what a browser test presses, so it is asserted where the
            // real scene published it.
            var published = JsonUtility.FromJson<TitleReportDto>(TitleReport.LastJson);
            Assert.That(published, Is.Not.Null, "the title screen published nothing: " + TitleReport.LastJson);
            Assert.That(published.scene, Is.EqualTo(SceneFlowHarness.TitleSceneName));
            Assert.That(published.startError, Is.Empty);
            Assert.That(published.title, Is.EqualTo(state.Title.Value), "the page reads a different title");
            Assert.That(published.startLabel, Is.EqualTo(state.StartLabel.Value));
            Assert.That(published.diagnostics, Is.Not.Empty, "the build line is missing from the report");
            Assert.That(published.canvasWidth, Is.GreaterThan(0), "the report does not name its canvas");

            Assert.That(published.buttonCount(), Is.EqualTo(1), "the screen's one control must be in the report");
            Assert.That(published.buttons[0].name, Is.EqualTo(TitleReport.Control.Start));

            var view = SceneFlowHarness.Find<TitleView>();
            Assert.That(view, Is.Not.Null, "the Title scene has no TitleView");
            Assert.That(
                view.TryGetButtonScreenPoint(TitleView.TitleButton.Start, out Vector2 point), Is.True,
                "the start control has no screen point, so a click has nothing to aim at");

            Assert.That(published.buttons[0].x, Is.EqualTo(point.x).Within(0.5f), "the report's x is not the view's");
            Assert.That(published.buttons[0].y, Is.EqualTo(point.y).Within(0.5f), "the report's y is not the view's");
            Assert.That(point.x, Is.GreaterThan(0f));
            Assert.That(point.y, Is.GreaterThan(0f));
            Assert.That(point.x, Is.LessThanOrEqualTo(Screen.width));
            Assert.That(point.y, Is.LessThanOrEqualTo(Screen.height));

            yield return Click(point);

            Assert.That(
                owner.HasRun, Is.True,
                "the click never reached the control, so no run was started");
            Assert.That(director.Presenter.RequestedScene, Is.EqualTo(SceneId.Camp), "the press asked for somewhere else");

            // The title leaves by itself; the test rides that transition rather than asking for one.
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.CampSceneName);

            Assert.That(director.Departed, Is.True, "the title screen did not record the move");
            Assert.That(director.StartError, Is.Empty, director.StartError);

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            Assert.That(camp, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(camp.Started, Is.True, "the camp did not start: " + camp.StartError);

            // One run, created once: the crossing did not add a second scope, and the camp is editing
            // the run the title started rather than one of its own.
            Assert.That(
                SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                "the crossing created " + SceneFlowHarness.Count<RunLifetimeScope>() + " run scopes");
            Assert.That(SceneFlowHarness.Count<CampSceneScope>(), Is.EqualTo(1));
            Assert.That(owner.HasRun, Is.True);
            Assert.That(owner.Current, Is.SameAs(camp.Run), "the camp is showing a different run than the one started");

            RunSession run = camp.Run;
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp));
            Assert.That(run.Seed, Is.EqualTo(owner.Seed), "the run does not carry the shell's seed");
            Assert.That(run.FloorIndex, Is.EqualTo(1), "a run started from the title begins on floor 1");
            Assert.That(run.FloorsCleared, Is.EqualTo(0));
            Assert.That(run.Wallet.Bia, Is.EqualTo(config.Economy.StartBia), "the run did not start with the starting purse");

            // And the camp's own report says the same, which is what the browser suite reads.
            var campReport = JsonUtility.FromJson<CampReportDto>(CampReport.LastStateJson);
            Assert.That(campReport, Is.Not.Null, "the camp published no state: " + CampReport.LastStateJson);
            Assert.That(campReport.flow, Is.EqualTo(nameof(RunFlowState.Camp)));
            Assert.That(campReport.floor, Is.EqualTo(1));
            Assert.That(campReport.canDepart, Is.True);
        }

        /// <summary>
        /// The same control, with the keyboard: Enter submits it.
        /// </summary>
        [UnityTest]
        public IEnumerator The_start_control_answers_the_enter_key()
        {
            yield return PressTheStartKey(_keyboard.enterKey, "Enter");
        }

        /// <summary>The same control, with the other key a player expects: Space submits it.</summary>
        [UnityTest]
        public IEnumerator The_start_control_answers_the_space_key()
        {
            yield return PressTheStartKey(_keyboard.spaceKey, "Space");
        }

        /// <summary>
        /// A second press while the first move is in flight changes nothing: the presenter refuses the
        /// request, and the loader refuses the transition with its own typed reason — so one click ends
        /// in one camp with one run.
        /// </summary>
        [UnityTest]
        public IEnumerator A_second_press_while_the_move_is_in_flight_does_not_stack_a_transition()
        {
            TitleReport.Reset();

            yield return SceneFlowHarness.BootShell();

            var director = SceneFlowHarness.Find<TitleSceneDirector>();
            var view = SceneFlowHarness.Find<TitleView>();
            SceneLoader loader = SceneFlowHarness.Loader();

            Assert.That(
                view.TryGetButtonScreenPoint(TitleView.TitleButton.Start, out Vector2 point), Is.True,
                "the start control has no screen point");

            yield return Click(point);
            yield return SceneFlowHarness.WaitUntil(
                () => loader.IsTransitioning, "the first press never started a transition");

            // The press the player makes twice, and the transition the loader would have to run twice.
            Assert.That(
                director.Presenter.RequestStart(), Is.False,
                "a second press was accepted while the first move was in flight");
            Assert.That(
                director.Presenter.RequestedScene, Is.EqualTo(SceneId.Camp), "the refusal changed the request");

            Assert.That(
                loader.TryGo(SceneId.Camp), Is.False, "the loader accepted a second transition");
            Assert.That(
                loader.LastError, Is.EqualTo(TransitionError.InProgress),
                "the refusal is not the typed one: " + loader.LastError);

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.CampSceneName);

            Assert.That(director.Departed, Is.True);
            Assert.That(
                SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                "the second press left a second run behind");
            Assert.That(
                SceneFlowHarness.Count<CampSceneScope>(), Is.EqualTo(1),
                "the second press left a second camp scope behind");

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            Assert.That(camp.Started, Is.True, camp.StartError);
            Assert.That(camp.Run.FloorIndex, Is.EqualTo(1), "the extra press advanced the run");
            Assert.That(camp.Run.FloorsCleared, Is.EqualTo(0));
        }

        /// <summary>
        /// The acceptance line \"กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่\": a run played to its
        /// summary, back to the title, and the control pressed again starts a run that is new — floor 1,
        /// the starting purse, the starting bag.
        /// </summary>
        [UnityTest]
        public IEnumerator A_run_started_after_the_summary_is_a_new_one()
        {
            TitleReport.Reset();
            ResultReport.Reset();

            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            RunSession finished = camp.Run;
            Assert.That(finished, Is.Not.Null, "the camp has no run");

            IGameConfig config = SceneFlowHarness.Find<CampSceneScope>().Container.Resolve<IGameConfig>();

            // Spend something and lose a floor, so \"nothing was carried over\" is a statement about a run
            // that really moved.
            camp.Presenter.Rent(0);
            int spent = finished.Wallet.Bia;
            Assert.That(spent, Is.LessThan(config.Economy.StartBia), "the rent took no bia");

            camp.Presenter.Depart();
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.BattleSceneName);

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            yield return WaitForFloorToStart(battle);

            Assert.That(
                battle.Runner.Submit(BattleCommand.Surrender()), Is.EqualTo(CommandResult.Ok),
                "the floor refused a surrender");
            yield return SceneFlowHarness.WaitUntil(() => battle.Settled, "the floor was never settled");
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.ResultSceneName);

            var result = SceneFlowHarness.Find<ResultSceneDirector>();
            Assert.That(result.Started, Is.True, result.StartError);
            Assert.That(result.Run, Is.SameAs(finished), "the summary screen summarised a different run");
            Assert.That(result.Presenter.RequestTitle(), Is.True, "the summary screen refused to go back to the title");

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.TitleSceneName);

            // Arriving at the title ends the run — asserted here as the starting point of the new one.
            Assert.That(SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(0), "the run scope outlived the title");
            RunScopeOwner owner = SceneFlowHarness.Owner();
            Assert.That(owner.HasRun, Is.False, "the App scope still holds the finished run");

            var director = SceneFlowHarness.Find<TitleSceneDirector>();
            Assert.That(director, Is.Not.Null, "the Title scene has no TitleSceneDirector");
            Assert.That(director.Started, Is.True, director.StartError);
            Assert.That(director.Departed, Is.False);

            var view = SceneFlowHarness.Find<TitleView>();
            Assert.That(
                view.TryGetButtonScreenPoint(TitleView.TitleButton.Start, out Vector2 point), Is.True,
                "the start control has no screen point");

            yield return Click(point);
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.CampSceneName);

            var restarted = SceneFlowHarness.Find<CampSceneDirector>();
            Assert.That(restarted.Started, Is.True, restarted.StartError);

            RunSession fresh = owner.Current;
            Assert.That(fresh, Is.Not.Null, "the title started no run");
            Assert.That(fresh, Is.Not.SameAs(finished), "the title resumed the finished run");
            Assert.That(restarted.Run, Is.SameAs(fresh), "the camp is showing a different run than the new one");
            Assert.That(fresh.FlowState, Is.EqualTo(RunFlowState.Camp));
            Assert.That(fresh.FloorIndex, Is.EqualTo(1), "a new run starts on floor 1");
            Assert.That(fresh.FloorsCleared, Is.EqualTo(0), "a new run has cleared nothing");
            Assert.That(fresh.GhostsDefeated, Is.EqualTo(0), "a new run has killed nothing");
            Assert.That(fresh.Hp, Is.EqualTo(fresh.MaxHp), "a new run starts at full HP");
            Assert.That(fresh.ChestUsed, Is.EqualTo(0), "the new run kept the finished run's chest");

            Assert.That(
                fresh.Wallet.Bia, Is.EqualTo(config.Economy.StartBia),
                "the new run kept the finished run's purse");
            Assert.That(fresh.Wallet.Bia, Is.GreaterThan(spent));

            // The starting bag: the run a shell builds from scratch, for the same seed, holds the same
            // pieces. A carry-over would show up here even if the purse did not.
            var reference = new RunSession(config, new MessagePackBackpackSnapshotCodec(), fresh.Seed);
            Assert.That(reference.StartRun().IsSuccess, Is.True, "the shipped tables must be able to start a run");

            Assert.That(
                DefIds(fresh.Backpack), Is.EqualTo(DefIds(reference.Backpack)),
                "the new run's bag is not the starting bag");
        }

        // ------------------------------------------------------------------
        // pointer and keyboard helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Boots the shell, checks the screen focused its control, and presses <paramref name="key"/>.
        /// </summary>
        /// <remarks>
        /// Enter and Space reach a Unity <c>Button</c> as the input module's submit event, and the module
        /// sends it to whatever is selected — so the focus is asserted first, because \"the key did
        /// nothing\" and \"the button was never focused\" are two different defects and this test has to
        /// be able to tell them apart.
        /// </remarks>
        private IEnumerator PressTheStartKey(KeyControl key, string name)
        {
            yield return SceneFlowHarness.BootShell();

            var director = SceneFlowHarness.Find<TitleSceneDirector>();
            Assert.That(director.Started, Is.True, director.StartError);

            GameObject selected = EventSystem.current is null
                ? null
                : EventSystem.current.currentSelectedGameObject;

            Assert.That(
                selected, Is.Not.Null,
                "nothing is selected on the title screen, so " + name + " reaches no control");
            Assert.That(selected.name, Is.EqualTo("StartButton"), name + " would submit a different control");

            var view = SceneFlowHarness.Find<TitleView>();
            Assert.That(view.Bound, Is.True, "the title view never bound");

            Assert.That(director.Presenter.RequestedScene, Is.Null, "the title screen was already leaving");

            yield return PressKey(key);

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.CampSceneName);

            Assert.That(director.Departed, Is.True, name + " left the title without an accepted move");
            Assert.That(SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1));

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            Assert.That(camp.Started, Is.True, camp.StartError);
            Assert.That(camp.Run.FloorIndex, Is.EqualTo(1));
        }

        /// <summary>A full press-and-release on one point, one frame per step.</summary>
        private IEnumerator Click(Vector2 point)
        {
            Set(_mouse.position, point);
            yield return null;

            Press(_mouse.leftButton);
            yield return null;

            Release(_mouse.leftButton);
            yield return null;
            yield return null;
        }

        /// <summary>Presses and releases a key across frames, the way a player taps it.</summary>
        private IEnumerator PressKey(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        private static IEnumerator WaitForFloorToStart(BattleSceneDirector battle)
        {
            float deadline = Time.realtimeSinceStartup + FloorStartBudgetSeconds;
            while ((battle.Runner is null || !battle.Runner.IsRunning) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(battle.Runner, Is.Not.Null, "the battle scene has no runner");
            Assert.That(battle.Runner.IsRunning, Is.True, "the floor never started: " + battle.StartError);
        }

        /// <summary>The def ids of a bag's pieces, sorted, so two starting bags compare equal.</summary>
        private static List<string> DefIds(Backpack bag)
        {
            var ids = new List<string>();
            IReadOnlyList<ItemInstance> items = bag.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ids.Add(items[i].DefId);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        // DTOs for JsonUtility: the Unity-side stand-in for the page's JSON.parse.
        [Serializable]
        private sealed class TitleReportDto
        {
            public string scene;
            public string startError;
            public string title;
            public string startLabel;
            public string diagnostics;
            public bool ready;
            public int canvasWidth;
            public int canvasHeight;
            public ControlDto[] buttons;

            /// <summary>How many controls the document names, or 0 when it names none.</summary>
            public int buttonCount() => buttons is null ? 0 : buttons.Length;
        }

        [Serializable]
        private sealed class ControlDto
        {
            public string name;
            public float x;
            public float y;
        }

        [Serializable]
        private sealed class CampReportDto
        {
            public string flow;
            public int floor;
            public bool canDepart;
        }
    }
}
