using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// What a merge would show the player before it happens: up to
    /// <c>constant.upgrade_choice_count</c> upgrade variants to pick from, or none at
    /// all (Stage 09f: "เลือก 1 จาก 3").
    /// </summary>
    /// <remarks>
    /// <para>
    /// An empty <see cref="Options"/> is not a failure: it is the documented fallback for a
    /// piece no variant matches (charms, tomes, offerings, familiars, bags), which folds
    /// immediately exactly as it did before this stage. A non-empty list always has at
    /// least <c>constant.upgrade_choice_count</c> entries in shipped data — the data
    /// validator refuses a pool of one or two.
    /// </para>
    /// <para>
    /// This is a <b>read model</b>: it is produced without touching the backpack, and the
    /// command that accepts a merge recomputes it, so a caller can never confirm a choice
    /// the current state would not offer.
    /// </para>
    /// </remarks>
    public sealed class MergeOffer
    {
        /// <summary>Creates an offer; prefer <see cref="UpgradePool.Offer"/>.</summary>
        public MergeOffer(string itemId, int fromTier, int toTier, IReadOnlyList<UpgradeVariantConfig> options)
        {
            ItemId = itemId ?? throw new ArgumentNullException(nameof(itemId));
            FromTier = fromTier;
            ToTier = toTier;
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>The item id both pieces share.</summary>
        public string ItemId { get; }

        /// <summary>The tier the two pieces are at.</summary>
        public int FromTier { get; }

        /// <summary>The tier the survivor would reach.</summary>
        public int ToTier { get; }

        /// <summary>The choices, in the order they should be shown. Empty means "no chooser".</summary>
        public IReadOnlyList<UpgradeVariantConfig> Options { get; }

        /// <summary>True when the player has a choice to make.</summary>
        public bool HasChoices => Options.Count > 0;

        /// <summary>True when <paramref name="variantId"/> is one of the offered choices.</summary>
        /// <remarks>
        /// The membership test a command needs, so "the id was not in the offer" is one
        /// rule in one place instead of a loop at every call site.
        /// </remarks>
        public bool Contains(string variantId)
        {
            if (string.IsNullOrEmpty(variantId))
            {
                return false;
            }

            for (int i = 0; i < Options.Count; i++)
            {
                if (string.Equals(Options[i].Id, variantId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public override string ToString() =>
            "offer " + ItemId + " t" + FromTier + "->" + ToTier + ": " + Options.Count + " choice(s)";
    }

    /// <summary>
    /// Which upgrade variants apply to a piece at a tier step, and which of them are offered.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A variant matches by <b>category or tag</b>, never by item id, so one row covers a
    /// family: an <c>ArcaneWeapon</c>-scoped variant applies to every arcane weapon, and a
    /// <c>fire</c>-scoped one only to the pieces carrying that tag. A piece's pool is the
    /// union of the two, filtered by the step's tier.
    /// </para>
    /// <para>
    /// <b>The draw is a pure function, not a consumed stream.</b> The choices for a merge
    /// come from a generator seeded by <c>(run seed, merge ordinal, item id, from tier)</c>,
    /// so asking twice about the same state gives the same three, and undoing a merge and
    /// repeating it cannot re-roll the offer. That is why the run has a fourth random
    /// stream family (<see cref="RandomStreamKind.Upgrade"/>) that is <i>derived from</i>
    /// rather than drawn from (ADR 0021).
    /// </para>
    /// </remarks>
    public static class UpgradePool
    {
        /// <summary>
        /// Fills <paramref name="pool"/> with the variants that apply to
        /// <paramref name="definition"/> at <paramref name="fromTier"/>, in data order.
        /// </summary>
        public static void Collect(
            IGameConfig config, ItemConfig definition, int fromTier, List<UpgradeVariantConfig> pool)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (definition is null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (pool is null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            pool.Clear();

            IReadOnlyList<UpgradeVariantConfig> variants = config.UpgradeVariants;
            for (int i = 0; i < variants.Count; i++)
            {
                UpgradeVariantConfig variant = variants[i];
                if (variant.FromTier != fromTier)
                {
                    continue;
                }

                if (Applies(variant, definition))
                {
                    pool.Add(variant);
                }
            }
        }

        /// <summary>
        /// The offer for one merge: an empty choice list when nothing applies (the pair
        /// then folds immediately), otherwise at most <c>constant.upgrade_choice_count</c>
        /// choices drawn from the pool.
        /// </summary>
        /// <remarks>
        /// <paramref name="choices"/> is written, never appended to, and is the caller's
        /// scratch list — the returned <see cref="MergeOffer"/> reads it directly, so a
        /// caller that keeps the offer alive keeps the list alive with it.
        /// </remarks>
        public static MergeOffer Offer(
            IGameConfig config,
            string itemId,
            ItemConfig definition,
            int fromTier,
            uint masterSeed,
            int mergeOrdinal,
            List<UpgradeVariantConfig> pool,
            List<UpgradeVariantConfig> choices)
        {
            Collect(config, definition, fromTier, pool);

            Draw(
                masterSeed,
                mergeOrdinal,
                itemId,
                fromTier,
                config.Constant.UpgradeChoiceCount,
                pool,
                choices);

            return new MergeOffer(itemId, fromTier, fromTier + 1, choices);
        }

        /// <summary>
        /// Draws up to <paramref name="choiceCount"/> distinct variants from
        /// <paramref name="pool"/>, weighted and without replacement, deterministically.
        /// </summary>
        /// <remarks>
        /// Removing a drawn variant from the candidate set keeps the remaining weights in
        /// proportion, so "the heaviest is most likely first" survives, and two draws from
        /// two pools of equal weights are still a permutation rather than a repeat.
        /// </remarks>
        public static void Draw(
            uint masterSeed,
            int mergeOrdinal,
            string itemId,
            int fromTier,
            int choiceCount,
            IReadOnlyList<UpgradeVariantConfig> pool,
            List<UpgradeVariantConfig> choices)
        {
            if (pool is null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            if (choices is null)
            {
                throw new ArgumentNullException(nameof(choices));
            }

            choices.Clear();

            if (pool.Count == 0 || choiceCount < 1)
            {
                return;
            }

            int wanted = pool.Count < choiceCount ? pool.Count : choiceCount;
            var random = new Xoshiro128(DeriveSeed(masterSeed, mergeOrdinal, itemId, fromTier));

            // A copy, because the pool belongs to the caller (and to the config).
            var remaining = new List<UpgradeVariantConfig>(pool);

            while (choices.Count < wanted && remaining.Count > 0)
            {
                long total = 0;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int weight = remaining[i].Weight;
                    if (weight > 0)
                    {
                        total += weight;
                    }
                }

                int picked = 0;
                if (total > 0)
                {
                    int roll = random.NextInt(0, total > int.MaxValue ? int.MaxValue : (int)total);
                    int cumulative = 0;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        int weight = remaining[i].Weight;
                        if (weight <= 0)
                        {
                            continue;
                        }

                        cumulative += weight;
                        if (roll < cumulative)
                        {
                            picked = i;
                            break;
                        }
                    }
                }

                choices.Add(remaining[picked]);
                remaining.RemoveAt(picked);
            }
        }

        /// <summary>
        /// The generator seed of one offer: a pure function of the run seed, how many
        /// merges the run has already confirmed, the item and the tier step.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every input is part of the key on purpose. The <b>ordinal</b> makes two merges of
        /// the same weapon in one run offer different choices, and being part of the undoable
        /// state is what makes repeating a merge reproduce its own offer instead of
        /// re-rolling it. The <b>item id</b> keeps two different weapons from sharing a
        /// draw, and the <b>tier step</b> keeps a 2 → 3 merge from repeating the 1 → 2 offer.
        /// </para>
        /// <para>
        /// Mixing is xor + multiply of well-separated salts (the same technique
        /// <c>RandomStreams.DeriveStreamSeed</c> uses), and the result is fed through
        /// <see cref="Xoshiro128"/>'s own seed expansion, so the avalanche step is not
        /// reimplemented here.
        /// </para>
        /// </remarks>
        public static uint DeriveSeed(uint masterSeed, int mergeOrdinal, string itemId, int fromTier)
        {
            unchecked
            {
                uint seed = RandomStreams.DeriveStreamSeed(masterSeed, RandomStreamKind.Upgrade);
                seed ^= (uint)mergeOrdinal * 0x9E37_79B9u;
                seed ^= (uint)fromTier * 0x85EB_CA6Bu;
                seed ^= HashId(itemId);
                return seed;
            }
        }

        private static bool Applies(UpgradeVariantConfig variant, ItemConfig definition)
        {
            if (!string.IsNullOrEmpty(variant.Category))
            {
                return UpgradeVocabulary.TryParseCategory(variant.Category, out ItemCategory category)
                    && category == definition.Category;
            }

            if (string.IsNullOrEmpty(variant.Tag))
            {
                return false;
            }

            IReadOnlyList<string> tags = definition.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], variant.Tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>FNV-1a over the id's chars: deterministic, allocation-free, order-sensitive.</summary>
        private static uint HashId(string id)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < id.Length; i++)
                {
                    hash ^= id[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }
    }
}
