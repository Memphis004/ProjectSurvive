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
    /// Stage 09n: a shelf offer is explained with the same resolver a piece in the chest is, plus its
    /// price, its rentability and the combos it could join.
    /// </summary>
    /// <remarks>
    /// Every expected value is derived from the shipped tables in the test (the pool row, the item or
    /// bag row, the synergy rules), so a retune cannot make a correct readout look wrong. The rent
    /// verdict is compared against the real <c>Purchase</c> rather than against a number written here:
    /// the point of the stage is that the tooltip cannot disagree with the command.
    /// </remarks>
    public sealed class OfferReadoutTests
    {
        /// <summary>
        /// Every product in <c>shop_pool</c> is explained with the numbers its tier-1 copy in the chest
        /// resolves to — the same path, not a second calculator.
        /// </summary>
        /// <remarks>
        /// A single camp only offers four of the pool's rows, and four of them are restricted to floors
        /// 2–5, so the sweep walks two camps (floor 1 and floor 3) over a fixed set of seeds. The seeds
        /// are fixed, so the coverage is deterministic, and the assertion names any row the sweep could
        /// not reach rather than passing vacuously.
        /// </remarks>
        [Fact]
        public void Every_pool_row_is_explained_with_the_numbers_its_tier_one_copy_in_the_chest_resolves_to()
        {
            IGameConfig config = RunFixture.Config();
            var covered = new HashSet<string>();

            foreach (int floor in new[] { 1, 3 })
            {
                for (uint seed = 1; seed <= 24; seed++)
                {
                    RunSession run = RunFixture.StartedRun(config, seed);
                    while (run.FloorIndex < floor)
                    {
                        Assert.True(run.StartFloor().IsSuccess);
                        Result<FloorSettlement> settled = run.ApplyBattleResult(Won(run));
                        Assert.True(settled.IsSuccess, settled.Error.ToString());
                    }

                    // Enough for the whole panel: what is compared is the readout, not the purse.
                    run.Wallet.Credit(200);

                    for (int slot = 0; slot < run.Stock.SlotCount; slot++)
                    {
                        if (run.Stock[slot].IsEmpty || run.Stock[slot].IsPurchased)
                        {
                            continue;
                        }

                        covered.Add(run.Stock[slot].OfferedId);
                        AssertMatchesChestCopy(run, config, slot);
                    }
                }
            }

            for (int i = 0; i < config.ShopPool.Count; i++)
            {
                Assert.True(covered.Contains(config.ShopPool[i].OfferedId),
                    "the sweep never saw " + config.ShopPool[i].OfferedId + " on a shelf");
            }
        }

        /// <summary>
        /// The offer readout carries the shelf's own price and the piece's table facts, and it has no
        /// combo of its own — an offer is not in a layout, so its numbers are the piece's own.
        /// </summary>
        [Fact]
        public void The_readout_carries_the_price_the_tables_weight_and_no_active_combo()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                ShopSlot shelf = run.Stock[slot];
                if (shelf.IsEmpty)
                {
                    continue;
                }

                Result<OfferReadout> readout = run.ExplainOffer(slot);
                Assert.True(readout.IsSuccess, readout.Error.ToString());

                OfferReadout offer = readout.Value;
                Assert.Equal(slot, offer.SlotIndex);
                Assert.Equal(shelf.OfferedId, offer.DefId);
                Assert.Equal(shelf.Kind, offer.Kind);
                Assert.Equal(shelf.Price, offer.Price);
                Assert.Equal(1, offer.Tier);

                if (offer.Kind == PieceKind.Bag)
                {
                    Assert.True(config.TryGetBag(offer.DefId, out BagConfig bag));
                    Assert.Equal(bag.NameKey, offer.NameKey);
                    Assert.Equal(bag.Weight, offer.Weight, 4);
                    Assert.True(offer.Piece.IsBag);
                    Assert.False(offer.Piece.Casts, "a bag has no damage number");
                    Assert.Equal(string.Empty, offer.DescriptionKey);
                    Assert.Empty(offer.Combos);
                }
                else
                {
                    Assert.True(config.TryGetItem(offer.DefId, out ItemConfig item));
                    Assert.Equal(item.NameKey, offer.NameKey);
                    Assert.Equal(item.DescriptionKey, offer.DescriptionKey);
                    Assert.Equal(item.Weight, offer.Weight, 4);
                    Assert.False(offer.Piece.IsBag);
                    Assert.False(offer.Piece.InLoadout, "a shelf offer is in no layout");
                    Assert.Equal(0, offer.Piece.ActiveSynergyCount);
                    Assert.Equal(1f, offer.Piece.DamageMultiplier);
                    Assert.Equal(1f, offer.Piece.CooldownMultiplier);
                    Assert.Empty(offer.Piece.ComboRuleIds);
                    Assert.Empty(offer.Piece.Steps);
                }
            }
        }

        /// <summary>
        /// The combos an offer could join are the <c>synergy</c> rules that name one of its tags, with
        /// the partner tag's pieces listed by their real names — a listing, not a second evaluation.
        /// </summary>
        [Fact]
        public void The_combo_list_is_the_synergy_rows_this_piece_could_pair_in()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            run.Wallet.Credit(200);

            int checkedPieces = 0;

            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                Result<OfferReadout> readout = run.ExplainOffer(slot);
                Assert.True(readout.IsSuccess, readout.Error.ToString());
                OfferReadout offer = readout.Value;

                if (offer.Kind == PieceKind.Bag)
                {
                    Assert.Empty(offer.Combos);
                    continue;
                }

                Assert.True(config.TryGetItem(offer.DefId, out ItemConfig item));
                List<SynergyConfig> expected = ExpectedCombos(config, item);

                Assert.Equal(expected.Count, offer.Combos.Count);

                for (int i = 0; i < expected.Count; i++)
                {
                    SynergyConfig rule = expected[i];
                    OfferCombo combo = offer.Combos[i];

                    Assert.Equal(rule.Id, combo.RuleId);
                    Assert.Equal(rule.NameKey, combo.NameKey);

                    string partnerTag = HasTag(item, rule.TagA) ? rule.TagB : rule.TagA;
                    Assert.Equal(partnerTag, combo.PartnerTag);

                    // Every named partner really carries the tag, and none of them is the offer itself.
                    Assert.NotEmpty(combo.PartnerNameKeys);
                    for (int p = 0; p < combo.PartnerNameKeys.Count; p++)
                    {
                        Assert.True(CarriesNameKey(config, combo.PartnerNameKeys[p], partnerTag),
                            combo.PartnerNameKeys[p] + " does not carry " + partnerTag);
                        Assert.NotEqual(item.NameKey, combo.PartnerNameKeys[p]);
                    }

                    Assert.Equal(PartnerCount(config, item, partnerTag), combo.PartnerCount);
                }

                if (expected.Count > 0)
                {
                    checkedPieces++;
                }
            }

            Assert.True(checkedPieces > 0, "no offered item could pair with anything, so the listing was never exercised");
        }

        /// <summary>
        /// A rented or empty shelf has nothing to explain: the run says so with the same codes
        /// <c>Purchase</c> uses, rather than handing back a blank picture.
        /// </summary>
        [Fact]
        public void A_rented_or_empty_shelf_has_no_readout()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());
            int slot = RunFixture.CheapestAvailableSlot(run);

            Assert.True(run.Purchase(slot).IsSuccess);

            Result<OfferReadout> rented = run.ExplainOffer(slot);
            Assert.True(rented.IsFailure);
            Assert.Equal(ShopError.SlotPurchased, rented.Error.Code);

            Result<OfferReadout> outOfRange = run.ExplainOffer(run.Stock.SlotCount);
            Assert.True(outOfRange.IsFailure);
            Assert.Equal(ShopError.UnknownSlot, outOfRange.Error.Code);

            // Before the run starts there is no camp at all.
            RunSession notStarted = RunFixture.NewRun(RunFixture.Config(), RunFixture.DefaultSeed);
            Result<OfferReadout> noCamp = notStarted.ExplainOffer(0);
            Assert.True(noCamp.IsFailure);
            Assert.Equal(RunError.WrongState, noCamp.Error.Code);
        }

        /// <summary>
        /// "เบี้ยไม่พอ" is not a sentence the tooltip invents: it is the code the real command returns,
        /// and the refused rent moves nothing.
        /// </summary>
        [Fact]
        public void A_short_purse_reads_the_code_purchase_returns_and_purchase_changes_nothing()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            run.Wallet.Credit(100);

            // An empty purse: every shelf is out of reach and none was rented.
            run.Wallet.TrySpend(run.Wallet.Bia);

            int target = -1;
            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                if (!run.Stock[slot].IsEmpty && run.Stock[slot].Price > run.Wallet.Bia)
                {
                    target = slot;
                    break;
                }
            }

            Assert.True(target >= 0, "no shelf costs more than an empty purse");
            Result<OfferReadout> readout = run.ExplainOffer(target);

            Assert.True(readout.IsSuccess, readout.Error.ToString());
            Assert.False(readout.Value.Rentable);
            Assert.Equal(WalletError.InsufficientBia, readout.Value.RefusalCode);
            Assert.Equal(0, run.Wallet.Bia);

            uint hash = run.ComputeStateHash();
            Result<PurchaseOutcome> purchase = run.Purchase(target);

            Assert.True(purchase.IsFailure);
            Assert.Equal(readout.Value.RefusalCode, purchase.Error.Code);
            Assert.Equal(hash, run.ComputeStateHash());
        }

        /// <summary>
        /// A full chest is the other refusal, and it reads the same way: the tooltip's verdict and the
        /// command's failure are one code, and the failed rent leaves the run byte-identical.
        /// </summary>
        [Fact]
        public void A_full_chest_reads_the_code_purchase_returns_and_purchase_changes_nothing()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            run.Wallet.Credit(100);

            while (!run.Backpack.Chest.IsFull)
            {
                Result<PieceRef> filled = run.Backpack.AddItem("item.boiled_egg");
                Assert.True(filled.IsSuccess, filled.Error.ToString());
            }

            int slot = RunFixture.CheapestAvailableSlot(run);
            Result<OfferReadout> readout = run.ExplainOffer(slot);

            Assert.True(readout.IsSuccess, readout.Error.ToString());
            Assert.False(readout.Value.Rentable);
            Assert.Equal(InventoryError.ChestFull, readout.Value.RefusalCode);

            uint hash = run.ComputeStateHash();
            int purse = run.Wallet.Bia;
            Result<PurchaseOutcome> purchase = run.Purchase(slot);

            Assert.True(purchase.IsFailure);
            Assert.Equal(readout.Value.RefusalCode, purchase.Error.Code);
            Assert.Equal(purse, run.Wallet.Bia);
            Assert.True(run.Stock[slot].IsPurchased == false, "a refused rent must not mark the shelf");
            Assert.Equal(hash, run.ComputeStateHash());
        }

        /// <summary>
        /// Asking for an explanation is pure observation: no revision moves and no event is emitted,
        /// however often a pointer asks.
        /// </summary>
        [Fact]
        public void Explaining_an_offer_changes_nothing()
        {
            RunSession run = RunFixture.StartedRun(RunFixture.Config());

            run.ClearEvents();
            uint before = run.ComputeStateHash();

            for (int repeat = 0; repeat < 50; repeat++)
            {
                for (int slot = 0; slot < run.Stock.SlotCount; slot++)
                {
                    run.ExplainOffer(slot);
                    run.CheckPurchase(slot);
                }
            }

            Assert.Equal(before, run.ComputeStateHash());
            Assert.Empty(run.Events);
        }

        /// <summary>Explains one shelf and compares it with the same piece bought into the chest.</summary>
        private static void AssertMatchesChestCopy(RunSession run, IGameConfig config, int slot)
        {
            ShopSlot shelf = run.Stock[slot];
            string offeredId = shelf.OfferedId;

            Result<OfferReadout> readout = run.ExplainOffer(slot);
            Assert.True(readout.IsSuccess, offeredId + ": " + readout.Error);
            OfferReadout offer = readout.Value;

            Assert.Equal(shelf.Price, offer.Price);
            Assert.Equal(shelf.Kind, offer.Kind);

            if (offer.Kind == PieceKind.Bag)
            {
                Assert.True(config.TryGetBag(offeredId, out BagConfig bag));
                Assert.Equal(bag.NameKey, offer.NameKey);
                Assert.True(offer.Piece.IsBag);
                Assert.False(offer.Piece.Casts);
                return;
            }

            Assert.True(config.TryGetItem(offeredId, out ItemConfig item));

            // The chest copy: what the same piece resolves to once it is the run's own.
            Result<PieceRef> added = run.Backpack.AddItem(offeredId);
            Assert.True(added.IsSuccess, offeredId + ": " + added.Error);
            Result<PieceReadout> chest = run.ExplainPiece(added.Value);

            Assert.True(chest.IsSuccess, chest.Error.ToString());
            PieceReadout copy = chest.Value;

            Assert.False(copy.InLoadout, "a chest copy is not in a layout either");
            Assert.Equal(offer.Tier, copy.Tier);
            Assert.Equal(copy.DefId, offer.DefId);
            Assert.Equal(copy.SkillId, offer.Piece.SkillId);
            Assert.Equal(copy.Casts, offer.Piece.Casts);
            Assert.Equal(copy.Base.Damage, offer.Piece.Base.Damage);
            Assert.Equal(copy.Base.CooldownSeconds, offer.Piece.Base.CooldownSeconds, 4);
            Assert.Equal(copy.Base.MpCost, offer.Piece.Base.MpCost);
            Assert.Equal(copy.Resolved.Damage, offer.Piece.Resolved.Damage);
            Assert.Equal(copy.Resolved.CooldownSeconds, offer.Piece.Resolved.CooldownSeconds, 4);
            Assert.Equal(copy.Resolved.MpCost, offer.Piece.Resolved.MpCost);
            Assert.Equal(copy.CooldownAtMinimum, offer.Piece.CooldownAtMinimum);
            Assert.Equal(copy.IsOffering, offer.Piece.IsOffering);
            Assert.Equal(copy.HasCharmEffect, offer.Piece.HasCharmEffect);
            Assert.Equal(copy.OfferingUses, offer.Piece.OfferingUses);
        }

        /// <summary>The rules of the synergy table that name one of the item's tags, in table order.</summary>
        private static List<SynergyConfig> ExpectedCombos(IGameConfig config, ItemConfig item)
        {
            var rules = new List<SynergyConfig>();
            for (int i = 0; i < config.Synergies.Count; i++)
            {
                SynergyConfig rule = config.Synergies[i];
                if (rule.Kind != SynergyKind.AdjacentPair && rule.Kind != SynergyKind.SameRow)
                {
                    continue;
                }

                bool carriesA = HasTag(item, rule.TagA);
                bool carriesB = HasTag(item, rule.TagB);
                if (!carriesA && !carriesB)
                {
                    continue;
                }

                string partnerTag = carriesA ? rule.TagB : rule.TagA;
                if (string.IsNullOrEmpty(partnerTag) || PartnerCount(config, item, partnerTag) == 0)
                {
                    continue;
                }

                rules.Add(rule);
            }

            return rules;
        }

        private static int PartnerCount(IGameConfig config, ItemConfig item, string tag)
        {
            int count = 0;
            for (int i = 0; i < config.Items.Count; i++)
            {
                ItemConfig other = config.Items[i];
                if (other.Id != item.Id && HasTag(other, tag))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CarriesNameKey(IGameConfig config, string nameKey, string tag)
        {
            for (int i = 0; i < config.Items.Count; i++)
            {
                ItemConfig other = config.Items[i];
                if (other.NameKey == nameKey && HasTag(other, tag))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTag(ItemConfig item, string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            for (int i = 0; i < item.Tags.Count; i++)
            {
                if (item.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }

        private static BattleResult Won(RunSession run) => new BattleResult
        {
            Outcome = BattlePhase.Won,
            EndReason = BattleEndReason.SurvivedAllWaves,
            FinalHp = run.MaxHp,
            RemainingConsumables = new List<int>(),
        };
    }
}
