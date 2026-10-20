using System;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The boss block of an enemy row, resolved to ticks once at spawn (Stage 11c).
    /// </summary>
    /// <remarks>
    /// The same split <see cref="EnemyAttack"/> uses: seconds live in the table, ticks live
    /// on the component, so <see cref="BossSystem"/> never converts a unit on the hot path.
    /// A zero block means the row is a boss that does not eat corpses or possess, which is
    /// legal — a boss with neither is still a boss.
    /// </remarks>
    public struct BossBehaviour
    {
        /// <summary>Absolute HP at or below which phase 2 begins; 0 when the row sets no threshold.</summary>
        public int Phase2HpThreshold;

        /// <summary>How close a dying ghost must fall to be eaten, in world units.</summary>
        public float CorpseRange;

        /// <summary>HP one corpse gives back.</summary>
        public int CorpseHeal;

        /// <summary>Warning ticks before a possession lands.</summary>
        public int PossessTelegraphTicks;

        /// <summary>Ticks the player's movement input stays reversed.</summary>
        public int PossessTicks;

        /// <summary>Shortest gap between two possessions, in ticks.</summary>
        public int PossessIntervalTicks;

        /// <summary>How close the player must be for a possession to start.</summary>
        public float PossessRange;
    }

    /// <summary>
    /// Turns the enemy row's boss columns into the numbers the boss runs on (Stage 11c).
    /// </summary>
    /// <remarks>
    /// The phase threshold is resolved to an absolute HP here rather than compared as a
    /// percentage every tick: a percentage comparison on the hot path would divide, and the
    /// absolute form is the same fact stated once. It is rounded once, at spawn, so two
    /// runs of one seed cross the threshold on the same tick.
    /// </remarks>
    public static class BossRules
    {
        /// <summary>Resolves a row's boss block.</summary>
        /// <param name="enemy">The enemy row.</param>
        /// <returns>The resolved behaviour; an all-zero block for a row that is not a boss.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="enemy"/> is null.</exception>
        public static BossBehaviour FromConfig(EnemyConfig enemy)
        {
            if (enemy is null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            if (!enemy.IsBoss)
            {
                return default;
            }

            return new BossBehaviour
            {
                Phase2HpThreshold = Threshold(enemy),
                CorpseRange = enemy.BossCorpseRange > 0f ? enemy.BossCorpseRange : 0f,
                CorpseHeal = enemy.BossCorpseHeal > 0 ? enemy.BossCorpseHeal : 0,
                PossessTelegraphTicks = EnemyAttackRules.Ticks(enemy.BossPossessTelegraphSeconds),
                PossessTicks = EnemyAttackRules.Ticks(enemy.BossPossessSeconds),
                PossessIntervalTicks = EnemyAttackRules.Ticks(enemy.BossPossessIntervalSeconds),
                PossessRange = enemy.BossPossessRange > 0f ? enemy.BossPossessRange : 0f,
            };
        }

        /// <summary>True when the player's movement input must be reversed this tick.</summary>
        /// <param name="possessed">The player's possession clock.</param>
        /// <returns>True while the clock has ticks left.</returns>
        public static bool IsFlipped(in Possessed possessed) => possessed.RemainingTicks > 0;

        /// <summary>
        /// HP a corpse of <paramref name="heal"/> restores, capped at the boss's max.
        /// </summary>
        /// <param name="hp">The boss's HP before the meal.</param>
        /// <param name="maxHp">The boss's max HP.</param>
        /// <param name="heal">What the row says one corpse is worth.</param>
        /// <returns>How much HP was actually regained; 0 at full health.</returns>
        public static int HealFromCorpse(int hp, int maxHp, int heal)
        {
            if (heal <= 0 || hp <= 0 || hp >= maxHp)
            {
                return 0;
            }

            int healed = maxHp - hp;
            return healed < heal ? healed : heal;
        }

        /// <summary>True when a body at <paramref name="x"/>,<paramref name="y"/> is within reach.</summary>
        /// <param name="centerX">Boss X.</param>
        /// <param name="centerY">Boss Y.</param>
        /// <param name="range">The reach, in world units.</param>
        /// <param name="x">Body X.</param>
        /// <param name="y">Body Y.</param>
        /// <param name="bodyRadius">Radius of the body being tested.</param>
        /// <returns>True when the body is inside the circle.</returns>
        public static bool WithinReach(
            float centerX, float centerY, float range, float x, float y, float bodyRadius)
        {
            if (range <= 0f)
            {
                return false;
            }

            float dx = x - centerX;
            float dy = y - centerY;
            float reach = range + bodyRadius;
            return ((dx * dx) + (dy * dy)) <= reach * reach;
        }

        private static int Threshold(EnemyConfig enemy)
        {
            float percent = enemy.BossPhase2HpPercent;
            if (!(percent > 0f) || enemy.MaxHp <= 0)
            {
                return 0;
            }

            int threshold = (int)Math.Round(enemy.MaxHp * (percent / 100f));
            return threshold < 1 ? 1 : threshold;
        }
    }
}
