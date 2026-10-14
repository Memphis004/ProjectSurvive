using System;
using System.IO;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Web;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// The Stage 08 camera experiment, as a menu item rather than a paragraph.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GDD fixes the tilt at roughly 50° and leaves orthographic vs perspective to be decided
    /// by trying both ("ทดลอง orthographic กับ perspective (depth sorting, การบังตัวละคร)
    /// แล้วเลือกด้วย ADR"). So the two things it names are measured here on the shipped rig, and
    /// the numbers are written to <c>artifacts/stage08/camera-projection.json</c>, which
    /// <c>docs/adr/0018-battle-camera-projection.md</c> cites. Re-running the item re-derives
    /// them; nothing is remembered from the run that wrote the ADR.
    /// </para>
    /// <para>
    /// Two measurements, one per named question:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Scale and framing</b> — how many screen pixels one world unit is worth at the near,
    /// middle and far edge of the visible ground band, and how deep that band is. A projection
    /// whose scale changes across the arena gives the same ghost a different size at different
    /// depths, which is a legibility cost in a game where a crowd of identical silhouettes has to
    /// be read at a glance.
    /// </item>
    /// <item>
    /// <b>Occlusion</b> — two probe quads are stacked at one point with the sorting orders
    /// <see cref="BattleWorldMap.SortingOrder"/> produces, the frame is rendered, and the centre
    /// pixel is sampled. The claim under test is that the projection does <i>not</i> decide which
    /// character is in front, because the scene sorts sprites explicitly; a claim about a picture
    /// needs a picture, so it is checked in pixels rather than reasoned about.
    /// </item>
    /// </list>
    /// <para>
    /// The probe camera is a temporary object built by <see cref="CreateAuthoredCamera"/>, so the
    /// scene is never touched and the probe cannot end up describing a camera nobody runs.
    /// </para>
    /// </remarks>
    public static class BattleCameraProbe
    {
        /// <summary>Probe resolution. Fixed, so the artifact does not depend on the machine.</summary>
        private const int ProbeWidth = 1280;

        /// <inheritdoc cref="ProbeWidth"/>
        private const int ProbeHeight = 720;

        /// <summary>Resolution the occlusion probe renders at (small, and it only samples one pixel).</summary>
        private const int OcclusionProbeSize = 128;

        /// <summary>Simulation Y the near probe quad stands at.</summary>
        private const float OcclusionNearY = -5f;

        /// <summary>Simulation Y the far probe quad stands at.</summary>
        private const float OcclusionFarY = 5f;

        /// <summary>
        /// Runs the experiment and writes its evidence. Safe to run from any open scene: the probe
        /// builds its own camera.
        /// </summary>
        [MenuItem("ProjectSurvive/Stage 08/4. Measure camera projection (ADR 0018)", priority = 103)]
        public static void MeasureCameraProjection()
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string artifactsFolder = Path.Combine(repositoryRoot, "artifacts", "stage08");
            Directory.CreateDirectory(artifactsFolder);

            var json = new JsonReportWriter();
            json.BeginObject();
            json.Write("unityVersion", Application.unityVersion);
            json.Write("resolution", ProbeWidth + "x" + ProbeHeight);
            json.Write("pitchDegrees", BattleCameraRig.PitchDegrees);
            json.Write("orthographicSize", BattleCameraRig.OrthographicSize);
            json.Write("fieldOfView", BattleCameraRig.FieldOfView);
            json.Write("perspectiveDistance", BattleCameraRig.PerspectiveDistance);

            MeasureProjection(json, BattleProjection.Orthographic);
            MeasureProjection(json, BattleProjection.Perspective);

            json.EndObject();

            File.WriteAllText(
                Path.Combine(artifactsFolder, "camera-projection.json"), json.ToString() + Environment.NewLine);

            Debug.Log("ProjectSurvive: camera projection measured -> artifacts/stage08/camera-projection.json");
        }

        /// <summary>
        /// The battle camera, posed the way the Battle scene hangs it.
        /// </summary>
        /// <remarks>
        /// Deliberately the only place the pose is written: <c>ProjectSetup</c>'s scene builder calls
        /// this, and so does the probe, so a change to the tilt or the standoff distance cannot
        /// leave the ADR describing a camera the game does not use. The pose comes from
        /// <see cref="BattleCameraRig"/> itself rather than from copied arithmetic.
        /// </remarks>
        public static Camera CreateAuthoredCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 1f);
            camera.nearClipPlane = 0.3f;

            var rig = new BattleCameraRig(camera, BattleProjection.Orthographic);
            rig.SnapTo(Vector3.zero);

            // URP needs its per-camera data component; without it the camera renders
            // through the built-in path and the scene comes out magenta or empty.
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            return camera;
        }

        /// <summary>
        /// One projection's row of the artifact: framing, scale across the band, and the occlusion
        /// probe's two booleans. Both projections are written by this one method, so the two rows
        /// cannot drift into different shapes.
        /// </summary>
        private static void MeasureProjection(JsonReportWriter json, BattleProjection projection)
        {
            Camera camera = CreateAuthoredCamera();
            RenderTexture target = null;

            try
            {
                new BattleCameraRig(camera, projection).SnapTo(Vector3.zero);

                // The band is measured at 720p so the pixels-per-unit numbers are about the
                // projection and not about the window the experiment happened to run in.
                target = new RenderTexture(ProbeWidth, ProbeHeight, 24);
                camera.targetTexture = target;

                float nearZ = GroundZAtScreenY(camera, 0f);
                float farZ = GroundZAtScreenY(camera, ProbeHeight);

                // Sampled two units inside the band: the exact edges are where a projection
                // diverges fastest, and a number taken on the edge is about the edge rather
                // than about the arena the player is looking at.
                float near = PixelsPerUnit(camera, nearZ + 2f);
                float mid = PixelsPerUnit(camera, 0f);
                float far = PixelsPerUnit(camera, farZ - 2f);

                json.BeginObjectProperty(projection.ToString().ToLowerInvariant());
                json.Write("groundBandNearZ", nearZ);
                json.Write("groundBandFarZ", farZ);
                json.Write("visibleDepth", farZ - nearZ);
                json.Write("pixelsPerUnitNear", near);
                json.Write("pixelsPerUnitMid", mid);
                json.Write("pixelsPerUnitFar", far);
                json.Write("nearOverFarScale", near / far);

                // The spread the sorting orders put between the two probe quads: a large span
                // says the authoring, not the depth buffer, is what puts a character in front.
                json.Write("sortingOrderSpread", BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, OcclusionNearY)
                    - BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, OcclusionFarY));

                camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target);
                target = new RenderTexture(OcclusionProbeSize, OcclusionProbeSize, 24);
                camera.targetTexture = target;

                (bool nearOnTop, bool higherOrderWins) = ProbeOcclusion(camera, target);
                json.Write("authoredOrderPutsNearOnTop", nearOnTop);
                json.Write("higherSortingOrderWins", higherOrderWins);
                json.EndObject();
            }
            finally
            {
                camera.targetTexture = null;
                if (target is not null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }

                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }

        /// <summary>
        /// Stacks two probe quads at one point and samples the centre pixel: once with the sorting
        /// orders the game authors, once with the far quad forced in front.
        /// </summary>
        /// <remarks>
        /// Both cases are needed. The first says the game's own order draws the near character over
        /// the far one; the second says the pixel moved because the <i>order</i> moved, which is what
        /// distinguishes "sprites are sorted" from "the depth buffer happened to agree".
        /// </remarks>
        private static (bool NearOnTop, bool HigherOrderWins) ProbeOcclusion(Camera camera, RenderTexture target)
        {
            Sprite quad = CreateProbeQuad();
            int nearOrder = BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, OcclusionNearY);
            int farOrder = BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, OcclusionFarY);

            GameObject nearQuad = CreateProbeObject("probe-near", quad, Color.red, nearOrder);
            GameObject farQuad = CreateProbeObject("probe-far", quad, Color.green, farOrder);

            try
            {
                Color authored = SampleCentre(camera, target);
                bool nearOnTop = IsNearer(authored);

                farQuad.GetComponent<SpriteRenderer>().sortingOrder = nearOrder + 1;
                Color swapped = SampleCentre(camera, target);
                bool higherOrderWins = !IsNearer(swapped);

                return (nearOnTop, higherOrderWins);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(nearQuad);
                UnityEngine.Object.DestroyImmediate(farQuad);
                UnityEngine.Object.DestroyImmediate(quad.texture);
                UnityEngine.Object.DestroyImmediate(quad);
            }
        }

        /// <summary>Red is the near quad, green is the far one; whichever dominates the sample wins.</summary>
        private static bool IsNearer(Color sampled) => sampled.r > sampled.g;

        private static Color SampleCentre(Camera camera, RenderTexture target)
        {
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;

            var pixel = new Texture2D(3, 3, TextureFormat.RGBA32, false);
            Color sampled;
            try
            {
                // A 3x3 block around the centre, so one antialiased pixel cannot decide the answer.
                pixel.ReadPixels(new Rect((target.width / 2) - 1, (target.height / 2) - 1, 3, 3), 0, 0);
                pixel.Apply();
                sampled = pixel.GetPixel(1, 1);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pixel);
                RenderTexture.active = active;
                camera.targetTexture = previous;
            }

            return sampled;
        }

        private static GameObject CreateProbeObject(string name, Sprite sprite, Color colour, int sortingOrder)
        {
            var probe = new GameObject(name);
            var renderer = probe.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = colour;
            renderer.sortingOrder = sortingOrder;

            // Same billboard the bridge applies, so the quad faces this camera square on.
            probe.transform.SetPositionAndRotation(
                new Vector3(0f, 0.5f, 0f), Quaternion.Euler(BattleCameraRig.PitchDegrees, 0f, 0f));
            return probe;
        }

        /// <summary>An opaque one-unit quad with no asset behind it: a probe, not art.</summary>
        private static Sprite CreateProbeQuad()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
        }

        private static float PixelsPerUnit(Camera camera, float simulationY) =>
            Mathf.Abs(
                camera.WorldToScreenPoint(new Vector3(0f, 1f, simulationY)).y
                - camera.WorldToScreenPoint(new Vector3(0f, 0f, simulationY)).y);

        /// <summary>
        /// Where the ground plane is under a screen row, in simulation Y. The camera is aimed at
        /// the origin and the floor sits on world Y = 0, so one ray intersection answers it.
        /// </summary>
        private static float GroundZAtScreenY(Camera camera, float screenY)
        {
            Ray ray = camera.ScreenPointToRay(
                new Vector3(camera.pixelWidth * 0.5f, screenY, 0f));
            float distance = -ray.origin.y / ray.direction.y;
            return (ray.origin + (ray.direction * distance)).z;
        }
    }
}
