using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.SpineActors;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Toggle = UnityEngine.UI.Toggle;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds ศาลาพัก: the sala the player walks (ground, ยายจันทร์'s stall, the four shelf
    /// stations and the เซียมซี cylinder), the figure, the จัดย่าม bag screen, the bottom bar and
    /// the depart button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One file per scene (Stage 10t): the construction, the screens it is made of and the menu
    /// item that builds it live together, so the scene can be dropped by deleting this file. The
    /// helpers it uses are <see cref="ProjectSetup"/>'s.
    /// </para>
    /// <para>
    /// <b>The shop is in the world, not in a panel.</b> Stage 09c moved the stall out of the UI:
    /// each shop slot is a shelf standing in the sala with its own price board and lock control,
    /// so there is no second list of offers in the report or on the screen. Everything the
    /// player needs without walking is still reachable without walking — a shelf can be rented
    /// by clicking it (what a phone and a browser test use) and the จัดย่าม button is on the
    /// canvas, not in the sala. The bag screen itself is the one thing that opens and closes,
    /// and it starts closed.
    /// </para>
    /// </remarks>
    public static class CampSceneBuilder
    {
        /// <summary>
        /// The camp tooltip canvas' draw order. Above the camp screen's own canvas (0) and above
        /// everything it contains — the board, the chest, the incense pot and the bottom bar — because
        /// the engine sorts overlay canvases by <c>Canvas.sortingOrder</c> before anything else.
        /// </summary>
        private const int TooltipSortingOrder = 100;

        [MenuItem("ProjectSurvive/Stage 09/5. Create Camp scene", priority = 104)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Camp scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();

            var scopeObject = new GameObject("Camp");
            CampSceneScope scope = scopeObject.AddComponent<CampSceneScope>();
            CampSceneDirector director = scopeObject.AddComponent<CampSceneDirector>();

            Camera camera = ProjectSetup.CreateCamera();
            camera.transform.position = new Vector3(0f, 7.2f, -12.7f);
            camera.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 6.6f;

            ProjectSetup.CreateEventSystem();

            // The sala is a view, not editor-authored sprites: it generates its own art at runtime
            // (AGENTS rule 13), so the scene carries no placeholder binaries. It is built once in
            // Awake and the camera follows the player underneath it.
            var worldObject = new GameObject("CampWorld");
            CampWorldView world = worldObject.AddComponent<CampWorldView>();
            ProjectSetup.SetObjectReference(world, "_scope", scope);
            ProjectSetup.SetObjectReference(world, "_camera", camera);
            ProjectSetup.SetObjectReference(world, "_font", font);

            // Stage 17c: the doctor is the Spine figure, built by the world when the camp starts.
            // The scene names the same profile the arena uses (one class, one look, ADR 0037 §2); a
            // missing profile is an error here rather than a sala that quietly draws the stand-in.
            var playerProfile =
                AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(SpineProfileTool.ProfileAssetPath);
            if (playerProfile == null)
            {
                Debug.LogError(
                    "[CampSceneBuilder] " + SpineProfileTool.ProfileAssetPath + " is missing: run " +
                    "ProjectSurvive/Spine/2. Create player profile. The sala will fall back to the " +
                    "placeholder silhouette for the player.");
            }

            ProjectSetup.SetObjectReference(world, "_spineProfile", playerProfile);

            // The walker: a figure the player drives with WASD, standing in the sala. It owns the
            // walking and the pose it asks the world's figure to draw (Stage 17c); the speed and the
            // reach are the run's own tuning, from data/tables/constant.csv (AGENTS rule 5).
            var playerObject = new GameObject("CampPlayer");
            CampPlayerView walker = playerObject.AddComponent<CampPlayerView>();
            ProjectSetup.SetObjectReference(walker, "_scope", scope);
            ProjectSetup.SetObjectReference(walker, "_world", world);

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            CampStatusBarView status = BuildCampStatusBar(canvasRect, font, scope);
            CampTooltipView tooltip = BuildCampTooltip(font, scope);
            CampBagScreenView bag = BuildCampBagScreen(canvasRect, canvasObject, font, scope);

            // Stage 12: a failed checkpoint write is visible in the sala, not just in the log.
            TMP_Text saveWarning = ProjectSetup.CreateTmpLabel(
                canvas, "SaveWarning", new Vector2(0f, 300f), 26f, font, new Vector2(1200f, 40f));
            saveWarning.color = new Color(1f, 0.62f, 0.32f, 1f);

            // A readout must not eat the sala's clicks. This label is a 1200 × 40 band across the
            // shelf row, and TMP makes a new text a raycast target by default: with the default on,
            // every click on a shelf became "pointer over UI" and rented nothing (found by
            // CampWorldPlayModeTests.Clicking_a_shelf_rents_it_without_walking).
            saveWarning.raycastTarget = false;

            ProjectSetup.SetObjectReference(walker, "_bag", bag);
            ProjectSetup.SetObjectReference(director, "_scope", scope);
            ProjectSetup.SetObjectReference(director, "_world", world);
            ProjectSetup.SetObjectReference(director, "_player", walker);
            ProjectSetup.SetObjectReference(director, "_bag", bag);
            ProjectSetup.SetObjectReference(director, "_status", status);
            ProjectSetup.SetObjectReference(director, "_tooltip", tooltip);
            ProjectSetup.SetObjectReference(director, "_saveWarning", saveWarning);

            // Stage 09o: the yard. The same card/decal/shadow prefabs and lighting profile the arena
            // uses, placed around the sala and saved <b>into this scene</b> — so the yard exists in
            // the asset, is destroyed with the scene, and nothing is Instantiated or Destroyed while
            // the game runs.
            CampEnvironmentBuilder.Build(camera, world);

            EditorSceneManager.SaveScene(scene, ProjectSetup.CampScenePath);
        }

        /// <summary>
        /// The จัดย่าม screen overlay: the board (left), the chest (right), the altar pot
        /// (bottom-centre), instructions (top-right), restore/lock with its checkbox (top-left),
        /// and navigation/stats (bottom-left).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every region is anchored to a <b>corner or edge of the canvas</b> — top-left, top-right,
        /// bottom-left, bottom-centre — and the canvas scaler (1920×1080, match 0.5) turns that into
        /// the same proportions at any window size, so no element is placed by an absolute screen
        /// pixel. The numbers below are in reference units and are measured rather than guessed: at
        /// 1920×1080 the rows are 20..106 from the top (the top-left controls), 140..720 from the top
        /// (the board) and 232..340, 214..320, 16..206 from the bottom (the panel's own foot, the
        /// pot and the status bar), the columns are 40..780, 822..1032 and 1104..1880, and no two of
        /// the regions the capture command measures intersect — at either size, which
        /// <c>artifacts/ui/ui-report.md</c> records.
        /// </para>
        /// <para>
        /// <b>Nothing here repeats the bottom bar.</b> Stage 09's panel carried its own เบี้ย/น้ำหนัก/HP
        /// readouts while the bar under it carried the same numbers, and the two disagreed the moment
        /// one of them was stale; the panel now has the controls the bar does not (กลับ, ไปชั้นถัดไป
        /// and the reason it is disabled) and the bar stays the only place a run stat is shown.
        /// </para>
        /// <para>
        /// <b>The blocks stand clear of each other and of the bar.</b> The panel's foot sits above the
        /// status bar rather than on it (Stage 09e work item 1: the buttons and the run's own numbers
        /// must not share pixels), and the incense pot sits in the column the bar does not reach —
        /// between the panel's foot (ends at x 780) and the bar's ออกเดินทาง button (starts at 1074) —
        /// so the pot cannot cover the one control that leaves the camp.
        /// </para>
        /// <para>
        /// Every label is ≥ 22 px at the 1080p base (Stage 09d item 2). Nothing here writes player
        /// text: the strings arrive over <c>CampUiState</c> from the localization table
        /// (AGENTS rule 5), and this method only places the widgets that will receive them.
        /// </para>
        /// </remarks>
        private static CampBagScreenView BuildCampBagScreen(
            RectTransform canvasRect, GameObject canvasObject, TMP_FontAsset font, CampSceneScope scope)
        {
            // Fullscreen overlay panel. Translucent rather than opaque: the layout is an overlay on
            // ศาลาพัก, so the camp has to stay visible behind it ("เห็นฉากอยู่ด้านหลัง").
            RectTransform panel = ProjectSetup.CreateUiRect(
                canvasRect, "BagPanel", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.pivot = new Vector2(0.5f, 0.5f);

            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.08f, 0.10f, 0.62f);
            panelImage.raycastTarget = true;

            // View is attached directly to the panel so it receives all graphic raycasts.
            CampBagScreenView view = panel.gameObject.AddComponent<CampBagScreenView>();

            // --- Top-left row (y 20..160): title, restore + its checkbox, lock, rotate, undo ------
            TMP_Text title = ProjectSetup.CreateUiText(panel, "BagTitle", new Vector2(40f, -20f), new Vector2(200f, 44f), 26f, font);
            Button reset = ProjectSetup.CreateUiButton(panel, "ResetButton", new Vector2(250f, -20f), new Vector2(150f, 44f), font, out TMP_Text resetLabel);
            Button lockButton = ProjectSetup.CreateUiButton(panel, "LockButton", new Vector2(415f, -20f), new Vector2(240f, 44f), font, out TMP_Text lockLabel);
            Button rotate = ProjectSetup.CreateUiButton(panel, "RotateButton", new Vector2(670f, -20f), new Vector2(90f, 44f), font, out TMP_Text rotateLabel);
            Button undo = ProjectSetup.CreateUiButton(panel, "UndoButton", new Vector2(775f, -20f), new Vector2(100f, 44f), font, out TMP_Text undoLabel);

            // ล็อกย่ามตอนคืนสภาพ sits directly under the restore button it modifies. A real Toggle,
            // not a caption that looks like a checkbox: the restore button reads this control's
            // value, so an unchecked box and a checked box are two different commands.
            Toggle lockRestore = CreateUiToggle(
                panel, "LockRestoreToggle", new Vector2(250f, -72f), new Vector2(30f, 30f), font,
                new Vector2(292f, -72f), new Vector2(400f, 34f), out TMP_Text lockRestoreLabel);

            // --- Top-right (y 20..180): the instruction scroll ----------------------------------
            //
            // Five rows, not four: the block also carries the first-run checklist (Stage 13b), and
            // its five steps must print in full. The rows stay 28 px tall — 26 was measured as an
            // overflow by the player's own text audit (a 22 px Thai line needs 28.61) — so the
            // heading moves up 4 px and the rows sit 28 apart, the last ending at -174 inside the
            // 176-tall panel. The chest panel below keeps its y.
            RectTransform instr = ProjectSetup.CreateUiRect(
                panel, "Instructions", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -20f), new Vector2(776f, 176f));
            var instrImage = instr.gameObject.AddComponent<Image>();
            instrImage.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            instrImage.raycastTarget = false;

            TMP_Text instrTitle = ProjectSetup.CreateUiText(instr, "InstrTitle", new Vector2(12f, -4f), new Vector2(752f, 28f), ProjectSetup.UiFontSize, font);
            instrTitle.color = new Color(1f, 0.82f, 0.35f, 1f);

            TMP_Text step1 = ProjectSetup.CreateUiText(instr, "Step1", new Vector2(12f, -34f), new Vector2(752f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text step2 = ProjectSetup.CreateUiText(instr, "Step2", new Vector2(12f, -62f), new Vector2(752f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text step3 = ProjectSetup.CreateUiText(instr, "Step3", new Vector2(12f, -90f), new Vector2(752f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text step4 = ProjectSetup.CreateUiText(instr, "Step4", new Vector2(12f, -118f), new Vector2(752f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text step5 = ProjectSetup.CreateUiText(instr, "Step5", new Vector2(12f, -146f), new Vector2(752f, 28f), ProjectSetup.UiFontSize, font);

            // --- Left column (y 140..720 from the top): the board. Nine columns of 78 + a 4 gap =
            //     734 wide, seven rows = 574 tall -----------------------------------------------
            RectTransform board = ProjectSetup.CreateUiRect(
                panel, "Board", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -140f), new Vector2(740f, 580f));

            // The board's own background. Opaque enough that the sala behind the overlay cannot be
            // read through the grid ("พาเนล Board และหีบทึบพอ (alpha ≥ 0.9) ไม่ให้ฉากด้านหลังโผล่ผ่าน"):
            // the cells are drawn on top of this and leave a 4 px gap, and the gaps used to show the
            // 2.5D sala straight through the placement area.
            var boardImage = board.gameObject.AddComponent<Image>();
            boardImage.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);
            boardImage.raycastTarget = false;

            // --- Right column (y 200..600 from the top): the chest panel and its ten slots -------
            RectTransform chestPanel = ProjectSetup.CreateUiRect(
                panel, "ChestPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -200f), new Vector2(776f, 400f));
            var chestPanelImage = chestPanel.gameObject.AddComponent<Image>();
            chestPanelImage.color = new Color(0.10f, 0.12f, 0.15f, 0.92f);
            chestPanelImage.raycastTarget = false;

            TMP_Text chestTitle = ProjectSetup.CreateUiText(chestPanel, "ChestTitle", new Vector2(16f, -10f), new Vector2(744f, 34f), ProjectSetup.UiFontSize, font);

            // 5 columns of 136 + a 16 gap = 744 wide, two rows = 288 tall.
            RectTransform chest = ProjectSetup.CreateUiRect(
                chestPanel, "Chest", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -52f), new Vector2(744f, 300f));

            // --- The free column above the bar (y 214..320, x 822..1032): the incense pot. It has
            //     to be above the status bar (which is 190 tall) and clear of both the panel's foot
            //     (ends at x 780) and the bar's ออกเดินทาง button (starts at x 1074), because a pot
            //     drawn over the depart button is a pot a player clicks instead of leaving camp -----
            RectTransform altar = ProjectSetup.CreateUiRect(
                panel, "Altar", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(822f, 214f), new Vector2(210f, 106f));
            var altarImage = altar.gameObject.AddComponent<Image>();
            altarImage.color = new Color(0.38f, 0.28f, 0.16f, 0.92f);
            altarImage.raycastTarget = false;

            TMP_Text altarCaption = ProjectSetup.CreateUiText(
                altar, "AltarCaption", new Vector2(6f, -4f), new Vector2(198f, 26f), ProjectSetup.UiFontSize, font, TextAlignmentOptions.Center);
            TMP_Text altarOffer = ProjectSetup.CreateUiText(
                altar, "AltarOffer", new Vector2(6f, -34f), new Vector2(198f, 66f), ProjectSetup.UiFontSize, font, TextAlignmentOptions.Center);

            // --- Bottom-left, above the bar (y 232..340): กลับ, ไปชั้นถัดไป and the reason the
            //     second one is disabled. The panel's foot used to be drawn straight over the status
            //     bar (y 24..214 against the bar's 16..206) with its own copy of the run's numbers ----
            RectTransform statsNav = ProjectSetup.CreateUiRect(
                panel, "StatsNav", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 232f), new Vector2(740f, 108f));

            Button closeButton = ProjectSetup.CreateUiButton(statsNav, "CloseButton", new Vector2(0f, 0f), new Vector2(130f, 50f), font, out TMP_Text closeLabel);
            Button nextFloorButton = ProjectSetup.CreateUiButton(statsNav, "NextFloorButton", new Vector2(142f, 0f), new Vector2(220f, 50f), font, out TMP_Text nextFloorLabel);

            // The reason the ไปชั้นถัดไป button is disabled ("ปิดพร้อมเหตุผลถ้าไม่มีอาวุธอาคม").
            TMP_Text departReason = ProjectSetup.CreateUiText(statsNav, "DepartReason", new Vector2(0f, -58f), new Vector2(740f, 28f), ProjectSetup.UiFontSize, font);
            departReason.color = new Color(0.95f, 0.45f, 0.45f, 1f);

            // The จัดย่าม button lives on the canvas, not in the panel, and is hidden while the
            // panel is open (the panel carries its own กลับ). Bottom-centre per the GDD, clear of
            // the bottom bar underneath it (x 24..984).
            Button open = ProjectSetup.CreateUiButton(
                canvasRect, "OpenBagButton", new Vector2(0f, 0f), new Vector2(220f, 64f), font, out TMP_Text openLabel);
            RectTransform openRect = open.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(0.5f, 0f);
            openRect.anchorMax = new Vector2(0.5f, 0f);
            openRect.pivot = new Vector2(0.5f, 0f);
            openRect.anchoredPosition = new Vector2(0f, 222f);

            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_panelRoot", panel.gameObject);
            ProjectSetup.SetObjectReference(view, "_boardRect", board);
            ProjectSetup.SetObjectReference(view, "_chestRect", chest);
            ProjectSetup.SetObjectReference(view, "_chestPanelRect", chestPanel);
            ProjectSetup.SetObjectReference(view, "_chestTitleLabel", chestTitle);
            ProjectSetup.SetObjectReference(view, "_altarRect", altar);
            ProjectSetup.SetObjectReference(view, "_altarCaptionLabel", altarCaption);
            ProjectSetup.SetObjectReference(view, "_altarOfferLabel", altarOffer);
            ProjectSetup.SetObjectReference(view, "_statsRect", statsNav);
            ProjectSetup.SetObjectReference(view, "_font", font);
            ProjectSetup.SetObjectReference(view, "_openButton", open);
            ProjectSetup.SetObjectReference(view, "_openButtonLabel", openLabel);
            ProjectSetup.SetObjectReference(view, "_closeButton", closeButton);
            ProjectSetup.SetObjectReference(view, "_closeLabel", closeLabel);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_rotateButton", rotate);
            ProjectSetup.SetObjectReference(view, "_rotateLabel", rotateLabel);
            ProjectSetup.SetObjectReference(view, "_undoButton", undo);
            ProjectSetup.SetObjectReference(view, "_undoLabel", undoLabel);
            ProjectSetup.SetObjectReference(view, "_resetButton", reset);
            ProjectSetup.SetObjectReference(view, "_resetLabel", resetLabel);
            ProjectSetup.SetObjectReference(view, "_lockButton", lockButton);
            ProjectSetup.SetObjectReference(view, "_lockLabel", lockLabel);
            ProjectSetup.SetObjectReference(view, "_lockRestoreToggle", lockRestore);
            ProjectSetup.SetObjectReference(view, "_lockRestoreLabel", lockRestoreLabel);
            ProjectSetup.SetObjectReference(view, "_nextFloorButton", nextFloorButton);
            ProjectSetup.SetObjectReference(view, "_nextFloorLabel", nextFloorLabel);
            ProjectSetup.SetObjectReference(view, "_departReasonLabel", departReason);
            ProjectSetup.SetObjectReference(view, "_instructionsRect", instr);
            ProjectSetup.SetObjectReference(view, "_instructionsTitleLabel", instrTitle);
            ProjectSetup.SetObjectReference(view, "_instructionStep1Label", step1);
            ProjectSetup.SetObjectReference(view, "_instructionStep2Label", step2);
            ProjectSetup.SetObjectReference(view, "_instructionStep3Label", step3);
            ProjectSetup.SetObjectReference(view, "_instructionStep4Label", step4);
            ProjectSetup.SetObjectReference(view, "_instructionStep5Label", step5);
            return view;
        }

        /// <summary>
        /// A checkbox and its caption: a box, a check mark that fades in when it is on, and a label
        /// to the right of it.
        /// </summary>
        /// <remarks>
        /// A real <see cref="Toggle"/> rather than a label that reads like one, because the control it
        /// belongs to (คืนสภาพ) reads its value. The mark is authored at zero alpha so the saved scene
        /// shows the unchecked state the player starts in; <see cref="Toggle"/> drives it from then on.
        /// </remarks>
        private static Toggle CreateUiToggle(
            RectTransform parent, string name, Vector2 boxPosition, Vector2 boxSize, TMP_FontAsset font,
            Vector2 labelPosition, Vector2 labelSize, out TMP_Text label)
        {
            RectTransform box = ProjectSetup.CreateUiRect(
                parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), boxPosition, boxSize);

            var background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.18f, 0.20f, 0.24f, 1f);

            RectTransform mark = ProjectSetup.CreateUiRect(
                box, "Checkmark", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(5f, -5f), new Vector2(boxSize.x - 10f, boxSize.y - 10f));

            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.color = new Color(0.95f, 0.82f, 0.35f, 1f);
            markImage.raycastTarget = false;
            markImage.canvasRenderer.SetAlpha(0f);

            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = markImage;
            toggle.isOn = false;

            label = ProjectSetup.CreateUiText(parent, name + "Label", labelPosition, labelSize, ProjectSetup.UiFontSize, font);
            label.text = string.Empty;
            return toggle;
        }

        /// <summary>
        /// The bottom bar: น้ำหนัก, สมาธิ, หีบ x/10, เบี้ย, ราคา refresh, the hint, the status line and
        /// the depart button with its reason.
        /// </summary>
        private static CampStatusBarView BuildCampStatusBar(RectTransform canvasRect, TMP_FontAsset font, CampSceneScope scope)
        {
            // Left-anchored and no wider than the gap the bag screen leaves: a bar that ran under
            // the bag panel would hide the depart button it is supposed to justify. It grew from 960
            // to 1400 in Stage 09d because the control hint has to read at 22 px, and Thai does not
            // break inside a run of syllables: the string is one unbreakable word as far as TMP is
            // concerned, so a box narrower than the text does not wrap it — it spills it.
            RectTransform panel = ProjectSetup.CreateUiRect(
                canvasRect, "StatusPanel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 16f), new Vector2(1400f, 190f));

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);

            // Row one: the run at a glance.
            TMP_Text floor = ProjectSetup.CreateUiText(panel, "FloorValue", new Vector2(10f, -6f), new Vector2(140f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text chestCaption = ProjectSetup.CreateUiText(panel, "ChestCaption", new Vector2(160f, -6f), new Vector2(90f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text chestValue = ProjectSetup.CreateUiText(panel, "ChestValue", new Vector2(252f, -6f), new Vector2(110f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text biaCaption = ProjectSetup.CreateUiText(panel, "BiaCaption", new Vector2(370f, -6f), new Vector2(80f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text biaValue = ProjectSetup.CreateUiText(panel, "BiaValue", new Vector2(452f, -6f), new Vector2(70f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text refreshCostCaption = ProjectSetup.CreateUiText(panel, "RefreshCostCaption", new Vector2(600f, -6f), new Vector2(190f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text refreshCostValue = ProjectSetup.CreateUiText(panel, "RefreshCostValue", new Vector2(794f, -6f), new Vector2(110f, 30f), ProjectSetup.UiFontSize, font);

            // Row two: the weight penalty and the focus rate, with the depart button on the right.
            TMP_Text weightCaption = ProjectSetup.CreateUiText(panel, "WeightCaption", new Vector2(10f, -40f), new Vector2(120f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text weightValue = ProjectSetup.CreateUiText(panel, "WeightValue", new Vector2(134f, -40f), new Vector2(260f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text focusCaption = ProjectSetup.CreateUiText(panel, "FocusCaption", new Vector2(404f, -40f), new Vector2(120f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text focusValue = ProjectSetup.CreateUiText(panel, "FocusValue", new Vector2(528f, -40f), new Vector2(160f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text departReason = ProjectSetup.CreateUiText(panel, "DepartReason", new Vector2(920f, -6f), new Vector2(460f, 64f), ProjectSetup.UiFontSize, font);

            // Row three: the penalty bar, the status line, the run's own HP/MP, and the depart button
            // itself. HP/MP used to be a second readout inside the จัดย่าม panel, which meant the camp
            // showed a stat twice and the panel's copy went stale as soon as a battle changed it; the
            // bar is now the only place a run stat appears (Stage 09e work item 1).
            RectTransform weightBar = ProjectSetup.CreateUiBar(panel, "WeightBar", new Vector2(10f, -76f), new Vector2(370f, 16f), new Color(0.72f, 0.58f, 0.30f, 1f));
            TMP_Text status = ProjectSetup.CreateUiText(panel, "StatusLabel", new Vector2(392f, -72f), new Vector2(300f, 40f), ProjectSetup.UiFontSize, font);
            TMP_Text hpMp = ProjectSetup.CreateUiText(panel, "HpMpLabel", new Vector2(700f, -72f), new Vector2(330f, 40f), ProjectSetup.UiFontSize, font);
            Button depart = ProjectSetup.CreateUiButton(panel, "DepartButton", new Vector2(1050f, -76f), new Vector2(320f, 64f), font, out TMP_Text departLabel);

            // Row four: the controls hint, across the bar — and *below* the depart button rather
            // than beside it, because at 22 px the hint is wider than the strip the button leaves.
            TMP_Text hint = ProjectSetup.CreateUiText(panel, "HintLabel", new Vector2(10f, -146f), new Vector2(1370f, 34f), ProjectSetup.UiFontSize, font);

            CampStatusBarView view = panel.gameObject.AddComponent<CampStatusBarView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_panelRect", panel);
            ProjectSetup.SetObjectReference(view, "_floorLabel", floor);
            ProjectSetup.SetObjectReference(view, "_weightCaptionLabel", weightCaption);
            ProjectSetup.SetObjectReference(view, "_weightValueLabel", weightValue);
            ProjectSetup.SetObjectReference(view, "_weightBar", weightBar);
            ProjectSetup.SetObjectReference(view, "_focusCaptionLabel", focusCaption);
            ProjectSetup.SetObjectReference(view, "_focusValueLabel", focusValue);
            ProjectSetup.SetObjectReference(view, "_chestCaptionLabel", chestCaption);
            ProjectSetup.SetObjectReference(view, "_chestValueLabel", chestValue);
            ProjectSetup.SetObjectReference(view, "_biaCaptionLabel", biaCaption);
            ProjectSetup.SetObjectReference(view, "_biaValueLabel", biaValue);
            ProjectSetup.SetObjectReference(view, "_refreshCostCaptionLabel", refreshCostCaption);
            ProjectSetup.SetObjectReference(view, "_refreshCostValueLabel", refreshCostValue);
            ProjectSetup.SetObjectReference(view, "_hintLabel", hint);
            ProjectSetup.SetObjectReference(view, "_statusLabel", status);
            ProjectSetup.SetObjectReference(view, "_hpMpLabel", hpMp);
            ProjectSetup.SetObjectReference(view, "_departReasonLabel", departReason);
            ProjectSetup.SetObjectReference(view, "_departButton", depart);
            ProjectSetup.SetObjectReference(view, "_departButtonLabel", departLabel);
            return view;
        }

        /// <summary>
        /// The piece tooltip: ชื่อ, ระดับ, สถานะ, คำอธิบาย, คอมโบที่ทำงานอยู่ — five lines on a canvas of
        /// its own that draws above the whole camp screen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Its own <b>canvas</b>, not a panel of the camp's, for one measured reason: the camp screen
        /// is a single canvas, so "on top" there means "added last", and the board's cells, the piece
        /// rectangles and the combo lines are all created at runtime and appended after everything
        /// authored. A second root canvas with a higher <c>Canvas.sortingOrder</c> is the documented
        /// way to draw over another canvas rather than a race with creation order.
        /// </para>
        /// <para>
        /// The canvas carries <b>no</b> <c>GraphicRaycaster</c> and every graphic on it has
        /// <c>raycastTarget</c> off, so a tooltip that follows the pointer cannot swallow the pointer
        /// events that move it. The panel's background is 0.95 alpha, above the 0.92 the stage asks
        /// for, so the board is not readable through it.
        /// </para>
        /// </remarks>
        private static CampTooltipView BuildCampTooltip(TMP_FontAsset font, CampSceneScope scope)
        {
            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject, TooltipSortingOrder, withRaycaster: false);
            var canvasRect = (RectTransform)canvas.transform;

            // Anchored to the canvas' bottom-left corner: the placement rule (CampTooltipPlacement)
            // writes this rect's anchoredPosition in the same units, so what a test measures is what
            // the view set. The initial position is the margin, which is where an unhovered tooltip
            // would sit if anything showed it.
            // Stage 09h widened and heightened the panel: the ค่าที่ใช้จริง block carries the resolved
            // row, the base row and (when it decided the number) the cooldown floor, and the upgrade
            // block carries one line per merge step. Stage 09n added the shelf offer's ราคา line
            // between the tier and the numbers, which is one more line of 28 px.
            RectTransform panel = ProjectSetup.CreateUiRect(
                canvasRect, "TooltipPanel", Vector2.zero, Vector2.zero,
                new Vector2(CampTooltipPlacement.Margin, CampTooltipPlacement.Margin),
                new Vector2(460f, 530f));

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            background.raycastTarget = false;

            TMP_Text title = ProjectSetup.CreateUiText(panel, "TooltipTitle", new Vector2(14f, -10f), new Vector2(432f, 34f), ProjectSetup.UiFontSize + 4f, font);
            title.color = new Color(1f, 0.85f, 0.45f, 1f);
            TMP_Text tier = ProjectSetup.CreateUiText(panel, "TooltipTier", new Vector2(14f, -46f), new Vector2(432f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text price = ProjectSetup.CreateUiText(panel, "TooltipPrice", new Vector2(14f, -76f), new Vector2(432f, 26f), ProjectSetup.UiFontSize, font);
            price.color = new Color(0.95f, 0.86f, 0.55f, 1f);
            TMP_Text status = ProjectSetup.CreateUiText(panel, "TooltipStatus", new Vector2(14f, -104f), new Vector2(432f, 84f), ProjectSetup.UiFontSize, font);
            TMP_Text upgrades = ProjectSetup.CreateUiText(panel, "TooltipUpgrades", new Vector2(14f, -190f), new Vector2(432f, 84f), ProjectSetup.UiFontSize, font);
            upgrades.color = new Color(0.75f, 0.9f, 0.75f, 1f);
            TMP_Text description = ProjectSetup.CreateUiText(panel, "TooltipDescription", new Vector2(14f, -276f), new Vector2(432f, 92f), ProjectSetup.UiFontSize, font);
            description.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            TMP_Text combos = ProjectSetup.CreateUiText(panel, "TooltipCombos", new Vector2(14f, -370f), new Vector2(432f, 150f), ProjectSetup.UiFontSize, font);
            combos.color = new Color(0.95f, 0.82f, 0.35f, 1f);

            CampTooltipView view = panel.gameObject.AddComponent<CampTooltipView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_root", panel.gameObject);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_tierLabel", tier);
            ProjectSetup.SetObjectReference(view, "_priceLabel", price);
            ProjectSetup.SetObjectReference(view, "_statusLabel", status);
            ProjectSetup.SetObjectReference(view, "_upgradesLabel", upgrades);
            ProjectSetup.SetObjectReference(view, "_descriptionLabel", description);
            ProjectSetup.SetObjectReference(view, "_combosLabel", combos);

            panel.gameObject.SetActive(false);
            return view;
        }
    }
}
