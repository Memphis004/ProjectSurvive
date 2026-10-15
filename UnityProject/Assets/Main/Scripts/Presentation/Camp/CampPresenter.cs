using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The Camp screen's logic: it turns a gesture or a button into a <see cref="RunSession"/>
    /// command and a command into the view models the screen paints.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10 is the whole design here. The presenter <b>calls Application and reads
    /// state</b>; it never decides a price, a payout, a combo or a placement rule. Everything the
    /// screen asks a question about — "can this piece go there?", "what would the altar pay?",
    /// "may I depart, and why not?" — is asked of the run, which asks the same
    /// <c>BackpackEditor</c> predicate the accepted command will ask. There is deliberately no
    /// second implementation of a rule anywhere in this file, and the architecture test that
    /// scans <c>Presentation/</c> for rule types reads this file too.
    /// </para>
    /// <para>
    /// <b>Nothing is formatted during a drag.</b> A pointer move republishes one value type
    /// (<see cref="CampDragState"/>) and, only when the refusal <i>code</i> changes, one string.
    /// The tooltip and the piece captions are rebuilt when a command moved the bag or the pointer
    /// changed piece — never per frame. That is what makes the stage's "no GC spike while
    /// dragging" measurable rather than hopeful.
    /// </para>
    /// </remarks>
    public sealed class CampPresenter : IDisposable
    {
        private readonly RunSession _run;
        private readonly IGameConfig _config;
        private readonly LocalizationService _text;
        private readonly CampUiState _state;
        private readonly StringBuilder _builder = new StringBuilder(192);

        private readonly List<CampPieceModel> _pieces = new List<CampPieceModel>(32);
        private readonly List<CellOffset> _highlight = new List<CellOffset>(48);
        private readonly CampShopSlotModel[] _shop;

        private SynergyEvaluation _layout;
        private int _layoutRevision = -1;

        private CampDragState _drag = CampDragState.None;
        private Rotation _dragRotation = Rotation.Degrees0;
        private CellOffset _dragAnchor;
        private PieceRef _selected = PieceRef.None;
        private PieceRef _hovered = PieceRef.None;
        private string _statusCode = string.Empty;
        private int _altarOffer = -1;
        private bool _disposed;

        /// <summary>Creates a presenter over one run and one screen state.</summary>
        public CampPresenter(RunSession run, IGameConfig config, LocalizationService text, CampUiState state)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _text = text ?? throw new ArgumentNullException(nameof(text));
            _state = state ?? throw new ArgumentNullException(nameof(state));

            _shop = new CampShopSlotModel[run.Stock.SlotCount];
        }

        /// <summary>The run every command goes through.</summary>
        public RunSession Run => _run;

        /// <summary>How many times each command was accepted (the browser report's numbers).</summary>
        public CampCounters Counters { get; } = new CampCounters();

        /// <summary>True while a piece is being dragged.</summary>
        public bool IsDragging => _drag.IsActive;

        /// <summary>The piece under the pointer.</summary>
        public PieceRef DraggedPiece => _drag.Piece;

        /// <summary>The cell the drag started from, used when the pointer is over nothing.</summary>
        public CellOffset DragOrigin => _drag.IsActive ? _drag.Origin : _dragAnchor;

        /// <summary>The rotation the drag would place with.</summary>
        public Rotation DragRotation => _dragRotation;

        /// <summary>The whole drag state, for a test or a report.</summary>
        public CampDragState Drag => _drag;

        /// <summary>The piece a click selected, for the rotate button.</summary>
        public PieceRef Selected => _selected;

        /// <summary>The last refusal code shown, or an empty string.</summary>
        public string StatusCode => _statusCode;

        /// <summary>The bag screen as of the last publish.</summary>
        public CampBagScreen Bag => _state.Bag.Value;

        /// <summary>Cells the working combos connect around the hovered piece.</summary>
        public IReadOnlyList<CellOffset> Highlight => _highlight;

        /// <summary>True once the run has departed for the floor.</summary>
        public bool Departed { get; private set; }

        /// <summary>The floor the departure targets (0 before one).</summary>
        public int DepartFloor { get; private set; }

        /// <summary>The seed drawn for the departure's battle (0 before one).</summary>
        public uint DepartSeed { get; private set; }

        /// <summary>
        /// Publishes every caption and fills the screen. The view may subscribe before or after
        /// this; R3 hands a new subscriber the current value immediately, so the order does not
        /// matter.
        /// </summary>
        public void Begin()
        {
            CampUiState.Set(_state.Title, Lookup("camp.title"));
            CampUiState.Set(_state.ShopCaption, Lookup("camp.shop.caption"));
            CampUiState.Set(_state.BagCaption, Lookup("camp.bag.caption"));
            CampUiState.Set(_state.ChestCaption, Lookup("camp.chest.caption"));
            CampUiState.Set(_state.AltarCaption, Lookup("camp.altar.caption"));
            CampUiState.Set(_state.HintCaption, Lookup("camp.world.hint"));
            CampUiState.Set(_state.NpcName, Lookup("camp.npc.name"));
            CampUiState.Set(_state.RentedLabel, Lookup("camp.shop.rented"));
            CampUiState.Set(_state.PinLabel, Lookup("camp.shop.lock"));
            CampUiState.Set(_state.UnpinLabel, Lookup("camp.shop.unlock"));
            CampUiState.Set(_state.EmptySlotLabel, Lookup("camp.shop.empty"));
            CampUiState.Set(_state.ResetLabel, Lookup("camp.bag.reset"));
            CampUiState.Set(_state.RotateLabel, Lookup("camp.bag.rotate"));
            CampUiState.Set(_state.UndoLabel, Lookup("camp.bag.undo"));
            CampUiState.Set(_state.DepartLabel, Lookup("camp.depart"));
            CampUiState.Set(_state.CloseLabel, Lookup("ui.generic.close"));
            CampUiState.Set(_state.NextFloorLabel, Lookup("camp.depart.next"));
            CampUiState.Set(_state.LockRestoreLabel, Lookup("camp.bag.lock_restore"));
            CampUiState.Set(_state.InstructionsTitle, Lookup("camp.instructions.title"));
            CampUiState.Set(_state.Instruction1, Lookup("camp.instructions.1"));
            CampUiState.Set(_state.Instruction2, Lookup("camp.instructions.2"));
            CampUiState.Set(_state.Instruction3, Lookup("camp.instructions.3"));
            CampUiState.Set(_state.Instruction4, Lookup("camp.instructions.4"));
            CampUiState.Set(_state.WeightCaption, Lookup("camp.stat.weight"));
            CampUiState.Set(_state.FocusCaption, Lookup("camp.stat.focus"));
            CampUiState.Set(_state.ChestStatCaption, Lookup("camp.stat.chest"));
            CampUiState.Set(_state.BiaCaption, Lookup("camp.stat.bia"));
            CampUiState.Set(_state.RefreshCostCaption, Lookup("camp.stat.refresh"));

            Refresh();
        }

        /// <summary>Re-reads the run and republishes the bars, the shop, the bag and the gate.</summary>
        public void Refresh()
        {
            PublishBars();
            PublishShop();
            PublishBag();
            PublishDepart();
            PublishTooltip();
        }

        // ------------------------------------------------------------------
        // shop
        // ------------------------------------------------------------------

        /// <summary>Rents the offer in one slot ("เช่า"), landing it in the chest.</summary>
        public void Rent(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _run.Stock.SlotCount)
            {
                SetStatus(ShopError.UnknownSlot);
                return;
            }

            Result<PurchaseOutcome> rented = _run.Purchase(slotIndex);

            if (rented.IsSuccess)
            {
                Counters.Rented++;
                ClearStatus();
            }
            else
            {
                SetStatus(rented.Error.Code);
            }

            Refresh();
        }

        /// <summary>Pins or unpins one offer so a re-roll keeps it ("ล็อกสินค้าที่อยากเก็บไว้").</summary>
        public void ToggleSlotLock(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _run.Stock.SlotCount)
            {
                SetStatus(ShopError.UnknownSlot);
                return;
            }

            bool locked = !_run.Stock[slotIndex].IsLocked;
            Result<ShopSlot> pinned = _run.SetSlotLocked(slotIndex, locked);

            if (pinned.IsSuccess)
            {
                Counters.SlotLocks++;
                ClearStatus();
            }
            else
            {
                SetStatus(pinned.Error.Code);
            }

            Refresh();
        }

        /// <summary>Re-rolls the shop ("เสี่ยงเซียมซี") for the price the data sets.</summary>
        public void RefreshShop()
        {
            Result<RefreshOutcome> rolled = _run.RefreshShop();

            if (rolled.IsSuccess)
            {
                Counters.Refreshes++;
                ClearStatus();
            }
            else
            {
                SetStatus(rolled.Error.Code);
            }

            Refresh();
        }

        // ------------------------------------------------------------------
        // bag buttons
        // ------------------------------------------------------------------

        /// <summary>
        /// Turns a piece a quarter turn: the dragged one while a drag is in progress, otherwise
        /// the selected one. This is the backup control for R and right-click ("มีปุ่ม UI สำรอง").
        /// </summary>
        public void RotateSelected()
        {
            if (_drag.IsActive)
            {
                RotateDrag();
                return;
            }

            if (!_selected.IsValid)
            {
                SetStatus(InventoryError.UnknownPiece);
                return;
            }

            Result rotated = _run.RotatePiece(_selected);

            if (rotated.IsSuccess)
            {
                Counters.Rotated++;
                ClearStatus();
            }
            else
            {
                SetStatus(rotated.Error.Code);
            }

            Refresh();
        }

        /// <summary>Returns the bag to how it looked when the run entered Camp ("คืนสภาพ").</summary>
        /// <param name="lockAfterwards">
        /// The lock-on-restore checkbox ("ล็อกย่ามตอนคืนสภาพ"). When set, a restore that landed
        /// locks the bag in the same command, so the picture the player just restored cannot be
        /// changed by accident before they set out. A restore that was refused locks nothing —
        /// rule 7: the state only changes when the command succeeded.
        /// </param>
        public void ResetBag(bool lockAfterwards = false)
        {
            Result reset = _run.Reset();

            if (reset.IsFailure)
            {
                SetStatus(reset.Error.Code);
                Refresh();
                return;
            }

            Counters.Resets++;
            ClearStatus();

            if (lockAfterwards && !_run.IsBagLocked)
            {
                Result locked = _run.SetBagLocked(true);
                if (locked.IsFailure)
                {
                    SetStatus(locked.Error.Code);
                }
                else
                {
                    Counters.LockToggles++;
                }
            }

            Refresh();
        }

        /// <summary>Undoes the last accepted bag edit.</summary>
        public void UndoEdit()
        {
            Result undone = _run.Undo();

            if (undone.IsSuccess)
            {
                Counters.Undos++;
                ClearStatus();
            }
            else
            {
                SetStatus(undone.Error.Code);
            }

            Refresh();
        }

        /// <summary>Locks or unlocks bag editing ("ล็อกย่าม").</summary>
        public void ToggleBagLock()
        {
            Result locked = _run.SetBagLocked(!_run.IsBagLocked);

            if (locked.IsSuccess)
            {
                Counters.LockToggles++;
                ClearStatus();
            }
            else
            {
                SetStatus(locked.Error.Code);
            }

            Refresh();
        }

        /// <summary>Selects the piece the rotate button acts on when nothing is being dragged.</summary>
        public void SelectOnly(PieceRef piece)
        {
            _selected = piece;

            if (piece.IsValid)
            {
                Hover(piece);
            }
        }

        // ------------------------------------------------------------------
        // drag
        // ------------------------------------------------------------------

        /// <summary>
        /// Starts dragging <paramref name="piece"/>, keeping the orientation it already has so a
        /// piece that was turned stays turned.
        /// </summary>
        public void DragStart(PieceRef piece, Rotation rotation)
        {
            if (!piece.IsValid)
            {
                return;
            }

            _selected = piece;

            if (_run.FlowState != RunFlowState.Camp)
            {
                SetStatus(RunError.WrongState);
                return;
            }

            if (_run.IsBagLocked)
            {
                SetStatus(InventoryError.Locked);
                return;
            }

            _dragRotation = rotation;

            if (_run.Backpack.TryGetPiece(piece, out PieceInstance instance) && instance != null
                && instance.Zone == PieceZone.Board)
            {
                _dragAnchor = instance.Origin;
            }
            else
            {
                _dragAnchor = new CellOffset(0, 0);
            }

            _drag = new CampDragState(
                true, piece, BagDropZone.Board, _dragAnchor, _dragRotation,
                BagDropAction.Place, true, 0, PieceRef.None, string.Empty);

            _drag = Evaluate(BagDropZone.Board, _dragAnchor);
            PublishDrag();
        }

        /// <summary>
        /// Moves the drag to a surface and a cell. The whole preview — action, legality, target,
        /// payout, refusal code — comes from the run on every call.
        /// </summary>
        public void DragMove(BagDropZone zone, CellOffset cell)
        {
            if (!_drag.IsActive)
            {
                return;
            }

            CampDragState next = Evaluate(zone, cell);
            if (next.Equals(_drag))
            {
                // The common case during a drag: the pointer moved inside the same cell with the
                // same verdict. Nothing is republished and nothing is allocated.
                Counters.DragMoves++;
                return;
            }

            _drag = next;
            PublishDrag();
        }

        /// <summary>Turns the dragged piece a quarter turn.</summary>
        public void RotateDrag()
        {
            if (!_drag.IsActive)
            {
                return;
            }

            _dragRotation = RotationOps.Clockwise(_dragRotation);
            Counters.DragRotations++;

            _drag = Evaluate(_drag.Zone, _drag.Origin);
            PublishDrag();
        }

        /// <summary>
        /// Cancels the drag (Escape) and puts the piece back where it was: the Domain was never
        /// told about the gesture, so there is nothing to undo.
        /// </summary>
        public void CancelDrag()
        {
            if (!_drag.IsActive)
            {
                return;
            }

            Counters.DragCancels++;
            EndDrag();
            ClearStatus();
        }

        /// <summary>
        /// Releases the drag: applies the command the preview described, or refuses it with the
        /// code the run gave.
        /// </summary>
        public void Drop()
        {
            if (!_drag.IsActive)
            {
                return;
            }

            PieceRef piece = _drag.Piece;
            BagDropZone zone = _drag.Zone;
            CellOffset origin = _drag.Origin;
            Rotation rotation = _drag.Rotation;

            EndDrag();

            // The drop re-asks the run rather than trusting the last preview: if anything moved
            // underneath the pointer, the command that runs is the one that is true now.
            DropPreview preview = _run.PreviewDrop(piece, zone, origin, rotation);

            if (!preview.IsValid)
            {
                Counters.InvalidDrops++;
                SetStatus(preview.ErrorCode);
                PublishBag();
                return;
            }

            Result applied = Apply(preview, piece, origin, rotation);

            if (applied.IsSuccess)
            {
                ClearStatus();
            }
            else
            {
                SetStatus(applied.Error.Code);
            }

            Refresh();
        }

        /// <summary>Points the tooltip and the combo highlight at a piece (or at nothing).</summary>
        public void Hover(PieceRef piece)
        {
            if (_hovered == piece)
            {
                return;
            }

            _hovered = piece;
            PublishTooltip();
            PublishBag();
        }

        /// <summary>Drops the tooltip.</summary>
        public void ClearHover() => Hover(PieceRef.None);

        // ------------------------------------------------------------------
        // departure
        // ------------------------------------------------------------------

        /// <summary>
        /// Departs for the floor ("ออกเดินทาง"): asks the run whether it may, and performs the
        /// real command when it may, so the run's state matches Core exactly.
        /// </summary>
        /// <remarks>
        /// The scene change that follows a departure is Stage 10's, and the screen says so: after
        /// the command the run is in <see cref="RunFlowState.Battle"/>, the bag is locked, the
        /// button is disabled and every other control is refused with <c>run.wrong_state</c> — the
        /// same refusal the run would give any other phase. The one-shot latch
        /// (<see cref="Departed"/>) is the "กันคลิกซ้ำ" half: a second click cannot reach
        /// <c>StartFloor</c> even if a UI event slipped through.
        /// </remarks>
        public void Depart()
        {
            if (Departed)
            {
                return;
            }

            Result check = _run.DepartCheck();
            if (check.IsFailure)
            {
                SetStatus(check.Error.Code);
                Refresh();
                return;
            }

            Result<BattleSetup> departed = _run.StartFloor();
            if (departed.IsFailure)
            {
                SetStatus(departed.Error.Code);
                Refresh();
                return;
            }

            Departed = true;
            DepartFloor = departed.Value.FloorIndex;
            DepartSeed = departed.Value.Seed;
            Counters.Departures++;
            ClearStatus();
            Refresh();
        }

        // ------------------------------------------------------------------
        // reads for the report
        // ------------------------------------------------------------------

        /// <summary>The synergy evaluation the tooltip and the highlight came from.</summary>
        public SynergyEvaluation Layout() => CachedLayout();

        /// <inheritdoc />
        /// <summary>
        /// Releases the screen. Every push after this is dropped rather than thrown.
        /// </summary>
        /// <remarks>
        /// A view can outlive the scope by one frame. Tearing the Camp scene down disables the
        /// EventSystem's input module, which dispatches a pointer exit to the bag screen as it goes;
        /// that handler asks this presenter to clear its hover, and by then the <c>CampUiState</c>
        /// this presenter writes into has been disposed with the scope. R3 throws
        /// <c>ObjectDisposedException</c> for a write to a disposed <c>ReactiveProperty</c>, so
        /// without the guards in the publishers below every scene change logged an exception —
        /// measured, see docs/stages/09b-report.md.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        // ------------------------------------------------------------------
        // internals
        // ------------------------------------------------------------------

        private CampDragState Evaluate(BagDropZone zone, CellOffset cell)
        {
            bool overAGrid = zone == BagDropZone.Board || zone == BagDropZone.Chest;
            CellOffset origin = overAGrid ? cell : _dragAnchor;

            DropPreview preview = _run.PreviewDrop(_drag.Piece, zone, origin, _dragRotation);
            Counters.DragPreviews++;

            if (!preview.IsValid)
            {
                Counters.InvalidPreviews++;
            }

            return new CampDragState(
                true, _drag.Piece, zone, origin, _dragRotation, preview.Action,
                preview.IsValid, preview.Bia, preview.Target,
                preview.IsValid ? string.Empty : preview.ErrorCode);
        }

        private void EndDrag()
        {
            _dragRotation = Rotation.Degrees0;
            _drag = CampDragState.None;
            PublishDrag();
        }

        private void PublishDrag()
        {
            if (_disposed)
            {
                return;
            }

            _state.Drag.Value = _drag;

            int offer = _drag.IsActive && _drag.Action == BagDropAction.Recycle ? _drag.Bia : -1;
            if (offer != _altarOffer)
            {
                // Only when the number changes: a drag that hovers the pot must not build the same
                // string on every pointer move.
                _altarOffer = offer;
                CampUiState.Set(
                    _state.AltarOffer, offer < 0 ? string.Empty : Format("camp.altar.format", offer));
            }

            SetStatus(_drag.IsActive && !_drag.IsValid ? _drag.ErrorCode : string.Empty);
        }

        private Result Apply(in DropPreview preview, PieceRef piece, CellOffset origin, Rotation rotation)
        {
            switch (preview.Action)
            {
                case BagDropAction.Place:
                {
                    Result moved = _run.MovePiece(piece, origin, rotation);
                    if (moved.IsSuccess)
                    {
                        Counters.Placed++;
                    }

                    return moved;
                }

                case BagDropAction.Merge:
                {
                    Result<MergeOutcome> merged = _run.MergeItems(piece, preview.Target);
                    if (merged.IsSuccess)
                    {
                        Counters.Merged++;
                    }

                    return merged.IsSuccess ? Result.Ok() : Result.Fail(merged.Error);
                }

                case BagDropAction.Transfer:
                {
                    Result stored = _run.TransferToChest(piece);
                    if (stored.IsSuccess)
                    {
                        Counters.Stored++;
                    }

                    return stored;
                }

                case BagDropAction.Recycle:
                {
                    Result<RecycleOutcome> sold = _run.Sell(piece);
                    if (sold.IsSuccess)
                    {
                        Counters.Recycled++;
                    }

                    return sold.IsSuccess ? Result.Ok() : Result.Fail(sold.Error);
                }

                default:
                    return Result.Fail(preview.ErrorCode);
            }
        }

        private void PublishBars()
        {
            if (_disposed)
            {
                return;
            }

            CampUiState.Set(_state.FloorValue, Format("camp.floor.format", _run.FloorIndex));
            int maxMp = _config.Constant.MaxMp;
            CampUiState.Set(_state.HpValue, $"{_run.Hp}/{_run.MaxHp}");
            CampUiState.Set(_state.MpValue, $"{maxMp}/{maxMp}");
            CampUiState.Set(_state.HpMpValue, $"HP {_run.Hp}/{_run.MaxHp}  MP {maxMp}/{maxMp}");
            CampUiState.Set(
                _state.WeightValue,
                Format("camp.weight.format", _run.BagWeight, _run.BagWeightPenaltyPercent));

            int cap = _config.Constant.MaxSpeedPenaltyPercent;
            CampUiState.Set(
                _state.WeightBar,
                cap <= 0 ? 0f : Mathf.Clamp01(_run.BagWeightPenaltyPercent / (float)cap));

            CampUiState.Set(_state.FocusValue, Format("camp.focus.format", _run.FocusPerSecond));
            CampUiState.Set(_state.ChestValue, Format("camp.chest.format", _run.ChestUsed, _run.ChestSlots));
            CampUiState.Set(_state.BiaValue, Number(_run.Wallet.Bia));
            CampUiState.Set(_state.RefreshCostValue, Number(_run.RefreshPrice));
            CampUiState.Set(_state.RefreshButtonLabel, Format("camp.refresh.format", _run.RefreshPrice));
            CampUiState.Set(_state.CanRefresh, _run.FlowState == RunFlowState.Camp);
            CampUiState.Set(_state.CanReset, _run.FlowState == RunFlowState.Camp && !_run.IsBagLocked);
            CampUiState.Set(_state.BagLocked, _run.IsBagLocked);
            CampUiState.Set(_state.LockLabel, Lookup(_run.IsBagLocked ? "camp.bag.unlock" : "camp.bag.lock"));
        }

        private void PublishShop()
        {
            if (_disposed)
            {
                return;
            }

            for (int i = 0; i < _shop.Length; i++)
            {
                ShopSlot slot = _run.Stock[i];
                string name = slot.IsEmpty ? string.Empty : PieceName(slot.Kind, slot.OfferedId);

                _shop[i] = new CampShopSlotModel(
                    i, slot.OfferedId, name, Number(slot.Price), slot.Price, slot.Kind,
                    slot.IsEmpty, slot.IsPurchased, slot.IsLocked, _run.Wallet.CanAfford(slot.Price));
            }

            _state.Shop.Value = (CampShopSlotModel[])_shop.Clone();
        }

        private void PublishBag()
        {
            if (_disposed)
            {
                return;
            }

            Backpack backpack = _run.Backpack;

            _pieces.Clear();

            IReadOnlyList<BagPieceInstance> bags = backpack.Bags;
            for (int i = 0; i < bags.Count; i++)
            {
                _pieces.Add(Model(bags[i]));
            }

            IReadOnlyList<ItemInstance> items = backpack.Items;
            for (int i = 0; i < items.Count; i++)
            {
                _pieces.Add(Model(items[i]));
            }

            BuildHighlight();

            _state.Bag.Value = new CampBagScreen(
                backpack.Board.Width,
                backpack.Board.Height,
                _run.ChestSlots,
                _run.IsBagLocked,
                _run.CanUndoEdit,
                _pieces.ToArray(),
                _highlight.ToArray(),
                backpack.Revision);
        }

        private CampPieceModel Model(PieceInstance instance)
        {
            string name = PieceName(instance.Kind, instance.DefId);
            string label = Format("camp.piece.label", name, instance.Tier);

            return new CampPieceModel(
                instance.Ref,
                instance.DefId,
                name,
                label,
                instance.Tier,
                instance.Kind,
                instance.Zone,
                instance.Origin,
                instance.Rotation,
                instance.ChestSlot,
                _run.Backpack.ShapeAt(instance, instance.Rotation));
        }

        /// <summary>
        /// The cells a combo connects around the hovered piece: the piece itself plus every piece
        /// the run says it is in a working relation with (GDD "synergy ที่ทำงานอยู่ (highlight
        /// ช่องที่เชื่อมกัน)").
        /// </summary>
        /// <remarks>
        /// The relations come from the run's own evaluation — this method only turns two instance
        /// ids into cells, which is geometry, not a rule.
        /// </remarks>
        private void BuildHighlight()
        {
            _highlight.Clear();

            if (!_hovered.IsValid)
            {
                return;
            }

            AddCellsOf(_hovered);

            SynergyEvaluation layout = CachedLayout();
            IReadOnlyList<ActiveSynergy> active = layout.Active;

            for (int i = 0; i < active.Count; i++)
            {
                ActiveSynergy synergy = active[i];

                if (synergy.SourceInstanceId == _hovered.InstanceId)
                {
                    AddCellsOf(new PieceRef(PieceKind.Item, synergy.TargetInstanceId));
                }
                else if (synergy.TargetInstanceId == _hovered.InstanceId)
                {
                    AddCellsOf(new PieceRef(PieceKind.Item, synergy.SourceInstanceId));
                    AddCellsOf(new PieceRef(PieceKind.Bag, synergy.SourceInstanceId));
                }
            }
        }

        private void AddCellsOf(PieceRef piece)
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                CampPieceModel model = _pieces[i];
                if (model.Piece != piece || !model.IsOnBoard || model.Shape == null)
                {
                    continue;
                }

                for (int c = 0; c < model.Shape.CellCount; c++)
                {
                    CellOffset offset = model.Shape.CellAt(c);
                    AddCell(new CellOffset(model.Origin.X + offset.X, model.Origin.Y + offset.Y));
                }

                return;
            }
        }

        private void AddCell(CellOffset cell)
        {
            for (int i = 0; i < _highlight.Count; i++)
            {
                if (_highlight[i] == cell)
                {
                    return;
                }
            }

            _highlight.Add(cell);
        }

        private void PublishTooltip()
        {
            if (_disposed)
            {
                return;
            }

            if (!_hovered.IsValid)
            {
                CampUiState.Set(_state.TooltipVisible, false);
                return;
            }

            CampPieceModel model = default;
            bool found = false;

            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].Piece == _hovered)
                {
                    model = _pieces[i];
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                CampUiState.Set(_state.TooltipVisible, false);
                return;
            }

            CampUiState.Set(_state.TooltipTitle, model.Name);
            CampUiState.Set(_state.TooltipStats, StatLine(model));
            CampUiState.Set(_state.TooltipCombos, ComboLine(model));
            CampUiState.Set(_state.TooltipVisible, true);
        }

        private string StatLine(CampPieceModel model)
        {
            if (model.IsBag)
            {
                return model.Shape == null
                    ? string.Empty
                    : Format("camp.tooltip.slots.format", model.Shape.CellCount);
            }

            if (!_config.TryGetItem(model.DefId, out ItemConfig item))
            {
                return string.Empty;
            }

            _builder.Clear();
            _builder.Append(Format("camp.tooltip.tier.format", model.Tier, _config.Constant.MaxTier));

            if (!string.IsNullOrEmpty(item.SkillId) && _config.TryGetSkill(item.SkillId, out SkillConfig skill))
            {
                _builder.Append('\n');
                _builder.Append(Format("camp.tooltip.stat.format", skill.Damage, skill.CooldownSeconds, skill.MpCost));
                return _builder.ToString();
            }

            _builder.Append('\n');
            _builder.Append(Lookup(CategoryKey(item.Category)));
            return _builder.ToString();
        }

        private string ComboLine(CampPieceModel model)
        {
            SynergyEvaluation layout = CachedLayout();
            ItemModifiers modifiers = layout.For(model.Piece.InstanceId);

            _builder.Clear();

            if (modifiers.ActiveSynergyCount > 0)
            {
                _builder.Append(Format(
                    "camp.tooltip.combo.format",
                    modifiers.ActiveSynergyCount,
                    modifiers.DamageMultiplier,
                    modifiers.CooldownMultiplier));
            }

            IReadOnlyList<ActiveSynergy> active = layout.Active;
            for (int i = 0; i < active.Count; i++)
            {
                ActiveSynergy synergy = active[i];
                bool involved = synergy.TargetInstanceId == model.Piece.InstanceId
                    || synergy.SourceInstanceId == model.Piece.InstanceId;

                if (!involved)
                {
                    continue;
                }

                if (_builder.Length > 0)
                {
                    _builder.Append(" · ");
                }

                _builder.Append(RuleName(synergy.RuleId));
            }

            if (_builder.Length == 0)
            {
                return Lookup("camp.tooltip.combo.none");
            }

            return Format("camp.tooltip.rules.format", _builder.ToString());
        }

        private string RuleName(string ruleId)
        {
            for (int i = 0; i < _config.Synergies.Count; i++)
            {
                if (string.Equals(_config.Synergies[i].Id, ruleId, StringComparison.Ordinal))
                {
                    return Lookup(_config.Synergies[i].NameKey);
                }
            }

            return ruleId;
        }

        private SynergyEvaluation CachedLayout()
        {
            if (_layout == null || _layoutRevision != _run.Backpack.Revision)
            {
                // Once per backpack revision: a combo is recomputed when the set changes, never
                // per frame (GDD "คำนวณใหม่เมื่อชุดอุปกรณ์เปลี่ยนเท่านั้น").
                _layout = _run.EvaluateLayout();
                _layoutRevision = _run.Backpack.Revision;
            }

            return _layout;
        }

        private void PublishDepart()
        {
            if (_disposed)
            {
                return;
            }

            if (_run.FlowState != RunFlowState.Camp)
            {
                CampUiState.Set(_state.CanDepart, false);
                CampUiState.Set(
                    _state.DepartReason,
                    Lookup(Departed ? "camp.departed" : "error." + RunError.WrongState));
                return;
            }

            Result check = _run.DepartCheck();
            CampUiState.Set(_state.CanDepart, check.IsSuccess);
            CampUiState.Set(
                _state.DepartReason, check.IsSuccess ? string.Empty : Lookup("error." + check.Error.Code));
        }

        private void SetStatus(string code)
        {
            if (_disposed)
            {
                return;
            }

            string next = code ?? string.Empty;

            if (string.Equals(_statusCode, next, StringComparison.Ordinal))
            {
                // The refusal did not change, so the label does not change either: one string per
                // distinct reason instead of one per pointer move.
                return;
            }

            _statusCode = next;
            CampUiState.Set(
                _state.StatusLine, next.Length == 0 ? string.Empty : Lookup("error." + next));
        }

        private void ClearStatus() => SetStatus(string.Empty);

        private string PieceName(PieceKind kind, string defId)
        {
            if (kind == PieceKind.Bag)
            {
                return _config.TryGetBag(defId, out BagConfig bag) ? Lookup(bag.NameKey) : defId;
            }

            return _config.TryGetItem(defId, out ItemConfig item) ? Lookup(item.NameKey) : defId;
        }

        private static string CategoryKey(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.ArcaneWeapon:
                    return "camp.category.arcane_weapon";
                case ItemCategory.Charm:
                    return "camp.category.charm";
                case ItemCategory.SpellTome:
                    return "camp.category.tome";
                case ItemCategory.Offering:
                    return "camp.category.offering";
                case ItemCategory.Familiar:
                    return "camp.category.familiar";
                default:
                    return "camp.category.unknown";
            }
        }

        private string Lookup(string key) => _text.TryGet(key, out string value) ? value : key;

        private string Format(string key, params object[] arguments)
        {
            string format = Lookup(key);
            return string.Format(CultureInfo.InvariantCulture, format, arguments);
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>How many times each Camp command was accepted. Read by the browser report.</summary>
    public sealed class CampCounters
    {
        public int Rented;
        public int Refreshes;
        public int SlotLocks;
        public int Placed;
        public int Stored;
        public int Merged;
        public int Recycled;
        public int Rotated;
        public int Resets;
        public int Undos;
        public int LockToggles;
        public int DragPreviews;
        public int InvalidPreviews;
        public int InvalidDrops;
        public int DragMoves;
        public int DragRotations;
        public int DragCancels;
        public int Departures;
    }
}
