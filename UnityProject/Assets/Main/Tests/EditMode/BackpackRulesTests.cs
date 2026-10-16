using NUnit.Framework;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 05 acceptance inside Unity: the bag rules run on the <b>real</b> shipped
    /// tables, through the assemblies Unity compiles, and hand a battle-ready
    /// <see cref="CombatLoadoutSnapshot"/> out the other side.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule engine's own behaviour is tested exhaustively in <c>lib/Tests~</c> (105
    /// cases; measured coverage and commands in <c>docs/stages/05-report.md</c>). What can
    /// only fail <i>here</i> is the seam: real <c>.bytes</c> data through the Luban
    /// adapter, the MessagePack source-generated formatter under Unity's compiler, and
    /// the Battle v1 contract as Unity sees it.
    /// </para>
    /// <para>
    /// Assertions read balance numbers out of the loaded config rather than hard-coding
    /// them, so tuning <c>data/</c> cannot make a correct rule look broken.
    /// </para>
    /// </remarks>
    public sealed class BackpackRulesTests
    {
        private IGameConfig _config;
        private Backpack _backpack;
        private BackpackEditor _editor;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();
            _backpack = new Backpack(_config);
            _editor = new BackpackEditor(_backpack, _config, new MessagePackBackpackSnapshotCodec());
        }

        /// <summary>Adds a bag and lays it on the board. Returns its handle.</summary>
        private PieceRef PlaceBag(string bagId, int x, int y)
        {
            Result<PieceRef> added = _backpack.AddBag(bagId);
            Assert.That(added.IsSuccess, Is.True, "AddBag(" + bagId + ") failed: " + added.Error);

            Result moved = _editor.Move(added.Value, new CellOffset(x, y), Rotation.Degrees0);
            Assert.That(moved.IsSuccess, Is.True, "Move(" + bagId + " -> " + x + "," + y + ") failed: " + moved.Error);

            return added.Value;
        }

        /// <summary>Adds an item and lays it on the board. Returns its handle.</summary>
        private PieceRef PlaceItem(string itemId, int x, int y)
        {
            Result<PieceRef> added = _backpack.AddItem(itemId);
            Assert.That(added.IsSuccess, Is.True, "AddItem(" + itemId + ") failed: " + added.Error);

            Result moved = _editor.Move(added.Value, new CellOffset(x, y), Rotation.Degrees0);
            Assert.That(moved.IsSuccess, Is.True, "Move(" + itemId + " -> " + x + "," + y + ") failed: " + moved.Error);

            return added.Value;
        }

        [Test]
        public void Real_data_places_a_bag_and_weapon_and_resolves_the_battle_contract()
        {
            PlaceBag("bag.raw_cotton_bag", 0, 0);
            PlaceItem("item.mor_knife", 1, 1);

            var resolver = new LoadoutResolver(_config, new SynergyEvaluator(_config));
            CombatLoadoutSnapshot snapshot = resolver.Resolve(_backpack);

            // Stats come from `constant`, plus the weight the two pieces added.
            Assert.That(snapshot.Stats.MaxHp, Is.EqualTo(_config.Constant.MaxHp));
            Assert.That(snapshot.Stats.MaxMp, Is.EqualTo(_config.Constant.MaxMp));
            Assert.That(snapshot.Stats.SpeedPenaltyPercent, Is.GreaterThan(0),
                "a 1.0-weight bag plus a weapon must slow the walker down");
            Assert.That(snapshot.Stats.SpeedPenaltyPercent,
                Is.LessThanOrEqualTo(_config.Constant.MaxSpeedPenaltyPercent));
            Assert.That(snapshot.Stats.MoveSpeed, Is.LessThan(_config.Constant.MoveSpeed));

            // The weapon is the Battle v1 contract type, built from the skill table.
            Assert.That(snapshot.Weapons, Has.Count.EqualTo(1), "one arcane weapon on the board is one weapon");
            WeaponLoadout weapon = snapshot.Weapons[0];
            Assert.That(_config.TryGetItem("item.mor_knife", out ItemConfig knife), Is.True);
            Assert.That(weapon.SkillId, Is.EqualTo(knife.SkillId));
            Assert.That(weapon.Tier, Is.EqualTo(1));
            Assert.That(snapshot.Summons, Is.Empty);
            Assert.That(snapshot.Consumables, Is.Empty);
        }

        [Test]
        public void An_item_with_no_bag_under_it_is_refused_and_changes_nothing()
        {
            Result<PieceRef> added = _backpack.AddItem("item.mor_knife");
            Assert.That(added.IsSuccess, Is.True);

            // Measured after the item was acquired: acquiring it is a real change, so the
            // baseline for "the rejected move changed nothing" is the state it landed in.
            int revision = _backpack.Revision;

            Result moved = _editor.Move(added.Value, new CellOffset(4, 3), Rotation.Degrees0);

            Assert.That(moved.IsFailure, Is.True, "an item may only sit where a bag covers it");
            Assert.That(moved.Error.Code, Is.EqualTo(InventoryError.ItemNotCovered));
            Assert.That(_backpack.Revision, Is.EqualTo(revision),
                "a rejected command must not move the revision");
            Assert.That(_backpack.TryGetPiece(added.Value, out PieceInstance untouched), Is.True);
            Assert.That(untouched.Zone, Is.EqualTo(PieceZone.Chest), "the refused item must still be in the chest");
        }

        [Test]
        public void Bags_may_not_overlap_and_may_not_leave_the_board()
        {
            PieceRef first = PlaceBag("bag.wicker_basket_l", 0, 0);
            Result<PieceRef> second = _backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(second.IsSuccess, Is.True);

            Result overlap = _editor.Move(second.Value, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.That(overlap.Error.Code, Is.EqualTo(InventoryError.BagOverlap));

            Result offBoard = _editor.Move(
                second.Value,
                new CellOffset(_backpack.Board.Width - 1, _backpack.Board.Height - 1),
                Rotation.Degrees0);
            Assert.That(offBoard.Error.Code, Is.EqualTo(InventoryError.OutOfBoard));

            // Both rejections left the board exactly as it was: the first bag still owns
            // its own cells, and the second is still in the chest.
            Assert.That(_backpack.TryGetBag(first.InstanceId, out BagPieceInstance placed), Is.True);
            Assert.That(_backpack.Board.BagOwnerAt(placed.CellAt(0)), Is.EqualTo(placed.InstanceId));
            Assert.That(_backpack.TryGetBag(second.Value.InstanceId, out BagPieceInstance stayed), Is.True);
            Assert.That(stayed.Zone, Is.EqualTo(PieceZone.Chest));
        }

        [Test]
        public void Four_quarter_turns_return_every_piece_to_its_authored_shape()
        {
            bool sawChiralShape = false;
            int checkedShapes = 0;

            foreach (ItemConfig item in _config.Items)
            {
                sawChiralShape |= CheckFourTurns(item.Id, item.Footprint);
                checkedShapes++;
            }

            foreach (BagConfig bag in _config.Bags)
            {
                sawChiralShape |= CheckFourTurns(bag.Id, bag.Footprint);
                checkedShapes++;
            }

            Assert.That(checkedShapes, Is.GreaterThan(0), "no piece has a footprint to rotate");
            Assert.That(sawChiralShape, Is.True,
                "no shipped piece has an asymmetric footprint, so the normalisation rule is untested by this data");
        }

        /// <summary>Turns a shape four times clockwise and reports whether it is asymmetric.</summary>
        private static bool CheckFourTurns(string id, Footprint authored)
        {
            Footprint quarter = FootprintRotation.Rotate(authored, Rotation.Degrees90);
            Footprint half = FootprintRotation.Rotate(authored, Rotation.Degrees180);

            // Four quarter turns, one at a time, must land back on the authored picture.
            Footprint walk = authored;
            for (int turn = 0; turn < 4; turn++)
            {
                walk = FootprintRotation.Rotate(walk, Rotation.Degrees90);
            }

            Assert.That(walk, Is.EqualTo(authored), id + " did not come back after four quarter turns");
            Assert.That(FootprintRotation.Rotate(quarter, Rotation.Degrees90), Is.EqualTo(half),
                id + " rotation does not compose");
            Assert.That(FootprintRotation.Rotate(half, Rotation.Degrees180), Is.EqualTo(authored),
                id + " two half turns are not a full turn");

            if (quarter == authored)
            {
                return false;
            }

            // An asymmetric shape must be re-normalised, never drifting off its origin.
            Assert.That(quarter.Width, Is.EqualTo(authored.Height), id + " width/height did not swap");
            Assert.That(quarter.Height, Is.EqualTo(authored.Width), id + " width/height did not swap");
            return true;
        }

        [Test]
        public void A_chiral_bag_is_refused_when_its_turn_would_leave_the_board()
        {
            // bag.wicker_basket_l is the L-tetromino: the shipped piece that proves
            // normalisation is real, because its 90-degree form differs from its authored one.
            Assert.That(_config.TryGetBag("bag.wicker_basket_l", out BagConfig basket), Is.True);
            Footprint authored = basket.Footprint;
            Footprint turned = FootprintRotation.Rotate(authored, Rotation.Degrees90);
            Assert.That(turned, Is.Not.EqualTo(authored), "the basket is expected to be asymmetric");

            // Park it flush against the edge its turn will grow into, so the rotation can
            // only be legal if the board rule is broken.
            int originX = authored.Width < authored.Height ? _backpack.Board.Width - authored.Width : 0;
            int originY = authored.Width < authored.Height ? 0 : _backpack.Board.Height - authored.Height;

            Result<PieceRef> added = _backpack.AddBag("bag.wicker_basket_l");
            Assert.That(added.IsSuccess, Is.True);
            Result placed = _editor.Move(added.Value, new CellOffset(originX, originY), Rotation.Degrees0);
            Assert.That(placed.IsSuccess, Is.True, placed.Error.ToString());

            bool turnedFits = originX + turned.Width <= _backpack.Board.Width
                && originY + turned.Height <= _backpack.Board.Height;

            Result rotate = _editor.Rotate(added.Value);

            if (!turnedFits)
            {
                Assert.That(rotate.IsFailure, Is.True, "a turn that hangs off the board must be refused");
                Assert.That(rotate.Error.Code, Is.EqualTo(InventoryError.OutOfBoard));
            }

            Assert.That(_backpack.TryGetBag(added.Value.InstanceId, out BagPieceInstance bag), Is.True);
            Assert.That(bag.Rotation, Is.EqualTo(rotate.IsSuccess ? Rotation.Degrees90 : Rotation.Degrees0),
                "a refused rotation must leave the piece as it was");
        }

        /// <summary>
        /// Stage 09f / ADR 0020: a bag that moves <b>carries</b> the items standing on it — and a bag
        /// that leaves the board still may not, because an item can never come along into the chest.
        /// </summary>
        /// <remarks>
        /// This test used to assert the opposite for both commands ("moving or storing the only bag
        /// under the item is refused with <c>inventory.item_stranded</c>"). The rule changed, so the
        /// assertions changed with it; what did not change is that a refused command leaves the state
        /// exactly as it was (AGENTS rule 7).
        /// </remarks>
        [Test]
        public void A_bag_move_carries_the_items_on_it_and_a_store_that_would_strand_one_is_refused()
        {
            // Two 3x3 bags side by side, and an item whose whole footprint sits inside the
            // right-hand bag only — so the right bag is the only thing covering it.
            PieceRef left = PlaceBag("bag.raw_cotton_bag", 0, 0);
            PieceRef right = PlaceBag("bag.raw_cotton_bag", 3, 0);
            PieceRef item = PlaceItem("item.mor_knife", 3, 0);

            int revision = _backpack.Revision;

            Result moveRight = _editor.Move(right, new CellOffset(4, 0), Rotation.Degrees0);

            Assert.That(moveRight.IsSuccess, Is.True, "a bag move now carries the item with it: " + moveRight.Error);
            Assert.That(OriginOf(item), Is.EqualTo(new CellOffset(4, 0)),
                "the item did not ride with the bag that was carrying it");
            Assert.That(_backpack.Revision, Is.EqualTo(revision + 1),
                "one gesture is one change, however many pieces moved with it");

            // Off the board is a different command: the item would be left with nothing under it.
            int afterMove = _backpack.Revision;
            Result storeRight = _editor.TransferToChest(right);

            Assert.That(storeRight.Error.Code, Is.EqualTo(InventoryError.ItemStranded),
                "storing a bag an item is standing on is still refused");
            Assert.That(_backpack.Revision, Is.EqualTo(afterMove), "a refused command must not change state");
            Assert.That(OriginOf(item), Is.EqualTo(new CellOffset(4, 0)), "the item did not move either");

            Result storeLeft = _editor.TransferToChest(left);
            Assert.That(storeLeft.IsSuccess, Is.True, "nothing depends on the left-hand bag");
        }

        /// <summary>
        /// Stage 09f / ADR 0020: an item lying across two bags does not ride with either of them. If
        /// the move would leave part of it with no bag under it, the whole command is refused with its
        /// own reason code so the screen can tell the player what to do about it.
        /// </summary>
        [Test]
        public void A_bag_move_is_refused_whole_when_it_would_leave_a_two_bag_item_uncovered()
        {
            // Two 3x3 bags side by side and a 2x2 tome across the seam: columns 2-3, rows 0-1.
            // Half of it belongs to a bag that is not moving.
            PieceRef left = PlaceBag("bag.raw_cotton_bag", 0, 0);
            PieceRef right = PlaceBag("bag.raw_cotton_bag", 3, 0);
            PieceRef tome = PlaceItem("item.spell_tome", 2, 0);

            int revision = _backpack.Revision;

            Result slid = _editor.Move(left, new CellOffset(0, 3), Rotation.Degrees0);

            Assert.That(slid.IsFailure, Is.True, "the item spans both bags, so half of it cannot come along");
            Assert.That(slid.Error.Code, Is.EqualTo(InventoryError.ItemSpansBags),
                "the refusal carries its own reason, not the one for a piece taken off the board");
            Assert.That(_backpack.Revision, Is.EqualTo(revision), "a refused move must not change the revision");
            Assert.That(OriginOf(left), Is.EqualTo(new CellOffset(0, 0)), "the bag stayed where it was");
            Assert.That(OriginOf(tome), Is.EqualTo(new CellOffset(2, 0)), "and so did the item");

            // Nothing in the rules says the tome may not slide within its own two bags: moving it onto
            // the right-hand bag alone puts it wholly on one bag, and then the same bag move is legal.
            Assert.That(_editor.Move(tome, new CellOffset(3, 0), Rotation.Degrees0).IsSuccess, Is.True);
            Assert.That(_editor.Move(left, new CellOffset(0, 3), Rotation.Degrees0).IsSuccess, Is.True,
                "with the tome off it, the bag may go where it likes");
            Assert.That(OriginOf(right), Is.EqualTo(new CellOffset(3, 0)), "the bag that was staying did not move");
        }

        /// <summary>The board cell a piece stands on, for the assertions above.</summary>
        private CellOffset OriginOf(PieceRef piece)
        {
            Assert.That(_backpack.TryGetPiece(piece, out PieceInstance instance), Is.True, "no such piece: " + piece);
            return instance.Origin;
        }

        [Test]
        public void Undo_restores_the_previous_layout_and_lock_freezes_every_edit()
        {
            PlaceBag("bag.raw_cotton_bag", 0, 0);
            PieceRef knife = PlaceItem("item.mor_knife", 0, 0);
            Assert.That(_editor.CanUndo, Is.True);

            Result undo = _editor.Undo();
            Assert.That(undo.IsSuccess, Is.True);
            Assert.That(_backpack.TryGetPiece(knife, out PieceInstance undone), Is.True);
            Assert.That(undone.Zone, Is.EqualTo(PieceZone.Chest), "undo must put the item back in the chest");

            Result<PieceRef> bag = _backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(bag.IsSuccess, Is.True);
            Assert.That(_editor.SetLocked(true).IsSuccess, Is.True);

            Result lockedMove = _editor.Move(bag.Value, new CellOffset(0, 0), Rotation.Degrees0);
            Assert.That(lockedMove.Error.Code, Is.EqualTo(InventoryError.Locked));
            Assert.That(_editor.Undo().Error.Code, Is.EqualTo(InventoryError.Locked), "locking freezes undo too");

            Assert.That(_editor.SetLocked(false).IsSuccess, Is.True);
            Assert.That(_editor.Move(bag.Value, new CellOffset(0, 3), Rotation.Degrees0).IsSuccess, Is.True);
        }

        [Test]
        public void Merge_raises_the_tier_and_recycle_pays_the_data_percentage_back()
        {
            PlaceBag("bag.raw_cotton_bag", 0, 0);
            PieceRef survivor = PlaceItem("item.mor_knife", 0, 0);
            PieceRef consumed = PlaceItem("item.mor_knife", 1, 0);

            // Stage 09f: an arcane weapon has upgrade choices, so the merge that carries one is the
            // one that names the pick — the fold refuses rather than choosing for the player.
            const uint Seed = 20261016u;
            Result<MergeOffer> offered = _editor.OfferMerge(consumed, survivor, Seed);
            Assert.That(offered.IsSuccess, Is.True, offered.Error.ToString());
            Assert.That(offered.Value.HasChoices, Is.True, "an arcane weapon has choices at the 1 -> 2 step");

            Result<MergeOutcome> merge = _editor.ConfirmMerge(
                consumed, survivor, offered.Value.Options[0].Id, Seed);
            Assert.That(merge.IsSuccess, Is.True, merge.Error.ToString());
            Assert.That(merge.Value.NewTier, Is.EqualTo(2));
            Assert.That(_backpack.Items.Count, Is.EqualTo(1), "the consumed instance must be gone, not duplicated");

            Result<RecycleOutcome> recycle = _editor.Recycle(survivor);
            Assert.That(recycle.IsSuccess, Is.True, recycle.Error.ToString());
            Assert.That(_config.TryGetItem("item.mor_knife", out ItemConfig knife), Is.True);
            Assert.That(recycle.Value.PayoutBia,
                Is.EqualTo((knife.Price * _config.Economy.SellBackPercent) / 100));
            Assert.That(_backpack.Items, Is.Empty);
        }

        [Test]
        public void The_snapshot_codec_round_trips_through_the_generated_formatter()
        {
            Assert.That(MessagePackBackpackSnapshotCodec.GeneratedFormatterIsPresent, Is.True,
                "the reflection resolver is not IL2CPP-safe, so the generated formatter must exist");

            PlaceBag("bag.raw_cotton_bag", 0, 0);
            PlaceItem("item.mor_knife", 1, 1);

            var codec = new MessagePackBackpackSnapshotCodec();
            byte[] bytes = codec.Encode(_backpack.CaptureSnapshot());
            BackpackSnapshot restored = codec.Decode(bytes);

            Assert.That(bytes.Length, Is.GreaterThan(0));
            Assert.That(restored.NextInstanceId, Is.EqualTo(_backpack.NextInstanceId));
            Assert.That(restored.Pieces.Count, Is.EqualTo(_backpack.Bags.Count + _backpack.Items.Count));

            for (int i = 0; i < restored.Pieces.Count; i++)
            {
                PieceSnapshot piece = restored.Pieces[i];
                Assert.That(
                    _backpack.TryGetPiece(new PieceRef(piece.Kind, piece.InstanceId), out PieceInstance live),
                    Is.True);
                Assert.That(piece.DefId, Is.EqualTo(live.DefId));
                Assert.That(piece.Zone, Is.EqualTo(live.Zone));
                Assert.That(piece.Rotation, Is.EqualTo(live.Rotation));
                Assert.That(piece.OriginX, Is.EqualTo(live.Origin.X));
                Assert.That(piece.OriginY, Is.EqualTo(live.Origin.Y));
                Assert.That(piece.ChestSlot, Is.EqualTo(live.ChestSlot));
            }
        }
    }
}
