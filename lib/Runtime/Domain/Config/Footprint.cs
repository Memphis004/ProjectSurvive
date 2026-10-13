using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// The set of board cells a bag piece or item occupies, normalised so that
    /// the minimum <see cref="CellOffset.X"/> / <see cref="CellOffset.Y"/> is 0.
    /// </summary>
    /// <remarks>
    /// Data authors a footprint as a list of "x,y" cells (see
    /// <c>data/Defines/schema.xml</c>), which is what makes an <b>empty</b> shape
    /// and a <b>duplicated</b> cell detectable in data rather than at runtime.
    /// Parsing never throws: <see cref="TryParse"/> returns a reason instead, so
    /// the data validator can report every bad row in one run.
    /// </remarks>
    public sealed class Footprint
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
        /// Parses footprint tokens ("0,0", "1,0", ...).
        /// </summary>
        /// <param name="tokens">Raw tokens straight from config, in authoring order.</param>
        /// <param name="footprint">The parsed shape, normalised; <see cref="Empty"/> on failure.</param>
        /// <param name="error">A machine-readable reason, or <c>null</c> on success.</param>
        public static bool TryParse(IReadOnlyList<string>? tokens, out Footprint footprint, out string? error)
        {
            footprint = Empty;
            error = null;

            if (tokens is null || tokens.Count == 0)
            {
                error = FootprintError.Empty;
                return false;
            }

            var parsed = new List<CellOffset>(tokens.Count);
            var seen = new HashSet<CellOffset>();

            for (int i = 0; i < tokens.Count; i++)
            {
                if (!TryParseCell(tokens[i], out CellOffset cell))
                {
                    error = FootprintError.MalformedCell;
                    return false;
                }

                if (!seen.Add(cell))
                {
                    error = FootprintError.DuplicateCell;
                    return false;
                }

                parsed.Add(cell);
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (CellOffset cell in parsed)
            {
                if (cell.X < minX) { minX = cell.X; }
                if (cell.Y < minY) { minY = cell.Y; }
                if (cell.X > maxX) { maxX = cell.X; }
                if (cell.Y > maxY) { maxY = cell.Y; }
            }

            var normalised = new CellOffset[parsed.Count];
            for (int i = 0; i < parsed.Count; i++)
            {
                normalised[i] = new CellOffset(parsed[i].X - minX, parsed[i].Y - minY);
            }

            footprint = new Footprint(normalised, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

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

        private static bool TryParseCell(string? token, out CellOffset cell)
        {
            cell = default;
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            int separator = token!.IndexOf(',');
            if (separator <= 0 || separator == token.Length - 1)
            {
                return false;
            }

            if (!int.TryParse(token.Substring(0, separator).Trim(), out int x))
            {
                return false;
            }

            if (!int.TryParse(token.Substring(separator + 1).Trim(), out int y))
            {
                return false;
            }

            cell = new CellOffset(x, y);
            return true;
        }
    }

    /// <summary>
    /// Reasons a footprint can be rejected. Plain constants, not an enum, so they
    /// stay stable strings when they reach the validator's report.
    /// </summary>
    public static class FootprintError
    {
        public const string Empty = "footprint.empty";
        public const string DuplicateCell = "footprint.duplicate_cell";
        public const string MalformedCell = "footprint.malformed_cell";
    }
}
