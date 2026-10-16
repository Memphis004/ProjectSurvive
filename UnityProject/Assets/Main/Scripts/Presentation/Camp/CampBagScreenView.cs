using System;
using System.Collections.Generic;
using System.Globalization;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// The bag screen overlay: the board, the chest, the altar pot, the instruction scroll,
    /// synergy lines, and the pointer drag & drop plus click-to-place interactions.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampBagScreenView : MonoBehaviour,
        IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerUpHandler, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
    {
        private const float HighlightAlpha = 0.65f;

        /// <summary>
        /// The opacity of the ghosts of the items a dragged bag carries (Stage 09f). Lower than the bag's
        /// own ghost on purpose: they are a prediction of a second thing, and the bag's landing place is
        /// what the player is aiming.
        /// </summary>
        private const float CarryGhostAlpha = 0.45f;

        [SerializeField] private LifetimeScope _scope;

        [Header("Surfaces (authored top-left anchored, pivot 0,1)")]
        [Tooltip("The 9x7 placement area. Cells are created under it from constant.board_*.")]
        [SerializeField] private RectTransform _boardRect;

        [Tooltip("The chest's slot area. Slots are created under it.")]
        [SerializeField] private RectTransform _chestRect;
        [SerializeField] private TMP_Text _chestTitleLabel;

        [Tooltip("The altar pot: dropping a piece here gives it back for bia.")]
        [SerializeField] private RectTransform _altarRect;
        [SerializeField] private TMP_Text _altarCaptionLabel;
        [SerializeField] private TMP_Text _altarOfferLabel;

        [Header("Instructions & Guidance (Top-Right)")]
        [SerializeField] private RectTransform _instructionsRect;
        [SerializeField] private TMP_Text _instructionsTitleLabel;
        [SerializeField] private TMP_Text _instructionStep1Label;
        [SerializeField] private TMP_Text _instructionStep2Label;
        [SerializeField] private TMP_Text _instructionStep3Label;
        [SerializeField] private TMP_Text _instructionStep4Label;

        [Header("Navigation (Bottom-Left, above the status bar)")]
        [Tooltip("The whole bottom-left block: กลับ, ไปชั้นถัดไป and the reason. Its screen rectangle is what the layout report measures.")]
        [SerializeField] private RectTransform _statsRect;

        [Tooltip("The chest panel's background, so the no-overlap check measures the panel, not just its grid.")]
        [SerializeField] private RectTransform _chestPanelRect;

        [Tooltip("ไปชั้นถัดไป: the same use case as the status bar's ออกเดินทาง, disabled with a reason.")]
        [SerializeField] private Button _nextFloorButton;
        [SerializeField] private TMP_Text _nextFloorLabel;
        [SerializeField] private TMP_Text _departReasonLabel;

        [Header("Metrics (reference pixels at 1920x1080)")]
        [SerializeField] private float _cellSize = 78f;
        [SerializeField] private float _cellGap = 4f;
        [SerializeField] private float _chestCellSize = 136f;
        [SerializeField] private float _chestCellGap = 16f;
        [SerializeField] private int _chestColumns = 5;
        [SerializeField] private TMP_FontAsset _font;

        [Header("จัดย่าม panel")]
        [Tooltip("The panel that holds this screen overlay. Shown and hidden by the จัดย่าม button.")]
        [SerializeField] private GameObject _panelRoot;

        [SerializeField] private Button _openButton;
        [SerializeField] private TMP_Text _openButtonLabel;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _closeLabel;
        [SerializeField] private TMP_Text _titleLabel;

        [Header("Bag actions (Top-Left)")]
        [SerializeField] private Button _rotateButton;
        [SerializeField] private TMP_Text _rotateLabel;
        [SerializeField] private Button _undoButton;
        [SerializeField] private TMP_Text _undoLabel;
        [SerializeField] private Button _resetButton;
        [SerializeField] private TMP_Text _resetLabel;
        [SerializeField] private Button _lockButton;
        [SerializeField] private TMP_Text _lockLabel;

        [Tooltip("ล็อกย่ามตอนคืนสภาพ: a real checkbox, read by the restore button.")]
        [SerializeField] private Toggle _lockRestoreToggle;
        [SerializeField] private TMP_Text _lockRestoreLabel;

        [Header("Palette (cosmetic)")]
        [SerializeField] private Color _cellIdle = new Color(0.12f, 0.14f, 0.18f, 0.90f);
        [SerializeField] private Color _cellActive = new Color(0.24f, 0.38f, 0.26f, 0.95f);
        [SerializeField] private Color _cellHighlight = new Color(0.85f, 0.72f, 0.30f, 0.95f);
        [SerializeField] private Color _bagPiece = new Color(0.36f, 0.52f, 0.36f, 0.95f);
        [SerializeField] private Color _itemPiece = new Color(0.62f, 0.46f, 0.24f, 0.95f);
        [SerializeField] private Color _selectedPieceColor = new Color(0.95f, 0.85f, 0.30f, 1f);
        [SerializeField] private Color _ghostValid = new Color(0.35f, 0.85f, 0.35f, 0.85f);
        [SerializeField] private Color _ghostInvalid = new Color(0.85f, 0.35f, 0.35f, 0.85f);
        [SerializeField] private Color _ghostMerge = new Color(0.95f, 0.80f, 0.35f, 0.90f);

        /// <summary>
        /// The choice modal's dim layer (Stage 09g): dark enough to say "the screen is waiting for an
        /// answer", light enough that the board stays recognisable behind it.
        /// </summary>
        [SerializeField] private Color _chooserDim = new Color(0f, 0f, 0f, 0.72f);

        /// <summary>The modal's own panel, opaque so the cards read against it.</summary>
        [SerializeField] private Color _chooserPanel = new Color(0.07f, 0.08f, 0.11f, 0.97f);

        /// <summary>One card's background.</summary>
        [SerializeField] private Color _chooserCard = new Color(0.14f, 0.16f, 0.20f, 1f);

        /// <summary>The strip that names the card's key and is also its pick affordance.</summary>
        [SerializeField] private Color _chooserPick = new Color(0.24f, 0.38f, 0.26f, 1f);

        /// <summary>
        /// A change for the better ("สีเขียวเมื่อดีขึ้น"). The green is light enough to hold contrast
        /// against <see cref="_chooserCard"/> at the 22 px floor, which is the whole point of the colour:
        /// it is the only thing distinguishing a stat line that helps from one that hurts.
        /// </summary>
        [SerializeField] private Color _chooserBetter = new Color(0.45f, 0.92f, 0.45f, 1f);

        /// <summary>A change for the worse ("สีแดงเมื่อแย่ลง").</summary>
        [SerializeField] private Color _chooserWorse = new Color(0.95f, 0.42f, 0.38f, 1f);

        /// <summary>
        /// How tall a bag's secondary name strip is, in canvas units. Tall enough for the 22 px
        /// floor's line box; an item's rectangle reaching it is what hides the bag's name.
        /// </summary>
        private const float BagLabelHeight = 30f;

        /// <summary>The side of a bag-area bonus badge, in canvas units.</summary>
        private const float BadgeSize = 18f;

        private readonly List<Image> _boardCells = new List<Image>(64);
        private readonly List<Image> _chestCells = new List<Image>(16);
        private readonly List<CampPieceView> _pieceViews = new List<CampPieceView>(32);
        private readonly List<CampPieceModel> _painted = new List<CampPieceModel>(32);
        private readonly List<Image> _synergyLines = new List<Image>(16);
        private readonly List<Image> _badges = new List<Image>(16);

        /// <summary>
        /// One pooled box per item riding on the dragged bag, parented to the board so a ghost sits
        /// exactly where the item will (Stage 09f / ADR 0020). Pooled because a drag must not allocate
        /// per pointer move, and disabled when nothing rides.
        /// </summary>
        private readonly List<Image> _carryGhosts = new List<Image>(8);

        /// <summary>The plans the carry ghosts currently on screen were drawn from.</summary>
        private CampCarryGhost[] _drawnCarry = NoCarryPlans;

        /// <summary>A drag that carries nothing: shared, because it is constant.</summary>
        private static readonly CampCarryGhost[] NoCarryPlans = new CampCarryGhost[0];

        /// <summary>
        /// How many cards the choice modal can show (Stage 09g). Three, because
        /// <c>constant.upgrade_choice_count</c> is 3 and the data validator refuses a pool smaller than
        /// two — so one pool of three cards covers every offer the run can make.
        /// </summary>
        private const int ChooserCardCapacity = 3;

        /// <summary>
        /// How many stat lines one card can show. The widest shipped variant changes two stats; the
        /// spare lines are pooled and hidden rather than created per open.
        /// </summary>
        private const int ChooserChangeLineCapacity = 4;

        /// <summary>The modal's own size in canvas units at the 1920×1080 reference resolution.</summary>
        private static readonly Vector2 ChooserPanelSize = new Vector2(1560f, 600f);

        /// <summary>
        /// The three choice cards, built once. Pooled rather than rebuilt per open, because the modal is
        /// published whenever the state replaces its model and a rebuild would allocate on every open.
        /// </summary>
        private readonly List<MergeCardView> _chooserCards = new List<MergeCardView>(ChooserCardCapacity);

        /// <summary>The five lines of the left panel, one label each, so an empty one is hidden.</summary>
        private readonly TMP_Text[] _chooserSourceLines = new TMP_Text[5];

        private RectTransform _chooserRoot;
        private RectTransform _chooserDimRect;
        private RectTransform _chooserPanelRect;
        private RectTransform _chooserBackRect;
        private TMP_Text _chooserTitleLabel;
        private TMP_Text _chooserHintLabel;
        private TMP_Text _chooserBackLabel;

        /// <summary>How many cards the last published chooser filled (a test reads it).</summary>
        private int _chooserCardsShown;

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>(16);

        private CampUiState _state;
        private CampPresenter _presenter;
        private Image _ghost;
        private RectTransform _ghostRect;
        private TMP_Text _ghostLabel;
        private RectTransform _linkRoot;
        private RectTransform _badgeRoot;

        private int _boardWidth;
        private int _boardHeight;
        private int _chestSlots;
        private int _renderedRevision = -1;
        private bool _bound;
        private bool _pointerDown;
        private PieceRef _selectedPiece = PieceRef.None;

        /// <summary>How many piece rectangles exist (a test reads the pool's size).</summary>
        public int PieceViewCount => _pieceViews.Count;

        /// <summary>How many cell rectangles exist.</summary>
        public int CellCount => _boardCells.Count + _chestCells.Count;

        /// <summary>Board columns as last rendered (0 before the first model).</summary>
        public int BoardWidth => _boardWidth;

        /// <summary>Board rows as last rendered (0 before the first model).</summary>
        public int BoardHeight => _boardHeight;

        /// <summary>Chest capacity as last rendered.</summary>
        public int ChestSlotCount => _chestSlots;

        /// <summary>Chest slots per row in the layout.</summary>
        public int ChestColumns => _chestColumns;

        /// <summary>The currently selected piece (for click-to-move).</summary>
        public PieceRef SelectedPiece => _selectedPiece;

        /// <summary>The bag screen's current model, or null before <see cref="Bind"/>.</summary>
        public CampBagScreen Model { get; private set; }

        /// <summary>The piece models from the current model.</summary>
        public CampPieceModel[] Pieces => Model?.Pieces ?? Array.Empty<CampPieceModel>();

        /// <summary>True while the จัดย่าม panel is on screen.</summary>
        public bool PanelVisible => _panelRoot == null || _panelRoot.activeSelf;

        /// <summary>
        /// How many combo lines are drawn right now. A test reads it: the rule is that a line means
        /// a ตำรา–อาวุธอาคม pair and nothing else, and a bag-area bonus must never raise it.
        /// </summary>
        public int VisibleLineCount { get; private set; }

        /// <summary>How many bag-area bonus badges are drawn right now.</summary>
        public int VisibleBadgeCount { get; private set; }

        /// <summary>
        /// How many item ghosts are drawn right now — the items that would ride with the dragged bag.
        /// </summary>
        public int VisibleCarryGhostCount { get; private set; }

        /// <summary>
        /// True while the choice modal is on screen. The presenter is the source of truth for
        /// <i>whether</i> a merge is asking; this mirrors what the screen is actually drawing.
        /// </summary>
        public bool ChooserVisible => _chooserRoot != null && _chooserRoot.gameObject.activeSelf;

        /// <summary>How many cards are on screen right now.</summary>
        public int ChooserCardCount => _chooserCardsShown;

        /// <summary>
        /// The centre of card <paramref name="index"/> in screen pixels — what a real pointer taps.
        /// </summary>
        /// <remarks>
        /// The <b>centre</b>, not a corner, and the whole card is the hit area: a corner lies on the
        /// rectangle's own boundary where <c>Rect.Contains</c> excludes it, which is the defect
        /// <see cref="CampScreenRects"/> exists to prevent.
        /// </remarks>
        public bool TryGetChoiceScreenPoint(int index, out Vector2 point)
        {
            point = default;
            RectTransform card = ChooserCardRect(index);
            return card != null && CampScreenRects.TryCentre(card, out point);
        }

        /// <summary>Card <paramref name="index"/>'s screen rectangle, or false when there is none.</summary>
        public bool TryGetChoiceRect(int index, out Rect rect)
        {
            rect = default;
            RectTransform card = ChooserCardRect(index);
            return card != null && CampScreenRects.TryOf(card, out rect);
        }

        /// <summary>The back button's screen point, or false while the modal is closed.</summary>
        public bool TryGetChooserBackScreenPoint(out Vector2 point)
        {
            point = default;
            return _chooserBackRect != null && CampScreenRects.TryCentre(_chooserBackRect, out point);
        }

        /// <summary>The modal panel's screen rectangle, for the layout report. False while closed.</summary>
        public bool TryGetChooserPanelRect(out Rect rect)
        {
            rect = default;
            return _chooserPanelRect != null && CampScreenRects.TryOf(_chooserPanelRect, out rect);
        }

        /// <summary>The dim layer's screen rectangle, for the layout report. False while closed.</summary>
        public bool TryGetChooserDimRect(out Rect rect)
        {
            rect = default;
            return _chooserDimRect != null && CampScreenRects.TryOf(_chooserDimRect, out rect);
        }

        /// <summary>
        /// The card at <paramref name="index"/> when it is on screen, or null. The range check is the
        /// count that was published, so a card the offer did not fill is never reported as tappable.
        /// </summary>
        private RectTransform ChooserCardRect(int index) =>
            index >= 0 && index < _chooserCardsShown && index < _chooserCards.Count
                ? _chooserCards[index].Root
                : null;

        /// <summary>
        /// The screen rectangle of a piece's name, or false when the piece has no name on screen.
        /// </summary>
        /// <remarks>
        /// A bag's name is secondary text and is hidden when an item's rectangle covers the strip it
        /// is drawn in, so "no name is drawn under another name" is a statement a test can make about
        /// two rectangles instead of about a picture.
        /// </remarks>
        public bool TryGetPieceLabelScreenRect(PieceRef piece, out Rect rect)
        {
            rect = default;

            for (int i = 0; i < _painted.Count; i++)
            {
                if (_painted[i].Piece != piece || i >= _pieceViews.Count)
                {
                    continue;
                }

                RectTransform label = _pieceViews[i].LabelRect;
                return label != null && label.gameObject.activeInHierarchy
                    && CampScreenRects.TryOf(label, out rect);
            }

            return false;
        }

        /// <summary>The screen rectangle of a piece's rectangle (not its name), or false when it is not drawn.</summary>
        public bool TryGetPieceScreenRect(PieceRef piece, out Rect rect)
        {
            rect = default;

            for (int i = 0; i < _painted.Count; i++)
            {
                if (_painted[i].Piece == piece && i < _pieceViews.Count)
                {
                    return CampScreenRects.TryOf(_pieceViews[i].Rect, out rect);
                }
            }

            return false;
        }

        /// <summary>Resolves the screen's services and starts rendering. Public and idempotent.</summary>
        public void Bind()
        {
            if (_bound)
            {
                return;
            }

            if (_scope == null || _scope.Container == null)
            {
                Debug.LogError("CampBagScreenView needs a built scope to resolve the bag screen's state.", this);
                return;
            }

            _bound = true;

            _state = _scope.Container.Resolve<CampUiState>();
            _presenter = _scope.Container.Resolve<CampPresenter>();

            CreateGhost();
            CreateMergeChooser();
            BindButtons();

            _subscriptions.Add(_state.Bag.Subscribe(RenderBag));
            _subscriptions.Add(_state.Drag.Subscribe(RenderDrag));
            _subscriptions.Add(_state.MergeChooser.Subscribe(RenderMergeChooser));

            // The panel starts closed. Stage 09c's camp is a place the player walks, so the sala is
            // what is on screen at the start and จัดย่าม is the control that opens the screen — which
            // is also what makes "เดิน → เช่า → เปิดหน้าจัดย่าม → ลากลง Board" the order it is.
            ShowPanel(false);
        }

        /// <summary>Shows or hides the whole จัดย่าม panel.</summary>
        public void ShowPanel(bool visible)
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(visible);
            }

            // The จัดย่าม button lives on the canvas, not in the panel, so it would otherwise be
            // drawn over the panel's bottom-centre. The panel carries its own กลับ button, so the
            // toggle is only useful while the panel is closed.
            if (_openButton != null)
            {
                _openButton.gameObject.SetActive(!visible);
            }

            if (!visible && _presenter != null && _presenter.IsDragging)
            {
                _presenter.CancelDrag();
            }

            if (!visible && _presenter != null)
            {
                _presenter.ClearHover();
            }

            // The choice modal belongs to this panel and can only exist while it is open: closing
            // จัดย่าม with a decision pending closes the decision too, rather than leaving a modal the
            // player has no way to answer on top of the sala (Stage 09g).
            if (!visible && _presenter != null && _presenter.IsMergeChooserOpen)
            {
                _presenter.CancelMergeChooser();
            }

            _selectedPiece = PieceRef.None;
        }

        /// <summary>Opens the panel when it is closed and closes it when it is open.</summary>
        public void TogglePanel() => ShowPanel(!PanelVisible);

        private void BindButtons()
        {
            Add(_openButton, TogglePanel);
            Add(_closeButton, () => ShowPanel(false));
            Add(_rotateButton, OnRotateClicked);
            Add(_undoButton, OnUndoClicked);
            Add(_resetButton, OnResetClicked);
            Add(_lockButton, OnLockClicked);
            Add(_nextFloorButton, OnDepartClicked);

            Subscribe(_state.Title, _titleLabel);
            Subscribe(_state.BagCaption, _openButtonLabel);
            Subscribe(_state.CloseLabel, _closeLabel);

            Subscribe(_state.AltarCaption, _altarCaptionLabel);
            Subscribe(_state.AltarOffer, _altarOfferLabel);
            Subscribe(_state.RotateLabel, _rotateLabel);
            Subscribe(_state.UndoLabel, _undoLabel);
            Subscribe(_state.ResetLabel, _resetLabel);
            Subscribe(_state.LockLabel, _lockLabel);
            Subscribe(_state.NextFloorLabel, _nextFloorLabel);
            Subscribe(_state.LockRestoreLabel, _lockRestoreLabel);
            Subscribe(_state.InstructionsTitle, _instructionsTitleLabel);
            Subscribe(_state.Instruction1, _instructionStep1Label);
            Subscribe(_state.Instruction2, _instructionStep2Label);
            Subscribe(_state.Instruction3, _instructionStep3Label);
            Subscribe(_state.Instruction4, _instructionStep4Label);

            // The panel carries no run stat of its own: น้ำหนัก/สมาธิ/หีบ/เบี้ย/HP/MP live on the
            // bottom bar, which is under the panel and never hidden by it, and a second copy here was
            // the duplication Stage 09e work item 1 removes.
            Subscribe(_state.ChestValue, _chestTitleLabel, format: "หีบสัมภาระ ({0})");
            Subscribe(_state.DepartReason, _departReasonLabel);

            _subscriptions.Add(_state.CanReset.Subscribe(value => SetInteractable(_resetButton, value)));
            _subscriptions.Add(_state.CanDepart.Subscribe(value => SetInteractable(_nextFloorButton, value)));
            _subscriptions.Add(_state.BagLocked.Subscribe(OnLockStateChanged));
        }

        private void OnDestroy()
        {
            Remove(_openButton, TogglePanel);
            Remove(_closeButton, ClosePanel);
            Remove(_rotateButton, OnRotateClicked);
            Remove(_undoButton, OnUndoClicked);
            Remove(_resetButton, OnResetClicked);
            Remove(_lockButton, OnLockClicked);
            Remove(_nextFloorButton, OnDepartClicked);

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
            _state = null;
            _presenter = null;
            _bound = false;
        }

        private void ClosePanel() => ShowPanel(false);

        private void OnRotateClicked()
        {
            if (_presenter != null)
            {
                _presenter.RotateSelected();
            }
        }

        private void OnUndoClicked()
        {
            if (_presenter != null)
            {
                _presenter.UndoEdit();
            }
        }

        /// <summary>
        /// คืนสภาพ, carrying the lock-on-restore checkbox across as a value.
        /// </summary>
        /// <remarks>
        /// The view forwards what the checkbox says and decides nothing: whether a restore may
        /// lock the bag, and what happens when either half is refused, is
        /// <see cref="CampPresenter.ResetBag(bool)"/>'s (AGENTS rule 10).
        /// </remarks>
        private void OnResetClicked()
        {
            if (_presenter != null)
            {
                _presenter.ResetBag(_lockRestoreToggle != null && _lockRestoreToggle.isOn);
            }
        }

        private void OnLockClicked()
        {
            if (_presenter != null)
            {
                _presenter.ToggleBagLock();
            }
        }

        private void OnDepartClicked()
        {
            if (_presenter != null)
            {
                _presenter.Depart();
            }
        }

        private void OnLockStateChanged(bool locked)
        {
            SetInteractable(_rotateButton, !locked);
            SetInteractable(_lockButton, true);
        }

        private void Subscribe(R3.ReactiveProperty<string> property, TMP_Text label, string format = null)
        {
            if (label == null)
            {
                return;
            }

            _subscriptions.Add(property.Subscribe(value =>
            {
                label.text = string.IsNullOrEmpty(format) ? value : string.Format(format, value);
            }));
        }

        private static void Add(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Remove(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        // ------------------------------------------------------------------
        // pointer events (Drag & Drop + Click-to-move)
        // ------------------------------------------------------------------

        /// <summary>
        /// Records that the left button is down, and which region it went down in.
        /// </summary>
        /// <remarks>
        /// The right button is the rotate gesture of Stage 09 work item 2 ("หมุนด้วย R หรือคลิกขวา"):
        /// it turns the piece under the pointer, or the piece in flight when a drag is already live.
        /// </remarks>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (ChooserVisible)
            {
                // The dim layer is the physical lock; this is the logical one, so a stray event that
                // somehow reaches the panel cannot rotate or pick a piece behind the modal.
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (_presenter != null && _presenter.IsDragging)
                {
                    _presenter.RotateDrag();
                    return;
                }

                if (!TryLocate(eventData.position, out BagDropZone rightZone, out CellOffset rightCell))
                {
                    return;
                }

                PieceRef rightPiece = PieceAt(rightZone, rightCell);
                if (rightPiece.IsValid)
                {
                    _presenter.SelectOnly(rightPiece);
                    _presenter.RotateSelected();
                }
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pointerDown = true;
        }

        /// <summary>
        /// Picks up the piece the player pressed on.
        /// </summary>
        /// <remarks>
        /// <b>Anchored at the press, not at the pointer's current position.</b> The EventSystem only
        /// reports the threshold crossing a few frames after the press, and by then the pointer has
        /// already left the cell the player aimed at: a press on a bag's outer cell (its cells are
        /// 82 px) and a pull outwards used to resolve to empty board, and the piece was never picked
        /// up at all. Measured with this fixture — the press landed on (2,2), the crossing on (3,2),
        /// and no drag ran; see docs/stages/09b-report.md.
        /// </remarks>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || _presenter == null)
            {
                return;
            }

            if (ChooserVisible)
            {
                // "ล็อกการลากในหน้าจัดย่าม": while a choice is pending, nothing on the board is picked up.
                return;
            }

            if (!TryLocate(eventData.pressPosition, out BagDropZone zone, out CellOffset cell))
            {
                return;
            }

            PieceRef piece = PieceAt(zone, cell);
            if (piece.IsValid && !IsDraggingPiece(piece))
            {
                _selectedPiece = piece;
                _presenter.DragStart(piece, RotationFor(piece));
                UpdateGhostPosition(eventData.position);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_presenter == null || !_presenter.IsDragging)
            {
                return;
            }

            if (ChooserVisible)
            {
                return;
            }

            UpdateGhostPosition(eventData.position);

            TryLocate(eventData.position, out BagDropZone zone, out CellOffset cell);
            _presenter.DragMove(zone, cell);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _pointerDown = false;

            if (_presenter != null && _presenter.IsDragging)
            {
                _presenter.Drop();
            }

            HideGhost();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pointerDown = false;
        }

        /// <summary>
        /// Confirms the card the player picked (Stage 09g). One caller for the card's own button and
        /// for the 1/2/3 keys, so there is exactly one path into the command.
        /// </summary>
        private void OnChoiceClicked(int index)
        {
            if (_presenter != null)
            {
                _presenter.ConfirmMergeChoice(index);
            }
        }

        /// <summary>The back button: the same thing Escape does — close without changing anything.</summary>
        private void OnChooserBackClicked()
        {
            if (_presenter != null)
            {
                _presenter.CancelMergeChooser();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging)
            {
                return;
            }

            if (ChooserVisible)
            {
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left || _presenter == null)
            {
                return;
            }

            if (!TryLocate(eventData.position, out BagDropZone zone, out CellOffset cell))
            {
                _selectedPiece = PieceRef.None;
                _presenter.SelectOnly(PieceRef.None);
                RenderBag(Model);
                return;
            }

            PieceRef clickedPiece = PieceAt(zone, cell);

            if (_selectedPiece.IsValid)
            {
                if (clickedPiece == _selectedPiece)
                {
                    _selectedPiece = PieceRef.None;
                    _presenter.SelectOnly(PieceRef.None);
                }
                else
                {
                    // Destination clicked: perform move / merge / transfer / recycle
                    _presenter.DragStart(_selectedPiece, RotationFor(_selectedPiece));
                    _presenter.DragMove(zone, cell);
                    _presenter.Drop();
                    _selectedPiece = PieceRef.None;
                    _presenter.SelectOnly(PieceRef.None);
                }
            }
            else
            {
                if (clickedPiece.IsValid)
                {
                    _selectedPiece = clickedPiece;
                    _presenter.SelectOnly(clickedPiece);
                }
            }

            RenderBag(Model);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_presenter == null || _pointerDown || _presenter.IsDragging)
            {
                return;
            }

            if (ChooserVisible)
            {
                // No hover behind the modal: the chooser's left panel is already showing the piece
                // this merge is about, and a tooltip may not appear over it (item 4).
                return;
            }

            if (!TryLocate(eventData.position, out BagDropZone zone, out CellOffset cell))
            {
                _presenter.ClearHover();
                return;
            }

            _presenter.Hover(PieceAt(zone, cell));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (ChooserVisible)
            {
                return;
            }

            if (_presenter != null && !_presenter.IsDragging)
            {
                _presenter.ClearHover();
            }
        }

        private void Update()
        {
            if (_presenter == null)
            {
                return;
            }

            UpdateKeyboard();

            if (!_presenter.IsDragging)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            bool held = mouse != null && mouse.leftButton.isPressed;
            if (_pointerDown && !held)
            {
                _pointerDown = false;
                _presenter.Drop();
                HideGhost();
            }
        }

        private void UpdateKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // Stage 09g: while a choice is pending the keys belong to the modal. 1/2/3 confirm the
            // card with that number, Escape closes it without changing anything — and the board's own
            // keys (R to rotate) do nothing until it closes, so the key handling stops here rather
            // than falling through.
            if (_presenter.IsMergeChooserOpen)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    _presenter.CancelMergeChooser();
                }
                else if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    _presenter.ConfirmMergeChoice(0);
                }
                else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    _presenter.ConfirmMergeChoice(1);
                }
                else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                {
                    _presenter.ConfirmMergeChoice(2);
                }

                return;
            }

            if (keyboard.rKey.wasPressedThisFrame)
            {
                _presenter.RotateSelected();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_presenter.IsDragging)
                {
                    _presenter.CancelDrag();
                    HideGhost();
                }
                else
                {
                    _selectedPiece = PieceRef.None;
                    _presenter.SelectOnly(PieceRef.None);
                    _presenter.ClearHover();
                    RenderBag(Model);
                }
            }
        }

        // ------------------------------------------------------------------
        // geometry
        // ------------------------------------------------------------------

        public bool TryLocate(Vector2 screenPoint, out BagDropZone zone, out CellOffset cell)
        {
            zone = BagDropZone.None;
            cell = default;

            if (_boardRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(_boardRect, screenPoint, null)
                && TryBoardCell(screenPoint, out cell))
            {
                zone = BagDropZone.Board;
                return true;
            }

            if (_chestRect != null
                && RectTransformUtility.RectangleContainsScreenPoint(_chestRect, screenPoint, null)
                && TryChestCell(screenPoint, out cell))
            {
                zone = BagDropZone.Chest;
                return true;
            }

            if (_altarRect != null && RectTransformUtility.RectangleContainsScreenPoint(_altarRect, screenPoint, null))
            {
                zone = BagDropZone.Altar;
                return true;
            }

            return false;
        }

        public bool TryBoardCell(Vector2 screenPoint, out CellOffset cell)
        {
            cell = default;

            if (_boardRect == null || _boardWidth <= 0 || _boardHeight <= 0)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_boardRect, screenPoint, null, out Vector2 local))
            {
                return false;
            }

            float pitch = _cellSize + _cellGap;
            int column = (int)Mathf.Floor(local.x / pitch);
            int row = (int)Mathf.Floor(-local.y / pitch);

            if (column < 0 || row < 0 || column >= _boardWidth || row >= _boardHeight)
            {
                return false;
            }

            cell = new CellOffset(column, row);
            return true;
        }

        private bool TryChestCell(Vector2 screenPoint, out CellOffset cell)
        {
            cell = default;

            if (_chestRect == null || _chestSlots <= 0
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_chestRect, screenPoint, null, out Vector2 local))
            {
                return false;
            }

            float pitch = _chestCellSize + _chestCellGap;
            int column = (int)Mathf.Floor(local.x / pitch);
            int row = (int)Mathf.Floor(-local.y / pitch);

            if (column < 0 || row < 0 || column >= _chestColumns || SlotIndexAt(new CellOffset(column, row)) >= _chestSlots)
            {
                return false;
            }

            cell = new CellOffset(column, row);
            return true;
        }

        /// <summary>
        /// The screen point at the <b>centre</b> of a board cell — the point a pointer aims at.
        /// </summary>
        /// <remarks>
        /// The centre, not the transform's position: a cell's pivot is its top-left corner, and a
        /// corner lies on the boundary of the cell and of the board, which
        /// <see cref="RectTransformUtility.RectangleContainsScreenPoint(RectTransform, Vector2, Camera)"/>
        /// and the cell arithmetic both treat as <i>outside</i>. Published corner points resolved to
        /// the neighbouring cell (or to no zone at all), so a browser test aiming at what this screen
        /// reported dropped its piece one cell off — the Stage 09b defect this method's round trip is
        /// asserted against.
        /// </remarks>
        public bool TryGetBoardCellScreenPoint(int column, int row, out Vector2 screen)
        {
            screen = default;
            int index = (row * _boardWidth) + column;
            if (column < 0 || row < 0 || index < 0 || index >= _boardCells.Count)
            {
                return false;
            }

            screen = CellCentreOnScreen(_boardCells[index].rectTransform);
            return true;
        }

        /// <summary>The screen point at the centre of a chest slot.</summary>
        public bool TryGetChestSlotScreenPoint(int slot, out Vector2 screen)
        {
            screen = default;
            if (slot < 0 || slot >= _chestCells.Count)
            {
                return false;
            }

            screen = CellCentreOnScreen(_chestCells[slot].rectTransform);
            return true;
        }

        private static Vector2 CellCentreOnScreen(RectTransform cell) =>
            RectTransformUtility.WorldToScreenPoint(null, cell.TransformPoint(cell.rect.center));

        /// <summary>The centre of the altar pot: the point a dropped piece aims at.</summary>
        /// <remarks>
        /// The <b>centre</b> of the pot, not its transform position: the pot is anchored and
        /// pivoted at its bottom-centre, so its position is a point on the pot's own bottom edge —
        /// the same boundary-the-corner-lies-on mistake the cell points had.
        /// </remarks>
        public bool TryGetAltarScreenPoint(out Vector2 screen) =>
            CampScreenRects.TryCentre(_altarRect, out screen);

        /// <summary>
        /// The screen <b>rectangle</b> of a control, and false when the view has not been given one
        /// or it is hidden (so the layout report never names a control a mouse cannot reach).
        /// </summary>
        public bool TryGetButtonScreenRect(CampBagButton button, out Rect rect) =>
            CampScreenRects.TryOf(ControlRect(button), out rect);

        /// <summary>The point a click aims at: the control's centre, never a corner.</summary>
        /// <remarks>
        /// A corner lies on the rectangle's own boundary, and <c>Rect.Contains</c> excludes the
        /// right and top edges — so a click aimed at a published corner can miss the button it was
        /// aimed at and land on whatever is behind it. Every screen point this view publishes is a
        /// centre for that reason.
        /// </remarks>
        public bool TryGetButtonScreenPoint(CampBagButton button, out Vector2 screen)
        {
            screen = default;
            if (!TryGetButtonScreenRect(button, out Rect rect))
            {
                return false;
            }

            screen = rect.center;
            return true;
        }

        /// <summary>The screen rectangle of a named region of the bag screen.</summary>
        public bool TryGetSurfaceScreenRect(CampBagSurface surface, out Rect rect)
        {
            rect = default;

            RectTransform target = surface switch
            {
                CampBagSurface.Board => _boardRect,
                CampBagSurface.ChestPanel => _chestPanelRect,
                CampBagSurface.Chest => _chestRect,
                CampBagSurface.Altar => _altarRect,
                CampBagSurface.Instructions => _instructionsRect,
                CampBagSurface.Stats => _statsRect,
                _ => null,
            };

            return CampScreenRects.TryOf(target, out rect);
        }

        private RectTransform ControlRect(CampBagButton button) => button switch
        {
            CampBagButton.Open => RectOf(_openButton),
            CampBagButton.Close => RectOf(_closeButton),
            CampBagButton.Rotate => RectOf(_rotateButton),
            CampBagButton.Undo => RectOf(_undoButton),
            CampBagButton.Reset => RectOf(_resetButton),
            CampBagButton.Lock => RectOf(_lockButton),
            CampBagButton.LockRestore => RectOf(_lockRestoreToggle),
            CampBagButton.NextFloor => RectOf(_nextFloorButton),
            _ => null,
        };

        private static RectTransform RectOf(Component control) =>
            control == null ? null : (RectTransform)control.transform;

        public bool TryGetDepartButtonScreenPoint(out Vector2 screen) =>
            TryGetButtonScreenPoint(CampBagButton.NextFloor, out screen);

        public float CellPitch => _cellSize + _cellGap;

        // ------------------------------------------------------------------
        // rendering
        // ------------------------------------------------------------------

        private void RenderBag(CampBagScreen model)
        {
            if (model == null)
            {
                return;
            }

            Model = model;

            EnsureCells(model.BoardWidth, model.BoardHeight, model.ChestSlots);

            for (int i = 0; i < _boardCells.Count; i++)
            {
                _boardCells[i].color = _cellIdle;
            }

            for (int i = 0; i < _chestCells.Count; i++)
            {
                _chestCells[i].color = _cellIdle;
            }

            _renderedRevision = model.Revision;

            _painted.Clear();
            int used = 0;
            for (int i = 0; i < model.Pieces.Length; i++)
            {
                CampPieceModel piece = model.Pieces[i];

                if (piece.IsOnBoard)
                {
                    PaintActiveSlots(piece);
                }

                used = Paint(piece, used);
            }

            PaintHighlight(model.Highlight);

            for (int i = used; i < _pieceViews.Count; i++)
            {
                _pieceViews[i].Hide();
            }

            // The names are decided once the pieces are placed: whether a bag may keep its own name
            // depends on which items ended up on top of it.
            LayoutPieceLabels();

            RenderLinks(model.Links);
        }

        /// <summary>
        /// Draws the two permanent marks of a working combo: a line for a ตำรา → อาวุธอาคม pair, a
        /// badge for the bag-area bonus. <see cref="CampBagScreen.Links"/> is what it draws, and it
        /// hides every mark the model no longer lists, so a pair that stopped qualifying loses its
        /// mark on the same render that dropped it.
        /// </summary>
        /// <remarks>
        /// Lines are drawn <b>under</b> the pieces and their names: a line through a name is a line
        /// the player cannot read the board through, so the marks sit directly above the cells and
        /// below the text.
        /// </remarks>
        private void RenderLinks(CampLink[] links)
        {
            if (_boardRect == null || Model == null)
            {
                VisibleLineCount = 0;
                VisibleBadgeCount = 0;
                return;
            }

            EnsureLinkRoots();

            float pitch = CellPitch;
            int lineIdx = 0;
            int badgeIdx = 0;

            if (links != null)
            {
                for (int i = 0; i < links.Length; i++)
                {
                    CampLink link = links[i];

                    if (!TryFindPiece(link.SourceInstanceId, out CampPieceModel source)
                        || !TryFindPiece(link.TargetInstanceId, out CampPieceModel target)
                        || !source.IsOnBoard || !target.IsOnBoard)
                    {
                        continue;
                    }

                    if (link.Style == CampLinkStyle.Badge)
                    {
                        PlaceBadge(badgeIdx++, target, pitch);
                    }
                    else
                    {
                        PlaceLine(lineIdx++, source, target, pitch);
                    }
                }
            }

            for (int i = lineIdx; i < _synergyLines.Count; i++)
            {
                _synergyLines[i].gameObject.SetActive(false);
            }

            for (int i = badgeIdx; i < _badges.Count; i++)
            {
                _badges[i].gameObject.SetActive(false);
            }

            VisibleLineCount = lineIdx;
            VisibleBadgeCount = badgeIdx;
        }

        private bool TryFindPiece(int instanceId, out CampPieceModel piece)
        {
            CampPieceModel[] pieces = Model.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Piece.InstanceId == instanceId)
                {
                    piece = pieces[i];
                    return true;
                }
            }

            piece = default;
            return false;
        }

        /// <summary>Lays a line from the centre of one piece to the centre of another.</summary>
        private void PlaceLine(int index, CampPieceModel source, CampPieceModel target, float pitch)
        {
            Vector2 from = CentreOf(source, pitch, _cellGap);
            Vector2 to = CentreOf(target, pitch, _cellGap);

            Image line = GetOrCreateLine(index);
            line.gameObject.SetActive(true);
            line.color = new Color(1f, 0.82f, 0.25f, 0.85f);

            RectTransform rect = line.rectTransform;
            Vector2 direction = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(direction.magnitude, 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Marks a piece the bag-area bonus reached, on the piece's own corner: the rule is about the
        /// cells a bag covers, so the mark belongs to what was covered, and a line from the bag would
        /// say the bag is a partner on the board.
        /// </summary>
        private void PlaceBadge(int index, CampPieceModel target, float pitch)
        {
            Rect piece = PieceRect(target, pitch, _cellGap);

            Image badge = GetOrCreateBadge(index);
            badge.gameObject.SetActive(true);

            RectTransform rect = badge.rectTransform;
            rect.anchoredPosition = new Vector2(piece.xMax - BadgeSize - 2f, piece.yMin - 2f);
        }

        /// <summary>
        /// The two roots the marks live in, kept directly above the cells and below every piece.
        /// </summary>
        /// <remarks>
        /// Their sibling index is reset on every render because pieces are created lazily and each new
        /// one is appended: without this a piece added later would be drawn under the existing lines.
        /// </remarks>
        private void EnsureLinkRoots()
        {
            if (_linkRoot == null)
            {
                _linkRoot = CreateOverlayRoot("SynergyLines");
            }

            if (_badgeRoot == null)
            {
                _badgeRoot = CreateOverlayRoot("BagBonusBadges");
            }

            int cells = _boardCells.Count;
            if (cells > _boardRect.childCount - 1)
            {
                cells = _boardRect.childCount - 1;
            }

            _linkRoot.SetSiblingIndex(cells < 0 ? 0 : cells);

            int badgeIndex = cells + 1;
            _badgeRoot.SetSiblingIndex(badgeIndex > _boardRect.childCount - 1 ? _boardRect.childCount - 1 : badgeIndex);
        }

        private RectTransform CreateOverlayRoot(string name)
        {
            var rootObject = new GameObject(name, typeof(RectTransform));
            var root = (RectTransform)rootObject.transform;
            root.SetParent(_boardRect, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.pivot = new Vector2(0f, 1f);
            return root;
        }

        private Image GetOrCreateLine(int index)
        {
            if (index < _synergyLines.Count)
            {
                return _synergyLines[index];
            }

            Image line = CreateMark(_linkRoot, "SynLine" + index, 4f);
            _synergyLines.Add(line);
            return line;
        }

        private Image GetOrCreateBadge(int index)
        {
            if (index < _badges.Count)
            {
                return _badges[index];
            }

            Image badge = CreateMark(_badgeRoot, "BagBadge" + index, BadgeSize);
            badge.color = new Color(0.45f, 0.85f, 0.95f, 0.95f);
            _badges.Add(badge);
            return badge;
        }

        private static Image CreateMark(RectTransform parent, string name, float size)
        {
            var markObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)markObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            var image = markObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The centre of a piece in board-local units, which is where a line ends.</summary>
        private static Vector2 CentreOf(CampPieceModel piece, float pitch, float gap)
        {
            Rect rect = PieceRect(piece, pitch, gap);
            return new Vector2(rect.x + (rect.width * 0.5f), rect.y + (rect.height * 0.5f));
        }

        /// <summary>
        /// A piece's rectangle in board-local units, built the way <see cref="CampPieceView.Show"/>
        /// places it: x right, y <b>down</b> from the board's top-left corner, both in cell pitches.
        /// </summary>
        private static Rect PieceRect(CampPieceModel piece, float pitch, float gap)
        {
            int width = piece.Shape == null ? 1 : piece.Shape.Width;
            int height = piece.Shape == null ? 1 : piece.Shape.Height;

            return new Rect(
                piece.Origin.X * pitch,
                -(piece.Origin.Y * pitch),
                (width * pitch) - gap,
                (height * pitch) - gap);
        }

        /// <summary>
        /// Shows or hides each bag's own name.
        /// </summary>
        /// <remarks>
        /// The item on top of a bag is the primary text; a bag's name is secondary. Both are drawn in
        /// the same local space, so "an item's rectangle reaches the strip the bag's name is drawn
        /// in" is an exact test and not an estimate of how wide a string will be
        /// (Stage 09e work item 1: "ชื่อย่ามกับชื่อไอเทมต้องไม่ซ้อนกัน (ชื่อไอเทมเป็นหลัก ชื่อย่ามเป็น
        /// ข้อความรองหรือซ่อนเมื่อที่ไม่พอ)").
        /// </remarks>
        private void LayoutPieceLabels()
        {
            float pitch = CellPitch;

            for (int i = 0; i < _painted.Count; i++)
            {
                CampPieceModel piece = _painted[i];

                if (!piece.IsBag || !piece.IsOnBoard)
                {
                    _pieceViews[i].SetLabel(true, secondary: false);
                    continue;
                }

                Rect strip = BagLabelStrip(piece, pitch, _cellGap);
                bool covered = false;

                for (int j = 0; j < _painted.Count && !covered; j++)
                {
                    CampPieceModel other = _painted[j];

                    if (other.IsBag || !other.IsOnBoard)
                    {
                        continue;
                    }

                    Rect rect = PieceRect(other, pitch, _cellGap);
                    covered = strip.xMin < rect.xMax && rect.xMin < strip.xMax
                        && strip.yMin < rect.yMax && rect.yMin < strip.yMax;
                }

                _pieceViews[i].SetLabel(!covered, secondary: true);
            }
        }

        /// <summary>The strip of a bag a name may be drawn in: its top edge, inset from both sides.</summary>
        private static Rect BagLabelStrip(CampPieceModel bag, float pitch, float gap)
        {
            Rect rect = PieceRect(bag, pitch, gap);
            return new Rect(rect.x + 4f, rect.y, Mathf.Max(rect.width - 8f, 1f), BagLabelHeight);
        }


        private void RenderDrag(CampDragState drag)
        {
            if (_ghost == null)
            {
                return;
            }

            if (!drag.IsActive)
            {
                HideGhost();
                if (Model != null)
                {
                    RenderBag(Model);
                }
                return;
            }

            _ghost.enabled = true;
            _ghost.color = drag.Action == BagDropAction.Merge
                ? _ghostMerge
                : drag.IsValid ? _ghostValid : _ghostInvalid;

            if (_ghostLabel != null)
            {
                _ghostLabel.text = string.Empty;
            }

            Footprint shape = null;
            if (Model != null)
            {
                for (int i = 0; i < Model.Pieces.Length; i++)
                {
                    if (Model.Pieces[i].Piece == drag.Piece)
                    {
                        shape = Model.Pieces[i].Shape;
                        break;
                    }
                }
            }

            int w = shape?.Width ?? 1;
            int h = shape?.Height ?? 1;
            float pitch = CellPitch;
            _ghostRect.sizeDelta = new Vector2((w * pitch) - _cellGap, (h * pitch) - _cellGap);
            _ghostRect.SetAsLastSibling();

            HighlightDropCells(drag, shape);
            RenderCarryGhosts(drag);
        }

        /// <summary>
        /// Draws a ghost for every item that would come along with the dragged bag, at the cell each
        /// would land on.
        /// </summary>
        /// <remarks>
        /// The positions are the presenter's, which are the run's: this method turns a published plan
        /// into rectangles and decides nothing about which items ride, where they land, or whether the
        /// drop is accepted (AGENTS rule 10). The colour is the bag's own ghost colour, fainter, so the
        /// rider's ghost reads as part of the same gesture.
        /// </remarks>
        private void RenderCarryGhosts(CampDragState drag)
        {
            int count = drag.CarryCount;
            Color colour = drag.IsValid ? _ghostValid : _ghostInvalid;
            colour.a = CarryGhostAlpha;
            float pitch = CellPitch;

            for (int i = 0; i < count; i++)
            {
                CampCarryGhost plan = drag.CarryAt(i);

                if (i >= _carryGhosts.Count)
                {
                    _carryGhosts.Add(CreateCarryGhost());
                }

                Image ghost = _carryGhosts[i];
                ghost.enabled = true;
                ghost.color = colour;

                var rect = (RectTransform)ghost.transform;
                rect.anchoredPosition = new Vector2(plan.Origin.X * pitch, -(plan.Origin.Y * pitch));
                rect.sizeDelta = new Vector2(
                    (plan.Width * pitch) - _cellGap,
                    (plan.Height * pitch) - _cellGap);
                rect.SetAsLastSibling();
            }

            for (int i = count; i < _carryGhosts.Count; i++)
            {
                _carryGhosts[i].enabled = false;
            }

            _drawnCarry = count == 0 ? NoCarryPlans : drag.Carry;
            VisibleCarryGhostCount = count;
        }

        /// <summary>Puts every rider ghost away, which is what the end of a drag does.</summary>
        private void HideCarryGhosts()
        {
            for (int i = 0; i < _carryGhosts.Count; i++)
            {
                _carryGhosts[i].enabled = false;
            }

            _drawnCarry = NoCarryPlans;
            VisibleCarryGhostCount = 0;
        }

        private Image CreateCarryGhost()
        {
            var gameObject = new GameObject("CarryGhost", typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(_boardRect, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        /// <summary>
        /// The screen rectangle of the ghost drawn for an item riding on the dragged bag, or false when
        /// that item has no ghost on screen.
        /// </summary>
        /// <remarks>
        /// Published so a test can state the stage's promise as geometry: the ghost is where the item
        /// really goes, so the rectangle captured mid-drag has to be the rectangle the item is drawn in
        /// once the drop has landed.
        /// </remarks>
        public bool TryGetCarryGhostScreenRect(PieceRef piece, out Rect rect)
        {
            rect = default;

            for (int i = 0; i < _drawnCarry.Length && i < _carryGhosts.Count; i++)
            {
                if (_drawnCarry[i].Piece != piece)
                {
                    continue;
                }

                return _carryGhosts[i].enabled
                    && CampScreenRects.TryOf((RectTransform)_carryGhosts[i].transform, out rect);
            }

            return false;
        }

        private void UpdateGhostPosition(Vector2 screenPoint)
        {
            if (_ghostRect == null)
            {
                return;
            }

            _ghostRect.position = screenPoint;
            _ghostRect.SetAsLastSibling();
        }

        private void HideGhost()
        {
            if (_ghost != null)
            {
                _ghost.enabled = false;
            }

            HideCarryGhosts();
        }

        private void HighlightDropCells(CampDragState drag, Footprint shape)
        {
            // Reset board cells to their idle/active color
            for (int i = 0; i < _boardCells.Count; i++)
            {
                _boardCells[i].color = _cellIdle;
            }

            if (Model != null)
            {
                for (int i = 0; i < Model.Pieces.Length; i++)
                {
                    if (Model.Pieces[i].IsOnBoard)
                    {
                        PaintActiveSlots(Model.Pieces[i]);
                    }
                }
            }

            for (int i = 0; i < _chestCells.Count; i++)
            {
                _chestCells[i].color = _cellIdle;
            }

            if (drag.Zone == BagDropZone.Board && _boardWidth > 0 && _boardHeight > 0)
            {
                Color highlightColor = drag.Action == BagDropAction.Merge ? _ghostMerge :
                    drag.IsValid ? _ghostValid : _ghostInvalid;
                highlightColor.a = HighlightAlpha;

                if (shape != null)
                {
                    for (int i = 0; i < shape.CellCount; i++)
                    {
                        CellOffset cell = shape.CellAt(i);
                        int col = drag.Origin.X + cell.X;
                        int row = drag.Origin.Y + cell.Y;
                        if (col >= 0 && col < _boardWidth && row >= 0 && row < _boardHeight)
                        {
                            int idx = (row * _boardWidth) + col;
                            if (idx >= 0 && idx < _boardCells.Count)
                            {
                                _boardCells[idx].color = highlightColor;
                            }
                        }
                    }
                }
            }
            else if (drag.Zone == BagDropZone.Chest)
            {
                int slot = drag.Origin.X + (drag.Origin.Y * _chestColumns);
                if (slot >= 0 && slot < _chestCells.Count)
                {
                    Color highlightColor = drag.IsValid ? _ghostValid : _ghostInvalid;
                    highlightColor.a = HighlightAlpha;
                    _chestCells[slot].color = highlightColor;
                }
            }
        }

        private void EnsureCells(int width, int height, int chestSlots)
        {
            if (width != _boardWidth || height != _boardHeight)
            {
                _boardWidth = width;
                _boardHeight = height;

                for (int i = _boardCells.Count; i < width * height; i++)
                {
                    _boardCells.Add(CreateCell(_boardRect, "Cell" + i));
                }

                for (int i = 0; i < width * height; i++)
                {
                    int column = i % width;
                    int row = i / width;
                    float pitch = CellPitch;
                    RectTransform rect = _boardCells[i].rectTransform;
                    rect.anchoredPosition = new Vector2(column * pitch, -row * pitch);
                    rect.sizeDelta = new Vector2(_cellSize, _cellSize);
                }
            }

            if (chestSlots != _chestSlots)
            {
                _chestSlots = chestSlots;

                for (int i = _chestCells.Count; i < chestSlots; i++)
                {
                    _chestCells.Add(CreateCell(_chestRect, "ChestSlot" + i));
                }

                float pitch = _chestCellSize + _chestCellGap;
                for (int i = 0; i < chestSlots; i++)
                {
                    int column = i % _chestColumns;
                    int row = i / _chestColumns;
                    RectTransform rect = _chestCells[i].rectTransform;
                    rect.anchoredPosition = new Vector2(column * pitch, -row * pitch);
                    rect.sizeDelta = new Vector2(_chestCellSize, _chestCellSize);
                }
            }
        }

        private Image CreateCell(RectTransform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = _cellIdle;
            return image;
        }

        private void PaintActiveSlots(CampPieceModel piece)
        {
            if (!piece.IsBag || piece.Shape == null)
            {
                return;
            }

            for (int i = 0; i < piece.Shape.CellCount; i++)
            {
                CellOffset cell = piece.Shape.CellAt(i);
                int index = ((piece.Origin.Y + cell.Y) * _boardWidth) + piece.Origin.X + cell.X;
                if (index >= 0 && index < _boardCells.Count)
                {
                    _boardCells[index].color = _cellActive;
                }
            }
        }

        private void PaintHighlight(CellOffset[] cells)
        {
            if (cells == null)
            {
                return;
            }

            for (int i = 0; i < cells.Length; i++)
            {
                int index = (cells[i].Y * _boardWidth) + cells[i].X;
                if (index >= 0 && index < _boardCells.Count)
                {
                    Color colour = _cellHighlight;
                    colour.a = HighlightAlpha;
                    _boardCells[index].color = colour;
                }
            }
        }

        private int Paint(CampPieceModel piece, int used)
        {
            if (piece.IsOnBoard && _presenter != null && _presenter.IsDragging
                && _presenter.DraggedPiece == piece.Piece)
            {
                return used;
            }

            CampPieceView view;
            if (used < _pieceViews.Count)
            {
                view = _pieceViews[used];
            }
            else
            {
                view = CampPieceView.Create(_boardRect, _font);
                _pieceViews.Add(view);
            }

            view.Show(piece, this);

            // The pool index and the painted array are the same index on purpose: the name test walks
            // the painted pieces and asks the view at that index where its label is.
            _painted.Add(piece);
            return used + 1;
        }

        private void CreateGhost()
        {
            if (_ghost != null)
            {
                return;
            }

            var gameObject = new GameObject("DragGhost", typeof(RectTransform));
            _ghostRect = (RectTransform)gameObject.transform;
            _ghostRect.SetParent(transform, false);
            _ghostRect.anchorMin = new Vector2(0f, 1f);
            _ghostRect.anchorMax = new Vector2(0f, 1f);
            _ghostRect.pivot = new Vector2(0.5f, 0.5f);
            _ghostRect.sizeDelta = new Vector2(_cellSize, _cellSize);

            _ghost = gameObject.AddComponent<Image>();
            _ghost.raycastTarget = false;
            _ghost.enabled = false;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelObject.transform;
            labelRect.SetParent(_ghostRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            _ghostLabel = labelObject.AddComponent<TextMeshProUGUI>();
            _ghostLabel.raycastTarget = false;
            // Stage 09d item 2: the same 22 px floor as the panel's authored labels, at the canvas'
            // 1920×1080 reference resolution (see ProjectSetup.UiFontSize).
            _ghostLabel.fontSize = 22f;
            if (_font != null)
            {
                _ghostLabel.font = _font;
            }
        }

        /// <summary>
        /// The piece the pointer is over: the <b>topmost</b> one, which is the last the view drew.
        /// </summary>
        /// <remarks>
        /// Searched from the end of the array on purpose. The pieces are painted in array order, so
        /// the last entry is the one on top; searching forwards returned the bottom-most piece, and
        /// because a bag is listed before the items it carries — and an item can only ever sit on a
        /// bag cell (Stage 05) — a forward search meant the pointer could <b>never</b> pick an item
        /// up: every press on an item grabbed the bag under it. That is the Stage 09b defect
        /// "ลากไอเทมไม่ได้", and it is why the hit test has to agree with what is drawn on top.
        /// </remarks>
        private PieceRef PieceAt(BagDropZone zone, CellOffset cell)
        {
            if (Model == null)
            {
                return PieceRef.None;
            }

            if (zone == BagDropZone.Board)
            {
                for (int i = Model.Pieces.Length - 1; i >= 0; i--)
                {
                    CampPieceModel piece = Model.Pieces[i];
                    if (piece.IsOnBoard && Occupies(piece, cell))
                    {
                        return piece.Piece;
                    }
                }
            }

            if (zone == BagDropZone.Chest)
            {
                for (int i = 0; i < Model.Pieces.Length; i++)
                {
                    CampPieceModel piece = Model.Pieces[i];
                    if (!piece.IsOnBoard && piece.ChestSlot >= 0)
                    {
                        if (SlotIndexAt(cell) == piece.ChestSlot)
                        {
                            return piece.Piece;
                        }
                    }
                }
            }

            return PieceRef.None;
        }

        private int SlotIndexAt(CellOffset cell) => (cell.Y * _chestColumns) + cell.X;

        private static bool Occupies(CampPieceModel piece, CellOffset cell)
        {
            CellOffset local = new CellOffset(cell.X - piece.Origin.X, cell.Y - piece.Origin.Y);
            return piece.Shape != null && piece.Shape.Contains(local);
        }

        private Rotation RotationFor(PieceRef piece)
        {
            if (Model == null)
            {
                return Rotation.Degrees0;
            }

            for (int i = 0; i < Model.Pieces.Length; i++)
            {
                if (Model.Pieces[i].Piece == piece)
                {
                    return Model.Pieces[i].IsOnBoard ? Model.Pieces[i].Rotation : Rotation.Degrees0;
                }
            }

            return Rotation.Degrees0;
        }

        private bool IsDraggingPiece(PieceRef piece) =>
            _presenter != null && _presenter.IsDragging && _presenter.DraggedPiece == piece;

        // ------------------------------------------------------------------
        // the merge chooser (Stage 09g)
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the choice modal once, at runtime, under this panel.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Built rather than authored, the way the tooltip's own widgets are: the modal is only ever
        /// seen while จัดย่าม is open, every string on it arrives from the state, and building it here
        /// means it needs no `ProjectSetup` pass and no scene rebuild.
        /// </para>
        /// <para>
        /// It is parented to this panel, which is the full-screen surface the whole จัดย่าม screen hangs
        /// off, so its dim layer covers everything the player could otherwise press.
        /// </para>
        /// </remarks>
        private void CreateMergeChooser()
        {
            if (_chooserRoot != null)
            {
                return;
            }

            var rootObject = new GameObject("MergeChooser", typeof(RectTransform));
            _chooserRoot = (RectTransform)rootObject.transform;
            _chooserRoot.SetParent(transform, false);
            _chooserRoot.anchorMin = Vector2.zero;
            _chooserRoot.anchorMax = Vector2.one;
            _chooserRoot.offsetMin = Vector2.zero;
            _chooserRoot.offsetMax = Vector2.zero;

            var dimObject = new GameObject("Dim", typeof(RectTransform));
            _chooserDimRect = (RectTransform)dimObject.transform;
            _chooserDimRect.SetParent(_chooserRoot, false);
            _chooserDimRect.anchorMin = Vector2.zero;
            _chooserDimRect.anchorMax = Vector2.one;
            _chooserDimRect.offsetMin = Vector2.zero;
            _chooserDimRect.offsetMax = Vector2.zero;
            var dim = dimObject.AddComponent<Image>();
            dim.color = _chooserDim;
            // Raycast-blocking on purpose: this half is the lock. Everything the player might press
            // lives behind it, and the EventSystem never delivers a pointer event to a graphic that a
            // raycast target sits in front of.
            dim.raycastTarget = true;

            var panelObject = new GameObject("Panel", typeof(RectTransform));
            _chooserPanelRect = (RectTransform)panelObject.transform;
            _chooserPanelRect.SetParent(_chooserRoot, false);
            _chooserPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _chooserPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _chooserPanelRect.pivot = new Vector2(0.5f, 0.5f);
            _chooserPanelRect.anchoredPosition = Vector2.zero;
            _chooserPanelRect.sizeDelta = ChooserPanelSize;
            var background = panelObject.AddComponent<Image>();
            background.color = _chooserPanel;
            // Also a raycast target: a press that misses every card lands here and does nothing, which
            // is better than falling through to the board.
            background.raycastTarget = true;

            // The heading, top-left rather than centred: it heads both the column below it and the row
            // of cards beside it.
            _chooserTitleLabel = CreateChooserLabel(
                _chooserPanelRect, "Title", new Vector2(28f, -18f), new Vector2(900f, 40f), 30f,
                TextAlignmentOptions.TopLeft, _font);

            // The left column: the item as the tooltip describes it — ชื่อ, ระดับ, สถานะ, คำอธิบาย,
            // คอมโบที่ทำงานอยู่ — one label per line, so an empty line is hidden rather than drawn blank.
            // The item's own name is the column's heading; the modal's heading above it says what the
            // screen is for.
            for (int i = 0; i < _chooserSourceLines.Length; i++)
            {
                _chooserSourceLines[i] = CreateChooserLabel(
                    _chooserPanelRect, "Source" + i, new Vector2(28f, -78f - (i * 34f)),
                    new Vector2(420f, 32f), 22f, TextAlignmentOptions.TopLeft, _font);
                _chooserSourceLines[i].gameObject.SetActive(false);
            }

            // Three cards in one row, centred in the space to the right of the left column.
            const float cardWidth = 328f;
            const float cardHeight = 400f;
            const float cardGap = 16f;
            const float cardsLeft = 480f;
            const float cardsWidth = 1056f;
            float total = (ChooserCardCapacity * cardWidth) + ((ChooserCardCapacity - 1) * cardGap);
            float first = cardsLeft + ((cardsWidth - total) * 0.5f);

            for (int i = 0; i < ChooserCardCapacity; i++)
            {
                _chooserCards.Add(MergeCardView.Create(
                    _chooserPanelRect,
                    _font,
                    i,
                    first + (i * (cardWidth + cardGap)),
                    -78f,
                    cardWidth,
                    cardHeight,
                    _chooserCard,
                    _chooserPick,
                    OnChoiceClicked));
            }

            // The short explanation under the row: what a pick does, and that closing changes nothing.
            _chooserHintLabel = CreateChooserLabel(
                _chooserPanelRect, "Hint", new Vector2(28f, -520f), new Vector2(1210f, 30f), 22f,
                TextAlignmentOptions.TopLeft, _font);

            // ย้อนกลับ, top-right: the same thing Escape does.
            var backObject = new GameObject("Back", typeof(RectTransform));
            _chooserBackRect = (RectTransform)backObject.transform;
            _chooserBackRect.SetParent(_chooserPanelRect, false);
            _chooserBackRect.anchorMin = new Vector2(0f, 1f);
            _chooserBackRect.anchorMax = new Vector2(0f, 1f);
            _chooserBackRect.pivot = new Vector2(0f, 1f);
            _chooserBackRect.anchoredPosition = new Vector2(ChooserPanelSize.x - 168f, -18f);
            _chooserBackRect.sizeDelta = new Vector2(140f, 44f);
            var backImage = backObject.AddComponent<Image>();
            backImage.color = _chooserPick;
            backImage.raycastTarget = true;
            var backButton = backObject.AddComponent<Button>();
            backButton.targetGraphic = backImage;
            backButton.onClick.AddListener(OnChooserBackClicked);
            _chooserBackLabel = CreateChooserLabel(
                _chooserBackRect, "BackLabel", Vector2.zero, new Vector2(140f, 44f), 22f,
                TextAlignmentOptions.Center, _font, stretch: true);

            // Closed until a merge asks: the modal is published as a replaced model, and Closed is a
            // shared constant, so this is the state it starts in.
            _chooserRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// One label of the modal. Anchored to its parent's top-left like every authored label on this
        /// panel, at the same 22 px floor (Stage 09d item 2), unless <paramref name="stretch"/> asks it
        /// to fill its parent instead — which is what a button's own caption wants.
        /// </summary>
        /// <remarks>
        /// Static and font-passed so <see cref="MergeCardView"/>, a class of its own, builds its labels
        /// the same way instead of repeating this.
        /// </remarks>
        private static TMP_Text CreateChooserLabel(
            RectTransform parent, string name, Vector2 position, Vector2 size, float fontSize,
            TextAlignmentOptions alignment, TMP_FontAsset font, bool stretch = false)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);

            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }

            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            if (font != null)
            {
                label.font = font;
            }

            return label;
        }

        /// <summary>
        /// Draws the chooser from one replaced model, the way the bag screen redraws from
        /// <see cref="CampBagScreen"/>. Closed hides the root and leaves every widget where it is, so
        /// opening the modal again — which ADR 0021 makes the same decision every time — allocates
        /// nothing.
        /// </summary>
        private void RenderMergeChooser(CampMergeChooserModel chooser)
        {
            if (_chooserRoot == null)
            {
                return;
            }

            bool open = chooser != null && chooser.IsOpen;

            if (_chooserRoot.gameObject.activeSelf != open)
            {
                _chooserRoot.gameObject.SetActive(open);
            }

            if (!open)
            {
                _chooserCardsShown = 0;
                return;
            }

            _chooserTitleLabel.text = chooser.Title;
            _chooserHintLabel.text = chooser.Hint;
            _chooserBackLabel.text = chooser.BackLabel;

            string[] source = chooser.SourceLines;
            for (int i = 0; i < _chooserSourceLines.Length; i++)
            {
                string line = source != null && i < source.Length ? source[i] : string.Empty;
                bool drawn = !string.IsNullOrEmpty(line);

                if (_chooserSourceLines[i].gameObject.activeSelf != drawn)
                {
                    _chooserSourceLines[i].gameObject.SetActive(drawn);
                }

                if (drawn)
                {
                    _chooserSourceLines[i].text = line;
                }
            }

            int cards = Mathf.Clamp(chooser.ChoiceCount, 0, _chooserCards.Count);

            for (int i = 0; i < _chooserCards.Count; i++)
            {
                if (i < cards)
                {
                    _chooserCards[i].Show(chooser.Choices[i], _chooserBetter, _chooserWorse);
                }
                else
                {
                    _chooserCards[i].Hide();
                }
            }

            _chooserCardsShown = cards;
        }

        /// <summary>
        /// One pooled card of the choice modal (Stage 09g): a numbered strip, the variant's name over
        /// the step, and one line per stat the variant changes.
        /// </summary>
        /// <remarks>
        /// The whole card is the pick target — a mouse click and a tap both land on it, and the strip
        /// under it only names the key that does the same thing. The strip's own graphics do not raycast,
        /// so a press on the strip is a press on the card and there is exactly one listener per card.
        /// </remarks>
        private sealed class MergeCardView
        {
            private TMP_Text _titleLabel;
            private TMP_Text _levelLabel;
            private TMP_Text[] _changeLabels;

            /// <summary>The card's rectangle: the hit area, and what a test or the report measures.</summary>
            public RectTransform Root { get; private set; }

            public static MergeCardView Create(
                RectTransform parent, TMP_FontAsset font, int index, float x, float y, float width,
                float height, Color background, Color pick, Action<int> onPick)
            {
                var gameObject = new GameObject("Choice" + index, typeof(RectTransform));
                var rect = (RectTransform)gameObject.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.sizeDelta = new Vector2(width, height);

                var image = gameObject.AddComponent<Image>();
                image.color = background;
                image.raycastTarget = true;

                var button = gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => onPick(index));

                var card = new MergeCardView
                {
                    Root = rect,
                    _changeLabels = new TMP_Text[ChooserChangeLineCapacity],
                };

                var pickObject = new GameObject("Pick", typeof(RectTransform));
                var pickRect = (RectTransform)pickObject.transform;
                pickRect.SetParent(rect, false);
                pickRect.anchorMin = new Vector2(0f, 1f);
                pickRect.anchorMax = new Vector2(0f, 1f);
                pickRect.pivot = new Vector2(0f, 1f);
                pickRect.anchoredPosition = new Vector2(12f, -(height - 56f));
                pickRect.sizeDelta = new Vector2(width - 24f, 44f);

                var pickImage = pickObject.AddComponent<Image>();
                pickImage.color = pick;
                pickImage.raycastTarget = false;

                TMP_Text key = CreateChooserLabel(
                    pickRect, "Key", Vector2.zero, Vector2.zero, 24f, TextAlignmentOptions.Center, font,
                    stretch: true);

                // Not player text: this is the number of the 1/2/3 shortcut, and it is written once, at
                // build time, so opening the modal composes no string at all.
                key.text = (index + 1).ToString(CultureInfo.InvariantCulture);

                card._titleLabel = CreateChooserLabel(
                    rect, "Title", new Vector2(12f, -48f), new Vector2(width - 24f, 64f), 24f,
                    TextAlignmentOptions.TopLeft, font);
                card._levelLabel = CreateChooserLabel(
                    rect, "Level", new Vector2(12f, -118f), new Vector2(width - 24f, 30f), 22f,
                    TextAlignmentOptions.TopLeft, font);

                for (int i = 0; i < card._changeLabels.Length; i++)
                {
                    card._changeLabels[i] = CreateChooserLabel(
                        rect, "Change" + i, new Vector2(12f, -152f - (i * 32f)),
                        new Vector2(width - 24f, 28f), 22f, TextAlignmentOptions.TopLeft, font);
                }

                return card;
            }

            /// <summary>
            /// Paints one card: the name over the step, then one line per stat the variant changes, in
            /// the colour the model's own improvement flag chose for it. The model formats every string
            /// and decides every direction; this only assigns (AGENTS rule 10).
            /// </summary>
            public void Show(CampMergeChoiceModel choice, Color better, Color worse)
            {
                Root.gameObject.SetActive(true);

                _titleLabel.text = choice.Title;
                _levelLabel.text = choice.LevelLine;

                for (int i = 0; i < _changeLabels.Length; i++)
                {
                    bool changed = choice.Changes != null && i < choice.Changes.Length;

                    if (_changeLabels[i].gameObject.activeSelf != changed)
                    {
                        _changeLabels[i].gameObject.SetActive(changed);
                    }

                    if (!changed)
                    {
                        continue;
                    }

                    _changeLabels[i].text = choice.Changes[i];
                    _changeLabels[i].color = choice.Improvements != null
                        && i < choice.Improvements.Length && choice.Improvements[i]
                            ? better
                            : worse;
                }
            }

            /// <summary>Hides the card, for an offer with fewer choices than the pool has cards.</summary>
            public void Hide() => Root.gameObject.SetActive(false);
        }

        /// <summary>One pooled piece rectangle: a tinted box and its localized name.</summary>
        private sealed class CampPieceView
        {
            private RectTransform _rect;
            private Image _image;
            private TMP_Text _label;

            /// <summary>The piece's own rectangle, for the label rules.</summary>
            public RectTransform Rect => _rect;

            /// <summary>The name's own rectangle; null before <see cref="Create"/> has run.</summary>
            public RectTransform LabelRect => _label == null ? null : (RectTransform)_label.transform;

            public static CampPieceView Create(RectTransform parent, TMP_FontAsset font)
            {
                var gameObject = new GameObject("Piece", typeof(RectTransform));
                var rect = (RectTransform)gameObject.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);

                var image = gameObject.AddComponent<Image>();
                image.raycastTarget = false;

                var labelObject = new GameObject("Label", typeof(RectTransform));
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                var label = labelObject.AddComponent<TextMeshProUGUI>();
                label.raycastTarget = false;
                label.fontSize = 22f;
                label.alignment = TextAlignmentOptions.Center;
                if (font != null)
                {
                    label.font = font;
                }

                return new CampPieceView
                {
                    _rect = rect,
                    _image = image,
                    _label = label,
                };
            }

            public void Show(CampPieceModel piece, CampBagScreenView owner)
            {
                _rect.gameObject.SetActive(true);

                if (piece.IsOnBoard)
                {
                    _rect.SetParent(owner._boardRect, false);
                    float pitch = owner.CellPitch;
                    int width = piece.Shape == null ? 1 : piece.Shape.Width;
                    int height = piece.Shape == null ? 1 : piece.Shape.Height;

                    _rect.anchoredPosition = new Vector2(piece.Origin.X * pitch, -piece.Origin.Y * pitch);
                    _rect.sizeDelta = new Vector2(
                        (width * pitch) - owner._cellGap,
                        (height * pitch) - owner._cellGap);
                }
                else
                {
                    _rect.SetParent(owner._chestRect, false);
                    int slot = piece.ChestSlot < 0 ? 0 : piece.ChestSlot;
                    float pitch = owner._chestCellSize + owner._chestCellGap;
                    _rect.anchoredPosition = new Vector2(
                        (slot % owner._chestColumns) * pitch,
                        -(slot / owner._chestColumns) * pitch);
                    _rect.sizeDelta = new Vector2(owner._chestCellSize, owner._chestCellSize);
                }

                if (owner.SelectedPiece == piece.Piece)
                {
                    _image.color = owner._selectedPieceColor;
                }
                else
                {
                    _image.color = piece.IsBag ? owner._bagPiece : owner._itemPiece;
                }

                _label.text = piece.Label;
            }

            /// <summary>
            /// Whether the name is drawn, and where: an item's name is centred over its own rectangle
            /// (the primary text of the cell), a bag's name is a single line along its top edge (the
            /// secondary text, which the caller hides when an item reaches it).
            /// </summary>
            public void SetLabel(bool visible, bool secondary)
            {
                if (_label == null)
                {
                    return;
                }

                _label.gameObject.SetActive(visible);

                if (!visible)
                {
                    return;
                }

                RectTransform rect = (RectTransform)_label.transform;

                if (secondary)
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(-8f, BagLabelHeight);
                    rect.anchoredPosition = new Vector2(0f, -2f);
                    _label.alignment = TextAlignmentOptions.TopLeft;
                    _label.color = new Color(1f, 1f, 1f, 0.85f);
                }
                else
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    _label.alignment = TextAlignmentOptions.Center;
                    _label.color = Color.white;
                }
            }

            public void Hide() => _rect.gameObject.SetActive(false);
        }
    }
}
