using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// Creates the battle's entities, and is the only place a component set is
    /// assembled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two reasons this is one type rather than a method in each system: a spawn is the
    /// one place where a half-built entity could exist (the world throws on an entity
    /// with no components in DEBUG, and a system reading a missing component throws in
    /// DEBUG), and the wave system and the load benchmark must create <i>identical</i>
    /// ghosts — a benchmark that spawns its own flavour of enemy measures a different
    /// battle than the one that ships.
    /// </para>
    /// <para>
    /// Every pool is resolved once, in the constructor, so a spawn during a tick is
    /// array writes and a dictionary-free path.
    /// </para>
    /// </remarks>
    public sealed class BattleSpawner
    {
        private readonly EcsWorld _world;
        private readonly EcsPool<Position> _positions;
        private readonly EcsPool<Velocity> _velocities;
        private readonly EcsPool<Health> _healths;
        private readonly EcsPool<Faction> _factions;
        private readonly EcsPool<EnemyTag> _enemyTags;
        private readonly EcsPool<Projectile> _projectiles;
        private readonly EcsPool<ContactAttack> _contacts;
        private readonly EcsPool<Collider> _colliders;
        private readonly EcsPool<Lifetime> _lifetimes;
        private readonly EcsPool<Pickup> _pickups;
        private readonly EcsPool<EnemyAttack> _attacks;
        private readonly EcsPool<EnemyBrain> _brains;

        /// <summary>Resolves every pool the spawner writes.</summary>
        /// <exception cref="System.ArgumentNullException"><paramref name="world"/> is null.</exception>
        public BattleSpawner(EcsWorld world)
        {
            _world = world ?? throw new System.ArgumentNullException(nameof(world));
            _positions = world.GetPool<Position>();
            _velocities = world.GetPool<Velocity>();
            _healths = world.GetPool<Health>();
            _factions = world.GetPool<Faction>();
            _enemyTags = world.GetPool<EnemyTag>();
            _projectiles = world.GetPool<Projectile>();
            _contacts = world.GetPool<ContactAttack>();
            _colliders = world.GetPool<Collider>();
            _lifetimes = world.GetPool<Lifetime>();
            _pickups = world.GetPool<Pickup>();
            _attacks = world.GetPool<EnemyAttack>();
            _brains = world.GetPool<EnemyBrain>();

            // Also register the pools only the systems read, so the world's pool table and
            // every dense array exist before the first tick. A pool created lazily on the
            // first tick still allocates, and the zero-allocation window starts at tick 0.
            world.GetPool<PlayerTag>();
            world.GetPool<Mana>();
            world.GetPool<Caster>();
            world.GetPool<Status>();
            world.GetPool<EnemyAttack>();
            world.GetPool<EnemyBrain>();
        }

        /// <summary>Creates the player: position, health, mana, body and the player faction.</summary>
        /// <param name="context">The session's shared state.</param>
        /// <returns>The new entity.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="context"/> is null.</exception>
        public int SpawnPlayer(BattleContext context)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            int entity = _world.NewEntity();

            ref Position position = ref _positions.Add(entity);
            position.X = 0f;
            position.Y = 0f;

            ref Velocity velocity = ref _velocities.Add(entity);
            velocity.X = 0f;
            velocity.Y = 0f;

            ref Health health = ref _healths.Add(entity);
            health.Hp = context.StartingHp;
            health.MaxHp = context.MaxHp;

            ref Mana mana = ref _world.GetPool<Mana>().Add(entity);
            mana.Mp = context.MaxMp;
            mana.MaxMp = context.MaxMp;
            mana.Carry = 0f;

            ref Faction faction = ref _factions.Add(entity);
            faction.Value = Faction.Player;

            ref Collider collider = ref _colliders.Add(entity);
            collider.Radius = BattleArena.PlayerRadius;

            _world.GetPool<PlayerTag>().Add(entity);

            return entity;
        }

        /// <summary>
        /// Creates one caster: a weapon's casting clock. It carries no position — a caster
        /// is bookkeeping for the player, and the aura or projectile it produces starts at
        /// the player's own position.
        /// </summary>
        /// <param name="context">The session's shared state.</param>
        /// <param name="index">The loadout entry it drives.</param>
        /// <param name="instanceId">Board instance id of the piece the entry came from, for the frame's cooldown list.</param>
        /// <param name="skillDefId">Skill def id, for events.</param>
        /// <param name="periodTicks">Cooldown length in ticks.</param>
        /// <param name="mpCost">MP one cast costs.</param>
        /// <param name="damage">Damage of one application.</param>
        /// <param name="element">Element of the cast, <c>0</c> when none.</param>
        /// <param name="behavior">Cast behaviour, see <c>SkillBehavior</c>.</param>
        /// <param name="range">Reach in world units.</param>
        /// <param name="projectileSpeed">Projectile speed, 0 for a non-projectile cast.</param>
        /// <param name="pierceCount">Extra ghosts a projectile passes through.</param>
        /// <param name="projectileCount">Projectiles per cast.</param>
        /// <returns>The new entity.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="context"/> is null.</exception>
        public int SpawnCaster(
            BattleContext context,
            int index,
            int instanceId,
            int skillDefId,
            int periodTicks,
            int mpCost,
            int damage,
            int element,
            int behavior,
            float range,
            float projectileSpeed,
            int pierceCount,
            int projectileCount)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            int entity = _world.NewEntity();

            ref Caster caster = ref _world.GetPool<Caster>().Add(entity);
            caster.Index = index;
            caster.InstanceId = instanceId;
            caster.CooldownTicks = 0;
            caster.PeriodTicks = periodTicks < 1 ? 1 : periodTicks;
            caster.MpCost = mpCost < 0 ? 0 : mpCost;
            caster.Damage = damage < 0 ? 0 : damage;
            caster.Element = element;
            caster.Behavior = behavior;
            caster.Range = range > 0f ? range : 0f;
            caster.ProjectileSpeed = projectileSpeed > 0f ? projectileSpeed : 0f;
            caster.PierceCount = pierceCount < 0 ? 0 : pierceCount;
            caster.ProjectileCount = projectileCount < 1 ? 1 : projectileCount;
            caster.DefId = skillDefId;

            return entity;
        }

        /// <summary>Creates one ghost from a resolved spawn template.</summary>
        /// <param name="context">The session's shared state.</param>
        /// <param name="template">The wave slot the stats came from.</param>
        /// <param name="x">World X.</param>
        /// <param name="y">World Y.</param>
        /// <returns>The new entity.</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="context"/> is null.</exception>
        public int SpawnEnemy(BattleContext context, in WaveSlot template, float x, float y)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            int entity = _world.NewEntity();

            ref Position position = ref _positions.Add(entity);
            position.X = x;
            position.Y = y;

            ref Velocity velocity = ref _velocities.Add(entity);
            velocity.X = 0f;
            velocity.Y = 0f;

            ref Health health = ref _healths.Add(entity);
            health.Hp = template.MaxHp;
            health.MaxHp = template.MaxHp;

            ref Faction faction = ref _factions.Add(entity);
            faction.Value = Faction.Ghost;

            ref EnemyTag tag = ref _enemyTags.Add(entity);
            tag.DefId = template.EnemyDefId;
            tag.MovePattern = template.MovePattern;
            tag.MoveSpeed = template.MoveSpeed;
            tag.IsBoss = template.IsBoss;
            tag.IsElite = template.IsElite;
            tag.DropValue = template.DropValue;

            ref ContactAttack contact = ref _contacts.Add(entity);
            contact.Damage = template.ContactDamage;
            contact.PeriodTicks = template.ContactPeriodTicks;
            contact.RemainingTicks = 0;

            ref Collider collider = ref _colliders.Add(entity);
            collider.Radius = EnemyRadius(template.IsBoss, template.IsElite);

            // Only a ghost with a telegraphed attack gets the attack and its brain: the systems
            // filter on them, so a walker is not charged for a machine it never runs.
            if (template.Attack.Pattern != (int)EnemyAttackPattern.None)
            {
                _attacks.Add(entity) = template.Attack;
                _brains.Add(entity) = default;
            }

            return entity;
        }

        /// <summary>Body radius of a ghost, from its kind.</summary>
        public static float EnemyRadius(bool isBoss, bool isElite)
        {
            if (isBoss)
            {
                return BattleArena.EnemyRadius * BattleArena.BossRadiusScale;
            }

            return isElite
                ? BattleArena.EnemyRadius * BattleArena.EliteRadiusScale
                : BattleArena.EnemyRadius;
        }

        /// <summary>Creates one projectile, owned by the player faction.</summary>
        /// <param name="defId">Skill def id, for events.</param>
        /// <param name="damage">Damage of one hit.</param>
        /// <param name="element">Element of the hit, <c>0</c> when none.</param>
        /// <param name="hitsLeft">Hits it may land before it disappears.</param>
        /// <param name="speed">Speed in world units per second.</param>
        /// <param name="x">World X.</param>
        /// <param name="y">World Y.</param>
        /// <param name="directionX">Unit direction X.</param>
        /// <param name="directionY">Unit direction Y.</param>
        /// <param name="lifetimeTicks">Ticks before it despawns.</param>
        /// <returns>The new entity.</returns>
        public int SpawnProjectile(
            int defId,
            int damage,
            int element,
            int hitsLeft,
            float speed,
            float x,
            float y,
            float directionX,
            float directionY,
            int lifetimeTicks)
        {
            int entity = _world.NewEntity();

            ref Position position = ref _positions.Add(entity);
            position.X = x;
            position.Y = y;

            ref Velocity velocity = ref _velocities.Add(entity);
            velocity.X = directionX * speed;
            velocity.Y = directionY * speed;

            ref Faction faction = ref _factions.Add(entity);
            faction.Value = Faction.Player;

            ref Projectile projectile = ref _projectiles.Add(entity);
            projectile.DefId = defId;
            projectile.Damage = damage;
            projectile.Element = element;
            projectile.HitsLeft = hitsLeft < 1 ? 1 : hitsLeft;

            ref Lifetime lifetime = ref _lifetimes.Add(entity);
            lifetime.RemainingTicks = lifetimeTicks < 1 ? 1 : lifetimeTicks;

            ref Collider collider = ref _colliders.Add(entity);
            collider.Radius = BattleArena.ProjectileRadius;

            return entity;
        }

        /// <summary>Creates one dropped pickup.</summary>
        /// <param name="x">World X.</param>
        /// <param name="y">World Y.</param>
        /// <param name="kind">Pickup kind, for events.</param>
        /// <param name="value">What it is worth, from <c>enemy.drop_value</c>.</param>
        /// <returns>The new entity.</returns>
        public int SpawnPickup(float x, float y, int kind, int value)
        {
            int entity = _world.NewEntity();

            ref Position position = ref _positions.Add(entity);
            position.X = x;
            position.Y = y;

            ref Faction faction = ref _factions.Add(entity);
            faction.Value = Faction.Player;

            ref Pickup pickup = ref _pickups.Add(entity);
            pickup.Kind = kind;
            pickup.Value = value;

            ref Collider collider = ref _colliders.Add(entity);
            collider.Radius = BattleArena.PickupRadius;

            return entity;
        }
    }
}
