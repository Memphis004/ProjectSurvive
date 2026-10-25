using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace AgentScripts
{
    /// <summary>
    /// Stage 08e evidence: the six screenshots and the numbers that go with them.
    /// </summary>
    /// <remarks>
    /// One run produces Day, Night and Night+300 ghosts at 1920×1080 and 1280×720, and measures
    /// what a reviewer cannot see in a picture: batches, draw calls, set-pass calls and ms/frame at
    /// each state, plus the luminance contrast between each actor and the floor it stands on.
    ///
    /// The capture is the project's own method (UiCaptureTool): the camera is pointed at a
    /// RenderTexture of the exact size, overlay canvases are borrowed into screen-space-camera for
    /// the render and restored afterwards. Nothing about the scene or the assets is changed by a
    /// capture — Day and Night are the environment's own one call.
    /// </remarks>
    public static class ArenaEnvironmentCapture
    {
        private const uint Seed = 20261024u;

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "battle-env"));

        public static async Task<string> Run()
        {
            Directory.CreateDirectory(OutputDirectory);

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            BattleSimulationRunner runner = null;
            for (int i = 0; i < 400; i++)
            {
                await Task.Delay(100);
                if (!EditorApplication.isPlaying)
                {
                    continue;
                }

                var director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
                if (director != null && director.Runner != null && director.Runner.IsRunning)
                {
                    runner = director.Runner;
                    break;
                }
            }

            if (runner == null)
            {
                return "battle did not start";
            }

            runner.Submit(BattleCommand.DevGodMode(true));
            await Task.Delay(1500);

            var log = new StringBuilder();
            var metrics = new List<string>();
            var contrast = new List<LumSample>();

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1))
            {
                // Three states in one session: the floor as it starts, the same floor at night, then
                // night with the enemy ceiling on it. Day and night are one call each on the same
                // loaded scene, and not a single asset is reloaded between them.
                var states = new[]
                {
                    ("day", false, 0),
                    ("night", true, 0),
                    ("night300", true, 300),
                };

                foreach ((string state, bool night, int ghosts) in states)
                {
                    if (ghosts > 0)
                    {
                        int spawned = 0;
                        for (int i = 0; i < ghosts; i++)
                        {
                            if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                            {
                                spawned++;
                            }
                        }

                        await Task.Delay(3500);
                        log.AppendLine("spawned " + spawned + " ghosts");
                    }

                    runner.View.Environment?.SetNight(night);
                    await Task.Delay(900);

                    foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                    {
                        float frameMs = await AverageFrameMs();
                        Texture2D image = Capture(state, size.x, size.y, out string path);
                        string line = state + " " + size.x + "x" + size.y
                            + ": batches=" + batches.LastValue
                            + " draw=" + drawCalls.LastValue
                            + " setPass=" + setPass.LastValue
                            + " tris=" + tris.LastValue
                            + " frameMs=" + frameMs.ToString("0.00", CultureInfo.InvariantCulture)
                            + " bound=" + runner.View.BoundViews
                            + " entities=" + runner.View.Snapshot.CurrentCount
                            + " file=" + Path.GetFileName(path);

                        log.AppendLine(line);
                        Debug.Log("[ArenaCapture] " + line);

                        if (state == "night300" && size.x == 1920)
                        {
                            MeasureContrast(image, Camera.main, contrast, log);
                        }

                        UnityEngine.Object.DestroyImmediate(image);
                    }
                }

                metrics.Add("{\n  \"seed\": " + Seed);
                metrics.Add(", \"states\": [\"day\", \"night\", \"night+300\"]");
                metrics.Add(", \"boundViews\": " + runner.View.BoundViews);
                metrics.Add(", \"entities\": " + runner.View.Snapshot.CurrentCount);
                metrics.Add(", \"createdAfterWarmup\": " + runner.View.Pool.CreatedAfterWarmup);
                metrics.Add("\n}");
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "metrics.txt"), log.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "luminance.json"), ContrastReport(contrast));

            EditorApplication.isPlaying = false;
            return log.ToString() + "\n" + ContrastReport(contrast);
        }

        // ------------------------------------------------------------------
        // capture
        // ------------------------------------------------------------------

        /// <summary>
        /// Renders the battle camera into an image of exactly <paramref name="width"/>×
        /// <paramref name="height"/> and writes it. Returns the image (the caller disposes it) and
        /// the frame time measured while waiting for it.
        /// </summary>
        private static Texture2D Capture(string state, int width, int height, out string path)
        {
            Camera camera = Camera.main;
            var borrowed = new List<Canvas>();
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
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

                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].renderMode = RenderMode.ScreenSpaceCamera;
                    borrowed[i].worldCamera = camera;
                    borrowed[i].planeDistance = camera.nearClipPlane + 1f;
                }

                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                path = Path.Combine(
                    OutputDirectory, "stage08e-" + state + "-" + width + "x" + height + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;

                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].renderMode = RenderMode.ScreenSpaceOverlay;
                    borrowed[i].worldCamera = null;
                }

                RenderTexture.ReleaseTemporary(target);
            }

            return image;
        }

        /// <summary>
        /// Frame time over about half a second, sampled on the main thread while the game keeps
        /// running. A single frame is not a measurement; this is the median of ~30 of them.
        /// </summary>
        private static async Task<float> AverageFrameMs()
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

        // ------------------------------------------------------------------
        // luminance
        // ------------------------------------------------------------------

        private readonly struct LumSample
        {
            public LumSample(string kind, float contrast) => (Kind, Contrast) = (kind, contrast);
            public string Kind { get; }
            public float Contrast { get; }
        }

        /// <summary>
        /// How far each actor's brightest pixel stands above the floor around it.
        /// </summary>
        /// <remarks>
        /// The floor reference is the <b>median</b> of a ring of samples at 1.8× the actor's screen
        /// radius: in a crowd a handful of ring samples land on neighbours and a median ignores them,
        /// while a mean would not. The actor reference is the <b>brightest</b> pixel inside its own
        /// bounds — its rim — because that is the edge the eye uses to separate it from the ground.
        /// WCAG's ratio form (L1+0.05)/(L2+0.05) is used so the number is a familiar one.
        /// </remarks>
        private static void MeasureContrast(Texture2D image, Camera camera, List<LumSample> into, StringBuilder log)
        {
            if (camera == null)
            {
                return;
            }

            Color32[] pixels = image.GetPixels32();
            int width = image.width;
            int height = image.height;

            var viewRoot = GameObject.Find("ViewRoot");
            if (viewRoot == null)
            {
                return;
            }

            var players = new List<SpriteRenderer>();
            var ghosts = new List<SpriteRenderer>();
            var drops = new List<SpriteRenderer>();

            foreach (SpriteRenderer renderer in viewRoot.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (!renderer.enabled || renderer.sprite == null)
                {
                    continue;
                }

                switch (renderer.gameObject.name)
                {
                    case "view-player":
                        players.Add(renderer);
                        break;
                    case "view-ghost":
                        ghosts.Add(renderer);
                        break;
                    case "view-drop":
                        drops.Add(renderer);
                        break;
                }
            }

            // One floor reference for the whole frame, measured where the floor is visible rather
            // than around each actor: in a 315-entity crowd a ring around a ghost is more likely to
            // land on another ghost than on the ground, which is what made a per-actor ring read as
            // "contrast 1.0" — it was comparing a ghost with a ghost.
            var actorRects = new List<Rect>();
            foreach (SpriteRenderer renderer in players.Concat(drops).Concat(ghosts))
            {
                Rect rect = ScreenRect(renderer, camera);
                if (rect.xMax > 0f && rect.yMax > 0f && rect.xMin < width && rect.yMin < height)
                {
                    actorRects.Add(rect);
                }
            }

            float floor = FloorReference(pixels, width, height, actorRects);
            log.AppendLine("floor reference luminance=" + floor.ToString("0.000", CultureInfo.InvariantCulture));

            Sample(players, "player", camera, pixels, width, height, floor, into, log);
            Sample(drops, "pickup", camera, pixels, width, height, floor, into, log);
            Sample(ghosts.Take(40).ToList(), "ghost", camera, pixels, width, height, floor, into, log);
        }

        /// <summary>Where a sprite lands on screen, in pixels.</summary>
        private static Rect ScreenRect(SpriteRenderer renderer, Camera camera)
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = camera.WorldToScreenPoint(bounds.min);
            Vector3 max = camera.WorldToScreenPoint(bounds.max);

            float xMin = Mathf.Min(min.x, max.x);
            float yMin = Mathf.Min(min.y, max.y);
            return new Rect(xMin, yMin, Mathf.Max(1f, Mathf.Abs(max.x - min.x)), Mathf.Max(1f, Mathf.Abs(max.y - min.y)));
        }

        /// <summary>
        /// The floor's own luminance: the median of a sparse grid over the part of the frame the
        /// camera sees most ground in, with every actor's box excluded. A median ignores the few
        /// decals, tufts and trees a sample still lands on.
        /// </summary>
        private static float FloorReference(Color32[] pixels, int width, int height, List<Rect> actors)
        {
            var samples = new List<float>(4096);
            int limit = Mathf.RoundToInt(height * 0.35f);

            for (int y = 0; y < limit; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    bool inside = false;
                    for (int i = 0; i < actors.Count; i++)
                    {
                        if (actors[i].Contains(new Vector2(x, y)))
                        {
                            inside = true;
                            break;
                        }
                    }

                    if (!inside)
                    {
                        samples.Add(Luminance(pixels, width, height, x, y));
                    }
                }
            }

            if (samples.Count == 0)
            {
                return 0.05f;
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        private static void Sample(
            List<SpriteRenderer> renderers, string kind, Camera camera, Color32[] pixels,
            int width, int height, float floor, List<LumSample> into, StringBuilder log)
        {
            if (renderers.Count == 0)
            {
                log.AppendLine("contrast: no " + kind + " on screen");
                return;
            }

            for (int i = 0; i < renderers.Count; i++)
            {
                Rect rect = ScreenRect(renderers[i], camera);
                if (rect.width <= 2f || rect.height <= 2f)
                {
                    continue;
                }

                // An actor off the edge of the frame is not a contrast failure; it is not in the
                // picture at all.
                if (rect.xMax <= 0f || rect.yMax <= 0f || rect.xMin >= width || rect.yMin >= height)
                {
                    continue;
                }

                rect.xMin = Mathf.Max(rect.xMin, 0f);
                rect.yMin = Mathf.Max(rect.yMin, 0f);
                rect.xMax = Mathf.Min(rect.xMax, width - 1f);
                rect.yMax = Mathf.Min(rect.yMax, height - 1f);

                // Inset a little so the box is the body, not its antialiased edge.
                int x0 = Mathf.RoundToInt(rect.xMin + (rect.width * 0.1f));
                int x1 = Mathf.RoundToInt(rect.xMax - (rect.width * 0.1f));
                int y0 = Mathf.RoundToInt(rect.yMin + (rect.height * 0.1f));
                int y1 = Mathf.RoundToInt(rect.yMax - (rect.height * 0.1f));

                float brightest = 0f;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float luminance = Luminance(pixels, width, height, x, y);
                        if (luminance > brightest)
                        {
                            brightest = luminance;
                        }
                    }
                }

                into.Add(new LumSample(kind, (brightest + 0.05f) / (floor + 0.05f)));
            }

            var own = into.FindAll(sample => sample.Kind == kind);
            if (own.Count == 0)
            {
                return;
            }

            var sorted = own.Select(sample => sample.Contrast).OrderBy(value => value).ToList();
            log.AppendLine(
                "contrast " + kind + ": n=" + own.Count
                + " min=" + sorted[0].ToString("0.00", CultureInfo.InvariantCulture)
                + " median=" + sorted[sorted.Count / 2].ToString("0.00", CultureInfo.InvariantCulture)
                + " max=" + sorted[sorted.Count - 1].ToString("0.00", CultureInfo.InvariantCulture));
        }

        private static float Luminance(Color32[] pixels, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                return 0f;
            }

            Color32 pixel = pixels[(y * width) + x];
            return ((0.2126f * pixel.r) + (0.7152f * pixel.g) + (0.0722f * pixel.b)) / 255f;
        }

        private static string ContrastReport(List<LumSample> samples)
        {
            var json = new StringBuilder();
            json.Append("{\n");

            bool first = true;
            foreach (string kind in new[] { "player", "ghost", "pickup" })
            {
                var own = samples.FindAll(sample => sample.Kind == kind)
                    .Select(sample => sample.Contrast)
                    .OrderBy(value => value)
                    .ToList();

                if (!first)
                {
                    json.Append(",\n");
                }

                first = false;
                json.Append("  \"").Append(kind).Append("\": { \"n\": ").Append(own.Count);
                if (own.Count > 0)
                {
                    json.Append(", \"min\": ").Append(own[0].ToString("0.000", CultureInfo.InvariantCulture));
                    json.Append(", \"median\": ").Append(own[own.Count / 2].ToString("0.000", CultureInfo.InvariantCulture));
                    json.Append(", \"max\": ").Append(own[own.Count - 1].ToString("0.000", CultureInfo.InvariantCulture));
                }

                json.Append(" }");
            }

            json.Append("\n}\n");
            return json.ToString();
        }
    }
}
