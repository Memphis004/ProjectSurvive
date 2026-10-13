using ProjectSurvive.Core.Domain.Combat.Battle;

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
                        MaxMp = 50,
                        MoveSpeed = 4f,
                        PickupRadius = 1.5f,
                        FocusPerSecond = 5f,
                    },
                },
            };
        }
    }
}
