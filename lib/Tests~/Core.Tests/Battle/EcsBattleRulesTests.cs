using System;
using System.Collections.Generic;
using Leopotam.EcsLite;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Simulation.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The Stage 07 acceptance rules of the real EcsLite battle: movement per tick, the
    /// casting clock and its MP cost, projectile hits and despawn, a kill paid exactly
    /// once, a player death ending the floor exactly once, and the load benchmark not
    /// allocating.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These tests drive the simulation through <see cref="IBattleSession"/> and read
    /// state through the contract's frame and event stream wherever the contract offers
    /// one; they reach into the <see cref="BattleContext"/> only to observe a number the
    /// contract does not publish (MP), which is the one place a rule test has to.
    /// </para>
    /// <para>
    /// Numbers come from <c>data/</c> (<see cref="ConfigFixture"/>), so a balance change
    /// in the tables is felt here. Where a test needs a player that survives standing
    /// still — the suite's input is always <see cref="PlayerInputFrame.Idle"/> — the
    /// loadout raises its HP. That is a rule test, not a balance claim: balance is what
    /// the bot simulation (tools/sim.ps1) measures.
    /// </para>
    /// </remarks>
    public sealed class EcsBattleRulesTests
    {
        private const string Forest1 = "floor.forest_1";
        private const string Forest5 = "floor.forest_5";
        private const string FireBolt = "skill.fire_bolt";

        // -----------------------------------------------------------------
        // Movement
        // -----------------------------------------------------------------

        [Fact]
        public void The_player_advances_by_move_speed_each_tick()
        {
            IBattleSession session = Create(Forest1, hp: 1_000);
            var simulation = (EcsBattleSimulation)session;
            IGameConfig config = simulation.Context.Config;

            float speed = simulation.Context.MoveSpeed;
            Assert.True(speed > 0f, "the loadout must carry a move speed");

            Position start = PlayerPosition(simulation);
            const int ticks = 30;
            var input = PlayerInputFrame.FromAxis(1f, 0f);

            for (int i = 0; i < ticks; i++)
            {
                session.Step(input);
            }

            Position end = PlayerPosition(simulation);
            float expected = speed * BattleArena.TickDeltaSeconds * ticks;

            Assert.Equal(expected, end.X - start.X, 0.01f);
            Assert.Equal(start.Y, end.Y, 0.0001f);
            Assert.Equal(config.Constant.MoveSpeed, speed, 0.0001f);
        }

        [Fact]
        public void The_player_cannot_leave_the_arena()
        {
            IBattleSession session = Create(Forest1, hp: 1_000);
            var simulation = (EcsBattleSimulation)session;
            var input = PlayerInputFrame.FromAxis(1f, 1f);

            for (int i = 0; i < 900; i++)
            {
                session.Step(input);
            }

            Position position = PlayerPosition(simulation);
            float distance = (float)Math.Sqrt((position.X * position.X) + (position.Y * position.Y));

            Assert.True(
                distance <= BattleArena.Radius + 0.001f,
                "the player left the arena disc: distance " + distance);
        }

        // -----------------------------------------------------------------
        // Casting clock and MP
        // -----------------------------------------------------------------

        [Fact]
        public void A_weapon_never_casts_faster_than_its_cooldown()
        {
            IGameConfig config = ConfigFixture.Load();
            SkillConfig bolt = ConfigFixture.Skill(config, FireBolt);

            IBattleSession session = Create(Forest1, hp: 1_000, weapons: new[] { Weapon(config, FireBolt) });
            int periodTicks = (int)Math.Round(bolt.CooldownSeconds * BattleConstants.TickRate);

            const int ticks = 300;
            for (int i = 0; i < ticks; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            List<BattleEvent> events = ReadEvents(session);
            int casts = Count(events, BattleEventType.ProjectileFired);

            Assert.True(casts > 0, "the weapon never cast at all");
            Assert.True(
                casts <= (ticks / periodTicks) + 1,
                "cast " + casts + " times in " + ticks + " ticks, which is faster than the cooldown of "
                + periodTicks + " ticks");
        }

        [Fact]
        public void Mp_never_goes_negative_and_a_cast_that_costs_too_much_never_happens()
        {
            IGameConfig config = ConfigFixture.Load();

            // Affordable: the pool must stay inside [0, max] on every tick.
            IBattleSession affordable = Create(Forest1, hp: 1_000, weapons: new[] { Weapon(config, FireBolt) });
            var affordableSimulation = (EcsBattleSimulation)affordable;
            for (int i = 0; i < 300; i++)
            {
                affordable.Step(PlayerInputFrame.Idle);
                Mana mana = PlayerMana(affordableSimulation);
                Assert.InRange(mana.Mp, 0, mana.MaxMp);
            }

            Assert.True(Count(ReadEvents(affordable), BattleEventType.ProjectileFired) > 0);

            // Unaffordable: the same weapon, priced beyond the whole pool, must never fire.
            WeaponLoadout expensive = Weapon(config, FireBolt);
            expensive.MpCost = affordableSimulation.Context.MaxMp + 1;

            IBattleSession refused = Create(Forest1, hp: 1_000, weapons: new[] { expensive });
            for (int i = 0; i < 300; i++)
            {
                refused.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(0, Count(ReadEvents(refused), BattleEventType.ProjectileFired));
        }

        // -----------------------------------------------------------------
        // Projectiles
        // -----------------------------------------------------------------

        [Fact]
        public void A_projectile_hits_a_ghost_for_the_weapon_damage_and_then_despawns()
        {
            IGameConfig config = ConfigFixture.Load();
            SkillConfig bolt = ConfigFixture.Skill(config, FireBolt);

            IBattleSession session = Create(Forest1, hp: 1_000, weapons: new[] { Weapon(config, FireBolt) });
            var simulation = (EcsBattleSimulation)session;

            // A shot lives exactly its range divided by its speed, and that is the whole
            // despawn rule: at any tick, the shots in flight cannot be more than the shots
            // fired in the last `lifetime` ticks. Nothing else releases a projectile, so a
            // leak shows up here as an ever-growing count.
            int lifetimeTicks = Math.Max(
                1, (int)Math.Round(bolt.Range / bolt.ProjectileSpeed * BattleConstants.TickRate));
            var firedPerTick = new int[lifetimeTicks];
            int firedInWindow = 0;
            bool landed = false;
            bool anyCast = false;
            bool sawTheShotDisappear = false;
            long casts = 0;

            var buffer = new BattleEvent[256];
            long cursor = 0;

            for (int tick = 0; tick < 900; tick++)
            {
                session.Step(PlayerInputFrame.Idle);

                int firedThisTick = 0;
                int read;
                while ((read = session.Events.Read(ref cursor, buffer, out _)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        BattleEvent e = buffer[i];
                        if (e.Type == BattleEventType.ProjectileFired)
                        {
                            firedThisTick++;
                        }
                        else if (e.Type == BattleEventType.DamageDealt && e.Value == bolt.Damage)
                        {
                            landed = true;
                        }
                    }
                }

                int slot = tick % lifetimeTicks;
                firedInWindow = firedInWindow - firedPerTick[slot] + firedThisTick;
                firedPerTick[slot] = firedThisTick;
                casts += firedThisTick;
                anyCast |= firedThisTick > 0;

                int alive = simulation.Context.ProjectilesAlive;
                Assert.True(
                    alive <= firedInWindow,
                    "tick " + tick + " had " + alive + " projectiles in flight but only "
                    + firedInWindow + " were fired in the last " + lifetimeTicks + " ticks");

                sawTheShotDisappear |= anyCast && alive == 0;
            }

            Assert.True(casts > 0, "the weapon never fired");
            Assert.True(landed, "no projectile landed for the weapon's damage (" + bolt.Damage + ")");
            Assert.True(sawTheShotDisappear, "a projectile never left the field");
            Assert.True(simulation.Context.Kills > 0, "nothing died, so no hit can be trusted");
        }

        // -----------------------------------------------------------------
        // Death and rewards
        // -----------------------------------------------------------------

        [Fact]
        public void A_kill_is_rewarded_exactly_once()
        {
            IGameConfig config = ConfigFixture.Load();

            IBattleSession session = Create(Forest1, hp: 1_000, weapons: new[] { Weapon(config, FireBolt) });
            var simulation = (EcsBattleSimulation)session;

            for (int i = 0; i < 900; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            List<BattleEvent> events = ReadEvents(session);
            var killed = new HashSet<EntityRef>();
            int killEvents = 0;
            int drops = 0;
            int announced = 0;
            int collected = 0;

            foreach (BattleEvent e in events)
            {
                switch (e.Type)
                {
                    case BattleEventType.EnemyKilled:
                        killEvents++;
                        killed.Add(e.Entity);
                        announced = e.Value;
                        break;

                    case BattleEventType.PickupSpawned:
                        drops++;
                        break;

                    case BattleEventType.PickupCollected:
                        collected++;
                        break;
                }
            }

            Assert.True(killEvents > 0, "no ghost died in 30 seconds of fire");

            // One entity, one kill: a slot recycled into a new ghost carries a new version,
            // so a double payout would show up as a repeated (index, version) pair.
            Assert.Equal(killEvents, killed.Count);
            Assert.Equal(killEvents, simulation.Context.Kills);

            // The event's Value is the running kill count, so the last one is the total.
            Assert.Equal(simulation.Context.Kills, announced);

            // One drop per death, and none for a ghost that was only hit.
            Assert.Equal(killEvents, drops);

            // Every collected pickup is one of those ghosts and floor 1 spawns only
            // wandering spirits, so the takings are exactly "collections x drop_value".
            int dropValue = ConfigFixture.Enemy(config, "enemy.wandering_spirit").DropValue;
            Assert.True(dropValue > 0, "the filler ghost must pay something for this rule to mean anything");
            Assert.Equal(collected * dropValue, simulation.Context.CurrencyEarned);
        }

        [Fact]
        public void Killing_a_ghost_and_collecting_its_drop_pays_the_enemys_drop_value()
        {
            IGameConfig config = ConfigFixture.Load();
            int dropValue = ConfigFixture.Enemy(config, "enemy.wandering_spirit").DropValue;
            Assert.True(dropValue > 0, "the filler ghost must pay something for this rule to mean anything");

            IBattleSession session = Create(Forest1, hp: 1_000, weapons: new[] { Weapon(config, FireBolt) });
            var simulation = (EcsBattleSimulation)session;

            // Fire until at least one ghost has died and left its drop behind.
            for (int i = 0; i < 900; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            Assert.True(simulation.Context.Kills > 0, "no ghost died in 30 seconds of fire");

            // Stop casting: otherwise a kill during the walk below could add a drop the
            // test did not account for.
            EcsFilter casters = simulation.Context.World.Filter<Caster>().End(BattleArena.FilterDenseCapacity);
            EcsPool<Caster> casterPool = simulation.Context.World.GetPool<Caster>();
            foreach (int caster in casters)
            {
                ref Caster c = ref casterPool.Get(caster);
                c.CooldownTicks = int.MaxValue / 2;
            }

            EcsFilter pickupFilter = simulation.Context.World
                .Filter<Pickup>()
                .Inc<Position>()
                .End(BattleArena.FilterDenseCapacity);
            EcsPool<Pickup> pickupPool = simulation.Context.World.GetPool<Pickup>();
            EcsPool<Position> positions = simulation.Context.World.GetPool<Position>();

            var drops = new List<(float X, float Y, int Value)>();
            foreach (int entity in pickupFilter)
            {
                Position position = positions.Get(entity);
                drops.Add((position.X, position.Y, pickupPool.Get(entity).Value));
            }

            Assert.True(drops.Count > 0, "a kill left no drop on the floor");
            int expected = simulation.Context.CurrencyEarned;
            for (int i = 0; i < drops.Count; i++)
            {
                Assert.Equal(dropValue, drops[i].Value);
                expected += drops[i].Value;
            }

            // Walk the player onto each drop, which is what the magnet would collect.
            for (int i = 0; i < drops.Count; i++)
            {
                ref Position player = ref positions.Get(simulation.Context.PlayerEntity);
                player.X = drops[i].X;
                player.Y = drops[i].Y;
                session.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(expected, simulation.Context.CurrencyEarned);

            // A collected pickup was deleted in the tick it paid, so it cannot pay again.
            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(expected, simulation.Context.CurrencyEarned);
        }

        [Fact]
        public void Collecting_pickups_credits_the_sum_of_their_drop_values_once()
        {
            IGameConfig config = ConfigFixture.Load();
            int dropValue = ConfigFixture.Enemy(config, "enemy.wandering_spirit").DropValue;
            Assert.True(dropValue > 0, "the filler ghost must pay something for this rule to mean anything");

            IBattleSession session = Create(Forest1, hp: 1_000);
            var simulation = (EcsBattleSimulation)session;
            session.Step(PlayerInputFrame.Idle);

            Position player = PlayerPosition(simulation);
            const int count = 3;
            for (int i = 0; i < count; i++)
            {
                simulation.Context.Spawner.SpawnPickup(player.X, player.Y, kind: 1, value: dropValue);
            }

            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(count * dropValue, simulation.Context.CurrencyEarned);
            Assert.Equal(count, simulation.Context.PickupsCollected);

            // A collected pickup is gone in the same tick, so more ticks cannot pay it again.
            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(count * dropValue, simulation.Context.CurrencyEarned);
            Assert.Equal(count, simulation.Context.PickupsCollected);
        }

        [Fact]
        public void The_player_dying_ends_the_floor_exactly_once()
        {
            IBattleSession session = Create(Forest1, hp: 1);

            int steps = 0;
            while (session.Phase == BattlePhase.Running && steps < 6000)
            {
                session.Step(PlayerInputFrame.Idle);
                steps++;
            }

            Assert.Equal(BattlePhase.Lost, session.Phase);
            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattleEndReason.PlayerDied, result.EndReason);
            Assert.Equal(0, result.FinalHp);

            List<BattleEvent> events = ReadEvents(session);
            Assert.Equal(1, Count(events, BattleEventType.BattleEnded));

            long tick = session.Tick;
            session.Step(PlayerInputFrame.Idle);
            session.Step(PlayerInputFrame.Idle);

            Assert.Equal(tick, session.Tick);
            Assert.Equal(BattlePhase.Lost, session.Phase);
            Assert.True(session.TryGetResult(out BattleResult again));
            Assert.Equal(result.EndReason, again.EndReason);
            Assert.Equal(result.SurvivedTicks, again.SurvivedTicks);

            // The same floor one HP higher is survivable, so the death above came from
            // contact damage and not from a rule that always ends the floor.
            IBattleSession survivor = Create(Forest1, hp: 1_000);
            for (int i = 0; i < 600; i++)
            {
                survivor.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(BattlePhase.Running, survivor.Phase);
        }

        [Fact]
        public void Killing_the_boss_wins_the_boss_floor()
        {
            IGameConfig config = ConfigFixture.Load();

            // A survivable pool on purpose: this asserts the outcome rule (a dead boss wins
            // the floor), not whether the balance lets a standing player out-damage a boss.
            IBattleSession session = Create(
                Forest5, hp: 10_000, weapons: new[] { Weapon(config, FireBolt) }, maxHp: 10_000);

            int steps = 0;
            while (session.Phase == BattlePhase.Running && steps < 6000)
            {
                session.Step(PlayerInputFrame.Idle);
                steps++;
            }

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Won, result.Outcome);
            Assert.Equal(BattleEndReason.BossDefeated, result.EndReason);
            Assert.True(result.Kills > 0);
        }

        [Fact]
        public void Ghosts_walk_towards_the_player()
        {
            IBattleSession session = Create(Forest1, hp: 1_000);
            var simulation = (EcsBattleSimulation)session;

            session.Step(PlayerInputFrame.Idle);
            float before = NearestGhostDistance(session);

            for (int i = 0; i < 60; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            float after = NearestGhostDistance(session);

            Assert.True(before > 0f, "no ghost was on the field to measure");
            Assert.True(after < before, "the nearest ghost got further away: " + before + " -> " + after);
            Assert.True(simulation.Context.EnemiesAlive > 0);
        }

        // -----------------------------------------------------------------
        // Load benchmark
        // -----------------------------------------------------------------

        /// <summary>
        /// The Stage 07 acceptance load, on .NET: ~300 ghosts and ~500 projectiles, with a
        /// tick that allocates nothing once the world has warmed up.
        /// </summary>
        /// <remarks>
        /// Timing is measured by <c>ProjectSurvive.Sim --mode benchmark</c> and recorded in
        /// the stage report, because a per-tick millisecond budget on a shared CI machine
        /// is not a stable assertion. GC allocation is: this is the test that fails the day
        /// a tick starts allocating, and it is measured at the acceptance load rather than
        /// on an empty floor.
        /// </remarks>
        [Fact]
        public void The_acceptance_load_allocates_nothing_per_tick()
        {
            IGameConfig config = ConfigFixture.Load();
            var options = new EcsBattleOptions
            {
                MaintainLoadTestEntities = true,
                LoadTestEnemies = 300,
                LoadTestProjectiles = 500,
            };

            var factory = new EcsBattleSessionFactory(config, options);
            BattleSetup setup = Setup(Forest1, hp: 100_000, weapons: new[] { Weapon(config, FireBolt) }, maxHp: 100_000);
            IBattleSession session = factory.Create(setup);
            var simulation = (EcsBattleSimulation)session;

            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            Assert.True(
                simulation.Context.EnemiesAlive >= 250,
                "the load did not reach the acceptance count: " + simulation.Context.EnemiesAlive);
            Assert.True(
                simulation.Context.ProjectilesAlive >= 400,
                "the projectile load did not reach the acceptance count: " + simulation.Context.ProjectilesAlive);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated == 0L, "a tick allocated " + (allocated / 100.0) + " bytes on average");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static IBattleSession Create(
            string floorId,
            int hp,
            IReadOnlyList<WeaponLoadout>? weapons = null,
            int? maxHp = null)
        {
            IGameConfig config = ConfigFixture.Load();
            var factory = new EcsBattleSessionFactory(config);
            return factory.Create(Setup(floorId, hp, weapons, maxHp));
        }

        private static BattleSetup Setup(
            string floorId,
            int hp,
            IReadOnlyList<WeaponLoadout>? weapons = null,
            int? maxHp = null)
        {
            IGameConfig config = ConfigFixture.Load();
            bool bossFloor = ConfigFixture.Floor(config, floorId).IsBoss;

            var setup = new BattleSetup
            {
                Seed = 20261015u,
                StageId = floorId,
                DifficultyId = "difficulty.normal",
                FloorIndex = ConfigFixture.Floor(config, floorId).Index,
                IsBossFloor = bossFloor,
                CurrentHp = hp,
                DevCheatsAllowed = true,
                Loadout = new CombatLoadoutSnapshot
                {
                    Stats = new PlayerStatBlock
                    {
                        MaxHp = maxHp ?? Math.Max(hp, config.Constant.MaxHp),
                        MaxMp = config.Constant.MaxMp,
                        MoveSpeed = config.Constant.MoveSpeed,
                        PickupRadius = config.Constant.PickupRadius,
                        FocusPerSecond = config.Constant.FocusPerSecond,
                    },
                },
            };

            if (weapons is not null)
            {
                var list = new List<WeaponLoadout>(weapons.Count);
                for (int i = 0; i < weapons.Count; i++)
                {
                    list.Add(weapons[i]);
                }

                setup.Loadout.Weapons = list;
            }

            return setup;
        }

        /// <summary>Turns a skill row into the loadout entry the battle casts from.</summary>
        private static WeaponLoadout Weapon(IGameConfig config, string skillId)
        {
            SkillConfig skill = ConfigFixture.Skill(config, skillId);
            return new WeaponLoadout
            {
                SkillId = skill.Id,
                Tier = 1,
                Behavior = skill.Behavior,
                Element = skill.Element,
                CooldownSeconds = skill.CooldownSeconds,
                MpCost = skill.MpCost,
                Damage = skill.Damage,
                ProjectileSpeed = skill.ProjectileSpeed,
                Range = skill.Range,
                PierceCount = skill.PierceCount,
                ProjectileCount = skill.ProjectileCount,
            };
        }

        private static Position PlayerPosition(EcsBattleSimulation simulation) =>
            simulation.Context.World.GetPool<Position>().Get(simulation.Context.PlayerEntity);

        private static Mana PlayerMana(EcsBattleSimulation simulation) =>
            simulation.Context.World.GetPool<Mana>().Get(simulation.Context.PlayerEntity);

        private static float NearestGhostDistance(IBattleSession session)
        {
            IBattleFrame frame = session.Frame;
            float best = float.MaxValue;
            float playerX = 0f;
            float playerY = 0f;
            bool found = false;

            foreach (RenderEntity entity in frame.Entities)
            {
                if (entity.Kind == RenderEntityKind.Player)
                {
                    playerX = entity.X;
                    playerY = entity.Y;
                    found = true;
                    break;
                }
            }

            Assert.True(found, "the frame carried no player");

            foreach (RenderEntity entity in frame.Entities)
            {
                if (entity.Kind != RenderEntityKind.Enemy)
                {
                    continue;
                }

                float dx = entity.X - playerX;
                float dy = entity.Y - playerY;
                float distance = (float)Math.Sqrt((dx * dx) + (dy * dy));
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private static List<BattleEvent> ReadEvents(IBattleSession session)
        {
            var events = new List<BattleEvent>(BattleConstants.EventRingCapacity);
            var buffer = new BattleEvent[BattleConstants.EventRingCapacity];
            long cursor = 0;

            int read;
            do
            {
                read = session.Events.Read(ref cursor, buffer, out _);
                for (int i = 0; i < read; i++)
                {
                    events.Add(buffer[i]);
                }
            }
            while (read == buffer.Length);

            return events;
        }

        private static int Count(List<BattleEvent> events, BattleEventType type)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
