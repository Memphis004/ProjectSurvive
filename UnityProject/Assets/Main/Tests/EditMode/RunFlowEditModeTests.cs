using NUnit.Framework;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 06 acceptance inside Unity: a scripted five-floor run over the <b>shipped</b>
    /// tables, driven through the scene flow, and a replay of it that lands on the same
    /// run-state hash.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The run's own behaviour is tested exhaustively in <c>lib/Tests~</c> (40 cases; the
    /// commands and results are in <c>docs/stages/06-report.md</c>). What can only fail
    /// <i>here</i> is the seam: the game's real <c>Resources/Config</c> tables through the
    /// Luban adapter, the MessagePack source-generated snapshot formatter under Unity's
    /// compiler, and the run driving a battle session that arrives only through
    /// <see cref="IBattleSessionFactory"/>.
    /// </para>
    /// <para>
    /// Balance numbers are read from the loaded config, never hard-coded, so retuning
    /// <c>data/</c> cannot make a correct rule look broken.
    /// </para>
    /// </remarks>
    public sealed class RunFlowEditModeTests
    {
        /// <summary>A seed that produces four distinct floor-1 offers.</summary>
        private const uint Seed = 20261014u;

        private IGameConfig _config;
        private IBackpackSnapshotCodec _codec;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();
            _codec = new MessagePackBackpackSnapshotCodec();
        }

        [Test]
        public void A_scripted_run_clears_every_shipped_floor_and_ends_at_the_result_scene()
        {
            RunSession run = StartedRun();
            var flow = new SceneFlow(run, new ScriptedFactory(_config));

            Assert.That(run.TotalFloors, Is.GreaterThan(0), "the floor table must ship rows");
            Assert.That(flow.CurrentScene, Is.EqualTo(SceneId.Camp));

            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Result<IBattleSession> entered = flow.EnterBattle();
                Assert.That(entered.IsSuccess, Is.True, "floor " + floor + ": " + entered.Error);
                Assert.That(flow.CurrentScene, Is.EqualTo(SceneId.Battle));

                Result<FloorSettlement> settled = flow.RunFloorToEnd();
                Assert.That(settled.IsSuccess, Is.True, "floor " + floor + ": " + settled.Error);
                Assert.That(settled.Value.IsWin, Is.True, "the scripted floor wins");
                Assert.That(settled.Value.SettlementId, Is.EqualTo(floor), "one settlement id per floor");
            }

            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Result));
            Assert.That(flow.CurrentScene, Is.EqualTo(SceneId.Result));
            Assert.That(run.FloorsCleared, Is.EqualTo(run.TotalFloors));
            Assert.That(run.Log.BattleCount, Is.EqualTo(run.TotalFloors), "every floor kept its recording");
        }

        [Test]
        public void The_run_replays_from_its_own_log_to_the_same_state_hash()
        {
            RunSession run = StartedRun();
            var factory = new ScriptedFactory(_config);
            var flow = new SceneFlow(run, factory);

            // Some camp work first, so the log carries more than the departures: rent the
            // cheapest offer, pin another slot and pay for a re-roll.
            int rentSlot = CheapestSlot(run);
            Assert.That(run.SetSlotLocked((rentSlot + 1) % run.Stock.SlotCount, true).IsSuccess, Is.True);
            Assert.That(run.Purchase(rentSlot).IsSuccess, Is.True);
            Assert.That(run.RefreshShop().IsSuccess, Is.True);

            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Assert.That(flow.EnterBattle().IsSuccess, Is.True, "floor " + floor);
                Assert.That(flow.RunFloorToEnd().IsSuccess, Is.True, "floor " + floor);
            }

            RunSession replayed = run.Log.Replay(_config, _codec, factory);

            Assert.That(replayed.ComputeStateHash(), Is.EqualTo(run.ComputeStateHash()));
            Assert.That(replayed.FloorsCleared, Is.EqualTo(run.FloorsCleared));
            Assert.That(replayed.Wallet.Bia, Is.EqualTo(run.Wallet.Bia));
            Assert.That(replayed.FlowState, Is.EqualTo(run.FlowState));
        }

        [Test]
        public void A_rented_slot_takes_the_money_once_and_never_refills_for_free()
        {
            RunSession run = StartedRun();
            int slot = CheapestSlot(run);
            int price = run.Stock[slot].Price;
            int startBia = run.Wallet.Bia;

            Result<PurchaseOutcome> rent = run.Purchase(slot);

            Assert.That(rent.IsSuccess, Is.True, rent.Error.ToString());
            Assert.That(run.Wallet.Bia, Is.EqualTo(startBia - price), "the table price is what was paid");
            Assert.That(run.Stock[slot].IsPurchased, Is.True);
            Assert.That(run.Stock[slot].IsEmpty, Is.True, "a rented slot holds nothing more to rent");

            int purse = run.Wallet.Bia;
            Result<PurchaseOutcome> again = run.Purchase(slot);

            Assert.That(again.IsFailure, Is.True, "\u0e0b\u0e37\u0e49\u0e2d\u0e0a\u0e48\u0e2d\u0e07\u0e40\u0e14\u0e34\u0e21\u0e0b\u0e49\u0e33\u0e44\u0e21\u0e48\u0e44\u0e14\u0e49");
            Assert.That(again.Error.Code, Is.EqualTo(ShopError.SlotPurchased));
            Assert.That(run.Wallet.Bia, Is.EqualTo(purse), "a refused rent must not move the purse");

            Result<RefreshOutcome> refresh = run.RefreshShop();
            Assert.That(refresh.IsSuccess, Is.True, refresh.Error.ToString());
            Assert.That(run.Stock[slot].IsPurchased, Is.True);
            Assert.That(run.Stock[slot].IsEmpty, Is.True, "a re-roll must not refill a rented slot");
            Assert.That(refresh.Value.Filled, Is.EqualTo(run.Stock.SlotCount - 1));
            Assert.That(refresh.Value.Cost, Is.EqualTo(_config.Economy.RefreshBaseCost));
        }

        [Test]
        public void Departing_without_an_arcane_weapon_is_refused_with_a_reason_code()
        {
            RunSession run = StartedRun();
            PieceRef weapon = StartingWeapon(run);

            Assert.That(run.Sell(weapon).IsSuccess, Is.True, "the altar takes the weapon back");

            Result<BattleSetup> departed = run.StartFloor();

            Assert.That(departed.IsFailure, Is.True, "\u0e2d\u0e2d\u0e01\u0e40\u0e14\u0e34\u0e19\u0e17\u0e32\u0e07\u0e44\u0e21\u0e48\u0e44\u0e14\u0e49\u0e16\u0e49\u0e32\u0e44\u0e21\u0e48\u0e21\u0e35\u0e2d\u0e32\u0e27\u0e38\u0e18\u0e2d\u0e32\u0e04\u0e21");
            Assert.That(departed.Error.Code, Is.EqualTo(RunError.NoArcaneWeapon),
                "the UI needs the reason code, not a generic refusal");
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp), "a refused departure stays at camp");
        }

        /// <summary>A run that has started and is sitting in the first Camp.</summary>
        private RunSession StartedRun()
        {
            var run = new RunSession(_config, _codec, Seed);
            Result started = run.StartRun();
            Assert.That(started.IsSuccess, Is.True, "the run must start on the shipped tables: " + started.Error);
            return run;
        }

        /// <summary>The still-available slot with the lowest price.</summary>
        private static int CheapestSlot(RunSession run)
        {
            int best = -1;
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                if (run.Stock[i].IsEmpty || run.Stock[i].IsPurchased)
                {
                    continue;
                }

                if (best < 0 || run.Stock[i].Price < run.Stock[best].Price)
                {
                    best = i;
                }
            }

            Assert.That(best, Is.GreaterThanOrEqualTo(0), "the shop must offer something to rent");
            return best;
        }

        /// <summary>The run's starting weapon by handle.</summary>
        private static PieceRef StartingWeapon(RunSession run)
        {
            for (int i = 0; i < run.Backpack.Items.Count; i++)
            {
                if (run.Backpack.Items[i].DefId == RunDefaults.InitialWeaponId)
                {
                    return run.Backpack.Items[i].Ref;
                }
            }

            throw new AssertionException("the run has no starting weapon");
        }

        /// <summary>
        /// Wins every floor on its first tick. The test's own implementation of the port,
        /// because the run must reach a battle only through
        /// <see cref="IBattleSessionFactory"/> — the same seam Stage 07 fills.
        /// </summary>
        private sealed class ScriptedFactory : IBattleSessionFactory
        {
            private readonly FakeBattleSessionFactory _normal;
            private readonly FakeBattleSessionFactory _boss;

            internal ScriptedFactory(IGameConfig config)
            {
                var win = new FakeBattleOptions
                {
                    WinAtTick = 1,
                    DummyEntityCount = 4,
                    EntityLifetimeTicks = 8,
                };

                _normal = new FakeBattleSessionFactory(config, win);
                _boss = new FakeBattleSessionFactory(config, win);
            }

            public IBattleSession Create(BattleSetup setup) =>
                (setup.IsBossFloor ? _boss : _normal).Create(setup);
        }
    }
}
