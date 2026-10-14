using System;
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
    /// Stage 06 task 3 and 7: the wallet, the starting kit, and purchase / sell / refresh
    /// as atomic commands — \"หีบเต็มต้องไม่หักเงิน\", \"ซื้อช่องเดิมซ้ำไม่ได้\".
    /// </summary>
    public sealed class RunEconomyTests
    {
        [Fact]
        public void The_run_starts_with_the_data_balance_the_starting_bag_and_one_arcane_weapon()
        {
            IGameConfig config = RunFixture.Config();
            Require(config.TryGetBag(RunDefaults.InitialBagId, out BagConfig bag), "the starting bag is in the bag table");
            Require(
                config.TryGetItem(RunDefaults.InitialWeaponId, out ItemConfig weapon),
                "the starting weapon is in the item table");

            RunSession run = RunFixture.StartedRun(config);

            Assert.Equal(RunFlowState.Camp, run.FlowState);
            Assert.Equal(config.Economy.StartBia, run.Wallet.Bia);
            Assert.Equal(config.Constant.MaxHp, run.Hp);
            Assert.Equal(1, run.FloorIndex);
            Assert.Equal(run.TotalFloors, config.Floors.Count);

            Assert.Single(run.Backpack.Bags);
            Assert.Single(run.Backpack.Items);
            Assert.Equal(RunDefaults.InitialBagId, run.Backpack.Bags[0].DefId);
            Assert.Equal(RunDefaults.InitialWeaponId, run.Backpack.Items[0].DefId);
            Assert.Equal(PieceZone.Board, run.Backpack.Bags[0].Zone);
            Assert.Equal(PieceZone.Board, run.Backpack.Items[0].Zone);

            // The whole point of the starting kit: the first floor is startable at once.
            CombatLoadoutSnapshotAssertSingleWeapon(config, run);
            Assert.True(run.Backpack.TryValidate(out string? violation), violation);
            Assert.Equal(ItemCategory.ArcaneWeapon, weapon.Category);
            Assert.Equal(bag.Footprint.CellCount, run.Backpack.Bags[0].CellCount);
        }

        [Fact]
        public void Renting_pays_the_table_price_and_lands_the_piece_in_the_chest()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            int slot = RunFixture.CheapestAvailableSlot(run);
            int price = run.Stock[slot].Price;
            string offeredId = run.Stock[slot].OfferedId;
            int chestBefore = run.Backpack.Chest.OccupiedCount;

            Result<PurchaseOutcome> purchase = run.Purchase(slot);

            Assert.True(purchase.IsSuccess, purchase.Error.ToString());
            Assert.Equal(price, purchase.Value.Price);
            Assert.Equal(offeredId, purchase.Value.OfferedId);
            Assert.Equal(config.Economy.StartBia - price, run.Wallet.Bia);
            Assert.Equal(purchase.Value.Bia, run.Wallet.Bia);
            Assert.Equal(chestBefore + 1, run.Backpack.Chest.OccupiedCount);
            Assert.True(run.Backpack.TryGetPiece(purchase.Value.Piece, out PieceInstance? landed));
            Assert.Equal(PieceZone.Chest, landed!.Zone);
            Assert.Equal(offeredId, landed.DefId);
            Assert.True(run.Stock[slot].IsPurchased);
        }

        [Fact]
        public void Renting_the_same_slot_twice_only_works_once()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            int slot = RunFixture.CheapestAvailableSlot(run);

            Result<PurchaseOutcome> first = run.Purchase(slot);
            int purse = run.Wallet.Bia;
            int revision = run.Backpack.Revision;

            Result<PurchaseOutcome> second = run.Purchase(slot);

            Assert.True(first.IsSuccess, first.Error.ToString());
            Assert.True(second.IsFailure);
            Assert.Equal(ShopError.SlotPurchased, second.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.Equal(revision, run.Backpack.Revision);

            // The first rent landed one piece in the chest; the second took nothing, so
            // the bag holds the starting weapon plus exactly that one piece.
            Assert.Equal(2, run.Backpack.Items.Count);
            Assert.True(run.Backpack.TryGetPiece(first.Value.Piece, out PieceInstance? acquired));
            Assert.Equal(PieceZone.Chest, acquired!.Zone);
        }

        [Fact]
        public void A_full_chest_refuses_the_rent_without_taking_the_money()
        {
            IGameConfig config = RunFixture.Config();

            // Two chest slots, and the starting kit moved into them: the third piece has
            // nowhere to land. The chest size is data, so this is a real configuration
            // the run has to survive, not a synthetic argument.
            config.Economy.ChestSlots = 2;
            RunSession run = RunFixture.StartedRun(config);
            Assert.Equal(2, run.Backpack.Chest.SlotCount);

            Assert.True(run.TransferToChest(RunFixture.StartingWeapon(run)).IsSuccess);
            Assert.True(run.TransferToChest(RunFixture.StartingBag(run)).IsSuccess);
            Assert.True(run.Backpack.Chest.IsFull);

            int slot = RunFixture.CheapestAvailableSlot(run);
            int purse = run.Wallet.Bia;
            int revision = run.Backpack.Revision;

            Result<PurchaseOutcome> purchase = run.Purchase(slot);

            Assert.True(purchase.IsFailure);
            Assert.Equal(InventoryError.ChestFull, purchase.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.Equal(revision, run.Backpack.Revision);
            Assert.False(run.Stock[slot].IsPurchased);
        }

        [Fact]
        public void An_expensive_rent_leaves_the_purse_and_the_shop_untouched()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            // Two re-rolls take the purse to 3 bia, below every price in the pool. The
            // numbers come from `economy`; what matters is that two re-rolls leave less
            // than the cheapest offer costs.
            Assert.True(run.RefreshShop().IsSuccess);
            Assert.True(run.RefreshShop().IsSuccess);

            int slot = RunFixture.CheapestAvailableSlot(run);
            Assert.True(run.Wallet.Bia < run.Stock[slot].Price, "the fixture should be short of bia here");

            int purse = run.Wallet.Bia;
            int revision = run.Backpack.Revision;
            string[] stock = RunFixture.StockIds(run);

            Result<PurchaseOutcome> purchase = run.Purchase(slot);

            Assert.True(purchase.IsFailure);
            Assert.Equal(WalletError.InsufficientBia, purchase.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.Equal(revision, run.Backpack.Revision);
            Assert.Equal(stock, RunFixture.StockIds(run));
        }

        [Fact]
        public void Selling_pays_the_data_percentage_into_the_purse()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);
            Require(config.TryGetItem(RunDefaults.InitialWeaponId, out ItemConfig definition), "the weapon is in the table");

            int purse = run.Wallet.Bia;
            Result<RecycleOutcome> sold = run.Sell(weapon);

            Assert.True(sold.IsSuccess, sold.Error.ToString());
            Assert.Equal((definition.Price * config.Economy.SellBackPercent) / 100, sold.Value.PayoutBia);
            Assert.Equal(purse + sold.Value.PayoutBia, run.Wallet.Bia);
            Assert.Empty(run.Backpack.Items);
        }

        [Fact]
        public void A_sale_the_bag_rules_refuse_does_not_pay_out()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());
            PieceRef weapon = RunFixture.StartingWeapon(run);

            // The weapon sits on the starting bag. Selling the bag would strand it, so the
            // bag rule refuses the whole command and no bia may be credited.
            int purse = run.Wallet.Bia;
            Result<RecycleOutcome> sold = run.Sell(RunFixture.StartingBag(run));

            Assert.True(sold.IsFailure);
            Assert.Equal(InventoryError.ItemStranded, sold.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.True(run.Backpack.TryGetPiece(weapon, out PieceInstance? stillThere));
            Assert.Equal(PieceZone.Board, stillThere!.Zone);
        }

        [Fact]
        public void Wallet_spend_is_atomic_and_never_goes_negative()
        {
            var wallet = new Wallet(10);

            Assert.True(wallet.CanAfford(10));
            Assert.False(wallet.CanAfford(11));
            Assert.Equal(WalletError.InsufficientBia, wallet.TrySpend(11).Error.Code);
            Assert.Equal(10, wallet.Bia);

            Assert.True(wallet.TrySpend(10).IsSuccess);
            Assert.Equal(0, wallet.Bia);

            Assert.Equal(WalletError.InsufficientBia, wallet.TrySpend(1).Error.Code);
            Assert.Equal(0, wallet.Bia);

            // A negative spend is a caller bug, not an empty purse, and it too leaves the
            // balance alone.
            Error negative = wallet.TrySpend(-1).Error;
            Assert.Equal(WalletError.InsufficientBia, negative.Code);
            Assert.Equal(ErrorKind.Validation, negative.Kind);
            Assert.Equal(0, wallet.Bia);

            wallet.Credit(4);
            wallet.Credit(-100);
            Assert.Equal(4, wallet.Bia);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => new Wallet(-1));
        }

        private static void CombatLoadoutSnapshotAssertSingleWeapon(IGameConfig config, RunSession run)
        {
            var resolver = new LoadoutResolver(config, new SynergyEvaluator(config));
            var loadout = resolver.Resolve(run.Backpack);
            Assert.Single(loadout.Weapons);
            Assert.Equal("item.kali_flame_yant", run.Backpack.Items[0].DefId);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("test setup failed: " + message);
            }
        }
    }
}
