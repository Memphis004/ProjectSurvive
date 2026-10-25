using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AgentScripts
{
    /// <summary>Temporary: is the pale card wash post-processing, the arena tint, or fog?</summary>
    public static class ArenaDiag
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

            Camera camera = Camera.main;
            var additional = camera.GetUniversalAdditionalCameraData();
            var log = new List<string>();

            log.Add(Capture("diag-base"));

            additional.renderPostProcessing = false;
            await Task.Delay(120);
            log.Add(Capture("diag-nopost"));
            additional.renderPostProcessing = true;

            Shader.SetGlobalColor("_ArenaTint", Color.red);
            await Task.Delay(120);
            log.Add(Capture("diag-tintred"));

            Shader.SetGlobalColor("_ArenaTint", Color.white);
            RenderSettings.fog = false;
            await Task.Delay(120);
            log.Add(Capture("diag-nofog"));

            log.Add("fogActive=" + RenderSettings.fog + " fogMode=" + RenderSettings.fogMode + " fogColor=" + RenderSettings.fogColor + " density=" + RenderSettings.fogDensity);
            log.Add("tint=" + Shader.GetGlobalColor("_ArenaTint"));

            EditorApplication.isPlaying = false;
            return string.Join("\n", log);
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
                return slug;
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
