using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 task 4: <c>Merge</c> (same id and tier → next tier, max 3) and
    /// <c>Recycle</c> (50 % of the standard price, rounded down), both atomic.
    /// </summary>
    public sealed class MergeAndRecycleTests
    {
        [Fact]
        public void Merge_of_equal_items_raises_the_tier_and_keeps_one_instance()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef left = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef right = InventoryFixture.Add(editor, "item.mor_knife");
            int massBefore = InventoryFixture.TierMass(backpack);
            int countBefore = backpack.Items.Count;

            var result = editor.Merge(right, left);

            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal(2, result.Value.NewTier);
            Assert.Equal(left, result.Value.Survivor);
            Assert.Equal(right, result.Value.Consumed);
            Assert.Equal("item.mor_knife", result.Value.DefId);
            Assert.Equal(countBefore - 1, backpack.Items.Count);
            Assert.Equal(massBefore, InventoryFixture.TierMass(backpack));
            Assert.Equal(2, InventoryFixture.ItemOf(backpack, left).Tier);
            Assert.False(backpack.TryGetPiece(right, out _));
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Three_of_a_kind_stop_at_max_tier()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef a = InventoryFixture.Add(editor, "item.tiger_fang");
            PieceRef b = InventoryFixture.Add(editor, "item.tiger_fang");
            PieceRef c = InventoryFixture.Add(editor, "item.tiger_fang");

            Assert.Equal(2, editor.Merge(b, a).Value.NewTier);
            // c is still tier 1, so it cannot merge with the tier-2 survivor.
            Assert.Equal(InventoryError.MergeMismatch, editor.Merge(c, a).Error.Code);
        }

        [Fact]
        public void Merging_two_pieces_at_max_tier_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef[] pieces = new PieceRef[8];
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i] = InventoryFixture.Add(editor, "item.mor_knife");
            }

            // Two full 1 -> 2 -> 3 ladders: 'a' and 'e' end at the maximum tier.
            Assert.Equal(2, editor.Merge(pieces[1], pieces[0]).Value.NewTier);
            Assert.Equal(2, editor.Merge(pieces[3], pieces[2]).Value.NewTier);
            Assert.Equal(3, editor.Merge(pieces[2], pieces[0]).Value.NewTier);
            Assert.Equal(2, editor.Merge(pieces[5], pieces[4]).Value.NewTier);
            Assert.Equal(2, editor.Merge(pieces[7], pieces[6]).Value.NewTier);
            Assert.Equal(3, editor.Merge(pieces[6], pieces[4]).Value.NewTier);

            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            var result = editor.Merge(pieces[4], pieces[0]);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.MergeMaxTier, result.Error.Code);
            Assert.Equal(ErrorKind.InvalidState, result.Error.Kind);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(revision, backpack.Revision);
            Assert.Equal(3, InventoryFixture.ItemOf(backpack, pieces[0]).Tier);
        }

        [Fact]
        public void Merging_different_ids_or_tiers_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef knife = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef fang = InventoryFixture.Add(editor, "item.tiger_fang");
            PieceRef knife2 = InventoryFixture.Add(editor, "item.mor_knife");

            Assert.Equal(InventoryError.MergeMismatch, editor.Merge(fang, knife).Error.Code);
            Assert.Equal(ErrorKind.Validation, editor.Merge(fang, knife).Error.Kind);
            Assert.Equal(2, editor.Merge(knife2, knife).Value.NewTier);

            PieceRef knife3 = InventoryFixture.Add(editor, "item.mor_knife");
            Assert.Equal(InventoryError.MergeMismatch, editor.Merge(knife3, knife).Error.Code);
        }

        [Fact]
        public void Merging_a_piece_with_itself_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef knife = InventoryFixture.Add(editor, "item.mor_knife");

            var result = editor.Merge(knife, knife);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.MergeMismatch, result.Error.Code);
        }

        [Fact]
        public void Merging_a_bag_piece_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.Add(editor, "bag.raw_cotton_bag");
            PieceRef bag2 = InventoryFixture.Add(editor, "bag.raw_cotton_bag");
            byte[] before = InventoryFixture.StateBytes(backpack);

            var result = editor.Merge(bag2, bag);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.MergeNotItem, result.Error.Code);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
        }

        [Fact]
        public void A_merged_item_keeps_its_placement_and_shape()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            // kuman_thong is 2x1, so a quarter turn really does change its shape (the
            // 2x2 tome would have hidden a rotation bug).
            PieceRef placed = InventoryFixture.Place(editor, "item.kuman_thong", 0, 0, Rotation.Degrees90);
            PieceRef other = InventoryFixture.Add(editor, "item.kuman_thong");

            var result = editor.Merge(other, placed);

            Assert.True(result.IsSuccess, result.Error.ToString());
            ItemInstance merged = InventoryFixture.ItemOf(backpack, placed);
            Assert.Equal(PieceZone.Board, merged.Zone);
            Assert.Equal(new CellOffset(0, 0), merged.Origin);
            Assert.Equal(Rotation.Degrees90, merged.Rotation);
            Assert.Equal(2, merged.Tier);
            Assert.Equal(1, merged.Shape.Width);
            Assert.Equal(2, merged.Shape.Height);
            Assert.False(backpack.TryGetPiece(other, out _));
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Merge_between_the_board_and_the_chest_is_allowed()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef onBoard = InventoryFixture.Place(editor, "item.tiger_fang", 0, 0);
            PieceRef inChest = InventoryFixture.Add(editor, "item.tiger_fang");

            var result = editor.Merge(inChest, onBoard);

            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal(2, InventoryFixture.ItemOf(backpack, onBoard).Tier);
            Assert.Equal(Chest.Full, backpack.Chest.SlotOf(inChest));
            Assert.Equal(PieceZone.Board, InventoryFixture.ItemOf(backpack, onBoard).Zone);
        }

        [Fact]
        public void Merging_an_unknown_piece_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef knife = InventoryFixture.Add(editor, "item.mor_knife");

            var result = editor.Merge(new PieceRef(PieceKind.Item, 88), knife);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.UnknownPiece, result.Error.Code);
        }

        [Fact]
        public void Recycle_pays_fifty_percent_rounded_down()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef soda = InventoryFixture.Add(editor, "item.red_soda");
            PieceRef knife = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef bag = InventoryFixture.Add(editor, "bag.raw_cotton_bag");

            Assert.Equal(3, editor.Recycle(soda).Value.PayoutBia);       // 6 -> 3
            Assert.Equal(5, editor.Recycle(knife).Value.PayoutBia);      // 11 -> 5 (5.5 floored)
            Assert.Equal(6, editor.Recycle(bag).Value.PayoutBia);        // 12 -> 6
            Assert.Empty(backpack.Items);
            Assert.Empty(backpack.Bags);
            InventoryFixture.AssertValid(backpack);
        }

        [Fact]
        public void Recycle_reports_the_piece_and_clears_its_cells()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef knife = InventoryFixture.Place(editor, "item.mor_knife", 1, 1);

            var result = editor.Recycle(knife);

            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal("item.mor_knife", result.Value.DefId);
            Assert.Equal(knife, result.Value.Piece);
            Assert.Equal(Board.NoOwner, backpack.Board.ItemOwnerAt(new CellOffset(1, 1)));
            Assert.False(backpack.TryGetPiece(knife, out _));
            Assert.Empty(backpack.OrderedBoardItems());
        }

        [Fact]
        public void Recycling_a_bag_with_an_item_on_it_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 0, 0);
            byte[] before = InventoryFixture.StateBytes(backpack);
            int revision = backpack.Revision;

            var result = editor.Recycle(bag);

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.ItemStranded, result.Error.Code);
            Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.Equal(revision, backpack.Revision);
        }

        [Fact]
        public void Recycling_an_empty_bag_works()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef bag = InventoryFixture.PlaceBag(editor, "bag.wicker_basket_l", 0, 0);

            Assert.True(editor.Recycle(bag).IsSuccess);
            Assert.Equal(0, backpack.Board.ActiveSlotCount);
            Assert.Empty(backpack.Bags);
        }

        [Fact]
        public void Recycling_an_unknown_piece_is_refused()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);

            var result = editor.Recycle(new PieceRef(PieceKind.Bag, 4));

            Assert.True(result.IsFailure);
            Assert.Equal(InventoryError.UnknownPiece, result.Error.Code);
        }

        [Fact]
        public void Recycled_pieces_can_be_undone()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef knife = InventoryFixture.Add(editor, "item.mor_knife");
            byte[] before = InventoryFixture.StateBytes(backpack);

            Assert.True(editor.Recycle(knife).IsSuccess);
            Assert.Empty(backpack.Items);

            Assert.True(editor.Undo().IsSuccess);
            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.True(backpack.TryGetPiece(knife, out _));
        }
    }
}
