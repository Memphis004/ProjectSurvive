using System.Collections.Generic;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Run
{
    /// <summary>
    /// Stage 06 tasks 1, 4–6: the flow state machine, the departure gate, the bag lock
    /// during a battle, and the one-shot floor settlement.
    /// </summary>
    public sealed class RunFlowTests
    {
        [Fact]
        public void The_flow_phase_picks_the_scene()
        {
            Assert.Equal(SceneId.Title, SceneFlowMap.SceneOf(RunFlowState.Title));
            Assert.Equal(SceneId.Camp, SceneFlowMap.SceneOf(RunFlowState.Camp));
            Assert.Equal(SceneId.Battle, SceneFlowMap.SceneOf(RunFlowState.Battle));
            Assert.Equal(SceneId.Result, SceneFlowMap.SceneOf(RunFlowState.Result));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SceneFlowMap.SceneOf((RunFlowState)99));
        }

        [Fact]
        public void Every_camp_the_run_enters_has_a_floor_left_to_start()
        {
            // `run.no_floor_left` guards the floor cursor rather than a player action, and
            // this is the invariant behind that: the phase machine only returns to Camp
            // while a floor is left, so a departure is never refused for having nothing to
            // start.
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            int camps = 0;
            while (run.FlowState != RunFlowState.Result)
            {
                Assert.Equal(RunFlowState.Camp, run.FlowState);
                Assert.NotNull(run.CurrentFloor);
                Assert.InRange(run.FloorIndex, 1, run.TotalFloors);
                camps++;

                Assert.True(flow.EnterBattle().IsSuccess);
                Assert.True(flow.RunFloorToEnd().IsSuccess);
            }

            Assert.Equal(run.TotalFloors, camps);
        }

        [Fact]
        public void Commands_out_of_order_are_refused_and_change_nothing()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.NewRun(config, RunFixture.DefaultSeed);

            Assert.Equal(RunFlowState.Title, run.FlowState);
            Assert.Equal(RunError.WrongState, run.Purchase(0).Error.Code);
            Assert.Equal(RunError.WrongState, run.RefreshShop().Error.Code);
            Assert.Equal(RunError.WrongState, run.StartFloor().Error.Code);
            Assert.Equal(RunError.WrongState, run.OpenShop().Error.Code);
            Assert.Equal(
                RunError.WrongState,
                run.MovePiece(new PieceRef(PieceKind.Item, 1), new CellOffset(0, 0), Rotation.Degrees0).Error.Code);
            Assert.Equal(RunError.WrongState, run.ReturnToTitle().Error.Code);

            Assert.True(run.StartRun().IsSuccess);
            Assert.Equal(RunFlowState.Camp, run.FlowState);

            // Starting twice is a different phase, so it is refused rather than
            // re-rolling the run under the caller.
            Assert.Equal(RunError.WrongState, run.StartRun().Error.Code);
            Assert.Equal(RunError.NotInBattle, run.ApplyBattleResult(Won()).Error.Code);
            Assert.Equal(RunError.WrongState, run.ReturnToTitle().Error.Code);
        }

        [Fact]
        public void A_floor_cannot_start_without_a_usable_arcane_weapon()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.True(run.Sell(RunFixture.StartingWeapon(run)).IsSuccess);
            Assert.Empty(run.Backpack.Items);

            Result<BattleSetup> blocked = run.StartFloor();

            Assert.True(blocked.IsFailure);
            Assert.Equal(RunError.NoArcaneWeapon, blocked.Error.Code);
            Assert.Equal(RunFlowState.Camp, run.FlowState);
            Assert.False(run.Editor.IsLocked, "a refused departure must not leave the bag locked");

            // The same answer through the scene flow, which is the seam Stage 09 uses.
            Result<IBattleSession> enter = flow.EnterBattle();
            Assert.True(enter.IsFailure);
            Assert.Equal(RunError.NoArcaneWeapon, enter.Error.Code);
            Assert.Null(flow.Battle);
        }

        [Fact]
        public void The_bag_is_locked_between_departure_and_settlement()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            PieceRef weapon = RunFixture.StartingWeapon(run);
            Assert.False(run.Editor.IsLocked);

            Assert.True(flow.EnterBattle().IsSuccess);
            Assert.True(run.Editor.IsLocked);
            Assert.Equal(RunError.WrongState, run.MovePiece(weapon, new CellOffset(2, 2), Rotation.Degrees0).Error.Code);
            Assert.Equal(RunError.WrongState, run.RotatePiece(weapon).Error.Code);
            Assert.Equal(RunError.WrongState, run.Sell(weapon).Error.Code);
            Assert.Equal(RunError.WrongState, run.Purchase(0).Error.Code);
            Assert.Equal(RunError.WrongState, run.Reset().Error.Code);

            Assert.True(flow.RunFloorToEnd().IsSuccess);
            Assert.False(run.Editor.IsLocked, "Camp unlocks the bag again");
            Assert.True(run.RotatePiece(weapon).IsSuccess);
        }

        [Fact]
        public void Entering_a_floor_twice_is_refused_until_the_first_is_settled()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.True(flow.EnterBattle().IsSuccess);

            Result<IBattleSession> again = flow.EnterBattle();
            Assert.True(again.IsFailure);
            Assert.Equal(RunError.BattleRunning, again.Error.Code);

            // Settling while the floor is still running is refused too, and the battle
            // stays usable.
            Result<FloorSettlement> tooEarly = flow.SettleBattle();
            Assert.True(tooEarly.IsFailure);
            Assert.Equal(RunError.BattleRunning, tooEarly.Error.Code);
            Assert.True(flow.IsInBattle);
            Assert.True(flow.StepBattle(PlayerInputFrame.Idle).IsSuccess);
            Assert.True(flow.RunFloorToEnd().IsSuccess);
        }

        [Fact]
        public void A_scripted_run_clears_five_floors_and_ends_at_the_result_scene()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.Equal(5, run.TotalFloors);
            Assert.Equal(SceneId.Camp, flow.CurrentScene);

            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Assert.Equal(RunFlowState.Camp, run.FlowState);
                Assert.Equal(floor, run.FloorIndex);

                bool isBoss = run.CurrentFloor!.IsBoss;

                Result<IBattleSession> started = flow.EnterBattle();
                Assert.True(started.IsSuccess, started.Error.ToString());
                Assert.Equal(RunFlowState.Battle, run.FlowState);
                Assert.Equal(SceneId.Battle, flow.CurrentScene);

                Result<FloorSettlement> settled = flow.RunFloorToEnd();

                Assert.True(settled.IsSuccess, settled.Error.ToString());
                Assert.True(settled.Value.IsWin);
                Assert.Equal(floor, settled.Value.FloorIndex);
                Assert.Equal(floor, settled.Value.SettlementId);
                Assert.Equal(isBoss, settled.Value.EndReason == BattleEndReason.BossDefeated);
                Assert.Equal(floor, run.FloorsCleared);
            }

            Assert.Equal(RunFlowState.Result, run.FlowState);
            Assert.Equal(SceneId.Result, flow.CurrentScene);
            Assert.True(run.IsRunOver);
            Assert.Equal(run.TotalFloors, run.LastSettlementId);
            Assert.Equal(run.TotalFloors, run.Log.BattleCount);

            // Nothing may start a floor once the run ended.
            Assert.Equal(RunError.WrongState, run.StartFloor().Error.Code);

            Assert.True(run.ReturnToTitle().IsSuccess);
            Assert.Equal(RunFlowState.Title, run.FlowState);
            Assert.Equal(SceneId.Title, flow.CurrentScene);
        }

        [Fact]
        public void The_boss_floor_timing_out_ends_the_run_in_defeat()
        {
            IGameConfig config = RunFixture.Config();
            var factory = new ScriptedBattleSessionFactory(
                config,
                normal: ScriptedBattleSessionFactory.WinOnFirstTick(),
                boss: ScriptedBattleSessionFactory.TimeoutOnFirstTick());

            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, factory);

            for (int floor = 1; floor < run.TotalFloors; floor++)
            {
                Assert.False(run.CurrentFloor!.IsBoss, "floors 1-4 are the normal ones");
                Assert.True(flow.EnterBattle().IsSuccess);
                Assert.True(flow.RunFloorToEnd().IsSuccess);
            }

            Assert.Equal(run.TotalFloors, run.FloorIndex);
            Assert.True(run.CurrentFloor!.IsBoss);
            Assert.True(flow.EnterBattle().IsSuccess);

            Result<FloorSettlement> settled = flow.RunFloorToEnd();

            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.False(settled.Value.IsWin);
            Assert.Equal(BattleEndReason.Timeout, settled.Value.EndReason);
            Assert.Equal(BattlePhase.Lost, settled.Value.Outcome);
            Assert.True(settled.Value.RunCompleted);
            Assert.Equal(0, settled.Value.HealedHp);
            Assert.Equal(run.TotalFloors - 1, run.FloorsCleared);
            Assert.Equal(RunFlowState.Result, run.FlowState);
            Assert.Equal(SceneId.Result, flow.CurrentScene);
        }

        [Fact]
        public void Clearing_a_floor_heals_the_data_percentage_capped_at_max_hp()
        {
            IGameConfig config = RunFixture.Config();
            int expectedHeal = (config.Constant.MaxHp * config.Economy.HealPercentOnClear) / 100;

            // 30 damage per second for one second, then a win: the run takes real damage
            // and then clears the floor, so the heal is observably not zero.
            var factory = new ScriptedBattleSessionFactory(
                config,
                normal: new FakeBattleOptions
                {
                    WinAtTick = BattleConstants.TickRate + 1,
                    DamagePerSecond = 30,
                    DummyEntityCount = 4,
                    EntityLifetimeTicks = 8,
                });

            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, factory);

            Assert.True(flow.EnterBattle().IsSuccess);
            while (flow.Battle!.Phase == BattlePhase.Running)
            {
                Assert.True(flow.StepBattle(PlayerInputFrame.Idle).IsSuccess);
            }

            Assert.True(flow.Battle.TryGetResult(out BattleResult result));
            Assert.True(result.FinalHp < run.MaxHp, "the fixture should have taken damage");

            Result<FloorSettlement> settled = flow.SettleBattle();

            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.Equal(System.Math.Min(run.MaxHp, result.FinalHp + expectedHeal), run.Hp);
            Assert.Equal(run.Hp - result.FinalHp, settled.Value.HealedHp);
            Assert.True(settled.Value.HealedHp > 0, "a damaged run must be healed by the clear");
        }

        [Fact]
        public void A_cleared_floor_with_full_hp_does_not_heal_past_the_cap()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.Equal(run.MaxHp, run.Hp);
            Assert.True(flow.EnterBattle().IsSuccess);
            Result<FloorSettlement> settled = flow.RunFloorToEnd();

            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.Equal(0, settled.Value.HealedHp);
            Assert.Equal(run.MaxHp, run.Hp);
        }

        [Fact]
        public void A_settlement_pays_the_battle_currency_and_the_clear_reward_exactly_once()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.True(flow.EnterBattle().IsSuccess);

            // A hand-built result, because the fake pays nothing by design (Run owns the
            // economy) and the credit path still has to be proven.
            var result = new BattleResult
            {
                Outcome = BattlePhase.Won,
                EndReason = BattleEndReason.SurvivedAllWaves,
                SurvivedTicks = 60 * BattleConstants.TickRate,
                FinalHp = 60,
                CurrencyEarned = 40,
                Kills = 12,
                RemainingConsumables = new List<int>(),
            };

            int clearReward = config.Economy.ClearRewardBia;
            Assert.True(clearReward > 0, "the shipped clear reward must be paid to test it");

            Result<FloorSettlement> first = run.ApplyBattleResult(result);

            Assert.True(first.IsSuccess, first.Error.ToString());
            Assert.Equal(40 + clearReward, first.Value.RewardBia);
            Assert.Equal(config.Economy.StartBia + 40 + clearReward, run.Wallet.Bia);

            int purse = run.Wallet.Bia;
            int hp = run.Hp;
            int settlementId = first.Value.SettlementId;

            Result<FloorSettlement> again = run.ApplyBattleResult(result);

            Assert.True(again.IsFailure);
            Assert.Equal(RunError.NotInBattle, again.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.Equal(hp, run.Hp);
            Assert.Equal(settlementId, run.LastSettlementId);
        }

        [Fact]
        public void A_lost_floor_pays_the_battle_currency_but_not_the_clear_reward()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.True(flow.EnterBattle().IsSuccess);

            var result = new BattleResult
            {
                Outcome = BattlePhase.Lost,
                EndReason = BattleEndReason.PlayerDied,
                SurvivedTicks = 30 * BattleConstants.TickRate,
                FinalHp = 0,
                CurrencyEarned = 12,
                Kills = 4,
                RemainingConsumables = new List<int>(),
            };

            Result<FloorSettlement> settled = run.ApplyBattleResult(result);

            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.Equal(12, settled.Value.RewardBia);
            Assert.Equal(config.Economy.StartBia + 12, run.Wallet.Bia);
            Assert.True(settled.Value.RunCompleted);
        }

        [Fact]
        public void A_used_offering_leaves_the_bag_at_settlement()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            // Two offerings on the board, so the loadout carries two of them.
            Assert.True(run.MovePiece(run.Backpack.AddItem("item.red_soda").Value, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            Assert.True(run.MovePiece(run.Backpack.AddItem("item.red_soda").Value, new CellOffset(2, 1), Rotation.Degrees0).IsSuccess);
            Assert.Equal(3, run.Backpack.Items.Count);

            Assert.True(flow.EnterBattle().IsSuccess);

            // The floor says one of the two pieces was used. RemainingConsumables counts USES
            // (Stage 11a): two red sodas at two uses each is four, so one used piece leaves two.
            var result = new BattleResult
            {
                Outcome = BattlePhase.Won,
                EndReason = BattleEndReason.SurvivedAllWaves,
                FinalHp = config.Constant.MaxHp,
                CurrencyEarned = 0,
                RemainingConsumables = new List<int> { 2 },
            };

            Result<FloorSettlement> settled = run.ApplyBattleResult(result);

            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.Equal(1, CountItems(run, "item.red_soda"));
            Assert.True(run.Backpack.TryValidate(out string? violation), violation);
        }

        /// <summary>
        /// A piece with several uses is still gone once any of them is spent: the result
        /// reports uses, and the settlement rounds the difference up to whole pieces.
        /// </summary>
        [Fact]
        public void A_partly_used_offering_still_leaves_the_bag_at_settlement()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.True(run.MovePiece(run.Backpack.AddItem("item.red_soda").Value, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            Assert.Equal(2, run.Backpack.Items.Count);

            Assert.True(flow.EnterBattle().IsSuccess);

            // One red soda carries two uses; one was spent, so the piece is consumed.
            var result = new BattleResult
            {
                Outcome = BattlePhase.Won,
                EndReason = BattleEndReason.SurvivedAllWaves,
                FinalHp = config.Constant.MaxHp,
                RemainingConsumables = new List<int> { 1 },
            };

            Result<FloorSettlement> settled = run.ApplyBattleResult(result);

            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.Equal(0, CountItems(run, "item.red_soda"));
            Assert.True(run.Backpack.TryValidate(out string? violation), violation);
        }

        [Fact]
        public void A_consumable_the_bag_cannot_cover_refuses_the_whole_settlement()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            var flow = new SceneFlow(run, new ScriptedBattleSessionFactory(config));

            Assert.True(run.MovePiece(run.Backpack.AddItem("item.red_soda").Value, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            Assert.True(flow.EnterBattle().IsSuccess);

            int purse = run.Wallet.Bia;
            int items = run.Backpack.Items.Count;

            // The battle claims both of its offering slots were spent, but the run only
            // ever carried one: a desync, and paying HP/currency for it would be worse
            // than refusing.
            var result = new BattleResult
            {
                Outcome = BattlePhase.Won,
                EndReason = BattleEndReason.SurvivedAllWaves,
                FinalHp = config.Constant.MaxHp,
                CurrencyEarned = 40,
                RemainingConsumables = new List<int> { 0, 0 },
            };

            Result<FloorSettlement> settled = run.ApplyBattleResult(result);

            Assert.True(settled.IsFailure);
            Assert.Equal(InventoryError.NotEnoughPieces, settled.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.Equal(items, run.Backpack.Items.Count);
            Assert.Equal(RunFlowState.Battle, run.FlowState);
        }

        [Fact]
        public void SceneFlow_requires_its_inputs()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());

            Assert.Throws<System.ArgumentNullException>(
                () => new SceneFlow(null!, new ScriptedBattleSessionFactory(RunFixture.Config())));
            Assert.Throws<System.ArgumentNullException>(() => new SceneFlow(run, null!));
            Assert.Throws<System.ArgumentNullException>(() => run.ApplyBattleResult(null!));
        }

        private static int CountItems(RunSession run, string itemId)
        {
            int count = 0;
            for (int i = 0; i < run.Backpack.Items.Count; i++)
            {
                if (run.Backpack.Items[i].DefId == itemId)
                {
                    count++;
                }
            }

            return count;
        }

        private static BattleResult Won() => new BattleResult
        {
            Outcome = BattlePhase.Won,
            EndReason = BattleEndReason.SurvivedAllWaves,
            FinalHp = 100,
            RemainingConsumables = new List<int>(),
        };
    }
}
