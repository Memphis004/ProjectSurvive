using System;
using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// The set of board cells a bag piece or item occupies, normalised so that
    /// the minimum <see cref="CellOffset.X"/> / <see cref="CellOffset.Y"/> is 0.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data authors a footprint as a <b>picture</b>: one element per row of the
    /// shape, <c>'X'</c> for a cell the piece covers and <c>'.'</c> for an empty
    /// one. <c>"XX.|.XX"</c> is the L-tromino, <c>"XXX|XXX|XXX"</c> is a 3x3 bag
    /// and <c>"XX"</c> is a 2x1 familiar. The shape therefore reads exactly the
    /// way it sits on the board, in the CSV and in the diff.
    /// </para>
    /// <para>
    /// Parsing never throws: <see cref="TryParse"/> returns a reason instead, so
    /// the data validator can report every bad row in one run. The result is a
    /// set of <see cref="CellOffset"/> because that is what placement, overlap and
    /// rotation (Stage 05) work on.
    /// </para>
    /// </remarks>
    public sealed class Footprint : IEquatable<Footprint>
    {
        private readonly CellOffset[] _cells;
        private readonly int _width;
        private readonly int _height;

        private Footprint(CellOffset[] cells, int width, int height)
        {
            _cells = cells;
            _width = width;
            _height = height;
        }

        public static Footprint Empty { get; } = new Footprint(new CellOffset[0], 0, 0);

        public IReadOnlyList<CellOffset> Cells => _cells;

        /// <summary>Number of columns in the normalised shape.</summary>
        public int Width => _width;

        /// <summary>Number of rows in the normalised shape.</summary>
        public int Height => _height;

        public int CellCount => _cells.Length;

        public bool IsEmpty => _cells.Length == 0;

        /// <summary>
        /// Parses footprint rows (a picture: "XX.", ".XX", ...).
        /// </summary>
        /// <param name="rows">Raw rows straight from config, top row first.</param>
        /// <param name="footprint">The parsed shape, normalised; <see cref="Empty"/> on failure.</param>
        /// <param name="error">A machine-readable reason, or <c>null</c> on success.</param>
        public static bool TryParse(IReadOnlyList<string>? rows, out Footprint footprint, out string? error)
        {
            footprint = Empty;
            error = null;

            if (rows is null || rows.Count == 0)
            {
                error = FootprintError.Empty;
                return false;
            }

            int width = -1;
            var cells = new List<CellOffset>(rows.Count * 4);

            for (int y = 0; y < rows.Count; y++)
            {
                string? row = rows[y];

                // An empty row element is a row of no cells. It still has to be
                // the same length as every other row, which is what makes a
                // ragged picture - the shape a bitmap cannot express - a data
                // error rather than a silent guess.
                if (row is null)
                {
                    error = FootprintError.MalformedCell;
                    return false;
                }

                if (width < 0)
                {
                    width = row.Length;
                }
                else if (row.Length != width)
                {
                    error = FootprintError.RaggedRow;
                    return false;
                }

                for (int x = 0; x < row.Length; x++)
                {
                    char cell = row[x];
                    if (cell == FilledCell)
                    {
                        cells.Add(new CellOffset(x, y));
                    }
                    else if (cell != EmptyCell)
                    {
                        error = FootprintError.MalformedCell;
                        return false;
                    }
                }
            }

            if (cells.Count == 0)
            {
                // A picture made only of '.', or of empty rows, covers nothing -
                // the same failure as no shape at all: it would open no ActiveSlot.
                error = FootprintError.Empty;
                return false;
            }

            // Normalise so the minimum x/y is 0, i.e. an author may draw the shape
            // anywhere in the picture and the board sees the same piece.
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (CellOffset cell in cells)
            {
                if (cell.X < minX) { minX = cell.X; }
                if (cell.Y < minY) { minY = cell.Y; }
                if (cell.X > maxX) { maxX = cell.X; }
                if (cell.Y > maxY) { maxY = cell.Y; }
            }

            var normalised = new CellOffset[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                normalised[i] = new CellOffset(cells[i].X - minX, cells[i].Y - minY);
            }

            footprint = new Footprint(normalised, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        /// <summary>
        /// Builds a footprint from an arbitrary cell set, normalised exactly the
        /// way <see cref="TryParse"/> normalises a picture.
        /// </summary>
        /// <remarks>
        /// Used by rotation (Stage 05): turning a shape 90 degrees moves the
        /// covered cells, and re-normalising is what makes the result a board
        /// shape again instead of one that drifts off its own origin. A cell is
        /// covered at most once, so duplicates are collapsed - a footprint is a
        /// <i>set</i> of cells, which is what placement, overlap and rotation all
        /// operate on. Never throws: an empty or null input gives
        /// <see cref="Empty"/>, the same "covers nothing" value a picture of dots
        /// produces.
        /// </remarks>
        public static Footprint FromCells(IReadOnlyList<CellOffset>? cells)
        {
            if (cells is null || cells.Count == 0)
            {
                return Empty;
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            var set = new HashSet<CellOffset>();
            for (int i = 0; i < cells.Count; i++)
            {
                CellOffset cell = cells[i];
                if (!set.Add(cell))
                {
                    continue;
                }

                if (cell.X < minX) { minX = cell.X; }
                if (cell.Y < minY) { minY = cell.Y; }
                if (cell.X > maxX) { maxX = cell.X; }
                if (cell.Y > maxY) { maxY = cell.Y; }
            }

            if (set.Count == 0)
            {
                return Empty;
            }

            var normalised = new CellOffset[set.Count];
            int index = 0;
            foreach (CellOffset cell in set)
            {
                normalised[index++] = new CellOffset(cell.X - minX, cell.Y - minY);
            }

            // Canonical order (top row first, left to right): two equal cell sets
            // must produce byte-identical footprints, because a rotated shape ends
            // up in a snapshot and a hash (inventory determinism).
            Array.Sort(normalised, CompareCells);

            return new Footprint(normalised, maxX - minX + 1, maxY - minY + 1);
        }

        private static int CompareCells(CellOffset left, CellOffset right) =>
            left.Y != right.Y ? left.Y.CompareTo(right.Y) : left.X.CompareTo(right.X);

        public bool Contains(CellOffset cell)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Structural equality: two footprints are the same shape when they cover the same
        /// cells in the same normalised frame.
        /// </summary>
        /// <remarks>
        /// A footprint <i>is</i> a value, and rotation makes that matter: rotating a shape
        /// produces a new instance with the same cells as another rotation would, and a
        /// checker asking "is this piece's shape the shape its rotation implies?" must not
        /// be answered by object identity.
        /// </remarks>
        public bool Equals(Footprint? other)
        {
            if (other is null || ReferenceEquals(this, other))
            {
                return ReferenceEquals(this, other);
            }

            if (_cells.Length != other._cells.Length
                || _width != other._width
                || _height != other._height)
            {
                return false;
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != other._cells[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Footprint other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ((_width * 397) ^ _height) * 397;
                for (int i = 0; i < _cells.Length; i++)
                {
                    hash = (hash * 397) ^ _cells[i].GetHashCode();
                }

                return hash;
            }
        }

        public static bool operator ==(Footprint? left, Footprint? right) =>
            left is null ? right is null : left.Equals(right);

        public static bool operator !=(Footprint? left, Footprint? right) => !(left == right);

        /// <summary>The character that marks a covered cell in a footprint picture.</summary>
        public const char FilledCell = 'X';

        /// <summary>The character that marks an empty cell in a footprint picture.</summary>
        public const char EmptyCell = '.';
    }

    /// <summary>
    /// Reasons a footprint can be rejected. Plain constants, not an enum, so they
    /// stay stable strings when they reach the validator's report.
    /// </summary>
    public static class FootprintError
    {
        /// <summary>No row at all, or a picture with no covered cell.</summary>
        public const string Empty = "footprint.empty";

        /// <summary>Rows of unequal length: the picture is not a rectangle.</summary>
        public const string RaggedRow = "footprint.ragged_row";

        /// <summary>A character that is neither 'X' nor '.'.</summary>
        public const string MalformedCell = "footprint.malformed_cell";
    }
}
