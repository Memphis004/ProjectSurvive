using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Turns overlap into <b>queued</b> damage: a ghost touching the player, and a
    /// projectile touching a ghost.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This system decides nothing about damage — it detects contact and hands the hit to
    /// <see cref="DamageStatusSystem"/>, which is what makes "Collision then
    /// Damage/Element/Status" a real seam rather than one system with two names.
    /// </para>
    /// <para>
    /// Narrow phase is circle-vs-circle on the entities' own <see cref="Collider"/> radii,
    /// broad phase is one <see cref="BattleSpatialHash.QueryRadius"/> per projectile. The
    /// grid was rebuilt earlier in this same tick, so every entity it returns is alive:
    /// nothing is deleted between the hash build and here.
    /// </para>
    /// <para>
    /// A projectile spends one hit per ghost it touches (pierce), and disappears on the
    /// hit that spends its last one. It is collected and released after the enumeration
    /// rather than deleted inside it, so no enumeration ever sees a destroyed entity.
    /// </para>
    /// </remarks>
    public sealed class CollisionSystem : IEcsInitSystem, IEcsRunSystem
    {
        /// <summary>
        /// Broad-phase radius of a projectile query: the projectile's own body plus the
        /// widest body a ghost can have, so no overlap is missed by the grid.
        /// </summary>
        private const float QueryRadius =
            BattleArena.ProjectileRadius + (BattleArena.EnemyRadius * BattleArena.BossRadiusScale);

        private BattleContext _context = null!;
        private EcsWorld _world = null!;
        private EcsPool<Position> _positions = null!;
        private EcsPool<Health> _healths = null!;
        private EcsPool<Collider> _colliders = null!;
        private EcsPool<ContactAttack> _contacts = null!;
        private EcsPool<Projectile> _projectiles = null!;
        private EcsFilter _enemyFilter = null!;
        private EcsFilter _projectileFilter = null!;
        private int[] _candidates = null!;
        private int[] _spent = null!;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _world = systems.GetWorld();
            _positions = _world.GetPool<Position>();
            _healths = _world.GetPool<Health>();
            _colliders = _world.GetPool<Collider>();
            _contacts = _world.GetPool<ContactAttack>();
            _projectiles = _world.GetPool<Projectile>();
            _enemyFilter = _world.Filter<EnemyTag>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
            _projectileFilter = _world.Filter<Projectile>().End(BattleArena.FilterDenseCapacity);
            _candidates = new int[BattleArena.MaxEnemies];
            _spent = new int[BattleArena.MaxProjectiles];
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
            float playerX = playerPosition.X;
            float playerY = playerPosition.Y;
            float playerRadius = _colliders.Get(player).Radius;

            TouchDamage(context, playerX, playerY, playerRadius);
            ProjectileHits(context);
        }

        private void TouchDamage(BattleContext context, float playerX, float playerY, float playerRadius)
        {
            foreach (int entity in _enemyFilter)
            {
                if (_healths.Get(entity).Hp <= 0)
                {
                    continue;
                }

                ref ContactAttack contact = ref _contacts.Get(entity);
                if (contact.RemainingTicks > 0)
                {
                    contact.RemainingTicks--;
                }

                ref Position position = ref _positions.Get(entity);
                float dx = position.X - playerX;
                float dy = position.Y - playerY;
                float reach = _colliders.Get(entity).Radius + playerRadius;

                if (((dx * dx) + (dy * dy)) > reach * reach)
                {
                    continue;
                }

                if (contact.RemainingTicks > 0)
                {
                    continue;
                }

                // The attack is spent whether or not it lands: an invulnerable player is
                // still being attacked, so the ghost's cooldown must still restart.
                context.PlayerHitDamage += contact.Damage;
                contact.RemainingTicks = contact.PeriodTicks;
            }
        }

        private void ProjectileHits(BattleContext context)
        {
            BattleSpatialHash hash = context.Hash;
            int spentCount = 0;

            foreach (int projectile in _projectileFilter)
            {
                ref Position position = ref _positions.Get(projectile);
                ref Projectile shot = ref _projectiles.Get(projectile);
                float radius = _colliders.Get(projectile).Radius;

                int found = hash.QueryRadius(position.X, position.Y, radius + QueryRadius, _candidates);
                for (int i = 0; i < found; i++)
                {
                    int target = _candidates[i];
                    if (target == projectile || !context.IsAlive(target))
                    {
                        continue;
                    }

                    ref Health targetHealth = ref _healths.Get(target);
                    if (targetHealth.Hp <= 0)
                    {
                        continue;
                    }

                    ref Position targetPosition = ref _positions.Get(target);
                    float dx = targetPosition.X - position.X;
                    float dy = targetPosition.Y - position.Y;
                    float reach = radius + _colliders.Get(target).Radius;

                    if (((dx * dx) + (dy * dy)) > reach * reach)
                    {
                        continue;
                    }

                    context.AddHit(target, shot.Damage, shot.Element, shot.DefId);
                    shot.HitsLeft--;

                    if (shot.HitsLeft <= 0)
                    {
                        if (spentCount < _spent.Length)
                        {
                            _spent[spentCount++] = projectile;
                        }

                        break;
                    }
                }
            }

            for (int i = 0; i < spentCount; i++)
            {
                _world.DelEntity(_spent[i]);
            }

            context.ProjectilesAlive = _projectileFilter.GetEntitiesCount();
        }
    }
}
