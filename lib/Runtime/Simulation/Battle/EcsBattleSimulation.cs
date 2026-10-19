using System;
using System.Collections.Generic;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Core.Simulation.Battle.Systems;

namespace ProjectSurvive.Core.Simulation.Battle
{
    /// <summary>
    /// The real battle: an EcsLite world of ghosts, projectiles and drops, driven one fixed
    /// 30 Hz tick at a time, implementing the Stage 02b <see cref="IBattleSession"/> contract
    /// with no <c>UnityEngine</c>, no scene and no physics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One tick is one <see cref="Step"/>.</b> The tick advances, the systems run in their
    /// documented order, the frame is published, and then — if a system decided the floor is
    /// over — the result is produced. The frame is published <i>before</i> the ending is
    /// recorded so the last frame describes the tick that ended, and <c>BattleEnded</c> stays
    /// the final event of the stream.
    /// </para>
    /// <para>
    /// <b>Allocation.</b> The world, every component pool, every filter and every queue are
    /// created here, once. A tick allocates nothing: the contract suite measures it, and the
    /// Stage 07 benchmark measures it again under ~300 ghosts and ~500 projectiles. The one
    /// thing a battle does allocate is its <see cref="BattleResult"/> and the consumable
    /// report inside it, when the floor ends.
    /// </para>
    /// <para>
    /// <b>Determinism</b> is the contract's: the same setup and the same sequence of
    /// <see cref="Step"/>/<see cref="Submit"/> calls produce the same
    /// <see cref="ComputeStateHash"/> in one runtime (AGENTS rule 6 — no bitwise claim across
    /// browsers or CPUs). Randomness is the battle stream only.
    /// </para>
    /// <para>
    /// <b>The ECS stops here.</b> Systems and components are <c>public</c> inside this
    /// assembly, but nothing outside <c>lib/Runtime/Simulation</c> may name an EcsLite type:
    /// callers hold <see cref="IBattleSession"/>, read <see cref="IBattleFrame"/>, and reach a
    /// session through <see cref="IBattleSessionFactory"/>. An architecture test enforces it.
    /// </para>
    /// </remarks>
    public sealed class EcsBattleSimulation : IBattleSession, IDisposable
    {
        private const uint HashOffsetBasis = 2166136261u;
        private const uint HashPrime = 16777619u;
        private const float PositionHashScale = 1000f;

        private readonly BattleContext _context;
        private readonly EcsWorld _world;
        private readonly EcsSystems _systems;
        private readonly DeathSystem _deathSystem;
        private readonly FloorEndSystem _floorEndSystem;
        private readonly SweepSystem _sweepSystem;
        private readonly FramePublishSystem _frameSystem;
        private readonly EcsPool<Position> _positions;
        private readonly EcsPool<Health> _healths;
        private readonly EcsPool<Mana> _manas;
        private readonly EcsPool<EnemyTag> _enemies;
        private readonly EcsPool<Projectile> _projectiles;
        private readonly EcsPool<Pickup> _pickups;
        private readonly EcsPool<Caster> _casters;
        private readonly EcsPool<ContactAttack> _contacts;
        private readonly EcsPool<Status> _statuses;
        private readonly EcsPool<Lifetime> _lifetimes;

        /// <summary>Scratch for the deterministic entity walk in <see cref="ComputeStateHash"/>.</summary>
        private int[] _hashBuffer;

        private readonly int[] _remainingConsumables;

        private BattlePhase _phase = BattlePhase.Running;
        private BattleEndReason _reason = BattleEndReason.None;
        private BattleResult? _result;
        private bool _disposed;

