namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// The independent randomness streams of a run (AGENTS.md rule 6). Shop
    /// rolls must never change how many attacks happen in battle, and visual
    /// jitter must never change either — so each stream owns its own generator
    /// state and is seeded separately.
    /// </summary>
    public enum RandomStreamKind
    {
        /// <summary>Shop stock, refresh rolls, and any other Camp-side roll.</summary>
        Shop = 1,

        /// <summary>Everything that decides combat outcomes: spawns, crits, damage variance.</summary>
        Battle = 2,

        /// <summary>Cosmetic only — particle jitter, idle offsets. Never read back by rules.</summary>
        Visual = 3,

        /// <summary>
        /// The upgrade-choice draw (Stage 09f). Its seed is <i>derived from</i> rather than
        /// drawn from: a merge's three choices are a pure function of the run seed, the
        /// merge ordinal, the item and the tier step, so asking twice about one state
        /// gives the same three (ADR 0021). Consuming a shared stream here would make the
        /// offer depend on how many offers had been asked before — which is exactly the
        /// re-roll an undo would then buy.
        /// </summary>
        Upgrade = 4,
    }
}
