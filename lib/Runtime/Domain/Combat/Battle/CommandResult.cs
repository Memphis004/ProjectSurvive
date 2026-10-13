namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The outcome of <see cref="IBattleSession.Submit"/>. A rejection is a value,
    /// never an exception, so a caller (a dev menu, a UI, a test) can branch on it.
    /// </summary>
    public enum CommandResult
    {
        /// <summary>The command was accepted and applied.</summary>
        Ok = 0,

        /// <summary>The session is not <see cref="BattlePhase.Running"/>.</summary>
        RejectedWrongPhase = 1,

        /// <summary>The command's argument is not in range (e.g. <c>DevGodMode</c> with an arg other than 0 or 1).</summary>
        RejectedInvalidArg = 2,

        /// <summary>The command exists but is not allowed here (a dev command with cheats disabled).</summary>
        RejectedNotAllowed = 3,
    }
}
