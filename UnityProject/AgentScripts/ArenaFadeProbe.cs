using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;

namespace AgentScripts
{
    /// <summary>Stage 08e follow-up: is the occlusion dither actually reaching the renderer?</summary>
    public static class ArenaFadeProbe
    {
        public static async Task<string> Run()
        {
            var log = new StringBuilder();

            // Walk to the edge the way the capture does, so the probe looks at a real occlusion.
            var director = Object.FindFirstObjectByType<BattleSceneDirector>();
            if (director != null && director.Runner != null && director.Runner.View.Input is BattleInputAdapter walking)
            {
                walking.SetOverride(0f, 1f);
                for (int i = 0; i < 200; i++)
                {
                    await Task.Delay(100);
                    GameObject probePlayer = GameObject.Find("view-player");
                    if (probePlayer != null
                        && (probePlayer.transform.position.x * probePlayer.transform.position.x)
                        + (probePlayer.transform.position.z * probePlayer.transform.position.z) >= 34f * 34f)
                    {
                        break;
                    }
                }

                walking.ClearOverride();
                await Task.Delay(600);
            }

            var fade = Object.FindFirstObjectByType<ArenaCardFade>();
            log.AppendLine("fade component=" + (fade == null ? "none" : "found")
                + " enabled=" + (fade != null && fade.Enabled));

            GameObject player = GameObject.Find("view-player");
            log.AppendLine("player=" + (player == null ? "none" : F(player.transform.position)));

            var block = new MaterialPropertyBlock();
            int fading = 0;
            var cards = Object.FindObjectsByType<ArenaCard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (ArenaCard card in cards)
            {
                var renderer = card.GetComponent<MeshRenderer>();
                if (renderer == null || card.Zone == ArenaCardZone.Decal)
                {
                    continue;
                }

                Vector3 p = card.transform.position;
                bool covers = player != null && ArenaCardFade.Covers(
                    p.x, p.z, card.transform.localScale.x, card.transform.localScale.y,
                    player.transform.position.x, player.transform.position.z, 0f);

                renderer.GetPropertyBlock(block);
                float value = block.isEmpty ? -1f : block.GetFloat(ArenaCardFade.FadeProperty);
                float material = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ArenaCardFade.FadeProperty)
                    ? renderer.sharedMaterial.GetFloat(ArenaCardFade.FadeProperty)
                    : -1f;

                if (covers || (value >= 0f && value < 0.999f))
                {
                    fading++;
                    log.AppendLine(
                        "card " + card.name + " zone=" + card.Zone + " pos=" + F(p)
                        + " size=" + F(card.transform.localScale)
                        + " covers=" + covers + " blockFade=" + value.ToString("0.00", CultureInfo.InvariantCulture)
                        + " materialFade=" + material.ToString("0.00", CultureInfo.InvariantCulture)
                        + " order=" + renderer.sortingOrder);
                }
            }

            log.AppendLine("cardsTotal=" + cards.Length + " matched=" + fading);
            log.AppendLine("fadingCount=" + (fade != null ? fade.FadingCount : -1));
            log.AppendLine("shaderHasFade=" + (Shader.Find("ProjectSurvive/ArenaCard") != null));
            Debug.Log("[ArenaFadeProbe]\n" + log);
            return log.ToString();
        }

        private static string F(Vector3 value) =>
            value.x.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + value.y.ToString("0.00", CultureInfo.InvariantCulture) + ","
            + value.z.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
