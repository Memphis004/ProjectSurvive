using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>
    /// One run: the wallet, the bag, the camp shop, the floor cursor and the flow
    /// phase, with every state-changing operation as an atomic, typed command.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the <b>only</b> command surface of a run. A Presenter calls it and
    /// never recomputes a price, a heal or a combo (AGENTS rules 5 and 10), and the
    /// battle is reached exclusively through <see cref="StartFloor"/>'s
    /// <see cref="BattleSetup"/> plus an <c>IBattleSessionFactory</c> the caller
    /// owns — this class never names a session implementation.
    /// </para>
    /// <para>
    /// <b>Scope and lifetime (GDD \"อายุข้อมูล 3 ระดับ\").</b> A run is created once
    /// and survives every Camp ↔ Battle trip; returning to <c>Title</c> ends it. The
    /// scene-scoped things (views, the ECS world, pooled visuals) belong to the
    /// Presentation layer and are never held here.
    /// </para>
    /// <para>
    /// <b>Determinism.</b> Every random draw comes from the run's own
    /// <see cref="RandomStreams"/>: the shop rolls use the shop stream and each
    /// floor's battle seed is drawn from the battle stream, so one stream can never
    /// shift another (AGENTS rule 6). Replaying the same command sequence on the
    /// same seed therefore rebuilds the same <see cref="RunState"/>.
    /// </para>
    /// <para>
    /// <b>Atomicity.</b> Every command validates the whole change first and only
    /// then mutates, so a rejection leaves the run byte-identical (AGENTS rule 7).
    /// Where a mutation could still fail after a spend, the spend is refunded in the
    /// same call rather than left half-applied.
    /// </para>
    /// </remarks>
    public sealed class RunSession
    {
        private readonly IGameConfig _config;
        private readonly LoadoutResolver _loadouts;
        private readonly SynergyEvaluator _synergies;
        private readonly ShopRoller _roller;
        private readonly RandomStreams _streams;
        private readonly ReplayLog _log;
        private readonly List<RunEvent> _events = new List<RunEvent>();
        private readonly List<FloorConfig> _floors;
        private readonly bool _recording;

        private BattleSetup? _pendingSetup;
        private CombatLoadoutSnapshot? _pendingLoadout;
        private int _pendingSettlementId;
        private int _settlementCounter;

        /// <summary>Creates a run for <paramref name="seed"/>. It starts in <see cref="RunFlowState.Title"/>.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public RunSession(IGameConfig config, IBackpackSnapshotCodec codec, uint seed)
            : this(config, codec, seed, recording: true)
        {
        }

        internal RunSession(IGameConfig config, IBackpackSnapshotCodec codec, uint seed, bool recording)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (codec is null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            Seed = seed;
            _recording = recording;
            _streams = new RandomStreams(seed);
            _log = new ReplayLog(seed);

            // The floor table is the run's spine; sorting by `index` once means no
            // command ever reads it in file order by accident.
            _floors = new List<FloorConfig>(config.Floors.Count);
            for (int i = 0; i < config.Floors.Count; i++)
            {
                _floors.Add(config.Floors[i]);
            }

            _floors.Sort(CompareFloors);

            Wallet = new Wallet(config.Economy.StartBia);
            Backpack = new Backpack(config);
            Editor = new BackpackEditor(Backpack, config, codec);
            Stock = new ShopStock(config.Economy.ShopSlotCount);
            _roller = new ShopRoller(config);
            _synergies = new SynergyEvaluator(config);
            _loadouts = new LoadoutResolver(config, _synergies);

            Hp = MaxHp;
        }

        /// <summary>The run seed every stream and the log were derived from.</summary>
        public uint Seed { get; }

        /// <summary>The purse (เบี้ย).</summary>
        public Wallet Wallet { get; }

        /// <summary>The bag (ย่าม) and the chest.</summary>
        public Backpack Backpack { get; }

        /// <summary>The bag command surface (\"ย่ามแก้ไขได้เฉพาะที่ศาลาพัก\").</summary>
        public BackpackEditor Editor { get; }

        /// <summary>The camp shop's stock.</summary>
        public ShopStock Stock { get; }

        /// <summary>The recorded run commands, replayable against the same seed.</summary>
        public ReplayLog Log => _log;

        /// <summary>The flow phase (Title → Camp → Battle → Camp → … → Result).</summary>
        public RunFlowState FlowState { get; private set; } = RunFlowState.Title;

        /// <summary>1-based index of the floor being attempted (or the next one).</summary>
        public int FloorIndex { get; private set; }

        /// <summary>HP carried across floors (<c>constant.max_hp</c> at the start).</summary>
        public int Hp { get; private set; }

        /// <summary>Maximum HP (<c>constant.max_hp</c>).</summary>
        public int MaxHp => _config.Constant.MaxHp;

        /// <summary>How many floors have been cleared.</summary>
        public int FloorsCleared { get; private set; }

        /// <summary>How many floors the run has (the floor table's row count).</summary>
        public int TotalFloors => _floors.Count;

        /// <summary>
        /// Ghosts the run has killed, summed over every floor it fought ("ผีที่ปราบ").
        /// </summary>
        /// <remarks>
        /// Each settled floor contributes its own <see cref="BattleResult.Kills"/>. The sum lives on
        /// the run because no floor's battle outlives the floor: the number the summary screen needs
        /// exists only here.
        /// </remarks>
        public int GhostsDefeated { get; private set; }

        /// <summary>Id of the last settlement paid out; 0 before the first one.</summary>
        public int LastSettlementId { get; private set; }

        /// <summary>True once the run has ended (win or loss).</summary>
        public bool IsRunOver => FlowState == RunFlowState.Result;

        /// <summary>
        /// How the run stands: still in progress, won, or lost.
        /// </summary>
        /// <remarks>
        /// Derived, not stored — the run is over in exactly two shapes (a floor it did not clear,
        /// or its last floor cleared), and a second flag would be one more thing that could disagree
        /// with <see cref="FlowState"/>.
        /// </remarks>
        public RunOutcome Outcome
        {
            get
            {
                if (FlowState != RunFlowState.Result)
                {
                    return RunOutcome.InProgress;
                }

                return FloorsCleared >= _floors.Count ? RunOutcome.Victory : RunOutcome.Defeat;
            }
        }

        /// <summary>
        /// The summary the Result screen shows: how the run ended, how far it got and what it killed.
        /// </summary>
        /// <remarks>
        /// A read — nothing in the run changes because a screen asked. It is built even while the run
        /// is in progress (the caller can check <see cref="RunSummary.IsFinished"/>), which keeps a
        /// screen from having to ask two questions in the right order to get an answer.
        /// </remarks>
        public RunSummary Summary() => new RunSummary(
            Seed,
            Outcome,
            FloorIndex,
            TotalFloors,
            FloorsCleared,
            GhostsDefeated,
            Wallet.Bia,
            Hp,
            MaxHp,
            LastSettlementId);

        /// <summary>
        /// The events raised since the last <see cref="ClearEvents"/>. Presentation
        /// reads them to refresh a label; a rule never does.
        /// </summary>
        public IReadOnlyList<RunEvent> Events => _events;

        /// <summary>The floor about to be attempted, or null when none is left.</summary>
        public FloorConfig? CurrentFloor =>
            FloorIndex >= 1 && FloorIndex <= _floors.Count ? _floors[FloorIndex - 1] : null;

        /// <summary>
        /// The setup of the floor this run has departed for, or null when no departure is
        /// outstanding.
        /// </summary>
        /// <remarks>
        /// The departure happens where the departure button is (the camp), and the floor it
        /// validated is fought somewhere else (the Battle scene, across a scene load). Passing the
        /// object through the run is what lets the arena scene start <b>the floor the camp already
        /// validated</b> — HP, purse, bag and the resolved loadout — instead of asking the same
        /// questions a second time and risking a different answer. It is cleared by
        /// <see cref="ApplyBattleResult"/>, exactly like the private field it exposes.
        /// </remarks>
        public BattleSetup? PendingSetup => _pendingSetup;

        // -------------------------------------------------------------------
        // flow
        // -------------------------------------------------------------------

        /// <summary>
        /// Starts the run: lays out the starting kit, sets HP and bia, and enters the
        /// first camp (which rolls the shop once).
        /// </summary>
        /// <returns>Success, or a failure that leaves the run in <see cref="RunFlowState.Title"/>.</returns>
        public Result StartRun()
        {
            if (FlowState != RunFlowState.Title)
            {
                return Result.Fail(RunError.WrongState, ErrorKind.InvalidState);
            }

            if (_floors.Count == 0)
            {
                return Result.Fail(RunError.UnknownFloor, ErrorKind.NotFound);
            }

            // Validate the whole starting kit before mutating anything: a missing id
            // or a board that cannot hold the bag is a data bug, and it must not leave
            // half a loadout behind.
            Result kit = ValidateStartKit();
            if (kit.IsFailure)
            {
                return kit;
            }

            var bagOrigin = new CellOffset(RunDefaults.InitialBagX, RunDefaults.InitialBagY);
            var weaponOrigin = new CellOffset(RunDefaults.InitialWeaponX, RunDefaults.InitialWeaponY);

            Result<PieceRef> addedBag = Backpack.AddBag(RunDefaults.InitialBagId);
            if (addedBag.IsFailure)
            {
                // Unreachable: ValidateStartKit proved the id exists and the chest has room.
                return Result.Fail(addedBag.Error);
            }

            Result<PieceRef> addedWeapon = Backpack.AddItem(RunDefaults.InitialWeaponId);
            if (addedWeapon.IsFailure)
            {
                return Result.Fail(addedWeapon.Error);
            }

            Result afterBag = Editor.Move(addedBag.Value, bagOrigin, Rotation.Degrees0);
            if (afterBag.IsFailure)
            {
                return afterBag;
            }

            Result afterWeapon = Editor.Move(addedWeapon.Value, weaponOrigin, Rotation.Degrees0);
            if (afterWeapon.IsFailure)
            {
                return afterWeapon;
            }

            Hp = MaxHp;
            FloorIndex = 1;
            FloorsCleared = 0;
            GhostsDefeated = 0;
            LastSettlementId = 0;

            EnterCamp();

            Record(RunCommand.StartRun());
            Emit(RunEventKind.InventoryChanged, Backpack.Revision);
            Emit(RunEventKind.LoadoutChanged, Backpack.Revision);
            Emit(RunEventKind.WalletChanged, Wallet.Bia);
            return Result.Ok();
        }

        /// <summary>
        /// Opens the camp shop. Idempotent on purpose: \"สร้าง stock ครั้งเดียวต่อการเข้าศาลา\"
        /// and \"เปิดร้านซ้ำสินค้าเดิม\", so opening it again in the same camp returns the
        /// same stock and consumes no randomness.
        /// </summary>
        public Result<ShopStock> OpenShop()
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<ShopStock>(RunError.WrongState, ErrorKind.InvalidState);
            }

            return Result.Ok(Stock);
        }

        /// <summary>
        /// Departs for <see cref="CurrentFloor"/> (\"ออกเดินทาง\"): validates the bag,
        /// locks it and returns the <see cref="BattleSetup"/> the caller hands to an
        /// <c>IBattleSessionFactory</c>.
        /// </summary>
        /// <returns>
        /// The setup, or a failure carrying <see cref="RunError.NoArcaneWeapon"/> when
        /// the resolved loadout has no usable arcane weapon — the reason code Stage 09
        /// shows on the disabled button.
        /// </returns>
        public Result<BattleSetup> StartFloor()
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<BattleSetup>(RunError.WrongState, ErrorKind.InvalidState);
            }

            // A precondition rather than a player-facing outcome: every phase that may
            // start a floor also has one left, so `run.no_floor_left` is a guard on the
            // cursor, not a refusal a player can reach (see the invariant test).
            FloorConfig? floor = CurrentFloor;
            if (floor is null)
            {
                return Result.Fail<BattleSetup>(RunError.NoFloorLeft, ErrorKind.InvalidState);
            }

            CombatLoadoutSnapshot loadout = _loadouts.Resolve(Backpack);

            if (loadout.Weapons.Count == 0)
            {
                return Result.Fail<BattleSetup>(RunError.NoArcaneWeapon, ErrorKind.InvalidState);
            }

            var setup = new BattleSetup
            {
                // One draw per floor from the run's own battle stream: the floor's
                // battle is reproducible from the run seed, and the shop stream is
                // untouched either way (AGENTS rule 6).
                Seed = _streams.Battle.NextUInt(),
                StageId = floor.Id,
                DifficultyId = RunDefaults.DifficultyId,
                FloorIndex = floor.Index,
                IsBossFloor = floor.IsBoss,
                CurrentHp = Hp,
                Loadout = loadout,

                // The floor copies whatever the shell decided about dev cheats
                // (Stage 10): false in a release build, on in the Editor and in a Development
                // build, and frozen for the floor's whole life either way.
                DevCheatsAllowed = DevCheats.Allowed,
            };

            _pendingSetup = setup;
            _pendingLoadout = loadout;
            _pendingSettlementId = ++_settlementCounter;

            // \"ล็อกย่ามระหว่าง Battle\": nothing in the bag may move until Camp.
            Editor.SetLocked(true);
            FlowState = RunFlowState.Battle;

            Record(RunCommand.StartFloor());
            Emit(RunEventKind.LoadoutChanged, Backpack.Revision);
            Emit(RunEventKind.FlowChanged, (int)FlowState);
            return Result.Ok(setup);
        }

        /// <summary>
        /// Settles a finished floor battle into the run — the \"CompleteFloor\" step:
        /// HP, bia and used consumables are updated, a cleared floor pays
        /// <c>economy.clear_reward_bia</c> and heals <c>economy.heal_percent_on_clear</c>
        /// percent of max HP (capped at max), and
        /// the run either returns to Camp or ends at <see cref="RunFlowState.Result"/>.
        /// </summary>
        /// <remarks>
        /// Paying out once is enforced by the phase: only the floor's own battle can be
        /// settled, so calling this twice refuses the second call with
        /// <see cref="RunError.NotInBattle"/> instead of paying the reward again.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
        public Result<FloorSettlement> ApplyBattleResult(BattleResult result)
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (FlowState != RunFlowState.Battle || _pendingSetup is null)
            {
                return Result.Fail<FloorSettlement>(RunError.NotInBattle, ErrorKind.InvalidState);
            }

            BattleSetup setup = _pendingSetup;
            CombatLoadoutSnapshot loadout = _pendingLoadout ?? setup.Loadout;
            int settlementId = _pendingSettlementId;
            int floorIndex = setup.FloorIndex;

            // Validate the consumable bookkeeping before anything is paid out: a bag
            // that cannot cover what the battle says it used is a desync, and the
            // settlement must not half-apply (AGENTS rule 7).
            Result consumables = ValidateConsumables(loadout, result);
            if (consumables.IsFailure)
            {
                return Result.Fail<FloorSettlement>(consumables.Error);
            }

            // Two sources, one payout: what the floor's pickups paid
            // (BattleResult.CurrencyEarned, already summed inside the battle) plus the flat
            // clear reward from economy. A defeat earns only the first — the clear reward
            // is the run's, not the battle's, and it is paid at most once because the
            // phase/pending-setup guard below admits exactly one settlement per floor.
            bool won = result.IsWin;
            int battleBia = result.CurrencyEarned > 0 ? result.CurrencyEarned : 0;
            int clearBia = won && _config.Economy.ClearRewardBia > 0
                ? _config.Economy.ClearRewardBia
                : 0;
            int reward = battleBia + clearBia;
            Wallet.Credit(reward);

            // The run's own kill count, added before the flow moves: the floor it came from is
            // gone by the time the summary screen reads it.
            if (result.Kills > 0)
            {
                GhostsDefeated += result.Kills;
            }

            Hp = Clamp(result.FinalHp, 0, MaxHp);
            ApplyConsumables(loadout, result);

            int healed = 0;
            if (won)
            {
                FloorsCleared++;
                healed = (MaxHp * _config.Economy.HealPercentOnClear) / 100;
                int room = MaxHp - Hp;
                if (healed > room)
                {
                    healed = room;
                }

                if (healed < 0)
                {
                    healed = 0;
                }

                Hp += healed;
            }

            LastSettlementId = settlementId;
            _pendingSetup = null;
            _pendingLoadout = null;
            Editor.SetLocked(false);

            bool runCompleted;
            if (!won)
            {
                FlowState = RunFlowState.Result;
                runCompleted = true;
                Emit(RunEventKind.FloorCompleted, settlementId);
                Emit(RunEventKind.FlowChanged, (int)FlowState);
                Emit(RunEventKind.RunCompleted, 0);
            }
            else if (FloorsCleared >= _floors.Count)
            {
                FlowState = RunFlowState.Result;
                runCompleted = true;
                Emit(RunEventKind.FloorCompleted, settlementId);
                Emit(RunEventKind.FlowChanged, (int)FlowState);
                Emit(RunEventKind.RunCompleted, 1);
            }
            else
            {
                FloorIndex++;
                EnterCamp();
                runCompleted = false;
                Emit(RunEventKind.FloorCompleted, settlementId);
            }

            Emit(RunEventKind.InventoryChanged, Backpack.Revision);
            Emit(RunEventKind.WalletChanged, Wallet.Bia);

            return Result.Ok(new FloorSettlement(
                settlementId, floorIndex, result.Outcome, result.EndReason, reward, healed, Hp, runCompleted));
        }

        /// <summary>Leaves the summary screen after a finished run (Result → Title).</summary>
        public Result ReturnToTitle()
        {
            if (FlowState != RunFlowState.Result)
            {
                return Result.Fail(RunError.WrongState, ErrorKind.InvalidState);
            }

            FlowState = RunFlowState.Title;
            Emit(RunEventKind.FlowChanged, (int)FlowState);
            return Result.Ok();
        }

        // -------------------------------------------------------------------
        // shop
        // -------------------------------------------------------------------

        /// <summary>
        /// Rents the piece in <paramref name="slotIndex"/> into the chest. Refused —
        /// with nothing changed — when the slot is empty, already rented, the chest is
        /// full, or the purse is short (\"หีบเต็มต้องไม่หักเงิน\").
        /// </summary>
        public Result<PurchaseOutcome> Purchase(int slotIndex)
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<PurchaseOutcome>(RunError.WrongState, ErrorKind.InvalidState);
            }

            if (slotIndex < 0 || slotIndex >= Stock.SlotCount)
            {
                return Result.Fail<PurchaseOutcome>(ShopError.UnknownSlot, ErrorKind.NotFound);
            }

            ShopSlot slot = Stock[slotIndex];

            if (slot.IsPurchased)
            {
                // \"ซื้อช่องเดิมซ้ำไม่ได้\"
                return Result.Fail<PurchaseOutcome>(ShopError.SlotPurchased, ErrorKind.Conflict);
            }

            if (slot.IsEmpty)
            {
                return Result.Fail<PurchaseOutcome>(ShopError.SlotEmpty, ErrorKind.Insufficient);
            }

            if (!_roller.TryResolve(slot.OfferedId, out PieceKind kind, out int price))
            {
                return Result.Fail<PurchaseOutcome>(ShopError.UnknownPiece, ErrorKind.NotFound);
            }

            if (!Wallet.CanAfford(slot.Price))
            {
                return Result.Fail<PurchaseOutcome>(WalletError.InsufficientBia, ErrorKind.Insufficient);
            }

            if (Backpack.Chest.IsFull)
            {
                return Result.Fail<PurchaseOutcome>(InventoryError.ChestFull, ErrorKind.Insufficient);
            }

            // Read the offer before anything is committed: renting takes the piece off
            // the slot, so the outcome and the event need the values captured here.
            string offeredId = slot.OfferedId;
            int paid = slot.Price;

            // Pay first, then take the piece: if the add still failed the money goes
            // straight back, so a purchase is never half-applied (AGENTS rule 7).
            Result spent = Wallet.TrySpend(paid);
            if (spent.IsFailure)
            {
                return Result.Fail<PurchaseOutcome>(spent.Error);
            }

            Result<PieceRef> acquired = kind == PieceKind.Bag
                ? Backpack.AddBag(offeredId)
                : Backpack.AddItem(offeredId);

            if (acquired.IsFailure)
            {
                Wallet.Credit(paid);
                return Result.Fail<PurchaseOutcome>(acquired.Error);
            }

            Stock.MarkPurchased(slotIndex);

            Record(RunCommand.Purchase(slotIndex));
            Emit(RunEventKind.ShopStockChanged, slotIndex, offeredId);
            Emit(RunEventKind.InventoryChanged, Backpack.Revision);
            Emit(RunEventKind.LoadoutChanged, Backpack.Revision);
            Emit(RunEventKind.WalletChanged, Wallet.Bia);

            return Result.Ok(new PurchaseOutcome(slotIndex, offeredId, kind, paid, Wallet.Bia, acquired.Value));
        }

        /// <summary>
        /// Gives a piece back to the altar (ถวายคืน) for
        /// <c>economy.sell_back_percent</c> percent of its standard price and credits
        /// the purse.
        /// </summary>
        public Result<RecycleOutcome> Sell(PieceRef piece)
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<RecycleOutcome>(RunError.WrongState, ErrorKind.InvalidState);
            }

            Result<RecycleOutcome> recycled = Editor.Recycle(piece);
            if (recycled.IsFailure)
            {
                return recycled;
            }

            Wallet.Credit(recycled.Value.PayoutBia);

            Record(RunCommand.Sell(piece));
            Emit(RunEventKind.InventoryChanged, Backpack.Revision);
            Emit(RunEventKind.LoadoutChanged, Backpack.Revision);
            Emit(RunEventKind.WalletChanged, Wallet.Bia);
            return recycled;
        }

        /// <summary>
        /// Re-rolls the shop (เสี่ยงเซียมซี): costs
        /// <c>refresh_base_cost + increment × refreshes already paid</c>, and replaces
        /// every slot that is not pinned — a rented slot included, because the re-roll
        /// itself is what was paid for. A rented slot only loses its rent flag once this
        /// paid re-roll actually reaches it; until then it stays empty and refuses a
        /// second rent.
        /// </summary>
        public Result<RefreshOutcome> RefreshShop()
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<RefreshOutcome>(RunError.WrongState, ErrorKind.InvalidState);
            }

            int cost = Stock.RefreshCost(_config.Economy);

            Result spent = Wallet.TrySpend(cost);
            if (spent.IsFailure)
            {
                return Result.Fail<RefreshOutcome>(spent.Error);
            }

            Stock.MarkRefreshed();
            int filled = _roller.Roll(Stock, FloorIndex, _streams.Shop);

            Record(RunCommand.RefreshShop());
            Emit(RunEventKind.ShopStockChanged, -1);
            Emit(RunEventKind.WalletChanged, Wallet.Bia);
            return Result.Ok(new RefreshOutcome(Stock.RefreshCount, cost, filled, Wallet.Bia));
        }

        /// <summary>
        /// Pins or unpins one shop slot so a re-roll keeps it. A slot with nothing on it —
        /// rented or left empty by a short pool — is refused: there is nothing to pin.
        /// </summary>
        public Result<ShopSlot> SetSlotLocked(int slotIndex, bool locked)
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<ShopSlot>(RunError.WrongState, ErrorKind.InvalidState);
            }

            if (slotIndex < 0 || slotIndex >= Stock.SlotCount)
            {
                return Result.Fail<ShopSlot>(ShopError.UnknownSlot, ErrorKind.NotFound);
            }

            ShopSlot slot = Stock[slotIndex];
            if (slot.IsPurchased)
            {
                return Result.Fail<ShopSlot>(ShopError.SlotPurchased, ErrorKind.Conflict);
            }

            if (slot.IsEmpty)
            {
                return Result.Fail<ShopSlot>(ShopError.SlotEmpty, ErrorKind.Insufficient);
            }

            if (slot.IsLocked == locked)
            {
                return Result.Ok(slot);
            }

            Stock.SetSlotLocked(slotIndex, locked);

            Record(RunCommand.SetSlotLocked(slotIndex, locked));
            Emit(RunEventKind.ShopStockChanged, slotIndex, slot.OfferedId);
            return Result.Ok(slot);
        }

        // -------------------------------------------------------------------
        // bag (camp only)
        // -------------------------------------------------------------------

        /// <summary>Places or moves a piece on the board (\"ย้ายของขึ้นย่าม\" / \"ย้ายในย่าม\").</summary>
        public Result MovePiece(PieceRef piece, CellOffset origin, Rotation rotation)
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return inCamp;
            }

            Result moved = Editor.Move(piece, origin, rotation);
            if (moved.IsFailure)
            {
                return moved;
            }

            Record(RunCommand.MovePiece(piece, origin.X, origin.Y, rotation));
            EmitBagChanged();
            return Result.Ok();
        }

        /// <summary>Turns a placed piece one quarter turn clockwise.</summary>
        public Result RotatePiece(PieceRef piece)
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return inCamp;
            }

            Result rotated = Editor.Rotate(piece);
            if (rotated.IsFailure)
            {
                return rotated;
            }

            Record(RunCommand.RotatePiece(piece));
            EmitBagChanged();
            return Result.Ok();
        }

        /// <summary>Parks a board piece in the chest.</summary>
        public Result TransferToChest(PieceRef piece)
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return inCamp;
            }

            Result stored = Editor.TransferToChest(piece);
            if (stored.IsFailure)
            {
                return stored;
            }

            Record(RunCommand.TransferToChest(piece));
            EmitBagChanged();
            return Result.Ok();
        }

        /// <summary>Merges two instances of the same piece and tier (\"วางทับเพื่อ merge\").</summary>
        public Result<MergeOutcome> MergeItems(PieceRef source, PieceRef target)
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail<MergeOutcome>(RunError.WrongState, ErrorKind.InvalidState);
            }

            Result<MergeOutcome> merged = Editor.Merge(source, target);
            if (merged.IsFailure)
            {
                return merged;
            }

            Record(RunCommand.MergeItems(source, target));
            EmitBagChanged();
            return merged;
        }

        /// <summary>
        /// The upgrade choices merging <paramref name="source"/> into
        /// <paramref name="target"/> would offer, without changing anything
        /// (Stage 09f: <c>OfferMerge</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A read model, so it is not recorded in the replay log: nothing happened. It is
        /// also stable — the same pair in the same state returns the same choices in the same
        /// order — because the draw is a pure function of the run seed, how many merges the run
        /// has confirmed, the item and the tier step. Cancelling the chooser and opening it
        /// again therefore cannot shop for a better roll, and neither can an undo.
        /// </para>
        /// <para>
        /// An offer with no choices is the documented fallback: that pair folds immediately
        /// through <see cref="MergeItems"/> (charms, tomes, offerings, familiars, bags).
        /// </para>
        /// </remarks>
        public Result<MergeOffer> OfferMerge(PieceRef source, PieceRef target)
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return Result.Fail<MergeOffer>(inCamp.Error);
            }

            return Editor.OfferMerge(source, target, Seed);
        }

        /// <summary>
        /// Merges <paramref name="source"/> into <paramref name="target"/> with
        /// <paramref name="variantId"/> as the upgrade this step keeps (Stage 09f:
        /// <c>ConfirmMerge</c>).
        /// </summary>
        /// <remarks>
        /// Atomic and typed (AGENTS rule 7): the pair and the choice are validated against the
        /// offer the <i>current</i> state produces before anything moves, so a stale or foreign
        /// id is refused with the run untouched — and the accepted command is recorded with its
        /// id, because which variant was chosen changes the loadout a replay must reproduce.
        /// </remarks>
        public Result<MergeOutcome> ConfirmMerge(PieceRef source, PieceRef target, string variantId)
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return Result.Fail<MergeOutcome>(inCamp.Error);
            }

            Result<MergeOutcome> merged = Editor.ConfirmMerge(source, target, variantId, Seed);
            if (merged.IsFailure)
            {
                return merged;
            }

            Record(RunCommand.ConfirmMerge(source, target, variantId));
            EmitBagChanged();
            return merged;
        }

        /// <summary>Undoes the last accepted bag edit.</summary>
        public Result Undo()
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return inCamp;
            }

            Result undone = Editor.Undo();
            if (undone.IsFailure)
            {
                return undone;
            }

            Record(RunCommand.Undo());
            EmitBagChanged();
            return Result.Ok();
        }

        /// <summary>
        /// Locks or unlocks bag editing ("ล็อกย่าม"): the guard the camp screen's lock button
        /// toggles, so a stray drag cannot rearrange a build.
        /// </summary>
        /// <remarks>
        /// Deliberately <b>not recorded</b> in the replay log: <c>RunState</c> does not carry the
        /// lock, no command's outcome depends on it, and the two locks the run itself owns (during
        /// a battle, and released by the settlement) are set by <see cref="StartFloor"/> and
        /// <see cref="ApplyBattleResult"/> rather than by a player input. Recording it would put a
        /// flag in the log that a replay must reproduce without any state it can be compared
        /// against.
        /// </remarks>
        public Result SetBagLocked(bool locked)
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail(RunError.WrongState, ErrorKind.InvalidState);
            }

            return Editor.SetLocked(locked);
        }

        /// <summary>Restores the bag to how it looked when the run entered Camp.</summary>
        public Result Reset()
        {
            Result inCamp = RequireCamp();
            if (inCamp.IsFailure)
            {
                return inCamp;
            }

            Result reset = Editor.Reset();
            if (reset.IsFailure)
            {
                return reset;
            }

            Record(RunCommand.Reset());
            EmitBagChanged();
            return Result.Ok();
        }

        // -------------------------------------------------------------------
        // state
        // -------------------------------------------------------------------

        /// <summary>A plain picture of the run, for the state hash and for tests.</summary>
        public RunState CaptureState() => new RunState
        {
            FlowState = FlowState,
            FloorIndex = FloorIndex,
            Hp = Hp,
            MaxHp = MaxHp,
            Bia = Wallet.Bia,
            FloorsCleared = FloorsCleared,
            GhostsDefeated = GhostsDefeated,
            LastSettlementId = LastSettlementId,
            RefreshCount = Stock.RefreshCount,
            BackpackRevision = Backpack.Revision,
            NextInstanceId = Backpack.NextInstanceId,
            MergeCount = Backpack.MergeCount,
            ShopSlots = Stock.Capture(),
            Pieces = Backpack.CaptureSnapshot().Pieces,
        };

        /// <summary>
        /// A hash of the current run state. Two runs that took the same commands from
        /// the same seed produce the same number — that is how a replay is checked
        /// (GDD: \"replay จาก log ได้ RunState hash เดิม\").
        /// </summary>
        public uint ComputeStateHash() => CaptureState().ComputeHash();

        /// <summary>Drops the buffered events. Presentation calls this after it has read them.</summary>
        public void ClearEvents() => _events.Clear();

        /// <summary>Appends the battle recording SceneFlow just ran, so the log can replay the floor.</summary>
        internal void RecordBattle(BattleReplay replay) => _log.RecordBattle(replay);

        // -------------------------------------------------------------------
        // camp read model (Stage 09)
        // -------------------------------------------------------------------

        /// <summary>
        /// What releasing a dragged piece in <paramref name="zone"/> would do, computed
        /// without changing anything ("preview วางได้และวางไม่ได้ โดยไม่แก้ Domain จนผู้เล่นยืนยัน").
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <b>dry run of the command, not a second rule</b>: every branch below asks the same
        /// <c>BackpackEditor</c> predicate the accepted command asks (<c>CanPlace</c>,
        /// <c>CanMerge</c>, <c>CanTransferToChest</c>, <c>CanRecycle</c>), so a green preview and
        /// a refused command are the same condition read twice. The only thing this method decides
        /// on its own is <i>intent</i> — which command the zone and the piece under the pointer
        /// imply, which is what "dropping an item onto another item means merge" states in one
        /// place instead of in a View.
        /// </para>
        /// <para>
        /// Nothing is recorded, nothing is emitted and no revision moves: a preview repeated a
        /// thousand times during a drag is observationally identical to not previewing at all.
        /// </para>
        /// </remarks>
        public DropPreview PreviewDrop(PieceRef piece, BagDropZone zone, CellOffset origin, Rotation rotation)
        {
            if (FlowState != RunFlowState.Camp)
            {
                return DropPreview.Refused(RunError.WrongState);
            }

            if (Editor.IsLocked)
            {
                return DropPreview.Refused(InventoryError.Locked);
            }

            switch (zone)
            {
                case BagDropZone.Altar:
                    return Editor.CanRecycle(piece, out int payout, out Error recycleError)
                        ? DropPreview.Accepted(BagDropAction.Recycle, default, payout)
                        : DropPreview.Refused(recycleError.Code);

                case BagDropZone.Chest:
                    return Editor.CanTransferToChest(piece, out Error chestError)
                        ? DropPreview.Accepted(BagDropAction.Transfer)
                        : DropPreview.Refused(chestError.Code);

                case BagDropZone.Board:
                {
                    // The piece under the pointer is the merge target: "วางทับเพื่อ merge" is a
                    // board gesture, and which instance is under it is a fact about the board
                    // rather than a decision a View should make.
                    PieceRef target = ItemAt(origin);
                    if (target.IsValid && target != piece)
                    {
                        if (!Editor.CanMerge(piece, target, out Error mergeError))
                        {
                            return DropPreview.Refused(mergeError.Code);
                        }

                        // Stage 09g: the gesture is the merge gesture whether or not the merge
                        // asks a question. A pair the piece matches previews as Merge and names
                        // the piece under the pointer; whether the fold can go ahead or the
                        // player has to pick an upgrade first is answered by OfferMerge, which
                        // the screen reads - so the preview stays a description of the gesture
                        // rather than a second copy of that rule.
                        return DropPreview.Accepted(BagDropAction.Merge, target);
                    }

                    return Editor.CanPlace(piece, origin, rotation, out Error placeError)
                        ? DropPreview.Accepted(BagDropAction.Place)
                        : DropPreview.Refused(placeError.Code);
                }

                default:
                    // Released over nothing. `inventory.out_of_board` is the honest reason: the
                    // piece would have had no legal cell to land on.
                    return DropPreview.Refused(InventoryError.OutOfBoard);
            }
        }

        /// <summary>
        /// Fills <paramref name="carry"/> with the items a bag drag at (<paramref name="origin"/>,
        /// <paramref name="rotation"/>) would bring along, and where each of them would land.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The screen draws these as ghosts while a bag is in the air (Stage 09f: "แสดงเงาของไอเทมที่ติดไป
        /// ที่ตำแหน่งใหม่"), and the plan is the run's, not the view's: it is the same
        /// <c>BackpackEditor</c> plan <see cref="PreviewDrop"/> validates, so a ghost can never show an
        /// item landing where the accepted command would refuse to put it.
        /// </para>
        /// <para>
        /// Read-only, and it judges nothing: this answers "which items ride and where", never "may I".
        /// The verdict stays <see cref="PreviewDrop"/>, and an empty list means the piece is not a bag on
        /// the board (nothing rides with a chest piece).
        /// </para>
        /// </remarks>
        public void CollectCarried(PieceRef piece, CellOffset origin, Rotation rotation, List<BagCarry> carry) =>
            Editor.CollectCarried(piece, origin, rotation, carry);

        /// <summary>
        /// The bag's layout as the combo rules see it — the Stage 09 tooltip's "synergy ที่ทำงานอยู่"
        /// and the cells it highlights.
        /// </summary>
        /// <remarks>
        /// Read-only and deliberately uncached: the caller decides when to re-ask (a screen asks
        /// once per backpack revision, not once per frame), and caching here would give the run a
        /// second copy of something <c>LoadoutResolver</c> already caches for the battle.
        /// </remarks>
        public SynergyEvaluation EvaluateLayout() => _synergies.Evaluate(Backpack);

        /// <summary>Weight carried on the board; a chest piece weighs nothing.</summary>
        public float BagWeight => Backpack.BoardWeight;

        /// <summary>The movement penalty the carried weight costs, in percent (capped).</summary>
        public int BagWeightPenaltyPercent => WeightPenalty.Percent(Backpack.BoardWeight, _config.Constant);

        /// <summary>MP regenerated per second (สมาธิ, <c>constant.focus_per_second</c>).</summary>
        public float FocusPerSecond => _config.Constant.FocusPerSecond;

        /// <summary>How many chest slots the run has (<c>economy.chest_slots</c>, 10).</summary>
        public int ChestSlots => Backpack.Chest.SlotCount;

        /// <summary>How many chest slots hold something.</summary>
        public int ChestUsed => Backpack.Chest.OccupiedCount;

        /// <summary>True while the bag refuses every edit ("ล็อกย่าม").</summary>
        public bool IsBagLocked => Editor.IsLocked;

        /// <summary>True when <see cref="Undo"/> would do something.</summary>
        public bool CanUndoEdit => Editor.CanUndo;

        /// <summary>What the next re-roll costs (เสี่ยงเซียมซี), before it is paid.</summary>
        public int RefreshPrice => Stock.RefreshCost(_config.Economy);

        /// <summary>
        /// Whether <see cref="StartFloor"/> would accept, without departing — what the Stage 09
        /// depart button is disabled on, and the reason it shows.
        /// </summary>
        /// <remarks>
        /// It asks the same two questions <see cref="StartFloor"/> asks, in the same order, and the
        /// loadout it resolves is cached on the backpack revision, so a screen may call this on
        /// every refresh without recomputing a combo.
        /// </remarks>
        public Result DepartCheck()
        {
            if (FlowState != RunFlowState.Camp)
            {
                return Result.Fail(RunError.WrongState, ErrorKind.InvalidState);
            }

            if (CurrentFloor is null)
            {
                return Result.Fail(RunError.NoFloorLeft, ErrorKind.InvalidState);
            }

            return _loadouts.Resolve(Backpack).Weapons.Count == 0
                ? Result.Fail(RunError.NoArcaneWeapon, ErrorKind.InvalidState)
                : Result.Ok();
        }

        /// <summary>
        /// Where a piece dropped on the board would fold: the item instance whose cell is
        /// <paramref name="cell"/>, or <see cref="PieceRef.None"/> when no item is there.
        /// </summary>
        private PieceRef ItemAt(CellOffset cell)
        {
            int owner = Backpack.Board.ItemOwnerAt(cell);
            return owner == Board.NoOwner ? PieceRef.None : new PieceRef(PieceKind.Item, owner);
        }

        // -------------------------------------------------------------------
        // internals
        // -------------------------------------------------------------------

        private void EnterCamp()
        {
            FlowState = RunFlowState.Camp;
            Editor.SetLocked(false);
            Editor.MarkCampEntry();
            Stock.BeginCamp();
            _roller.Roll(Stock, FloorIndex, _streams.Shop);

            Emit(RunEventKind.ShopStockChanged, -1);
            Emit(RunEventKind.FlowChanged, (int)FlowState);
        }

        private Result RequireCamp() =>
            FlowState == RunFlowState.Camp
                ? Result.Ok()
                : Result.Fail(RunError.WrongState, ErrorKind.InvalidState);

        private void EmitBagChanged()
        {
            Emit(RunEventKind.InventoryChanged, Backpack.Revision);
            Emit(RunEventKind.LoadoutChanged, Backpack.Revision);
        }

        private void Emit(RunEventKind kind, int value = 0, string id = "") =>
            _events.Add(new RunEvent(kind, value, id));

        private void Record(in RunCommand command)
        {
            if (_recording)
            {
                _log.Record(command);
            }
        }

        /// <summary>
        /// Checks that the starting kit exists, fits the board and has chest room. Runs
        /// before the first mutation so <see cref="StartRun"/> is atomic too.
        /// </summary>
        private Result ValidateStartKit()
        {
            if (!_config.TryGetBag(RunDefaults.InitialBagId, out BagConfig bag))
            {
                return Result.Fail(RunError.UnknownPiece, ErrorKind.NotFound);
            }

            if (!_config.TryGetItem(RunDefaults.InitialWeaponId, out ItemConfig weapon))
            {
                return Result.Fail(RunError.UnknownPiece, ErrorKind.NotFound);
            }

            if (_config.Economy.ChestSlots < 2)
            {
                // Both pieces land in the chest first, so they need two slots.
                return Result.Fail(InventoryError.ChestFull, ErrorKind.Insufficient);
            }

            var bagOrigin = new CellOffset(RunDefaults.InitialBagX, RunDefaults.InitialBagY);
            if (!Backpack.Board.CanPlaceBag(Board.NoOwner, bag.Footprint, bagOrigin, out Error bagError))
            {
                return Result.Fail(bagError);
            }

            // The weapon is laid down before any other bag exists, so every cell of it
            // has to be inside the starting bag's own footprint.
            var weaponOrigin = new CellOffset(RunDefaults.InitialWeaponX, RunDefaults.InitialWeaponY);
            for (int i = 0; i < weapon.Footprint.Cells.Count; i++)
            {
                CellOffset cell = weapon.Footprint.Cells[i];
                var local = new CellOffset(
                    weaponOrigin.X + cell.X - bagOrigin.X,
                    weaponOrigin.Y + cell.Y - bagOrigin.Y);

                if (!bag.Footprint.Contains(local))
                {
                    return Result.Fail(InventoryError.ItemNotCovered, ErrorKind.Conflict);
                }
            }

            return Result.Ok();
        }

        /// <summary>
        /// Checks that the bag can cover what the floor says it used.
        /// <see cref="BattleResult.RemainingConsumables"/> is aligned with the loadout the
        /// floor started from, and the difference is the number of offerings that are gone.
        /// </summary>
        private Result ValidateConsumables(CombatLoadoutSnapshot loadout, BattleResult result)
        {
            IReadOnlyList<int> remaining = result.RemainingConsumables;
            if (remaining is null || remaining.Count == 0)
            {
                return Result.Ok();
            }

            if (remaining.Count != loadout.Consumables.Count)
            {
                // The list is aligned with the loadout the floor started from, so a
                // different length means the two sides disagree about what the player
                // carried. Paying HP and bia for that would harden the desync.
                return Result.Fail(InventoryError.NotEnoughPieces, ErrorKind.InvalidState);
            }

            for (int i = 0; i < loadout.Consumables.Count; i++)
            {
                ConsumableLoadout entry = loadout.Consumables[i];
                int used = entry.Count - ClampedRemaining(remaining[i], entry.Count);
                if (used > 0 && !Editor.CanConsume(entry.ItemId, used))
                {
                    return Result.Fail(InventoryError.NotEnoughPieces, ErrorKind.InvalidState);
                }
            }

            return Result.Ok();
        }

        /// <summary>Removes the offerings the floor used. Precondition: <see cref="ValidateConsumables"/> passed.</summary>
        private void ApplyConsumables(CombatLoadoutSnapshot loadout, BattleResult result)
        {
            IReadOnlyList<int> remaining = result.RemainingConsumables;
            if (remaining is null || remaining.Count == 0)
            {
                return;
            }

            for (int i = 0; i < loadout.Consumables.Count && i < remaining.Count; i++)
            {
                ConsumableLoadout entry = loadout.Consumables[i];
                int used = entry.Count - ClampedRemaining(remaining[i], entry.Count);
                if (used > 0)
                {
                    Editor.Consume(entry.ItemId, used);
                }
            }
        }

        private static int ClampedRemaining(int left, int count)
        {
            if (left < 0)
            {
                return 0;
            }

            return left > count ? count : left;
        }

        private static int CompareFloors(FloorConfig left, FloorConfig right) =>
            left.Index.CompareTo(right.Index);

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
