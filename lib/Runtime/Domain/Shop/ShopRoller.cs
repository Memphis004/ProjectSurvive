using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Shop
{
    /// <summary>
    /// Rolls a floor's shop stock out of the <c>shop_pool</c> table, by weight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pool is filtered by the floor's <c>index</c> (<c>floor_min</c> / <c>floor_max</c>)
    /// and drawn without replacement, so one camp visit never offers the same piece
    /// twice; the file order of the table is the draw order, which keeps
    /// \"seed + state เดิม ให้สินค้าเดิม\" true without a sort.
    /// </para>
    /// <para>
    /// Randomness comes in as an <see cref="IRandomSource"/> — the run passes its
    /// <b>shop</b> stream, never the battle one (AGENTS rule 6). This class holds no
    /// state between calls, so two runs with the same seed and the same sequence of
    /// rolls produce the same offers.
    /// </para>
    /// </remarks>
    public sealed class ShopRoller
    {
        private readonly IGameConfig _config;

        /// <summary>Creates a roller over one config snapshot.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="config"/> is null.</exception>
        public ShopRoller(IGameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Re-rolls every slot that is not pinned, and returns how many it filled. A
        /// rented slot is re-rolled too — the re-roll was paid for — and loses its rent
        /// flag; a pinned slot keeps its offer exactly as it is
        /// (\"ล็อกสินค้าที่อยากเก็บไว้ก่อนสุ่มได้\").
        /// </summary>
        /// <param name="stock">The stock to fill.</param>
        /// <param name="floorIndex">1-based run order of the floor being entered, matched against the pool's floor band.</param>
        /// <param name="rng">The shop random stream.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public int Roll(ShopStock stock, int floorIndex, IRandomSource rng) =>
            Roll(stock, Eligible(floorIndex), rng);

        /// <summary>
        /// Re-rolls every slot that is not pinned from an explicit pool, and returns how
        /// many it filled. The overload exists so a caller (or a test) can draw from a
        /// subset — and so the "fewer offers than slots" rule is reachable.
        /// </summary>
        /// <param name="stock">The stock to fill.</param>
        /// <param name="pool">The candidate rows, in draw order.</param>
        /// <param name="rng">The shop random stream.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public int Roll(ShopStock stock, IReadOnlyList<ShopPoolEntryConfig> pool, IRandomSource rng)
        {
            if (stock is null)
            {
                throw new ArgumentNullException(nameof(stock));
            }

            if (pool is null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (rng is null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var remaining = new List<ShopPoolEntryConfig>(pool);
            int filled = 0;

            for (int i = 0; i < stock.SlotCount; i++)
            {
                ShopSlot slot = stock[i];
                if (!slot.CanBeRerolled)
                {
                    continue;
                }

                // A re-roll replaces what the slot held, rent flag included. Clearing
                // before the pool check is what leaves a slot with no candidate simply
                // empty rather than empty-and-rented.
                slot.BeginReroll();

                if (remaining.Count == 0)
                {
                    // Fewer eligible offers than slots (shipped data has 10+ on every
                    // floor). Leaving the slot empty is honest; inventing a duplicate
                    // would break "หนึ่งชิ้นต่อช่อง".
                    continue;
                }

                ShopPoolEntryConfig picked = Pick(remaining, rng);
                remaining.Remove(picked);

                if (!TryResolve(picked.OfferedId, out PieceKind kind, out int price))
                {
                    continue;
                }

                slot.Fill(kind, picked.OfferedId, price);
                filled++;
            }

            return filled;
        }

        /// <summary>Every pool row whose floor band contains <paramref name="floorIndex"/>, in table order.</summary>
        public List<ShopPoolEntryConfig> Eligible(int floorIndex)
        {
            var eligible = new List<ShopPoolEntryConfig>();
            IReadOnlyList<ShopPoolEntryConfig> pool = _config.ShopPool;
            for (int i = 0; i < pool.Count; i++)
            {
                ShopPoolEntryConfig entry = pool[i];
                if (entry.Weight > 0 && floorIndex >= entry.FloorMin && floorIndex <= entry.FloorMax)
                {
                    eligible.Add(entry);
                }
            }

            return eligible;
        }

        /// <summary>
        /// The price and kind of an offered id, or false when the id is in neither
        /// the bag nor the item table.
        /// </summary>
        public bool TryResolve(string offeredId, out PieceKind kind, out int price)
        {
            if (!string.IsNullOrEmpty(offeredId))
            {
                if (_config.TryGetBag(offeredId, out BagConfig bag))
                {
                    kind = PieceKind.Bag;
                    price = bag.Price;
                    return true;
                }

                if (_config.TryGetItem(offeredId, out ItemConfig item))
                {
                    kind = PieceKind.Item;
                    price = item.Price;
                    return true;
                }
            }

            kind = PieceKind.None;
            price = 0;
            return false;
        }

        /// <summary>
        /// Weighted draw, no modulo bias beyond what <see cref="RandomSourceExtensions.NextInt"/>
        /// already removes. Picked entries are removed by the caller so one roll never
        /// repeats a piece.
        /// </summary>
        private static ShopPoolEntryConfig Pick(List<ShopPoolEntryConfig> pool, IRandomSource rng)
        {
            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                total += pool[i].Weight;
            }

            int roll = rng.NextInt(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= pool[i].Weight;
                if (roll < 0)
                {
                    return pool[i];
                }
            }

            return pool[pool.Count - 1];
        }
    }
}
