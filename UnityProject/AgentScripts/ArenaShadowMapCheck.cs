using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Game.Battle;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>
    /// Stage 08g evidence for the real-shadow item: does the Battle main light's shadow map actually
    /// change the frame, and by how much?
    /// </summary>
    /// <remarks>
    /// <para>
    /// A test can prove the wiring (light is hard, <c>RenderSettings.sun</c> is that light, every card
    /// casts). It cannot prove the pixels moved. So this renders the same camera twice — once with
    /// <see cref="LightShadows.Hard"/> and once with <see cref="LightShadows.None"/> — from a frozen
    /// frame, and reports the per-pixel difference. The only difference between the two frames is the
    /// shadow map, so a non-zero difference is the shadow map's own contribution.
    /// </para>
    /// <para>
    /// Two framings are measured. <c>shipped</c> is what the player sees (the arena profile's
    /// orthographic size). <c>wide</c> temporarily widens the orthographic size so the border trees,
    /// which sit outside the shipped frame, are visible with the shadows they throw across the floor.
    /// The wide shot is a diagnostic view, not a look the game ships.
    /// </para>
    /// <para>
    /// It also prices the change: the shadow map replaces one instanced decal call with a second pass
    /// over every card, so the batches and the frame time are sampled with the map on and off in the
    /// shipped framing before anything is frozen. The 200-batch budget of Stage 08f is checked there.
    /// </para>
    /// <para>
    /// Play mode is started from the outside: entering it reloads the domain and unloads this
    /// ephemeral assembly, taking the pending continuation with it. The run also ends on its own
    /// after a while, so this pauses the editor the moment the arena appears and works from there.
    /// </para>
    /// </remarks>
    public static class ArenaShadowMapCheck
    {
        private const string ArenaRootName = "Arena";
        private const float WideOrthographicSize = 16f;

        private static readonly Vector2Int Size = new Vector2Int(1920, 1080);
        private static readonly StringBuilder Report = new StringBuilder();

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "stage08g"));

        public static async Task<string> Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            Log("start, out=" + OutputDirectory);

            if (!EditorApplication.isPlaying)
            {
                Log("not in play mode: start it first");
                return "not in play mode";
            }

            GameObject arena = await WaitForArena();
            if (arena == null)
            {
                Log("the arena never appeared");
                return "no arena";
            }

            KeyLight = arena.transform.Find("Lights/KeyLight")?.GetComponent<Light>();
            if (KeyLight != null)
            {
                await MeasureCost();
            }

            // Freeze the run: the floor ends by itself after a while and unloads the scene under us.
            EditorApplication.isPaused = true;

            try
            {
                return Work(arena);
            }
            finally
            {
                EditorApplication.isPaused = false;
            }
        }

        private static string Work(GameObject arena)
        {
            if (KeyLight == null)
            {
                Log("the arena has no Lights/KeyLight");
                return "no key light";
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Log("no main camera");
                return "no camera";
            }

            Transform cards = arena.transform.Find("Cards");
            var renderers = cards == null
                ? new MeshRenderer[0]
                : cards.GetComponentsInChildren<MeshRenderer>(true);

            int casting = 0;
            int receiving = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On)
                {
                    casting++;
                }

                if (renderers[i].receiveShadows)
                {
                    receiving++;
                }
            }

            Report.AppendLine("# Stage 08g — the Battle shadow map, measured");
            Report.AppendLine("# machine: " + SystemInfo.processorType + " | " + SystemInfo.graphicsDeviceName);
            Report.AppendLine("# " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            Report.AppendLine();
            Report.AppendLine("key light shadows : " + KeyLight.shadows);
            Report.AppendLine("key light colour  : " + Colour(KeyLight.color) + "  intensity " + KeyLight.intensity.ToString("0.00", CultureInfo.InvariantCulture));
            Report.AppendLine("key light euler   : " + KeyLight.transform.eulerAngles.ToString("0.0"));
            Report.AppendLine("RenderSettings.sun: " + (RenderSettings.sun == null ? "none" : RenderSettings.sun.name)
                + (RenderSettings.sun == KeyLight ? " (the key light)" : " (NOT the key light)"));
            Report.AppendLine("quality shadows   : " + QualitySettings.shadows);
            Report.AppendLine("cards             : " + renderers.Length + " (" + casting + " casting, " + receiving + " receiving)");
            Report.AppendLine();
            Report.Append(CostReport);

            float shipped = camera.orthographicSize;
            Measure(camera, "shipped", shipped);
            Measure(camera, "wide", WideOrthographicSize);

            string path = Path.Combine(OutputDirectory, "shadow-map-check.txt");
            File.WriteAllText(path, Report.ToString());
            Log("wrote " + path);
            Debug.Log(Report.ToString());
            return "ok";
        }

        private static void Measure(Camera camera, string framing, float orthographicSize)
        {
            float previousSize = camera.orthographicSize;
            camera.orthographicSize = orthographicSize;

            try
            {
                Color32[] on = Render(camera, LightShadows.Hard, framing + "-on");
                Color32[] off = Render(camera, LightShadows.None, framing + "-off");

                int differing = 0;
                long sum = 0;
                int worst = 0;
                double squared = 0d;

                for (int i = 0; i < on.Length; i++)
                {
                    int dr = Math.Abs(on[i].r - off[i].r);
                    int dg = Math.Abs(on[i].g - off[i].g);
                    int db = Math.Abs(on[i].b - off[i].b);
                    int d = Math.Max(dr, Math.Max(dg, db));

                    if (d > 0)
                    {
                        differing++;
                    }

                    if (d > worst)
                    {
                        worst = d;
                    }

                    sum += dr + dg + db;
                    squared += (double)d * d;
                }

                double total = on.Length;
                Report.AppendLine("## " + framing + " (" + Size.x + "x" + Size.y + ", orthographicSize "
                    + orthographicSize.ToString("0.00", CultureInfo.InvariantCulture) + ")");
                Report.AppendLine("shadow-on  : shadow-map-" + framing + "-on.png");
                Report.AppendLine("shadow-off : shadow-map-" + framing + "-off.png");
                Report.AppendLine("pixels whose max channel moved : " + differing.ToString(CultureInfo.InvariantCulture)
                    + " / " + ((int)total).ToString(CultureInfo.InvariantCulture)
                    + " (" + ((differing / total) * 100d).ToString("0.00", CultureInfo.InvariantCulture) + " %)");
                Report.AppendLine("mean absolute channel change   : "
                    + (sum / (total * 3d)).ToString("0.0000", CultureInfo.InvariantCulture));
                Report.AppendLine("root mean square change        : "
                    + Math.Sqrt(squared / total).ToString("0.0000", CultureInfo.InvariantCulture));
                Report.AppendLine("largest single channel change  : " + worst);
                Report.AppendLine();
            }
            finally
            {
                camera.orthographicSize = previousSize;
            }
        }

        private static Color32[] Render(Camera camera, LightShadows mode, string name)
        {
            KeyLight.shadows = mode;

            RenderTexture target = RenderTexture.GetTemporary(Size.x, Size.y, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            var image = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, false);
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
                camera.aspect = Size.x / (float)Size.y;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, Size.x, Size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(Path.Combine(OutputDirectory, "shadow-map-" + name + ".png"), image.EncodeToPNG());
                return image.GetPixels32();
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

        /// <summary>
        /// The price of the shadow map, sampled before the frame is frozen: the same frozen viewport
        /// with the key light casting and not casting. Only the shadow pass differs, so the delta is
        /// the shadow map's own cost.
        /// </summary>
        private static async Task MeasureCost()
        {
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                var modes = new[] { LightShadows.Hard, LightShadows.None };
                var lines = new string[modes.Length];

                for (int i = 0; i < modes.Length; i++)
                {
                    KeyLight.shadows = modes[i];

                    // One frame for URP to rebuild (or drop) the map, then sample what the view costs.
                    await Task.Delay(200);
                    float milliseconds = await MedianFrameMs();
                    lines[i] = (modes[i] == LightShadows.Hard ? "on  (" : "off (")
                        + "shadows=" + modes[i] + ")"
                        + "  batches " + (int)batches.LastValue
                        + " | draw " + (int)drawCalls.LastValue
                        + " | set-pass " + (int)setPass.LastValue
                        + " | ms/frame " + milliseconds.ToString("0.00", CultureInfo.InvariantCulture);

                    Log("cost " + lines[i]);
                }

                KeyLight.shadows = LightShadows.Hard;

                CostReport.AppendLine("## cost (shipped framing, 1920x1080, key light casting and not)");
                CostReport.AppendLine(lines[0]);
                CostReport.AppendLine(lines[1]);
                CostReport.AppendLine();
            }
        }

        /// <summary>Median frame time of the running view, in milliseconds, over ~30 frames.</summary>
        private static async Task<float> MedianFrameMs()
        {
            var samples = new List<float>(30);
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(16);
                samples.Add(Time.unscaledDeltaTime * 1000f);
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        private static Light KeyLight;
        private static readonly StringBuilder CostReport = new StringBuilder();

        /// <summary>
        /// Polls until the Battle arena exists, pressing the camp's departure if the run is still in
        /// camp. Polls fast: the run can end on its own before a slow poll notices it started.
        /// </summary>
        private static async Task<GameObject> WaitForArena()
        {
            int pressed = 0;

            for (int i = 0; i < 1200; i++)
            {
                if (!EditorApplication.isPlaying)
                {
                    return null;
                }

                GameObject arena = GameObject.Find(ArenaRootName);
                if (arena != null && arena.activeInHierarchy)
                {
                    Log("arena up after " + (i * 50) + " ms");
                    return arena;
                }

                var director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
                if (director != null && director.Runner != null && director.Runner.IsRunning)
                {
                    // The runner is up before its scene's arena root is activated; give it a moment.
                    await Task.Delay(50);
                    arena = GameObject.Find(ArenaRootName);
                    if (arena != null)
                    {
                        Log("arena up after " + (i * 50) + " ms");
                        return arena;
                    }
                }

                if (pressed == 0 || i % 40 == 0)
                {
                    var camp = UnityEngine.Object.FindFirstObjectByType<ProjectSurvive.Game.Camp.CampSceneDirector>();
                    if (camp != null && camp.Presenter != null && !camp.Presenter.Departed)
                    {
                        Log("pressing the camp's departure");
                        camp.Presenter.Depart();
                        pressed++;
                    }
                }

                await Task.Delay(50);
            }

            return null;
        }

        private static string Colour(Color colour) => "("
            + colour.r.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + colour.g.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + colour.b.ToString("0.00", CultureInfo.InvariantCulture) + ")";

        private static void Log(string message)
        {
            Debug.Log("[shadow-map-check] " + message);
        }
    }
}
