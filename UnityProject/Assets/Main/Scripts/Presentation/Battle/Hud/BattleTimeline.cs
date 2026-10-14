using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>What a timeline marker announces.</summary>
    public enum BattleTimelineMarkerKind
    {
        /// <summary>A normal spawn window.</summary>
        Normal = 0,

        /// <summary>An elite spawn window.</summary>
        Elite = 1,

        /// <summary>The boss.</summary>
        Boss = 2,
    }

    /// <summary>One spawn window on the floor's timeline bar, in normalized floor time.</summary>
    public readonly struct BattleTimelineMarker
    {
        /// <summary>Creates a marker.</summary>
        public BattleTimelineMarker(float start01, float end01, BattleTimelineMarkerKind kind)
        {
            Start01 = start01;
            End01 = end01;
            Kind = kind;
        }

        /// <summary>Window start as a fraction of the floor's duration.</summary>
        public float Start01 { get; }

        /// <summary>Window end as a fraction of the floor's duration.</summary>
        public float End01 { get; }

        /// <summary>What the window announces.</summary>
        public BattleTimelineMarkerKind Kind { get; }
    }

    /// <summary>
    /// Builds the timeline markers from the floor's authored wave windows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A lookup, not a rule: the markers are the <c>wave</c> rows of the floor that is
    /// already running, placed on a bar by <c>start / duration</c>. Nothing here decides
    /// when a ghost spawns — the simulation does that from the same rows — so the bar cannot
    /// disagree with the floor it describes by more than the table's own values, and
    /// Presentation never computes a spawn count, a damage or a price (AGENTS rule 10).
    /// </para>
    /// <para>
    /// Both rows count: a wave is an elite window when the wave says so or when the enemy it
    /// spawns is elite, and it is the boss window when the enemy is the floor's boss. That
    /// covers the shipped data and stays correct if a later stage marks the elite on either
    /// side.
    /// </para>
    /// </remarks>
    public static class BattleTimelineModel
    {
        /// <summary>
        /// The markers for one floor, in wave-table order. Empty when the floor has no waves
        /// or no duration.
        /// </summary>
        public static BattleTimelineMarker[] Build(IGameConfig config, FloorConfig floor)
        {
            if (config is null || floor is null || floor.DurationSeconds <= 0f)
            {
                return System.Array.Empty<BattleTimelineMarker>();
            }

            var markers = new List<BattleTimelineMarker>(config.Waves.Count);
            float duration = floor.DurationSeconds;

            for (int i = 0; i < config.Waves.Count; i++)
            {
                WaveConfig wave = config.Waves[i];
                if (!string.Equals(wave.FloorId, floor.Id, System.StringComparison.Ordinal))
                {
                    continue;
                }

                bool boss = config.TryGetEnemy(wave.EnemyId, out EnemyConfig enemy)
                    && (enemy.IsBoss || string.Equals(floor.BossEnemyId, wave.EnemyId, System.StringComparison.Ordinal));

                BattleTimelineMarkerKind kind = boss
                    ? BattleTimelineMarkerKind.Boss
                    : wave.IsElite || (enemy is not null && enemy.IsElite)
                        ? BattleTimelineMarkerKind.Elite
                        : BattleTimelineMarkerKind.Normal;

                markers.Add(new BattleTimelineMarker(
                    Clamp01(wave.StartTimeSeconds / duration),
                    Clamp01(wave.EndTimeSeconds / duration),
                    kind));
            }

            return markers.ToArray();
        }

        private static float Clamp01(float value) =>
            value < 0f ? 0f : (value > 1f ? 1f : value);
    }
}
