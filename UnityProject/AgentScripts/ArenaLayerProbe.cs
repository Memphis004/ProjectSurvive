using System;
using System.IO;
using System.Threading.Tasks;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>Temporary: three captures that isolate what paints the pale floor.</summary>
    public static class ArenaLayerProbe
    {
        public static async Task<string> Run()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            BattleSimulationRunner runner = null;
            for (int i = 0; i < 300; i++)
            {
                await Task.Delay(100);
                if (EditorApplication.isPlaying)
                {
                    var director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
                    if (director != null && director.Runner != null && director.Runner.IsRunning)
                    {
                        runner = director.Runner;
                        break;
                    }
                }
            }

            if (runner == null)
            {
                return "battle did not start";
            }

            await Task.Delay(800);

            var root = GameObject.Find("Arena");
            var cards = root != null ? root.transform.Find("Cards") : null;
            var decals = root != null ? root.transform.Find("Decals") : null;
            var pools = root != null ? root.transform.Find("LightPools") : null;

            string all = Capture("probe-all");

            if (decals != null) decals.gameObject.SetActive(false);
            await Task.Delay(200);
            string noDecals = Capture("probe-nodecals");
            if (decals != null) decals.gameObject.SetActive(true);

            if (cards != null) cards.gameObject.SetActive(false);
            await Task.Delay(200);
            string noCards = Capture("probe-nocards");
            if (cards != null) cards.gameObject.SetActive(true);

            if (pools != null) pools.gameObject.SetActive(false);
            await Task.Delay(200);
            string noPools = Capture("probe-nopools");
            if (pools != null) pools.gameObject.SetActive(true);

            // Turn off only the dirt patches, then only the small decals.
            var dirt = new System.Collections.Generic.List<GameObject>();
            var small = new System.Collections.Generic.List<GameObject>();
            if (decals != null)
            {
                foreach (Transform child in decals)
                {
                    (child.name.Contains("dirt") ? dirt : small).Add(child.gameObject);
                }
            }

            foreach (var go in dirt) go.SetActive(false);
            await Task.Delay(200);
            string noDirt = Capture("probe-nodirt");
            foreach (var go in dirt) go.SetActive(true);

            foreach (var go in small) go.SetActive(false);
            await Task.Delay(200);
            string noSmall = Capture("probe-nosmall");
            foreach (var go in small) go.SetActive(true);

            EditorApplication.isPlaying = false;
            return all + "\n" + noDecals + "\n" + noCards + "\n" + noPools + "\n" + noDirt + "\n" + noSmall;
        }

        private static string Capture(string slug)
        {
            Camera camera = Camera.main;
            int width = 1280, height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);

            try
            {
                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                string path = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "..", "artifacts", "ui", "battle-env", slug + ".png"));
                File.WriteAllBytes(path, image.EncodeToPNG());
                return slug + " -> " + path;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
