using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Camp;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 09i: the combo lines — where they start and end, what layer they are drawn on, what
    /// they look like — and the names inside the cells, on the <b>real Camp scene</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The board is built at runtime, so there is no rectangle in <c>Camp.unity</c> to measure: the
    /// scene is started and played with, exactly as a player would. The state is the stage's own
    /// diagnosis case — a 2×2 tome and two 1×1 weapons sharing a row — built through the run's own
    /// commands (<c>MovePiece</c>) rather than by editing a model, so a rule that refused the layout
    /// fails this suite instead of being papered over.
    /// </para>
    /// <para>
    /// <b>The acceptance is geometry, so it is asserted in pixels.</b> Every line's two ends are read
    /// from the bar that was actually drawn (<c>TryGetLineScreenEnds</c>) and compared with the screen
    /// rectangles of the two pieces (<c>TryGetPieceScreenRect</c>). The defect this stage fixes was a
    /// <i>formula</i> — a rectangle rebuilt from the cell grid with y pointing the wrong way — and a
    /// test that asked the same formula where the middle was would have agreed with it. These ask the
    /// rectangles the player sees instead.
    /// </para>
    /// </remarks>
    public sealed class CampSynergyLinePlayModeTests
    {
        private const string CampSceneName = "Camp";

        /// <summary>The tolerance the stage names: a line end may be off its piece's middle by 1 px.</summary>
        private const float TolerancePixels = 1f;

        /// <summary>The smallest type an in-cell name may be drawn at (Stage 09i).</summary>
        private const float MinCellLabelSize = 14f;

        /// <summary>The largest type an in-cell name may be drawn at (the 22 px Thai floor).</summary>
        private const float MaxCellLabelSize = 22f;

        /// <summary>The thin, bright line the stage asks for: at least 3 px at the 1080p reference.</summary>
        private const float MinLineThickness = 3f;

        private CampSceneDirector _director;
        private CampBagScreenView _bag;

        /// <summary>
        /// Loads the camp, opens จัดย่าม, and builds the state the stage reports on: two 3×3 bags side by
        /// side, a 2×2 tome on the first, and a 1×1 ยันต์เพลิงกาลี on each — all in one board row.
        /// </summary>
        /// <remarks>
        /// Two bags rather than one because an item is only legal on a cell a bag covers
        /// (<c>inventory.item_not_covered</c>): a tome and two weapons sharing a row need six cells of
        /// bag under them, which is one bag wider than the starting รยาม.
        /// </remarks>
        private IEnumerator LoadCampWithATomeAndTwoWeapons()
        {
            SceneFlowHarness.ResetShell();
            yield return null;

            SceneManager.LoadScene(CampSceneName, LoadSceneMode.Single);
            yield return null;

            _director = Object.FindFirstObjectByType<CampSceneDirector>();
            Assert.That(_director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(_director.TryStart(), Is.True, "the camp did not start: " + _director.StartError);
            yield return null;

            _bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
            Assert.That(_bag, Is.Not.Null, "the Camp scene has no CampBagScreenView");

            _bag.ShowPanel(true);
            Canvas.ForceUpdateCanvases();

            RunSession run = _director.Run;
            Assert.That(run, Is.Not.Null, "the camp started without a run");

            PieceRef firstBag = run.Backpack.Bags[0].Ref;
            PieceRef startingWeapon = run.Backpack.Items[0].Ref;

            Assert.That(
                Move(run, firstBag, 0, 1), Is.True,
                "the starting รยาม could not be moved to (0,1): " + _director.StartError);

            var secondBag = run.Backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(secondBag.IsSuccess, Is.True, "a second รยาม could not be added");

            Assert.That(Move(run, secondBag.Value, 3, 1), Is.True, "the second รยาม could not be placed at (3,1)");
            Assert.That(Move(run, startingWeapon, 2, 1), Is.True, "the starting weapon could not be placed at (2,1)");

            var tome = run.Backpack.AddItem("item.spell_tome");
            Assert.That(tome.IsSuccess, Is.True, "the tome could not be added");

            Assert.That(Move(run, tome.Value, 0, 1), Is.True, "the 2x2 tome could not be placed at (0,1)");

            var secondWeapon = run.Backpack.AddItem("item.kali_flame_yant");
            Assert.That(secondWeapon.IsSuccess, Is.True, "a second weapon could not be added");

            Assert.That(Move(run, secondWeapon.Value, 3, 1), Is.True, "the second weapon could not be placed at (3,1)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        /// <summary>The run's own placement command, so a layout this suite builds is one the game allows.</summary>
        private static bool Move(RunSession run, PieceRef piece, int x, int y) =>
            run.MovePiece(piece, new CellOffset(x, y), Rotation.Degrees0).IsSuccess;

        /// <summary>
        /// The stage's first acceptance, stated as geometry: for every pair the run says is active, both
        /// ends of the drawn line sit on the <b>middle</b> of the rectangles the two pieces are drawn in.
        /// </summary>
        /// <remarks>
        /// Before this stage both ends were one whole piece-height too high — the tome's end sat on the
        /// board's top edge and each weapon's end 78 px above the weapon — so this test is the one that
        /// would have failed. It also states the weaker half of the acceptance (each end is inside its
        /// own piece's rectangle), which is what "ไม่ล้นออกจากชิ้น" means for a mark.
        /// </remarks>
        [UnityTest]
        public IEnumerator Every_line_reaches_the_middle_of_the_two_pieces_it_connects()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(2),
                "the tome and two weapons in one row should be two pairs, and the view drew "
                + _bag.VisibleLineCount + " line(s)");

            AssertEveryLineEndsAtAPieceCentre("a fresh board");
        }

        /// <summary>
        /// A รยาม that is moved carries its items (Stage 09f) and the lines follow in the same gesture;
        /// a pair that stops sharing a row loses its line on the next render, and gets it back when it
        /// shares one again.
        /// </summary>
        /// <remarks>
        /// The tome's own carried weapon always stays in the tome's row — that is what "the bag
        /// carries its items" means — so the pair that leaves is the one on the <b>other</b> bag, and
        /// the count moves 2 → 1 → 2 → 1 as that รยาม is walked away and back.
        /// </remarks>
        [UnityTest]
        public IEnumerator A_moved_bag_carries_its_lines_and_a_pair_that_leaves_the_row_loses_its_line()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            RunSession run = _director.Run;
            PieceRef secondBag = BagAt(run, 3, 1);
            PieceRef firstBag = BagAt(run, 0, 1);

            // The second bag, with its weapon on it, three cells to the right: still the same row.
            Assert.That(Move(run, secondBag, 6, 1), Is.True, "the second รยาม could not be moved to (6,1)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(_bag.VisibleLineCount, Is.EqualTo(2), "moving a รยาม along the row lost a line");
            AssertEveryLineEndsAtAPieceCentre("after the second รยาม moved to (6,1)");

            // The first bag — with the tome and one weapon on it — down to row 4. The tome keeps that
            // weapon, so one of the two pairs survives the move.
            Assert.That(Move(run, firstBag, 0, 4), Is.True, "the first รยาม could not be moved to (0,4)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(1),
                "the tome left the other weapon's row, so one pair should be left, and the view drew "
                + _bag.VisibleLineCount);
            AssertEveryLineEndsAtAPieceCentre("after the first รยาม moved to (0,4)");

            // The second bag back into the tome's row: the mark returns with the pair.
            Assert.That(Move(run, secondBag, 6, 4), Is.True, "the second รยาม could not be moved to (6,4)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(_bag.VisibleLineCount, Is.EqualTo(2), "the weapon is back in the tome's row, so two lines");
            AssertEveryLineEndsAtAPieceCentre("after the second รยาม moved to (6,4)");

            // And away again: one line is removed, and the removed one is the pair whose weapon left.
            Assert.That(Move(run, secondBag, 6, 1), Is.True, "the second รยาม could not be moved back to (6,1)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(1),
                "the weapon left the tome's row and its line should be gone, yet the view drew "
                + _bag.VisibleLineCount);
            AssertEveryLineEndsAtAPieceCentre("after the second รยาม left the row again");
        }

        /// <summary>
        /// Rotation changes a piece's footprint, so the rectangle a line has to reach is not the one it
        /// reached before: every rotation of every pair's pieces is checked against the rectangles as
        /// they are drawn afterwards.
        /// </summary>
        /// <remarks>
        /// The shipped tome is 2×2 and every weapon is 1×1, so the four rotations leave these
        /// rectangles the same size — what this proves is that the geometry is read per render rather
        /// than cached from the first one. The bag's own asymmetric footprint is the case that makes
        /// rotation a visible change, and it is covered by `CampBagDragPlayModeTests`' badge and carry
        /// rules.
        /// </remarks>
        [UnityTest]
        public IEnumerator Every_rotation_of_a_paired_piece_keeps_the_line_on_its_middle()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            RunSession run = _director.Run;
            PieceRef tome = ItemAt(run, 0, 1);

            for (int turn = 0; turn < 4; turn++)
            {
                Assert.That(run.RotatePiece(tome).IsSuccess, Is.True, "the tome could not be rotated");

                _director.Presenter.Refresh();
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.That(_bag.VisibleLineCount, Is.EqualTo(2), "a rotation of the tome lost a line");
                AssertEveryLineEndsAtAPieceCentre("after turning the tome " + (turn + 1) + " quarter(s)");
            }
        }

        /// <summary>
        /// The same geometry at a <b>different canvas scale</b> — the 1280×720 case. A canvas scaled to
        /// two thirds lays the board out at two thirds of the pixels, and the ends still have to land on
        /// the pieces' middles <i>on screen</i>, because the marks are placed in the container's own
        /// space and only then scaled.
        /// </summary>
        /// <remarks>
        /// The scale is produced the way the game produces it — through the canvas' own
        /// <c>CanvasScaler</c>, whose reference height decides the factor — rather than by scaling a
        /// transform the player never scales. A 1620-unit reference against a 1080-pixel screen is the
        /// factor a 1280×720 window has.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_lines_still_land_on_their_pieces_when_the_canvas_is_scaled_to_the_720p_factor()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            var scaler = _bag.GetComponentInParent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, "the camp's canvas has no CanvasScaler");

            Vector2 original = scaler.referenceResolution;

            try
            {
                scaler.referenceResolution = new Vector2(2880f, 1620f);
                Canvas.ForceUpdateCanvases();
                yield return null;

                float factor = _bag.GetComponentInParent<Canvas>().scaleFactor;
                Assert.That(
                    factor, Is.EqualTo(2f / 3f).Within(0.01f),
                    "the canvas did not take the 720p factor: it is scaled by " + factor);

                Assert.That(_bag.VisibleLineCount, Is.EqualTo(2), "the resized canvas lost a line");
                AssertEveryLineEndsAtAPieceCentre("at a canvas scale of " + factor.ToString("0.###"));

                // The stage's thickness is a 1080p statement: 3 px where one canvas unit is one
                // pixel. At this factor the same line is two thirds of that, which is correct and is
                // reported rather than asserted against the 1080p number.
                Assert.That(
                    _bag.TryGetLineStyle(0, out _, out _, out float thickness), Is.True);
                Assert.That(
                    thickness, Is.GreaterThanOrEqualTo(MinLineThickness),
                    "the line is " + thickness + " canvas units thick, below the "
                    + MinLineThickness + " px it is authored at for the 1080p reference");
            }
            finally
            {
                scaler.referenceResolution = original;
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// The mark is legible on a bag's green: a vivid red core, at least 0.9 opaque and at least 3 px
        /// thick at the 1080p reference, drawn over a darker edge that cuts it off from the board —
        /// the three things the stage names, read from the images that were built.
        /// </summary>
        [UnityTest]
        public IEnumerator A_line_is_a_thick_opaque_red_drawn_over_a_dark_edge()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            Assert.That(_bag.VisibleLineCount, Is.GreaterThan(0), "there is no line to look at");

            for (int i = 0; i < _bag.VisibleLineCount; i++)
            {
                Assert.That(_bag.TryGetLineStyle(i, out Color core, out Color edge, out float thickness), Is.True);

                Assert.That(core.r, Is.GreaterThan(0.80f), "line " + i + "'s core is not red: " + core);
                Assert.That(core.g, Is.LessThan(0.30f), "line " + i + "'s core is not red: " + core);
                Assert.That(core.b, Is.LessThan(0.30f), "line " + i + "'s core is not red: " + core);
                Assert.That(
                    core.a, Is.GreaterThanOrEqualTo(0.9f),
                    "line " + i + " is only " + core.a.ToString("0.##") + " opaque");

                Assert.That(
                    thickness, Is.GreaterThanOrEqualTo(MinLineThickness),
                    "line " + i + " is " + thickness + " canvas units thick, below the 3 px floor");

                Assert.That(
                    Luminance(edge), Is.LessThan(0.2f),
                    "line " + i + "'s edge is not dark enough to cut against the board: " + edge);
                Assert.That(
                    edge.a, Is.GreaterThan(0.5f),
                    "line " + i + "'s edge is see-through: " + edge);

                Assert.That(
                    Luminance(core) - Luminance(edge), Is.GreaterThan(0.3f),
                    "line " + i + "'s core and edge are too close in tone to read as a line on an edge");
            }
        }

        /// <summary>
        /// The layer the stage asks for: the lines are the board's <b>last</b> children, after every
        /// piece and the name inside it — so a mark that enters a piece is seen entering it rather than
        /// stopping at its edge.
        /// </summary>
        /// <remarks>
        /// The other half of the order is proved elsewhere: the tooltip is on a canvas of its own at a
        /// higher sorting order (`The_tooltip_is_drawn_on_a_canvas_above_the_board_and_stays_on_screen`
        /// in <c>CampBagDragPlayModeTests</c>), and the drag ghost and the merge window are siblings of
        /// the whole panel that are shown last when they appear.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_lines_are_drawn_after_every_piece_and_its_name()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            RectTransform container = _bag.LineContainer;
            Assert.That(container, Is.Not.Null, "the view has no line container");

            var board = container.parent as RectTransform;
            Assert.That(board, Is.Not.Null, "the line container is not under the board");

            int lineIndex = container.GetSiblingIndex();
            var statements = new List<string>();

            for (int i = 0; i < board.childCount; i++)
            {
                var child = (RectTransform)board.GetChild(i);

                // A piece is the child that carries a name of its own; the cells, the ghosts and the
                // two mark containers are not pieces and may sit either side of the marks.
                TMP_Text label = child.Find(CampBagScreenView.CellLabelName)?.GetComponent<TMP_Text>();
                if (label == null)
                {
                    continue;
                }

                statements.Add(child.name + " at " + i);

                Assert.That(
                    i, Is.LessThan(lineIndex),
                    child.name + " (a piece and its name) is drawn at sibling " + i
                    + ", at or after the line container at " + lineIndex
                    + " — the line would run under the piece it names");
            }

            Assert.That(statements.Count, Is.GreaterThanOrEqualTo(4), "the board drew too few pieces to judge the order");

            // Nothing is drawn above the marks except the bag-area badges, which are the other mark.
            for (int i = lineIndex + 1; i < board.childCount; i++)
            {
                var child = (RectTransform)board.GetChild(i);
                Assert.That(
                    child.name, Is.EqualTo("BagBonusBadges"),
                    "'" + child.name + "' is drawn above the combo lines, which the stage does not allow");
            }
        }

        /// <summary>
        /// Stage 09i, item 5: no name drawn inside a cell leaves the rectangle of its piece, every one
        /// of them is drawn between 14 and 22 px, and the full name is still on the tooltip.
        /// </summary>
        /// <remarks>
        /// The text's own laid-out bounds (<c>TMP_Text.textBounds</c> after a forced mesh update) are
        /// what is measured, not the rectangle the label was given: a label can be exactly the size of
        /// its piece and still paint a wider line of text over the next one. That is the defect the
        /// stage reports — the weapon names were drawn wider than the cells they sit in.
        /// </remarks>
        [UnityTest]
        public IEnumerator A_name_in_a_cell_stays_inside_its_piece_and_is_never_below_14_px()
        {
            yield return LoadCampWithATomeAndTwoWeapons();

            TMP_Text[] labels = _bag.GetComponentsInChildren<TMP_Text>(true);
            int measured = 0;

            foreach (TMP_Text label in labels)
            {
                if (label.name != CampBagScreenView.CellLabelName || !label.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var rect = (RectTransform)label.transform;
                label.ForceMeshUpdate();

                measured++;

                Assert.That(
                    label.fontSize, Is.InRange(MinCellLabelSize, MaxCellLabelSize),
                    "'" + label.text + "' is drawn at " + label.fontSize + " px");

                Bounds bounds = label.textBounds;
                Rect frame = rect.rect;
                string story = "'" + label.text + "' bounds " + bounds + " in a " + frame.size + " cell";

                Assert.That(
                    bounds.size.x, Is.GreaterThan(0f), "the name measured nothing: " + story);
                Assert.That(
                    bounds.min.x, Is.GreaterThanOrEqualTo(frame.xMin - 0.5f), "the name leaves its cell on the left: " + story);
                Assert.That(
                    bounds.max.x, Is.LessThanOrEqualTo(frame.xMax + 0.5f), "the name leaves its cell on the right: " + story);
                Assert.That(
                    bounds.min.y, Is.GreaterThanOrEqualTo(frame.yMin - 0.5f), "the name leaves its cell at the bottom: " + story);
                Assert.That(
                    bounds.max.y, Is.LessThanOrEqualTo(frame.yMax + 0.5f), "the name leaves its cell at the top: " + story);
            }

            // Two รยาม and three items are on the board; a bag's own name gives way to the item on it,
            // so the names that are actually drawn are the three items'.
            Assert.That(measured, Is.GreaterThanOrEqualTo(3), "the board drew too few names to judge");

            // The full name the cell may not fit is the tooltip's job: hovering the tome publishes it.
            // The tooltip is a canvas of its own (Stage 09e), so it is found by its view rather than
            // under the panel's canvas.
            PieceRef tome = ItemAt(_director.Run, 0, 1);
            CampPieceModel model = PieceOf(tome);

            var tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Assert.That(tooltip, Is.Not.Null, "the Camp scene has no CampTooltipView");

            _director.Presenter.Hover(tome);
            Canvas.ForceUpdateCanvases();
            yield return null;

            TMP_Text title = null;
            foreach (TMP_Text label in tooltip.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "TooltipTitle")
                {
                    title = label;
                }
            }

            Assert.That(title, Is.Not.Null, "the tooltip has no title label");
            Assert.That(
                title.text, Is.EqualTo(model.Name),
                "the tooltip draws '" + title.text + "' instead of the piece's full name '" + model.Name + "'");
        }

        // ------------------------------------------------------------------
        // Reading the picture's own geometry
        // ------------------------------------------------------------------

        /// <summary>
        /// Every drawn line, checked against the two rectangles it connects: each end inside its
        /// piece's rectangle, and within 1 px of that rectangle's middle.
        /// </summary>
        private void AssertEveryLineEndsAtAPieceCentre(string state)
        {
            CampLink[] links = _bag.Model.Links;
            int drawn = 0;

            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style != CampLinkStyle.Line)
                {
                    continue;
                }

                Assert.That(
                    _bag.TryGetLineScreenEnds(drawn, out Vector2 from, out Vector2 to), Is.True,
                    state + ": line " + drawn + " is listed by the model but not drawn");

                var source = new PieceRef(PieceKind.Item, links[i].SourceInstanceId);
                var target = new PieceRef(PieceKind.Item, links[i].TargetInstanceId);

                Assert.That(
                    _bag.TryGetPieceScreenRect(source, out Rect sourceRect), Is.True,
                    state + ": the source piece has no rectangle on screen");
                Assert.That(
                    _bag.TryGetPieceScreenRect(target, out Rect targetRect), Is.True,
                    state + ": the target piece has no rectangle on screen");

                AssertEnd(state, drawn, "source", from, sourceRect);
                AssertEnd(state, drawn, "target", to, targetRect);

                drawn++;
            }

            Assert.That(
                drawn, Is.EqualTo(_bag.VisibleLineCount),
                state + ": the model lists " + drawn + " line(s) and the view drew " + _bag.VisibleLineCount);
        }

        private static void AssertEnd(string state, int line, string end, Vector2 point, Rect piece)
        {
            Assert.That(
                piece.Contains(point), Is.True,
                state + ": line " + line + "'s " + end + " end " + point
                + " is outside the piece's rectangle " + piece + " (centre " + piece.center + ")");

            Assert.That(
                Vector2.Distance(point, piece.center), Is.LessThanOrEqualTo(TolerancePixels),
                state + ": line " + line + "'s " + end + " end is " + Vector2.Distance(point, piece.center).ToString("0.##")
                + " px from the piece's middle " + piece.center + " (it is at " + point + ")");
        }

        private static float Luminance(Color colour) =>
            (0.299f * colour.r) + (0.587f * colour.g) + (0.114f * colour.b);

        private static PieceRef BagAt(RunSession run, int x, int y)
        {
            for (int i = 0; i < run.Backpack.Bags.Count; i++)
            {
                if (run.Backpack.Bags[i].Origin.X == x && run.Backpack.Bags[i].Origin.Y == y)
                {
                    return run.Backpack.Bags[i].Ref;
                }
            }

            Assert.Fail("the run has no รยาม at (" + x + "," + y + ")");
            return PieceRef.None;
        }

        private static PieceRef ItemAt(RunSession run, int x, int y)
        {
            for (int i = 0; i < run.Backpack.Items.Count; i++)
            {
                if (run.Backpack.Items[i].Origin.X == x && run.Backpack.Items[i].Origin.Y == y)
                {
                    return run.Backpack.Items[i].Ref;
                }
            }

            Assert.Fail("the run has no item at (" + x + "," + y + ")");
            return PieceRef.None;
        }

        private CampPieceModel PieceOf(PieceRef piece)
        {
            CampPieceModel[] pieces = _bag.Model.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i].Piece == piece)
                {
                    return pieces[i];
                }
            }

            Assert.Fail("the screen has no piece " + piece);
            return default;
        }
    }
}
