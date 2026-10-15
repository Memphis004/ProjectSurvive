using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Run
{
    /// <summary>
    /// Stage 09's drag preview: what a drop would do, answered without changing anything,
    /// and answered by the <b>same</b> rule the command it previews uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen can only be as honest as this surface. "preview วางได้และวางไม่ได้ โดยไม่แก้
    /// Domain จนผู้เล่นยืนยัน" is two claims — nothing moves, and the answer is the command's own
    /// answer — and each has a test here that fails if it stops being true. The commands
    /// themselves are covered by the Stage 05 suite; these cases are about the seam between a
    /// gesture and a rule.
    /// </para>
    /// <para>
    /// Everything runs over the real generated tables, so no balance number is repeated here
    /// (AGENTS rule 5).
    /// </para>
    /// </remarks>
    public sealed class BagDropPreviewTests
    {
        /// <summary>
        /// The claim the whole feature rests on: the preview is a dry run of the command, so a
        /// green preview is an accepted command and a refusal carries the code the command would
        /// have returned — for every cell, two rotations, and the starting weapon.
        /// </summary>
        /// <remarks>
        /// Each case starts from a fresh run, because a command that succeeds moves the state the
        /// next cell's preview would be computed against. The sweep is the whole board on purpose:
        /// a hand-picked cell cannot catch an off-by-one in the footprint's origin.
        /// </remarks>
        [Fact]
        public void The_preview_and_the_command_it_previews_always_agree()
        {
            IGameConfig config = RunFixture.Config();
            int width = config.Constant.BoardWidth;
            int height = config.Constant.BoardHeight;
            var rotations = new[] { Rotation.Degrees0, Rotation.Degrees90 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var origin = new CellOffset(x, y);

                    for (int r = 0; r < rotations.Length; r++)
                    {
                        RunSession run = RunFixture.StartedRun(config);
                        PieceRef weapon = RunFixture.StartingWeapon(run);
                        Rotation rotation = rotations[r];

                        // StartRun's own events are not this case's subject.
                        run.ClearEvents();

                        uint before = run.ComputeStateHash();
                        DropPreview preview = run.PreviewDrop(weapon, BagDropZone.Board, origin, rotation);

                        Assert.Equal(before, run.ComputeStateHash());
                        Assert.Empty(run.Events);

                        Result applied = Apply(run, weapon, preview, origin, rotation);

                        Assert.Equal(preview.IsValid, applied.IsSuccess);

                        if (!preview.IsValid)
                        {
                            Assert.Equal(preview.ErrorCode, applied.Error.Code);
                            Assert.Equal(before, run.ComputeStateHash());
                        }
                    }
                }
            }
        }

        /// <summary>Repeating a preview cannot accumulate anything: no hash move, no event.</summary>
        [Fact]
        public void A_thousand_previews_leave_the_run_byte_identical()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);
            PieceRef bag = RunFixture.StartingBag(run);
            int revision = run.Backpack.Revision;
            run.ClearEvents();

            uint before = run.ComputeStateHash();

            var zones = new[] { BagDropZone.Board, BagDropZone.Chest, BagDropZone.Altar, BagDropZone.None };
            for (int i = 0; i < 250; i++)
            {
                var cell = new CellOffset(i % config.Constant.BoardWidth, i % config.Constant.BoardHeight);
                for (int z = 0; z < zones.Length; z++)
                {
                    run.PreviewDrop(weapon, zones[z], cell, Rotation.Degrees0);
                    run.PreviewDrop(bag, zones[z], cell, Rotation.Degrees90);
                }
            }

            Assert.Equal(before, run.ComputeStateHash());
            Assert.Equal(revision, run.Backpack.Revision);
            Assert.Empty(run.Events);
        }

        /// <summary>A drop over nothing is refused with the code a command would carry.</summary>
        [Fact]
        public void A_release_outside_every_surface_is_refused_as_out_of_board()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            DropPreview preview = run.PreviewDrop(
                RunFixture.StartingWeapon(run), BagDropZone.None, new CellOffset(0, 0), Rotation.Degrees0);

            Assert.False(preview.IsValid);
            Assert.Equal(InventoryError.OutOfBoard, preview.ErrorCode);
            Assert.Equal(BagDropAction.None, preview.Action);
        }

        /// <summary>Over the board edge there is no legal cell, and the board says so.</summary>
        [Fact]
        public void A_drop_that_would_hang_off_the_board_is_refused_with_the_boards_own_code()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);

            DropPreview preview = run.PreviewDrop(
                weapon, BagDropZone.Board, new CellOffset(config.Constant.BoardWidth, 0), Rotation.Degrees0);

            Assert.False(preview.IsValid);
            Assert.Equal(InventoryError.OutOfBoard, preview.ErrorCode);
        }

        /// <summary>
        /// Dropping an item onto another item is the merge gesture ("วางทับเพื่อ merge"), so the
        /// preview names the instance under the pointer and the mismatch is refused without
        /// losing anything.
        /// </summary>
        [Fact]
        public void The_merge_gesture_names_the_piece_under_the_pointer_and_refuses_a_mismatch()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef first = RunFixture.StartingWeapon(run);
            var cell = new CellOffset(run.Backpack.Items[0].Origin.X, run.Backpack.Items[0].Origin.Y);

            Result<PieceRef> second = run.Backpack.AddItem(first.DefIdOf(run));
            Assert.True(second.IsSuccess, "a second copy of the starting weapon must be addable");

            DropPreview merge = run.PreviewDrop(second.Value, BagDropZone.Board, cell, Rotation.Degrees0);
            Assert.True(merge.IsValid, merge.ErrorCode);
            Assert.Equal(BagDropAction.Merge, merge.Action);
            Assert.Equal(first, merge.Target);

            // The real command agrees, and the tier rises.
            Result<MergeOutcome> merged = run.MergeItems(second.Value, first);
            Assert.True(merged.IsSuccess, merged.Error.ToString());
            Assert.Equal(2, merged.Value.NewTier);

            // A different item on the same cell is a mismatch, and nothing is lost.
            string otherId = OtherItemId(config, first.DefIdOf(run));
            Result<PieceRef> other = run.Backpack.AddItem(otherId);
            Assert.True(other.IsSuccess, "another item must be addable");

            int itemsBefore = run.Backpack.Items.Count;
            DropPreview mismatch = run.PreviewDrop(other.Value, BagDropZone.Board, cell, Rotation.Degrees0);

            Assert.False(mismatch.IsValid);
            Assert.Equal(InventoryError.MergeMismatch, mismatch.ErrorCode);
            Assert.Equal(itemsBefore, run.Backpack.Items.Count);
            Assert.Equal(InventoryError.MergeMismatch, run.MergeItems(other.Value, first).Error.Code);
        }

        /// <summary>
        /// The altar quotes a payout before the player commits ("ถวายคืน 50% ปัดลง"), and the drop
        /// that would strand an item is refused with the same code the command uses.
        /// </summary>
        [Fact]
        public void The_altar_quotes_its_payout_and_refuses_a_bag_that_would_strand_an_item()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);
            PieceRef bag = RunFixture.StartingBag(run);

            Assert.True(config.TryGetItem(weapon.DefIdOf(run), out ItemConfig weaponRow));

            DropPreview quote = run.PreviewDrop(weapon, BagDropZone.Altar, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.True(quote.IsValid, quote.ErrorCode);
            Assert.Equal(BagDropAction.Recycle, quote.Action);
            Assert.Equal(
                (weaponRow.Price * config.Economy.SellBackPercent) / 100,
                quote.Bia);

            // The bag still carries the weapon, so giving the bag back would strand it.
            DropPreview bagToAltar = run.PreviewDrop(bag, BagDropZone.Altar, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.False(bagToAltar.IsValid);
            Assert.Equal(InventoryError.ItemStranded, bagToAltar.ErrorCode);
            Assert.Equal(InventoryError.ItemStranded, run.Sell(bag).Error.Code);

            DropPreview bagToChest = run.PreviewDrop(bag, BagDropZone.Chest, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.False(bagToChest.IsValid);
            Assert.Equal(InventoryError.ItemStranded, bagToChest.ErrorCode);

            // And the weapon itself may be parked: the chest is empty and only items are on the bag.
            DropPreview weaponToChest = run.PreviewDrop(weapon, BagDropZone.Chest, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.True(weaponToChest.IsValid, weaponToChest.ErrorCode);
            Assert.Equal(BagDropAction.Transfer, weaponToChest.Action);
        }

        /// <summary>The altar pays exactly what the preview promised.</summary>
        [Fact]
        public void The_altar_pays_what_the_preview_promised()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);
            int biaBefore = run.Wallet.Bia;

            DropPreview preview = run.PreviewDrop(weapon, BagDropZone.Altar, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.True(preview.IsValid, preview.ErrorCode);

            Result<RecycleOutcome> sold = run.Sell(weapon);

            Assert.True(sold.IsSuccess, sold.Error.ToString());
            Assert.Equal(preview.Bia, sold.Value.PayoutBia);
            Assert.Equal(biaBefore + preview.Bia, run.Wallet.Bia);
        }

        /// <summary>A full chest refuses the drop and keeps the piece on the board.</summary>
        [Fact]
        public void A_full_chest_refuses_the_drop_and_keeps_the_piece()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);

            string fillerId = config.Items[0].Id;
            while (!run.Backpack.Chest.IsFull)
            {
                Result<PieceRef> stored = run.Backpack.AddItem(fillerId);
                Assert.True(stored.IsSuccess, "the chest must accept pieces until it is full");
            }

            Assert.Equal(run.ChestSlots, run.ChestUsed);

            DropPreview preview = run.PreviewDrop(weapon, BagDropZone.Chest, new CellOffset(0, 0), Rotation.Degrees0);

            Assert.False(preview.IsValid);
            Assert.Equal(InventoryError.ChestFull, preview.ErrorCode);
            Assert.Equal(PieceZone.Board, run.Backpack.Items[0].Zone);
            Assert.Equal(InventoryError.ChestFull, run.TransferToChest(weapon).Error.Code);
        }

        /// <summary>A locked bag refuses every drop, and says so with the bag's own code.</summary>
        [Fact]
        public void A_locked_bag_refuses_every_drop()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);

            Assert.True(run.Editor.SetLocked(true).IsSuccess);
            Assert.True(run.IsBagLocked);

            var zones = new[] { BagDropZone.Board, BagDropZone.Chest, BagDropZone.Altar, BagDropZone.None };
            for (int i = 0; i < zones.Length; i++)
            {
                DropPreview preview = run.PreviewDrop(weapon, zones[i], new CellOffset(1, 1), Rotation.Degrees0);
                Assert.False(preview.IsValid, zones[i].ToString());
                Assert.Equal(InventoryError.Locked, preview.ErrorCode);
            }

            Assert.False(run.MovePiece(weapon, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            Assert.True(run.Editor.SetLocked(false).IsSuccess);
        }

        /// <summary>
        /// The bottom bar's numbers come from the run, not from the screen: weight and its
        /// movement penalty, the chest's occupancy, the re-roll price and สมาธิ.
        /// </summary>
        [Fact]
        public void The_read_model_reports_the_bars_numbers_from_the_tables()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);

            Assert.Equal(config.Economy.ChestSlots, run.ChestSlots);
            Assert.Equal(0, run.ChestUsed);
            Assert.Equal(config.Economy.RefreshBaseCost, run.RefreshPrice);
            Assert.Equal(config.Constant.FocusPerSecond, run.FocusPerSecond);
            Assert.Equal(run.Backpack.BoardWeight, run.BagWeight);
            Assert.Equal(WeightPenalty.Percent(run.BagWeight, config.Constant), run.BagWeightPenaltyPercent);
            Assert.True(run.DepartCheck().IsSuccess, "the starting kit must be able to depart");

            // A re-roll in this camp climbs the ladder the table describes.
            Assert.True(run.RefreshShop().IsSuccess);
            Assert.Equal(
                config.Economy.RefreshBaseCost + config.Economy.RefreshCostIncrement,
                run.RefreshPrice);

            // A chest piece weighs nothing, so parking the weapon lightens the bag.
            float before = run.BagWeight;
            Assert.True(run.TransferToChest(RunFixture.StartingWeapon(run)).IsSuccess);
            Assert.True(run.BagWeight < before, "a chest piece must not be carried");
            Assert.Equal(1, run.ChestUsed);
        }

        /// <summary>The depart gate answers with the reason the button shows.</summary>
        [Fact]
        public void The_depart_check_reports_the_missing_weapon_reason_before_the_button_is_pressed()
        {
            IGameConfig config = RunFixture.Config();
            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);

            Assert.True(run.DepartCheck().IsSuccess);
            Assert.True(run.Sell(weapon).IsSuccess);

            Result check = run.DepartCheck();
            Assert.True(check.IsFailure);
            Assert.Equal(RunError.NoArcaneWeapon, check.Error.Code);

            // The gate and the command agree, and the run is still in Camp.
            Assert.Equal(RunError.NoArcaneWeapon, run.StartFloor().Error.Code);
            Assert.Equal(RunFlowState.Camp, run.FlowState);
        }

        /// <summary>
        /// The combos a tooltip lists are the layout's own, and leaving a bag's area turns them off
        /// again — the same numbers the battle is handed at departure.
        /// </summary>
        [Fact]
        public void The_layouts_active_combos_are_visible_and_move_with_the_layout()
        {
            IGameConfig config = RunFixture.Config();
            RequireHasBagAreaRule(config);

            RunSession run = RunFixture.StartedRun(config);
            PieceRef weapon = RunFixture.StartingWeapon(run);
            PieceRef bag = RunFixture.StartingBag(run);

            SynergyEvaluation onBag = run.EvaluateLayout();
            Assert.True(onBag.Active.Count > 0, "an item sitting on a bag must fire the bag-area rule");
            Assert.True(onBag.For(weapon.InstanceId).ActiveSynergyCount > 0);
            Assert.Equal(bag.InstanceId, onBag.Active[0].SourceInstanceId);

            // Parked in the chest the piece is off the board, so nothing connects it.
            Assert.True(run.TransferToChest(weapon).IsSuccess);

            SynergyEvaluation parked = run.EvaluateLayout();
            Assert.Equal(0, parked.For(weapon.InstanceId).ActiveSynergyCount);
            Assert.Empty(parked.Active);
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        /// <summary>The command a preview describes, so a case can apply exactly what it promised.</summary>
        private static Result Apply(RunSession run, PieceRef piece, in DropPreview preview, CellOffset origin, Rotation rotation)
        {
            switch (preview.Action)
            {
                case BagDropAction.Place:
                    return run.MovePiece(piece, origin, rotation);

                case BagDropAction.Merge:
                    Result<MergeOutcome> merged = run.MergeItems(piece, preview.Target);
                    return merged.IsSuccess ? Result.Ok() : Result.Fail(merged.Error.Code);

                case BagDropAction.Transfer:
                    return run.TransferToChest(piece);

                case BagDropAction.Recycle:
                    Result<RecycleOutcome> sold = run.Sell(piece);
                    return sold.IsSuccess ? Result.Ok() : Result.Fail(sold.Error.Code);

                default:
                    // A refusal previews no command: the case asserts on the code instead.
                    return Result.Fail(preview.ErrorCode);
            }
        }

        /// <summary>A valid item id that is not <paramref name="defId"/>.</summary>
        private static string OtherItemId(IGameConfig config, string defId)
        {
            for (int i = 0; i < config.Items.Count; i++)
            {
                if (!string.Equals(config.Items[i].Id, defId, StringComparison.Ordinal))
                {
                    return config.Items[i].Id;
                }
            }

            throw new InvalidOperationException("the item table must hold more than one row.");
        }

        private static void RequireHasBagAreaRule(IGameConfig config)
        {
            IReadOnlyList<SynergyConfig> rules = config.Synergies;
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].Kind == SynergyKind.BagArea)
                {
                    return;
                }
            }

            throw new InvalidOperationException("the shipped synergy table must hold a BagArea rule.");
        }
    }

    /// <summary>Small conveniences that keep the cases readable.</summary>
    internal static class BagDropTestExtensions
    {
        /// <summary>The permanent data id of a piece the run holds.</summary>
        internal static string DefIdOf(this PieceRef piece, RunSession run) =>
            run.Backpack.TryGetPiece(piece, out PieceInstance? instance) && instance is not null
                ? instance.DefId
                : throw new InvalidOperationException("the run does not hold " + piece);
    }
}
