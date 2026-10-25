using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>Stage art001: the "before" pictures, at the two evidence resolutions.</summary>
    public static class ArenaBeforeCapture
    {
        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "battle-env"));

        public static async Task<string> Run()
        {
            Directory.CreateDirectory(OutputDirectory);

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            Camera camera = null;
            for (int i = 0; i < 300; i++)
            {
                await Task.Delay(100);
                if (EditorApplication.isPlaying && (camera = Camera.main) != null)
                {
                    break;
                }
            }

            if (camera == null)
            {
                return "no camera";
            }

            await Task.Delay(1200);

            var written = new List<string>();
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                string path = Capture(camera, size.x, size.y);
                written.Add(path);
            }

            return string.Join("\n", written);
        }

        private static string Capture(Camera camera, int width, int height)
        {
            var borrowed = new List<Canvas>();
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            float previousAspect = camera.aspect;
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
                camera.aspect = width / (float)height;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string path = Path.Combine(OutputDirectory, "art001-before-" + width + "x" + height + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                return path;
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
}
