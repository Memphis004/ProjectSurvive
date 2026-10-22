using System;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Domain.Shop;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// Publishes the camp screen's state and its layout to the page, so a browser test can drive
    /// the real UI and check the run it left behind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A WebGL player cannot run Unity's test framework, so the only place the acceptance
    /// "ซื้อ → วาง → หมุน → รวม → ถวายคืน → คืนสภาพ ผ่าน UI แล้ว state ตรงกับ Core" can be observed
    /// end to end is a browser (the same reason Stage 03/08 publish their smoke and battle
    /// reports). Drag the mouse at a cell the report names, click a button the scene drew, then
    /// read back what the run holds.
    /// </para>
    /// <para>
    /// <b>Two globals, not one.</b> The layout (where each cell is on screen) changes when the
    /// canvas is resized; the state changes when a command moved something. Keeping them apart is
    /// what lets the state report stay small, and what keeps the layout from being rebuilt four
    /// times a second while a piece is being dragged — the stage's "no GC spike while dragging"
    /// is measured against a build whose only per-interaction cost is one comparison.
    /// </para>
    /// <para>
    /// <b>Nothing is published while a drag is in progress.</b>
    /// <see cref="CampSceneDirector"/> calls <see cref="Key"/> first and republishes only when the
    /// key changed and no gesture is live, so a thousand pointer moves over one cell build no
    /// strings at all: the run did not change, so there is nothing new to say.
    /// </para>
    /// </remarks>
    public static class CampReport
    {
        /// <summary>Page global carrying the camp's state.</summary>
        public const string StateGlobal = "__projectSurviveCamp";

        /// <summary>Page global carrying the camp's on-screen layout (cell positions).</summary>
        public const string GeometryGlobal = "__projectSurviveCampGeometry";

        /// <summary>
        /// The names the layout report uses for the scene's controls, so a browser test clicks the
        /// control by name rather than at a coordinate it guessed from a screenshot.
        /// </summary>
        /// <remarks>
        /// Per shop slot there are two: <c>rent0</c>..<c>rent3</c> and <c>pin0</c>..<c>pin3</c>.
        /// </remarks>
        public static class Control
        {
            public const string OpenBag = "openBag";
            public const string CloseBag = "closeBag";
            public const string Rotate = "rotate";
            public const string Undo = "undo";
            public const string Restore = "restore";
            public const string LockBag = "lockBag";
            /// <summary>The จัดย่าม toggle's own name, so a test can open the panel by clicking it.</summary>
            public const string BagToggle = "bagToggle";

            /// <summary>The เซียมซี cylinder in the sala (Stage 09c work item 4).</summary>
            public const string ResetDrum = "resetDrum";

            /// <summary>The player's own figure.</summary>
            public const string Player = "player";

            /// <summary>Shelf station <c>n</c> in the sala (<c>shelf0</c>..).</summary>
            public static string Shelf(int index) => "shelf" + index;

            /// <summary>Shelf station <c>n</c>'s lock control (<c>shelfLock0</c>..).</summary>
            public static string ShelfLock(int index) => "shelfLock" + index;

            /// <summary>ล็อกย่ามตอนคืนสภาพ — the checkbox beside the restore button.</summary>
            public const string LockRestore = "lockRestore";

            /// <summary>The panel's own way out of camp (ไปชั้นถัดไป).</summary>
            public const string NextFloor = "nextFloor";

            /// <summary>
            /// Whichever button departs is on top where the player can reach it: the panel's
            /// ไปชั้นถัดไป while the panel is open, otherwise the bottom bar's ออกเดินทาง.
            /// </summary>
            public const string Depart = "depart";

            /// <summary>The bottom bar's own background, named so the closed layout can be measured.</summary>
            public const string StatusBar = "statusBar";

            /// <summary>The rent button of one shop slot (<c>rent0</c>..).</summary>
            public static string Rent(int slot) => "rent" + slot;

            /// <summary>The pin button of one shop slot (<c>pin0</c>..).</summary>
            public static string Pin(int slot) => "pin" + slot;
        }

        /// <summary>The most recent state report, in-process. The PlayMode tests read this.</summary>
        public static string LastStateJson { get; private set; } = string.Empty;

        /// <summary>The most recent geometry report, in-process.</summary>
        public static string LastGeometryJson { get; private set; } = string.Empty;

        /// <summary>How many state reports have been published. A test proves freshness with it.</summary>
        public static int StatePublishCount { get; private set; }

        /// <summary>Clears both mirrors (a test starts from a known empty state).</summary>
        public static void Reset()
        {
            LastStateJson = string.Empty;
            LastGeometryJson = string.Empty;
            StatePublishCount = 0;
        }

        /// <summary>
        /// A cheap change key over everything the state report contains, so a publish is one
        /// comparison rather than one JSON document per frame.
        /// </summary>
        public static int Key(
            RunSession run, CampPresenter presenter, CampPlayerView player = null, CampWorldView world = null)
        {
            int hash = run.Backpack.Revision;

            // The sala's own state: the player walked, or the focus changed. Walking republishes at
            // the report's interval (four times a second), which is what lets a browser test see
            // the figure move — and the position is folded at quarter-unit resolution so a standstill
            // publishes nothing at all.
            if (player != null)
            {
                hash = Fold(hash, Mathf.RoundToInt(player.Position.x * 4f));
                hash = Fold(hash, Mathf.RoundToInt(player.Position.z * 4f));
                hash = Fold(hash, player.FocusedShelf);
                hash = Fold(hash, player.ResetFocused ? 1 : 0);
                hash = Fold(hash, player.Moving ? 1 : 0);
                hash = Fold(hash, player.WalkingBlocked ? 1 : 0);
                hash = Fold(hash, HashOf(player.LastAction));
            }

            hash = Fold(hash, run.Wallet.Bia);
            hash = Fold(hash, (int)run.FlowState);
            hash = Fold(hash, run.FloorIndex);
            hash = Fold(hash, run.ChestUsed);
            hash = Fold(hash, run.Hp);
            hash = Fold(hash, HashOf(presenter.StatusCode));
            hash = Fold(hash, presenter.Departed ? 1 : 0);

            // The selection is drawn (the piece is painted in the selected colour), so the document
            // has to follow it: a click that only selected a piece used to leave the report saying
            // "nothing is selected", and a browser test cannot aim a key at a piece it cannot see.
            hash = Fold(hash, (int)presenter.Selected.Kind);
            hash = Fold(hash, presenter.Selected.InstanceId);

            CampCounters counters = presenter.Counters;
            hash = Fold(hash, counters.Rented);
            hash = Fold(hash, counters.Refreshes);
            hash = Fold(hash, counters.SlotLocks);
            hash = Fold(hash, counters.Placed);
            hash = Fold(hash, counters.Stored);
            hash = Fold(hash, counters.Merged);
            hash = Fold(hash, counters.Recycled);
            hash = Fold(hash, counters.Rotated);
            hash = Fold(hash, counters.Resets);
            hash = Fold(hash, counters.Undos);
            hash = Fold(hash, counters.LockToggles);
            hash = Fold(hash, counters.DragPreviews);
            hash = Fold(hash, counters.InvalidPreviews);
            hash = Fold(hash, counters.InvalidDrops);
            hash = Fold(hash, counters.DragMoves);
            hash = Fold(hash, counters.DragRotations);
            hash = Fold(hash, counters.DragCancels);
            hash = Fold(hash, counters.Departures);

            ShopStock stock = run.Stock;
            for (int i = 0; i < stock.SlotCount; i++)
            {
                ShopSlot slot = stock[i];
                hash = Fold(hash, HashOf(slot.OfferedId));
                hash = Fold(hash, slot.Price);
                hash = Fold(hash, slot.IsPurchased ? 1 : 0);
                hash = Fold(hash, slot.IsLocked ? 2 : 0);
            }

            return hash;
        }

        /// <summary>
        /// Builds and publishes the state report. Returns the JSON it published.
        /// </summary>
        public static string PublishState(
            RunSession run, CampPresenter presenter, string sceneName, string startError,
            CampPlayerView player, CampWorldView world,
            bool saveFailed = false, string saveWarning = "", string saveError = "")
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName);
            report.Write("publishCount", StatePublishCount + 1);
            report.Write("startError", startError ?? string.Empty);

            // --- the sala the player walks (Stage 09c) ---
            report.Write("bagVisible", player != null && player.WalkingBlocked);
            report.Write("moving", player != null && player.Moving);
            report.Write("playerX", player == null ? 0f : player.Position.x);
            report.Write("playerZ", player == null ? 0f : player.Position.z);
            report.Write("focusedShelf", player == null ? -1 : player.FocusedShelf);
            report.Write("resetFocused", player != null && player.ResetFocused);
            report.Write("lastWorldAction", player == null ? string.Empty : player.LastAction);
            report.Write("worldRents", player == null ? 0 : player.RentRequests);
            report.Write("worldLocks", player == null ? 0 : player.LockRequests);
            report.Write("worldResets", player == null ? 0 : player.ResetRequests);
            report.Write("shelfCount", world == null ? 0 : world.ShelfCount);

            report.Write("flow", run.FlowState.ToString());
            report.Write("floor", run.FloorIndex);
            report.Write("totalFloors", run.TotalFloors);
            report.Write("hp", run.Hp);
            report.Write("maxHp", run.MaxHp);
            report.Write("bia", run.Wallet.Bia);
            report.Write("weight", run.BagWeight);
            report.Write("weightPenaltyPercent", run.BagWeightPenaltyPercent);
            report.Write("focusPerSecond", run.FocusPerSecond);
            report.Write("chestUsed", run.ChestUsed);
            report.Write("chestSlots", run.ChestSlots);
            report.Write("bagLocked", run.IsBagLocked);
            report.Write("canUndoEdit", run.CanUndoEdit);
            report.Write("backpackRevision", run.Backpack.Revision);
            report.Write("boardWidth", run.Backpack.Board.Width);
            report.Write("boardHeight", run.Backpack.Board.Height);
            report.Write("selectedPiece", presenter.Selected.ToString());
            report.Write("statusCode", presenter.StatusCode);
            report.Write("dragging", presenter.IsDragging);

            // Stage 12: a failed write is visible on the screen and in the document, and it never
            // reads as a success. The warning is the localized line the player sees.
            report.Write("saveFailed", saveFailed);
            report.Write("saveWarning", saveWarning ?? string.Empty);
            report.Write("saveError", saveError ?? string.Empty);

            // Stage 13b: the first-run checklist. "active" is what decides whether the instruction
            // block is a checklist at all; "step" is the next step the player has to do, 1..5.
            report.Write("tutorialActive", presenter.Tutorial.IsActive);
            report.Write("tutorialComplete", presenter.Tutorial.IsComplete);
            report.Write("tutorialDone", presenter.Tutorial.DoneCount);
            report.Write("tutorialStep", (int)presenter.Tutorial.Current + 1);

            report.Write("departed", presenter.Departed);
            report.Write("departFloor", presenter.DepartFloor);
            report.Write("departSeed", presenter.DepartSeed.ToString());

            Result departCheck = run.DepartCheck();
            report.Write("canDepart", departCheck.IsSuccess);
            report.Write("departError", departCheck.IsSuccess ? string.Empty : departCheck.Error.Code);

            PublishCounters(report, presenter.Counters);
            PublishShop(report, run);
            PublishPieces(report, presenter.Bag);

            report.EndObject();

            string json = report.ToString();
            LastStateJson = json;
            StatePublishCount++;
            WebGlBridge.Publish(StateGlobal, json);
            return json;
        }

        /// <summary>
        /// Builds and publishes where every board cell, chest slot, altar pot and control is on
        /// screen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Screen pixels with the origin at the <b>bottom-left</b>, which is the convention Unity
        /// itself reports; a browser test converts once, using the canvas element's own box, so the
        /// conversion keeps working at any resolution and either pixel ratio.
        /// </para>
        /// <para>
        /// The controls are in here for one reason: a browser test has to press the *real* button
        /// (work item 6's depart guard, work item 2's backup rotate button, the restore button) and
        /// a click needs somewhere to land. Their positions come from the views, so a scene that
        /// moved a button moves the report with it.
        /// </para>
        /// </remarks>
        public static string PublishGeometry(
            CampBagScreenView bag, CampWorldView world, CampPlayerView player, CampStatusBarView status)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);
            report.Write("bagVisible", bag != null && bag.PanelVisible);

            // The sala's own geometry, which changes as the follow camera moves: a browser test has
            // to aim a click at where a shelf is *now*, not where it was when the page loaded.
            PublishShelves(report, world);
            PublishWorldPoints(report, world, player);
            report.Write("cellPitch", bag.CellPitch);
            report.Write("boardWidth", bag.BoardWidth);
            report.Write("boardHeight", bag.BoardHeight);
            report.Write("chestColumns", bag.ChestColumns);
            report.Write("chestSlots", bag.ChestSlotCount);
            report.Write("pieceViews", bag.PieceViewCount);

            report.BeginArrayProperty("cells");
            for (int row = 0; row < bag.BoardHeight; row++)
            {
                for (int column = 0; column < bag.BoardWidth; column++)
                {
                    if (!bag.TryGetBoardCellScreenPoint(column, row, out Vector2 screen))
                    {
                        continue;
                    }

                    report.BeginObject();
                    report.Write("column", column);
                    report.Write("row", row);
                    report.Write("x", screen.x);
                    report.Write("y", screen.y);
                    report.EndObject();
                }
            }

            report.EndArray();

            report.BeginArrayProperty("chest");
            for (int slot = 0; slot < bag.ChestSlotCount; slot++)
            {
                if (!bag.TryGetChestSlotScreenPoint(slot, out Vector2 screen))
                {
                    continue;
                }

                report.BeginObject();
                report.Write("slot", slot);
                report.Write("x", screen.x);
                report.Write("y", screen.y);
                report.EndObject();
            }

            report.EndArray();

            if (bag.TryGetAltarScreenPoint(out Vector2 altar))
            {
                report.BeginObjectProperty("altar");
                report.Write("x", altar.x);
                report.Write("y", altar.y);
                report.EndObject();
            }

            PublishRects(report, bag, world, player, status);
            PublishControls(report, bag, world, status);

            report.EndObject();

            string json = report.ToString();
            LastGeometryJson = json;
            WebGlBridge.Publish(GeometryGlobal, json);
            return json;
        }

        /// <summary>
        /// One rectangle per named region and control, so a test can check the layout instead of
        /// describing it: Stage 09b's "ต้องไม่ทับกันที่ 1920×1080 และ 1280×720" is a statement about
        /// these rectangles, and pushing them to the page is what lets the browser test measure the
        /// same ones the player sees.
        /// </summary>
        /// <remarks>
        /// Screen pixels, origin bottom-left, measured from each transform's four world corners — so
        /// a control's rectangle is its rectangle, not the point its pivot happens to sit on.
        /// </remarks>
        /// <summary>
        /// One rectangle per shelf station and its lock control, so a browser test can see where
        /// each station is without reading a screenshot.
        /// </summary>
        private static void PublishShelves(JsonReportWriter report, CampWorldView world)
        {
            report.BeginArrayProperty("shelves");

            if (world != null)
            {
                for (int i = 0; i < world.ShelfCount; i++)
                {
                    Vector3 anchor = world.ShelfPosition(i);

                    report.BeginObject();
                    report.Write("index", i);

                    // Where the station stands in the sala, not only where it lands on screen: a
                    // browser test that walks the figure has to know which column to walk to, and
                    // the player's own world position is published beside it.
                    report.Write("worldX", anchor.x);
                    report.Write("worldZ", anchor.z);

                    if (!world.TryGetShelfScreenRect(i, out Rect station))
                    {
                        report.EndObject();
                        continue;
                    }

                    report.Write("x", station.x);
                    report.Write("y", station.y);
                    report.Write("width", station.width);
                    report.Write("height", station.height);

                    if (world.TryGetShelfLockScreenRect(i, out Rect lockRect))
                    {
                        report.Write("lockX", lockRect.x);
                        report.Write("lockY", lockRect.y);
                        report.Write("lockWidth", lockRect.width);
                        report.Write("lockHeight", lockRect.height);
                    }

                    report.EndObject();
                }
            }

            report.EndArray();
        }

        /// <summary>Where the player and the เซียมซี cylinder are on screen right now.</summary>
        private static void PublishWorldPoints(
            JsonReportWriter report, CampWorldView world, CampPlayerView player)
        {
            if (world != null)
            {
                // The area the figure may walk in, in world XZ. A browser test that walks to a shelf
                // has to know the furniture is not walkable — the shelf row stands behind the area's
                // far edge — or its walk would never arrive and it would report a walking bug.
                Rect walk = world.WalkArea;
                report.BeginObjectProperty("walk");
                report.Write("xMin", walk.xMin);
                report.Write("xMax", walk.xMax);
                report.Write("zMin", walk.yMin);
                report.Write("zMax", walk.yMax);
                report.EndObject();
            }

            if (world != null && world.TryGetPlayerScreenRect(out Rect playerRect))
            {
                report.BeginObjectProperty("player");
                report.Write("x", playerRect.x);
                report.Write("y", playerRect.y);
                report.Write("width", playerRect.width);
                report.Write("height", playerRect.height);
                report.Write("worldX", player == null ? 0f : player.Position.x);
                report.Write("worldZ", player == null ? 0f : player.Position.z);
                report.EndObject();
            }

            if (world != null && world.TryGetResetScreenRect(out Rect reset))
            {
                report.BeginObjectProperty("resetDrum");
                report.Write("x", reset.x);
                report.Write("y", reset.y);
                report.Write("width", reset.width);
                report.Write("height", reset.height);
                report.EndObject();
            }
        }

        private static void PublishRects(
            JsonReportWriter report, CampBagScreenView bag, CampWorldView world, CampPlayerView player,
            CampStatusBarView status)
        {
            report.BeginArrayProperty("rects");

            var surfaces = new (string Name, CampBagSurface Surface)[]
            {
                ("board", CampBagSurface.Board),
                ("chestPanel", CampBagSurface.ChestPanel),
                ("chest", CampBagSurface.Chest),
                ("altar", CampBagSurface.Altar),
                ("instructions", CampBagSurface.Instructions),
                ("stats", CampBagSurface.Stats),
            };

            if (bag != null)
            {
                for (int i = 0; i < surfaces.Length; i++)
                {
                    if (bag.TryGetSurfaceScreenRect(surfaces[i].Surface, out Rect surface))
                    {
                        RectOf(report, surfaces[i].Name, surface);
                    }
                }

                var controls = new (string Name, CampBagButton Button)[]
                {
                    (Control.OpenBag, CampBagButton.Open),
                    (Control.CloseBag, CampBagButton.Close),
                    (Control.Rotate, CampBagButton.Rotate),
                    (Control.Undo, CampBagButton.Undo),
                    (Control.Restore, CampBagButton.Reset),
                    (Control.LockBag, CampBagButton.Lock),
                    (Control.LockRestore, CampBagButton.LockRestore),
                    (Control.NextFloor, CampBagButton.NextFloor),
                };

                for (int i = 0; i < controls.Length; i++)
                {
                    if (bag.TryGetButtonScreenRect(controls[i].Button, out Rect control))
                    {
                        RectOf(report, controls[i].Name, control);
                    }
                }
            }

            if (status != null && status.TryGetPanelScreenRect(out Rect bar))
            {
                RectOf(report, Control.StatusBar, bar);
            }

            // The sala's pieces are in the same list, each tagged with its layer: the overlap rule
            // ("ภาพหน้าจอไม่มี element ทับกัน") is a statement about two UI controls, while a world
            // station passing under the bottom bar is what a walking player does.
            if (world != null)
            {
                for (int i = 0; i < world.ShelfCount; i++)
                {
                    if (world.TryGetShelfScreenRect(i, out Rect station))
                    {
                        RectOf(report, Control.Shelf(i), station, "world");
                    }

                    if (world.TryGetShelfLockScreenRect(i, out Rect lockRect))
                    {
                        RectOf(report, Control.ShelfLock(i), lockRect, "world");
                    }
                }

                if (world.TryGetResetScreenRect(out Rect drum))
                {
                    RectOf(report, Control.ResetDrum, drum, "world");
                }

                if (world.TryGetPlayerScreenRect(out Rect figure))
                {
                    RectOf(report, Control.Player, figure, "world");
                }
            }

            report.EndArray();
        }

        private static void RectOf(JsonReportWriter report, string name, Rect rect, string layer = "ui")
        {
            report.BeginObject();
            report.Write("name", name);
            report.Write("layer", layer);
            report.Write("x", rect.x);
            report.Write("y", rect.y);
            report.Write("width", rect.width);
            report.Write("height", rect.height);
            report.EndObject();
        }

        private static void PublishControls(
            JsonReportWriter report, CampBagScreenView bag, CampWorldView world, CampStatusBarView status)
        {
            report.BeginArrayProperty("buttons");

            if (bag != null)
            {
                Named(report, Control.OpenBag, bag.TryGetButtonScreenPoint(CampBagButton.Open, out Vector2 open), open);
                Named(report, Control.CloseBag, bag.TryGetButtonScreenPoint(CampBagButton.Close, out Vector2 close), close);
                Named(report, Control.Rotate, bag.TryGetButtonScreenPoint(CampBagButton.Rotate, out Vector2 rotate), rotate);
                Named(report, Control.Undo, bag.TryGetButtonScreenPoint(CampBagButton.Undo, out Vector2 undo), undo);
                Named(report, Control.Restore, bag.TryGetButtonScreenPoint(CampBagButton.Reset, out Vector2 restore), restore);
                Named(report, Control.LockBag, bag.TryGetButtonScreenPoint(CampBagButton.Lock, out Vector2 lockBag), lockBag);
            }

            // The rent and lock targets are the sala's stations now, not panel buttons (Stage 09c:
            // the shop list exists once, in the world). The names stay the same so a browser test
            // that clicked "rent0" still names the same thing.
            if (world != null)
            {
                for (int slot = 0; slot < world.ShelfCount; slot++)
                {
                    Named(report, Control.Rent(slot), world.TryGetShelfScreenRect(slot, out Rect rent), rent.center);
                    Named(report, Control.Pin(slot), world.TryGetShelfLockScreenRect(slot, out Rect pin), pin.center);
                }

                Named(report, Control.ResetDrum, world.TryGetResetScreenRect(out Rect drum), drum.center);
            }

            if (bag != null)
            {
                Named(report, Control.LockRestore,
                    bag.TryGetButtonScreenPoint(CampBagButton.LockRestore, out Vector2 lockRestore),
                    lockRestore);
                Named(report, Control.NextFloor,
                    bag.TryGetButtonScreenPoint(CampBagButton.NextFloor, out Vector2 nextFloor),
                    nextFloor);
            }

            bool departFound = false;
            if (bag != null && bag.TryGetDepartButtonScreenPoint(out Vector2 bagDepart))
            {
                Named(report, Control.Depart, true, bagDepart);
                departFound = true;
            }

            if (!departFound && status != null)
            {
                Named(report, Control.Depart, status.TryGetDepartButtonScreenPoint(out Vector2 depart), depart);
            }

            report.EndArray();
        }

        /// <summary>Writes one control, or nothing when the view could not place it on screen.</summary>
        private static void Named(JsonReportWriter report, string name, bool found, Vector2 screen)
        {
            if (!found)
            {
                return;
            }

            report.BeginObject();
            report.Write("name", name);
            report.Write("x", screen.x);
            report.Write("y", screen.y);
            report.EndObject();
        }

        private static void PublishCounters(JsonReportWriter report, CampCounters counters)
        {
            report.BeginObjectProperty("counters");
            report.Write("rented", counters.Rented);
            report.Write("refreshes", counters.Refreshes);
            report.Write("slotLocks", counters.SlotLocks);
            report.Write("placed", counters.Placed);
            report.Write("stored", counters.Stored);
            report.Write("merged", counters.Merged);
            report.Write("recycled", counters.Recycled);
            report.Write("rotated", counters.Rotated);
            report.Write("resets", counters.Resets);
            report.Write("undos", counters.Undos);
            report.Write("lockToggles", counters.LockToggles);
            report.Write("dragPreviews", counters.DragPreviews);
            report.Write("invalidPreviews", counters.InvalidPreviews);
            report.Write("invalidDrops", counters.InvalidDrops);
            report.Write("dragMoves", counters.DragMoves);
            report.Write("dragRotations", counters.DragRotations);
            report.Write("dragCancels", counters.DragCancels);
            report.Write("departures", counters.Departures);
            report.EndObject();
        }

        private static void PublishShop(JsonReportWriter report, RunSession run)
        {
            ShopStock stock = run.Stock;

            report.BeginArrayProperty("shop");
            for (int i = 0; i < stock.SlotCount; i++)
            {
                ShopSlot slot = stock[i];

                report.BeginObject();
                report.Write("index", i);
                report.Write("pieceId", slot.OfferedId);
                report.Write("kind", slot.Kind.ToString());
                report.Write("price", slot.Price);
                report.Write("empty", slot.IsEmpty);
                report.Write("purchased", slot.IsPurchased);
                report.Write("locked", slot.IsLocked);
                report.Write("affordable", run.Wallet.CanAfford(slot.Price));
                report.EndObject();
            }

            report.EndArray();
        }

        /// <summary>
        /// Every piece the run holds, with the identity and the placement a check needs.
        /// </summary>
        /// <remarks>
        /// Read from the run, not from the view: this is the report a test compares Core against,
        /// so it must not be able to agree with a view that drew something else.
        /// </remarks>
        private static void PublishPieces(JsonReportWriter report, CampBagScreen bag)
        {
            report.BeginArrayProperty("pieces");

            CampPieceModel[] pieces = bag.Pieces;
            for (int i = 0; i < pieces.Length; i++)
            {
                CampPieceModel piece = pieces[i];

                report.BeginObject();
                report.Write("kind", piece.Kind.ToString());
                report.Write("instanceId", piece.Piece.InstanceId);
                report.Write("defId", piece.DefId);
                report.Write("tier", piece.Tier);
                report.Write("zone", piece.Zone.ToString());
                report.Write("x", piece.Origin.X);
                report.Write("y", piece.Origin.Y);
                report.Write("rotation", (int)piece.Rotation);
                report.Write("chestSlot", piece.IsOnBoard ? -1 : piece.ChestSlot);
                report.Write("cells", piece.Shape?.CellCount ?? 0);
                report.EndObject();
            }

            report.EndArray();
        }

        private static int Fold(int hash, int value) => unchecked((hash * 31) + value);

        /// <summary>
        /// A stable hash of a string. Ordinal and explicit rather than
        /// <c>string.GetHashCode</c>, which is randomized per process and would make the key
        /// differ between two runs that hold the same state.
        /// </summary>
        private static int HashOf(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            int hash = 17;
            for (int i = 0; i < value!.Length; i++)
            {
                hash = unchecked((hash * 31) + value[i]);
            }

            return hash;
        }
    }
}
