using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Takes the UI shots and judges the layout <b>inside the Editor</b>, so a layout can be checked
    /// without a WebGL build and without a browser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why this exists: until now the only honest picture of the จัดย่าม screen was a screenshot of the
    /// WebGL player (Stage 09d), which costs a full build plus a Playwright boot to look at a
    /// rectangle that overlaps a label. This command opens the scene, renders it off-screen at the two
    /// sizes the GDD names, writes the images under <c>artifacts/ui/</c>, and fails when a panel that
    /// is supposed to stand clear of another one does not.
    /// </para>
    /// <para>
    /// How a shot is taken, and why this way. A canvas in <c>ScreenSpaceOverlay</c> is laid out to the
    /// game view's size, which the Editor cannot be asked for from a script without reaching into
    /// undocumented <c>GameView</c> internals. Instead the root canvases are switched to
    /// <c>ScreenSpaceCamera</c> for the duration of the shot, pointed at the scene's own camera, whose
    /// <c>targetTexture</c> is a <c>RenderTexture</c> of exactly the size being measured. A canvas in
    /// that mode takes its size from the camera's pixel rectangle, so the layout the shot contains is
    /// the layout a player would get at that size — measured, not assumed: at 1280×720 the board comes
    /// out at <c>{x:26.667,y:213.333,width:493.333,height:386.667}</c>, the rectangle the browser
    /// reported for the same screen in Stage 09 (<c>artifacts/stage09/playwright-web.log</c>).
    /// </para>
    /// <para>
    /// The rectangles are read through <c>SerializedObject</c> from the views' own serialized fields —
    /// the same <c>RectTransform</c>s the scene was authored with — because the views' public geometry
    /// API (<c>CampScreenRects</c>) resolves against <b>overlay</b> canvases and would report nonsense
    /// while the canvas is in camera mode (measured: <c>-11.244,4.231</c> instead of
    /// <c>26.667,213.333</c>). No game code is changed by this tool.
    /// </para>
    /// <para>
    /// Entry points: the menu item below, and <c>-executeMethod
    /// ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit</c> for a scripted run. The batch
    /// one needs the Editor to be closed, exactly like <c>tools/build-web.ps1</c>: one process owns
    /// the project lock at a time.
    /// </para>
    /// <para>
    /// The tool never saves a scene. It restores the panel state and the canvases it borrowed, and it
    /// leaves whatever scene the Editor started in open — unless one of them is dirty, in which case
    /// it says so instead of opening a scene Unity would ask a human about.
    /// </para>
    /// </remarks>
    public static class UiCaptureTool
    {
        private const string CampScenePath = "Assets/Main/Scenes/Camp.unity";
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";

        /// <summary>The two sizes the GDD names, in the order it names them.</summary>
        private static readonly Vector2Int[] CapturedSizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1280, 720),
        };

        /// <summary>
        /// The panel's surfaces, as the view serializes them. Names match <c>CampBagSurface</c> so the
        /// report reads the same as the PlayMode test's failure messages.
        /// </summary>
        private static readonly (string Name, string Field)[] PanelSurfaces =
        {
            ("Board", "_boardRect"),
            (UiLayoutCheck.ChestPanel, "_chestPanelRect"),
            (UiLayoutCheck.Chest, "_chestRect"),
            ("Altar", "_altarRect"),
            ("Instructions", "_instructionsRect"),
            ("Stats", "_statsRect"),
        };

        /// <summary>
        /// The panel's controls, as the view serializes them. กลับ (Close) is in here because it lives
        /// inside the panel; จัดย่าม (Open) lives on the canvas outside it and is measured with the
        /// closed camp instead.
        /// </summary>
        private static readonly (string Name, string Field)[] PanelControls =
        {
            ("Close", "_closeButton"),
            ("Rotate", "_rotateButton"),
            ("Undo", "_undoButton"),
            ("Reset", "_resetButton"),
            ("Lock", "_lockButton"),
            ("LockRestore", "_lockRestoreToggle"),
            ("NextFloor", "_nextFloorButton"),
        };

        private static readonly Vector3[] Corners = new Vector3[4];

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui"));

        /// <summary>Captures both scenes at both sizes and judges the layouts.</summary>
        [MenuItem("ProjectSurvive/Tools/1. Capture UI (Camp + Battle, 1920x1080 + 1280x720)", priority = 130)]
        private static void CaptureFromMenu()
        {
            CaptureAll(exitEditor: false);
        }

        /// <summary>
        /// The <c>-executeMethod</c> entry point. Exits the Editor with 0 when every check passed and
        /// 1 otherwise, so a script can branch on the result.
        /// </summary>
        public static void CaptureAllAndExit()
        {
            CaptureAll(exitEditor: true);
        }

        /// <summary>
        /// The same run with one invented region dropped on top of the board, which is the only way to
        /// show that this command <b>fails</b> when panels collide without editing a scene to break it.
        /// Its report goes to <c>ui-report-selftest.*</c>, so it cannot be mistaken for the real one.
        /// </summary>
        [MenuItem("ProjectSurvive/Tools/2. Capture UI self-test (expected to FAIL)", priority = 131)]
        private static void SelfTestFromMenu()
        {
            RunSelfTest(exitEditor: false);
        }

        /// <summary>The <c>-executeMethod</c> form of <see cref="SelfTestFromMenu"/>.</summary>
        public static void CaptureSelfTestAndExit()
        {
            RunSelfTest(exitEditor: true);
        }

        private static void RunSelfTest(bool exitEditor)
        {
            sDeliberateOverlap = true;
            try
            {
                CaptureAll(exitEditor);
            }
            finally
            {
                sDeliberateOverlap = false;
            }
        }

        /// <summary>Set only by <see cref="CaptureSelfTestAndExit"/>.</summary>
        private static bool sDeliberateOverlap;

        /// <summary>Runs the whole capture. Returns the process exit code the batch path would use.</summary>
        public static int CaptureAll(bool exitEditor)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var report = new CaptureReport();
            string startedIn = SceneManager.GetActiveScene().path;

            try
            {
                Directory.CreateDirectory(OutputDirectory);
            }
            catch (Exception error)
            {
                report.Failures.Add("cannot write to " + OutputDirectory + ": " + error.Message);
                WriteReports(report, started.ElapsedMilliseconds);
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return 1;
            }

            CaptureCamp(report);
            CaptureBattle(report);
            RestoreScene(startedIn, report);

            WriteReports(report, started.ElapsedMilliseconds);

            int exitCode = report.Failures.Count == 0 ? 0 : 1;
            if (exitEditor)
            {
                EditorApplication.Exit(exitCode);
            }

            return exitCode;
        }

        // ------------------------------------------------------------------
        // the Camp: the panel's layout is what this command is for
        // ------------------------------------------------------------------

        private static void CaptureCamp(CaptureReport report)
        {
            if (!EnsureOpen(CampScenePath, report))
            {
                return;
            }

            var bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
            var bar = Object.FindFirstObjectByType<CampStatusBarView>(FindObjectsInactive.Include);
            Camera camera = Camera.main;

            if (bag == null || bar == null || camera == null)
            {
                report.Failures.Add(
                    "the Camp scene is missing " + (bag == null ? "CampBagScreenView " : string.Empty)
                    + (bar == null ? "CampStatusBarView " : string.Empty)
                    + (camera == null ? "a MainCamera" : string.Empty));
                return;
            }

            var bagFields = new SerializedObject(bag);
            var fixedRects = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>();
            AddSerializedRects(bagFields, PanelSurfaces, UiRegionKind.Surface, fixedRects);
            AddSerializedRects(bagFields, PanelControls, UiRegionKind.Control, fixedRects);

            var barFields = new SerializedObject(bar);
            var statusBar = barFields.FindProperty("_panelRect")?.objectReferenceValue as RectTransform;
            var openButton = bagFields.FindProperty("_openButton")?.objectReferenceValue as Button;

            if (statusBar == null || openButton == null)
            {
                report.Failures.Add("the camp's status bar or จัดย่าม button is not wired in the scene");
                return;
            }

            var closed = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>
            {
                ("StatusBar", UiRegionKind.Surface, statusBar),
                ("Open", UiRegionKind.Control, openButton.transform as RectTransform),
            };

            report.Notes.Add(
                "the shapes are the authored ones: every label in Camp.unity is empty until the runtime "
                + "fills it from the localization tables, so these shots show the panels and their "
                + "backgrounds without their text, and the board's cells are built at runtime "
                + "(CampBagDragPlayModeTests measures that layout in play mode)");
            report.Notes.Add(
                "the world behind the UI is whatever the scene's own camera is aimed at in the Editor: "
                + "the camp's camera follows the player at runtime, and nothing here runs the player");

            for (int i = 0; i < CapturedSizes.Length; i++)
            {
                Vector2Int size = CapturedSizes[i];

                // The closed camp first: this is the state the status bar is the top layer in, so
                // "จัดย่าม does not sit on the bar" is checked here.
                bag.ShowPanel(false);
                Capture(SceneKind.Camp, "closed", "camp-closed", camera, size, closed, report);

                // Then the panel, which is where the board, the chest and the altar live.
                bag.ShowPanel(true);
                Capture(SceneKind.Camp, "panel", "camp", camera, size, fixedRects, report);
            }

            // Leave the panel exactly as the scene was authored: closed.
            bag.ShowPanel(false);
        }

        // ------------------------------------------------------------------
        // the Battle scene: shots only, its HUD is not part of the region check
        // ------------------------------------------------------------------

        private static void CaptureBattle(CaptureReport report)
        {
            report.Notes.Add("the Battle HUD is shot but not measured: the region check is the camp's");
            if (!EnsureOpen(BattleScenePath, report))
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                report.Failures.Add("the Battle scene has no MainCamera");
                return;
            }

            for (int i = 0; i < CapturedSizes.Length; i++)
            {
                Vector2Int size = CapturedSizes[i];
                Capture(SceneKind.Battle, "hud", "battle", camera, size, null, report);
            }
        }

        // ------------------------------------------------------------------
        // one shot
        // ------------------------------------------------------------------

        private static void Capture(
            SceneKind scene,
            string state,
            string fileSlug,
            Camera camera,
            Vector2Int size,
            List<(string Name, UiRegionKind Kind, RectTransform Rect)> regions,
            CaptureReport report)
        {
            var borrowed = new List<(Canvas Canvas, RenderMode Mode, Camera Camera, float Plane)>();
            RenderTexture target = null;
            Texture2D image = null;
            var started = System.Diagnostics.Stopwatch.StartNew();

            string fileName = fileSlug + "-" + size.x + "x" + size.y + ".png";
            string path = Path.Combine(OutputDirectory, fileName);

            try
            {
                target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);

                Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas.rootCanvas != canvas
                        || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        || !canvas.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    borrowed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 1f;
                }

                RenderTexture previousTarget = camera.targetTexture;
                camera.targetTexture = target;

                Canvas.ForceUpdateCanvases();

                var measured = new List<UiRegion>();
                if (sDeliberateOverlap && regions != null && regions.Count > 0)
                {
                    // A collision nothing in the scene asked for: the board's rectangle again, under
                    // another surface's name. Two surfaces may only share space when they are the
                    // chest panel and its grid, so this pair has to be reported.
                    Rect board = ScreenRectOf(regions[0].Rect, camera);
                    measured.Add(new UiRegion("SelfTestCollision", board, UiRegionKind.Surface));
                }

                if (regions != null)
                {
                    for (int i = 0; i < regions.Count; i++)
                    {
                        Rect rect = ScreenRectOf(regions[i].Rect, camera);
                        if (rect.width <= 0f || rect.height <= 0f)
                        {
                            report.Failures.Add(
                                state + " " + size.x + "x" + size.y + ": " + regions[i].Name
                                + " has no size (is it inactive in the scene?)");
                            continue;
                        }

                        measured.Add(new UiRegion(regions[i].Name, rect, regions[i].Kind));
                    }

                    CheckInside(measured, size, state, report);
                }

                camera.Render();

                image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                byte[] png = image.EncodeToPNG();
                File.WriteAllBytes(path, png);

                camera.targetTexture = previousTarget;

                var violations = new List<string>();
                if (measured.Count > 0)
                {
                    violations = UiLayoutCheck.FindViolations(measured);
                    for (int i = 0; i < violations.Count; i++)
                    {
                        report.Failures.Add(state + " " + size.x + "x" + size.y + ": " + violations[i]);
                    }
                }

                report.Shots.Add(new Shot(
                    scene, state, fileName, size.x, size.y, png.Length, started.ElapsedMilliseconds,
                    measured, violations));
            }
            catch (Exception error)
            {
                report.Failures.Add(fileName + ": " + error.GetType().Name + " " + error.Message);
            }
            finally
            {
                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].Canvas.renderMode = borrowed[i].Mode;
                    borrowed[i].Canvas.worldCamera = borrowed[i].Camera;
                    borrowed[i].Canvas.planeDistance = borrowed[i].Plane;
                }

                if (image != null)
                {
                    Object.DestroyImmediate(image);
                }

                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                    if (camera.targetTexture == target)
                    {
                        camera.targetTexture = null;
                    }
                }

                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// A region that runs off the canvas is a defect the overlap check cannot see: two things can
        /// be disjoint and both half off the screen.
        /// </summary>
        private static void CheckInside(List<UiRegion> regions, Vector2Int size, string state, CaptureReport report)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Rect.xMin < -0.5f || regions[i].Rect.yMin < -0.5f
                    || regions[i].Rect.xMax > size.x + 0.5f || regions[i].Rect.yMax > size.y + 0.5f)
                {
                    report.Failures.Add(
                        state + " " + size.x + "x" + size.y + ": " + regions[i]
                        + " is not inside the canvas");
                }
            }
        }

        // ------------------------------------------------------------------
        // scene bookkeeping
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens the scene unless the Editor is on it already. Refuses to open one while another
        /// loaded scene is dirty: Unity would put a save prompt in front of a scripted run, which
        /// looks exactly like a hang.
        /// </summary>
        private static bool EnsureOpen(string scenePath, CaptureReport report)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == scenePath && active.isLoaded)
            {
                return true;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    report.Failures.Add(
                        "refusing to open " + scenePath + ": " + scene.path
                        + " has unsaved changes (save or discard it first)");
                    return false;
                }
            }

            try
            {
                Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (opened.IsValid() && opened.isLoaded)
                {
                    return true;
                }

                report.Failures.Add("could not open " + scenePath);
                return false;
            }
            catch (Exception error)
            {
                report.Failures.Add("could not open " + scenePath + ": " + error.Message);
                return false;
            }
        }

        /// <summary>Puts the Editor back on the scene it started on, when that is safe.</summary>
        private static void RestoreScene(string scenePath, CaptureReport report)
        {
            if (string.IsNullOrEmpty(scenePath) || SceneManager.GetActiveScene().path == scenePath)
            {
                return;
            }

            if (SceneManager.GetActiveScene().isDirty)
            {
                report.Notes.Add("left " + SceneManager.GetActiveScene().path + " open (it has unsaved changes)");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // ------------------------------------------------------------------
        // measurement
        // ------------------------------------------------------------------

        private static void AddSerializedRects(
            SerializedObject source,
            (string Name, string Field)[] fields,
            UiRegionKind kind,
            List<(string Name, UiRegionKind Kind, RectTransform Rect)> into)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = source.FindProperty(fields[i].Field);
                var rect = property?.objectReferenceValue as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                into.Add((fields[i].Name, kind, rect));
            }
        }

        /// <summary>
        /// A transform's rectangle in the pixels of <paramref name="camera"/>'s target: for a camera
        /// whose target is a 1920×1080 texture, that is the 1920×1080 pixel space, bottom-left origin.
        /// </summary>
        private static Rect ScreenRectOf(RectTransform rect, Camera camera)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return default;
            }

            rect.GetWorldCorners(Corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);

            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        // ------------------------------------------------------------------
        // the report
        // ------------------------------------------------------------------

        private static void WriteReports(CaptureReport report, long elapsedMs)
        {
            string suffix = sDeliberateOverlap ? "-selftest" : string.Empty;

            var text = new StringBuilder();
            text.AppendLine("# UI capture — Camp and Battle, in the Editor");
            text.AppendLine();
            text.AppendLine(
                "Taken by `ProjectSurvive/Tools/1. Capture UI` (or `-executeMethod "
                + "ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit`), "
                + (report.Failures.Count == 0 ? "**PASS**" : "**FAIL**")
                + $" — {report.Failures.Count} failure(s), {elapsedMs} ms total.");
            text.AppendLine();
            text.AppendLine("| Scene | State | Size | Image | Bytes | Regions | Overlaps | ms |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

            for (int i = 0; i < report.Shots.Count; i++)
            {
                Shot shot = report.Shots[i];
                text.AppendLine(string.Format(
                    "| {0} | {1} | {2}×{3} | `artifacts/ui/{4}` | {5} | {6} | {7} | {8} |",
                    shot.Scene, shot.State, shot.Width, shot.Height, shot.FileName, shot.Bytes,
                    shot.Regions.Count, shot.Violations.Count, shot.Milliseconds));
            }

            text.AppendLine();
            text.AppendLine("## Measured regions (pixels, origin bottom-left)");
            text.AppendLine();

            for (int i = 0; i < report.Shots.Count; i++)
            {
                Shot shot = report.Shots[i];
                if (shot.Regions.Count == 0)
                {
                    text.AppendLine($"- **{shot.FileName}** — not measured (no region check for this scene).");
                    continue;
                }

                text.AppendLine($"- **{shot.FileName}**");
                for (int r = 0; r < shot.Regions.Count; r++)
                {
                    UiRegion region = shot.Regions[r];
                    text.AppendLine(
                        $"  - {region.Name} ({region.Kind}): "
                        + string.Format("{0:0.###},{1:0.###} {2:0.###}×{3:0.###}",
                            region.Rect.x, region.Rect.y, region.Rect.width, region.Rect.height));
                }
            }

            if (report.Failures.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Failures");
                text.AppendLine();
                for (int i = 0; i < report.Failures.Count; i++)
                {
                    text.AppendLine("- " + report.Failures[i]);
                }
            }

            if (report.Notes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Notes");
                text.AppendLine();
                for (int i = 0; i < report.Notes.Count; i++)
                {
                    text.AppendLine("- " + report.Notes[i]);
                }
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "ui-report" + suffix + ".md"), text.ToString());

            var json = new StringBuilder();
            json.Append("{\n  \"passed\": ").Append(report.Failures.Count == 0 ? "true" : "false");
            json.Append(",\n  \"elapsedMs\": ").Append(elapsedMs);
            json.Append(",\n  \"failures\": [");
            for (int i = 0; i < report.Failures.Count; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ").Append(Quote(report.Failures[i]));
            }

            json.Append(report.Failures.Count > 0 ? "\n  ],\n  \"shots\": [" : "],\n  \"shots\": [");
            for (int i = 0; i < report.Shots.Count; i++)
            {
                Shot shot = report.Shots[i];
                json.Append(i == 0 ? "\n    {" : ",\n    {");
                json.Append("\"scene\": ").Append(Quote(shot.Scene.ToString()));
                json.Append(", \"state\": ").Append(Quote(shot.State));
                json.Append(", \"file\": ").Append(Quote(shot.FileName));
                json.Append(", \"width\": ").Append(shot.Width);
                json.Append(", \"height\": ").Append(shot.Height);
                json.Append(", \"bytes\": ").Append(shot.Bytes);
                json.Append(", \"milliseconds\": ").Append(shot.Milliseconds);
                json.Append(", \"violations\": [");
                for (int v = 0; v < shot.Violations.Count; v++)
                {
                    json.Append(v == 0 ? "" : ", ").Append(Quote(shot.Violations[v]));
                }

                json.Append("], \"regions\": [");
                for (int r = 0; r < shot.Regions.Count; r++)
                {
                    UiRegion region = shot.Regions[r];
                    json.Append(r == 0 ? "\n      " : ",\n      ");
                    json.Append("{\"name\": ").Append(Quote(region.Name));
                    json.Append(", \"kind\": ").Append(Quote(region.Kind.ToString()));
                    json.Append(", \"x\": ").Append(Round(region.Rect.x));
                    json.Append(", \"y\": ").Append(Round(region.Rect.y));
                    json.Append(", \"width\": ").Append(Round(region.Rect.width));
                    json.Append(", \"height\": ").Append(Round(region.Rect.height));
                    json.Append("}");
                }

                json.Append(shot.Regions.Count > 0 ? "\n    ]}" : "]}");
            }

            json.Append("\n  ]\n}\n");

            File.WriteAllText(Path.Combine(OutputDirectory, "ui-report" + suffix + ".json"), json.ToString());

            string line = string.Format(
                "[ProjectSurvive] UI capture: {0} image(s), {1} failure(s), {2} ms -> {3}",
                report.Shots.Count, report.Failures.Count, elapsedMs, OutputDirectory);

            if (report.Failures.Count == 0)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line + "\n" + string.Join("\n", report.Failures));
            }
        }

        private static string Round(float value) =>
            value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\').Append(c);
                }
                else if (c == '\n')
                {
                    builder.Append("\\n");
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.Append('"').ToString();
        }

        private enum SceneKind
        {
            Camp,
            Battle,
        }

        private readonly struct Shot
        {
            public Shot(
                SceneKind scene, string state, string fileName, int width, int height, int bytes,
                long milliseconds, List<UiRegion> regions, List<string> violations)
            {
                Scene = scene;
                State = state;
                FileName = fileName;
                Width = width;
                Height = height;
                Bytes = bytes;
                Milliseconds = milliseconds;
                Regions = regions;
                Violations = violations;
            }

            public readonly SceneKind Scene;
            public readonly string State;
            public readonly string FileName;
            public readonly int Width;
            public readonly int Height;
            public readonly int Bytes;
            public readonly long Milliseconds;
            public readonly List<UiRegion> Regions;
            public readonly List<string> Violations;
        }

        private sealed class CaptureReport
        {
            public readonly List<Shot> Shots = new List<Shot>();
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Notes = new List<string>();
        }
    }
}
