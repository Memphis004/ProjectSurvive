using System;
using System.Collections.Generic;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Simulation.Battle;
using ProjectSurvive.Core.Simulation.Battle.Systems;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The Stage 11c acceptance rules for Pob: two phases that turn at the row's HP percent,
    /// a possession that reverses the player's movement for the row's duration and gives it
    /// back, and corpses that heal the boss without ever going over its max.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The boss is placed by hand a couple of units from the player, because the floor's own
    /// wave puts it on the arena ring and it would spend half a minute walking into reach; it
    /// goes through <see cref="BattleSpawner"/> with the same <see cref="WaveSlot"/> a wave
    /// resolves, so the path under test is the shipped one.
    /// </para>
    /// <para>
    /// Every number the assertions use is read from the enemy row, so a balance pass in
    /// <c>data/tables/enemy.csv</c> moves the test with it instead of breaking it.
    /// </para>
    /// </remarks>
    public sealed class BossBehaviourTests
    {
        private const string Forest1 = "floor.forest_1";
        private const string BossFloor = "floor.forest_5";
        private const string Pob = "enemy.pob";
        private const string Spirit = "enemy.wandering_spirit";

        [Fact]
        public void Phase_two_begins_at_the_rows_hp_percent_and_is_announced_once()
        {
            EcsBattleSimulation simulation = Create(Forest1);
            IGameConfig config = simulation.Context.Config;
            EnemyConfig row = ConfigFixture.Enemy(config, Pob);

            int boss = SpawnBoss(simulation, config, x: 3f, y: 0f);
            EcsPool<Health> healths = simulation.Context.World.GetPool<Health>();

            int threshold = (int)Math.Round(row.MaxHp * (row.BossPhase2HpPercent / 100f));
            Assert.True(threshold > 0, "the row must set a phase-2 threshold");

            var recorder = new Recorder(simulation);

            // One HP above the threshold: still phase 1, and nothing announced.
            healths.Get(boss).Hp = threshold + 1;
            recorder.Step(PlayerInputFrame.Idle);
            Assert.Equal(1, simulation.Context.World.GetPool<BossMind>().Get(boss).Phase);
            Assert.Equal(0, recorder.Count(BattleEventType.BossPhaseChanged));

            // At the threshold: phase 2, announced with the new phase number and the boss tag.
            healths.Get(boss).Hp = threshold;
            recorder.Step(PlayerInputFrame.Idle);

            Assert.Equal(2, simulation.Context.World.GetPool<BossMind>().Get(boss).Phase);

            Recorded changed = recorder.First(BattleEventType.BossPhaseChanged);
            Assert.True(changed.Tick >= 0, "the boss never announced its second phase");
            Assert.Equal(2, changed.Event.Value);
            Assert.Equal(simulation.Context.DefIds.Enemy(Pob), changed.Event.DefId);
            Assert.True((changed.Event.Flags & BattleEventFlags.Boss) != 0, "the event must carry the boss tag");

            // It never changes again — not after more damage, and not after a meal.
            healths.Get(boss).Hp = 1;
            for (int i = 0; i < 30; i++)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(1, recorder.Count(BattleEventType.BossPhaseChanged));
        }

        [Fact]
        public void A_possession_warns_reverses_the_input_for_the_rows_ticks_and_gives_it_back()
        {
            EcsBattleSimulation simulation = Create(Forest1);
            IGameConfig config = simulation.Context.Config;
            EnemyConfig row = ConfigFixture.Enemy(config, Pob);

            int boss = SpawnBoss(simulation, config, x: 3f, y: 0f);

            // The recorder starts before the phase turns, so every event is labelled with the
            // tick it was published on rather than the tick that happened to read it.
            var recorder = new Recorder(simulation);
            EnterPhaseTwo(simulation, boss, recorder);

            int warnTicks = Ticks(row.BossPossessTelegraphSeconds);
            int flipTicks = Ticks(row.BossPossessSeconds);
            Assert.True(warnTicks > 0, "the row must warn before a possession");
            Assert.True(flipTicks > 0, "the row must say how long the reversal lasts");

            // The player holds still until the reversal lands, then pushes +X every tick, so
            // "which way did the body go" answers "which way did the input point".
            PlayerInputFrame pushRight = PlayerInputFrame.FromAxis(1f, 0f);

            int guard = 0;
            while (simulation.Frame.Hud.ControlFlipTicks == 0 && guard++ < 200)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            Assert.True(
                simulation.Frame.Hud.ControlFlipTicks > 0,
                "the boss never possessed the player inside " + warnTicks + " ticks of warning");

            float atLanding = PlayerPosition(simulation).X;

            while (simulation.Frame.Hud.ControlFlipTicks > 0 && guard++ < 400)
            {
                recorder.Step(pushRight);
            }

            Recorded started = recorder.First(BattleEventType.TelegraphStarted);
            Recorded ended = recorder.First(BattleEventType.TelegraphEnded);

            Assert.True(started.Tick >= 0, "the boss never warned before it possessed");
            Assert.True(ended.Tick >= 0, "the warning never closed");
            Assert.True(
                ended.Tick - started.Tick >= warnTicks,
                "the possession landed " + (ended.Tick - started.Tick) + " ticks after the warning; the row asks for "
                + warnTicks);

            // The warning carries the reach the possession really uses.
            Assert.Equal((int)TelegraphShape.Circle, started.Event.Value);
            Assert.Equal(row.BossPossessRange, started.Event.Radius, 0.001f);
            Assert.Equal(simulation.Context.DefIds.Enemy(Pob), started.Event.DefId);

            int flippedTicks = recorder.CountFlipped();
            Assert.True(flippedTicks > 0, "the player was never possessed");
            Assert.Equal(flipTicks, flippedTicks);

            // While reversed, pushing right walks left; once it is over, the same push walks right.
            Assert.True(
                PlayerPosition(simulation).X < atLanding,
                "the reversal did not turn the input around: x went " + atLanding + " -> "
                + PlayerPosition(simulation).X);

            float beforeRelease = PlayerPosition(simulation).X;
            for (int i = 0; i < 15; i++)
            {
                recorder.Step(pushRight);
            }

            Assert.True(
                PlayerPosition(simulation).X > beforeRelease,
                "the controls did not come back: x went " + beforeRelease + " -> "
                + PlayerPosition(simulation).X);

            // And the HUD says so: the frame that says "flipped" is the frame the movement used.
            Assert.Equal(0, simulation.Frame.Hud.ControlFlipTicks);
        }

        [Fact]
        public void A_corpse_heals_the_boss_without_ever_passing_its_max()
        {
            EcsBattleSimulation simulation = Create(Forest1);
            IGameConfig config = simulation.Context.Config;
            EnemyConfig row = ConfigFixture.Enemy(config, Pob);

            int boss = SpawnBoss(simulation, config, x: 3f, y: 0f);
            EcsPool<Health> healths = simulation.Context.World.GetPool<Health>();

            Assert.True(row.BossCorpseHeal > 0, "Pob must be able to eat a corpse");
            Assert.True(row.BossCorpseRange > 0f, "Pob must say how close a corpse has to fall");

            var recorder = new Recorder(simulation);

            // Half dead, with a corpse beside it: the meal pays the row's amount.
            healths.Get(boss).Hp = row.MaxHp / 2;
            int hpBefore = healths.Get(boss).Hp;

            KillBeside(simulation, config, x: 3.5f, y: 0f);
            recorder.Step(PlayerInputFrame.Idle);

            Recorded meal = recorder.Last(BattleEventType.CorpseConsumed);
            Assert.True(meal.Tick >= 0, "the boss did not eat the corpse beside it");
            Assert.Equal(row.BossCorpseHeal, meal.Event.Value);
            Assert.Equal(simulation.Context.DefIds.Enemy(Pob), meal.Event.DefId);
            Assert.Equal(hpBefore + row.BossCorpseHeal, healths.Get(boss).Hp);

            // One HP short of full: the meal is clipped to the max, and the event says so.
            healths.Get(boss).Hp = row.MaxHp - 1;
            KillBeside(simulation, config, x: 2.5f, y: 0f);
            recorder.Step(PlayerInputFrame.Idle);

            Recorded clipped = recorder.Last(BattleEventType.CorpseConsumed);
            Assert.Equal(2, recorder.Count(BattleEventType.CorpseConsumed));
            Assert.Equal(1, clipped.Event.Value);
            Assert.Equal(row.MaxHp, healths.Get(boss).Hp);

            // Full: nothing is eaten and nothing is claimed.
            KillBeside(simulation, config, x: 2.5f, y: 0f);
            recorder.Step(PlayerInputFrame.Idle);
            Assert.Equal(2, recorder.Count(BattleEventType.CorpseConsumed));
            Assert.Equal(row.MaxHp, healths.Get(boss).Hp);

            // Out of reach: no meal on the other side of the arena.
            healths.Get(boss).Hp = row.MaxHp / 2;
            KillBeside(simulation, config, x: 3f + row.BossCorpseRange + 2f, y: 0f);
            recorder.Step(PlayerInputFrame.Idle);
            Assert.Equal(2, recorder.Count(BattleEventType.CorpseConsumed));
        }

        /// <summary>
        /// A boss pays the same drop its row says, through the same drop queue every ghost
        /// uses, so the end-of-floor sweep of Stage 07c is the thing that collects it; and the
        /// possession it was holding ends with it rather than outliving the ghost.
        /// </summary>
        [Fact]
        public void A_dead_boss_drops_the_rows_bia_and_releases_the_possession()
        {
            EcsBattleSimulation simulation = Create(Forest1);
            IGameConfig config = simulation.Context.Config;
            EnemyConfig row = ConfigFixture.Enemy(config, Pob);

            Assert.True(row.DropValue > 0, "Pob must say what its death is worth");

            int boss = SpawnBoss(simulation, config, x: 3f, y: 0f);
            var recorder = new Recorder(simulation);
            EnterPhaseTwo(simulation, boss, recorder);

            int guard = 0;
            while (simulation.Frame.Hud.ControlFlipTicks == 0 && guard++ < 200)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            Assert.True(simulation.Frame.Hud.ControlFlipTicks > 0, "the boss never possessed the player");

            int earnedBefore = simulation.Context.CurrencyEarned;
            int droppedBefore = PickupValueSum(simulation);

            simulation.Context.World.GetPool<Health>().Get(boss).Hp = 0;
            recorder.Step(PlayerInputFrame.Idle);

            // Paid once, at the death, on the ordinary drop queue.
            int paid = (simulation.Context.CurrencyEarned - earnedBefore)
                + PickupValueSum(simulation) - droppedBefore;
            Assert.Equal(row.DropValue, paid);
            Assert.Equal(-1, simulation.Context.BossEntity);

            // The reversal dies with the ghost that cast it.
            Assert.Equal(0, simulation.Frame.Hud.ControlFlipTicks);
            Assert.True(
                recorder.CountFlipped() > 0
                    && recorder.CountFlipped() < Ticks(row.BossPossessSeconds) + 5,
                "the possession outlived the boss: " + recorder.CountFlipped() + " flipped ticks");
        }

        [Fact]
        public void A_boss_floor_with_a_possession_replays_to_the_same_hash()
        {
            IGameConfig config = ConfigFixture.Load();

            // Phase 2 is lowered to "almost full HP" for this scenario only. The replay harness
            // records Step and nothing else, so it cannot spawn a boss or take HP off one from the
            // outside; the floor itself has to reach the possession the hash is about. What the test
            // asserts is the FSM's determinism — the live threshold is a balance number and belongs
            // to Stage 11d's pass over data/.
            EnemyConfig row = ConfigFixture.Enemy(config, Pob);
            row.BossPhase2HpPercent = 99.9f;

            BattleSetup setup = Setup(config, BossFloor, hp: 100_000, maxHp: 100_000);
            var factory = new EcsBattleSessionFactory(config);
            var replay = new BattleReplay(setup);

            IBattleSession original = factory.Create(setup);
            var recorder = new Recorder(original);

            // Past the boss's threshold and deep into the floor: the recording covers phase 2,
            // at least one possession and the floor's own sweep.
            int ticks = 1800 + 120;
            for (int i = 0; i < ticks; i++)
            {
                PlayerInputFrame input = PlayerInputFrame.FromAxis(i % 2 == 0 ? 1f : -1f, 0.5f);
                replay.RecordStep(input);
                recorder.Step(input);
            }

            // The floor really did reach the phase and the possession the hash is about.
            Assert.True(recorder.Count(BattleEventType.BossPhaseChanged) > 0, "the boss never reached phase 2");
            Assert.True(recorder.CountFlipped() > 0, "the boss never possessed the player");

            uint expected = original.ComputeStateHash();
            uint actual = replay.ReplayHash(factory);

            Assert.Equal(expected, actual);
        }

        // -----------------------------------------------------------------
        // helpers
        // -----------------------------------------------------------------

        private static EcsBattleSimulation Create(string floorId)
        {
            IGameConfig config = ConfigFixture.Load();
            var factory = new EcsBattleSessionFactory(config);
            return (EcsBattleSimulation)factory.Create(Setup(config, floorId, hp: 100_000, maxHp: 100_000));
        }

        private static BattleSetup Setup(IGameConfig config, string floorId, int hp, int maxHp)
        {
            FloorConfig floor = ConfigFixture.Floor(config, floorId);

            return new BattleSetup
            {
                Seed = 20261021u,
                StageId = floorId,
                DifficultyId = "difficulty.normal",
                FloorIndex = floor.Index,
                IsBossFloor = floor.IsBoss,
                CurrentHp = hp,
                DevCheatsAllowed = true,
                Loadout = new CombatLoadoutSnapshot
                {
                    Stats = new PlayerStatBlock
                    {
                        MaxHp = maxHp,
                        MaxMp = config.Constant.MaxMp,
                        MoveSpeed = config.Constant.MoveSpeed,
                        PickupRadius = config.Constant.PickupRadius,
                        FocusPerSecond = config.Constant.FocusPerSecond,
                    },
                },
            };
        }

        /// <summary>
        /// Places the boss where the player can be reached, and registers it as the floor's boss
        /// exactly the way <see cref="WaveSpawnSystem"/> does.
        /// </summary>
        private static int SpawnBoss(EcsBattleSimulation simulation, IGameConfig config, float x, float y)
        {
            EnemyConfig row = ConfigFixture.Enemy(config, Pob);
            WaveSlot slot = Slot(simulation, row, isBoss: true);

            int entity = simulation.Context.Spawner.SpawnEnemy(simulation.Context, slot, x, y);
            simulation.Context.EnemiesAlive++;
            simulation.Context.BossEntity = entity;
            simulation.Context.BossMaxHp = row.MaxHp;
            simulation.Context.BossHp = row.MaxHp;
            simulation.Context.BossSpawned = true;
            return entity;
        }

        /// <summary>Puts the boss into phase 2 the only way the game does: by spending its HP.</summary>
        private static void EnterPhaseTwo(EcsBattleSimulation simulation, int boss, Recorder recorder)
        {
            EcsPool<BossMind> minds = simulation.Context.World.GetPool<BossMind>();
            EcsPool<Health> healths = simulation.Context.World.GetPool<Health>();

            healths.Get(boss).Hp = minds.Get(boss).Phase2HpThreshold;
            recorder.Step(PlayerInputFrame.Idle);

            Assert.Equal(2, minds.Get(boss).Phase);
        }

        /// <summary>Drops a ghost at a position and takes it to zero HP, the tick it dies on.</summary>
        private static void KillBeside(EcsBattleSimulation simulation, IGameConfig config, float x, float y)
        {
            EnemyConfig row = ConfigFixture.Enemy(config, Spirit);
            WaveSlot slot = Slot(simulation, row, isBoss: false);

            int corpse = simulation.Context.Spawner.SpawnEnemy(simulation.Context, slot, x, y);
            simulation.Context.EnemiesAlive++;
            simulation.Context.World.GetPool<Health>().Get(corpse).Hp = 0;
        }

        private static WaveSlot Slot(EcsBattleSimulation simulation, EnemyConfig row, bool isBoss)
        {
            return new WaveSlot
            {
                EnemyDefId = simulation.Context.DefIds.Enemy(row.Id),
                MaxHp = row.MaxHp,
                MoveSpeed = row.MoveSpeed,
                ContactDamage = row.ContactDamage,
                ContactPeriodTicks = Math.Max(1, (int)Math.Round(row.AttackCooldownSeconds * BattleConstants.TickRate)),
                MovePattern = (int)row.MovePattern,
                IsBoss = isBoss,
                IsElite = row.IsElite,
                DropValue = row.DropValue,
                Attack = EnemyAttackRules.FromConfig(row),
                Boss = BossRules.FromConfig(row),
            };
        }

        /// <summary>What every bia still lying on the floor is worth together.</summary>
        private static int PickupValueSum(EcsBattleSimulation simulation)
        {
            EcsWorld world = simulation.Context.World;
            EcsFilter filter = world.Filter<Pickup>().Inc<Position>().End(BattleArena.FilterDenseCapacity);
            EcsPool<Pickup> pool = world.GetPool<Pickup>();

            int sum = 0;
            foreach (int entity in filter)
            {
                sum += pool.Get(entity).Value;
            }

            return sum;
        }

        private static int Ticks(float seconds) =>
            seconds <= 0f ? 0 : Math.Max(1, (int)Math.Round(seconds * BattleConstants.TickRate));

        private static Position PlayerPosition(EcsBattleSimulation simulation) =>
            simulation.Context.World.GetPool<Position>().Get(simulation.Context.PlayerEntity);

        /// <summary>One event, with the tick it happened on.</summary>
        private readonly struct Recorded
        {
            internal Recorded(int tick, BattleEvent battleEvent)
            {
                Tick = tick;
                Event = battleEvent;
            }

            internal int Tick { get; }

            internal BattleEvent Event { get; }
        }

        /// <summary>
        /// Steps a session and keeps everything it publishes — including the HUD's own
        /// possession clock, which is the number the screen shows.
        /// </summary>
        private sealed class Recorder
        {
            private readonly IBattleSession _session;
            private readonly BattleEvent[] _buffer = new BattleEvent[512];
            private readonly List<Recorded> _events = new List<Recorded>(256);
            private long _cursor;
            private int _flippedTicks;

            internal Recorder(IBattleSession session)
            {
                _session = session;
                _cursor = session.Events.OldestSeq;
            }

            internal void Step(PlayerInputFrame input)
            {
                _session.Step(input);

                if (_session.Frame.Hud.ControlFlipTicks > 0)
                {
                    _flippedTicks++;
                }

                int read;
                do
                {
                    read = _session.Events.Read(ref _cursor, _buffer, out _);
                    for (int i = 0; i < read; i++)
                    {
                        _events.Add(new Recorded((int)_session.Tick, _buffer[i]));
                    }
                }
                while (read == _buffer.Length);
            }

            /// <summary>Ticks whose published HUD said the controls were reversed.</summary>
            internal int CountFlipped() => _flippedTicks;

            /// <summary>The last event of a type read so far, or a tick of -1.</summary>
            internal Recorded Last(BattleEventType type)
            {
                for (int i = _events.Count - 1; i >= 0; i--)
                {
                    if (_events[i].Event.Type == type)
                    {
                        return _events[i];
                    }
                }

                return new Recorded(-1, default);
            }

            internal Recorded First(BattleEventType type)
            {
                for (int i = 0; i < _events.Count; i++)
                {
                    if (_events[i].Event.Type == type)
                    {
                        return _events[i];
                    }
                }

                return new Recorded(-1, default);
            }

            internal int Count(BattleEventType type)
            {
                int count = 0;
                for (int i = 0; i < _events.Count; i++)
                {
                    if (_events[i].Event.Type == type)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }
}
