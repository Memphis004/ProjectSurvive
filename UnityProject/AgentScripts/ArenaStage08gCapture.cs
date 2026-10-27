using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using ProjectSurvive.Game.Presentation.Battle.Vfx;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace AgentScripts
{
    /// <summary>
    /// Stage 08g evidence: the arena's night look, empty and under the Stage 07 load, with the
    /// frame's own average hue / luminance / saturation measured on the pixels that were saved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stage's acceptance is a claim about a picture — "the night reads closer to the target's
    /// tone" — so the measurement has to be a number taken from the same buffer the PNG was written
    /// from, not an impression. <see cref="Tone"/> computes the mean linear luminance, the mean hue
    /// (circular, saturated pixels only) and the mean saturation, over the whole frame and over the
    /// central 60 % box the fight is read in.
    /// </para>
    /// <para>
    /// Run it twice: once on the tree as delivered (the <c>before</c> prefix) and once after the
    /// look-dev pass (<c>after</c>), and the two tone lines are the before/after the report quotes.
    /// The prefix comes from the environment variable <c>PS_STAGE08G_TAG</c> so nothing in this file
    /// has to be edited between the two runs.
    /// </para>
    /// <para>
    /// Contrast is measured too, unchanged from Stage 08f (<see cref="ArenaContrast"/>): a look that
    /// darkens the frame must not take the doctor, a ghost, a pickup or a telegraph under the
    /// thresholds the theme already ships.
    /// </para>
    /// </remarks>
    public static class ArenaStage08gCapture
    {
        private const int TotalGhosts = 300;
        private const int QuietGhosts = 12;

        private static readonly Vector2Int Size1920 = new Vector2Int(1920, 1080);
        private static readonly Vector2Int Size1280 = new Vector2Int(1280, 720);

        /// <summary>
        /// The label the run's files carry: <c>before</c> for the tree as delivered, <c>after</c> for
        /// the look-dev pass. It is a file rather than a field so the two runs differ by one line of
        /// text on disk, and an environment variable cannot reach an Editor that was started before it.
        /// </summary>
        private static string Tag
        {
            get
            {
                string path = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "..", "artifacts", "ui", "battle-lookdev", "tag.txt"));
                if (!File.Exists(path))
                {
                    return "shot";
                }

                string tag = File.ReadAllText(path).Trim();
                return string.IsNullOrEmpty(tag) ? "shot" : tag;
            }
        }

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "battle-lookdev"));

        public static async Task<string> Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            Log("start tag=" + Tag + " out=" + OutputDirectory);

            // <b>Play mode is started from the outside.</b> This script is compiled into an
            // ephemeral assembly; entering play mode reloads the domain, which unloads that assembly
            // and takes the pending continuation with it — the run then dies silently after the first
            // line. So the caller presses play (and waits for the arena), and this method only waits
            // for the runner it finds there.
            if (!EditorApplication.isPlaying)
            {
                Log("not in play mode: start it first");
                return "not in play mode";
            }

            BattleSimulationRunner runner = null;
            bool pushed = false;
            for (int i = 0; i < 1800; i++)
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

                // The run reaches the arena through the camp, which waits for the player. Press that
                // departure (the same use case the camp's own control calls) rather than relying on a
                // previous session having left a floor running. `Depart()` refuses safely when the
                // run is not in a state to leave, so a retry is harmless.
                if (!pushed || i % 20 == 0)
                {
                    var camp = UnityEngine.Object.FindFirstObjectByType<ProjectSurvive.Game.Camp.CampSceneDirector>();
                    if (camp != null && camp.Presenter != null && !camp.Presenter.Departed)
                    {
                        Log("pressing the camp's departure");
                        camp.Presenter.Depart();
                        pushed = true;
                    }
                }
            }

            if (runner == null)
            {
                Log("battle did not start");
                return "battle did not start";
            }

            Log("runner up");

            ArenaEnvironmentView environment = runner.View.Environment;
            if (environment == null)
            {
                return "the battle view has no arena environment";
            }

            runner.Submit(BattleCommand.DevGodMode(true));
            await Task.Delay(400);

            var tone = new StringBuilder();
            tone.AppendLine("# Stage 08g tone: mean linear luminance, circular mean hue (saturated pixels) and mean saturation");
            tone.AppendLine("# tag=" + Tag + "  machine: i5-14400F / RTX 5060 Ti / 32 GB, Unity " + Application.unityVersion + " Editor Play Mode");
            tone.AppendLine("# whole = the entire 16:9 frame, centre = the central 60% box the fight is read in");

            var metrics = new StringBuilder();
            metrics.AppendLine("# Stage 08g — batches / draw calls / set-pass / ms per frame");
            metrics.AppendLine("# tag=" + Tag + "  machine: i5-14400F / RTX 5060 Ti / 32 GB, Unity " + Application.unityVersion + " Editor Play Mode");

            var contrast = new StringBuilder();
            contrast.AppendLine("# Stage 08g readability (Stage 08f thresholds, unchanged)");
            contrast.AppendLine("# tag=" + Tag);
            contrast.AppendLine("# thresholds: " + Thresholds(environment.Theme));

            // ---- night, empty ----
            environment.SetTimeOfDay(ArenaTimeOfDay.Night);
            await Task.Delay(1200);

            CaptureResult nightEmpty = await Capture(runner, environment, "night-empty", Size1920);
            tone.AppendLine(ToneLine("night-empty", Size1920, nightEmpty));
            metrics.AppendLine(MetricsLine("night-empty", Size1920, nightEmpty, runner));
            contrast.AppendLine(ContrastLine("night-empty", Size1920, nightEmpty, environment.Theme));

            CaptureResult nightEmptySmall = await Capture(runner, environment, "night-empty", Size1280);
            metrics.AppendLine(MetricsLine("night-empty", Size1280, nightEmptySmall, runner));
            contrast.AppendLine(ContrastLine("night-empty", Size1280, nightEmptySmall, environment.Theme));

            // ---- dusk, for the report's reference row ----
            environment.SetTimeOfDay(ArenaTimeOfDay.Dusk);
            await Task.Delay(1000);
            CaptureResult dusk = await Capture(runner, environment, "dusk-empty", Size1920);
            tone.AppendLine(ToneLine("dusk-empty", Size1920, dusk));
            metrics.AppendLine(MetricsLine("dusk-empty", Size1920, dusk, runner));

            // ---- the load: 300 ghosts, measured at night ----
            environment.SetTimeOfDay(ArenaTimeOfDay.Night);
            await Task.Delay(600);

            // The probes draw the real pickup and telegraph sprites at known spots, so the contrast
            // sample describes the art the game uses. Stage 08f's reasoning, unchanged.
            Vector3 player = PlayerPosition();
            GameObject pickupProbe = Probe(
                "contrast-pickup", runner.View.Art.Pickup, Color.white,
                player + new Vector3(3.6f, 0.5f, 1.6f),
                BattleWorldMap.SortingOrder(RenderEntityKind.Pickup, player.z), 1f);
            GameObject telegraphProbe = Probe(
                "contrast-telegraph", runner.View.Art.TelegraphCircle,
                TelegraphPool.TintFor(TelegraphShape.Circle),
                player + new Vector3(-4.2f, 0.01f, 1.2f),
                BattleViewCaps.TelegraphSortingOrder, 3.2f);

            int spawned = 0;
            for (int i = 0; i < QuietGhosts; i++)
            {
                if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                {
                    spawned++;
                }
            }

            // Wait until some ghosts are inside the view: a ghost measured off-screen samples the
            // clamped image edge, which is floor, not a ghost.
            for (int i = 0; i < 120; i++)
            {
                await Task.Delay(200);
                if (VisibleGhosts(runner) >= 3)
                {
                    break;
                }
            }

            await Task.Delay(800);

            CaptureResult nightQuiet = await Capture(runner, environment, "night-ghosts", Size1920);
            tone.AppendLine(ToneLine("night-ghosts", Size1920, nightQuiet));
            metrics.AppendLine(MetricsLine("night-ghosts", Size1920, nightQuiet, runner));
            contrast.AppendLine(ContrastLine("night-ghosts", Size1920, nightQuiet, environment.Theme));

            int extra = 0;
            for (int i = QuietGhosts; i < TotalGhosts; i++)
            {
                if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                {
                    extra++;
                }
            }

            await Task.Delay(3000);

            CaptureResult night300 = await Capture(runner, environment, "night-300", Size1920);
            tone.AppendLine(ToneLine("night-300", Size1920, night300));
            metrics.AppendLine(MetricsLine("night-300", Size1920, night300, runner));
            contrast.AppendLine(ContrastLine("night-300", Size1920, night300, environment.Theme));

            CaptureResult night300Small = await Capture(runner, environment, "night-300", Size1280);
            metrics.AppendLine(MetricsLine("night-300", Size1280, night300Small, runner));
            contrast.AppendLine(ContrastLine("night-300", Size1280, night300Small, environment.Theme));

            UnityEngine.Object.Destroy(pickupProbe);
            UnityEngine.Object.Destroy(telegraphProbe);

            string suffix = "-" + Tag + ".txt";
            File.WriteAllText(Path.Combine(OutputDirectory, "stage08g-tone" + suffix), tone.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "stage08g-metrics" + suffix), metrics.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "stage08g-contrast" + suffix), contrast.ToString());

            EditorApplication.isPlaying = false;
            return tone + "\n" + metrics + "\n" + contrast;
        }

        private static string TracePath =>
            Path.Combine(OutputDirectory, "trace-" + Tag + ".log");

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(TracePath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + "\n");
            }
            catch (Exception)
            {
                // The trace is a debugging aid only; never let it be the failure.
            }

            Debug.Log("[Stage08g] " + message);
        }

        // ------------------------------------------------------------------
        // capture
        // ------------------------------------------------------------------

        private static int _lastBatches;
        private static int _lastDrawCalls;
        private static int _lastSetPass;

        private struct CaptureResult
        {
            public string File;
            public int Entities;
            public int Batches;
            public int DrawCalls;
            public int SetPass;
            public float FrameMs;
            public ToneStats Whole;
            public ToneStats Centre;
            public ContrastSample Player;
            public ContrastSample Ghost;
            public ContrastSample Pickup;
            public ContrastSample Telegraph;
            public int GhostsMeasured;
        }

        /// <summary>Mean luminance, hue and saturation of a region of the saved frame.</summary>
        private struct ToneStats
        {
            public float Luminance;
            public float HueDegrees;
            public float Saturation;
        }

        private static async Task<CaptureResult> Capture(
            BattleSimulationRunner runner, ArenaEnvironmentView environment, string state, Vector2Int size)
        {
            float frameMs = await MedianFrameMs();

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                await Task.Delay(250);

                if ((int)batches.LastValue > 0)
                {
                    _lastBatches = (int)batches.LastValue;
                    _lastDrawCalls = (int)drawCalls.LastValue;
                    _lastSetPass = (int)setPass.LastValue;
                }

                Camera camera = Camera.main;
                RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                RenderTexture previousTarget = camera.targetTexture;
                float previousAspect = camera.aspect;
                Texture2D image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                var borrowed = new List<Canvas>();

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
                    camera.aspect = size.x / (float)size.y;
                    Canvas.ForceUpdateCanvases();
                    camera.Render();

                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;

                    string path = Path.Combine(
                        OutputDirectory, "stage08g-" + state + "-" + size.x + "x" + size.y + "-" + Tag + ".png");
                    File.WriteAllBytes(path, image.EncodeToPNG());

                    Color32[] pixels = image.GetPixels32();
                    var result = new CaptureResult
                    {
                        File = Path.GetFileName(path),
                        Entities = runner.View.Snapshot.CurrentCount,
                        Batches = _lastBatches,
                        DrawCalls = _lastDrawCalls,
                        SetPass = _lastSetPass,
                        FrameMs = frameMs,
                    };

                    result.Whole = MeasureTone(pixels, size, 0f, 0f, 1f, 1f);
                    result.Centre = MeasureTone(pixels, size, 0.20f, 0.20f, 0.60f, 0.60f);
                    Log("saved " + Path.GetFileName(path));

                    result.Player = MeasureBillboard(
                        pixels, camera, PlayerPosition() + new Vector3(0f, 0.5f, 0f), 1f, size);
                    result.Ghost = NearestGhosts(pixels, camera, runner, size, out result.GhostsMeasured);
                    result.Pickup = MeasureProbe(pixels, camera, "contrast-pickup", 0.375f, size);
                    result.Telegraph = MeasureProbeFlat(pixels, camera, "contrast-telegraph", 3.2f, size);

                    return result;
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    camera.aspect = previousAspect;

                    for (int i = 0; i < borrowed.Count; i++)
                    {
                        borrowed[i].renderMode = RenderMode.ScreenSpaceOverlay;
                        borrowed[i].worldCamera = null;
                    }

                    RenderTexture.ReleaseTemporary(target);
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
        }

        /// <summary>
        /// Mean linear luminance, and the circular mean of hue weighted by saturation, over a
        /// sub-rectangle of the frame given in viewport fractions (origin bottom-left, as Unity's).
        /// </summary>
        private static ToneStats MeasureTone(
            Color32[] pixels, Vector2Int size, float x0, float y0, float width, float height)
        {
            int minX = Mathf.RoundToInt(x0 * size.x);
            int minY = Mathf.RoundToInt(y0 * size.y);
            int maxX = Mathf.RoundToInt((x0 + width) * size.x);
            int maxY = Mathf.RoundToInt((y0 + height) * size.y);

            double luminance = 0d;
            double saturationSum = 0d;
            double hueX = 0d;
            double hueY = 0d;
            int count = 0;
            int hueCount = 0;

            for (int y = minY; y < maxY; y++)
            {
                int row = y * size.x;
                for (int x = minX; x < maxX; x++)
                {
                    Color32 pixel = pixels[row + x];
                    Color colour = pixel;
                    float r = SrgbToLinear(colour.r);
                    float g = SrgbToLinear(colour.g);
                    float b = SrgbToLinear(colour.b);
                    luminance += (0.2126f * r) + (0.7152f * g) + (0.0722f * b);
                    count++;

                    Color.RGBToHSV(colour, out float h, out float s, out _);
                    saturationSum += s;

                    // Saturated pixels only: a frame is mostly grey-blue floor, and its hue would
                    // otherwise be dominated by near-neutral pixels whose hue is numerical noise.
                    if (s > 0.12f)
                    {
                        double angle = h * Math.PI * 2d;
                        hueX += Math.Cos(angle) * s;
                        hueY += Math.Sin(angle) * s;
                        hueCount++;
                    }
                }
            }

            if (count == 0)
            {
                return default;
            }

            float hueDegrees = 0f;
            if (hueCount > 0)
            {
                double angle = Math.Atan2(hueY, hueX);
                if (angle < 0d)
                {
                    angle += Math.PI * 2d;
                }

                hueDegrees = (float)(angle * 180d / Math.PI);
            }

            return new ToneStats
            {
                Luminance = (float)(luminance / count),
                HueDegrees = hueDegrees,
                Saturation = (float)(saturationSum / count),
            };
        }

        private static float SrgbToLinear(float value) =>
            value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);

        private static ContrastSample NearestGhosts(
            Color32[] pixels, Camera camera, BattleSimulationRunner runner, Vector2Int size, out int count)
        {
            count = 0;
            ReadOnlySpan<RenderEntity> entities = runner.View.Snapshot.Current;
            Vector3 origin = PlayerPosition();
            var measured = new bool[entities.Length];
            ContrastSample worst = default;
            float worstRatio = float.MaxValue;

            for (int pass = 0; pass < 3; pass++)
            {
                float best = float.MaxValue;
                int found = -1;
                for (int i = 0; i < entities.Length; i++)
                {
                    if (entities[i].Kind != RenderEntityKind.Enemy || measured[i])
                    {
                        continue;
                    }

                    if (!OnScreen(camera, BattleWorldMap.ToWorld(entities[i].X, entities[i].Y)))
                    {
                        continue;
                    }

                    float dx = entities[i].X - origin.x;
                    float dz = entities[i].Y - origin.z;
                    float distance = (dx * dx) + (dz * dz);
                    if (distance < best)
                    {
                        best = distance;
                        found = i;
                    }
                }

                if (found < 0)
                {
                    break;
                }

                measured[found] = true;
                ContrastSample sample = MeasureBillboard(
                    pixels, camera, new Vector3(entities[found].X, 0.5f, entities[found].Y), 1f, size);
                count++;
                if (sample.Ratio < worstRatio)
                {
                    worstRatio = sample.Ratio;
                    worst = sample;
                }
            }

            return worst;
        }

        private static ContrastSample MeasureProbe(
            Color32[] pixels, Camera camera, string name, float worldSize, Vector2Int size)
        {
            GameObject probe = GameObject.Find(name);
            return probe == null
                ? default
                : MeasureBillboard(pixels, camera, probe.transform.position, worldSize, size);
        }

        private static ContrastSample MeasureProbeFlat(
            Color32[] pixels, Camera camera, string name, float worldSize, Vector2Int size)
        {
            GameObject probe = GameObject.Find(name);
            return probe == null
                ? default
                : MeasureFlat(pixels, camera, probe.transform.position, worldSize, size);
        }

        private static ContrastSample MeasureBillboard(
            Color32[] pixels, Camera camera, Vector3 centre, float worldSize, Vector2Int size)
        {
            Vector3 right = camera.transform.right * (worldSize * 0.5f);
            Vector3 up = camera.transform.up * (worldSize * 0.5f);
            return MeasureQuad(
                pixels, camera, size,
                centre - right - up, centre + right - up, centre - right + up, centre + right + up);
        }

        private static ContrastSample MeasureFlat(
            Color32[] pixels, Camera camera, Vector3 centre, float worldSize, Vector2Int size)
        {
            float half = worldSize * 0.5f;
            return MeasureQuad(
                pixels, camera, size,
                centre + new Vector3(-half, 0f, -half),
                centre + new Vector3(half, 0f, -half),
                centre + new Vector3(-half, 0f, half),
                centre + new Vector3(half, 0f, half));
        }

        private static ContrastSample MeasureQuad(
            Color32[] pixels, Camera camera, Vector2Int size,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector2 pa = Project(camera, a, size);
            Vector2 pb = Project(camera, b, size);
            Vector2 pc = Project(camera, c, size);
            Vector2 pd = Project(camera, d, size);

            int minX = Mathf.RoundToInt(Mathf.Min(Mathf.Min(pa.x, pb.x), Mathf.Min(pc.x, pd.x))) - 2;
            int maxX = Mathf.RoundToInt(Mathf.Max(Mathf.Max(pa.x, pb.x), Mathf.Max(pc.x, pd.x))) + 2;
            int minY = Mathf.RoundToInt(Mathf.Min(Mathf.Min(pa.y, pb.y), Mathf.Min(pc.y, pd.y))) - 2;
            int maxY = Mathf.RoundToInt(Mathf.Max(Mathf.Max(pa.y, pb.y), Mathf.Max(pc.y, pd.y))) + 2;

            var box = new RectInt(minX, minY, Mathf.Max(6, maxX - minX), Mathf.Max(6, maxY - minY));
            int margin = Mathf.Max(14, Mathf.RoundToInt(box.width * 0.9f));
            RectInt floor = ArenaContrast.FloorAround(box, margin);
            return ArenaContrast.Measure(pixels, size.x, size.y, box, floor);
        }

        private static Vector2 Project(Camera camera, Vector3 world, Vector2Int size)
        {
            Vector3 local = camera.transform.InverseTransformPoint(world);
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * (size.x / (float)size.y);
            return new Vector2(
                ((local.x / halfWidth * 0.5f) + 0.5f) * size.x,
                ((local.y / halfHeight * 0.5f) + 0.5f) * size.y);
        }

        private static bool OnScreen(Camera camera, Vector3 world)
        {
            Vector2 point = Project(camera, world, new Vector2Int(100, 100));
            return point.x > 6f && point.x < 94f && point.y > 6f && point.y < 94f;
        }

        private static int VisibleGhosts(BattleSimulationRunner runner)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return 0;
            }

            ReadOnlySpan<RenderEntity> entities = runner.View.Snapshot.Current;
            int visible = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (entities[i].Kind != RenderEntityKind.Enemy)
                {
                    continue;
                }

                Vector3 local = camera.transform.InverseTransformPoint(
                    BattleWorldMap.ToWorld(entities[i].X, entities[i].Y));
                float halfHeight = camera.orthographicSize;
                float halfWidth = halfHeight * camera.aspect;
                if (Mathf.Abs(local.x) < halfWidth * 0.8f && Mathf.Abs(local.y) < halfHeight * 0.8f)
                {
                    visible++;
                }
            }

            return visible;
        }

        private static Vector3 PlayerPosition()
        {
            GameObject player = GameObject.Find("view-player");
            return player == null ? Vector3.zero : player.transform.position;
        }

        private static GameObject Probe(
            string name, Sprite sprite, Color colour, Vector3 position, int sortingOrder, float scale)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }

            var probe = new GameObject(name);
            var renderer = probe.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = sortingOrder;
            probe.transform.position = position;
            probe.transform.localScale = new Vector3(scale, scale, 1f);
            return probe;
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

        // ------------------------------------------------------------------
        // report lines
        // ------------------------------------------------------------------

        private static string ToneLine(string state, Vector2Int size, CaptureResult result) =>
            state + " " + size.x + "x" + size.y
            + ": whole L=" + F(result.Whole.Luminance) + " hue=" + F(result.Whole.HueDegrees)
            + " sat=" + F(result.Whole.Saturation)
            + " | centre L=" + F(result.Centre.Luminance) + " hue=" + F(result.Centre.HueDegrees)
            + " sat=" + F(result.Centre.Saturation)
            + " file=" + result.File;

        private static string MetricsLine(string state, Vector2Int size, CaptureResult result, BattleSimulationRunner runner) =>
            state + " " + size.x + "x" + size.y
            + ": batches=" + result.Batches
            + " draw=" + result.DrawCalls
            + " setPass=" + result.SetPass
            + " frameMs=" + F(result.FrameMs)
            + " entities=" + result.Entities
            + " bound=" + runner.View.BoundViews
            + " file=" + result.File;

        private static string Thresholds(ArenaTheme theme) =>
            "player=" + F(theme.MinPlayerContrast) + " ghost=" + F(theme.MinGhostContrast)
            + " pickup=" + F(theme.MinPickupContrast) + " telegraph=" + F(theme.MinTelegraphContrast);

        private static string ContrastLine(string state, Vector2Int size, CaptureResult result, ArenaTheme theme) =>
            state + " " + size.x + "x" + size.y
            + "  player=" + F(result.Player.Ratio) + " ghost=" + F(result.Ghost.Ratio)
            + " pickup=" + F(result.Pickup.Ratio) + " telegraph=" + F(result.Telegraph.Ratio)
            + "  ghostsMeasured=" + result.GhostsMeasured
            + "  pass=" + Pass(result, theme);

        private static bool Pass(CaptureResult result, ArenaTheme theme) =>
            result.Player.Ratio >= theme.MinPlayerContrast
            && result.Ghost.Ratio >= theme.MinGhostContrast
            && result.Pickup.Ratio >= theme.MinPickupContrast
            && result.Telegraph.Ratio >= theme.MinTelegraphContrast;

        private static string F(float value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
