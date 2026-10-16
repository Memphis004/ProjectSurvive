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

        /// <summary>
        /// Dev: credit bia straight to the floor's takings. <see cref="BattleCommand.Arg"/> is the
        /// amount and must be positive.
        /// </summary>
        /// <remarks>
        /// It goes through the floor's currency (<c>BattleContext.CurrencyEarned</c>) on purpose
        /// rather than into the run's wallet: bia earned in a floor is credited by the settlement,
        /// so a dev grant takes exactly the path a picked-up drop takes — same HUD number, same
        /// payout rules, no second way for money to reach the purse.
        /// </remarks>
        DevAddBia = 5,

        /// <summary>Dev: put one more ghost on the field, at the arena's edge like a wave spawn.</summary>
        /// <remarks>
        /// <see cref="BattleCommand.Arg"/> is unused. A field already at its enemy ceiling has no
        /// room and simply does not get the extra ghost — the same ceiling a wave spawn hits — and
        /// the command still reports <c>Ok</c>, because the request was accepted, not refused.
        /// </remarks>
        DevSpawnGhost = 6,
    }
}
