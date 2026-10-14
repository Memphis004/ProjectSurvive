using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Flies every projectile one tick and removes the ones whose clock has run out or
    /// which have left the arena.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A projectile's life is its <see cref="Lifetime"/>, which <see cref="AutoCastSystem"/>
    /// derived from the skill's own <c>range</c> and <c>projectile_speed</c> — so "how far
    /// a shot reaches" is a data question, and nothing here invents a distance.
    /// </para>
    /// <para>
    /// Expired projectiles are collected first and destroyed after the loop. EcsLite allows
    /// a delete inside an enumeration (it defers the filter update), but the enumerator can
    /// then hand out an entity that is already gone, and every pool access on it throws in
    /// DEBUG. Collecting first removes the whole class of problem for one small array.
    /// </para>
    /// </remarks>
    public sealed class ProjectileMoveSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsWorld _world = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Velocity> _velocities = null!;
        private EcsPool<Lifetime> _lifetimes = null!;
        private EcsFilter _filter = null!;
        private int[] _expired = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _world = systems.GetWorld();
            _positions = _world.GetPool<Position>();
            _velocities = _world.GetPool<Velocity>();
            _lifetimes = _world.GetPool<Lifetime>();
            _filter = _world.Filter<Lifetime>().Inc<Velocity>().End(BattleArena.FilterDenseCapacity);
            _expired = new int[BattleArena.MaxProjectiles];
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            int expiredCount = 0;
            float bounds = BattleArena.Radius * BattleArena.Radius;

            foreach (int entity in _filter)
            {
                ref Position position = ref _positions.Get(entity);
                ref Velocity velocity = ref _velocities.Get(entity);
                ref Lifetime lifetime = ref _lifetimes.Get(entity);

                position.X += velocity.X * BattleArena.TickDeltaSeconds;
                position.Y += velocity.Y * BattleArena.TickDeltaSeconds;
                lifetime.RemainingTicks--;

                if (lifetime.RemainingTicks <= 0
                    || ((position.X * position.X) + (position.Y * position.Y)) > bounds)
                {
                    if (expiredCount < _expired.Length)
                    {
                        _expired[expiredCount++] = entity;
                    }
                }
            }

            for (int i = 0; i < expiredCount; i++)
            {
                _world.DelEntity(_expired[i]);
            }

            // The filter count is the truth: it already excludes what was just released,
            // and nothing else in this tick creates a projectile before the cast phase.
            _context.ProjectilesAlive = _filter.GetEntitiesCount();
        }
    }
}
