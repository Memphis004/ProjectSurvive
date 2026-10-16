using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 09g: the upgrade chooser — the 1-from-N decision a merge of two equal pieces asks for —
    /// driven by <b>real pointer and keyboard events</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gesture that opens the window is a real drag through the scene's own
    /// <c>InputSystemUIInputModule</c> and the canvas's <c>GraphicRaycaster</c>; the choice is a real
    /// click on a card, or a real key press. Nothing in these tests calls
    /// <c>ConfirmMergeChoice</c>, <c>CancelMergeChooser</c> or <c>Drop</c> and then claims to have
    /// tested the screen: a use case called directly can never see a dim layer that does not block
    /// the board, a card that is not where the pointer aims, or a key that was never wired.
    /// </para>
    /// <para>
    /// The fixture builds the <i>state</i> (a second copy of the starting weapon, placed on a free
    /// bag cell) through the run, then hands the merge itself to the pointer. Loading the scene in the
    /// test body rather than in <c>[UnitySetUp]</c> is deliberate and is the same measured finding the
    /// drag suite records in <c>docs/stages/09b-report.md</c>: a scene loaded from <c>[UnitySetUp]</c>
    /// has its input module torn out from under it by <c>InputTestFixture.Setup</c> a frame later, and
    /// no pointer event is ever delivered.
    /// </para>
    /// </remarks>
    public sealed class CampMergeChooserPlayModeTests : InputTestFixture
    {
        private const string CampSceneName = "Camp";

        /// <summary>Intermediate pointer positions between press and release.</summary>
        private const int DragSteps = 6;

        private Mouse _mouse;
        private Keyboard _keyboard;
        private CampSceneDirector _director;
        private CampBagScreenView _bag;
        private CampTooltipView _tooltip;

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        // ------------------------------------------------------------------
        // the stage's headline case
        // ------------------------------------------------------------------

        /// <summary>
        /// วางทับไอเทม id และ tier เดียวกัน: a drag onto the twin opens the window instead of folding
        /// the pair, and tapping the <b>second</b> card upgrades the item in the cell it already had.
        /// </summary>
        [UnityTest]
        public IEnumerator Dropping_an_item_on_its_twin_opens_the_chooser_and_the_second_card_upgrades_it()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            (PieceRef source, PieceRef target) = PlaceATwin();

            int bia = run.Wallet.Bia;
            int chest = run.ChestUsed;
            uint before = run.ComputeStateHash();
            CellOffset landed = Piece(target).Origin;
            CellOffset from = Piece(source).Origin;

            yield return Drag(CellCentre(from.X, from.Y), CellCentre(landed.X, landed.Y));

            Assert.That(
                presenter.Counters.DragPreviews, Is.GreaterThan(0),
                "no drag handler ran: the pointer never reached the view");
            Assert.That(presenter.StatusCode, Is.Empty, "a legal merge gesture was refused: " + presenter.StatusCode);

            // Item 1: the pair asked a question instead of folding, and asking it changed nothing.
            Assert.That(presenter.IsMergeChooserOpen, Is.True, "dropping an item on its twin did not open the window");
            Assert.That(_bag.ChooserVisible, Is.True, "the window is open but the screen is not drawing it");
            Assert.That(
                _bag.ChooserCardCount, Is.GreaterThanOrEqualTo(2),
                "the window offered " + _bag.ChooserCardCount + " card(s), so there is no choice to make");
            Assert.That(presenter.Counters.MergeChoicesOpened, Is.EqualTo(1), "the opening was not counted");
            Assert.That(
                run.ComputeStateHash(), Is.EqualTo(before),
                "opening the window changed the run: nothing may happen before a card is confirmed");
            Assert.That(ItemOf(run, target).Tier, Is.EqualTo(1), "the target went up a tier before a card was tapped");
            Assert.That(ItemCount(run), Is.EqualTo(2), "a piece was consumed before a card was tapped");

            Canvas.ForceUpdateCanvases();

            // Item 2: the window's own geometry — it dims the board it covers, and the cards live inside
            // its panel without sharing pixels.
            Assert.That(_bag.TryGetChooserDimRect(out Rect dim), Is.True, "the window has no dim layer");
            Assert.That(
                _bag.TryGetSurfaceScreenRect(CampBagSurface.Board, out Rect board), Is.True,
                "the board is not on screen");
            Assert.That(
                Contains(dim, board), Is.True,
                "the dim layer does not cover the board behind it: " + dim + " against " + board);
            Assert.That(_bag.TryGetChooserPanelRect(out Rect panel), Is.True, "the window's panel has no rectangle");
            Assert.That(Contains(dim, panel), Is.True, "the window's panel is not inside the dim layer");

            var cards = new List<Rect>();
            for (int i = 0; i < _bag.ChooserCardCount; i++)
            {
                Assert.That(_bag.TryGetChoiceRect(i, out Rect card), Is.True, "card " + i + " has no rectangle");
                Assert.That(card.width, Is.GreaterThan(0f), "card " + i + " has no width");
                Assert.That(card.height, Is.GreaterThan(0f), "card " + i + " has no height");
                Assert.That(Contains(panel, card), Is.True, "card " + i + " is drawn outside the window: " + card);

                Assert.That(_bag.TryGetChoiceScreenPoint(i, out Vector2 point), Is.True, "card " + i + " has no screen point");
                Assert.That(card.Contains(point), Is.True, "the published point of card " + i + " is not inside the card");

                for (int j = 0; j < cards.Count; j++)
                {
                    Assert.That(
                        Overlaps(cards[j], card), Is.False,
                        "card " + i + " shares pixels with card " + j + ": " + card + " against " + cards[j]);
                }

                cards.Add(card);
            }

            // Item 5, step one: the card that the pointer taps is the upgrade the piece keeps.
            string chosen = presenter.MergeChooser.Choices[1].VariantId;
            Assert.That(_bag.TryGetChoiceScreenPoint(1, out Vector2 second), Is.True, "the second card is not on screen");

            yield return Click(second);
            yield return WaitUntil(
                () => !presenter.IsMergeChooserOpen, "the window stayed open after a card was tapped");

            Assert.That(_bag.ChooserVisible, Is.False, "the window is still drawn after a card was tapped");
            Assert.That(presenter.Counters.Merged, Is.EqualTo(1), "tapping a card did not merge the pair");
            Assert.That(presenter.Counters.MergeChoicesConfirmed, Is.EqualTo(1), "the confirmation was not counted");
            Assert.That(presenter.Counters.MergeChoicesCancelled, Is.Zero, "a confirmation was counted as a cancel");
            Assert.That(presenter.IsDragging, Is.False, "the drag came back after the confirmation");

            ItemInstance survivor = ItemOf(run, target);
            Assert.That(survivor.Tier, Is.EqualTo(2), "the survivor did not go up a tier");
            Assert.That(survivor.Origin, Is.EqualTo(landed), "ไอเทมไม่ได้อยู่ช่องเดิม: the survivor left its cell");
            Assert.That(survivor.Zone, Is.EqualTo(PieceZone.Board), "the survivor is not on the board");
            Assert.That(survivor.VariantIds.Count, Is.EqualTo(1), "the chosen upgrade was not recorded on the piece");
            Assert.That(
                survivor.VariantIds[0], Is.EqualTo(chosen),
                "the piece kept a different upgrade than the card the pointer tapped");

            Assert.That(ItemCount(run), Is.EqualTo(1), "the merge did not consume one of the two copies");
            Assert.That(run.Wallet.Bia, Is.EqualTo(bia), "the merge changed the purse");
            Assert.That(run.ChestUsed, Is.EqualTo(chest), "the merge changed the chest");

            // The screen recomputed from the new bag, so the cell draws the upgraded piece.
            Assert.That(Piece(target).Tier, Is.EqualTo(2), "the board did not redraw the upgraded item");
        }

        // ------------------------------------------------------------------
        // closing without changing anything
        // ------------------------------------------------------------------

        /// <summary>
        /// Item 3: Escape, and the back control, close the window without changing anything — and the
        /// same gesture offers the same choices again, because the offer is a pure function of the run.
        /// </summary>
        [UnityTest]
        public IEnumerator Escape_and_the_back_control_close_the_chooser_and_change_nothing()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            (PieceRef source, PieceRef target) = PlaceATwin();

            uint before = run.ComputeStateHash();
            CellOffset from = Piece(source).Origin;
            CellOffset landed = Piece(target).Origin;

            yield return Drag(CellCentre(from.X, from.Y), CellCentre(landed.X, landed.Y));
            Assert.That(presenter.IsMergeChooserOpen, Is.True, "the gesture did not open the window");

            string[] offered = Ids(presenter);

            yield return PressKey(_keyboard.escapeKey);
            yield return WaitUntil(() => !presenter.IsMergeChooserOpen, "Escape did not close the window");

            Assert.That(_bag.ChooserVisible, Is.False, "the window is still drawn after Escape");
            Assert.That(presenter.Counters.MergeChoicesCancelled, Is.EqualTo(1), "the cancel was not counted");
            Assert.That(presenter.Counters.Merged, Is.Zero, "Escape merged the pair");
            Assert.That(presenter.Counters.MergeChoicesConfirmed, Is.Zero, "Escape confirmed a choice");
            Assert.That(
                run.ComputeStateHash(), Is.EqualTo(before),
                "Escape changed the run: closing the window must be a no-op");
            Assert.That(ItemCount(run), Is.EqualTo(2), "Escape consumed a piece");
            Assert.That(presenter.StatusCode, Is.Empty, "Escape left a refusal on screen: " + presenter.StatusCode);

            // The same gesture again: the offer is drawn from the run's state, so it is the same offer —
            // a window that could be closed and reopened for a better roll would show up here.
            yield return Drag(CellCentre(from.X, from.Y), CellCentre(landed.X, landed.Y));
            Assert.That(presenter.IsMergeChooserOpen, Is.True, "the repeated gesture did not open the window");
            Assert.That(
                Ids(presenter), Is.EqualTo(offered),
                "reopening the window offered different choices: the offer is not deterministic");

            Assert.That(_bag.TryGetChooserBackScreenPoint(out Vector2 back), Is.True, "the back control is not on screen");
            yield return Click(back);
            yield return WaitUntil(() => !presenter.IsMergeChooserOpen, "the back control did not close the window");

            Assert.That(presenter.Counters.MergeChoicesCancelled, Is.EqualTo(2), "the back control was not counted as a cancel");
            Assert.That(presenter.Counters.Merged, Is.Zero, "the back control merged the pair");
            Assert.That(
                run.ComputeStateHash(), Is.EqualTo(before),
                "the back control changed the run: closing the window must be a no-op");
        }

        // ------------------------------------------------------------------
        // the board is locked behind the window
        // ------------------------------------------------------------------

        /// <summary>
        /// Item 4: while the window is open the dim layer swallows the board, so the very gesture that
        /// opened it does nothing — and the tooltip is not drawn over the modal.
        /// </summary>
        [UnityTest]
        public IEnumerator While_the_chooser_is_open_the_board_is_locked_and_no_tooltip_is_drawn()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            (PieceRef source, PieceRef target) = PlaceATwin();

            CellOffset from = Piece(source).Origin;
            CellOffset landed = Piece(target).Origin;

            yield return Drag(CellCentre(from.X, from.Y), CellCentre(landed.X, landed.Y));
            Assert.That(presenter.IsMergeChooserOpen, Is.True, "the gesture did not open the window");

            int previews = presenter.Counters.DragPreviews;
            int moves = presenter.Counters.DragMoves;
            PieceRef selected = _bag.SelectedPiece;
            uint opened = run.ComputeStateHash();

            // The same drag, repeated over the same cells, with the window open.
            yield return Drag(CellCentre(from.X, from.Y), CellCentre(landed.X, landed.Y));
            yield return null;

            Assert.That(
                presenter.Counters.DragPreviews, Is.EqualTo(previews),
                "a drag ran behind the window: the dim layer is not blocking the pointer");
            Assert.That(presenter.Counters.DragMoves, Is.EqualTo(moves), "a pointer move reached the board behind the window");
            Assert.That(presenter.IsDragging, Is.False, "the board behind the window picked a piece up");
            Assert.That(_bag.SelectedPiece, Is.EqualTo(selected), "a click behind the window changed the selection");

            // A hover over a piece behind the window must not raise the floating tooltip either.
            Set(_mouse.position, CellCentre(landed.X, landed.Y));
            yield return null;
            yield return null;

            Assert.That(_tooltip.Visible, Is.False, "the tooltip is drawn over the window");

            Assert.That(_bag.ChooserVisible, Is.True, "the window closed itself while the board was locked");
            Assert.That(presenter.Counters.MergeChoicesConfirmed, Is.Zero, "a click behind the window confirmed a choice");
            Assert.That(
                run.ComputeStateHash(), Is.EqualTo(opened),
                "a gesture behind the window changed the run");
        }

        // ------------------------------------------------------------------
        // the keyboard
        // ------------------------------------------------------------------

        /// <summary>
        /// Item 3: the 1/2/3 keys pick the card with that number, so the choice is reachable without a
        /// pointer at all.
        /// </summary>
        [UnityTest]
        public IEnumerator Pressing_a_number_key_picks_the_card_with_that_number()
        {
            yield return LoadCamp();

            RunSession run = _director.Run;
            CampPresenter presenter = _director.Presenter;

            (PieceRef source, PieceRef target) = PlaceATwin();

            yield return Drag(
                CellCentre(Piece(source).Origin.X, Piece(source).Origin.Y),
                CellCentre(Piece(target).Origin.X, Piece(target).Origin.Y));

            Assert.That(presenter.IsMergeChooserOpen, Is.True, "the gesture did not open the window");
            Assert.That(_bag.ChooserCardCount, Is.GreaterThanOrEqualTo(3), "fewer than three cards, so key 2 has no card");

            string second = presenter.MergeChooser.Choices[1].VariantId;

            yield return PressKey(_keyboard.digit2Key);
            yield return WaitUntil(() => !presenter.IsMergeChooserOpen, "the 2 key did not confirm a card");

            Assert.That(presenter.Counters.Merged, Is.EqualTo(1), "the 2 key did not merge the pair");
            Assert.That(presenter.Counters.MergeChoicesConfirmed, Is.EqualTo(1), "the 2 key was not counted as a confirmation");
            Assert.That(_bag.ChooserVisible, Is.False, "the window is still drawn after the 2 key");

            ItemInstance survivor = ItemOf(run, target);
            Assert.That(survivor.Tier, Is.EqualTo(2), "the survivor did not go up a tier");
            Assert.That(
                survivor.VariantIds[0], Is.EqualTo(second),
                "the 2 key confirmed a different card than the second one");
        }

        // ------------------------------------------------------------------
        // fixture
        // ------------------------------------------------------------------

        /// <summary>Loads the Camp scene, starts it, and opens the จัดย่าม panel the way a player does.</summary>
        private IEnumerator LoadCamp()
        {
            // A shell this fixture owns: see SceneFlowHarness. The App scope persists once a
            // fixture loads Boot, so a test that did not reset would inherit a live run.
            SceneFlowHarness.ResetShell();
            yield return null;

            SceneManager.LoadScene(CampSceneName, LoadSceneMode.Single);
            yield return null;

            Assert.That(EventSystem.current, Is.Not.Null, "the Camp scene has no EventSystem");
            Assert.That(
                EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null,
                "the EventSystem does not use the Input System module");

            _director = UnityEngine.Object.FindFirstObjectByType<CampSceneDirector>();
            Assert.That(_director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(_director.TryStart(), Is.True, "the camp did not start: " + _director.StartError);
            yield return null;

            // FindObjectsInactive.Include: the จัดย่าม panel starts closed, so the view sits on an
            // inactive GameObject.
            _bag = UnityEngine.Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
            Assert.That(_bag, Is.Not.Null, "the Camp scene has no CampBagScreenView");
            Assert.That(_bag.Model, Is.Not.Null, "the bag screen never received a model");

            _tooltip = UnityEngine.Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Assert.That(_tooltip, Is.Not.Null, "the Camp scene has no CampTooltipView");

            Assert.That(_bag.PanelVisible, Is.False, "the จัดย่าม panel should start closed");

            // The panel has only just been shown, so its rectangles may still be the previous frame's:
            // forcing the rebuild makes the aim deterministic rather than a matter of which side of the
            // frame boundary the coroutine step landed on (the drag suite measured exactly that).
            Canvas.ForceUpdateCanvases();

            Assert.That(
                _bag.TryGetButtonScreenPoint(CampBagButton.Open, out Vector2 toggle), Is.True,
                "the จัดย่าม button is not on screen");

            yield return Click(toggle);

            Assert.That(_bag.PanelVisible, Is.True, "clicking จัดย่าม did not open the panel");
            Assert.That(_bag.BoardWidth, Is.GreaterThan(0), "the board never rendered its cells");
        }

        /// <summary>
        /// Builds the pair the merge gesture needs: a second copy of the starting item, both on the
        /// board, the twin as far away as a bag cell allows so the drag crosses the EventSystem's
        /// threshold.
        /// </summary>
        /// <remarks>
        /// This is the fixture <b>building a state</b>, not performing the action under test: the merge
        /// itself is still the pointer's, and the screen is republished afterwards so the pointer sees
        /// the new picture.
        /// </remarks>
        private (PieceRef Source, PieceRef Target) PlaceATwin()
        {
            RunSession run = _director.Run;
            CampPieceModel first = FirstPieceOfKind(PieceKind.Item);

            Result<PieceRef> added = run.Backpack.AddItem(first.DefId);
            Assert.That(added.IsSuccess, Is.True, "a second copy of " + first.DefId + " must be addable");

            CellOffset cell = FarthestFreeCellCoveredByABag(first.Origin);
            Assert.That(
                cell.X, Is.GreaterThanOrEqualTo(0),
                "the fixture found no free bag cell to stand the twin on");

            Assert.That(
                run.MovePiece(added.Value, cell, Rotation.Degrees0).IsSuccess, Is.True,
                "the twin must be placeable on " + cell);

            Vector2 fromPoint = CellCentre(first.Origin.X, first.Origin.Y);
            Vector2 toPoint = CellCentre(cell.X, cell.Y);
            Assert.That(
                Vector2.Distance(fromPoint, toPoint),
                Is.GreaterThan(EventSystem.current.pixelDragThreshold * 2f),
                "the twin stands too close for the EventSystem's drag threshold");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();

            return (first.Piece, added.Value);
        }

        /// <summary>The variant ids the open window is offering, in the order the cards are drawn.</summary>
        private static string[] Ids(CampPresenter presenter)
        {
            CampMergeChooserModel chooser = presenter.MergeChooser;
            Assert.That(chooser.IsOpen, Is.True, "there is no window open to read the choices from");

            var ids = new string[chooser.ChoiceCount];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = chooser.Choices[i].VariantId;
            }

            return ids;
        }

        // ------------------------------------------------------------------
        // pointer and keyboard helpers
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

        /// <summary>Presses and releases a key across frames, the way a player taps it.</summary>
        private IEnumerator PressKey(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        /// <summary>
        /// Advances frames until <paramref name="condition"/> holds, then asserts it — a UI state that
        /// arrives a frame after the event is waited for rather than slept through.
        /// </summary>
        private IEnumerator WaitUntil(Func<bool> condition, string what, int frames = 12)
        {
            for (int i = 0; i < frames && !condition(); i++)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, what);
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

            Assert.Fail("the run holds no " + kind + " on the board");
            return default;
        }

        private static ItemInstance ItemOf(RunSession run, PieceRef piece)
        {
            IReadOnlyList<ItemInstance> items = run.Backpack.Items;
            ItemInstance found = null!;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Ref == piece)
                {
                    found = items[i];
                    break;
                }
            }

            Assert.That(found, Is.Not.Null, "the run no longer holds " + piece);
            return found;
        }

        private static int ItemCount(RunSession run) => run.Backpack.Items.Count;

        /// <summary>The board cell an item can legally stand on that is furthest from where it is.</summary>
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

                    // A bag cell is a legal place for an item exactly when no other item is on it; the
                    // bag covering it is what makes it legal in the first place.
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

        private static bool Overlaps(Rect a, Rect b) =>
            a.xMin < b.xMax && b.xMin < a.xMax && a.yMin < b.yMax && b.yMin < a.yMax;

        private static bool Contains(Rect outer, Rect inner) =>
            outer.xMin <= inner.xMin && outer.yMin <= inner.yMin
            && outer.xMax >= inner.xMax && outer.yMax >= inner.yMax;
    }
}
