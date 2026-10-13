namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>Why a battle ended. Carried on <see cref="BattleResult"/> and on the <c>BattleEnded</c> event.</summary>
    public enum BattleEndReason
    {
        /// <summary>No end condition has been reached (only valid while <see cref="BattlePhase.Running"/>).</summary>
        None = 0,

        /// <summary>A normal floor was survived for its full duration.</summary>
        SurvivedAllWaves = 1,

        /// <summary>The boss of a boss floor was killed before the timer ran out.</summary>
        BossDefeated = 2,

        /// <summary>The player's HP reached zero.</summary>
        PlayerDied = 3,

        /// <summary>A boss floor reached its full duration without the boss dying.</summary>
        Timeout = 4,

        /// <summary>The player surrendered, or a session-ending dev command was used.</summary>
        Surrendered = 5,
    }
}
