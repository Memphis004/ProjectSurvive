using System;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// The 9x7 placement grid (board) of GDD v0.3: the largest area a run may lay
    /// pieces out on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two independent occupancy maps, because the two layers have different rules:
    /// a bag piece covers cells and must not overlap another bag piece, while an item
    /// may only cover cells a bag already covers and must not overlap another item.
    /// An item lying over a bag is therefore <i>expected</i>, not a conflict.
    /// </para>
    /// <para>
    /// A cell covered by a bag is an <b>ActiveSlot</b> (ช่องใช้งาน) — the term the GDD
    /// and the glossary use for "a cell an item may sit on".
    /// </para>
    /// <para>
    /// The board never mutates by itself: every write is preceded by the matching
    /// <c>CanPlace*</c> query, so a command either passes both halves or changes
    /// nothing (AGENTS rule 7). See <see cref="BackpackEditor"/>.
    /// </para>
    /// </remarks>
    public sealed class Board
    {
        /// <summary>Owner id of a cell nothing covers.</summary>
        public const int NoOwner = 0;

        private readonly int[] _bagOwner;
        private readonly int[] _itemOwner;

        /// <summary>Creates an empty board.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Either dimension is below 1.</exception>
        public Board(int width, int height)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "The board needs at least one column.");
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "The board needs at least one row.");
            }

            Width = width;
            Height = height;
            _bagOwner = new int[width * height];
            _itemOwner = new int[width * height];
        }

        /// <summary>Columns (<c>constant.board_width</c>, 9).</summary>
        public int Width { get; }

        /// <summary>Rows (<c>constant.board_height</c>, 7).</summary>
        public int Height { get; }

        /// <summary>True when <paramref name="cell"/> is inside the board.</summary>
        public bool Contains(CellOffset cell) =>
            cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

        /// <summary>Instance id of the bag piece covering <paramref name="cell"/>, or 0.</summary>
        public int BagOwnerAt(CellOffset cell)
        {
            int index = Index(cell);
            return index < 0 ? NoOwner : _bagOwner[index];
        }

        /// <summary>Instance id of the item covering <paramref name="cell"/>, or 0.</summary>
        public int ItemOwnerAt(CellOffset cell)
        {
            int index = Index(cell);
            return index < 0 ? NoOwner : _itemOwner[index];
        }

        /// <summary>
        /// True when a bag piece covers <paramref name="cell"/> — i.e. the cell is an
        /// ActiveSlot an item may be placed on.
        /// </summary>
        public bool IsActiveSlot(CellOffset cell) => BagOwnerAt(cell) != NoOwner;

        /// <summary>
        /// True when a bag <i>other than</i> <paramref name="excludedBagInstanceId"/>
        /// covers <paramref name="cell"/>.
        /// </summary>
        /// <remarks>
        /// The query behind "moving this bag must not strand an item": a cell of an
        /// item stays usable after the move if another bag still covers it.
        /// </remarks>
        public bool IsActiveSlotExcept(CellOffset cell, int excludedBagInstanceId) =>
            BagOwnerAt(cell) is int owner && owner != NoOwner && owner != excludedBagInstanceId;

        /// <summary>How many cells carry a bag piece.</summary>
        public int ActiveSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _bagOwner.Length; i++)
                {
                    if (_bagOwner[i] != NoOwner)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Checks whether a bag piece may be placed at <paramref name="origin"/>
        /// with <paramref name="shape"/>.
        /// </summary>
        /// <param name="bagInstanceId">
        /// The moving bag's own id, so a move that overlaps the bag's current cells
        /// is allowed; pass 0 when placing a piece that is not on the board yet.
        /// </param>
        /// <param name="shape">The rotated footprint.</param>
        /// <param name="origin">Top-left cell the footprint hangs from.</param>
        /// <param name="error">A machine-readable reason when the answer is false.</param>
        public bool CanPlaceBag(int bagInstanceId, Footprint shape, CellOffset origin, out Error error)
        {
            if (!TryCheckInside(shape, origin, out error))
            {
                return false;
            }

            // Index-addressed, not `foreach`: the static type of `Cells` is the interface, so an
            // enumerator would be boxed on every call — and Stage 09's drag preview calls this on
            // every pointer move (AGENTS rule 8).
            for (int i = 0; i < shape.CellCount; i++)
            {
                CellOffset cell = shape.CellAt(i);
                var boardCell = new CellOffset(origin.X + cell.X, origin.Y + cell.Y);
                int owner = BagOwnerAt(boardCell);
                if (owner != NoOwner && owner != bagInstanceId)
                {
                    error = new Error(InventoryError.BagOverlap, ErrorKind.Conflict);
                    return false;
                }
            }

            error = default;
            return true;
        }

        /// <summary>
        /// Checks whether an item may be placed at <paramref name="origin"/> with
        /// <paramref name="shape"/>: every covered cell must be an ActiveSlot, and no
        /// other item may be in the way.
        /// </summary>
        /// <param name="itemInstanceId">The moving item's own id, or 0 for a new item.</param>
        public bool CanPlaceItem(int itemInstanceId, Footprint shape, CellOffset origin, out Error error)
        {
            if (!TryCheckInside(shape, origin, out error))
            {
                return false;
            }

            for (int i = 0; i < shape.CellCount; i++)
            {
                CellOffset cell = shape.CellAt(i);
                var boardCell = new CellOffset(origin.X + cell.X, origin.Y + cell.Y);

                if (BagOwnerAt(boardCell) == NoOwner)
                {
                    // GDD v0.3: an item may span several bags, but every cell it
                    // occupies has to be covered by one.
                    error = new Error(InventoryError.ItemNotCovered, ErrorKind.Conflict);
                    return false;
                }

                int owner = ItemOwnerAt(boardCell);
                if (owner != NoOwner && owner != itemInstanceId)
                {
                    error = new Error(InventoryError.ItemOverlap, ErrorKind.Conflict);
                    return false;
                }
            }

            error = default;
            return true;
        }

        /// <summary>Writes a bag piece into the occupancy map. Precondition: <see cref="CanPlaceBag"/>.</summary>
        public void OccupyBag(int bagInstanceId, Footprint shape, CellOffset origin)
        {
            for (int i = 0; i < shape.CellCount; i++)
            {
                CellOffset cell = shape.CellAt(i);
                _bagOwner[Index(new CellOffset(origin.X + cell.X, origin.Y + cell.Y))] = bagInstanceId;
            }
        }

        /// <summary>Clears a bag piece's cells. Only the cells it actually covers are touched.</summary>
        public void ReleaseBag(int bagInstanceId, Footprint shape, CellOffset origin)
        {
            for (int i = 0; i < shape.CellCount; i++)
            {
                CellOffset cell = shape.CellAt(i);
                int index = Index(new CellOffset(origin.X + cell.X, origin.Y + cell.Y));
                if (index >= 0 && _bagOwner[index] == bagInstanceId)
                {
                    _bagOwner[index] = NoOwner;
                }
            }
        }

        /// <summary>Writes an item into the occupancy map. Precondition: <see cref="CanPlaceItem"/>.</summary>
        public void OccupyItem(int itemInstanceId, Footprint shape, CellOffset origin)
        {
            for (int i = 0; i < shape.CellCount; i++)
            {
                CellOffset cell = shape.CellAt(i);
                _itemOwner[Index(new CellOffset(origin.X + cell.X, origin.Y + cell.Y))] = itemInstanceId;
            }
        }

        /// <summary>Clears an item's cells.</summary>
        public void ReleaseItem(int itemInstanceId, Footprint shape, CellOffset origin)
        {
            for (int i = 0; i < shape.CellCount; i++)
            {
                CellOffset cell = shape.CellAt(i);
                int index = Index(new CellOffset(origin.X + cell.X, origin.Y + cell.Y));
                if (index >= 0 && _itemOwner[index] == itemInstanceId)
                {
                    _itemOwner[index] = NoOwner;
                }
            }
        }

        private bool TryCheckInside(Footprint shape, CellOffset origin, out Error error)
        {
            if (shape is null || shape.IsEmpty)
            {
                // Shipped data never contains an empty footprint (the validator
                // rejects it); refusing to place one keeps the board consistent
                // instead of silently opening nothing.
                error = new Error(InventoryError.OutOfBoard, ErrorKind.Validation);
                return false;
            }

            if (origin.X < 0 || origin.Y < 0
                || origin.X + shape.Width > Width
                || origin.Y + shape.Height > Height)
            {
                error = new Error(InventoryError.OutOfBoard, ErrorKind.Validation);
                return false;
            }

            error = default;
            return true;
        }

        private int Index(CellOffset cell) =>
            Contains(cell) ? (cell.Y * Width) + cell.X : -1;
    }
}
