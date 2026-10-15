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
        /// True when <see cref="Move"/> would accept this placement. Reads only; moves
        /// nothing and records nothing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Stage 09's drag preview ("preview วางได้และวางไม่ได้ โดยไม่แก้ Domain จนผู้เล่น
        /// ยืนยัน") is a dry run of the same command, and a second implementation of
        /// "can this go here?" is exactly how a preview and a command come to disagree. The
        /// command is therefore written <i>in terms of</i> this predicate, and the predicate
        /// carries the whole rule: the piece exists, its rotated footprint is on the board,
        /// nothing it would collide with is there, and a bag move leaves every item on an
        /// <see cref="ActiveSlot"/>.
        /// </para>
        /// <para>
        /// It deliberately does <b>not</b> consult <see cref="IsLocked"/>: the lock is a policy
        /// guard on the command (so a stray drag during a battle cannot rearrange a build),
        /// not a property of the placement. A caller that wants to render a locked bag asks
        /// <see cref="IsLocked"/> separately — <c>RunSession.PreviewDrop</c> does.
        /// </para>
        /// </remarks>
        public bool CanPlace(PieceRef piece, CellOffset origin, Rotation rotation, out Error error)
        {
            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                error = new Error(InventoryError.UnknownPiece, ErrorKind.NotFound);
                return false;
            }

            Footprint shape = _backpack.ShapeAt(instance, rotation);

            if (instance is BagPieceInstance bag)
            {
                if (!_backpack.Board.CanPlaceBag(bag.InstanceId, shape, origin, out error))
                {
                    return false;
                }

                if (bag.Zone == PieceZone.Board
                    && _backpack.FindStrandedItem(bag.InstanceId, shape, origin) is not null)
                {
                    error = new Error(InventoryError.ItemStranded, ErrorKind.Conflict);
                    return false;
                }

                return true;
            }

            return _backpack.Board.CanPlaceItem(instance.InstanceId, shape, origin, out error);
        }

        /// <summary>True when <see cref="Merge"/> would accept this pair. Reads only.</summary>
        /// <remarks>
        /// Same id, same tier, both items, and below <c>constant.max_tier</c> — the whole
        /// rule the merge gesture ("วางทับเพื่อ merge") needs, asked once here so the drag
        /// preview and the command cannot drift apart.
        /// </remarks>
        public bool CanMerge(PieceRef source, PieceRef target, out Error error)
        {
            if (!_backpack.TryGetPiece(source, out PieceInstance? sourceInstance) || sourceInstance is null
                || !_backpack.TryGetPiece(target, out PieceInstance? targetInstance) || targetInstance is null)
            {
                error = new Error(InventoryError.UnknownPiece, ErrorKind.NotFound);
                return false;
            }

            if (sourceInstance is not ItemInstance sourceItem || targetInstance is not ItemInstance targetItem)
            {
                error = new Error(InventoryError.MergeNotItem, ErrorKind.Validation);
                return false;
            }

            if (sourceItem.InstanceId == targetItem.InstanceId
                || !string.Equals(sourceItem.ItemId, targetItem.ItemId, StringComparison.Ordinal)
                || sourceItem.Tier != targetItem.Tier)
            {
                error = new Error(InventoryError.MergeMismatch, ErrorKind.Validation);
                return false;
            }

            if (targetItem.Tier >= _backpack.MaxTier)
            {
                error = new Error(InventoryError.MergeMaxTier, ErrorKind.InvalidState);
                return false;
            }

            error = default;
            return true;
        }

        /// <summary>True when <see cref="TransferToChest"/> would accept this piece. Reads only.</summary>
        /// <remarks>The realistic refusal is a full chest; a bag that would strand an item is the other.</remarks>
        public bool CanTransferToChest(PieceRef piece, out Error error)
        {
            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                error = new Error(InventoryError.UnknownPiece, ErrorKind.NotFound);
                return false;
            }

            if (instance.Zone != PieceZone.Board)
            {
                error = new Error(InventoryError.WrongZone, ErrorKind.InvalidState);
                return false;
            }

            if (_backpack.Chest.IsFull)
            {
                error = new Error(InventoryError.ChestFull, ErrorKind.Insufficient);
                return false;
            }

            if (instance is BagPieceInstance bag
                && _backpack.FindStrandedItem(bag.InstanceId, null, default) is not null)
            {
                error = new Error(InventoryError.ItemStranded, ErrorKind.Conflict);
                return false;
            }

            error = default;
            return true;
        }

        /// <summary>
        /// True when <see cref="Recycle"/> would accept this piece, and what the altar would
        /// pay for it (<c>floor(base price × economy.sell_back_percent / 100)</c>). Reads only.
        /// </summary>
        /// <remarks>
        /// The payout is part of the predicate on purpose: the Stage 09 altar shows the bia a
        /// drop would pay <i>before</i> the player commits, and a number computed in a tooltip
        /// is a number the UI owns ([ADR 0002](../../../../docs/adr/0002-thin-mvp-ui-approach.md)).
        /// </remarks>
        public bool CanRecycle(PieceRef piece, out int payout, out Error error)
        {
            payout = 0;

            if (!_backpack.TryGetPiece(piece, out PieceInstance? instance) || instance is null)
            {
                error = new Error(InventoryError.UnknownPiece, ErrorKind.NotFound);
                return false;
            }

            if (instance is BagPieceInstance bag
                && _backpack.FindStrandedItem(bag.InstanceId, null, default) is not null)
            {
                error = new Error(InventoryError.ItemStranded, ErrorKind.Conflict);
                return false;
            }

            if (!_backpack.TryGetBasePrice(instance, out int price))
            {
                error = new Error(InventoryError.UnknownPiece, ErrorKind.NotFound);
                return false;
            }

            payout = (price * _config.Economy.SellBackPercent) / 100;
            error = default;
            return true;
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

            if (!CanPlace(piece, origin, rotation, out Error error))
            {
                return Result.Fail(error);
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

            if (!CanPlace(piece, instance.Origin, turned, out Error error))
            {
                return Result.Fail(error);
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

            if (!CanTransferToChest(piece, out Error error))
            {
                return Result.Fail(error);
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

            if (!CanMerge(source, target, out Error error))
            {
                return Result.Fail<MergeOutcome>(error);
            }

            // Precondition: CanMerge proved both pieces are items, so the cast holds and the
            // survivor keeps its exact placement (the footprint never changes).
            var survivor = (ItemInstance)targetInstance;

            PushUndo();
            _backpack.RemovePiece(sourceInstance);
            _backpack.RaiseTier(survivor, survivor.Tier + 1);
            return Result.Ok(new MergeOutcome(survivor, source));
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

            if (!CanRecycle(piece, out int payout, out Error error))
            {
                return Result.Fail<RecycleOutcome>(error);
            }

            PushUndo();
            _backpack.RemovePiece(instance);
            return Result.Ok(new RecycleOutcome(piece, instance.DefId, payout));
        }

        /// <summary>How many copies of <paramref name="itemId"/> the run holds, board and chest.</summary>
        public int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<ItemInstance> items = _backpack.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True when <see cref="Consume"/> would remove <paramref name="count"/> copies.</summary>
        public bool CanConsume(string itemId, int count) => count <= 0 || CountOf(itemId) >= count;

        /// <summary>
        /// Removes <paramref name="count"/> copies of an item because they were used
        /// during a floor (an offering the battle spent).
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is <b>not</b> a player edit: it is not pushed to undo (undo must not
        /// resurrect a used offering), and the bag lock — which exists to stop a stray
        /// drag during a battle — does not stop the battle reporting what it used.
        /// </para>
        /// <para>
        /// Which copy disappears is fixed: chest slots first (lowest slot), then board
        /// pieces in canonical position order, so two runs with the same history lose
        /// the same instances. Nothing is removed when the run does not hold that many.
        /// </para>
        /// </remarks>
        public Result<int> Consume(string itemId, int count)
        {
            if (count < 0)
            {
                return Result.Fail<int>(InventoryError.NotEnoughPieces, ErrorKind.Validation);
            }

            if (count == 0)
            {
                return Result.Ok(0);
            }

            if (string.IsNullOrEmpty(itemId))
            {
                return Result.Fail<int>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            var chosen = new List<ItemInstance>(count);

            for (int slot = 0; slot < _backpack.Chest.SlotCount && chosen.Count < count; slot++)
            {
                PieceRef reference = _backpack.Chest[slot];
                if (!reference.IsItem)
                {
                    continue;
                }

                if (_backpack.TryGetItem(reference.InstanceId, out ItemInstance? item)
                    && item is not null
                    && string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    chosen.Add(item);
                }
            }

            if (chosen.Count < count)
            {
                List<ItemInstance> board = _backpack.OrderedBoardItems();
                for (int i = 0; i < board.Count && chosen.Count < count; i++)
                {
                    if (string.Equals(board[i].ItemId, itemId, StringComparison.Ordinal))
                    {
                        chosen.Add(board[i]);
                    }
                }
            }

            if (chosen.Count < count)
            {
                return Result.Fail<int>(InventoryError.NotEnoughPieces, ErrorKind.Insufficient);
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                _backpack.RemovePiece(chosen[i]);
            }

            return Result.Ok(chosen.Count);
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
