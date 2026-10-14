using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 tasks 4, 5 and 9: move and rotate, the lock, and undo / reset over a
    /// snapshot.
    /// </summary>
    public sealed class BackpackEditorTests
    {
        [Fact]
        public void Move_takes_a_piece_from_the_chest_to_the_board()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef item = InventoryFixture.Add(editor, "item.mor_knife");

            var result = editor.Move(item, new CellOffset(2, 2), Rotation.Degrees0);

            Assert.True(result.IsSuccess, result.Error.ToString());
            ItemInstance placed = InventoryFixture.ItemOf(backpack, item);
            Assert.Equal(PieceZone.Board, placed.Zone);
            Assert.Equal(new CellOffset(2, 2), placed.Origin);
            Assert.Equal(PieceInstance.NoChestSlot, placed.ChestSlot);
            Assert.Equal(Chest.Full, backpack.Chest.SlotOf(item));
            Assert.Equal(item.InstanceId, backpack.Board.ItemOwnerAt(new CellOffset(2, 2)));
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Move_within_the_board_relocates_without_losing_the_piece()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef item = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            int revision = backpack.Revision;

            Assert.True(editor.Move(item, new CellOffset(2, 2), Rotation.Degrees0).IsSuccess);

            Assert.Equal(Board.NoOwner, backpack.Board.ItemOwnerAt(new CellOffset(0, 0)));
            Assert.Equal(item.InstanceId, backpack.Board.ItemOwnerAt(new CellOffset(2, 2)));
            Assert.Equal(revision + 1, backpack.Revision);
            Assert.Single(backpack.OrderedBoardItems());
        }

        [Fact]
        public void Rotate_four_times_returns_the_piece_to_where_it_started()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef basket = InventoryFixture.Place(editor, "bag.wicker_basket_l", 4, 0);
            byte[] before = InventoryFixture.StateBytes(backpack);

            for (int i = 0; i < 4; i++)
            {
                Assert.True(editor.Rotate(basket).IsSuccess);
            }

            Assert.Equal(Rotation.Degrees0, InventoryFixture.BagOf(backpack, basket).Rotation);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.True(editor.CanUndo);
        }

        [Fact]
        public void Rotate_a_rotated_item_updates_its_cells()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef familiar = InventoryFixture.Place(editor, "item.kuman_thong", 0, 0);

            Assert.Equal(2, InventoryFixture.ItemOf(backpack, familiar).Shape.Width);
            Assert.True(editor.Rotate(familiar).IsSuccess);
            Assert.Equal(1, InventoryFixture.ItemOf(backpack, familiar).Shape.Width);
            Assert.Equal(2, InventoryFixture.ItemOf(backpack, familiar).Shape.Height);
            Assert.Equal(familiar.InstanceId, backpack.Board.ItemOwnerAt(new CellOffset(0, 1)));
            Assert.Equal(Board.NoOwner, backpack.Board.ItemOwnerAt(new CellOffset(1, 0)));
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Rotating_a_bag_that_would_strand_an_item_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.wicker_basket_l", 0, 0);
            // (0,2) is the basket's bottom-left cell (the L's foot).
            InventoryFixture.Place(editor, "item.mor_knife", 0, 2);
            byte[] before = InventoryFixture.StateBytes(backpack);

            var result = editor.Rotate(bag);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.ItemStranded, result.Error.Code);
            Assert.Equal(Rotation.Degrees0, InventoryFixture.BagOf(backpack, bag).Rotation);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Lock_refuses_every_edit_and_leaves_the_state_alone()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            PieceRef spare = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            Assert.True(editor.SetLocked(true).IsSuccess);
            Assert.True(editor.IsLocked);

            Assert.Equal(InventoryError.Locked, editor.Move(knife, new CellOffset(1, 1), Rotation.Degrees0).Error.Code);
            Assert.Equal(InventoryError.Locked, editor.Rotate(knife).Error.Code);
            Assert.Equal(InventoryError.Locked, editor.TransferToChest(knife).Error.Code);
            Assert.Equal(InventoryError.Locked, editor.Merge(spare, knife).Error.Code);
            Assert.Equal(InventoryError.Locked, editor.Recycle(knife).Error.Code);
            Assert.Equal(InventoryError.Locked, editor.Undo().Error.Code);
            Assert.Equal(InventoryError.Locked, editor.Reset().Error.Code);
            Assert.Equal(ErrorKind.InvalidState, editor.Undo().Error.Kind);

            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(revision, backpack.Revision);

            Assert.True(editor.SetLocked(false).IsSuccess);
            Assert.True(editor.Move(knife, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
        }

        [Fact]
        public void Undo_restores_the_previous_state()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            // Entering Camp is what starts the history: laying the camp out is not "an edit".
            editor.MarkCampEntry();
            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            Assert.True(editor.Move(knife, new CellOffset(2, 2), Rotation.Degrees0).IsSuccess);
            Assert.True(editor.CanUndo);
            Assert.True(backpack.Revision > revision);

            Assert.True(editor.Undo().IsSuccess);

            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.True(backpack.TryValidate(out _));
            Assert.False(editor.CanUndo);
        }

        [Fact]
        public void Undo_without_history_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            byte[] before = InventoryFixture.StateBytes(backpack);

            var result = editor.Undo();

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.NothingToUndo, result.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(0, backpack.Revision);
        }

        [Fact]
        public void Undo_history_is_bounded()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, 2, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            byte[] afterPlacement = InventoryFixture.StateBytes(backpack);

            Assert.True(editor.Move(knife, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            Assert.True(editor.Move(knife, new CellOffset(2, 1), Rotation.Degrees0).IsSuccess);
            Assert.True(editor.Move(knife, new CellOffset(2, 2), Rotation.Degrees0).IsSuccess);

            Assert.Equal(2, editor.UndoDepth);
            Assert.True(editor.Undo().IsSuccess);
            Assert.True(editor.Undo().IsSuccess);

            // The first move's snapshot was pushed out of the bounded history, so the
            // third undo is refused and the state keeps the oldest snapshot still held.
            Assert.False(editor.CanUndo);
            Assert.Equal(InventoryError.NothingToUndo, editor.Undo().Error.Code);
            Assert.Equal(new CellOffset(1, 1), backpack.OrderedBoardItems()[0].Origin);
            Assert.NotEqual(afterPlacement, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void Reset_returns_to_the_camp_entry_state()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            // The layout the player walked in with (the starter bag and weapon of Stage 06's
            // initial loadout) is the state "คืนสภาพ" goes back to.
            editor.MarkCampEntry();
            byte[] atCamp = InventoryFixture.StateBytes(backpack);

            Assert.True(editor.Move(knife, new CellOffset(2, 2), Rotation.Degrees90).IsSuccess);
            Assert.True(editor.Recycle(knife).IsSuccess);
            Assert.NotEqual(atCamp, InventoryFixture.StateBytes(backpack));

            var reset = editor.Reset();

            Assert.True(reset.IsSuccess, reset.Error.ToString());
            Assert.Equal(atCamp, InventoryFixture.StateBytes(backpack));
            Assert.False(editor.CanUndo);
            Assert.True(backpack.TryValidate(out _));
            Assert.Single(backpack.OrderedBoardItems());
        }

        [Fact]
        public void Reset_keeps_working_after_a_new_camp_entry()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);

            editor.MarkCampEntry();
            byte[] atSecondCamp = InventoryFixture.StateBytes(backpack);

            Assert.True(editor.Move(knife, new CellOffset(2, 2), Rotation.Degrees0).IsSuccess);
            Assert.True(editor.Reset().IsSuccess);

            Assert.Equal(atSecondCamp, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void A_rejected_command_leaves_the_revision_alone()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            PieceRef spare = InventoryFixture.Add(editor, "item.mor_knife");
            int revision = backpack.Revision;

            Assert.True(editor.Move(spare, new CellOffset(0, 0), Rotation.Degrees0).IsFailure);
            Assert.True(editor.Rotate(spare).IsFailure);
            Assert.True(editor.Merge(spare, spare).IsFailure);
            Assert.True(editor.Move(new PieceRef(PieceKind.Item, 99), new CellOffset(0, 0), Rotation.Degrees0).IsFailure);
            Assert.Equal(revision, backpack.Revision);

            Assert.True(editor.Move(knife, new CellOffset(1, 1), Rotation.Degrees0).IsSuccess);
            Assert.Equal(revision + 1, backpack.Revision);
        }

        [Fact]
        public void Undo_of_a_failed_command_stack_stays_empty()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef spare = InventoryFixture.Add(editor, "item.mor_knife");
            editor.MarkCampEntry();

            Assert.True(editor.Move(spare, new CellOffset(5, 5), Rotation.Degrees0).IsFailure);
            Assert.False(editor.CanUndo);
            Assert.Equal(0, editor.UndoDepth);
        }

        [Fact]
        public void Items_in_the_chest_do_not_give_active_slots()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.Add(editor, "bag.raw_cotton_bag");
            PieceRef item = InventoryFixture.Add(editor, "item.mor_knife");

            // A bag in the chest opens nothing, so the item has nowhere to go.
            Assert.Equal(0, backpack.Board.ActiveSlotCount);
            Assert.Equal(InventoryError.ItemNotCovered, editor.Move(item, new CellOffset(0, 0), Rotation.Degrees0).Error.Code);

            Assert.True(editor.Move(bag, new CellOffset(0, 0), Rotation.Degrees0).IsSuccess);
            Assert.True(editor.Move(item, new CellOffset(0, 0), Rotation.Degrees0).IsSuccess);
        }

        [Fact]
        public void Editor_requires_its_inputs()
        {
            IGameConfig config = InventoryFixture.Config();
            var backpack = new Backpack(config);
            var codec = new MessagePackBackpackSnapshotCodec();

            Assert.Throws<System.ArgumentNullException>(() => new BackpackEditor(null!, config, codec));
            Assert.Throws<System.ArgumentNullException>(() => new BackpackEditor(backpack, null!, codec));
            Assert.Throws<System.ArgumentNullException>(() => new BackpackEditor(backpack, config, null!));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BackpackEditor(backpack, config, codec, 0));
            Assert.Throws<System.ArgumentNullException>(() => new Backpack(null!));
        }
    }
}
