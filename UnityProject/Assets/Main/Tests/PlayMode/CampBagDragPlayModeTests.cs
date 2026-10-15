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
        /// Regions that are allowed to overlap because one is drawn inside the other. The chest's
        /// slot grid is a child of the chest panel; everything else has to be disjoint.
        /// </summary>
        private static readonly (CampBagSurface Outer, CampBagSurface Inner)[] NestedByDesign =
        {
            (CampBagSurface.ChestPanel, CampBagSurface.Chest),
        };

        private Mouse _mouse;
        private CampSceneDirector _director;
        private CampBagScreenView _bag;

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
        /// assertions below would pass without the bag ever being picked up. The destination is an
        /// empty corner far from the item, where a bag would leave that item stranded — the Domain
        /// refuses it ("inventory.item_stranded").
        /// </remarks>
        [UnityTest]
        public IEnumerator A_refused_drop_leaves_the_piece_where_it_was()
        {
            yield return LoadCamp();

            CampPieceModel bag = FirstPieceOfKind(PieceKind.Bag);
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
            Assert.That(presenter.StatusCode, Is.Not.Empty, "a refused drop said nothing to the player");
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
                Assert.That(
                    label.fontSize, Is.GreaterThanOrEqualTo(MinimumFontSize),
                    "'" + label.name + "' is " + label.fontSize + " px, below the " + MinimumFontSize + " px floor");
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
