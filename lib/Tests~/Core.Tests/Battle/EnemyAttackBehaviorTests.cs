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
    /// The Stage 11b acceptance rules: Krasue and Nang Tani each run their own telegraphed
    /// attack, every strong hit is preceded by a warning that travels as an event, and the
    /// numbers that decide both come from the enemy row.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ghosts are placed by hand a few units from the player, because a wave spawns on
    /// the arena ring and would need half a minute to walk into reach. A hand-placed ghost
    /// goes through <see cref="BattleSpawner"/> with the same <see cref="WaveSlot"/> a wave
    /// resolves, so what is tested is the shipped path, not a test-only one.
    /// </para>
    /// <para>
    /// Timing is asserted in ticks read off the event stream and the brain's state, never off
    /// a stopwatch: the tick is the unit the whole simulation is written in.
    /// </para>
    /// </remarks>
    public sealed class EnemyAttackBehaviorTests
    {
        private const string Forest1 = "floor.forest_1";
        private const string Krasue = "enemy.krasue";
        private const string NangTani = "enemy.nang_tani";
        private const string Krahang = "enemy.krahang";

        // -----------------------------------------------------------------
        // Krasue: dash -> drain -> retreat
        // -----------------------------------------------------------------

        [Fact]
        public void Krasue_walks_telegraph_dash_drain_retreat_in_that_order()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);

            // Far enough out that the first stage really is the walk: the ghost has to chase
            // until the row's own range opens the cycle.
            int ghost = Spawn(simulation, config, Krasue, 12f, 0f);

            var states = new List<int>();
            EcsPool<EnemyBrain> brains = simulation.Context.World.GetPool<EnemyBrain>();
            EcsPool<Position> positions = simulation.Context.World.GetPool<Position>();

            float atStrikeStart = 0f;
            float atStrikeEnd = 0f;
            bool sawStrike = false;

            for (int i = 0; i < 300; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
                int state = brains.Get(ghost).State;

                if (states.Count == 0 || states[states.Count - 1] != state)
                {
                    states.Add(state);
                }

                if (state == (int)EnemyAttackState.Strike)
                {
                    float distance = Distance(positions.Get(ghost), 0f, 0f);
                    if (!sawStrike)
                    {
                        atStrikeStart = distance;
                        sawStrike = true;
                    }

                    atStrikeEnd = distance;
                }
            }

            Assert.Equal((int)EnemyAttackState.Chase, states[0]);
            AssertInOrder(
                states,
                (int)EnemyAttackState.Telegraph,
                (int)EnemyAttackState.Strike,
                (int)EnemyAttackState.FollowUpTelegraph,
                (int)EnemyAttackState.FollowUp,
                (int)EnemyAttackState.Retreat,
                (int)EnemyAttackState.Recover);

            // The dash actually travels its line: it closes most of the distance it warned about.
            Assert.True(sawStrike, "the ghost never dashed");
            Assert.True(
                atStrikeEnd < atStrikeStart - 3f,
                "the dash did not travel: " + atStrikeStart + " -> " + atStrikeEnd);
        }

        [Fact]
        public void Krasue_warns_for_the_rows_telegraph_ticks_before_it_hits()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, Krasue);
            int telegraphTicks = (int)Math.Round(row.AttackTelegraphSeconds * BattleConstants.TickRate);

            Spawn(simulation, config, Krasue, 4f, 0f);

            Recorder recorder = new Recorder(simulation);
            recorder.StepUntil(BattleEventType.PlayerDamaged, maxTicks: 120);

            Recorded started = recorder.First(BattleEventType.TelegraphStarted);
            Recorded hit = recorder.First(BattleEventType.PlayerDamaged);

            Assert.True(started.Tick >= 0, "the Krasue never telegraphed");
            Assert.True(hit.Tick >= 0, "the Krasue never landed its dash");
            Assert.True(
                hit.Tick - started.Tick >= telegraphTicks,
                "the dash landed " + (hit.Tick - started.Tick) + " ticks after the warning; the row asks for "
                + telegraphTicks);

            // The warning's footprint is the line the rule uses, and it says so.
            Assert.Equal((int)TelegraphShape.Line, started.Event.Value);
            Assert.Equal(simulation.Context.DefIds.Enemy(Krasue), started.Event.DefId);
            Assert.Equal(row.AttackLength, started.Event.Length, 0.001f);
            Assert.Equal(row.AttackRadius, started.Event.Radius, 0.001f);
        }

        [Fact]
        public void A_player_who_steps_out_of_the_warned_line_is_not_hit()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            Spawn(simulation, config, Krasue, 6f, 0f);

            Recorder recorder = new Recorder(simulation);
            recorder.StepUntil(BattleEventType.TelegraphStarted, maxTicks: 60);
            Assert.True(recorder.First(BattleEventType.TelegraphStarted).Tick >= 0, "no warning");

            // Walk out along the line's side while the warning is on the floor. A dash's
            // half-width plus the player's body is 1.3 units, and half a second of walking is
            // 2 units, so this is out by the time the line lands.
            for (int i = 0; i < 180; i++)
            {
                recorder.Step(PlayerInputFrame.FromAxis(0f, 1f));
            }

            Assert.True(
                recorder.Count(BattleEventType.PlayerDamaged) == 0,
                "the dash hit a player who walked out of the line it warned about");
        }

        // -----------------------------------------------------------------
        // Nang Tani: ring -> vines
        // -----------------------------------------------------------------

        [Fact]
        public void Nang_Tani_warns_a_ring_and_then_vines_before_either_lands()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, NangTani);
            Spawn(simulation, config, NangTani, 3f, 0f);

            Recorder recorder = new Recorder(simulation);
            for (int i = 0; i < 90; i++)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            List<Recorded> warnings = recorder.All(BattleEventType.TelegraphStarted);
            Assert.True(warnings.Count >= 2, "Nang Tani did not warn twice in one cycle");

            Assert.Equal((int)TelegraphShape.Circle, warnings[0].Event.Value);
            Assert.Equal(0f, warnings[0].Event.Length, 0.001f);
            Assert.Equal(row.AttackRadius, warnings[0].Event.Radius, 0.001f);

            Assert.Equal((int)TelegraphShape.Line, warnings[1].Event.Value);
            Assert.Equal(row.FollowUpLength, warnings[1].Event.Length, 0.001f);
            Assert.Equal(row.FollowUpRadius, warnings[1].Event.Radius, 0.001f);

            // Every warning closes, and each one closes before the first damage it caused.
            List<Recorded> ends = recorder.All(BattleEventType.TelegraphEnded);
            Assert.True(ends.Count >= 2, "a warning never ended");
            Assert.True(
                ends[0].Tick > warnings[0].Tick,
                "the ring's warning ended before it was drawn");

            Recorded firstHit = recorder.First(BattleEventType.PlayerDamaged);
            Assert.True(firstHit.Tick >= 0, "the ring never landed");
            Assert.True(
                firstHit.Tick > warnings[0].Tick,
                "the ring landed before its own warning");

            int ringTicks = (int)Math.Round(row.AttackTelegraphSeconds * BattleConstants.TickRate);
            Assert.True(
                firstHit.Tick - warnings[0].Tick >= ringTicks,
                "the ring landed " + (firstHit.Tick - warnings[0].Tick) + " ticks after its warning");
        }

        [Fact]
        public void A_player_outside_the_ring_is_only_hit_by_the_vines()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, NangTani);

            // 5.5 units away: outside the 4.5-unit ring, inside the 7-unit vine line.
            Spawn(simulation, config, NangTani, 5.5f, 0f);

            Recorder recorder = new Recorder(simulation);
            for (int i = 0; i < 60; i++)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            List<Recorded> hits = recorder.All(BattleEventType.PlayerDamaged);
            Assert.True(hits.Count == 1, "expected exactly the vine lash, saw " + hits.Count + " hit(s)");
            Assert.Equal(row.FollowUpDamage, hits[0].Event.Value);

            List<Recorded> warnings = recorder.All(BattleEventType.TelegraphStarted);
            Assert.True(
                hits[0].Tick > warnings[1].Tick,
                "the vines landed before their warning");
        }

        // -----------------------------------------------------------------
        // Krahang: dash -> gale -> recover (Stage 16)
        // -----------------------------------------------------------------

        [Fact]
        public void Krahang_dives_warns_a_gale_and_recovers_without_retreating()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, Krahang);

            // Spawned outside the row's 11-unit range, so the cycle really starts with a walk.
            int ghost = Spawn(simulation, config, Krahang, 14f, 0f);

            var states = new List<int>();
            EcsPool<EnemyBrain> brains = simulation.Context.World.GetPool<EnemyBrain>();

            for (int i = 0; i < 400; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
                int state = brains.Get(ghost).State;

                if (states.Count == 0 || states[states.Count - 1] != state)
                {
                    states.Add(state);
                }
            }

            Assert.Equal((int)EnemyAttackState.Chase, states[0]);
            AssertInOrder(
                states,
                (int)EnemyAttackState.Chase,
                (int)EnemyAttackState.Telegraph,
                (int)EnemyAttackState.Strike,
                (int)EnemyAttackState.FollowUpTelegraph,
                (int)EnemyAttackState.Recover);

            // A gale is a single burst with no window, and Krahang does not step back: the two
            // stages its row leaves at zero are the two stages it must never enter.
            Assert.DoesNotContain((int)EnemyAttackState.FollowUp, states);
            Assert.DoesNotContain((int)EnemyAttackState.Retreat, states);
            Assert.Equal(0f, row.FollowUpSeconds, 0.001f);
            Assert.Equal(0f, row.RetreatSeconds, 0.001f);
        }

        [Fact]
        public void Krahang_warns_a_circle_where_it_lands_and_the_gale_is_a_burst()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, Krahang);
            Spawn(simulation, config, Krahang, 12f, 0f);

            var recorder = new Recorder(simulation);
            for (int i = 0; i < 120; i++)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            List<Recorded> warnings = recorder.All(BattleEventType.TelegraphStarted);
            Assert.True(warnings.Count >= 2, "Krahang did not warn its dive and its gale");

            // The dive is the row's line; the gale is a circle at the point the dive ended.
            Assert.Equal((int)TelegraphShape.Line, warnings[0].Event.Value);
            Assert.Equal(row.AttackLength, warnings[0].Event.Length, 0.001f);
            Assert.Equal((int)TelegraphShape.Circle, warnings[1].Event.Value);
            Assert.Equal(0f, warnings[1].Event.Length, 0.001f);
            Assert.Equal(row.FollowUpRadius, warnings[1].Event.Radius, 0.001f);
            Assert.Equal(simulation.Context.DefIds.Enemy(Krahang), warnings[1].Event.DefId);
        }

        [Fact]
        public void Krahang_gale_throws_the_player_away_from_where_it_landed()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, Krahang);
            Spawn(simulation, config, Krahang, 12f, 0f);

            int player = simulation.Context.PlayerEntity;
            EcsPool<Position> positions = simulation.Context.World.GetPool<Position>();
            float startX = positions.Get(player).X;
            float startY = positions.Get(player).Y;

            Assert.Equal(0f, startX, 0.001f);
            Assert.True(row.FollowUpPush > 0f, "the shipped gale does not push, so this proves nothing");

            float movedX = 0f;
            for (int i = 0; i < 200 && Math.Abs(movedX) < 0.01f; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
                movedX = positions.Get(player).X - startX;
            }

            // The dive lands past the player (on their side of the arena), so the throw is back
            // along -X and exactly the row's distance.
            Assert.InRange(movedX, -row.FollowUpPush - 0.05f, -row.FollowUpPush + 0.05f);
            Assert.Equal(startY, positions.Get(player).Y, 0.001f);
        }

        [Fact]
        public void A_ghost_that_does_not_ask_for_a_push_leaves_the_player_where_they_stood()
        {
            // Krasue's follow-up is a drain with follow_up_push 0. The player takes the damage
            // and keeps their position: only a row that asks for a gale moves anyone.
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            EnemyConfig row = ConfigFixture.Enemy(config, Krasue);
            Assert.Equal(0f, row.FollowUpPush, 0.001f);

            Spawn(simulation, config, Krasue, 4f, 0f);

            int player = simulation.Context.PlayerEntity;
            EcsPool<Position> positions = simulation.Context.World.GetPool<Position>();

            var recorder = new Recorder(simulation);
            for (int i = 0; i < 300; i++)
            {
                recorder.Step(PlayerInputFrame.Idle);
            }

            Assert.True(
                recorder.Count(BattleEventType.PlayerDamaged) > 0,
                "the drain never landed, so the test proves nothing");
            Assert.Equal(0f, positions.Get(player).X, 0.001f);
            Assert.Equal(0f, positions.Get(player).Y, 0.001f);
        }

        // -----------------------------------------------------------------
        // The footprint rules and the data behind them
        // -----------------------------------------------------------------

        [Fact]
        public void The_footprint_rules_are_the_ones_the_warning_publishes()
        {
            // A circle reaches its centre plus a body radius, and no further.
            Assert.True(EnemyAttackRules.CircleHits(0f, 0f, 4f, 3f, 0f, 0.4f));
            Assert.False(EnemyAttackRules.CircleHits(0f, 0f, 4f, 4.5f, 0f, 0.4f));

            // A line is a capsule: half-width to either side, rounded at its far end, and
            // nothing beyond that.
            Assert.True(EnemyAttackRules.LineHits(0f, 0f, 1f, 0f, 6f, 0.9f, 5f, 1.2f, 0.4f));
            Assert.False(EnemyAttackRules.LineHits(0f, 0f, 1f, 0f, 6f, 0.9f, 5f, 1.5f, 0.4f));
            Assert.True(EnemyAttackRules.LineHits(0f, 0f, 1f, 0f, 6f, 0.9f, 7f, 0f, 0.4f));
            Assert.False(EnemyAttackRules.LineHits(0f, 0f, 1f, 0f, 6f, 0.9f, 7.6f, 0f, 0.4f));

            // The capsule is rounded at the origin too, but only by its own reach.
            Assert.True(EnemyAttackRules.LineHits(0f, 0f, 1f, 0f, 6f, 0.9f, -1f, 0f, 0.4f));
            Assert.False(EnemyAttackRules.LineHits(0f, 0f, 1f, 0f, 6f, 0.9f, -1.5f, 0f, 0.4f));
        }

        [Fact]
        public void The_attack_block_is_read_from_the_enemy_row()
        {
            IGameConfig config = ConfigFixture.Load();

            EnemyConfig krasueRow = ConfigFixture.Enemy(config, Krasue);
            EnemyAttack krasue = EnemyAttackRules.FromConfig(krasueRow);
            Assert.Equal((int)EnemyAttackPattern.DashFollowUp, krasue.Pattern);
            Assert.Equal(
                (int)Math.Round(krasueRow.AttackTelegraphSeconds * BattleConstants.TickRate),
                krasue.TelegraphTicks);
            Assert.True(krasue.Length > 0f, "a dash must be a line");

            EnemyAttack nangTani = EnemyAttackRules.FromConfig(ConfigFixture.Enemy(config, NangTani));
            Assert.Equal((int)EnemyAttackPattern.RingVines, nangTani.Pattern);
            Assert.Equal(0f, nangTani.Length, 0.001f);
            Assert.True(nangTani.Radius > 0f, "a ring must have a radius");
            Assert.True(nangTani.FollowUpLength > 0f, "the vines must be a line");

            // A ghost with no pattern reads as all zeroes and never gets a brain.
            EnemyAttack walker = EnemyAttackRules.FromConfig(
                ConfigFixture.Enemy(config, "enemy.wandering_spirit"));
            Assert.Equal((int)EnemyAttackPattern.None, walker.Pattern);
            Assert.Equal(0, walker.TelegraphTicks);
        }

        // -----------------------------------------------------------------
        // Allocation
        // -----------------------------------------------------------------

        [Fact]
        public void A_tick_that_runs_a_telegraph_allocates_nothing_after_warm_up()
        {
            EcsBattleSimulation simulation = Create(out IGameConfig config);
            Spawn(simulation, config, Krasue, 8f, 0f);

            for (int i = 0; i < 30; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated == 0L, "a telegraphing tick allocated " + (allocated / 60.0) + " bytes on average");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static EcsBattleSimulation Create(out IGameConfig config)
        {
            config = ConfigFixture.Load();
            var setup = new BattleSetup
            {
                Seed = 20261020u,
                StageId = Forest1,
                DifficultyId = "difficulty.normal",
                FloorIndex = ConfigFixture.Floor(config, Forest1).Index,
                IsBossFloor = false,
                CurrentHp = 100_000,
                DevCheatsAllowed = true,
                Loadout = new CombatLoadoutSnapshot
                {
                    Stats = new PlayerStatBlock
                    {
                        MaxHp = 100_000,
                        MaxMp = config.Constant.MaxMp,
                        MoveSpeed = config.Constant.MoveSpeed,
                        PickupRadius = config.Constant.PickupRadius,
                        FocusPerSecond = config.Constant.FocusPerSecond,
                    },
                },
            };

            return (EcsBattleSimulation)new EcsBattleSessionFactory(config).Create(setup);
        }

        /// <summary>Places one ghost of <paramref name="enemyId"/> at a world position.</summary>
        private static int Spawn(EcsBattleSimulation simulation, IGameConfig config, string enemyId, float x, float y)
        {
            EnemyConfig row = ConfigFixture.Enemy(config, enemyId);
            var slot = new WaveSlot
            {
                EnemyDefId = simulation.Context.DefIds.Enemy(enemyId),
                MaxHp = row.MaxHp,
                MoveSpeed = row.MoveSpeed,
                ContactDamage = row.ContactDamage,
                ContactPeriodTicks = Math.Max(1, (int)Math.Round(row.AttackCooldownSeconds * BattleConstants.TickRate)),
                MovePattern = (int)row.MovePattern,
                IsBoss = false,
                IsElite = row.IsElite,
                DropValue = row.DropValue,
                Attack = EnemyAttackRules.FromConfig(row),
            };

            int entity = simulation.Context.Spawner.SpawnEnemy(simulation.Context, slot, x, y);
            simulation.Context.EnemiesAlive++;
            return entity;
        }

        private static float Distance(Position position, float x, float y)
        {
            float dx = position.X - x;
            float dy = position.Y - y;
            return (float)Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static void AssertInOrder(List<int> actual, params int[] expected)
        {
            int cursor = -1;
            for (int i = 0; i < expected.Length; i++)
            {
                int found = actual.IndexOf(expected[i], cursor + 1);
                Assert.True(
                    found > cursor,
                    "stage " + (EnemyAttackState)expected[i] + " never came in order; saw "
                    + string.Join(", ", actual.ConvertAll(s => ((EnemyAttackState)s).ToString())));
                cursor = found;
            }
        }

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
        /// Steps a session and keeps everything it publishes, so a test can ask "when did
        /// the warning go up, and when did the hit land" in the simulation's own clock.
        /// </summary>
        private sealed class Recorder
        {
            private readonly IBattleSession _session;
            private readonly BattleEvent[] _buffer = new BattleEvent[512];
            private readonly List<Recorded> _events = new List<Recorded>(256);
            private long _cursor;

            internal Recorder(IBattleSession session)
            {
                _session = session;
                _cursor = session.Events.OldestSeq;
            }

            internal void Step(PlayerInputFrame input)
            {
                _session.Step(input);

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

            internal void StepUntil(BattleEventType type, int maxTicks)
            {
                for (int i = 0; i < maxTicks; i++)
                {
                    if (First(type).Tick >= 0)
                    {
                        return;
                    }

                    Step(PlayerInputFrame.Idle);
                }
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

            internal List<Recorded> All(BattleEventType type)
            {
                var found = new List<Recorded>();
                for (int i = 0; i < _events.Count; i++)
                {
                    if (_events[i].Event.Type == type)
                    {
                        found.Add(_events[i]);
                    }
                }

                return found;
            }

            internal int Count(BattleEventType type) => All(type).Count;
        }
    }
}