        /// <summary>Builds and starts a session. Prefer <see cref="EcsBattleSessionFactory"/>.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public EcsBattleSimulation(
            BattleSetup setup,
            FloorConfig floor,
            IGameConfig config,
            EcsBattleOptions options,
            IRandomSource random,
            BattleDefIds defIds)
        {
            if (setup is null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (floor is null)
            {
                throw new ArgumentNullException(nameof(floor));
            }

            _world = new EcsWorld(new EcsWorld.Config
            {
                Entities = BattleArena.EntityCapacity,
                RecycledEntities = BattleArena.RecycledCapacity,
                Pools = BattleArena.PoolCapacity,
                Filters = BattleArena.FilterCapacity,
                PoolDenseSize = BattleArena.ComponentCapacity,
                PoolRecycledSize = BattleArena.RecycledCapacity,
                EntityComponentsSize = BattleArena.EntityComponentsCapacity,
            });

            _context = new BattleContext(setup, floor, config, options, random, defIds, _world);
            _context.PlayerEntity = _context.Spawner.SpawnPlayer(_context);
            BuildCasters(_context);

            _deathSystem = new DeathSystem();
            _floorEndSystem = new FloorEndSystem();
            _sweepSystem = new SweepSystem();
            _frameSystem = new FramePublishSystem();

            _systems = new EcsSystems(_world, _context);

            // The system order is the GDD's, and it is load-bearing: steering must run
            // before the grid is built, the grid before collision, collision before the
            // damage that can kill, and the frame last so it describes the finished tick.
            _systems
                .Add(new WaveSpawnSystem())
                .Add(new LoadTestSystem())
                .Add(new PlayerMoveSystem())
                .Add(new EnemySteerSystem())
                .Add(new AutoCastSystem())
                .Add(new ProjectileMoveSystem())
                .Add(new SpatialHashSystem())
                .Add(new CollisionSystem())
                .Add(new DamageStatusSystem())
                .Add(_deathSystem)
                .Add(new DropSystem())
                .Add(new PickupSystem())
                .Add(_floorEndSystem)
                .Add(_frameSystem);

            _systems.Init();

            // The sweep is not on the normal run list: a sweeping tick runs it and the frame
            // publisher instead of the fourteen systems, so nothing fights on while the drops
            // are pulled in (Stage 07c). Its Init is called here because the systems object
            // only initialises what was added to it.
            _sweepSystem.Init(_systems);

            _positions = _world.GetPool<Position>();
            _healths = _world.GetPool<Health>();
            _manas = _world.GetPool<Mana>();
            _enemies = _world.GetPool<EnemyTag>();
            _projectiles = _world.GetPool<Projectile>();
            _pickups = _world.GetPool<Pickup>();
            _casters = _world.GetPool<Caster>();
            _contacts = _world.GetPool<ContactAttack>();
            _statuses = _world.GetPool<Status>();
            _lifetimes = _world.GetPool<Lifetime>();

            // Preallocated because the result is produced inside a tick, and a tick that
            // allocates is exactly what the contract forbids.
            _remainingConsumables = new int[setup.Loadout.Consumables.Count];
            _hashBuffer = new int[BattleArena.EntityCapacity];
        }

        /// <inheritdoc />
        public BattlePhase Phase => _phase;

        /// <inheritdoc />
        public long Tick => _context.Tick;

        /// <inheritdoc />
        public IBattleFrame Frame => _context.Frame;

        /// <inheritdoc />
        public IBattleEventSource Events => _context.Events;

        /// <summary>
        /// The simulation's live state bag. Exposed for tests and for the composition root;
        /// Presentation must read <see cref="Frame"/> and <see cref="Events"/> instead.
        /// </summary>
        public BattleContext Context => _context;

        /// <inheritdoc />
        public void Step(in PlayerInputFrame input)
        {
            if (_disposed || _phase != BattlePhase.Running)
            {
                return;
            }

            _context.Input = input;
            _context.ClearTickBuffers();
            _context.Tick++;

            if (_context.SweepActive)
            {
                // A won floor that is sweeping: the world is frozen except for the drops, and
                // the frame still publishes every tick so the pickup views move and the HUD's
                // bia climbs before the fade.
                _sweepSystem.Run(_systems);
                _frameSystem.Run(_systems);
            }
            else
            {
                _systems.Run();
            }

            if (_context.EndRequested)
            {
                Finish(_context.EndPhase, _context.EndReason);
            }
        }

        /// <inheritdoc />
        public CommandResult Submit(in BattleCommand command)
        {
            if (_disposed || _phase != BattlePhase.Running)
            {
                return CommandResult.RejectedWrongPhase;
            }

            if (command.IsDevCommand && !_context.Setup.DevCheatsAllowed)
            {
                return CommandResult.RejectedNotAllowed;
            }

            switch (command.Kind)
            {
                case BattleCommandKind.Surrender:
                    Finish(BattlePhase.Aborted, BattleEndReason.Surrendered);
                    return CommandResult.Ok;

                case BattleCommandKind.DevKillAll:
                    KillAll();
                    return CommandResult.Ok;

                case BattleCommandKind.DevGodMode:
                    if (command.Arg != 0 && command.Arg != 1)
                    {
                        return CommandResult.RejectedInvalidArg;
                    }

                    _context.GodMode = command.Arg == 1;
                    return CommandResult.Ok;

                case BattleCommandKind.DevWinNow:
                    Finish(
                        BattlePhase.Won,
                        _context.Floor.IsBoss ? BattleEndReason.BossDefeated : BattleEndReason.SurvivedAllWaves);
                    return CommandResult.Ok;

                case BattleCommandKind.DevLoseNow:
                    // The HP is spent first, and only then is the floor finished. "Lose now" is the
                    // player dying, which is the one defeat the rules produce (FloorEndSystem ends
                    // the floor once HP has reached zero), so ending it with the health bar still
                    // full would hand every reader of the result — BattleResult.FinalHp, the run,
                    // and the summary screen the run ends on — a defeat that never happened.
                    KillPlayer();
                    Finish(BattlePhase.Lost, BattleEndReason.PlayerDied);
                    return CommandResult.Ok;

                case BattleCommandKind.DevAddBia:
                    if (command.Arg <= 0)
                    {
                        return CommandResult.RejectedInvalidArg;
                    }

                    DevAddBia(command.Arg);
                    return CommandResult.Ok;

                case BattleCommandKind.DevSpawnGhost:
                    DevSpawnGhost();
                    return CommandResult.Ok;

                default:
                    return CommandResult.RejectedInvalidArg;
            }
        }

        /// <inheritdoc />
        public bool TryGetResult(out BattleResult result)
        {
            result = _result!;
            return _result is not null;
        }

        /// <inheritdoc />
        public uint ComputeStateHash()
        {
            unchecked
            {
                uint hash = HashOffsetBasis;

                hash = Mix(hash, (uint)_phase);
                hash = Mix(hash, (uint)_reason);
                hash = Mix(hash, (uint)_context.Tick);
                hash = Mix(hash, (uint)_context.Kills);
                hash = Mix(hash, (uint)_context.EnemiesAlive);
                hash = Mix(hash, (uint)_context.ProjectilesAlive);
                hash = Mix(hash, (uint)_context.PickupsCollected);
                hash = Mix(hash, (uint)_context.CurrencyEarned);
                hash = Mix(hash, (uint)_context.WaveIndex);
                hash = Mix(hash, (uint)_context.BossHp);
                hash = Mix(hash, _context.BossSpawned ? 1u : 0u);
                hash = Mix(hash, _context.GodMode ? 1u : 0u);
                hash = Mix(hash, _context.EndRequested ? 1u : 0u);
                hash = Mix(hash, (uint)_context.EndPhase);
                hash = Mix(hash, (uint)_context.EndReason);

                for (int i = 0; i < _context.WaveCount; i++)
                {
                    hash = Mix(hash, (uint)_context.Waves[i].Spawned);
                    hash = Mix(hash, _context.Waves[i].Announced ? 1u : 0u);
                }

                // One pass over every live entity, in entity-index order. That order is a
                // function of the operation sequence alone — not of which pools a system
                // happened to touch — and the buffer is preallocated, so the walk allocates
                // nothing. Components an entity does not carry are simply skipped.
                int alive = _world.GetAllEntities(ref _hashBuffer);
                for (int i = 0; i < alive; i++)
                {
                    hash = MixEntity(hash, _hashBuffer[i]);
                }

                return hash;
            }
        }

        /// <summary>
        /// Releases the ECS world. Idempotent, and safe to call mid-battle: the session
        /// stops advancing and the world is gone.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _systems.Destroy();
            _world.Destroy();
        }

