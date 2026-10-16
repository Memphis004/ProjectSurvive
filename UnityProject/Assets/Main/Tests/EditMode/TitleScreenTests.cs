using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 10d: the title screen's start control, without a scene. What pressing it does to the run,
    /// and what it says while doing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The PlayMode fixture proves the screen itself — a real pointer press, a real scene change. This
    /// one proves the rule that press rests on, in milliseconds: <b>the title starts the run through
    /// the same Application command the camp uses</b>, the run is created lazily (resolving the
    /// presenter must not build one), a second press while the move is outstanding is refused rather
    /// than started twice, and a run that is already live is never restarted.
    /// </para>
    /// <para>
    /// The scopes are built the way the game builds them — a real <see cref="RootLifetimeScope"/>
    /// component with a real <see cref="RunScopeOwner"/> — rather than a hand-rolled container, so a
    /// registration mistake in the composition root fails here rather than in a build.
    /// </para>
    /// </remarks>
    public sealed class TitleScreenTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // Edit mode rejects Object.Destroy and a scope's own Dispose calls it, so the scopes are
            // released explicitly first (the order RunScopeLifetimeTests uses).
            var scopes = Object.FindObjectsByType<RootLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] is null)
                {
                    continue;
                }

                scopes[i].StopPersisting();
                scopes[i].DisposeCore();
            }

            var runs = Object.FindObjectsByType<RunLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < runs.Length; i++)
            {
                if (runs[i] is not null)
                {
                    runs[i].DisposeCore();
                }
            }

            for (int i = 0; i < _objects.Count; i++)
            {
                if (_objects[i] is not null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        /// <summary>
        /// The screen's strings come from the localization table — including the start caption, which is
        /// player-facing text like any other (AGENTS rule 5).
        /// </summary>
        [Test]
        public void The_screen_renders_the_title_and_the_start_caption_from_the_table()
        {
            RootLifetimeScope app = NewScope();
            var text = app.Container.Resolve<LocalizationService>();
            UiState state = app.Container.Resolve<UiState>();

            app.Container.Resolve<TitlePresenter>().Refresh();

            Assert.That(state.Title.Value, Is.EqualTo(text.Get(TitleKeys.Title)));
            Assert.That(state.StartLabel.Value, Is.EqualTo(text.Get(TitleKeys.Start)));
            Assert.That(state.StartLabel.Value, Does.Not.Contain(TitleKeys.Start), "the key was shown instead of its text");
            Assert.That(state.Diagnostics.Value, Is.Not.Empty, "the build line was never written");
        }

        /// <summary>
        /// Pressing the control starts the run, and a second press while the move is outstanding is
        /// refused: one request at a time, nothing changed by the refusal.
        /// </summary>
        [Test]
        public void The_start_control_starts_the_run_once_and_refuses_a_second_press()
        {
            RootLifetimeScope app = NewScope();
            RunScopeOwner owner = app.Container.Resolve<RunScopeOwner>();
            IGameConfig config = app.Container.Resolve<IGameConfig>();
            TitlePresenter presenter = app.Container.Resolve<TitlePresenter>();

            // The laziness the presenter's doc claims: building the screen does not build a run, so a
            // player who never presses anything never has one.
            Assert.That(owner.HasRun, Is.False, "resolving the title's presenter created a run");
            Assert.That(presenter.RequestedScene, Is.Null, "the screen came up already leaving");

            Assert.That(presenter.RequestStart(), Is.True, "the first press is recorded");
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Camp), "the way on is the camp");
            Assert.That(presenter.StartError, Is.Empty, presenter.StartError);

            RunSession run = owner.RequireRun();
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp), "the run did not start");
            Assert.That(run.FloorIndex, Is.EqualTo(1), "a run started from the title begins on floor 1");
            Assert.That(run.Wallet.Bia, Is.EqualTo(config.Economy.StartBia), "a run started from the title has the starting purse");

            // The refusal that stops a double click: the presenter has one outstanding request, and the
            // run's own state machine would refuse a second StartRun anyway (RunError.WrongState).
            Assert.That(presenter.RequestStart(), Is.False, "a second press while the move is outstanding was accepted");
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Camp), "the refusal changed the request");
            Assert.That(
                run.StartRun().IsSuccess, Is.False,
                "the run accepted a second start, so a double click could have created a second run");

            Assert.That(
                run.Wallet.Bia, Is.EqualTo(config.Economy.StartBia), "the purse moved, so something was applied twice");
            Assert.That(CountRuns(), Is.EqualTo(1), "there is not exactly one run scope");
        }

        /// <summary>
        /// A run that is already under way is not started again: the title screen opened over a live run
        /// only takes the player to the camp, where that run belongs.
        /// </summary>
        [Test]
        public void A_live_run_is_taken_to_the_camp_rather_than_started_again()
        {
            RootLifetimeScope app = NewScope();
            RunScopeOwner owner = app.Container.Resolve<RunScopeOwner>();
            TitlePresenter presenter = app.Container.Resolve<TitlePresenter>();

            RunSession live = owner.RequireRun();
            Assert.That(live.StartRun().IsSuccess, Is.True);
            Assert.That(live.Purchase(0).IsSuccess, Is.True, "the fixture expected a rentable first shelf");

            int bia = live.Wallet.Bia;
            int floor = live.FloorIndex;

            Assert.That(presenter.RequestStart(), Is.True, "a live run must still be able to leave the title");
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Camp));
            Assert.That(live.FlowState, Is.EqualTo(RunFlowState.Camp), "the live run was sent back to the title phase");
            Assert.That(live.Wallet.Bia, Is.EqualTo(bia), "the live run's purse was reset by the press");
            Assert.That(live.FloorIndex, Is.EqualTo(floor));
        }

        /// <summary>
        /// The acceptance line \"กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่\": the presenter holds
        /// no run, so the run the shell builds after the previous one was disposed is a new one — floor 1,
        /// the starting purse, an empty chest.
        /// </summary>
        [Test]
        public void After_the_previous_run_was_disposed_the_start_control_builds_a_new_one()
        {
            RootLifetimeScope app = NewScope();
            RunScopeOwner owner = app.Container.Resolve<RunScopeOwner>();
            IGameConfig config = app.Container.Resolve<IGameConfig>();
            TitlePresenter presenter = app.Container.Resolve<TitlePresenter>();

            RunSession finished = owner.RequireRun();
            Assert.That(finished.StartRun().IsSuccess, Is.True);
            Assert.That(finished.Purchase(0).IsSuccess, Is.True);
            int spent = finished.Wallet.Bia;
            Assert.That(spent, Is.LessThan(config.Economy.StartBia), "the fixture expected the rent to cost something");

            // What arriving at the title does: SceneLoader disposes the run scope.
            owner.DisposeRun();

            Assert.That(presenter.RequestStart(), Is.True);
            RunSession fresh = owner.RequireRun();

            Assert.That(fresh, Is.Not.SameAs(finished), "the disposed run came back");
            Assert.That(fresh.FlowState, Is.EqualTo(RunFlowState.Camp));
            Assert.That(fresh.FloorIndex, Is.EqualTo(1));
            Assert.That(fresh.FloorsCleared, Is.EqualTo(0));
            Assert.That(fresh.GhostsDefeated, Is.EqualTo(0));
            Assert.That(fresh.Wallet.Bia, Is.EqualTo(config.Economy.StartBia), "the new run kept the finished run's purse");
            Assert.That(fresh.ChestUsed, Is.EqualTo(0), "the new run kept the finished run's chest");
        }

        /// <summary>Every string the screen reads exists in <b>both</b> languages.</summary>
        [Test]
        public void Every_title_string_exists_in_both_languages()
        {
            var config = ResourcesGameConfigLoader.Load();
            var text = new LocalizationService(config);

            foreach (string key in TitleKeys.All)
            {
                Language[] languages = { Language.Th, Language.En };
                foreach (Language language in languages)
                {
                    text.Language = language;
                    Assert.That(
                        text.TryGet(key, out string value), Is.True,
                        "the title screen reads '" + key + "', which is not in the table");
                    Assert.That(
                        string.IsNullOrWhiteSpace(value), Is.False,
                        "'" + key + "' has no " + language + " text");
                }
            }
        }

        private RootLifetimeScope NewScope()
        {
            var scopeObject = new GameObject("EditModeAppScope");
            _objects.Add(scopeObject);

            RootLifetimeScope scope = scopeObject.AddComponent<RootLifetimeScope>();

            // Awake does not run outside play mode, so the scope is built explicitly. It has no parent,
            // so this is the App scope: the shell plus the run owner.
            scope.Build();
            return scope;
        }

        private static int CountRuns() =>
            Object.FindObjectsByType<RunLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
    }
}
