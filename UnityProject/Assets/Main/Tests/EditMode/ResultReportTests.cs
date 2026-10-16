using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// The document the summary screen publishes to the page: what it says about the run, and where a
    /// browser test can click.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A browser test is the only place the last leg of Stage 10's loop can be observed end to end in a
    /// <i>build</i>, and it is a slow gate (a WebGL player has to boot). So what the document contains
    /// is checked here, in the Fast suite, against the shipped scene and the shipped tables: the same
    /// strings <c>ResultPresenter</c> composed, the same numbers <c>RunSummary</c> reports, and the two
    /// controls the screen really draws. The browser test then has one job — that a real player, in a
    /// real build, gets there and the document appears.
    /// </para>
    /// <para>
    /// The scene is opened <b>additively</b> and closed in <see cref="TearDown"/>, so this fixture never
    /// disturbs the scene the Editor has open and never enters play mode; the run is built from the
    /// shipped tables through the fake battle port, exactly as <c>ResultScreenTests</c> builds it.
    /// </para>
    /// </remarks>
    public sealed class ResultReportTests
    {
        private const string ResultScenePath = "Assets/Main/Scenes/Result.unity";

        /// <summary>A seed whose floor-1 run reaches the fake's outcome overrides cleanly.</summary>
        private const uint Seed = 20261014u;

        private Scene _scene;

        /// <summary>
        /// The report's mirror is process-wide, so it starts every test from empty: a PlayMode fixture
        /// that published a summary leaves a count behind, and "this screen published once" is only a
        /// statement about this screen when the counter started at zero.
        /// </summary>
        [SetUp]
        public void SetUp() => ResultReport.Reset();

        [TearDown]
        public void TearDown()
        {
            ResultReport.Reset();

            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }
        }

        /// <summary>
        /// A lost run publishes the defeat headline the table holds, the run's own numbers, and both
        /// ways out of the screen — each one a point a browser test can click.
        /// </summary>
        [Test]
        public void A_lost_run_publishes_the_defeat_headline_its_numbers_and_both_controls()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            ResultView view = OpenResultScene();
            RunSession run = DefeatedRun(config);
            RunSummary summary = run.Summary();

            var state = new ResultUiState();
            var text = new LocalizationService(config);
            var presenter = new ResultPresenter(run, text, state);
            presenter.Begin();

            string json = ResultReport.Publish(summary, state, view, "Result", string.Empty);

            Assert.That(ResultReport.PublishCount, Is.EqualTo(1));
            Assert.That(ResultReport.LastJson, Is.EqualTo(json), "the mirror is not what was published");
            Assert.That(json, Does.Not.Contain("}{"), "array elements lost their comma: " + json);

            var parsed = JsonUtility.FromJson<SummaryReportDto>(json);
            Assert.That(parsed, Is.Not.Null, "the summary report is not valid JSON: " + json);

            Assert.That(parsed.scene, Is.EqualTo("Result"), "the report does not name the scene on screen");
            Assert.That(parsed.startError, Is.Empty, "a started screen reports no error: " + parsed.startError);

            // The run's own answer, and the screen's words for it.
            Assert.That(parsed.outcome, Is.EqualTo(nameof(RunOutcome.Defeat)));
            Assert.That(parsed.finished, Is.True);
            Assert.That(parsed.victory, Is.False);
            Assert.That(parsed.floor, Is.EqualTo(1), "the losing floor is the floor reached");
            Assert.That(parsed.totalFloors, Is.EqualTo(run.TotalFloors));
            Assert.That(parsed.floorsCleared, Is.EqualTo(0));
            Assert.That(parsed.kills, Is.EqualTo(summary.GhostsDefeated));
            Assert.That(parsed.bia, Is.EqualTo(summary.Bia));
            Assert.That(parsed.hp, Is.EqualTo(summary.Hp));
            Assert.That(parsed.maxHp, Is.EqualTo(summary.MaxHp));
            Assert.That(parsed.seed, Is.EqualTo(run.Seed.ToString()), "the seed a bug report needs");

            // Every string is the state's, and the state's is the table's — the report invents nothing.
            Assert.That(parsed.headline, Is.EqualTo(state.Headline.Value));
            Assert.That(parsed.headline, Is.EqualTo(text.Get(ResultKeys.Defeat)));
            Assert.That(parsed.floorLine, Is.EqualTo(state.FloorLine.Value));
            Assert.That(parsed.killsLine, Is.EqualTo(state.KillsLine.Value));
            Assert.That(parsed.biaLine, Is.EqualTo(state.BiaLine.Value));
            Assert.That(parsed.hpLine, Is.EqualTo(state.HpLine.Value));
            Assert.That(parsed.restartLabel, Is.EqualTo(text.Get(ResultKeys.Restart)));
            Assert.That(parsed.titleLabel, Is.EqualTo(text.Get(ResultKeys.BackToTitle)));

            foreach (string line in new[]
                     {
                         parsed.headline, parsed.floorLine, parsed.killsLine, parsed.biaLine, parsed.hpLine,
                         parsed.restartLabel, parsed.titleLabel,
                     })
            {
                Assert.That(string.IsNullOrWhiteSpace(line), Is.False, "the screen shows no text: '" + line + "'");
            }

            // The two controls, by the names the page uses.
            Assert.That(parsed.buttons, Is.Not.Null, "the report names no control: " + json);
            Assert.That(
                Names(parsed), Is.EquivalentTo(new[] { ResultReport.Control.Restart, ResultReport.Control.BackToTitle }),
                "the screen's two ways out are not both in the report");

            foreach (ControlDto control in parsed.buttons)
            {
                Assert.That(control.x, Is.GreaterThan(0f), control.name + " has no x");
                Assert.That(control.y, Is.GreaterThan(0f), control.name + " has no y");

                if (parsed.canvasWidth > 0 && parsed.canvasHeight > 0)
                {
                    Assert.That(
                        control.x, Is.LessThanOrEqualTo(parsed.canvasWidth),
                        control.name + " is off the canvas to the right");
                    Assert.That(
                        control.y, Is.LessThanOrEqualTo(parsed.canvasHeight),
                        control.name + " is off the canvas at the top");
                }
            }

            Assert.That(
                CentreY(parsed, ResultReport.Control.Restart),
                Is.GreaterThan(CentreY(parsed, ResultReport.Control.BackToTitle)),
                "restart is the upper control, which is what the scene draws");
        }

        /// <summary>A won run publishes the victory headline and the whole floor table's count.</summary>
        [Test]
        public void A_won_run_publishes_the_victory_headline()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            ResultView view = OpenResultScene();
            RunSession run = StartedRun(config);

            var flow = new SceneFlow(run, new ScriptedFactory(config, lose: false));
            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Assert.That(flow.EnterBattle().IsSuccess, Is.True, "floor " + floor + " must be enterable");
                DriveFloorToItsEnd(flow);
                Assert.That(flow.SettleBattle().IsSuccess, Is.True, "floor " + floor + " must settle");
            }

            RunSummary summary = run.Summary();
            Assert.That(summary.IsVictory, Is.True, "every floor was cleared");

            var state = new ResultUiState();
            var text = new LocalizationService(config);
            var presenter = new ResultPresenter(run, text, state);
            presenter.Begin();

            string json = ResultReport.Publish(summary, state, view, "Result", string.Empty);

            var parsed = JsonUtility.FromJson<SummaryReportDto>(json);
            Assert.That(parsed, Is.Not.Null, "the summary report is not valid JSON: " + json);
            Assert.That(parsed.outcome, Is.EqualTo(nameof(RunOutcome.Victory)));
            Assert.That(parsed.victory, Is.True);
            Assert.That(parsed.headline, Is.EqualTo(text.Get(ResultKeys.Victory)));
            Assert.That(parsed.headline, Is.Not.EqualTo(text.Get(ResultKeys.Defeat)), "the two endings read alike");
            Assert.That(parsed.floorsCleared, Is.EqualTo(run.TotalFloors));
            Assert.That(parsed.floor, Is.EqualTo(run.TotalFloors));
        }

        // ------------------------------------------------------------------
        // the run, built from the shipped tables
        // ------------------------------------------------------------------

        private static RunSession StartedRun(IGameConfig config)
        {
            var run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), Seed);
            Result started = run.StartRun();
            Assert.That(started.IsSuccess, Is.True, "the run must start on the shipped tables: " + started.Error);
            return run;
        }

        /// <summary>A run that lost floor 1, which is the ending the browser check produces.</summary>
        private static RunSession DefeatedRun(IGameConfig config)
        {
            RunSession run = StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedFactory(config, lose: true));

            Assert.That(flow.EnterBattle().IsSuccess, Is.True);
            DriveFloorToItsEnd(flow);

            Result<FloorSettlement> settled = flow.SettleBattle();
            Assert.That(settled.IsSuccess, Is.True, settled.Error.ToString());
            Assert.That(settled.Value.RunCompleted, Is.True, "a lost floor ends the run");
            Assert.That(settled.Value.IsWin, Is.False);

            return run;
        }

        private static void DriveFloorToItsEnd(SceneFlow flow)
        {
            IBattleSession session = flow.Battle;
            int steps = 0;
            while (session.Phase == BattlePhase.Running && steps++ < 100_000)
            {
                Assert.That(flow.StepBattle(PlayerInputFrame.Idle).IsSuccess, Is.True);
            }

            Assert.That(session.Phase, Is.Not.EqualTo(BattlePhase.Running), "the floor never ended");
        }

        /// <summary>Wins (or loses) a floor on its first ticks, so a fixture is quick and deterministic.</summary>
        private sealed class ScriptedFactory : IBattleSessionFactory
        {
            private readonly FakeBattleSessionFactory _factory;

            internal ScriptedFactory(IGameConfig config, bool lose) =>
                _factory = new FakeBattleSessionFactory(config, new FakeBattleOptions
                {
                    WinAtTick = lose ? 0 : 40,
                    LoseAtTick = lose ? 1 : 0,
                    DummyEntityCount = 4,
                    EntityLifetimeTicks = 8,
                });

            public IBattleSession Create(BattleSetup setup) => _factory.Create(setup);
        }

        // ------------------------------------------------------------------
        // the shipped scene
        // ------------------------------------------------------------------

        private ResultView OpenResultScene()
        {
            Scene loaded = default;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded && candidate.path == ResultScenePath)
                {
                    loaded = candidate;
                }
            }

            if (!loaded.IsValid())
            {
                loaded = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Additive);
            }

            _scene = loaded;
            Assert.That(_scene.IsValid() && _scene.isLoaded, "the Result scene could not be opened");

            ResultView view = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<ResultView>(includeInactive: true);
                if (view is not null)
                {
                    break;
                }
            }

            Assert.That(view, Is.Not.Null, "the Result scene has no ResultView");
            return view;
        }

        private static List<string> Names(SummaryReportDto report)
        {
            var names = new List<string>();
            if (report.buttons is null)
            {
                return names;
            }

            foreach (ControlDto control in report.buttons)
            {
                names.Add(control.name);
            }

            return names;
        }

        private static float CentreY(SummaryReportDto report, string name)
        {
            foreach (ControlDto control in report.buttons)
            {
                if (control.name == name)
                {
                    return control.y;
                }
            }

            Assert.Fail("the report has no '" + name + "' control");
            return 0f;
        }

        // DTOs for JsonUtility: the Unity-side stand-in for the page's JSON.parse.
        [Serializable]
        private sealed class SummaryReportDto
        {
            public string scene;
            public string startError;
            public string outcome;
            public bool finished;
            public bool victory;
            public int floor;
            public int totalFloors;
            public int floorsCleared;
            public int kills;
            public int bia;
            public int hp;
            public int maxHp;
            public string seed;
            public string headline;
            public string floorLine;
            public string killsLine;
            public string biaLine;
            public string hpLine;
            public string restartLabel;
            public string titleLabel;
            public int canvasWidth;
            public int canvasHeight;
            public ControlDto[] buttons;
        }

        [Serializable]
        private sealed class ControlDto
        {
            public string name;
            public float x;
            public float y;
        }
    }
}
