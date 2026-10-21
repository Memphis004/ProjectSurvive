using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Save;
using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Config;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 12 at the shell boundary: the file store the game uses, the generated formatter,
    /// and the settings presenter's immediate language switch.
    /// </summary>
    public sealed class PersistenceEditModeTests
    {
        private string _root = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(
                Path.GetTempPath(), "ProjectSurviveTests", "persistence-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        [Test]
        public void The_file_store_rotates_a_backup_and_deletes_both()
        {
            var store = new FileCheckpointStore(_root);
            Assert.IsTrue(store.IsAvailable, store.UnavailableReason);

            Assert.IsTrue(store.WriteAsync("run-checkpoint", new byte[] { 1, 2, 3 }).GetAwaiter().GetResult().IsSuccess);
            Assert.IsTrue(store.WriteAsync("run-checkpoint", new byte[] { 4, 5 }).GetAwaiter().GetResult().IsSuccess);

            CheckpointStoreRead primary = store.ReadAsync("run-checkpoint").GetAwaiter().GetResult();
            CheckpointStoreRead backup = store.ReadAsync("run-checkpoint.bak").GetAwaiter().GetResult();
            CollectionAssert.AreEqual(new byte[] { 4, 5 }, primary.Bytes);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, backup.Bytes);

            Assert.IsTrue(store.DeleteAsync("run-checkpoint").GetAwaiter().GetResult().IsSuccess);
            Assert.IsFalse(store.ReadAsync("run-checkpoint").GetAwaiter().GetResult().Found);
            Assert.IsFalse(store.ReadAsync("run-checkpoint.bak").GetAwaiter().GetResult().Found);
        }

        [Test]
        public void The_checkpoint_round_trips_through_the_generated_formatter()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            Assert.IsTrue(MessagePackRunCheckpointCodec.GeneratedFormatterIsPresent);
            Assert.IsTrue(MessagePackSettingsCodec.GeneratedFormatterIsPresent);

            var store = new FileCheckpointStore(_root);
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);
            RunSession run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), 20261021u);
            Assert.IsTrue(run.StartRun().IsSuccess);

            CheckpointWrite written = service.SaveAsync(run).GetAwaiter().GetResult();
            Assert.IsTrue(written.IsSuccess, written.ErrorCode);

            CheckpointLoad loaded = service.LoadAsync().GetAwaiter().GetResult();
            Assert.IsTrue(loaded.IsSuccess, loaded.ErrorCode);
            Assert.AreEqual(config.ConfigHash, loaded.Checkpoint.ConfigHash);
            Assert.AreEqual(run.RunId, loaded.Checkpoint.RunId);

            var restored = new RunSession(config, new MessagePackBackpackSnapshotCodec(), run.Seed);
            Assert.IsTrue(restored.RestoreCheckpoint(loaded.Checkpoint).IsSuccess);
            Assert.AreEqual(run.ComputeStateHash(), restored.ComputeStateHash());
        }

        [Test]
        public void A_finished_run_deletes_its_checkpoint()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            var store = new FileCheckpointStore(_root);
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);
            RunSession run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), 7u);
            Assert.IsTrue(run.StartRun().IsSuccess);
            Assert.IsTrue(service.SaveAsync(run).GetAwaiter().GetResult().IsSuccess);

            // A loss on the first floor ends the run; the service must delete, not store, it.
            Assert.IsTrue(run.StartFloor().IsSuccess);
            Assert.IsTrue(run.ApplyBattleResult(new ProjectSurvive.Core.Domain.Combat.Battle.BattleResult
            {
                Outcome = ProjectSurvive.Core.Domain.Combat.Battle.BattlePhase.Lost,
                EndReason = ProjectSurvive.Core.Domain.Combat.Battle.BattleEndReason.PlayerDied,
                FinalHp = 0,
                Kills = 0,
                CurrencyEarned = 0,
                SurvivedTicks = 1,
            }).IsSuccess);
            Assert.IsTrue(run.IsRunOver);

            Assert.IsTrue(service.SaveAsync(run).GetAwaiter().GetResult().IsSuccess);
            Assert.AreEqual(CheckpointError.NotFound, service.LoadAsync().GetAwaiter().GetResult().ErrorCode);
        }

        [Test]
        public void Settings_change_language_and_volume_immediately_and_persist()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            var store = new FileCheckpointStore(_root);
            var checkpoints = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);
            var settings = new SettingsService(store, new MessagePackSettingsCodec());
            var ui = new UiState();
            var text = new LocalizationService(config);
            var save = new GameSave(checkpoints, settings, ui, text);
            var presenter = new SettingsPresenter(settings, ui, text, save);

            try
            {
                presenter.ToggleLanguage();
                Assert.AreEqual(Language.En, ui.Language.Value);
                Assert.AreEqual(Language.En, text.Language);
                StringAssert.Contains("Volume", ui.VolumeLabel.Value);

                presenter.AdjustVolume(-10f);
                Assert.AreEqual(GameSettings.MinVolume, ui.MasterVolume.Value, 0.0001f);

                presenter.ToggleSpeed();
                Assert.AreEqual(2f, ui.DefaultBattleSpeed.Value);
            }
            finally
            {
                presenter.Dispose();
                ui.Dispose();
            }

            Assert.IsTrue(settings.SaveAsync(CancellationToken.None).GetAwaiter().GetResult().IsSuccess);

            // A fresh service reads what was written.
            var reloaded = new SettingsService(store, new MessagePackSettingsCodec());
            SettingsLoad load = reloaded.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsTrue(load.IsSuccess, load.ErrorCode);
            Assert.AreEqual(Language.En, load.Settings.Language);
            Assert.AreEqual(2f, load.Settings.DefaultBattleSpeed);
        }

        [Test]
        public void Unavailable_storage_reports_failure_instead_of_success()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();
            var store = new FileCheckpointStore(Path.Combine(_root, "blocked-file"));
            Assert.IsTrue(store.IsAvailable);
            string file = Path.Combine(Path.GetTempPath(), "projectsurvive-store-block-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(file, "x");

            try
            {
                // A regular file where the directory should be makes the store unavailable.
                var blocked = new FileCheckpointStore(file);
                var checkpoints = new CheckpointService(
                    config, new MessagePackRunCheckpointCodec(), blocked);
                var run = new RunSession(config, new MessagePackBackpackSnapshotCodec(), 1u);
                Assert.IsTrue(run.StartRun().IsSuccess);

                CheckpointWrite written = checkpoints.SaveAsync(run).GetAwaiter().GetResult();
                Assert.IsFalse(written.IsSuccess);
                Assert.AreEqual(CheckpointError.StorageUnavailable, written.ErrorCode);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
