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

        /// <summary>Second builder for a line inside a line (a step's comma-separated changes).</summary>
        private readonly StringBuilder _detail = new StringBuilder(96);

        private readonly List<CampPieceModel> _pieces = new List<CampPieceModel>(32);
        private readonly List<CellOffset> _highlight = new List<CellOffset>(48);
        private readonly CampShopSlotModel[] _shop;

        private readonly List<CampLink> _linkScratch = new List<CampLink>(16);

        /// <summary>
        /// Scratch for a bag drag's riders: the run's plan, then the ghosts it becomes. Reused, because
        /// a pointer move must not allocate (Stage 09's measured promise, still measured here).
        /// </summary>
        private readonly List<BagCarry> _carryScratch = new List<BagCarry>(8);
        private readonly List<CampCarryGhost> _carryGhostScratch = new List<CampCarryGhost>(8);

        /// <summary>The plan of a drag that carries nothing: shared, because it is constant.</summary>
        private static readonly CampCarryGhost[] NoCarry = new CampCarryGhost[0];

        private SynergyEvaluation _layout;
        private int _layoutRevision = -1;
        private CampLink[] _links;
        private int _linksRevision = -1;

        /// <summary>The mark set of a layout with no working combo: shared, because it is constant.</summary>
        private static readonly CampLink[] NoLinks = new CampLink[0];

        /// <summary>
        /// The merge chooser's scratch: the source lines of the piece being upgraded, the cards
        /// built for the offer, and each card's stat lines. Reused, because opening the chooser must
        /// not allocate a fresh list per open (Stage 09g).
        /// </summary>
        private readonly List<string> _sourceScratch = new List<string>(6);
        private readonly List<CampMergeChoiceModel> _choiceScratch = new List<CampMergeChoiceModel>(4);
        private readonly List<string> _changeScratch = new List<string>(4);
        private readonly List<bool> _improveScratch = new List<bool>(4);

        private CampMergeChooserModel _chooser = CampMergeChooserModel.Closed;
        private PieceRef _mergeSource = PieceRef.None;
        private PieceRef _mergeTarget = PieceRef.None;

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

            if (_chooser.IsOpen)
            {
                // The modal is the screen's decision right now: nothing on the board behind the dim
                // layer may be picked up until it closes (Stage 09g, "ล็อกการลากในหน้าจัดย่าม").
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

            // One call, not a placeholder then an overwrite: the first preview is also the first plan,
            // and a placeholder with no riders would make the plan look unchanged (Stage 09f).
            _drag = Evaluate(piece, BagDropZone.Board, _dragAnchor, _dragRotation, CampDragState.None);
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

            CampDragState next = Evaluate(_drag.Piece, zone, cell, _dragRotation, _drag);
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

            // A bag's turn is a new placement at the same cell, so the riders' landing places are
            // recomputed: the difference in rotation is what the plan reads (ADR 0020 §1).
            _drag = Evaluate(_drag.Piece, _drag.Zone, _drag.Origin, _dragRotation, _drag);
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

            if (_chooser.IsOpen)
            {
                // The chooser's left panel is already showing the piece this merge is about; a hover
                // behind the modal must not rewrite it.
                return;
            }

            _hovered = piece;
            PublishTooltip();
            PublishBag();
        }

        /// <summary>Drops the tooltip.</summary>
        public void ClearHover() => Hover(PieceRef.None);

        // ------------------------------------------------------------------
        // the merge chooser (Stage 09g)
        // ------------------------------------------------------------------

        /// <summary>The upgrade decision on screen, or <see cref="CampMergeChooserModel.Closed"/>.</summary>
        public CampMergeChooserModel MergeChooser => _chooser;

        /// <summary>True while the chooser is open ("ระหว่างเปิด" — the board is locked).</summary>
        public bool IsMergeChooserOpen => _chooser.IsOpen;

        /// <summary>
        /// Closes the chooser without changing anything — the back button and Escape both land here
        /// ("ปิดโดยไม่เปลี่ยนอะไร").
        /// </summary>
        /// <remarks>
        /// Nothing is undone because nothing was done: the merge command only ran if a card was
        /// confirmed. Reopening the same gesture offers the same choices, because the offer is a pure
        /// function of the run's state (ADR 0021), which is what makes closing a chooser free.
        /// </remarks>
        public void CancelMergeChooser()
        {
            if (!_chooser.IsOpen)
            {
                return;
            }

            Counters.MergeChoicesCancelled++;
            CloseMergeChooser();
            ClearStatus();
        }

        /// <summary>
        /// Confirms the card at <paramref name="index"/>: the click on a card and the 1/2/3 keys both
        /// call this. False when there was nothing to confirm.
        /// </summary>
        /// <remarks>
        /// The chooser is closed <i>before</i> the command runs, so a second click has nothing left to
        /// confirm and cannot fold the pair twice ("กันคลิกซ้ำ"). The command itself is atomic
        /// (<c>RunSession.ConfirmMerge</c> recomputes the offer and checks the id against it), so even a
        /// racing caller cannot apply a choice the current state does not offer.
        /// </remarks>
        public bool ConfirmMergeChoice(int index)
        {
            if (!_chooser.IsOpen || index < 0 || index >= _chooser.ChoiceCount)
            {
                return false;
            }

            string variantId = _chooser.Choices[index].VariantId;
            PieceRef source = _mergeSource;
            PieceRef target = _mergeTarget;

            CloseMergeChooser();

            Result<MergeOutcome> merged = _run.ConfirmMerge(source, target, variantId);

            if (merged.IsFailure)
            {
                SetStatus(merged.Error.Code);
                Refresh();
                return false;
            }

            Counters.Merged++;
            Counters.MergeChoicesConfirmed++;
            ClearStatus();
            Refresh();
            return true;
        }

        /// <summary>Builds the chooser for an offer and puts it on screen.</summary>
        private void OpenMergeChooser(MergeOffer offer, PieceRef source, PieceRef target)
        {
            _mergeSource = source;
            _mergeTarget = target;

            _sourceScratch.Clear();
            CollectSourceLines(ModelOf(target), _sourceScratch);

            _choiceScratch.Clear();
            for (int i = 0; i < offer.Options.Count; i++)
            {
                _choiceScratch.Add(Choice(offer, offer.Options[i]));
            }

            Counters.MergeChoicesOpened++;

            _chooser = new CampMergeChooserModel(
                true,
                Lookup("camp.merge.title"),
                Lookup("camp.merge.hint"),
                Lookup("camp.merge.back"),
                _sourceScratch.ToArray(),
                _choiceScratch.ToArray());

            // Order matters: the chooser is published first so the tooltip publisher's guard sees it,
            // then the floating tooltip is taken off screen (item 4: it must not draw over the modal).
            PublishMergeChooser();
            PublishTooltip();
        }

        private void CloseMergeChooser()
        {
            _mergeSource = PieceRef.None;
            _mergeTarget = PieceRef.None;
            _chooser = CampMergeChooserModel.Closed;
            _sourceScratch.Clear();
            _choiceScratch.Clear();
            _changeScratch.Clear();
            _improveScratch.Clear();
            PublishMergeChooser();
        }

        private void PublishMergeChooser()
        {
            if (_disposed)
            {
                return;
            }

            _state.MergeChooser.Value = _chooser;
        }

        /// <summary>One card: the variant's name over the item's, the step, and one line per stat.</summary>
        private CampMergeChoiceModel Choice(MergeOffer offer, UpgradeVariantConfig variant)
        {
            string title = Format(
                "camp.merge.card.title.format",
                Lookup(variant.NameKey),
                PieceName(PieceKind.Item, offer.ItemId));
            string level = Format("camp.merge.level.format", offer.FromTier, offer.ToTier);

            _changeScratch.Clear();
            _improveScratch.Clear();

            IReadOnlyList<UpgradeModifierConfig> modifiers = variant.Modifiers;
            for (int i = 0; i < modifiers.Count; i++)
            {
                UpgradeModifierConfig modifier = modifiers[i];

                _changeScratch.Add(Format(
                    "camp.merge.change.format",
                    Lookup(StatKey(modifier.Stat)),
                    ModifierValue(modifier)));
                _improveScratch.Add(UpgradeVocabulary.IsImprovement(modifier.Stat, Signed(modifier)));
            }

            return new CampMergeChoiceModel(
                variant.Id, title, level, _changeScratch.ToArray(), _improveScratch.ToArray());
        }

        /// <summary>
        /// The signed change a modifier makes, in the units the direction rule reads: <c>Multiply</c>
        /// is stated as a factor (<c>0.8</c> is a reduction), so it is turned into the difference from
        /// one before anyone asks whether it helps.
        /// </summary>
        private static float Signed(UpgradeModifierConfig modifier) =>
            modifier.Method == UpgradeMethod.Multiply ? modifier.Magnitude - 1f : modifier.Magnitude;

        /// <summary>How a modifier's amount is written on a card, sign included.</summary>
        private static string ModifierValue(UpgradeModifierConfig modifier)
        {
            switch (modifier.Method)
            {
                case UpgradeMethod.Percent:
                    return modifier.Magnitude.ToString("+0.##;-0.##", CultureInfo.InvariantCulture) + "%";
                case UpgradeMethod.Multiply:
                    return "×" + modifier.Magnitude.ToString("0.##", CultureInfo.InvariantCulture);
                default:
                    return modifier.Magnitude.ToString("+0.##;-0.##", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>The localization key of a stat's name, or the key of the unknown fallback.</summary>
        private static string StatKey(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Damage: return "upgrade.stat.damage";
                case UpgradeStat.Cooldown: return "upgrade.stat.cooldown";
                case UpgradeStat.MpCost: return "upgrade.stat.mp_cost";
                case UpgradeStat.ProjectileSpeed: return "upgrade.stat.projectile_speed";
                case UpgradeStat.Range: return "upgrade.stat.range";
                case UpgradeStat.PierceCount: return "upgrade.stat.pierce_count";
                case UpgradeStat.ProjectileCount: return "upgrade.stat.projectile_count";
                case UpgradeStat.MaxHp: return "upgrade.stat.max_hp";
                case UpgradeStat.MaxMp: return "upgrade.stat.max_mp";
                case UpgradeStat.MoveSpeed: return "upgrade.stat.move_speed";
                case UpgradeStat.PickupRadius: return "upgrade.stat.pickup_radius";
                case UpgradeStat.FocusPerSecond: return "upgrade.stat.focus_per_second";
                default: return "upgrade.stat.unknown";
            }
        }

        /// <summary>
        /// The six lines the tooltip shows for a piece, in order: ชื่อ, ระดับ, ค่าที่ใช้จริง,
        /// รุ่นย่อยที่เลือก, คำอธิบาย, คอมโบที่ทำงานอยู่. One producer for both the tooltip and the
        /// chooser's left panel, which is what "เนื้อหาเดียวกับ tooltip" means in practice.
        /// </summary>
        private void CollectSourceLines(CampPieceModel model, List<string> lines)
        {
            PieceReadout readout = ReadoutOf(model.Piece);

            lines.Add(model.Name);
            lines.Add(TierLine(model));
            lines.Add(StatusLine(model, readout));
            lines.Add(UpgradesLine(readout));
            lines.Add(DescriptionLine(model));
            lines.Add(ComboLine(readout));
        }

        /// <summary>The model of a piece the screen already built, or a default when it is unknown.</summary>
        private CampPieceModel ModelOf(PieceRef piece)
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].Piece == piece)
                {
                    return _pieces[i];
                }
            }

            return default;
        }

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

        /// <summary>
        /// Asks the run what this gesture would do and turns the answer into the published drag.
        /// </summary>
        /// <param name="previous">
        /// The drag this one replaces, if any. Used for one thing only: when it already describes this
        /// very placement, the riders' plan is handed back untouched instead of being worked out again.
        /// That is what makes a pointer move inside one cell free during a <b>bag</b> drag, exactly as
        /// it already is for an item.
        /// </param>
        private CampDragState Evaluate(
            PieceRef piece, BagDropZone zone, CellOffset cell, Rotation rotation, CampDragState previous)
        {
            bool overAGrid = zone == BagDropZone.Board || zone == BagDropZone.Chest;
            CellOffset origin = overAGrid ? cell : _dragAnchor;

            DropPreview preview = _run.PreviewDrop(piece, zone, origin, rotation);
            Counters.DragPreviews++;

            if (!preview.IsValid)
            {
                Counters.InvalidPreviews++;
            }

            return new CampDragState(
                true, piece, zone, origin, rotation, preview.Action,
                preview.IsValid, preview.Bia, preview.Target,
                preview.IsValid ? string.Empty : preview.ErrorCode,
                PlanCarry(piece, zone, origin, rotation, previous));
        }

        /// <summary>
        /// The items a bag drag at this placement would carry, each with the cell and the size it would
        /// land on — the ghosts drawn at the new positions.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The plan is the run's (<c>RunSession.CollectCarried</c>), which is the same one the accepted
        /// command applies: nothing here decides which items ride, and no placement rule is repeated in
        /// the screen (AGENTS rule 10, and the stage's "ห้ามเขียนกฎซ้ำใน View").
        /// </para>
        /// <para>
        /// Two cases carry nothing: a piece that is not a bag, and a bag being dragged off the board
        /// into the chest — where its items would stay behind, which is precisely why that command is
        /// refused while any item stands on it (ADR 0020 §2). Both are answered with the empty array
        /// without asking the run at all, so an item drag still allocates nothing per pointer move.
        /// </para>
        /// </remarks>
        private CampCarryGhost[] PlanCarry(
            PieceRef piece, BagDropZone zone, CellOffset origin, Rotation rotation, CampDragState previous)
        {
            if (zone != BagDropZone.Board || piece.Kind != PieceKind.Bag)
            {
                return NoCarry;
            }

            if (previous.IsActive && previous.Zone == zone && previous.Origin == origin
                && previous.Rotation == rotation)
            {
                return previous.Carry;
            }

            _carryScratch.Clear();
            _run.CollectCarried(piece, origin, rotation, _carryScratch);

            if (_carryScratch.Count == 0)
            {
                return NoCarry;
            }

            _carryGhostScratch.Clear();

            for (int i = 0; i < _carryScratch.Count; i++)
            {
                BagCarry carry = _carryScratch[i];

                if (!_run.Backpack.TryGetPiece(carry.Piece, out PieceInstance? instance) || instance is null)
                {
                    continue;
                }

                // A rider keeps its own orientation relative to the bag, so the footprint it lands with
                // is the one for the rotation the plan gave it — read from the same table the board
                // rules read, never re-derived here.
                Footprint landed = _run.Backpack.ShapeAt(instance, carry.Rotation);

                _carryGhostScratch.Add(new CampCarryGhost(
                    carry.Piece,
                    carry.Origin,
                    landed is null ? 1 : landed.Width,
                    landed is null ? 1 : landed.Height));
            }

            return _carryGhostScratch.Count == 0 ? NoCarry : _carryGhostScratch.ToArray();
        }

        private void EndDrag()
        {
            _dragRotation = Rotation.Degrees0;
            _drag = CampDragState.None;
            _carryScratch.Clear();
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
                    // Stage 09g: the drop asks the run what the merge would offer. A pair with
                    // choices opens the chooser and changes nothing yet; a pair with no choices
                    // folds immediately, exactly as it did before. Neither the gesture nor the
                    // screen decides which of the two commands runs — the offer does.
                    Result<MergeOffer> offered = _run.OfferMerge(piece, preview.Target);
                    if (offered.IsFailure)
                    {
                        return Result.Fail(offered.Error);
                    }

                    if (offered.Value.HasChoices)
                    {
                        OpenMergeChooser(offered.Value, piece, preview.Target);
                        return Result.Ok();
                    }

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
                DrawnLinks(),
                backpack.Revision);
        }

        /// <summary>
        /// The combos the screen marks permanently, as pairs plus how each is drawn.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The pairs are Core's: <c>SynergyEvaluator.Active</c> already resolved which piece
        /// provided a condition and which one received the effect, and this method does not add to
        /// that or re-derive it (AGENTS rule 10). What it decides is the <i>drawing</i>: the
        /// stage's rule is one line style — the same-line rule from a ตำรา to each อาวุธอาคม — a
        /// badge for the bag-area bonus (never a line from a bag to an item), and nothing permanent
        /// for the adjacency rules, which are shown by the hover highlight instead.
        /// </para>
        /// <para>
        /// Which style a rule gets comes from its own <c>synergy.kind</c> row, so a same-line rule
        /// added to <c>data/</c> arrives with a line and no code change, and an adjacency rule
        /// arrives with no mark at all.
        /// </para>
        /// <para>
        /// Rebuilt only when the layout was rebuilt (a command moved the bag) and cached otherwise,
        /// so a pointer move over the board pays nothing for it.
        /// </para>
        /// </remarks>
        private CampLink[] DrawnLinks()
        {
            // The layout is asked for first, always: it is the thing that knows whether the bag moved,
            // and the cache key below is its revision. Reading `_layoutRevision` before this call was
            // the one real bug in the first version of this method — with nothing hovered,
            // `BuildHighlight` returns before touching the layout, so a cache keyed on that stale field
            // answered with the marks of the previous revision for the rest of the camp visit
            // (measured: a tome placed on the weapon's row drew no line until something was hovered).
            SynergyEvaluation layout = CachedLayout();

            if (_links != null && _linksRevision == _layoutRevision)
            {
                return _links;
            }

            _linkScratch.Clear();

            IReadOnlyList<ActiveSynergy> active = layout.Active;
            for (int i = 0; i < active.Count; i++)
            {
                ActiveSynergy synergy = active[i];

                if (!TryRuleKind(synergy.RuleId, out SynergyKind kind))
                {
                    // A rule id the config does not know: drawn as nothing rather than guessed at.
                    continue;
                }

                switch (kind)
                {
                    case SynergyKind.SameRow:
                    case SynergyKind.SameColumn:
                        _linkScratch.Add(new CampLink(
                            synergy.SourceInstanceId, synergy.TargetInstanceId, CampLinkStyle.Line));
                        break;

                    case SynergyKind.BagArea:
                        // The bag is not a partner on the board: the bonus belongs to the cell it
                        // reached, so the mark goes on the receiver.
                        _linkScratch.Add(new CampLink(
                            synergy.SourceInstanceId, synergy.TargetInstanceId, CampLinkStyle.Badge));
                        break;

                    default:
                        // AdjacentPair: highlighted while the pointer is on one of the pair.
                        break;
                }
            }

            _linksRevision = _layoutRevision;
            _links = _linkScratch.Count == 0 ? NoLinks : _linkScratch.ToArray();
            return _links;
        }

        /// <summary>The kind of a rule the screen is drawing, or false when the config has no such row.</summary>
        private bool TryRuleKind(string ruleId, out SynergyKind kind)
        {
            for (int i = 0; i < _config.Synergies.Count; i++)
            {
                if (string.Equals(_config.Synergies[i].Id, ruleId, StringComparison.Ordinal))
                {
                    kind = _config.Synergies[i].Kind;
                    return true;
                }
            }

            kind = default;
            return false;
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

            if (_chooser.IsOpen)
            {
                // The chooser's left panel shows the piece the merge is about, so the floating
                // tooltip stays off screen for as long as the modal is up (Stage 09g, item 4).
                CampUiState.Set(_state.TooltipVisible, false);
                return;
            }

            if (!_hovered.IsValid)
            {
                CampUiState.Set(_state.TooltipVisible, false);
                return;
            }

            CampPieceModel model = ModelOf(_hovered);

            if (!model.Piece.IsValid)
            {
                CampUiState.Set(_state.TooltipVisible, false);
                return;
            }

            // One field per line: ชื่อ, ระดับ, ค่าที่ใช้จริง, รุ่นย่อยที่เลือก, คำอธิบาย, คอมโบที่
            // ทำงานอยู่. Every number comes from the run's resolved read model (Stage 09h); the
            // presenter formats, and the view assigns. Nothing here reads a skill row.
            PieceReadout readout = ReadoutOf(model.Piece);

            CampUiState.Set(_state.TooltipTitle, model.Name);
            CampUiState.Set(_state.TooltipTier, TierLine(model));
            CampUiState.Set(_state.TooltipStatus, StatusLine(model, readout));
            CampUiState.Set(_state.TooltipUpgrades, UpgradesLine(readout));
            CampUiState.Set(_state.TooltipDescription, DescriptionLine(model));
            CampUiState.Set(_state.TooltipCombos, ComboLine(readout));
            CampUiState.Set(_state.TooltipVisible, true);
        }

        /// <summary>
        /// The run's resolved explanation of a piece, or null when the run does not hold it.
        /// </summary>
        /// <remarks>
        /// The one source for every number the tooltip and the chooser's left panel print. The
        /// loadout behind it is cached per bag revision, so a hover costs one evaluation and no
        /// recompute of the snapshot.
        /// </remarks>
        private PieceReadout ReadoutOf(PieceRef piece)
        {
            Result<PieceReadout> readout = _run.ExplainPiece(piece);
            return readout.IsSuccess ? readout.Value : null;
        }

        /// <summary>ระดับ <c>t/max</c> — the piece's own tier, which a merge raised, not the table's.</summary>
        private string TierLine(CampPieceModel model) =>
            Format("camp.tooltip.tier.format", model.Tier, _config.Constant.MaxTier);

        /// <summary>
        /// สถานะ: the numbers the battle will use, the table's own numbers beside them, and (when it
        /// decided) the cooldown floor — for a piece that casts. A bag shows its usable slots and a
        /// piece that casts nothing shows its category.
        /// </summary>
        /// <remarks>
        /// These are the <b>resolved</b> numbers: tier upgrades and active combos applied. Before
        /// Stage 09h this line read the <c>skill</c> row, which is why a piece at 3/3 showed its 1/1
        /// damage and a −15 % cooldown variant changed nothing on screen.
        /// </remarks>
        private string StatusLine(CampPieceModel model, PieceReadout readout)
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

            if (readout == null || !readout.Casts)
            {
                return Lookup(CategoryKey(item.Category));
            }

            _builder.Clear();
            _builder.Append(Format(
                "camp.tooltip.stat.format",
                readout.Resolved.Damage,
                readout.Resolved.CooldownSeconds,
                readout.Resolved.MpCost));

            // The base row is drawn beside it so a tier-3 piece says what it changed from rather
            // than making the player remember 1/1.
            _builder.Append('\n');
            _builder.Append(Format(
                "camp.tooltip.base.format",
                Format(
                    "camp.tooltip.stat.format",
                    readout.Base.Damage,
                    readout.Base.CooldownSeconds,
                    readout.Base.MpCost)));

            if (readout.CooldownAtMinimum)
            {
                _builder.Append('\n');
                _builder.Append(Format("camp.tooltip.minimum.format", readout.MinCooldownSeconds));
            }

            return _builder.ToString();
        }

        /// <summary>
        /// รุ่นย่อยที่เลือก: one line per merge step that kept a variant — the step's new tier, the
        /// variant's name and the stats it changed.
        /// </summary>
        /// <remarks>
        /// A step with <b>no</b> variant (a charm's immediate fold) contributes no line at all: the
        /// tier line already says the piece went up, and inventing an effect for a step that has none
        /// is what this stage exists to stop.
        /// </remarks>
        private string UpgradesLine(PieceReadout readout)
        {
            if (readout == null || readout.Steps.Count == 0)
            {
                return string.Empty;
            }

            _builder.Clear();

            for (int i = 0; i < readout.Steps.Count; i++)
            {
                PieceUpgradeStep step = readout.Steps[i];
                if (!step.HasVariant)
                {
                    continue;
                }

                if (_builder.Length > 0)
                {
                    _builder.Append('\n');
                }

                _builder.Append(Format(
                    "camp.tooltip.upgrade.format",
                    step.ToTier,
                    Lookup(step.NameKey),
                    StepsOf(step.Modifiers)));
            }

            return _builder.ToString();
        }

        /// <summary>
        /// One step's changes, written by the same two helpers the upgrade card uses
        /// (<see cref="StatKey"/> and <see cref="ModifierValue"/> through
        /// <c>camp.merge.change.format</c>), so a step cannot read one way on a card and another in
        /// the tooltip.
        /// </summary>
        private string StepsOf(IReadOnlyList<UpgradeModifierConfig> modifiers)
        {
            _detail.Clear();

            for (int i = 0; i < modifiers.Count; i++)
            {
                UpgradeModifierConfig modifier = modifiers[i];

                if (_detail.Length > 0)
                {
                    _detail.Append(" · ");
                }

                _detail.Append(Format(
                    "camp.merge.change.format",
                    Lookup(StatKey(modifier.Stat)),
                    ModifierValue(modifier)));
            }

            return _detail.ToString();
        }

        /// <summary>
        /// คำอธิบาย: the item's own description row, or empty when its table has none (a bag never
        /// has one). Empty is a real answer — the tooltip hides that line rather than drawing a
        /// placeholder.
        /// </summary>
        private string DescriptionLine(CampPieceModel model)
        {
            if (model.IsBag || !_config.TryGetItem(model.DefId, out ItemConfig item)
                || string.IsNullOrEmpty(item.DescriptionKey))
            {
                return string.Empty;
            }

            return Lookup(item.DescriptionKey);
        }

        /// <summary>คอมโบที่ทำงานอยู่: the totals and the rule names, both from the read model.</summary>
        private string ComboLine(PieceReadout readout)
        {
            if (readout == null)
            {
                return Lookup("camp.tooltip.combo.none");
            }

            _builder.Clear();

            if (readout.ActiveSynergyCount > 0)
            {
                _builder.Append(Format(
                    "camp.tooltip.combo.format",
                    readout.ActiveSynergyCount,
                    readout.DamageMultiplier,
                    readout.CooldownMultiplier));
            }

            IReadOnlyList<string> rules = readout.ComboRuleIds;
            for (int i = 0; i < rules.Count; i++)
            {
                if (_builder.Length > 0)
                {
                    _builder.Append(" · ");
                }

                _builder.Append(RuleName(rules[i]));
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

        /// <summary>How many times a merge opened the upgrade chooser (Stage 09g).</summary>
        public int MergeChoicesOpened;

        /// <summary>How many upgrades were confirmed from the chooser.</summary>
        public int MergeChoicesConfirmed;

        /// <summary>How many choosers were closed without changing anything.</summary>
        public int MergeChoicesCancelled;
    }
}
