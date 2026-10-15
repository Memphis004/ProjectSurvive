using System;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{        /// <summary>
        /// Stage 05 task 3: bags never overlap, an item needs an ActiveSlot under every cell
        /// it covers (and may span several bags), and items never overlap each other — plus the
        /// Stage 09f rule that a bag brings the items lying on it along (ADR 0020).
        /// </summary>
        public sealed class BoardPlacementTests
        {
        /// <summary>An item's top-left cell measured inside the bag's own frame.</summary>
        private static CellOffset Relative(PieceRef bag, PieceRef item, Backpack backpack)
        {
            CellOffset bagOrigin = InventoryFixture.BagOf(backpack, bag).Origin;
            CellOffset itemOrigin = InventoryFixture.ItemOf(backpack, item).Origin;
            return new CellOffset(itemOrigin.X - bagOrigin.X, itemOrigin.Y - bagOrigin.Y);
        }


        [Fact]
        public void Board_is_the_configured_size()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            Assert.Equal(9, backpack.Board.Width);
            Assert.Equal(7, backpack.Board.Height);
            Assert.Equal(10, backpack.Chest.SlotCount);
            Assert.Equal(3, backpack.MaxTier);
            Assert.Equal(0, backpack.Board.ActiveSlotCount);
            Assert.False(editor.IsLocked);
        }

        [Fact]
        public void Piece_off_the_board_is_refused_and_changes_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.Add(editor, "bag.raw_cotton_bag");
            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            // x = 7 puts the 3-wide bag's last column on 9, past the 9-column board.
            var result = editor.Move(bag, new CellOffset(7, 0), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.OutOfBoard, result.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(revision, backpack.Revision);
        }

        [Fact]
        public void Item_on_an_uncovered_cell_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef item = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);

            var result = editor.Move(item, new CellOffset(5, 5), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.ItemNotCovered, result.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Bags_may_not_overlap()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef second = InventoryFixture.Add(editor, "bag.raw_cotton_bag");

            // (1,1) overlaps the first bag's middle cell.
            var result = editor.Move(second, new CellOffset(1, 1), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.BagOverlap, result.Error.Code);
            Assert.Single(backpack.OrderedBoardBags());
        }

        [Fact]
        public void Items_may_not_overlap()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            PieceRef second = InventoryFixture.Add(editor, "item.tiger_fang");

            var result = editor.Move(second, new CellOffset(0, 0), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.ItemOverlap, result.Error.Code);
        }

        [Fact]
        public void Item_may_span_two_bags()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            // Two 3x3 bags side by side: columns 0-2 and 3-5.
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);

            // The 2x2 tome needs (2,0) (2,1) from the first bag and (3,0) (3,1) from the
            // second: exactly the GDD's "ไอเทมพาดข้ามหลายย่ามได้".
            var result = editor.Move(InventoryFixture.Add(editor, "item.spell_tome"), new CellOffset(2, 0), Rotation.Degrees0);

            Assert.True(result.IsSuccess, result.Error.ToString());
            InventoryFixture.AssertValid(backpack);
            Assert.Equal(18, backpack.Board.ActiveSlotCount);
            Assert.Equal(new CellOffset(2, 0), backpack.OrderedBoardItems()[0].Origin);

            // The tome lies on two bags, so it cannot ride with either of them (ADR 0020 §1):
            // moving the first bag down leaves the two cells it gave up with no bag under them,
            // and the whole command is refused — the refusal names the span, not a lost cell.
            PieceRef firstBag = backpack.OrderedBoardBags()[0].Ref;
            Assert.Equal(
                InventoryError.ItemSpansBags,
                editor.Move(firstBag, new CellOffset(0, 3), Rotation.Degrees0).Error.Code);

            // Turning that same bag in place keeps every cell of the tome covered, so coverage is
            // what is judged, not the mere presence of an item.
            Assert.True(editor.Rotate(firstBag).IsSuccess);
        }

        [Fact]
        public void Item_that_hangs_over_an_uncovered_gap_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 4, 0);

            // Column 3 belongs to no bag, so the 2x2 tome cannot bridge the gap.
            var result = editor.Move(InventoryFixture.Add(editor, "item.spell_tome"), new CellOffset(2, 0), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.ItemNotCovered, result.Error.Code);
        }

        [Fact]
        public void Rotating_a_piece_against_the_board_edge_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);

            // The L basket is 2 columns x 3 rows; a quarter turn makes it 3 x 2.
            // At x = 6 it still fits (6 + 3 = 9), at x = 7 it does not (7 + 3 = 10).
            PieceRef tight = InventoryFixture.Place(editor, "bag.wicker_basket_l", 6, 4);
            PieceRef edge = InventoryFixture.Place(editor, "bag.wicker_basket_l", 7, 0);

            Assert.True(editor.Rotate(tight).IsSuccess);
            Assert.Equal(3, InventoryFixture.BagOf(backpack, tight).Shape.Width);
            Assert.Equal(Rotation.Degrees90, InventoryFixture.BagOf(backpack, tight).Rotation);

            var result = editor.Rotate(edge);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.OutOfBoard, result.Error.Code);
            Assert.Equal(Rotation.Degrees0, InventoryFixture.BagOf(backpack, edge).Rotation);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Moving_a_bag_carries_the_item_standing_on_it()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            // The middle cell of the 3x3 bag: the case the previous rule refused outright.
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 1, 1);
            int revision = backpack.Revision;
            int history = editor.UndoDepth;

            var result = editor.Move(bag, new CellOffset(5, 3), Rotation.Degrees0);

            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal(new CellOffset(5, 3), InventoryFixture.BagOf(backpack, bag).Origin);
            Assert.Equal(new CellOffset(6, 4), InventoryFixture.ItemOf(backpack, knife).Origin);
            Assert.Equal(
                new CellOffset(1, 1),
                Relative(bag, knife, backpack));
            // One gesture is one change: the revision moves once and the gesture is one undo step.
            Assert.Equal(revision + 1, backpack.Revision);
            Assert.Equal(history + 1, editor.UndoDepth);
            InventoryFixture.AssertValid(backpack);
        }

        /// <summary>
        /// A bag may step onto cells its own assembly was covering this moment: the whole assembly is
        /// lifted before it is put down (ADR 0020 §4), so the bag's own footprint and its riders' old
        /// cells are never read as an obstacle.
        /// </summary>
        [Fact]
        public void A_bag_may_step_onto_the_cells_its_own_assembly_just_left()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 1, 1);

            // One column right: the new footprint overlaps two columns of the old one, and the
            // knife lands on (2,1) — a cell the bag itself was covering a moment ago.
            var result = editor.Move(bag, new CellOffset(1, 0), Rotation.Degrees0);

            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal(new CellOffset(1, 0), InventoryFixture.BagOf(backpack, bag).Origin);
            Assert.Equal(new CellOffset(2, 1), InventoryFixture.ItemOf(backpack, knife).Origin);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void A_bag_move_that_would_leave_a_spanning_item_uncovered_is_refused_whole()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef first = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);
            PieceRef tome = InventoryFixture.Place(editor, "item.spell_tome", 2, 0);
            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            // Columns 0-2 rows 3-5: a legal bag cell that overlaps no other bag, and that leaves
            // (2,0) and (2,1) — the tome's share of the first bag — with nothing under them.
            var moved = editor.Move(first, new CellOffset(0, 3), Rotation.Degrees0);

            Assert.True(moved.IsFailure);
            Assert.Equal(InventoryError.ItemSpansBags, moved.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(revision, backpack.Revision);

            // The preview answers with the command's own code, before the player commits.
            Assert.False(editor.CanPlace(first, new CellOffset(0, 3), Rotation.Degrees0, out Error preview));
            Assert.Equal(InventoryError.ItemSpansBags, preview.Code);

            Assert.Equal(new CellOffset(2, 0), InventoryFixture.ItemOf(backpack, tome).Origin);
        }

        [Fact]
        public void Bag_move_is_allowed_when_no_item_stands_on_it()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef first = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef second = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 3, 0);

            // The knife sits on the first bag, so moving the second one is unaffected: its
            // cells are still covered by a bag, and by the same one.
            InventoryFixture.Place(editor, "item.mor_knife", 2, 0);

            var result = editor.Move(second, new CellOffset(6, 0), Rotation.Degrees0);

            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal(0, backpack.Board.BagOwnerAt(new CellOffset(3, 0)));
            Assert.Equal(18, backpack.Board.ActiveSlotCount);
            InventoryFixture.AssertValid(backpack);
            Assert.Equal(first, backpack.OrderedBoardBags()[0].Ref);
        }

        [Fact]
        public void Removing_a_bag_from_under_an_item_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            // A 1x3 pouch at column 8 with two bags' worth of items on it.
            PieceRef pouch = InventoryFixture.PlaceBag(editor, "bag.yant_cloth_pouch", 8, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 8, 0);

            var transfer = editor.TransferToChest(pouch);
            Assert.True(transfer.IsFailure);
            Assert.Equal(InventoryError.ItemStranded, transfer.Error.Code);

            var recycle = editor.Recycle(pouch);
            Assert.True(recycle.IsFailure);
            Assert.Equal(InventoryError.ItemStranded, recycle.Error.Code);
        }

        [Fact]
        public void Move_is_refused_for_an_unknown_piece()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            var result = editor.Move(new PieceRef(PieceKind.Item, 99), new CellOffset(0, 0), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.UnknownPiece, result.Error.Code);
        }

        [Fact]
        public void Move_is_refused_for_an_unknown_bag()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            var result = editor.Move(new PieceRef(PieceKind.Bag, 7), new CellOffset(0, 0), Rotation.Degrees0);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.UnknownPiece, result.Error.Code);
        }

        [Fact]
        public void Adding_an_unknown_piece_id_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            InventoryFixture.NewEditor(config, out Backpack backpack);

            var bag = backpack.AddBag("bag.does_not_exist");
            var item = backpack.AddItem("item.does_not_exist");

            Assert.True(bag.IsFailure);
            Assert.Equal(InventoryError.UnknownPiece, bag.Error.Code);
            Assert.Equal(ErrorKind.NotFound, bag.Error.Kind);
            Assert.True(item.IsFailure);
            Assert.Equal(0, backpack.Bags.Count + backpack.Items.Count);
            Assert.Equal(1, backpack.NextInstanceId);
        }

        [Fact]
        public void Newly_added_pieces_wait_in_the_chest()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            PieceRef bag = InventoryFixture.Add(editor, "bag.raw_cotton_bag");
            PieceRef item = InventoryFixture.Add(editor, "item.mor_knife");

            Assert.True(backpack.TryGetPiece(bag, out PieceInstance? bagPiece));
            Assert.True(backpack.TryGetPiece(item, out PieceInstance? itemPiece));
            Assert.Equal(PieceZone.Chest, bagPiece!.Zone);
            Assert.Equal(PieceZone.Chest, itemPiece!.Zone);
            Assert.Equal(0, bagPiece.ChestSlot);
            Assert.Equal(1, itemPiece.ChestSlot);
            Assert.Equal(2, backpack.Chest.OccupiedCount);
            Assert.Empty(backpack.OrderedBoardPieces());
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void A_piece_keeps_its_rotation_when_it_goes_to_the_chest_and_back()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef basket = InventoryFixture.Place(editor, "bag.wicker_basket_l", 3, 0, Rotation.Degrees90);

            Assert.True(editor.TransferToChest(basket).IsSuccess);
            Assert.Equal(Rotation.Degrees90, InventoryFixture.BagOf(backpack, basket).Rotation);

            Assert.True(editor.Move(basket, new CellOffset(3, 0), Rotation.Degrees90).IsSuccess);
            Assert.Equal(3, InventoryFixture.BagOf(backpack, basket).Shape.Width);
            Assert.Equal(2, InventoryFixture.BagOf(backpack, basket).Shape.Height);
        }

        [Fact]
        public void Chest_holds_ten_slots_and_refuses_the_eleventh_piece()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            for (int i = 0; i < 10; i++)
            {
                Assert.True(InventoryFixture.Add(editor, "item.mor_knife").IsValid);
            }

            var eleventh = backpack.AddItem("item.mor_knife");

            Assert.True(eleventh.IsFailure);
            Assert.Equal(InventoryError.ChestFull, eleventh.Error.Code);
            Assert.Equal(ErrorKind.Insufficient, eleventh.Error.Kind);
            Assert.Equal(10, backpack.Chest.OccupiedCount);
        }

        [Fact]
        public void Transfer_to_a_full_chest_is_refused_and_changes_nothing()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef placed = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);

            // Fill the chest (the placed knife freed slot 1, so ten more are needed after
            // the bag and the knife).
            for (int i = 0; i < 10; i++)
            {
                InventoryFixture.Add(editor, "item.tiger_fang");
            }

            Assert.True(backpack.Chest.IsFull);
            byte[] before = InventoryFixture.StateBytes(backpack);
            var result = editor.TransferToChest(placed);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.ChestFull, result.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Transfer_to_the_chest_from_the_chest_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef item = InventoryFixture.Add(editor, "item.mor_knife");

            var result = editor.TransferToChest(item);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.WrongZone, result.Error.Code);
        }

        [Fact]
        public void Rotate_is_refused_for_a_chest_piece()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef item = InventoryFixture.Add(editor, "item.mor_knife");

            var result = editor.Rotate(item);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.NotOnBoard, result.Error.Code);
        }

        [Fact]
        public void Board_tracks_active_slots_as_bags_move()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);

            Assert.Equal(9, backpack.Board.ActiveSlotCount);
            Assert.True(backpack.Board.IsActiveSlot(new CellOffset(2, 2)));
            Assert.False(backpack.Board.IsActiveSlot(new CellOffset(3, 2)));

            Assert.True(editor.TransferToChest(bag).IsSuccess);
            Assert.Equal(0, backpack.Board.ActiveSlotCount);
            Assert.False(backpack.Board.IsActiveSlot(new CellOffset(0, 0)));
        }

        [Fact]
        public void Board_only_answers_inside_its_bounds()
        {
            IGameConfig config = InventoryFixture.Config();
            InventoryFixture.NewEditor(config, out Backpack backpack);

            Assert.True(backpack.Board.Contains(new CellOffset(8, 6)));
            Assert.False(backpack.Board.Contains(new CellOffset(9, 6)));
            Assert.False(backpack.Board.Contains(new CellOffset(-1, 0)));
            Assert.Equal(Board.NoOwner, backpack.Board.BagOwnerAt(new CellOffset(99, 99)));
            Assert.Equal(Board.NoOwner, backpack.Board.ItemOwnerAt(new CellOffset(-4, 0)));
        }

        [Fact]
        public void Constructing_a_board_without_cells_is_a_programming_error()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Board(0, 7));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Board(9, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Chest(-1));
        }
    }
}
