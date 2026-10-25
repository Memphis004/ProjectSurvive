using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace AgentScripts
{
    public static class ArenaBatchProbe
    {
        public static async Task<string> Run()
        {
            var log = new List<string>();

            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            for (int i = 0; i < 200; i++)
            {
                await Task.Delay(100);
                if (EditorApplication.isPlaying)
                {
                    var d0 = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
                    if (d0 != null && d0.Runner != null && d0.Runner.IsRunning)
                    {
                        break;
                    }
                }
            }

            if (!EditorApplication.isPlaying)
            {
                return "did not enter play mode";
            }

            var director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
            if (director == null || director.Runner == null || !director.Runner.IsRunning)
            {
                EditorApplication.isPlaying = false;
                return "battle did not start: " + (director == null ? "no director" : director.StartError);
            }

            var runner = director.Runner;

            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1))
            {
                await Task.Delay(1500);
                Say(log, "baseline: " + Line(batches, drawCalls, setPass, tris) +
                        " bound=" + runner.View.BoundViews + " entities=" + runner.View.Snapshot.CurrentCount +
                        " views=" + runner.View.Pool.CreatedTotal);

                runner.Submit(BattleCommand.DevGodMode(true));
                int spawned = 0;
                for (int i = 0; i < 300; i++)
                {
                    if (runner.Submit(BattleCommand.DevSpawnGhost()) == CommandResult.Ok)
                    {
                        spawned++;
                    }
                }

                await Task.Delay(3000);
                Say(log, "300ghosts: " + Line(batches, drawCalls, setPass, tris) +
                        " spawned=" + spawned + " bound=" + runner.View.BoundViews +
                        " entities=" + runner.View.Snapshot.CurrentCount);

                await Task.Delay(1000);
                Say(log, "300ghosts(2): " + Line(batches, drawCalls, setPass, tris) +
                        " bound=" + runner.View.BoundViews + " entities=" + runner.View.Snapshot.CurrentCount);
            }

            EditorApplication.isPlaying = false;
            return string.Join("\n", log);
        }

        private static void Say(List<string> log, string line)
        {
            log.Add(line);
            Debug.Log("[ArenaBatchProbe] " + line);
        }

        private static string Line(
            ProfilerRecorder batches, ProfilerRecorder drawCalls,
            ProfilerRecorder setPass, ProfilerRecorder tris)
        {
            return "batches=" + batches.LastValue +
                   " drawCalls=" + drawCalls.LastValue +
                   " setPass=" + setPass.LastValue +
                   " tris=" + tris.LastValue +
                   " frameMs=" + (Time.unscaledDeltaTime * 1000f).ToString("0.00");
        }
    }
}
