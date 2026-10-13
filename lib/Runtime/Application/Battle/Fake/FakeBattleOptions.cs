namespace ProjectSurvive.Core.Application.Battle.Fake
{
    /// <summary>
    /// Tunable knobs of <see cref="FakeBattleSession"/>. They exist so tests (and
    /// a dev build before Stage 07 lands) can force a specific outcome, not to
    /// hold balance numbers — none of these are gameplay data.
    /// </summary>
    public sealed class FakeBattleOptions
    {
        /// <summary>Force a win at this tick (1-based). 0 disables the override.</summary>
        public int WinAtTick { get; set; }

        /// <summary>Force a loss at this tick (1-based). 0 disables the override.</summary>
        public int LoseAtTick { get; set; }

        /// <summary>Force a timeout loss at this tick (1-based). 0 disables the override.</summary>
        public int TimeoutAtTick { get; set; }

        /// <summary>How many dummy entities the fake keeps on the field, walking in a circle.</summary>
        public int DummyEntityCount { get; set; } = 16;

        /// <summary>Ticks before a dummy entity is recycled (dies and respawns). At least 1.</summary>
        public int EntityLifetimeTicks { get; set; } = 8;

        /// <summary>Damage per second applied to the player. 0 means no pressure, so the player survives the timer.</summary>
        public int DamagePerSecond { get; set; }
    }
}
