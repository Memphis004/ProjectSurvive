using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// What the combo rules do to one item's numbers, as multipliers around 1.
    /// </summary>
    /// <remarks>
    /// Multipliers, not deltas, so two rules that touch the same item compose without
    /// either needing to know the other exists. <see cref="CooldownMultiplier"/> folds
    /// both <see cref="SynergyStat.Cooldown"/> (already a negative percent) and
    /// <see cref="SynergyStat.AttackSpeed"/> (a positive percent, which <i>shortens</i>
    /// the cooldown) — that conversion is the only place the two signs meet.
    /// </remarks>
    public readonly struct ItemModifiers
    {
        internal ItemModifiers(float damageMultiplier, float cooldownMultiplier, int activeSynergyCount)
        {
            DamageMultiplier = damageMultiplier;
            CooldownMultiplier = cooldownMultiplier;
            ActiveSynergyCount = activeSynergyCount;
        }

        /// <summary>1 = unchanged, 1.2 = +20 % damage.</summary>
        public float DamageMultiplier { get; }

        /// <summary>1 = unchanged, 0.85 = 15 % faster.</summary>
        public float CooldownMultiplier { get; }

        /// <summary>How many rule applications landed on this item (for the UI tooltip).</summary>
        public int ActiveSynergyCount { get; }

        /// <summary>The neutral modifiers, for an item no rule touched.</summary>
        public static ItemModifiers None => new ItemModifiers(1f, 1f, 0);

        /// <inheritdoc />
        public override string ToString() =>
            "dmg x" + DamageMultiplier + " cd x" + CooldownMultiplier + " (" + ActiveSynergyCount + ")";
    }

    /// <summary>One rule application: which rule hit which item because of which neighbour.</summary>
    /// <remarks>
    /// Recorded so the Stage 09 tooltip can highlight the connected cells (GDD: "synergy
    /// ที่ทำงานอยู่ (highlight ช่องที่เชื่อมกัน)") without re-deriving the rules in a View.
    /// </remarks>
    public readonly struct ActiveSynergy
    {
        internal ActiveSynergy(string ruleId, int sourceInstanceId, int targetInstanceId, SynergyStat stat, float valuePercent)
        {
            RuleId = ruleId;
            SourceInstanceId = sourceInstanceId;
            TargetInstanceId = targetInstanceId;
            Stat = stat;
            ValuePercent = valuePercent;
        }

        /// <summary>The <c>synergy</c> row that fired.</summary>
        public string RuleId { get; }

        /// <summary>Instance id of the piece that provided the condition (a takrut, a tome, a curse piece, a bag).</summary>
        public int SourceInstanceId { get; }

        /// <summary>Instance id of the piece that received the bonus or the decay.</summary>
        public int TargetInstanceId { get; }

        /// <summary>Which stat the rule moves.</summary>
        public SynergyStat Stat { get; }

        /// <summary>The applied percent (already signed: cooldown is negative, decay is negative).</summary>
        public float ValuePercent { get; }

        /// <inheritdoc />
        public override string ToString() =>
            RuleId + " " + SourceInstanceId + "->" + TargetInstanceId + " " + ValuePercent + "%";
    }

    /// <summary>All the combo effects of one backpack layout.</summary>
    public sealed class SynergyEvaluation
    {
        private readonly Dictionary<int, ItemModifiers> _modifiers;
        private readonly List<ActiveSynergy> _active;

        internal SynergyEvaluation(Dictionary<int, ItemModifiers> modifiers, List<ActiveSynergy> active)
        {
            _modifiers = modifiers;
            _active = active;
        }

        /// <summary>Modifiers for one item instance; <see cref="ItemModifiers.None"/> when no rule matched.</summary>
        public ItemModifiers For(int itemInstanceId) =>
            _modifiers.TryGetValue(itemInstanceId, out ItemModifiers modifiers) ? modifiers : ItemModifiers.None;

        /// <summary>Every rule application, in canonical order (rule, then target, then source).</summary>
        public IReadOnlyList<ActiveSynergy> Active => _active;
    }

    /// <summary>
    /// Applies the <c>synergy</c> table to a layout: the 5 combo rules of GDD v0.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One pass, no fixed point.</b> The rules read positions and write multipliers;
    /// nothing a rule writes can turn another rule on, so "A เพิ่ม B แล้ว B เพิ่ม A"
    /// cannot loop — there is no iteration to diverge. Evaluating a layout twice gives
    /// the same answer (asserted by a test).
    /// </para>
    /// <para>
    /// <b>Counting, not contact.</b> For every rule and every eligible target it counts
    /// the <i>distinct</i> pieces that satisfy the condition, then clamps that count to
    /// the rule's <c>max_stacks</c>. A neighbour touching along three cells is one
    /// neighbour; a rule that may stack twice needs two different partners. This is what
    /// "สองชิ้นที่ติดกันหลายช่องนับความสัมพันธ์ครั้งเดียว" and "ซ้อนได้สูงสุด 2 ชั้น" mean
    /// together, and it makes the answer independent of iteration order.
    /// </para>
    /// <para>
    /// <b>Direction from the data.</b> <c>tag_a</c> is the piece that provides the
    /// condition and <c>tag_b</c> the piece that receives it — the GDD reads that way for
    /// four of the five rules ("ตะกรุดวางติดผ้ายันต์ → cooldown ของ<u>ยันต์</u>",
    /// "ภูติ<u>โจมตีเร็วขึ้น</u>"). <see cref="SynergyStat.Debuff"/> is the exception the
    /// GDD states explicitly — "ทั้งสองชิ้นเสื่อมพลัง" — so it applies to both sides.
    /// </para>
    /// <para>
    /// Amounts of the same stat add before being applied ("โบนัสเปอร์เซ็นต์ชนิดเดียวกันบวกกัน").
    /// </para>
    /// <para>
    /// <see cref="SynergyKind.SameRow"/> and <see cref="SynergyKind.SameColumn"/> are the two
    /// halves of the GDD's "แถวเดียวกัน" condition ("the same line"): a piece sharing any board
    /// row, or any board column, with the source satisfies it. Which one a rule uses is a
    /// <c>synergy.kind</c> value, so a column rule needs a data row and no code
    /// (ADR 0015, <c>docs/adr/0015-same-line-synergy-kinds.md</c>).
    /// </para>
    /// </remarks>
    public sealed class SynergyEvaluator
    {
        private readonly IGameConfig _config;

        /// <summary>Creates an evaluator over one config snapshot.</summary>
        public SynergyEvaluator(IGameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Evaluates every rule against the pieces currently on the board.</summary>
        public SynergyEvaluation Evaluate(Backpack backpack)
        {
            if (backpack is null)
            {
                throw new ArgumentNullException(nameof(backpack));
            }

            List<ItemInstance> items = backpack.OrderedBoardItems();
            List<BagPieceInstance> bags = backpack.OrderedBoardBags();

            var damagePercent = new Dictionary<int, float>();
            var cooldownPercent = new Dictionary<int, float>();
            var applied = new Dictionary<int, int>();
            var active = new List<ActiveSynergy>();

            IReadOnlyList<SynergyConfig> rules = _config.Synergies;
            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
                SynergyConfig rule = rules[ruleIndex];
                int maxStacks = rule.MaxStacks < 1 ? 1 : rule.MaxStacks;

                switch (rule.Kind)
                {
                    case SynergyKind.AdjacentPair:
                        ApplyPairs(rule, maxStacks, items, (source, target) => source.IsAdjacentTo(target));
                        break;

                    case SynergyKind.SameRow:
                        ApplyPairs(rule, maxStacks, items, (source, target) => source.SharesRowWith(target));
                        break;

                    case SynergyKind.SameColumn:
                        ApplyPairs(rule, maxStacks, items, (source, target) => source.SharesColumnWith(target));
                        break;

                    case SynergyKind.BagArea:
                        ApplyBagArea(rule, maxStacks, items, bags);
                        break;

                    default:
                        // An unknown kind in data is a data bug (the validator rejects it);
                        // skipping it keeps the engine total instead of throwing mid-battle.
                        break;
                }
            }

            var modifiers = new Dictionary<int, ItemModifiers>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                int id = items[i].InstanceId;
                float damage = damagePercent.TryGetValue(id, out float d) ? d : 0f;
                float cooldown = cooldownPercent.TryGetValue(id, out float c) ? c : 0f;
                int count = applied.TryGetValue(id, out int a) ? a : 0;

                float damageMultiplier = 1f + (damage / 100f);
                if (damageMultiplier < 0f)
                {
                    damageMultiplier = 0f;
                }

                modifiers[id] = new ItemModifiers(damageMultiplier, 1f + (cooldown / 100f), count);
            }

            return new SynergyEvaluation(modifiers, active);

            void ApplyPairs(
                SynergyConfig rule, int maxStacks, List<ItemInstance> items, Func<ItemInstance, ItemInstance, bool> relation)
            {
                if (string.IsNullOrEmpty(rule.TagA) || string.IsNullOrEmpty(rule.TagB))
                {
                    return;
                }

                int directions = rule.Stat == SynergyStat.Debuff ? 2 : 1;
                for (int direction = 0; direction < directions; direction++)
                {
                    string sourceTag = direction == 0 ? rule.TagA : rule.TagB;
                    string targetTag = direction == 0 ? rule.TagB : rule.TagA;

                    for (int t = 0; t < items.Count; t++)
                    {
                        ItemInstance target = items[t];
                        if (!HasTag(target, targetTag))
                        {
                            continue;
                        }

                        // Sources are visited in canonical order and the loop stops at the
                        // cap, so the recorded pairs are exactly the applied ones.
                        int contributions = 0;
                        for (int s = 0; s < items.Count && contributions < maxStacks; s++)
                        {
                            ItemInstance source = items[s];
                            if (source.InstanceId == target.InstanceId
                                || !HasTag(source, sourceTag)
                                || !relation(source, target))
                            {
                                continue;
                            }

                            contributions++;
                            Accumulate(rule, target, source.InstanceId);
                        }
                    }
                }
            }

            void ApplyBagArea(
                SynergyConfig rule, int maxStacks, List<ItemInstance> items, List<BagPieceInstance> bags)
            {
                for (int t = 0; t < items.Count; t++)
                {
                    ItemInstance target = items[t];
                    int contributions = 0;
                    var counted = new HashSet<int>();

                    for (int b = 0; b < bags.Count && contributions < maxStacks; b++)
                    {
                        BagPieceInstance bag = bags[b];
                        if (counted.Contains(bag.InstanceId) || !OverlapsBag(target, bag))
                        {
                            continue;
                        }

                        counted.Add(bag.InstanceId);
                        contributions++;
                        Accumulate(rule, target, bag.InstanceId);
                    }
                }
            }

            void Accumulate(SynergyConfig rule, ItemInstance target, int sourceInstanceId)
            {
                int id = target.InstanceId;

                switch (rule.Stat)
                {
                    case SynergyStat.Damage:
                    case SynergyStat.Debuff:
                        damagePercent[id] = (damagePercent.TryGetValue(id, out float d) ? d : 0f) + rule.ValuePercent;
                        break;

                    case SynergyStat.Cooldown:
                        cooldownPercent[id] = (cooldownPercent.TryGetValue(id, out float c) ? c : 0f) + rule.ValuePercent;
                        break;

                    case SynergyStat.AttackSpeed:
                        // A faster attack is a shorter cooldown, so the sign flips here and
                        // only here.
                        cooldownPercent[id] = (cooldownPercent.TryGetValue(id, out float a) ? a : 0f) - rule.ValuePercent;
                        break;

                    default:
                        return;
                }

                applied[id] = (applied.TryGetValue(id, out int a2) ? a2 : 0) + 1;
                active.Add(new ActiveSynergy(rule.Id, sourceInstanceId, id, rule.Stat, rule.ValuePercent));
            }
        }

        private static bool HasTag(ItemInstance item, string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return false;
            }

            IReadOnlyList<string> tags = item.Tags;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverlapsBag(ItemInstance item, BagPieceInstance bag)
        {
            int cells = item.CellCount;
            for (int i = 0; i < cells; i++)
            {
                if (bag.Occupies(item.CellAt(i)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
