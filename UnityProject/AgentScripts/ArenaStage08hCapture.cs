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
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>
    /// Stage 08h evidence: what the arena floor and the actor sprites actually ask the depth buffer
    /// for, and how much of a billboard's lower half survives the floor being on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Run it twice — once on the tree as delivered (<c>before</c>) and once after the fix
    /// (<c>after</c>) — and the two files are the before/after the report quotes. The tag comes from
    /// <c>artifacts/stage08h/tag.txt</c> so nothing in this file is edited between the runs.
    /// </para>
    /// <para>
    /// Play mode is started from the outside: entering it reloads the domain and unloads this
    /// ephemeral assembly, taking the pending continuation with it.
    /// </para>
    /// </remarks>
    public static class ArenaStage08hCapture
    {
        private const int Ghosts = 60;

        private static readonly Vector2Int Size1920 = new Vector2Int(1920, 1080);
        private static readonly Vector2Int Size1280 = new Vector2Int(1280, 720);

        /// <summary>A pixel counts as "the sprite drew here" above this channel difference.</summary>
        private const int DifferenceThreshold = 8;

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "stage08h"));

        private static string Tag
        {
            get
            {
                string path = Path.Combine(OutputDirectory, "tag.txt");
                if (!File.Exists(path))
                {
                    return "shot";
                }

                string tag = File.ReadAllText(path).Trim();
                return string.IsNullOrEmpty(tag) ? "shot" : tag;
            }
        }

        public static async Task<string> Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            Log("start tag=" + Tag);

            if (!EditorApplication.isPlaying)
            {
                Log("not in play mode: start it first");
                return "not in play mode";
            }

            BattleSimulationRunner runner = await WaitForRunner();
            if (runner == null)
            {
                Log("battle did not start");
                return "battle did not start";
            }

            Log("runner up");

            var diagnostics = new StringBuilder();
            diagnostics.AppendLine("# Stage 08h — what each material asks of the depth buffer");
            diagnostics.AppendLine("# Unity " + Application.unityVersion + ", Editor Play Mode");
            diagnostics.AppendLine();
            DescribeFloor(runner, diagnostics);
            DescribeSprites(runner, diagnostics);

            runner.Submit(BattleCommand.DevGodMode(true));
            await Task.Delay(400);

            int spawned = 0;
            for (int i = 0; i < Ghosts; i++)
            {
                if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                {
                    spawned++;
                }
            }

            for (int i = 0; i < 150; i++)
            {
                await Task.Delay(200);
                if (VisibleGhosts(runner) >= 6)
                {
                    break;
                }
            }

            await Task.Delay(2000);

            // A paused floor is a still picture: the crowd, the figure and the probes cannot move
            // between the two renders a coverage measurement compares.
            runner.SetPaused(true);
            await Task.Delay(300);

            Vector3 player = PlayerPosition();
            float pitch = runner.View.Camera.BillboardPitch;
            var probes = BuildProbes(runner, player, pitch);

            Extras extras = BuildExtras(runner, player, pitch);

            ArenaEnvironmentView environment = runner.View.Environment;
            var coverage = new StringBuilder();
            coverage.AppendLine("# Stage 08h — the sprite's own pixels in the lower half of its quad");
            coverage.AppendLine("# tag=" + Tag + "  ghosts=" + spawned + "  machine: i5-14400F / RTX 5060 Ti / 32 GB");
            coverage.AppendLine("# lower-half / lower-quarter = fraction of that band of the sprite's screen quad that");
            coverage.AppendLine("# changes when the sprite is hidden, i.e. the sprite's own colour there.");
            coverage.AppendLine();

            foreach (ArenaTimeOfDay timeOfDay in new[] { ArenaTimeOfDay.Night, ArenaTimeOfDay.Dusk })
            {
                environment.SetTimeOfDay(timeOfDay);
                await Task.Delay(600);

                foreach (Vector2Int size in new[] { Size1920, Size1280 })
                {
                    string state = (timeOfDay == ArenaTimeOfDay.Night ? "night" : "dusk")
                        + "-" + size.x + "x" + size.y;

                    Cover floorOn = await Measure(runner, probes, extras, size, floorEnabled: true, state: state + "-floor-on", save: true);
                    Cover floorOff = await Measure(runner, probes, extras, size, floorEnabled: false, state: state + "-floor-off", save: true);

                    coverage.AppendLine("## " + state + "  entities=" + runner.View.Snapshot.CurrentCount
                        + "  floor=" + (runner.View.FloorRenderer != null
                            ? runner.View.FloorRenderer.sharedMaterial.name : "none")
                        + "  yOfSpriteBottom=" + F(floorOn.SpriteBottomY));
                    foreach (string line in floorOn.Lines("floor on "))
                    {
                        coverage.AppendLine(line);
                    }

                    foreach (string line in floorOff.Lines("floor off"))
                    {
                        coverage.AppendLine(line);
                    }

                    coverage.AppendLine();
                }
            }

            environment.SetTimeOfDay(ArenaTimeOfDay.Night);

            File.WriteAllText(Path.Combine(OutputDirectory, "stage08h-materials-" + Tag + ".txt"), diagnostics.ToString());
            File.WriteAllText(Path.Combine(OutputDirectory, "stage08h-coverage-" + Tag + ".txt"), coverage.ToString());

            foreach (GameObject probe in probes.Created)
            {
                UnityEngine.Object.Destroy(probe);
            }

            foreach (GameObject extra in extras.Created)
            {
                UnityEngine.Object.Destroy(extra);
            }

            runner.SetPaused(false);
            EditorApplication.isPlaying = false;

            return diagnostics + "\n" + coverage;
        }

        // ------------------------------------------------------------------
        // wait / describe
        // ------------------------------------------------------------------

        private static async Task<BattleSimulationRunner> WaitForRunner()
        {
            bool pushed = false;
            for (int i = 0; i < 1800; i++)
            {
                await Task.Delay(100);
                if (!EditorApplication.isPlaying)
                {
                    continue;
                }

                var director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
                if (director != null && director.Runner != null && director.Runner.IsRunning
                    && director.Runner.View != null)
                {
                    return director.Runner;
                }

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

            return null;
        }

        private static void DescribeFloor(BattleSimulationRunner runner, StringBuilder output)
        {
            Renderer floor = runner.View.FloorRenderer;
            output.AppendLine("== floor");
            if (floor == null)
            {
                output.AppendLine("no floor renderer");
                return;
            }

            Material material = floor.sharedMaterial;
            Shader shader = material.shader;
            string path = AssetDatabase.GetAssetPath(shader);

            output.AppendLine("renderer      " + floor.name + " (" + floor.GetType().Name + ")");
            output.AppendLine("material      " + material.name);
            output.AppendLine("shader        " + shader.name);
            output.AppendLine("asset         " + path);
            output.AppendLine("renderQueue   " + material.renderQueue);
            output.AppendLine("sortingOrder  " + floor.sortingOrder);
            output.AppendLine("transform     pos=" + V(floor.transform.position)
                + " euler=" + V(floor.transform.eulerAngles)
                + " scale=" + V(floor.transform.localScale));
            output.AppendLine();
            output.AppendLine("-- " + path + " (pass states as written)");
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("ZWrite", StringComparison.Ordinal)
                        || trimmed.StartsWith("ZTest", StringComparison.Ordinal)
                        || trimmed.Contains("\"Queue\"")
                        || trimmed.StartsWith("Tags", StringComparison.Ordinal)
                        || trimmed.StartsWith("Name ", StringComparison.Ordinal))
                    {
                        output.AppendLine("   " + trimmed);
                    }
                }
            }

            output.AppendLine();
        }

        private static void DescribeSprites(BattleSimulationRunner runner, StringBuilder output)
        {
            output.AppendLine("== actor sprites (the pooled views the bridge drives)");
            var seen = new HashSet<string>();

            SpriteRenderer[] renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (SpriteRenderer renderer in renderers)
            {
                string name = renderer.gameObject.name;
                if (!name.StartsWith("view-", StringComparison.Ordinal) || seen.Contains(name))
                {
                    continue;
                }

                seen.Add(name);

                Material material = renderer.sharedMaterial;
                Shader shader = material != null ? material.shader : null;
                string path = shader != null ? AssetDatabase.GetAssetPath(shader) : string.Empty;
                Bounds bounds = renderer.bounds;

                output.AppendLine("-- " + name + "  enabled=" + renderer.enabled
                    + "  active=" + renderer.gameObject.activeInHierarchy);
                output.AppendLine("   material      " + (material != null ? material.name : "none"));
                output.AppendLine("   shader        " + (shader != null ? shader.name : "none")
                    + (string.IsNullOrEmpty(path) ? " (built-in, no asset path)" : "  " + path));
                output.AppendLine("   renderQueue   " + (material != null ? material.renderQueue : 0));
                output.AppendLine("   sortingOrder  " + renderer.sortingOrder);
                output.AppendLine("   sprite        " + (renderer.sprite != null ? renderer.sprite.name : "none")
                    + " pivot=" + (renderer.sprite != null ? V2(renderer.sprite.pivot) : "-")
                    + " ppu=" + (renderer.sprite != null ? F(renderer.sprite.pixelsPerUnit) : "-"));
                output.AppendLine("   transform     pos=" + V(renderer.transform.position)
                    + " euler=" + V(renderer.transform.eulerAngles)
                    + " scale=" + V(renderer.transform.lossyScale));
                output.AppendLine("   world bounds  centre=" + V(bounds.center) + " size=" + V(bounds.size)
                    + "  bottomY=" + F(renderer.transform.TransformPoint(
                        new Vector3(0f, -bounds.extents.y / Mathf.Max(0.0001f, renderer.transform.lossyScale.y), 0f)).y));
                output.AppendLine("   " + PassState(path, "ZWrite") + "  " + PassState(path, "ZTest"));
            }

            output.AppendLine();
            output.AppendLine("Sprites/Default (the material a bare SpriteRenderer gets, which every pooled view has)");
            output.AppendLine("writes no ZWrite and states no ZTest, so it runs at queue 3000 with ZTest LEqual —");
            output.AppendLine("i.e. it is tested against whatever the opaque pass left in the depth buffer.");
            output.AppendLine();
        }

        private static string PassState(string shaderPath, string token)
        {
            if (string.IsNullOrEmpty(shaderPath) || !File.Exists(shaderPath))
            {
                return token + "=(not in the project)";
            }

            foreach (string line in File.ReadAllLines(shaderPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(token, StringComparison.Ordinal))
                {
                    return trimmed;
                }
            }

            return token + "=(not stated -> Unity default)";
        }

        // ------------------------------------------------------------------
        // probes
        // ------------------------------------------------------------------

        private sealed class Probes
        {
            internal readonly List<GameObject> Created = new List<GameObject>();
            internal readonly List<string> Names = new List<string>();

            /// <summary>Half the sprite's local size, before the probe's own scale.</summary>
            internal readonly List<Vector2> Halves = new List<Vector2>();
        }

        private static Probes BuildProbes(BattleSimulationRunner runner, Vector3 player, float pitch)
        {
            var probes = new Probes();
            BattlePlaceholderArt art = runner.View.Art;

            // Every probe is a billboard: the same pivot, the same tilt and the same ground height
            // the bridge gives a real entity. That is the whole point — the defect is the interaction
            // of that convention with the floor, not anything about which sprite is drawn.
            // Spread wide enough that no two probes share a pixel: a band's coverage has to be that
            // sprite's own, or the measurement describes the neighbour.
            Add(probes, art.Enemy, "probe-ghost", RenderEntityKind.Enemy, RenderEntityFlags.None,
                player + new Vector3(-4.2f, 0f, 1.4f), pitch, 1f);
            Add(probes, art.Enemy, "probe-elite", RenderEntityKind.Enemy, RenderEntityFlags.Elite,
                player + new Vector3(-2.2f, 0f, 1.4f), pitch, BattlePlaceholderArt.VisualScale(RenderEntityFlags.Elite));
            Add(probes, art.Enemy, "probe-boss", RenderEntityKind.Enemy, RenderEntityFlags.Boss,
                player + new Vector3(1.0f, 0f, 1.4f), pitch, BattlePlaceholderArt.VisualScale(RenderEntityFlags.Boss));
            Add(probes, art.Projectile, "probe-bolt", RenderEntityKind.Projectile, RenderEntityFlags.None,
                player + new Vector3(-3.4f, 0f, -1.8f), pitch, 1f);
            Add(probes, art.Pickup, "probe-drop", RenderEntityKind.Pickup, RenderEntityFlags.None,
                player + new Vector3(3.6f, 0f, -1.8f), pitch, 1f);

            return probes;
        }

        private static void Add(
            Probes probes, Sprite sprite, string name, RenderEntityKind kind, RenderEntityFlags flags,
            Vector3 position, float pitch, float scale)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing);
            }

            var probe = new GameObject(name);
            var renderer = probe.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = BattleWorldMap.SortingOrder(kind, position.z);

            probe.transform.position = position;
            probe.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            probe.transform.localScale = new Vector3(scale, scale, 1f);

            probes.Created.Add(probe);
            probes.Names.Add(name);
            probes.Halves.Add(new Vector2(sprite.bounds.size.x * 0.5f, sprite.bounds.size.y * 0.5f));
        }

        // ------------------------------------------------------------------
        // the kinds that never hang below the plane
        // ------------------------------------------------------------------

        /// <summary>
        /// One thing whose pixels are counted by hiding it: the warning ring (a sprite lying flat on
        /// the floor), a floating damage number (a world-space TMP label 0.8 u up) and the doctor
        /// (the Spine figure, which is toggled by its renderers so its state is untouched).
        /// </summary>
        private sealed class Extra
        {
            internal string Name;
            internal GameObject Root;
            internal Renderer[] Renderers;
            internal bool ToggleRoot;

            internal void Show(bool shown)
            {
                if (ToggleRoot)
                {
                    if (Root != null)
                    {
                        Root.SetActive(shown);
                    }

                    return;
                }

                if (Renderers == null)
                {
                    return;
                }

                for (int i = 0; i < Renderers.Length; i++)
                {
                    if (Renderers[i] != null)
                    {
                        Renderers[i].enabled = shown;
                    }
                }
            }
        }

        private sealed class Extras
        {
            internal readonly List<GameObject> Created = new List<GameObject>();
            internal readonly List<Extra> Items = new List<Extra>();

            internal void Show(bool shown)
            {
                for (int i = 0; i < Items.Count; i++)
                {
                    Items[i].Show(shown);
                }
            }
        }

        private static Extras BuildExtras(BattleSimulationRunner runner, Vector3 player, float pitch)
        {
            var extras = new Extras();
            BattlePlaceholderArt art = runner.View.Art;

            // A warning ring, exactly as TelegraphPool builds one: a sprite lying flat on the ground
            // plane at its simulation position, in the telegraphs' sorting order.
            var ring = new GameObject("probe-ring");
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = art.TelegraphCircle;
            ringRenderer.color = ProjectSurvive.Game.Presentation.Battle.Vfx.TelegraphPool.TintFor(
                TelegraphShape.Circle);
            ringRenderer.sortingOrder = BattleViewCaps.TelegraphSortingOrder;
            ring.transform.position = BattleWorldMap.ToWorld(player.x - 6f, player.z + 3f);
            ring.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ring.transform.localScale = new Vector3(3.2f, 3.2f, 1f);

            extras.Created.Add(ring);
            extras.Items.Add(new Extra
            {
                Name = "probe-ring",
                Root = ring,
                Renderers = ring.GetComponentsInChildren<Renderer>(true),
                ToggleRoot = true,
            });

            // A floating damage number, exactly as FloatingDamagePool places one: 0.8 u above the
            // simulation position, billboarded at the camera's pitch, in the HUD's world order.
            var number = new GameObject("probe-number");
            var text = number.AddComponent<TMPro.TextMeshPro>();
            text.text = "48";
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.92f, 0.55f, 1f);
            text.sortingOrder = BattleViewCaps.FloatingTextSortingOrder;
            number.transform.position = BattleWorldMap.ToWorld(player.x + 2.6f, player.z - 0.4f)
                + new Vector3(0f, 0.8f, 0f);
            number.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

            extras.Created.Add(number);
            extras.Items.Add(new Extra
            {
                Name = "probe-number",
                Root = number,
                Renderers = number.GetComponentsInChildren<Renderer>(true),
                ToggleRoot = true,
            });

            // The doctor, drawn by the Spine figure rather than by a pooled billboard.
            PlayerFigurePresenter figure = runner.View.PlayerFigure;
            if (figure != null && figure.View != null)
            {
                extras.Items.Add(new Extra
                {
                    Name = "figure-player",
                    Root = figure.View.gameObject,
                    Renderers = figure.View.GetComponentsInChildren<Renderer>(true),
                    ToggleRoot = false,
                });
            }

            return extras;
        }

        private static async Task<Cover> MeasureExtras(
            BattleSimulationRunner runner, Probes probes, Extras extras, Vector2Int size,
            bool floorEnabled, string state)
        {
            Renderer floor = runner.View.FloorRenderer;
            bool floorWasEnabled = floor != null && floor.enabled;
            if (floor != null)
            {
                floor.enabled = floorEnabled;
            }

            // The billboard probes are props, not part of this measurement: hidden for both frames
            // so nothing of theirs can land inside a warning ring's or the doctor's box.
            SetProbesActive(probes, false);

            Color32[] on = Render(size, save: false, state: state);
            extras.Show(false);
            Color32[] off = Render(size, save: false, state: state + "-hidden");
            extras.Show(true);
            SetProbesActive(probes, true);

            if (floor != null)
            {
                floor.enabled = floorWasEnabled;
            }

            var cover = new Cover(extras.Items.Count);
            for (int i = 0; i < extras.Items.Count; i++)
            {
                Extra extra = extras.Items[i];
                Bounds bounds = WorldBounds(extra);
                if (bounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                Vector2 a = Project(Camera.main, bounds.min, size);
                Vector2 b = Project(Camera.main, bounds.max, size);

                float minX = Mathf.Min(a.x, b.x);
                float maxX = Mathf.Max(a.x, b.x);
                float minY = Mathf.Min(a.y, b.y);
                float maxY = Mathf.Max(a.y, b.y);

                cover.Names.Add(extra.Name);
                cover.Height.Add(maxY - minY);
                cover.Half.Add(Coverage(on, off, size, minX, minY, maxX, (minY + maxY) * 0.5f));
                cover.Quarter.Add(Coverage(on, off, size, minX, minY, maxX, minY + ((maxY - minY) * 0.25f)));
            }

            Log("extras " + state + " floor=" + floorEnabled
                + " ringLowerHalf=" + (cover.Half.Count > 0 ? F(cover.Half[0]) : "-"));

            await Task.CompletedTask;
            return cover;
        }

        private static Bounds WorldBounds(Extra extra)
        {
            bool any = false;
            Bounds bounds = default;
            if (extra.Renderers == null)
            {
                return default;
            }

            for (int i = 0; i < extra.Renderers.Length; i++)
            {
                Renderer renderer = extra.Renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any ? bounds : default;
        }

        // ------------------------------------------------------------------
        // measurement
        // ------------------------------------------------------------------

        private struct Cover
        {
            internal string State;
            internal float SpriteBottomY;
            internal Vector2Int Size;
            internal int Ghosts;
            internal readonly List<string> Names;
            internal readonly List<float> Half;
            internal readonly List<float> Quarter;
            internal readonly List<float> Height;

            internal Cover(int capacity)
            {
                State = string.Empty;
                SpriteBottomY = 0f;
                Size = default;
                Ghosts = 0;
                Names = new List<string>(capacity);
                Half = new List<float>(capacity);
                Quarter = new List<float>(capacity);
                Height = new List<float>(capacity);
            }

            internal IEnumerable<string> Lines(string prefix)
            {
                for (int i = 0; i < Names.Count; i++)
                {
                    yield return prefix + "  " + Names[i].PadRight(12)
                        + " lowerHalf=" + F(Half[i]) + " lowerQuarter=" + F(Quarter[i])
                        + " height=" + F(Height[i]) + "px";
                }
            }
        }

        private static async Task<Cover> Measure(
            BattleSimulationRunner runner, Probes probes, Extras extras, Vector2Int size,
            bool floorEnabled, string state, bool save)
        {
            Renderer floor = runner.View.FloorRenderer;
            bool floorWasEnabled = floor != null && floor.enabled;
            if (floor != null)
            {
                floor.enabled = floorEnabled;
            }

            Color32[] on = Render(size, save, state);
            SetProbesActive(probes, false);
            Color32[] off = Render(size, save: false, state: state + "-no-probes");
            SetProbesActive(probes, true);

            if (floor != null)
            {
                floor.enabled = floorWasEnabled;
            }

            Camera camera = Camera.main;
            var cover = new Cover(probes.Names.Count)
            {
                State = state,
                Size = size,
                Ghosts = VisibleGhosts(runner),
                SpriteBottomY = BottomOf(runner, probes, size),
            };

            for (int i = 0; i < probes.Created.Count; i++)
            {
                // The quad's own corners in the probe's local space, not its axis-aligned bounds:
                // the sprite is tilted 50° and the probe may be scaled, so the AABB is a different
                // (larger) screen box than the quad the sprite actually covers.
                Vector2 half = probes.Halves[i];
                Transform transform = probes.Created[i].transform;
                Vector3 a = transform.TransformPoint(new Vector3(-half.x, -half.y, 0f));
                Vector3 b = transform.TransformPoint(new Vector3(half.x, -half.y, 0f));
                Vector3 c = transform.TransformPoint(new Vector3(-half.x, half.y, 0f));
                Vector3 d = transform.TransformPoint(new Vector3(half.x, half.y, 0f));

                Vector2 pa = Project(camera, a, size);
                Vector2 pb = Project(camera, b, size);
                Vector2 pc = Project(camera, c, size);
                Vector2 pd = Project(camera, d, size);

                float minX = Mathf.Min(Mathf.Min(pa.x, pb.x), Mathf.Min(pc.x, pd.x));
                float maxX = Mathf.Max(Mathf.Max(pa.x, pb.x), Mathf.Max(pc.x, pd.x));
                float minY = Mathf.Min(Mathf.Min(pa.y, pb.y), Mathf.Min(pc.y, pd.y));
                float maxY = Mathf.Max(Mathf.Max(pa.y, pb.y), Mathf.Max(pc.y, pd.y));

                cover.Names.Add(probes.Names[i]);
                cover.Height.Add(maxY - minY);

                float midY = (minY + maxY) * 0.5f;
                float quarterY = minY + ((maxY - minY) * 0.25f);
                cover.Half.Add(Coverage(on, off, size, minX, minY, maxX, midY));
                cover.Quarter.Add(Coverage(on, off, size, minX, minY, maxX, quarterY));
            }

            Log("measured " + state + " floor=" + floorEnabled
                + " ghostLowerHalf=" + (cover.Half.Count > 0 ? F(cover.Half[0]) : "-"));

            // The kinds that never hang below the plane are counted in their own pair of frames.
            Cover extrasCover = await MeasureExtras(runner, probes, extras, size, floorEnabled, state);
            for (int i = 0; i < extrasCover.Names.Count; i++)
            {
                cover.Names.Add(extrasCover.Names[i]);
                cover.Half.Add(extrasCover.Half[i]);
                cover.Quarter.Add(extrasCover.Quarter[i]);
                cover.Height.Add(extrasCover.Height[i]);
            }

            return cover;
        }

        private static float BottomOf(BattleSimulationRunner runner, Probes probes, Vector2Int size)
        {
            _ = runner;
            _ = size;
            return probes.Created.Count == 0
                ? 0f
                : probes.Created[0].transform.TransformPoint(
                    new Vector3(0f, -probes.Halves[0].y, 0f)).y;
        }

        private static void SetProbesActive(Probes probes, bool active)
        {
            for (int i = 0; i < probes.Created.Count; i++)
            {
                if (probes.Created[i] != null)
                {
                    probes.Created[i].SetActive(active);
                }
            }
        }

        /// <summary>Fraction of the band whose pixels change when the sprite is hidden.</summary>
        private static float Coverage(
            Color32[] on, Color32[] off, Vector2Int size, float x0, float y0, float x1, float y1)
        {
            int minX = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(x0, x1)), 0, size.x - 1);
            int maxX = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(x0, x1)), 0, size.x - 1);
            int minY = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(y0, y1)), 0, size.y - 1);
            int maxY = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(y0, y1)), 0, size.y - 1);

            int total = 0;
            int drawn = 0;
            for (int y = minY; y <= maxY; y++)
            {
                int row = y * size.x;
                for (int x = minX; x <= maxX; x++)
                {
                    Color32 a = on[row + x];
                    Color32 b = off[row + x];
                    int difference = Mathf.Max(
                        Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

                    total++;
                    if (difference > DifferenceThreshold)
                    {
                        drawn++;
                    }
                }
            }

            return total == 0 ? 0f : drawn / (float)total;
        }

        private static Color32[] Render(Vector2Int size, bool save, string state)
        {
            Camera camera = Camera.main;
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
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

                if (save)
                {
                    string path = Path.Combine(OutputDirectory, "stage08h-" + state + "-" + Tag + ".png");
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    Log("saved " + Path.GetFileName(path));
                }

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

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private static Vector2 Project(Camera camera, Vector3 world, Vector2Int size)
        {
            Vector3 local = camera.transform.InverseTransformPoint(world);
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * (size.x / (float)size.y);
            return new Vector2(
                ((local.x / halfWidth * 0.5f) + 0.5f) * size.x,
                ((local.y / halfHeight * 0.5f) + 0.5f) * size.y);
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

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(OutputDirectory, "trace-" + Tag + ".log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + "\n");
            }
            catch (Exception)
            {
                // The trace is a debugging aid only.
            }

            Debug.Log("[Stage08h] " + message);
        }

        private static string V(Vector3 value) =>
            "(" + F(value.x) + ", " + F(value.y) + ", " + F(value.z) + ")";

        private static string V2(Vector2 value) => "(" + F(value.x) + ", " + F(value.y) + ")";

        private static string F(float value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
