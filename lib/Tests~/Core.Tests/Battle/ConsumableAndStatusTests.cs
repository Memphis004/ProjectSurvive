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
    /// Stage 11a: offerings spend their data-authored uses, and an elemental status uses the
    /// <c>status</c> row rather than the applying skill's damage.
    /// </summary>
    public sealed class ConsumableAndStatusTests
    {
        private const string Forest1 = "floor.forest_1";
        private const string FireBolt = "skill.fire_bolt";

        // -----------------------------------------------------------------
        // Offerings
        // -----------------------------------------------------------------

        [Fact]
        public void An_offering_is_spent_at_its_threshold_and_never_beyond_its_uses()
        {
            IGameConfig config = ConfigFixture.Load();
            ConsumableLoadout offering = new ConsumableLoadout
            {
                ItemId = "item.red_soda",
                Count = 1,
                Effect = OfferingEffect.Heal,
                EffectAmount = 20,
                UsesPerItem = 2,
                AutoUseHpPercent = 40,
            };

            var simulation = (EcsBattleSimulation)Create(config, consumables: new[] { offering });
            EcsPool<Health> healths = simulation.Context.World.GetPool<Health>();
            int player = simulation.Context.PlayerEntity;

            // Below the threshold: one use heals 20.
            healths.Get(player).Hp = 30;
            simulation.Step(PlayerInputFrame.Idle);
            Assert.Equal(50, healths.Get(player).Hp);
            Assert.Equal(1, simulation.Context.ConsumableUses[0]);

            // A second crossing spends the second use.
            healths.Get(player).Hp = 30;
            simulation.Step(PlayerInputFrame.Idle);
            Assert.Equal(50, healths.Get(player).Hp);
            Assert.Equal(0, simulation.Context.ConsumableUses[0]);

            // A third crossing has nothing left to spend.
            healths.Get(player).Hp = 30;
            simulation.Step(PlayerInputFrame.Idle);
            Assert.Equal(30, healths.Get(player).Hp);
            Assert.Equal(0, simulation.Context.ConsumableUses[0]);

            Assert.Equal(CommandResult.Ok, simulation.Submit(BattleCommand.DevWinNow()));
            Assert.True(simulation.TryGetResult(out BattleResult result));
            Assert.Equal(0, Assert.Single(result.RemainingConsumables));
        }

        [Fact]
        public void A_bia_offering_pays_its_amount_when_the_threshold_is_crossed()
        {
            IGameConfig config = ConfigFixture.Load();
            ItemConfig definition = ConfigFixture.Item(config, "item.boiled_egg");

            var offering = new ConsumableLoadout
            {
                ItemId = definition.Id,
                Count = 1,
                Effect = OfferingEffect.Bia,
                EffectAmount = definition.OfferingAmount,
                UsesPerItem = definition.OfferingUses,
                AutoUseHpPercent = definition.OfferingAutoHpPercent,
            };

            var simulation = (EcsBattleSimulation)Create(config, consumables: new[] { offering });
            EcsPool<Health> healths = simulation.Context.World.GetPool<Health>();

            healths.Get(simulation.Context.PlayerEntity).Hp = 20;
            int before = simulation.Context.CurrencyEarned;
            simulation.Step(PlayerInputFrame.Idle);

            Assert.Equal(before + definition.OfferingAmount, simulation.Context.CurrencyEarned);
            Assert.Equal(0, simulation.Context.ConsumableUses[0]);
        }

        // -----------------------------------------------------------------
        // Status
        // -----------------------------------------------------------------

        [Fact]
        public void A_status_row_sets_damage_interval_duration_and_stacks()
        {
            IGameConfig config = ConfigFixture.Load();
            StatusConfig row = StatusRow(config, Element.Fire);
            const int defId = 7;

            Status status = default;
            StatusRules.Apply(ref status, row, defId);

            Assert.Equal(defId, status.DefId);
            Assert.Equal((int)row.Element, status.Element);
            Assert.Equal(1, status.Stacks);
            Assert.Equal(row.DamagePerTick, status.DamagePerTick);
            Assert.Equal(StatusRules.Ticks(row.TickIntervalSeconds), status.TickInterval);
            Assert.Equal(StatusRules.Ticks(row.DurationSeconds), status.RemainingTicks);

            // A second application adds a stack (up to the row's cap) and restarts the clock.
            StatusRules.Apply(ref status, row, defId);
            int expectedStacks = Math.Min(2, row.MaxStacks);
            Assert.Equal(expectedStacks, status.Stacks);
            Assert.Equal(row.DamagePerTick * expectedStacks, status.DamagePerTick);

            // Pushing past the cap never exceeds it.
            for (int i = 0; i < row.MaxStacks + 3; i++)
            {
                StatusRules.Apply(ref status, row, defId);
            }

            Assert.Equal(row.MaxStacks, status.Stacks);
            Assert.Equal(row.DamagePerTick * row.MaxStacks, status.DamagePerTick);
        }

        [Fact]
        public void A_fire_hit_applies_the_status_row_from_data()
        {
            IGameConfig config = ConfigFixture.Load();
            SkillConfig skill = ConfigFixture.Skill(config, FireBolt);
            Assert.Equal(Element.Fire, skill.Element);

            StatusConfig row = StatusRow(config, Element.Fire);
            WeaponLoadout weapon = new WeaponLoadout
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

            var simulation = (EcsBattleSimulation)Create(config, weapons: new[] { weapon });

            // A stationary, high-HP target inside the fire bolt's reach: the wave ghosts spawn on
            // the arena ring and may take longer than a test window to walk into a 6-unit reach.
            var slot = new WaveSlot
            {
                MaxHp = 100,
                MovePattern = (int)MovePattern.Stationary,
            };
            simulation.Context.Spawner.SpawnEnemy(simulation.Context, slot, 3f, 0f);
            simulation.Context.EnemiesAlive++;

            EcsFilter filter = simulation.Context.World
                .Filter<Status>()
                .Inc<Health>()
                .End(BattleArena.FilterDenseCapacity);

            for (int i = 0; i < 200 && filter.GetEntitiesCount() == 0; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
            }

            Assert.True(
                filter.GetEntitiesCount() > 0,
                "no hit ever applied the fire status (tick " + simulation.Context.Tick + ", kills "
                + simulation.Context.Kills + ", enemies " + simulation.Context.EnemiesAlive
                + ", projectiles " + simulation.Context.ProjectilesAlive
                + ", casters " + simulation.Context.World.Filter<Caster>().End(BattleArena.FilterDenseCapacity).GetEntitiesCount()
                + ", phase " + simulation.Phase + ")");

            EcsPool<Status> statuses = simulation.Context.World.GetPool<Status>();
            int expectedDefId = simulation.Context.DefIds.Status(row.Id);
            bool found = false;

            foreach (int entity in filter)
            {
                Status status = statuses.Get(entity);
                Assert.Equal(expectedDefId, status.DefId);
                Assert.Equal((int)row.Element, status.Element);
                Assert.InRange(status.Stacks, 1, row.MaxStacks);
                Assert.Equal(row.DamagePerTick * status.Stacks, status.DamagePerTick);
                Assert.Equal(StatusRules.Ticks(row.TickIntervalSeconds), status.TickInterval);
                found = true;
            }

            Assert.True(found);
        }

        // -----------------------------------------------------------------
        // Allocation
        // -----------------------------------------------------------------

        [Fact]
        public void A_tick_with_a_status_and_an_offering_allocates_nothing_after_warm_up()
        {
            IGameConfig config = ConfigFixture.Load();
            SkillConfig skill = ConfigFixture.Skill(config, FireBolt);
            var weapon = new WeaponLoadout
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

            var offering = new ConsumableLoadout
            {
                ItemId = "item.red_soda",
                Count = 1,
                Effect = OfferingEffect.Heal,
                EffectAmount = 20,
                UsesPerItem = 2,
                AutoUseHpPercent = 0,
            };

            var simulation = (EcsBattleSimulation)Create(
                config, hp: 100_000, maxHp: 100_000, weapons: new[] { weapon }, consumables: new[] { offering });

            for (int i = 0; i < 120; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 30; i++)
            {
                simulation.Step(PlayerInputFrame.Idle);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(allocated == 0L, "a tick allocated " + (allocated / 30.0) + " bytes on average");
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static IBattleSession Create(
            IGameConfig config,
            int hp = 100,
            int maxHp = 100,
            IReadOnlyList<WeaponLoadout>? weapons = null,
            IReadOnlyList<ConsumableLoadout>? consumables = null)
        {
            bool bossFloor = ConfigFixture.Floor(config, Forest1).IsBoss;

            var setup = new BattleSetup
            {
                Seed = 20261019u,
                StageId = Forest1,
                DifficultyId = "difficulty.normal",
                FloorIndex = ConfigFixture.Floor(config, Forest1).Index,
                IsBossFloor = bossFloor,
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
                    Weapons = weapons ?? Array.Empty<WeaponLoadout>(),
                    Consumables = consumables ?? Array.Empty<ConsumableLoadout>(),
                },
            };

            return new EcsBattleSessionFactory(config).Create(setup);
        }

        private static StatusConfig StatusRow(IGameConfig config, Element element)
        {
            for (int i = 0; i < config.Statuses.Count; i++)
            {
                if (config.Statuses[i].Element == element)
                {
                    return config.Statuses[i];
                }
            }

            throw new InvalidOperationException("no status row for " + element);
        }
    }
}
