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
    /// Stage 09l: the combo lines on the <b>real Camp scene</b> — which pairs get one, where the mark
    /// is drawn, what it looks like, and which layer it lives on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The board is built at runtime, so there is no rectangle in <c>Camp.unity</c> to measure: the
    /// scene is started and played with, exactly as a player would. The state is the stage's own case —
    /// a 2×2 tome with a weapon on <b>each of its four sides</b> and one weapon far away — built through
    /// the run's own commands (<c>MovePiece</c>) rather than by editing a model, so a rule that refused
    /// the layout fails this suite instead of being papered over.
    /// </para>
    /// <para>
    /// <b>The acceptance is geometry, so it is asserted in pixels.</b> Every line's two ends are read
    /// from the bar that was actually drawn (<c>TryGetLineScreenEnds</c>) and compared with the screen
    /// rectangles of the two pieces (<c>TryGetPieceScreenRect</c>): since this stage a mark runs from
    /// one piece's middle to the other's, whether the pair sits side by side or above one another.
    /// </para>
    /// </remarks>
    public sealed class CampSynergyLinePlayModeTests
    {
        private const string CampSceneName = "Camp";

        /// <summary>The tolerance the stage names: a line end may be off the middle it names by 1 px.</summary>
        private const float TolerancePixels = 1f;

        /// <summary>The smallest type an in-cell name may be drawn at (Stage 09i).</summary>
        private const float MinCellLabelSize = 14f;

        /// <summary>The largest type an in-cell name may be drawn at (the 22 px Thai floor).</summary>
        private const float MaxCellLabelSize = 22f;

        /// <summary>The thin, bright line the stage asks for: at least 3 px at the 1080p reference.</summary>
        private const float MinLineThickness = 3f;

        private const string TomeId = "item.spell_tome";
        private const string WeaponId = "item.kali_flame_yant";

        private CampSceneDirector _director;
        private CampBagScreenView _bag;
        private PieceRef _tome;
        private readonly PieceRef[] _weapons = new PieceRef[4];
        private PieceRef _farWeapon;

        /// <summary>
        /// Loads the camp, opens จัดย่าม, and builds the stage's state: four 3×3 รยาม in a square, a
        /// 2×2 tome at (1,2), a weapon on each of the tome's four sides, and one weapon far away.
        /// </summary>
        /// <remarks>
        /// Four bags because an item is only legal on a cell a bag covers
        /// (<c>inventory.item_not_covered</c>): the tome's four cells and the four cells around it need
        /// eight covered cells, and one bag is 3×3. The far weapon shares no edge with the tome, so it
        /// must draw nothing — the rule does not fire for it at all since ADR 0031.
        /// </remarks>
        private IEnumerator LoadCampWithAWeaponOnEverySideOfTheTome()
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

            // The bags first: everything else needs a cell one of them covers.
            PieceRef firstBag = run.Backpack.Bags[0].Ref;
            Assert.That(Move(run, firstBag, 0, 0), Is.True, "the starting รยาม could not be moved to (0,0)");

            var corners = new (int X, int Y)[] { (3, 0), (0, 3), (3, 3) };
            for (int i = 0; i < corners.Length; i++)
            {
                var added = run.Backpack.AddBag("bag.raw_cotton_bag");
                Assert.That(added.IsSuccess, Is.True, "a รยาม could not be added: " + added.Error);
                Assert.That(
                    Move(run, added.Value, corners[i].X, corners[i].Y), Is.True,
                    "a รยาม could not be placed at (" + corners[i].X + "," + corners[i].Y + ")");
            }

            // The tome: 2×2 at (1,2), so its four sides are (0,2), (3,2), (1,1) and (1,4).
            var tome = run.Backpack.AddItem(TomeId);
            Assert.That(tome.IsSuccess, Is.True, "the tome could not be added: " + tome.Error);
            _tome = tome.Value;
            Assert.That(Move(run, _tome, 1, 2), Is.True, "the 2x2 tome could not be placed at (1,2)");

            var sides = new (int X, int Y)[] { (3, 2), (0, 2), (1, 1), (1, 4) };
            for (int i = 0; i < sides.Length; i++)
            {
                PieceRef weapon = i == 0 ? run.Backpack.Items[0].Ref : AddWeapon(run);
                Assert.That(
                    Move(run, weapon, sides[i].X, sides[i].Y), Is.True,
                    "a weapon could not be placed at (" + sides[i].X + "," + sides[i].Y + ")");
                _weapons[i] = weapon;
            }

            _farWeapon = AddWeapon(run);
            Assert.That(Move(run, _farWeapon, 5, 4), Is.True, "the far weapon could not be placed at (5,4)");

            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private static PieceRef AddWeapon(RunSession run)
        {
            var added = run.Backpack.AddItem(WeaponId);
            Assert.That(added.IsSuccess, Is.True, "a weapon could not be added: " + added.Error);
            return added.Value;
        }

        /// <summary>The shipped tables — the config the camp itself runs on.</summary>
        private static IGameConfig ShippedConfig() => ResourcesGameConfigLoader.Load();

        /// <summary>The run's own placement command, so a layout this suite builds is one the game allows.</summary>
        private static bool Move(RunSession run, PieceRef piece, int x, int y) =>
            run.MovePiece(piece, new CellOffset(x, y), Rotation.Degrees0).IsSuccess;

        /// <summary>
        /// Stage 09l item B, as a count: a weapon on each of the tome's four sides is four working
        /// pairs and four lines, and the far weapon — which shares no edge — gets none.
        /// </summary>
        [UnityTest]
        public IEnumerator The_tome_gets_one_line_per_weapon_on_its_four_sides()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

            CampLink[] links = _bag.Model.Links;

            for (int i = 0; i < _weapons.Length; i++)
            {
                Assert.That(
                    HasLine(links, _tome.InstanceId, _weapons[i].InstanceId), Is.True,
                    "the weapon on side " + i + " of the tome has no line: " + Describe(links));
            }

            Assert.That(
                HasLine(links, _tome.InstanceId, _farWeapon.InstanceId), Is.False,
                "a line was drawn to a weapon the tome does not touch: " + Describe(links));
            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(4),
                "four touching pairs should be four lines, and the view drew " + _bag.VisibleLineCount);
        }

        /// <summary>
        /// Every line runs from the middle of one piece to the middle of the other (Stage 09l), read
        /// from the bar that was drawn and from the rectangles the player sees.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_line_runs_between_the_two_pieces_middles()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

            Assert.That(_bag.VisibleLineCount, Is.EqualTo(4), "there are not four lines to measure");
            AssertEveryLineJoinsItsPieces("a fresh board");
            AssertTheNamesAreDrawnOverTheLines();
        }

        /// <summary>
        /// Turning the tome re-draws the marks from the new geometry in the same frame: a 2×2 piece is
        /// the same box in every quarter turn, so all four pairs keep their line.
        /// </summary>
        [UnityTest]
        public IEnumerator Every_quarter_turn_of_the_tome_keeps_all_four_lines()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

            RunSession run = _director.Run;

            for (int turn = 0; turn < 4; turn++)
            {
                Assert.That(run.RotatePiece(_tome).IsSuccess, Is.True, "the tome could not be rotated");

                _director.Presenter.Refresh();
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.That(
                    _bag.VisibleLineCount, Is.EqualTo(4),
                    "a turn of the tome changed the number of lines to " + _bag.VisibleLineCount);
                AssertEveryLineJoinsItsPieces("after turning the tome " + (turn + 1) + " quarter(s)");
            }
        }

        /// <summary>
        /// The line goes the instant the pair stops touching and comes back when it touches again: one
        /// weapon is moved two cells away and then back, in the same camp visit.
        /// </summary>
        [UnityTest]
        public IEnumerator A_line_appears_and_disappears_with_the_pair_it_belongs_to()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

            RunSession run = _director.Run;
            PieceRef weapon = _weapons[0];

            Assert.That(Move(run, weapon, 5, 2), Is.True, "the weapon could not be moved two cells to the right");
            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                HasLine(_bag.Model.Links, _tome.InstanceId, weapon.InstanceId), Is.False,
                "the line survived the pair leaving contact: " + Describe(_bag.Model.Links));
            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(3),
                "the view drew " + _bag.VisibleLineCount + " lines for the three pairs still touching");

            Assert.That(Move(run, weapon, 3, 2), Is.True, "the weapon could not be moved back beside the tome");
            _director.Presenter.Refresh();
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(
                _bag.VisibleLineCount, Is.EqualTo(4),
                "the line did not come back when the weapon returned to the tome's edge, and the view drew "
                + _bag.VisibleLineCount);
            AssertEveryLineJoinsItsPieces("after the weapon returned to the tome's edge");
        }

        /// <summary>
        /// A canvas scaled to two thirds — the 1280×720 factor — lays the board out at two thirds of the
        /// pixels, and the ends still land on the pieces' middles: the marks are placed in the
        /// container's own space and only then scaled.
        /// </summary>
        [UnityTest]
        public IEnumerator The_marks_hold_their_geometry_when_the_canvas_is_scaled_to_the_720p_factor()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

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

                Assert.That(_bag.VisibleLineCount, Is.EqualTo(4), "the resized canvas changed the number of lines");
                AssertEveryLineJoinsItsPieces("at a canvas scale of " + factor.ToString("0.###"));
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
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

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
        /// The layer the stage asks for: the lines are drawn after every piece and before every name,
        /// so a mark that reaches a piece is seen reaching it and never covers a word.
        /// </summary>
        /// <remarks>
        /// Since Stage 09k the names live in a container of their own after the marks, because a line
        /// between two middles would otherwise be painted across the middle of a name. The order this
        /// asserts is therefore: every piece, then the combo lines, then the badges, then the names.
        /// </remarks>
        [UnityTest]
        public IEnumerator The_lines_are_drawn_after_every_piece_and_before_every_name()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

            RectTransform container = _bag.LineContainer;
            Assert.That(container, Is.Not.Null, "the view has no line container");

            var board = container.parent as RectTransform;
            Assert.That(board, Is.Not.Null, "the line container is not under the board");

            int lineIndex = container.GetSiblingIndex();
            int pieces = 0;
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

                if (child.name == "PieceLabels")
                {
                    labelRoot = child;

                    Assert.That(
                        i, Is.GreaterThan(lineIndex),
                        "the names are drawn at sibling " + i + ", before the line container at "
                        + lineIndex + " — the line would be painted over the word");
                }
            }

            Assert.That(pieces, Is.GreaterThanOrEqualTo(9), "the board drew too few pieces to judge the order");
            Assert.That(labelRoot, Is.Not.Null, "the board drew no name container above the marks");

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
        /// Stage 09i's name rule, kept: no name drawn inside its piece leaves that piece's rectangle,
        /// every one of them is drawn between 14 and 22 px, and the full name is on the tooltip.
        /// </summary>
        [UnityTest]
        public IEnumerator A_name_in_a_cell_stays_inside_its_piece_and_is_never_below_14_px()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

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
                string story = "'" + label.text + "' bounds " + bounds + " in a " + frame.size + " box";

                Assert.That(bounds.size.x, Is.GreaterThan(0f), "the name measured nothing: " + story);
                Assert.That(
                    bounds.min.x, Is.GreaterThanOrEqualTo(frame.xMin - 0.5f), "the name leaves its box on the left: " + story);
                Assert.That(
                    bounds.max.x, Is.LessThanOrEqualTo(frame.xMax + 0.5f), "the name leaves its box on the right: " + story);
                Assert.That(
                    bounds.min.y, Is.GreaterThanOrEqualTo(frame.yMin - 0.5f), "the name leaves its box at the bottom: " + story);
                Assert.That(
                    bounds.max.y, Is.LessThanOrEqualTo(frame.yMax + 0.5f), "the name leaves its box at the top: " + story);
            }

            Assert.That(measured, Is.GreaterThanOrEqualTo(4), "the board drew too few names to judge");

            // The full name the piece cannot fit is the tooltip's job: hovering the tome publishes it.
            CampPieceModel model = PieceOf(_tome);

            var tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Assert.That(tooltip, Is.Not.Null, "the Camp scene has no CampTooltipView");

            _director.Presenter.Hover(_tome);
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
        /// The combo a line marks is also named on the tooltip and lit on the board: hovering a weapon
        /// beside the tome says which combo is working and highlights the tome's cells with it.
        /// </summary>
        [UnityTest]
        public IEnumerator The_combo_a_line_marks_is_named_on_the_tooltip_and_lit_on_the_board()
        {
            yield return LoadCampWithAWeaponOnEverySideOfTheTome();

            PieceRef weapon = _weapons[0];
            _director.Presenter.Hover(weapon);
            Canvas.ForceUpdateCanvases();
            yield return null;

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

            // The highlight: the hovered piece's cells plus its partner's — the tome beside it.
            IReadOnlyList<CellOffset> highlight = _director.Presenter.Highlight;
            bool tomeCellLit = false;
            for (int i = 0; i < highlight.Count; i++)
            {
                if (highlight[i].X == 1 && highlight[i].Y == 2)
                {
                    tomeCellLit = true;
                }
            }

            Assert.That(
                tomeCellLit, Is.True,
                "hovering the weapon did not highlight the tome's cell, so the pair is invisible in every way");
        }

        // ------------------------------------------------------------------
        // Reading the picture's own geometry
        // ------------------------------------------------------------------

        /// <summary>
        /// Every drawn line against the two <b>pieces</b> the model names: each end is the middle of its
        /// own piece's rectangle on screen, within 1 px.
        /// </summary>
        private void AssertEveryLineJoinsItsPieces(string state)
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
                    state + ": line " + drawn + "'s source piece has no rectangle");
                Assert.That(
                    _bag.TryGetPieceScreenRect(target, out Rect targetRect), Is.True,
                    state + ": line " + drawn + "'s target piece has no rectangle");

                Assert.That(
                    Vector2.Distance(from, sourceRect.center), Is.LessThanOrEqualTo(TolerancePixels),
                    state + ": line " + drawn + " starts at " + from + " instead of the middle of its source "
                    + sourceRect.center);
                Assert.That(
                    Vector2.Distance(to, targetRect.center), Is.LessThanOrEqualTo(TolerancePixels),
                    state + ": line " + drawn + " ends at " + to + " instead of the middle of its target "
                    + targetRect.center);

                // Both ends are inside their own piece, which is what makes the mark a bridge between
                // two pieces rather than a stroke across a cell.
                Assert.That(sourceRect.Contains(from), Is.True, state + ": line " + drawn + " starts outside its piece");
                Assert.That(targetRect.Contains(to), Is.True, state + ": line " + drawn + " ends outside its piece");

                drawn++;
            }

            Assert.That(
                drawn, Is.EqualTo(_bag.VisibleLineCount),
                state + ": the model lists " + drawn + " line(s) and the view drew " + _bag.VisibleLineCount);
        }

        /// <summary>
        /// Stage 09k's answer to "the line must not cover a name": the names are drawn after the marks.
        /// </summary>
        /// <remarks>
        /// A stroke from one middle to another passes through the pieces it joins, so geometry alone
        /// cannot keep the two apart. What keeps the word readable is the layer: the label container is
        /// a later child of the board than the line container.
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
