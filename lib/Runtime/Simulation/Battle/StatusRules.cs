using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The data-driven half of an elemental status: how many ticks a row lasts, how often it
    /// applies its damage, and what one more application does to the stack count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before Stage 11a the effect re-used the applying skill's damage on a one-second cadence
    /// (ADR 0017's interim rule). The numbers now come from the <c>status</c> table and this
    /// type is the only place that turns a row into component state, so the system and the
    /// tests apply exactly the same rule.
    /// </para>
    /// <para>
    /// No allocation: a row is read and the component is written in place.
    /// </para>
    /// </remarks>
    public static class StatusRules
    {
        /// <summary>Seconds to whole ticks, never zero for a positive duration.</summary>
        public static int Ticks(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 0;
            }

            int ticks = (int)Math.Round(seconds * BattleConstants.TickRate);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// Applies one hit of <paramref name="config"/> to <paramref name="status"/>.
        /// </summary>
        /// <remarks>
        /// A second application of the same row adds one stack (up to <c>max_stacks</c>) and
        /// restarts the clock; a different row replaces the effect outright. Damage per
        /// application is the row's number times the stack count, so a two-stack burn ticks
        /// twice as hard without any extra component.
        /// </remarks>
        public static void Apply(ref Status status, StatusConfig config, int defId)
        {
            if (config is null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            int stacks = status.DefId == defId && status.Stacks > 0
                ? status.Stacks + 1
                : 1;

            int maxStacks = config.MaxStacks > 0 ? config.MaxStacks : 1;
            if (stacks > maxStacks)
            {
                stacks = maxStacks;
            }

            status.DefId = defId;
            status.Element = (int)config.Element;
            status.Stacks = stacks;
            status.RemainingTicks = Math.Max(1, Ticks(config.DurationSeconds));
            status.TickInterval = Math.Max(1, Ticks(config.TickIntervalSeconds));
            status.NextTickIn = status.TickInterval;
            status.DamagePerTick = config.DamagePerTick * stacks;
        }
    }
}
