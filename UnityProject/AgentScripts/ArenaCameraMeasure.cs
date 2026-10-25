using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>Stage art001: what the battle camera really is, and how much ground it sees.</summary>
    public static class ArenaCameraMeasure
    {
        public static async Task<string> Run()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            Camera camera = null;
            for (int i = 0; i < 300; i++)
            {
                await Task.Delay(100);
                if (!EditorApplication.isPlaying)
                {
                    continue;
                }

                camera = Camera.main;
                if (camera != null)
                {
                    break;
                }
            }

            if (camera == null)
            {
                return "no camera";
            }

            await Task.Delay(1200);

            var log = new StringBuilder();
            log.AppendLine("projection=" + (camera.orthographic ? "orthographic" : "perspective"));
            log.AppendLine("orthographicSize=" + F(camera.orthographicSize));
            log.AppendLine("fieldOfView=" + F(camera.fieldOfView));
            log.AppendLine("euler=" + F(camera.transform.eulerAngles.x) + "," + F(camera.transform.eulerAngles.y)
                + "," + F(camera.transform.eulerAngles.z));
            log.AppendLine("position=" + F(camera.transform.position.x) + "," + F(camera.transform.position.y)
                + "," + F(camera.transform.position.z));
            log.AppendLine("near=" + F(camera.nearClipPlane) + " far=" + F(camera.farClipPlane));
            log.AppendLine("rigPitch=" + F(BattleCameraRig.PitchDegrees)
                + " rigOrthoSize=" + F(BattleCameraRig.OrthographicSize)
                + " billboardPitch=" + F(new BattleCameraRig(camera, camera.orthographic
                    ? BattleProjection.Orthographic
                    : BattleProjection.Perspective).BillboardPitch));
            log.AppendLine("arenaRadius(presentation cap)=" + F(BattleViewCaps.ArenaRadius)
                + " target=" + F(camera.transform.position.x) + "," + F(camera.transform.position.z));

            // Ground plane is world Y = 0. Cast each viewport corner onto it and read where it lands.
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                float aspect = size.x / (float)size.y;
                var probe = new GameObject("aspect-probe").AddComponent<Camera>();
                probe.CopyFrom(camera);
                probe.aspect = aspect;
                probe.enabled = false;
                probe.targetTexture = null;

                Vector3 nearLeft = Ground(probe, new Vector3(0f, 0f), out bool nearOk);
                Vector3 nearRight = Ground(probe, new Vector3(1f, 0f), out _);
                Vector3 farLeft = Ground(probe, new Vector3(0f, 1f), out bool farOk);
                Vector3 farRight = Ground(probe, new Vector3(1f, 1f), out _);

                float[] ys = { nearLeft.z, nearRight.z, farLeft.z, farRight.z };
                float[] xs = { nearLeft.x, nearRight.x, farLeft.x, farRight.x };
                Array.Sort(ys);
                Array.Sort(xs);

                float widthPx = size.x;
                float pxPerUnitX = widthPx / (xs[xs.Length - 1] - xs[0]);
                float pxPerUnitZ = size.y / (ys[ys.Length - 1] - ys[0]);

                log.AppendLine("--- " + size.x + "x" + size.y + " aspect=" + F(aspect)
                    + " ok=" + nearOk + "/" + farOk);
                log.AppendLine("  x " + F(xs[0]) + ".." + F(xs[xs.Length - 1])
                    + "  halfWidth=" + F((xs[xs.Length - 1] - xs[0]) * 0.5f));
                log.AppendLine("  simY " + F(ys[0]) + ".." + F(ys[ys.Length - 1])
                    + "  depth=" + F(ys[ys.Length - 1] - ys[0]));
                log.AppendLine("  pxPerWorldUnit x=" + F(pxPerUnitX) + " z=" + F(pxPerUnitZ)
                    + " (z is foreshortened by pitch)");

                UnityEngine.Object.DestroyImmediate(probe.gameObject);
            }

            log.AppendLine("player=" + Where(GameObject.Find("view-player")));
            Debug.Log("[ArenaCameraMeasure]\n" + log);
            return log.ToString();
        }

        private static string Where(GameObject go) =>
            go == null ? "none" : F(go.transform.position.x) + "," + F(go.transform.position.z);

        private static Vector3 Ground(Camera camera, Vector2 viewport, out bool ok)
        {
            Ray ray = camera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                ok = true;
                return ray.GetPoint(distance);
            }

            ok = false;
            return Vector3.zero;
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
