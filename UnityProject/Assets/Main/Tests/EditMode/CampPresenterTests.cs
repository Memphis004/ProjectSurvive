using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Audio;
using ProjectSurvive.Game.Presentation.Camp;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 09 acceptance for ศาลาพัก and the จัดย่าม screen, driven through the surface the UI
    /// actually calls — <see cref="CampPresenter"/> — over the <b>shipped</b> tables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claim under test is the stage's first acceptance line: <i>buy → place → rotate → merge →
    /// give back → restore, through the UI, and the state matches Core</i>. It is checked the only
    /// way that can be checked honestly — a second run is built from the same seed and driven by the
    /// same commands issued <b>directly</b>, and the two run-state hashes must agree. A screen that
    /// had re-implemented one rule anywhere would land on a different number.
    /// </para>
    /// <para>
    /// The rest are the promises that only fail through this seam: a refused drop loses nothing, a
    /// drag changes nothing, Escape puts the picture back, and a long unbroken pointer sweep
    /// allocates <b>zero</b> bytes. That last one is measured, not inspected.
    /// </para>
    /// <para>
    /// Balance numbers are read from the loaded config and the run itself, never hard-coded, so
    /// retuning <c>data/</c> cannot make a correct rule look broken. The one pinned number is the
    /// seed, and the assertion says why it is pinned.
    /// </para>
    /// </remarks>
    public sealed class CampPresenterTests
    {
        /// <summary>
        /// The scene's authored run seed. Pinned because the shop roll is derived from it: this seed
        /// is one where floor 1 offers the starting weapon, so a merge is reachable at all.
        /// </summary>
        private const uint Seed = 5u;

        /// <summary>
        /// A charm: the piece no upgrade variant matches, so a pair of them folds immediately. Used to
        /// prove the chooser only opens for a pair that has a choice to make (Stage 09g item 1).
        /// </summary>
        private const string CharmId = "item.tiger_fang";

        private IGameConfig _config;
        private IBackpackSnapshotCodec _codec;
        private LocalizationService _text;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();
            _codec = new MessagePackBackpackSnapshotCodec();
            _text = new LocalizationService(_config, Language.Th);
        }

        // -------------------------------------------------------------------
        // the acceptance line
        // -------------------------------------------------------------------

        [Test]
        public void Buying_placing_rotating_merging_giving_back_and_restoring_reach_the_same_state_as_the_same_commands_issued_directly()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;
            RunSession core = AtFirstCamp();

            // Buy. The slot offering the piece the run already carries is the only rent that makes a
            // merge possible later, so it is required rather than hoped for.
            int slot = SlotOffering(run, RunDefaults.InitialWeaponId);
            Assert.That(
                slot,
                Is.GreaterThanOrEqualTo(0),
                "seed " + Seed + " must offer " + RunDefaults.InitialWeaponId + " for this flow to merge; re-pin the seed");

            int biaBeforeRent = run.Wallet.Bia;
            int rentPrice = run.Stock[slot].Price;

            ui.Presenter.Rent(slot);

            Assert.That(ui.Presenter.Counters.Rented, Is.EqualTo(1), "the rent went through the screen");
            Assert.That(run.Wallet.Bia, Is.EqualTo(biaBeforeRent - rentPrice), "the price paid is the one the slot showed");
            Assert.That(
                core.Purchase(slot).IsSuccess,
                Is.True,
                "the same rent issued directly to the run is accepted");

            // A rented piece lands in the chest, so the gesture starts from the chest.
            PieceRef twin = OnlyChestItem(run);
            Assert.That(twin.IsValid, Is.True, "a rented piece is carried in the chest");

            // Place: the first board cell the screen's own preview accepts.
            CellOffset cell = FirstPlaceableCell(ui.Presenter, twin, out BagDropAction action);
            Assert.That(action, Is.EqualTo(BagDropAction.Place), "an empty covered cell is a placement");
            Assert.That(cell.X, Is.GreaterThanOrEqualTo(0), "the bag must have a free slot to place into");
            Assert.That(ui.Presenter.Drag.IsValid, Is.True, "the preview the drop re-asks is legal");

            ui.Presenter.Drop();

            Assert.That(ui.Presenter.Counters.Placed, Is.EqualTo(1));
            Assert.That(OriginOf(run, twin), Is.EqualTo(cell), "the piece is where the preview said it would go");
            Assert.That(
                core.MovePiece(twin, cell, Rotation.Degrees0).IsSuccess,
                Is.True,
                "the same move issued directly is accepted");

            // Rotate: the backup button turns the selected piece (R does the same through this call).
            ui.Presenter.SelectOnly(twin);
            ui.Presenter.RotateSelected();

            Assert.That(ui.Presenter.Counters.Rotated, Is.EqualTo(1), "the rotation was accepted");
            Assert.That(RotationOf(run, twin), Is.EqualTo(Rotation.Degrees90));
            Assert.That(core.RotatePiece(twin).IsSuccess, Is.True, "the same rotation issued directly is accepted");

            // Merge: dropping the twin onto the piece it matches. The screen decides from the
            // preview, and the preview is the run's own predicate. Stage 09g: two arcane weapons have
            // upgrade choices, so the drop opens the chooser and the pick is what folds the pair —
            // the same pick, issued directly to the second run, is what keeps the two states equal.
            PieceRef survivor = run.Backpack.Items[0].Ref;

            ui.Presenter.DragStart(twin, RotationOf(run, twin));
            ui.Presenter.DragMove(BagDropZone.Board, OriginOf(run, survivor));

            Assert.That(ui.Presenter.Drag.Action, Is.EqualTo(BagDropAction.Merge), "dropping onto a matching piece merges");
            Assert.That(ui.Presenter.Drag.Target, Is.EqualTo(survivor), "the screen highlights the piece it would fold into");

            ui.Presenter.Drop();

            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.True, "an arcane pair asks which upgrade to keep first");
            string chosen = ui.Presenter.MergeChooser.Choices[0].VariantId;
            Assert.That(ui.Presenter.ConfirmMergeChoice(0), Is.True, "the card the player picks is the one confirmed");

            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(1));
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(1), "the merge consumed one of the two");
            Assert.That(run.Backpack.Items[0].Tier, Is.EqualTo(2), "the survivor went up a tier");
            Assert.That(
                core.ConfirmMerge(twin, survivor, chosen).IsSuccess,
                Is.True,
                "the same merge with the same pick issued directly is accepted");

            // Give back: the altar. The payout on screen is the run's own number, read before the drop.
            PieceRef merged = run.Backpack.Items[0].Ref;
            int biaBeforeAltar = run.Wallet.Bia;

            ui.Presenter.DragStart(merged, RotationOf(run, merged));
            ui.Presenter.DragMove(BagDropZone.Altar, default);

            Assert.That(ui.Presenter.Drag.Action, Is.EqualTo(BagDropAction.Recycle), "the pot gives a piece back");
            Assert.That(ui.Presenter.Drag.IsValid, Is.True);
            Assert.That(ui.State.AltarOffer.Value, Is.Not.Empty, "the screen shows what the altar would pay");

            int offered = ui.Presenter.Drag.Bia;

            ui.Presenter.Drop();

            Assert.That(ui.Presenter.Counters.Recycled, Is.EqualTo(1));
            Assert.That(run.Wallet.Bia, Is.EqualTo(biaBeforeAltar + offered), "the run paid what the preview offered");
            Assert.That(core.Sell(merged).IsSuccess, Is.True, "the same give-back issued directly is accepted");
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(0), "the altar took the piece");

            // Restore: back to how the bag looked when this camp was entered.
            ui.Presenter.ResetBag();
            Assert.That(ui.Presenter.Counters.Resets, Is.EqualTo(1));
            Assert.That(core.Reset().IsSuccess, Is.True, "the same restore issued directly is accepted");

            Assert.That(run.Backpack.Items.Count, Is.EqualTo(1), "the restore brought the weapon back");
            Assert.That(run.Backpack.Items[0].Tier, Is.EqualTo(1), "and at its original tier");

            Assert.That(
                run.ComputeStateHash(),
                Is.EqualTo(core.ComputeStateHash()),
                "the screen's run and the directly-driven run are the same state");
            Assert.That(run.Wallet.Bia, Is.EqualTo(core.Wallet.Bia), "including the purse, which every step moved");

            // And the screen is showing the restored picture, not the one it had a moment ago.
            Assert.That(
                ui.State.Bag.Value.Pieces.Length,
                Is.EqualTo(run.Backpack.Bags.Count + run.Backpack.Items.Count),
                "the bag screen paints every piece the run holds");
        }

        // -------------------------------------------------------------------
        // Stage 09g: the upgrade chooser
        // -------------------------------------------------------------------

        /// <summary>
        /// Stage 09g item 1: dropping a piece onto a matching piece that has upgrade choices opens the
        /// chooser and changes nothing. The merge belongs to the pick, not to the drop.
        /// </summary>
        [Test]
        public void Dropping_a_matching_pair_opens_the_chooser_and_changes_nothing()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            RunSession run = ui.Run;

            int bia = run.Wallet.Bia;
            int chest = run.ChestUsed;
            int pieces = run.Backpack.Items.Count;
            uint hash = run.ComputeStateHash();

            DropOnto(ui.Presenter, run, twin, survivor);

            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.True, "a pair with choices opens the modal");
            Assert.That(ui.State.MergeChooser.Value.IsOpen, Is.True, "and the state the view renders from says so");
            Assert.That(
                ui.Presenter.MergeChooser.ChoiceCount,
                Is.EqualTo(_config.Constant.UpgradeChoiceCount),
                "the cards are the configured number of choices (เลือก 1 จาก 3)");
            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(0), "nothing was folded yet");
            Assert.That(ui.Presenter.Counters.MergeChoicesOpened, Is.EqualTo(1), "the chooser was opened once");
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(pieces), "both pieces are still there");
            Assert.That(TierOf(run, survivor), Is.EqualTo(1), "and the survivor is still at its own tier");
            Assert.That(run.Wallet.Bia, Is.EqualTo(bia), "opening the chooser moves no bia");
            Assert.That(run.ChestUsed, Is.EqualTo(chest), "and takes nothing out of the chest");
            Assert.That(run.ComputeStateHash(), Is.EqualTo(hash), "and the run is byte for byte where it was");
        }

        /// <summary>
        /// Stage 09g item 2: the cards are the offer's own choices — the same ids in the same order —
        /// and every line is the variant's own data. In shipped data every modifier helps the player,
        /// which is what the green colour on the card is chosen from.
        /// </summary>
        [Test]
        public void The_cards_are_the_offer_and_every_line_is_the_variants_own_data()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            RunSession run = ui.Run;

            Result<MergeOffer> offer = run.OfferMerge(twin, survivor);
            Assert.That(offer.IsSuccess, Is.True, "the offer is a read model: " + offer.Error);
            Assert.That(offer.Value.HasChoices, Is.True, "an arcane weapon has choices at the 1 -> 2 step");

            ItemConfig item = ItemOf(run, survivor);

            DropOnto(ui.Presenter, run, twin, survivor);

            CampMergeChooserModel chooser = ui.Presenter.MergeChooser;
            Assert.That(chooser.ChoiceCount, Is.EqualTo(offer.Value.Options.Count), "one card per offered choice");
            Assert.That(chooser.Title, Is.Not.Empty, "the modal has a heading");
            Assert.That(chooser.Hint, Is.Not.Empty, "and the one-line explanation under the cards");
            Assert.That(chooser.BackLabel, Is.Not.Empty, "and a back button caption");

            for (int card = 0; card < chooser.ChoiceCount; card++)
            {
                UpgradeVariantConfig variant = VariantOf(chooser.Choices[card].VariantId);

                Assert.That(
                    chooser.Choices[card].VariantId,
                    Is.EqualTo(offer.Value.Options[card].Id),
                    "the cards are the offer, in the order it named them");
                Assert.That(
                    chooser.Choices[card].Title,
                    Is.EqualTo(Format("camp.merge.card.title.format", Text(variant.NameKey), Text(item.NameKey))),
                    "ชื่อรุ่น is the variant's name plus the item's");
                Assert.That(
                    chooser.Choices[card].LevelLine,
                    Is.EqualTo(Format("camp.merge.level.format", offer.Value.FromTier, offer.Value.ToTier)),
                    "ระดับ N -> N+1");
                Assert.That(
                    chooser.Choices[card].ChangeCount,
                    Is.EqualTo(variant.Modifiers.Count),
                    "one line per stat the variant changes");
                Assert.That(
                    chooser.Choices[card].Improvements.Length,
                    Is.EqualTo(chooser.Choices[card].ChangeCount),
                    "every line has a colour to draw");

                for (int line = 0; line < variant.Modifiers.Count; line++)
                {
                    UpgradeModifierConfig modifier = variant.Modifiers[line];

                    Assert.That(
                        chooser.Choices[card].Changes[line],
                        Is.EqualTo(Format("camp.merge.change.format", Text(StatKey(modifier.Stat)), Amount(modifier))),
                        "the line is the data row, formatted by the presenter");
                    Assert.That(
                        chooser.Choices[card].Improvements[line],
                        Is.True,
                        "every upgrade in shipped data helps the player, so its line is drawn green");
                }
            }
        }

        /// <summary>
        /// Stage 09g item 2: the left panel of the modal is the same content the tooltip shows — one
        /// producer feeds both, so the two can never disagree.
        /// </summary>
        [Test]
        public void The_left_panel_is_the_same_content_the_tooltip_shows()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            RunSession run = ui.Run;

            ui.Presenter.Hover(survivor);
            Assert.That(ui.State.TooltipVisible.Value, Is.True, "the tooltip is up, so there is content to compare");

            var expected = new[]
            {
                ui.State.TooltipTitle.Value,
                ui.State.TooltipTier.Value,
                ui.State.TooltipStatus.Value,
                ui.State.TooltipUpgrades.Value,
                ui.State.TooltipDescription.Value,
                ui.State.TooltipCombos.Value,
            };

            DropOnto(ui.Presenter, run, twin, survivor);

            Assert.That(
                ui.Presenter.MergeChooser.SourceLines,
                Is.EqualTo(expected),
                "แผงซ้ายแสดงค่าปัจจุบันของไอเทม, in the tooltip's own six lines");
            Assert.That(
                ui.State.TooltipVisible.Value,
                Is.False,
                "and the floating tooltip is taken off screen while the modal is up (item 4)");
        }

        /// <summary>
        /// Stage 09g item 5: confirming a card raises the tier in the same cell, recomputes the
        /// loadout, and leaves bia and the chest alone — and the card that was picked is the one the
        /// battle numbers came from.
        /// </summary>
        [Test]
        public void Confirming_a_card_raises_the_tier_and_the_pick_is_what_the_loadout_gets()
        {
            Screen first = AtACampWithAMergeablePair(out PieceRef firstSurvivor, out PieceRef firstTwin);
            Screen second = AtACampWithAMergeablePair(out PieceRef secondSurvivor, out PieceRef secondTwin);

            int bia = first.Run.Wallet.Bia;
            int chest = first.Run.ChestUsed;
            CellOffset origin = OriginOf(first.Run, firstSurvivor);

            DropOnto(first.Presenter, first.Run, firstTwin, firstSurvivor);

            string pick = first.Presenter.MergeChooser.Choices[0].VariantId;
            Assert.That(
                first.Presenter.MergeChooser.ChoiceCount,
                Is.GreaterThan(1),
                "the second card exists, so there is a different choice to compare against");

            Assert.That(first.Presenter.ConfirmMergeChoice(0), Is.True, "the first card is confirmed");

            Assert.That(first.Presenter.IsMergeChooserOpen, Is.False, "confirming closes the modal");
            Assert.That(first.Run.Backpack.Items.Count, Is.EqualTo(1), "the merge consumed one of the two");
            Assert.That(TierOf(first.Run, firstSurvivor), Is.EqualTo(2), "ไอเทมเป็นขั้นใหม่");
            Assert.That(OriginOf(first.Run, firstSurvivor), Is.EqualTo(origin), "in the same cell it stood in");
            Assert.That(first.Run.Wallet.Bia, Is.EqualTo(bia), "เบี้ยไม่เปลี่ยน");
            Assert.That(
                first.Run.ChestUsed,
                Is.EqualTo(chest - 1),
                "และหีบไม่เปลี่ยน beyond the copy the merge consumed: the twin was carried in the chest, "
                + "the merge ate it, and nothing new was stored (one fewer, not one more)");
            Assert.That(first.Presenter.Counters.Merged, Is.EqualTo(1), "one merge happened");
            Assert.That(first.Presenter.Counters.MergeChoicesConfirmed, Is.EqualTo(1), "from one confirmed card");
            Assert.That(first.State.Bag.Value.CanUndo, Is.True, "the merge is one undoable edit");
            Assert.That(
                first.State.Bag.Value.Pieces.Length,
                Is.EqualTo(first.Run.Backpack.Bags.Count + first.Run.Backpack.Items.Count),
                "and the bag screen was recomputed from the new state");

            IReadOnlyList<string> steps = VariantSteps(first.Run, firstSurvivor);
            Assert.That(steps.Count, Is.EqualTo(1), "one merge step is on record");
            Assert.That(steps[0], Is.EqualTo(pick), "and it kept the card that was picked");

            // The same gesture, the same seed, the other card: the two runs differ in nothing else, so
            // two loadouts that agreed would mean the pick never reached the battle.
            DropOnto(second.Presenter, second.Run, secondTwin, secondSurvivor);
            Assert.That(second.Presenter.ConfirmMergeChoice(1), Is.True, "the second run picks the other card");
            Assert.That(
                VariantSteps(second.Run, secondSurvivor)[0],
                Is.Not.EqualTo(pick),
                "with a different variant on record");

            CombatLoadoutSnapshot one = Loadout(first.Run);
            CombatLoadoutSnapshot two = Loadout(second.Run);

            Assert.That(one.Weapons.Count, Is.GreaterThan(0), "the upgraded piece resolves to a weapon");
            Assert.That(
                WeaponLine(one),
                Is.Not.EqualTo(WeaponLine(two)),
                "the card the player picked is the one the battle numbers came from");
        }

        /// <summary>
        /// Stage 09g item 3: closing without choosing changes nothing, and the determinism the offer is
        /// built on means reopening asks the same question (ADR 0021).
        /// </summary>
        [Test]
        public void Cancelling_changes_nothing_and_reopening_offers_the_same_cards()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            RunSession run = ui.Run;

            uint hash = run.ComputeStateHash();
            int revision = run.Backpack.Revision;

            DropOnto(ui.Presenter, run, twin, survivor);
            string[] first = CardIds(ui.Presenter);

            ui.Presenter.CancelMergeChooser();

            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.False, "the back button or Escape closes the modal");
            Assert.That(ui.State.MergeChooser.Value.IsOpen, Is.False, "and the view is told to stop drawing it");
            Assert.That(ui.Presenter.Counters.MergeChoicesCancelled, Is.EqualTo(1), "the cancel was counted");
            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(0), "nothing was merged");
            Assert.That(run.ComputeStateHash(), Is.EqualTo(hash), "and nothing changed");
            Assert.That(run.Backpack.Revision, Is.EqualTo(revision), "not even the revision");

            DropOnto(ui.Presenter, run, twin, survivor);

            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.True, "the gesture opens it again");
            Assert.That(
                CardIds(ui.Presenter),
                Is.EqualTo(first),
                "with the same cards in the same order: the offer is a function of the run's state");
        }

        /// <summary>
        /// Stage 09g item 1: the other half of the rule — a pair no variant matches still folds on the
        /// drop, with no modal in the way.
        /// </summary>
        [Test]
        public void A_pair_without_choices_still_folds_on_the_drop()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            Result<PieceRef> one = run.Backpack.AddItem(CharmId);
            Result<PieceRef> two = run.Backpack.AddItem(CharmId);
            Assert.That(one.IsSuccess, Is.True, "the charm must be addable: " + one.Error);
            Assert.That(two.IsSuccess, Is.True, "twice: " + two.Error);

            CellOffset cell = FirstPlaceableCell(ui.Presenter, one.Value, out BagDropAction action);
            Assert.That(action, Is.EqualTo(BagDropAction.Place), "the charm goes onto the board");
            ui.Presenter.Drop();
            Assert.That(OriginOf(run, one.Value), Is.EqualTo(cell), "where the preview said it would go");

            DropOnto(ui.Presenter, run, two.Value, one.Value);

            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.False, "a charm has no upgrade variants, so no modal opens");
            Assert.That(ui.Presenter.Counters.MergeChoicesOpened, Is.EqualTo(0), "the chooser was never opened");
            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(1), "the drop folded the pair, as it always did");
            Assert.That(TierOf(run, one.Value), Is.EqualTo(2), "and the survivor went up a tier");
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(2), "one charm was consumed");

            // Stage 09h: the step really happened, and it has no variant to name — so the tooltip
            // shows the new tier and no upgrade line, instead of inventing an effect for the fold.
            ui.Presenter.Hover(one.Value);

            Assert.That(
                ui.State.TooltipTier.Value,
                Is.EqualTo(Format("camp.tooltip.tier.format", 2, _config.Constant.MaxTier)),
                "the tier line is the new one");
            Assert.That(ui.State.TooltipUpgrades.Value, Is.Empty, "and there is no variant line to draw");
        }

        /// <summary>
        /// Stage 09g item 4: while the modal is up the board behind it is locked, the tooltip stays off
        /// screen, and a second pick cannot fold anything a second time.
        /// </summary>
        [Test]
        public void While_the_chooser_is_open_the_board_is_locked_and_a_second_pick_cannot_fold_twice()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            RunSession run = ui.Run;

            ui.Presenter.Hover(survivor);
            string[] panel = { ui.State.TooltipTitle.Value, ui.State.TooltipStatus.Value };

            DropOnto(ui.Presenter, run, twin, survivor);

            // ล็อกการลากในหน้าจัดย่าม: nothing behind the dim layer may be picked up.
            ui.Presenter.DragStart(survivor, RotationOf(run, survivor));
            Assert.That(ui.Presenter.IsDragging, Is.False, "the board behind the modal is not draggable");
            ui.Presenter.DragMove(BagDropZone.Board, new CellOffset(0, 0));
            Assert.That(ui.Presenter.Drag.IsActive, Is.False, "and a pointer move cannot revive the gesture");

            // tooltip ไม่โผล่ทับ: hovering behind the modal neither shows it nor rewrites the panel.
            ui.Presenter.Hover(twin);
            Assert.That(ui.State.TooltipVisible.Value, Is.False, "the floating tooltip stays off screen");
            Assert.That(
                ui.Presenter.MergeChooser.SourceLines[0],
                Is.EqualTo(panel[0]),
                "and the panel still shows the piece the merge is about");
            Assert.That(ui.Presenter.MergeChooser.SourceLines[2], Is.EqualTo(panel[1]), "including its สถานะ line");

            // กันคลิกซ้ำ: an index no card has is refused, and the modal stays up.
            Assert.That(ui.Presenter.ConfirmMergeChoice(-1), Is.False, "an index no card has is refused");
            Assert.That(
                ui.Presenter.ConfirmMergeChoice(ui.Presenter.MergeChooser.ChoiceCount),
                Is.False,
                "and so is one past the last card");
            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(0), "neither refusal changed anything");
            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.True, "the modal is still up");

            Assert.That(ui.Presenter.ConfirmMergeChoice(0), Is.True, "the real click goes through");
            Assert.That(ui.Presenter.ConfirmMergeChoice(0), Is.False, "a second one finds nothing to confirm");
            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(1), "so the pair folded exactly once");
            Assert.That(ui.Presenter.Counters.MergeChoicesConfirmed, Is.EqualTo(1), "and one card was confirmed");
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(1), "with one piece left");
        }

        /// <summary>Stage 09g item 3: a pick when nothing is on screen is refused and changes nothing.</summary>
        [Test]
        public void A_pick_with_no_chooser_open_is_refused_and_changes_nothing()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            uint hash = run.ComputeStateHash();

            Assert.That(ui.Presenter.ConfirmMergeChoice(0), Is.False, "there is nothing to confirm");
            Assert.That(run.ComputeStateHash(), Is.EqualTo(hash), "and nothing changed");
            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(0), "nothing was merged");
            Assert.That(ui.Presenter.Counters.MergeChoicesConfirmed, Is.EqualTo(0), "and nothing was confirmed");
        }

        // -------------------------------------------------------------------
        // a gesture changes nothing until it is released
        // -------------------------------------------------------------------

        [Test]
        public void Sweeping_a_drag_over_every_surface_changes_nothing_in_the_run()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            uint hash = run.ComputeStateHash();
            int revision = run.Backpack.Revision;
            int commands = run.Log.CommandCount;
            int bia = run.Wallet.Bia;

            PieceRef weapon = run.Backpack.Items[0].Ref;
            ui.Presenter.DragStart(weapon, Rotation.Degrees0);

            CampBagScreen bag = ui.State.Bag.Value;
            int moves = 0;

            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    ui.Presenter.DragMove(BagDropZone.Board, new CellOffset(column, row));
                    moves++;
                }
            }

            ui.Presenter.DragMove(BagDropZone.Chest, default);
            ui.Presenter.DragMove(BagDropZone.Altar, default);
            ui.Presenter.DragMove(BagDropZone.None, default);
            moves += 3;

            Assert.That(ui.Presenter.Counters.DragPreviews, Is.GreaterThanOrEqualTo(moves), "every pointer move asked the run");
            Assert.That(ui.Presenter.Counters.InvalidPreviews, Is.GreaterThan(0), "some of those cells are illegal, so both answers were exercised");
            Assert.That(ui.Presenter.IsDragging, Is.True, "the gesture is still in progress");

            Assert.That(run.ComputeStateHash(), Is.EqualTo(hash), "a drag must not change the run");
            Assert.That(run.Backpack.Revision, Is.EqualTo(revision), "a preview must not bump the revision");
            Assert.That(run.Log.CommandCount, Is.EqualTo(commands), "a preview is not a recorded command");
            Assert.That(run.Wallet.Bia, Is.EqualTo(bia), "a preview never moves bia");

            ui.Presenter.CancelDrag();

            Assert.That(run.ComputeStateHash(), Is.EqualTo(hash), "a cancelled drag leaves nothing behind either");
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp));
        }

        [Test]
        public void A_long_pointer_sweep_allocates_nothing()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            PieceRef weapon = run.Backpack.Items[0].Ref;
            ui.Presenter.DragStart(weapon, Rotation.Degrees0);

            var moving = new CellOffset(1, 1);
            ui.Presenter.DragMove(BagDropZone.Board, moving);

            const int moves = 2000;

            // A pointer move inside one cell: the verdict cannot have changed, so the whole call has
            // to be a comparison and a counter. Nothing is formatted and nothing is published.
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < moves; i++)
            {
                ui.Presenter.DragMove(BagDropZone.Board, moving);
            }

            long sameCell = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(
                sameCell,
                Is.Zero,
                "a pointer move inside one cell allocated " + sameCell + " bytes over " + moves + " calls");

            // Sweeping across the board changes the verdict on most calls, and even that is free: the
            // refusal is formatted only when the refusal is a different one.
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < moves; i++)
            {
                ui.Presenter.DragMove(BagDropZone.Board, new CellOffset(i % 8, (i / 8) % 6));
            }

            long acrossCells = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(
                acrossCells,
                Is.Zero,
                "sweeping the board allocated " + acrossCells + " bytes over " + moves + " calls; a per-move string or a boxed drag state would show up here");

            Assert.That(ui.Presenter.IsDragging, Is.True, "the gesture survived the measurement");
        }

        // -------------------------------------------------------------------
        // failure keeps its promises
        // -------------------------------------------------------------------

        [Test]
        public void An_invalid_drop_loses_nothing_and_says_which_rule_refused_it()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            PieceRef weapon = run.Backpack.Items[0].Ref;
            CellOffset origin = OriginOf(run, weapon);
            Rotation rotation = RotationOf(run, weapon);
            int revision = run.Backpack.Revision;
            int wallet = run.Wallet.Bia;

            ui.Presenter.DragStart(weapon, rotation);

            // The far corner of the board is outside every bag, so no ActiveSlot covers it and the
            // run refuses the placement.
            ui.Presenter.DragMove(
                BagDropZone.Board, new CellOffset(run.Backpack.Board.Width - 1, run.Backpack.Board.Height - 1));

            Assert.That(ui.Presenter.Drag.IsValid, Is.False, "the preview refuses a cell with no bag under it");
            Assert.That(ui.Presenter.Drag.ErrorCode, Is.Not.Empty, "a refusal always carries a code");
            Assert.That(ui.Presenter.Counters.InvalidPreviews, Is.GreaterThan(0));

            string refusal = ui.Presenter.Drag.ErrorCode;
            ui.Presenter.Drop();

            Assert.That(ui.Presenter.IsDragging, Is.False, "the gesture ended");
            Assert.That(ui.Presenter.Counters.InvalidDrops, Is.EqualTo(1));
            Assert.That(ui.Presenter.Counters.Placed, Is.Zero, "an invalid drop is not a placement");

            Assert.That(OriginOf(run, weapon), Is.EqualTo(origin), "the piece never moved");
            Assert.That(RotationOf(run, weapon), Is.EqualTo(rotation));
            Assert.That(run.Backpack.Revision, Is.EqualTo(revision), "a refused command must not bump the revision");
            Assert.That(run.Wallet.Bia, Is.EqualTo(wallet));
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(1), "nothing was consumed");

            // And the screen says why, in the player's language: the code is an API, not a caption.
            Assert.That(ui.State.StatusLine.Value, Is.Not.Empty, "the refusal is on screen");
            Assert.That(ui.State.StatusLine.Value, Is.Not.EqualTo(refusal), "the caption is text, not the error code");
            Assert.That(
                ui.State.StatusLine.Value,
                Is.EqualTo(Text("error." + refusal)),
                "the caption comes from localization, keyed by the code");
        }

        [Test]
        public void Escape_cancels_the_drag_and_the_picture_comes_back()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            PieceRef weapon = run.Backpack.Items[0].Ref;
            CellOffset origin = OriginOf(run, weapon);
            int revision = run.Backpack.Revision;

            ui.Presenter.DragStart(weapon, Rotation.Degrees0);

            // A cell the weapon may be placed on that is not the cell it already occupies, so the
            // ghost really does move.
            CellOffset target = PlaceableCellOtherThan(ui.Presenter, weapon, origin);
            Assert.That(target.X, Is.GreaterThanOrEqualTo(0), "the bag has a free slot for the weapon to hover over");

            ui.Presenter.DragMove(BagDropZone.Board, target);

            Assert.That(ui.Presenter.Drag.IsValid, Is.True);
            Assert.That(ui.Presenter.Drag.Origin, Is.EqualTo(target), "the ghost followed the pointer");
            Assert.That(ui.State.Drag.Value.IsActive, Is.True, "and it is on screen");

            ui.Presenter.CancelDrag();

            Assert.That(ui.Presenter.IsDragging, Is.False);
            Assert.That(ui.Presenter.Counters.DragCancels, Is.EqualTo(1));
            Assert.That(ui.Presenter.Counters.Placed, Is.Zero, "a cancelled drag is not a placement");
            Assert.That(ui.State.Drag.Value.IsActive, Is.False, "the ghost is off screen");

            Assert.That(OriginOf(run, weapon), Is.EqualTo(origin), "the piece is where it started");
            Assert.That(run.Backpack.Revision, Is.EqualTo(revision), "the Domain was never told about the gesture");
            Assert.That(ui.State.StatusLine.Value, Is.Empty, "a cancelled drag leaves no refusal on screen");
        }

        // -------------------------------------------------------------------
        // Stage 09f: a bag brings the items on it along
        // -------------------------------------------------------------------

        /// <summary>
        /// Stage 09f / ADR 0020: a bag drag publishes the items that come with it and where each of
        /// them would land, so the screen can draw their ghosts.
        /// </summary>
        /// <remarks>
        /// The plan is the run's — the same one the accepted command applies — so the ghost's cell is
        /// compared with the cell the drop itself produced rather than with a number the test worked
        /// out. The three cases a plan has to get right are all here: a bag that carries something, an
        /// item (nothing rides with an item), and a bag dragged towards the chest (nothing rides with it
        /// there, which is exactly why that drop is refused while an item stands on it).
        /// </remarks>
        [Test]
        public void Dragging_a_bag_publishes_a_ghost_for_every_item_that_rides_and_where_it_lands()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            PieceRef bag = run.Backpack.Bags[0].Ref;
            PieceRef weapon = run.Backpack.Items[0].Ref;

            CellOffset bagOrigin = OriginOf(run, bag);
            CellOffset weaponOrigin = OriginOf(run, weapon);
            Assert.That(weaponOrigin, Is.EqualTo(bagOrigin), "the starting weapon is expected to stand on the starting bag");

            ui.Presenter.DragStart(bag, Rotation.Degrees0);

            CellOffset target = FirstPlaceableCell(ui.Presenter, bag, out BagDropAction action);
            Assert.That(target.X, Is.GreaterThanOrEqualTo(0), "the fixture found no free cell to drag the bag to");
            Assert.That(action, Is.EqualTo(BagDropAction.Place));

            Assert.That(ui.Presenter.Drag.CarryCount, Is.EqualTo(1), "the weapon stands on the bag, so it rides with it");

            CampCarryGhost plan = ui.Presenter.Drag.CarryAt(0);
            var expected = new CellOffset(
                target.X + weaponOrigin.X - bagOrigin.X, target.Y + weaponOrigin.Y - bagOrigin.Y);

            Assert.That(plan.Piece, Is.EqualTo(weapon));
            Assert.That(plan.Origin, Is.EqualTo(expected), "a move carries the item rigidly, so its cell keeps its offset from the bag's");
            Assert.That(plan.Width, Is.EqualTo(1), "the weapon's footprint once it lands is the one the board rules read");
            Assert.That(plan.Height, Is.EqualTo(1));
            Assert.That(ui.State.Drag.Value.CarryCount, Is.EqualTo(1), "the plan is on screen, not only inside the presenter");

            ui.Presenter.Drop();

            Assert.That(ui.Presenter.Counters.Placed, Is.EqualTo(1));
            Assert.That(OriginOf(run, bag), Is.EqualTo(target));
            Assert.That(OriginOf(run, weapon), Is.EqualTo(plan.Origin), "the ghost was where the item really landed");

            // Nothing rides with an item: its drag publishes no plan at all, which is also what keeps a
            // pointer move free during the gesture the stage measures.
            ui.Presenter.DragStart(weapon, Rotation.Degrees0);
            ui.Presenter.DragMove(BagDropZone.Board, target);

            Assert.That(ui.Presenter.Drag.CarryCount, Is.Zero, "an item carries nothing");
            ui.Presenter.CancelDrag();

            // A bag on its way to the chest leaves its items where they are, so the run refuses it.
            ui.Presenter.DragStart(bag, Rotation.Degrees0);
            ui.Presenter.DragMove(BagDropZone.Chest, default);

            Assert.That(ui.Presenter.Drag.CarryCount, Is.Zero, "a bag takes nothing with it into the chest");
            Assert.That(ui.Presenter.Drag.IsValid, Is.False, "and the run refuses that move while the item is on it");
            Assert.That(ui.Presenter.Drag.ErrorCode, Is.EqualTo(InventoryError.ItemStranded));
            ui.Presenter.CancelDrag();
        }

        /// <summary>
        /// The Stage 09 measurement, still made for the piece that now has more to publish: a pointer
        /// move inside one cell during a <b>bag</b> drag allocates nothing, because a plan that cannot
        /// have changed is not worked out again.
        /// </summary>
        [Test]
        public void A_pointer_move_inside_one_cell_allocates_nothing_while_a_bag_is_in_the_air()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;
            PieceRef bag = run.Backpack.Bags[0].Ref;

            ui.Presenter.DragStart(bag, Rotation.Degrees0);

            CellOffset target = FirstPlaceableCell(ui.Presenter, bag, out _);
            Assert.That(target.X, Is.GreaterThanOrEqualTo(0), "the fixture found no free cell to drag the bag to");
            Assert.That(ui.Presenter.Drag.CarryCount, Is.EqualTo(1), "the measured drag has to be one that carries something");

            const int moves = 2000;
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < moves; i++)
            {
                ui.Presenter.DragMove(BagDropZone.Board, target);
            }

            long sameCell = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(
                sameCell,
                Is.Zero,
                "a pointer move inside one cell allocated " + sameCell + " bytes over " + moves + " calls");
            Assert.That(ui.Presenter.IsDragging, Is.True, "the gesture survived the measurement");
        }

        /// <summary>
        /// Stage 09f / ADR 0020: the combo lines are recomputed after a bag <b>turn</b>, because the
        /// items on the bag turned with it. One quarter turn takes the tome and the weapon off each
        /// other's row, so the same-line rule stops firing and its line goes — while the bag-area badge,
        /// which is never a line, stays.
        /// </summary>
        /// <remarks>
        /// The turn makes <i>two</i> decisions at once, which is the point: the items moved (the bag
        /// carries them) and the rules were read from where they landed. A screen that had re-used the
        /// previous revision's marks would still be drawing the line here — that failure is the one this
        /// test is for (it is the shape of the Stage 09d defect).
        /// </remarks>
        [Test]
        public void A_bag_turn_takes_the_combo_line_with_the_items_that_ride()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;
            Backpack backpack = run.Backpack;

            PieceRef bag = backpack.Bags[0].Ref;
            PieceRef weapon = backpack.Items[0].Ref;
            CellOffset bagOrigin = OriginOf(run, bag);

            CampPieceModel bagModel = PieceOf(ui.Presenter.Bag, bag);
            Assert.That(bagModel.Shape.Width, Is.GreaterThanOrEqualTo(3), "this fixture needs a bag at least 3 columns wide");
            Assert.That(bagModel.Shape.Height, Is.GreaterThanOrEqualTo(2), "and two rows tall, for the tome");

            // The weapon off the cells the tome will take, then a 2x2 tome onto the bag. The weapon's
            // own row is the bag's top row, so the tome's two rows include it: that is the rule's
            // condition, and the fixture checks the fact before asking the screen to draw it.
            var weaponCell = new CellOffset(bagOrigin.X + 2, bagOrigin.Y);

            ui.Presenter.DragStart(weapon, Rotation.Degrees0);
            ui.Presenter.DragMove(BagDropZone.Board, weaponCell);
            ui.Presenter.Drop();

            Assert.That(OriginOf(run, weapon), Is.EqualTo(weaponCell), "the weapon had to leave the tome's cells");

            Result<PieceRef> added = backpack.AddItem("item.spell_tome");
            Assert.That(added.IsSuccess, Is.True, "the fixture could not take a tome: " + added.Error);
            PieceRef tome = added.Value;

            ui.Presenter.Refresh();
            ui.Presenter.DragStart(tome, Rotation.Degrees0);
            ui.Presenter.DragMove(BagDropZone.Board, bagOrigin);
            ui.Presenter.Drop();

            Assert.That(OriginOf(run, tome), Is.EqualTo(bagOrigin), "the tome did not land on the bag's own top-left cell");

            CampPieceModel tomeModel = PieceOf(ui.Presenter.Bag, tome);
            CampPieceModel weaponModel = PieceOf(ui.Presenter.Bag, weapon);

            Assert.That(
                Touches(tomeModel, tomeModel.Origin, weaponModel),
                Is.True,
                "the fixture could not put the pair side by side, so the line cannot be checked");
            Assert.That(
                HasLine(ui.Presenter.Bag.Links, tome.InstanceId, weapon.InstanceId),
                Is.True,
                "the pair touches but the screen drew no line for it");

            int revision = backpack.Revision;
            ui.Presenter.SelectOnly(bag);
            ui.Presenter.RotateSelected();

            Assert.That(ui.Presenter.Counters.Rotated, Is.EqualTo(1), "the turn was accepted");
            Assert.That(backpack.Revision, Is.EqualTo(revision + 1), "one gesture is one change");
            Assert.That(OriginOf(run, weapon), Is.Not.EqualTo(weaponCell), "the weapon did not ride with the bag");

            tomeModel = PieceOf(ui.Presenter.Bag, tome);
            weaponModel = PieceOf(ui.Presenter.Bag, weapon);

            // Since ADR 0031 the mark follows the pieces, not a row: whatever the turn did to their
            // cells, the line is there exactly when the two still touch. A stale cache shows up as a
            // line for a pair that no longer touches (or none for a pair that does).
            Assert.That(
                HasLine(ui.Presenter.Bag.Links, tome.InstanceId, weapon.InstanceId),
                Is.EqualTo(Touches(tomeModel, tomeModel.Origin, weaponModel)),
                "the line does not match where the turn left the two pieces: " + Links(ui));
            Assert.That(
                HasBadgeFromBag(ui.Presenter.Bag.Links, bag.InstanceId),
                Is.True,
                "the bag-area bonus is a badge and must not go with the line");
        }

        /// <summary>True when a line is drawn from <paramref name="source"/> to <paramref name="target"/>.</summary>
        private static bool HasLine(CampLink[] links, int source, int target)
        {
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style == CampLinkStyle.Line
                    && links[i].SourceInstanceId == source
                    && links[i].TargetInstanceId == target)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the bag's own area rule is marked, which is a badge and never a line.</summary>
        private static bool HasBadgeFromBag(CampLink[] links, int bagInstanceId)
        {
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style != CampLinkStyle.Line && links[i].SourceInstanceId == bagInstanceId)
                {
                    return true;
                }
            }

            return false;
        }

        // -------------------------------------------------------------------
        // the tooltip and the bottom bar
        // -------------------------------------------------------------------

        [Test]
        public void The_tooltip_shows_the_tier_the_skill_and_the_combos_of_the_piece_under_the_pointer()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            Assert.That(ui.State.TooltipVisible.Value, Is.False, "nothing is hovered at first");

            PieceRef weapon = run.Backpack.Items[0].Ref;
            ui.Presenter.Hover(weapon);

            Assert.That(ui.State.TooltipVisible.Value, Is.True);
            Assert.That(ui.State.TooltipTitle.Value, Is.Not.Empty);
            Assert.That(ui.State.TooltipTitle.Value, Does.Not.Contain("item."), "the title is the localized name, not the id");

            // The numbers are read off the shipped tables, never written into the test: a retuned
            // skill must not be able to make a correct tooltip look wrong.
            Assert.That(_config.TryGetItem(RunDefaults.InitialWeaponId, out ItemConfig weaponConfig), Is.True);
            Assert.That(_config.TryGetSkill(weaponConfig.SkillId, out SkillConfig skill), Is.True, "the starting weapon has a skill");

            // Stage 09e item 2: one label per line — ชื่อ, ระดับ, ค่าที่ใช้จริง, รุ่นย่อยที่เลือก,
            // คำอธิบาย, คอมโบที่ทำงานอยู่ — so the screen owns one label each. One combined string
            // would be the shape that stage replaced, and the halves are asserted apart here.
            string tier = ui.State.TooltipTier.Value;
            Assert.That(tier, Does.Not.Contain("camp.tooltip"), "every key resolved");
            Assert.That(
                tier,
                Is.EqualTo(Format("camp.tooltip.tier.format", run.Backpack.Items[0].Tier, _config.Constant.MaxTier)),
                "the tier line is built from the surface's own format");

            // Stage 09h: the numbers are the run's resolved ones, with the table's row beside them —
            // never the skill row on its own (that is what made 3/3 read like 1/1).
            PieceReadout readout = Explain(run, weapon);
            string resolvedLine = Format(
                "camp.tooltip.stat.format",
                readout.Resolved.Damage,
                readout.Resolved.CooldownSeconds,
                readout.Resolved.MpCost);
            string baseLine = Format(
                "camp.tooltip.base.format",
                Format(
                    "camp.tooltip.stat.format",
                    skill.Damage,
                    skill.CooldownSeconds,
                    skill.MpCost));

            string status = ui.State.TooltipStatus.Value;
            Assert.That(status, Does.Not.Contain("camp.tooltip"), "every key resolved");
            Assert.That(
                status,
                Is.EqualTo(resolvedLine + "\n" + baseLine),
                "the tooltip prints the resolved row and the table's row under it");
            Assert.That(status, Does.Not.Contain(tier), "ระดับ is its own line, not a prefix of สถานะ");
            Assert.That(
                readout.Resolved.CooldownSeconds,
                Is.EqualTo(skill.CooldownSeconds).Within(0.0001f),
                "a tier-1 piece with no cooldown variant resolves to its base cooldown");
            Assert.That(
                ui.State.TooltipUpgrades.Value,
                Is.Empty,
                "with no merge behind it there is no variant line at all");

            Assert.That(
                ui.State.TooltipDescription.Value,
                Is.EqualTo(Text(weaponConfig.DescriptionKey)),
                "คำอธิบาย is the item's own description row, localized");

            Assert.That(ui.State.TooltipCombos.Value, Is.Not.Empty, "a piece with no combo still says so");

            // The highlight names cells, and the piece's own cell is always among them.
            CellOffset origin = OriginOf(run, weapon);
            CellOffset[] highlight = ui.State.Bag.Value.Highlight;

            Assert.That(highlight.Length, Is.GreaterThan(0), "the hovered piece highlights itself");

            var found = false;
            for (int i = 0; i < highlight.Length; i++)
            {
                found |= highlight[i] == origin;
            }

            Assert.That(found, Is.True, "the highlight includes the piece's own cell");

            ui.Presenter.ClearHover();

            Assert.That(ui.State.TooltipVisible.Value, Is.False);
            Assert.That(ui.State.Bag.Value.Highlight.Length, Is.Zero, "the highlight goes away with the pointer");
        }

        /// <summary>
        /// Stage 11a: a charm and an offering print the effect their data row carries, not just
        /// their category. The expected strings are built from the row through the same
        /// localization keys, so a balance edit moves the test with the data.
        /// </summary>
        [Test]
        public void The_tooltip_shows_a_charms_and_an_offerings_effect()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            PieceRef charm = run.Backpack.AddItem("item.phra_amulet").Value;
            Assert.That(run.MovePiece(charm, new CellOffset(1, 0), Rotation.Degrees0).IsSuccess, Is.True);
            Assert.That(_config.TryGetItem("item.phra_amulet", out ItemConfig charmRow), Is.True);

            ui.Presenter.Hover(charm);

            string charmEffect = Format(
                "camp.tooltip.charm_effect.format",
                Text("upgrade.stat.max_hp"),
                "+" + charmRow.CharmMagnitude.ToString("0.##", CultureInfo.InvariantCulture));
            Assert.That(
                ui.State.TooltipStatus.Value,
                Is.EqualTo(Text("camp.category.charm") + "\n" + charmEffect),
                "the charm's own passive is on screen (visible=" + ui.State.TooltipVisible.Value
                + ", title='" + ui.State.TooltipTitle.Value + "', category='"
                + Text("camp.category.charm") + "', effect='" + charmEffect + "')");

            PieceRef offering = run.Backpack.AddItem("item.red_soda").Value;
            Assert.That(run.MovePiece(offering, new CellOffset(2, 0), Rotation.Degrees0).IsSuccess, Is.True);
            Assert.That(_config.TryGetItem("item.red_soda", out ItemConfig offeringRow), Is.True);

            ui.Presenter.Hover(offering);

            string expected = Text("camp.category.offering")
                + "\n" + Format(
                    "camp.tooltip.offering_heal.format", offeringRow.OfferingAmount, offeringRow.OfferingUses)
                + "\n" + Format("camp.tooltip.offering_auto.format", offeringRow.OfferingAutoHpPercent);

            Assert.That(ui.State.TooltipStatus.Value, Is.EqualTo(expected), "the offering's effect and uses are on screen");
        }

        /// <summary>
        /// Stage 09h: after a card is confirmed the tooltip prints the numbers the loadout resolved,
        /// the step that produced them, and nothing stale; undo puts the old numbers back.
        /// </summary>
        /// <remarks>
        /// The card is chosen to be one that moves damage, cooldown or focus (a range-only variant
        /// would change the build without changing this line), and the expected strings are built
        /// from the run's read model and the snapshot it comes from — never from a literal.
        /// </remarks>
        [Test]
        public void The_tooltip_shows_the_resolved_numbers_of_the_variant_that_was_picked()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            RunSession run = ui.Run;

            ui.Presenter.Hover(survivor);
            string before = ui.State.TooltipStatus.Value;
            Assert.That(ui.State.TooltipUpgrades.Value, Is.Empty, "a tier-1 piece has no step to describe");

            DropOnto(ui.Presenter, run, twin, survivor);
            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.True, "the pair asks which upgrade to keep");

            int card = FirstCardTouchingTheShownStats(ui.Presenter);
            Assert.That(card, Is.GreaterThanOrEqualTo(0), "the offer has a card that moves damage, cooldown or focus");
            CampMergeChoiceModel choice = ui.Presenter.MergeChooser.Choices[card];

            Assert.That(ui.Presenter.ConfirmMergeChoice(card), Is.True, "the card is confirmed");
            Assert.That(ui.State.TooltipVisible.Value, Is.True, "the pointer is still on the piece");

            // The tooltip's numbers are the snapshot's numbers, field for field.
            PieceReadout readout = Explain(run, survivor);
            WeaponLoadout snapshot = Loadout(run).Weapons[0];

            Assert.That(readout.Tier, Is.EqualTo(2), "one merge step");
            Assert.That(readout.Resolved.Damage, Is.EqualTo(snapshot.Damage));
            Assert.That(readout.Resolved.CooldownSeconds, Is.EqualTo(snapshot.CooldownSeconds).Within(0.0001f));
            Assert.That(readout.Resolved.MpCost, Is.EqualTo(snapshot.MpCost));

            string expected = Format(
                "camp.tooltip.stat.format",
                snapshot.Damage,
                snapshot.CooldownSeconds,
                snapshot.MpCost);

            Assert.That(ui.State.TooltipStatus.Value, Does.Contain(expected), "the resolved row is what is on screen");
            Assert.That(ui.State.TooltipStatus.Value, Is.Not.EqualTo(before), "the picked card moved the tooltip");

            // The step names its variant and each stat it changed, the way the card wrote it.
            string upgrades = ui.State.TooltipUpgrades.Value;
            Assert.That(upgrades, Is.Not.Empty, "a confirmed variant has its line");
            Assert.That(
                upgrades,
                Does.Contain(Text(VariantOf(choice.VariantId).NameKey)),
                "the line names the variant that was picked");

            for (int i = 0; i < choice.Changes.Length; i++)
            {
                Assert.That(upgrades, Does.Contain(choice.Changes[i]), "and repeats the card's own change line");
            }

            // Undo is the way back: the readout is a function of the run, not a frozen string.
            ui.Presenter.UndoEdit();

            Assert.That(ui.State.TooltipStatus.Value, Is.EqualTo(before), "undo shows the numbers from before the merge");
            Assert.That(ui.State.TooltipUpgrades.Value, Is.Empty, "and the step line is gone with the step");
        }

        /// <summary>
        /// The first card whose variant changes a stat the tooltip prints. Damage, cooldown and focus
        /// are what the สถานะ line carries; a range-only upgrade changes the build without changing it.
        /// </summary>
        private int FirstCardTouchingTheShownStats(CampPresenter presenter)
        {
            CampMergeChooserModel chooser = presenter.MergeChooser;

            for (int i = 0; i < chooser.ChoiceCount; i++)
            {
                IReadOnlyList<UpgradeModifierConfig> modifiers = VariantOf(chooser.Choices[i].VariantId).Modifiers;

                for (int m = 0; m < modifiers.Count; m++)
                {
                    UpgradeStat stat = modifiers[m].Stat;
                    if (stat == UpgradeStat.Damage || stat == UpgradeStat.Cooldown || stat == UpgradeStat.MpCost)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// A bag has no table description, so its tooltip says so by <b>leaving the line out</b>
        /// rather than by drawing a placeholder: the five lines are the shape of the panel, and an
        /// empty one is a line the view hides.
        /// </summary>
        [Test]
        public void A_bag_shows_its_usable_slots_and_no_description()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            PieceRef bag = run.Backpack.Bags[0].Ref;
            ui.Presenter.Hover(bag);

            Assert.That(ui.State.TooltipVisible.Value, Is.True);
            Assert.That(ui.State.TooltipTitle.Value, Is.Not.Empty, "the bag has a name");
            Assert.That(ui.State.TooltipTier.Value, Is.Not.Empty, "and a tier");
            Assert.That(
                ui.State.TooltipStatus.Value,
                Is.Not.Empty,
                "a bag's สถานะ line is the slots it opens");
            Assert.That(
                ui.State.TooltipDescription.Value,
                Is.Empty,
                "a bag's table has no description row, so the line is empty rather than invented");
        }

        /// <summary>
        /// The stage's line rule: a line is drawn for a ตำรา that touches an อาวุธอาคม, for nothing
        /// else, and it is gone the moment the pair stops touching (Stage 09l, ADR 0032).
        /// </summary>
        /// <remarks>
        /// The pairs come from <c>SynergyEvaluator</c>; what is under test is the <b>classification</b>
        /// the screen draws from — which rule gets a line, which gets a badge and which gets no
        /// permanent mark at all. The board is arranged through the run's own commands (this test is
        /// about what the screen draws, not about how a piece got there), and the tome is the one the
        /// seed's own shop offers at the first camp: no item id is written into the test.
        /// </remarks>
        [Test]
        public void Only_the_tome_to_weapon_rule_is_drawn_as_a_line_and_the_bag_bonus_is_not()
        {
            // The shipped tables, exactly as they are: since Stage 09k the tome's rule is an
            // AdjacentPair, and the screen's answer to that is to look at the rule's own two tags.
            IGameConfig config = _config;
            const string tomeTag = "tome";

            uint seed = SeedOffering(tomeTag, out int slot);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "no seed in range offered a '" + tomeTag + "' piece in any camp of its run");

            var run = new RunSession(config, _codec, seed);
            Assert.That(run.StartRun().IsSuccess, Is.True);

            // The tome is a mid-run offer (the shop's own floor band says so), so the walk to it is
            // made the way the game makes it: win the floors in front of it. Nothing synthesises a
            // camp, and no floor number is written into the test.
            int slotReached = 0;
            while (run.FlowState == RunFlowState.Camp && (slotReached = SlotOfferingTag(run, tomeTag)) < 0)
            {
                Assert.That(WinOneFloor(run), Is.True, "the run could not reach the camp that offers a '" + tomeTag + "' piece");
            }

            Assert.That(slotReached, Is.GreaterThanOrEqualTo(0), "the searched camp stopped offering the piece it was found for");

            var state = new CampUiState();
            var presenter = new CampPresenter(run, config, _text, state);
            presenter.Begin();

            presenter.Rent(slotReached);

            PieceRef tome = OnlyChestItem(run);
            Assert.That(tome.IsValid, Is.True, "the rented tome is carried in the chest");

            CampPieceModel weapon = FirstBoardItem(presenter.Bag);
            Assert.That(weapon.Kind, Is.EqualTo(PieceKind.Item));

            // Place the tome where it <b>touches</b> the weapon: that is the rule's whole condition since
            // Stage 09k, and the arrangement the line has to appear in.
            CellOffset placed = PlaceTouchingTheWeapon(presenter, tome, weapon, out bool rowsOverlap);

            Assert.That(rowsOverlap, Is.True, "the fixture could not put the tome against the weapon");
            Assert.That(placed.X, Is.GreaterThanOrEqualTo(0), "the bag has no free 2x2 block left for the tome");

            CampLink[] links = presenter.Bag.Links;
            Assert.That(links.Length, Is.GreaterThan(0), "the touching rule is working but the screen draws nothing for it");

            int lines = 0;
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style != CampLinkStyle.Line)
                {
                    continue;
                }

                lines++;

                // ห้ามวาดเส้นจากย่ามไปไอเทม: a bag is never an end of a line, whichever rule fired.
                Assert.That(
                    IsBag(run, links[i].SourceInstanceId), Is.False,
                    "a line was drawn from a bag: " + links[i]);
                Assert.That(
                    IsBag(run, links[i].TargetInstanceId), Is.False,
                    "a line was drawn to a bag: " + links[i]);
                Assert.That(
                    links[i].TargetInstanceId, Is.EqualTo(weapon.Piece.InstanceId),
                    "the only arcane weapon on the board is the one a line should reach");
            }

            Assert.That(lines, Is.EqualTo(1), "one touching pair is one line, and the screen drew " + lines);

            // The bag-area bonus is every item the bag's own rule reached: a badge, never a line.
            int badges = 0;
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style == CampLinkStyle.Line)
                {
                    continue;
                }

                badges++;
                Assert.That(
                    IsBag(run, links[i].SourceInstanceId), Is.True,
                    "only the area rule reaches an item from a bag, so a badge always comes from one: " + links[i]);
            }

            Assert.That(badges, Is.GreaterThan(0), "the starting kit puts an item inside a bag, so the area rule fired");

            // Move the tome where it no longer touches the weapon: the pair stops qualifying, so the
            // line has to be gone from the very next picture the screen publishes.
            CellOffset away = PlaceAwayFromTheWeapon(presenter, tome, weapon, out bool movedAway);
            Assert.That(movedAway, Is.True, "no free 2x2 block exists away from the weapon, so the drop cannot be checked");
            Assert.That(away, Is.Not.EqualTo(placed), "the tome has to leave the weapon's side for the line to go");
            Assert.That(
                OriginOf(run, tome), Is.EqualTo(away),
                "the tome did not land on the cell the scan chose: it is at " + OriginOf(run, tome));
            CampPieceModel tomeAfter = PieceOf(presenter.Bag, tome);
            CampPieceModel weaponAfter = PieceOf(presenter.Bag, weapon.Piece);

            Assert.That(
                Touches(tomeAfter, tomeAfter.Origin, weaponAfter),
                Is.False,
                "the fixture left the pair touching at " + OriginOf(run, tome) + " and " + OriginOf(run, weapon.Piece));

            links = presenter.Bag.Links;
            for (int i = 0; i < links.Length; i++)
            {
                Assert.That(
                    links[i].Style, Is.Not.EqualTo(CampLinkStyle.Line),
                    "a line survived the pair stopping touching: " + links[i]);
            }
        }

        // -------------------------------------------------------------------
        // the line's adjacency condition (Stage 09j)
        // -------------------------------------------------------------------

        /// <summary>
        /// ADR 0031: the row is not the condition any more. A weapon one cell further along the same row
        /// is <b>not</b> a working pair — Core says so and the screen draws nothing for it, which is the
        /// claim Stage 09j could not make (it needed Core's row rule to keep the far pair alive).
        /// </summary>
        [Test]
        public void A_tome_one_cell_away_on_the_same_row_is_not_a_combo_and_draws_nothing()
        {
            Screen ui = CampWithATome(out RunSession run, out _, out PieceRef weapon, out PieceRef tome);

            var secondBag = run.Backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(secondBag.IsSuccess, Is.True, "a second รยาม could not be added");
            Assert.That(MoveTo(run, secondBag.Value, 3, 1), Is.True, "the second รยาม could not be placed");
            Assert.That(MoveTo(run, weapon, 3, 1), Is.True, "the weapon could not be moved a cell past the tome's edge");
            Assert.That(MoveTo(run, tome, 0, 1), Is.True, "the tome could not be placed on the first รยาม");

            ui.Presenter.Refresh();

            SynergyEvaluation layout = new SynergyEvaluator(_config).Evaluate(run.Backpack);
            bool pairIsActive = false;
            for (int i = 0; i < layout.Active.Count; i++)
            {
                ActiveSynergy synergy = layout.Active[i];
                if ((synergy.SourceInstanceId == tome.InstanceId && synergy.TargetInstanceId == weapon.InstanceId)
                    || (synergy.SourceInstanceId == weapon.InstanceId && synergy.TargetInstanceId == tome.InstanceId))
                {
                    pairIsActive = true;
                }
            }

            Assert.That(
                pairIsActive, Is.False,
                "Core still reports the pair a cell apart, so the rule is still reading a shared row");
            Assert.That(
                HasLineBetween(ui.Presenter.Bag.Links, tome.InstanceId, weapon.InstanceId), Is.False,
                "a line was drawn across an empty cell to a weapon the tome does not touch: " + Links(ui));
        }

        /// <summary>
        /// Two pieces that only touch at a corner are not neighbours: the rule does not fire and the
        /// screen draws nothing, which is the diagonal the stage excludes.
        /// </summary>
        [Test]
        public void A_tome_touching_a_weapon_only_at_a_corner_draws_nothing()
        {
            Screen ui = CampWithATome(out RunSession run, out _, out PieceRef weapon, out PieceRef tome);

            Assert.That(MoveTo(run, weapon, 2, 3), Is.True, "the weapon could not be placed diagonally off the tome's corner");
            Assert.That(MoveTo(run, tome, 0, 1), Is.True, "the tome could not be placed on the รยาม");

            ui.Presenter.Refresh();

            SynergyEvaluation layout = new SynergyEvaluator(_config).Evaluate(run.Backpack);
            for (int i = 0; i < layout.Active.Count; i++)
            {
                Assert.That(
                    layout.Active[i].RuleId, Is.Not.EqualTo("synergy.tome_row_damage"),
                    "a corner touch must not count as the touching rule's condition");
            }

            Assert.That(
                HasLineBetween(ui.Presenter.Bag.Links, tome.InstanceId, weapon.InstanceId), Is.False,
                "a diagonal touch drew a line: " + Links(ui));
        }

        /// <summary>
        /// One tome can touch two weapons — its right edge spans two cells, so one partner per row —
        /// and each of them draws its own line, because each is its own working pair (Stage 09l).
        /// </summary>
        [Test]
        public void A_tome_that_touches_two_weapons_draws_two_lines()
        {
            Screen ui = CampWithATome(out RunSession run, out _, out PieceRef weapon, out PieceRef tome);

            var secondWeapon = run.Backpack.AddItem("item.kali_flame_yant");
            Assert.That(secondWeapon.IsSuccess, Is.True, "the run could not take a second weapon");

            Assert.That(MoveTo(run, weapon, 2, 1), Is.True, "the first weapon could not be placed beside the tome");
            Assert.That(MoveTo(run, secondWeapon.Value, 2, 2), Is.True, "the second weapon could not be placed beside the tome");
            Assert.That(MoveTo(run, tome, 0, 1), Is.True, "the tome could not be placed on the รยาม");

            ui.Presenter.Refresh();

            int lines = 0;
            CampLink[] links = ui.Presenter.Bag.Links;
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style == CampLinkStyle.Line)
                {
                    lines++;
                }
            }

            Assert.That(
                HasLineBetween(links, tome.InstanceId, weapon.InstanceId), Is.True,
                "the tome's first neighbour has no line: " + Links(ui));
            Assert.That(
                HasLineBetween(links, tome.InstanceId, secondWeapon.Value.InstanceId), Is.True,
                "the tome's second neighbour has no line: " + Links(ui));
            Assert.That(lines, Is.EqualTo(2), "two touching pairs should be two lines, not " + lines);
        }

        // -------------------------------------------------------------------
        // the grab offset (Stage 09k)
        // -------------------------------------------------------------------

        /// <summary>
        /// The cell the player grabbed stays under the pointer: the origin is the pointer's cell minus
        /// that cell's own offset, for a 1×1 piece and for a 2×2 grabbed by each of its four corners.
        /// </summary>
        [Test]
        public void The_cell_a_player_grabs_stays_under_the_pointer()
        {
            Screen ui = CampWithATome(out RunSession run, out _, out PieceRef weapon, out PieceRef tome);
            Assert.That(MoveTo(run, tome, 0, 1), Is.True, "the tome could not be placed at (0,1)");
            ui.Presenter.Refresh();

            CellOffset weaponOrigin = OriginOf(run, weapon);
            var pointer = new CellOffset(5, 3);

            ui.Presenter.DragStart(weapon, Rotation.Degrees0, weaponOrigin);
            ui.Presenter.DragMove(BagDropZone.Board, pointer);

            Assert.That(
                ui.Presenter.Drag.Origin, Is.EqualTo(pointer),
                "a 1x1 piece grabbed by its only cell should follow the pointer exactly");

            ui.Presenter.CancelDrag();

            CellOffset tomeOrigin = OriginOf(run, tome);
            for (int corner = 0; corner < 4; corner++)
            {
                var offset = new CellOffset(corner & 1, (corner >> 1) & 1);
                var grabbed = new CellOffset(tomeOrigin.X + offset.X, tomeOrigin.Y + offset.Y);
                var expected = new CellOffset(pointer.X - offset.X, pointer.Y - offset.Y);

                ui.Presenter.DragStart(tome, Rotation.Degrees0, grabbed);
                ui.Presenter.DragMove(BagDropZone.Board, pointer);

                Assert.That(
                    ui.Presenter.Drag.Origin, Is.EqualTo(expected),
                    "grabbing " + grabbed + " put the piece at " + ui.Presenter.Drag.Origin
                    + " instead of " + expected);

                ui.Presenter.CancelDrag();
            }
        }

        /// <summary>
        /// A piece turned while it is held keeps the grabbed cell under the pointer: the offset is
        /// turned with the piece by Core's own quarter-turn map, so a turn does not make it jump.
        /// </summary>
        [Test]
        public void A_turned_piece_keeps_the_grabbed_cell_under_the_pointer()
        {
            Screen ui = CampWithATome(out RunSession run, out _, out _, out PieceRef tome);
            Assert.That(MoveTo(run, tome, 0, 1), Is.True, "the tome could not be placed at (0,1)");
            ui.Presenter.Refresh();

            Footprint shape = PieceOf(ui.Presenter.Bag, tome).Shape;
            Assert.That(shape, Is.Not.Null, "the tome has no shape on screen");

            var grabbed = new CellOffset(1, 2);          // the 2x2's lower-right cell
            var local = new CellOffset(1, 1);            // relative to the piece's origin at (0,1)
            var pointer = new CellOffset(5, 3);

            FootprintRotation.Rotate(shape, Rotation.Degrees90, out CellOffset shift);
            CellOffset turned = FootprintRotation.Turn(local, Rotation.Degrees90);
            var expected = new CellOffset(
                pointer.X - (turned.X - shift.X), pointer.Y - (turned.Y - shift.Y));

            // Grab it while it stands at 0°, then turn it in flight: the grabbed cell has to stay
            // under the pointer, which means the origin moves with the turn.
            ui.Presenter.DragStart(tome, Rotation.Degrees0, grabbed);
            ui.Presenter.DragMove(BagDropZone.Board, pointer);

            Assert.That(
                ui.Presenter.Drag.Origin, Is.EqualTo(new CellOffset(4, 2)),
                "before any turn the piece should sit exactly one cell up and left of the pointer");

            ui.Presenter.RotateDrag();

            Assert.That(
                ui.Presenter.Drag.Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(
                ui.Presenter.Drag.Origin, Is.EqualTo(expected),
                "a piece grabbed at " + grabbed + " and turned in flight landed at "
                + ui.Presenter.Drag.Origin + " instead of " + expected);

            ui.Presenter.RotateDrag();

            FootprintRotation.Rotate(shape, Rotation.Degrees180, out CellOffset shift180);
            CellOffset turned180 = FootprintRotation.Turn(local, Rotation.Degrees180);
            var expected180 = new CellOffset(
                pointer.X - (turned180.X - shift180.X), pointer.Y - (turned180.Y - shift180.Y));

            Assert.That(
                ui.Presenter.Drag.Origin, Is.EqualTo(expected180),
                "a second turn did not keep the grabbed cell under the pointer");
        }

        /// <summary>
        /// The L-shaped รยาม: a 4-cell piece whose footprint changes shape when it turns, grabbed by
        /// the cell at the end of its foot. The pointer stays over that cell through a turn.
        /// </summary>
        [Test]
        public void The_L_shaped_bag_keeps_its_grabbed_cell_through_a_turn()
        {
            RunSession run = AtFirstCamp();
            var state = new CampUiState();
            var presenter = new CampPresenter(run, _config, _text, state);
            presenter.Begin();

            var added = run.Backpack.AddBag("bag.wicker_basket_l");
            Assert.That(added.IsSuccess, Is.True, "the run could not take the L-shaped รยาม");
            PieceRef bag = added.Value;
            Assert.That(MoveTo(run, bag, 0, 4), Is.True, "the L-shaped รยาม could not be placed at (0,4)");

            presenter.Refresh();

            var pointer = new CellOffset(5, 3);
            presenter.DragStart(bag, Rotation.Degrees0, new CellOffset(1, 6));
            presenter.DragMove(BagDropZone.Board, pointer);

            Assert.That(
                presenter.Drag.Origin, Is.EqualTo(new CellOffset(4, 1)),
                "the grabbed foot cell did not stay under the pointer: " + presenter.Drag.Origin);

            presenter.RotateDrag();

            CampDragState drag = presenter.Drag;
            Assert.That(drag.Rotation, Is.EqualTo(Rotation.Degrees90));

            Assert.That(run.Backpack.TryGetItem(bag.InstanceId, out _), Is.False, "the L-shaped bag is a bag, not an item");
            Assert.That(
                run.Backpack.TryGetPiece(bag, out PieceInstance piece) && piece != null, Is.True,
                "the run lost its L-shaped รยาม");

            Footprint turned = run.Backpack.ShapeAt(piece, drag.Rotation);
            var underPointer = new CellOffset(pointer.X - drag.Origin.X, pointer.Y - drag.Origin.Y);
            Assert.That(
                turned.Contains(underPointer), Is.True,
                "after the turn the pointer at " + pointer + " is no longer over the piece at " + drag.Origin);
        }

        // -------------------------------------------------------------------
        // the line's four directions (Stage 09l)
        // -------------------------------------------------------------------

        /// <summary>
        /// Stage 09l item B: a weapon on <b>any</b> of the tome's four sides gets its own line — left,
        /// right, above and below — because since ADR 0031 the rule is a touch on any edge.
        /// </summary>
        /// <remarks>
        /// Each direction gets its own board: four weapons on one board would make "one line per pair"
        /// impossible to tell apart from "one line per direction". The board is four 3×3 รยาม in a
        /// square, so the tome's four cells and a cell on every side of it are covered — an item is only
        /// legal on a cell a bag covers, which is what makes the arrangement a real one.
        /// </remarks>
        [Test]
        public void A_weapon_on_each_of_the_tomes_four_sides_draws_one_line_per_pair()
        {
            var sides = new (int X, int Y, string Name)[]
            {
                (X: 3, Y: 2, Name: "right of"),
                (X: 0, Y: 2, Name: "left of"),
                (X: 1, Y: 4, Name: "below"),
                (X: 1, Y: 1, Name: "above"),
            };

            for (int i = 0; i < sides.Length; i++)
            {
                Screen ui = CampWithRoomAroundTheTome(
                    out RunSession run, out PieceRef weapon, out PieceRef tome);

                Assert.That(
                    MoveTo(run, weapon, sides[i].X, sides[i].Y), Is.True,
                    "the weapon could not be placed " + sides[i].Name + " the tome at (" + sides[i].X + "," + sides[i].Y + ")");
                ui.Presenter.Refresh();

                CampLink[] links = ui.Presenter.Bag.Links;

                Assert.That(
                    HasLineBetween(links, tome.InstanceId, weapon.InstanceId), Is.True,
                    "a weapon " + sides[i].Name + " the tome draws no line: " + Links(ui));
                Assert.That(
                    CountLines(links), Is.EqualTo(1),
                    "one touching pair is one line, and a weapon " + sides[i].Name
                    + " the tome got " + CountLines(links) + ": " + Links(ui));
            }
        }

        /// <summary>How many lines a mark set holds.</summary>
        private static int CountLines(CampLink[] links)
        {
            int lines = 0;

            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style == CampLinkStyle.Line)
                {
                    lines++;
                }
            }

            return lines;
        }

        /// <summary>The first line in a mark set.</summary>
        private static CampLink FirstLine(CampLink[] links)
        {
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style == CampLinkStyle.Line)
                {
                    return links[i];
                }
            }

            Assert.Fail("the mark set holds no line");
            return default;
        }

        [Test]
        public void The_bottom_bar_reads_the_run_and_a_disabled_departure_explains_itself()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            Assert.That(ui.State.FloorValue.Value, Is.EqualTo(Format("camp.floor.format", run.FloorIndex)));
            Assert.That(ui.State.BiaValue.Value, Is.EqualTo(run.Wallet.Bia.ToString(CultureInfo.InvariantCulture)));
            Assert.That(
                ui.State.ChestValue.Value,
                Is.EqualTo(Format("camp.chest.format", run.ChestUsed, run.ChestSlots)));
            Assert.That(ui.State.WeightValue.Value, Is.Not.Empty);
            Assert.That(
                ui.State.RefreshCostValue.Value,
                Is.EqualTo(run.RefreshPrice.ToString(CultureInfo.InvariantCulture)));
            Assert.That(ui.State.CanDepart.Value, Is.True, "the starting kit has an arcane weapon on its slots");

            // Take the weapon off the board — the one reason a departure is refused — and the button
            // must go dead and say why, before the player presses it.
            PieceRef weapon = run.Backpack.Items[0].Ref;
            ui.Presenter.DragStart(weapon, Rotation.Degrees0);
            ui.Presenter.DragMove(BagDropZone.Chest, default);

            Assert.That(ui.Presenter.Drag.Action, Is.EqualTo(BagDropAction.Transfer), "the chest is a store, not a merge target");

            ui.Presenter.Drop();

            Assert.That(ui.Presenter.Counters.Stored, Is.EqualTo(1));
            Assert.That(ui.State.CanDepart.Value, Is.False, "no arcane weapon on the slots, no departure");
            Assert.That(ui.State.DepartReason.Value, Is.Not.Empty, "a disabled button shows its reason");
            Assert.That(
                ui.State.DepartReason.Value,
                Is.EqualTo(Text("error." + RunError.NoArcaneWeapon)),
                "the reason is the run's own refusal, localized");

            ui.Presenter.Depart();

            Assert.That(ui.Presenter.Departed, Is.False, "a refused departure does not leave");
            Assert.That(ui.Presenter.Counters.Departures, Is.Zero);
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp), "the run did not move");
            Assert.That(ui.State.StatusLine.Value, Is.Not.Empty, "the press is answered on screen");
        }

        [Test]
        public void Departing_runs_the_real_command_once_and_a_second_press_cannot_run_it_again()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;
            PieceRef weapon = run.Backpack.Items[0].Ref;

            ui.Presenter.Depart();

            Assert.That(ui.Presenter.Departed, Is.True);
            Assert.That(ui.Presenter.Counters.Departures, Is.EqualTo(1));
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Battle), "the run is where Core says a departure leaves it");
            Assert.That(ui.Presenter.DepartFloor, Is.EqualTo(run.FloorIndex), "the floor it left for is the one on the bar");
            Assert.That(ui.Presenter.DepartSeed, Is.Not.EqualTo(0u), "the floor's battle seed was drawn and recorded");
            Assert.That(ui.State.CanDepart.Value, Is.False, "the button is dead once the run has left camp");
            Assert.That(ui.State.DepartReason.Value, Is.EqualTo(Text("camp.departed")));

            // The click guard, and the run's own refusal behind it: two reasons a double press is
            // harmless, and both are checked.
            int commands = run.Log.CommandCount;

            ui.Presenter.Depart();

            Assert.That(ui.Presenter.Counters.Departures, Is.EqualTo(1));
            Assert.That(run.Log.CommandCount, Is.EqualTo(commands), "no second command reached the run");

            // Every other control now goes through the run and comes back with the phase's own refusal.
            ui.Presenter.SelectOnly(weapon);
            ui.Presenter.RotateSelected();

            Assert.That(ui.Presenter.Counters.Rotated, Is.Zero);
            Assert.That(
                ui.State.StatusLine.Value,
                Is.EqualTo(Text("error." + RunError.WrongState)),
                "a bag command outside camp is refused with run.wrong_state");

            ui.Presenter.Rent(0);

            Assert.That(ui.Presenter.Counters.Rented, Is.Zero, "the shop is closed once the run has left");
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Battle));
        }

        // -------------------------------------------------------------------
        // the scene
        // -------------------------------------------------------------------

        [Test]
        public void The_camp_scene_exists_is_in_the_build_list_and_is_reachable_from_the_boot_allow_list()
        {
            const string scenePath = "Assets/Main/Scenes/Camp.unity";

            Assert.That(
                File.Exists(Path.Combine(RepositoryRoot(), "UnityProject", scenePath)),
                Is.True,
                "the Camp scene is missing: run ProjectSurvive/Stage 09/5. Create Camp scene");

            var inBuildList = false;
            foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
            {
                inBuildList |= string.Equals(scene.path, scenePath, StringComparison.Ordinal) && scene.enabled;
            }

            Assert.That(inBuildList, Is.True, scenePath + " is not enabled in the build settings");

            // `?scene=Camp` is how a browser test reaches the camp, and Boot's allow-list is the only
            // thing that lets it. Read as text on purpose: the field is a serialized array on a
            // component, and opening the Boot scene from a test would change what other tests see.
            string boot = File.ReadAllText(
                Path.Combine(RepositoryRoot(), "UnityProject", "Assets", "Main", "Scenes", "Boot.unity"));

            Assert.That(
                boot.Contains("- Camp"),
                Is.True,
                "Boot's _urlSceneOverrides does not list Camp, so ?scene=Camp would load the Title screen instead");
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Stage 09l: with the rule as the data ships it (<c>AdjacentPair</c>, tags ตำรา/อาวุธ), a
        /// touching tome–weapon pair is a working combo <b>and</b> draws exactly one line — the state
        /// Stage 09k report §5 handed to this stage.
        /// </summary>
        [Test]
        public void With_the_shipped_touching_rule_the_pair_draws_exactly_one_line()
        {
            Screen ui = CampWithATome(out RunSession run, out _, out PieceRef weapon, out PieceRef tome);

            Assert.That(MoveTo(run, tome, 0, 1), Is.True, "the tome could not be placed beside the weapon");
            ui.Presenter.Refresh();

            SynergyEvaluation layout = new SynergyEvaluator(_config).Evaluate(run.Backpack);
            bool pairIsActive = false;
            for (int i = 0; i < layout.Active.Count; i++)
            {
                ActiveSynergy synergy = layout.Active[i];
                if (synergy.RuleId == "synergy.tome_row_damage"
                    && synergy.SourceInstanceId == tome.InstanceId
                    && synergy.TargetInstanceId == weapon.InstanceId)
                {
                    pairIsActive = true;
                }
            }

            Assert.That(pairIsActive, Is.True, "Core does not report the touching pair, so the rule is not a touch rule");

            CampLink[] links = ui.Presenter.Bag.Links;

            Assert.That(
                HasLineBetween(links, tome.InstanceId, weapon.InstanceId), Is.True,
                "the shipped touching rule draws no line for the pair it reports: " + Links(ui));
            Assert.That(
                CountLines(links), Is.EqualTo(1),
                "one touching pair is one line, and the screen drew " + CountLines(links) + ": " + Links(ui));

            Assert.That(
                HasBadgeFromBag(links, run.Backpack.Bags[0].InstanceId), Is.True,
                "the bag-area rule is a badge and must keep its mark: " + Links(ui));
        }

        /// <summary>
        /// The first seed with a camp that offers a piece carrying <paramref name="tag"/>, and the
        /// slot it sits in.
        /// </summary>
        /// <remarks>
        /// Searched rather than pinned: the shop roll is weighted by floor band, so a pinned seed
        /// whose offer happens to be the piece would be a second copy of that band to keep in step
        /// with <c>data/</c>. The walk itself is deterministic, so the test is too.
        /// </remarks>
        private uint SeedOffering(string tag, out int slot)
        {
            for (uint seed = 1u; seed <= 400u; seed++)
            {
                var run = new RunSession(_config, _codec, seed);
                if (run.StartRun().IsFailure)
                {
                    continue;
                }

                while (run.FlowState == RunFlowState.Camp)
                {
                    int offered = SlotOfferingTag(run, tag);
                    if (offered >= 0)
                    {
                        slot = offered;
                        return seed;
                    }

                    if (!WinOneFloor(run))
                    {
                        break;
                    }
                }
            }

            slot = -1;
            return 0u;
        }

        /// <summary>The first slot whose offer carries <paramref name="tag"/>, or -1.</summary>
        private int SlotOfferingTag(RunSession run, string tag)
        {
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                if (run.Stock[i].IsEmpty
                    || !_config.TryGetItem(run.Stock[i].OfferedId, out ItemConfig item))
                {
                    continue;
                }

                for (int t = 0; t < item.Tags.Count; t++)
                {
                    if (string.Equals(item.Tags[t], tag, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// Plays one floor to a win so the run lands in its next camp.
        /// </summary>
        /// <remarks>
        /// The result is the run's own input type, filled with a winning outcome and a purse the test
        /// chooses: the command, the payout, the heal and the next camp are all the run's, so a camp
        /// reached this way is a real camp and not a synthesised one.
        /// </remarks>
        private static bool WinOneFloor(RunSession run)
        {
            if (run.StartFloor().IsFailure)
            {
                return false;
            }

            var result = new BattleResult
            {
                Outcome = BattlePhase.Won,
                EndReason = BattleEndReason.SurvivedAllWaves,
                SurvivedTicks = 0,
                FinalHp = run.Hp,
                CurrencyEarned = 200,
                Kills = 0,
            };

            return run.ApplyBattleResult(result).IsSuccess && run.FlowState == RunFlowState.Camp;
        }

        /// <summary>The first piece the screen draws on the board.</summary>
        private static CampPieceModel FirstBoardItem(CampBagScreen bag)
        {
            for (int i = 0; i < bag.Pieces.Length; i++)
            {
                if (bag.Pieces[i].IsOnBoard && !bag.Pieces[i].IsBag)
                {
                    return bag.Pieces[i];
                }
            }

            return default;
        }

        /// <summary>One piece of the drawn model, or a default value when it is not on the board.</summary>
        private static CampPieceModel PieceOf(CampBagScreen bag, PieceRef piece)
        {
            for (int i = 0; i < bag.Pieces.Length; i++)
            {
                if (bag.Pieces[i].Piece == piece)
                {
                    return bag.Pieces[i];
                }
            }

            return default;
        }

        /// <summary>True when an instance id belongs to one of the run's bags.</summary>
        private static bool IsBag(RunSession run, int instanceId)
        {
            IReadOnlyList<BagPieceInstance> bags = run.Backpack.Bags;
            for (int i = 0; i < bags.Count; i++)
            {
                if (bags[i].InstanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a piece placed with its top-left at <paramref name="origin"/> covers a cell in one of <paramref name="other"/>'s rows.</summary>
        private static bool SharesARow(CampPieceModel piece, CellOffset origin, CampPieceModel other)
        {
            int height = piece.Shape == null ? 1 : piece.Shape.Height;
            int otherHeight = other.Shape == null ? 1 : other.Shape.Height;

            for (int row = origin.Y; row < origin.Y + height; row++)
            {
                for (int there = other.Origin.Y; there < other.Origin.Y + otherHeight; there++)
                {
                    if (row == there)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Drops the piece on a cell that shares a row with the weapon, so the same-row rule fires.</summary>
        private static CellOffset PlaceSharingARow(
            CampPresenter presenter, PieceRef piece, CampPieceModel weapon, out bool rowsOverlap)
        {
            rowsOverlap = false;

            CampBagScreen bag = presenter.Bag;
            CampPieceModel model = PieceOf(bag, piece);

            presenter.DragStart(piece, Rotation.Degrees0);

            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    var cell = new CellOffset(column, row);
                    presenter.DragMove(BagDropZone.Board, cell);

                    if (!presenter.Drag.IsValid || presenter.Drag.Action != BagDropAction.Place
                        || !SharesARow(model, cell, weapon))
                    {
                        continue;
                    }

                    rowsOverlap = true;
                    presenter.Drop();
                    return cell;
                }
            }

            presenter.CancelDrag();
            return new CellOffset(-1, -1);
        }

        /// <summary>
        /// The same scan as <see cref="PlaceSharingARow"/>, but the placement also has to <b>touch</b>
        /// the weapon: since Stage 09j a line is drawn only for a pair that is both on one row and
        /// edge-adjacent, so the fixture has to arrange that pair.
        /// </summary>
        private static CellOffset PlaceTouchingTheWeapon(
            CampPresenter presenter, PieceRef piece, CampPieceModel weapon, out bool rowsOverlap)
        {
            rowsOverlap = false;

            CampBagScreen bag = presenter.Bag;
            CampPieceModel model = PieceOf(bag, piece);

            presenter.DragStart(piece, Rotation.Degrees0);

            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    var cell = new CellOffset(column, row);
                    presenter.DragMove(BagDropZone.Board, cell);

                    if (!presenter.Drag.IsValid || presenter.Drag.Action != BagDropAction.Place
                        || !SharesARow(model, cell, weapon)
                        || !Touches(model, cell, weapon))
                    {
                        continue;
                    }

                    rowsOverlap = true;
                    presenter.Drop();
                    return cell;
                }
            }

            presenter.CancelDrag();
            return new CellOffset(-1, -1);
        }

        /// <summary>True when the piece placed at <paramref name="origin"/> would touch the weapon along an edge.</summary>
        private static bool Touches(CampPieceModel piece, CellOffset origin, CampPieceModel other)
        {
            int width = piece.Shape == null ? 1 : piece.Shape.Width;
            int height = piece.Shape == null ? 1 : piece.Shape.Height;
            int otherWidth = other.Shape == null ? 1 : other.Shape.Width;
            int otherHeight = other.Shape == null ? 1 : other.Shape.Height;

            int left = origin.X;
            int top = origin.Y;
            int right = left + width - 1;
            int bottom = top + height - 1;
            int otherLeft = other.Origin.X;
            int otherTop = other.Origin.Y;
            int otherRight = otherLeft + otherWidth - 1;
            int otherBottom = otherTop + otherHeight - 1;

            bool sideBySide = (right + 1 == otherLeft || otherRight + 1 == left)
                && top <= otherBottom && otherTop <= bottom;
            bool stacked = (bottom + 1 == otherTop || otherBottom + 1 == top)
                && left <= otherRight && otherLeft <= right;

            return sideBySide || stacked;
        }

        /// <summary>
        /// The opposite: drops the piece where it does <b>not touch</b> the weapon (Stage 09l).
        /// </summary>
        /// <remarks>
        /// Since ADR 0031 the condition is contact on an edge, not a shared row: a 2×2 tome whose rows
        /// sit above the weapon's is a working pair while it touches, so "shares no row" would no longer
        /// be "not a pair".
        /// </remarks>
        private static CellOffset PlaceAwayFromTheWeapon(
            CampPresenter presenter, PieceRef piece, CampPieceModel weapon, out bool found)
        {
            found = false;

            CampBagScreen bag = presenter.Bag;
            CampPieceModel model = PieceOf(bag, piece);

            presenter.DragStart(piece, Rotation.Degrees0);

            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    var cell = new CellOffset(column, row);
                    presenter.DragMove(BagDropZone.Board, cell);

                    if (!presenter.Drag.IsValid || presenter.Drag.Action != BagDropAction.Place
                        || Touches(model, cell, weapon))
                    {
                        continue;
                    }

                    found = true;
                    presenter.Drop();
                    return cell;
                }
            }

            presenter.CancelDrag();
            return new CellOffset(-1, -1);
        }

        // -------------------------------------------------------------------
        // the shelf offer's tooltip (Stage 09n)
        // -------------------------------------------------------------------

        /// <summary>
        /// Stage 09n: hovering a shelf fills the tooltip from the run's own offer readout — the same
        /// numbers a tier-1 copy in the chest resolves to, the price the shelf asks, and the combos the
        /// piece could join.
        /// </summary>
        [Test]
        public void A_hovered_shelf_fills_the_tooltip_from_the_runs_offer_readout()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            int checkedSlots = 0;

            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                if (run.Stock[slot].IsEmpty)
                {
                    continue;
                }

                Result<OfferReadout> explained = run.ExplainOffer(slot);
                Assert.That(explained.IsSuccess, Is.True, explained.Error.ToString());
                OfferReadout offer = explained.Value;

                ui.Presenter.HoverOffer(slot);

                Assert.That(ui.Presenter.HoveredOffer, Is.EqualTo(slot), "the presenter follows the shelf under the pointer");
                Assert.That(ui.State.TooltipVisible.Value, Is.True, "a shelf with an offer explains itself");
                Assert.That(ui.State.TooltipTitle.Value, Is.EqualTo(Text(offer.NameKey)), "the name is the piece's localized one");
                Assert.That(
                    ui.State.TooltipTier.Value,
                    Is.EqualTo(Format("camp.tooltip.tier.format", offer.Tier, _config.Constant.MaxTier)));

                // The price line carries the shelf's price and the verdict the run would give.
                string verdict = Text(
                    offer.Rentable ? "camp.tooltip.offer.rentable" : "error." + offer.RefusalCode);
                Assert.That(
                    ui.State.TooltipPrice.Value,
                    Is.EqualTo(Format("camp.tooltip.offer.price.format", offer.Price) + " · " + verdict),
                    "the price line is the shelf's own price and the rent verdict");

                // The numbers are the read model's, printed through the tooltip's own rows.
                if (offer.Piece.IsBag)
                {
                    Assert.That(_config.TryGetBag(offer.DefId, out BagConfig bag), Is.True);
                    Assert.That(
                        ui.State.TooltipStatus.Value,
                        Is.EqualTo(Format("camp.tooltip.slots.format", bag.Footprint.CellCount)),
                        "a bag shows its slots and no damage");
                    Assert.That(
                        ui.State.TooltipDescription.Value,
                        Is.EqualTo(Format("camp.tooltip.offer.weight.format", offer.Weight)));
                }
                else
                {
                    Assert.That(_config.TryGetItem(offer.DefId, out ItemConfig item), Is.True);

                    if (offer.Piece.Casts)
                    {
                        string resolved = Format(
                            "camp.tooltip.stat.format",
                            offer.Piece.Resolved.Damage,
                            offer.Piece.Resolved.CooldownSeconds,
                            offer.Piece.Resolved.MpCost);
                        string baseRow = Format(
                            "camp.tooltip.base.format",
                            Format(
                                "camp.tooltip.stat.format",
                                offer.Piece.Base.Damage,
                                offer.Piece.Base.CooldownSeconds,
                                offer.Piece.Base.MpCost));

                        StringAssert.StartsWith(resolved, ui.State.TooltipStatus.Value);
                        StringAssert.Contains(baseRow, ui.State.TooltipStatus.Value);
                    }
                    else
                    {
                        StringAssert.StartsWith(
                            Text(CategoryKeyOf(item.Category)), ui.State.TooltipStatus.Value);
                    }

                    Assert.That(
                        ui.State.TooltipDescription.Value,
                        Is.EqualTo(offer.DescriptionKey.Length == 0 ? string.Empty : Text(offer.DescriptionKey)));
                }

                Assert.That(ui.State.TooltipUpgrades.Value, Is.Empty, "an offer has no merge steps yet");

                // The combos line is the listing the read model produced, not a second calculation.
                if (offer.Combos.Count == 0)
                {
                    Assert.That(ui.State.TooltipCombos.Value, Is.EqualTo(Text("camp.tooltip.offer.combos.none")));
                }
                else
                {
                    StringAssert.StartsWith(Text("camp.tooltip.offer.combos.header"), ui.State.TooltipCombos.Value);
                    Assert.That(
                        ui.State.TooltipCombos.Value.Split('\n').Length,
                        Is.EqualTo(offer.Combos.Count + 1),
                        "one row per rule, under the header");
                    StringAssert.Contains(Text(offer.Combos[0].NameKey), ui.State.TooltipCombos.Value);
                }

                checkedSlots++;
            }

            Assert.That(checkedSlots, Is.GreaterThan(0), "the first camp always offers something to explain");
        }

        /// <summary>Stage 09n: leaving the shelf takes the tooltip with it.</summary>
        [Test]
        public void Leaving_a_shelf_closes_the_tooltip()
        {
            Screen ui = ScreenAtFirstCamp();
            int slot = FirstAvailableSlot(ui.Run);

            ui.Presenter.HoverOffer(slot);
            Assert.That(ui.State.TooltipVisible.Value, Is.True);

            ui.Presenter.HoverOffer(-1);

            Assert.That(ui.Presenter.HoveredOffer, Is.EqualTo(-1));
            Assert.That(ui.State.TooltipVisible.Value, Is.False, "nothing under the pointer means no tooltip");
        }

        /// <summary>
        /// Stage 09n: renting the shelf under the pointer empties it, so the tooltip that described it
        /// must go in the same refresh rather than keep quoting a price nobody can pay again.
        /// </summary>
        [Test]
        public void Renting_the_hovered_shelf_takes_its_tooltip_away()
        {
            Screen ui = ScreenAtFirstCamp();
            int slot = FirstAvailableSlot(ui.Run);

            ui.Presenter.HoverOffer(slot);
            Assert.That(ui.State.TooltipVisible.Value, Is.True);

            ui.Presenter.Rent(slot);

            Assert.That(ui.Presenter.Counters.Rented, Is.EqualTo(1), "the rent went through the screen");
            Assert.That(ui.Run.Stock[slot].IsPurchased, Is.True);
            Assert.That(ui.State.TooltipVisible.Value, Is.False, "a rented shelf has nothing left to explain");
            Assert.That(ui.State.FocusedShelfDetail.Value, Is.Empty);
        }

        /// <summary>Stage 09n: the upgrade chooser is the screen while it is up, so no shelf tooltip.</summary>
        [Test]
        public void The_merge_chooser_suppresses_the_shelf_tooltip()
        {
            Screen ui = AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin);
            int slot = FirstAvailableSlot(ui.Run);

            ui.Presenter.HoverOffer(slot);
            Assert.That(ui.State.TooltipVisible.Value, Is.True);

            DropOnto(ui.Presenter, ui.Run, twin, survivor);
            Assert.That(ui.Presenter.IsMergeChooserOpen, Is.True);

            // A pointer move that lands on another shelf while the modal is up must not draw it.
            ui.Presenter.HoverOffer(slot == 0 ? 1 : 0);
            Assert.That(ui.State.TooltipVisible.Value, Is.False, "the modal is the screen");
        }

        /// <summary>Stage 09n: a piece in the air is what the pointer means, not the shelf under it.</summary>
        [Test]
        public void A_piece_in_the_air_suppresses_the_shelf_tooltip()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;
            int slot = FirstAvailableSlot(run);

            ui.Presenter.HoverOffer(slot);
            Assert.That(ui.State.TooltipVisible.Value, Is.True);

            PieceRef piece = run.Backpack.Items[0].Ref;
            ui.Presenter.DragStart(piece, RotationOf(run, piece));
            ui.Presenter.DragMove(BagDropZone.Board, new CellOffset(0, 0));

            Assert.That(ui.Presenter.IsDragging, Is.True);
            Assert.That(ui.State.TooltipVisible.Value, Is.False, "a drag hides the shelf tooltip");

            ui.Presenter.HoverOffer(slot == 0 ? 1 : 0);
            Assert.That(ui.State.TooltipVisible.Value, Is.False, "and it stays hidden while the piece is in the air");

            ui.Presenter.CancelDrag();
            Assert.That(ui.State.TooltipVisible.Value, Is.True, "putting the piece down brings the tooltip back");
        }

        /// <summary>
        /// Stage 09n: the shelf the player is <b>standing</b> at shows the same readout on its own
        /// board, so walking up to a station answers the question hovering it does.
        /// </summary>
        [Test]
        public void The_shelf_the_player_stands_at_shows_the_same_readout()
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;
            int slot = FirstAvailableSlot(run);

            Result<OfferReadout> explained = run.ExplainOffer(slot);
            Assert.That(explained.IsSuccess, Is.True, explained.Error.ToString());
            OfferReadout offer = explained.Value;

            ui.Presenter.FocusOffer(slot);

            Assert.That(ui.Presenter.FocusedOffer, Is.EqualTo(slot));
            StringAssert.StartsWith(
                Format("camp.tooltip.offer.price.format", offer.Price), ui.State.FocusedShelfDetail.Value);

            if (offer.Piece.IsBag)
            {
                StringAssert.Contains(
                    Format("camp.tooltip.offer.weight.format", offer.Weight),
                    ui.State.FocusedShelfDetail.Value);
            }
            else if (offer.Piece.Casts)
            {
                StringAssert.Contains(
                    Format(
                        "camp.tooltip.stat.format",
                        offer.Piece.Resolved.Damage,
                        offer.Piece.Resolved.CooldownSeconds,
                        offer.Piece.Resolved.MpCost),
                    ui.State.FocusedShelfDetail.Value);
            }

            ui.Presenter.FocusOffer(-1);
            Assert.That(ui.State.FocusedShelfDetail.Value, Is.Empty, "walking away leaves no detail behind");
        }

        /// <summary>The first shelf with something on it, or -1 (the first camp always has four).</summary>
        private static int FirstAvailableSlot(RunSession run) =>
            RunFixtureSlots(run, slot => !run.Stock[slot].IsEmpty && !run.Stock[slot].IsPurchased);

        private static int RunFixtureSlots(RunSession run, Func<int, bool> predicate)
        {
            for (int slot = 0; slot < run.Stock.SlotCount; slot++)
            {
                if (predicate(slot))
                {
                    return slot;
                }
            }

            Assert.Fail("the camp has no shelf to hover");
            return -1;
        }

        /// <summary>The localization key of an item's category, as the tooltip prints it.</summary>
        private static string CategoryKeyOf(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.ArcaneWeapon:
                    return "camp.category.arcane_weapon";
                case ItemCategory.Charm:
                    return "camp.category.charm";
                case ItemCategory.SpellTome:
                    return "camp.category.tome";
                case ItemCategory.Offering:
                    return "camp.category.offering";
                case ItemCategory.Familiar:
                    return "camp.category.familiar";
                default:
                    return "camp.category.unknown";
            }
        }

        // -------------------------------------------------------------------
        // helpers for the upgrade chooser (Stage 09g)
        // -------------------------------------------------------------------

        /// <summary>
        /// A camp whose board holds the starting weapon and whose chest holds a second copy of it:
        /// the only pair the shipped shop can make reachable at this seed, and one with upgrade
        /// choices, because an arcane weapon matches the four <c>ArcaneWeapon</c> variants at the
        /// 1 → 2 step.
        /// </summary>
        private Screen AtACampWithAMergeablePair(out PieceRef survivor, out PieceRef twin)
        {
            Screen ui = ScreenAtFirstCamp();
            RunSession run = ui.Run;

            survivor = run.Backpack.Items[0].Ref;

            int slot = SlotOffering(run, RunDefaults.InitialWeaponId);
            Assert.That(
                slot,
                Is.GreaterThanOrEqualTo(0),
                "seed " + Seed + " must offer " + RunDefaults.InitialWeaponId + " for a merge; re-pin the seed");

            ui.Presenter.Rent(slot);
            Assert.That(ui.Presenter.Counters.Rented, Is.EqualTo(1), "the rent went through the screen");

            twin = OnlyChestItem(run);
            Assert.That(twin.IsValid, Is.True, "a rented piece is carried in the chest");
            return ui;
        }

        /// <summary>
        /// The merge gesture through the screen's own surface: pick the piece up, hover the piece it
        /// matches, release. Asserts the gesture is a merge on the piece under the pointer before the
        /// drop, so a chooser that failed to open could not be blamed on the drop going elsewhere.
        /// </summary>
        private static void DropOnto(CampPresenter presenter, RunSession run, PieceRef piece, PieceRef target)
        {
            presenter.DragStart(piece, RotationOf(run, piece));
            presenter.DragMove(BagDropZone.Board, OriginOf(run, target));

            Assert.That(presenter.Drag.IsValid, Is.True, "the gesture is legal");
            Assert.That(presenter.Drag.Action, Is.EqualTo(BagDropAction.Merge), "the gesture is the merge");
            Assert.That(presenter.Drag.Target, Is.EqualTo(target), "and it names the piece under the pointer");

            presenter.Drop();
        }

        /// <summary>The variant ids of the cards on screen, in the order they are shown.</summary>
        private static string[] CardIds(CampPresenter presenter)
        {
            CampMergeChooserModel chooser = presenter.MergeChooser;
            var ids = new string[chooser.ChoiceCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = chooser.Choices[i].VariantId;
            }

            return ids;
        }

        /// <summary>The step history of a piece: which upgrade each completed merge step kept.</summary>
        private static IReadOnlyList<string> VariantSteps(RunSession run, PieceRef piece)
        {
            Assert.That(
                run.Backpack.TryGetPiece(piece, out PieceInstance? instance) && instance is ItemInstance,
                Is.True,
                "the piece exists and is an item");
            return ((ItemInstance)instance!).VariantIds;
        }

        /// <summary>The tier of a piece ("ระดับ" on screen).</summary>
        private static int TierOf(RunSession run, PieceRef piece) =>
            run.Backpack.TryGetPiece(piece, out PieceInstance? instance) && instance is not null
                ? instance.Tier
                : -1;

        /// <summary>The item row of a piece, asserted to exist.</summary>
        private ItemConfig ItemOf(RunSession run, PieceRef piece)
        {
            Assert.That(
                run.Backpack.TryGetPiece(piece, out PieceInstance? instance) && instance is not null,
                Is.True,
                "the piece exists");
            Assert.That(
                _config.TryGetItem(instance!.DefId, out ItemConfig item),
                Is.True,
                "item " + instance.DefId + " has a row");
            return item;
        }

        /// <summary>The variant row of a card, asserted to exist.</summary>
        private UpgradeVariantConfig VariantOf(string variantId)
        {
            Assert.That(
                _config.TryGetUpgradeVariant(variantId, out UpgradeVariantConfig variant),
                Is.True,
                "variant " + variantId + " has a row");
            return variant;
        }

        /// <summary>
        /// The localization key of a stat's name — the vocabulary a card's line is built from, spelled
        /// here so a rename in the screen has to break this test rather than pass silently.
        /// </summary>
        private static string StatKey(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Damage: return "upgrade.stat.damage";
                case UpgradeStat.Cooldown: return "upgrade.stat.cooldown";
                case UpgradeStat.MpCost: return "upgrade.stat.mp_cost";
                case UpgradeStat.ProjectileSpeed: return "upgrade.stat.projectile_speed";
                case UpgradeStat.Range: return "upgrade.stat.range";
                case UpgradeStat.PierceCount: return "upgrade.stat.pierce_count";
                case UpgradeStat.ProjectileCount: return "upgrade.stat.projectile_count";
                case UpgradeStat.MaxHp: return "upgrade.stat.max_hp";
                case UpgradeStat.MaxMp: return "upgrade.stat.max_mp";
                case UpgradeStat.MoveSpeed: return "upgrade.stat.move_speed";
                case UpgradeStat.PickupRadius: return "upgrade.stat.pickup_radius";
                case UpgradeStat.FocusPerSecond: return "upgrade.stat.focus_per_second";
                default: return "upgrade.stat.unknown";
            }
        }

        /// <summary>The amount a modifier shows, sign included: the data row, written the way a card writes it.</summary>
        private static string Amount(UpgradeModifierConfig modifier)
        {
            if (modifier.Method == UpgradeMethod.Percent)
            {
                return modifier.Magnitude.ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + "%";
            }

            return modifier.Method == UpgradeMethod.Multiply
                ? "×" + modifier.Magnitude.ToString("0.##", CultureInfo.InvariantCulture)
                : modifier.Magnitude.ToString("+0.##;-0.##", CultureInfo.InvariantCulture);
        }

        /// <summary>The battle loadout of a run, resolved by the same resolver the run itself uses.</summary>
        private CombatLoadoutSnapshot Loadout(RunSession run) =>
            new LoadoutResolver(_config, new SynergyEvaluator(_config)).Resolve(run.Backpack);

        /// <summary>The run's resolved explanation of a piece — the tooltip's only source (Stage 09h).</summary>
        private static PieceReadout Explain(RunSession run, PieceRef piece)
        {
            Result<PieceReadout> readout = run.ExplainPiece(piece);
            Assert.That(readout.IsSuccess, Is.True, "the run must be able to explain its own piece: " + readout.Error);
            return readout.Value;
        }

        /// <summary>
        /// The numbers of a one-weapon loadout as one comparable line. Used to prove that two runs
        /// which differ only in the card they picked resolve to different battle numbers.
        /// </summary>
        private static string WeaponLine(CombatLoadoutSnapshot loadout)
        {
            if (loadout.Weapons.Count == 0)
            {
                return "no weapon";
            }

            WeaponLoadout weapon = loadout.Weapons[0];
            return string.Join(
                " ",
                new List<string>(9)
                {
                    weapon.SkillId,
                    "t" + weapon.Tier,
                    "dmg" + weapon.Damage,
                    "mp" + weapon.MpCost,
                    "cd" + Number(weapon.CooldownSeconds),
                    "spd" + Number(weapon.ProjectileSpeed),
                    "range" + Number(weapon.Range),
                    "pierce" + weapon.PierceCount,
                    "count" + weapon.ProjectileCount,
                });
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        // -------------------------------------------------------------------
        // helpers for the line rule's adjacency condition (Stage 09j)
        // -------------------------------------------------------------------

        /// <summary>
        /// A camp whose รยาม holds a 2×2 tome and a weapon in the cell right of the tome's cells: the
        /// smallest layout in which the same-line rule can draw a line. The tome is added through the
        /// backpack's own command rather than won, because what is under test is the drawing.
        /// </summary>
        private Screen CampWithATome(
            out RunSession run, out PieceRef bag, out PieceRef weapon, out PieceRef tome)
        {
            Screen ui = ScreenAtFirstCamp();
            run = ui.Run;
            bag = run.Backpack.Bags[0].Ref;
            weapon = run.Backpack.Items[0].Ref;

            Assert.That(MoveTo(run, bag, 0, 1), Is.True, "the starting รยาม could not be moved to (0,1)");
            Assert.That(MoveTo(run, weapon, 2, 1), Is.True, "the weapon could not be placed at (2,1)");

            Result<PieceRef> added = run.Backpack.AddItem("item.spell_tome");
            Assert.That(added.IsSuccess, Is.True, "the run could not take a tome: " + added.Error);
            tome = added.Value;

            ui.Presenter.Refresh();
            return ui;
        }

        /// <summary>
        /// The run with room on every side of its tome (Stage 09l): four 3×3 รยาม in a square, and a
        /// 2×2 tome at (1,2) with a covered cell to its left, right, above and below.
        /// </summary>
        /// <remarks>
        /// Four bags because an item is only legal on a cell a bag covers: the tome's four cells and the
        /// four cells around it need eight covered cells, and one bag is 3×3.
        /// </remarks>
        private Screen CampWithRoomAroundTheTome(
            out RunSession run, out PieceRef weapon, out PieceRef tome)
        {
            Screen ui = ScreenAtFirstCamp();
            run = ui.Run;

            PieceRef first = run.Backpack.Bags[0].Ref;
            Assert.That(MoveTo(run, first, 0, 0), Is.True, "the starting รยาม could not be moved to (0,0)");

            var corners = new (int X, int Y)[] { (3, 0), (0, 3), (3, 3) };
            for (int i = 0; i < corners.Length; i++)
            {
                var added = run.Backpack.AddBag("bag.raw_cotton_bag");
                Assert.That(added.IsSuccess, Is.True, "a bags could not be added: " + added.Error);
                Assert.That(
                    MoveTo(run, added.Value, corners[i].X, corners[i].Y), Is.True,
                    "a รยาม could not be placed at (" + corners[i].X + "," + corners[i].Y + ")");
            }

            weapon = run.Backpack.Items[0].Ref;

            var addedTome = run.Backpack.AddItem("item.spell_tome");
            Assert.That(addedTome.IsSuccess, Is.True, "the run could not take a tome: " + addedTome.Error);
            tome = addedTome.Value;

            Assert.That(MoveTo(run, tome, 1, 2), Is.True, "the 2x2 tome could not be placed at (1,2)");

            // The weapon stands aside until the test says which side of the tome it goes.
            Assert.That(
                MoveTo(run, weapon, 1, 0), Is.True,
                "the weapon could not be put out of the tome's four cells");

            ui.Presenter.Refresh();
            return ui;
        }

        /// <summary>The run's own placement command, so a layout this suite builds is one the game allows.</summary>
        private static bool MoveTo(RunSession run, PieceRef piece, int x, int y) =>
            run.MovePiece(piece, new CellOffset(x, y), Rotation.Degrees0).IsSuccess;

        /// <summary>True when a line joins the two instances, in either direction.</summary>
        private static bool HasLineBetween(CampLink[] links, int first, int second)
        {
            return HasLine(links, first, second) || HasLine(links, second, first);
        }

        /// <summary>The marks the screen is drawing, as one line, for a failure message.</summary>
        private static string Links(Screen ui)
        {
            CampLink[] links = ui.Presenter.Bag.Links;
            if (links.Length == 0)
            {
                return "no links at all";
            }

            var written = new List<string>(links.Length);
            for (int i = 0; i < links.Length; i++)
            {
                written.Add(links[i].Style + ":" + links[i].SourceInstanceId + "->" + links[i].TargetInstanceId);
            }

            return string.Join(", ", written);
        }

        /// <summary>
        /// The screen and the run behind it, at the first camp of a fresh run.
        /// </summary>
        /// <summary>
        /// Stage 13c-1: the screen asks for a sound when a command <b>landed</b>, and for the refusal
        /// cue when one was turned down. A cue is UI-level state (it travels on
        /// <see cref="CampUiState.Cues"/>), so this is checked here, without a scene.
        /// </summary>
        [Test]
        public void The_screen_asks_for_a_sound_only_when_a_command_landed()
        {
            Screen ui = ScreenAtFirstCamp();
            var cues = new List<AudioCue>();
            using IDisposable subscription = R3.ObservableSubscribeExtensions.Subscribe(
                ui.State.Cues, cue => cues.Add(cue));

            int slot = SlotOffering(ui.Run, RunDefaults.InitialWeaponId);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "the pinned seed must offer the starting weapon");

            ui.Presenter.Rent(slot);
            Assert.That(cues, Is.EqualTo(new[] { AudioCue.Rent }), "a rent that landed asks for one rent cue");

            // A refused command is the refusal cue and nothing else: the run did not change, so
            // neither did the thing the sound is reporting.
            cues.Clear();
            ui.Presenter.Rent(-1);
            Assert.That(cues, Is.EqualTo(new[] { AudioCue.Refuse }), "a refusal is the only cue it asks for");

            // Turning a selected piece asks for the rotate cue.
            PieceRef onBoard = OnBoardItem(ui.Run);
            Assert.That(onBoard.IsValid, Is.True, "the starting kit carries an item on the board");
            cues.Clear();
            ui.Presenter.SelectOnly(onBoard);
            ui.Presenter.RotateSelected();
            Assert.That(cues, Is.EqualTo(new[] { AudioCue.Rotate }));
        }

        private Screen ScreenAtFirstCamp()
        {
            RunSession run = AtFirstCamp();
            var state = new CampUiState();
            var presenter = new CampPresenter(run, _config, _text, state);
            presenter.Begin();
            return new Screen(run, presenter, state);
        }

        private RunSession AtFirstCamp()
        {
            var run = new RunSession(_config, _codec, Seed);
            Result started = run.StartRun();
            Assert.That(started.IsSuccess, Is.True, "the run must start on the shipped tables: " + started.Error);
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp));
            return run;
        }

        private string Text(string key) => _text.Get(key);

        private string Format(string key, params object[] arguments) =>
            string.Format(CultureInfo.InvariantCulture, Text(key), arguments);

        /// <summary>The first shop slot whose offer is <paramref name="defId"/>, or -1.</summary>
        private static int SlotOffering(RunSession run, string defId)
        {
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                if (string.Equals(run.Stock[i].OfferedId, defId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The one item the run is carrying in its chest.</summary>
        /// <summary>The first item standing on the board, or <see cref="PieceRef.None"/>.</summary>
        private static PieceRef OnBoardItem(RunSession run)
        {
            IReadOnlyList<ItemInstance> items = run.Backpack.Items;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Zone == PieceZone.Board)
                {
                    return items[i].Ref;
                }
            }

            return PieceRef.None;
        }

        private static PieceRef OnlyChestItem(RunSession run)
        {
            IReadOnlyList<ItemInstance> items = run.Backpack.Items;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Zone != PieceZone.Board)
                {
                    return items[i].Ref;
                }
            }

            return PieceRef.None;
        }

        /// <summary>
        /// Starts dragging <paramref name="piece"/> when nothing is being dragged, then returns the
        /// first board cell the presenter's own preview accepts as a placement.
        /// </summary>
        private static CellOffset FirstPlaceableCell(CampPresenter presenter, PieceRef piece, out BagDropAction action) =>
            ScanForPlacement(presenter, piece, default, skipOrigin: false, out action);

        /// <summary>The first cell that would place the piece somewhere other than where it already is.</summary>
        private static CellOffset PlaceableCellOtherThan(CampPresenter presenter, PieceRef piece, CellOffset origin) =>
            ScanForPlacement(presenter, piece, origin, skipOrigin: true, out _);

        private static CellOffset ScanForPlacement(
            CampPresenter presenter, PieceRef piece, CellOffset origin, bool skipOrigin, out BagDropAction action)
        {
            action = BagDropAction.None;

            if (!presenter.IsDragging)
            {
                presenter.DragStart(piece, Rotation.Degrees0);
            }

            CampBagScreen bag = presenter.Bag;

            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    var cell = new CellOffset(column, row);

                    if (skipOrigin && cell == origin)
                    {
                        continue;
                    }

                    presenter.DragMove(BagDropZone.Board, cell);

                    if (presenter.Drag.IsValid && presenter.Drag.Action == BagDropAction.Place)
                    {
                        action = presenter.Drag.Action;
                        return cell;
                    }
                }
            }

            return new CellOffset(-1, -1);
        }

        private static CellOffset OriginOf(RunSession run, PieceRef piece) =>
            run.Backpack.TryGetPiece(piece, out PieceInstance? instance) && instance is not null
                ? instance.Origin
                : new CellOffset(-1, -1);

        private static Rotation RotationOf(RunSession run, PieceRef piece) =>
            run.Backpack.TryGetPiece(piece, out PieceInstance? instance) && instance is not null
                ? instance.Rotation
                : Rotation.Degrees0;

        /// <summary>
        /// The repository root, two folders above <c>Assets/</c> — the same anchor the Stage 08
        /// tests use. (Not the test binary's directory: under the Test Runner that is the Editor
        /// install, not the project.)
        /// </summary>
        private static string RepositoryRoot() =>
            Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", ".."));

        /// <summary>One screen: the run it edits, the logic under test and the state it publishes.</summary>
        private readonly struct Screen
        {
            public Screen(RunSession run, CampPresenter presenter, CampUiState state)
            {
                Run = run;
                Presenter = presenter;
                State = state;
            }

            public RunSession Run { get; }

            public CampPresenter Presenter { get; }

            public CampUiState State { get; }
        }
    }
}
