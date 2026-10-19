using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Decides whether the floor is over, and why. The only place a win or a loss is
    /// chosen — every other system reports facts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules are the contract's, and every number in them is data:
    /// </para>
    /// <list type="bullet">
    /// <item><description>the player's HP reached zero → <see cref="BattleEndReason.PlayerDied"/>, on any floor;</description></item>
    /// <item><description>a boss floor (<c>floor.is_boss</c>): the boss died →
    /// <see cref="BattleEndReason.BossDefeated"/>; the clock ran out first →
    /// <see cref="BattleEndReason.Timeout"/>;</description></item>
    /// <item><description>a normal floor: the clock reached <c>floor.duration_seconds</c> →
    /// <see cref="BattleEndReason.SurvivedAllWaves"/>.</description></item>
    /// </list>
    /// <para>
    /// "The boss died" requires that a boss <b>spawned</b> on this floor. A boss floor whose
    /// waves contain no boss would otherwise win on tick 1 with a boss HP of zero; it times
    /// out instead, which is what the data says happened.
    /// </para>
    /// <para>
    /// <b>A win is not the end of the tick.</b> Since Stage 07c a win opens the end-of-floor
    /// <i>sweep</i> instead of ending straight away: the drops still on the ground drift in and
    /// pay, and <see cref="BattleContext.SweepActive"/> keeps the phase <c>Running</c> until
    /// they are gone or the cap is reached. A loss (the player dying, or a boss floor timing
    /// out) ends immediately — there is nothing to collect on a floor that was not cleared.
    /// </para>
    /// <para>
    /// The two timer warnings are emitted once each, here, because this is the only system
    /// that already knows how much time is left.
    /// </para>
    /// </remarks>
    public sealed class FloorEndSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Health> _healths = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _healths = systems.GetWorld().GetPool<Health>();
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;

            if (context.EndRequested)
            {
                return;
            }

            int player = context.PlayerEntity;
            if (context.IsAlive(player) && _healths.Get(player).Hp <= 0)
            {
                End(context, BattlePhase.Lost, BattleEndReason.PlayerDied);
                return;
            }

            int durationTicks = context.FloorDurationTicks;
            if (durationTicks > 0)
            {
                long remaining = durationTicks - context.Tick;
                int rate = BattleConstants.TickRate;

                if (!context.WarnedSeconds15 && remaining <= 15L * rate)
                {
                    context.WarnedSeconds15 = true;
                    context.Emit(BattleEventType.TimeWarning, default, -1, 15);
                }

                if (!context.WarnedSeconds5 && remaining <= 5L * rate)
                {
                    context.WarnedSeconds5 = true;
                    context.Emit(BattleEventType.TimeWarning, default, -1, 5);
                }
            }

            if (context.Floor.IsBoss)
            {
                if (context.BossSpawned && context.BossHp <= 0)
                {
                    BeginSweep(context, BattleEndReason.BossDefeated);
                    return;
                }

                if (durationTicks > 0 && context.Tick >= durationTicks)
                {
                    End(context, BattlePhase.Lost, BattleEndReason.Timeout);
                }

                return;
            }

            if (durationTicks > 0 && context.Tick >= durationTicks)
            {
                BeginSweep(context, BattleEndReason.SurvivedAllWaves);
            }
        }

        /// <summary>
        /// Opens the end-of-floor sweep: the floor is decided (won, with this reason) but the
        /// result waits for <see cref="Systems.SweepSystem"/>.
        /// </summary>
        private static void BeginSweep(BattleContext context, BattleEndReason reason)
        {
            context.SweepActive = true;
            context.SweepTicksRemaining = context.SweepTimeoutTicks;
            context.EndPhase = BattlePhase.Won;
            context.EndReason = reason;
        }

        private static void End(BattleContext context, BattlePhase phase, BattleEndReason reason)
        {
            context.EndRequested = true;
            context.EndPhase = phase;
            context.EndReason = reason;
        }
    }
}
