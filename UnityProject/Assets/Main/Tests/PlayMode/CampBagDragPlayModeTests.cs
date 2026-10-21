using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Camp;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 09b, items B, D, E and F: the จัดย่าม screen driven by <b>real pointer events</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A mouse device is fed into the scene's own <c>InputSystemUIInputModule</c> and the press is
    /// resolved by the canvas's own <c>GraphicRaycaster</c>, so this suite fails when the thing that
    /// is broken is the input module, the raycaster, a transparent graphic covering the board, or the
    /// hit test — the four causes a test that calls a use case directly can never see. That is the
    /// whole point of the stage: Stage 09's drag "worked" in a test that never dragged
    /// ("ห้ามนับเทสต์ที่เรียก use case ตรงเป็นเทสต์ drag").
    /// </para>
    /// <para>
    /// The gesture spans several frames, like a human's: press, a few moves (the EventSystem's drag
    /// threshold is 10 screen pixels), release. Nothing here calls <c>CampPresenter</c> to move a
    /// piece; every state change in these tests comes from a pointer event.
    /// </para>
    /// </remarks>
    public sealed class CampBagDragPlayModeTests : InputTestFixture
    {
        private const string CampSceneName = "Camp";

        /// <summary>Intermediate pointer positions between press and release.</summary>
        private const int DragSteps = 6;

        /// <summary>The smallest type size the GDD allows on the bag screen at 1080p.</summary>
        /// <summary>
        /// Stage 09d item 2: Thai labels are authored at 22 px or more at the canvas' 1920×1080
        /// reference resolution. Stage 09b's floor was 14 px, which left the panel's body text below
        /// what Thai needs once its upper and lower marks are drawn.
        /// </summary>
        private const float MinimumFontSize = 22f;

        /// <summary>
        /// The floor for the one exception Stage 09i adds: a name drawn inside a piece's own cell may
        /// shrink to fit that rectangle, but never below 14 px at 1080p.
        /// </summary>
        private const float CellLabelFloor = 14f;

        /// <summary>
        /// Regions that are allowed to overlap because one is drawn inside the other. The chest's
        /// slot grid is a child of the chest panel; everything else has to be disjoint.
        /// </summary>
        private static readonly (CampBagSurface Outer, CampBagSurface Inner)[] NestedByDesign =
        {
            (CampBagSurface.ChestPanel, CampBagSurface.Chest),
        };

        /// <summary>The opacity the tooltip's own background has to reach (Stage 09d, item 2).</summary>
        private const float TooltipAlphaFloor = 0.92f;

        private Mouse _mouse;
        private CampSceneDirector _director;
        private CampBagScreenView _bag;
        private CampTooltipView _tooltip;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        /// <summary>
        /// Loads the Camp scene, starts it, and hands back the view under test.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Deliberately called from the test body and <b>not</b> from a <c>[UnitySetUp]</c>, which is
        /// the opposite of what this fixture did first. Measured, with everything else identical:
        /// loading the scene in <c>[UnitySetUp]</c> gives the drag 0 previews and no refused drop;
        /// loading it here gives 7 previews and 1 refused drop. See <c>docs/stages/09b-report.md</c>.
        /// </para>
        /// <para>
        /// The cause is <see cref="InputTestFixture.Setup"/>: it pushes the whole input system onto
        /// its own <c>InputTestRuntime</c>, removes every device and rewires the update hook, and in
        /// this runner it runs <i>after</i> a <c>[UnitySetUp]</c> coroutine has already advanced
        /// frames. A scene loaded there has its <c>InputSystemUIInputModule</c> enabled against the
        /// runtime that is torn out from under it a frame later, and the module then never delivers
        /// another pointer event — the device reports the position, the UI never hears about it.
        /// </para>
        /// </remarks>
        private IEnumerator LoadCamp()
        {
            // A shell this fixture owns. Stage 10a made the App scope persist across scene loads,
            // and a run that outlives a scene would otherwise carry another test's wallet, rented
            // shelves and locked slots into this one.
            SceneFlowHarness.ResetShell();
            yield return null;

            SceneManager.LoadScene(CampSceneName, LoadSceneMode.Single);
            yield return null;

            Assert.That(EventSystem.current, Is.Not.Null, "the Camp scene has no EventSystem");

            // The scene's own module, left exactly as the player gets it. An earlier version of
            // this fixture re-assigned the module's action asset here (to stop it reading a device
            // an earlier session had left behind) and every pointer test then reported that the
            // pointer never reached the view: the module hooks its action callbacks in its own
            // OnEnable, so swapping the asset afterwards leaves it without callbacks.
            Assert.That(
                EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null,
                "the EventSystem does not use the Input System module");

            _director = Object.FindFirstObjectByType<CampSceneDirector>();
            Assert.That(_director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(_director.TryStart(), Is.True, "the camp did not start: " + _director.StartError);
            yield return null;

            // FindObjectsInactive.Include: since Stage 09c the จัดย่าม panel starts closed, so the
            // view sits on an inactive GameObject and the default (active-only) search finds nothing.
            _bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
            Assert.That(_bag, Is.Not.Null, "the Camp scene has no CampBagScreenView");
            Assert.That(_bag.Model, Is.Not.Null, "the bag screen never received a model");

            _tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Assert.That(_tooltip, Is.Not.Null, "the Camp scene has no CampTooltipView");

            // Since Stage 09c the camp is a place the player walks and the panel starts closed, so
            // the panel is opened here the way a player opens it — by clicking จัดย่าม. The stage's
            // acceptance is that order ("เดิน → เช่า → เปิดหน้าจัดย่าม → ลากลง Board ผ่านจริง"), which
            // is why the click is part of the fixture rather than a test-only shortcut.
            Assert.That(_bag.PanelVisible, Is.False, "the จัดย่าม panel should start closed");

            // The panel starts closed, so the จัดย่าม button has only just been shown and its screen
            // rectangle is whatever the canvas layout last wrote. Read before the rebuild that
            // follows that activation, the rectangle can still be the previous frame's, and a click
            // aimed at it misses the button: this suite failed on exactly that once in four
            // back-to-back full runs (`clicking จัดย่าม did not open the panel`, 0.26 s in, while the
            // same test passes on its own and every run since). Forcing the pending rebuild makes
            // the aim deterministic instead of a matter of which side of the frame boundary the
            // coroutine step landed on. It relaxes no assertion below.
            Canvas.ForceUpdateCanvases();

            Assert.That(
                _bag.TryGetButtonScreenPoint(CampBagButton.Open, out Vector2 toggle), Is.True,
                "the จัดย่าม button is not on screen");

            yield return Click(toggle);

            Assert.That(_bag.PanelVisible, Is.True, "clicking จัดย่าม did not open the panel");
            Assert.That(_bag.BoardWidth, Is.GreaterThan(0), "the board never rendered its cells");
        }

        /// <summary>
        /// The drag a player actually performs: pick an item up and put it down on another bag cell.
        /// </summary>
        /// <remarks>
        /// The bag and the item start on the same cell (the item is on the bag), and an item is only
        /// ever legal on a cell a bag covers. The view picks the last piece in model order, which is
        /// the item — the piece drawn on top — so a hit test that returned the bag would fail here
        /// rather than silently dragging the wrong piece.
        /// </remarks>
        [UnityTest]
        public IEnumerator Dragging_an_item_moves_the_item_and_leaves_the_bag_alone()
        {
            yield return LoadCamp();

            CampPieceModel item = FirstPieceOfKind(PieceKind.Item);
            CampPieceModel bag = FirstPieceOfKind(PieceKind.Bag);
            CellOffset from = item.Origin;
            CellOffset to = FarthestFreeCellCoveredByABag(from);

            Assert.That(to.X, Is.GreaterThanOrEqualTo(0), "the fixture found no free bag cell to drag to");

            Vector2 fromPoint = CellCentre(from.X, from.Y);
            Vector2 toPoint = CellCentre(to.X, to.Y);
            Assert.That(
                Vector2.Distance(fromPoint, toPoint), Is.GreaterThan(EventSystem.current.pixelDragThreshold * 2f),
                "the two cells are too close on screen for the EventSystem's drag threshold");

            yield return Drag(fromPoint, toPoint);

            CampCounters counters = _director.Presenter.Counters;
            Assert.That(counters.DragPreviews, Is.GreaterThan(0), "no drag handler ran: the pointer never reached the view");
            Assert.That(_director.Presenter.IsDragging, Is.False, "the drag never ended");

            Assert.That(
                Piece(item.Piece).Origin.X, Is.EqualTo(to.X),
                "the item did not move to the cell it was dropped on");
            Assert.That(Piece(item.Piece).Origin.Y, Is.EqualTo(to.Y));
            Assert.That(
                Piece(bag.Piece).Origin, Is.EqualTo(bag.Origin),
                "the bag moved: the drag picked the bag up instead of the item under the pointer");
            Assert.That(counters.Placed, Is.GreaterThan(0), "no placement command ran, so nothing was dropped");
        }

        /// <summary>
        /// A drop the rules refuse must put the piece back where it was (Stage 09b, item D).
        /// </summary>
        /// <remarks>
        /// The gesture starts on a cell the bag covers and the item does not, so the piece under the
        /// pointer really is the bag: starting on the item's own cell would drag the item and the
        /// assertions below would pass without the bag ever being picked up. The destination is the far
        /// corner, where the bag's own footprint does not fit — the Domain refuses the whole assembly
        /// ("inventory.out_of_board"), and since Stage 09f that refusal covers the items riding on the
        /// bag too, so nothing may move: neither the bag nor the item standing on it.
        /// </remarks>
        [UnityTest]
        public IEnumerator A_refused_drop_leaves_the_piece_where_it_was()
        {
            yield return LoadCamp();

            CampPieceModel bag = FirstPieceOfKind(PieceKind.Bag);
            CampPieceModel carried = FirstPieceOfKind(PieceKind.Item);
            CellOffset from = BagOnlyCell(bag, out bool found);
            Assert.That(found, Is.True, "the fixture found no bag cell that holds nothing else to grab the bag by");

            CellOffset to = new CellOffset(_bag.BoardWidth - 1, _bag.BoardHeight - 1);

            yield return Drag(CellCentre(from.X, from.Y), CellCentre(to.X, to.Y));

            CampPresenter presenter = _director.Presenter;
            Assert.That(presenter.Counters.DragPreviews, Is.GreaterThan(0), "no drag handler ran");
            Assert.That(presenter.Counters.InvalidDrops, Is.GreaterThan(0), "a drop that should have been refused was not");
            Assert.That(
                Piece(bag.Piece).Origin, Is.EqualTo(bag.Origin),
                "a refused drop moved the piece anyway");
            Assert.That(
                Piece(carried.Piece).Origin, Is.EqualTo(carried.Origin),
                "a refused bag move carried the item on it anyway");
            Assert.That(presenter.StatusCode, Is.Not.Empty, "a refused drop said nothing to the player");
        }

        /// <summary>
        /// Stage 09f / ADR 0020: a bag dragged with the real pointer takes the items standing on it,
        /// and the ghost the screen draws mid-gesture is where they really land.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The gesture is deliberately read <b>before</b> the button is released: the ghost is a promise
        /// about the future, so the rectangle captured while the pointer is still down has to be the
        /// rectangle the item is drawn in once the drop has landed. That is the same claim as the item's
        /// own cell, checked through the screen instead of through the run — a view that drew its own
        /// guess would pass the first and fail this one.
        /// </para>
        /// <para>
        /// One item rides here, because the camp's starting kit is one bag with one weapon on it. The
        /// refusals a <i>set</i> of bags produces (an item lying across two of them) are covered where
        /// they can be set up exactly, in the EditMode and Core suites.
        /// </para>
        /// </remarks>
        [UnityTest]
        public IEnumerator Dragging_a_bag_carries_the_items_standing_on_it()
        {
            yield return LoadCamp();

            CampPieceModel bag = FirstPieceOfKind(PieceKind.Bag);
            CampPieceModel item = FirstPieceOfKind(PieceKind.Item);

            CellOffset from = BagOnlyCell(bag, out bool found);
            Assert.That(found, Is.True, "the fixture found no bag cell that holds nothing else to grab the bag by");

            CellOffset to = FarthestFreeBagCell(bag, out bool fits);
            Assert.That(fits, Is.True, "the fixture found nowhere the bag fits whole");

            Vector2 fromPoint = CellCentre(from.X, from.Y);
            Vector2 toPoint = CellCentre(to.X, to.Y);
            Assert.That(
                Vector2.Distance(fromPoint, toPoint), Is.GreaterThan(EventSystem.current.pixelDragThreshold * 2f),
                "the two cells are too close on screen for the EventSystem's drag threshold");

            yield return DragWithoutReleasing(fromPoint, toPoint);

            CampPresenter presenter = _director.Presenter;
            Assert.That(presenter.Counters.DragPreviews, Is.GreaterThan(0), "no drag handler ran: the pointer never reached the view");
            Assert.That(presenter.IsDragging, Is.True, "the gesture ended before it could be read");
            Assert.That(
                presenter.Drag.CarryCount, Is.EqualTo(1),
                "the item standing on the bag is not riding with it");
            Assert.That(
                _bag.VisibleCarryGhostCount, Is.EqualTo(1),
                "the rider has no ghost on screen");

            Assert.That(
                _bag.TryGetCarryGhostScreenRect(item.Piece, out Rect ghost), Is.True,
                "no ghost was drawn for the item that rides");

            // Where the item has to end up: the bag's own move, applied to the item's cell (a pure move
            // is the zero-turn case of the assembly transform, so the cell keeps its offset).
            var landing = new CellOffset(
                to.X + item.Origin.X - bag.Origin.X, to.Y + item.Origin.Y - bag.Origin.Y);

            yield return ReleaseAt(toPoint);

            CampPieceModel movedBag = Piece(bag.Piece);
            CampPieceModel movedItem = Piece(item.Piece);

            Assert.That(movedBag.Origin, Is.EqualTo(to), "the bag did not land on the cell it was dropped on");
            Assert.That(movedItem.Origin, Is.EqualTo(landing), "the item did not ride with the bag");
            Assert.That(movedItem.IsOnBoard, Is.True, "the item was left off the board");
            Assert.That(Covers(movedBag, movedItem.Origin), Is.True, "the item landed off the bag that carried it");
            Assert.That(presenter.Counters.Placed, Is.GreaterThan(0), "no placement command ran, so nothing was dropped");
            Assert.That(_bag.VisibleCarryGhostCount, Is.Zero, "the ghosts stayed on screen after the drop");

            Assert.That(
                _bag.TryGetPieceScreenRect(item.Piece, out Rect drawn), Is.True,
                "the item is not drawn after the drop");

            Assert.That(drawn.xMin, Is.EqualTo(ghost.xMin).Within(0.5f), "the ghost was not where the item landed");
            Assert.That(drawn.yMin, Is.EqualTo(ghost.yMin).Within(0.5f), "the ghost was not where the item landed");
            Assert.That(drawn.width, Is.EqualTo(ghost.width).Within(0.5f));
            Assert.That(drawn.height, Is.EqualTo(ghost.height).Within(0.5f));
        }

        /// <summary>
        /// Stage 09b, item E: the pointer-free backup — click a piece, then click where it goes.
        /// </summary>
        [UnityTest]
        public IEnumerator Clicking_a_piece_then_a_cell_moves_it()
        {
            yield return LoadCamp();

            CampPieceModel item = FirstPieceOfKind(PieceKind.Item);
            CellOffset from = item.Origin;
            CellOffset to = FarthestFreeCellCoveredByABag(from);

            Assert.That(to.X, Is.GreaterThanOrEqualTo(0), "the fixture found no free bag cell to click");

            yield return Click(CellCentre(from.X, from.Y));
            Assert.That(_bag.SelectedPiece, Is.EqualTo(item.Piece), "clicking a piece did not select it");

            yield return Click(CellCentre(to.X, to.Y));

            Assert.That(
                Piece(item.Piece).Origin.X, Is.EqualTo(to.X),
                "the second click did not place the selected piece");
            Assert.That(Piece(item.Piece).Origin.Y, Is.EqualTo(to.Y));
        }

        /// <summary>
        /// ล็อกย่ามตอนคืนสภาพ through the real widgets: an unchecked box restores without locking, a
        /// checked one restores and locks.
        /// </summary>
        /// <remarks>
        /// The control was authored in Stage 09 as a caption that read like a checkbox and was wired
        /// to nothing; this is the test that says it is a control now.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_lock_on_restore_checkbox_locks_the_bag_with_the_restore()
        {
            yield return LoadCamp();

            CampPresenter presenter = _director.Presenter;
            RunSession run = _director.Run;

            Assert.That(run.IsBagLocked, Is.False, "the run starts with the bag locked");

            yield return Click(ControlCentre(CampBagButton.Reset, "restore"));

            Assert.That(presenter.Counters.Resets, Is.EqualTo(1), "the restore button did not restore");
            Assert.That(run.IsBagLocked, Is.False, "an unchecked checkbox locked the bag");

            yield return Click(ControlCentre(CampBagButton.LockRestore, "the lock-on-restore checkbox"));
            yield return Click(ControlCentre(CampBagButton.Reset, "restore"));

            Assert.That(presenter.Counters.Resets, Is.EqualTo(2), "the second restore did not restore");
            Assert.That(run.IsBagLocked, Is.True, "the checkbox did not lock the bag together with the restore");
        }

        /// <summary>
        /// Stage 09b, item B: no two named regions of the panel are on top of each other, and Stage
        /// 09d, item 2: no label is smaller than 22 px.
        /// </summary>
        /// <remarks>
        /// Measured from the same rectangles the layout report publishes to the page, so the browser
        /// test and this one check the same statement at the same resolution.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_panel_layout_has_no_overlapping_regions_and_no_tiny_labels()
        {
            yield return LoadCamp();

            var regions = new List<(string Name, Rect Rect, bool IsSurface)>();

            foreach (CampBagSurface surface in new[]
                     {
                         CampBagSurface.Board, CampBagSurface.ChestPanel, CampBagSurface.Chest,
                         CampBagSurface.Altar, CampBagSurface.Instructions, CampBagSurface.Stats,
                     })
            {
                if (_bag.TryGetSurfaceScreenRect(surface, out Rect rect))
                {
                    regions.Add((surface.ToString(), rect, true));
                }
            }

            foreach (CampBagButton button in new[]
                     {
                         CampBagButton.Rotate, CampBagButton.Undo, CampBagButton.Reset, CampBagButton.Lock,
                         CampBagButton.LockRestore, CampBagButton.NextFloor, CampBagButton.Close,
                     })
            {
                if (_bag.TryGetButtonScreenRect(button, out Rect rect))
                {
                    regions.Add((button.ToString(), rect, false));
                }
            }

            Assert.That(regions.Count, Is.GreaterThanOrEqualTo(12), "too few regions came back to be checking the layout");

            for (int i = 0; i < regions.Count; i++)
            {
                Assert.That(regions[i].Rect.width, Is.GreaterThan(0f), regions[i].Name + " has no width");
                Assert.That(regions[i].Rect.height, Is.GreaterThan(0f), regions[i].Name + " has no height");
            }

            for (int i = 0; i < regions.Count; i++)
            {
                for (int j = i + 1; j < regions.Count; j++)
                {
                    Assert.That(
                        IsAllowed(regions[i], regions[j]), Is.True,
                        regions[i].Name + " " + regions[i].Rect + " overlaps "
                        + regions[j].Name + " " + regions[j].Rect);
                }
            }

            TMP_Text[] labels = _bag.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(labels.Length, Is.GreaterThan(0), "the panel has no labels at all");

            foreach (TMP_Text label in labels)
            {
                // Stage 09i: the name inside a piece's own cell autosizes to the rectangle it may not
                // leave, so its floor is the 14 px the stage allows rather than the panel's 22 px.
                // That it really does fit is `CampSynergyLinePlayModeTests`' name test, which measures
                // the laid-out text bounds rather than the setting.
                if (label.enableAutoSizing)
                {
                    Assert.That(
                        label.fontSizeMin, Is.GreaterThanOrEqualTo(CellLabelFloor),
                        "'" + label.name + "' shrinks to " + label.fontSizeMin
                        + " px, below the " + CellLabelFloor + " px floor");
                    Assert.That(
                        label.fontSizeMax, Is.LessThanOrEqualTo(MinimumFontSize),
                        "'" + label.name + "' grows to " + label.fontSizeMax
                        + " px, above the " + MinimumFontSize + " px ceiling");
                    continue;
                }

                Assert.That(
                    label.fontSize, Is.GreaterThanOrEqualTo(MinimumFontSize),
                    "'" + label.name + "' is " + label.fontSize + " px, below the " + MinimumFontSize + " px floor");
            }
        }

        // ------------------------------------------------------------------
        // Stage 09d: the tooltip, the names in the cells, and the combo lines
        // ------------------------------------------------------------------

        /// <summary>
        /// Stage 09d, item 2: the tooltip is drawn on a canvas above the whole camp screen, the
        /// rectangle it occupies really does sit over the board, nothing in it can be clicked, and it
        /// follows the pointer into a corner without leaving the screen.
        /// </summary>
        /// <remarks>
        /// The pointer drives it, as it does for a player: the tooltip appears from a hover over a
        /// piece and is placed by <c>CampTooltipView.Update</c> from the real mouse position. The
        /// draw order is the statement that matters — a tooltip on the camp's own canvas would be
        /// under the board's cells, since both are children of one canvas and the cells are created
        /// at runtime, after everything authored.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_tooltip_is_drawn_on_a_canvas_above_the_board_and_stays_on_screen()
        {
            yield return LoadCamp();

            CampPieceModel item = FirstPieceOfKind(PieceKind.Item);

            Set(_mouse.position, CellCentre(item.Origin.X, item.Origin.Y));
            yield return null;
            yield return null;

            Assert.That(_tooltip.Visible, Is.True, "hovering a piece did not show the tooltip");

            // The content is one line per fact, in the stage's order, and a line with nothing to say
            // is hidden rather than drawn empty: the tooltip's shape then says which of the six lines
            // this piece actually has (an item with no คำอธิบาย in its own table has five, and a
            // piece with no merge behind it has no variant line either).
            string[] lines =
            {
                "TooltipTitle", "TooltipTier", "TooltipStatus", "TooltipUpgrades",
                "TooltipDescription", "TooltipCombos",
            };
            var labels = new TMP_Text[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                labels[i] = LabelNamed(_tooltip, lines[i]);

                Assert.That(
                    labels[i].gameObject.activeSelf, Is.EqualTo(!string.IsNullOrEmpty(labels[i].text)),
                    "'" + lines[i] + "' is " + (labels[i].gameObject.activeSelf ? "shown" : "hidden")
                    + " with text '" + labels[i].text + "'");

                for (int j = 0; j < i; j++)
                {
                    Assert.That(
                        labels[i].gameObject, Is.Not.EqualTo(labels[j].gameObject),
                        "'" + lines[i] + "' and '" + lines[j] + "' are the same label: the lines are not separate");
                }
            }

            Assert.That(
                labels[0].text, Is.EqualTo(item.Name),
                "the tooltip's first line is not the hovered piece's own name");
            Assert.That(labels[1].text, Is.Not.Empty, "the tooltip has no ระดับ line for an item that has a tier");
            Assert.That(labels[2].text, Is.Not.Empty, "the tooltip has no สถานะ line for an item");

            Canvas tooltipCanvas = _tooltip.RootCanvas;
            Canvas screenCanvas = _bag.GetComponentInParent<Canvas>();
            Assert.That(tooltipCanvas, Is.Not.Null, "the tooltip never resolved its canvas");
            Assert.That(screenCanvas, Is.Not.Null, "the จัดย่าม panel is not under a canvas");
            Assert.That(
                tooltipCanvas.rootCanvas, Is.Not.EqualTo(screenCanvas.rootCanvas),
                "the tooltip shares the camp's canvas: it would be drawn in creation order, under the "
                + "board's cells, which are built at runtime");
            Assert.That(
                tooltipCanvas.rootCanvas.sortingOrder, Is.GreaterThan(screenCanvas.rootCanvas.sortingOrder),
                "the tooltip's canvas does not sort above the camp's");

            Assert.That(_bag.TryGetSurfaceScreenRect(CampBagSurface.Board, out Rect board), Is.True, "the board is not on screen");
            Assert.That(_tooltip.TryGetScreenRect(out Rect tooltip), Is.True, "the tooltip has no screen rectangle");
            Assert.That(
                Overlaps(board, tooltip), Is.True,
                "the tooltip does not reach the board at all, so its draw order proves nothing: tooltip "
                + tooltip + " against board " + board);

            Assert.That(
                tooltipCanvas.rootCanvas.GetComponent<GraphicRaycaster>(), Is.Null,
                "the tooltip's canvas carries a GraphicRaycaster: it can be clicked, and a tooltip that "
                + "follows the pointer must not eat the pointer's own events");

            var background = _tooltip.GetComponent<Image>();
            Assert.That(background, Is.Not.Null, "the tooltip has no background of its own");
            Assert.That(background.raycastTarget, Is.False, "the tooltip's background is a raycast target");
            Assert.That(
                background.color.a, Is.GreaterThanOrEqualTo(TooltipAlphaFloor),
                "the tooltip's background is alpha " + background.color.a + ", below the " + TooltipAlphaFloor + " floor");

            // The corners: the placement rule flips the panel rather than letting it hang half off the
            // canvas, which is the difference between a readable tooltip and a clipped one.
            float width = Screen.width;
            float height = Screen.height;
            var corners = new[]
            {
                new Vector2(1f, 1f),
                new Vector2(width - 1f, 1f),
                new Vector2(1f, height - 1f),
                new Vector2(width - 1f, height - 1f),
                new Vector2(width * 0.5f, height * 0.5f),
            };

            var screen = new Rect(0f, 0f, width, height);
            for (int i = 0; i < corners.Length; i++)
            {
                _tooltip.PlaceAt(corners[i]);
                Canvas.ForceUpdateCanvases();

                Assert.That(
                    _tooltip.TryGetScreenRect(out Rect placed), Is.True,
                    "the tooltip has no rectangle after being placed at " + corners[i]);
                Assert.That(
                    placed.xMin >= -0.5f && placed.yMin >= -0.5f
                    && placed.xMax <= screen.xMax + 0.5f && placed.yMax <= screen.yMax + 0.5f,
                    "placed at " + corners[i] + " the tooltip runs off the screen: " + placed);
            }
        }

        /// <summary>
        /// Stage 09d, item 1: no two piece names on the board share pixels — an item's name is the one
        /// that keeps its place, and a รยาม's name is the secondary text that gives way to it.
        /// </summary>
        /// <remarks>
        /// The names are read as rectangles, the same way the layout is: a name is a label the view
        /// shows or hides, so "they do not overlap" is a statement about two rectangles rather than
        /// about a screenshot. The check runs before and after the item is dragged off the bag, which
        /// is the case where the bag's own name has room again.
        /// </remarks>
        [UnityTest]
        public IEnumerator An_items_name_keeps_its_place_over_the_bag_under_it_and_no_two_names_overlap()
        {
            yield return LoadCamp();

            CampPieceModel item = FirstPieceOfKind(PieceKind.Item);
            CellOffset to = FarthestFreeCellCoveredByABag(item.Origin);

            Assert.That(to.X, Is.GreaterThanOrEqualTo(0), "the fixture found no free bag cell to move the item to");

            Assert.That(
                TryFindBagUnder(item, out CampPieceModel bag), Is.True,
                "the starting kit does not put an item on a รยาม, so there is no pair of names to check");

            Assert.That(
                _bag.TryGetPieceLabelScreenRect(item.Piece, out Rect itemName), Is.True,
                "the item on the board has no name on screen");
            // Either the bag's name is hidden under the item or it is drawn somewhere the item's name
            // is not: an item's name is the primary text of the cell.
            bool bagNameDrawn = _bag.TryGetPieceLabelScreenRect(bag.Piece, out Rect bagName);

            Assert.That(
                !bagNameDrawn || !Overlaps(itemName, bagName), Is.True,
                "the รยาม's name is drawn over the item's name: " + bagName + " against " + itemName);

            AssertNoTwoNamesOverlap();

            yield return Drag(CellCentre(item.Origin.X, item.Origin.Y), CellCentre(to.X, to.Y));

            Assert.That(Piece(item.Piece).Origin.X, Is.EqualTo(to.X), "the item did not move off the bag's name");
            AssertNoTwoNamesOverlap();
        }

        /// <summary>
        /// Stage 09d, item 3: the view draws <b>exactly</b> the marks the run's own evaluation listed —
        /// a line for every pair that shares a board line, never for a bag — and nothing for the
        /// adjacency rules, which are shown by the hover highlight instead.
        /// </summary>
        /// <remarks>
        /// The pairs are Core's (<c>SynergyEvaluator.Active</c>); this test says the view rendered the
        /// classification and that no line has a bag at either end, which is the stage's "ห้ามวาดเส้นจาก
        /// รยามไปไอเทม" read from the picture's own counters. The deeper placement claim — a tome
        /// carried to a weapon's row draws a line, and leaving the row takes it away — is
        /// <c>CampPresenterTests</c>, where the run can be walked to the camp that offers the tome.
        /// </remarks>
        [UnityTest]
        public IEnumerator Only_a_pair_that_shares_a_board_line_is_drawn_as_a_line_and_never_a_bag()
        {
            yield return LoadCamp();

            CampLink[] links = _bag.Model.Links;
            int lines = 0;
            int badges = 0;

            for (int i = 0; i < links.Length; i++)
            {
                CampPieceModel source = PieceById(links[i].SourceInstanceId);
                CampPieceModel target = PieceById(links[i].TargetInstanceId);

                if (links[i].Style == CampLinkStyle.Line)
                {
                    lines++;

                    Assert.That(source.Kind, Is.EqualTo(PieceKind.Item), "a line was drawn from a รยาม: " + links[i]);
                    Assert.That(target.Kind, Is.EqualTo(PieceKind.Item), "a line was drawn to a รยาม: " + links[i]);
                    Assert.That(
                        SharesABoardLine(source, target), Is.True,
                        "a line was drawn between two pieces that share no board row or column: " + links[i]);
                }
                else
                {
                    badges++;

                    Assert.That(
                        source.Kind, Is.EqualTo(PieceKind.Bag),
                        "a badge is the area rule's mark, and only a รยาม can start it: " + links[i]);
                }
            }

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(lines),
                "the view drew " + _bag.VisibleLineCount + " line(s) for the " + lines + " the run listed");
            Assert.That(
                _bag.VisibleBadgeCount, Is.EqualTo(badges),
                "the view drew " + _bag.VisibleBadgeCount + " badge(s) for the " + badges + " the run listed");
        }

        /// <summary>
        /// Stage 09j: a permanent line needs the pair to <b>touch</b>, and the mark follows the weapon
        /// the moment it is carried off the tome's edge and back.
        /// </summary>
        /// <remarks>
        /// The layout is the stage's own: a 2×2 tome with one weapon against its right edge and a second
        /// weapon a cell further along the same row. Core reports both weapons; the screen draws one
        /// line. Dragging the touching weapon two rows down is a real gesture through the presenter, so
        /// the count dropping to zero is the picture following a command rather than a cache expiring.
        /// </remarks>
        [UnityTest]
        public IEnumerator Only_a_touching_pair_keeps_its_line_and_the_mark_follows_the_piece()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            PieceRef firstBag = run.Backpack.Bags[0].Ref;
            PieceRef touchingWeapon = run.Backpack.Items[0].Ref;

            Assert.That(
                run.MovePiece(firstBag, new CellOffset(0, 1), Rotation.Degrees0).IsSuccess, Is.True,
                "the starting รยาม could not be moved to (0,1)");

            var secondBag = run.Backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(secondBag.IsSuccess, Is.True, "a second รยาม could not be added");
            Assert.That(
                run.MovePiece(secondBag.Value, new CellOffset(3, 1), Rotation.Degrees0).IsSuccess, Is.True,
                "the second รยาม could not be placed at (3,1)");

            // The weapon that rode the bag has to leave the tome's cells before the tome lands on them.
            Assert.That(
                run.MovePiece(touchingWeapon, new CellOffset(2, 1), Rotation.Degrees0).IsSuccess, Is.True,
                "the touching weapon could not be placed at (2,1)");

            var tome = run.Backpack.AddItem("item.spell_tome");
            Assert.That(tome.IsSuccess, Is.True, "the tome could not be added");
            var farWeapon = run.Backpack.AddItem("item.kali_flame_yant");
            Assert.That(farWeapon.IsSuccess, Is.True, "a second weapon could not be added");

            Assert.That(
                run.MovePiece(tome.Value, new CellOffset(0, 1), Rotation.Degrees0).IsSuccess, Is.True,
                "the 2x2 tome could not be placed at (0,1)");
            Assert.That(
                run.MovePiece(farWeapon.Value, new CellOffset(3, 1), Rotation.Degrees0).IsSuccess, Is.True,
                "the far weapon could not be placed at (3,1)");

            presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(1),
                "one touching pair should be one line, and the view drew " + _bag.VisibleLineCount);

            // Off the tome's row, with the dock's own gesture.
            presenter.DragStart(touchingWeapon, Rotation.Degrees0);
            presenter.DragMove(BagDropZone.Board, new CellOffset(2, 3));
            presenter.Drop();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(0),
                "the line survived the weapon being carried off the tome's row: the view drew "
                + _bag.VisibleLineCount);

            // And back against the tome's edge: the mark returns with the pair.
            presenter.DragStart(touchingWeapon, Rotation.Degrees0);
            presenter.DragMove(BagDropZone.Board, new CellOffset(2, 1));
            presenter.Drop();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(1),
                "the line did not come back when the weapon returned to the tome's edge: the view drew "
                + _bag.VisibleLineCount);

            // The mark it came back as still reaches the two pieces and no name.
            Assert.That(_bag.TryGetLineScreenEnds(0, out Vector2 from, out Vector2 to), Is.True);
            Assert.That(
                _bag.TryGetPieceScreenRect(tome.Value, out Rect tomeRect), Is.True,
                "the tome has no rectangle on screen");
            Assert.That(
                _bag.TryGetPieceScreenRect(touchingWeapon, out Rect weaponRect), Is.True,
                "the weapon has no rectangle on screen");

            Assert.That(
                tomeRect.Contains(from) || EdgesWithin(from, tomeRect, 1f), Is.True,
                "the line does not start on the tome: " + from + " against " + tomeRect);
            Assert.That(
                weaponRect.Contains(to) || EdgesWithin(to, weaponRect, 1f), Is.True,
                "the line does not end on the weapon: " + to + " against " + weaponRect);

            for (int i = 0; i < _bag.VisibleLineCount; i++)
            {
                Assert.That(_bag.TryGetLineScreenEnds(i, out Vector2 start, out Vector2 finish), Is.True);

                foreach (TMP_Text label in _bag.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.name != CampBagScreenView.CellLabelName || !label.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (!CampScreenRects.TryOf((RectTransform)label.transform, out Rect text))
                    {
                        continue;
                    }

                    Assert.That(
                        Crosses(start, finish, text), Is.False,
                        "line " + i + " runs through the name '" + label.text + "' at " + text);
                }
            }
        }

        /// <summary>True when the point is on the rectangle's edge, within a tolerance.</summary>
        private static bool EdgesWithin(Vector2 point, Rect rect, float tolerance)
        {
            float distance = Mathf.Min(
                Mathf.Min(Mathf.Abs(point.x - rect.xMin), Mathf.Abs(point.x - rect.xMax)),
                Mathf.Min(Mathf.Abs(point.y - rect.yMin), Mathf.Abs(point.y - rect.yMax)));
            return distance <= tolerance;
        }

        /// <summary>
        /// True when the stroke runs through the name's inside — not merely touching its outline
        /// (both ends of a mark sit on a piece's edge, and a name fills its piece).
        /// </summary>
        private static bool Crosses(Vector2 from, Vector2 to, Rect rect)
        {
            const int Steps = 24;
            const float Inset = 1.5f;

            var inside = new Rect(
                rect.x + Inset, rect.y + Inset, rect.width - (Inset * 2f), rect.height - (Inset * 2f));

            if (inside.width <= 0f || inside.height <= 0f)
            {
                return false;
            }

            for (int i = 1; i < Steps; i++)
            {
                if (inside.Contains(Vector2.Lerp(from, to, i / (float)Steps)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>One of the tooltip's five lines, found by the name the scene authored it under.</summary>
        private static TMP_Text LabelNamed(Component owner, string name)
        {
            TMP_Text[] labels = owner.GetComponentsInChildren<TMP_Text>(true);
            TMP_Text found = null;
            int matches = 0;

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].name == name)
                {
                    found = labels[i];
                    matches++;
                }
            }

            Assert.That(matches, Is.EqualTo(1), "the tooltip has " + matches + " '" + name + "' labels, not one");
            return found;
        }

        /// <summary>Every name the board draws, as rectangles, and none of them sharing pixels.</summary>
        private void AssertNoTwoNamesOverlap()
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            var names = new List<(string Name, Rect Rect)>();

            for (int i = 0; i < pieces.Length; i++)
            {
                if (_bag.TryGetPieceLabelScreenRect(pieces[i].Piece, out Rect rect))
                {
                    names.Add((pieces[i].Name, rect));
                }
            }

            Assert.That(names.Count, Is.GreaterThan(0), "the board draws no piece name at all");

            for (int i = 0; i < names.Count; i++)
            {
                for (int j = i + 1; j < names.Count; j++)
                {
                    Assert.That(
                        Overlaps(names[i].Rect, names[j].Rect), Is.False,
                        "'" + names[i].Name + "' " + names[i].Rect + " is drawn over '" + names[j].Name
                        + "' " + names[j].Rect);
                }
            }
        }

        /// <summary>The bag a piece currently sits on, which is the pair the name rule is about.</summary>
        private bool TryFindBagUnder(CampPieceModel item, out CampPieceModel bag)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].IsBag && pieces[i].IsOnBoard && Covers(pieces[i], item.Origin))
                {
                    bag = pieces[i];
                    return true;
                }
            }

            bag = default;
            return false;
        }

        private CampPieceModel PieceById(int instanceId)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Piece.InstanceId == instanceId)
                {
                    return pieces[i];
                }
            }

            Assert.Fail("the run holds no piece " + instanceId);
            return default;
        }

        /// <summary>
        /// True when two pieces occupy at least one common board row or column — the condition the
        /// same-line rule matches on, computed from the footprints the model carries.
        /// </summary>
        /// <remarks>
        /// A test is not allowed to name the rule's tags (<c>tome</c>, <c>weapon</c>): the data is the
        /// source of those, and a rule the data retunes must not make this test stale. What is fixed is
        /// the geometry the rule reads.
        /// </remarks>
        private static bool SharesABoardLine(CampPieceModel a, CampPieceModel b)
        {
            Cells(a, out int aRowMin, out int aRowMax, out int aColumnMin, out int aColumnMax);
            Cells(b, out int bRowMin, out int bRowMax, out int bColumnMin, out int bColumnMax);

            bool sameRow = aRowMin <= bRowMax && bRowMin <= aRowMax;
            bool sameColumn = aColumnMin <= bColumnMax && bColumnMin <= aColumnMax;
            return sameRow || sameColumn;
        }

        /// <summary>The board rows and columns a piece's footprint covers.</summary>
        private static void Cells(
            CampPieceModel piece, out int rowMin, out int rowMax, out int columnMin, out int columnMax)
        {
            rowMin = int.MaxValue;
            rowMax = int.MinValue;
            columnMin = int.MaxValue;
            columnMax = int.MinValue;

            Footprint shape = piece.Shape;
            if (shape == null)
            {
                return;
            }

            for (int y = 0; y < shape.Height; y++)
            {
                for (int x = 0; x < shape.Width; x++)
                {
                    if (!shape.Contains(new CellOffset(x, y)))
                    {
                        continue;
                    }

                    int row = piece.Origin.Y + y;
                    int column = piece.Origin.X + x;

                    if (row < rowMin) { rowMin = row; }
                    if (row > rowMax) { rowMax = row; }
                    if (column < columnMin) { columnMin = column; }
                    if (column > columnMax) { columnMax = column; }
                }
            }
        }

        /// <summary>
        /// The layout contract the browser test depends on: the point the screen publishes for a cell
        /// must resolve back to <b>that</b> cell.
        /// </summary>
        /// <remarks>
        /// A published point that resolves to a neighbour (or to nothing at all) makes a browser test
        /// drop a piece one cell off while reporting a pass, so the round trip is asserted here where
        /// the failure is readable. This is the Stage 09b defect in its original form: the published
        /// point was each cell's top-left <i>corner</i>, and a corner is outside the cell by both the
        /// rectangle test and the cell arithmetic.
        /// </remarks>
        [UnityTest]
        public IEnumerator Every_published_board_cell_point_resolves_back_to_its_own_cell()
        {
            yield return LoadCamp();

            for (int row = 0; row < _bag.BoardHeight; row++)
            {
                for (int column = 0; column < _bag.BoardWidth; column++)
                {
                    Assert.That(_bag.TryGetBoardCellScreenPoint(column, row, out Vector2 point), Is.True, "no point for " + column + "," + row);
                    Assert.That(_bag.TryLocate(point, out BagDropZone zone, out CellOffset cell), Is.True, "(" + column + "," + row + ") resolved to no zone");
                    Assert.That(zone, Is.EqualTo(BagDropZone.Board), "(" + column + "," + row + ") is not on the board");
                    Assert.That(
                        cell.X == column && cell.Y == row, Is.True,
                        "the published point for (" + column + "," + row + ") resolves to (" + cell.X + "," + cell.Y + ")");
                }
            }
        }

        /// <summary>The same round trip for the chest, which is where a piece starts or ends up.</summary>
        [UnityTest]
        public IEnumerator Every_published_chest_slot_point_resolves_back_to_its_own_slot()
        {
            yield return LoadCamp();

            for (int slot = 0; slot < _bag.ChestSlotCount; slot++)
            {
                Assert.That(_bag.TryGetChestSlotScreenPoint(slot, out Vector2 point), Is.True, "no point for chest slot " + slot);
                Assert.That(_bag.TryLocate(point, out BagDropZone zone, out CellOffset cell), Is.True, "chest slot " + slot + " resolved to no zone");
                Assert.That(zone, Is.EqualTo(BagDropZone.Chest), "chest slot " + slot + " is not in the chest");
            }
        }

        /// <summary>The altar has to be reachable by the pointer too: it is where a piece is given back.</summary>
        [UnityTest]
        public IEnumerator The_published_altar_point_lands_inside_the_altar()
        {
            yield return LoadCamp();

            Assert.That(_bag.TryGetAltarScreenPoint(out Vector2 altar), Is.True, "the altar is not on screen");
            Assert.That(
                _bag.TryLocate(altar, out BagDropZone zone, out _), Is.True,
                "the published altar centre resolves to nothing");
            Assert.That(zone, Is.EqualTo(BagDropZone.Altar), "the published altar centre is not the altar");
        }

        private static bool Overlaps(Rect a, Rect b) =>
            a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        /// <summary>True when <paramref name="outer"/> encloses <paramref name="inner"/> completely.</summary>
        private static bool Contains(Rect outer, Rect inner) =>
            outer.xMin <= inner.xMin && outer.yMin <= inner.yMin
            && outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;

        /// <summary>
        /// Whether two named regions may share space.
        /// </summary>
        /// <remarks>
        /// A control drawn inside its own surface is by design: ไปชั้นถัดไป and กลับ live in the
        /// bottom-left block, and the chest's slot grid lives in the chest panel. Everything else has
        /// to be disjoint — including two controls, where a button drawn inside another button would
        /// be a defect rather than nesting.
        /// </remarks>
        private static bool IsAllowed((string Name, Rect Rect, bool IsSurface) a, (string Name, Rect Rect, bool IsSurface) b)
        {
            if (!Overlaps(a.Rect, b.Rect))
            {
                return true;
            }

            if (a.IsSurface && b.IsSurface)
            {
                for (int i = 0; i < NestedByDesign.Length; i++)
                {
                    if (Matches(a.Name, b.Name, NestedByDesign[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (a.IsSurface || b.IsSurface)
            {
                return Contains(a.Rect, b.Rect) || Contains(b.Rect, a.Rect);
            }

            return false;
        }

        private static bool Matches(string first, string second, (CampBagSurface Outer, CampBagSurface Inner) pair)
        {
            string outer = pair.Outer.ToString();
            string inner = pair.Inner.ToString();
            return (first == outer && second == inner) || (first == inner && second == outer);
        }

        // ------------------------------------------------------------------
        // pointer helpers
        // ------------------------------------------------------------------

        /// <summary>Press, move across the cells, release — one frame per step.</summary>
        private IEnumerator Drag(Vector2 from, Vector2 to)
        {
            yield return DragWithoutReleasing(from, to);
            yield return ReleaseAt(to);
        }

        /// <summary>
        /// Press and move across the cells, <b>leaving the button down</b>, so a gesture can be read while
        /// it is still in flight.
        /// </summary>
        private IEnumerator DragWithoutReleasing(Vector2 from, Vector2 to)
        {
            Set(_mouse.position, from);
            Press(_mouse.leftButton);
            yield return null;
            yield return null;

            for (int step = 1; step <= DragSteps; step++)
            {
                Set(_mouse.position, Vector2.Lerp(from, to, step / (float)DragSteps));
                yield return null;
            }

            Set(_mouse.position, to);
            yield return null;
        }

        /// <summary>Releases the button where the pointer already is.</summary>
        private IEnumerator ReleaseAt(Vector2 point)
        {
            Set(_mouse.position, point);
            Release(_mouse.leftButton);
            yield return null;
            yield return null;
        }

        /// <summary>A full press-and-release on one point.</summary>
        private IEnumerator Click(Vector2 point)
        {
            Set(_mouse.position, point);
            yield return null;

            Press(_mouse.leftButton);
            yield return null;

            Release(_mouse.leftButton);
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------
        // model helpers
        // ------------------------------------------------------------------

        private CampPieceModel Piece(PieceRef piece)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Piece == piece)
                {
                    return pieces[i];
                }
            }

            Assert.Fail("the run no longer holds " + piece);
            return default;
        }

        private CampPieceModel FirstPieceOfKind(PieceKind kind)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Kind == kind && pieces[i].IsOnBoard)
                {
                    return pieces[i];
                }
            }

            Assert.Fail("the run holds no " + kind + " on the board to drag");
            return default;
        }

        /// <summary>
        /// A cell the bag covers that holds nothing else, which is where a pointer grabs the bag.
        /// </summary>
        private CellOffset BagOnlyCell(CampPieceModel bag, out bool found)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            Vector2 bagCentre = CellCentre(bag.Origin.X, bag.Origin.Y);

            CellOffset best = new CellOffset(-1, -1);
            float bestDistance = -1f;
            found = false;

            for (int row = 0; row < _bag.BoardHeight; row++)
            {
                for (int column = 0; column < _bag.BoardWidth; column++)
                {
                    var cell = new CellOffset(column, row);

                    if (!Covers(bag, cell) || OccupiedByAnItem(pieces, cell))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(bagCentre, CellCentre(column, row));
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        best = cell;
                        found = true;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The cell furthest from the bag's own where the bag fits whole on the board, which is where a
        /// player would drag it to; false when there is no such cell.
        /// </summary>
        private CellOffset FarthestFreeBagCell(CampPieceModel bag, out bool found)
        {
            int width = bag.Shape == null ? 1 : bag.Shape.Width;
            int height = bag.Shape == null ? 1 : bag.Shape.Height;
            Vector2 from = CellCentre(bag.Origin.X, bag.Origin.Y);

            CellOffset best = new CellOffset(-1, -1);
            float bestDistance = -1f;
            found = false;

            for (int row = 0; row + height <= _bag.BoardHeight; row++)
            {
                for (int column = 0; column + width <= _bag.BoardWidth; column++)
                {
                    if (column == bag.Origin.X && row == bag.Origin.Y)
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(from, CellCentre(column, row));
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        best = new CellOffset(column, row);
                        found = true;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// The board cell an item can legally be dropped on that is furthest from where it is: empty,
        /// inside the board, and covered by a bag.
        /// </summary>
        private CellOffset FarthestFreeCellCoveredByABag(CellOffset from)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            CellOffset best = new CellOffset(-1, -1);
            float bestDistance = -1f;
            Vector2 origin = CellCentre(from.X, from.Y);

            for (int row = 0; row < _bag.BoardHeight; row++)
            {
                for (int column = 0; column < _bag.BoardWidth; column++)
                {
                    var cell = new CellOffset(column, row);

                    // A bag cell is a legal place for an item exactly when no other item is on it;
                    // the bag covering it is what makes it legal in the first place.
                    if (OccupiedByAnItem(pieces, cell) || !CoveredByABag(pieces, cell))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(origin, CellCentre(column, row));
                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        best = cell;
                    }
                }
            }

            return best;
        }

        private static bool OccupiedByAnItem(CampPieceModel[] pieces, CellOffset cell)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Kind == PieceKind.Item && pieces[i].IsOnBoard && Covers(pieces[i], cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CoveredByABag(CampPieceModel[] pieces, CellOffset cell)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].IsBag && pieces[i].IsOnBoard && Covers(pieces[i], cell))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Covers(CampPieceModel piece, CellOffset cell)
        {
            if (piece.Shape == null)
            {
                return false;
            }

            var local = new CellOffset(cell.X - piece.Origin.X, cell.Y - piece.Origin.Y);
            return piece.Shape.Contains(local);
        }

        /// <summary>The centre of a board cell in screen pixels, which is where a player aims.</summary>
        private Vector2 CellCentre(int column, int row)
        {
            Assert.That(
                _bag.TryGetBoardCellScreenPoint(column, row, out Vector2 point), Is.True,
                "the board cell (" + column + "," + row + ") is not on screen");
            return point;
        }

        /// <summary>The centre of a panel control, which is where a player clicks.</summary>
        private Vector2 ControlCentre(CampBagButton button, string what)
        {
            Assert.That(
                _bag.TryGetButtonScreenPoint(button, out Vector2 point), Is.True,
                "the " + what + " control is not on screen");
            return point;
        }
    }
}
