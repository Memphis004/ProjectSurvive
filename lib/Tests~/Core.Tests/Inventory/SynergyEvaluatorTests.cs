using System.Collections.Generic;
using System.Linq;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 task 7: the five GDD combo rules over the Stage 04 tag vocabulary —
    /// 4-direction adjacency counted once per pair, adjacency stacking with a cap (the tome
    /// rule since ADR 0031), the power-decay rule on both pieces, the bag-area bonus, and a
    /// single pass (no loop).
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

        /// <summary>
        /// The tome rule is <c>AdjacentPair</c> since ADR 0031: a spell tome empowers an arcane weapon
        /// it <b>touches</b>, from whichever of the four edges — left, right, above, below, and never
        /// diagonally (<c>docs/adr/0031-a-tome-touches-its-weapon.md</c>).
        /// </summary>
        [Theory]
        [InlineData(1, 0, 3, 0)] // right
        [InlineData(1, 0, 0, 0)] // left
        [InlineData(1, 1, 1, 0)] // above
        [InlineData(1, 0, 1, 2)] // below
        public void A_tome_touching_a_weapon_on_any_of_the_four_edges_adds_damage(
            int tomeX, int tomeY, int weaponX, int weaponY)
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);

            PieceRef tome = InventoryFixture.Place(editor, "item.spell_tome", tomeX, tomeY);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", weaponX, weaponY);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // +20 % for the touch, +10 % from the bag area.
            Assert.Equal(1.3f, evaluation.For(knife.InstanceId).DamageMultiplier, 4);
            Assert.Contains(
                evaluation.Active,
                entry => entry.RuleId == "synergy.tome_row_damage"
                    && entry.SourceInstanceId == tome.InstanceId
                    && entry.TargetInstanceId == knife.InstanceId);
            // The rule and the bag, one application each.
            Assert.Equal(2, evaluation.For(knife.InstanceId).ActiveSynergyCount);
        }

        /// <summary>
        /// The same row is no longer the condition: a cell of empty board between the two pieces ends
        /// the pair, however many edges of the stamped 2x2 grid they share (ADR 0031).
        /// </summary>
        [Fact]
        public void A_tome_one_cell_away_on_the_same_row_does_not_reach_the_weapon()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);

            InventoryFixture.Place(editor, "item.spell_tome", 1, 0); // columns 1-2
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 4, 0); // column 3 is empty

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // Only the bag-area bonus is left.
            Assert.Equal(1.1f, evaluation.For(knife.InstanceId).DamageMultiplier, 4);
            Assert.DoesNotContain(evaluation.Active, entry => entry.RuleId == "synergy.tome_row_damage");
        }

        [Fact]
        public void A_tome_touching_only_at_a_corner_does_not_reach_the_weapon()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = WithBag(config, out Backpack backpack);
            InventoryFixture.Place(editor, "item.spell_tome", 0, 0); // rows 0-1, columns 0-1
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 2, 2);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // Only the bag-area bonus is left.
            Assert.Equal(1.1f, evaluation.For(knife.InstanceId).DamageMultiplier, 4);
            Assert.DoesNotContain(evaluation.Active, entry => entry.RuleId == "synergy.tome_row_damage");
        }

        /// <summary>
        /// A 2x2 tome has two columns and two rows of edges, so it can touch two weapons that do not
        /// share a row with each other — and each of them is its own working pair.
        /// </summary>
        [Fact]
        public void A_tome_touching_two_weapons_on_different_rows_empowers_both()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);

            PieceRef tome = InventoryFixture.Place(editor, "item.spell_tome", 1, 0); // columns 1-2, rows 0-1
            PieceRef upper = InventoryFixture.Place(editor, "item.mor_knife", 3, 0);
            PieceRef lower = InventoryFixture.Place(editor, "item.kali_flame_yant", 3, 1);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            Assert.Equal(1.3f, evaluation.For(upper.InstanceId).DamageMultiplier, 4);
            Assert.Equal(1.3f, evaluation.For(lower.InstanceId).DamageMultiplier, 4);
            Assert.Equal(
                2,
                evaluation.Active.Count(entry =>
                    entry.RuleId == "synergy.tome_row_damage"
                    && entry.SourceInstanceId == tome.InstanceId));
        }

        /// <summary>
        /// <c>max_stacks = 2</c> means one weapon takes the bonus from at most two tomes, whichever
        /// three of them it touches (ADR 0031; the cap was already a per-target count under
        /// <c>SameRow</c>, so this stage did not change what it means).
        /// </summary>
        [Fact]
        public void A_weapon_touching_three_tomes_takes_at_most_two_stacks()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 3);

            // Three tomes, each touching the weapon on a different edge.
            InventoryFixture.Place(editor, "item.spell_tome", 0, 1); // left
            InventoryFixture.Place(editor, "item.spell_tome", 3, 1); // right
            InventoryFixture.Place(editor, "item.spell_tome", 1, 3); // below
            PieceRef weapon = InventoryFixture.Place(editor, "item.mor_knife", 2, 2);

            SynergyEvaluation evaluation = new SynergyEvaluator(config).Evaluate(backpack);

            // +20 +20 from two of the three tomes, +10 from the bag area.
            Assert.Equal(1.5f, evaluation.For(weapon.InstanceId).DamageMultiplier, 4);
            Assert.Equal(
                2,
                evaluation.Active.Count(entry =>
                    entry.RuleId == "synergy.tome_row_damage" && entry.TargetInstanceId == weapon.InstanceId));
            Assert.Equal(3, evaluation.For(weapon.InstanceId).ActiveSynergyCount);
        }

        /// <summary>
        /// One command, one recompute: moving the weapon a cell further along the row ends the touch, so
        /// the very next evaluation loses the pair and the revision moved exactly once.
        /// </summary>
        [Fact]
        public void Moving_the_weapon_one_cell_away_recomputes_once_and_drops_the_bonus()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);

            InventoryFixture.Place(editor, "item.spell_tome", 1, 0); // columns 1-2
            PieceRef weapon = InventoryFixture.Place(editor, "item.mor_knife", 3, 0);
            var evaluator = new SynergyEvaluator(config);

            Assert.Equal(1.3f, evaluator.Evaluate(backpack).For(weapon.InstanceId).DamageMultiplier, 4);

            int revision = backpack.Revision;
            Assert.True(editor.Move(weapon, new CellOffset(4, 0), Rotation.Degrees0).IsSuccess);

            Assert.Equal(revision + 1, backpack.Revision);
            SynergyEvaluation moved = evaluator.Evaluate(backpack);
            Assert.Equal(1.1f, moved.For(weapon.InstanceId).DamageMultiplier, 4);
            Assert.DoesNotContain(moved.Active, entry => entry.RuleId == "synergy.tome_row_damage");
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
            // The same-line kinds still exist and still mean what ADR 0015 said (ADR 0031 keeps
            // both, it only stops shipping one). No shipped row uses either, so the tome rule is
            // re-pointed here: the column relation is under test without inventing a balance row.
            IGameConfig config = InventoryFixture.Config();
            SynergyConfig tomeRule = config.Synergies.Single(s => s.Id == "synergy.tome_row_damage");

            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 3);
            InventoryFixture.Place(editor, "item.spell_tome", 0, 0); // 2x2: rows 0-1, columns 0-1
            PieceRef weapon = InventoryFixture.Place(editor, "item.mor_knife", 1, 4); // column 1, rows away

            // Same column 1, no shared row, not touching: the column rule fires and the others do not.
            tomeRule.Kind = SynergyKind.SameColumn;
            SynergyEvaluation asColumn = new SynergyEvaluator(config).Evaluate(backpack);
            Assert.Equal(1.3f, asColumn.For(weapon.InstanceId).DamageMultiplier, 4);

            // Same layout as a row rule: no shared row, so only the bag-area bonus remains.
            tomeRule.Kind = SynergyKind.SameRow;
            SynergyEvaluation asRow = new SynergyEvaluator(config).Evaluate(backpack);
            Assert.Equal(1.1f, asRow.For(weapon.InstanceId).DamageMultiplier, 4);

            // And as the kind the shipping data uses: still nothing, because they do not touch.
            tomeRule.Kind = SynergyKind.AdjacentPair;
            SynergyEvaluation asTouch = new SynergyEvaluator(config).Evaluate(backpack);
            Assert.Equal(1.1f, asTouch.For(weapon.InstanceId).DamageMultiplier, 4);
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

        /// <summary>
        /// Stage 09f / ADR 0020: a bag brings the items on it along, so the combos are read from where
        /// they landed. The tome rule is a touch rule (ADR 0031), so a quarter turn that keeps the two
        /// pieces against each other keeps the pair; four turns return the bag and both riders, so the
        /// pair is still there. What this test pins is that the picture follows the <b>landing cells</b>.
        /// </summary>
        /// <remarks>
        /// The landing cells are asserted exactly rather than "somewhere else": the point of the stage
        /// is that the bag and its riders turn as <b>one rigid object</b> about the bag's own origin, and
        /// a test that only checked "the item moved" would pass for a rule that scattered them.
        /// </remarks>
        [Fact]
        public void A_bag_turn_carries_the_items_so_the_touching_combo_turns_with_them()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef tome = InventoryFixture.Place(editor, "item.spell_tome", 0, 0);
            PieceRef weapon = InventoryFixture.Place(editor, "item.kali_flame_yant", 2, 0);

            CellOffset tomeStart = InventoryFixture.ItemOf(backpack, tome).Origin;
            CellOffset weaponStart = InventoryFixture.ItemOf(backpack, weapon).Origin;
            var evaluator = new SynergyEvaluator(config);

            // The tome's right column is the weapon's left edge, so the rule fires.
            Assert.Contains(
                evaluator.Evaluate(backpack).Active,
                entry => entry.RuleId == "synergy.tome_row_damage"
                    && entry.SourceInstanceId == tome.InstanceId
                    && entry.TargetInstanceId == weapon.InstanceId);

            int revision = backpack.Revision;
            Assert.True(editor.Rotate(bag).IsSuccess);

            // One gesture is one change: the revision the evaluation is cached on moved exactly once.
            Assert.Equal(revision + 1, backpack.Revision);

            ItemInstance turnedTome = InventoryFixture.ItemOf(backpack, tome);
            ItemInstance turnedWeapon = InventoryFixture.ItemOf(backpack, weapon);

            // Rigid: the same turn and the same re-normalisation the bag's own shape got.
            Assert.Equal(new CellOffset(1, 0), turnedTome.Origin);
            Assert.Equal(Rotation.Degrees90, turnedTome.Rotation);
            Assert.Equal(new CellOffset(2, 2), turnedWeapon.Origin);
            Assert.Equal(Rotation.Degrees90, turnedWeapon.Rotation);
            Assert.False(
                turnedTome.SharesRowWith(turnedWeapon),
                "the riders did not turn with the bag: " + turnedTome.Origin + " still shares a row with " + turnedWeapon.Origin);

            // The turn took them off each other's row, and the rotated tome now stands above the
            // weapon: a touch rule reads the landing cells, so the pair survives.
            Assert.True(
                turnedTome.IsAdjacentTo(turnedWeapon),
                "the rigid turn broke the contact the rule is read from: " + turnedTome.Origin + " / " + turnedWeapon.Origin);

            SynergyEvaluation turned = evaluator.Evaluate(backpack);

            Assert.Contains(
                turned.Active,
                entry => entry.RuleId == "synergy.tome_row_damage"
                    && entry.SourceInstanceId == tome.InstanceId
                    && entry.TargetInstanceId == weapon.InstanceId);
            Assert.Contains(
                turned.Active,
                entry => entry.RuleId == "synergy.bag_area_bonus" && entry.TargetInstanceId == weapon.InstanceId);

            // Four quarter turns return the bag and both riders, so the combo is back too.
            for (int turn = 1; turn < 4; turn++)
            {
                Assert.True(editor.Rotate(bag).IsSuccess);
            }

            Assert.Equal(tomeStart, InventoryFixture.ItemOf(backpack, tome).Origin);
            Assert.Equal(weaponStart, InventoryFixture.ItemOf(backpack, weapon).Origin);
            Assert.Contains(
                evaluator.Evaluate(backpack).Active,
                entry => entry.RuleId == "synergy.tome_row_damage"
                    && entry.TargetInstanceId == weapon.InstanceId);
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
