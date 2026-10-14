using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// A quarter-turn orientation of a placed piece (GDD v0.3: "หมุนทีละ 90°").
    /// </summary>
    /// <remarks>
    /// Four values, one per quarter turn clockwise, and the numeric value is the
    /// number of turns — so <c>(int)Rotation.Degrees90</c> is 1 and four clockwise
    /// turns return any piece to <see cref="Degrees0"/> exactly (the board is
    /// square per cell, and the footprint is re-normalised after every turn).
    /// </remarks>
    public enum Rotation
    {
        /// <summary>As authored in the data.</summary>
        Degrees0 = 0,

        /// <summary>One clockwise quarter turn.</summary>
        Degrees90 = 1,

        /// <summary>Two clockwise quarter turns.</summary>
        Degrees180 = 2,

        /// <summary>Three clockwise quarter turns (one counter-clockwise).</summary>
        Degrees270 = 3,
    }

    /// <summary>Small, allocation-free helpers over <see cref="Rotation"/>.</summary>
    public static class RotationOps
    {
        /// <summary>The next orientation clockwise; four calls return the same value.</summary>
        public static Rotation Clockwise(this Rotation rotation) => (Rotation)(((int)rotation + 1) & 3);

        /// <summary>The number of quarter turns this orientation represents (0..3).</summary>
        public static int QuarterTurns(this Rotation rotation) => (int)rotation;

        /// <summary>The angle in degrees (0, 90, 180 or 270).</summary>
        public static float Degrees(this Rotation rotation) => (int)rotation * 90f;

        /// <summary>Normalises any turn count (including negatives) into an orientation.</summary>
        public static Rotation FromQuarterTurns(int quarterTurns) => (Rotation)(((quarterTurns % 4) + 4) & 3);
    }

    /// <summary>
    /// Turns a footprint into the footprint of the same piece placed at a different
    /// orientation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rotation is <b>clockwise on screen</b>, in the board's own axes
    /// (<c>X</c> right, <c>Y</c> down, see <see cref="CellOffset"/>): a cell
    /// <c>(x, y)</c> becomes <c>(-y, x)</c> and the result is re-normalised by
    /// <see cref="Footprint.FromCells"/>. That is exactly why an asymmetric shape
    /// (the L-tetromino basket of Stage 04) must come back as a <i>different</i> cell
    /// set at 90/270 while the symmetric 3x3 bag is unchanged at every orientation.
    /// </para>
    /// <para>
    /// No allocation beyond the result: the intermediate list is sized from the
    /// source shape. This runs on a player drag, never on a battle tick.
    /// </para>
    /// </remarks>
    public static class FootprintRotation
    {
        /// <summary>The cells of <paramref name="shape"/> after <paramref name="rotation"/>.</summary>
        public static Footprint Rotate(Footprint shape, Rotation rotation)
        {
            if (shape is null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            int turns = rotation.QuarterTurns();
            if (shape.IsEmpty || turns == 0)
            {
                return shape;
            }

            IReadOnlyList<CellOffset> cells = shape.Cells;
            var turned = new CellOffset[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                turned[i] = Turn(cells[i], turns);
            }

            return Footprint.FromCells(turned);
        }

        private static CellOffset Turn(CellOffset cell, int turns)
        {
            switch (turns)
            {
                case 1:
                    return new CellOffset(-cell.Y, cell.X);
                case 2:
                    return new CellOffset(-cell.X, -cell.Y);
                case 3:
                    return new CellOffset(cell.Y, -cell.X);
                default:
                    return cell;
            }
        }
    }
}
