using System;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// The two kinds of piece a run carries: a bag piece (which opens board cells)
    /// and an item (which must sit on them).
    /// </summary>
    /// <remarks>
    /// A bag piece is a <i>thing you own and place</i>, exactly like an item - it can
    /// be bought, stored in the chest, moved and recycled - which is why it shares
    /// the chest with items and why it has an instance id of its own. What it cannot
    /// do is act on its own: it has no tier effect and no combat behaviour.
    /// </remarks>
    public enum PieceKind
    {
        /// <summary>Not a piece (the value of <see cref="PieceRef.None"/>).</summary>
        None = 0,

        /// <summary>A bag piece (<c>data/tables/bag.csv</c>).</summary>
        Bag = 1,

        /// <summary>A non-bag item (<c>data/tables/item.csv</c>).</summary>
        Item = 2,
    }

    /// <summary>Where a piece currently is.</summary>
    public enum PieceZone
    {
        /// <summary>Laid out on the <see cref="Board"/>; its cells are meaningful.</summary>
        Board = 0,

        /// <summary>Parked in the <see cref="Chest"/>; it gives no bonus and does not act.</summary>
        Chest = 1,
    }

    /// <summary>
    /// A handle to one owned piece: a <see cref="PieceKind"/> plus the instance id.
    /// </summary>
    /// <remarks>
    /// Commands are addressed by this rather than by a raw <c>int</c> for the same
    /// reason <see cref="ProjectSurvive.Core.Domain.Combat.Battle.EntityRef"/> exists:
    /// a bag instance id and an item instance id come from one counter but mean
    /// different things, and a command that takes the wrong one must not compile away
    /// into a silent no-op. <see cref="None"/> is the all-zero value, so a default
    /// <see cref="PieceRef"/> never names a live piece.
    /// </remarks>
    public readonly struct PieceRef : IEquatable<PieceRef>
    {
        /// <summary>An invalid reference.</summary>
        public static readonly PieceRef None = default;

        /// <summary>Creates a reference to one owned piece.</summary>
        public PieceRef(PieceKind kind, int instanceId)
        {
            Kind = kind;
            InstanceId = instanceId;
        }

        /// <summary>Which table the piece comes from.</summary>
        public PieceKind Kind { get; }

        /// <summary>Instance id within the run; 0 when this is <see cref="None"/>.</summary>
        public int InstanceId { get; }

        /// <summary>True when this reference names a live piece.</summary>
        public bool IsValid => Kind != PieceKind.None && InstanceId > 0;

        /// <summary>True when this reference names a bag piece.</summary>
        public bool IsBag => Kind == PieceKind.Bag;

        /// <summary>True when this reference names an item.</summary>
        public bool IsItem => Kind == PieceKind.Item;

        /// <inheritdoc />
        public bool Equals(PieceRef other) => Kind == other.Kind && InstanceId == other.InstanceId;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is PieceRef other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => ((int)Kind * 397) ^ InstanceId;

        /// <summary>Developer-facing representation. Do not show it to a player.</summary>
        public override string ToString() => IsValid ? Kind + "#" + InstanceId : "#none";

        public static bool operator ==(PieceRef left, PieceRef right) => left.Equals(right);

        public static bool operator !=(PieceRef left, PieceRef right) => !left.Equals(right);
    }

    /// <summary>
    /// The stable, machine-readable failure codes of an inventory command
    /// (AGENTS rule 7). Player-facing text comes from localization, keyed
    /// <c>error.&lt;code&gt;</c>.
    /// </summary>
    public static class InventoryError
    {
        /// <summary>The referenced piece does not exist in this backpack.</summary>
        public const string UnknownPiece = "inventory.unknown_piece";

        /// <summary>The piece is already where the command wanted it.</summary>
        public const string WrongZone = "inventory.wrong_zone";

        /// <summary>A footprint would leave the board's width x height area.</summary>
        public const string OutOfBoard = "inventory.out_of_board";

        /// <summary>The placement overlaps another bag piece.</summary>
        public const string BagOverlap = "inventory.bag_overlap";

        /// <summary>A cell of the item is not covered by any bag (no ActiveSlot).</summary>
        public const string ItemNotCovered = "inventory.item_not_covered";

        /// <summary>The placement overlaps another item.</summary>
        public const string ItemOverlap = "inventory.item_overlap";

        /// <summary>
        /// Taking the bag off the board (into the chest, or to the altar) would leave an item
        /// lying on the bag with nothing under it.
        /// </summary>
        public const string ItemStranded = "inventory.item_stranded";

        /// <summary>
        /// An item lies across this bag and another one, so it cannot ride along; and after the
        /// change part of it would have no bag under it (Stage 09f / ADR 0020).
        /// </summary>
        public const string ItemSpansBags = "inventory.item_spans_bags";

        /// <summary>Every chest slot is taken.</summary>
        public const string ChestFull = "inventory.chest_full";

        /// <summary>The bag is locked; no edit is accepted.</summary>
        public const string Locked = "inventory.locked";

        /// <summary>There is no edit to undo.</summary>
        public const string NothingToUndo = "inventory.nothing_to_undo";

        /// <summary>Merge needs two different instances of the same piece and tier.</summary>
        public const string MergeMismatch = "inventory.merge_mismatch";

        /// <summary>Merge needs an item, not a bag piece.</summary>
        public const string MergeNotItem = "inventory.merge_not_item";

        /// <summary>The piece is already at <c>constant.max_tier</c>.</summary>
        public const string MergeMaxTier = "inventory.merge_max_tier";

        /// <summary>Rotating only means something for a piece on the board.</summary>
        public const string NotOnBoard = "inventory.not_on_board";

        /// <summary>The run does not hold that many copies of the piece ("ใช้ของไปแล้ว").</summary>
        public const string NotEnoughPieces = "inventory.not_enough_pieces";
    }
}
