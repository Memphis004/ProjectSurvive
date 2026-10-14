using System.Collections.Generic;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 task 7: the five GDD combo rules over the Stage 04 tag vocabulary —
    /// 4-direction adjacency counted once per pair, same-row stacking with a cap, the
    /// power-decay rule on both pieces, the bag-area bonus, and a single pass (no loop).
    /// </summary>
    public sealed class SynergyEvaluatorTests
    {
        /// <summary>One 3x3 bag at the origin: columns 0-2, rows 0-2 are ActiveSlots.</summary>
        private static BackpackEditor WithBag(IGameConfig config, out Backpack backpack)
        {
            BackpackEditor editor = InventoryFixture.NewEditor(config, out backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            return editor;
        }

        /// <summary>Two 3x3 bags: columns 0-5, rows 0-2 are ActiveSlots.</summary>
        private static BackpackEditor WithTwoBags(IGameConfig config, out Backpack backpack)
        {
            BackpackEditor editor = WithBag(config, out backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);
            return editor;
        }

        [Fact]
        public void Takrut_next_to_a_yant_weapon_shortens_only_that_weapon()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            PieceRef takrut = InventoryFixture.Place(editor, "item.takrut_tone", 0, 0);
            PieceRef yant = InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 0);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(0.85f, evaluation.For(yant.InstanceId).CooldownMultiplier, 4);
            Assert.Equal(1f, evaluation.For(takrut.InstanceId).CooldownMultiplier, 4);
            // Two rule applications on the yant: the takrut's cooldown and the bag-area bonus.
            Assert.Equal(2, evaluation.For(yant.InstanceId).ActiveSynergyCount);
            Assert.Contains(
                evaluation.Active,
                entry => entry.RuleId == "synergy.takrut_yant_cooldown"
                    && entry.TargetInstanceId == yant.InstanceId
                    && entry.SourceInstanceId == takrut.InstanceId);
        }

        [Fact]
        public void A_yant_two_tiles_away_gets_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            InventoryFixture.Place(editor, "item.takrut_tone", 0, 0);
            PieceRef yant = InventoryFixture.Place(editor, "item.kali_flame_yant", 2, 0);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1f, evaluation.For(yant.InstanceId).CooldownMultiplier, 4);
        }

        [Fact]
        public void A_diagonal_neighbour_is_not_adjacent()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            InventoryFixture.Place(editor, "item.takrut_tone", 0, 0);
            PieceRef yant = InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 1);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1f, evaluation.For(yant.InstanceId).CooldownMultiplier, 4);
        }

        [Fact]
        public void Adjacency_is_counted_once_per_pair_even_when_two_cells_touch()
        {
            // Shipped data has no adjacent pair where both pieces cover more than one
            // cell (kuman_thong 2x1 meets only 1x1 pieces), so the tag vocabulary is
            // extended here to build one: the same evaluator rule is under test, and the
            // tags are data.
            IGameConfig config = InventoryFixture.Config();
            ConfigFixture.Item(config, "item.spell_tome").Tags = new List<string> { "tome", "offering" };

            BackpackEditor editor = WithTwoBags(config, out Backpack backpack);
            // The familiar stands on its end (1 column, 2 rows) so the 2x2 tome beside it
            // shares two edges with the same pair of pieces.
            PieceRef familiar = InventoryFixture.Place(editor, "item.kuman_thong", 0, 0, Rotation.Degrees90);
            InventoryFixture.Place(editor, "item.spell_tome", 1, 0);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // +25 % attack speed once, not twice: 0.75, never 0.5625.
            Assert.Equal(0.75f, evaluation.For(familiar.InstanceId).CooldownMultiplier, 4);
            Assert.Equal(
                1,
                evaluation.Active.Count(entry =>
                    entry.RuleId == "synergy.offering_familiar_speed" && entry.TargetInstanceId == familiar.InstanceId));
        }

        [Fact]
        public void A_second_offering_next_to_the_familiar_does_not_stack_past_the_cap()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            PieceRef familiar = InventoryFixture.Place(editor, "item.kuman_thong", 1, 0);
            InventoryFixture.Place(editor, "item.red_soda", 0, 0);
            InventoryFixture.Place(editor, "item.boiled_egg", 1, 1);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(0.75f, evaluation.For(familiar.InstanceId).CooldownMultiplier, 4);
            Assert.Equal(
                1,
                evaluation.Active.Count(entry =>
                    entry.RuleId == "synergy.offering_familiar_speed" && entry.TargetInstanceId == familiar.InstanceId));
            Assert.Equal(2, evaluation.For(familiar.InstanceId).ActiveSynergyCount);
        }

        [Fact]
        public void A_tome_in_the_same_row_adds_damage_and_two_tomes_stack_once_each()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 6, 0);

            InventoryFixture.Place(editor, "item.spell_tome", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 2, 0);

            SynergyEvaluation oneTome = new SynergyEvaluator(config).Evaluate(backpack);
            // One tome: +20 % for the row, +10 % from the bag area.
            Assert.Equal(1.3f, oneTome.For(knife.InstanceId).DamageMultiplier, 4);

            InventoryFixture.Place(editor, "item.spell_tome", 3, 0);
            SynergyEvaluation twoTomes = new SynergyEvaluator(config).Evaluate(backpack);
            Assert.Equal(1.5f, twoTomes.For(knife.InstanceId).DamageMultiplier, 4);

            // A third tome in the row is past max_stacks = 2 and adds nothing more.
            InventoryFixture.Place(editor, "item.spell_tome", 6, 0);
            SynergyEvaluation threeTomes = new SynergyEvaluator(config).Evaluate(backpack);
            Assert.Equal(1.5f, threeTomes.For(knife.InstanceId).DamageMultiplier, 4);
        }

        [Fact]
        public void A_tome_in_another_row_does_not_reach_the_weapon()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            InventoryFixture.Place(editor, "item.spell_tome", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 2, 2);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // Only the bag-area bonus is left.
            Assert.Equal(1.1f, evaluation.For(knife.InstanceId).DamageMultiplier, 4);
        }

        [Fact]
        public void Curse_next_to_holy_decays_both_pieces()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            PieceRef curse = InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);
            PieceRef holy = InventoryFixture.Place(editor, "item.sai_sin_thread", 2, 0);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // -30 % decay, +10 % from the bag area, on both sides of the rule.
            Assert.Equal(0.8f, evaluation.For(curse.InstanceId).DamageMultiplier, 4);
            Assert.Equal(0.8f, evaluation.For(holy.InstanceId).DamageMultiplier, 4);
            Assert.Equal(2, evaluation.Active.Count(entry => entry.RuleId == "synergy.curse_holy_debuff"));
        }

        [Fact]
        public void Two_curse_pieces_still_decay_a_holy_piece_once()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);
            InventoryFixture.Place(editor, "item.kuman_thong", 1, 1);
            PieceRef holy = InventoryFixture.Place(editor, "item.phra_amulet", 2, 0);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(0.8f, evaluation.For(holy.InstanceId).DamageMultiplier, 4);
            Assert.Equal(
                1,
                evaluation.Active.Count(entry =>
                    entry.RuleId == "synergy.curse_holy_debuff" && entry.TargetInstanceId == holy.InstanceId));
            Assert.Equal(3, evaluation.Active.Count(entry => entry.RuleId == "synergy.curse_holy_debuff"));
        }

        [Fact]
        public void The_bag_area_bonus_lands_once_per_item_whatever_the_shape()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            // A 2x2 item inside one bag: four of its cells are covered by that bag, and the
            // bonus must still be counted once ("many cells, one relation").
            PieceRef tome = InventoryFixture.Place(editor, "item.spell_tome", 0, 0);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1.1f, evaluation.For(tome.InstanceId).DamageMultiplier, 4);
            Assert.Equal(1, evaluation.Active.Count(entry => entry.RuleId == "synergy.bag_area_bonus"));
        }

        [Fact]
        public void Chest_pieces_take_no_part_in_any_rule()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            // takrut and yant both in the chest: adjacent on paper, but not on the board.
            PieceRef takrut = InventoryFixture.Add(editor, "item.takrut_tone");
            PieceRef yant = InventoryFixture.Add(editor, "item.kali_flame_yant");

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Empty(evaluation.Active);
            Assert.Equal(1f, evaluation.For(takrut.InstanceId).CooldownMultiplier, 4);
            Assert.Equal(1f, evaluation.For(yant.InstanceId).DamageMultiplier, 4);
        }

        [Fact]
        public void Evaluating_twice_gives_the_same_answer()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            PieceRef takrut = InventoryFixture.Place(editor, "item.takrut_tone", 0, 0);
            PieceRef yant = InventoryFixture.Place(editor, "item.kali_flame_yant", 1, 0);
            PieceRef holy = InventoryFixture.Place(editor, "item.sai_sin_thread", 2, 0);
            PieceRef familiar = InventoryFixture.Place(editor, "item.kuman_thong", 0, 1);
            Assert.NotEqual(PieceRef.None, takrut);

            var evaluator = new SynergyEvaluator(config);
            SynergyEvaluation first = evaluator.Evaluate(backpack);
            SynergyEvaluation second = evaluator.Evaluate(backpack);

            // No fixed point to iterate, so a rule can never feed itself: A raising B raising
            // A would show up as a different answer on the second pass.
            Assert.Equal(first.For(yant.InstanceId).CooldownMultiplier, second.For(yant.InstanceId).CooldownMultiplier, 4);
            Assert.Equal(first.For(yant.InstanceId).DamageMultiplier, second.For(yant.InstanceId).DamageMultiplier, 4);
            Assert.Equal(first.For(holy.InstanceId).DamageMultiplier, second.For(holy.InstanceId).DamageMultiplier, 4);
            Assert.Equal(first.For(familiar.InstanceId).CooldownMultiplier, second.For(familiar.InstanceId).CooldownMultiplier, 4);
            Assert.Equal(first.Active.Count, second.Active.Count);
            for (int i = 0; i < first.Active.Count; i++)
            {
                Assert.Equal(first.Active[i].RuleId, second.Active[i].RuleId);
                Assert.Equal(first.Active[i].SourceInstanceId, second.Active[i].SourceInstanceId);
                Assert.Equal(first.Active[i].TargetInstanceId, second.Active[i].TargetInstanceId);
            }
        }

        [Fact]
        public void Bonus_records_and_values_are_identical_whatever_the_build_order()
        {
            IGameConfig config = InventoryFixture.Config();
            IGameConfig config2 = InventoryFixture.Config();

            BackpackEditor forward = WithBag(config, out Backpack backpackForward);
            PieceRef yantForward = InventoryFixture.Place(forward, "item.kali_flame_yant", 1, 0);
            PieceRef takrutForward = InventoryFixture.Place(forward, "item.takrut_tone", 0, 0);

            BackpackEditor reverse = WithBag(config2, out Backpack backpackReverse);
            PieceRef takrutReverse = InventoryFixture.Place(reverse, "item.takrut_tone", 0, 0);
            PieceRef yantReverse = InventoryFixture.Place(reverse, "item.kali_flame_yant", 1, 0);

            SynergyEvaluation left = new SynergyEvaluator(config).Evaluate(backpackForward);
            SynergyEvaluation right = new SynergyEvaluator(config2).Evaluate(backpackReverse);

            Assert.Equal(
                left.For(yantForward.InstanceId).CooldownMultiplier,
                right.For(yantReverse.InstanceId).CooldownMultiplier,
                4);
            Assert.Equal(left.Active.Count, right.Active.Count);
            Assert.Equal(
                left.Active.Select(entry => entry.RuleId).ToList(),
                right.Active.Select(entry => entry.RuleId).ToList());
            Assert.NotEqual(PieceRef.None, takrutForward);
            Assert.NotEqual(PieceRef.None, takrutReverse);
        }

        [Fact]
        public void An_unmatched_item_only_gets_the_bag_area_bonus()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            PieceRef item = InventoryFixture.Place(editor, "item.tiger_fang", 1, 1);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1.1f, evaluation.For(item.InstanceId).DamageMultiplier, 4);
            Assert.Equal(1f, evaluation.For(item.InstanceId).CooldownMultiplier, 4);
            Assert.Single(evaluation.Active);
        }

        [Fact]
        public void An_empty_board_has_no_modifiers_and_no_records()
        {
            IGameConfig config = InventoryFixture.Config();
            InventoryFixture.NewEditor(config, out Backpack backpack);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Empty(evaluation.Active);
            Assert.Equal(ItemModifiers.None.DamageMultiplier, evaluation.For(1).DamageMultiplier, 4);
            Assert.Equal(ItemModifiers.None.CooldownMultiplier, evaluation.For(1).CooldownMultiplier, 4);
            Assert.Equal(0, evaluation.For(1).ActiveSynergyCount);
        }

        [Fact]
        public void Evaluation_requires_a_backpack()
        {
            IGameConfig config = InventoryFixture.Config();
            var evaluator = new SynergyEvaluator(config);

            Assert.Throws<System.ArgumentNullException>(() => evaluator.Evaluate(null!));
            Assert.Throws<System.ArgumentNullException>(() => new SynergyEvaluator(null!));
        }

        [Fact]
        public void The_same_line_rule_fires_down_a_column_when_the_data_asks_for_a_column()
        {
            // The GDD's condition is "อยู่แถวเดียวกัน" — the same line — and `synergy.kind`
            // chooses which half of that a rule means (ADR 0015). The shipped tome rule is
            // authored as SameRow; here the one authored row is re-pointed at SameColumn so
            // the column relation is under test without inventing balance for a new rule.
            IGameConfig config = InventoryFixture.Config();
            SynergyConfig tomeRule = config.Synergies.Single(s => s.Id == "synergy.tome_row_damage");

            BackpackEditor editor = WithBag(config, out Backpack backpack);
            InventoryFixture.Place(editor, "item.spell_tome", 0, 0); // 2x2: rows 0-1, columns 0-1
            PieceRef weapon = InventoryFixture.Place(editor, "item.mor_knife", 0, 2);

            // Same column 0, different row: the column rule must fire.
            tomeRule.Kind = SynergyKind.SameColumn;
            SynergyEvaluation asColumn = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1.3f, asColumn.For(weapon.InstanceId).DamageMultiplier, 4);

            // Same layout, row rule: no shared row, so only the bag-area bonus remains.
            tomeRule.Kind = SynergyKind.SameRow;
            SynergyEvaluation asRow = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1.1f, asRow.For(weapon.InstanceId).DamageMultiplier, 4);
        }

        [Fact]
        public void A_piece_shares_a_column_or_a_row_with_the_cells_it_actually_covers()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            PieceRef tome = InventoryFixture.Place(editor, "item.spell_tome", 0, 0); // rows 0-1, columns 0-1
            PieceRef below = InventoryFixture.Place(editor, "item.mor_knife", 0, 2);
            PieceRef beside = InventoryFixture.Place(editor, "item.mor_knife", 2, 2);
            PieceRef inTheChest = InventoryFixture.Add(editor, "item.tiger_fang");

            Assert.True(InventoryFixture.ItemOf(backpack, tome).SharesColumnWith(InventoryFixture.ItemOf(backpack, below)));
            Assert.False(InventoryFixture.ItemOf(backpack, tome).SharesRowWith(InventoryFixture.ItemOf(backpack, below)));

            Assert.False(InventoryFixture.ItemOf(backpack, tome).SharesColumnWith(InventoryFixture.ItemOf(backpack, beside)));
            Assert.False(InventoryFixture.ItemOf(backpack, tome).SharesRowWith(InventoryFixture.ItemOf(backpack, beside)));

            // A piece in the chest is not on the board, so it shares no line with anything.
            Assert.False(InventoryFixture.ItemOf(backpack, tome).SharesColumnWith(InventoryFixture.ItemOf(backpack, inTheChest)));
        }

        [Fact]
        public void Every_shipped_synergy_kind_is_one_the_engine_implements()
        {
            IGameConfig config = InventoryFixture.Config();

            Assert.All(
                config.Synergies,
                rule => Assert.True(
                    rule.Kind == SynergyKind.AdjacentPair
                        || rule.Kind == SynergyKind.SameRow
                        || rule.Kind == SynergyKind.SameColumn
                        || rule.Kind == SynergyKind.BagArea,
                    rule.Id + " has an unimplemented kind"));
        }
    }
}
