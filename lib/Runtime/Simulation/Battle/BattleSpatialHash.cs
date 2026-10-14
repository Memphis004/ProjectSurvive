using System;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// A uniform grid over the arena, rebuilt from scratch every tick by
    /// <see cref="Systems.SpatialHashSystem"/>, so collisions and target picking do
    /// not scan every ghost for every projectile.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Zero allocation.</b> The grid is one <c>int[]</c> of cell heads plus three
    /// parallel arrays, all allocated once in the constructor. <see cref="Begin"/>
    /// clears the cell heads with <see cref="Array.Clear(System.Array,int,int)"/> and
    /// rewinds the cursor; <see cref="Insert"/> links an item onto its cell's head.
    /// Nothing here grows, and nothing here is a collection type.
    /// </para>
    /// <para>
    /// <b>Determinism.</b> A cell is a singly linked list built by prepending, so the
    /// order a query visits two items in the same cell is the reverse of the insert
    /// order. That is stable for a given sequence of inserts, which is all the
    /// determinism guarantee needs (same seed, same inputs, same runtime —
    /// AGENTS rule 6). A rule that depended on "the nearest of several equidistant
    /// candidates" would be reading that order, so <see cref="TryFindNearest"/> is
    /// written to keep the earliest-inserted candidate on a tie.
    /// </para>
    /// <para>
    /// The grid covers the square that circumscribes the arena disc; an item outside
    /// it is clamped into the border cell rather than dropped, so a stray entity is
    /// still findable.
    /// </para>
    /// </remarks>
    public sealed class BattleSpatialHash
    {
        private readonly float _minX;
        private readonly float _minY;
        private readonly float _cellSize;
        private readonly int _cellsPerAxis;

        private readonly int[] _cellHead;
        private readonly int[] _next;
        private readonly int[] _entities;
        private readonly float[] _x;
        private readonly float[] _y;
        private readonly int _capacity;

        private int _count;

        /// <summary>Creates a grid for a square of <paramref name="halfExtent"/> around the origin.</summary>
        /// <param name="halfExtent">Half the edge of the covered square, in world units.</param>
        /// <param name="cellSize">Edge length of one cell, in world units.</param>
        /// <param name="capacity">How many items one build may hold.</param>
        /// <exception cref="ArgumentOutOfRangeException">An argument is not positive.</exception>
        public BattleSpatialHash(float halfExtent, float cellSize, int capacity)
        {
            if (!(halfExtent > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(halfExtent), halfExtent, "must be positive.");
            }

            if (!(cellSize > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "must be positive.");
            }

            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "must be at least 1.");
            }

            _cellSize = cellSize;
            _minX = -halfExtent;
            _minY = -halfExtent;
            _capacity = capacity;
            _cellsPerAxis = Math.Max(1, (int)Math.Ceiling((halfExtent * 2f) / cellSize));

            _cellHead = new int[_cellsPerAxis * _cellsPerAxis];
            _next = new int[capacity];
            _entities = new int[capacity];
            _x = new float[capacity];
            _y = new float[capacity];

            Begin();
        }

        /// <summary>Cells along one axis of the grid.</summary>
        public int CellsPerAxis => _cellsPerAxis;

        /// <summary>How many items the last <see cref="Insert"/> round holds.</summary>
        public int Count => _count;

        /// <summary>How many inserts the grid can hold before the extra ones are ignored.</summary>
        public int Capacity => _capacity;

        /// <summary>Empties the grid and starts a new build.</summary>
        public void Begin()
        {
            Array.Clear(_cellHead, 0, _cellHead.Length);
            _count = 0;
        }

        /// <summary>
        /// Adds one item. Ignored (and not counted) when the grid is full, so a runaway
        /// spawn cannot grow anything mid-tick.
        /// </summary>
        /// <returns>True when the item was stored.</returns>
        public bool Insert(int entity, float x, float y)
        {
            if (_count >= _capacity)
            {
                return false;
            }

            int index = _count++;
            int cell = CellIndex(x, y);

            _entities[index] = entity;
            _x[index] = x;
            _y[index] = y;
            _next[index] = _cellHead[cell];
            _cellHead[cell] = index + 1;
            return true;
        }

        /// <summary>
        /// Collects the items within <paramref name="radius"/> of a point into
        /// <paramref name="results"/>, up to its length.
        /// </summary>
        /// <returns>How many items were written.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
        public int QueryRadius(float x, float y, float radius, int[] results)
        {
            if (results is null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (_count == 0 || results.Length == 0)
            {
                return 0;
            }

            float radiusSquared = radius * radius;
            int found = 0;

            int minCellX = CellCoord(x - radius, _minX, _cellsPerAxis);
            int maxCellX = CellCoord(x + radius, _minX, _cellsPerAxis);
            int minCellY = CellCoord(y - radius, _minY, _cellsPerAxis);
            int maxCellY = CellCoord(y + radius, _minY, _cellsPerAxis);

            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                int rowBase = cellY * _cellsPerAxis;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int item = _cellHead[rowBase + cellX];
                    while (item != 0)
                    {
                        int index = item - 1;
                        float dx = _x[index] - x;
                        float dy = _y[index] - y;
                        if ((dx * dx) + (dy * dy) <= radiusSquared)
                        {
                            if (found == results.Length)
                            {
                                return found;
                            }

                            results[found++] = _entities[index];
                        }

                        item = _next[index];
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Finds the closest item to a point within <paramref name="maxRadius"/>.
        /// </summary>
        /// <returns>True when something was found.</returns>
        public bool TryFindNearest(float x, float y, float maxRadius, out int entity, out float distanceSquared)
        {
            entity = 0;
            distanceSquared = 0f;

            if (_count == 0)
            {
                return false;
            }

            float best = maxRadius * maxRadius;
            bool found = false;

            int minCellX = CellCoord(x - maxRadius, _minX, _cellsPerAxis);
            int maxCellX = CellCoord(x + maxRadius, _minX, _cellsPerAxis);
            int minCellY = CellCoord(y - maxRadius, _minY, _cellsPerAxis);
            int maxCellY = CellCoord(y + maxRadius, _minY, _cellsPerAxis);

            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                int rowBase = cellY * _cellsPerAxis;
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int item = _cellHead[rowBase + cellX];
                    while (item != 0)
                    {
                        int index = item - 1;
                        float dx = _x[index] - x;
                        float dy = _y[index] - y;
                        float candidate = (dx * dx) + (dy * dy);

                        // Strictly-better only: a tie keeps the candidate found first, which
                        // is the one inserted last. That is stable, and it is why this is not
                        // written as "<= best".
                        if (!found || candidate < best)
                        {
                            best = candidate;
                            entity = _entities[index];
                            found = true;
                        }

                        item = _next[index];
                    }
                }
            }

            distanceSquared = best;
            return found;
        }

        /// <summary>Grid cell a world position falls in (border-clamped).</summary>
        public int CellIndex(float x, float y)
        {
            int cellX = CellCoord(x, _minX, _cellsPerAxis);
            int cellY = CellCoord(y, _minY, _cellsPerAxis);
            return (cellY * _cellsPerAxis) + cellX;
        }

        /// <summary>Axis cell index of a coordinate, clamped into the grid.</summary>
        private int CellCoord(float value, float min, int cellsPerAxis)
        {
            int cell = (int)Math.Floor((value - min) / _cellSize);
            if (cell < 0)
            {
                return 0;
            }

            return cell >= cellsPerAxis ? cellsPerAxis - 1 : cell;
        }
    }
}
