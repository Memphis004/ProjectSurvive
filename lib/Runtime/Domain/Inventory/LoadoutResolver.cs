using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// Turns carried weight into the movement penalty (GDD: "ยิ่งหนักยิ่งเดินช้า").
    /// </summary>
    /// <remarks>
    /// The curve is linear — <c>constant.weight_speed_percent_per_unit</c> percent per
    /// weight unit, clamped to <c>constant.max_speed_penalty_percent</c>. Both numbers
    /// come from <c>data/</c>; this type only holds the arithmetic (AGENTS rule 5).
    /// </remarks>
    public static class WeightPenalty
    {
        /// <summary>
        /// Percent of movement speed lost at <paramref name="totalWeight"/>, in
        /// <c>[0, constant.max_speed_penalty_percent]</c>.
        /// </summary>
        public static int Percent(float totalWeight, ConstantConfig constant)
        {
            if (constant is null)
            {
                throw new ArgumentNullException(nameof(constant));
            }

            if (totalWeight <= 0f)
            {
                return 0;
            }

            // The tolerance is not a fudge factor for the rule, it is for the weight
            // itself: the total is a sum of `float`s from data (0.8 + 0.4 + ...), and a
            // sum that lands one ulp below an integer would otherwise cost the player a
            // whole percent for no reason. Deterministic either way.
            int percent = (int)Math.Floor((totalWeight * constant.WeightSpeedPercentPerUnit) + 0.0001);

            if (percent < 0)
            {
                return 0;
            }

            return percent > constant.MaxSpeedPenaltyPercent ? constant.MaxSpeedPenaltyPercent : percent;
        }
    }

    /// <summary>
    /// Builds the <see cref="CombatLoadoutSnapshot"/> a battle consumes, from the pieces
    /// on the board.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only bridge from the bag to the battle (GDD: "จากย่ามถึง Battle", the
    /// "Battle ไม่อ่านตำแหน่ง UI และไม่คำนวณคอมโบเอง" rule). It sums the stats, applies the
    /// tier and the combo multipliers, and emits the Battle v1 contract types from
    /// <c>ProjectSurvive.Core.Domain.Combat.Battle</c> — this type does not introduce a
    /// second loadout model.
    /// </para>
    /// <para>
    /// <b>Recompute policy.</b> The result is cached against
    /// <see cref="Backpack.Revision"/>: the GDD only recomputes "เมื่อชุดอุปกรณ์เปลี่ยน"
    /// (when the set changes), and <see cref="ResolveCount"/> makes that observable.
    /// </para>
    /// <para>
    /// <b>Order.</b> Weapons, summons and consumables come out in a canonical order
    /// derived from board position and ids, never from dictionary iteration, so two
    /// backpacks holding the same layout produce identical snapshots field by field.
    /// </para>
    /// <para>
    /// Chest contents contribute nothing (GDD: "ของในหีบไม่ให้โบนัสและไม่โจมตี").
    /// </para>
    /// </remarks>
    public sealed class LoadoutResolver
    {
        private readonly IGameConfig _config;
        private readonly SynergyEvaluator _evaluator;
        private readonly UpgradeAccumulator _upgrades = new UpgradeAccumulator();
        private Backpack? _cachedFor;
        private int _cachedRevision = -1;
        private CombatLoadoutSnapshot? _cached;

        /// <summary>Creates a resolver over one config snapshot.</summary>
        public LoadoutResolver(IGameConfig config, SynergyEvaluator evaluator)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        /// <summary>How many times a snapshot was actually computed (cache misses).</summary>
        public int ResolveCount { get; private set; }

        /// <summary>
        /// Returns the loadout for <paramref name="backpack"/>, recomputing only when the
        /// backpack moved since the last call.
        /// </summary>
        public CombatLoadoutSnapshot Resolve(Backpack backpack)
        {
            if (backpack is null)
            {
                throw new ArgumentNullException(nameof(backpack));
            }

            if (_cached is not null && ReferenceEquals(_cachedFor, backpack) && _cachedRevision == backpack.Revision)
            {
                return _cached;
            }

            CombatLoadoutSnapshot snapshot = Compute(backpack);
            ResolveCount++;
            _cached = snapshot;
            _cachedFor = backpack;
            _cachedRevision = backpack.Revision;
            return snapshot;
        }

        private CombatLoadoutSnapshot Compute(Backpack backpack)
        {
            ConstantConfig constant = _config.Constant;
            SynergyEvaluation evaluation = _evaluator.Evaluate(backpack);
            List<ItemInstance> items = backpack.OrderedBoardItems();

            int speedPenalty = WeightPenalty.Percent(backpack.BoardWeight, constant);

            // The player's own numbers carry the upgrade variants of every item on the
            // board, resolved exactly like a weapon's (Stage 09f). The weight penalty comes
            // first so a move-speed upgrade scales the speed the player actually has.
            var stats = new PlayerStatBlock
            {
                MaxHp = AtLeast(ResolvePlayerStat(UpgradeStat.MaxHp, constant.MaxHp, items), 1),
                MaxMp = AtLeast(ResolvePlayerStat(UpgradeStat.MaxMp, constant.MaxMp, items), 0),
                MoveSpeed = ResolvePlayerStat(
                    UpgradeStat.MoveSpeed, constant.MoveSpeed * (1f - (speedPenalty / 100f)), items),
                PickupRadius = ResolvePlayerStat(UpgradeStat.PickupRadius, constant.PickupRadius, items),
                FocusPerSecond = ResolvePlayerStat(
                    UpgradeStat.FocusPerSecond, constant.FocusPerSecond, items),
                SpeedPenaltyPercent = speedPenalty,
            };

            var weapons = new List<WeaponLoadout>();
            var summons = new List<SummonLoadout>();
            var consumableCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var consumableSkill = new Dictionary<string, string>(StringComparer.Ordinal);

            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];
                if (!_config.TryGetItem(item.ItemId, out ItemConfig definition))
                {
                    continue;
                }

                switch (definition.Category)
                {
                    case ItemCategory.ArcaneWeapon:
                        if (TryBuildWeapon(item, definition, evaluation, out WeaponLoadout weapon))
                        {
                            weapons.Add(weapon);
                        }

                        break;

                    case ItemCategory.Familiar:
                        if (TryBuildSummon(item, definition, evaluation, out SummonLoadout summon))
                        {
                            summons.Add(summon);
                        }

                        break;

                    case ItemCategory.Offering:
                        consumableCounts[definition.Id] =
                            (consumableCounts.TryGetValue(definition.Id, out int count) ? count : 0) + 1;
                        consumableSkill[definition.Id] = definition.SkillId;
                        break;
                }
            }

            // Aggregated per item id and ordered by id: the contract has no tier on a
            // consumable, so N identical offerings are "one entry, N uses".
            var consumables = new List<ConsumableLoadout>(consumableCounts.Count);
            foreach (KeyValuePair<string, int> entry in consumableCounts)
            {
                consumables.Add(new ConsumableLoadout
                {
                    ItemId = entry.Key,
                    Count = entry.Value,
                    SkillId = consumableSkill[entry.Key],
                });
            }

            consumables.Sort(CompareConsumables);

            return new CombatLoadoutSnapshot
            {
                Stats = stats,
                Weapons = weapons,
                Summons = summons,
                Consumables = consumables,
            };
        }

        private bool TryBuildWeapon(
            ItemInstance item, ItemConfig definition, SynergyEvaluation evaluation, out WeaponLoadout weapon)
        {
            weapon = new WeaponLoadout();

            if (string.IsNullOrEmpty(definition.SkillId)
                || !_config.TryGetSkill(definition.SkillId, out SkillConfig skill))
            {
                return false;
            }

            ItemModifiers modifiers = evaluation.For(item.InstanceId);

            // The upgrades apply to the number the combos already resolved: a synergy is a
            // property of the layout, an upgrade is a property of this piece's history.
            IReadOnlyList<string> upgrades = item.VariantIds;

            weapon = new WeaponLoadout
            {
                SkillId = skill.Id,
                Tier = item.Tier,
                Behavior = skill.Behavior,
                Element = skill.Element != Element.None ? skill.Element : definition.Element,
                CooldownSeconds = ResolveCooldown(
                    _upgrades.Resolve(skill.CooldownSeconds, _config, upgrades, UpgradeStat.Cooldown), modifiers),
                MpCost = AtLeast(
                    _upgrades.Resolve(skill.MpCost, _config, upgrades, UpgradeStat.MpCost), 0),
                Damage = AtLeast(
                    _upgrades.Resolve(
                        Scale(skill.Damage, modifiers.DamageMultiplier), _config, upgrades, UpgradeStat.Damage), 0),
                ProjectileSpeed = Math.Max(
                    _upgrades.Resolve(skill.ProjectileSpeed, _config, upgrades, UpgradeStat.ProjectileSpeed), 0f),
                Range = Math.Max(_upgrades.Resolve(skill.Range, _config, upgrades, UpgradeStat.Range), 0f),
                PierceCount = AtLeast(
                    _upgrades.Resolve(skill.PierceCount, _config, upgrades, UpgradeStat.PierceCount), 0),
                ProjectileCount = AtLeast(
                    _upgrades.Resolve(skill.ProjectileCount, _config, upgrades, UpgradeStat.ProjectileCount), 1),
            };

            return true;
        }

        private bool TryBuildSummon(
            ItemInstance item, ItemConfig definition, SynergyEvaluation evaluation, out SummonLoadout summon)
        {
            summon = new SummonLoadout();

            if (string.IsNullOrEmpty(definition.SkillId)
                || !_config.TryGetSkill(definition.SkillId, out SkillConfig skill))
            {
                return false;
            }

            ItemModifiers modifiers = evaluation.For(item.InstanceId);
            IReadOnlyList<string> upgrades = item.VariantIds;

            summon = new SummonLoadout
            {
                SkillId = skill.Id,
                Tier = item.Tier,
                CooldownSeconds = ResolveCooldown(
                    _upgrades.Resolve(skill.CooldownSeconds, _config, upgrades, UpgradeStat.Cooldown), modifiers),
                MpCost = AtLeast(
                    _upgrades.Resolve(skill.MpCost, _config, upgrades, UpgradeStat.MpCost), 0),
                Damage = AtLeast(
                    _upgrades.Resolve(
                        Scale(skill.Damage, modifiers.DamageMultiplier), _config, upgrades, UpgradeStat.Damage), 0),
                Range = Math.Max(_upgrades.Resolve(skill.Range, _config, upgrades, UpgradeStat.Range), 0f),
            };

            return true;
        }

        /// <summary>The rule's floor ("cooldown มีค่าต่ำสุด"), never crossed by a combo bonus.</summary>
        private float ResolveCooldown(float baseCooldown, ItemModifiers modifiers)
        {
            float scaled = baseCooldown * modifiers.CooldownMultiplier;
            float minimum = _config.Constant.MinCooldownSeconds;
            return scaled < minimum ? minimum : scaled;
        }

        private static int Scale(int damage, float multiplier)
        {
            float scaled = damage * multiplier;
            return scaled <= 0f ? 0 : (int)Math.Round(scaled, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// One player stat after every upgrade on the board that targets it, resolved as a
        /// whole: the flat, the percentage and the factors of all pieces combine before the
        /// base is touched, so two "+20% move speed" upgrades are +40% and not +44%.
        /// </summary>
        private float ResolvePlayerStat(UpgradeStat stat, float baseValue, List<ItemInstance> items)
        {
            _upgrades.Reset();

            for (int i = 0; i < items.Count; i++)
            {
                _upgrades.Include(_config, items[i].VariantIds, stat);
            }

            return _upgrades.Apply(baseValue);
        }

        /// <summary>Rounds an upgraded value to the nearest int, never below <paramref name="minimum"/>.</summary>
        private static int AtLeast(float value, int minimum)
        {
            if (value <= minimum)
            {
                return minimum;
            }

            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        private static int CompareConsumables(ConsumableLoadout left, ConsumableLoadout right) =>
            string.CompareOrdinal(left.ItemId, right.ItemId);
    }
}
