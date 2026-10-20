using System;
using System.Collections.Generic;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The state every battle system shares: the config it was built from, the
    /// pre-resolved spawn timeline, the per-tick queues between systems, and the few
    /// counters the HUD and the result need.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Created once per session by <see cref="EcsBattleSimulation"/>, and handed to the
    /// systems through <c>IEcsSystems.GetShared&lt;BattleContext&gt;()</c> — a field read
    /// that allocates nothing. Every array here is allocated in the constructor and
    /// reused, because a tick must not allocate (AGENTS rule 8) and the contract suite
    /// asserts it (<c>BattleContractTests…Step_does_not_allocate_after_warm_up</c>).
    /// </para>
    /// <para>
    /// <b>Where the numbers live.</b> Player HP, MP and position are <i>not</i> mirrored
    /// here: they are ECS components on the player entity (<see cref="Health"/>,
    /// <see cref="Mana"/>, <see cref="Position"/>), which is the single source of truth
    /// every system, the render snapshot and <c>ComputeStateHash</c> read. A mirror
    /// field is how a HUD and a simulation start disagreeing.
    /// </para>
    /// <para>
    /// <b>The wave timeline is pre-resolved</b> into <see cref="Waves"/> once, so no
    /// system looks a def up while the clock runs: a spawn is one array read plus the
    /// entity's components.
    /// </para>
    /// <para>
    /// <b>Randomness</b> is the <see cref="RandomStreamKind.Battle"/> stream only
    /// (AGENTS rule 6), so a battle can never shift the camp's shop stock.
    /// </para>
    /// </remarks>
    public sealed class BattleContext
    {
        private readonly int[] _hitTarget;
        private readonly int[] _hitDamage;
        private readonly int[] _hitElement;
        private readonly int[] _hitDefId;
        private readonly float[] _dropX;
        private readonly float[] _dropY;
        private readonly int[] _dropDefId;
        private readonly int[] _dropValue;

        /// <summary>Builds the shared state for one session.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public BattleContext(
            BattleSetup setup,
            FloorConfig floor,
            IGameConfig config,
            EcsBattleOptions options,
            IRandomSource random,
            BattleDefIds defIds,
            EcsWorld world)
        {
            Setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Floor = floor ?? throw new ArgumentNullException(nameof(floor));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            DefIds = defIds ?? throw new ArgumentNullException(nameof(defIds));
            World = world ?? throw new ArgumentNullException(nameof(world));

            Events = new BattleEventRing(BattleConstants.EventRingCapacity);
            Frame = new BattleFrameBuffer();
            Hash = new BattleSpatialHash(
                BattleArena.Radius, BattleArena.SpatialCellSize, BattleArena.EntityCapacity);
            Spawner = new BattleSpawner(world);

            _hitTarget = new int[BattleArena.MaxHitsPerTick];
            _hitDamage = new int[BattleArena.MaxHitsPerTick];
            _hitElement = new int[BattleArena.MaxHitsPerTick];
            _hitDefId = new int[BattleArena.MaxHitsPerTick];

            _dropX = new float[BattleArena.MaxDropsPerTick];
            _dropY = new float[BattleArena.MaxDropsPerTick];
            _dropDefId = new int[BattleArena.MaxDropsPerTick];
            _dropValue = new int[BattleArena.MaxDropsPerTick];

            MaxHp = setup.Loadout.Stats.MaxHp > 0 ? setup.Loadout.Stats.MaxHp : config.Constant.MaxHp;
            MaxMp = setup.Loadout.Stats.MaxMp > 0 ? setup.Loadout.Stats.MaxMp : config.Constant.MaxMp;
            FocusPerSecond = setup.Loadout.Stats.FocusPerSecond > 0f
                ? setup.Loadout.Stats.FocusPerSecond
                : config.Constant.FocusPerSecond;
            PickupRadius = setup.Loadout.Stats.PickupRadius > 0f
                ? setup.Loadout.Stats.PickupRadius
                : config.Constant.PickupRadius;
            MoveSpeed = setup.Loadout.Stats.MoveSpeed > 0f
                ? setup.Loadout.Stats.MoveSpeed
                : config.Constant.MoveSpeed;
            MagnetSpeed = config.Constant.MagnetSpeed > 0f ? config.Constant.MagnetSpeed : 0f;
            SweepTimeoutTicks = config.Constant.SweepTimeoutSeconds > 0f
                ? Math.Max(1, (int)Math.Round(config.Constant.SweepTimeoutSeconds * BattleConstants.TickRate))
                : 0;

            FloorDurationTicks = floor.DurationSeconds > 0f
                ? (int)Math.Round(floor.DurationSeconds * BattleConstants.TickRate)
                : 0;

            StartingHp = Clamp(setup.CurrentHp, 0, MaxHp);

            // Remaining *uses*, not remaining pieces: an offering row says how many times one
            // carried piece can be used (Stage 11a), and the result reports what is left in
            // that unit. The settlement converts it back to pieces to remove.
            IReadOnlyList<ConsumableLoadout> consumables = setup.Loadout.Consumables;
            ConsumableUses = new int[consumables.Count];
            for (int i = 0; i < consumables.Count; i++)
            {
                ConsumableLoadout entry = consumables[i];
                int perItem = entry.UsesPerItem > 0 ? entry.UsesPerItem : 1;
                int count = entry.Count > 0 ? entry.Count : 0;
                ConsumableUses[i] = count * perItem;
            }

            Waves = BuildWaves(floor, config, defIds);
            WaveCount = Waves.Length;
        }

        /// <summary>The config the battle reads its balance from.</summary>
        public IGameConfig Config { get; }

        /// <summary>The load-benchmark knobs; the shipped game uses the defaults.</summary>
        public EcsBattleOptions Options { get; }

        /// <summary>The battle's own random stream.</summary>
        public IRandomSource Random { get; }

        /// <summary>The event ring Presentation reads — never a rule (see the contract).</summary>
        public BattleEventRing Events { get; }

        /// <summary>The frame publisher, shared with the fake (ADR 0012).</summary>
        public BattleFrameBuffer Frame { get; }

        /// <summary>String id to numeric def id, for event payloads.</summary>
        public BattleDefIds DefIds { get; }

        /// <summary>The spatial grid, rebuilt every tick by <see cref="Systems.SpatialHashSystem"/>.</summary>
        public BattleSpatialHash Hash { get; }

        /// <summary>Creates the simulation's entities.</summary>
        public BattleSpawner Spawner { get; }

        /// <summary>The floor being fought.</summary>
        public FloorConfig Floor { get; }

        /// <summary>The run's setup for this floor.</summary>
        public BattleSetup Setup { get; }

        /// <summary>The ECS world the entities live in.</summary>
        public EcsWorld World { get; }

        /// <summary>The player's movement speed for this floor, from the loadout.</summary>
        public float MoveSpeed { get; }

        /// <summary>
        /// Speed at which a drop drifts toward the player during the end-of-floor sweep,
        /// in world units per second (<c>constant.magnet_speed</c>, Stage 07c).
        /// </summary>
        public float MagnetSpeed { get; }

        /// <summary>
        /// Longest the end-of-floor sweep may run, in ticks (<c>constant.sweep_timeout_seconds</c>).
        /// At the cap the drops that are left are collected at once, so the sweep always ends.
        /// </summary>
        public int SweepTimeoutTicks { get; }

        /// <summary>How close a pickup must be to be pulled in, from the loadout.</summary>
        public float PickupRadius { get; }

        /// <summary>Player max HP for this floor.</summary>
        public int MaxHp { get; }

        /// <summary>Player max MP for this floor.</summary>
        public int MaxMp { get; }

        /// <summary>Focus (MP) regained per second.</summary>
        public float FocusPerSecond { get; }

        /// <summary>HP the floor started from (<see cref="BattleSetup.CurrentHp"/> clamped to max).</summary>
        public int StartingHp { get; }

        /// <summary>Length of the floor in ticks (0 when the floor has no duration).</summary>
        public int FloorDurationTicks { get; }

        /// <summary>The floor's spawn windows, in the order they open.</summary>
        public WaveSlot[] Waves { get; }

        /// <summary>How many spawn windows the floor has.</summary>
        public int WaveCount { get; }

        /// <summary>The move input for the tick being simulated.</summary>
        public PlayerInputFrame Input { get; set; }

        /// <summary>Ticks elapsed; advanced once per <c>Step</c>.</summary>
        public long Tick { get; set; }

        /// <summary>Ghosts killed this floor.</summary>
        public int Kills { get; set; }

        /// <summary>Ghosts alive and still standing.</summary>
        public int EnemiesAlive { get; set; }

        /// <summary>Projectiles in flight.</summary>
        public int ProjectilesAlive { get; set; }

        /// <summary>Pickups collected this floor.</summary>
        public int PickupsCollected { get; set; }

        /// <summary>Bia earned this floor; 0 while no drop table exists (ADR 0017).</summary>
        public int CurrencyEarned { get; set; }

        /// <summary>Index of the wave currently running, 0-based.</summary>
        public int WaveIndex { get; set; }

        /// <summary>The player's entity, or -1 before the session built it.</summary>
        public int PlayerEntity { get; set; } = -1;

        /// <summary>
        /// Uses left per <see cref="BattleSetup.Loadout"/> consumable entry, in loadout order.
        /// Initialised to <c>Count * UsesPerItem</c> and spent by
        /// <see cref="Systems.ConsumableSystem"/>; the result reports it verbatim.
        /// </summary>
        public int[] ConsumableUses { get; }

        /// <summary>The floor's boss entity, or -1 when none has spawned.</summary>
        public int BossEntity { get; set; } = -1;

        /// <summary>The boss's current HP; 0 on a floor with no boss.</summary>
        public int BossHp { get; set; }

        /// <summary>The boss's max HP; 0 on a floor with no boss.</summary>
        public int BossMaxHp { get; set; }

        /// <summary>True once this floor's boss has spawned, so "boss HP is 0" can mean "beaten".</summary>
        public bool BossSpawned { get; set; }

        /// <summary>True while <c>DevGodMode</c> is on.</summary>
        public bool GodMode { get; set; }

        /// <summary>
        /// True while the end-of-floor sweep is running (Stage 07c). The floor is already won
        /// (<see cref="EndPhase"/>/<see cref="EndReason"/> say why) but the result waits until
        /// every drop on the ground has been pulled in or the cap is reached. The phase the
        /// contract publishes stays <c>Running</c> for the whole sweep.
        /// </summary>
        public bool SweepActive { get; set; }

        /// <summary>Sweep ticks left before the cap collects everything that is left.</summary>
        public int SweepTicksRemaining { get; set; }

        /// <summary>True once a system decided the floor is over.</summary>
        public bool EndRequested { get; set; }

        /// <summary>The phase the floor ended in, once <see cref="EndRequested"/>.</summary>
        public BattlePhase EndPhase { get; set; }

        /// <summary>Why the floor ended, once <see cref="EndRequested"/>.</summary>
        public BattleEndReason EndReason { get; set; }

        /// <summary>Set by <c>DevKillAll</c> and consumed by the death system.</summary>
        public bool KillAllRequested { get; set; }

        /// <summary>True once the 15-second warning was emitted.</summary>
        public bool WarnedSeconds15 { get; set; }

        /// <summary>True once the 5-second warning was emitted.</summary>
        public bool WarnedSeconds5 { get; set; }

        /// <summary>How many damage applications are queued for this tick.</summary>
        public int HitCount { get; private set; }

        /// <summary>Damage queued against the player this tick.</summary>
        public int PlayerHitDamage { get; set; }

        /// <summary>How many drops are queued for this tick.</summary>
        public int DropCount { get; private set; }

        /// <summary>Queues one damage application, ignoring it when the queue is full.</summary>
        public void AddHit(int target, int damage, int element, int defId)
        {
            if (damage <= 0 || HitCount >= _hitTarget.Length)
            {
                return;
            }

            _hitTarget[HitCount] = target;
            _hitDamage[HitCount] = damage;
            _hitElement[HitCount] = element;
            _hitDefId[HitCount] = defId;
            HitCount++;
        }

        /// <summary>A queued hit's target entity id.</summary>
        public int HitTargetAt(int index) => _hitTarget[index];

        /// <summary>A queued hit's damage.</summary>
        public int HitDamageAt(int index) => _hitDamage[index];

        /// <summary>A queued hit's element (<c>0</c> when none).</summary>
        public int HitElementAt(int index) => _hitElement[index];

        /// <summary>A queued hit's skill def id.</summary>
        public int HitDefIdAt(int index) => _hitDefId[index];

        /// <summary>Queues one drop, ignoring it when the queue is full.</summary>
        /// <param name="x">World X where the ghost fell.</param>
        /// <param name="y">World Y where the ghost fell.</param>
        /// <param name="defId">The dead enemy's def id, for events and presentation.</param>
        /// <param name="value">Bia the pickup pays, already read from the enemy row.</param>
        public void AddDrop(float x, float y, int defId, int value)
        {
            if (DropCount >= _dropX.Length)
            {
                return;
            }

            _dropX[DropCount] = x;
            _dropY[DropCount] = y;
            _dropDefId[DropCount] = defId;
            _dropValue[DropCount] = value > 0 ? value : 0;
            DropCount++;
        }

        /// <summary>A queued drop's world X.</summary>
        public float DropXAt(int index) => _dropX[index];

        /// <summary>A queued drop's world Y.</summary>
        public float DropYAt(int index) => _dropY[index];

        /// <summary>A queued drop's enemy def id.</summary>
        public int DropDefIdAt(int index) => _dropDefId[index];

        /// <summary>A queued drop's bia value.</summary>
        public int DropValueAt(int index) => _dropValue[index];

        /// <summary>Empties the per-tick queues. Called once, before the systems run.</summary>
        public void ClearTickBuffers()
        {
            HitCount = 0;
            DropCount = 0;
            PlayerHitDamage = 0;
        }

        /// <summary>
        /// Appends one presentation event. The ring assigns <c>Seq</c> itself, so the
        /// value passed as the sequence is ignored — pass <c>0</c>, as the fake does.
        /// </summary>
        /// <param name="type">What happened.</param>
        /// <param name="entity">The entity the event is about.</param>
        /// <param name="defId">The event's def id, or -1.</param>
        /// <param name="value">The event's integer payload.</param>
        /// <param name="flags">Optional tags.</param>
        /// <param name="x">World X, when the event has a position.</param>
        /// <param name="y">World Y, when the event has a position.</param>
        /// <param name="radius">Footprint radius / half-width (Stage 11b telegraphs).</param>
        /// <param name="length">Footprint length; 0 for a circle (Stage 11b telegraphs).</param>
        /// <param name="angle">Footprint direction in radians (Stage 11b telegraphs).</param>
        public long Emit(
            BattleEventType type,
            EntityRef entity = default,
            int defId = -1,
            int value = 0,
            BattleEventFlags flags = BattleEventFlags.None,
            float x = 0f,
            float y = 0f,
            float radius = 0f,
            float length = 0f,
            float angle = 0f)
        {
            return Events.Write(new BattleEvent(
                0, type, entity, defId, value, flags, x, y, radius, length, angle));
        }

        /// <summary>
        /// A stable handle to an ECS entity, for the frame and the event stream: the
        /// slot index is 1-based and the version is the world's generation, so a handle
        /// held across a recycle is detectable (see <see cref="EntityRef"/>).
        /// </summary>
        public EntityRef Ref(int ecsEntity)
        {
            int generation = World.GetEntityGen(ecsEntity);
            return new EntityRef(ecsEntity + 1, generation < 0 ? -generation : generation);
        }

        /// <summary>True while an ECS entity is still alive (its generation is positive).</summary>
        public bool IsAlive(int ecsEntity) => ecsEntity >= 0 && World.GetEntityGen(ecsEntity) > 0;

        /// <summary>
        /// Pushes a position back inside the arena disc, keeping <paramref name="margin"/>
        /// clear of the edge. Returns whether it moved.
        /// </summary>
        public static bool ClampToArena(ref float x, ref float y, float margin)
        {
            float limit = BattleArena.Radius - margin;
            float squared = (x * x) + (y * y);
            if (squared <= limit * limit)
            {
                return false;
            }

            float length = (float)Math.Sqrt(squared);
            if (length <= 0f)
            {
                x = 0f;
                y = 0f;
                return true;
            }

            float scale = limit / length;
            x *= scale;
            y *= scale;
            return true;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        /// <summary>
        /// Resolves the floor's spawn windows from the wave table once: def id, combat
        /// stats, spawn ticks and end tick, in the canonical order (start time, then id).
        /// </summary>
        private static WaveSlot[] BuildWaves(FloorConfig floor, IGameConfig config, BattleDefIds defIds)
        {
            var rows = new List<WaveConfig>();
            for (int i = 0; i < config.Waves.Count; i++)
            {
                WaveConfig wave = config.Waves[i];
                if (string.Equals(wave.FloorId, floor.Id, StringComparison.Ordinal))
                {
                    rows.Add(wave);
                }
            }

            rows.Sort(CompareWaves);

            var slots = new WaveSlot[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                WaveConfig wave = rows[i];
                var slot = new WaveSlot
                {
                    FirstSpawnTick = SecondsToTicks(wave.StartTimeSeconds),
                    EndTick = SecondsToTicks(wave.EndTimeSeconds),
                    SpawnCount = wave.SpawnCount < 0 ? 0 : wave.SpawnCount,
                    IntervalTicks = Math.Max(1, SecondsToTicks(wave.SpawnIntervalSeconds)),
                    EnemyDefId = defIds.Enemy(wave.EnemyId),
                };

                // The enemy row carries the combat numbers. A wave naming an enemy that is
                // not in the table is a data error the validator rejects; if one somehow
                // reaches here the slot stays inert rather than inventing a stat block.
                if (config.TryGetEnemy(wave.EnemyId, out EnemyConfig enemy))
                {
                    slot.MaxHp = enemy.MaxHp;
                    slot.MoveSpeed = enemy.MoveSpeed;
                    slot.ContactDamage = enemy.ContactDamage;
                    slot.ContactPeriodTicks = Math.Max(1, SecondsToTicks(enemy.AttackCooldownSeconds));
                    slot.MovePattern = (int)enemy.MovePattern;
                    slot.IsBoss = enemy.IsBoss;
                    slot.IsElite = enemy.IsElite || wave.IsElite;
                    slot.DropValue = enemy.DropValue > 0 ? enemy.DropValue : 0;

                    // Stage 11b: the whole telegraphed-attack block, resolved to ticks once.
                    slot.Attack = EnemyAttackRules.FromConfig(enemy);

                    // Stage 11c: the boss block — phases, corpse eating, possession — resolved
                    // to ticks (and to an absolute HP threshold) in the same pass.
                    slot.Boss = BossRules.FromConfig(enemy);
                }
                else
                {
                    slot.SpawnCount = 0;
                }

                slots[i] = slot;
            }

            return slots;
        }

        private static int CompareWaves(WaveConfig left, WaveConfig right)
        {
            int byStart = left.StartTimeSeconds.CompareTo(right.StartTimeSeconds);
            return byStart != 0 ? byStart : string.CompareOrdinal(left.Id, right.Id);
        }

        private static int SecondsToTicks(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 0;
            }

            return (int)Math.Round(seconds * BattleConstants.TickRate);
        }
    }
}
