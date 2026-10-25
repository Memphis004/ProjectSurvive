using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>Stage art001: what the built arena actually contains, read off the live scene.</summary>
    public static class ArenaCardAudit
    {
        public static async Task<string> Run()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.EnterPlaymode();
            }

            for (int i = 0; i < 300; i++)
            {
                await Task.Delay(100);
                if (EditorApplication.isPlaying)
                {
                    break;
                }
            }

            await Task.Delay(1000);

            var cards = UnityEngine.Object.FindObjectsByType<ArenaCard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var log = new StringBuilder();
            log.AppendLine("cards=" + cards.Length);

            var zones = new Dictionary<ArenaCardZone, int>();
            var pitches = new SortedSet<int>();
            var sortErrors = 0;
            float maxRadius = 0f;
            var sortMin = int.MaxValue;
            var sortMax = int.MinValue;
            float pitchYawMax = 0f;

            foreach (ArenaCard card in cards)
            {
                Vector3 p = card.transform.position;
                float radius = Mathf.Sqrt((p.x * p.x) + (p.z * p.z));
                maxRadius = Mathf.Max(maxRadius, radius);
                pitches.Add(Mathf.RoundToInt(card.transform.eulerAngles.x));
                pitchYawMax = Mathf.Max(pitchYawMax, Mathf.Abs(Mathf.DeltaAngle(0f, card.transform.eulerAngles.y)));

                if (!zones.ContainsKey(card.Zone))
                {
                    zones[card.Zone] = 0;
                }

                zones[card.Zone]++;

                var renderer = card.GetComponent<MeshRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                sortMin = Math.Min(sortMin, renderer.sortingOrder);
                sortMax = Math.Max(sortMax, renderer.sortingOrder);

                // A decal is a floor quad and never has to interleave with an actor.
                if (!card.Zone.ToString().Equals("Decal")
                    && renderer.sortingOrder != BattleWorldMap.PropSortingOrder(p.z))
                {
                    sortErrors++;
                }
            }

            foreach (KeyValuePair<ArenaCardZone, int> pair in zones)
            {
                log.AppendLine("zone " + pair.Key + "=" + pair.Value);
            }

            log.AppendLine("distinct eulerX=" + string.Join(",", pitches));
            log.AppendLine("max |yaw|=" + F(pitchYawMax));
            log.AppendLine("max radius=" + F(maxRadius) + " (arena=" + F(BattleViewCaps.ArenaRadius) + ")");
            log.AppendLine("sortingOrder " + sortMin + ".." + sortMax + " mismatches=" + sortErrors);

            // The sprite side of the same promise: what pitch are the pooled actors leaning at?
            var actors = new List<string>();
            GameObject player = GameObject.Find("view-player");
            if (player != null)
            {
                actors.Add("player eulerX=" + F(player.transform.eulerAngles.x)
                    + " pos=" + F(player.transform.position.x) + "," + F(player.transform.position.z));
            }

            foreach (SpriteRenderer renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.name == "view-ghost" && renderer.enabled && actors.Count < 4)
                {
                    actors.Add("ghost eulerX=" + F(renderer.transform.eulerAngles.x)
                        + " pos=" + F(renderer.transform.position.x) + "," + F(renderer.transform.position.z));
                }
            }

            foreach (string actor in actors)
            {
                log.AppendLine(actor);
            }

            log.AppendLine("rigPitch=" + F(BattleCameraRig.PitchDegrees));
            Debug.Log("[ArenaCardAudit]\n" + log);
            return log.ToString();
        }

        private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
