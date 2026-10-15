using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// One item that travels with a bag (ไอเทมที่ติดไปกับย่าม) and the placement it lands on:
    /// a handle, the top-left board cell, and the orientation in the bag's new frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <b>planned</b> placement, not a placement: the value is produced by reading the board
    /// (<see cref="Backpack.CollectCarried"/>) and is applied only if the whole command is accepted.
    /// <c>BackpackEditor.CanPlace</c> fills the caller's list with the same plan the command then
    /// applies, which is what lets a drag draw the items where they <i>would</i> land and be wrong
    /// nowhere: the preview and the command read one plan.
    /// </para>
    /// <para>
    /// Kept as a value with the piece's own rotation rather than a package of final cells, because
    /// the shape is a function of the def id and the rotation and is re-derived on the way in
    /// (ADR 0009) — a carried cell set would be a second source of truth.
    /// </para>
    /// </remarks>
    public readonly struct BagCarry
    {
        /// <summary>Captures one carried item.</summary>
        public BagCarry(PieceRef piece, CellOffset origin, Rotation rotation)
        {
            Piece = piece;
            Origin = origin;
            Rotation = rotation;
        }

        /// <summary>The item that rides along.</summary>
        public PieceRef Piece { get; }

        /// <summary>The top-left board cell it would land on (<see cref="PieceZone.Board"/>).</summary>
        public CellOffset Origin { get; }

        /// <summary>The orientation it would have after the move — the bag's own rotation follows it.</summary>
        public Rotation Rotation { get; }

        /// <inheritdoc />
        public override string ToString() => Piece + " -> " + Origin + "/" + Rotation;
    }
}
