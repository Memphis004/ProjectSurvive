using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Stage 09o evidence: the sala at both captured sizes, in both camp profiles, with the player
    /// standing in the middle and in front of each of the four shelf stations, plus the จัดย่าม panel
    /// open — with the readability contrast measured on the very pixels that were saved.
    /// </summary>
    /// <remarks>
    /// The same entry point takes the "before" set (the camp as it was) and the "after" set (the camp
    /// with its yard): whether there is an environment decides which profiles exist, and the tag
    /// decides the file names. Everything measured is read from the rendered buffer the PNG was
    /// written from, so a number and its picture always describe one frame.
    /// </remarks>
    public static class CampEnvironmentCaptureTool
    {
        /// <summary>WCAG AA's ratio for body text. Reported, never used as this stage's gate.</summary>
        private const float WcagAaText = 4.5f;

        /// <summary>
        /// The tag of a capture that was asked for in edit mode, kept in <see cref="SessionState"/> so
        /// it survives the domain reload entering play mode brings.
        /// </summary>
        private const string PendingTagKey = "ProjectSurvive.CampCapture.PendingTag";

        private static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1280, 720),
        };

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "camp-env"));

        private static int _lastBatches;
        private static int _lastDrawCalls;
        private static int _lastSetPass;

        private struct CaptureResult
        {
            public string File;
            public int Batches;
            public int DrawCalls;
            public int SetPass;
            public float FrameMs;
            public ContrastSample Player;
            public ContrastSample Rack;
            public ContrastSample Drum;
            public ContrastSample Pot;
            public ContrastSample Price;

            /// <summary>
            /// The label's <b>design</b> contrast: the ink colour the label is drawn in against the
            /// board's rendered median. This is the quantity WCAG's ratio is defined on, and unlike
            /// <see cref="Price"/> it does not shrink when the same text lands on fewer pixels.
            /// </summary>
            public ContrastSample PriceDesign;
            public float PriceInkHeight;
            public string Rects;
        }

        /// <summary>
        /// Runs the whole capture from the Editor menu. The menu call returns at once and the capture
        /// finishes on its own — a play-mode capture takes minutes, far longer than a menu click.
        /// </summary>
        /// <remarks>
        /// The continuation is on the default scheduler on purpose: <c>Debug.Log</c> is thread-safe,
        /// and <c>FromCurrentSynchronizationContext</c> throws when the caller has none — which is the
        /// case for a menu item executed by the command line, the one caller that needs this most.
        /// </remarks>
        [MenuItem("ProjectSurvive/Stage 09o/2. Capture the camp yard (both profiles, both sizes)", priority = 106)]
        private static void CaptureFromMenu() => Run("after").ContinueWith(LogResult, TaskScheduler.Default);

        /// <summary>
        /// Runs the whole capture. <paramref name="tag"/> prefixes every file.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two things have to survive for this to finish, and neither belongs to the caller: <b>the
        /// domain reload that entering play mode brings</b> — which ends a running task where it
        /// stands, leaving no exception and no log — and <b>the context the continuations resume on</b>.
        /// So the run does not start while the editor is stopped: it records its tag in
        /// <see cref="SessionState"/> (the one store that crosses the reload), asks for play mode, and
        /// <see cref="ResumePendingCapture"/> picks the tag up in the new domain.
        /// </para>
        /// <para>
        /// The body then runs under <see cref="MainThreadPump"/>: every wait here is an
        /// <c>await Task.Delay</c>, and what a continuation resumes on is the
        /// <see cref="SynchronizationContext"/> installed at the moment of the await. From a menu click
        /// that is Unity's own context, which the player loop pumps; from the command line it is the
        /// pipeline's context, which nothing pumps once the command returns — the capture then stops
        /// silently after its first wait.
        /// </para>
        /// </remarks>
        public static Task<string> Run(string tag)
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetString(PendingTagKey, tag);
                EditorApplication.EnterPlaymode();
                return Task.FromResult("play mode is starting; the capture resumes in the new domain");
            }

            return RunInPlayMode(tag);
        }

        private static async Task<string> RunInPlayMode(string tag)
        {
            SynchronizationContext previous = SynchronizationContext.Current;
            var pump = new MainThreadPump();
            SynchronizationContext.SetSynchronizationContext(pump);

            try
            {
                return await RunOnMainThread(tag);
            }
            finally
            {
                pump.Dispose();
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        /// <summary>
        /// Picks the capture back up after the domain reload that entering play mode caused, and waits
        /// for the player loop to be running before it starts (during a domain load it is not yet).
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ResumePendingCapture()
        {
            if (SessionState.GetString(PendingTagKey, string.Empty).Length == 0)
            {
                return;
            }

            var waited = System.Diagnostics.Stopwatch.StartNew();

            void Resume()
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.update -= Resume;
                    string tag = SessionState.GetString(PendingTagKey, string.Empty);
                    SessionState.EraseString(PendingTagKey);

                    if (tag.Length > 0)
                    {
                        RunInPlayMode(tag).ContinueWith(LogResult, TaskScheduler.Default);
                    }

                    return;
                }

                if (waited.Elapsed.TotalSeconds > 120d)
                {
                    // Play mode never came up (a cancelled run, a compile error): drop the request
                    // rather than leaving a flag that would fire on the next play session.
                    EditorApplication.update -= Resume;
                    SessionState.EraseString(PendingTagKey);
                    Debug.LogWarning("[ProjectSurvive] the camp capture never reached play mode; it was dropped.");
                }
            }

            EditorApplication.update += Resume;
        }

        private static void LogResult(Task<string> task)
        {
            if (task.IsFaulted)
            {
                Debug.LogError("[ProjectSurvive] the camp capture failed: " + task.Exception);
                return;
            }

            Debug.Log("[ProjectSurvive] the camp capture finished: " + task.Result);
        }

        private static async Task<string> RunOnMainThread(string tag)
        {
            Directory.CreateDirectory(OutputDirectory);

            CampSceneDirector director = null;
            for (int i = 0; i < 400; i++)
            {
                await Task.Delay(100);
                if (!EditorApplication.isPlaying)
                {
                    continue;
                }

                director = UnityEngine.Object.FindFirstObjectByType<CampSceneDirector>();
                if (director != null && director.Started && director.Run != null)
                {
                    break;
                }
            }

            if (director == null || !director.Started)
            {
                return "the camp did not start";
            }

            CampWorldView world = UnityEngine.Object.FindFirstObjectByType<CampWorldView>();
            CampPlayerView player = UnityEngine.Object.FindFirstObjectByType<CampPlayerView>();
            CampBagScreenView bag =
                UnityEngine.Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
            ArenaEnvironmentView environment = UnityEngine.Object.FindFirstObjectByType<ArenaEnvironmentView>();

            if (world == null || player == null || bag == null)
            {
                return "the camp scene is missing its views";
            }

            var log = new StringBuilder();
            var contrast = new StringBuilder();
            var metrics = new StringBuilder();

            contrast.AppendLine("# Stage 09o readability: WCAG contrast of the things the sala asks the player to read.");
            contrast.AppendLine("# thresholds: ArenaTheme.MinPlayerContrast (player) and CampTheme.MinPropContrast / MinLabelContrast.");
            contrast.AppendLine("# price is dark ink on its own board: feature = 10th percentile inside the label, floor = the board around it.");
            contrast.AppendLine("# price is the DESIGN ratio (the label's ink colour against the board's rendered median), which is what WCAG defines and");
            contrast.AppendLine("# what the AA gate uses; pricePx is the same label measured on its own anti-aliased pixels, reported alongside it because it falls");
            contrast.AppendLine("# when the same text lands on fewer pixels. aa=" + Format(WcagAaText) + " also requires the ink to clear the theme's LabelFloorPixels at 1080p.");
            contrast.AppendLine("# machine: i5-14400F / RTX 5060 Ti / 32 GB, Unity " + Application.unityVersion + " Editor Play Mode");
            metrics.AppendLine("# Stage 09o - batches / draw calls / ms per frame");
            metrics.AppendLine("# budget from Stage 08e: <= 200 batches");
            metrics.AppendLine("# machine: i5-14400F / RTX 5060 Ti / 32 GB, Unity " + Application.unityVersion + " Editor Play Mode");

            CampTheme theme = environment == null ? null : environment.Theme as CampTheme;
            contrast.AppendLine("# theme: " + (theme == null ? "none (before the yard)" : theme.name)
                + (theme == null
                    ? string.Empty
                    : "  MinPlayerContrast=" + Format(theme.MinPlayerContrast)
                      + " MinPropContrast=" + Format(theme.MinPropContrast)
                      + " MinLabelContrast=" + Format(theme.MinLabelContrast)));
            log.AppendLine("environment=" + (environment == null ? "none" : environment.name)
                + " theme=" + (environment == null || environment.Theme == null ? "none" : environment.Theme.name)
                + " shelves=" + world.ShelfCount);

            ArenaTimeOfDay[] profiles = environment == null
                ? new[] { ArenaTimeOfDay.Dusk }
                : new[] { ArenaTimeOfDay.Dusk, ArenaTimeOfDay.Night };

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                for (int p = 0; p < profiles.Length; p++)
                {
                    string profile = environment == null
                        ? "before"
                        : profiles[p] == ArenaTimeOfDay.Night ? "night" : "dusk-camp";

                    if (environment != null)
                    {
                        environment.SetTimeOfDay(profiles[p]);
                        await Task.Delay(700);
                    }

                    for (int state = 0; state < 6; state++)
                    {
                        string name = StateName(state);
                        bag.ShowPanel(state == 5);
                        player.Teleport(StatePosition(world, state));
                        await Task.Delay(350);

                        if (state == 5)
                        {
                            // The panel is a canvas: it is laid out for the Game View's own size, so
                            // it is photographed there rather than re-rendered through a RenderTexture
                            // (the same reason UiCaptureTool's play-mode runs shoot the view itself).
                            string shot = await CaptureScreen(tag, profile, name);
                            metrics.AppendLine(profile + " " + name + " " + shot);
                            continue;
                        }

                        foreach (Vector2Int size in Sizes)
                        {
                            CaptureResult result = await Capture(
                                tag, profile, name, size, world, player, batches, drawCalls, setPass);

                            contrast.AppendLine(ContrastLine(profile, name, size, result, theme));
                            metrics.AppendLine(MetricsLine(profile, name, size, result));
                        }
                    }

                    bag.ShowPanel(false);
                }

                if (environment != null)
                {
                    environment.SetTimeOfDay(ArenaTimeOfDay.Dusk);
                }
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "stage09o-" + tag + "-contrast.txt"), contrast.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "stage09o-" + tag + "-metrics.txt"), metrics + log.ToString());

            EditorApplication.isPlaying = false;
            return contrast + "\n" + metrics + "\n" + log;
        }

        private static string StateName(int state) => state switch
        {
            0 => "centre",
            5 => "bag",
            _ => "shelf" + (state - 1),
        };

        private static Vector3 StatePosition(CampWorldView world, int state) => state switch
        {
            0 => world.SpawnPosition,
            5 => world.ClampToWalkArea(world.ShelfPosition(0)),
            _ => world.ClampToWalkArea(world.ShelfPosition(state - 1)),
        };

        // ------------------------------------------------------------------
        // capture and measurement
        // ------------------------------------------------------------------

        private static async Task<CaptureResult> Capture(
            string tag, string profile, string state, Vector2Int size,
            CampWorldView world, CampPlayerView player,
            ProfilerRecorder batches, ProfilerRecorder drawCalls, ProfilerRecorder setPass)
        {
            await Task.Delay(120);

            // Read the render counters after real frames have gone by in this state, rather than at
            // the call site, where the sample would still describe the previous capture.
            if ((int)batches.LastValue > 0)
            {
                _lastBatches = (int)batches.LastValue;
                _lastDrawCalls = (int)drawCalls.LastValue;
                _lastSetPass = (int)setPass.LastValue;
            }

            float frameMs = await MedianFrameMs();
            Camera camera = world.Camera != null ? world.Camera : Camera.main;

            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            Texture2D image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            var borrowed = new List<Canvas>();

            try
            {
                // The camp's HUD is drawn by overlay canvases. A camera render into a RenderTexture
                // cannot see them, so they are borrowed into camera mode for the shot and put back.
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (canvas.rootCanvas != canvas
                        || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        || !canvas.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    borrowed.Add(canvas);
                }

                // The camp's HUD is an overlay canvas laid out for the Game View's size; re-rendering
                // it into a differently-sized texture would show a layout the player never sees, so
                // the yard shots are taken without it. The panel's own shot keeps it (CaptureScreen).
                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].gameObject.SetActive(false);
                }

                camera.targetTexture = target;
                camera.aspect = size.x / (float)size.y;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string path = Path.Combine(
                    OutputDirectory, tag + "-" + profile + "-" + state + "-" + size.x + "x" + size.y + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());

                Color32[] pixels = image.GetPixels32();
                var result = new CaptureResult
                {
                    File = Path.GetFileName(path),
                    Batches = _lastBatches,
                    DrawCalls = _lastDrawCalls,
                    SetPass = _lastSetPass,
                    FrameMs = frameMs,
                };

                // The measurement reads the same buffer the PNG was written from, so the numbers
                // describe the saved image and not a second render that could differ.
                // Measured by the sprite's own opaque rectangle, not by its quad: the sala's art is
                // silhouettes on transparent quads, and a box that is mostly transparent pixels
                // measures the floor it is standing on instead of the thing standing there.
                SpriteRenderer playerRenderer = Find("Player");
                SpriteRenderer boardRenderer = Find("ShelfBoard0");
                SpriteRenderer drumRenderer = Find("ResetDrum");
                SpriteRenderer potRenderer = Find("AltarPot");
                var labelRenderer = FindComponent<MeshRenderer>("ShelfPrice0");

                result.Player = MeasureSprite(pixels, camera, size, playerRenderer);
                result.Rack = MeasureSprite(pixels, camera, size, boardRenderer);
                result.Drum = MeasureSprite(pixels, camera, size, drumRenderer);
                result.Pot = MeasureSprite(pixels, camera, size, potRenderer);

                if (labelRenderer != null && boardRenderer != null)
                {
                    Rect label = ProjectBox(camera, labelRenderer.bounds, size);
                    Rect board = ProjectBox(camera, boardRenderer.bounds, size);
                    result.Price = MeasureLabel(pixels, label, board, size);
                    result.PriceInkHeight = InkHeight(pixels, label, size);

                    // The design ratio: the ink the label is coloured with (its own TMP colour)
                    // against the board's rendered median. Reporting both keeps the AA claim about
                    // the colours and the rendered number about how the glyphs actually landed.
                    var labelText = labelRenderer.GetComponent<TMPro.TextMeshPro>();
                    if (labelText != null)
                    {
                        result.PriceDesign = new ContrastSample(
                            ArenaContrast.RelativeLuminance(Rgba(labelText.color)),
                            result.Price.Floor, result.Price.FeaturePixels, result.Price.FloorPixels);
                    }
                    result.Rects = "label=" + Short(label) + " board=" + Short(board);
                }

                _lastBatches = result.Batches;
                return result;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;

                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].gameObject.SetActive(true);
                }

                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        /// <summary>
        /// A shot of the Game View exactly as the player sees it, HUD and จัดย่าม panel included.
        /// </summary>
        /// <remarks>
        /// The panel's layout is computed by the canvas scaler from <c>Screen.width/height</c>, so a
        /// RenderTexture of another size would photograph a layout that does not exist. The image is
        /// therefore the view's own, and its size is reported with it.
        /// </remarks>
        private static async Task<string> CaptureScreen(string tag, string profile, string state)
        {
            string path = Path.Combine(
                OutputDirectory,
                tag + "-" + profile + "-" + state + "-" + Screen.width + "x" + Screen.height + ".png");

            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 30 && !File.Exists(path); i++)
            {
                await Task.Delay(100);
            }

            await Task.Delay(200);

            return File.Exists(path)
                ? "screen=" + Screen.width + "x" + Screen.height + " bytes=" + new FileInfo(path).Length
                : "screen=missing " + Screen.width + "x" + Screen.height;
        }

        private static SpriteRenderer Find(string name) =>
            FindComponent<SpriteRenderer>(name);

        /// <summary>
        /// A world box's on-screen rectangle, in pixels with the origin at the bottom-left.
        /// Orthographic arithmetic rather than <c>WorldToScreenPoint</c>: the capture changes
        /// <c>camera.aspect</c> and <c>targetTexture</c>, and the camera's cached projection is not
        /// guaranteed to have been rebuilt when a measurement asks for it.
        /// </summary>
        private static Rect ProjectBox(Camera camera, Bounds bounds, Vector2Int size)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float xMin = float.MaxValue;
            float yMin = float.MaxValue;
            float xMax = float.MinValue;
            float yMax = float.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);

                Vector3 local = camera.transform.InverseTransformPoint(point);
                float halfHeight = camera.orthographicSize;
                float halfWidth = halfHeight * (size.x / (float)size.y);
                float x = (((local.x / halfWidth) * 0.5f) + 0.5f) * size.x;
                float y = (((local.y / halfHeight) * 0.5f) + 0.5f) * size.y;

                xMin = Mathf.Min(xMin, x);
                yMin = Mathf.Min(yMin, y);
                xMax = Mathf.Max(xMax, x);
                yMax = Mathf.Max(yMax, y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static readonly Dictionary<int, Vector4> OpaqueCache = new Dictionary<int, Vector4>(16);

        /// <summary>
        /// Where a sprite's non-transparent pixels sit inside its own quad, as a 0 … 1 rectangle
        /// (xMin, yMin, xMax, yMax). A silhouette drawn on a 128 × 128 sheet covers a fraction of
        /// it, and measuring the empty part would report the ground behind the figure.
        /// </summary>
        private static Vector4 OpaqueFraction(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            int key = sprite.GetInstanceID();
            if (OpaqueCache.TryGetValue(key, out Vector4 cached))
            {
                return cached;
            }

            Rect rect = sprite.textureRect;
            Color32[] pixels = sprite.texture.GetPixels32();
            int width = sprite.texture.width;
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;

            for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
            {
                for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
                {
                    if (pixels[(y * width) + x].a < 8)
                    {
                        continue;
                    }

                    xMin = Mathf.Min(xMin, x);
                    xMax = Mathf.Max(xMax, x);
                    yMin = Mathf.Min(yMin, y);
                    yMax = Mathf.Max(yMax, y);
                }
            }

            if (xMin > xMax || yMin > yMax)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            var fraction = new Vector4(
                (xMin - rect.xMin) / rect.width,
                (yMin - rect.yMin) / rect.height,
                (xMax + 1 - rect.xMin) / rect.width,
                (yMax + 1 - rect.yMin) / rect.height);

            OpaqueCache[key] = fraction;
            return fraction;
        }

        private static T FindComponent<T>(string name) where T : Component
        {
            GameObject go = GameObject.Find("CampWorld/" + name);
            return go == null ? null : go.GetComponent<T>();
        }

        /// <summary>One sprite measured by the rectangle its own art covers.</summary>
        private static ContrastSample MeasureSprite(
            Color32[] pixels, Camera camera, Vector2Int size, SpriteRenderer renderer)
        {
            if (renderer == null || !renderer.enabled)
            {
                return default;
            }

            Rect rect = ProjectBox(camera, renderer.bounds, size);
            Vector4 opaque = OpaqueFraction(renderer.sprite);
            rect = Rect.MinMaxRect(
                rect.xMin + (rect.width * opaque.x),
                rect.yMin + (rect.height * opaque.y),
                rect.xMin + (rect.width * opaque.z),
                rect.yMin + (rect.height * opaque.w));

            return MeasureRect(pixels, size, box: ToBox(rect, size));
        }

        private static ContrastSample MeasureRect(
            Color32[] pixels, Vector2Int size, RectInt box, float percentile = 0.90f)
        {
            if (box.width <= 0 || box.height <= 0)
            {
                return default;
            }

            int margin = Mathf.Max(12, Mathf.RoundToInt(box.width * 0.8f));
            return ArenaContrast.Measure(
                pixels, size.x, size.y, box, ArenaContrast.FloorAround(box, margin), percentile);
        }

        /// <summary>A dark ink label on its own board: the glyph pixels against the board they sit on.</summary>
        /// <remarks>
        /// The ink is the median of the label's own dark pixels (anything under half the board's
        /// luminance), not a low percentile of the whole box: Thai glyphs are thin, so a box that is
        /// nine tenths board reports the board's own antialiasing as if it were the text.
        /// </remarks>
        private static ContrastSample MeasureLabel(
            Color32[] pixels, Rect label, Rect board, Vector2Int size)
        {
            RectInt box = ToBox(label, size);
            RectInt around = ToBox(board, size);

            float paper = ArenaContrast.Percentile(pixels, size.x, size.y, around, 0.50f, box);
            float ink = InkLuminance(pixels, size, box, paper * 0.5f);

            return new ContrastSample(
                ink, paper,
                ArenaContrast.Count(size.x, size.y, box, default),
                ArenaContrast.Count(size.x, size.y, around, box));
        }

        /// <summary>The median luminance of the pixels darker than <paramref name="threshold"/>.</summary>
        private static float InkLuminance(Color32[] pixels, Vector2Int size, RectInt box, float threshold)
        {
            var histogram = new int[256];
            int total = 0;

            for (int y = Mathf.Clamp(box.yMin, 0, size.y); y < Mathf.Clamp(box.yMax, 0, size.y); y++)
            {
                for (int x = Mathf.Clamp(box.xMin, 0, size.x); x < Mathf.Clamp(box.xMax, 0, size.x); x++)
                {
                    float luminance = ArenaContrast.RelativeLuminance(pixels[(y * size.x) + x]);
                    if (luminance > threshold)
                    {
                        continue;
                    }

                    histogram[Mathf.Clamp(Mathf.RoundToInt(luminance * 255f), 0, 255)]++;
                    total++;
                }
            }

            if (total == 0)
            {
                // No glyph pixels at all: the label is not there, and no contrast can be claimed.
                return threshold;
            }

            int target = total / 2;
            int seen = 0;
            for (int bucket = 0; bucket < histogram.Length; bucket++)
            {
                seen += histogram[bucket];
                if (seen > target)
                {
                    return bucket / 255f;
                }
            }

            return threshold;
        }

        /// <summary>
        /// The tallest run of ink rows inside a label, in pixels: the number the stage's 22 px rule is
        /// stated in. Read from the pixels, not from the font size, so a label drawn smaller than it
        /// is authored shows up here.
        /// </summary>
        private static float InkHeight(Color32[] pixels, Rect label, Vector2Int size)
        {
            RectInt box = ToBox(label, size);
            float paper = ArenaContrast.Percentile(pixels, size.x, size.y, box, 0.50f, default);
            float threshold = paper - 0.12f;

            int best = 0;
            int run = 0;
            for (int y = Mathf.Clamp(box.yMin, 0, size.y); y < Mathf.Clamp(box.yMax, 0, size.y); y++)
            {
                bool ink = false;
                for (int x = Mathf.Clamp(box.xMin, 0, size.x); x < Mathf.Clamp(box.xMax, 0, size.x); x++)
                {
                    if (ArenaContrast.RelativeLuminance(pixels[(y * size.x) + x]) <= threshold)
                    {
                        ink = true;
                        break;
                    }
                }

                run = ink ? run + 1 : 0;
                if (run > best)
                {
                    best = run;
                }
            }

            return best;
        }

        private static RectInt ToBox(Rect rect, Vector2Int size)
        {
            int minX = Mathf.RoundToInt(rect.xMin);
            int maxX = Mathf.RoundToInt(rect.xMax);
            int minY = Mathf.RoundToInt(rect.yMin);
            int maxY = Mathf.RoundToInt(rect.yMax);

            return new RectInt(minX, minY, Mathf.Max(0, maxX - minX), Mathf.Max(0, maxY - minY));
        }

        private static async Task<float> MedianFrameMs()
        {
            var samples = new List<float>(40);
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(16);
                samples.Add(Time.unscaledDeltaTime * 1000f);
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        /// <summary>
        /// A <see cref="SynchronizationContext"/> that queues async continuations and runs them from
        /// <see cref="EditorApplication.update"/>, i.e. on the Editor's main thread, whichever thread
        /// or context started the work.
        /// </summary>
        /// <remarks>
        /// Unity pumps its own context in the player loop, so an <c>await</c> started by a menu click
        /// resumes on its own. A menu item executed by the command line runs under the pipeline's
        /// context instead, which nothing pumps after the command returns: continuations posted there
        /// are never run, and the task simply stops — with no exception and no log. Installing this
        /// context around the run makes resumption depend on the editor's own update, which ticks in
        /// both cases. <see cref="Send"/> is inline because every call comes from the main thread.
        /// </remarks>
        private sealed class MainThreadPump : SynchronizationContext, IDisposable
        {
            private readonly Queue<(SendOrPostCallback Callback, object State)> _queue =
                new Queue<(SendOrPostCallback, object)>();

            private readonly object _gate = new object();
            private bool _disposed;

            public MainThreadPump()
            {
                EditorApplication.update += Drain;
            }

            public override void Post(SendOrPostCallback d, object state)
            {
                lock (_gate)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _queue.Enqueue((d, state));
                }
            }

            public override void Send(SendOrPostCallback d, object state) => d(state);

            public void Dispose()
            {
                EditorApplication.update -= Drain;

                lock (_gate)
                {
                    _disposed = true;
                    _queue.Clear();
                }
            }

            private void Drain()
            {
                while (true)
                {
                    SendOrPostCallback callback;
                    object state;

                    lock (_gate)
                    {
                        if (_disposed || _queue.Count == 0)
                        {
                            return;
                        }

                        (callback, state) = _queue.Dequeue();
                    }

                    try
                    {
                        callback(state);
                    }
                    catch (Exception exception)
                    {
                        // One broken continuation must not swallow the queue behind it.
                        Debug.LogException(exception);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // report lines
        // ------------------------------------------------------------------

        private static string ContrastLine(
            string profile, string state, Vector2Int size, CaptureResult result, CampTheme theme)
        {
            float propMinimum = theme == null ? 1.9f : theme.MinPropContrast;
            float labelMinimum = theme == null ? 4.5f : theme.MinLabelContrast;
            float playerMinimum = theme == null ? 2.2f : theme.MinPlayerContrast;

            bool pass = result.Player.Ratio >= playerMinimum
                && (result.Rack.FeaturePixels == 0 || result.Rack.Ratio >= propMinimum)
                && (result.Drum.FeaturePixels == 0 || result.Drum.Ratio >= propMinimum)
                && (result.Pot.FeaturePixels == 0 || result.Pot.Ratio >= propMinimum)
                && (result.PriceDesign.FeaturePixels == 0 || result.PriceDesign.Ratio >= labelMinimum)
                && (size.y != 1080 || result.PriceInkHeight >= (theme == null ? 22f : theme.LabelFloorPixels));

            return profile + " " + state + " " + size.x + "x" + size.y
                + "  player=" + Format(result.Player.Ratio) + " (min " + Format(playerMinimum) + ")"
                + "  shelf=" + Format(result.Rack.Ratio) + " (min " + Format(propMinimum) + ")"
                + "  drum=" + Format(result.Drum.Ratio) + " (min " + Format(propMinimum) + ")"
                + "  pot=" + Format(result.Pot.Ratio) + " (min " + Format(propMinimum) + ")"
                + "  price=" + Format(result.PriceDesign.Ratio) + " (min " + Format(labelMinimum) + ")"
                + "  pricePx=" + Format(result.Price.Ratio)
                + "  priceInkPx=" + result.PriceInkHeight.ToString("0", CultureInfo.InvariantCulture)
                + "  aa=" + (result.PriceDesign.Ratio >= WcagAaText
                    && (size.y != 1080 || result.PriceInkHeight >= (theme == null ? 22f : theme.LabelFloorPixels)))
                + "  | luminance player=" + Format(result.Player.Feature) + "/" + Format(result.Player.Floor)
                + " shelf=" + Format(result.Rack.Feature) + "/" + Format(result.Rack.Floor)
                + " price=" + Format(result.PriceDesign.Feature) + "/" + Format(result.PriceDesign.Floor)
                + "  pass=" + pass;
        }

        private static string MetricsLine(string profile, string state, Vector2Int size, CaptureResult result)
        {
            return profile + " " + state + " " + size.x + "x" + size.y
                + ": batches=" + result.Batches
                + " draw=" + result.DrawCalls
                + " setPass=" + result.SetPass
                + " frameMs=" + Format(result.FrameMs)
                + " overBudget=" + (result.Batches > 200)
                + " file=" + result.File
                + "  (" + result.Rects + ")";
        }

        private static string Short(Rect rect) =>
            rect.x.ToString("0", CultureInfo.InvariantCulture) + ","
            + rect.y.ToString("0", CultureInfo.InvariantCulture) + " "
            + rect.width.ToString("0", CultureInfo.InvariantCulture) + "x"
            + rect.height.ToString("0", CultureInfo.InvariantCulture);

        private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>An authored colour as the 8-bit pixel the ratio is computed on.</summary>
        private static Color32 Rgba(Color colour) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.b) * 255f),
            255);
    }
}
