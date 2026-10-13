using System.Collections.Generic;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// What a finished battle reports back to the Run flow.
    /// </summary>
    /// <remarks>
    /// Only produced once the session has ended (<see cref="IBattleSession.TryGetResult"/>).
    /// The Run flow owns what happens next: the 15% clear heal, settlement ids,
    /// currency crediting. The battle reports facts and never mutates run state.
    /// </remarks>
    public sealed class BattleResult
    {
        /// <summary><see cref="BattlePhase.Won"/>, <see cref="BattlePhase.Lost"/> or <see cref="BattlePhase.Aborted"/>.</summary>
        public BattlePhase Outcome { get; set; }

        /// <summary>Why it ended.</summary>
        public BattleEndReason EndReason { get; set; }

        /// <summary>Ticks survived.</summary>
        public long SurvivedTicks { get; set; }

        /// <summary>Player HP at the end. Always in <c>[0, MaxHp]</c>.</summary>
        public int FinalHp { get; set; }

        /// <summary>Currency earned in this battle.</summary>
        public int CurrencyEarned { get; set; }

        /// <summary>Enemies killed.</summary>
        public int Kills { get; set; }

        /// <summary>Consumables still unused, aligned with <see cref="CombatLoadoutSnapshot.Consumables"/> order.</summary>
        public IReadOnlyList<int> RemainingConsumables { get; set; } = new List<int>();

        /// <summary>True when the run should be offered a clear reward.</summary>
        public bool IsWin => Outcome == BattlePhase.Won;

        /// <inheritdoc />
        public override string ToString() => Outcome + "/" + EndReason + " ticks=" + SurvivedTicks + " hp=" + FinalHp;
    }
}
