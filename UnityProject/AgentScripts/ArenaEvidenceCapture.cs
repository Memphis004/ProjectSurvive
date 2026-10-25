using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace AgentScripts
{
    /// <summary>Stage 08e evidence: the arena at two player positions, with metrics that match.</summary>
    public static class ArenaEvidenceCapture
    {
        private const int Ghosts = 300;

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui", "battle-env"));

        public static async Task<string> Run()
        {
            Directory.CreateDirectory(OutputDirectory);

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            BattleSimulationRunner runner = null;
            for (int i = 0; i < 400; i++)
            {
                await Task.Delay(100);
                if (!EditorApplication.isPlaying)
                {
                    continue;
                }

                var director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
                if (director != null && director.Runner != null && director.Runner.IsRunning)
                {
                    runner = director.Runner;
                    break;
                }
            }

            if (runner == null)
            {
                return "battle did not start";
            }

            runner.Submit(BattleCommand.DevGodMode(true));
            await Task.Delay(1200);

            int spawned = 0;
            for (int i = 0; i < Ghosts; i++)
            {
                if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                {
                    spawned++;
                }
            }

            await Task.Delay(3500);

            runner.View.Environment?.SetNight(true);
            await Task.Delay(900);

            var log = new StringBuilder();
            log.AppendLine("ghosts requested=" + Ghosts + " spawned=" + spawned
                + " bound=" + runner.View.BoundViews
                + " entities=" + runner.View.Snapshot.CurrentCount);

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                await CaptureState(runner, "center", batches, drawCalls, setPass, log);

                // Walk to the edge the way a player would: the input override is the same adapter the
                // keyboard feeds, so nothing here is a teleport.
                if (runner.View.Input is BattleInputAdapter input)
                {
                    input.SetOverride(0f, 1f);
                    for (int i = 0; i < 200; i++)
                    {
                        await Task.Delay(100);
                        Vector3 player = PlayerPosition();
                        if ((player.x * player.x) + (player.z * player.z) >= 34f * 34f)
                        {
                            break;
                        }
                    }

                    input.ClearOverride();
                    await Task.Delay(700);
                    log.AppendLine("walked to player=" + Format(PlayerPosition()));
                }
                else
                {
                    log.AppendLine("input adapter was not a BattleInputAdapter; the edge picture is missing");
                }

                await CaptureState(runner, "edge", batches, drawCalls, setPass, log);
            }

            log.AppendLine("viewsGrownDuringPlay=" + (runner.View != null ? runner.View.Pool.CreatedAfterWarmup : -1));

            File.WriteAllText(Path.Combine(OutputDirectory, "metrics.txt"), log.ToString());
            EditorApplication.isPlaying = false;
            return log.ToString();
        }

        private static async Task CaptureState(
            BattleSimulationRunner runner, string state,
            ProfilerRecorder batches, ProfilerRecorder drawCalls, ProfilerRecorder setPass,
            StringBuilder log)
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            {
                float frameMs = await AverageFrameMs();

                // The view can be torn down under us if the floor settles mid-run; read what it holds
                // before the capture and report -1 rather than throwing out of the evidence run.
                int bound = runner.View != null ? runner.View.BoundViews : -1;
                int entities = runner.View != null ? runner.View.Snapshot.CurrentCount : -1;
                string path = Capture(runner, state, size.x, size.y);
                string line = state + " " + size.x + "x" + size.y
                    + ": batches=" + batches.LastValue
                    + " draw=" + drawCalls.LastValue
                    + " setPass=" + setPass.LastValue
                    + " frameMs=" + frameMs.ToString("0.00", CultureInfo.InvariantCulture)
                    + " bound=" + bound
                    + " entities=" + entities
                    + " player=" + Format(PlayerPosition())
                    + " file=" + Path.GetFileName(path);

                var fade = UnityEngine.Object.FindFirstObjectByType<ProjectSurvive.Game.Presentation.Battle.Environment.ArenaCardFade>();
                log.AppendLine(line + " fadingCards=" + (fade != null ? fade.FadingCount : -1));
                Debug.Log("[ArenaEvidence] " + line);
            }
        }

        private static Vector3 PlayerPosition()
        {
            GameObject player = GameObject.Find("view-player");
            return player == null ? Vector3.zero : player.transform.position;
        }

        private static string Format(Vector3 value) =>
            value.x.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + value.z.ToString("0.00", CultureInfo.InvariantCulture);

        private static async Task<float> AverageFrameMs()
        {
            var samples = new List<float>(40);
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(16);
                samples.Add(Time.unscaledDeltaTime * 1000f);
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        private static string Capture(BattleSimulationRunner runner, string state, int width, int height)
        {
            Camera camera = Camera.main;
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

                string path = Path.Combine(
                    OutputDirectory, "stage08e-" + state + "-" + width + "x" + height + ".png");
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
