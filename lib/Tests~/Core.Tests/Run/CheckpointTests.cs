using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Save;
using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Run
{
    /// <summary>
    /// Stage 12: a checkpoint round-trips the run, resumes the shop stream and the bag's
    /// merge history, and refuses everything it cannot honestly read.
    /// </summary>
    public sealed class CheckpointTests
    {
        [Fact]
        public void Checkpoint_round_trip_restores_the_same_run_state()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out OfferPair pair);
            RunCheckpoint checkpoint = Capture(run, config);

            var codec = new MessagePackRunCheckpointCodec();
            RunCheckpoint decoded = codec.Decode(codec.Encode(checkpoint));

            var restored = new RunSession(config, new MessagePackBackpackSnapshotCodec(), run.Seed);
            Result result = restored.RestoreCheckpoint(decoded);

            Assert.True(result.IsSuccess, result.ToString());
            Assert.Equal(RunFlowState.Camp, restored.FlowState);
            Assert.Equal(run.ComputeStateHash(), restored.ComputeStateHash());
            Assert.Equal(run.Wallet.Bia, restored.Wallet.Bia);
            Assert.Equal(run.Hp, restored.Hp);
            Assert.Equal(run.Stock.RefreshCount, restored.Stock.RefreshCount);
            Assert.Equal(run.Backpack.MergeCount, restored.Backpack.MergeCount);
            Assert.Single(run.Backpack.Items.Single(item => item.Tier == 2).VariantIds);
        }

        [Fact]
        public void Restored_run_offers_the_same_merge_choices()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out OfferPair pair);
            string[] before = pair.Offer(run);

            RunCheckpoint checkpoint = Capture(run, config);
            var codec = new MessagePackRunCheckpointCodec();
            var restored = new RunSession(config, new MessagePackBackpackSnapshotCodec(), run.Seed);
            Assert.True(restored.RestoreCheckpoint(codec.Decode(codec.Encode(checkpoint))).IsSuccess);

            PieceRef source = new PieceRef(PieceKind.Item, pair.SourceInstance);
            PieceRef target = new PieceRef(PieceKind.Item, pair.TargetInstance);
            Result<MergeOffer> offered = restored.OfferMerge(source, target);

            Assert.True(offered.IsSuccess, offered.ToString());
            Assert.Equal(before, offered.Value.Options.Select(option => option.Id).ToArray());
        }

        [Fact]
        public void Restored_run_gets_the_same_loadout_and_battle_seed()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            RunCheckpoint checkpoint = Capture(run, config);

            var codec = new MessagePackRunCheckpointCodec();
            var restored = new RunSession(config, new MessagePackBackpackSnapshotCodec(), run.Seed);
            Assert.True(restored.RestoreCheckpoint(codec.Decode(codec.Encode(checkpoint))).IsSuccess);

            Result<BattleSetup> first = run.StartFloor();
            Result<BattleSetup> second = restored.StartFloor();

            Assert.True(first.IsSuccess, first.ToString());
            Assert.True(second.IsSuccess, second.ToString());
            Assert.Equal(first.Value.Seed, second.Value.Seed);
            Assert.Equal(first.Value.Loadout.Weapons.Count, second.Value.Loadout.Weapons.Count);
            for (int i = 0; i < first.Value.Loadout.Weapons.Count; i++)
            {
                Assert.Equal(first.Value.Loadout.Weapons[i].Damage, second.Value.Loadout.Weapons[i].Damage);
                Assert.Equal(first.Value.Loadout.Weapons[i].CooldownSeconds, second.Value.Loadout.Weapons[i].CooldownSeconds);
                Assert.Equal(first.Value.Loadout.Weapons[i].InstanceId, second.Value.Loadout.Weapons[i].InstanceId);
            }
        }

        [Fact]
        public void Reload_during_battle_returns_to_the_camp_and_repeats_the_battle_seed()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            int biaBefore = run.Wallet.Bia;
            int settlementBefore = run.LastSettlementId;

            Result<BattleSetup> departure = run.StartFloor();
            Assert.True(departure.IsSuccess, departure.ToString());
            Assert.Equal(RunFlowState.Battle, run.FlowState);

            // A save taken while the floor is running is still a camp checkpoint: no battle
            // result has been accepted, so no HP, bia or reward moved.
            RunCheckpoint checkpoint = Capture(run, config);
            Assert.True(checkpoint.HasNextBattleSeed);
            Assert.Equal(departure.Value.Seed, checkpoint.NextBattleSeed);
            Assert.Equal(biaBefore, checkpoint.Bia);
            Assert.Equal(settlementBefore, checkpoint.LastSettlementId);

            var codec = new MessagePackRunCheckpointCodec();
            var restored = new RunSession(config, new MessagePackBackpackSnapshotCodec(), run.Seed);
            Assert.True(restored.RestoreCheckpoint(codec.Decode(codec.Encode(checkpoint))).IsSuccess);

            Assert.Equal(RunFlowState.Camp, restored.FlowState);
            Assert.Equal(biaBefore, restored.Wallet.Bia);
            Assert.Equal(settlementBefore, restored.LastSettlementId);

            Result<BattleSetup> again = restored.StartFloor();
            Assert.True(again.IsSuccess, again.ToString());
            Assert.Equal(departure.Value.Seed, again.Value.Seed);
        }

        [Fact]
        public void Restored_shop_continues_the_same_rolls()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            RunCheckpoint checkpoint = Capture(run, config);

            var codec = new MessagePackRunCheckpointCodec();
            var restored = new RunSession(config, new MessagePackBackpackSnapshotCodec(), run.Seed);
            Assert.True(restored.RestoreCheckpoint(codec.Decode(codec.Encode(checkpoint))).IsSuccess);

            Result<RefreshOutcome> first = run.RefreshShop();
            Result<RefreshOutcome> second = restored.RefreshShop();

            Assert.True(first.IsSuccess, first.ToString());
            Assert.True(second.IsSuccess, second.ToString());
            Assert.Equal(first.Value.Filled, second.Value.Filled);
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                Assert.Equal(run.Stock[i].OfferedId, restored.Stock[i].OfferedId);
                Assert.Equal(run.Stock[i].Price, restored.Stock[i].Price);
            }
        }

        [Fact]
        public void Checkpoint_dto_carries_no_battle_session_or_ecs_state()
        {
            Type[] types =
            {
                typeof(ProjectSurvive.Core.Contracts.Save.RunCheckpointDto),
                typeof(ProjectSurvive.Core.Contracts.Save.ShopSlotDto),
                typeof(ProjectSurvive.Core.Contracts.Save.RandomStreamStateDto),
                typeof(ProjectSurvive.Core.Contracts.Save.BackpackSnapshotDto),
                typeof(ProjectSurvive.Core.Contracts.Save.BackpackPieceDto),
            };

            foreach (Type type in types)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    AssertClean(property.PropertyType);
                }
            }
        }

        [Fact]
        public async Task Older_and_newer_schema_versions_are_reported_without_being_readAsync()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            var store = new InMemoryCheckpointStore();
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);

            RunCheckpoint checkpoint = Capture(run, config);

            checkpoint.SchemaVersion = RunCheckpointFormat.SchemaVersion + 1;
            Assert.Equal(CheckpointError.SchemaTooNew, (await LoadWith(service, store, checkpoint)).ErrorCode);

            checkpoint.SchemaVersion = RunCheckpointFormat.MinimumSchemaVersion - 1;
            Assert.Equal(CheckpointError.SchemaTooOld, (await LoadWith(service, store, checkpoint)).ErrorCode);
        }

        [Fact]
        public async Task Config_mismatch_is_reported_and_nothing_is_restoredAsync()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            var store = new InMemoryCheckpointStore();
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);

            RunCheckpoint checkpoint = Capture(run, config);
            checkpoint.ConfigHash = "0000000000000000000000000000000000000000000000000000000000000000";
            store.Put(CheckpointService.CheckpointKey, new MessagePackRunCheckpointCodec().Encode(checkpoint));

            CheckpointLoad loaded = await service.LoadAsync();
            Assert.False(loaded.IsSuccess);
            Assert.Equal(CheckpointError.ConfigMismatch, loaded.ErrorCode);
        }

        [Fact]
        public async Task Corrupt_payload_falls_back_to_the_backupAsync()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            var store = new InMemoryCheckpointStore();
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);

            Assert.True((await service.SaveAsync(run)).IsSuccess);
            Assert.True((await service.SaveAsync(run)).IsSuccess);

            store.CorruptPrimary(CheckpointService.CheckpointKey);

            CheckpointLoad loaded = await service.LoadAsync();
            Assert.True(loaded.IsSuccess, loaded.ErrorCode);
            Assert.True(loaded.RecoveredFromBackup);
        }

        [Fact]
        public async Task Corrupt_payload_without_a_backup_is_reported_as_corruptAsync()
        {
            IGameConfig config = RunFixture.Config();
            var store = new InMemoryCheckpointStore();
            store.Put(CheckpointService.CheckpointKey, new byte[] { 0xC1, 0x00, 0x01 });
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);

            CheckpointLoad loaded = await service.LoadAsync();
            Assert.False(loaded.IsSuccess);
            Assert.Equal(CheckpointError.Corrupt, loaded.ErrorCode);
        }

        [Fact]
        public async Task Missing_payload_is_not_an_errorAsync()
        {
            IGameConfig config = RunFixture.Config();
            var service = new CheckpointService(
                config, new MessagePackRunCheckpointCodec(), new InMemoryCheckpointStore());

            CheckpointLoad loaded = await service.LoadAsync();
            Assert.False(loaded.IsSuccess);
            Assert.Equal(CheckpointError.NotFound, loaded.ErrorCode);
        }

        [Fact]
        public async Task Storage_that_is_unavailable_reports_failure_and_never_savesAsync()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            var store = new InMemoryCheckpointStore { IsAvailable = false, UnavailableReason = "IndexedDB blocked" };
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);

            CheckpointWrite saved = await service.SaveAsync(run);
            CheckpointLoad loaded = await service.LoadAsync();

            Assert.False(saved.IsSuccess);
            Assert.Equal(CheckpointError.StorageUnavailable, saved.ErrorCode);
            Assert.False(loaded.IsSuccess);
            Assert.Equal(CheckpointError.StorageUnavailable, loaded.ErrorCode);
            Assert.Empty(store.Keys);
        }

        [Fact]
        public async Task Concurrent_saves_let_the_newest_request_winAsync()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = PreparedRun(config, out _);
            var store = new InMemoryCheckpointStore { BlockWrites = true };
            var service = new CheckpointService(config, new MessagePackRunCheckpointCodec(), store);

            Task<CheckpointWrite> older = service.SaveAsync(run);
            Assert.True(await store.WaitForWriteStartAsync(TimeSpan.FromSeconds(5)));

            // Advance the run while the first save is in flight.
            int beforeRefresh = run.Stock.RefreshCount;
            Assert.True(run.RefreshShop().IsSuccess);

            Task<CheckpointWrite> newer = service.SaveAsync(run);
            store.ReleaseWrites();

            await Task.WhenAll(older, newer).WaitAsync(TimeSpan.FromSeconds(10));
            CheckpointWrite newerResult = await newer;
            Assert.True(newerResult.IsSuccess, newerResult.ErrorCode);

            CheckpointLoad loaded = await service.LoadAsync();
            Assert.True(loaded.IsSuccess, loaded.ErrorCode);
            Assert.NotEqual(beforeRefresh, loaded.Checkpoint!.RefreshCount);
            Assert.Equal(run.Stock.RefreshCount, loaded.Checkpoint.RefreshCount);
        }

        [Fact]
        public void Settings_round_trip_and_invalid_values_are_refused()
        {
            var codec = new MessagePackSettingsCodec();
            var settings = new GameSettings();
            settings.SetVolume(0.35f);
            settings.SetLanguage(Language.En);
            settings.SetDefaultBattleSpeed(2f);

            GameSettings decoded = codec.Decode(codec.Encode(settings));

            Assert.Equal(0.35f, decoded.MasterVolume, 4);
            Assert.Equal(Language.En, decoded.Language);
            Assert.Equal(2f, decoded.DefaultBattleSpeed);
            Assert.True(decoded.IsValid);

            settings.SetVolume(5f);
            Assert.Equal(GameSettings.MaxVolume, settings.MasterVolume);
            settings.SetDefaultBattleSpeed(3f);
            Assert.Equal(2f, settings.DefaultBattleSpeed);
        }

        private static async Task<CheckpointLoad> LoadWith(
            CheckpointService service, InMemoryCheckpointStore store, RunCheckpoint checkpoint)
        {
            store.Put(CheckpointService.CheckpointKey, new MessagePackRunCheckpointCodec().Encode(checkpoint));
            return await service.LoadAsync();
        }

        private static RunCheckpoint Capture(RunSession run, IGameConfig config)
        {
            RunCheckpoint checkpoint = run.CaptureCheckpoint();
            checkpoint.ConfigHash = config.ConfigHash;
            checkpoint.ConfigSchemaVersion = config.SchemaVersion;
            return checkpoint;
        }

        /// <summary>
        /// A camp with a merged tier-2 weapon (so a variant id and a merge count are in the
        /// save) and a second unmerged pair for the offer comparison.
        /// </summary>
        private static RunSession PreparedRun(IGameConfig config, out OfferPair pair)
        {
            RunSession run = RunFixture.StartedRun(config);

            PieceRef a = AddItem(run, "item.takrut_tone", 1, 0);
            PieceRef b = AddItem(run, "item.takrut_tone", 2, 0);
            PieceRef c = AddItem(run, "item.takrut_tone", 0, 1);
            PieceRef d = AddItem(run, "item.takrut_tone", 1, 1);

            Result<MergeOffer> offered = run.OfferMerge(a, b);
            Assert.True(offered.IsSuccess, offered.ToString());
            if (offered.Value.HasChoices)
            {
                Assert.True(run.ConfirmMerge(a, b, offered.Value.Options[0].Id).IsSuccess);
            }
            else
            {
                Assert.True(run.MergeItems(a, b).IsSuccess);
            }

            // Advance the shop stream once, so a restored stream that restarted from its
            // seed would immediately disagree.
            Assert.True(run.RefreshShop().IsSuccess);

            pair = new OfferPair(c.InstanceId, d.InstanceId);
            return run;
        }

        private static PieceRef AddItem(RunSession run, string id, int x, int y)
        {
            Result<PieceRef> added = run.Backpack.AddItem(id);
            Assert.True(added.IsSuccess, added.ToString());
            Assert.True(run.MovePiece(added.Value, new CellOffset(x, y), Rotation.Degrees0).IsSuccess);
            return added.Value;
        }

        private static void AssertClean(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
            {
                return;
            }

            string name = type.FullName ?? type.Name;
            Assert.DoesNotContain("UnityEngine", name);
            Assert.DoesNotContain("Ecs", name);
            Assert.DoesNotContain("IBattleSession", name);
            Assert.DoesNotContain("BattleSession", name);

            if (type.IsArray)
            {
                AssertClean(type.GetElementType()!);
                return;
            }

            foreach (Type argument in type.GetGenericArguments())
            {
                AssertClean(argument);
            }
        }

        /// <summary>The two instance ids the post-merge offer is asked about.</summary>
        private readonly struct OfferPair
        {
            public OfferPair(int source, int target)
            {
                SourceInstance = source;
                TargetInstance = target;
            }

            public int SourceInstance { get; }

            public int TargetInstance { get; }

            public string[] Offer(RunSession run)
            {
                var source = new PieceRef(PieceKind.Item, SourceInstance);
                var target = new PieceRef(PieceKind.Item, TargetInstance);
                Result<MergeOffer> offered = run.OfferMerge(source, target);
                Assert.True(offered.IsSuccess, offered.ToString());
                return offered.Value.Options.Select(option => option.Id).ToArray();
            }
        }
    }

    /// <summary>
    /// An in-memory store with the same backup rotation as the file adapter, plus the
    /// failure and blocking hooks the concurrency tests need.
    /// </summary>
    internal sealed class InMemoryCheckpointStore : ICheckpointStore
    {
        private readonly Dictionary<string, byte[]> _files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private readonly object _gate = new object();
        private readonly TaskCompletionSource<bool> _writeStarted = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> _release = new TaskCompletionSource<bool>();

        public bool IsAvailable { get; set; } = true;

        public string UnavailableReason { get; set; } = string.Empty;

        public bool BlockWrites { get; set; }

        public IReadOnlyCollection<string> Keys
        {
            get
            {
                lock (_gate)
                {
                    return _files.Keys.ToArray();
                }
            }
        }

        public Task<CheckpointStoreRead> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return Task.FromResult(CheckpointStoreRead.Failed(CheckpointError.StorageUnavailable));
            }

            lock (_gate)
            {
                return Task.FromResult(_files.TryGetValue(key, out byte[]? bytes)
                    ? CheckpointStoreRead.Ok(bytes)
                    : CheckpointStoreRead.NotFound());
            }
        }

        public async Task<CheckpointStoreWrite> WriteAsync(
            string key, byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return CheckpointStoreWrite.Failed(CheckpointError.StorageUnavailable);
            }

            if (BlockWrites)
            {
                _writeStarted.TrySetResult(true);
                await _release.Task.ConfigureAwait(false);
            }

            lock (_gate)
            {
                if (_files.TryGetValue(key, out byte[]? previous))
                {
                    _files[key + ".bak"] = previous;
                }

                _files[key] = bytes;
            }

            return CheckpointStoreWrite.Ok();
        }

        public Task<CheckpointStoreWrite> DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _files.Remove(key);
                _files.Remove(key + ".bak");
            }

            return Task.FromResult(CheckpointStoreWrite.Ok());
        }

        public Task<CheckpointStoreWrite> FlushAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CheckpointStoreWrite.Ok());

        /// <summary>Seeds a key without rotating the backup.</summary>
        public void Put(string key, byte[] bytes)
        {
            lock (_gate)
            {
                _files[key] = bytes;
            }
        }

        /// <summary>Makes the primary unreadable while leaving its backup intact.</summary>
        public void CorruptPrimary(string key)
        {
            lock (_gate)
            {
                _files[key] = new byte[] { 0xC1, 0x01 };
            }
        }

        public async Task<bool> WaitForWriteStartAsync(TimeSpan timeout)
        {
            Task finished = await Task.WhenAny(_writeStarted.Task, Task.Delay(timeout));
            return finished == _writeStarted.Task;
        }

        public void ReleaseWrites() => _release.TrySetResult(true);
    }
}
