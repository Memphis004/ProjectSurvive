using System;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using Xunit;

namespace ProjectSurvive.Core.Tests.Inventory
{
    /// <summary>
    /// Stage 05 task 9: undo and reset ride on a MessagePack snapshot, so the snapshot has
    /// to survive a real round trip through the source-generated formatter — the one the
    /// WebGL build uses (a reflection resolver would throw under IL2CPP instead of failing
    /// a test here).
    /// </summary>
    public sealed class BackpackSnapshotCodecTests
    {
        [Fact]
        public void The_generated_formatter_is_present()
        {
            Assert.True(
                MessagePackBackpackSnapshotCodec.GeneratedFormatterIsPresent,
                "MessagePack's source generator did not produce a formatter for BackpackSnapshotDto");
        }

        [Fact]
        public void An_empty_snapshot_round_trips()
        {
            IGameConfig config = InventoryFixture.Config();
            InventoryFixture.NewEditor(config, out Backpack backpack);

            BackpackSnapshot restored = RoundTrip(backpack);

            Assert.Equal(1, restored.NextInstanceId);
            Assert.Empty(restored.Pieces);
            Assert.Equal(0, restored.MergeCount);

            // 2 since Stage 09f added `MergeCount` and the per-step upgrade variants to the
            // shape, so an old payload can be recognised rather than misread.
            Assert.Equal(2, restored.SchemaVersion);
        }

        [Fact]
        public void Every_field_of_every_piece_survives_the_round_trip()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            PieceRef basket = InventoryFixture.Place(editor, "bag.wicker_basket_l", 3, 0, Rotation.Degrees90);
            PieceRef item = InventoryFixture.Place(editor, "item.mor_knife", 1, 1, Rotation.Degrees180);
            PieceRef inChest = InventoryFixture.Add(editor, "item.kuman_thong");
            Assert.NotEqual(PieceRef.None, basket);

            byte[] bytes = InventoryFixture.StateBytes(backpack);
            BackpackSnapshot snapshot = new MessagePackBackpackSnapshotCodec().Decode(bytes);

            Assert.Equal(backpack.Bags.Count + backpack.Items.Count, snapshot.Pieces.Count);
            for (int i = 0; i < snapshot.Pieces.Count; i++)
            {
                PieceSnapshot piece = snapshot.Pieces[i];
                Assert.True(piece.InstanceId > 0);
                Assert.False(string.IsNullOrEmpty(piece.DefId));
                Assert.True(piece.Tier >= 1);
                Assert.InRange((int)piece.Rotation, 0, 3);
            }

            PieceSnapshot boardItem = Find(snapshot, item.InstanceId);
            Assert.Equal(PieceKind.Item, boardItem.Kind);
            Assert.Equal("item.mor_knife", boardItem.DefId);
            Assert.Equal(1, boardItem.Tier);
            Assert.Equal(PieceZone.Board, boardItem.Zone);
            Assert.Equal(1, boardItem.OriginX);
            Assert.Equal(1, boardItem.OriginY);
            Assert.Equal(Rotation.Degrees180, boardItem.Rotation);
            Assert.Equal(PieceInstance.NoChestSlot, boardItem.ChestSlot);

            PieceSnapshot chestItem = Find(snapshot, inChest.InstanceId);
            Assert.Equal(PieceZone.Chest, chestItem.Zone);
            Assert.Equal(0, chestItem.ChestSlot);
            Assert.Equal("item.kuman_thong", chestItem.DefId);
        }

        [Fact]
        public void Restoring_a_snapshot_reproduces_the_state_exactly()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.spell_tome", 1, 0, Rotation.Degrees90);
            InventoryFixture.Add(editor, "item.red_soda");
            byte[] before = InventoryFixture.StateBytes(backpack);
            int pieceCount = snapshotCount(before);

            // Recycle then undo: the state has to come back byte-identical, which is what
            // makes "an undo changes nothing else" checkable.
            Assert.True(editor.Recycle(backpack.OrderedBoardItems()[0].Ref).IsSuccess);
            Assert.NotEqual(before, InventoryFixture.StateBytes(backpack));
            Assert.True(editor.Undo().IsSuccess);

