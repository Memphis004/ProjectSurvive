using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

namespace AgentScripts
{
    /// <summary>Which scene group costs which batches: one group hidden at a time.</summary>
    public static class ArenaGroupBatchProbe
    {
        public static async Task<string> Run()
        {
            var log = new List<string>();

            if (!EditorApplication.isPlaying)
            {
                return "not playing";
            }

            BattleSceneDirector director = UnityEngine.Object.FindFirstObjectByType<BattleSceneDirector>();
            if (director == null || director.Runner == null || !director.Runner.IsRunning)
            {
                return "battle did not start: " + (director == null ? "no director" : director.StartError);
            }

            var runner = director.Runner;
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1))
            using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1))
            {
                await Task.Delay(1200);
                log.Add("baseline: " + Line(batches, drawCalls, setPass) + " entities=" + runner.View.Snapshot.CurrentCount);

                string[] groups = { "CardShadows", "Motes", "Decals", "ScreenGradient", "LightPools", "Cards" };
                GameObject arena = GameObject.Find("Arena");
                for (int i = 0; i < groups.Length; i++)
                {
                    Transform group = arena == null ? null : arena.transform.Find(groups[i]);
                    if (group == null)
                    {
                        log.Add(groups[i] + ": not found");
                        continue;
                    }

                    GameObject target = group.gameObject;
                    bool previous = target.activeSelf;
                    target.SetActive(false);
                    await Task.Delay(700);
                    log.Add(groups[i] + " off: " + Line(batches, drawCalls, setPass));
                    target.SetActive(previous);
                    await Task.Delay(500);
                }

                // How many shadow renderers share the one material, and are they static?
                var shadows = arena == null ? null : arena.transform.Find("CardShadows");
                if (shadows != null)
                {
                    var renderers = shadows.GetComponentsInChildren<MeshRenderer>(true);
                    var materials = new HashSet<int>();
                    int staticOn = 0;
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        materials.Add(renderers[i].sharedMaterial == null ? 0 : renderers[i].sharedMaterial.GetInstanceID());
                        if (UnityEditor.GameObjectUtility.GetStaticEditorFlags(renderers[i].gameObject)
                            .HasFlag(StaticEditorFlags.BatchingStatic))
                        {
                            staticOn++;
                        }
                    }

                    log.Add("shadows: renderers=" + renderers.Length + " materials=" + materials.Count
                        + " batchingStatic=" + staticOn);
                }

                await Task.Delay(300);
                log.Add("baseline again: " + Line(batches, drawCalls, setPass));
            }

            return string.Join("\n", log);
        }

        private static string Line(
            ProfilerRecorder batches, ProfilerRecorder drawCalls, ProfilerRecorder setPass)
        {
            return "batches=" + batches.LastValue + " drawCalls=" + drawCalls.LastValue + " setPass=" + setPass.LastValue;
        }
    }
}
