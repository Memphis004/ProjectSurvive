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
                    End(context, BattlePhase.Won, BattleEndReason.BossDefeated);
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
                End(context, BattlePhase.Won, BattleEndReason.SurvivedAllWaves);
            }
        }

        private static void End(BattleContext context, BattlePhase phase, BattleEndReason reason)
        {
            context.EndRequested = true;
            context.EndPhase = phase;
            context.EndReason = reason;
        }
    }
}
