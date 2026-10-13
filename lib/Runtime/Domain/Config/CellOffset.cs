using System;

namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// A cell offset relative to a placement origin, in board cells.
    /// <c>X</c> grows to the right, <c>Y</c> grows downwards, so a footprint
    /// written as "0,0|1,0|0,1" reads exactly like it is authored in data/.
    /// </summary>
    public readonly struct CellOffset : IEquatable<CellOffset>
    {
        public CellOffset(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(CellOffset other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is CellOffset other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => X + "," + Y;

        public static bool operator ==(CellOffset left, CellOffset right) => left.Equals(right);

        public static bool operator !=(CellOffset left, CellOffset right) => !left.Equals(right);
    }
}