        // -------------------------------------------------------------------
        // internals
        // -------------------------------------------------------------------

        private void KillAll()
        {
            // Zero the ghosts' HP and let the ordinary death rule do the rest, so a dev kill
            // and a fought kill produce identical events, drops and counts.
            _context.KillAllRequested = true;
            _context.ClearTickBuffers();
            _deathSystem.Run(_systems);
            _floorEndSystem.Run(_systems);
            _frameSystem.Run(_systems);

            if (_context.EndRequested)
            {
                Finish(_context.EndPhase, _context.EndReason);
            }
        }

        /// <summary>
        /// Adds dev bia to the floor's takings.
        /// </summary>
        /// <remarks>
        /// No event: bia has no <c>PickupCollected</c> to report because no drop was collected, and
        /// the HUD reads <c>HudState.Currency</c> from the frame every tick, so the number on screen
        /// moves without one. The amount lands in <c>CurrencyEarned</c>, which is what the settlement
        /// credits into the run's purse.
        /// </remarks>
        private void DevAddBia(int amount)
        {
            _context.CurrencyEarned += amount;
            _frameSystem.Run(_systems);
        }

        /// <summary>
        /// Puts one more ghost on the field, through the same spawner a wave uses and with the
        /// current wave's own template, so a dev ghost is an ordinary ghost of this floor.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Deliberately <b>not</b> counted against the wave's <c>Spawned</c> count: a dev spawn is an
        /// extra ghost, not the wave's own ghost arriving early, and taking one out of the wave would
        /// silently shorten the floor. A field already at the contract's enemy ceiling simply gets
        /// nothing — the same ceiling a wave spawn hits — and the command still reports <c>Ok</c>.
        /// </para>
        /// <para>
        /// The ghost arrives at the arena's edge, at an angle drawn from the session's own random
        /// source, exactly as <c>WaveSpawnSystem</c> places one (AGENTS rule 6).
        /// </para>
        /// </remarks>
        private void DevSpawnGhost()
        {
            if (_context.WaveCount == 0 || _context.EnemiesAlive >= BattleArena.MaxEnemies)
            {
                return;
            }

            int wave = _context.WaveIndex;
            if (wave < 0 || wave >= _context.WaveCount)
            {
                wave = 0;
            }

            ref WaveSlot slot = ref _context.Waves[wave];

            float angle = _context.Random.NextFloat() * BattleArena.Tau;
            float radius = BattleArena.Radius * BattleArena.SpawnRadiusFactor;
            float x = (float)System.Math.Cos(angle) * radius;
            float y = (float)System.Math.Sin(angle) * radius;

            int entity = _context.Spawner.SpawnEnemy(_context, slot, x, y);
            _context.EnemiesAlive++;

            if (slot.IsBoss)
            {
                // The same bookkeeping the wave system does for a boss, so the HUD's bar and the
                // boss end condition see the ghost that was just put on the field.
                _context.BossEntity = entity;
                _context.BossMaxHp = slot.MaxHp;
                _context.BossHp = slot.MaxHp;
                _context.BossSpawned = true;
            }

            _context.Emit(
                BattleEventType.EnemySpawned,
                _context.Ref(entity),
                slot.EnemyDefId,
                0,
                BattleEventFlags.None,
                x,
                y);

            _frameSystem.Run(_systems);
        }

