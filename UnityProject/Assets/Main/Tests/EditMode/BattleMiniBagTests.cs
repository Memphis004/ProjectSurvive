using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 08c: the mini ย่าม and the cooldown plates it draws from a battle frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is under test is the <b>read model</b> and the <b>plate arithmetic</b> — the two halves a
    /// screenshot cannot check: that the box drawn is the bag's own footprint rather than the whole
    /// 9×7 board, that the pieces come from the same run the battle started from, that a piece gets a
    /// plate exactly when the loadout resolved it into a caster, and that a plate's fraction is the
    /// frame's <c>remaining / period</c> — nothing else.
    /// </para>
    /// <para>
    /// The picture half (no overlap at 1920×1080 and 1280×720, real cooldowns on screen) is
    /// <c>ProjectSurvive/Tools/1. Capture UI</c> and <c>ProjectSurvive/Tools/4. Capture the Battle HUD</c>,
    /// and the running battle half is <c>BattleHudPlayModeTests</c>.
    /// </para>
    /// </remarks>
    public sealed class BattleMiniBagTests
    {
        /// <summary>The seed every case here departs on: the shipped tables must build a real bag from it.</summary>
        private const uint Seed = 20261013;

        private static readonly int Capacity = BattleViewCaps.MiniBagCells;

        private readonly List<GameObject> _temporary = new List<GameObject>();
        private readonly List<System.IDisposable> _owned = new List<System.IDisposable>();

        private IGameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                _owned[i].Dispose();
            }

            _owned.Clear();

            for (int i = 0; i < _temporary.Count; i++)
            {
                Object.DestroyImmediate(_temporary[i]);
            }

            _temporary.Clear();
        }

        // -------------------------------------------------------------------
        // the read model
        // -------------------------------------------------------------------

        [Test]
        public void The_mini_bag_draws_the_bags_own_box_and_not_the_whole_board()
        {
            RunSession run = Depart(out BattleSetup setup);
            MiniBagLayout layout = MiniBagLayout.Build(run.Backpack, setup.Loadout, _config, 400f, 300f);

            BagPieceInstance bag = run.Backpack.Bags[0];
            int boardCells = _config.Constant.BoardWidth * _config.Constant.BoardHeight;

            Assert.That(layout.HasBag, Is.True, "the run departs with a bag on the board");
            Assert.That(
                layout.Columns,
                Is.EqualTo(bag.Shape.Width),
                "the box is the bag's own footprint, not the board's");
            Assert.That(layout.Rows, Is.EqualTo(bag.Shape.Height));
            Assert.That(
                layout.Columns * layout.Rows,
                Is.LessThan(boardCells),
                "a 3x3 bag is nine cells of a 63-cell board, and the mini bag draws nine");

            // The bag's own cells are the cloth; the rest of the box is not.
            int cloth = 0;
            for (int row = 0; row < layout.Rows; row++)
            {
                for (int column = 0; column < layout.Columns; column++)
                {
                    if (layout.IsBagCell(column, row))
                    {
                        cloth++;
                    }
                }
            }

            Assert.That(cloth, Is.EqualTo(bag.Shape.CellCount), "the cloth is the bag's own cells");
        }

        [Test]
        public void Every_piece_is_drawn_inside_the_bags_box_with_its_own_tier()
        {
            // The run opens with a bag holding the starting weapon; the charm is placed beside it so
            // the case covers a piece that cannot cast as well as one that can.
            RunSession run = Depart(out BattleSetup setup, "item.phra_amulet");
            MiniBagLayout layout = MiniBagLayout.Build(run.Backpack, setup.Loadout, _config, 400f, 300f);

            Assert.That(
                layout.PieceCount,
                Is.EqualTo(run.Backpack.Bags.Count + run.Backpack.Items.Count),
                "every board piece is on the mini bag (the two bags-and-items lists are the board)");

            for (int i = 0; i < layout.Pieces.Count; i++)
            {
                MiniBagPiece piece = layout.Pieces[i];

                Assert.That(piece.Column, Is.GreaterThanOrEqualTo(0));
                Assert.That(piece.Row, Is.GreaterThanOrEqualTo(0));
                Assert.That(
                    piece.Column + piece.Columns,
                    Is.LessThanOrEqualTo(layout.Columns),
                    piece.DefId + " is drawn outside the bag's box");
                Assert.That(
                    piece.Row + piece.Rows,
                    Is.LessThanOrEqualTo(layout.Rows),
                    piece.DefId + " is drawn outside the bag's box");
                Assert.That(piece.TierText, Is.EqualTo(piece.Tier.ToString()), "the tier number is the piece's own");
            }

            MiniBagPiece weapon = layout.Pieces[FindPiece(layout, setup.Loadout.Weapons[0].InstanceId)];
            Assert.That(weapon.Casts, Is.True, "the piece the loadout turned into a weapon is a caster");
            Assert.That(weapon.Tier, Is.EqualTo(setup.Loadout.Weapons[0].Tier));
        }

        /// <summary>
        /// An item's box shows its category's mark instead of its tier number, two categories never
        /// share a mark, and a bag shows neither a mark nor a number it would centre over a corner it
        /// does not cover (Stage 08i's second pass at the bag).
        /// </summary>
        /// <remarks>
        /// The point of the change is speed: the box's colour already says which kind of thing a piece
        /// is, but the five category colours are mid-tones that a colour-blind player, a night floor or
        /// a 1280 × 720 screenshot soften into each other. A silhouette survives all three.
        /// </remarks>
        [Test]
        public void An_items_box_shows_its_categorys_mark_and_a_corner_tier()
        {
            // Four items of four categories beside the starting weapon (ArcaneWeapon). The familiar
            // is left out on purpose: the starting ย่าม is 3 x 3 and this case needs the tome's four
            // cells to fit, so the fifth and sixth marks are covered by the glyph test below.
            RunSession run = Depart(
                out BattleSetup setup,
                "item.phra_amulet", // Charm
                "item.spell_tome", // SpellTome
                "item.red_soda"); // Offering

            BattleMiniBagView view = NewView(run, setup);
            Assert.That(view.PieceCount, Is.GreaterThanOrEqualTo(5), "the case did not get its pieces on the board");

            var marksByCategory = new Dictionary<ItemCategory, Sprite>();
            int itemsSeen = 0;
            int bagsSeen = 0;

            for (int i = 0; i < view.PieceCount; i++)
            {
                MiniBagPiece piece = view.Layout.Pieces[i];
                Assert.That(view.TryGetPieceGlyph(i, out Sprite mark), Is.True);
                Assert.That(view.TryGetPieceTierVisible(i, out bool tierVisible), Is.True);
                Assert.That(view.TryGetPiecePipsVisible(i, out bool pipsVisible), Is.True);
                Assert.That(view.TryGetPieceRect(i, out Rect box), Is.True);

                if (piece.Kind == PieceKind.Bag)
                {
                    bagsSeen++;
                    Assert.That(mark, Is.Null, "a bag shows a category mark it does not have");
                    Assert.That(pipsVisible, Is.False, "a bag carries tier pips it has no category for");
                    Assert.That(tierVisible, Is.True, "a bag lost its tier number");
                    continue;
                }

                itemsSeen++;
                Assert.That(mark, Is.Not.Null, piece.DefId + " shows no category mark");
                Assert.That(tierVisible, Is.False, piece.DefId + " still shows a digit over its mark");
                Assert.That(pipsVisible, Is.True, piece.DefId + " lost its tier");

                // The tier is a small strip in the corner, not the middle of the box: the mark owns
                // the centre. The strip is anchored to the box's bottom-right corner, so its offset
                // from that corner runs left (negative x) and up (positive y).
                Assert.That(view.TryGetPieceTierPips(i, out Sprite pips, out Vector2 centre, out Vector2 size), Is.True);
                Assert.That(pips, Is.Not.Null, piece.DefId + " has pips that draw nothing");
                Assert.That(
                    size.x, Is.LessThan(box.width * 0.5f),
                    piece.DefId + "'s tier strip is not a small corner mark");
                Assert.That(size.y, Is.LessThan(size.x), "the pip strip is not wider than it is tall");
                Assert.That(centre.x, Is.LessThan(0f), piece.DefId + "'s tier strip is not against the right edge");
                Assert.That(centre.y, Is.GreaterThan(0f), piece.DefId + "'s tier strip is not against the bottom edge");

                if (marksByCategory.TryGetValue(piece.Category, out Sprite known))
                {
                    Assert.That(mark, Is.SameAs(known), "two " + piece.Category + " pieces drew different marks");
                }
                else
                {
                    marksByCategory[piece.Category] = mark;
                }
            }

            Assert.That(itemsSeen, Is.GreaterThanOrEqualTo(4), "the case did not get four items on the board");
            Assert.That(bagsSeen, Is.GreaterThanOrEqualTo(1), "the case has no bag on the board");
            Assert.That(
                new HashSet<Sprite>(marksByCategory.Values).Count,
                Is.EqualTo(marksByCategory.Count),
                "two categories share one mark");
        }

        /// <summary>
        /// The pip strip says the tier: one dot per level, a different strip for every tier the run
        /// allows, and a strip with no dots is not shipped.
        /// </summary>
        [Test]
        public void The_tier_pips_grow_one_dot_per_level_and_stop_at_the_runs_ceiling()
        {
            var glyphs = new MiniBagGlyphs(maxTier: 3);

            try
            {
                var seen = new HashSet<Sprite>();

                for (int tier = 1; tier <= 3; tier++)
                {
                    Sprite pips = glyphs.TierPips(tier);
                    Assert.That(pips, Is.Not.Null, "tier " + tier + " has no pip strip");
                    Assert.That(seen.Add(pips), Is.True, "tier " + tier + " shares a strip with another tier");

                    // One dot per level, left to right, is what the sprite's own mask has to show.
                    Assert.That(DotCount(pips), Is.EqualTo(tier), "tier " + tier + "'s strip has the wrong dot count");
                }

                // Above the ceiling the strip saturates instead of inventing a stage the game cannot reach.
                Assert.That(glyphs.TierPips(9), Is.SameAs(glyphs.TierPips(3)), "a tier above the ceiling drew a new strip");
            }
            finally
            {
                glyphs.Dispose();
            }
        }

        /// <summary>Counts the runs of drawn pixels across a pip strip's middle row.</summary>
        private static int DotCount(Sprite strip)
        {
            Color32[] pixels = strip.texture.GetPixels32();
            int row = (MiniBagGlyphs.PipStripHeight / 2) * MiniBagGlyphs.Size;
            int dots = 0;
            bool inside = false;

            for (int x = 0; x < MiniBagGlyphs.Size; x++)
            {
                bool drawn = pixels[row + x].a > 0;
                if (drawn && !inside)
                {
                    dots++;
                }

                inside = drawn;
            }

            return dots;
        }

        /// <summary>
        /// Every category has its own mark, the fallback is its own too, and no mark is blank: a mark
        /// that draws nothing is a box with no way to tell what it holds.
        /// </summary>
        [Test]
        public void Every_category_has_its_own_mark_and_none_of_them_is_blank()
        {
            var glyphs = new MiniBagGlyphs(maxTier: 3);

            try
            {
                var categories = new[]
                {
                    ItemCategory.ArcaneWeapon,
                    ItemCategory.Charm,
                    ItemCategory.SpellTome,
                    ItemCategory.Offering,
                    ItemCategory.Familiar,
                };

                var seen = new HashSet<Sprite>();

                for (int i = 0; i < categories.Length; i++)
                {
                    Sprite mark = glyphs.For(categories[i]);
                    Assert.That(mark, Is.Not.Null, categories[i] + " has no mark");
                    Assert.That(seen.Add(mark), Is.True, categories[i] + " shares a mark with another category");
                    Assert.That(
                        mark.texture.width, Is.EqualTo(MiniBagGlyphs.Size),
                        categories[i] + "'s mark is not one generated mask");

                    Assert.That(
                        OpaqueShare(mark) > 0.12f, Is.True,
                        categories[i] + "'s mark covers almost nothing, so it would read as blank");
                }

                // An item the tables do not name still gets a mark, and it is not one of the five.
                Sprite fallback = glyphs.For(default);
                Assert.That(fallback, Is.Not.Null, "an unnamed item has no mark at all");
                Assert.That(seen.Contains(fallback), Is.False, "an unnamed item is drawn as a named category");
                Assert.That(OpaqueShare(fallback) > 0.12f, Is.True, "the fallback mark covers almost nothing");
            }
            finally
            {
                glyphs.Dispose();
            }
        }

        /// <summary>The share of a generated mask's pixels that are drawn at all.</summary>
        private static float OpaqueShare(Sprite sprite)
        {
            Color32[] pixels = sprite.texture.GetPixels32();
            int drawn = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                {
                    drawn++;
                }
            }

            return pixels.Length == 0 ? 0f : drawn / (float)pixels.Length;
        }

        [Test]
        public void A_piece_is_a_caster_exactly_when_the_loadout_resolved_it_into_one()
        {
            RunSession run = Depart(out BattleSetup setup, "item.phra_amulet");
            MiniBagLayout layout = MiniBagLayout.Build(run.Backpack, setup.Loadout, _config, 400f, 300f);

            int charmInstanceId = run.Backpack.Items[run.Backpack.Items.Count - 1].InstanceId;

            int expected = setup.Loadout.Weapons.Count + setup.Loadout.Summons.Count;
            Assert.That(
                layout.CasterCount,
                Is.EqualTo(expected),
                "a plate belongs to the weapons and familiars the loadout resolved, not to a category");

            for (int i = 0; i < layout.Pieces.Count; i++)
            {
                MiniBagPiece piece = layout.Pieces[i];
                bool inLoadout = MiniBagLayoutBuilder.Casts(setup.Loadout, piece.InstanceId);

                Assert.That(piece.Casts, Is.EqualTo(inLoadout), piece.DefId + " disagrees with the loadout");
            }

            // The charm is not in the loadout, so it carries no plate.
            Assert.That(
                layout.Pieces[FindPiece(layout, charmInstanceId)].Casts,
                Is.False,
                "a charm is not a caster");
        }

        [Test]
        public void The_cell_size_fits_the_area_it_was_handed_and_the_panels_two_sizes()
        {
            RunSession run = Depart(out BattleSetup setup);

            // The content box the scene authors (Stage 08d, re-cut by 08i): the panel is 288 x 216
            // at the 1920 x 1080 reference, and the grid's area runs from `LeftInset` on the left,
            // `BottomInset` at the bottom, to `ContentTop` (6 + 30 + 8) under the panel's top edge.
            // That is 268 x 162 in the canvas' own pixels. The canvas scalar draws the same rect 2/3
            // the size at 1280 x 720, which is why the second entry is the same layout measured in
            // screen pixels rather than a different layout.
            var sizes = new[]
            {
                new Vector2(268f, 162f),          // 1920x1080, UI reference pixels
                new Vector2(178.6667f, 108f),     // 1280x720, the same rect in screen pixels
            };

            // The largest bag the shipped data has is 3 x 3, so the cell is min(268/3, 162/3) = 54
            // reference px at 1080p and 36 screen px at 720p — both above the 16 px floor.
            float[] expected = { 54f, 36f };

            for (int i = 0; i < sizes.Length; i++)
            {
                MiniBagLayout layout = MiniBagLayout.Build(
                    run.Backpack, setup.Loadout, _config, sizes[i].x, sizes[i].y);

                Assert.That(
                    layout.CellSize,
                    Is.GreaterThanOrEqualTo(MiniBagLayoutBuilder.MinimumCellSize),
                    "a cell a player cannot read is not a cell");
                Assert.That(
                    layout.CellSize,
                    Is.LessThanOrEqualTo(MiniBagLayoutBuilder.MaximumCellSize));
                Assert.That(
                    layout.Width,
                    Is.LessThanOrEqualTo(sizes[i].x + 0.001f),
                    "the grid must fit the width it was handed");
                Assert.That(
                    layout.Height,
                    Is.LessThanOrEqualTo(sizes[i].y + 0.001f),
                    "the grid must fit the height it was handed");

                // The largest shipped bag (3 x 3) still leaves a readable cell at both sizes.
                Assert.That(
                    layout.CellSize, Is.EqualTo(expected[i]).Within(0.01f),
                    "the cell size for a 3 x 3 bag in this panel changed");
            }

            // A cell never grows past the ceiling: a one-cell bag is not drawn a quarter of a screen wide.
            Assert.That(
                MiniBagLayoutBuilder.Fit(400f, 300f, 1, 1),
                Is.EqualTo(MiniBagLayoutBuilder.MaximumCellSize));
        }

        [Test]
        public void The_panel_is_at_most_288_by_216_at_1080p_and_holds_the_largest_bag()
        {
            // Stage 08i: a named ceiling, not a number drifting in the scene. The largest shipped
            // bag is 3 x 3 (bag.raw_cotton_bag), and its 54 px cell clears the 16 px floor.
            Assert.That(BattleMiniBagView.PanelWidth, Is.EqualTo(288f));
            Assert.That(BattleMiniBagView.PanelHeight, Is.EqualTo(216f));

            float contentWidth = BattleMiniBagView.PanelWidth
                - BattleMiniBagView.LeftInset - BattleMiniBagView.RightInset;
            float contentHeight = BattleMiniBagView.PanelHeight
                - BattleMiniBagView.ContentTop - BattleMiniBagView.BottomInset;
            float cell = MiniBagLayoutBuilder.Fit(contentWidth, contentHeight, 3, 3);

            Assert.That(cell, Is.EqualTo(54f).Within(0.01f));
            Assert.That(cell, Is.GreaterThanOrEqualTo(MiniBagLayoutBuilder.MinimumCellSize));
            Assert.That(
                3f * cell, Is.LessThanOrEqualTo(contentWidth + 0.001f),
                "a 3 x 3 bag must not overflow the panel's width");
            Assert.That(
                3f * cell, Is.LessThanOrEqualTo(contentHeight + 0.001f),
                "a 3 x 3 bag must not overflow the panel's height");
        }

        [Test]
        public void The_first_box_hugs_the_content_top_left_at_both_panel_sizes()
        {
            RunSession run = Depart(out BattleSetup setup);

            var contents = new[]
            {
                new Vector2(268f, 162f),          // 1920x1080
                new Vector2(178.6667f, 108f),     // 1280x720 (the same rect, 2/3 the pixels)
            };

            for (int i = 0; i < contents.Length; i++)
            {
                MiniBagLayout layout = MiniBagLayout.Build(
                    run.Backpack, setup.Loadout, _config, contents[i].x, contents[i].y);

                Assert.That(layout.HasBag, Is.True);

                // The bag's own bounding box was normalised to its minimum cell, so the first piece
                // (the bag, which sorts before the items at the same row) starts at (0, 0) — the
                // content area's top-left corner — at either size.
                MiniBagPiece first = layout.Pieces[0];
                Assert.That(first.Kind, Is.EqualTo(PieceKind.Bag), "the bag box is drawn first");
                Assert.That(first.Column, Is.Zero, "the first box is not at the left edge");
                Assert.That(first.Row, Is.Zero, "the first box is not at the top edge");

                // The view writes that origin to the box's own rect: nothing is centred any more.
                BattleMiniBagView view = NewView(run.Backpack, setup.Loadout);
                view.Build(layout);

                Assert.That(view.TryGetPieceTopLeft(0, out Vector2 topLeft), Is.True);
                Assert.That(
                    topLeft,
                    Is.EqualTo(Vector2.zero),
                    "the first box is not at the content area's top-left corner");
            }
        }

        [Test]
        public void An_l_shaped_bag_hugs_the_top_left_of_its_own_bounding_box()
        {
            // bag.wicker_basket_l is the shipped "X.|X.|XX" piece: two columns wide, three tall, and
            // missing its top-right cell. Its bounding box is still what the mini bag draws, and that
            // box must start at the content area's corner rather than being centred because a corner
            // of it is empty.
            var backpack = new Backpack(_config);
            var editor = new BackpackEditor(backpack, _config, new MessagePackBackpackSnapshotCodec());

            Result<PieceRef> added = backpack.AddBag("bag.wicker_basket_l");
            Assert.That(added.IsSuccess, Is.True);
            Assert.That(
                editor.Move(added.Value, new CellOffset(2, 1), Rotation.Degrees0).IsSuccess,
                Is.True,
                "the L bag must fit the board for this case to mean anything");

            MiniBagLayout layout = MiniBagLayout.Build(backpack, null, _config, 268f, 162f);

            Assert.That(layout.Columns, Is.EqualTo(2));
            Assert.That(layout.Rows, Is.EqualTo(3));
            Assert.That(layout.IsBagCell(0, 0), Is.True);
            Assert.That(layout.IsBagCell(1, 0), Is.False, "the L's missing cell was filled in");
            Assert.That(layout.Pieces[0].Column, Is.Zero, "the L's box is not at the left edge");
            Assert.That(layout.Pieces[0].Row, Is.Zero, "the L's box is not at the top edge");

            BattleMiniBagView view = NewView(backpack, null);
            view.Build(layout);

            Assert.That(view.TryGetPieceTopLeft(0, out Vector2 topLeft), Is.True);
            Assert.That(topLeft, Is.EqualTo(Vector2.zero));

            // Stage 09l's rule, carried to the Battle HUD: the bag is drawn only by the cells it
            // covers, never by its bounding box, so the L's empty corner stays empty.
            Assert.That(view.TryGetPieceBoxDrawn(0, out bool drawn), Is.True);
            Assert.That(drawn, Is.False, "the L bag's bounding box was painted over its missing cell");
        }

        [Test]
        public void Two_bags_anchor_to_the_top_left_of_their_shared_bounding_box()
        {
            // The box is the union of every bag's cells, so a second bag only makes it bigger. Its
            // origin is still the top-left of that union — not the middle of the panel.
            var backpack = new Backpack(_config);
            var editor = new BackpackEditor(backpack, _config, new MessagePackBackpackSnapshotCodec());

            Result<PieceRef> cotton = backpack.AddBag("bag.raw_cotton_bag");
            Assert.That(cotton.IsSuccess, Is.True);
            Assert.That(editor.Move(cotton.Value, new CellOffset(0, 0), Rotation.Degrees0).IsSuccess, Is.True);

            Result<PieceRef> pouch = backpack.AddBag("bag.yant_cloth_pouch");
            Assert.That(pouch.IsSuccess, Is.True);
            Assert.That(editor.Move(pouch.Value, new CellOffset(4, 2), Rotation.Degrees0).IsSuccess, Is.True);

            MiniBagLayout layout = MiniBagLayout.Build(backpack, null, _config, 268f, 162f);

            Assert.That(layout.Columns, Is.EqualTo(5), "the union of a 3-wide and a 1-wide bag");
            Assert.That(layout.Rows, Is.EqualTo(5), "the union of a 3-tall and a 3-tall bag, offset by 2");
            Assert.That(layout.Pieces[0].Column, Is.Zero);
            Assert.That(layout.Pieces[0].Row, Is.Zero);

            BattleMiniBagView view = NewView(backpack, null);
            view.Build(layout);

            Assert.That(view.TryGetPieceTopLeft(0, out Vector2 topLeft), Is.True);
            Assert.That(topLeft, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void The_grid_starts_one_caption_gap_below_the_caption_and_on_its_left_edge()
        {
            // The scene's own arrangement: a panel a quarter of the canvas wide, the caption at the
            // panel's top-left, and the grid's area anchored through the view's own helper. What is
            // asserted is the arithmetic between the named constants, so changing one without the
            // others (the original defect) fails here rather than on a screenshot.
            var canvasObject = new GameObject("MiniBagCanvas", typeof(RectTransform), typeof(Canvas));
            _temporary.Add(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var panelObject = new GameObject("MiniBagPanel", typeof(RectTransform));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvasObject.transform, worldPositionStays: false);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(BattleMiniBagView.PanelWidth, BattleMiniBagView.PanelHeight);
            panel.anchoredPosition = new Vector2(
                -24f - (BattleMiniBagView.PanelWidth / 2f), 24f + (BattleMiniBagView.PanelHeight / 2f));

            // The caption's line box only — the words are the presenter's business and this case is
            // about geometry. `CreateUiRect` is the same helper `CreateUiText` builds the box with.
            RectTransform captionBox = ProjectSetup.CreateUiRect(
                panel, UiLayoutCheck.BagCaption, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(BattleMiniBagView.LeftInset, -BattleMiniBagView.CaptionTop),
                new Vector2(
                    BattleMiniBagView.PanelWidth - BattleMiniBagView.LeftInset - BattleMiniBagView.RightInset,
                    BattleMiniBagView.CaptionHeight));

            var contentObject = new GameObject(UiLayoutCheck.MiniBagContent, typeof(RectTransform));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(panel, worldPositionStays: false);
            BattleMiniBagView.AnchorContent(content);

            Canvas.ForceUpdateCanvases();

            Rect panelRect = panel.rect;
            Rect contentRect = content.rect;
            Rect captionRect = captionBox.rect;
            Vector2 contentOffset = content.offsetMin;
            Vector2 contentTop = content.offsetMax;

            // Both boxes are measured from the panel's top-left corner: the caption's own top stops
            // `CaptionTop` below it, and the grid's area starts a caption's line box plus one gap
            // below that.
            Assert.That(captionRect.height, Is.EqualTo(BattleMiniBagView.CaptionHeight).Within(0.001f));
            Assert.That(
                BattleMiniBagView.ContentTop,
                Is.EqualTo(BattleMiniBagView.CaptionTop + BattleMiniBagView.CaptionHeight + BattleMiniBagView.CaptionGap)
                    .Within(0.001f));
            Assert.That(
                BattleMiniBagView.CaptionGap,
                Is.InRange(8f, 12f),
                "the brief asks for 8-12 px between the caption and the bag at 1080p");

            // offsetMin.x and offsetMax.y are the two insets from the panel's left and top edges.
            Assert.That(contentOffset.x, Is.EqualTo(BattleMiniBagView.LeftInset).Within(0.001f));
            Assert.That(contentTop.y, Is.EqualTo(-BattleMiniBagView.ContentTop).Within(0.001f));
            Assert.That(
                -contentTop.y - BattleMiniBagView.CaptionTop - BattleMiniBagView.CaptionHeight,
                Is.EqualTo(BattleMiniBagView.CaptionGap).Within(0.001f),
                "the grid's area does not start one gap below the caption's line box");

            // The content's left edge is the caption's left edge, and its area is inside the panel.
            Assert.That(contentOffset.x, Is.EqualTo(BattleMiniBagView.LeftInset).Within(0.001f));
            Assert.That(contentRect.width, Is.EqualTo(panelRect.width - BattleMiniBagView.LeftInset - BattleMiniBagView.RightInset).Within(0.01f));
            Assert.That(contentRect.height, Is.EqualTo(panelRect.height - BattleMiniBagView.ContentTop - BattleMiniBagView.BottomInset).Within(0.01f));
        }

        [Test]
        public void A_board_with_no_bag_has_nothing_to_draw()
        {
            var run = new RunSession(_config, new MessagePackBackpackSnapshotCodec(), Seed);

            MiniBagLayout layout = MiniBagLayout.Build(run.Backpack, null, _config, 400f, 300f);

            Assert.That(layout.HasBag, Is.False, "a run that has not started has no bag on the board");
            Assert.That(layout.PieceCount, Is.Zero);
        }

        [Test]
        public void The_pools_cover_the_board_so_a_floor_never_creates_a_view()
        {
            Assert.That(
                BattleViewCaps.MiniBagCells,
                Is.GreaterThanOrEqualTo(_config.Constant.BoardWidth * _config.Constant.BoardHeight),
                "the board is the largest box a bag can cover");
            Assert.That(
                BattleViewCaps.MiniBagPieces,
                Is.GreaterThanOrEqualTo(BattleViewCaps.MiniBagCells),
                "a piece occupies at least one cell, so no floor can hold more pieces than cells");
            Assert.That(Capacity, Is.EqualTo(BattleViewCaps.MiniBagCells));
        }

        // -------------------------------------------------------------------
        // the plates: the frame's numbers, and nothing else
        // -------------------------------------------------------------------

        [TestCase(10, 10, 1f, MiniBagPlateTone.Counting)]
        [TestCase(5, 10, 0.5f, MiniBagPlateTone.Counting)]
        [TestCase(1, 10, 0.1f, MiniBagPlateTone.Counting)]
        [TestCase(0, 10, 0f, MiniBagPlateTone.None)]
        public void A_plate_fraction_is_the_frames_remaining_ticks_over_its_period(
            int remaining, int period, float expected, MiniBagPlateTone tone)
        {
            RunSession run = Depart(out BattleSetup setup);
            int caster = setup.Loadout.Weapons[0].InstanceId;

            BattleMiniBagView view = NewView(run, setup);

            view.ApplyCooldowns(One(new LoadoutCooldown(caster, remaining, period)));

            Assert.That(view.TryGetPlate(caster, out float fraction, out MiniBagPlateTone shown), Is.True);
            Assert.That(fraction, Is.EqualTo(expected).Within(0.0001f), "the plate is the frame's own fraction");
            Assert.That(shown, Is.EqualTo(tone));
        }

        [Test]
        public void A_plate_is_written_only_when_the_frame_says_something_new()
        {
            RunSession run = Depart(out BattleSetup setup);
            int caster = setup.Loadout.Weapons[0].InstanceId;

            BattleMiniBagView view = NewView(run, setup);

            view.ApplyCooldowns(One(new LoadoutCooldown(caster, 8, 10)));
            int afterFirst = view.PlateUpdates;
            Assert.That(afterFirst, Is.EqualTo(1), "the first frame writes the plate");

            // Same tick, twice: the plate is already what the frame says.
            view.ApplyCooldowns(One(new LoadoutCooldown(caster, 8, 10)));
            view.ApplyCooldowns(One(new LoadoutCooldown(caster, 8, 10)));
            Assert.That(view.PlateUpdates, Is.EqualTo(afterFirst), "an unchanged fraction must not rewrite the plate");

            view.ApplyCooldowns(One(new LoadoutCooldown(caster, 7, 10)));
            Assert.That(view.PlateUpdates, Is.EqualTo(afterFirst + 1), "a tick that moved the cooldown writes it once");
        }

        [Test]
        public void A_ready_caster_that_cannot_pay_is_flagged_and_toned_blue()
        {
            RunSession run = Depart(out BattleSetup setup);
            int caster = setup.Loadout.Weapons[0].InstanceId;

            BattleMiniBagView view = NewView(run, setup);

            view.ApplyCooldowns(One(new LoadoutCooldown(
                caster, 0, 10, LoadoutCooldownFlags.ManaShort)));

            Assert.That(view.TryGetPlate(caster, out float fraction, out MiniBagPlateTone tone), Is.True);
            Assert.That(fraction, Is.Zero, "a ready-but-blocked entry is at zero ticks left");
            Assert.That(tone, Is.EqualTo(MiniBagPlateTone.ManaShort));
            Assert.That(view.ManaShortCount, Is.EqualTo(1));

            // With MP to pay, the same entry is simply ready: no plate at all.
            view.ApplyCooldowns(One(new LoadoutCooldown(caster, 0, 10)));
            Assert.That(view.ManaShortCount, Is.Zero, "a cast the pool can pay for is not flagged");
            Assert.That(view.TryGetPlate(caster, out _, out MiniBagPlateTone ready), Is.True);
            Assert.That(ready, Is.EqualTo(MiniBagPlateTone.None));
        }

        [Test]
        public void A_cooldown_entry_this_bag_does_not_hold_is_ignored()
        {
            RunSession run = Depart(out BattleSetup setup);
            int caster = setup.Loadout.Weapons[0].InstanceId;

            BattleMiniBagView view = NewView(run, setup);

            // An entry with no board piece (a hand-built loadout publishes instance id 0) and one for a
            // piece that is not on this bag: neither may throw or write anything.
            view.ApplyCooldowns(new[]
            {
                new LoadoutCooldown(LoadoutCooldown.NoInstance, 3, 10),
                new LoadoutCooldown(9999, 3, 10),
            });

            Assert.That(view.PlateUpdates, Is.Zero, "nothing was written for entries this bag does not hold");
            Assert.That(view.TryGetPlate(9999, out _, out _), Is.False);

            view.ApplyCooldowns(One(new LoadoutCooldown(caster, 4, 10)));
            Assert.That(view.PlateUpdates, Is.EqualTo(1));
        }

        [Test]
        public void The_view_builds_every_object_once_and_never_again()
        {
            RunSession run = Depart(out BattleSetup setup);

            BattleMiniBagView view = NewView(run, setup);
            MiniBagLayout outline = view.Layout;

            int built = outline.PieceCount;
            Assert.That(built, Is.GreaterThan(0), "the floor departed with pieces on the board");
            Assert.That(view.CreatedAfterBuild, Is.Zero, "the pool was created during the build, not after it");
            Assert.That(view.CellCount, Is.EqualTo(outline.Columns * outline.Rows));

            int pieces = view.PieceCount;
            int cells = view.CellCount;

            // Applying a whole floor's worth of frames must not create anything either.
            for (int tick = 0; tick < 300; tick++)
            {
                view.ApplyCooldowns(One(new LoadoutCooldown(
                    setup.Loadout.Weapons[0].InstanceId, 30 - (tick % 30), 30)));
            }

            Assert.That(view.CreatedAfterBuild, Is.Zero, "a floor must not create a view while it runs");
            Assert.That(view.PieceCount, Is.EqualTo(pieces));
            Assert.That(view.CellCount, Is.EqualTo(cells));
        }

        /// <summary>
        /// The cloth reads against the panel it sits in, against its own empty cells, and does not
        /// swallow the item boxes drawn on it (Stage 08i's second pass at the bag).
        /// </summary>
        /// <remarks>
        /// A photograph is what showed the first cut's problem — a tan cloth at (0.30, 0.28, 0.22) on
        /// a (0.07, 0.08, 0.11) panel at 0.72 alpha measured **1.38 : 1** against its own panel and
        /// 1.22 : 1 against the floor, so the bag was harder to find than the slab under it. The
        /// numbers here are WCAG ratios of the <b>composited</b> pixels: each palette colour over the
        /// panel, and the panel over the arena, blended as the UI blends them (straight sRGB, which
        /// under-states the separation in a linear framebuffer rather than flattering it). Two arena
        /// tones stand in for the floor the bag is photographed over: the capture's own bright tone
        /// and a night-dusk one, because the panel is transparent and the floor is what changes.
        /// </remarks>
        [Test]
        public void The_bags_cloth_reads_against_its_panel_and_its_empty_cells()
        {
            var arenaTones = new[]
            {
                new Color(0.314f, 0.337f, 0.169f), // the dusk arena, sampled from the 1920x1080 capture
                new Color(0.13f, 0.15f, 0.08f), // its night tone
            };

            for (int i = 0; i < arenaTones.Length; i++)
            {
                Color panel = Over(MiniBagPalette.Backdrop, arenaTones[i]);
                Color cloth = Over(MiniBagPalette.Bag, panel);
                Color empty = Over(MiniBagPalette.EmptyCell, panel);

                Assert.That(
                    Contrast(cloth, panel), Is.GreaterThanOrEqualTo(1.8f),
                    "the bag's cloth is not separable from its own panel over arena tone " + i);
                Assert.That(
                    Contrast(cloth, empty), Is.GreaterThanOrEqualTo(1.8f),
                    "a cell the bag covers and one it does not look the same over arena tone " + i);

                // The closest category colour to the cloth is the offering's gold; if that box still
                // reads, the four cooler ones do too. A box that vanishes on the cloth is a piece the
                // player cannot see, which is the whole reason the bag came back.
                Color offering = Over(new Color(0.70f, 0.62f, 0.32f, 0.95f), cloth);
                Assert.That(
                    Contrast(offering, cloth), Is.GreaterThanOrEqualTo(1.4f),
                    "the offering's box is lost on the cloth over arena tone " + i);
            }
        }

        /// <summary>Composites a UI colour over an opaque one, the way the canvas blends it.</summary>
        private static Color Over(Color colour, Color behind) => new Color(
            (colour.r * colour.a) + (behind.r * (1f - colour.a)),
            (colour.g * colour.a) + (behind.g * (1f - colour.a)),
            (colour.b * colour.a) + (behind.b * (1f - colour.a)),
            1f);

        /// <summary>The WCAG contrast ratio of two composited colours (the measure the arena uses).</summary>
        private static float Contrast(Color first, Color second) =>
            ArenaContrast.Ratio(
                ArenaContrast.RelativeLuminance(Rgb(first)),
                ArenaContrast.RelativeLuminance(Rgb(second)));

        private static Color32 Rgb(Color colour) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.b) * 255f),
            255);

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        /// <summary>
        /// Starts a run, places whatever the case needs, and departs for floor 1 — exactly the shape
        /// the Battle scene's own bootstrap plays (<c>BattleRunSource.TryStartFrom</c>).
        /// </summary>
        /// <remarks>
        /// The extra pieces are added and placed <b>before</b> the departure because the bag is the
        /// camp's to edit: once a floor has departed, the run refuses a move ("ย่ามแก้ไขได้เฉพาะที่ศาลาพัก").
        /// </remarks>
        private RunSession Depart(out BattleSetup setup, params string[] extraItems)
        {
            var run = new RunSession(_config, new MessagePackBackpackSnapshotCodec(), Seed);
            Assert.That(run.StartRun().IsSuccess, Is.True, "the run must start on the shipped tables");

            for (int i = 0; i < extraItems.Length; i++)
            {
                Result<PieceRef> added = run.Backpack.AddItem(extraItems[i]);
                Assert.That(added.IsSuccess, Is.True, added.IsFailure ? added.Error.Code : string.Empty);
                Assert.That(
                    TryPlaceAnywhere(run, added.Value),
                    Is.True,
                    extraItems[i] + " must fit on the board for this case to mean anything");
            }

            Assert.That(
                BattleRunSource.TryStartFrom(run, _config, equipSecondWeapon: false, out setup, out _, out string error),
                Is.True,
                error);
            return run;
        }

        /// <summary>A view on a canvas of its own, wired and built the way the Battle scene does it.</summary>
        private BattleMiniBagView NewView(RunSession run, BattleSetup setup) =>
            NewView(run.Backpack, setup.Loadout);

        /// <summary>The same view over any bag (the L-shaped case has no departed run to read one from).</summary>
        private BattleMiniBagView NewView(Backpack bag, CombatLoadoutSnapshot loadout)
        {
            var canvasObject = new GameObject("MiniBagCanvas", typeof(RectTransform), typeof(Canvas));
            _temporary.Add(canvasObject);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var panelObject = new GameObject("MiniBagPanel", typeof(RectTransform));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvasObject.transform, worldPositionStays: false);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(BattleMiniBagView.PanelWidth, BattleMiniBagView.PanelHeight);
            panel.anchoredPosition = new Vector2(
                -24f - (BattleMiniBagView.PanelWidth / 2f), 24f + (BattleMiniBagView.PanelHeight / 2f));

            // The same anchoring the scene builder applies (Stage 08d), from the same helper.
            var contentObject = new GameObject(UiLayoutCheck.MiniBagContent, typeof(RectTransform));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(panel, worldPositionStays: false);
            BattleMiniBagView.AnchorContent(content);

            var view = panelObject.AddComponent<BattleMiniBagView>();

            // The two references ProjectSetup authors through SerializedProperty, set the same way here.
            SetReference(view, "_panel", panel);
            SetReference(view, "_content", content);

            view.Build(bag, loadout, _config);
            return view;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool TryPlaceAnywhere(RunSession run, PieceRef piece)
        {
            for (int y = 0; y < run.Backpack.Board.Height; y++)
            {
                for (int x = 0; x < run.Backpack.Board.Width; x++)
                {
                    if (run.MovePiece(piece, new CellOffset(x, y), Rotation.Degrees0).IsSuccess)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int FindPiece(MiniBagLayout layout, int instanceId)
        {
            for (int i = 0; i < layout.Pieces.Count; i++)
            {
                if (layout.Pieces[i].InstanceId == instanceId)
                {
                    return i;
                }
            }

            Assert.Fail("instance " + instanceId + " is not on the mini bag");
            return -1;
        }

        private static LoadoutCooldown[] One(in LoadoutCooldown entry) =>
            new[] { entry };
    }
}
