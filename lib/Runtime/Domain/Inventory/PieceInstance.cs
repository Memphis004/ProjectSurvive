using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// One owned piece on the board or in the chest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Identity is split in two, on purpose.</b> <see cref="DefId"/> is the
    /// permanent, data-owned definition (<c>item.mor_knife</c>, <c>bag.raw_cotton_bag</c>)
    /// and many instances can share it. <see cref="InstanceId"/> is this run's
    /// handle: unique, never reused, and the thing every command addresses. Merging
    /// is the case that makes the split matter — <c>Merge</c> needs one of two
    /// instances of the same <see cref="DefId"/> to disappear, which is impossible to
    /// express if the item <i>is</i> its def id, and a save file that stored only def
    /// ids could not tell a tier-3 weapon from three tier-1 ones.
    /// </para>
    /// <para>
    /// Placement is stored as cells + orientation, never as a fixed sprite: an
    /// instance carries its <i>rotated</i> <see cref="Shape"/> (so placement checks
    /// are a lookup rather than a rotation per query), plus the origin it is drawn
    /// from when it is on the board.
    /// </para>
    /// </remarks>
    public abstract class PieceInstance
    {
        /// <summary>Value of <see cref="ChestSlot"/> for a piece that is not in the chest.</summary>
        public const int NoChestSlot = -1;

        /// <summary>Creates an instance. Called by <see cref="Backpack"/> as it hands out ids.</summary>
        protected PieceInstance(PieceKind kind, int instanceId, string defId, int tier, Footprint shape)
        {
            if (instanceId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceId), "Instance ids start at 1.");
            }

            Kind = kind;
            InstanceId = instanceId;
            DefId = defId ?? throw new ArgumentNullException(nameof(defId));
            Tier = tier;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Rotation = Rotation.Degrees0;
            Zone = PieceZone.Chest;
            Origin = default;
            ChestSlot = NoChestSlot;
        }

        /// <summary>Which table <see cref="DefId"/> comes from.</summary>
        public PieceKind Kind { get; }

        /// <summary>Run-unique handle. Never reused, starts at 1.</summary>
        public int InstanceId { get; }

        /// <summary>The permanent definition id authored in <c>data/</c>.</summary>
        public string DefId { get; }

        /// <summary>Tier of this instance (1..<c>constant.max_tier</c>). Raised by <c>Merge</c>.</summary>
        public int Tier { get; internal set; }

        /// <summary>The shape at the current <see cref="Rotation"/>, normalised.</summary>
        public Footprint Shape { get; internal set; }

        /// <summary>Orientation of <see cref="Shape"/> inside its <see cref="Footprint"/>.</summary>
        public Rotation Rotation { get; internal set; }

        /// <summary>Board or chest.</summary>
        public PieceZone Zone { get; internal set; }

        /// <summary>Top-left board cell the footprint hangs from; only valid when <see cref="Zone"/> is <see cref="PieceZone.Board"/>.</summary>
        public CellOffset Origin { get; internal set; }

        /// <summary>Chest slot index; only valid when <see cref="Zone"/> is <see cref="PieceZone.Chest"/>, otherwise -1.</summary>
        public int ChestSlot { get; internal set; }

        /// <summary>A handle to this instance.</summary>
        public PieceRef Ref => new PieceRef(Kind, InstanceId);

        /// <summary>How many board cells this piece covers.</summary>
        public int CellCount => Shape.CellCount;

        /// <summary>The board cell of the <paramref name="index"/>-th covered cell.</summary>
        public CellOffset CellAt(int index)
        {
            CellOffset local = Shape.Cells[index];
            return new CellOffset(Origin.X + local.X, Origin.Y + local.Y);
        }

        /// <summary>True when this piece covers <paramref name="cell"/> while on the board.</summary>
        public bool Occupies(CellOffset cell)
        {
            if (Zone != PieceZone.Board)
            {
                return false;
            }

            int dx = cell.X - Origin.X;
            int dy = cell.Y - Origin.Y;
            return dx >= 0 && dy >= 0 && Shape.Contains(new CellOffset(dx, dy));
        }

        /// <summary>
        /// True when the two pieces share a board edge (4 directions, no diagonals).
        /// </summary>
        /// <remarks>
        /// Counts the <i>pair</i>, not the contacts: a piece lying along two of the
        /// other's cells still answers true once, which is what the combo rule
        /// "สองชิ้นที่ติดกันหลายช่องนับความสัมพันธ์ครั้งเดียว" needs.
        /// </remarks>
        public bool IsAdjacentTo(PieceInstance other)
        {
            if (other is null || Zone != PieceZone.Board || other.Zone != PieceZone.Board)
            {
                return false;
            }

            int selfCells = CellCount;
            int otherCells = other.CellCount;
            for (int i = 0; i < selfCells; i++)
            {
                CellOffset a = CellAt(i);
                for (int j = 0; j < otherCells; j++)
                {
                    CellOffset b = other.CellAt(j);
                    int dx = a.X - b.X;
                    int dy = a.Y - b.Y;
                    if ((dx == 0 && (dy == 1 || dy == -1)) || (dy == 0 && (dx == 1 || dx == -1)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>True when the two pieces have at least one board row in common.</summary>
        public bool SharesRowWith(PieceInstance other)
        {
            if (other is null || Zone != PieceZone.Board || other.Zone != PieceZone.Board)
            {
                return false;
            }

            int selfCells = CellCount;
            int otherCells = other.CellCount;
            for (int i = 0; i < selfCells; i++)
            {
                int y = CellAt(i).Y;
                for (int j = 0; j < otherCells; j++)
                {
                    if (other.CellAt(j).Y == y)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>True when the two pieces have at least one board column in common.</summary>
        public bool SharesColumnWith(PieceInstance other)
        {
            if (other is null || Zone != PieceZone.Board || other.Zone != PieceZone.Board)
            {
                return false;
            }

            int selfCells = CellCount;
            int otherCells = other.CellCount;
            for (int i = 0; i < selfCells; i++)
            {
                int x = CellAt(i).X;
                for (int j = 0; j < otherCells; j++)
                {
                    if (other.CellAt(j).X == x)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Developer-facing representation. Do not show it to a player.</summary>
        public override string ToString() =>
            Kind + "#" + InstanceId + "(" + DefId + " t" + Tier + ")@" + Zone + " " + Origin + "/" + Rotation;
    }

    /// <summary>
    /// A bag piece instance. Placing it opens <see cref="ActiveSlot"/> cells that
    /// items may occupy.
    /// </summary>
    public sealed class BagPieceInstance : PieceInstance
    {
        internal BagPieceInstance(int instanceId, string bagId, int tier, Footprint shape)
            : base(PieceKind.Bag, instanceId, bagId, tier, shape)
        {
        }

        /// <summary>The permanent bag definition id (<c>bag.raw_cotton_bag</c>).</summary>
        public string BagId => DefId;
    }

    /// <summary>
    /// An item instance (weapon, charm, tome, offering or familiar).
    /// </summary>
    /// <remarks>
    /// <see cref="ItemId"/> is the permanent data id; <see cref="PieceInstance.InstanceId"/>
    /// is this run's instance. Merging two instances of the same <see cref="ItemId"/>
    /// raises <see cref="PieceInstance.Tier"/> on the survivor.
    /// </remarks>
    public sealed class ItemInstance : PieceInstance
    {
        private readonly List<string> _variantIds = new List<string>(4);

        internal ItemInstance(int instanceId, string itemId, int tier, Footprint shape)
            : base(PieceKind.Item, instanceId, itemId, tier, shape)
        {
        }

        /// <summary>The permanent item definition id (<c>item.mor_knife</c>).</summary>
        public string ItemId => DefId;

        /// <summary>Tags the synergy rules match on, straight from <c>item.csv</c>.</summary>
        public IReadOnlyList<string> Tags { get; internal set; } = Array.Empty<string>();

        /// <summary>
        /// The upgrade variant chosen at <b>each completed merge step</b>, indexed by
        /// step: entry <c>i</c> is the choice made by the merge that produced tier
        /// <c>i + 2</c>, so a tier-3 item has two of them (Stage 09f: "เก็บ variantId ของ
        /// แต่ละขั้น").
        /// </summary>
        /// <remarks>
        /// <para>
        /// A step that had no chooser stores <see cref="NoVariant"/> rather than being
        /// skipped, which is what keeps the entry at its step's index: the 2 → 3 merge of
        /// a piece whose 1 → 2 merge offered nothing still has a step-1 slot to inherit
        /// from (ADR 0021).
        /// </para>
        /// <para>
        /// There is no tier here on purpose: the modifiers of every step apply, so a tier-3
        /// item is the sum of two choices rather than one. The loadout resolver reads the
        /// whole list.
        /// </para>
        /// </remarks>
        public IReadOnlyList<string> VariantIds => _variantIds;

        /// <summary>The value stored for a step that had no offer to choose from.</summary>
        public const string NoVariant = "";

        /// <summary>How many merge steps this instance has been through.</summary>
        public int VariantStepCount => _variantIds.Count;

        /// <summary>Replaces the whole step history (snapshot restore).</summary>
        internal void SetVariants(IReadOnlyList<string> variantIds)
        {
            _variantIds.Clear();

            if (variantIds is null)
            {
                return;
            }

            for (int i = 0; i < variantIds.Count; i++)
            {
                _variantIds.Add(variantIds[i] ?? NoVariant);
            }
        }

        /// <summary>Appends the choice of one merge step (merge, and nothing else).</summary>
        internal void AddVariant(string variantId) => _variantIds.Add(variantId ?? NoVariant);
    }
}