        private void Finish(BattlePhase phase, BattleEndReason reason)
        {
            if (_phase != BattlePhase.Running)
            {
                return;
            }

            _phase = phase;
            _reason = reason;

            IReadOnlyList<ConsumableLoadout> consumables = _context.Setup.Loadout.Consumables;
            for (int i = 0; i < _remainingConsumables.Length; i++)
            {
                // The battle never consumes an offering: the item-local effects that would
                // spend one are not modelled yet (ADR 0010), so the report is "nothing was
                // used" rather than a guess, and RunSession reconciles it.
                _remainingConsumables[i] = consumables[i].Count;
            }

            _result = new BattleResult
            {
                Outcome = phase,
                EndReason = reason,
                SurvivedTicks = _context.Tick,
                FinalHp = FinalHp(),
                CurrencyEarned = _context.CurrencyEarned,
                Kills = _context.Kills,
                RemainingConsumables = _remainingConsumables,
            };

            _context.Emit(BattleEventType.BattleEnded, default, -1, (int)reason);
        }

        /// <summary>Spends the player's HP, the way the ordinary damage path would.</summary>
        private void KillPlayer()
        {
            int player = _context.PlayerEntity;
            if (!_context.IsAlive(player))
            {
                return;
            }

            _healths.Get(player).Hp = 0;
        }

        private int FinalHp()
        {
            int player = _context.PlayerEntity;
            if (!_context.IsAlive(player))
            {
                return 0;
            }

            int hp = _healths.Get(player).Hp;
            if (hp < 0)
            {
                return 0;
            }

            return hp > _context.MaxHp ? _context.MaxHp : hp;
        }

