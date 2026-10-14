using System;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Simulation.Battle.Systems
{
    /// <summary>
    /// Keeps a chosen number of ghosts and projectiles alive so the Stage 07 benchmark
    /// can measure a steady tick cost (~300 ศัตรู + 500 กระสุน) instead of a spike that
    /// decays as the load is killed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inert in the shipped game: it does nothing unless
    /// <see cref="EcsBattleOptions.LoadTestEnemies"/> or
    /// <see cref="EcsBattleOptions.LoadTestProjectiles"/> is above zero, and the real
    /// factory never sets either. The entities it creates go through the same
    /// <see cref="BattleSpawner"/> and the same components as a wave spawn, so what is
    /// measured is the real pipeline — steering, the spatial hash, collision, damage,
    /// death, the frame — carrying a large load.
    /// </para>
    /// <para>
    /// It deliberately never spawns a boss: a boss changes the floor's win condition, and
    /// a benchmark must not be able to end its own floor early.
    /// </para>
    /// <para>
    /// <see cref="EcsBattleOptions.LoadTestEntityLifetimeTicks"/> turns the steady state
    /// into churn: the batch this system raised is retired through the normal death path
    /// (HP to zero, and the death system does the rest), which is what a test needing a
    /// battle that generates many events asks for.
    /// </para>
    /// </remarks>
    public sealed class LoadTestSystem : IEcsInitSystem, IEcsRunSystem
    {
        private BattleContext _context = null!;
        private WaveSlot _enemyTemplate;
        private int _projectileDefId;
        private int _projectileDamage;
        private float _projectileSpeed = 12f;

        /// <summary>The load ghosts this system raised, so a churn tick can retire them.</summary>
        /// <remarks>
        /// Preallocated, and only written while the load option is on. An entity that died
        /// on its own is skipped when it is retired (its slot already belongs to the world
        /// again); the list only has to be long enough for one batch.
        /// </remarks>
        private int[] _spawnedEnemies = null!;
        private int _spawnedEnemyCount;

        /// <inheritdoc />
        public void Init(IEcsSystems systems)
        {
            _context = systems.GetShared<BattleContext>();
            _spawnedEnemies = new int[BattleArena.MaxEnemies];

            IGameConfig config = _context.Config;
            string enemyId = string.IsNullOrEmpty(_context.Options.LoadTestEnemyId)
                ? BattleDefIds.FirstEnemyId(config)
                : _context.Options.LoadTestEnemyId;

            if (!string.IsNullOrEmpty(enemyId) && config.TryGetEnemy(enemyId, out EnemyConfig enemy))
            {
                _enemyTemplate = new WaveSlot
                {
                    SpawnCount = 1,
                    IntervalTicks = 1,
                    EnemyDefId = _context.DefIds.Enemy(enemyId),
                    MaxHp = enemy.MaxHp,
                    MoveSpeed = enemy.MoveSpeed,
                    ContactDamage = enemy.ContactDamage,
                    ContactPeriodTicks = Math.Max(1, (int)Math.Round(enemy.AttackCooldownSeconds * BattleConstants.TickRate)),
                    MovePattern = (int)enemy.MovePattern,
                    IsBoss = false,
                    IsElite = false,
                };
            }

            string projectileSkillId = string.IsNullOrEmpty(_context.Options.LoadTestProjectileSkillId)
                ? BattleDefIds.FirstProjectileSkillId(config)
                : _context.Options.LoadTestProjectileSkillId;

            if (!string.IsNullOrEmpty(projectileSkillId) && config.TryGetSkill(projectileSkillId, out SkillConfig skill))
            {
                _projectileDefId = _context.DefIds.Skill(projectileSkillId);
                _projectileDamage = skill.Damage > 0 ? skill.Damage : 1;
                _projectileSpeed = skill.ProjectileSpeed > 0f ? skill.ProjectileSpeed : _projectileSpeed;
            }
            else
            {
                // No projectile row to borrow from: the load still has to move and hit.
                _projectileDamage = 1;
            }
        }

        /// <inheritdoc />
        public void Run(IEcsSystems systems)
        {
            BattleContext context = _context;
            EcsBattleOptions options = context.Options;

            if (!options.MaintainLoadTestEntities)
            {
                return;
            }

            if (options.LoadTestEnemies > 0 && _enemyTemplate.MaxHp > 0)
            {
                int wanted = options.LoadTestEnemies;
                if (wanted > BattleArena.MaxEnemies)
                {
                    wanted = BattleArena.MaxEnemies;
                }

                RetireLoadEnemies(context);

                while (context.EnemiesAlive < wanted)
                {
                    float angle = context.Random.NextFloat() * BattleArena.Tau;
                    float radius = BattleArena.Radius * BattleArena.SpawnRadiusFactor;
                    float x = (float)Math.Cos(angle) * radius;
                    float y = (float)Math.Sin(angle) * radius;

                    int entity = context.Spawner.SpawnEnemy(context, _enemyTemplate, x, y);
                    context.EnemiesAlive++;

                    if (_spawnedEnemyCount < _spawnedEnemies.Length)
                    {
                        _spawnedEnemies[_spawnedEnemyCount++] = entity;
                    }

                    context.Emit(
                        BattleEventType.EnemySpawned,
                        context.Ref(entity),
                        _enemyTemplate.EnemyDefId,
                        0,
                        BattleEventFlags.None,
                        x,
                        y);
                }
            }

            if (options.LoadTestProjectiles > 0 && _projectileDamage > 0)
            {
                int wanted = options.LoadTestProjectiles;
                if (wanted > BattleArena.MaxProjectiles)
                {
                    wanted = BattleArena.MaxProjectiles;
                }

                while (context.ProjectilesAlive < wanted)
                {
                    float angle = context.Random.NextFloat() * BattleArena.Tau;
                    float offsetAngle = context.Random.NextFloat() * BattleArena.Tau;
                    float offsetRadius = context.Random.NextFloat() * (BattleArena.Radius * 0.5f);

                    context.Spawner.SpawnProjectile(
                        _projectileDefId,
                        _projectileDamage,
                        0,
                        1,
                        _projectileSpeed,
                        (float)Math.Cos(offsetAngle) * offsetRadius,
                        (float)Math.Sin(offsetAngle) * offsetRadius,
                        (float)Math.Cos(angle),
                        (float)Math.Sin(angle),
                        BattleConstants.TickRate);
                    context.ProjectilesAlive++;
                }
            }
        }

        /// <summary>
        /// Retires the batch on a churn tick by zeroing its HP: the death system, later in
        /// the same tick, is what actually kills, rewards and releases them — the same path
        /// a wave ghost takes, so the churn exercises the real pipeline.
        /// </summary>
        private void RetireLoadEnemies(BattleContext context)
        {
            int lifetime = context.Options.LoadTestEntityLifetimeTicks;
            if (lifetime <= 0 || context.Tick <= 0 || context.Tick % lifetime != 0)
            {
                return;
            }

            EcsPool<Health> healths = context.World.GetPool<Health>();
            for (int i = 0; i < _spawnedEnemyCount; i++)
            {
                int entity = _spawnedEnemies[i];
                if (context.IsAlive(entity) && healths.Has(entity))
                {
                    healths.Get(entity).Hp = 0;
                }
            }

            _spawnedEnemyCount = 0;
        }
    }
}
