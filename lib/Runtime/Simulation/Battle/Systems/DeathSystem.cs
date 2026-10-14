using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Removes the ghosts at zero HP: counts the kill once, announces it, queues the drop
    /// and releases the entity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Exactly once.</b> The kill counter and the <c>EnemyKilled</c> event happen in
    /// this one place, and the entity is gone before the next tick, so no path can pay a
    /// ghost out twice — "ศัตรูตายให้รางวัลครั้งเดียว".
    /// </para>
    /// <para>
    /// The player is <b>not</b> destroyed here. Being at zero HP ends the floor
    /// (<see cref="FloorEndSystem"/>), and the run keeps the last HP it saw
    /// (<c>BattleResult.FinalHp</c>); a destroyed player entity would have to be re-created
    /// for the render snapshot that describes the losing tick.
    /// </para>
    /// <para>
    /// <c>DevKillAll</c> arrives as <see cref="BattleContext.KillAllRequested"/>: the system
    /// zeroes every ghost's HP and then lets the ordinary rule above run, so a dev kill and
    /// a fought kill produce the same events and the same drops.
    /// </para>
    /// </remarks>
    public sealed class DeathSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private EcsWorld _world = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<EnemyTag> _enemies = null!;
        private EcsFilter _filter = null!;
        private int[] _dead = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _world = systems.GetWorld();
            _positions = _world.GetPool<Position>();
            _healths = _world.GetPool<Health>();
            _enemies = _world.GetPool<EnemyTag>();
            _filter = _world.Filter<EnemyTag>().Inc<Health>().End(BattleArena.FilterDenseCapacity);
            _dead = new int[BattleArena.MaxEnemies];
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;

            if (context.KillAllRequested)
            {
                context.KillAllRequested = false;
                foreach (int entity in _filter)
                {
                    ref Health health = ref _healths.Get(entity);
                    if (health.Hp > 0)
                    {
                        health.Hp = 0;
                    }
                }
            }

            int deadCount = 0;

            foreach (int entity in _filter)
            {
                if (_healths.Get(entity).Hp > 0)
                {
                    continue;
                }

                EnemyTag tag = _enemies.Get(entity);
                ref Position position = ref _positions.Get(entity);
                float x = position.X;
                float y = position.Y;

                context.Kills++;
                context.Emit(
                    BattleEventType.EnemyKilled,
                    context.Ref(entity),
                    tag.DefId,
                    context.Kills,
                    Flags(tag),
                    x,
                    y);
                context.AddDrop(x, y, tag.DefId);

                if (tag.IsBoss)
                {
                    context.BossHp = 0;
                    context.BossEntity = -1;
                }

                if (deadCount < _dead.Length)
                {
                    _dead[deadCount++] = entity;
                }
            }

            for (int i = 0; i < deadCount; i++)
            {
                _world.DelEntity(_dead[i]);
            }

            context.EnemiesAlive -= deadCount;
            if (context.EnemiesAlive < 0)
            {
                context.EnemiesAlive = 0;
            }
        }

        private static BattleEventFlags Flags(in EnemyTag tag)
        {
            if (tag.IsBoss)
            {
                return BattleEventFlags.Boss;
            }

            return tag.IsElite ? BattleEventFlags.Elite : BattleEventFlags.None;
        }
    }
}