        /// <summary>
        /// Builds one caster per loadout entry. Weapons and familiars both cast; a familiar's
        /// behaviour, element and reach come from its skill row, because the summon part of
        /// the loadout carries only what differs.
        /// </summary>
        /// <remarks>
        /// Weapons come first and familiars second, in the order the loadout lists them, and each
        /// caster keeps the board instance id of the piece it came from — that pair is what the
        /// frame's cooldown list is ordered by and keyed with (Stage 08b).
        /// </remarks>
        private void BuildCasters(BattleContext context)
        {
            IReadOnlyList<WeaponLoadout> weapons = context.Setup.Loadout.Weapons;
            int index = 0;

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponLoadout weapon = weapons[i];
                int periodTicks = Ticks(weapon.CooldownSeconds);

                context.Spawner.SpawnCaster(
                    context,
                    index++,
                    weapon.InstanceId,
                    context.DefIds.Skill(weapon.SkillId),
                    periodTicks,
                    weapon.MpCost,
                    weapon.Damage,
                    (int)weapon.Element,
                    (int)weapon.Behavior,
                    weapon.Range,
                    weapon.ProjectileSpeed,
                    weapon.PierceCount,
                    weapon.ProjectileCount);
            }

            IReadOnlyList<SummonLoadout> summons = context.Setup.Loadout.Summons;
            for (int i = 0; i < summons.Count; i++)
            {
                SummonLoadout summon = summons[i];
                if (string.IsNullOrEmpty(summon.SkillId)
                    || !context.Config.TryGetSkill(summon.SkillId, out SkillConfig skill))
                {
                    continue;
                }

                context.Spawner.SpawnCaster(
                    context,
                    index++,
                    summon.InstanceId,
                    context.DefIds.Skill(summon.SkillId),
                    Ticks(summon.CooldownSeconds),
                    summon.MpCost,
                    summon.Damage,
                    (int)skill.Element,
                    (int)skill.Behavior,
                    skill.Range,
                    skill.ProjectileSpeed,
                    skill.PierceCount,
                    skill.ProjectileCount);
            }
        }

        private static int Ticks(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 1;
            }

            int ticks = (int)Math.Round(seconds * BattleConstants.TickRate);
            return ticks < 1 ? 1 : ticks;
        }

        private uint MixEntity(uint hash, int entity)
        {
            if (_context.IsAlive(entity))
            {
                hash = Mix(hash, (uint)entity);

                if (_positions.Has(entity))
                {
                    Position position = _positions.Get(entity);
                    hash = Mix(hash, (uint)(int)Math.Round(position.X * PositionHashScale));
                    hash = Mix(hash, (uint)(int)Math.Round(position.Y * PositionHashScale));
                }

                if (_healths.Has(entity))
                {
                    Health health = _healths.Get(entity);
                    hash = Mix(hash, (uint)health.Hp);
                    hash = Mix(hash, (uint)health.MaxHp);
                }

                if (_enemies.Has(entity))
                {
                    EnemyTag tag = _enemies.Get(entity);
                    hash = Mix(hash, (uint)tag.DefId);
                }

                if (_contacts.Has(entity))
                {
                    hash = Mix(hash, (uint)_contacts.Get(entity).RemainingTicks);
                }

                if (_casters.Has(entity))
                {
                    hash = Mix(hash, (uint)_casters.Get(entity).CooldownTicks);
                }

                if (_projectiles.Has(entity))
                {
                    Projectile projectile = _projectiles.Get(entity);
                    hash = Mix(hash, (uint)projectile.DefId);
                    hash = Mix(hash, (uint)projectile.HitsLeft);
                }

                if (_lifetimes.Has(entity))
                {
                    hash = Mix(hash, (uint)_lifetimes.Get(entity).RemainingTicks);
                }

                if (_statuses.Has(entity))
                {
                    Status status = _statuses.Get(entity);
                    hash = Mix(hash, (uint)status.Element);
                    hash = Mix(hash, (uint)status.RemainingTicks);
                }

                if (_pickups.Has(entity))
                {
                    hash = Mix(hash, (uint)_pickups.Get(entity).Kind);
                }

                if (_manas.Has(entity))
                {
                    Mana mana = _manas.Get(entity);
                    hash = Mix(hash, (uint)mana.Mp);
                }
            }

            return hash;
        }

        private static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value & 0xFFu;
                hash *= HashPrime;
                hash ^= (value >> 8) & 0xFFu;
                hash *= HashPrime;
                hash ^= (value >> 16) & 0xFFu;
                hash *= HashPrime;
                hash ^= (value >> 24) & 0xFFu;
                hash *= HashPrime;
                return hash;
            }
        }
    }
}
