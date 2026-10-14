using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Run
{
    /// <summary>
    /// Stage 06 acceptance: \"scripted run 5 ชั้น ... จบที่ Result และ replay จาก log ได้
    /// RunState hash เดิม\", and \"replay ชั้นด้วย BattleReplay แล้วได้ ComputeStateHash เดิม\".
    /// </summary>
    public sealed class ReplayTests
    {
        [Fact]
        public void A_full_run_replays_from_its_log_to_the_same_state_hash()
        {
            IGameConfig config = RunFixture.Config();
            var codec = new MessagePackBackpackSnapshotCodec();
            var factory = new ScriptedBattleSessionFactory(config);
            var run = new RunSession(config, codec, 31337u);
            var flow = new SceneFlow(run, factory);

            Assert.True(run.StartRun().IsSuccess);

            // Camp work, so the log is not just "start, five floors": pin one offer, rent
            // another, lay it out, turn it and re-roll.
            int rentSlot = RunFixture.CheapestAvailableSlot(run);
            int pinSlot = (rentSlot + 1) % run.Stock.SlotCount;
            Assert.True(run.SetSlotLocked(pinSlot, true).IsSuccess);

            Result<PurchaseOutcome> rent = run.Purchase(rentSlot);
            Assert.True(rent.IsSuccess, rent.Error.ToString());
            PieceRef rented = rent.Value.Piece;

            Assert.True(run.MovePiece(rented, new CellOffset(1, 0), Rotation.Degrees0).IsSuccess);
            Assert.True(run.RotatePiece(rented).IsSuccess);
            Assert.True(run.RefreshShop().IsSuccess);

            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Result<IBattleSession> started = flow.EnterBattle();
                Assert.True(started.IsSuccess, started.Error.ToString());
                Assert.True(flow.RunFloorToEnd().IsSuccess);
            }

            Assert.Equal(RunFlowState.Result, run.FlowState);

            // Six camp commands plus one departure per floor. A settlement is not a
            // command: the floor's StartFloor plus its recording carry it.
            Assert.Equal(6 + run.TotalFloors, run.Log.CommandCount);
            Assert.Equal(run.TotalFloors, run.Log.BattleCount);

            RunSession replayed = run.Log.Replay(config, codec, factory);

            Assert.Equal(run.ComputeStateHash(), replayed.ComputeStateHash());
            Assert.Equal(run.FloorIndex, replayed.FloorIndex);
            Assert.Equal(run.Hp, replayed.Hp);
            Assert.Equal(run.Wallet.Bia, replayed.Wallet.Bia);
            Assert.Equal(run.FloorsCleared, replayed.FloorsCleared);
            Assert.Equal(run.LastSettlementId, replayed.LastSettlementId);
            Assert.Equal(RunFlowState.Result, replayed.FlowState);
            Assert.Equal(RunFixture.StockIds(run), RunFixture.StockIds(replayed));

            // The replay is a run in its own right, not a share of the original's state.
            Assert.NotSame(run.Backpack, replayed.Backpack);
            Assert.Equal(run.Backpack.Items.Count, replayed.Backpack.Items.Count);
        }

        [Fact]
        public void A_defeated_run_also_replays_to_the_same_state_hash()
        {
            IGameConfig config = RunFixture.Config();
            var codec = new MessagePackBackpackSnapshotCodec();
            var factory = new ScriptedBattleSessionFactory(
                config,
                normal: ScriptedBattleSessionFactory.WinOnFirstTick(),
                boss: ScriptedBattleSessionFactory.TimeoutOnFirstTick());

            var run = new RunSession(config, codec, 8u);
            var flow = new SceneFlow(run, factory);
            Assert.True(run.StartRun().IsSuccess);

            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Assert.True(flow.EnterBattle().IsSuccess, "floor " + floor + " should be startable");
                Assert.True(flow.RunFloorToEnd().IsSuccess);
            }

            Assert.Equal(RunFlowState.Result, run.FlowState);
            Assert.True(run.FloorsCleared < run.TotalFloors);

            RunSession replayed = run.Log.Replay(config, codec, factory);

            Assert.Equal(run.ComputeStateHash(), replayed.ComputeStateHash());
            Assert.Equal(run.FloorsCleared, replayed.FloorsCleared);
        }

        [Fact]
        public void The_run_state_hash_moves_when_the_state_moves()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            uint start = run.ComputeStateHash();
            Assert.Equal(start, run.ComputeStateHash());

            Assert.True(run.RefreshShop().IsSuccess);
            uint afterRefresh = run.ComputeStateHash();
            Assert.NotEqual(start, afterRefresh);

            Assert.True(run.SetSlotLocked(1, true).IsSuccess);
            Assert.NotEqual(afterRefresh, run.ComputeStateHash());

            // Each re-roll in a camp costs two bia more than the last (5, 7, 9, ...), so
            // the third one outruns the starting purse.
            Assert.True(run.RefreshShop().IsSuccess);
            uint afterSecondRefresh = run.ComputeStateHash();
            Assert.NotEqual(afterRefresh, afterSecondRefresh);
            Assert.True(
                run.Wallet.Bia < run.Stock.RefreshCost(config.Economy),
                "the purse should be short of the next re-roll by now");

            // A refused command is not a state change.
            Assert.True(run.RefreshShop().IsFailure);
            Assert.Equal(afterSecondRefresh, run.ComputeStateHash());
        }

        [Fact]
        public void A_recorded_floor_replays_through_the_factory_to_the_same_battle_hash()
        {
            IGameConfig config = RunFixture.Config();
            var factory = new ScriptedBattleSessionFactory(
                config,
                normal: new FakeBattleOptions
                {
                    WinAtTick = 40,
                    DamagePerSecond = 10,
                    DummyEntityCount = 8,
                    EntityLifetimeTicks = 5,
                });

            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, factory);

            Assert.True(flow.EnterBattle().IsSuccess);
            while (flow.Battle!.Phase == BattlePhase.Running)
            {
                Assert.True(flow.StepBattle(PlayerInputFrame.FromAxis(1f, 0f)).IsSuccess);
            }

            uint live = flow.Battle.ComputeStateHash();
            BattleReplay? replay = flow.CurrentBattleReplay;
            Assert.NotNull(replay);

            Assert.Equal(live, replay!.ReplayHash(factory));

            // And it is the recording the run log kept, so the run-level replay uses it.
            Assert.True(flow.SettleBattle().IsSuccess);
            Assert.Single(run.Log.Battles);
            Assert.Equal(live, run.Log.Battles[0].ReplayHash(factory));
        }

        [Fact]
        public void The_log_starts_empty_and_records_only_accepted_commands()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.NewRun(config, 4u);

            Assert.Equal(0, run.Log.CommandCount);
            Assert.Equal(0, run.Log.BattleCount);
            Assert.Equal(4u, run.Log.Seed);

            // Refused: no run yet.
            Assert.True(run.Purchase(0).IsFailure);
            Assert.Equal(0, run.Log.CommandCount);

            Assert.True(run.StartRun().IsSuccess);
            Assert.Equal(1, run.Log.CommandCount);
            Assert.Equal(RunCommandKind.StartRun, run.Log.Commands[0].Kind);

            // Refused: the slot index is out of range.
            Assert.True(run.Purchase(run.Stock.SlotCount).IsFailure);
            Assert.Equal(1, run.Log.CommandCount);

            PieceRef weapon = RunFixture.StartingWeapon(run);
            Assert.True(run.Sell(weapon).IsSuccess);
            Assert.Equal(2, run.Log.CommandCount);
            Assert.Equal(RunCommandKind.Sell, run.Log.Commands[1].Kind);
            Assert.Equal(weapon, run.Log.Commands[1].Piece);
        }

        [Fact]
        public void Replay_requires_its_inputs()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var factory = new ScriptedBattleSessionFactory(config);
            var codec = new MessagePackBackpackSnapshotCodec();

            Assert.Throws<System.ArgumentNullException>(() => run.Log.Replay(null!, codec, factory));
            Assert.Throws<System.ArgumentNullException>(() => run.Log.Replay(config, null!, factory));
            Assert.Throws<System.ArgumentNullException>(() => run.Log.Replay(config, codec, null!));
        }
    }
}
