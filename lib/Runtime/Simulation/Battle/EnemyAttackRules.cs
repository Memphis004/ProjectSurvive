using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The rules a telegraphed ghost attack is built from and resolved with (Stage 11b).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One place converts the enemy row's seconds into ticks and decides whether the
    /// player stands in a warned footprint. Keeping the geometry here — pure floats, no
    /// ECS, no UnityEngine — is what lets the Core tests assert "a telegraph that ends
    /// inside the circle hurts, one that ends outside it does not" without running a
    /// tick.
    /// </para>
    /// <para>
    /// The footprint rule is the one the telegraph event publishes: <c>length == 0</c> is
    /// a circle of <c>radius</c> around the origin, and a positive <c>length</c> is a line
    /// from the origin along the aim, <c>radius</c> to either side. Drawing the event and
    /// applying the damage therefore read the same two numbers.
    /// </para>
    /// </remarks>
    public static class EnemyAttackRules
    {
        /// <summary>Converts a row's attack columns into the component the tick reads.</summary>
        /// <param name="enemy">The enemy row.</param>
        /// <returns>The resolved attack.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="enemy"/> is null.</exception>
        public static EnemyAttack FromConfig(EnemyConfig enemy)
        {
            if (enemy is null)
            {
                throw new ArgumentNullException(nameof(enemy));
            }

            return new EnemyAttack
            {
                Pattern = (int)enemy.AttackPattern,
                Range = enemy.AttackRange > 0f ? enemy.AttackRange : 0f,
                TelegraphTicks = Ticks(enemy.AttackTelegraphSeconds),
                StrikeTicks = Ticks(enemy.AttackSeconds),
                Damage = enemy.AttackDamage > 0 ? enemy.AttackDamage : 0,
                Radius = enemy.AttackRadius > 0f ? enemy.AttackRadius : 0f,
                Length = enemy.AttackLength > 0f ? enemy.AttackLength : 0f,
                FollowUpTelegraphTicks = Ticks(enemy.FollowUpTelegraphSeconds),
                FollowUpTicks = Ticks(enemy.FollowUpSeconds),
                FollowUpDamage = enemy.FollowUpDamage > 0 ? enemy.FollowUpDamage : 0,
                FollowUpRadius = enemy.FollowUpRadius > 0f ? enemy.FollowUpRadius : 0f,
                FollowUpLength = enemy.FollowUpLength > 0f ? enemy.FollowUpLength : 0f,
                FollowUpTickTicks = Ticks(enemy.FollowUpTickSeconds),
                RetreatTicks = Ticks(enemy.RetreatSeconds),
                RetreatSpeed = enemy.RetreatSpeed > 0f ? enemy.RetreatSpeed : 0f,
                CooldownTicks = Ticks(enemy.AttackCooldownSeconds),
            };
        }

        /// <summary>Seconds to whole ticks, rounding as the rest of the simulation does.</summary>
        /// <param name="seconds">A duration in seconds; 0 or less means "this stage does not happen".</param>
        /// <returns>The tick count, 0 for a non-positive duration.</returns>
        public static int Ticks(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 0;
            }

            int ticks = (int)Math.Round(seconds * BattleConstants.TickRate);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>True when a disc of <paramref name="radius"/> at the centre reaches a body.</summary>
        /// <param name="centerX">Centre X.</param>
        /// <param name="centerY">Centre Y.</param>
        /// <param name="radius">Telegraph radius.</param>
        /// <param name="x">Body X.</param>
        /// <param name="y">Body Y.</param>
        /// <param name="bodyRadius">Body radius.</param>
        /// <returns>True when the body is inside the footprint.</returns>
        public static bool CircleHits(
            float centerX, float centerY, float radius, float x, float y, float bodyRadius)
        {
            float dx = x - centerX;
            float dy = y - centerY;
            float reach = radius + bodyRadius;
            return ((dx * dx) + (dy * dy)) <= reach * reach;
        }

        /// <summary>True when a line of <paramref name="length"/> and <paramref name="halfWidth"/> reaches a body.</summary>
        /// <param name="originX">Line start X.</param>
        /// <param name="originY">Line start Y.</param>
        /// <param name="directionX">Unit direction X.</param>
        /// <param name="directionY">Unit direction Y.</param>
        /// <param name="length">Line length.</param>
        /// <param name="halfWidth">Half-width of the line.</param>
        /// <param name="x">Body X.</param>
        /// <param name="y">Body Y.</param>
        /// <param name="bodyRadius">Body radius.</param>
        /// <returns>True when the body is inside the footprint.</returns>
        public static bool LineHits(
            float originX, float originY,
            float directionX, float directionY,
            float length, float halfWidth,
            float x, float y, float bodyRadius)
        {
            float dx = x - originX;
            float dy = y - originY;

            float along = (dx * directionX) + (dy * directionY);
            if (along < 0f)
            {
                along = 0f;
            }
            else if (along > length)
            {
                along = length;
            }

            float closestX = originX + (directionX * along);
            float closestY = originY + (directionY * along);
            float offsetX = x - closestX;
            float offsetY = y - closestY;
            float reach = halfWidth + bodyRadius;

            return ((offsetX * offsetX) + (offsetY * offsetY)) <= reach * reach;
        }
    }
}
