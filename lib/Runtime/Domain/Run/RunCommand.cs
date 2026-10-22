using System;
using ProjectSurvive.Core.Domain.Inventory;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The run-level commands a <c>ReplayLog</c> records and replays (GDD: \"ReplayLog
    /// (command + seed)\").
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the command set of <c>docs/architecture/overview.md</c> —
    /// <c>PurchaseItem</c>, <c>SellItem</c>, <c>RefreshShop</c>, <c>PlaceBag</c> /
    /// <c>MoveItem</c>, <c>RotateItem</c>, <c>MergeItems</c>, <c>StartFloor</c> — plus
    /// the camp commands with no other representation (<c>Undo</c>, <c>Reset</c>).
    /// Every recorded command carries all of its own inputs, so replaying the log in
    /// order onto a fresh run rebuilds the same state and the same
    /// <c>RunState</c> hash.
    /// </para>
    /// <para>
    /// Battle <i>outcomes</i> are deliberately absent: a floor's result is
    /// reproduced by replaying its <c>BattleReplay</c> through the same
    /// <c>IBattleSessionFactory</c> (Stage 02b), never by recording a
    /// <c>BattleResult</c> that could disagree with the simulation.
    /// </para>
    /// <para>
    /// The numbers are part of the recorded log, not of the run state hash, so a
    /// new command kind may be appended but not inserted.
    /// </para>
    /// </remarks>
    public enum RunCommandKind
    {
        /// <summary>Start the run (<c>Title</c> → Camp).</summary>
        StartRun = 0,

        /// <summary>Rent the piece in shop slot <c>Index</c>.</summary>
        Purchase = 1,

        /// <summary>Give piece <c>Piece</c> back to the altar for bia.</summary>
        Sell = 2,

        /// <summary>Lock or unlock shop slot <c>Index</c> to <c>Flag</c>.</summary>
        SetSlotLocked = 3,

        /// <summary>Re-roll the unlocked, unrented shop slots (เสี่ยงเซียมซี).</summary>
        RefreshShop = 4,

        /// <summary>Depart for the next floor (Camp → Battle).</summary>
        StartFloor = 5,

        /// <summary>Place or move piece <c>Piece</c> at (<c>X</c>, <c>Y</c>) with <c>Rotation</c>.</summary>
        MovePiece = 6,

        /// <summary>Turn piece <c>Piece</c> one quarter turn clockwise.</summary>
        RotatePiece = 7,

        /// <summary>Park board piece <c>Piece</c> in the chest.</summary>
        TransferToChest = 8,

        /// <summary>Merge <c>Piece</c> into <c>Second</c> (same id, same tier).</summary>
        MergeItems = 9,

        /// <summary>Undo the previous bag edit.</summary>
        Undo = 10,

        /// <summary>Restore the bag to its camp-entry state.</summary>
        Reset = 11,

        /// <summary>
        /// Merge <c>Piece</c> into <c>Second</c> keeping upgrade variant <c>VariantId</c>
        /// (Stage 09f). The chosen id is part of the record: a replay that picked a different
        /// variant would resolve a different loadout from the same bag shape, so the log has
        /// to carry it and not just the pair.
        /// </summary>
        ConfirmMerge = 12,

        /// <summary>
        /// Leave a running floor and return to the camp without settling it (Stage 13c-2b).
        /// </summary>
        /// <remarks>
        /// The log has to carry this one because no <c>BattleResult</c> is ever produced for an
        /// abandoned floor, so a replay cannot learn it from the battle: <c>Index</c> carries the HP
        /// the floor ended with.
        /// </remarks>
        AbandonFloor = 13,
    }

    /// <summary>
    /// One recorded run command: a kind plus every input that does not come from the
    /// seed.
    /// </summary>
    /// <remarks>
    /// A readonly struct for the same reason <c>Result</c> is one: recording is
    /// bookkeeping, not a hot path, and the log should never be the thing that
    /// boxes. <see cref="Index"/> defaults to -1 so it can never be confused with the
    /// real slot 0.
    /// </remarks>
    public readonly struct RunCommand
    {
        /// <summary>Creates a command; prefer the named factories.</summary>
        public RunCommand(
            RunCommandKind kind,
            int index = -1,
            bool flag = false,
            PieceRef piece = default,
            PieceRef second = default,
            int x = 0,
            int y = 0,
            Rotation rotation = Rotation.Degrees0,
            string variantId = "")
        {
            Kind = kind;
            Index = index;
            Flag = flag;
            Piece = piece;
            Second = second;
            X = x;
            Y = y;
            Rotation = rotation;
            VariantId = variantId ?? string.Empty;
        }

        /// <summary>Which command this is.</summary>
        public RunCommandKind Kind { get; }

        /// <summary>Shop slot index for the shop commands; -1 otherwise.</summary>
        public int Index { get; }

        /// <summary>The requested lock state for <see cref="RunCommandKind.SetSlotLocked"/>.</summary>
        public bool Flag { get; }

        /// <summary>The addressed piece (sell / move / rotate / chest / merge source).</summary>
        public PieceRef Piece { get; }

        /// <summary>The merge target; <see cref="PieceRef.None"/> otherwise.</summary>
        public PieceRef Second { get; }

        /// <summary>Board column for <see cref="RunCommandKind.MovePiece"/>.</summary>
        public int X { get; }

        /// <summary>Board row for <see cref="RunCommandKind.MovePiece"/>.</summary>
        public int Y { get; }

        /// <summary>Orientation for <see cref="RunCommandKind.MovePiece"/>.</summary>
        public Rotation Rotation { get; }

        /// <summary>The chosen upgrade variant for <see cref="RunCommandKind.ConfirmMerge"/>; empty otherwise.</summary>
        public string VariantId { get; }

        /// <summary>Records that the run started.</summary>
        public static RunCommand StartRun() => new RunCommand(RunCommandKind.StartRun);

        /// <summary>Records a shop purchase.</summary>
        public static RunCommand Purchase(int slotIndex) => new RunCommand(RunCommandKind.Purchase, slotIndex);

        /// <summary>Records an altar sale.</summary>
        public static RunCommand Sell(PieceRef piece) => new RunCommand(RunCommandKind.Sell, -1, false, piece);

        /// <summary>Records a lock toggle on one shop slot.</summary>
        public static RunCommand SetSlotLocked(int slotIndex, bool locked) =>
            new RunCommand(RunCommandKind.SetSlotLocked, slotIndex, locked);

        /// <summary>Records a shop refresh.</summary>
        public static RunCommand RefreshShop() => new RunCommand(RunCommandKind.RefreshShop);

        /// <summary>Records a departure to the next floor.</summary>
        public static RunCommand StartFloor() => new RunCommand(RunCommandKind.StartFloor);

        /// <summary>Records a bag placement or move.</summary>
        public static RunCommand MovePiece(PieceRef piece, int x, int y, Rotation rotation) =>
            new RunCommand(RunCommandKind.MovePiece, -1, false, piece, default, x, y, rotation);

        /// <summary>Records a quarter turn.</summary>
        public static RunCommand RotatePiece(PieceRef piece) =>
            new RunCommand(RunCommandKind.RotatePiece, -1, false, piece);

        /// <summary>Records a move into the chest.</summary>
        public static RunCommand TransferToChest(PieceRef piece) =>
            new RunCommand(RunCommandKind.TransferToChest, -1, false, piece);

        /// <summary>Records a merge.</summary>
        public static RunCommand MergeItems(PieceRef source, PieceRef target) =>
            new RunCommand(RunCommandKind.MergeItems, -1, false, source, target);

        /// <summary>Records a merge confirmed with an upgrade choice.</summary>
        public static RunCommand ConfirmMerge(PieceRef source, PieceRef target, string variantId) =>
            new RunCommand(RunCommandKind.ConfirmMerge, -1, false, source, target, 0, 0, Rotation.Degrees0, variantId);

        /// <summary>Records an undo.</summary>
        public static RunCommand Undo() => new RunCommand(RunCommandKind.Undo);

        /// <summary>Records a camp-reset.</summary>
        public static RunCommand Reset() => new RunCommand(RunCommandKind.Reset);

        /// <summary>Records leaving a floor for the camp, with the HP the floor ended with.</summary>
        public static RunCommand AbandonFloor(int hp) => new RunCommand(RunCommandKind.AbandonFloor, hp);

        /// <inheritdoc />
        public override string ToString()
        {
            switch (Kind)
            {
                case RunCommandKind.Purchase: return "Purchase(" + Index + ")";
                case RunCommandKind.SetSlotLocked:
                    return "SetSlotLocked(" + Index + "," + (Flag ? "on" : "off") + ")";
                case RunCommandKind.Sell: return "Sell(" + Piece + ")";
                case RunCommandKind.MovePiece:
                    return "MovePiece(" + Piece + " -> " + X + "," + Y + " " + Rotation + ")";
                case RunCommandKind.RotatePiece: return "RotatePiece(" + Piece + ")";
                case RunCommandKind.TransferToChest: return "TransferToChest(" + Piece + ")";
                case RunCommandKind.MergeItems: return "MergeItems(" + Piece + " -> " + Second + ")";
                case RunCommandKind.ConfirmMerge:
                    return "ConfirmMerge(" + Piece + " -> " + Second + " with " + VariantId + ")";
                case RunCommandKind.AbandonFloor:
                    return "AbandonFloor(at " + Index + " hp)";
                default: return Kind.ToString();
            }
        }

        /// <summary>True when the two commands would do the same thing.</summary>
        public bool Equals(RunCommand other) =>
            Kind == other.Kind && Index == other.Index && Flag == other.Flag && Piece == other.Piece
            && Second == other.Second && X == other.X && Y == other.Y && Rotation == other.Rotation
            && string.Equals(VariantId, other.VariantId, System.StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is RunCommand other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            ((int)Kind * 397) ^ (Index * 31) ^ (Flag ? 7 : 0) ^ Piece.GetHashCode()
            ^ (Second.GetHashCode() * 17) ^ (X * 13) ^ (Y * 11) ^ ((int)Rotation * 5)
            ^ (VariantId.Length * 23);

        public static bool operator ==(RunCommand left, RunCommand right) => left.Equals(right);

        public static bool operator !=(RunCommand left, RunCommand right) => !left.Equals(right);
    }
}
