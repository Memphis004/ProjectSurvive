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

        /// <summary>
        /// Explains one piece: the base numbers, the numbers the battle will use, the upgrade steps
        /// it kept and the combos acting on it (Stage 09h).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>One source with <see cref="Resolve"/>.</b> A piece on the board is read straight out
        /// of this resolver's own <see cref="CombatLoadoutSnapshot"/> — the tooltip cannot disagree
        /// with the battle because it is quoting the same entry. A piece in the chest is resolved
        /// with the same helpers (<see cref="UpgradeAccumulator"/> and
        /// <see cref="ResolveCooldown"/>) but with no combos, which is what the chest means
        /// ("ของในหีบไม่ให้โบนัสและไม่โจมตี").
        /// </para>
        /// <para>
        /// Returns false only when the run holds no such piece; a bag is a valid answer (it has a
        /// tier and no stats).
        /// </para>
        /// </remarks>
        public bool TryExplain(Backpack backpack, PieceRef piece, out PieceReadout readout)
        {
            if (backpack is null)
            {
                throw new ArgumentNullException(nameof(backpack));
            }

            readout = null!;

            if (!backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                return false;
            }

            ItemInstance? item = instance as ItemInstance;
            SynergyEvaluation? evaluation = item is null ? null : _evaluator.Evaluate(backpack);
            ItemModifiers modifiers = evaluation is null ? ItemModifiers.None : evaluation.For(item!.InstanceId);

            PieceReadout result = Explain(
                piece, instance.DefId, instance.Tier, instance is BagPieceInstance,
                item?.VariantIds ?? Array.Empty<string>(), modifiers,
                backpack, item?.InstanceId ?? 0);

            if (item is not null && evaluation is not null)
            {
                result.ActiveSynergyCount = modifiers.ActiveSynergyCount;
                result.DamageMultiplier = modifiers.DamageMultiplier;
                result.CooldownMultiplier = modifiers.CooldownMultiplier;
                result.ComboRuleIds = ComboRules(evaluation, item.InstanceId);
                result.Steps = Steps(item);
            }

            readout = result;
            return true;
        }

        /// <summary>
        /// Explains a <b>shelf offer</b> — a piece that is not in the bag yet (Stage 09n).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The same <see cref="Explain"/> core <see cref="TryExplain"/> uses, with the numbers a piece
        /// that is in no layout has: its own skill row and its own (absent) upgrades, no combo. That is
        /// exactly how a tier-1 piece in the chest resolves, so the shelf and the bag cannot print two
        /// different sets of numbers for the same item — a second calculator is what this method
        /// exists to avoid.
        /// </para>
        /// <para>
        /// Returns false only for an id that is in neither table (a data bug, reported by the caller as
        /// <c>shop.unknown_piece</c>).
        /// </para>
        /// </remarks>
        public bool TryExplainOffer(string defId, int tier, out PieceReadout readout)
        {
            readout = null!;

            if (string.IsNullOrEmpty(defId))
            {
                return false;
            }

            bool isBag = _config.TryGetBag(defId, out _);

            if (!isBag && !_config.TryGetItem(defId, out _))
            {
                return false;
            }

            readout = Explain(
                PieceRef.None, defId, tier, isBag, Array.Empty<string>(), ItemModifiers.None, null, 0);
            return true;
        }

        /// <summary>How many partner names one <see cref="OfferCombo"/> carries (the rest are counted).</summary>
        private const int PartnerNamesShown = 3;

        /// <summary>
        /// Fills <paramref name="into"/> with the combos an offer of <paramref name="defId"/> could take
        /// part in, and returns how many there are.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <b>listing</b> read straight off the <c>synergy</c> and <c>item</c> tables: a rule whose two
        /// tags this piece carries one of, with the other tag's pieces named. No multiplier is applied
        /// and no <see cref="SynergyEvaluator"/> pass runs, because there is nothing on a board to
        /// evaluate — which is why this is not a second combo calculation (AGENTS rule 10: the numbers
        /// still come from <see cref="PieceReadout"/>).
        /// </para>
        /// <para>
        /// A bag lists nothing: the only rule a bag takes part in is the bag-area bonus, which is a
        /// property of the whole layout rather than a pairing with another piece.
        /// </para>
        /// </remarks>
        public int PotentialCombos(string defId, List<OfferCombo> into)
        {
            if (into is null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();

            if (string.IsNullOrEmpty(defId) || !_config.TryGetItem(defId, out ItemConfig item))
            {
                return 0;
            }

            IReadOnlyList<SynergyConfig> rules = _config.Synergies;
            for (int i = 0; i < rules.Count; i++)
            {
                SynergyConfig rule = rules[i];

                // Only a rule whose condition is "these two tags sit next to each other" can be said
                // about one piece on its own; the bag-area bonus names no pair.
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

                // The partner is the rule's other half. A piece carrying both tags is listed against
                // the second one, and the piece itself is filtered out of the names below.
                string partnerTag = carriesA ? rule.TagB : rule.TagA;

                if (string.IsNullOrEmpty(partnerTag))
                {
                    continue;
                }

                var names = new List<string>(PartnerNamesShown);
                int count = 0;
                IReadOnlyList<ItemConfig> items = _config.Items;

                for (int c = 0; c < items.Count; c++)
                {
                    ItemConfig partner = items[c];

                    if (string.Equals(partner.Id, item.Id, StringComparison.Ordinal) || !HasTag(partner, partnerTag))
                    {
                        continue;
                    }

                    count++;
                    if (names.Count < PartnerNamesShown)
                    {
                        names.Add(partner.NameKey);
                    }
                }

                if (count == 0)
                {
                    continue;
                }

                into.Add(new OfferCombo(rule.Id, rule.NameKey, partnerTag, names, count));
            }

            return into.Count;
        }

        /// <summary>
        /// The one description of a piece: its row's own effects, its skill's base row and the numbers
        /// it resolves to — with the board consulted only when the piece is on it.
        /// </summary>
        private PieceReadout Explain(
            PieceRef piece, string defId, int tier, bool isBag,
            IReadOnlyList<string> variantIds, ItemModifiers modifiers,
            Backpack? boardBackpack, int instanceId)
        {
            var result = new PieceReadout
            {
                Piece = piece,
                DefId = defId,
                Tier = tier,
                IsBag = isBag,
                MinCooldownSeconds = _config.Constant.MinCooldownSeconds,
            };

            if (isBag)
            {
                // A bag has a tier, a shape and a weight — never a damage number.
                return result;
            }

            if (!_config.TryGetItem(defId, out ItemConfig definition))
            {
                return result;
            }

            // The item-local effects the tooltip draws (Stage 11a). They are copied from
            // the same row the loadout resolver reads, so the screen and the snapshot
            // cannot disagree about a charm's passive or an offering's uses.
            result.HasCharmEffect = definition.HasCharmEffect;
            result.CharmStat = definition.CharmStat;
            result.CharmMethod = definition.CharmMethod;
            result.CharmMagnitude = definition.CharmMagnitude;
            result.IsOffering = definition.Category == ItemCategory.Offering;
            result.OfferingEffect = definition.OfferingEffect;
            result.OfferingAmount = definition.OfferingAmount;
            result.OfferingUses = definition.OfferingUses;
            result.OfferingAutoHpPercent = definition.OfferingAutoHpPercent;

            if (!string.IsNullOrEmpty(definition.SkillId)
                && _config.TryGetSkill(definition.SkillId, out SkillConfig skill))
            {
                result.SkillId = skill.Id;
                result.Casts = true;
                result.Base = new PieceStatValues(skill.Damage, skill.CooldownSeconds, skill.MpCost);

                if (boardBackpack is not null && instanceId > 0
                    && TryFindResolved(boardBackpack, instanceId, out PieceStatValues resolved))
                {
                    result.InLoadout = true;
                    result.Resolved = resolved;
                }
                else
                {
                    result.Resolved = ResolveStandalone(skill, variantIds, modifiers);
                }

                // The floor is the difference between "the combo did nothing" and "the combo was
                // capped", so it is answered from the value before the clamp (same accumulation
                // rule: every percent sums, every factor multiplies).
                float intended = _upgrades.Resolve(
                    skill.CooldownSeconds, _config, variantIds, UpgradeStat.Cooldown)
                    * modifiers.CooldownMultiplier;

                result.CooldownAtMinimum =
                    intended < _config.Constant.MinCooldownSeconds
                    && result.Resolved.CooldownSeconds <= _config.Constant.MinCooldownSeconds;
            }

            return result;
        }

        /// <summary>True when <paramref name="item"/> carries <paramref name="tag"/> (ordinal, never a fuzzy match).</summary>
        private static bool HasTag(ItemConfig item, string tag)
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

        /// <summary>The synergy rules this piece takes part in (provided or received).</summary>
        private static IReadOnlyList<string> ComboRules(SynergyEvaluation evaluation, int instanceId)
        {
            IReadOnlyList<ActiveSynergy> active = evaluation.Active;
            var rules = new List<string>(2);

            for (int i = 0; i < active.Count; i++)
            {
                ActiveSynergy synergy = active[i];
                if (synergy.SourceInstanceId != instanceId && synergy.TargetInstanceId != instanceId)
                {
                    continue;
                }

                rules.Add(synergy.RuleId);
            }

            return rules.Count == 0 ? Array.Empty<string>() : rules.ToArray();
        }

        /// <summary>One entry per merge step the piece went through, variant rows included.</summary>
        private IReadOnlyList<PieceUpgradeStep> Steps(ItemInstance item)
        {
            IReadOnlyList<string> ids = item.VariantIds;
            if (ids.Count == 0)
            {
                return Array.Empty<PieceUpgradeStep>();
            }

            var steps = new PieceUpgradeStep[ids.Count];
            for (int step = 0; step < ids.Count; step++)
            {
                string id = ids[step];
                if (!string.IsNullOrEmpty(id) && _config.TryGetUpgradeVariant(id, out UpgradeVariantConfig variant))
                {
                    steps[step] = new PieceUpgradeStep(
                        step + 1, variant.FromTier, variant.ToTier, id, variant.NameKey, variant.Modifiers);
                    continue;
                }

                // No chooser on that step: the merge folded the pair and kept no variant. The step
                // still happened, and the tooltip draws no variant line for it.
                steps[step] = new PieceUpgradeStep(
                    step + 1, step + 1, step + 2, string.Empty, string.Empty, Array.Empty<UpgradeModifierConfig>());
            }

            return steps;
        }

        /// <summary>
        /// The numbers this piece receives in the battle, copied out of the snapshot this resolver
        /// hands to the floor. False when the piece is not in it (it is in the chest).
        /// </summary>
        private bool TryFindResolved(Backpack backpack, int instanceId, out PieceStatValues resolved)
        {
            CombatLoadoutSnapshot snapshot = Resolve(backpack);

            IReadOnlyList<WeaponLoadout> weapons = snapshot.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].InstanceId != instanceId)
                {
                    continue;
                }

                resolved = new PieceStatValues(
                    weapons[i].Damage, weapons[i].CooldownSeconds, weapons[i].MpCost);
                return true;
            }

            IReadOnlyList<SummonLoadout> summons = snapshot.Summons;
            for (int i = 0; i < summons.Count; i++)
            {
                if (summons[i].InstanceId != instanceId)
                {
                    continue;
                }

                resolved = new PieceStatValues(
                    summons[i].Damage, summons[i].CooldownSeconds, summons[i].MpCost);
                return true;
            }

            resolved = default;
            return false;
        }

        /// <summary>A piece that is in no layout: its own upgrades apply, no combo does.</summary>
        private PieceStatValues ResolveStandalone(
            SkillConfig skill, IReadOnlyList<string> upgrades, ItemModifiers modifiers)
        {
            return new PieceStatValues(
                AtLeast(
                    _upgrades.Resolve(
                        Scale(skill.Damage, modifiers.DamageMultiplier), _config, upgrades, UpgradeStat.Damage), 0),
                ResolveCooldown(
                    _upgrades.Resolve(skill.CooldownSeconds, _config, upgrades, UpgradeStat.Cooldown), modifiers),
                AtLeast(_upgrades.Resolve(skill.MpCost, _config, upgrades, UpgradeStat.MpCost), 0));
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

            // A charm adjusts a player stat the snapshot already carries (Stage 11a). The
            // modifiers are accumulated across the board before the base is touched, exactly
            // like an upgrade: every Add sums, every Percent sums, every Multiply folds, so
            // the result never depends on the board's iteration order.
            stats.MaxHp = AtLeast(ApplyCharms(UpgradeStat.MaxHp, stats.MaxHp, items), 1);
            stats.MaxMp = AtLeast(ApplyCharms(UpgradeStat.MaxMp, stats.MaxMp, items), 0);
            stats.MoveSpeed = ApplyCharms(UpgradeStat.MoveSpeed, stats.MoveSpeed, items);
            stats.PickupRadius = ApplyCharms(UpgradeStat.PickupRadius, stats.PickupRadius, items);
            stats.FocusPerSecond = ApplyCharms(UpgradeStat.FocusPerSecond, stats.FocusPerSecond, items);

            var weapons = new List<WeaponLoadout>();
            var summons = new List<SummonLoadout>();
            var consumableCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var consumableDefinitions = new Dictionary<string, ItemConfig>(StringComparer.Ordinal);

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
                        consumableDefinitions[definition.Id] = definition;
                        break;
                }
            }

            // Aggregated per item id and ordered by id: the contract has no tier on a
            // consumable, so N identical offerings are "one entry, N uses".
            var consumables = new List<ConsumableLoadout>(consumableCounts.Count);
            foreach (KeyValuePair<string, int> entry in consumableCounts)
            {
                ItemConfig definition = consumableDefinitions[entry.Key];
                consumables.Add(new ConsumableLoadout
                {
                    ItemId = entry.Key,
                    Count = entry.Value,
                    SkillId = definition.SkillId,
                    Effect = definition.OfferingEffect,
                    EffectAmount = definition.OfferingAmount,
                    UsesPerItem = definition.OfferingUses > 0 ? definition.OfferingUses : 1,
                    AutoUseHpPercent = definition.OfferingAutoHpPercent,
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

        /// <summary>
        /// The value <paramref name="baseValue"/> reaches after every charm on the board that
        /// adjusts <paramref name="stat"/> (Stage 11a). Order-independent by construction.
        /// </summary>
        private float ApplyCharms(UpgradeStat stat, float baseValue, List<ItemInstance> items)
        {
            float flatAdd = 0f;
            float percentAdd = 0f;
            float factor = 1f;
            bool touched = false;

            for (int i = 0; i < items.Count; i++)
            {
                ItemInstance item = items[i];
                if (!_config.TryGetItem(item.ItemId, out ItemConfig definition)
                    || definition.Category != ItemCategory.Charm
                    || !definition.HasCharmEffect
                    || definition.CharmStat != stat)
                {
                    continue;
                }

                switch (definition.CharmMethod)
                {
                    case UpgradeMethod.Add:
                        flatAdd += definition.CharmMagnitude;
                        break;
                    case UpgradeMethod.Percent:
                        percentAdd += definition.CharmMagnitude;
                        break;
                    case UpgradeMethod.Multiply:
                        factor *= definition.CharmMagnitude;
                        break;
                }

                touched = true;
            }

            return touched ? ((baseValue + flatAdd) * (1f + (percentAdd / 100f))) * factor : baseValue;
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
                // The board piece this entry stands for, so a frame's cooldown list can be drawn
                // on the item the player placed (Stage 08b) rather than on "the second weapon".
                InstanceId = item.InstanceId,
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
                InstanceId = item.InstanceId,
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
