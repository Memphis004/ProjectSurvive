using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Opens each of the floor's spawn windows on its own tick and walks its ghosts in
    /// from the edge of the arena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The timeline was already resolved into <see cref="WaveSlot"/>s by
    /// <see cref="BattleContext"/>, so this system is a clock and a loop: a wave
    /// announces itself once (<c>WaveStarted</c>), then spawns its count on
    /// <c>first + n × interval</c> until the count or the window runs out.
    /// </para>
    /// <para>
    /// The spawn position comes from the <b>battle</b> random stream only, so a replay
    /// of the same seed places every ghost identically (AGENTS rule 6).
    /// </para>
    /// <para>
    /// <see cref="BattleArena.MaxEnemies"/> is a hard memory bound, not a balance rule:
    /// a wave that would cross it waits a tick and, if its window closes first, simply
    /// spawns fewer ghosts. Losing spawns at the ceiling is the honest failure — the
    /// alternative is growing an array on the hot path.
    /// </para>
    /// </remarks>
    public sealed class WaveSpawnSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;

            for (int wave = 0; wave < context.WaveCount; wave++)
            {
                ref WaveSlot slot = ref context.Waves[wave];

                if (!slot.Announced)
                {
                    if (context.Tick < slot.FirstSpawnTick)
                    {
                        continue;
                    }

                    slot.Announced = true;
                    context.WaveIndex = wave;
                    context.Emit(BattleEventType.WaveStarted, default, -1, wave);
                }

                while (slot.Spawned < slot.SpawnCount)
                {
                    long nextTick = slot.FirstSpawnTick + ((long)slot.Spawned * slot.IntervalTicks);
                    if (context.Tick < nextTick)
                    {
                        break;
                    }

                    if (slot.EndTick > 0 && context.Tick > slot.EndTick)
                    {
                        break;
                    }

                    if (context.EnemiesAlive >= BattleArena.MaxEnemies)
                    {
                        break;
                    }

                    Spawn(context, ref slot);
                }
            }
        }

        private static void Spawn(BattleContext context, ref WaveSlot slot)
        {
            float angle = context.Random.NextFloat() * BattleArena.Tau;
            float radius = BattleArena.Radius * BattleArena.SpawnRadiusFactor;
            float x = (float)System.Math.Cos(angle) * radius;
            float y = (float)System.Math.Sin(angle) * radius;

            int entity = context.Spawner.SpawnEnemy(context, slot, x, y);
            slot.Spawned++;
            context.EnemiesAlive++;

            if (slot.IsBoss)
            {
                context.BossEntity = entity;
                context.BossMaxHp = slot.MaxHp;
                context.BossHp = slot.MaxHp;
                context.BossSpawned = true;
            }

            context.Emit(
                BattleEventType.EnemySpawned,
                context.Ref(entity),
                slot.EnemyDefId,
                0,
                BattleEventFlags.None,
                x,
                y);
        }
    }
}
