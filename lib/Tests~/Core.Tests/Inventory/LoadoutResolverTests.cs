using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 task 8: the bag becomes a <see cref="CombatLoadoutSnapshot"/> of the
    /// Battle v1 contract — every section filled, active synergies already applied, and
    /// the same answer for the same layout whatever order it was built in.
    /// </summary>
    public sealed class LoadoutResolverTests
    {
        private static LoadoutResolver Resolver(IGameConfig config) =>
            new LoadoutResolver(config, new SynergyEvaluator(config));

        [Fact]
        public void Snapshot_carries_the_full_contract_for_a_starter_build()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 1);
            InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);
            InventoryFixture.Place(editor, "item.red_soda", 2, 2);

            // Chest furniture that must NOT reach the snapshot.
            InventoryFixture.Add(editor, "item.mor_knife");
            InventoryFixture.Add(editor, "item.boiled_egg");

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            Assert.NotNull(snapshot.Stats);
            Assert.NotNull(snapshot.Weapons);
            Assert.NotNull(snapshot.Summons);
            Assert.NotNull(snapshot.Consumables);

            ConstantConfig constant = config.Constant;
            Assert.Equal(constant.MaxHp, snapshot.Stats.MaxHp);
            Assert.Equal(constant.MaxMp, snapshot.Stats.MaxMp);
            Assert.Equal(constant.PickupRadius, snapshot.Stats.PickupRadius);
            Assert.Equal(constant.FocusPerSecond, snapshot.Stats.FocusPerSecond);

            // One weapon (the yant), one summon (kuman_thong), one offering (red_soda) —
            // the two chest pieces are excluded.
            WeaponLoadout weapon = Assert.Single(snapshot.Weapons);
            Assert.Equal("skill.fire_bolt", weapon.SkillId);
            Assert.Equal(1, weapon.Tier);
            Assert.Equal(SkillBehavior.Projectile, weapon.Behavior);
            Assert.Equal(Element.Fire, weapon.Element);
            Assert.Equal(4, weapon.MpCost);
            Assert.Equal(1, weapon.ProjectileCount);

            SummonLoadout summon = Assert.Single(snapshot.Summons);
            Assert.Equal("skill.curse_wave", summon.SkillId);

            ConsumableLoadout consumable = Assert.Single(snapshot.Consumables);
            Assert.Equal("item.red_soda", consumable.ItemId);
            Assert.Equal(1, consumable.Count);
            Assert.Equal(string.Empty, consumable.SkillId);
        }

        /// <summary>
        /// Every casting entry names the board piece it was resolved from, which is the key a HUD uses
        /// to draw a cooldown on the item the player placed (Stage 08b).
        /// </summary>
        /// <remarks>
        /// The battle-side contract suite proves the frame republishes the id it was handed, but the id
        /// itself is produced here and nowhere else: a resolver that forgot to set it would hand every
        /// floor a loadout whose entries all claimed to come from no piece, and no fake or fixture could
        /// catch that.
        /// </remarks>
        [Fact]
        public void Every_caster_entry_names_the_board_piece_it_was_resolved_from()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            // The same placed pair <see cref="Snapshot_carries_the_full_contract_for_a_starter_build"/>
            // uses, at the same cells: one weapon, one familiar, both fully on the bag.
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef weapon = InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 1);
            PieceRef familiar = InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            Assert.Equal(weapon.InstanceId, Assert.Single(snapshot.Weapons).InstanceId);
            Assert.Equal(familiar.InstanceId, Assert.Single(snapshot.Summons).InstanceId);
        }

        [Fact]
        public void An_empty_board_still_produces_a_valid_snapshot()
        {
            IGameConfig config = InventoryFixture.Config();
            InventoryFixture.NewEditor(config, out Backpack backpack);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            Assert.Empty(snapshot.Weapons);
            Assert.Empty(snapshot.Summons);
            Assert.Empty(snapshot.Consumables);
            Assert.Equal(config.Constant.MoveSpeed, snapshot.Stats.MoveSpeed);
            Assert.Equal(0, snapshot.Stats.SpeedPenaltyPercent);
            Assert.Equal(1f, snapshot.Stats.CooldownMultiplier);
            Assert.Equal(1f, snapshot.Stats.DamageMultiplier);
        }

        [Fact]
        public void Passive_pieces_are_not_weapons()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.phra_amulet", 0, 0);
            InventoryFixture.Place(editor, "item.spell_tome", 1, 1);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            Assert.Empty(snapshot.Weapons);
            Assert.Empty(snapshot.Summons);
        }

        [Fact]
        public void Offerings_of_one_id_are_aggregated_into_one_entry()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.red_soda", 0, 0);
            InventoryFixture.Place(editor, "item.red_soda", 1, 0);
            InventoryFixture.Place(editor, "item.boiled_egg", 2, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            Assert.Equal(2, snapshot.Consumables.Count);
            Assert.Equal("item.boiled_egg", snapshot.Consumables[0].ItemId);
            Assert.Equal(1, snapshot.Consumables[0].Count);
            Assert.Equal("item.red_soda", snapshot.Consumables[1].ItemId);
            Assert.Equal(2, snapshot.Consumables[1].Count);
        }

        /// <summary>
        /// Every charm on the board moves the player stat its row names, and no charm is inert
        /// (Stage 11a). The expected value is computed from the row itself, so a data edit moves
        /// the test with it rather than pinning a balance number here.
        /// </summary>
        [Fact]
        public void Every_charm_changes_the_player_stat_its_row_names()
        {
            IGameConfig config = InventoryFixture.Config();
            int covered = 0;

            for (int i = 0; i < config.Items.Count; i++)
            {
                ItemConfig charm = config.Items[i];
                if (charm.Category != ItemCategory.Charm)
                {
                    continue;
                }

                Assert.True(charm.HasCharmEffect, charm.Id + " has no effect columns");

                BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
                InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
                InventoryFixture.Place(editor, charm.Id, 0, 0);

                CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);
                ConstantConfig constant = config.Constant;

                switch (charm.CharmStat)
                {
                    case UpgradeStat.MaxHp:
                        Assert.Equal(constant.MaxHp + (int)charm.CharmMagnitude, snapshot.Stats.MaxHp);
                        break;
                    case UpgradeStat.MaxMp:
                        Assert.Equal(constant.MaxMp + (int)charm.CharmMagnitude, snapshot.Stats.MaxMp);
                        break;
                    case UpgradeStat.MoveSpeed:
                    {
                        int penalty = WeightPenalty.Percent(backpack.BoardWeight, constant);
                        float baseSpeed = constant.MoveSpeed * (1f - (penalty / 100f));
                        Assert.Equal(
                            baseSpeed * (1f + (charm.CharmMagnitude / 100f)),
                            snapshot.Stats.MoveSpeed, 4);
                        break;
                    }

                    default:
                        Assert.Fail(charm.Id + " names a stat this test does not know: " + charm.CharmStat);
                        break;
                }

                covered++;
            }

            Assert.True(covered >= 3, "the data must carry at least the three charm rows, found " + covered);
        }

        /// <summary>
        /// An offering's effect, amount, uses per piece and auto-use threshold reach the snapshot
        /// exactly as the item row states them (Stage 11a).
        /// </summary>
        [Fact]
        public void Every_offering_carries_its_effect_uses_and_auto_use_threshold()
        {
            IGameConfig config = InventoryFixture.Config();
            int covered = 0;

            for (int i = 0; i < config.Items.Count; i++)
            {
                ItemConfig definition = config.Items[i];
                if (definition.Category != ItemCategory.Offering)
                {
                    continue;
                }

                BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
                InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
                InventoryFixture.Place(editor, definition.Id, 0, 0);

                CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);
                ConsumableLoadout entry = Assert.Single(snapshot.Consumables);

                Assert.Equal(definition.Id, entry.ItemId);
                Assert.Equal(1, entry.Count);
                Assert.Equal(definition.OfferingEffect, entry.Effect);
                Assert.Equal(definition.OfferingAmount, entry.EffectAmount);
                Assert.Equal(definition.OfferingUses, entry.UsesPerItem);
                Assert.Equal(definition.OfferingAutoHpPercent, entry.AutoUseHpPercent);
                covered++;
            }

            Assert.True(covered >= 2, "the data must carry at least the two offering rows, found " + covered);
        }

        [Fact]
        public void An_active_cooldown_synergy_is_already_applied_to_the_weapon()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.takrut_tone", 0, 0);
            InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            // The takrut is an aura weapon itself, so there are two weapons; the yant is the
            // one the rule shortens.
            Assert.Equal(2, snapshot.Weapons.Count);
            WeaponLoadout weapon = Weapon(snapshot, "skill.fire_bolt");

            // 0.9 s * 0.85 from synergy.takrut_yant_cooldown. The battle must not have to
            // know the rule to be correct ("Battle ไม่อ่านตำแหน่ง UI และไม่คำนวณคอมโบเอง").
            Assert.Equal(0.9f * 0.85f, weapon.CooldownSeconds, 4);
            // +10 % from the bag-area rule, and no other damage rule is active. The base is
            // read from the row, so a balance pass (Stage 11d) moves the test with it.
            Assert.Equal(
                Rounded(BaseDamage(config, "skill.fire_bolt") * 1.1f), weapon.Damage);
            // The takrut keeps its own cooldown: the rule only ever shortens the yant.
            Assert.Equal(1.4f, Weapon(snapshot, "skill.takrut_ward").CooldownSeconds, 4);
        }

        [Fact]
        public void Damage_bonuses_add_before_they_are_applied()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);
            InventoryFixture.Place(editor, "item.spell_tome", 0, 0);
            InventoryFixture.Place(editor, "item.spell_tome", 3, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 2, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);
            WeaponLoadout weapon = Assert.Single(snapshot.Weapons);

            // The knife's tome damage: +20 +20 from the two tomes it touches (ADR 0031),
            // +10 from the bag area -> +50 %.
            Assert.Equal(Rounded(BaseDamage(config, "skill.knife_thrust") * 1.5f), weapon.Damage);
        }

        [Fact]
        public void The_power_decay_reaches_the_curse_summon_and_the_holy_weapon()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);
            InventoryFixture.Place(editor, "item.sai_sin_thread", 2, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            // Both rows take the curse-power decay (x0.8) and are rounded to whole damage.
            Assert.Equal(
                Rounded(BaseDamage(config, "skill.curse_wave") * 0.8f),
                Assert.Single(snapshot.Summons).Damage);
            Assert.Equal(
                Rounded(BaseDamage(config, "skill.thread_pierce") * 0.8f),
                Assert.Single(snapshot.Weapons).Damage);
        }

        [Fact]
        public void The_familiar_attack_speed_synergy_shortens_the_summon_cooldown()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.kuman_thong", 1, 0);
            InventoryFixture.Place(editor, "item.red_soda", 0, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            // 1.5 s * 0.75 from synergy.offering_familiar_speed.
            Assert.Equal(1.5f * 0.75f, Assert.Single(snapshot.Summons).CooldownSeconds, 4);
        }

        [Fact]
        public void A_bonus_never_pushes_a_cooldown_below_the_configured_floor()
        {
            IGameConfig config = InventoryFixture.Config();
            // The prototype's own numbers cannot reach the floor (the largest reduction in
            // data is 25 %), so the floor itself is what this test moves.
            config.Constant.MinCooldownSeconds = 1.0f;

            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.takrut_tone", 0, 0);
            InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 0);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            // 0.9 s * 0.85 = 0.765 s, held up to the configured floor of 1.0 s.
            Assert.Equal(1.0f, Weapon(snapshot, "skill.fire_bolt").CooldownSeconds, 4);
        }

        [Fact]
        public void Weight_slows_the_mor_down_and_stops_at_the_cap()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);

            LoadoutResolver resolver = Resolver(config);

            // Bag only: 1.0 weight * 5 percent = 5 %.
            CombatLoadoutSnapshot light = resolver.Resolve(backpack);
            Assert.Equal(5, light.Stats.SpeedPenaltyPercent);
            Assert.Equal(config.Constant.MoveSpeed * 0.95f, light.Stats.MoveSpeed, 4);

            // A heavy board: the penalty clamps at constant.max_speed_penalty_percent.
            InventoryFixture.Place(editor, "item.spell_tome", 1, 1);
            InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 2, 0);

            CombatLoadoutSnapshot heavy = resolver.Resolve(backpack);
            float weight = backpack.BoardWeight;
            Assert.True(weight > 3f, "the fixture should be carrying real weight, was " + weight);
            Assert.Equal(
                (int)(weight * config.Constant.WeightSpeedPercentPerUnit),
                heavy.Stats.SpeedPenaltyPercent);
            Assert.True(heavy.Stats.SpeedPenaltyPercent <= config.Constant.MaxSpeedPenaltyPercent);
        }

        [Fact]
        public void The_penalty_cap_is_respected_for_an_absurd_weight()
        {
            IGameConfig config = InventoryFixture.Config();
            ConstantConfig constant = config.Constant;

            Assert.Equal(40, WeightPenalty.Percent(1000f, constant));
            Assert.Equal(0, WeightPenalty.Percent(0f, constant));
            Assert.Equal(0, WeightPenalty.Percent(-5f, constant));
            Assert.Equal(5, WeightPenalty.Percent(1f, constant));
            Assert.Equal(3, WeightPenalty.Percent(0.7f, constant));
        }

        [Fact]
        public void Identical_layouts_built_in_any_order_produce_identical_snapshots()
        {
            IGameConfig config = InventoryFixture.Config();
            IGameConfig config2 = InventoryFixture.Config();

            // takrut and yant next to each other, so the cooldown synergy is active while
            // the two backpacks are built in opposite orders.
            BackpackEditor forward = InventoryFixture.NewEditor(config, out Backpack forwardBag);
            InventoryFixture.PlaceBag(forward, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(forward, "item.kali_flame_yant", 1, 0);
            InventoryFixture.Place(forward, "item.takrut_tone", 0, 0);
            InventoryFixture.Place(forward, "item.kuman_thong", 1, 2);
            InventoryFixture.Place(forward, "item.red_soda", 2, 0);

            BackpackEditor reverse = InventoryFixture.NewEditor(config2, out Backpack reverseBag);
            InventoryFixture.PlaceBag(reverse, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(reverse, "item.red_soda", 2, 0);
            InventoryFixture.Place(reverse, "item.kuman_thong", 1, 2);
            InventoryFixture.Place(reverse, "item.takrut_tone", 0, 0);
            InventoryFixture.Place(reverse, "item.kali_flame_yant", 1, 0);

            CombatLoadoutSnapshot left = Resolver(config).Resolve(forwardBag);
            CombatLoadoutSnapshot right = Resolver(config2).Resolve(reverseBag);

            // Every field, in order: the loadout is a function of the layout, not of the
            // dictionary iteration order or the build history. (The backpack snapshots
            // differ on purpose - they carry instance ids, which exist to tell two equal
            // pieces apart.)
            AssertSnapshotsEqual(left, right);
        }

        [Fact]
        public void The_snapshot_is_recomputed_only_when_the_set_changes()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);

            LoadoutResolver resolver = Resolver(config);

            CombatLoadoutSnapshot first = resolver.Resolve(backpack);
            Assert.Equal(1, resolver.ResolveCount);

            // Nothing moved: the same snapshot comes back, not a new one.
            Assert.Same(first, resolver.Resolve(backpack));
            Assert.Same(first, resolver.Resolve(backpack));
            Assert.Equal(1, resolver.ResolveCount);

            // A move changes the layout, so the loadout is rebuilt.
            Assert.True(editor.Move(knife, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            CombatLoadoutSnapshot second = resolver.Resolve(backpack);
            Assert.Equal(2, resolver.ResolveCount);
            Assert.NotSame(first, second);

            // A rejected command changes nothing, so it must not cost a recompute either.
            Assert.True(editor.Move(knife, new CellOffset(7, 7), Rotation.Degrees0).IsFailure);
            Assert.Same(second, resolver.Resolve(backpack));
            Assert.Equal(2, resolver.ResolveCount);
        }

        [Fact]
        public void Two_backpacks_never_share_a_cached_snapshot()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor a = InventoryFixture.NewEditor(config, out Backpack first);
            InventoryFixture.PlaceBag(a, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(a, "item.mor_knife", 0, 0);

            BackpackEditor b = InventoryFixture.NewEditor(config, out Backpack second);
            InventoryFixture.PlaceBag(b, "bag.raw_cotton_bag", 0, 0);

            LoadoutResolver resolver = Resolver(config);

            Assert.Single(resolver.Resolve(first).Weapons);
            Assert.Empty(resolver.Resolve(second).Weapons);
            Assert.Single(resolver.Resolve(first).Weapons);
            Assert.Equal(3, resolver.ResolveCount);
        }

        [Fact]
        public void A_merged_tier_travels_into_the_loadout()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef first = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            PieceRef second = InventoryFixture.Add(editor, "item.mor_knife");

            Assert.True(InventoryFixture.Merge(editor, second, first).IsSuccess);

            CombatLoadoutSnapshot snapshot = Resolver(config).Resolve(backpack);

            Assert.Equal(2, Assert.Single(snapshot.Weapons).Tier);
        }

        [Fact]
        public void Resolver_requires_its_inputs()
        {
            IGameConfig config = InventoryFixture.Config();
            var evaluator = new SynergyEvaluator(config);
            var resolver = new LoadoutResolver(config, evaluator);

            Assert.Throws<System.ArgumentNullException>(() => resolver.Resolve(null!));
            Assert.Throws<System.ArgumentNullException>(() => new LoadoutResolver(null!, evaluator));
            Assert.Throws<System.ArgumentNullException>(() => new LoadoutResolver(config, null!));
        }

        /// <summary>Rounds the way the resolver does: the nearest whole damage, half away from zero.</summary>
        private static int Rounded(float value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);

        /// <summary>The damage the skill's own row carries (a data number, not a test constant).</summary>
        private static int BaseDamage(IGameConfig config, string skillId) =>
            ConfigFixture.Skill(config, skillId).Damage;

        private static WeaponLoadout Weapon(CombatLoadoutSnapshot snapshot, string skillId)
        {
            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                if (snapshot.Weapons[i].SkillId == skillId)
                {
                    return snapshot.Weapons[i];
                }
            }

            throw new System.InvalidOperationException("the snapshot has no weapon '" + skillId + "'");
        }

        /// <summary>
        /// Compares every field of two snapshots: stats, then each list element by element
        /// (the requirement is "เหมือนกันทุก field", so a missing field would be a hole).
        /// </summary>
        private static void AssertSnapshotsEqual(CombatLoadoutSnapshot left, CombatLoadoutSnapshot right)
        {
            Assert.Equal(left.Stats.MaxHp, right.Stats.MaxHp);
            Assert.Equal(left.Stats.MaxMp, right.Stats.MaxMp);
            Assert.Equal(left.Stats.MoveSpeed, right.Stats.MoveSpeed, 4);
            Assert.Equal(left.Stats.PickupRadius, right.Stats.PickupRadius, 4);
            Assert.Equal(left.Stats.FocusPerSecond, right.Stats.FocusPerSecond, 4);
            Assert.Equal(left.Stats.CooldownMultiplier, right.Stats.CooldownMultiplier, 4);
            Assert.Equal(left.Stats.DamageMultiplier, right.Stats.DamageMultiplier, 4);
            Assert.Equal(left.Stats.SpeedPenaltyPercent, right.Stats.SpeedPenaltyPercent);

            Assert.Equal(left.Weapons.Count, right.Weapons.Count);
            for (int i = 0; i < left.Weapons.Count; i++)
            {
                AssertWeaponsEqual(left.Weapons[i], right.Weapons[i]);
            }

            Assert.Equal(left.Summons.Count, right.Summons.Count);
            for (int i = 0; i < left.Summons.Count; i++)
            {
                Assert.Equal(left.Summons[i].SkillId, right.Summons[i].SkillId);
                Assert.Equal(left.Summons[i].Tier, right.Summons[i].Tier);
                Assert.Equal(left.Summons[i].CooldownSeconds, right.Summons[i].CooldownSeconds, 4);
                Assert.Equal(left.Summons[i].MpCost, right.Summons[i].MpCost);
                Assert.Equal(left.Summons[i].Damage, right.Summons[i].Damage);
                Assert.Equal(left.Summons[i].Range, right.Summons[i].Range, 4);
            }

            Assert.Equal(left.Consumables.Count, right.Consumables.Count);
            for (int i = 0; i < left.Consumables.Count; i++)
            {
                Assert.Equal(left.Consumables[i].ItemId, right.Consumables[i].ItemId);
                Assert.Equal(left.Consumables[i].Count, right.Consumables[i].Count);
                Assert.Equal(left.Consumables[i].SkillId, right.Consumables[i].SkillId);
            }
        }

        private static void AssertWeaponsEqual(WeaponLoadout left, WeaponLoadout right)
        {
            Assert.Equal(left.SkillId, right.SkillId);
            Assert.Equal(left.Tier, right.Tier);
            Assert.Equal(left.Behavior, right.Behavior);
            Assert.Equal(left.Element, right.Element);
            Assert.Equal(left.CooldownSeconds, right.CooldownSeconds, 4);
            Assert.Equal(left.MpCost, right.MpCost);
            Assert.Equal(left.Damage, right.Damage);
            Assert.Equal(left.ProjectileSpeed, right.ProjectileSpeed, 4);
            Assert.Equal(left.Range, right.Range, 4);
            Assert.Equal(left.PierceCount, right.PierceCount);
            Assert.Equal(left.ProjectileCount, right.ProjectileCount);
        }
    }
}
