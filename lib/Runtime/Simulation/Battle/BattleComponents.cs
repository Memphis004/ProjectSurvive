namespace ProjectSurvive.Core.Simulation.Battle
{
    /*
     * The battle's component vocabulary (Stage 07, GDD "Components").
     *
     * Every one of these is a plain value type with public fields, because that is
     * what the ECS works with: EcsLite hands out `ref T` into a pooled dense array,
     * so a system mutates the component in place and nothing is ever boxed or
     * copied. A component must not hold a reference (that would need an AutoReset
     * hook and would reintroduce allocation into the tick), and it must not name an
     * EcsLite type, so the component set stays usable from the tests and readable
     * without knowing the ECS.
     *
     * Field order is the struct layout; there is no padding-tuning here and none is
     * needed at this scale.
     */

    /// <summary>World position, in the arena plane.</summary>
    public struct Position
    {
        /// <summary>World X.</summary>
        public float X;

        /// <summary>World Y.</summary>
        public float Y;
    }

    /// <summary>Velocity in world units per second, written by whichever system moved the entity.</summary>
    public struct Velocity
    {
        /// <summary>X component, units per second.</summary>
        public float X;

        /// <summary>Y component, units per second.</summary>
        public float Y;
    }

    /// <summary>Hit points.</summary>
    public struct Health
    {
        /// <summary>Current HP; 0 or below means the entity is dying.</summary>
        public int Hp;

        /// <summary>Maximum HP, from the enemy table (or the loadout, for the player).</summary>
        public int MaxHp;
    }

    /// <summary>Which side an entity is on; damage only crosses factions.</summary>
    public struct Faction
    {
        /// <summary>The player's side.</summary>
        public const byte Player = 0;

        /// <summary>The ghosts' side.</summary>
        public const byte Ghost = 1;

        /// <summary>0 = player, 1 = ghost. See <see cref="Player"/>/<see cref="Ghost"/>.</summary>
        public byte Value;
    }

    /// <summary>Marks the one player entity.</summary>
    public struct PlayerTag
    {
    }

    /// <summary>
    /// Marks a ghost and carries everything the tick needs about its kind, so no
    /// system has to look a def up on the hot path.
    /// </summary>
    public struct EnemyTag
    {
        /// <summary>Enemy def id (1-based index into the enemy table), for events.</summary>
        public int DefId;

        /// <summary>Move pattern, from <see cref="ProjectSurvive.Core.Domain.Config.MovePattern"/>.</summary>
        public int MovePattern;

        /// <summary>Move speed in world units per second, so steering never looks a def up.</summary>
        public float MoveSpeed;

        /// <summary>True when this ghost is the floor's boss.</summary>
        public bool IsBoss;

        /// <summary>True when this ghost is an elite.</summary>
        public bool IsElite;

        /// <summary>Bia its pickup pays when collected, copied from <c>enemy.drop_value</c>.</summary>
        public int DropValue;
    }

    /// <summary>The player's MP pool and the fractional focus carried between ticks.</summary>
    public struct Mana
    {
        /// <summary>Current MP.</summary>
        public int Mp;

        /// <summary>Maximum MP, from the loadout's stat block.</summary>
        public int MaxMp;

        /// <summary>Focus earned but not yet a whole point, so regen is not lost to truncation.</summary>
        public float Carry;
    }

    /// <summary>
    /// One weapon or familiar the player carries, and its casting clock. There is one
    /// caster entity per entry in the loadout, which is how several weapons keep
    /// independent cooldowns without the player entity holding a collection.
    /// </summary>
    public struct Caster
    {
        /// <summary>Index of the loadout entry this caster drives, for the hash and for debugging.</summary>
        public int Index;

        /// <summary>
        /// Board instance id of the piece this caster's loadout entry came from, or
        /// <see cref="ProjectSurvive.Core.Domain.Combat.Battle.LoadoutCooldown.NoInstance"/>. Published
        /// with the cooldown so a HUD can draw it on the item the player placed (Stage 08b). It is
        /// <b>not</b> part of <c>ComputeStateHash</c>: it is which piece the entry came from, not what
        /// the cast did, and a field that only ever answers a question about the bag must not be able
        /// to move a deterministic fingerprint.
        /// </summary>
        public int InstanceId;

        /// <summary>Ticks until this weapon may cast again; 0 means ready.</summary>
        public int CooldownTicks;

        /// <summary>Length of this weapon's cooldown in ticks, resolved from the loadout.</summary>
        public int PeriodTicks;

        /// <summary>MP the cast costs.</summary>
        public int MpCost;

        /// <summary>Damage of one application.</summary>
        public int Damage;

        /// <summary>Element of the cast, or <c>0</c> when it carries none.</summary>
        public int Element;

        /// <summary>Cast behaviour, from <see cref="ProjectSurvive.Core.Domain.Config.SkillBehavior"/>.</summary>
        public int Behavior;

        /// <summary>Reach in world units: the aura radius, the melee reach, or the projectile's range.</summary>
        public float Range;

        /// <summary>Projectile speed in world units per second (0 for a non-projectile).</summary>
        public float ProjectileSpeed;

        /// <summary>Extra ghosts one projectile passes through after its first hit.</summary>
        public int PierceCount;

        /// <summary>Projectiles spawned per cast (1 for a single shot or for an aura).</summary>
        public int ProjectileCount;

        /// <summary>Skill def id (1-based index into the skill table), for events.</summary>
        public int DefId;
    }

    /// <summary>
    /// One hostile projectile in flight. Owner faction is on the entity's own
    /// <see cref="Faction"/>, so a future ghost projectile needs no new component.
    /// </summary>
    public struct Projectile
    {
        /// <summary>Skill def id (1-based index into the skill table), for events.</summary>
        public int DefId;

        /// <summary>Damage of one hit.</summary>
        public int Damage;

        /// <summary>Element of the hit, or <c>0</c> when it carries none.</summary>
        public int Element;

        /// <summary>Hits this projectile may still land before it disappears.</summary>
        public int HitsLeft;
    }

    /// <summary>
    /// An elemental effect riding on a ghost: applied by a hit whose skill carries an
    /// element, refreshed by the next one, and expired by time.
    /// </summary>
    public struct Status
    {
        /// <summary>
        /// Def id of the <c>status</c> row that applied it (Stage 11a), so the event stream
        /// names the effect rather than the skill that happened to carry the element.
        /// </summary>
        public int DefId;

        /// <summary>Element that applied it, or <c>0</c> when none.</summary>
        public int Element;

        /// <summary>Applications riding at once; capped by the row's <c>max_stacks</c>.</summary>
        public int Stacks;

        /// <summary>Ticks until it expires.</summary>
        public int RemainingTicks;

        /// <summary>Ticks between two damage applications.</summary>
        public int TickInterval;

        /// <summary>Ticks until the next damage application.</summary>
        public int NextTickIn;

        /// <summary>Damage of one application.</summary>
        public int DamagePerTick;
    }

    /// <summary>A ghost's contact attack: walking into the player hurts, on a cooldown.</summary>
    public struct ContactAttack
    {
        /// <summary>Damage of one contact.</summary>
        public int Damage;

        /// <summary>Length of the attack cooldown in ticks.</summary>
        public int PeriodTicks;

        /// <summary>Ticks until this ghost may hurt the player again; 0 means ready.</summary>
        public int RemainingTicks;
    }

    /// <summary>
    /// The telegraphed attack a ghost owns, copied from its enemy row at spawn (Stage 11b).
    /// </summary>
    /// <remarks>
    /// A plain copy of the numbers, so the state machine never looks a def up on the hot
    /// path — the same rule <see cref="EnemyTag"/> follows for HP and speed. Zero on a
    /// stage means "this ghost does not run that stage", which is how one struct describes
    /// both Krasue (dash, drain, retreat) and Nang Tani (ring, vines).
    /// </remarks>
    public struct EnemyAttack
    {
        /// <summary>Attack pattern, from <see cref="ProjectSurvive.Core.Domain.Config.EnemyAttackPattern"/>.</summary>
        public int Pattern;

        /// <summary>Distance at which the cycle starts, in world units.</summary>
        public float Range;

        /// <summary>Warning time before the attack lands, in ticks.</summary>
        public int TelegraphTicks;

        /// <summary>Strike window (a dash's travel), in ticks.</summary>
        public int StrikeTicks;

        /// <summary>Damage of one application of the attack.</summary>
        public int Damage;

        /// <summary>Circle radius, or the half-width of a line.</summary>
        public float Radius;

        /// <summary>Line length; 0 makes the attack a circle centred on the ghost.</summary>
        public float Length;

        /// <summary>Warning time of the second attack, in ticks; 0 for none.</summary>
        public int FollowUpTelegraphTicks;

        /// <summary>Duration of the second attack's window, in ticks; 0 for a single application.</summary>
        public int FollowUpTicks;

        /// <summary>Damage of one application of the second attack.</summary>
        public int FollowUpDamage;

        /// <summary>Circle radius, or the half-width of the second attack's line.</summary>
        public float FollowUpRadius;

        /// <summary>Line length of the second attack; 0 makes it a circle.</summary>
        public float FollowUpLength;

        /// <summary>Ticks between applications inside the second attack's window.</summary>
        public int FollowUpTickTicks;

        /// <summary>How long it steps away after the cycle, in ticks; 0 for none.</summary>
        public int RetreatTicks;

        /// <summary>Retreat speed, in world units per second.</summary>
        public float RetreatSpeed;

        /// <summary>
        /// Ticks between two cycles, from the row's <c>attack_cooldown_seconds</c> — the same
        /// column the contact attack uses, because a ghost that attacks on a rhythm has one
        /// rhythm.
        /// </summary>
        public int CooldownTicks;
    }

    /// <summary>
    /// The tiny state machine a telegraphed attack runs (Stage 11b), one per ghost that has
    /// an <see cref="EnemyAttack"/>.
    /// </summary>
    /// <remarks>
    /// State and aim are part of the battle's deterministic state, so they are hashed: where a
    /// ghost is in its cycle is as much a fact of the fight as its HP.
    /// </remarks>
    public struct EnemyBrain
    {
        /// <summary>Current stage, from <see cref="Systems.EnemyAttackState"/>.</summary>
        public int State;

        /// <summary>Ticks left in the current stage.</summary>
        public int StateTicks;

        /// <summary>Ticks until the next damage application inside a window.</summary>
        public int DamageTicks;

        /// <summary>Unit aim locked when the telegraph began (X).</summary>
        public float AimX;

        /// <summary>Unit aim locked when the telegraph began (Y).</summary>
        public float AimY;
    }

    /// <summary>A drop on the floor, waiting to be pulled in.</summary>
    public struct Pickup
    {
        /// <summary>Pickup kind, for the <c>PickupSpawned</c>/<c>PickupCollected</c> events.</summary>
        public int Kind;

        /// <summary>
        /// What collecting it is worth, copied from the dead enemy's <c>enemy.drop_value</c>
        /// (Stage 07b). <see cref="Systems.PickupSystem"/> adds it to
        /// <c>BattleContext.CurrencyEarned</c> exactly once, in the tick it is collected.
        /// </summary>
        public int Value;
    }

    /// <summary>How long an entity may still exist; the projectile despawn clock.</summary>
    public struct Lifetime
    {
        /// <summary>Ticks left before the entity is removed. 0 or below means "gone".</summary>
        public int RemainingTicks;
    }

    /// <summary>The body radius a collision test uses.</summary>
    public struct Collider
    {
        /// <summary>Radius in world units.</summary>
        public float Radius;
    }

    /// <summary>One spawn window of a floor, pre-resolved from the wave table.</summary>
    public struct WaveSlot
    {
        /// <summary>Tick of the wave's first spawn.</summary>
        public int FirstSpawnTick;

        /// <summary>Tick its window closes; a spawn after this is not made.</summary>
        public int EndTick;

        /// <summary>How many ghosts the wave asks for.</summary>
        public int SpawnCount;

        /// <summary>Ticks between two spawns, at least 1.</summary>
        public int IntervalTicks;

        /// <summary>How many have been spawned so far.</summary>
        public int Spawned;

        /// <summary>True once the <c>WaveStarted</c> event was emitted.</summary>
        public bool Announced;

        /// <summary>Enemy def id (1-based index into the enemy table), for events.</summary>
        public int EnemyDefId;

        /// <summary>Enemy max HP.</summary>
        public int MaxHp;

        /// <summary>Enemy move speed in world units per second.</summary>
        public float MoveSpeed;

        /// <summary>Enemy contact damage.</summary>
        public int ContactDamage;

        /// <summary>Enemy contact-attack period in ticks.</summary>
        public int ContactPeriodTicks;

        /// <summary>Enemy move pattern, from <see cref="ProjectSurvive.Core.Domain.Config.MovePattern"/>.</summary>
        public int MovePattern;

        /// <summary>True when the wave spawns the floor's boss.</summary>
        public bool IsBoss;

        /// <summary>True when the wave spawns an elite.</summary>
        public bool IsElite;

        /// <summary>Bia one kill of this enemy pays, copied from <c>enemy.drop_value</c>.</summary>
        public int DropValue;

        /// <summary>The row's telegraphed attack, copied whole (Stage 11b).</summary>
        public EnemyAttack Attack;
    }
}
