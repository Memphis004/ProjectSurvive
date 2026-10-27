using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 08h: an actor's billboard stands on the ground plane, so half of it is below that
    /// plane — and the arena floor must not eat that half.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The claim is a claim about pixels, so it is measured in pixels, from the scene's own camera
    /// and its own materials. A probe sprite is placed exactly where the render bridge places an
    /// entity: the frame's ground height (<c>BattleWorldMap.ToWorld</c>), the camera rig's billboard
    /// pitch, a centre pivot and the kind's sorting order. The frame is rendered twice — once with
    /// the probe and once without — and the pixels that change inside the probe's own screen quad are
    /// the ones it drew.
    /// </para>
    /// <para>
    /// The failure this test exists for is silent everywhere else: the sprite is bound, the pool is
    /// warm, the sorting order is right, and the picture simply loses its lower half to the floor's
    /// depth. It has to be measured at both canvas sizes and under both lighting profiles because
    /// neither of those touches the depth buffer — if one size or one profile fails, the cause is
    /// somewhere else and this test says so.
    /// </para>
    /// </remarks>
    public sealed class ArenaSpriteFloorDepthPlayModeTests
    {
        private const string BattleSceneName = "Battle";
        private const float StartDeadlineSeconds = 30f;

        /// <summary>
        /// Smallest share of a band that must carry the sprite's own colour. Measured, not chosen:
        /// with the floor drawing completely the ghost fills ~0.17 of its own quad's lower quarter
        /// (its widest row is 0.6 of the texture) and ~0.38 of the lower half, and with the floor
        /// cutting it both are 0.000 and 0.02. The thresholds sit between the two with room for the
        /// arena's own sway and fog on either side.
        /// </summary>
        private const float MinimumHalfCoverage = 0.25f;

        private const float MinimumQuarterCoverage = 0.10f;

        private const int RenderWidth1080 = 1920;
        private const int RenderWidth720 = 1280;

        [UnityTest]
        public IEnumerator AnActorsLowerHalfSurvivesTheFloorAtBothSizesAndBothProfiles()
        {
            yield return LoadScene(BattleSceneName);
            yield return WaitForFloorToStart();

            BattleSimulationRunner runner = Find<BattleSimulationRunner>();
            Assert.That(runner, Is.Not.Null, "the Battle scene has no BattleSimulationRunner");
            Assert.That(runner.View, Is.Not.Null);
            Assert.That(runner.View.FloorRenderer, Is.Not.Null,
                "the arena floor is missing, so there is nothing for a sprite to be cut by");

            // A still floor: the probes must not move between the two renders a measurement compares.
            runner.SetPaused(true);
            yield return null;

            Vector3 player = PlayerPosition();
            float pitch = runner.View.Camera.BillboardPitch;
            BattlePlaceholderArt art = runner.View.Art;

            var probes = new List<GameObject>();
            try
            {
                Probe(probes, art.Enemy, "probe-ghost", RenderEntityKind.Enemy,
                    player + new Vector3(-4.2f, 0f, 1.4f), pitch, 1f);
                Probe(probes, art.Enemy, "probe-elite", RenderEntityKind.Enemy,
                    player + new Vector3(-2.2f, 0f, 1.4f), pitch,
                    BattlePlaceholderArt.VisualScale(RenderEntityFlags.Elite));
                Probe(probes, art.Enemy, "probe-boss", RenderEntityKind.Enemy,
                    player + new Vector3(1.0f, 0f, 1.4f), pitch,
                    BattlePlaceholderArt.VisualScale(RenderEntityFlags.Boss));
                Probe(probes, art.Projectile, "probe-bolt", RenderEntityKind.Projectile,
                    player + new Vector3(-3.4f, 0f, -1.8f), pitch, 1f);
                Probe(probes, art.Pickup, "probe-drop", RenderEntityKind.Pickup,
                    player + new Vector3(3.6f, 0f, -1.8f), pitch, 1f);

                foreach (ArenaTimeOfDay timeOfDay in new[] { ArenaTimeOfDay.Night, ArenaTimeOfDay.Dusk })
                {
                    runner.View.Environment.SetTimeOfDay(timeOfDay);
                    yield return null;

                    foreach (int width in new[] { RenderWidth1080, RenderWidth720 })
                    {
                        var size = new Vector2Int(width, (width * 9) / 16);

                        Color32[] withProbes = Render(size, save: true, name: Name(timeOfDay, width, true));
                        SetActive(probes, false);
                        yield return null;
                        Color32[] withoutProbes = Render(size, save: false, name: null);
                        SetActive(probes, true);
                        yield return null;

                        for (int i = 0; i < probes.Count; i++)
                        {
                            float half = Coverage(withProbes, withoutProbes, size, probes[i], 0.5f);
                            float quarter = Coverage(withProbes, withoutProbes, size, probes[i], 0.25f);

                            Debug.Log("[Stage08h] " + Name(timeOfDay, width, true) + " " + probes[i].name
                                + " lowerHalf=" + half.ToString("0.000") + " lowerQuarter="
                                + quarter.ToString("0.000"));

                            Assert.That(
                                quarter, Is.GreaterThanOrEqualTo(MinimumQuarterCoverage),
                                probes[i].name + "'s lower quarter is missing at " + size.x + "x" + size.y
                                + " (" + timeOfDay + "): coverage " + quarter.ToString("0.000")
                                + " — the floor is cutting the billboard where it drops below the plane");

                            Assert.That(
                                half, Is.GreaterThanOrEqualTo(MinimumHalfCoverage),
                                probes[i].name + "'s lower half is missing at " + size.x + "x" + size.y
                                + " (" + timeOfDay + "): coverage " + half.ToString("0.000"));
                        }
                    }
                }
            }
            finally
            {
                for (int i = 0; i < probes.Count; i++)
                {
                    if (probes[i] != null)
                    {
                        Object.Destroy(probes[i]);
                    }
                }

                if (runner != null)
                {
                    runner.SetPaused(false);
                }
            }
        }

        /// <summary>
        /// Fraction of the band that changes when the probe is hidden — i.e. that the sprite drew.
        /// </summary>
        private static float Coverage(
            Color32[] withProbe, Color32[] withoutProbe, Vector2Int size, GameObject probe, float bandHeight)
        {
            Camera camera = Camera.main;
            Assert.That(camera, Is.Not.Null, "the Battle scene has no main camera");

            // The quad's own corners in local space: the sprite's own size, and the probe's scale
            // applied by the transform. An axis-aligned bounds box would be larger than the quad at
            // this 50° tilt, and the coverage would describe empty pixels.
            Sprite sprite = probe.GetComponent<SpriteRenderer>().sprite;
            float halfX = sprite.bounds.size.x * 0.5f;
            float halfY = sprite.bounds.size.y * 0.5f;
            Transform transform = probe.transform;

            Vector2 a = Project(camera, transform.TransformPoint(new Vector3(-halfX, -halfY, 0f)), size);
            Vector2 b = Project(camera, transform.TransformPoint(new Vector3(halfX, -halfY, 0f)), size);
            Vector2 c = Project(camera, transform.TransformPoint(new Vector3(-halfX, halfY, 0f)), size);
            Vector2 d = Project(camera, transform.TransformPoint(new Vector3(halfX, halfY, 0f)), size);

            float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));
            float bandTop = minY + ((maxY - minY) * bandHeight);

            int x0 = Mathf.Clamp(Mathf.RoundToInt(minX), 0, size.x - 1);
            int x1 = Mathf.Clamp(Mathf.RoundToInt(maxX), 0, size.x - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(minY), 0, size.y - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(bandTop), 0, size.y - 1);

            int total = 0;
            int drawn = 0;
            for (int y = y0; y <= y1; y++)
            {
                int row = y * size.x;
                for (int x = x0; x <= x1; x++)
                {
                    Color32 on = withProbe[row + x];
                    Color32 off = withoutProbe[row + x];
                    int difference = Mathf.Max(
                        Mathf.Abs(on.r - off.r), Mathf.Max(Mathf.Abs(on.g - off.g), Mathf.Abs(on.b - off.b)));

                    total++;
                    if (difference > 8)
                    {
                        drawn++;
                    }
                }
            }

            return total == 0 ? 0f : drawn / (float)total;
        }

        private static Color32[] Render(Vector2Int size, bool save, string name)
        {
            Camera camera = Camera.main;
            RenderTexture target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);

            try
            {
                camera.targetTexture = target;
                camera.aspect = size.x / (float)size.y;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                if (save && !string.IsNullOrEmpty(name))
                {
                    string directory = Path.GetFullPath(Path.Combine(
                        Application.dataPath, "..", "..", "artifacts", "stage08h", "playmode"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                }

                return image.GetPixels32();
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
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

        private static string Name(ArenaTimeOfDay timeOfDay, int width, bool withFloor)
        {
            string tag = File.Exists(TagPath) ? File.ReadAllText(TagPath).Trim() : "shot";
            if (string.IsNullOrEmpty(tag))
            {
                tag = "shot";
            }

            return "stage08h-"
                + (timeOfDay == ArenaTimeOfDay.Night ? "night" : "dusk") + "-" + width
                + (withFloor ? "-floor-on-" : "-floor-off-") + tag;
        }

        private static string TagPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "stage08h", "tag.txt"));

        private static void Probe(
            List<GameObject> probes, Sprite sprite, string name, RenderEntityKind kind,
            Vector3 position, float pitch, float scale)
        {
            var probe = new GameObject(name);
            var renderer = probe.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = BattleWorldMap.SortingOrder(kind, position.z);

            probe.transform.position = position;
            probe.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            probe.transform.localScale = new Vector3(scale, scale, 1f);

            probes.Add(probe);
        }

        private static void SetActive(List<GameObject> probes, bool active)
        {
            for (int i = 0; i < probes.Count; i++)
            {
                if (probes[i] != null)
                {
                    probes[i].SetActive(active);
                }
            }
        }

        private static Vector3 PlayerPosition()
        {
            GameObject player = GameObject.Find("view-player");
            return player == null ? Vector3.zero : player.transform.position;
        }

        private static T Find<T>() where T : Object =>
            Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static IEnumerator LoadScene(string name)
        {
            int index = SceneUtility.GetBuildIndexByScenePath("Assets/Main/Scenes/" + name + ".unity");
            Assert.That(index, Is.GreaterThanOrEqualTo(0), name + " is not in the build settings");

            Scene scene = SceneManager.GetSceneAt(0);
            if (scene.isLoaded && scene.name == name)
            {
                yield break;
            }

            yield return SceneManager.LoadSceneAsync(index);
        }

        private static IEnumerator WaitForFloorToStart()
        {
            float deadline = Time.realtimeSinceStartup + StartDeadlineSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                BattleSimulationRunner runner = Find<BattleSimulationRunner>();
                if (runner != null && runner.IsRunning && runner.View != null)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("the Battle scene did not start a floor within " + StartDeadlineSeconds + " s");
        }
    }
}
