using System.Collections.Generic;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Run
{
    /// <summary>
    /// Stage 06 task 2–3: the camp shop's stock comes from the weighted pool, is created
    /// once per camp entry, and a re-roll keeps what the player pinned.
    /// </summary>
    public sealed class ShopStockTests
    {
        [Fact]
        public void Every_offer_comes_from_the_pool_row_for_that_floor_and_no_piece_repeats()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            Assert.Equal(config.Economy.ShopSlotCount, run.Stock.SlotCount);
            Assert.Equal(4, run.Stock.SlotCount);

            var seen = new HashSet<string>();
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                ShopSlot slot = run.Stock[i];
                Assert.False(slot.IsEmpty, "slot " + i + " should hold an offer on the first camp entry");
                Assert.True(seen.Add(slot.OfferedId), "the same piece was offered twice: " + slot.OfferedId);

                bool found = false;
                for (int p = 0; p < config.ShopPool.Count; p++)
                {
                    ShopPoolEntryConfig entry = config.ShopPool[p];
                    if (entry.OfferedId != slot.OfferedId)
                    {
                        continue;
                    }

                    found = true;
                    Assert.True(entry.FloorMin <= 1 && entry.FloorMax >= 1,
                        entry.OfferedId + " is not in the floor-1 band");
                }

                Assert.True(found, slot.OfferedId + " is not in the shop pool at all");
                Assert.True(slot.Price > 0, "a rentable offer needs a real price");
                Assert.False(slot.IsPurchased);
                Assert.False(slot.IsLocked);
            }
        }

        [Fact]
        public void The_same_seed_and_state_roll_the_same_offers()
        {
            IGameConfig config = RunFixture.Config();
            RunSession first = RunFixture.StartedRun(config, 4242u);
            RunSession second = RunFixture.StartedRun(RunFixture.Config(), 4242u);

            Assert.Equal(RunFixture.StockIds(first), RunFixture.StockIds(second));

            // No re-roll has been paid for in this camp, so the next one costs the base.
            Assert.Equal(config.Economy.RefreshBaseCost, first.Stock.RefreshCost(config.Economy));
        }

        [Fact]
        public void A_different_seed_moves_the_offers()
        {
            // Not one seed against one other (that is a coin flip on 10 weighted rows):
            // eight seeds must not all roll the same panel.
            var panels = new HashSet<string>();
            for (uint seed = 1; seed <= 8; seed++)
            {
                RunSession run = RunFixture.StartedRun(RunFixture.Config(), seed);
                panels.Add(string.Join("|", RunFixture.StockIds(run)));
            }

            Assert.True(panels.Count > 1, "eight different seeds produced one and the same shop panel");
        }

        [Fact]
        public void Re_opening_the_shop_returns_the_same_stock()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());
            string[] before = RunFixture.StockIds(run);

            Result<ShopStock> first = run.OpenShop();
            Result<ShopStock> second = run.OpenShop();

            Assert.True(first.IsSuccess);
            Assert.True(second.IsSuccess);
            Assert.Same(run.Stock, first.Value);
            Assert.Same(first.Value, second.Value);
            Assert.Equal(before, RunFixture.StockIds(run));
        }

        [Fact]
        public void The_pool_band_filters_by_floor()
        {
            IGameConfig config = RunFixture.Config();
            var roller = new ShopRoller(config);

            List<ShopPoolEntryConfig> floorOne = roller.Eligible(1);
            List<ShopPoolEntryConfig> floorFive = roller.Eligible(5);

            Assert.NotEmpty(floorOne);
            Assert.NotEmpty(floorFive);

            for (int i = 0; i < floorOne.Count; i++)
            {
                Assert.True(floorOne[i].FloorMin <= 1 && floorOne[i].FloorMax >= 1);
            }

            // A floor-1-only row must not be offered on the boss floor, and vice versa:
            // the spell tome does not exist before floor 3.
            Assert.DoesNotContain(floorOne, entry => entry.OfferedId == "item.spell_tome");
            Assert.Contains(floorFive, entry => entry.OfferedId == "item.spell_tome");
            Assert.DoesNotContain(floorFive, entry => entry.OfferedId == "bag.raw_cotton_bag");
        }

        [Fact]
        public void A_pool_smaller_than_the_panel_leaves_slots_empty_instead_of_duplicating()
        {
            // No shipped floor has an empty band, so the degenerate case is driven through
            // the explicit-pool overload: one offer, four slots.
            IGameConfig config = RunFixture.Config();
            var roller = new ShopRoller(config);
            var stock = new ShopStock(config.Economy.ShopSlotCount);
            var rng = new Xoshiro128(1);
            var pool = new List<ShopPoolEntryConfig>
            {
                new ShopPoolEntryConfig
                {
                    Id = "shop_pool.test_egg",
                    FloorMin = 1,
                    FloorMax = 5,
                    ItemId = "item.boiled_egg",
                    Weight = 1,
                },
            };

            Assert.Equal(1, roller.Roll(stock, pool, rng));
            Assert.Equal("item.boiled_egg", stock[0].OfferedId);
            for (int i = 1; i < stock.SlotCount; i++)
            {
                Assert.True(stock[i].IsEmpty, "slot " + i + " should be empty, not a duplicate");
            }

            // A roll whose band matches nothing clears rather than invents.
            var empty = new ShopStock(config.Economy.ShopSlotCount);
            Assert.Empty(roller.Eligible(99));
            Assert.Equal(0, roller.Roll(empty, 99, rng));
            for (int i = 0; i < empty.SlotCount; i++)
            {
                Assert.True(empty[i].IsEmpty);
            }
        }

        [Fact]
        public void Refresh_costs_five_then_seven_and_keeps_pinned_slots()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            EconomyConfig economy = config.Economy;

            Assert.True(run.SetSlotLocked(2, true).IsSuccess);
            string pinned = run.Stock[2].OfferedId;
            int startBia = run.Wallet.Bia;
            Assert.Equal(economy.StartBia, startBia);

            Result<RefreshOutcome> first = run.RefreshShop();

            Assert.True(first.IsSuccess, first.Error.ToString());
            Assert.Equal(economy.RefreshBaseCost, first.Value.Cost);
            Assert.Equal(economy.RefreshBaseCost, startBia - run.Wallet.Bia);
            Assert.Equal(pinned, run.Stock[2].OfferedId);
            Assert.Equal(1, run.Stock.RefreshCount);
            Assert.Equal(run.Stock.SlotCount - 1, first.Value.Filled);

            Result<RefreshOutcome> second = run.RefreshShop();

            Assert.True(second.IsSuccess, second.Error.ToString());
            Assert.Equal(economy.RefreshBaseCost + economy.RefreshCostIncrement, second.Value.Cost);
            Assert.Equal(2, run.Stock.RefreshCount);

            // The next re-roll costs 9 and the purse is 3: refused, and the purse does
            // not move when it is (\"เงินไม่พอ ... ไม่เปลี่ยน state\").
            int purse = run.Wallet.Bia;
            Result<RefreshOutcome> third = run.RefreshShop();

            Assert.True(third.IsFailure);
            Assert.Equal(WalletError.InsufficientBia, third.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.Equal(2, run.Stock.RefreshCount);
        }

        [Fact]
        public void A_refresh_moves_the_shop_stream_but_a_re_open_does_not()
        {
            IGameConfig config = RunFixture.Config();
            RunSession rolled = RunFixture.StartedRun(config, 777u);
            RunSession untouched = RunFixture.StartedRun(RunFixture.Config(), 777u);

            Assert.Equal(RunFixture.StockIds(untouched), RunFixture.StockIds(rolled));

            Assert.True(rolled.RefreshShop().IsSuccess);

            Assert.NotEqual(RunFixture.StockIds(untouched), RunFixture.StockIds(rolled));
            Assert.Equal(RunFixture.StockIds(untouched), RunFixture.StockIds(untouched));
        }

        [Fact]
        public void A_refresh_never_refills_slots_that_were_rented()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            int slot = RunFixture.CheapestAvailableSlot(run);

            Assert.True(run.Purchase(slot).IsSuccess);
            Assert.True(run.Stock[slot].IsPurchased);

            Result<RefreshOutcome> refresh = run.RefreshShop();

            Assert.True(refresh.IsSuccess, refresh.Error.ToString());
            Assert.True(run.Stock[slot].IsPurchased, "a rented slot must stay rented");
            Assert.True(run.Stock[slot].IsEmpty, "a rented slot must not refill for free");
            Assert.Equal(run.Stock.SlotCount - 1, refresh.Value.Filled);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void A_new_camp_refills_a_slot_rented_in_the_previous_one(int slot)
        {
            // Stage 06b: "ทุกการเข้าศาลาสร้าง stock ใหม่ 4 ช่อง; ช่องที่เช่าแล้วไม่เติม
            // ฟรีเฉพาะภายในศาลาเดียวกัน". Renting a slot must not follow the run into
            // the next sala, which is the defect the player reported as one empty shelf
            // that never refills for the rest of the run.
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            Assert.True(run.Purchase(slot).IsSuccess, "slot " + slot + " could not be rented");
            Assert.True(run.Stock[slot].IsPurchased);

            Assert.True(run.StartFloor().IsSuccess);
            Result<FloorSettlement> settled = run.ApplyBattleResult(Won(run));
            Assert.True(settled.IsSuccess, settled.Error.ToString());
            Assert.Equal(RunFlowState.Camp, run.FlowState);
            Assert.Equal(2, run.FloorIndex);

            AssertNewCampPanel(run, "after renting slot " + slot);
        }

        [Fact]
        public void A_new_camp_refills_every_slot_after_several_purchases()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            run.Wallet.Credit(200);

            Assert.True(run.Purchase(0).IsSuccess);
            Assert.True(run.Purchase(2).IsSuccess);

            Assert.True(run.StartFloor().IsSuccess);
            Assert.True(run.ApplyBattleResult(Won(run)).IsSuccess);

            AssertNewCampPanel(run, "after renting slots 0 and 2");
        }

        [Fact]
        public void A_new_camp_refills_every_slot_after_the_whole_panel_was_rented()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            run.Wallet.Credit(200);

            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                Assert.True(run.Purchase(slot).IsSuccess, "slot " + slot);
            }

            Assert.True(run.Stock.IsSoldOut);

            Assert.True(run.StartFloor().IsSuccess);
            Assert.True(run.ApplyBattleResult(Won(run)).IsSuccess);

            AssertNewCampPanel(run, "after renting the whole panel");
        }

        [Fact]
        public void A_new_camp_does_not_inherit_a_lock_from_the_previous_one()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            Assert.True(run.SetSlotLocked(2, true).IsSuccess);
            Assert.True(run.Stock[2].IsLocked);

            Assert.True(run.StartFloor().IsSuccess);
            Assert.True(run.ApplyBattleResult(Won(run)).IsSuccess);

            AssertNewCampPanel(run, "after departing with slot 2 pinned");
        }

        [Fact]
        public void An_offer_that_resolves_to_nothing_leaves_its_slot_empty()
        {
            // A pool row whose id is in neither the bag nor the item table is a data bug.
            // It must not become a rentable slot with a price of zero, and it must not
            // crash a re-roll either — the slot is left empty.
            IGameConfig config = RunFixture.Config();
            var roller = new ShopRoller(config);
            var stock = new ShopStock(config.Economy.ShopSlotCount);
            var rng = new Xoshiro128(7);
            var pool = new List<ShopPoolEntryConfig>
            {
                new ShopPoolEntryConfig
                {
                    Id = "shop_pool.ghost",
                    FloorMin = 1,
                    FloorMax = 5,
                    ItemId = "item.does_not_exist",
                    Weight = 1,
                },
            };

            Assert.False(roller.TryResolve("item.does_not_exist", out PieceKind kind, out int price));
            Assert.Equal(PieceKind.None, kind);
            Assert.Equal(0, price);
            Assert.False(roller.TryResolve(string.Empty, out _, out _));

            Assert.Equal(0, roller.Roll(stock, pool, rng));
            Assert.True(stock[0].IsEmpty);

            Assert.Throws<System.ArgumentNullException>(() => roller.Roll((ShopStock)null!, pool, rng));
            Assert.Throws<System.ArgumentNullException>(
                () => roller.Roll(stock, (IReadOnlyList<ShopPoolEntryConfig>)null!, rng));
            Assert.Throws<System.ArgumentNullException>(() => roller.Roll(stock, pool, null!));
        }

        [Fact]
        public void Renting_every_slot_sells_the_panel_out()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            Assert.False(run.Stock.IsSoldOut, "a fresh camp panel has offers");
            Assert.Equal(run.Stock.SlotCount, run.Stock.AvailableCount);

            // A purse big enough to rent the whole panel: what is being tested is the panel
            // read, not what 15 bia can afford.
            run.Wallet.Credit(200);

            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                Assert.True(run.Purchase(slot).IsSuccess, "slot " + slot);
                Assert.Equal(run.Stock.SlotCount - 1 - slot, run.Stock.AvailableCount);
            }

            Assert.True(run.Stock.IsSoldOut);
            Assert.Equal(0, run.Stock.AvailableCount);

            // The re-roll is still paid for, but a sold-out panel stays sold out: there is
            // nothing left that a re-roll may replace.
            Result<RefreshOutcome> refresh = run.RefreshShop();
            Assert.True(refresh.IsSuccess, refresh.Error.ToString());
            Assert.Equal(0, refresh.Value.Filled);
            Assert.True(run.Stock.IsSoldOut);
        }

        [Fact]
        public void Pinning_a_rented_slot_is_refused()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());
            int slot = RunFixture.CheapestAvailableSlot(run);
            Assert.True(run.Purchase(slot).IsSuccess);

            Result<ShopSlot> locked = run.SetSlotLocked(slot, true);

            Assert.True(locked.IsFailure);
            Assert.Equal(ShopError.SlotPurchased, locked.Error.Code);
            Assert.False(run.Stock[slot].IsLocked);
        }

        [Fact]
        public void An_out_of_range_slot_is_refused()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());

            Result<PurchaseOutcome> tooHigh = run.Purchase(run.Stock.SlotCount);
            Result<PurchaseOutcome> negative = run.Purchase(-1);
            Result<ShopSlot> lockTooHigh = run.SetSlotLocked(run.Stock.SlotCount, true);

            Assert.Equal(ShopError.UnknownSlot, tooHigh.Error.Code);
            Assert.Equal(ShopError.UnknownSlot, negative.Error.Code);
            Assert.Equal(ShopError.UnknownSlot, lockTooHigh.Error.Code);
        }

        /// <summary>
        /// A camp the run has just entered: four rentable offers, nothing carried over
        /// from the previous sala (no rent, no pin), and a fresh re-roll counter.
        /// </summary>
        private static void AssertNewCampPanel(RunSession run, string context)
        {
            Assert.Equal(4, run.Stock.SlotCount);
            Assert.Equal(0, run.Stock.RefreshCount);
            Assert.Equal(run.Stock.SlotCount, run.Stock.AvailableCount);

            var seen = new HashSet<string>();
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                ShopSlot slot = run.Stock[i];
                Assert.False(slot.IsEmpty, context + ": slot " + i + " was left empty");
                Assert.False(slot.IsPurchased, context + ": slot " + i + " is still marked rented");
                Assert.False(slot.IsLocked, context + ": slot " + i + " is still marked locked");
                Assert.True(slot.Price > 0, context + ": slot " + i + " has no price");
                Assert.True(seen.Add(slot.OfferedId), context + ": the same piece was offered twice: " + slot.OfferedId);
            }
        }

        private static BattleResult Won(RunSession run) => new BattleResult
        {
            Outcome = BattlePhase.Won,
            EndReason = BattleEndReason.SurvivedAllWaves,
            FinalHp = run.MaxHp,
            RemainingConsumables = new List<int>(),
        };

        [Fact]
        public void ShopStock_requires_its_inputs()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ShopStock(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => run.Stock[run.Stock.SlotCount]);
            Assert.Throws<System.ArgumentNullException>(() => run.Stock.RefreshCost(null!));
        }
    }
}
