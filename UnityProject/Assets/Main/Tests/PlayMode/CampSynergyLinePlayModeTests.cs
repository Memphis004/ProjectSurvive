using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
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
    /// Stage 09j: the combo lines on the <b>real Camp scene</b> — which pairs get one, where the mark
    /// is drawn, what it looks like, and which layer it lives on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The board is built at runtime, so there is no rectangle in <c>Camp.unity</c> to measure: the
    /// scene is started and played with, exactly as a player would. The state is the stage's own
    /// case — a 2×2 tome with a weapon touching it and a second weapon a cell further along the same
    /// row — built through the run's own commands (<c>MovePiece</c>) rather than by editing a model,
    /// so a rule that refused the layout fails this suite instead of being papered over.
    /// </para>
    /// <para>
    /// <b>The acceptance is geometry, so it is asserted in pixels.</b> Every line's two ends are read
    /// from the bar that was actually drawn (<c>TryGetLineScreenEnds</c>) and compared with the screen
    /// rectangles of the two pieces (<c>TryGetPieceScreenRect</c>). Since this stage a mark bridges
    /// the gap between two touching pieces, so the assertion is: each end sits on its own piece's
    /// <i>edge</i>, and the stroke never crosses a name or a piece it does not belong to.
    /// </para>
    /// </remarks>
    public sealed class CampSynergyLinePlayModeTests
    {
        private const string CampSceneName = "Camp";

        /// <summary>The tolerance the stage names: a line end may be off the edge it should reach by 1 px.</summary>
        private const float TolerancePixels = 1f;

        /// <summary>The smallest type an in-cell name may be drawn at (Stage 09i).</summary>
        private const float MinCellLabelSize = 14f;

        /// <summary>The largest type an in-cell name may be drawn at (the 22 px Thai floor).</summary>
        private const float MaxCellLabelSize = 22f;

        /// <summary>The thin, bright line the stage asks for: at least 3 px at the 1080p reference.</summary>
        private const float MinLineThickness = 3f;

        private CampSceneDirector _director;
        private CampBagScreenView _bag;
        private PieceRef _tome;
        private PieceRef _touchingWeapon;
        private PieceRef _farWeapon;

        /// <summary>
        /// Loads the camp, opens จัดย่าม, and builds the stage's state: two 3×3 bags side by side, a
        /// 2×2 tome on the first, a weapon touching the tome's right edge, and a second weapon a cell
        /// further along the row — on the second bag, so the rule fires for it too and only the
        /// drawing decides.
        /// </summary>
        /// <remarks>
        /// Two bags because an item is only legal on a cell a bag covers
        /// (<c>inventory.item_not_covered</c>): the tome and two weapons need six cells of bag under
        /// them, which is one bag wider than the starting รยาม.
        /// </remarks>
        private IEnumerator LoadCampWithTouchingAndDistantWeapons()
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

            Assert.That(Move(run, firstBag, 0, 1), Is.True, "the starting รยาม could not be moved to (0,1)");

            var secondBag = run.Backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(secondBag.IsSuccess, Is.True, "a second รยาม could not be added");
            Assert.That(Move(run, secondBag.Value, 3, 1), Is.True, "the second รยาม could not be placed at (3,1)");

            // The weapon that rode the bag off its corner has to leave the tome's cells before the tome
            // lands on them; then the tome sits at (0,1), the touching weapon against its right edge,
            // and the far weapon one cell further along the same row.
            _touchingWeapon = startingWeapon;
            Assert.That(Move(run, _touchingWeapon, 2, 1), Is.True, "the touching weapon could not be placed at (2,1)");

            var tome = run.Backpack.AddItem("item.spell_tome");
            Assert.That(tome.IsSuccess, Is.True, "the tome could not be added");
            _tome = tome.Value;
            Assert.That(Move(run, _tome, 0, 1), Is.True, "the 2x2 tome could not be placed at (0,1)");

            var farWeapon = run.Backpack.AddItem("item.kali_flame_yant");
            Assert.That(farWeapon.IsSuccess, Is.True, "a second weapon could not be added");
            _farWeapon = farWeapon.Value;
            Assert.That(Move(run, _farWeapon, 3, 1), Is.True, "the far weapon could not be placed at (3,1)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        /// <summary>The shipped tables — the config the camp itself runs on.</summary>
        private static IGameConfig ShippedConfig() => ResourcesGameConfigLoader.Load();

        /// <summary>The run's own placement command, so a layout this suite builds is one the game allows.</summary>
        private static bool Move(RunSession run, PieceRef piece, int x, int y) =>
            run.MovePiece(piece, new CellOffset(x, y), Rotation.Degrees0).IsSuccess;

        /// <summary>
        /// The stage's rule, stated as a count: one line for the pair that touches, none for the pair
        /// that shares the row across an empty cell — even though Core reports both pairs.
        /// </summary>
        [UnityTest]
        public IEnumerator Only_the_pair_that_touches_gets_a_line()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            CampLink[] links = _bag.Model.Links;
            Assert.That(
                HasLine(links, _tome.InstanceId, _touchingWeapon.InstanceId), Is.True,
                "the tome's neighbour has no line: " + Describe(links));
            Assert.That(
                HasLine(links, _tome.InstanceId, _farWeapon.InstanceId), Is.False,
                "a line was drawn across an empty cell to the weapon in column 3: " + Describe(links));
            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(1),
                "one touching pair should be one line, and the view drew " + _bag.VisibleLineCount);
        }

        /// <summary>
        /// Core is untouched by this stage: the rule still reports <b>both</b> weapons, and the far one
        /// is still the owner of a working bonus. Only the drawing distinguishes them.
        /// </summary>
        [UnityTest]
        public IEnumerator The_rule_still_reports_the_pair_that_gets_no_line()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            SynergyEvaluation layout = new SynergyEvaluator(ShippedConfig()).Evaluate(_director.Run.Backpack);
            int pairs = 0;

            for (int i = 0; i < layout.Active.Count; i++)
            {
                ActiveSynergy synergy = layout.Active[i];
                if ((synergy.SourceInstanceId == _tome.InstanceId && synergy.TargetInstanceId == _farWeapon.InstanceId)
                    || (synergy.SourceInstanceId == _farWeapon.InstanceId && synergy.TargetInstanceId == _tome.InstanceId))
                {
                    pairs++;
                }
            }

            Assert.That(
                pairs, Is.EqualTo(1),
                "Core stopped reporting the far weapon's same-row pair, which would mean this stage changed a rule");
        }

        /// <summary>
        /// Where a mark is drawn (Stage 09k): level, from the middle of one cell to the middle of the
        /// other, on the row the two cells share — and the names are painted above it.
        /// </summary>
        /// <remarks>
        /// Stage 09j bridged the nearest edges of the two rectangles instead, which for a 2×2 tome
        /// beside a 1×1 weapon was a near-vertical stub across the cells' join — the mark the player
        /// reported. A stroke along a row crosses the names' cells by design; what keeps it off the
        /// words is the layer, which is asserted below and painted by the label container.
        /// </remarks>
        [UnityTest]
        public IEnumerator Every_line_is_level_and_joins_the_two_cells_it_names()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            Assert.That(_bag.VisibleLineCount, Is.GreaterThan(0), "there is no line to measure");
            AssertEveryLineJoinsItsCells("a fresh board");
            AssertTheNamesAreDrawnOverTheLines();
        }

        /// <summary>
        /// Stretching a bag's own footprint (a rotation) and carrying its items re-draws the marks in
        /// the same frame: a mark is read from the rectangles as they are drawn, not cached.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_rotation_of_a_paired_piece_keeps_its_line_on_the_gap()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            RunSession run = _director.Run;

            for (int turn = 0; turn < 4; turn++)
            {
                Assert.That(run.RotatePiece(_tome).IsSuccess, Is.True, "the tome could not be rotated");

                _director.Presenter.Refresh();
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.That(_bag.VisibleLineCount, Is.EqualTo(1), "a rotation of the tome changed the number of lines");
                AssertEveryLineJoinsItsCells("after turning the tome " + (turn + 1) + " quarter(s)");
            }
        }

        /// <summary>
        /// The line goes the instant the pair stops qualifying and comes back when it qualifies again:
        /// the weapon is dragged off the tome's row and back onto it, in the same camp visit.
        /// </summary>
        [UnityTest]
        public IEnumerator A_line_appears_and_disappears_with_the_pair_it_belongs_to()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            RunSession run = _director.Run;

            Assert.That(Move(run, _touchingWeapon, 2, 3), Is.True, "the weapon could not be moved off the tome's row");
            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                HasLine(_bag.Model.Links, _tome.InstanceId, _touchingWeapon.InstanceId), Is.False,
                "the line survived the pair leaving the same row: " + Describe(_bag.Model.Links));
            Assert.That(_bag.VisibleLineCount, Is.EqualTo(0), "the view still drew a mark after the pair stopped qualifying");

            Assert.That(Move(run, _touchingWeapon, 2, 1), Is.True, "the weapon could not be moved back beside the tome");
            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(1),
                "the line did not come back when the weapon returned to the tome's edge, and the view drew "
                + _bag.VisibleLineCount);
            AssertEveryLineJoinsItsCells("after the weapon returned to the tome's edge");
        }

        /// <summary>
        /// A canvas scaled to two thirds — the 1280×720 factor — lays the board out at two thirds of the
        /// pixels, and the ends still land on the pieces' edges: the marks are placed in the container's
        /// own space and only then scaled.
        /// </summary>
        [UnityTest]
        public IEnumerator The_marks_hold_their_geometry_when_the_canvas_is_scaled_to_the_720p_factor()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

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

                Assert.That(_bag.VisibleLineCount, Is.EqualTo(1), "the resized canvas changed the number of lines");
                AssertEveryLineJoinsItsCells("at a canvas scale of " + factor.ToString("0.###"));
                AssertTheNamesAreDrawnOverTheLines();

                // The stage's thickness is a 1080p statement: at this factor the same line is two
                // thirds of that on screen, which is correct for a scaled canvas and is reported rather
                // than asserted against the 1080p number.
                Assert.That(_bag.TryGetLineStyle(0, out _, out _, out float thickness), Is.True);
                Assert.That(
                    thickness, Is.GreaterThanOrEqualTo(MinLineThickness),
                    "the line is " + thickness + " canvas units thick, below the " + MinLineThickness
                    + " px it is authored at for the 1080p reference");
            }
            finally
            {
                scaler.referenceResolution = original;
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// A mark has to be legible on a bag's green: a vivid red core, at least 0.9 opaque and at least
        /// 3 px thick at the 1080p reference, drawn over a darker edge that cuts it off from the board.
        /// </summary>
        [UnityTest]
        public IEnumerator A_line_is_a_thick_opaque_red_drawn_over_a_dark_edge()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

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
                Assert.That(edge.a, Is.GreaterThan(0.5f), "line " + i + "'s edge is see-through: " + edge);
                Assert.That(
                    Luminance(core) - Luminance(edge), Is.GreaterThan(0.3f),
                    "line " + i + "'s core and edge are too close in tone to read as a line on an edge");
            }
        }

        /// <summary>
        /// The layer the stage asks for: the lines are the board's <b>last</b> children, after every
        /// piece and the name inside it, so a mark that reaches a piece is seen reaching it.
        /// </summary>
        /// <remarks>
        /// Since Stage 09k the names live in a container of their own after the marks, because a line
        /// along the middle of a row would otherwise be painted across the middle of a name. The order
        /// this asserts is therefore: every piece, then the combo lines, then the badges, then the names.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_lines_are_drawn_after_every_piece_and_before_every_name()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            RectTransform container = _bag.LineContainer;
            Assert.That(container, Is.Not.Null, "the view has no line container");

            var board = container.parent as RectTransform;
            Assert.That(board, Is.Not.Null, "the line container is not under the board");

            int lineIndex = container.GetSiblingIndex();
            int pieces = 0;
            int names = 0;
            RectTransform labelRoot = null;

            for (int i = 0; i < board.childCount; i++)
            {
                var child = (RectTransform)board.GetChild(i);

                // A piece is the child the view creates for it; the cells, the ghosts and the mark
                // containers are not pieces and may sit either side of the marks.
                if (child.name == "Piece")
                {
                    pieces++;

                    Assert.That(
                        i, Is.LessThan(lineIndex),
                        child.name + " (a piece) is drawn at sibling " + i
                        + ", at or after the line container at " + lineIndex
                        + " — the line would run under the piece it names");
                }

                // The names live in one container of their own (Stage 09k), and it is the container
                // that has to sit above the marks: every box inside it then does too.
                if (child.name == "PieceLabels")
                {
                    labelRoot = child;

                    Assert.That(
                        i, Is.GreaterThan(lineIndex),
                        "the names are drawn at sibling " + i + ", before the line container at "
                        + lineIndex + " — the line would be painted over the word");
                }
            }

            Assert.That(pieces, Is.GreaterThanOrEqualTo(4), "the board drew too few pieces to judge the order");
            Assert.That(labelRoot, Is.Not.Null, "the board drew no name container above the marks");

            for (int i = 0; i < labelRoot.childCount; i++)
            {
                if (labelRoot.GetChild(i).name == "LabelBox")
                {
                    names++;
                }
            }

            Assert.That(names, Is.GreaterThanOrEqualTo(4), "the board drew too few names to judge the order");

            // Nothing else is drawn above the marks except the bag-area badges, which are the other
            // mark, and the names, which sit above both so a stroke runs behind the word.
            for (int i = lineIndex + 1; i < board.childCount; i++)
            {
                var child = (RectTransform)board.GetChild(i);
                Assert.That(
                    child.name == "BagBonusBadges" || child.name == "PieceLabels",
                    "'" + child.name + "' is drawn above the combo lines, which the stage does not allow");
            }
        }

        /// <summary>
        /// Stage 09i's name rule, kept: no name drawn inside a cell leaves the rectangle of its piece,
        /// every one of them is drawn between 14 and 22 px, and the full name is on the tooltip.
        /// </summary>
        [UnityTest]
        public IEnumerator A_name_in_a_cell_stays_inside_its_piece_and_is_never_below_14_px()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

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

                Assert.That(bounds.size.x, Is.GreaterThan(0f), "the name measured nothing: " + story);
                Assert.That(
                    bounds.min.x, Is.GreaterThanOrEqualTo(frame.xMin - 0.5f), "the name leaves its cell on the left: " + story);
                Assert.That(
                    bounds.max.x, Is.LessThanOrEqualTo(frame.xMax + 0.5f), "the name leaves its cell on the right: " + story);
                Assert.That(
                    bounds.min.y, Is.GreaterThanOrEqualTo(frame.yMin - 0.5f), "the name leaves its cell at the bottom: " + story);
                Assert.That(
                    bounds.max.y, Is.LessThanOrEqualTo(frame.yMax + 0.5f), "the name leaves its cell at the top: " + story);
            }

            Assert.That(measured, Is.GreaterThanOrEqualTo(2), "the board drew too few names to judge");

            // The full name the cell may not fit is the tooltip's job: hovering the tome publishes it.
            PieceRef tome = _tome;
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

        /// <summary>
        /// Stage 09j item B: a combo that works without a line still tells the player it is working —
        /// the tooltip's existing combo line names it — and the hover highlight still reaches the pair.
        /// </summary>
        [UnityTest]
        public IEnumerator A_combo_without_a_line_still_shows_on_the_tooltip_and_the_highlight()
        {
            yield return LoadCampWithTouchingAndDistantWeapons();

            // The far weapon: a working same-row pair with no permanent mark.
            CampPieceModel far = PieceOf(_farWeapon);
            _director.Presenter.Hover(_farWeapon);
            Canvas.ForceUpdateCanvases();
            yield return null;

            // Read from the tooltip the player is looking at, not from the model behind it: the claim
            // is that the screen says the combo is working even though the pair draws no line.
            var tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Assert.That(tooltip, Is.Not.Null, "the Camp scene has no CampTooltipView");

            string comboName = new LocalizationService(ShippedConfig(), Language.Th).Get("synergy.tome_row_damage.name");
            string combo = string.Empty;
            foreach (TMP_Text label in tooltip.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.gameObject.activeInHierarchy && label.text.Contains(comboName))
                {
                    combo = label.text;
                }
            }

            Assert.That(combo, Is.Not.Empty, "the tooltip of a piece with a working combo says nothing about it");
            Assert.That(
                combo.Contains("ตำราหนุนอาคม"),
                Is.True,
                "the tooltip does not name the same-row combo the far weapon is part of: '" + combo + "'");

            // The highlight: the hovered piece's cells plus its partner's — the tome, three cells to
            // the left, is the partner of a pair that draws no line.
            IReadOnlyList<CellOffset> highlight = _director.Presenter.Highlight;
            bool tomeCellLit = false;
            for (int i = 0; i < highlight.Count; i++)
            {
                if (highlight[i].X == 0 && highlight[i].Y == 1)
                {
                    tomeCellLit = true;
                }
            }

            Assert.That(
                tomeCellLit, Is.True,
                "hovering the far weapon did not highlight the tome's cell, so the pair is invisible in every way");
            Assert.That(far.Piece.IsValid, Is.True);
        }

        // ------------------------------------------------------------------
        // Reading the picture's own geometry
        // ------------------------------------------------------------------

        /// <summary>
        /// Every drawn line against the two <b>cells</b> the model names: each end is the middle of
        /// its cell (within 1 px), the stroke is level, and the two cells are neighbours on one row.
        /// </summary>
        private void AssertEveryLineJoinsItsCells(string state)
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

                Assert.That(
                    links[i].SourceCell.Y, Is.EqualTo(links[i].TargetCell.Y),
                    state + ": line " + drawn + "'s cells are not on one row: " + links[i]);
                Assert.That(
                    Mathf.Abs(links[i].SourceCell.X - links[i].TargetCell.X), Is.EqualTo(1),
                    state + ": line " + drawn + "'s cells are not neighbours: " + links[i]);

                Vector2 sourceMiddle = CellMiddle(links[i].SourceCell);
                Vector2 targetMiddle = CellMiddle(links[i].TargetCell);

                Assert.That(
                    Vector2.Distance(from, sourceMiddle), Is.LessThanOrEqualTo(TolerancePixels),
                    state + ": line " + drawn + " starts at " + from + " instead of the middle of "
                    + links[i].SourceCell + " at " + sourceMiddle);
                Assert.That(
                    Vector2.Distance(to, targetMiddle), Is.LessThanOrEqualTo(TolerancePixels),
                    state + ": line " + drawn + " ends at " + to + " instead of the middle of "
                    + links[i].TargetCell + " at " + targetMiddle);
                Assert.That(
                    Mathf.Abs(to.y - from.y), Is.LessThanOrEqualTo(TolerancePixels),
                    state + ": line " + drawn + " is not level: " + from + " -> " + to);

                drawn++;
            }

            Assert.That(
                drawn, Is.EqualTo(_bag.VisibleLineCount),
                state + ": the model lists " + drawn + " line(s) and the view drew " + _bag.VisibleLineCount);
        }

        /// <summary>The middle of a board cell on screen, which is where a line end has to be.</summary>
        private Vector2 CellMiddle(CellOffset cell)
        {
            Assert.That(
                _bag.TryGetBoardCellScreenPoint(cell.X, cell.Y, out Vector2 point), Is.True,
                "the board cell " + cell + " is not on screen");
            return point;
        }

        /// <summary>
        /// Stage 09k's answer to "the line must not cover a name": the names are drawn after the marks.
        /// </summary>
        /// <remarks>
        /// A stroke along the middle of a row passes through the cell a one-line name is centred in, so
        /// geometry alone cannot keep the two apart. What keeps the word readable is the layer: the
        /// label container is a later child of the board than the line container.
        /// </remarks>
        private void AssertTheNamesAreDrawnOverTheLines()
        {
            RectTransform container = _bag.LineContainer;
            Assert.That(container, Is.Not.Null, "the view has no line container");

            var board = container.parent as RectTransform;
            Assert.That(board, Is.Not.Null, "the line container is not under the board");

            int lineIndex = container.GetSiblingIndex();
            RectTransform labelRoot = null;

            for (int i = 0; i < board.childCount; i++)
            {
                var child = (RectTransform)board.GetChild(i);
                if (child.name != "PieceLabels")
                {
                    continue;
                }

                labelRoot = child;
                Assert.That(
                    i, Is.GreaterThan(lineIndex),
                    "the names are drawn at sibling " + i + ", before the line container at " + lineIndex);
            }

            Assert.That(labelRoot, Is.Not.Null, "no name container was found on the board, so nothing was checked");

            int names = 0;
            for (int i = 0; i < labelRoot.childCount; i++)
            {
                if (labelRoot.GetChild(i).name == "LabelBox")
                {
                    names++;
                }
            }

            Assert.That(names, Is.GreaterThan(0), "the name container holds no name box, so nothing was checked");
        }

        private static float Luminance(Color colour) =>
            (0.299f * colour.r) + (0.587f * colour.g) + (0.114f * colour.b);

        private static bool HasLine(CampLink[] links, int first, int second)
        {
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Style != CampLinkStyle.Line)
                {
                    continue;
                }

                if ((links[i].SourceInstanceId == first && links[i].TargetInstanceId == second)
                    || (links[i].SourceInstanceId == second && links[i].TargetInstanceId == first))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Describe(CampLink[] links)
        {
            if (links.Length == 0)
            {
                return "no links at all";
            }

            var written = new List<string>(links.Length);
            for (int i = 0; i < links.Length; i++)
            {
                written.Add(links[i].Style + ":" + links[i].SourceInstanceId + "->" + links[i].TargetInstanceId);
            }

            return string.Join(", ", written);
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
