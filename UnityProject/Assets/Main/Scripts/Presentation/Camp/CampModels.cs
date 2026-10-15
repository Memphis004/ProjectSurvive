using System;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The จัดย่าม panel's own controls, named so a test can ask where one is on screen.
    /// </summary>
    /// <remarks>
    /// The backup buttons of Stage 09 work item 2 ("หมุนด้วย R หรือคลิกขวา (มีปุ่ม UI สำรอง)"), the
    /// restore and lock buttons, and the จัดย่าม open/close pair. A browser test cannot press a
    /// keyboard key and a button the same way, so it needs to be able to click the real control.
    /// </remarks>
    public enum CampBagButton
    {
        Open = 0,
        Close = 1,
        Rotate = 2,
        Undo = 3,
        Reset = 4,
        Lock = 5,

        /// <summary>ล็อกย่ามตอนคืนสภาพ — a checkbox, so the report names where its box is.</summary>
        LockRestore = 6,

        /// <summary>ไปชั้นถัดไป — the panel's own way out of camp.</summary>
        NextFloor = 7,
    }

    /// <summary>
    /// The named regions of the จัดย่าม panel, so the layout report can publish a rectangle for each
    /// and a test can check that no two of them are on top of each other (Stage 09b work item B).
    /// </summary>
    public enum CampBagSurface
    {
        /// <summary>The 9×7 placement area.</summary>
        Board = 0,

        /// <summary>The chest panel's background.</summary>
        ChestPanel = 1,

        /// <summary>The chest's slot grid.</summary>
        Chest = 2,

        /// <summary>The incense pot (ถวายคืน).</summary>
        Altar = 3,

        /// <summary>The instruction scroll.</summary>
        Instructions = 4,

        /// <summary>The bottom-left block: กลับ, ไปชั้นถัดไป and the HP/weight/bia readouts.</summary>
        Stats = 5,
    }

    /// <summary>
    /// One piece as the bag screen draws it: identity, where it is, and the shape it
    /// currently occupies.
    /// </summary>
    /// <remarks>
    /// A plain value, rebuilt only when a command moved something. The view reads it and
    /// assigns; it decides nothing — <see cref="Shape"/> is the Domain's footprint for the
    /// piece's <b>current</b> rotation, so the cells a view paints are the cells the rules
    /// validated.
    /// </remarks>
    public readonly struct CampPieceModel : IEquatable<CampPieceModel>
    {
        /// <summary>Captures one piece.</summary>
        public CampPieceModel(
            PieceRef piece, string defId, string name, string label, int tier, PieceKind kind, PieceZone zone,
            CellOffset origin, Rotation rotation, int chestSlot, Footprint shape)
        {
            Piece = piece;
            DefId = defId ?? string.Empty;
            Name = name ?? string.Empty;
            Label = label ?? string.Empty;
            Tier = tier;
            Kind = kind;
            Zone = zone;
            Origin = origin;
            Rotation = rotation;
            ChestSlot = chestSlot;
            Shape = shape;
        }

        /// <summary>The run's handle for the piece.</summary>
        public PieceRef Piece { get; }

        /// <summary>Permanent data id (<c>item.mor_knife</c>).</summary>
        public string DefId { get; }

        /// <summary>Localized display name.</summary>
        public string Name { get; }

        /// <summary>
        /// The short caption painted on the piece's rectangle (name and tier), composed by the
        /// presenter so that the view has nothing to format.
        /// </summary>
        public string Label { get; }

        /// <summary>1..<c>constant.max_tier</c>.</summary>
        public int Tier { get; }

        /// <summary>Bag or item.</summary>
        public PieceKind Kind { get; }

        /// <summary>Board or chest.</summary>
        public PieceZone Zone { get; }

        /// <summary>Top-left board cell (meaningless in the chest).</summary>
        public CellOffset Origin { get; }

        /// <summary>Orientation (meaningless in the chest).</summary>
        public Rotation Rotation { get; }

        /// <summary>Chest slot, or <see cref="PieceInstance.NoChestSlot"/>.</summary>
        public int ChestSlot { get; }

        /// <summary>The footprint at <see cref="Rotation"/>.</summary>
        public Footprint Shape { get; }

        /// <summary>True for a bag piece.</summary>
        public bool IsBag => Kind == PieceKind.Bag;

        /// <summary>True when the piece is on the board.</summary>
        public bool IsOnBoard => Zone == PieceZone.Board;

        /// <inheritdoc />
        public bool Equals(CampPieceModel other) =>
            Piece == other.Piece && Tier == other.Tier && Zone == other.Zone
            && Origin == other.Origin && Rotation == other.Rotation && ChestSlot == other.ChestSlot
            && string.Equals(DefId, other.DefId, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CampPieceModel other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Piece.GetHashCode() * 397) ^ (Tier * 31) ^ (int)Zone ^ (int)Rotation;

        /// <inheritdoc />
        public override string ToString() => Name + " t" + Tier + " (" + Piece + ")";
    }

    /// <summary>One of the four shop slots as the panel draws it.</summary>
    public readonly struct CampShopSlotModel
    {
        /// <summary>Captures one slot.</summary>
        public CampShopSlotModel(
            int index, string pieceId, string name, string priceLabel, int price, PieceKind kind,
            bool isEmpty, bool isPurchased, bool isLocked, bool canAfford)
        {
            Index = index;
            PieceId = pieceId ?? string.Empty;
            Name = name ?? string.Empty;
            PriceLabel = priceLabel ?? string.Empty;
            Price = price;
            Kind = kind;
            IsEmpty = isEmpty;
            IsPurchased = isPurchased;
            IsLocked = isLocked;
            CanAfford = canAfford;
        }

        /// <summary>Panel position, 0-based.</summary>
        public int Index { get; }

        /// <summary>Offered piece id, or empty.</summary>
        public string PieceId { get; }

        /// <summary>Localized display name, or empty.</summary>
        public string Name { get; }

        /// <summary>The price as it is shown, formatted by the presenter.</summary>
        public string PriceLabel { get; }

        /// <summary>Standard price in bia.</summary>
        public int Price { get; }

        /// <summary>Bag or item.</summary>
        public PieceKind Kind { get; }

        /// <summary>Holds nothing to rent.</summary>
        public bool IsEmpty { get; }

        /// <summary>Already rented; such a slot never refills.</summary>
        public bool IsPurchased { get; }

        /// <summary>Pinned across a re-roll.</summary>
        public bool IsLocked { get; }

        /// <summary>True when the purse can pay <see cref="Price"/>.</summary>
        public bool CanAfford { get; }

        /// <summary>True when the rent button should be live.</summary>
        public bool CanRent => !IsEmpty && !IsPurchased && CanAfford;
    }

    /// <summary>
    /// One item that rides with a dragged bag, as the screen draws its ghost: the item, the board
    /// cell its top-left corner would land on, and the size of the footprint it would land with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <i>plan</i>, not the gesture. It is the run's own answer for the cell the pointer is over
    /// (<c>RunSession.CollectCarried</c>) and it is the same plan the accepted command applies, which
    /// is why the ghost can never show an item landing where the command would refuse to put it —
    /// the picture and the verdict come from one rule (AGENTS rule 10).
    /// </para>
    /// <para>
    /// A bag brings along every item whose <i>every</i> cell lies on that bag; an item lying across
    /// two bags does not ride at all and the whole command is refused instead (ADR 0020). The width
    /// and height are the rider's footprint <b>at</b> <see cref="Origin"/>'s placement, so the view
    /// never has to turn a shape itself.
    /// </para>
    /// </remarks>
    public readonly struct CampCarryGhost : IEquatable<CampCarryGhost>
    {
        /// <summary>Captures one rider's landing place.</summary>
        public CampCarryGhost(PieceRef piece, CellOffset origin, int width, int height)
        {
            Piece = piece;
            Origin = origin;
            Width = width;
            Height = height;
        }

        /// <summary>The item that rides along.</summary>
        public PieceRef Piece { get; }

        /// <summary>The top-left board cell it would land on.</summary>
        public CellOffset Origin { get; }

        /// <summary>Columns its footprint covers once it lands.</summary>
        public int Width { get; }

        /// <summary>Rows its footprint covers once it lands.</summary>
        public int Height { get; }

        /// <inheritdoc />
        public bool Equals(CampCarryGhost other) =>
            Piece == other.Piece && Origin == other.Origin && Width == other.Width && Height == other.Height;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CampCarryGhost other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Piece.GetHashCode() * 397) ^ (Origin.X * 31) ^ Origin.Y;

        /// <inheritdoc />
        public override string ToString() => Piece + " -> " + Origin;
    }

    /// <summary>
    /// What the drag is doing right now: which piece, where it would land, what the run says about
    /// that, and which items would ride along.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A struct with value equality on purpose. It is assigned on every pointer move, and
    /// <c>ReactiveProperty&lt;T&gt;</c> compares with <c>EqualityComparer&lt;T&gt;.Default</c>:
    /// without <see cref="IEquatable{T}"/> that comparison boxes both sides, which is a heap
    /// allocation per pointer event — the one thing the Stage 09 acceptance measures
    /// ("ไม่มี GC spike ระหว่างลาก").
    /// </para>
    /// <para>
    /// <see cref="Origin"/>/<see cref="Rotation"/> are what the ghost would occupy, and come
    /// from the same <c>DropPreview</c> call the drop itself will use.
    /// </para>
    /// <para>
    /// <see cref="Carry"/> is the other half of a bag's ghost (Stage 09f / ADR 0020): the items that
    /// would come along, each with where it would land. It travels in this one value rather than in a
    /// channel of its own so that the items' ghosts and the bag's are drawn from one push and can
    /// never be a frame apart, and it is an array of value types compared element by element — an
    /// unchanged plan republishes nothing.
    /// </para>
    /// </remarks>
    public readonly struct CampDragState : IEquatable<CampDragState>
    {
        private readonly CampCarryGhost[] _carry;

        /// <summary>Captures the drag.</summary>
        public CampDragState(
            bool isActive, PieceRef piece, BagDropZone zone, CellOffset origin, Rotation rotation,
            BagDropAction action, bool isValid, int bia, PieceRef target, string errorCode,
            CampCarryGhost[] carry)
        {
            IsActive = isActive;
            Piece = piece;
            Zone = zone;
            Origin = origin;
            Rotation = rotation;
            Action = action;
            IsValid = isValid;
            Bia = bia;
            Target = target;
            ErrorCode = errorCode ?? string.Empty;
            _carry = carry;
        }

        /// <summary>A drag that is not happening.</summary>
        public static CampDragState None => default;

        /// <summary>True while a piece is being dragged.</summary>
        public bool IsActive { get; }

        /// <summary>The piece under the pointer.</summary>
        public PieceRef Piece { get; }

        /// <summary>Which surface the pointer is over.</summary>
        public BagDropZone Zone { get; }

        /// <summary>The cell the piece would be placed at (its top-left cell).</summary>
        public CellOffset Origin { get; }

        /// <summary>The rotation this drag would place with.</summary>
        public Rotation Rotation { get; }

        /// <summary>What releasing here would do.</summary>
        public BagDropAction Action { get; }

        /// <summary>Whether releasing here would be accepted.</summary>
        public bool IsValid { get; }

        /// <summary>Bia the altar would pay, when <see cref="Action"/> is <see cref="BagDropAction.Recycle"/>.</summary>
        public int Bia { get; }

        /// <summary>The piece a merge would fold into, so the screen can highlight it.</summary>
        public PieceRef Target { get; }

        /// <summary>The code a refusal would carry; empty when valid.</summary>
        public string ErrorCode { get; }

        /// <summary>How many items would ride along with the dragged bag.</summary>
        public int CarryCount => _carry == null ? 0 : _carry.Length;

        /// <summary>The riders, in the order the bag's own items are held.</summary>
        public CampCarryGhost[] Carry => _carry;

        /// <summary>The rider at <paramref name="index"/>.</summary>
        public CampCarryGhost CarryAt(int index) => _carry[index];

        /// <inheritdoc />
        public bool Equals(CampDragState other)
        {
            if (IsActive != other.IsActive || Piece != other.Piece || Zone != other.Zone
                || Origin != other.Origin || Rotation != other.Rotation || Action != other.Action
                || IsValid != other.IsValid || Bia != other.Bia || Target != other.Target
                || !string.Equals(ErrorCode, other.ErrorCode, StringComparison.Ordinal)
                || CarryCount != other.CarryCount)
            {
                return false;
            }

            // Element by element: the plan is part of the drag, so a bag that moved to the same cell
            // with the same riders draws the same picture and must not republish.
            for (int i = 0; i < CarryCount; i++)
            {
                if (!_carry[i].Equals(other._carry[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CampDragState other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            (Piece.GetHashCode() * 397) ^ (int)Zone ^ (int)Action ^ (IsValid ? 7 : 0) ^ (int)Rotation
            ^ (CarryCount * 17);

        /// <inheritdoc />
        public override string ToString() =>
            IsActive
                ? "drag " + Piece + " -> " + Zone + " (" + Action + ", " + CarryCount + " rider(s))"
                : "no drag";
    }

    /// <summary>
    /// How one working combo is drawn on the board.
    /// </summary>
    /// <remarks>
    /// The stage's rule is that a <b>line</b> means one thing only: the same-line rule that runs
    /// from a ตำรา to each อาวุธอาคม it empowers. Everything else the Core evaluation reports is
    /// drawn differently on purpose — the bag-area bonus marks the cell it reached (a line from a
    /// bag to an item would say the bag is a "partner" on the board, which it is not), and the
    /// three adjacency rules are only ever highlighted while the pointer is on one of their pieces.
    /// </remarks>
    public enum CampLinkStyle
    {
        /// <summary>A line, drawn from the piece that provides the condition to the piece that receives it.</summary>
        Line = 0,

        /// <summary>A small badge on the receiving piece's cell, never a line.</summary>
        Badge = 1,
    }

    /// <summary>
    /// One working combo, resolved by <c>SynergyEvaluator</c> into instance ids and classified by
    /// <see cref="CampPresenter"/> into how the screen draws it.
    /// </summary>
    /// <remarks>
    /// The <i>pair</i> is Core's (the evaluator already decided which piece provided the condition
    /// and which one received it); the <i>style</i> is a drawing decision, which is why it is made
    /// here and not in the rules engine.
    /// </remarks>
    public readonly struct CampLink : IEquatable<CampLink>
    {
        /// <summary>Captures one drawn combo.</summary>
        public CampLink(int sourceInstanceId, int targetInstanceId, CampLinkStyle style)
        {
            SourceInstanceId = sourceInstanceId;
            TargetInstanceId = targetInstanceId;
            Style = style;
        }

        /// <summary>Instance id of the piece that provided the condition.</summary>
        public int SourceInstanceId { get; }

        /// <summary>Instance id of the piece the bonus landed on (the one a badge is drawn on).</summary>
        public int TargetInstanceId { get; }

        /// <summary>How the screen draws it.</summary>
        public CampLinkStyle Style { get; }

        /// <inheritdoc />
        public bool Equals(CampLink other) =>
            SourceInstanceId == other.SourceInstanceId
            && TargetInstanceId == other.TargetInstanceId
            && Style == other.Style;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is CampLink other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            (SourceInstanceId * 397) ^ (TargetInstanceId * 31) ^ (int)Style;

        /// <inheritdoc />
        public override string ToString() =>
            Style + " " + SourceInstanceId + "->" + TargetInstanceId;
    }

    /// <summary>
    /// The bag screen's whole picture: every piece, the cells a combo connects, the combos to
    /// draw, and the drag in progress.
    /// </summary>
    /// <remarks>
    /// Replaced wholesale when a command moved the bag ("คำนวณใหม่เมื่อชุดอุปกรณ์เปลี่ยนเท่านั้น"),
    /// which is why the view can re-render from scratch here and never per frame. The drag is
    /// the one field that changes during a gesture, and it is a value type for that reason.
    /// </remarks>
    public sealed class CampBagScreen
    {
        /// <summary>An empty screen.</summary>
        public static CampBagScreen Empty { get; } = new CampBagScreen(
            0, 0, 0, false, false, new CampPieceModel[0], new CellOffset[0], new CampLink[0], 0);

        /// <summary>Captures the screen.</summary>
        public CampBagScreen(
            int boardWidth, int boardHeight, int chestSlots, bool isLocked, bool canUndo,
            CampPieceModel[] pieces, CellOffset[] highlight, CampLink[] links, int revision)
        {
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            ChestSlots = chestSlots;
            IsLocked = isLocked;
            CanUndo = canUndo;
            Pieces = pieces;
            Highlight = highlight;
            Links = links;
            Revision = revision;
        }

        /// <summary>Board columns (<c>constant.board_width</c>).</summary>
        public int BoardWidth { get; }

        /// <summary>Board rows (<c>constant.board_height</c>).</summary>
        public int BoardHeight { get; }

        /// <summary>Chest capacity (<c>economy.chest_slots</c>).</summary>
        public int ChestSlots { get; }

        /// <summary>True while every edit is refused ("ล็อกย่าม").</summary>
        public bool IsLocked { get; }

        /// <summary>True when there is something to undo.</summary>
        public bool CanUndo { get; }

        /// <summary>Every piece the run holds, bags first then items, in creation order.</summary>
        public CampPieceModel[] Pieces { get; }

        /// <summary>Cells a working combo connects, plus the piece the pointer is over.</summary>
        public CellOffset[] Highlight { get; }

        /// <summary>
        /// Every combo the screen draws as a <b>permanent</b> mark, as pairs and a style: a line for
        /// the ตำรา → อาวุธอาคม rule and a badge for the bag-area bonus.
        /// </summary>
        /// <remarks>
        /// Only the two styles that are permanent are here. The three adjacency rules are shown by
        /// <see cref="Highlight"/> while the pointer is on one of their pieces and are drawn nowhere
        /// else, which is the stage's rule ("ไม่วาดเส้นถาวร ... ไฮไลต์คู่ที่เกี่ยวข้องเฉพาะตอน hover
        /// หรือ drag"). Replaced with the layout, not per frame.
        /// </remarks>
        public CampLink[] Links { get; }

        /// <summary>Bumps whenever the pieces or their placement changed.</summary>
        public int Revision { get; }
    }
}
