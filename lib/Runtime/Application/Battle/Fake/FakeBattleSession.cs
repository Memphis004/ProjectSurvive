using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Battle.Fake
{
    /// <summary>
    /// A hand-written <see cref="IBattleSession"/> that satisfies the battle
    /// contract without EcsLite, so Stage 03–06 can run a full loop (and the
    /// contract tests have something to run) before the real simulation exists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It obeys the same rules as the real thing: fixed ticks, duration and win
    /// condition read from <see cref="FloorConfig"/>, random only through
    /// <see cref="IRandomSource"/>, events only through the ring, no allocation
    /// once running. It does not fight: enemies walk in a circle, do no damage,
    /// and die of old age — everything that matters for the <em>contract</em>
    /// (phases, results, events, determinism) is real, and everything about
    /// combat is absent.
    /// </para>
    /// <para>
    /// The geometric constants below are fake presentation values, not balance
    /// numbers: the player is at the origin and the dummies orbit it. No gameplay
    /// value is invented here — HP, MP, duration and boss presence all come from
    /// config. The cooldown frame (Stage 08b) follows the same rule: the periods come
    /// from the loadout's resolved cooldowns and only the <i>progress</i> through them
    /// is the fake's own predictable cycle, because a fake that had to fight to show a
    /// cooldown would be a second battle instead of a stand-in.
    /// </para>
    /// </remarks>
    public sealed class FakeBattleSession : IBattleSession
    {
        private const float OrbitRadiusBase = 3f;
        private const float OrbitRadiusStep = 1f;
        private const float OrbitAngularSpeed = 0.02f;
        private const float Tau = 6.2831855f;

        /// <summary>
        /// The fake fights nothing, so it earns nothing on its own: this is what a floor's takings
        /// are before any dev command, and Run owns the economy (GDD v0.3).
        /// </summary>
        private const int FakeCurrencyEarned = 0;

        private readonly BattleSetup _setup;
        private readonly FloorConfig _floor;
        private readonly FakeBattleOptions _options;
        private readonly IRandomSource _random;
        private readonly BattleEventRing _events;
        private readonly BattleFrameBuffer _frame;

        private readonly int _floorDurationTicks;
        private readonly int _maxHp;
        private readonly int _maxMp;

        // One slot per loadout entry, in loadout order (weapons, then familiars).
        private readonly CastSlot[] _casters;
        private readonly int _casterCount;

        // Dummy entity state, in fixed arrays so nothing is allocated per tick.
        private readonly int _entityCount;
        private readonly float[] _entityX;
        private readonly float[] _entityY;
        private readonly float[] _entityPhase;
        private readonly float[] _entityRadius;
        private readonly int[] _entityLife;
        private readonly int[] _entityVersion;
        private readonly bool[] _entityIsBoss;
        private readonly int _bossMaxHp;

        private BattlePhase _phase = BattlePhase.Running;
        private BattleEndReason _reason = BattleEndReason.None;
        private long _tick;
        private int _hp;
        private int _mp;
        private int _kills;

        /// <summary>
        /// Bia earned this floor. Only a dev command can raise it in the fake — the dummies drop
        /// nothing — but it takes the same path as a real drop, so the HUD and the settlement see
        /// one number either way.
        /// </summary>
        private int _currencyEarned = FakeCurrencyEarned;

        private int _waveIndex;
        private int _bossHp;
        private bool _godMode;
        private float _damageCarry;
        private bool _waveStarted;
        private bool _warned15;
        private bool _warned5;
        private BattleResult? _result;

        /// <summary>Creates a fake session. Prefer <see cref="FakeBattleSessionFactory"/>.</summary>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="FakeBattleOptions.EntityLifetimeTicks"/> is below 1.</exception>
        public FakeBattleSession(
            BattleSetup setup, FloorConfig floor, FakeBattleOptions options, IRandomSource random, int bossMaxHp = 0)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            _floor = floor ?? throw new ArgumentNullException(nameof(floor));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            if (options.EntityLifetimeTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "EntityLifetimeTicks must be at least 1.");
            }

            _events = new BattleEventRing(BattleConstants.EventRingCapacity);
            _frame = new BattleFrameBuffer();
            _floorDurationTicks = floor.DurationSeconds > 0f
                ? (int)Math.Round(floor.DurationSeconds * BattleConstants.TickRate)
                : 0;

            _maxHp = setup.Loadout.Stats.MaxHp;
            if (_maxHp <= 0)
            {
                _maxHp = setup.CurrentHp;
            }

            _maxMp = setup.Loadout.Stats.MaxMp;
            _hp = Clamp(setup.CurrentHp, 0, _maxHp);
            _mp = _maxMp;

            _casters = BuildCasters(setup.Loadout, out _casterCount);

            // One slot is reserved for the player render entity, so the frame can
            // never exceed the contract's MaxRenderEntities.
            int requested = options.DummyEntityCount;
            int cap = BattleConstants.MaxRenderEntities - 1;
            _entityCount = requested < 0 ? 0 : (requested > cap ? cap : requested);

            _entityX = new float[_entityCount];
            _entityY = new float[_entityCount];
            _entityPhase = new float[_entityCount];
            _entityRadius = new float[_entityCount];
            _entityLife = new int[_entityCount];
            _entityVersion = new int[_entityCount];
            _entityIsBoss = new bool[_entityCount];

            for (int i = 0; i < _entityCount; i++)
            {
                _entityVersion[i] = 1;
                _entityRadius[i] = OrbitRadiusBase + ((i % 5) * OrbitRadiusStep);
                _entityPhase[i] = i * (Tau / Math.Max(1, _entityCount));
                _entityLife[i] = 1 + ((i * options.EntityLifetimeTicks) / Math.Max(1, _entityCount));
                _entityIsBoss[i] = floor.IsBoss && i == 0;
            }

            // Boss HP comes from the enemy table via the factory; when the factory
            // has none it falls back to the player's max HP so the HUD still has a
            // meaningful bar and a boss floor does not instantly "win".
            _bossMaxHp = floor.IsBoss ? (bossMaxHp > 0 ? bossMaxHp : _maxHp) : 0;
            _bossHp = _bossMaxHp;

            EvaluatePositions();
            BuildFrame();
        }

        /// <inheritdoc />
        public BattlePhase Phase => _phase;

        /// <inheritdoc />
        public long Tick => _tick;

        /// <inheritdoc />
        public IBattleFrame Frame => _frame;

        /// <inheritdoc />
        public IBattleEventSource Events => _events;

        /// <summary>True while the session still runs (the <see cref="IBattleEventSource"/> equivalent for the fake).</summary>
        public bool IsRunning => _phase == BattlePhase.Running;

        /// <inheritdoc />
        public void Step(in PlayerInputFrame input)
        {
            if (_phase != BattlePhase.Running)
            {
                return;
            }

            _tick++;

            EmitWaveStart();
            AdvanceEntities();
            ApplyDamage();
            EmitTimeWarnings();
            EvaluateEnd();
            AdvanceCooldowns();
            BuildFrame();
        }

        /// <inheritdoc />
        public CommandResult Submit(in BattleCommand command)
        {
            if (_phase != BattlePhase.Running)
            {
                return CommandResult.RejectedWrongPhase;
            }

            if (command.IsDevCommand && !_setup.DevCheatsAllowed)
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

                    _godMode = command.Arg == 1;
                    return CommandResult.Ok;

                case BattleCommandKind.DevWinNow:
                    Finish(BattlePhase.Won, _floor.IsBoss ? BattleEndReason.BossDefeated : BattleEndReason.SurvivedAllWaves);
                    return CommandResult.Ok;

                case BattleCommandKind.DevLoseNow:
                    // The health goes first, so the loss this reports is the loss the rules report
                    // (PlayerDied with FinalHp 0), matching both the real session and the fake's
                    // own death path in EvaluateEnd. A shortcut that leaves the player at full
                    // health hands the run, and the summary screen after it, a defeat nobody could
                    // have reached.
                    _hp = 0;
                    Finish(BattlePhase.Lost, BattleEndReason.PlayerDied);
                    return CommandResult.Ok;

                case BattleCommandKind.DevAddBia:
                    if (command.Arg <= 0)
                    {
                        return CommandResult.RejectedInvalidArg;
                    }

                    _currencyEarned += command.Arg;
                    BuildFrame();
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
        /// <summary>
        /// Releases the session. The fake owns no unmanaged state — the contract's ring and
        /// frame buffer are plain managed objects — so this exists because
        /// <see cref="IBattleSession"/> requires it and because a caller must be able to treat
        /// the fake and the EcsLite session identically (which is the point of the fake).
        /// </summary>
        public void Dispose()
        {
            // Nothing to release: the fake allocates on the GC heap only.
        }

        public uint ComputeStateHash()
        {
            uint hash = 2166136261u;
            hash = Mix(hash, (uint)_phase);
            hash = Mix(hash, (uint)_reason);
            hash = Mix(hash, (uint)_tick);
            hash = Mix(hash, (uint)_hp);
            hash = Mix(hash, (uint)_mp);
            hash = Mix(hash, (uint)_kills);
            hash = Mix(hash, (uint)_waveIndex);
            hash = Mix(hash, (uint)_bossHp);
            hash = Mix(hash, _godMode ? 1u : 0u);

            for (int i = 0; i < _entityCount; i++)
            {
                hash = Mix(hash, (uint)_entityVersion[i]);
                hash = Mix(hash, (uint)_entityLife[i]);
                hash = Mix(hash, (uint)(int)Math.Round(_entityPhase[i] * 10000f));
            }

            return hash;
        }

        // -------------------------------------------------------------------
        // tick body
        // -------------------------------------------------------------------

        private void EmitWaveStart()
        {
            if (_waveStarted)
            {
                return;
            }

            _waveStarted = true;
            _waveIndex = 0;
            _events.Write(new BattleEvent(_events.NextSeq, BattleEventType.WaveStarted, default, -1, _waveIndex));
        }

        private void AdvanceEntities()
        {
            int lifetime = _options.EntityLifetimeTicks;
            for (int i = 0; i < _entityCount; i++)
            {
                _entityPhase[i] += OrbitAngularSpeed;
                if (_entityPhase[i] > Tau)
                {
                    _entityPhase[i] -= Tau;
                }

                if (_entityIsBoss[i])
                {
                    continue;
                }

                _entityLife[i]--;
                if (_entityLife[i] > 0)
                {
                    continue;
                }

                // Recycle: die and respawn on the same tick, both reported.
                int index = i + 1;
                _kills++;
                _events.Write(new BattleEvent(
                    _events.NextSeq, BattleEventType.EnemyKilled, new EntityRef(index, _entityVersion[i]), -1, _kills));

                _entityVersion[i]++;
                _entityLife[i] = lifetime;
                _entityPhase[i] = _random.NextFloat() * Tau;
                _events.Write(new BattleEvent(
                    _events.NextSeq, BattleEventType.EnemySpawned, new EntityRef(index, _entityVersion[i]), -1, 0));
            }

            EvaluatePositions();
        }

        private void ApplyDamage()
        {
            int damagePerSecond = _options.DamagePerSecond;
            if (damagePerSecond <= 0)
            {
                return;
            }

            _damageCarry += damagePerSecond / (float)BattleConstants.TickRate;
            int damage = (int)_damageCarry;
            if (damage <= 0)
            {
                return;
            }

            _damageCarry -= damage;

            if (_godMode)
            {
                return;
            }

            _hp -= damage;
            if (_hp < 0)
            {
                _hp = 0;
            }

            _events.Write(new BattleEvent(
                _events.NextSeq, BattleEventType.PlayerDamaged, new EntityRef(0, 1), -1, damage));
        }

        private void EmitTimeWarnings()
        {
            int rate = BattleConstants.TickRate;
            int remainingTicks = _floorDurationTicks - (int)_tick;
            if (!_warned15 && remainingTicks <= 15 * rate)
            {
                _warned15 = true;
                _events.Write(new BattleEvent(_events.NextSeq, BattleEventType.TimeWarning, default, -1, 15));
            }

            if (!_warned5 && remainingTicks <= 5 * rate)
            {
                _warned5 = true;
                _events.Write(new BattleEvent(_events.NextSeq, BattleEventType.TimeWarning, default, -1, 5));
            }
        }

        private void EvaluateEnd()
        {
            if (_hp <= 0)
            {
                Finish(BattlePhase.Lost, BattleEndReason.PlayerDied);
                return;
            }

            if (_options.LoseAtTick > 0 && _tick >= _options.LoseAtTick)
            {
                Finish(BattlePhase.Lost, BattleEndReason.PlayerDied);
                return;
            }

            if (_options.WinAtTick > 0 && _tick >= _options.WinAtTick)
            {
                Finish(BattlePhase.Won, _floor.IsBoss ? BattleEndReason.BossDefeated : BattleEndReason.SurvivedAllWaves);
                return;
            }

            if (_options.TimeoutAtTick > 0 && _tick >= _options.TimeoutAtTick)
            {
                Finish(BattlePhase.Lost, BattleEndReason.Timeout);
                return;
            }

            if (_floor.IsBoss)
            {
                if (_bossHp <= 0)
                {
                    Finish(BattlePhase.Won, BattleEndReason.BossDefeated);
                    return;
                }

                if (_floorDurationTicks > 0 && _tick >= _floorDurationTicks)
                {
                    Finish(BattlePhase.Lost, BattleEndReason.Timeout);
                }

                return;
            }

            if (_floorDurationTicks > 0 && _tick >= _floorDurationTicks)
            {
                Finish(BattlePhase.Won, BattleEndReason.SurvivedAllWaves);
            }
        }

        private void KillAll()
        {
            for (int i = 0; i < _entityCount; i++)
            {
                if (_entityIsBoss[i])
                {
                    continue;
                }

                _kills++;
                _events.Write(new BattleEvent(
                    _events.NextSeq, BattleEventType.EnemyKilled, new EntityRef(i + 1, _entityVersion[i]), -1, _kills));
            }

            if (_floor.IsBoss && _bossHp > 0)
            {
                _bossHp = 0;
                _kills++;
                _events.Write(new BattleEvent(
                    _events.NextSeq, BattleEventType.EnemyKilled, new EntityRef(1, 1), -1, _kills, BattleEventFlags.Boss));
            }

            EvaluateEnd();
            BuildFrame();
        }

        /// <summary>
        /// Re-enters one dummy, which is the fake's whole vocabulary for "a ghost arrives".
        /// </summary>
        /// <remarks>
        /// The fake's field is a fixed orbit of dummies that recycle forever
        /// (<see cref="AdvanceEntities"/> kills one and spawns its next version on the same tick), so
        /// there is no free slot to fill and no ceiling to hit. A dev spawn therefore does what the
        /// recycle does: the first non-boss dummy's version goes up, its phase is drawn from the
        /// session's own random source, and an <c>EnemySpawned</c> event is written — so whatever
        /// binds a real spawn binds this one, and the frame and the state hash move with it.
        /// </remarks>
        private void DevSpawnGhost()
        {
            if (_entityCount == 0)
            {
                return;
            }

            int index = 0;
            while (index < _entityCount && _entityIsBoss[index])
            {
                index++;
            }

            if (index >= _entityCount)
            {
                // Only the boss is on the field; it is already the one ghost this floor has.
                index = 0;
            }

            _entityVersion[index]++;
            _entityLife[index] = _options.EntityLifetimeTicks;
            _entityPhase[index] = _random.NextFloat() * Tau;

            _events.Write(new BattleEvent(
                _events.NextSeq,
                BattleEventType.EnemySpawned,
                new EntityRef(index + 1, _entityVersion[index]),
                -1,
                0,
                _entityIsBoss[index] ? BattleEventFlags.Boss : BattleEventFlags.None));

            EvaluatePositions();
            BuildFrame();
        }

        private void Finish(BattlePhase phase, BattleEndReason reason)
        {
            if (_phase != BattlePhase.Running)
            {
                return;
            }

            _phase = phase;
            _reason = reason;

            // Remaining uses, like the real session (Stage 11a). The fake never spends one
            // (it has no auto-use rule), so every entry reports its full stack.
            int[] remaining = new int[_setup.Loadout.Consumables.Count];
            for (int i = 0; i < remaining.Length; i++)
            {
                ConsumableLoadout entry = _setup.Loadout.Consumables[i];
                int perItem = entry.UsesPerItem > 0 ? entry.UsesPerItem : 1;
                remaining[i] = (entry.Count > 0 ? entry.Count : 0) * perItem;
            }

            _result = new BattleResult
            {
                Outcome = phase,
                EndReason = reason,
                SurvivedTicks = _tick,
                FinalHp = Clamp(_hp, 0, _maxHp),
                CurrencyEarned = _currencyEarned,
                Kills = _kills,
                RemainingConsumables = remaining,
            };

            _events.Write(new BattleEvent(
                _events.NextSeq, BattleEventType.BattleEnded, default, -1, (int)reason));
        }

        // -------------------------------------------------------------------
        // frame
        // -------------------------------------------------------------------

        private void EvaluatePositions()
        {
            for (int i = 0; i < _entityCount; i++)
            {
                _entityX[i] = _entityRadius[i] * (float)Math.Cos(_entityPhase[i]);
                _entityY[i] = _entityRadius[i] * (float)Math.Sin(_entityPhase[i]);
            }
        }

        /// <summary>
        /// Builds one casting slot per loadout entry, in loadout order, capped at the contract's
        /// maximum (<see cref="BattleConstants.MaxLoadoutEntries"/>).
        /// </summary>
        /// <remarks>
        /// The fake does not fight, so these are <b>manufactured</b> clocks rather than a second
        /// implementation of auto-cast: every entry starts ready, runs down one tick per tick, and
        /// resets when it could cast. The numbers are entirely predictable — period straight from the
        /// loadout's resolved cooldown in seconds — which is what makes the fake usable to develop a
        /// HUD against (a cooldown indicator on a board item) before the real floor is tuned.
        /// </remarks>
        private static CastSlot[] BuildCasters(CombatLoadoutSnapshot loadout, out int count)
        {
            int total = loadout.Weapons.Count + loadout.Summons.Count;
            count = total > BattleConstants.MaxLoadoutEntries ? BattleConstants.MaxLoadoutEntries : total;

            var slots = new CastSlot[count];
            int index = 0;

            for (int i = 0; i < loadout.Weapons.Count && index < count; i++, index++)
            {
                WeaponLoadout weapon = loadout.Weapons[i];
                slots[index] = new CastSlot
                {
                    InstanceId = weapon.InstanceId,
                    PeriodTicks = Ticks(weapon.CooldownSeconds),
                    MpCost = weapon.MpCost < 0 ? 0 : weapon.MpCost,
                    RemainingTicks = 0,
                };
            }

            for (int i = 0; i < loadout.Summons.Count && index < count; i++, index++)
            {
                SummonLoadout summon = loadout.Summons[i];
                slots[index] = new CastSlot
                {
                    InstanceId = summon.InstanceId,
                    PeriodTicks = Ticks(summon.CooldownSeconds),
                    MpCost = summon.MpCost < 0 ? 0 : summon.MpCost,
                    RemainingTicks = 0,
                };
            }

            return slots;
        }

        /// <summary>
        /// Runs every manufactured clock down one tick, and resets the ones that could cast — the same
        /// shape the real auto-cast produces (ready, then a reset), gated by MP the same way, so a HUD
        /// developed against the fake sees a state the real session also reaches.
        /// </summary>
        private void AdvanceCooldowns()
        {
            for (int i = 0; i < _casterCount; i++)
            {
                ref CastSlot slot = ref _casters[i];

                if (slot.RemainingTicks > 0)
                {
                    slot.RemainingTicks--;
                }

                if (slot.RemainingTicks <= 0 && slot.MpCost <= _mp)
                {
                    slot.RemainingTicks = slot.PeriodTicks;
                }
            }
        }

        private void BuildFrame()
        {
            _frame.BeginFrame(_tick);
            _frame.SetHud(BuildHud());

            _frame.Add(new RenderEntity(new EntityRef(0, 1), RenderEntityKind.Player, 0f, 0f));

            for (int i = 0; i < _entityCount; i++)
            {
                bool isBoss = _entityIsBoss[i];
                RenderEntityFlags flags = isBoss ? RenderEntityFlags.Boss : RenderEntityFlags.None;
                float health = isBoss && _bossMaxHp > 0 ? _bossHp / (float)_bossMaxHp : 1f;
                _frame.Add(new RenderEntity(
                    new EntityRef(i + 1, _entityVersion[i]),
                    RenderEntityKind.Enemy,
                    _entityX[i],
                    _entityY[i],
                    (float)(_entityPhase[i] * 57.29578f),
                    1f,
                    health,
                    flags));
            }

            for (int i = 0; i < _casterCount; i++)
            {
                ref CastSlot slot = ref _casters[i];

                LoadoutCooldownFlags coolFlags = LoadoutCooldownFlags.None;
                if (slot.RemainingTicks <= 0 && slot.MpCost > _mp)
                {
                    coolFlags = LoadoutCooldownFlags.ManaShort;
                }

                _frame.AddCooldown(new LoadoutCooldown(
                    slot.InstanceId, slot.RemainingTicks, slot.PeriodTicks, coolFlags));
            }

            // Swap Prev/Cur: this tick becomes the current frame and the previous
            // one stays readable for interpolation. Publishing here (rather than
            // per Add) is what keeps a half-built frame invisible to a reader.
            _frame.Publish();
        }

        private HudState BuildHud()
        {
            return new HudState
            {
                Hp = _hp,
                MaxHp = _maxHp,
                Mp = _mp,
                MaxMp = _maxMp,
                Currency = _currencyEarned,
                Kills = _kills,
                ElapsedTicks = (int)_tick,
                FloorDurationTicks = _floorDurationTicks,
                WaveIndex = _waveIndex,
                BossHp = _bossHp,
                BossMaxHp = _bossMaxHp,
                HasBoss = _floor.IsBoss,
            };
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
        /// A resolved cooldown in seconds as ticks, rounded the way the real session rounds it
        /// (<see cref="BattleConstants.TickRate"/>), never below one tick. The fake mirrors the
        /// simulation's arithmetic the same way it mirrors the floor duration.
        /// </summary>
        private static int Ticks(float seconds)
        {
            if (!(seconds > 0f))
            {
                return 1;
            }

            int ticks = (int)Math.Round(seconds * BattleConstants.TickRate);
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>One loadout entry's manufactured casting clock (see <see cref="BuildCasters"/>).</summary>
        private struct CastSlot
        {
            /// <summary>Board instance id of the piece the entry came from, or 0.</summary>
            public int InstanceId;

            /// <summary>Cooldown length in ticks.</summary>
            public int PeriodTicks;

            /// <summary>MP one cast costs, from the loadout.</summary>
            public int MpCost;

            /// <summary>Ticks left before it may cast again; 0 means ready.</summary>
            public int RemainingTicks;
        }

        private static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value & 0xFFu;
                hash *= 16777619u;
                hash ^= (value >> 8) & 0xFFu;
                hash *= 16777619u;
                hash ^= (value >> 16) & 0xFFu;
                hash *= 16777619u;
                hash ^= (value >> 24) & 0xFFu;
                hash *= 16777619u;
                return hash;
            }
        }
    }
}
