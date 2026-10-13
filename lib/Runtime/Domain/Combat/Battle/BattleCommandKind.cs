namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>The kind of a <see cref="BattleCommand"/>.</summary>
    public enum BattleCommandKind
    {
        /// <summary>End the battle as <see cref="BattlePhase.Aborted"/> / <see cref="BattleEndReason.Surrendered"/>.</summary>
        Surrender = 0,

        /// <summary>Dev: kill every enemy currently on the field.</summary>
        DevKillAll = 1,

        /// <summary>Dev: toggle player invulnerability. <see cref="BattleCommand.Arg"/> 1 = on, 0 = off.</summary>
        DevGodMode = 2,

        /// <summary>Dev: force a win.</summary>
        DevWinNow = 3,

        /// <summary>Dev: force a loss.</summary>
        DevLoseNow = 4,
    }
}
