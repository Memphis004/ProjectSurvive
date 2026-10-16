using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Core.Simulation.Battle;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 10c's dev cheats, and the rule that keeps them out of a release build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The DevMenu is a development tool, and "ให้ DevCheatsAllowed=false ใน release (มีเทสต์ยืนยัน)"
    /// asks for the gate to be <i>proved</i> rather than asserted in prose. Three links are checked
    /// here, each on its own: the decision (<see cref="DevCheats.Allows"/> — true only for a
    /// development build or the Editor, and false by default), the per-floor freeze the run copies
    /// into <c>BattleSetup.DevCheatsAllowed</c> when it departs, and the floor itself, which refuses
    /// a dev command it was not told to accept.
    /// </para>
    /// <para>
    /// <see cref="DevCheats.Allowed"/> is process-wide (the shell sets it once at startup), so every
    /// test here starts from <see cref="DevCheats.Reset"/> and puts it back afterwards: a play session
    /// in the Editor leaves it on, and a test that inherited that would be testing the wrong build.
    /// </para>
    /// </remarks>
    public sealed class DevCheatTests
    {
        private const uint Seed = 20261014u;

        private IGameConfig _config;
        private IBackpackSnapshotCodec _codec;

        [SetUp]
        public void SetUp()
        {
            DevCheats.Reset();
            _config = ResourcesGameConfigLoader.Load();
            _codec = new MessagePackBackpackSnapshotCodec();
        }

        [TearDown]
        public void TearDown() => DevCheats.Reset();

        [Test]
        public void A_release_build_does_not_allow_dev_cheats()
        {
            Assert.That(DevCheats.AllowedInRelease, Is.False, "the release answer is compiled in");
            Assert.That(DevCheats.Allows(isDevelopmentBuild: false, isEditor: false), Is.False,
                "a release player build is the one build that must not be cheatable");
            Assert.That(DevCheats.Allowed, Is.False,
                "before the shell decides anything, the answer is the release one");
        }

        [Test]
        public void A_development_build_and_the_editor_do_allow_them()
        {
            Assert.That(DevCheats.Allows(isDevelopmentBuild: true, isEditor: false), Is.True);
            Assert.That(DevCheats.Allows(isDevelopmentBuild: false, isEditor: true), Is.True,
                "the Editor is where the cheats are meant to be used");

            DevCheats.Configure(isDevelopmentBuild: true, isEditor: false);
            Assert.That(DevCheats.Allowed, Is.True);

            DevCheats.Reset();
            Assert.That(DevCheats.Allowed, Is.False);
        }

        [Test]
        public void The_menu_is_only_visible_where_cheats_are_allowed()
        {
            var menu = new DevMenuPresenter(_config);

            menu.Begin();
            Assert.That(menu.Visible.Value, Is.False, "a release build shows no dev menu");

            DevCheats.Configure(isDevelopmentBuild: true, isEditor: false);
            menu.Begin();
            Assert.That(menu.Visible.Value, Is.True);
            Assert.That(menu.Status.Value, Is.Not.EqualTo(DevMenuText.Disabled), "the status line says it is on");

            DevCheats.Reset();
            menu.Begin();
            Assert.That(menu.Visible.Value, Is.False);
            Assert.That(menu.Status.Value, Is.EqualTo(DevMenuText.Disabled));

            menu.Dispose();
        }

        [Test]
        public void Every_control_sends_a_battle_command_and_nothing_else()
        {
            var menu = new DevMenuPresenter(_config);

            BattleCommand god = menu.NextGodMode();
            Assert.That(god.Kind, Is.EqualTo(BattleCommandKind.DevGodMode));
            Assert.That(god.Arg, Is.EqualTo(1), "the first press turns it on");
            Assert.That(god.IsDevCommand, Is.True, "it is behind the dev gate, not the surrender one");

            Assert.That(menu.NextGodMode().Arg, Is.EqualTo(1), "a press that was never accepted asks again");
            menu.NoteResult(BattleCommandKind.DevGodMode, CommandResult.Ok);
            Assert.That(menu.GodMode.Value, Is.True);
            Assert.That(menu.NextGodMode().Arg, Is.EqualTo(0), "the next press turns it off");

            Assert.That(menu.AddBia().Kind, Is.EqualTo(BattleCommandKind.DevAddBia));
            Assert.That(menu.AddBia().Arg, Is.EqualTo(_config.Economy.StartBia),
                "every balance number comes from data/, including the one only a developer sees");
            Assert.That(menu.SpawnGhost().Kind, Is.EqualTo(BattleCommandKind.DevSpawnGhost));
            Assert.That(menu.KillAll().Kind, Is.EqualTo(BattleCommandKind.DevKillAll));
            Assert.That(menu.WinNow().Kind, Is.EqualTo(BattleCommandKind.DevWinNow));
            Assert.That(menu.LoseNow().Kind, Is.EqualTo(BattleCommandKind.DevLoseNow));

            // Surrender is the one command that is not a dev command, so the gate below cannot
            // accidentally lock a player out of giving up.
            Assert.That(BattleCommand.Surrender().IsDevCommand, Is.False);

            menu.Dispose();
        }

        [Test]
        public void A_refused_god_mode_leaves_the_label_telling_the_truth()
        {
            var menu = new DevMenuPresenter(_config);
            menu.Begin();

            // Press, then accept: the toggle describes what the floor is really doing.
            menu.NoteResult(menu.NextGodMode().Kind, CommandResult.Ok);
            Assert.That(menu.GodMode.Value, Is.True);

            // Now press again — which asks for the opposite — and have the floor refuse it, the way a
            // floor that forbids cheats answers. The label must not follow the press.
            Assert.That(menu.NextGodMode().Arg, Is.EqualTo(0), "the second press asks for off");
            menu.NoteResult(BattleCommandKind.DevGodMode, CommandResult.RejectedNotAllowed);
            Assert.That(menu.GodMode.Value, Is.True, "a refused command changes nothing");
            Assert.That(menu.Status.Value, Does.Contain(CommandResult.RejectedNotAllowed.ToString()));

            menu.Dispose();
        }

        [Test]
        public void The_floor_freezes_what_the_shell_decided_when_the_run_departs()
        {
            RunSession run = StartedRun();

            DevCheats.Reset();
            Result<BattleSetup> refused = run.StartFloor();

            Assert.That(refused.IsSuccess, Is.True, refused.Error.ToString());
            Assert.That(refused.Value.DevCheatsAllowed, Is.False,
                "a run that departed while cheats were off must not be cheatable afterwards");

            DevCheats.Configure(isDevelopmentBuild: true, isEditor: false);
            Assert.That(refused.Value.DevCheatsAllowed, Is.False,
                "the floor's answer is frozen at departure, not read from the shell every tick");
        }

        [Test]
        public void A_floor_that_forbids_cheats_refuses_every_dev_command()
        {
            RunSession run = StartedRun();
            DevCheats.Reset();

            Result<BattleSetup> departed = run.StartFloor();
            Assert.That(departed.IsSuccess, Is.True, departed.Error.ToString());

            // The real battle, not the fake: the refusal has to be the shipped simulation's.
            IBattleSession session = new EcsBattleSessionFactory(_config).Create(departed.Value);

            BattleCommand[] cheats =
            {
                BattleCommand.DevKillAll(),
                BattleCommand.DevGodMode(true),
                BattleCommand.DevWinNow(),
                BattleCommand.DevLoseNow(),
                BattleCommand.DevAddBia(_config.Economy.StartBia),
                BattleCommand.DevSpawnGhost(),
            };

            foreach (BattleCommand cheat in cheats)
            {
                Assert.That(session.Submit(cheat), Is.EqualTo(CommandResult.RejectedNotAllowed),
                    cheat.Kind + " must be refused by a floor that was not told to accept it");
            }

            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Running),
                "no refused command may have moved the floor");
            Assert.That(session.Submit(BattleCommand.Surrender()), Is.EqualTo(CommandResult.Ok),
                "surrender is not a cheat and stays available");
            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Aborted));
        }

        [Test]
        public void The_same_commands_are_accepted_when_the_shell_allowed_them()
        {
            DevCheats.Configure(isDevelopmentBuild: true, isEditor: false);

            RunSession run = StartedRun();
            Result<BattleSetup> departed = run.StartFloor();
            Assert.That(departed.IsSuccess, Is.True, departed.Error.ToString());
            Assert.That(departed.Value.DevCheatsAllowed, Is.True);

            IBattleSession session = new EcsBattleSessionFactory(_config).Create(departed.Value);

            Assert.That(session.Submit(BattleCommand.DevGodMode(true)), Is.EqualTo(CommandResult.Ok));
            Assert.That(session.Submit(BattleCommand.DevSpawnGhost()), Is.EqualTo(CommandResult.Ok),
                "the shipped ghost spawn accepts the command on a floor that allows it");
            Assert.That(session.Submit(BattleCommand.DevWinNow()), Is.EqualTo(CommandResult.Ok));
            Assert.That(session.Phase, Is.EqualTo(BattlePhase.Won), "the skip-floor control wins the floor");
        }

        /// <summary>A run that has started and is sitting in the first Camp.</summary>
        private RunSession StartedRun()
        {
            var run = new RunSession(_config, _codec, Seed);
            Result started = run.StartRun();
            Assert.That(started.IsSuccess, Is.True, "the run must start on the shipped tables: " + started.Error);
            return run;
        }
    }
}
