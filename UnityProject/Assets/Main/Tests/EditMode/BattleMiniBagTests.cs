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
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation.Battle;
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
        public void Every_piece_on_the_bag_is_drawn_inside_the_box_with_its_tier()
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

            // The two panels the scene authors: a quarter of the canvas minus the margins, and the
            // content box below the caption (Stage 08c's own numbers — the same ones
            // `ProjectSurvive/Tools/4. Capture the Battle HUD` re-measures on the real screen).
            var sizes = new[]
            {
                new Vector2(432f, 500f), // 1920x1080: (0.25 * 1920) - 48 wide, and a tall box
                new Vector2(272f, 300f), // 1280x720:  (0.25 * 1280) - 48 wide
            };

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
                Assert.That(layout.OffsetX, Is.GreaterThanOrEqualTo(0f));
            }

            // A cell never grows past the ceiling: a one-cell bag is not drawn a quarter of a screen wide.
            Assert.That(
                MiniBagLayoutBuilder.Fit(400f, 300f, 1, 1),
                Is.EqualTo(MiniBagLayoutBuilder.MaximumCellSize));
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
        private BattleMiniBagView NewView(RunSession run, BattleSetup setup)
        {
            var canvasObject = new GameObject("MiniBagCanvas", typeof(RectTransform), typeof(Canvas));
            _temporary.Add(canvasObject);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var panelObject = new GameObject("MiniBagPanel", typeof(RectTransform));
            var panel = (RectTransform)panelObject.transform;
            panel.SetParent(canvasObject.transform, worldPositionStays: false);
            panel.anchorMin = new Vector2(0f, 0.45f);
            panel.anchorMax = new Vector2(0.25f, 1f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.offsetMin = new Vector2(24f, 24f);
            panel.offsetMax = new Vector2(-24f, -24f);

            var contentObject = new GameObject("MiniBagContent", typeof(RectTransform));
            var content = (RectTransform)contentObject.transform;
            content.SetParent(panel, worldPositionStays: false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(10f, 10f);
            content.offsetMax = new Vector2(-10f, -42f);

            var view = panelObject.AddComponent<BattleMiniBagView>();

            // The two references ProjectSetup authors through SerializedProperty, set the same way here.
            SetReference(view, "_panel", panel);
            SetReference(view, "_content", content);

            view.Build(run.Backpack, setup.Loadout, _config);
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
