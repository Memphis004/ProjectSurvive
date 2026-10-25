using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>
    /// Stage 08e follow-up: the edge frame with the occlusion dither on, then off.
    /// </summary>
    /// <remarks>
    /// Time is frozen between the two captures. The leaves sway and the actors move every frame, so
    /// two live frames differ everywhere and prove nothing; with <c>timeScale = 0</c> the only
    /// pixels that can change are the ones the dither removes, which is the whole claim.
    /// </remarks>
    public static class ArenaFadeCompare
    {
        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "battle-env"));

        public static async Task<string> Run()
        {
            var director = Object.FindFirstObjectByType<BattleSceneDirector>();
            if (director == null || director.Runner == null || !director.Runner.IsRunning)
            {
                return "battle did not start";
            }

            if (director.Runner.View.Input is BattleInputAdapter walking)
            {
                walking.SetOverride(0f, 1f);
                for (int i = 0; i < 200; i++)
                {
                    await Task.Delay(100);
                    GameObject player = GameObject.Find("view-player");
                    if (player != null
                        && (player.transform.position.x * player.transform.position.x)
                        + (player.transform.position.z * player.transform.position.z) >= 34f * 34f)
                    {
                        break;
                    }
                }

                walking.ClearOverride();
                await Task.Delay(900);
            }

            var fade = Object.FindFirstObjectByType<ArenaCardFade>();
            var environment = Object.FindFirstObjectByType<ArenaEnvironmentView>();
            GameObject viewPlayer = GameObject.Find("view-player");
            if (fade == null || environment == null || viewPlayer == null)
            {
                return "fade=" + (fade != null) + " environment=" + (environment != null) + " player=" + (viewPlayer != null);
            }

            // The fade has settled by now; freezing time stops the sway so the pair can be a pixel
            // comparison rather than two different frames.
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            await Task.Delay(200);

            int fading = fade.FadingCount;
            string on = Capture("fade-on");

            environment.SetOcclusionFade(false);
            await Task.Delay(200);
            string off = Capture("fade-off");

            environment.SetOcclusionFade(true);
            Time.timeScale = previousScale;

            Vector3 playerPosition = viewPlayer.transform.position;
            Camera camera = Camera.main;
            Vector3 screen = camera.WorldToScreenPoint(playerPosition);
            return "on=" + on + " fading=" + fading
                + "\noff=" + off
                + "\nplayer=" + F(playerPosition)
                + "\nscreen=" + screen.x.ToString("0", CultureInfo.InvariantCulture)
                + "," + screen.y.ToString("0", CultureInfo.InvariantCulture)
                + "\nminFade=" + fade.MinFade.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Capture(string name)
        {
            Camera camera = Camera.main;
            const int width = 1920;
            const int height = 1080;

            var borrowed = new List<Canvas>();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (canvas.rootCanvas != canvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        || !canvas.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    borrowed.Add(canvas);
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 1f;
                }

                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string path = Path.Combine(OutputDirectory, "stage08e-" + name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                return path;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                foreach (Canvas canvas in borrowed)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }

                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
            }
        }

        private static string F(Vector3 value) =>
            value.x.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + value.y.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + value.z.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
