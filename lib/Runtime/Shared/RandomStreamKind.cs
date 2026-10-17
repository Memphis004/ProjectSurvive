namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// The independent randomness streams of a run (AGENTS.md rule 6). A reward roll must never
    /// change how many attacks happen in an encounter, and cosmetic jitter must never change
    /// either — so each stream owns its own generator state and is seeded separately.
    /// </summary>
    public enum RandomStreamKind
    {
        /// <summary>Run-level rolls: loot tables, prices, anything decided outside play.</summary>
        Run = 1,

        /// <summary>Everything that decides play outcomes: spawns, crits, damage variance.</summary>
        Encounter = 2,

        /// <summary>Cosmetic only — particle jitter, idle offsets. Never read back by rules.</summary>
        Visual = 3,

        /// <summary>
        /// A reward draw. Its seed is <i>derived from</i> rather than drawn from: an offer is a
        /// pure function of the run seed and the position (encounter ordinal, choice ordinal), so
        /// asking twice about one state gives the same answer (ADR 0021). Consuming a shared
        /// stream here would make an offer depend on how many offers came before it — which is
        /// exactly the re-roll an undo would then buy.
        /// </summary>
        Reward = 4,
    }
}
