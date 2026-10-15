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
            // preview, and the preview is the run's own predicate.
            PieceRef survivor = run.Backpack.Items[0].Ref;

            ui.Presenter.DragStart(twin, RotationOf(run, twin));
            ui.Presenter.DragMove(BagDropZone.Board, OriginOf(run, survivor));

            Assert.That(ui.Presenter.Drag.Action, Is.EqualTo(BagDropAction.Merge), "dropping onto a matching piece merges");
            Assert.That(ui.Presenter.Drag.Target, Is.EqualTo(survivor), "the screen highlights the piece it would fold into");

            ui.Presenter.Drop();

            Assert.That(ui.Presenter.Counters.Merged, Is.EqualTo(1));
            Assert.That(run.Backpack.Items.Count, Is.EqualTo(1), "the merge consumed one of the two");
            Assert.That(run.Backpack.Items[0].Tier, Is.EqualTo(2), "the survivor went up a tier");
            Assert.That(core.MergeItems(twin, survivor).IsSuccess, Is.True, "the same merge issued directly is accepted");

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

            // Stage 09e item 2: five separate lines — ชื่อ, ระดับ, สถานะ, คำอธิบาย, คอมโบที่ทำงานอยู่ — so
            // the screen owns one label each. One combined string would be the shape this stage
            // replaced, and the two halves are asserted apart here.
            string tier = ui.State.TooltipTier.Value;
            Assert.That(tier, Does.Not.Contain("camp.tooltip"), "every key resolved");
            Assert.That(
                tier,
                Is.EqualTo(Format("camp.tooltip.tier.format", run.Backpack.Items[0].Tier, _config.Constant.MaxTier)),
                "the tier line is built from the surface's own format");

            string status = ui.State.TooltipStatus.Value;
            Assert.That(status, Does.Not.Contain("camp.tooltip"), "every key resolved");
            Assert.That(
                status,
                Is.EqualTo(Format("camp.tooltip.stat.format", skill.Damage, skill.CooldownSeconds, skill.MpCost)),
                "the weapon's skill numbers are the table's, formatted by the presenter");
            Assert.That(status, Does.Not.Contain(tier), "ระดับ is its own line, not a prefix of สถานะ");

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
        /// The stage's line rule: a line is drawn for a ตำรา–อาวุธอาคม pair on the same row, for
        /// nothing else, and it is gone the moment the pair stops qualifying.
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
            IGameConfig config = _config;
            SynergyConfig lineRule = SameLineRule(config, out bool found);

            Assert.That(
                found,
                Is.True,
                "the shipped tables have no same-line rule, so there is nothing for this test to check");

            uint seed = SeedOffering(lineRule.TagA, out int slot);
            Assert.That(slot, Is.GreaterThanOrEqualTo(0), "no seed in range offered a '" + lineRule.TagA + "' piece in any camp of its run");

            var run = new RunSession(config, _codec, seed);
            Assert.That(run.StartRun().IsSuccess, Is.True);

            // The tome is a mid-run offer (the shop's own floor band says so), so the walk to it is
            // made the way the game makes it: win the floors in front of it. Nothing synthesises a
            // camp, and no floor number is written into the test.
            int slotReached = 0;
            while (run.FlowState == RunFlowState.Camp && (slotReached = SlotOfferingTag(run, lineRule.TagA)) < 0)
            {
                Assert.That(WinOneFloor(run), Is.True, "the run could not reach the camp that offers a '" + lineRule.TagA + "' piece");
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

            // Place the tome on a cell that shares a row with the weapon: that is the rule's own
            // condition, so the line has to appear.
            CellOffset placed = PlaceSharingARow(presenter, tome, weapon, out bool rowsOverlap);

            Assert.That(rowsOverlap, Is.True, "the fixture could not put the tome on the weapon's row");
            Assert.That(placed.X, Is.GreaterThanOrEqualTo(0), "the bag has no free 2x2 block left for the tome");

            CampLink[] links = presenter.Bag.Links;
            Assert.That(links.Length, Is.GreaterThan(0), "the same-row rule is working but the screen draws nothing for it");

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

            Assert.That(lines, Is.GreaterThan(0), "the tome shares the weapon's row but no line was drawn for it");

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

            // Move the tome to a row the weapon is not on: the pair stops qualifying, so the line
            // has to be gone from the very next picture the screen publishes.
            CellOffset away = PlaceOffEveryRow(presenter, tome, weapon, out bool movedAway);
            Assert.That(movedAway, Is.True, "no free 2x2 block exists off the weapon's row, so the drop cannot be checked");
            Assert.That(away, Is.Not.EqualTo(placed), "the tome has to leave the weapon's row for the line to go");

            links = presenter.Bag.Links;
            for (int i = 0; i < links.Length; i++)
            {
                Assert.That(
                    links[i].Style, Is.Not.EqualTo(CampLinkStyle.Line),
                    "a line survived the pair going off the same row: " + links[i]);
            }
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

        /// <summary>The same-line rule the screen draws a line for, or false when the tables have none.</summary>
        private static SynergyConfig SameLineRule(IGameConfig config, out bool found)
        {
            for (int i = 0; i < config.Synergies.Count; i++)
            {
                SynergyKind kind = config.Synergies[i].Kind;
                if (kind == SynergyKind.SameRow || kind == SynergyKind.SameColumn)
                {
                    found = true;
                    return config.Synergies[i];
                }
            }

            found = false;
            return null;
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

        /// <summary>The opposite: drops the piece where it shares no row with the weapon.</summary>
        private static CellOffset PlaceOffEveryRow(
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
                        || SharesARow(model, cell, weapon))
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

        /// <summary>The screen and the run behind it, at the first camp of a fresh run.</summary>
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
