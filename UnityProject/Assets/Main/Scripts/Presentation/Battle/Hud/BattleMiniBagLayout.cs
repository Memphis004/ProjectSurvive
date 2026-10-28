using System.Collections.Generic;
using System.Globalization;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// The mini bag's placeholder colours: the panel, the bag's own cells, the box of each
    /// category of item, and the two cooldown tones.
    /// </summary>
    /// <remarks>
    /// Cosmetic only — nothing here is a balance number and nothing reads it back (AGENTS rule 5
    /// keeps balance in <c>data/</c>). The category colours are a <i>presentation</i> mapping from
    /// <see cref="ItemCategory"/>, the same five categories the จัดย่าม panel names, so a player
    /// recognizes a weapon's box on the mini bag from the camp screen.
    /// </remarks>
    public static class MiniBagPalette
    {
        /// <summary>
        /// The panel behind the bag. Deliberately more transparent than the Battle HUD's other four
        /// panels (0.92): the bag sits over the arena and must not hide the fight behind a fifth
        /// near-opaque slab, while still separating its cells from the floor (Stage 08i).
        /// </summary>
        /// <remarks>
        /// Darker than the 08c value (0.07, 0.08, 0.11), and the reason is measured rather than
        /// aesthetic: at 0.72 alpha the panel's own colour is only a third of the composite, so a
        /// dark backdrop is what lets the cloth above it read. Over the arena's bright tone the first
        /// capture of this stage put the cloth at **1.38 : 1** against the panel and 1.22 : 1 against
        /// the floor — the bag was harder to see than the panel under it. See <see cref="Bag"/>.
        /// </remarks>
        public static readonly Color Backdrop = new Color(0.04f, 0.05f, 0.07f, 0.72f);

        /// <summary>
        /// A bag cell: the rag the ย่าม is made of — a tan cloth, opaque, and the brightest surface
        /// inside the panel so the bag's footprint is the first thing the eye finds.
        /// </summary>
        /// <remarks>
        /// Chosen against four measurements the capture made on the first cut of this stage: cloth
        /// against the panel **2.14 : 1**, cloth against an empty cell **2.05 : 1**, the weakest item
        /// box against the cloth **1.64 : 1** (the offering's gold, the closest category colour to a
        /// tan) and a cooldown plate against the item under it **3.7 : 1**. Lighter cloth buys the
        /// first two numbers and spends the third: at 0.52 the offering box drops to 1.02 : 1, which
        /// is why the cloth stops here. <code>The_bags_cloth_reads_against_its_panel_and_its_empty_cells</code>
        /// holds all three.
        /// </remarks>
        public static readonly Color Bag = new Color(0.38f, 0.32f, 0.22f, 1f);

        /// <summary>
        /// A cell inside the bag's bounding box that the bag does not cover — the bite of an L-shaped
        /// basket.
        /// </summary>
        /// <remarks>
        /// Darker than the cloth but lighter than the composite panel, so the missing corner reads as
        /// a <i>cell that is not cloth</i> rather than as a hole in the panel.
        /// </remarks>
        public static readonly Color EmptyCell = new Color(0.13f, 0.14f, 0.17f, 0.92f);

        /// <summary>The plate drawn over a piece whose cooldown is still running.</summary>
        public static readonly Color Cooldown = new Color(0.02f, 0.03f, 0.05f, 0.78f);

        /// <summary>The faint blue tone of a piece that is ready but cannot pay for its cast.</summary>
        public static readonly Color ManaShort = new Color(0.30f, 0.52f, 0.86f, 0.42f);

        /// <summary>Outline of a piece that can cast.</summary>
        public static readonly Color CasterEdge = new Color(0.92f, 0.80f, 0.32f, 1f);

        /// <summary>Colour of the tier number.</summary>
        public static readonly Color TierText = new Color(0.98f, 0.97f, 0.94f, 1f);

        /// <summary>The box colour of a piece whose category the tables do not name.</summary>
        public static readonly Color Unknown = new Color(0.45f, 0.45f, 0.48f, 0.95f);

        /// <summary>The box colour for an item of <paramref name="category"/>.</summary>
        public static Color Category(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.ArcaneWeapon:
                    return new Color(0.72f, 0.36f, 0.30f, 0.95f);
                case ItemCategory.Charm:
                    return new Color(0.42f, 0.56f, 0.72f, 0.95f);
                case ItemCategory.SpellTome:
                    return new Color(0.52f, 0.44f, 0.70f, 0.95f);
                case ItemCategory.Offering:
                    return new Color(0.70f, 0.62f, 0.32f, 0.95f);
                case ItemCategory.Familiar:
                    return new Color(0.36f, 0.62f, 0.50f, 0.95f);
                default:
                    return Unknown;
            }
        }
    }

    /// <summary>
    /// One board piece as the mini bag draws it: which cells of the bag's own box it covers, and
    /// the colour and number of its box.
    /// </summary>
    /// <remarks>
    /// A piece covers a set of cells and may be L-shaped; the box here is its <b>bounding
    /// rectangle</b> inside the bag's box, which is what a map this size can draw. The cells it
    /// really covers are the bag's business, not the HUD's.
    /// </remarks>
    public readonly struct MiniBagPiece
    {
        /// <summary>Creates one drawable piece.</summary>
        public MiniBagPiece(
            int instanceId, PieceKind kind, string defId, int column, int row, int columns, int rows,
            int cells, int tier, bool casts, Color colour)
        {
            InstanceId = instanceId;
            Kind = kind;
            DefId = defId;
            Column = column;
            Row = row;
            Columns = columns;
            Rows = rows;
            Cells = cells;
            Tier = tier;
            Casts = casts;
            Colour = colour;

            // Built once, when the floor starts — never per frame, which is what keeps the hot
            // path free of string work (AGENTS rule 8).
            TierText = tier.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Board instance id — the key a frame's cooldown entry carries.</summary>
        public int InstanceId { get; }

        /// <summary>Bag or item.</summary>
        public PieceKind Kind { get; }

        /// <summary>The permanent definition id (<c>item.mor_knife</c>).</summary>
        public string DefId { get; }

        /// <summary>Column inside the bag's own box, 0-based.</summary>
        public int Column { get; }

        /// <summary>Row inside the bag's own box, 0-based.</summary>
        public int Row { get; }

        /// <summary>Width of the box, in cells.</summary>
        public int Columns { get; }

        /// <summary>Height of the box, in cells.</summary>
        public int Rows { get; }

        /// <summary>How many cells the footprint really covers (less than the box for an L shape).</summary>
        public int Cells { get; }

        /// <summary>Tier of this instance.</summary>
        public int Tier { get; }

        /// <summary>True when the loadout resolved this piece into a weapon or a familiar.</summary>
        public bool Casts { get; }

        /// <summary>The box's colour, from the piece's category.</summary>
        public Color Colour { get; }

        /// <summary>The tier number as text, formatted once when the floor starts.</summary>
        public string TierText { get; }
    }

    /// <summary>
    /// The mini bag of one floor: the bag's own bounding box, the size of a cell, and the pieces
    /// inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A read model, not a rule. It is built from the <b>same</b> run and loadout the battle was
    /// started from — the bag the player built and the
    /// <see cref="CombatLoadoutSnapshot"/> resolved out of it — so the HUD cannot disagree with
    /// the fight about what is on the board, and nothing here recomputes a placement, a synergy or
    /// a shape: <see cref="Backpack.ShapeAt"/> and the piece's own <c>Origin</c> are asked
    /// (ADR 0024).
    /// </para>
    /// <para>
    /// The box is the <b>bag's</b> footprint, never the whole 9×7 board: a player who covers four
    /// cells with one bag sees four cells.
    /// </para>
    /// </remarks>
    public sealed class MiniBagLayout
    {
        /// <summary>Nothing on the board: an empty panel.</summary>
        public static readonly MiniBagLayout Empty = new MiniBagLayout(
            0, 0, 0f, new MiniBagPiece[0], new bool[0]);

        private MiniBagLayout(
            int columns, int rows, float cellSize,
            IReadOnlyList<MiniBagPiece> pieces, IReadOnlyList<bool> bagCells)
        {
            Columns = columns;
            Rows = rows;
            CellSize = cellSize;
            Pieces = pieces;
            _bagCells = bagCells;
        }

        private readonly IReadOnlyList<bool> _bagCells;

        /// <summary>Columns of the bag's own bounding box.</summary>
        public int Columns { get; }

        /// <summary>Rows of the bag's own bounding box.</summary>
        public int Rows { get; }

        /// <summary>
        /// Side of one cell, in pixels.
        /// </summary>
        /// <remarks>
        /// The grid's own origin is the content area's <b>top-left corner</b> (Stage 08d): cell
        /// ((column, row)) is drawn at <c>(column * CellSize, -(row * CellSize))</c> from it. The
        /// layout therefore needs no separate offset — a centred grid was what put the ย่าม in the
        /// middle of the panel instead of under its caption.
        /// </remarks>
        public float CellSize { get; }

        /// <summary>Every piece on the bag, bags included.</summary>
        public IReadOnlyList<MiniBagPiece> Pieces { get; }

        /// <summary>Width of the grid, in pixels.</summary>
        public float Width => Columns * CellSize;

        /// <summary>Height of the grid, in pixels.</summary>
        public float Height => Rows * CellSize;

        /// <summary>How many pieces the bag holds (0 when nothing is on the board).</summary>
        public int PieceCount => Pieces.Count;

        /// <summary>True when there is a bag on the board to draw.</summary>
        public bool HasBag => Columns > 0 && Rows > 0;

        /// <summary>
        /// True when the bag's own footprint covers this cell of its box — the cloth a player sees
        /// behind the boxes. Read from the bag's cells rather than from a piece's bounding
        /// rectangle, so an L-shaped ย่าม keeps its bite.
        /// </summary>
        public bool IsBagCell(int column, int row)
        {
            if (column < 0 || row < 0 || column >= Columns || row >= Rows)
            {
                return false;
            }

            int index = (row * Columns) + column;
            return index < _bagCells.Count && _bagCells[index];
        }

        /// <summary>How many of the pieces the loadout turned into casters.</summary>
        public int CasterCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Pieces.Count; i++)
                {
                    if (Pieces[i].Casts)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Creates the layout from a bag, its resolved loadout and the tables.</summary>
        public static MiniBagLayout Build(
            Backpack bag, CombatLoadoutSnapshot loadout, IGameConfig config,
            float contentWidth, float contentHeight) =>
            MiniBagLayoutBuilder.Build(bag, loadout, config, contentWidth, contentHeight);

        /// <summary>Creates a layout directly (used by the builder and by tests).</summary>
        internal static MiniBagLayout Of(
            int columns, int rows, float cellSize,
            IReadOnlyList<MiniBagPiece> pieces, IReadOnlyList<bool> bagCells) =>
            new MiniBagLayout(columns, rows, cellSize, pieces, bagCells);
    }

    /// <summary>
    /// Builds a <see cref="MiniBagLayout"/>: the bag's bounding box, a cell size that fits the
    /// panel it is drawn in, and one box per piece.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every number here is a <i>drawing</i> number: the cell size is chosen so the bag's box fits
    /// the content area it was handed, and the panel that area came from is anchored to the canvas
    /// at a quarter of its width, so "ไม่เกินราว 1/4 ของความกว้างจอ" is a property of the scene
    /// (measured by <c>UiCaptureTool</c> at 1920×1080 and 1280×720) rather than of this file.
    /// </para>
    /// <para>
    /// It reads the bag through <see cref="Backpack.Bags"/>, <see cref="Backpack.Items"/> and the
    /// pieces' own <c>CellAt</c> — the same board the <c>LoadoutResolver</c> read — and the tables
    /// only for a piece's category. Whether a piece <i>casts</i> is not decided here: it is
    /// <see cref="CombatLoadoutSnapshot"/> that answers, so the plate on the mini bag and the
    /// cooldown entry on the frame can only ever agree.
    /// </para>
    /// </remarks>
    public static class MiniBagLayoutBuilder
    {
        /// <summary>Smallest cell the mini bag will draw, in pixels.</summary>
        public const float MinimumCellSize = 16f;

        /// <summary>Largest cell the mini bag will draw, in pixels.</summary>
        public const float MaximumCellSize = 64f;

        /// <summary>Builds the layout for one floor.</summary>
        /// <param name="bag">The player's bag at the moment the floor departed (never null in play).</param>
        /// <param name="loadout">The resolved loadout the battle is running.</param>
        /// <param name="config">The tables, for each piece's category.</param>
        /// <param name="contentWidth">Width of the area the grid may use, in pixels.</param>
        /// <param name="contentHeight">Height of the area the grid may use, in pixels.</param>
        public static MiniBagLayout Build(
            Backpack bag, CombatLoadoutSnapshot loadout, IGameConfig config,
            float contentWidth, float contentHeight)
        {
            if (bag is null || config is null)
            {
                return MiniBagLayout.Empty;
            }

            int minColumn = int.MaxValue;
            int minRow = int.MaxValue;
            int maxColumn = int.MinValue;
            int maxRow = int.MinValue;

            IReadOnlyList<BagPieceInstance> bags = bag.Bags;
            for (int i = 0; i < bags.Count; i++)
            {
                BagPieceInstance piece = bags[i];
                if (piece.Zone != PieceZone.Board)
                {
                    continue;
                }

                for (int cell = 0; cell < piece.CellCount; cell++)
                {
                    CellOffset at = piece.CellAt(cell);
                    minColumn = at.X < minColumn ? at.X : minColumn;
                    minRow = at.Y < minRow ? at.Y : minRow;
                    maxColumn = at.X > maxColumn ? at.X : maxColumn;
                    maxRow = at.Y > maxRow ? at.Y : maxRow;
                }
            }

            if (maxColumn < minColumn)
            {
                // No bag on the board: there is nothing the mini bag could draw a box for.
                return MiniBagLayout.Empty;
            }

            int columns = (maxColumn - minColumn) + 1;
            int rows = (maxRow - minRow) + 1;

            var bagCells = new bool[columns * rows];
            for (int i = 0; i < bags.Count; i++)
            {
                BagPieceInstance piece = bags[i];
                if (piece.Zone != PieceZone.Board)
                {
                    continue;
                }

                for (int cell = 0; cell < piece.CellCount; cell++)
                {
                    CellOffset at = piece.CellAt(cell);
                    int column = at.X - minColumn;
                    int row = at.Y - minRow;
                    if (column >= 0 && row >= 0 && column < columns && row < rows)
                    {
                        bagCells[(row * columns) + column] = true;
                    }
                }
            }

            float cellSize = Fit(contentWidth, contentHeight, columns, rows);

            var pieces = new List<MiniBagPiece>(columns * rows);
            AddBags(bag, loadout, pieces, minColumn, minRow);
            AddItems(bag, loadout, config, pieces, minColumn, minRow);

            Sort(pieces);

            // The grid hugs the content area's top-left corner (Stage 08d), so the first box sits
            // exactly under the caption's left edge. Whatever room the bag does not use is left on
            // the right and at the bottom; a smaller bag never drifts towards the middle of a panel
            // that is a quarter of the screen wide. Cell size is still the fit computed above, so a
            // resize re-fits the bag rather than clipping it.
            return MiniBagLayout.Of(columns, rows, cellSize, pieces, bagCells);
        }

        /// <summary>
        /// The side of one cell: the largest square that fits the box in the area it was handed,
        /// bounded so a one-cell bag is not drawn four hundred pixels wide and a nine-cell one is
        /// still a number a player can read.
        /// </summary>
        public static float Fit(float contentWidth, float contentHeight, int columns, int rows)
        {
            if (columns <= 0 || rows <= 0 || contentWidth <= 0f || contentHeight <= 0f)
            {
                return MinimumCellSize;
            }

            float byWidth = contentWidth / columns;
            float byHeight = contentHeight / rows;
            float fit = byWidth < byHeight ? byWidth : byHeight;

            return Mathf.Clamp(fit, MinimumCellSize, MaximumCellSize);
        }

        private static void AddBags(
            Backpack bag, CombatLoadoutSnapshot loadout, List<MiniBagPiece> into,
            int minColumn, int minRow)
        {
            IReadOnlyList<BagPieceInstance> bags = bag.Bags;
            for (int i = 0; i < bags.Count; i++)
            {
                BagPieceInstance piece = bags[i];
                if (piece.Zone != PieceZone.Board)
                {
                    continue;
                }

                Add(
                    into, piece.Kind, piece.InstanceId, piece.DefId, piece, loadout,
                    MiniBagPalette.Bag, minColumn, minRow);
            }
        }

        private static void AddItems(
            Backpack bag, CombatLoadoutSnapshot loadout, IGameConfig config, List<MiniBagPiece> into,
            int minColumn, int minRow)
        {
            IReadOnlyList<ItemInstance> items = bag.Items;
            for (int index = 0; index < items.Count; index++)
            {
                ItemInstance piece = items[index];
                if (piece.Zone != PieceZone.Board)
                {
                    continue;
                }

                Color colour = config.TryGetItem(piece.DefId, out ItemConfig item)
                    ? MiniBagPalette.Category(item.Category)
                    : MiniBagPalette.Unknown;

                Add(into, piece.Kind, piece.InstanceId, piece.DefId, piece, loadout, colour, minColumn, minRow);
            }
        }

        private static void Add(
            List<MiniBagPiece> into, PieceKind kind, int instanceId, string defId, PieceInstance piece,
            CombatLoadoutSnapshot loadout, Color colour, int minColumn, int minRow)
        {
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            for (int cell = 0; cell < piece.CellCount; cell++)
            {
                CellOffset at = piece.CellAt(cell);
                left = at.X < left ? at.X : left;
                top = at.Y < top ? at.Y : top;
                right = at.X > right ? at.X : right;
                bottom = at.Y > bottom ? at.Y : bottom;
            }

            into.Add(new MiniBagPiece(
                instanceId,
                kind,
                defId,
                left - minColumn,
                top - minRow,
                (right - left) + 1,
                (bottom - top) + 1,
                piece.CellCount,
                piece.Tier,
                Casts(loadout, instanceId),
                colour));
        }

        /// <summary>
        /// True when the resolved loadout turned this board piece into a caster — the <b>same</b>
        /// answer the frame's cooldown list is keyed by, so a piece with a plate is exactly a piece
        /// the battle can cast with.
        /// </summary>
        public static bool Casts(CombatLoadoutSnapshot loadout, int instanceId)
        {
            if (loadout is null || instanceId <= 0)
            {
                return false;
            }

            IReadOnlyList<WeaponLoadout> weapons = loadout.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].InstanceId == instanceId)
                {
                    return true;
                }
            }

            IReadOnlyList<SummonLoadout> summons = loadout.Summons;
            for (int i = 0; i < summons.Count; i++)
            {
                if (summons[i].InstanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Draws pieces in a stable order — by row, then by column, then by instance id — so two
        /// runs of one floor paint the same pixels and the boxes of two pieces never swap places
        /// from frame to frame.
        /// </summary>
        private static void Sort(List<MiniBagPiece> pieces)
        {
            pieces.Sort(Compare);
        }

        private static int Compare(MiniBagPiece a, MiniBagPiece b)
        {
            if (a.Row != b.Row)
            {
                return a.Row < b.Row ? -1 : 1;
            }

            if (a.Column != b.Column)
            {
                return a.Column < b.Column ? -1 : 1;
            }

            if (a.InstanceId != b.InstanceId)
            {
                return a.InstanceId < b.InstanceId ? -1 : 1;
            }

            return 0;
        }
    }
}
