using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Walks every ghost one tick, straight at the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 07 deliberately ships <b>one</b> chase behaviour (the prompt's "ศัตรูเดินเข้าหา
    /// 1 ชนิด"): a ghost that is not <c>Stationary</c> walks at its own
    /// <c>move_speed</c> towards the player, and a stationary one holds position. The
    /// <c>MovePattern</c> column already distinguishes <c>Chase</c> from <c>Charge</c>, and
    /// giving <c>Charge</c> its own burst-and-rest rhythm is Stage 11's work — the data it
    /// would need (a charge window, a rest window) has no column yet, and a code constant
    /// would be exactly the balance number AGENTS rule 5 keeps out of code.
    /// </para>
    /// <para>
    /// Speed, body radius and the pattern all come from the ghost's own components, so this
    /// system never looks a def up on the hot path.
    /// </para>
    /// </remarks>
    public sealed class EnemySteerSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Velocity> _velocities = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsPool<Collider> _colliders = null!;
        private EcsFilter _filter = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            EcsWorld world = systems.GetWorld();
            _positions = world.GetPool<Position>();
            _velocities = world.GetPool<Velocity>();
            _healths = world.GetPool<Health>();
            _enemies = world.GetPool<EnemyTag>();
            _colliders = world.GetPool<Collider>();
            _filter = world.Filter<EnemyTag>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;
            int player = context.PlayerEntity;

            if (!context.IsAlive(player))
            {
                return;
            }

            ref Position playerPosition = ref _positions.Get(player);
            float targetX = playerPosition.X;
            float targetY = playerPosition.Y;

            foreach (int entity in _filter)
            {
                if (_healths.Get(entity).Hp <= 0)
                {
                    continue;
                }

                ref Position position = ref _positions.Get(entity);
                ref Velocity velocity = ref _velocities.Get(entity);
                EnemyTag tag = _enemies.Get(entity);

                if (tag.MovePattern == (int)MovePattern.Stationary)
                {
                    velocity.X = 0f;
                    velocity.Y = 0f;
                    continue;
                }

                float dx = targetX - position.X;
                float dy = targetY - position.Y;
                float squared = (dx * dx) + (dy * dy);

                if (squared > 1e-8f)
                {
                    float inverse = 1f / (float)Math.Sqrt(squared);
                    velocity.X = dx * inverse * tag.MoveSpeed;
                    velocity.Y = dy * inverse * tag.MoveSpeed;
                }
                else
                {
                    velocity.X = 0f;
                    velocity.Y = 0f;
                }

                position.X += velocity.X * BattleArena.TickDeltaSeconds;
                position.Y += velocity.Y * BattleArena.TickDeltaSeconds;

                BattleContext.ClampToArena(
                    ref position.X, ref position.Y, _colliders.Get(entity).Radius);
            }
        }
    }
}
