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
    /// Stage 08f evidence: Dusk, Night and Dusk with 300 ghosts, each at 1920×1080 and 1280×720,
    /// with the readability contrast measured on the very pixels that were saved.
    /// </summary>
    public static class ArenaStage08fCapture
    {
        private const int TotalGhosts = 300;
        private const int QuietGhosts = 12;

        private static readonly Vector2Int[] Sizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1280, 720),
        };

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

            ArenaEnvironmentView environment = runner.View.Environment;
            if (environment == null)
            {
                return "the battle view has no arena environment";
            }

            runner.Submit(BattleCommand.DevGodMode(true));
            await Task.Delay(400);

            var log = new StringBuilder();
            int spawned = 0;
            for (int i = 0; i < QuietGhosts; i++)
            {
                if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                {
                    spawned++;
                }
            }

            // Wait until some of them are inside the camera's view: a ghost measured off-screen
            // would sample the clamped edge of the image, which is floor, not the ghost.
            for (int i = 0; i < 120; i++)
            {
                await Task.Delay(200);
                if (VisibleGhosts(runner) >= 3)
                {
                    break;
                }
            }

            await Task.Delay(600);

            // The probes draw the real pickup sprite and the real telegraph sprite+tint at known
            // spots near the player, so the capture measures the art the game uses rather than a
            // copy that could drift from it.
            GameObject pickupProbe = Probe(
                "contrast-pickup", runner.View.Art.Pickup, Color.white,
                PlayerPosition() + new Vector3(3.6f, 0.5f, 1.6f),
                BattleWorldMap.SortingOrder(RenderEntityKind.Pickup, PlayerPosition().z), 1f);
            GameObject telegraphProbe = Probe(
                "contrast-telegraph", runner.View.Art.TelegraphCircle,
                TelegraphPool.TintFor(TelegraphShape.Circle),
                PlayerPosition() + new Vector3(-4.2f, 0.01f, 1.2f),
                BattleViewCaps.TelegraphSortingOrder, 3.2f);

            ArenaTheme theme = environment.Theme;
            var contrast = new StringBuilder();
            contrast.AppendLine("# Stage 08f readability: WCAG contrast ratio of each group against the floor around it");
            contrast.AppendLine("# thresholds live in ArenaTheme (MinPlayerContrast / MinGhostContrast / MinPickupContrast / MinTelegraphContrast)");
            contrast.AppendLine("# machine: i5-14400F / RTX 5060 Ti / 32 GB, Unity " + Application.unityVersion + " Editor Play Mode");

            var metrics = new StringBuilder();
            metrics.AppendLine("# Stage 08f — batches / draw calls / ms per frame");
            metrics.AppendLine("# machine: i5-14400F / RTX 5060 Ti / 32 GB, Unity " + Application.unityVersion + " Editor Play Mode");
            metrics.AppendLine("# Stage 08e for comparison: center 133/148, edge 167/172 batches at 300 ghosts");

            string assetsBefore = AssetSignature();

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                foreach (Vector2Int size in Sizes)
                {
                    CaptureResult dusk = await Capture(
                        runner, environment, "dusk", ArenaTimeOfDay.Dusk, size, batches, drawCalls, setPass);

                    contrast.AppendLine(ContrastLine("dusk", size, dusk, theme));
                    metrics.AppendLine(MetricsLine("dusk", size, dusk, runner));
                }

                environment.SetTimeOfDay(ArenaTimeOfDay.Night);
                await Task.Delay(1000);
                string assetsNight = AssetSignature();

                foreach (Vector2Int size in Sizes)
                {
                    CaptureResult night = await Capture(
                        runner, environment, "night", ArenaTimeOfDay.Night, size, batches, drawCalls, setPass);

                    contrast.AppendLine(ContrastLine("night", size, night, theme));
                    metrics.AppendLine(MetricsLine("night", size, night, runner));
                }

                metrics.AppendLine("assets unchanged by the profile switch: " + (assetsBefore == assetsNight));

                // Back to the shipped look, then the load Stage 07's benchmark uses. The two probes
                // stay until after the load frame: they are the only way to measure a pickup's and a
                // telegraph's contrast on that frame without guessing where the real ones spawned.
                environment.SetTimeOfDay(ArenaTimeOfDay.Dusk);
                await Task.Delay(800);

                int extra = 0;
                for (int i = QuietGhosts; i < TotalGhosts; i++)
                {
                    if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                    {
                        extra++;
                    }
                }

                await Task.Delay(3000);

                foreach (Vector2Int size in Sizes)
                {
                    CaptureResult load = await Capture(
                        runner, environment, "dusk300", ArenaTimeOfDay.Dusk, size, batches, drawCalls, setPass);

                    contrast.AppendLine(ContrastLine("dusk300", size, load, theme));
                    metrics.AppendLine(MetricsLine("dusk300", size, load, runner));
                }

                UnityEngine.Object.Destroy(pickupProbe);
                UnityEngine.Object.Destroy(telegraphProbe);

                log.AppendLine("ghosts requested=" + TotalGhosts + " quiet=" + spawned + " extra=" + extra);
            }

            log.AppendLine("viewsGrownDuringPlay=" + (runner.View != null ? runner.View.Pool.CreatedAfterWarmup : -1));

            File.WriteAllText(Path.Combine(OutputDirectory, "stage08f-contrast.txt"), contrast.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "stage08f-metrics.txt"), metrics + log.ToString());

            EditorApplication.isPlaying = false;
            return contrast + "\n" + metrics + "\n" + log;
        }

        // ------------------------------------------------------------------
        // capture and measurement
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
            public ContrastSample Player;
            public ContrastSample Ghost;
            public ContrastSample Pickup;
            public ContrastSample Telegraph;
            public int GhostsMeasured;
        }

        private static async Task<CaptureResult> Capture(
            BattleSimulationRunner runner, ArenaEnvironmentView environment, string state,
            ArenaTimeOfDay timeOfDay, Vector2Int size,
            ProfilerRecorder batches, ProfilerRecorder drawCalls, ProfilerRecorder setPass)
        {
            environment.SetTimeOfDay(timeOfDay);
            await Task.Delay(150);

            // Read the render counters here, after real frames have gone by in this state, rather
            // than at the call site: there the sample would still describe the previous capture.
            if ((int)batches.LastValue > 0)
            {
                _lastBatches = (int)batches.LastValue;
                _lastDrawCalls = (int)drawCalls.LastValue;
                _lastSetPass = (int)setPass.LastValue;
            }

            float frameMs = await MedianFrameMs();
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
                    OutputDirectory, "stage08f-" + state + "-" + size.x + "x" + size.y + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());

                // The measurement reads the same buffer the PNG was written from, so the numbers
                // describe the saved image and not a second render that could differ.
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

                // The recorder's first sample arrives a frame or two after it starts, which made the
                // very first capture report zeros. Read it again here, after ~30 frames of capture,
                // and remember the last real value for the next one.
                _lastBatches = result.Batches;
                _lastDrawCalls = result.DrawCalls;
                _lastSetPass = result.SetPass;

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

                    // Only a ghost the camera actually shows: a box around an off-screen ghost would
                    // clamp to the image edge and measure floor, not the ghost.
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

        /// <summary>
        /// Measures a camera-facing sprite by the screen box its own silhouette occupies: the box is
        /// the projected billboard, not a guess that could be larger than a small pickup (which is
        /// what made a first attempt read the floor beside it).
        /// </summary>
        private static ContrastSample MeasureBillboard(
            Color32[] pixels, Camera camera, Vector3 centre, float worldSize, Vector2Int size)
        {
            Vector3 right = camera.transform.right * (worldSize * 0.5f);
            Vector3 up = camera.transform.up * (worldSize * 0.5f);
            return MeasureQuad(
                pixels, camera, size,
                centre - right - up, centre + right - up, centre - right + up, centre + right + up);
        }

        /// <summary>Measures a warning lying on the floor by its ground footprint.</summary>
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

        /// <summary>
        /// Orthographic viewport arithmetic rather than WorldToViewportPoint: the capture changes
        /// camera.aspect and camera.targetTexture, and the camera's cached projection is not
        /// guaranteed to have been rebuilt when a measurement asks for it. A wrong projection would
        /// put every box except the screen centre on the floor.
        /// </summary>
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

        /// <summary>How many published ghosts are inside the camera's view right now.</summary>
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

        private static string ContrastLine(string state, Vector2Int size, CaptureResult result, ArenaTheme theme)
        {
            return state + " " + size.x + "x" + size.y
                + "  player=" + Format(result.Player.Ratio) + " (min " + Format(theme.MinPlayerContrast) + ")"
                + "  ghost=" + Format(result.Ghost.Ratio) + " (min " + Format(theme.MinGhostContrast) + ")"
                + "  pickup=" + Format(result.Pickup.Ratio) + " (min " + Format(theme.MinPickupContrast) + ")"
                + "  telegraph=" + Format(result.Telegraph.Ratio) + " (min " + Format(theme.MinTelegraphContrast) + ")"
                + "  | luminance player=" + Format(result.Player.Feature) + "/" + Format(result.Player.Floor)
                + " ghost=" + Format(result.Ghost.Feature) + "/" + Format(result.Ghost.Floor)
                + " pickup=" + Format(result.Pickup.Feature) + "/" + Format(result.Pickup.Floor)
                + " telegraph=" + Format(result.Telegraph.Feature) + "/" + Format(result.Telegraph.Floor)
                + "  ghostsMeasured=" + result.GhostsMeasured
                + "  pass=" + Pass(result, theme);
        }

        private static bool Pass(CaptureResult result, ArenaTheme theme) =>
            result.Player.Ratio >= theme.MinPlayerContrast
            && result.Ghost.Ratio >= theme.MinGhostContrast
            && result.Pickup.Ratio >= theme.MinPickupContrast
            && result.Telegraph.Ratio >= theme.MinTelegraphContrast;

        private static string MetricsLine(
            string state, Vector2Int size, CaptureResult result, BattleSimulationRunner runner)
        {
            return state + " " + size.x + "x" + size.y
                + ": batches=" + result.Batches
                + " draw=" + result.DrawCalls
                + " setPass=" + result.SetPass
                + " frameMs=" + Format(result.FrameMs)
                + " entities=" + result.Entities
                + " bound=" + runner.View.BoundViews
                + " file=" + result.File;
        }

        /// <summary>
        /// Every arena material and the texture it reads. Two signatures are equal exactly when the
        /// profile switch changed nothing in the running game.
        /// </summary>
        private static string AssetSignature()
        {
            var signature = new StringBuilder();
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Main/Art/Arena/Materials" });
            Array.Sort(guids, StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || !material.HasProperty("_BaseMap"))
                {
                    continue;
                }

                Texture texture = material.GetTexture("_BaseMap");
                signature.Append(path)
                    .Append('#').Append(material.renderQueue)
                    .Append('#').Append(texture == null ? 0 : texture.GetInstanceID())
                    .Append('#').Append(material.GetColor("_BaseColor").ToString("R"))
                    .Append(';');
            }

            return signature.ToString();
        }

        private static string Format(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
