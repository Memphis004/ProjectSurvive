namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The lifecycle phase of a battle session.
    /// </summary>
    /// <remarks>
    /// <see cref="Running"/> is 0 on purpose: a freshly created session is
    /// running, and a default-initialised value is a legal phase. Value 1 is
    /// deliberately skipped and reserved for <c>AwaitingChoice</c>, which the
    /// prototype does not have (there is no level-up / blessing / XP in GDD v0.3
    /// — AGENTS rule: do not build a choice gate the design does not ask for).
    /// </remarks>
    public enum BattlePhase
    {
        /// <summary>The tick advances on <see cref="IBattleSession.Step"/>.</summary>
        Running = 0,

        // 1 = AwaitingChoice (reserved; unused in the prototype).

        /// <summary>The floor was cleared. See <see cref="BattleEndReason"/>.</summary>
        Won = 2,

        /// <summary>The floor was failed. See <see cref="BattleEndReason"/>.</summary>
        Lost = 3,

        /// <summary>The session was ended early (surrender / dev command).</summary>
        Aborted = 4,
    }
}