            Assert.Equal(before, InventoryFixture.StateBytes(backpack));
            Assert.True(backpack.TryValidate(out string? violation), violation);
            Assert.Equal(pieceCount, backpack.Bags.Count + backpack.Items.Count);

            static int snapshotCount(byte[] bytes) =>
                new MessagePackBackpackSnapshotCodec().Decode(bytes).Pieces.Count;
        }

        [Fact]
        public void Encoding_the_same_state_twice_gives_the_same_bytes()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.PlaceBag(editor, "bag.raw_cotton_bag", 0, 0);
            InventoryFixture.Place(editor, "item.mor_knife", 1, 1);

            byte[] first = InventoryFixture.StateBytes(backpack);
            byte[] second = InventoryFixture.StateBytes(backpack);

            Assert.Equal(first, second);
        }

        [Fact]
        public void A_tier_raised_by_a_merge_survives_the_round_trip()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef first = InventoryFixture.Add(editor, "item.mor_knife");
            PieceRef second = InventoryFixture.Add(editor, "item.mor_knife");

            Assert.True(InventoryFixture.Merge(editor, second, first).IsSuccess);

            BackpackSnapshot snapshot = new MessagePackBackpackSnapshotCodec()
                .Decode(InventoryFixture.StateBytes(backpack));

            Assert.Single(snapshot.Pieces);
            Assert.Equal(2, snapshot.Pieces[0].Tier);
            Assert.Equal(first.InstanceId, snapshot.Pieces[0].InstanceId);
        }

        /// <summary>
        /// Stage 09f: the chosen upgrade variants and the merge counter are undoable state, so
        /// they have to survive the same codec the undo stack is stored in - and come back with
        /// the right step order, because step 1 is what a 2 -> 3 merge inherits.
        /// </summary>
        [Fact]
        public void The_chosen_upgrade_variants_and_the_merge_count_survive_the_round_trip()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            PieceRef[] pieces = new PieceRef[4];
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i] = InventoryFixture.Add(editor, "item.mor_knife");
            }

            Assert.True(InventoryFixture.Merge(editor, pieces[1], pieces[0]).IsSuccess);
            Assert.True(InventoryFixture.Merge(editor, pieces[3], pieces[2]).IsSuccess);
            Result<MergeOutcome> second = InventoryFixture.Merge(editor, pieces[2], pieces[0]);
            Assert.True(second.IsSuccess, second.Error.ToString());

            var codec = new MessagePackBackpackSnapshotCodec();
            byte[] bytes = InventoryFixture.StateBytes(backpack);
            BackpackSnapshot snapshot = codec.Decode(bytes);

            Assert.Equal(3, backpack.MergeCount);
            Assert.Equal(3, snapshot.MergeCount);
            Assert.Equal(3, snapshot.Pieces[0].Tier);
            Assert.Equal(2, snapshot.Pieces[0].VariantIds.Count);
            Assert.Equal(
                InventoryFixture.ItemOf(backpack, pieces[0]).VariantIds,
                snapshot.Pieces[0].VariantIds);

            // And through an undo, which is the restore path the game takes: the counter and
            // the step slots come back with the pieces, so a repeated merge re-draws the same
            // offer instead of a new one.
            Assert.True(editor.Undo().IsSuccess);
            Assert.Equal(2, backpack.MergeCount);
            Assert.Equal(2, InventoryFixture.ItemOf(backpack, pieces[0]).Tier);
            Assert.Single(InventoryFixture.ItemOf(backpack, pieces[0]).VariantIds);
            Assert.True(backpack.TryValidate(out string? violation), violation);
        }

        [Fact]
        public void Restoring_continues_the_instance_id_sequence()
        {
            IGameConfig config = InventoryFixture.Config();
            BackpackEditor editor = InventoryFixture.NewEditor(config, out Backpack backpack);
            InventoryFixture.Add(editor, "item.mor_knife");
            InventoryFixture.Add(editor, "item.mor_knife");
            byte[] bytes = InventoryFixture.StateBytes(backpack);
            Assert.Equal(3, backpack.NextInstanceId);

            Assert.True(editor.Recycle(backpack.Items[0].Ref).IsSuccess);

            // Undo restores the two pieces, and the next piece still gets id 3: the id
            // counter is part of the snapshot, so a restored state never reuses an id.
            Assert.True(editor.Undo().IsSuccess);
            var third = backpack.AddItem("item.mor_knife");

            Assert.True(third.IsSuccess);
            Assert.Equal(3, third.Value.InstanceId);
            Assert.Equal(4, backpack.NextInstanceId);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void The_dto_is_mapped_both_ways()
        {
            var piece = new PieceSnapshot
            {
                Kind = PieceKind.Bag,
                InstanceId = 7,
                DefId = "bag.raw_cotton_bag",
                Tier = 2,
                Zone = PieceZone.Board,
                OriginX = 3,
                OriginY = 4,
                Rotation = Rotation.Degrees270,
                ChestSlot = PieceInstance.NoChestSlot,
            };
            var snapshot = new BackpackSnapshot
            {
                SchemaVersion = 1,
                NextInstanceId = 8,
                Pieces = new[] { piece },
            };

            BackpackSnapshotDto dto = MessagePackBackpackSnapshotCodec.ToDto(snapshot);
            Assert.Equal(1, dto.SchemaVersion);
            Assert.Equal(8, dto.NextInstanceId);
            Assert.Single(dto.Pieces);
            Assert.Equal((int)PieceKind.Bag, dto.Pieces[0].Kind);
            Assert.Equal((int)PieceZone.Board, dto.Pieces[0].Zone);
            Assert.Equal((int)Rotation.Degrees270, dto.Pieces[0].Rotation);
            Assert.Equal(-1, dto.Pieces[0].ChestSlot);

            BackpackSnapshotDto roundTripped = BackpackSnapshotCodec.RoundTrip(dto);
            BackpackSnapshot back = MessagePackBackpackSnapshotCodec.FromDto(roundTripped);

            Assert.Equal(snapshot.NextInstanceId, back.NextInstanceId);
            Assert.Single(back.Pieces);
            Assert.Equal(piece.Kind, back.Pieces[0].Kind);
            Assert.Equal(piece.InstanceId, back.Pieces[0].InstanceId);
            Assert.Equal(piece.DefId, back.Pieces[0].DefId);
            Assert.Equal(piece.Tier, back.Pieces[0].Tier);
            Assert.Equal(piece.Zone, back.Pieces[0].Zone);
            Assert.Equal(piece.OriginX, back.Pieces[0].OriginX);
            Assert.Equal(piece.OriginY, back.Pieces[0].OriginY);
            Assert.Equal(piece.Rotation, back.Pieces[0].Rotation);
            Assert.Equal(piece.ChestSlot, back.Pieces[0].ChestSlot);
        }

        [Fact]
        public void A_snapshot_with_an_impossible_enum_value_is_rejected()
        {
            var dto = new BackpackSnapshotDto
            {
                SchemaVersion = 1,
                NextInstanceId = 2,
                Pieces =
                {
                    new BackpackPieceDto
                    {
                        Kind = 9,
                        InstanceId = 1,
                        DefId = "item.mor_knife",
                        Tier = 1,
                        Zone = 0,
                        Rotation = 0,
                        ChestSlot = -1,
                    },
                },
            };

            Assert.Throws<InvalidOperationException>(() => MessagePackBackpackSnapshotCodec.FromDto(dto));
        }

        [Fact]
        public void The_codec_requires_its_input()
        {
            var codec = new MessagePackBackpackSnapshotCodec();

            Assert.Throws<ArgumentNullException>(() => codec.Encode(null!));
            Assert.Throws<ArgumentNullException>(() => codec.Decode(null!));
        }

        private static BackpackSnapshot RoundTrip(Backpack backpack)
        {
            var codec = new MessagePackBackpackSnapshotCodec();
            return codec.Decode(codec.Encode(backpack.CaptureSnapshot()));
        }

        private static PieceSnapshot Find(BackpackSnapshot snapshot, int instanceId)
        {
            for (int i = 0; i < snapshot.Pieces.Count; i++)
            {
                if (snapshot.Pieces[i].InstanceId == instanceId)
                {
                    return snapshot.Pieces[i];
                }
            }

            throw new InvalidOperationException("snapshot has no piece " + instanceId);
        }
    }
}
