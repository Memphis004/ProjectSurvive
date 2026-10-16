using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Builds <see cref="BattleSetup"/> values for the contract tests. Floor ids
    /// and loadout numbers are the real ones from <c>data/</c>, so a duration or
    /// boss flag change in the tables is felt here.
    /// </summary>
    internal static class BattleTestFixtures
    {
        /// <summary><c>floor.forest_1</c> — a normal 60s floor.</summary>
        internal const string NormalFloorId = "floor.forest_1";

        /// <summary><c>floor.forest_5</c> — the 90s boss floor.</summary>
        internal const string BossFloorId = "floor.forest_5";

        /// <summary>Default max HP used by the test loadout.</summary>
        internal const int DefaultMaxHp = 100;

        /// <summary>Default max MP used by the test loadout (<c>constant.max_mp</c>).</summary>
        internal const int DefaultMaxMp = 50;

        /// <summary>
        /// <c>skill.fire_bolt</c> (0.9 s = 27 ticks) — the weapon the caster fixture casts.
        /// </summary>
        /// <remarks>
        /// A projectile, not the shortest cooldown in the table, on purpose: the contract suite's
        /// EcsLite floor spawns its ghosts on the arena ring, and a cast that needs a target inside
        /// its reach (a melee sweep, an aura) would not fire inside a case's window. A projectile
        /// is aimed at the nearest ghost anywhere in the arena, so the cooldown cycle a case watches
        /// is the real auto-cast rule rather than an artefact of the load.
        /// </remarks>
        internal const string WeaponSkillId = "skill.fire_bolt";

        /// <summary><c>skill.thread_pierce</c> (1.2 s = 36 ticks) — the familiar the caster fixture summons.</summary>
        internal const string SummonSkillId = "skill.thread_pierce";

        /// <summary>Board instance id the fixture gives its weapon, so the frame's mapping is checkable.</summary>
        internal const int WeaponInstanceId = 101;

        /// <summary>Board instance id the fixture gives its familiar.</summary>
        internal const int SummonInstanceId = 202;

        /// <summary>
        /// One <see cref="WeaponLoadout"/> built from a real <c>skill</c> row.
        /// </summary>
        /// <remarks>
        /// The same projection <c>LoadoutResolver</c> makes for a weapon on the board, minus the
        /// combo and upgrade maths: every combat number comes from the table, so the published
        /// cooldown period tracks <c>data/</c> instead of a number copied into the test. The
        /// instance id is the fixture's, which is what the frame's mapping to the board is
        /// checked against (there is no board in a headless test).
        /// </remarks>
        internal static WeaponLoadout Weapon(string skillId, int instanceId)
        {
            SkillConfig skill = ConfigFixture.Skill(ConfigFixture.Load(), skillId);
            return new WeaponLoadout
            {
                InstanceId = instanceId,
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

        /// <summary>One <see cref="SummonLoadout"/> built from a real <c>skill</c> row.</summary>
        internal static SummonLoadout Summon(string skillId, int instanceId)
        {
            SkillConfig skill = ConfigFixture.Skill(ConfigFixture.Load(), skillId);
            return new SummonLoadout
            {
                InstanceId = instanceId,
                SkillId = skill.Id,
                Tier = 1,
                CooldownSeconds = skill.CooldownSeconds,
                MpCost = skill.MpCost,
                Damage = skill.Damage,
                Range = skill.Range,
            };
        }

        internal static BattleSetup Setup(
            bool bossFloor,
            int currentHp = DefaultMaxHp,
            uint seed = 20261014u,
            bool devCheatsAllowed = true)
        {
            return new BattleSetup
            {
                Seed = seed,
                StageId = bossFloor ? BossFloorId : NormalFloorId,
                DifficultyId = "difficulty.normal",
                FloorIndex = bossFloor ? 5 : 1,
                IsBossFloor = bossFloor,
                CurrentHp = currentHp,
                DevCheatsAllowed = devCheatsAllowed,
                Loadout = new CombatLoadoutSnapshot
                {
                    Stats = new PlayerStatBlock
                    {
                        MaxHp = DefaultMaxHp,
                        MaxMp = DefaultMaxMp,
                        MoveSpeed = 4f,
                        PickupRadius = 1.5f,
                        FocusPerSecond = 5f,
                    },
                },
            };
        }
    }
}
