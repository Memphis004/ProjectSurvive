using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 09f: merging two equal pieces asks the player to pick one of
    /// <c>constant.upgrade_choice_count</c> upgrade variants, and the pick is what the
    /// survivor keeps for that step.
    /// </summary>
    /// <remarks>
    /// Every case runs over the real generated tables, so the pools, the weights and the
    /// modifiers are the shipped ones. The numbers this file asserts are written out by hand
    /// from <c>data/tables/upgrade_variant_modifier.csv</c>: a test that re-read the same row
    /// to compute its expectation would pass even if the row stopped reaching the battle.
    /// </remarks>
    public sealed class UpgradeOfferTests
    {
        private const uint Seed = InventoryFixture.MergeSeed;

        // -------------------------------------------------------------------
        // which pieces have a chooser
        // -------------------------------------------------------------------

        [Fact]
        public void An_arcane_weapon_offers_the_configured_number_of_distinct_choices()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");

            Result<MergeOffer> offered = editor.OfferMerge(right, left, Seed);

            Assert.True(offered.IsSuccess, offered.Error.ToString());
            MergeOffer offer = offered.Value;
            Assert.True(offer.HasChoices, "an ArcaneWeapon 1 -> 2 step must offer choices");
            Assert.Equal(config.Constant.UpgradeChoiceCount, offer.Options.Count);
            Assert.Equal(1, offer.FromTier);
            Assert.Equal(2, offer.ToTier);
            Assert.Equal("item.mor_knife", offer.ItemId);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < offer.Options.Count; i++)
            {
                Assert.True(seen.Add(offer.Options[i].Id), "a choice may not be offered twice");
                Assert.Equal(1, offer.Options[i].FromTier);
                Assert.NotEmpty(offer.Options[i].Modifiers);
            }
        }

        [Fact]
        public void A_piece_no_variant_matches_still_folds_immediately()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef first = InventoryFixture.Add(editor, "item.tiger_fang");
            PieceRef second = InventoryFixture.Add(editor, "item.tiger_fang");

            Result<MergeOffer> offered = editor.OfferMerge(second, first, Seed);
            Assert.True(offered.IsSuccess, offered.Error.ToString());
            Assert.False(offered.Value.HasChoices, "a charm has no upgrade variants in this data");

            Result<MergeOutcome> merged = editor.Merge(second, first);

            Assert.True(merged.IsSuccess, merged.Error.ToString());
            Assert.Equal(2, merged.Value.NewTier);
            Assert.Equal(string.Empty, merged.Value.VariantId);

            // The step still takes its slot, empty, so a later 2 -> 3 merge keeps its numbering.
            ItemInstance survivor = InventoryFixture.ItemOf(backpack, first);
            Assert.Equal(2, survivor.Tier);
            Assert.Single(survivor.VariantIds);
            Assert.Equal(ItemInstance.NoVariant, survivor.VariantIds[0]);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void A_tag_scoped_variant_joins_only_the_pool_of_a_piece_that_carries_the_tag()
        {
            IGameConfig config = InventoryFixture.Config();
            var pool = new List<UpgradeVariantConfig>(8);

            // item.kali_flame_yant carries `fire`; item.takrut_tone does not.
            UpgradePool.Collect(config, ConfigFixture.Item(config, "item.kali_flame_yant"), 1, pool);
            Assert.Contains(pool, variant => variant.Id == "variant.fire.blazing_seal");

            UpgradePool.Collect(config, ConfigFixture.Item(config, "item.takrut_tone"), 1, pool);
            Assert.DoesNotContain(pool, variant => variant.Id == "variant.fire.blazing_seal");
            Assert.Contains(pool, variant => variant.Id == "variant.arcane.keen_edge");
        }

        // -------------------------------------------------------------------
        // the offer is a pure function of the state
        // -------------------------------------------------------------------

        [Fact]
        public void Offer_is_the_same_every_time_and_changes_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            string[] first = Ids(editor.OfferMerge(right, left, Seed));
            string[] second = Ids(editor.OfferMerge(right, left, Seed));
            string[] otherSeed = Ids(editor.OfferMerge(right, left, Seed + 1));

            Assert.Equal(first, second);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(revision, backpack.Revision);
            Assert.Equal(0, backpack.MergeCount);

            // A different run seed is a different key, and a different key means a different
            // draw: asking twice is stable, asking about another run is not the same question.
            Assert.NotEqual(first, otherSeed);
        }

        [Fact]
        public void The_draw_is_a_function_of_every_input_in_its_key()
        {
            uint baseline = UpgradePool.DeriveSeed(7u, 0, "item.mor_knife", 1);

            Assert.Equal(baseline, UpgradePool.DeriveSeed(7u, 0, "item.mor_knife", 1));
            Assert.NotEqual(baseline, UpgradePool.DeriveSeed(8u, 0, "item.mor_knife", 1));
            Assert.NotEqual(baseline, UpgradePool.DeriveSeed(7u, 1, "item.mor_knife", 1));
            Assert.NotEqual(baseline, UpgradePool.DeriveSeed(7u, 0, "item.takrut_tone", 1));
            Assert.NotEqual(baseline, UpgradePool.DeriveSeed(7u, 0, "item.mor_knife", 2));
        }

        // -------------------------------------------------------------------
        // confirming the choice
        // -------------------------------------------------------------------

        [Fact]
        public void Merge_is_refused_for_a_pair_with_choices_and_changes_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);

            Result<MergeOutcome> merged = editor.Merge(right, left);

            Assert.True(merged.IsFailure);
            Assert.Equal(InventoryError.MergeNeedsVariant, merged.Error.Code);
            Assert.Equal(ErrorKind.Validation, merged.Error.Kind);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Confirming_an_id_that_is_not_offered_is_refused_and_changes_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);

            // An id the draw did not pick, and an id that belongs to the NEXT step: both are
            // rejected, so nothing can be confirmed into a pool it was never offered from.
            Result<MergeOutcome> invented =
                editor.ConfirmMerge(right, left, "variant.arcane.keen_edge.nope", Seed);
            Result<MergeOutcome> nextStep =
                editor.ConfirmMerge(right, left, "variant.arcane.twin_bolt", Seed);

            Assert.Equal(InventoryError.MergeVariantNotOffered, invented.Error.Code);
            Assert.Equal(ErrorKind.Validation, invented.Error.Kind);
            Assert.Equal(InventoryError.MergeVariantNotOffered, nextStep.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Confirming_without_a_choice_is_refused_and_changes_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);

            Result<MergeOutcome> merged = editor.ConfirmMerge(right, left, string.Empty, Seed);

            Assert.Equal(InventoryError.MergeVariantRequired, merged.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Confirming_keeps_the_chosen_variant_on_the_survivor_for_that_step()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");
            string chosen = Ids(editor.OfferMerge(right, left, Seed))[1];

            Result<MergeOutcome> merged = editor.ConfirmMerge(right, left, chosen, Seed);

            Assert.True(merged.IsSuccess, merged.Error.ToString());
            Assert.Equal(2, merged.Value.NewTier);
            Assert.Equal(chosen, merged.Value.VariantId);

            ItemInstance survivor = InventoryFixture.ItemOf(backpack, left);
            Assert.Equal(new[] { chosen }, survivor.VariantIds);
            Assert.Equal(1, backpack.MergeCount);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Undo_restores_the_counter_so_repeating_the_merge_offers_the_same_choices()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");

            string[] before = Ids(editor.OfferMerge(right, left, Seed));
            Assert.True(editor.ConfirmMerge(right, left, before[0], Seed).IsSuccess);
            Assert.Equal(1, backpack.MergeCount);

            Assert.True(editor.Undo().IsSuccess);

            Assert.Equal(0, backpack.MergeCount);
            string[] after = Ids(editor.OfferMerge(right, left, Seed));

            // The whole point of putting the ordinal in the draw key AND in the undoable
            // state: an undone merge is not a free re-roll.
            Assert.Equal(before, after);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void A_second_merge_in_the_same_run_draws_its_own_choices()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef[] pieces = new PieceRef[4];
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i] = InventoryFixture.Add(editor, "item.mor_knife");
            }

            string[] first = Ids(editor.OfferMerge(pieces[1], pieces[0], Seed));
            Assert.True(editor.ConfirmMerge(pieces[1], pieces[0], first[0], Seed).IsSuccess);

            string[] second = Ids(editor.OfferMerge(pieces[3], pieces[2], Seed));

            Assert.Equal(1, backpack.MergeCount);
            Assert.NotEqual(first, second);
        }

        // -------------------------------------------------------------------
        // the survivor's history, step by step
        // -------------------------------------------------------------------

        [Fact]
        public void The_result_keeps_the_step_one_variant_of_the_piece_it_folded_into()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef[] pieces = new PieceRef[4];
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i] = InventoryFixture.Add(editor, "item.mor_knife");
            }

            // Two tier-2 knives whose step-1 choices differ: each picks from its own offer,
            // and the second deliberately avoids the first's id.
            string kept = Ids(editor.OfferMerge(pieces[1], pieces[0], Seed))[0];
            Assert.True(editor.ConfirmMerge(pieces[1], pieces[0], kept, Seed).IsSuccess);

            string discarded = Ids(editor.OfferMerge(pieces[3], pieces[2], Seed))[0];
            if (discarded == kept)
            {
                discarded = Ids(editor.OfferMerge(pieces[3], pieces[2], Seed))[1];
            }

            Assert.True(editor.ConfirmMerge(pieces[3], pieces[2], discarded, Seed).IsSuccess);
            Assert.NotEqual(kept, discarded);

            // "ยังไม่ทำการเลือกชิ้นฐาน": the piece the other was folded into keeps its own
            // step-1 variant (ADR 0021).
            Result<MergeOutcome> merged = InventoryFixture.Merge(editor, pieces[2], pieces[0]);

            Assert.True(merged.IsSuccess, merged.Error.ToString());
            ItemInstance survivor = InventoryFixture.ItemOf(backpack, pieces[0]);
            Assert.Equal(3, survivor.Tier);
            Assert.Equal(2, survivor.VariantIds.Count);
            Assert.Equal(kept, survivor.VariantIds[0]);
            Assert.Equal(merged.Value.VariantId, survivor.VariantIds[1]);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Every_step_modifier_reaches_the_battle_loadout()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef survivor = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            PieceRef[] others = new PieceRef[3];
            for (int i = 0; i < others.Length; i++)
            {
                others[i] = InventoryFixture.Add(editor, "item.mor_knife");
            }

            var resolver = new LoadoutResolver(config, new SynergyEvaluator(config));
            WeaponLoadout plain = Weapon(resolver.Resolve(backpack), "skill.knife_thrust");

            // The baseline: no step has been chosen yet, so the weapon is the data's skill with
            // the layout's combos on it - one knife and its bag are `synergy.bag_area_bonus`
            // (+10% damage), which is 15 off a base of 14.
            Assert.Equal(15, plain.Damage);
            Assert.Equal(1, plain.Tier);

            string stepOne = Ids(editor.OfferMerge(others[0], survivor, Seed))[0];
            Assert.True(editor.ConfirmMerge(others[0], survivor, stepOne, Seed).IsSuccess);

            WeaponLoadout afterFirst = Weapon(resolver.Resolve(backpack), "skill.knife_thrust");
            Assert.Equal(2, afterFirst.Tier);
            AssertOneStepMatches(config, stepOne, plain, afterFirst);

            // A tier-2 pair now folds into tier 3: both steps' modifiers apply to one weapon.
            Assert.True(InventoryFixture.Merge(editor, others[1], others[2]).IsSuccess);
            PieceRef secondTierTwo = InventoryFixture.ItemOf(backpack, others[2]).Ref;
            Result<MergeOutcome> third = InventoryFixture.Merge(editor, secondTierTwo, survivor);
            Assert.True(third.IsSuccess, third.Error.ToString());

            WeaponLoadout upgraded = Weapon(resolver.Resolve(backpack), "skill.knife_thrust");
            Assert.Equal(3, upgraded.Tier);
            AssertBothStepsMatch(config, stepOne, third.Value.VariantId, plain.Damage, upgraded);
        }

        [Fact]
        public void A_player_stat_variant_reaches_the_stat_block_of_the_board()
        {
            // No shipped variant targets a player stat yet, so this pins the wiring the way
            // the data would use it: the real config with one hand-added variant as the only
            // choice of the step.
            IGameConfig config = InventoryFixture.Config();

            // A shipped variant, retargeted: the config's lookup dictionary already holds this
            // object, and rewiring its modifiers is how the data would spell "this upgrade
            // raises max HP". The other step-1 choices are dropped so the pool is exactly one
            // entry and the offer cannot be the wrong id.
            Assert.True(config.TryGetUpgradeVariant("variant.arcane.keen_edge", out UpgradeVariantConfig variant));
            variant.Modifiers = new List<UpgradeModifierConfig>
            {
                new UpgradeModifierConfig
                {
                    Id = "mod.test.max_hp",
                    Stat = UpgradeStat.MaxHp,
                    Method = UpgradeMethod.Percent,
                    Magnitude = 50f,
                },
            };

            var variants = (List<UpgradeVariantConfig>)config.UpgradeVariants;
            variants.RemoveAll(candidate => candidate.FromTier == 1);
            variants.Add(variant);

            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef survivor = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            PieceRef second = InventoryFixture.Add(editor, "item.mor_knife");

            var resolver = new LoadoutResolver(config, new SynergyEvaluator(config));
            Assert.Equal(config.Constant.MaxHp, resolver.Resolve(backpack).Stats.MaxHp);

            Assert.True(editor.ConfirmMerge(second, survivor, variant.Id, Seed).IsSuccess);
            Assert.Equal(new[] { variant.Id }, InventoryFixture.ItemOf(backpack, survivor).VariantIds);

            Assert.Equal((config.Constant.MaxHp * 3) / 2, resolver.Resolve(backpack).Stats.MaxHp);
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static string[] Ids(Result<MergeOffer> offer)
        {
            Assert.True(offer.IsSuccess, offer.Error.ToString());
            var ids = new string[offer.Value.Options.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = offer.Value.Options[i].Id;
            }

            return ids;
        }

        private static WeaponLoadout Weapon(CombatLoadoutSnapshot snapshot, string skillId)
        {
            for (int i = 0; i < snapshot.Weapons.Count; i++)
            {
                if (snapshot.Weapons[i].SkillId == skillId)
                {
                    return snapshot.Weapons[i];
                }
            }

            throw new InvalidOperationException("the loadout has no weapon '" + skillId + "'");
        }

        /// <summary>
        /// Hand-written expectations for one shipped 1 -> 2 variant of an arcane weapon, read
        /// off <c>data/tables/upgrade_variant_modifier.csv</c>. The knife's skill is 14 damage,
        /// 0.6s cooldown, 0 MP and 6 range.
        /// </summary>
        private static void AssertOneStepMatches(
            IGameConfig config, string variantId, WeaponLoadout plain, WeaponLoadout weapon)
        {
            ItemConfig knife = ConfigFixture.Item(config, "item.mor_knife");
            Assert.Equal("skill.knife_thrust", knife.SkillId);
            SkillConfig skill = ConfigFixture.Skill(config, knife.SkillId);

            // The `plain` numbers are the un-upgraded weapon (15 damage, 0.6s, 6 range), so
            // each case below states what its own modifier did to exactly one of them.
            switch (variantId)
            {
                case "variant.arcane.keen_edge":
                    Assert.Equal(19, weapon.Damage);        // 15 + 25%
                    Assert.Equal(plain.CooldownSeconds, weapon.CooldownSeconds);
                    Assert.Equal(plain.Range, weapon.Range);
                    break;

                case "variant.arcane.swift_chant":
                    Assert.Equal(plain.Damage, weapon.Damage);
                    Assert.Equal(0.51f, weapon.CooldownSeconds, 3);   // 0.6 - 15%
                    break;

                case "variant.arcane.frugal_mantra":
                    Assert.Equal(plain.Damage, weapon.Damage);
                    Assert.Equal(Math.Max(0, skill.MpCost - 2), weapon.MpCost);
                    break;

                case "variant.arcane.far_sight":
                    Assert.Equal(plain.Damage, weapon.Damage);
                    Assert.Equal(7.2f, weapon.Range, 3);    // 6 + 20%
                    break;

                case "variant.fire.blazing_seal":
                    Assert.Equal(18, weapon.Damage);        // 15 + 20%
                    Assert.Equal(Math.Max(0, skill.MpCost - 1), weapon.MpCost);
                    break;

                default:
                    Assert.Fail("unexpected 1 -> 2 variant for the weapon assertion: " + variantId);
                    break;
            }
        }

        /// <summary>
        /// Two steps on one weapon. Grouped application: the flat adds, then the percents,
        /// then the factors — so the two damage percents add up before the base is scaled
        /// (ADR 0021).
        /// </summary>
        private static void AssertBothStepsMatch(
            IGameConfig config, string stepOne, string stepTwo, int plainDamage, WeaponLoadout weapon)
        {
            float flat = 0f;
            float percent = 0f;

            Append(stepOne);
            Append(stepTwo);

            float expected = (plainDamage + flat) * (1f + (percent / 100f));
            Assert.Equal(
                (int)Math.Round(expected, MidpointRounding.AwayFromZero),
                weapon.Damage);

            void Append(string variantId)
            {
                Assert.True(config.TryGetUpgradeVariant(variantId, out UpgradeVariantConfig variant), variantId);
                for (int i = 0; i < variant.Modifiers.Count; i++)
                {
                    UpgradeModifierConfig modifier = variant.Modifiers[i];
                    if (modifier.Stat != UpgradeStat.Damage)
                    {
                        continue;
                    }

                    if (modifier.Method == UpgradeMethod.Add)
                    {
                        flat += modifier.Magnitude;
                    }
                    else if (modifier.Method == UpgradeMethod.Percent)
                    {
                        percent += modifier.Magnitude;
                    }
                }
            }
        }
    }
}
