using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Inventory
{
    /// <summary>
    /// Everything a run owns and where it is: the <see cref="Board"/> with its bag
    /// pieces and items, the <see cref="Chest"/>, and the piece instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This type owns <b>state</b>; <see cref="BackpackEditor"/> owns the
    /// <b>commands</b> (validation, atomicity, undo, lock). The split is what makes
    /// "a rejected command changes nothing" checkable: the editor asks this type
    /// whether a placement is legal, and only then calls a mutator here.
    /// </para>
    /// <para>
    /// Every mutator bumps <see cref="Revision"/>. The loadout is recomputed when —
    /// and only when — that number moved (GDD: "คำนวณใหม่เมื่อชุดอุปกรณ์เปลี่ยนเท่านั้น"),
    /// which is the cache key <see cref="LoadoutResolver"/> keys on.
    /// </para>
    /// <para>
    /// Nothing here reads a dictionary in iteration order: rules walk
    /// <see cref="OrderedBoardPieces"/>, which is sorted by board position. Two
    /// backpacks holding the same pieces in the same places therefore produce the same
    /// answer no matter what order they were built in (and a snapshot of one is
    /// byte-identical to a snapshot of the other).
    /// </para>
    /// </remarks>
    public sealed class Backpack
    {
        private readonly List<BagPieceInstance> _bags = new List<BagPieceInstance>();
        private readonly List<ItemInstance> _items = new List<ItemInstance>();
        private readonly Dictionary<int, BagPieceInstance> _bagsById = new Dictionary<int, BagPieceInstance>();
        private readonly Dictionary<int, ItemInstance> _itemsById = new Dictionary<int, ItemInstance>();
        private readonly IGameConfig _config;
        private int _nextInstanceId = 1;

        /// <summary>Creates an empty backpack sized from <c>constant</c> / <c>economy</c>.</summary>
        public Backpack(IGameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            Board = new Board(config.Constant.BoardWidth, config.Constant.BoardHeight);
            Chest = new Chest(config.Economy.ChestSlots);
        }

        /// <summary>The board (board_width x board_height).</summary>
        public Board Board { get; }

        /// <summary>The chest (<c>economy.chest_slots</c> slots).</summary>
        public Chest Chest { get; }

        /// <summary>Highest tier a piece may reach (<c>constant.max_tier</c>, 3).</summary>
        public int MaxTier => _config.Constant.MaxTier;

        /// <summary>
        /// Increases whenever an applied change moved a piece, changed a tier or added
        /// or removed a piece. Rejected commands leave it alone.
        /// </summary>
        public int Revision { get; private set; }

        /// <summary>Instance id that will be handed to the next created piece.</summary>
        public int NextInstanceId => _nextInstanceId;

        /// <summary>
        /// How many merges this run has confirmed (Stage 09f).
        /// </summary>
        /// <remarks>
        /// Part of the backpack's undoable state rather than a run statistic: the upgrade
        /// offer is a function of <c>(run seed, this count, item id, tier step)</c>, so
        /// <c>Undo</c> and <c>Reset</c> have to bring it back with the pieces. Otherwise
        /// undoing a merge and repeating it would draw a fresh set of choices.
        /// </remarks>
        public int MergeCount { get; private set; }

        /// <summary>Every bag piece this run owns, board and chest, in creation order.</summary>
        public IReadOnlyList<BagPieceInstance> Bags => _bags;

        /// <summary>Every item this run owns, board and chest, in creation order.</summary>
        public IReadOnlyList<ItemInstance> Items => _items;

        /// <summary>Finds a piece by handle.</summary>
        public bool TryGetPiece(PieceRef piece, out PieceInstance? instance)
        {
            if (piece.IsBag && _bagsById.TryGetValue(piece.InstanceId, out BagPieceInstance? bag))
            {
                instance = bag;
                return true;
            }

            if (piece.IsItem && _itemsById.TryGetValue(piece.InstanceId, out ItemInstance? item))
            {
                instance = item;
                return true;
            }

            instance = null;
            return false;
        }

        /// <summary>Finds a bag piece by instance id.</summary>
        public bool TryGetBag(int instanceId, out BagPieceInstance? bag) =>
            _bagsById.TryGetValue(instanceId, out bag);

        /// <summary>Finds an item by instance id.</summary>
        public bool TryGetItem(int instanceId, out ItemInstance? item) =>
            _itemsById.TryGetValue(instanceId, out item);

        /// <summary>
        /// Adds a bag piece at tier 1 and parks it in the chest (the GDD's rule for
        /// anything acquired: "ของที่ซื้อเข้าหีบก่อนเสมอ").
        /// </summary>
        public Result<PieceRef> AddBag(string bagId)
        {
            if (!_config.TryGetBag(bagId, out BagConfig definition))
            {
                return Result.Fail<PieceRef>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            int slot = Chest.FirstFreeSlot();
            if (slot == Chest.Full)
            {
                return Result.Fail<PieceRef>(InventoryError.ChestFull, ErrorKind.Insufficient);
            }

            var bag = new BagPieceInstance(_nextInstanceId++, definition.Id, definition.Tier, definition.Footprint);
            _bags.Add(bag);
            _bagsById.Add(bag.InstanceId, bag);
            StoreInChest(bag);
            return Result.Ok(bag.Ref);
        }

        /// <summary>Adds an item at tier 1 and parks it in the chest.</summary>
        public Result<PieceRef> AddItem(string itemId)
        {
            if (!_config.TryGetItem(itemId, out ItemConfig definition))
            {
                return Result.Fail<PieceRef>(InventoryError.UnknownPiece, ErrorKind.NotFound);
            }

            int slot = Chest.FirstFreeSlot();
            if (slot == Chest.Full)
            {
                return Result.Fail<PieceRef>(InventoryError.ChestFull, ErrorKind.Insufficient);
            }

            var item = new ItemInstance(_nextInstanceId++, definition.Id, definition.Tier, definition.Footprint)
            {
                Tags = definition.Tags,
            };

            _items.Add(item);
            _itemsById.Add(item.InstanceId, item);
            StoreInChest(item);
            return Result.Ok(item.Ref);
        }

        /// <summary>The unrotated data shape of a piece (bags and items live in different tables).</summary>
        public Footprint BaseShapeOf(PieceInstance piece)
        {
            if (piece is BagPieceInstance bag && _config.TryGetBag(bag.BagId, out BagConfig bagDefinition))
            {
                return bagDefinition.Footprint;
            }

            if (piece is ItemInstance item && _config.TryGetItem(item.ItemId, out ItemConfig itemDefinition))
            {
                return itemDefinition.Footprint;
            }

            return piece.Shape;
        }

        /// <summary>The shape of a piece as it would sit at <paramref name="rotation"/>.</summary>
        public Footprint ShapeAt(PieceInstance piece, Rotation rotation) =>
            FootprintRotation.Rotate(BaseShapeOf(piece), rotation);

        /// <summary>Weight of one piece in <c>data/</c> units.</summary>
        public float WeightOf(PieceInstance piece)
        {
            if (piece is BagPieceInstance bag && _config.TryGetBag(bag.BagId, out BagConfig bagDefinition))
            {
                return bagDefinition.Weight;
            }

            if (piece is ItemInstance item && _config.TryGetItem(item.ItemId, out ItemConfig itemDefinition))
            {
                return itemDefinition.Weight;
            }

            return 0f;
        }

        /// <summary>
        /// Base (tier-1) price of a piece, the number sell-back is a percentage of
        /// (<c>economy.sell_back_percent</c>).
        /// </summary>
        public bool TryGetBasePrice(PieceInstance piece, out int price)
        {
            if (piece is BagPieceInstance bag && _config.TryGetBag(bag.BagId, out BagConfig bagDefinition))
            {
                price = bagDefinition.Price;
                return true;
            }

            if (piece is ItemInstance item && _config.TryGetItem(item.ItemId, out ItemConfig itemDefinition))
            {
                price = itemDefinition.Price;
                return true;
            }

            price = 0;
            return false;
        }

        /// <summary>
        /// Every piece currently on the board, ordered by board position
        /// (row, then column, then kind and id).
        /// </summary>
        /// <remarks>
        /// The one iteration order the rules are allowed to use. Two pieces can never
        /// share a board cell, so this order is total and independent of the order the
        /// pieces were created in.
        /// </remarks>
        public List<PieceInstance> OrderedBoardPieces()
        {
            var ordered = new List<PieceInstance>(_bags.Count + _items.Count);
            for (int i = 0; i < _bags.Count; i++)
            {
                if (_bags[i].Zone == PieceZone.Board)
                {
                    ordered.Add(_bags[i]);
                }
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Zone == PieceZone.Board)
                {
                    ordered.Add(_items[i]);
                }
            }

            ordered.Sort(CompareByPosition);
            return ordered;
        }

        /// <summary>Currently placed items, ordered by board position.</summary>
        public List<ItemInstance> OrderedBoardItems()
        {
            var ordered = new List<ItemInstance>(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Zone == PieceZone.Board)
                {
                    ordered.Add(_items[i]);
                }
            }

            ordered.Sort(CompareByPosition);
            return ordered;
        }

        /// <summary>Currently placed bag pieces, ordered by board position.</summary>
        public List<BagPieceInstance> OrderedBoardBags()
        {
            var ordered = new List<BagPieceInstance>(_bags.Count);
            for (int i = 0; i < _bags.Count; i++)
            {
                if (_bags[i].Zone == PieceZone.Board)
                {
                    ordered.Add(_bags[i]);
                }
            }

            ordered.Sort(CompareByPosition);
            return ordered;
        }

        /// <summary>
        /// Total weight of everything laid out on the board, summed in canonical
        /// order (so the sum is reproducible — floating-point addition is
        /// order-dependent).
        /// </summary>
        /// <remarks>
        /// Chest contents do not count: the GDD gives chest pieces no effect, and
        /// "น้ำหนักย่าม" is the weight of the bag you carry into a floor.
        /// </remarks>
        public float BoardWeight
        {
            get
            {
                List<PieceInstance> ordered = OrderedBoardPieces();
                float total = 0f;
                for (int i = 0; i < ordered.Count; i++)
                {
                    total += WeightOf(ordered[i]);
                }

                return total;
            }
        }

        // ------------------------------------------------------------------
        // Mutators. Called by BackpackEditor only, after its own validation.
        // ------------------------------------------------------------------

        /// <summary>
        /// Places or moves a piece onto the board at <paramref name="origin"/> with
        /// <paramref name="rotation"/>, taking it out of the chest first if needed.
        /// Precondition: the matching <c>Board.CanPlace*</c> check passed.
        /// </summary>
        internal void PlaceOnBoard(PieceInstance piece, CellOffset origin, Rotation rotation)
        {
            Lift(piece);
            Settle(piece, origin, rotation);
            MarkChanged();
        }

        /// <summary>
        /// Places a bag and the items riding on it as <b>one</b> change (Stage 09f / ADR 0020).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The whole assembly is lifted before anything is put down, because a rider may legally
        /// land on a cell another moving piece was occupying (a small move overlaps the old
        /// footprint) and the occupancy maps are single-valued per cell — placing one piece at a
        /// time would read another moving piece's own cells as a collision.
        /// </para>
        /// <para>
        /// <see cref="Revision"/> moves <b>once</b>, not once per piece: the revision is the cache key
        /// of the loadout and the combo evaluation (ADR 0014 §7), and one gesture is one change. The
        /// command pushes its undo snapshot before calling this, so a single <c>Undo</c> puts the bag
        /// and every item back in one step.
        /// </para>
        /// <para>
        /// Precondition: <see cref="TryPlanBagPlacement"/> accepted this placement, so the carry list
        /// is the one it validated and nothing is checked again here.
        /// </para>
        /// </remarks>
        internal void PlaceAssembly(BagPieceInstance bag, CellOffset origin, Rotation rotation, List<BagCarry> carry)
        {
            Lift(bag);

            for (int i = 0; i < carry.Count; i++)
            {
                if (TryGetItem(carry[i].Piece.InstanceId, out ItemInstance? item) && item is not null)
                {
                    Lift(item);
                }
            }

            Settle(bag, origin, rotation);

            for (int i = 0; i < carry.Count; i++)
            {
                if (TryGetItem(carry[i].Piece.InstanceId, out ItemInstance? item) && item is not null)
                {
                    Settle(item, carry[i].Origin, carry[i].Rotation);
                }
            }

            MarkChanged();
        }

        /// <summary>Gives up a piece's own cells (or its chest slot). No revision bump.</summary>
        private void Lift(PieceInstance piece)
        {
            if (piece.Zone == PieceZone.Board)
            {
                if (piece is BagPieceInstance placedBag)
                {
                    Board.ReleaseBag(placedBag.InstanceId, placedBag.Shape, placedBag.Origin);
                }
                else
                {
                    Board.ReleaseItem(piece.InstanceId, piece.Shape, piece.Origin);
                }
            }
            else
            {
                Chest.ClearByPiece(piece.Ref);
            }
        }

        /// <summary>Writes a piece's placement into the piece and the occupancy maps. No revision bump.</summary>
        private void Settle(PieceInstance piece, CellOffset origin, Rotation rotation)
        {
            Footprint shape = ShapeAt(piece, rotation);
            piece.Shape = shape;
            piece.Rotation = rotation;
            piece.Origin = origin;
            piece.Zone = PieceZone.Board;
            piece.ChestSlot = PieceInstance.NoChestSlot;

            if (piece is BagPieceInstance bag)
            {
                Board.OccupyBag(bag.InstanceId, shape, origin);
            }
            else
            {
                Board.OccupyItem(piece.InstanceId, shape, origin);
            }
        }

        /// <summary>Parks a piece in the chest. Precondition: there is a free slot.</summary>
        internal void StoreInChest(PieceInstance piece)
        {
            if (piece.Zone == PieceZone.Board)
            {
                if (piece is BagPieceInstance bag)
                {
                    Board.ReleaseBag(bag.InstanceId, bag.Shape, bag.Origin);
                }
                else
                {
                    Board.ReleaseItem(piece.InstanceId, piece.Shape, piece.Origin);
                }
            }

            int slot = Chest.Store(piece.Ref);
            piece.Zone = PieceZone.Chest;
            piece.ChestSlot = slot;
            MarkChanged();
        }

        /// <summary>Removes a piece from the run entirely (recycling).</summary>
        internal void RemovePiece(PieceInstance piece)
        {
            if (piece.Zone == PieceZone.Board)
            {
                if (piece is BagPieceInstance bag)
                {
                    Board.ReleaseBag(bag.InstanceId, bag.Shape, bag.Origin);
                }
                else
                {
                    Board.ReleaseItem(piece.InstanceId, piece.Shape, piece.Origin);
                }
            }
            else
            {
                Chest.ClearByPiece(piece.Ref);
            }

            piece.Zone = PieceZone.Chest;
            piece.ChestSlot = PieceInstance.NoChestSlot;

            if (piece is BagPieceInstance removedBag)
            {
                _bags.Remove(removedBag);
                _bagsById.Remove(removedBag.InstanceId);
            }
            else
            {
                var removedItem = (ItemInstance)piece;
                _items.Remove(removedItem);
                _itemsById.Remove(removedItem.InstanceId);
            }

            MarkChanged();
        }

        /// <summary>Raises an item's tier (merge). Precondition: <c>newTier</c> is above the old one and at most <see cref="MaxTier"/>.</summary>
        internal void RaiseTier(ItemInstance item, int newTier)
        {
            item.Tier = newTier;
            MarkChanged();
        }

        /// <summary>Records that a merge was confirmed, and restores its counter.</summary>
        internal void RecordMerge()
        {
            MergeCount++;
            MarkChanged();
        }

        /// <summary>Restores the merge counter while restoring a snapshot.</summary>
        internal void RestoreMergeCount(int mergeCount) => MergeCount = mergeCount < 0 ? 0 : mergeCount;

        /// <summary>
        /// True when <paramref name="piece"/> would still be fully covered by ActiveSlots
        /// after the bag <paramref name="bagInstanceId"/> changed to
        /// <paramref name="newShape"/> at <paramref name="newOrigin"/>
        /// (pass an empty shape for "the bag left the board").
        /// </summary>
        /// <remarks>
        /// Used by chest / recycle of a bag — the two commands that take a bag off the board, where an
        /// item sitting on it has nowhere to go — and by a board move or turn for the items that do
        /// <i>not</i> ride along (the ones lying across this bag and another, ADR 0020): GDD v0.3
        /// refuses the whole command rather than letting an item dangle.
        /// </remarks>
        internal bool WouldStayCovered(ItemInstance piece, int bagInstanceId, Footprint? newShape, CellOffset newOrigin)
        {
            int cells = piece.CellCount;
            for (int i = 0; i < cells; i++)
            {
                if (!CoversCell(piece.CellAt(i), bagInstanceId, newShape, newOrigin))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True when <paramref name="cell"/> would be an ActiveSlot after the bag
        /// <paramref name="bagInstanceId"/> changed to <paramref name="newShape"/> at
        /// <paramref name="newOrigin"/>: another bag covers it, or the new footprint does.
        /// </summary>
        /// <remarks>
        /// The one sentence behind every coverage question, so "an item still stands on a bag" is
        /// asked the same way for a piece that stays (its own cells) and for one that rides (its
        /// mapped cells, ADR 0020).
        /// </remarks>
        internal bool CoversCell(CellOffset cell, int bagInstanceId, Footprint? newShape, CellOffset newOrigin)
        {
            if (Board.IsActiveSlotExcept(cell, bagInstanceId))
            {
                return true;
            }

            if (newShape is null || newShape.IsEmpty)
            {
                return false;
            }

            int dx = cell.X - newOrigin.X;
            int dy = cell.Y - newOrigin.Y;
            return dx >= 0 && dy >= 0 && newShape.Contains(new CellOffset(dx, dy));
        }

        /// <summary>
        /// Finds the first placed item that a bag change would leave off an ActiveSlot.
        /// </summary>
        /// <remarks>
        /// Asked by the two commands that take a bag <b>off</b> the board (into the chest, or to the
        /// altar), where every item standing on it would be left dangling — an item can never come
        /// along to the chest (Stage 09f / ADR 0020 §2).
        /// </remarks>
        internal ItemInstance? FindStrandedItem(int bagInstanceId, Footprint? newShape, CellOffset newOrigin)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance item = _items[i];
                if (item.Zone != PieceZone.Board)
                {
                    continue;
                }

                if (!WouldStayCovered(item, bagInstanceId, newShape, newOrigin))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>
        /// Works out which items ride with <paramref name="bag"/> when it is placed at
        /// (<paramref name="origin"/>, <paramref name="rotation"/>), and where each of them lands.
        /// Reads only.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The rule (ADR 0020).</b> An item rides when <i>every</i> one of its cells lies on this
        /// bag — bags never overlap, so that means this bag and no other. An item lying across this
        /// bag and a second one does not ride: part of it belongs to a bag that is staying.
        /// </para>
        /// <para>
        /// <b>The whole assembly turns as one.</b> A rider keeps its position in the bag's own frame:
        /// its cells are turned the same number of quarter turns the bag is being turned about the
        /// bag's own origin, minus the translation re-normalisation removed from the bag's shape
        /// (<see cref="FootprintRotation.Rotate(Footprint, Rotation, out CellOffset)"/>). A pure move is
        /// the zero-turn case of the same map, so "move" and "turn" cannot disagree — one function
        /// answers both, and four turns return the bag and every rider to where they started.
        /// </para>
        /// </remarks>
        internal void CollectRiders(BagPieceInstance bag, CellOffset origin, Rotation rotation, List<BagCarry> into)
        {
            into.Clear();

            if (bag.Zone != PieceZone.Board || _items.Count == 0)
            {
                // Nothing can be lying on a bag in the chest: a chest piece covers no cell.
                return;
            }

            // How far the assembly is being turned: the difference between the orientation asked for
            // and the one the bag stands in, as quarter turns clockwise.
            int turns = ((int)rotation - (int)bag.Rotation) & 3;

            Rotation turn = RotationOps.FromQuarterTurns(turns);
            FootprintRotation.Rotate(bag.Shape, turn, out CellOffset shift);

            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance item = _items[i];
                if (item.Zone != PieceZone.Board || !RidesOn(item, bag.InstanceId))
                {
                    continue;
                }

                int minX = int.MaxValue;
                int minY = int.MaxValue;

                for (int cell = 0; cell < item.CellCount; cell++)
                {
                    CellOffset board = item.CellAt(cell);
                    CellOffset rigid = FootprintRotation.Turn(
                        new CellOffset(board.X - bag.Origin.X, board.Y - bag.Origin.Y), turn);

                    int x = origin.X + rigid.X - shift.X;
                    int y = origin.Y + rigid.Y - shift.Y;

                    if (x < minX) { minX = x; }
                    if (y < minY) { minY = y; }
                }

                into.Add(new BagCarry(
                    item.Ref,
                    new CellOffset(minX, minY),
                    RotationOps.FromQuarterTurns(item.Rotation.QuarterTurns() + turns)));
            }
        }

        /// <summary>
        /// True when placing <paramref name="bag"/> at (<paramref name="origin"/>, <paramref name="rotation"/>)
        /// is legal — including what happens to the items lying on it — and fills <paramref name="carry"/>
        /// with the items that ride along and where they land. Reads only.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The bag's own footprint is not checked here; <see cref="Board.CanPlaceBag"/> already did that
        /// (it ignores the moving bag's own cells, so a placement overlapping where the bag stands is
        /// legal). This adds the two questions the presence of items raises:
        /// </para>
        /// <list type="number">
        /// <item><b>Nothing that stays behind may be left uncovered.</b> Only an item lying across this
        /// bag and another can be affected at all (a bag's new cells cannot reach across another bag),
        /// and if the cells that leave this bag's cover are not picked up by the new footprint or by
        /// the other bag, the whole command is refused with <see cref="InventoryError.ItemSpansBags"/>.
        /// </item>
        /// <item><b>Every rider must land somewhere legal</b>: inside the board, on a bag, and clear of
        /// every piece that is not moving. A rider may land on a cell another <i>moving</i> piece was
        /// occupying — the assembly is lifted whole before it is put down.
        /// </item>
        /// </list>
        /// </remarks>
        internal bool TryPlanBagPlacement(
            BagPieceInstance bag, CellOffset origin, Rotation rotation, Footprint shape, List<BagCarry> carry, out Error error)
        {
            CollectRiders(bag, origin, rotation, carry);

            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance item = _items[i];
                if (item.Zone != PieceZone.Board || IsCarried(carry, item.InstanceId))
                {
                    continue;
                }

                if (!WouldStayCovered(item, bag.InstanceId, shape, origin))
                {
                    error = new Error(InventoryError.ItemSpansBags, ErrorKind.Conflict);
                    carry.Clear();
                    return false;
                }
            }

            for (int i = 0; i < carry.Count; i++)
            {
                BagCarry rider = carry[i];
                if (!TryGetItem(rider.Piece.InstanceId, out ItemInstance? item) || item is null)
                {
                    continue;
                }

                Footprint ridden = ShapeAt(item, rider.Rotation);
                for (int cell = 0; cell < ridden.CellCount; cell++)
                {
                    CellOffset local = ridden.CellAt(cell);
                    var boardCell = new CellOffset(rider.Origin.X + local.X, rider.Origin.Y + local.Y);

                    if (!Board.Contains(boardCell))
                    {
                        error = new Error(InventoryError.OutOfBoard, ErrorKind.Validation);
                        carry.Clear();
                        return false;
                    }

                    if (!CoversCell(boardCell, bag.InstanceId, shape, origin))
                    {
                        error = new Error(InventoryError.ItemNotCovered, ErrorKind.Conflict);
                        carry.Clear();
                        return false;
                    }

                    int owner = Board.ItemOwnerAt(boardCell);
                    if (owner != Board.NoOwner && owner != item.InstanceId && !IsCarried(carry, owner))
                    {
                        error = new Error(InventoryError.ItemOverlap, ErrorKind.Conflict);
                        carry.Clear();
                        return false;
                    }
                }
            }

            error = default;
            return true;
        }

        /// <summary>True when every cell of <paramref name="item"/> lies on that one bag.</summary>
        private bool RidesOn(ItemInstance item, int bagInstanceId)
        {
            for (int cell = 0; cell < item.CellCount; cell++)
            {
                if (Board.BagOwnerAt(item.CellAt(cell)) != bagInstanceId)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when the assembled plan already contains that instance (a linear scan of ≤ a handful).</summary>
        private static bool IsCarried(List<BagCarry> carry, int instanceId)
        {
            for (int i = 0; i < carry.Count; i++)
            {
                if (carry[i].Piece.InstanceId == instanceId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Instance ids are handed out in order, so a restored state continues the sequence.</summary>
        internal void RestoreNextInstanceId(int nextInstanceId) => _nextInstanceId = nextInstanceId;

        /// <summary>Re-adds a piece while restoring a snapshot, keeping its identity.</summary>
        internal void AdoptPiece(PieceInstance piece)
        {
            if (piece is BagPieceInstance bag)
            {
                _bags.Add(bag);
                _bagsById.Add(bag.InstanceId, bag);
            }
            else
            {
                var item = (ItemInstance)piece;
                _items.Add(item);
                _itemsById.Add(item.InstanceId, item);
            }
        }

        /// <summary>
        /// Rebuilds the board and chest occupancy from the pieces' own placement, and
        /// resets the registries. Used when restoring a snapshot: the snapshot is the
        /// only input, so the derived maps are recomputed rather than trusted.
        /// </summary>
        internal void RebuildDerivedState()
        {
            for (int i = 0; i < _bags.Count; i++)
            {
                BagPieceInstance bag = _bags[i];
                if (bag.Zone == PieceZone.Board)
                {
                    Board.OccupyBag(bag.InstanceId, bag.Shape, bag.Origin);
                }
            }


            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance item = _items[i];
                if (item.Zone == PieceZone.Board)
                {
                    Board.OccupyItem(item.InstanceId, item.Shape, item.Origin);
                }
            }
        }

        /// <summary>Drops every piece and every cell (used before restoring a snapshot).</summary>
        internal void ClearAll()
        {
            for (int i = 0; i < _bags.Count; i++)
            {
                BagPieceInstance bag = _bags[i];
                if (bag.Zone == PieceZone.Board)
                {
                    Board.ReleaseBag(bag.InstanceId, bag.Shape, bag.Origin);
                }
            }

            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance item = _items[i];
                if (item.Zone == PieceZone.Board)
                {
                    Board.ReleaseItem(item.InstanceId, item.Shape, item.Origin);
                }
            }

            _bags.Clear();
            _bagsById.Clear();
            _items.Clear();
            _itemsById.Clear();
        }

        /// <summary>
        /// Captures the entire state as plain values — the undo and camp-reset unit.
        /// </summary>
        public BackpackSnapshot CaptureSnapshot()
        {
            var pieces = new List<PieceSnapshot>(_bags.Count + _items.Count);

            for (int i = 0; i < _bags.Count; i++)
            {
                pieces.Add(CapturePiece(_bags[i]));
            }

            for (int i = 0; i < _items.Count; i++)
            {
                pieces.Add(CapturePiece(_items[i]));
            }

            return new BackpackSnapshot
            {
                SchemaVersion = 2,
                NextInstanceId = _nextInstanceId,
                MergeCount = MergeCount,
                Pieces = pieces,
            };
        }

        /// <summary>
        /// Replaces the entire state with <paramref name="snapshot"/>: the pieces,
        /// their placement, the chest slots and the id counter.
        /// </summary>
        /// <remarks>
        /// The board's occupancy maps are rebuilt from the pieces rather than trusted,
        /// so a restored backpack can never disagree with the board it draws.
        /// </remarks>
        internal void RestoreSnapshot(BackpackSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ClearAll();
            Chest.ClearAllSlots();
            RestoreNextInstanceId(snapshot.NextInstanceId < 1 ? 1 : snapshot.NextInstanceId);
            RestoreMergeCount(snapshot.MergeCount);

            IReadOnlyList<PieceSnapshot> pieces = snapshot.Pieces;
            for (int i = 0; i < pieces.Count; i++)
            {
                PieceSnapshot captured = pieces[i];
                PieceInstance piece = Rebuild(captured);

                piece.Tier = captured.Tier;
                piece.Rotation = captured.Rotation;
                piece.Zone = captured.Zone;
                piece.Origin = new CellOffset(captured.OriginX, captured.OriginY);
                piece.Shape = ShapeAt(piece, captured.Rotation);

                if (captured.Zone == PieceZone.Chest)
                {
                    piece.ChestSlot = captured.ChestSlot;
                    Chest.SetSlot(captured.ChestSlot, piece.Ref);
                }
                else
                {
                    piece.ChestSlot = PieceInstance.NoChestSlot;
                }

                AdoptPiece(piece);
            }

            RebuildDerivedState();
            MarkChanged();
        }

        private PieceInstance Rebuild(PieceSnapshot captured)
        {
            if (captured.Kind == PieceKind.Bag)
            {
                _config.TryGetBag(captured.DefId, out BagConfig definition);
                return new BagPieceInstance(captured.InstanceId, captured.DefId, captured.Tier, definition.Footprint);
            }

            _config.TryGetItem(captured.DefId, out ItemConfig itemDefinition);
            var item = new ItemInstance(captured.InstanceId, captured.DefId, captured.Tier, itemDefinition.Footprint)
            {
                Tags = itemDefinition.Tags,
            };

            // The chosen variants survive the round trip, and they are restored before the
            // board is rebuilt so a restored piece is complete the moment it exists.
            item.SetVariants(captured.VariantIds);
            return item;
        }

        private static PieceSnapshot CapturePiece(PieceInstance piece) => new PieceSnapshot
        {
            Kind = piece.Kind,
            InstanceId = piece.InstanceId,
            DefId = piece.DefId,
            Tier = piece.Tier,
            Zone = piece.Zone,
            OriginX = piece.Origin.X,
            OriginY = piece.Origin.Y,
            Rotation = piece.Rotation,
            ChestSlot = piece.ChestSlot,
            VariantIds = piece is ItemInstance item ? ItemVariants(item) : Array.Empty<string>(),
        };

        /// <summary>
        /// An item's chosen variants as a snapshot list. A copy, so a snapshot never holds a
        /// live view of a piece the game keeps mutating.
        /// </summary>
        private static IReadOnlyList<string> ItemVariants(ItemInstance item)
        {
            IReadOnlyList<string> variants = item.VariantIds;
            if (variants.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[variants.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = variants[i];
            }

            return copy;
        }

        /// <summary>Raises <see cref="Revision"/> by one.</summary>
        internal void MarkChanged() => Revision++;

        /// <summary>
        /// Checks every invariant the rest of the game assumes, and describes the
        /// first violation found.
        /// </summary>
        /// <remarks>
        /// Used by the property test that hammers randomized commands, and available to
        /// the editor as a debug assert: if a command ever left the board in a state no
        /// rule can produce, this is where it is caught rather than three systems later.
        /// </remarks>
        public bool TryValidate(out string? violation)
        {
            var seen = new HashSet<int>();

            for (int i = 0; i < _bags.Count; i++)
            {
                if (!seen.Add(_bags[i].InstanceId))
                {
                    violation = "instance id repeated across pieces: " + _bags[i].InstanceId;
                    return false;
                }

                if (!ValidatePieceShape(_bags[i], out violation))
                {
                    return false;
                }
            }

            for (int i = 0; i < _items.Count; i++)
            {
                ItemInstance item = _items[i];

                if (!seen.Add(item.InstanceId))
                {
                    violation = "instance id repeated across pieces: " + item.InstanceId;
                    return false;
                }

                if (item.Tier < 1 || item.Tier > MaxTier)
                {
                    violation = "item " + item.InstanceId + " has tier " + item.Tier;
                    return false;
                }

                // One variant slot per completed merge step (Stage 09f), and a filled slot
                // must name a variant that still exists in data - the two things a restored
                // or replayed item could otherwise get wrong silently.
                if (item.VariantStepCount != item.Tier - 1)
                {
                    violation = "item " + item.InstanceId + " is tier " + item.Tier + " with "
                        + item.VariantStepCount + " upgrade variant slot(s)";
                    return false;
                }

                for (int step = 0; step < item.VariantStepCount; step++)
                {
                    string variantId = item.VariantIds[step];
                    if (variantId.Length == 0)
                    {
                        continue;
                    }

                    if (!_config.TryGetUpgradeVariant(variantId, out _))
                    {
                        violation = "item " + item.InstanceId + " step " + (step + 1)
                            + " names upgrade variant '" + variantId + "' which does not exist";
                        return false;
                    }
                }

                if (!ValidatePieceShape(item, out violation))
                {
                    return false;
                }

                if (item.Zone != PieceZone.Board)
                {
                    continue;
                }

                for (int cell = 0; cell < item.CellCount; cell++)
                {
                    CellOffset boardCell = item.CellAt(cell);
                    if (Board.ItemOwnerAt(boardCell) != item.InstanceId)
                    {
                        violation = "item " + item.InstanceId + " does not own cell " + boardCell;
                        return false;
                    }

                    if (!Board.IsActiveSlot(boardCell))
                    {
                        violation = "item " + item.InstanceId + " sits on an inactive cell " + boardCell;
                        return false;
                    }
                }
            }

            for (int i = 0; i < _bags.Count; i++)
            {
                BagPieceInstance bag = _bags[i];
                if (bag.Zone != PieceZone.Board)
                {
                    continue;
                }

                for (int cell = 0; cell < bag.CellCount; cell++)
                {
                    CellOffset boardCell = bag.CellAt(cell);
                    if (Board.BagOwnerAt(boardCell) != bag.InstanceId)
                    {
                        violation = "bag " + bag.InstanceId + " does not own cell " + boardCell;
                        return false;
                    }
                }
            }

            for (int slot = 0; slot < Chest.SlotCount; slot++)
            {
                PieceRef reference = Chest[slot];
                if (!reference.IsValid)
                {
                    continue;
                }

                if (!TryGetPiece(reference, out PieceInstance? piece) || piece is null)
                {
                    violation = "chest slot " + slot + " names a piece that does not exist: " + reference;
                    return false;
                }

                if (piece.Zone != PieceZone.Chest || piece.ChestSlot != slot)
                {
                    violation = "chest slot " + slot + " disagrees with piece " + reference;
                    return false;
                }
            }

            for (int i = 0; i < _bags.Count; i++)
            {
                if (!ValidateChestMembership(_bags[i], out violation))
                {
                    return false;
                }
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (!ValidateChestMembership(_items[i], out violation))
                {
                    return false;
                }
            }

            violation = null;
            return true;

            bool ValidatePieceShape(PieceInstance piece, out string? problem)
            {
                if (piece.Zone != PieceZone.Board)
                {
                    problem = null;
                    return true;
                }

                Footprint expected = ShapeAt(piece, piece.Rotation);
                if (!expected.Equals(piece.Shape))
                {
                    problem = "piece " + piece.InstanceId + " shape does not match rotation " + piece.Rotation;
                    return false;
                }

                for (int cell = 0; cell < piece.CellCount; cell++)
                {
                    if (!Board.Contains(piece.CellAt(cell)))
                    {
                        problem = "piece " + piece.InstanceId + " reaches outside the board";
                        return false;
                    }
                }

                problem = null;
                return true;
            }

            bool ValidateChestMembership(PieceInstance piece, out string? problem)
            {
                if (piece.Zone == PieceZone.Board)
                {
                    if (piece.ChestSlot != PieceInstance.NoChestSlot)
                    {
                        problem = "board piece " + piece.InstanceId + " still claims chest slot " + piece.ChestSlot;
                        return false;
                    }

                    problem = null;
                    return true;
                }

                if (piece.ChestSlot < 0 || piece.ChestSlot >= Chest.SlotCount)
                {
                    problem = "piece " + piece.InstanceId + " has an out-of-range chest slot " + piece.ChestSlot;
                    return false;
                }

                if (Chest[piece.ChestSlot] != piece.Ref)
                {
                    problem = "piece " + piece.InstanceId + " is not in its own chest slot";
                    return false;
                }

                problem = null;
                return true;
            }
        }

        /// <summary>Canonical piece order: board row, then column, then kind, id, tier.</summary>
        private static int CompareByPosition(PieceInstance left, PieceInstance right)
        {
            if (left.Origin.Y != right.Origin.Y)
            {
                return left.Origin.Y.CompareTo(right.Origin.Y);
            }

            if (left.Origin.X != right.Origin.X)
            {
                return left.Origin.X.CompareTo(right.Origin.X);
            }

            if (left.Kind != right.Kind)
            {
                return left.Kind.CompareTo(right.Kind);
            }

            int byId = string.CompareOrdinal(left.DefId, right.DefId);
            return byId != 0 ? byId : left.InstanceId.CompareTo(right.InstanceId);
        }
    }
}
