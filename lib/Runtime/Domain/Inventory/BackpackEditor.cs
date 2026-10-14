using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>What one successful <see cref="BackpackEditor.Merge"/> did.</summary>
    public readonly struct MergeOutcome
    {
        internal MergeOutcome(ItemInstance survivor, PieceRef consumed)
        {
            Survivor = survivor.Ref;
            NewTier = survivor.Tier;
            DefId = survivor.ItemId;
            Consumed = consumed;
        }

        /// <summary>The instance that stayed on the board or in the chest, at the new tier.</summary>
        public PieceRef Survivor { get; }

        /// <summary>The tier the survivor now has.</summary>
        public int NewTier { get; }

        /// <summary>The item id both instances shared.</summary>
        public string DefId { get; }

        /// <summary>The instance that was folded into <see cref="Survivor"/>.</summary>
        public PieceRef Consumed { get; }

        /// <inheritdoc />
        public override string ToString() => "merge " + DefId + " -> t" + NewTier + " (" + Consumed + ")";
    }

    /// <summary>What one successful <see cref="BackpackEditor.Recycle"/> paid out.</summary>
    public readonly struct RecycleOutcome
    {
        internal RecycleOutcome(PieceRef piece, string defId, int payoutBia)
        {
            Piece = piece;
            DefId = defId;
            PayoutBia = payoutBia;
        }

        /// <summary>The piece that left the run.</summary>
        public PieceRef Piece { get; }

        /// <summary>Its permanent data id.</summary>
        public string DefId { get; }

        /// <summary>
        /// Bia the altar pays back — <c>economy.sell_back_percent</c> of the piece's
        /// standard price, rounded down. Stage 05 does not own the wallet; the Run flow
        /// credits this number (AGENTS rule 7 keeps the wallet out of the bag rules).
        /// </summary>
        public int PayoutBia { get; }

        /// <inheritdoc />
        public override string ToString() => "recycle " + DefId + " +" + PayoutBia + " bia";
    }

    /// <summary>
    /// The command surface for editing a <see cref="Backpack"/> at Camp.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every command is atomic and typed (AGENTS rule 7): it validates the whole
    /// change, and only then touches state, so a rejection — including a rejected
    /// <c>Undo</c> — leaves the backpack byte-identical. Each accepted command first
    /// pushes the previous state to the undo stack, and <c>Reset</c> returns to the
    /// snapshot taken when Camp was entered ("คืนสภาพ"), both stored as MessagePack
    /// bytes through <see cref="IBackpackSnapshotCodec"/> rather than as a second
    /// in-memory model that could drift.
    /// </para>
    /// <para>
    /// <b>Lock</b> ("ล็อกย่าม") refuses every edit, including undo, so a stray drag
    /// during a battle cannot rearrange a build.
    /// </para>
    /// </remarks>
    public sealed class BackpackEditor
    {
        /// <summary>How many undo steps are kept. Older snapshots are dropped.</summary>
        public const int DefaultUndoDepth = 64;

        private readonly Backpack _backpack;
        private readonly IGameConfig _config;
        private readonly IBackpackSnapshotCodec _codec;
        private readonly List<byte[]> _undo = new List<byte[]>();
        private readonly int _maxUndoDepth;
        private byte[] _campEntry;

        /// <summary>Creates an editor and records the current state as the Camp-entry snapshot.</summary>
        public BackpackEditor(Backpack backpack, IGameConfig config, IBackpackSnapshotCodec codec,
            int maxUndoDepth = DefaultUndoDepth)
        {
            _backpack = backpack ?? throw new ArgumentNullException(nameof(backpack));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _codec = codec ?? throw new ArgumentNullException(nameof(codec));

            if (maxUndoDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxUndoDepth), "Keep at least one undo step.");
            }

            _maxUndoDepth = maxUndoDepth;
            _campEntry = EncodeCurrent();
        }

        /// <summary>The state being edited.</summary>
        public Backpack Backpack => _backpack;

        /// <summary>True while edits are refused.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>True when <see cref="Undo"/> would do something.</summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>How many undo steps are held.</summary>
        public int UndoDepth => _undo.Count;

        /// <summary>
        /// Records the current state as the one <see cref="Reset"/> returns to, and
        /// clears the undo history. Called when the run enters Camp.
        /// </summary>
        public void MarkCampEntry()
        {
            _campEntry = EncodeCurrent();
            _undo.Clear();
        }

        /// <summary>Locks or unlocks editing ("ล็อกย่าม"). Not undoable, and always allowed.</summary>
        public Result SetLocked(bool locked)
        {
            IsLocked = locked;
            return Result.Ok();
        }

        /// <summary>Restores the state captured by <see cref="MarkCampEntry"/>.</summary>
        public Result Reset()
        {
            if (IsLocked)
            {
                return Result.Fail(InventoryError.Locked, ErrorKind.InvalidState);
            }

            Apply(Decode(_campEntry));
            _undo.Clear();
            return Result.Ok();
        }

        /// <summary>Undoes the last accepted edit.</summary>
        public Result Undo()
        {
            if (IsLocked)
            {
                return Result.Fail(InventoryError.Locked, ErrorKind.InvalidState);
            }

            if (_undo.Count == 0)
            {
                return Result.Fail(InventoryError.NothingToUndo, ErrorKind.InvalidState);
            }

            byte[] previous = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            Apply(Decode(previous));
            return Result.Ok();
        }

        /// <summary>
        /// Places or moves a piece at <paramref name="origin"/> with
        /// <paramref name="rotation"/>, whether it currently sits in the chest or on
        /// the board.
        /// </summary>
        /// <remarks>
        /// One command covers both "ย้ายของขึ้นย่าม" and "ย้ายในย่าม" because the board
        /// rules are the same for both; the difference is only which cell the piece
        /// came from. A bag move additionally has to leave every item on an ActiveSlot.
        /// </remarks>
        public Result Move(PieceRef piece, CellOffset origin, Rotation rotation)
        {
            if (IsLocked)
            {
                return Result.Fail(InventoryError.Locked, ErrorKind.InvalidState);
            }

            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                return Result.Fail(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            Footprint shape = _backpack.ShapeAt(instance, rotation);

            if (instance is BagPieceInstance bag)
            {
                if (!_backpack.Board.CanPlaceBag(bag.InstanceId, shape, origin, out Error error))
                {
                    return Result.Fail(error);
                }

                if (bag.Zone == PieceZone.Board
                    && _backpack.FindStrandedItem(bag.InstanceId, shape, origin) is not null)
                {
                    return Result.Fail(InventoryError.ItemStranded, ErrorKind.Conflict);
                }

                PushUndo();
                _backpack.PlaceOnBoard(bag, origin, rotation);
                return Result.Ok();
            }

            if (!_backpack.Board.CanPlaceItem(instance.InstanceId, shape, origin, out Error itemError))
            {
                return Result.Fail(itemError);
            }

            PushUndo();
            _backpack.PlaceOnBoard(instance, origin, rotation);
            return Result.Ok();
        }

        /// <summary>
        /// Turns a placed piece one quarter turn clockwise (GDD: right click or R), in
        /// place. Four calls return it to its original orientation.
        /// </summary>
        public Result Rotate(PieceRef piece)
        {
            if (IsLocked)
            {
                return Result.Fail(InventoryError.Locked, ErrorKind.InvalidState);
            }

            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                return Result.Fail(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            if (instance.Zone != PieceZone.Board)
            {
                return Result.Fail(InventoryError.NotOnBoard, ErrorKind.InvalidState);
            }

            Rotation turned = instance.Rotation.Clockwise();
            Footprint shape = _backpack.ShapeAt(instance, turned);

            if (instance is BagPieceInstance bag)
            {
                if (!_backpack.Board.CanPlaceBag(bag.InstanceId, shape, bag.Origin, out Error error))
                {
                    return Result.Fail(error);
                }

                if (_backpack.FindStrandedItem(bag.InstanceId, shape, bag.Origin) is not null)
                {
                    return Result.Fail(InventoryError.ItemStranded, ErrorKind.Conflict);
                }

                PushUndo();
                _backpack.PlaceOnBoard(bag, bag.Origin, turned);
                return Result.Ok();
            }

            if (!_backpack.Board.CanPlaceItem(instance.InstanceId, shape, instance.Origin, out Error itemError))
            {
                return Result.Fail(itemError);
            }

            PushUndo();
            _backpack.PlaceOnBoard(instance, instance.Origin, turned);
            return Result.Ok();
        }

        /// <summary>
        /// Moves a board piece into the chest. Refused when the chest is full, and for a
        /// bag, when the move would leave an item without an ActiveSlot.
        /// </summary>
        public Result TransferToChest(PieceRef piece)
        {
            if (IsLocked)
            {
                return Result.Fail(InventoryError.Locked, ErrorKind.InvalidState);
            }

            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                return Result.Fail(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            if (instance.Zone != PieceZone.Board)
            {
                return Result.Fail(InventoryError.WrongZone, ErrorKind.InvalidState);
            }

            if (_backpack.Chest.IsFull)
            {
                return Result.Fail(InventoryError.ChestFull, ErrorKind.Insufficient);
            }

            if (instance is BagPieceInstance bag
                && _backpack.FindStrandedItem(bag.InstanceId, null, default) is not null)
            {
                return Result.Fail(InventoryError.ItemStranded, ErrorKind.Conflict);
            }

            PushUndo();
            _backpack.StoreInChest(instance);
            return Result.Ok();
        }

        /// <summary>
        /// Merges <paramref name="source"/> into <paramref name="target"/>: same item id
        /// and same tier becomes the next tier, up to <c>constant.max_tier</c>. The
        /// survivor keeps its exact placement, so the footprint never changes.
        /// </summary>
        public Result<MergeOutcome> Merge(PieceRef source, PieceRef target)
        {
            if (IsLocked)
            {
                return Result.Fail<MergeOutcome>(InventoryError.Locked, ErrorKind.InvalidState);
            }

            if (!_backpack.TryGetPiece(source, out PieceInstance? sourceInstance) || sourceInstance is null)
            {
                return Result.Fail<MergeOutcome>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            if (!_backpack.TryGetPiece(target, out PieceInstance? targetInstance) || targetInstance is null)
            {
                return Result.Fail<MergeOutcome>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            if (sourceInstance is not ItemInstance sourceItem || targetInstance is not ItemInstance targetItem)
            {
                return Result.Fail<MergeOutcome>(InventoryError.MergeNotItem, ErrorKind.Validation);
            }

            if (sourceItem.InstanceId == targetItem.InstanceId)
            {
                return Result.Fail<MergeOutcome>(InventoryError.MergeMismatch, ErrorKind.Validation);
            }

            if (!string.Equals(sourceItem.ItemId, targetItem.ItemId, StringComparison.Ordinal)
                || sourceItem.Tier != targetItem.Tier)
            {
                return Result.Fail<MergeOutcome>(InventoryError.MergeMismatch, ErrorKind.Validation);
            }

            if (targetItem.Tier >= _backpack.MaxTier)
            {
                return Result.Fail<MergeOutcome>(InventoryError.MergeMaxTier, ErrorKind.InvalidState);
            }

            PushUndo();
            _backpack.RemovePiece(sourceItem);
            _backpack.RaiseTier(targetItem, targetItem.Tier + 1);
            return Result.Ok(new MergeOutcome(targetItem, source));
        }

        /// <summary>
        /// Removes a piece from the run — board or chest — and reports the bia the altar
        /// pays back.
        /// </summary>
        /// <remarks>
        /// A bag may only be recycled when nothing is left stranded on it. The payout is
        /// the <b>standard</b> (tier-1) price times <c>economy.sell_back_percent</c>,
        /// rounded down; the data table has no per-tier price, which is written down as a
        /// gap rather than invented here.
        /// </remarks>
        public Result<RecycleOutcome> Recycle(PieceRef piece)
        {
            if (IsLocked)
            {
                return Result.Fail<RecycleOutcome>(InventoryError.Locked, ErrorKind.InvalidState);
            }

            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                return Result.Fail<RecycleOutcome>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            if (instance is BagPieceInstance bag
                && _backpack.FindStrandedItem(bag.InstanceId, null, default) is not null)
            {
                return Result.Fail<RecycleOutcome>(InventoryError.ItemStranded, ErrorKind.Conflict);
            }

            if (!_backpack.TryGetBasePrice(instance, out int price))
            {
                return Result.Fail<RecycleOutcome>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            int payout = (price * _config.Economy.SellBackPercent) / 100;

            PushUndo();
            _backpack.RemovePiece(instance);
            return Result.Ok(new RecycleOutcome(piece, instance.DefId, payout));
        }

        private void PushUndo()
        {
            _undo.Add(EncodeCurrent());
            if (_undo.Count > _maxUndoDepth)
            {
                _undo.RemoveAt(0);
            }
        }

        private byte[] EncodeCurrent() => _codec.Encode(_backpack.CaptureSnapshot());

        private BackpackSnapshot Decode(byte[] bytes) => _codec.Decode(bytes);

        private void Apply(BackpackSnapshot snapshot) => _backpack.RestoreSnapshot(snapshot);
    }
}
