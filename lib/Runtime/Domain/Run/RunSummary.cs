using System;

namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// How a finished run ended (GDD: the Result screen shows Victory or Defeat).
    /// </summary>
    /// <remarks>
    /// A read over existing run state rather than a stored flag: <see cref="RunSession.Outcome"/>
    /// derives it from the phase and the floor cursor, so there is no second copy of "did this run
    /// win" that a command has to remember to set.
    /// </remarks>
    public enum RunOutcome
    {
        /// <summary>The run has not ended; the summary screen must not be shown.</summary>
        InProgress = 0,

        /// <summary>Every floor of the run was cleared.</summary>
        Victory = 1,

        /// <summary>The run ended on a floor it did not clear.</summary>
        Defeat = 2,
    }

    /// <summary>
    /// What the summary screen reports about a finished run: how it ended, how far it got and what
    /// it killed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Produced by <see cref="RunSession.Summary"/> and read once by the Result screen's presenter.
    /// The screen itself computes nothing (AGENTS rule 10), which is why the numbers are gathered
    /// here in Domain where the rules live — including <see cref="GhostsDefeated"/>, which is the
    /// sum of the floors' own <c>BattleResult.Kills</c> and exists nowhere else.
    /// </para>
    /// <para>
    /// A readonly struct, like <see cref="FloorSettlement"/>: it is a report of a moment, and a
    /// caller holding one must not be able to make the run disagree with it.
    /// </para>
    /// </remarks>
    public readonly struct RunSummary
    {
        /// <summary>Creates a summary; the run does this, tests may too.</summary>
        public RunSummary(
            uint seed,
            RunOutcome outcome,
            int floorReached,
            int totalFloors,
            int floorsCleared,
            int ghostsDefeated,
            int bia,
            int hp,
            int maxHp,
            int lastSettlementId)
        {
            Seed = seed;
            Outcome = outcome;
            FloorReached = floorReached;
            TotalFloors = totalFloors;
            FloorsCleared = floorsCleared;
            GhostsDefeated = ghostsDefeated;
            Bia = bia;
            Hp = hp;
            MaxHp = maxHp;
            LastSettlementId = lastSettlementId;
        }

        /// <summary>The run's seed, for a bug report or a replay.</summary>
        public uint Seed { get; }

        /// <summary>Victory, defeat, or in progress.</summary>
        public RunOutcome Outcome { get; }

        /// <summary>
        /// The 1-based floor the run reached — the floor being attempted when it ended, so a defeat
        /// on floor 3 reports 3 and a cleared run reports the last floor.
        /// </summary>
        public int FloorReached { get; }

        /// <summary>How many floors the run has (the floor table's row count).</summary>
        public int TotalFloors { get; }

        /// <summary>How many floors were cleared.</summary>
        public int FloorsCleared { get; }

        /// <summary>Ghosts the run killed, over every floor it fought ("ผีที่ปราบ").</summary>
        public int GhostsDefeated { get; }

        /// <summary>Bia held when the run ended.</summary>
        public int Bia { get; }

        /// <summary>HP when the run ended.</summary>
        public int Hp { get; }

        /// <summary>Maximum HP (<c>constant.max_hp</c>).</summary>
        public int MaxHp { get; }

        /// <summary>The last settlement paid out; 0 when no floor was settled.</summary>
        public int LastSettlementId { get; }

        /// <summary>True once the run has ended — the summary is worth showing.</summary>
        public bool IsFinished => Outcome != RunOutcome.InProgress;

        /// <summary>True when every floor was cleared.</summary>
        public bool IsVictory => Outcome == RunOutcome.Victory;

        /// <inheritdoc />
        public override string ToString() =>
            Outcome + " floor " + FloorReached + "/" + TotalFloors + " kills " + GhostsDefeated +
            " bia " + Bia + " hp " + Hp + "/" + MaxHp + " seed " + Seed;

        /// <summary>Throws when the outcome is not one a summary screen may show.</summary>
        /// <exception cref="InvalidOperationException">The run has not ended.</exception>
        public RunSummary RequireFinished()
        {
            if (!IsFinished)
            {
                throw new InvalidOperationException("The run has not ended, so it has no summary.");
            }

            return this;
        }
    }
}
