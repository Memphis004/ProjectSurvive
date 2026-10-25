using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>Does the compiled card shader apply _Fade at all?</summary>
    public static class ArenaFadeMaterialTest
    {
        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "battle-env"));

        public static async Task<string> Run()
        {
            var log = new StringBuilder();
            var materials = new List<Material>();
            foreach (ArenaCard candidate in Object.FindObjectsByType<ArenaCard>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var candidateRenderer = candidate.GetComponent<MeshRenderer>();
                if (candidateRenderer == null || candidate.Zone == ArenaCardZone.Decal)
                {
                    continue;
                }

                Material material = candidateRenderer.sharedMaterial;
                if (material != null && !materials.Contains(material))
                {
                    materials.Add(material);
                }
            }

            if (materials.Count == 0)
            {
                return "no card in the scene";
            }

            Shader shader = materials[0].shader;
            log.AppendLine("materials=" + materials.Count + " shader=" + shader.name
                + " supported=" + shader.isSupported
                + " hasFadeProperty=" + materials[0].HasProperty(ArenaCardFade.FadeProperty)
                + " value=" + materials[0].GetFloat(ArenaCardFade.FadeProperty));

            foreach (Material material in materials)
            {
                material.SetFloat(ArenaCardFade.FadeProperty, 0f);
            }

            await Task.Delay(400);
            string off = Capture("material-fade-0");

            foreach (Material material in materials)
            {
                material.SetFloat(ArenaCardFade.FadeProperty, 1f);
            }

            await Task.Delay(400);
            string on = Capture("material-fade-1");

            log.AppendLine("captured0=" + off);
            log.AppendLine("captured1=" + on);
            return log.ToString();
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
    }
}
