using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Shop;

namespace ProjectSurvive.Sim
{
    /// <summary>What one camp visit did, for the balance report.</summary>
    internal struct CampReport
    {
        internal int Purchases;

        internal int Merges;

        internal int Placed;

        internal int LeftInChest;
    }

    /// <summary>
    /// The bot's camp policy: <b>greedy</b> — rent whatever it can afford, fold equal
    /// pieces together, and put what fits on the bag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Greedy means deliberately short-sighted: cheapest affordable offer first (so the
    /// number of pieces bought is maximised), merge every equal pair, and place pieces in
    /// creation order at the first cell and rotation that fits. It never re-rolls the
    /// shop, never sells and never plans a combo — those are build-quality decisions, and
    /// the balance pass (Stage 11) is where they get made. What this bot is for is
    /// <b>pressure</b>: it plays hundreds of floors with a plausible build so the CSV
    /// describes the floor tables rather than one hand-tuned loadout.
    /// </para>
    /// <para>
    /// Every command goes through the run's own API, so a rejection is the run's decision
    /// and leaves state untouched (AGENTS rule 7) — the bot never has to be trusted about
    /// legality, and an illegal placement is simply the next candidate.
    /// </para>
    /// </remarks>
    internal sealed class CampBot
    {
        private readonly IGameConfig _config;

        internal CampBot(IGameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Plays one camp visit on <paramref name="run"/>.</summary>
        internal CampReport PlayCamp(RunSession run)
        {
            var report = new CampReport();

            report.Purchases = BuyWhatIsAffordable(run);
            report.Merges = MergeEquals(run);
            report.Placed = PlaceEverything(run);
            report.Merges += MergeEquals(run);

            int inChest = 0;
            IReadOnlyList<BagPieceInstance> bags = run.Backpack.Bags;
            for (int i = 0; i < bags.Count; i++)
            {
                if (bags[i].Zone == PieceZone.Chest)
                {
                    inChest++;
                }
            }

            IReadOnlyList<ItemInstance> items = run.Backpack.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Zone == PieceZone.Chest)
                {
                    inChest++;
                }
            }

            report.LeftInChest = inChest;
            return report;
        }

        /// <summary>Rents the cheapest affordable offer, over and over, until none is left.</summary>
        private int BuyWhatIsAffordable(RunSession run)
        {
            int purchases = 0;

            while (true)
            {
                int slot = CheapestAffordableSlot(run);
                if (slot < 0)
                {
                    return purchases;
                }

                if (run.Purchase(slot).IsFailure)
                {
                    // The run refused a purchase it advertised as buyable: stop rather than
                    // spin. The CSV will show the pieces that were never bought.
                    return purchases;
                }

                purchases++;
            }
        }

        private int CheapestAffordableSlot(RunSession run)
        {
            ShopStock stock = run.Stock;
            int best = -1;
            int bestPrice = int.MaxValue;

            for (int i = 0; i < stock.SlotCount; i++)
            {
                ShopSlot slot = stock[i];
                if (slot.IsEmpty || slot.IsPurchased || slot.Price > run.Wallet.Bia)
                {
                    continue;
                }

                if (slot.Price < bestPrice)
                {
                    bestPrice = slot.Price;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Merges every pair of equal, mergeable items, lowest instance ids first.</summary>
        private static int MergeEquals(RunSession run)
        {
            int merges = 0;
            int maxTier = run.Backpack.MaxTier;

            while (true)
            {
                if (!TryFindMergePair(run, maxTier, out PieceRef source, out PieceRef target))
                {
                    return merges;
                }

                if (run.MergeItems(source, target).IsFailure)
                {
                    return merges;
                }

                merges++;
            }
        }

        private static bool TryFindMergePair(RunSession run, int maxTier, out PieceRef source, out PieceRef target)
        {
            source = default;
            target = default;

            List<ItemInstance> items = CollectMergeableItems(run);
            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance candidate = items[i];
                if (candidate.Tier >= maxTier)
                {
                    continue;
                }

                for (int j = i + 1; j < items.Count; j++)
                {
                    ItemInstance other = items[j];
                    if (other.Tier != candidate.Tier)
                    {
                        continue;
                    }

                    if (!string.Equals(other.ItemId, candidate.ItemId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // The later instance folds into the earlier one: a fixed rule, so two
                    // runs with the same pieces merge the same way.
                    target = candidate.Ref;
                    source = other.Ref;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Places every chest piece, bags first (items need a bag's ActiveSlots), at the
        /// first cell and rotation the run accepts.
        /// </summary>
        private int PlaceEverything(RunSession run)
        {
            int placed = 0;
            var rotations = new[]
            {
                Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270,
            };

            List<PieceInstance> pending = CollectItems(run);
            List<PieceInstance> bags = new List<PieceInstance>();
            IReadOnlyList<BagPieceInstance> bagPieces = run.Backpack.Bags;
            for (int i = 0; i < bagPieces.Count; i++)
            {
                if (bagPieces[i].Zone == PieceZone.Chest)
                {
                    bags.Add(bagPieces[i]);
                }
            }

            for (int order = 0; order < 2; order++)
            {
                List<PieceInstance> group = order == 0 ? bags : pending;

                for (int i = 0; i < group.Count; i++)
                {
                    PieceInstance piece = group[i];
                    if (TryPlace(run, piece, rotations))
                    {
                        placed++;
                    }
                }
            }

            return placed;
        }

        private static bool TryPlace(RunSession run, PieceInstance piece, Rotation[] rotations)
        {
            Board board = run.Backpack.Board;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var origin = new CellOffset(x, y);
                    for (int r = 0; r < rotations.Length; r++)
                    {
                        if (run.MovePiece(piece.Ref, origin, rotations[r]).IsSuccess)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Every item the run holds, board and chest, in instance order. Merging works
        /// across the two zones — the survivor keeps its own placement — so the bot looks
        /// at both rather than only at what is still waiting in the chest.
        /// </summary>
        private static List<ItemInstance> CollectMergeableItems(RunSession run)
        {
            var all = new List<ItemInstance>(run.Backpack.Items);
            all.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return all;
        }

        private static List<PieceInstance> CollectItems(RunSession run)
        {
            var chest = new List<PieceInstance>();
            IReadOnlyList<ItemInstance> items = run.Backpack.Items;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Zone == PieceZone.Chest)
                {
                    chest.Add(items[i]);
                }
            }

            chest.Sort((left, right) => left.InstanceId.CompareTo(right.InstanceId));
            return chest;
        }
    }
}
