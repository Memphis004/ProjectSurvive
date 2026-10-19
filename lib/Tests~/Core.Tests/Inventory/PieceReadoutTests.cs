using System.Collections.Generic;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Tests.Run;
using Xunit;
using Xunit.Abstractions;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 09h: the tooltip reads a piece's <b>resolved</b> numbers from the same producer the
    /// battle's loadout comes from, so 3/3 no longer reads like 1/1 and a chosen variant shows.
    /// </summary>
    /// <remarks>
    /// Every expected number is derived here from the shipped tables (the skill row and the chosen
    /// variant rows) rather than written in, so a retune cannot make a correct readout look wrong.
    /// </remarks>
    public sealed class PieceReadoutTests
    {
        private const string Yant = "item.kali_flame_yant";
        private const string Charm = "item.phra_amulet";

        private readonly ITestOutputHelper _output;

        public PieceReadoutTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Part A of the stage, as a test: print the base row the old tooltip read, the resolved
        /// numbers, and the cooldown floor for one piece at 1/3, 2/3 and 3/3.
        /// </summary>
        /// <remarks>
        /// The floor is not what the 0.9 s came from — 0.9 × 0.65 = 0.585 is above
        /// <c>min_cooldown_seconds</c> 0.2 — which is why the stage's hypothesis had to be checked
        /// before the fix: the tooltip was reading the base row, not a clamped number.
        /// </remarks>
        [Fact]
        public void The_readout_prints_the_base_row_the_tooltip_used_and_the_numbers_the_battle_uses()
        {
            IGameConfig config = ConfigFixture.Load();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef weapon = InventoryFixture.Place(editor, Yant, 1, 1);

            LoadoutResolver resolver = Resolver(config);

            Assert.True(config.TryGetItem(Yant, out ItemConfig item));
            Assert.True(config.TryGetSkill(item.SkillId, out SkillConfig skill));

            Print(resolver, backpack, weapon, skill, "1/3");

            Merge(editor, weapon, "variant.arcane.swift_chant");
            Print(resolver, backpack, weapon, skill, "2/3 after swift chant (-15 % cooldown)");

            PieceRef partner = InventoryFixture.Add(editor, Yant);
            Merge(editor, partner, "variant.arcane.swift_chant");
            Merge(editor, weapon, "variant.arcane.great_swift_chant", partner);
            Print(resolver, backpack, weapon, skill, "3/3 after great swift chant (-20 % cooldown)");

            Assert.False(
                resolver.Resolve(backpack).Weapons[0].CooldownSeconds <= config.Constant.MinCooldownSeconds,
                "the prototype cannot reach the floor, so the stale 0.9 s was the base row and not a clamp");
        }

        /// <summary>
        /// The tooltip's number is the snapshot's number, field for field, at 3/3 — the stage's test
        /// ("ค่าที่ tooltip = ค่าใน snapshot").
        /// </summary>
        [Fact]
        public void The_resolved_numbers_are_the_snapshot_the_next_floor_receives()
        {
            IGameConfig config = ConfigFixture.Load();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);

            AddTier(run, weapon, "variant.arcane.swift_chant", "variant.arcane.great_swift_chant");

            var readout = run.ExplainPiece(weapon);
            Assert.True(readout.IsSuccess, readout.Error.ToString());

            var departed = run.StartFloor();
            Assert.True(departed.IsSuccess, departed.Error.ToString());

            WeaponLoadout snapshot = Assert.Single(departed.Value.Loadout.Weapons);

            Assert.Equal(snapshot.InstanceId, weapon.InstanceId);
            Assert.Equal(snapshot.Tier, readout.Value.Tier);
            Assert.Equal(3, readout.Value.Tier);
            Assert.Equal(snapshot.Damage, readout.Value.Resolved.Damage);
            Assert.Equal(snapshot.CooldownSeconds, readout.Value.Resolved.CooldownSeconds, 4);
            Assert.Equal(snapshot.MpCost, readout.Value.Resolved.MpCost);
            Assert.True(readout.Value.InLoadout, "a board piece is part of the loadout");
        }

        /// <summary>
        /// Each merge step lands in the readout: the variant that was kept, and numbers that moved by
        /// that variant's own modifiers ("ต่างจากก่อนอัปเกรดตามตัวปรับของรุ่น X").
        /// </summary>
        [Fact]
        public void A_confirmed_variant_moves_the_resolved_number_by_its_own_modifier()
        {
            IGameConfig config = ConfigFixture.Load();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef weapon = InventoryFixture.Place(editor, Yant, 1, 1);

            LoadoutResolver resolver = Resolver(config);

            PieceReadout before = Explain(resolver, backpack, weapon);
            Assert.Equal(1, before.Tier);
            Assert.Empty(before.Steps);

            Assert.True(config.TryGetSkill(before.SkillId, out SkillConfig skill));

            const string chosen = "variant.arcane.swift_chant";
            Merge(editor, weapon, chosen);

            PieceReadout after = Explain(resolver, backpack, weapon);

            Assert.Equal(2, after.Tier);
            Assert.Single(after.Steps);
            Assert.Equal(chosen, after.Steps[0].VariantId);
            Assert.Equal(1, after.Steps[0].FromTier);
            Assert.Equal(2, after.Steps[0].ToTier);

            // The step names the variant and carries its rows, so a card line and a tooltip line are
            // the same data.
            Assert.True(config.TryGetUpgradeVariant(chosen, out UpgradeVariantConfig variant));
            Assert.Equal(variant.NameKey, after.Steps[0].NameKey);
            Assert.Equal(variant.Modifiers.Count, after.Steps[0].Modifiers.Count);

            // The cooldown moved by exactly that variant's percent, from the table's own base.
            float percent = 0f;
            Assert.True(config.TryGetUpgradeVariant(chosen, out UpgradeVariantConfig row));
            for (int i = 0; i < row.Modifiers.Count; i++)
            {
                if (row.Modifiers[i].Stat == UpgradeStat.Cooldown
                    && row.Modifiers[i].Method == UpgradeMethod.Percent)
                {
                    percent += row.Modifiers[i].Magnitude;
                }
            }

            Assert.True(percent < 0f, "the chosen variant is the fast-chant one: " + chosen);
            Assert.Equal(skill.CooldownSeconds * (1f + (percent / 100f)), after.Resolved.CooldownSeconds, 4);
            Assert.NotEqual(before.Resolved.CooldownSeconds, after.Resolved.CooldownSeconds);

            // The base row never moves — that is what the old tooltip printed at every tier.
            Assert.Equal(skill.CooldownSeconds, after.Base.CooldownSeconds, 4);
            Assert.Equal(skill.Damage, after.Base.Damage);
        }

        /// <summary>
        /// A piece with no upgrade variants (a charm) folds immediately; its tooltip shows the new
        /// tier and no variant line, rather than inventing one.
        /// </summary>
        [Fact]
        public void A_piece_without_variants_shows_the_new_tier_and_no_step_line()
        {
            IGameConfig config = ConfigFixture.Load();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef charm = InventoryFixture.Place(editor, Charm, 0, 0);
            PieceRef twin = InventoryFixture.Add(editor, Charm);

            var merged = InventoryFixture.Merge(editor, twin, charm);
            Assert.True(merged.IsSuccess, merged.Error.ToString());
            Assert.True(merged.Value.VariantId.Length == 0, "the fold kept no variant");

            PieceReadout readout = Explain(Resolver(config), backpack, charm);

            Assert.Equal(2, readout.Tier);
            Assert.Single(readout.Steps);
            Assert.False(readout.Steps[0].HasVariant, "the step happened, and no variant names it");
            Assert.Empty(readout.Steps[0].Modifiers);
        }

        /// <summary>
        /// Undo puts the tooltip back where it was: the readout is a function of the run, not a
        /// snapshot a screen kept.
        /// </summary>
        [Fact]
        public void Undo_returns_the_readout_to_the_numbers_before_the_merge()
        {
            IGameConfig config = ConfigFixture.Load();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);

            PieceReadout before = Explain(run, weapon);

            AddTier(run, weapon, "variant.arcane.swift_chant", "variant.arcane.great_swift_chant");

            PieceReadout merged = Explain(run, weapon);
            Assert.Equal(3, merged.Tier);
            Assert.NotEqual(before.Resolved.CooldownSeconds, merged.Resolved.CooldownSeconds);

            // One undo per merge step: three merges, so three undos to reach the first camp's state.
            Assert.True(run.Undo().IsSuccess);
            Assert.True(run.Undo().IsSuccess);
            Assert.True(run.Undo().IsSuccess);

            PieceReadout undone = Explain(run, weapon);

            Assert.Equal(before.Tier, undone.Tier);
            Assert.Equal(before.Resolved.CooldownSeconds, undone.Resolved.CooldownSeconds, 4);
            Assert.Equal(before.Resolved.Damage, undone.Resolved.Damage);
            Assert.Empty(undone.Steps);
        }

        /// <summary>
        /// When the floor, not the modifiers, decided the cooldown, the readout says so — the other
        /// half of part A's question ("ถ้าค่าต่ำสุดเท่ากับหรือสูงกว่า...").
        /// </summary>
        [Fact]
        public void A_cooldown_held_up_by_the_floor_is_reported_as_at_its_minimum()
        {
            IGameConfig config = ConfigFixture.Load();
            config.Constant.MinCooldownSeconds = 1.0f;

            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef weapon = InventoryFixture.Place(editor, Yant, 1, 1);

            Merge(editor, weapon, "variant.arcane.swift_chant");

            PieceReadout readout = Explain(Resolver(config), backpack, weapon);

            Assert.True(readout.CooldownAtMinimum, "0.9 × 0.85 is 0.765, under the 1.0 floor");
            Assert.Equal(1.0f, readout.Resolved.CooldownSeconds, 4);
            Assert.Equal(1.0f, readout.MinCooldownSeconds, 4);
            Assert.Equal(0.9f, readout.Base.CooldownSeconds, 4);
        }

        /// <summary>
        /// A piece in the chest is explained on its own numbers, with no combo — the chest neither
        /// gives a bonus nor fights.
        /// </summary>
        [Fact]
        public void A_chest_piece_resolves_its_own_upgrades_and_no_combo()
        {
            IGameConfig config = ConfigFixture.Load();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef weapon = InventoryFixture.Place(editor, Yant, 1, 1);

            Assert.True(config.TryGetSkill("skill.fire_bolt", out SkillConfig skill));

            PieceReadout onBoard = Explain(Resolver(config), backpack, weapon);
            Assert.True(onBoard.InLoadout);
            Assert.True(onBoard.ActiveSynergyCount == 1, "the bag-area rule is acting on it");

            Assert.True(editor.TransferToChest(weapon).IsSuccess);

            PieceReadout inChest = Explain(Resolver(config), backpack, weapon);

            Assert.False(inChest.InLoadout, "a chest piece is not in the loadout the floor receives");
            Assert.True(inChest.ActiveSynergyCount == 0, "no rule reaches a chest piece");
            Assert.Equal(1f, inChest.DamageMultiplier, 4);
            Assert.Equal(skill.Damage, inChest.Resolved.Damage);
        }

        /// <summary>A handle the run does not hold is a typed refusal, not an empty panel.</summary>
        [Fact]
        public void An_unknown_piece_is_refused()
        {
            IGameConfig config = ConfigFixture.Load();
            RunSession run = RunFixture.StartedRun(config);

            var readout = run.ExplainPiece(PieceRef.None);

            Assert.True(readout.IsFailure);
            Assert.Equal(InventoryError.UnknownPiece, readout.Error.Code);
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static LoadoutResolver Resolver(IGameConfig config) =>
            new LoadoutResolver(config, new SynergyEvaluator(config));

        private static PieceReadout Explain(LoadoutResolver resolver, Backpack backpack, PieceRef piece)
        {
            Assert.True(resolver.TryExplain(backpack, piece, out PieceReadout readout));
            return readout;
        }

        private static PieceReadout Explain(RunSession run, PieceRef piece)
        {
            var readout = run.ExplainPiece(piece);
            Assert.True(readout.IsSuccess, readout.Error.ToString());
            return readout.Value;
        }

        /// <summary>
        /// Builds a tier-3 piece through the real two-phase merge: one copy folded in for each step,
        /// and a second tier-2 partner for the last one (a merge needs equal tiers).
        /// </summary>
        private static void AddTier(RunSession run, PieceRef target, string first, string second)
        {
            Assert.True(run.Backpack.TryGetPiece(target, out PieceInstance? piece));
            var item = (ItemInstance)piece!;
            string id = item.ItemId;

            MergeRun(run, target, first, id);

            var partner = run.Backpack.AddItem(id);
            Assert.True(partner.IsSuccess, partner.Error.ToString());
            MergeRun(run, partner.Value, first, id);
            MergeRun(run, target, second, id, partner.Value);
        }

        private static void MergeRun(
            RunSession run, PieceRef target, string preferred, string itemId, PieceRef? source = null)
        {
            PieceRef twin = source ?? AddCopy(run, itemId);

            var offered = run.OfferMerge(twin, target);
            Assert.True(offered.IsSuccess, offered.Error.ToString());
            Assert.True(offered.Value.HasChoices, "the pair should raise an upgrade question");

            string chosen = offered.Value.Options[0].Id;
            for (int i = 0; i < offered.Value.Options.Count; i++)
            {
                if (offered.Value.Options[i].Id == preferred)
                {
                    chosen = preferred;
                }
            }

            var merged = run.ConfirmMerge(twin, target, chosen);
            Assert.True(merged.IsSuccess, merged.Error.ToString());
        }

        private static PieceRef AddCopy(RunSession run, string itemId)
        {
            var added = run.Backpack.AddItem(itemId);
            Assert.True(added.IsSuccess, added.Error.ToString());
            return added.Value;
        }

        /// <summary>Folds a copy into <paramref name="target"/>, preferring one variant id.</summary>
        private static void Merge(
            BackpackEditor editor, PieceRef target, string preferred, PieceRef? source = null)
        {
            Assert.True(editor.Backpack.TryGetPiece(target, out PieceInstance? piece));
            var item = (ItemInstance)piece!;

            PieceRef twin = source ?? InventoryFixture.Add(editor, item.ItemId);
            Assert.True(editor.Backpack.TryGetPiece(twin, out PieceInstance? twinPiece));
            Assert.Equal(item.Tier, ((ItemInstance)twinPiece!).Tier);

            var offered = editor.OfferMerge(twin, target, InventoryFixture.MergeSeed);
            Assert.True(offered.IsSuccess, offered.Error.ToString());

            string chosen = offered.Value.Options[0].Id;
            for (int i = 0; i < offered.Value.Options.Count; i++)
            {
                if (offered.Value.Options[i].Id == preferred)
                {
                    chosen = preferred;
                }
            }

            var merged = editor.ConfirmMerge(twin, target, chosen, InventoryFixture.MergeSeed);
            Assert.True(merged.IsSuccess, merged.Error.ToString());
        }

        /// <summary>Prints one row of the stage's diagnosis table.</summary>
        private void Print(
            LoadoutResolver resolver, Backpack backpack, PieceRef weapon, SkillConfig skill, string label)
        {
            PieceReadout readout = Explain(resolver, backpack, weapon);

            _output.WriteLine("=== " + label + " ===");
            _output.WriteLine(
                "tooltip used to read:  damage " + skill.Damage + " | cooldown " + skill.CooldownSeconds
                + " | focus " + skill.MpCost);
            _output.WriteLine(
                "loadout resolved:      damage " + readout.Resolved.Damage + " | cooldown "
                + readout.Resolved.CooldownSeconds + " | focus " + readout.Resolved.MpCost
                + " | tier " + readout.Tier);
            _output.WriteLine("min_cooldown_seconds = " + readout.MinCooldownSeconds
                + " | at minimum: " + readout.CooldownAtMinimum);
            _output.WriteLine("steps = " + readout.Steps.Count + " | combos = " + readout.ComboRuleIds.Count);
        }
    }
}
