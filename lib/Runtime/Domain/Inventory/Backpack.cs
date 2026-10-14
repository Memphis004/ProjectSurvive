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

            MarkChanged();
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

        /// <summary>
        /// True when <paramref name="piece"/> would still be fully covered by ActiveSlots
        /// after the bag <paramref name="bagInstanceId"/> changed to
        /// <paramref name="newShape"/> at <paramref name="newOrigin"/>
        /// (pass an empty shape for "the bag left the board").
        /// </summary>
        /// <remarks>
        /// Used by move / rotate / chest / recycle of a bag: GDD v0.3 refuses the whole
        /// command rather than letting an item dangle.
        /// </remarks>
        internal bool WouldStayCovered(ItemInstance piece, int bagInstanceId, Footprint? newShape, CellOffset newOrigin)
        {
            int cells = piece.CellCount;
            for (int i = 0; i < cells; i++)
            {
                CellOffset cell = piece.CellAt(i);

                if (Board.IsActiveSlotExcept(cell, bagInstanceId))
                {
                    continue;
                }

                if (newShape is not null && newShape.CellCount > 0)
                {
                    int dx = cell.X - newOrigin.X;
                    int dy = cell.Y - newOrigin.Y;
                    if (dx >= 0 && dy >= 0 && newShape.Contains(new CellOffset(dx, dy)))
                    {
                        continue;
                    }
                }

                return false;
            }

            return true;
        }

        /// <summary>Finds the first placed item that a bag change would leave off an ActiveSlot.</summary>
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
                SchemaVersion = 1,
                NextInstanceId = _nextInstanceId,
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
            return new ItemInstance(captured.InstanceId, captured.DefId, captured.Tier, itemDefinition.Footprint)
            {
                Tags = itemDefinition.Tags,
            };
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
        };

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
