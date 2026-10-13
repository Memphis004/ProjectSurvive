using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The fixed, engine-level constants of the battle contract (GDD v0.3).
    /// </summary>
    /// <remarks>
    /// These are structural — how fast the simulation ticks, how many steps a
    /// frame may run, how large the event ring and the render snapshot are — and
    /// never gameplay balance. Every balance number (HP, damage, cooldown,
    /// duration, spawn counts) lives in <c>data/</c> and reaches the battle
    /// through <see cref="ProjectSurvive.Core.Domain.Config.IGameConfig"/>
    /// (AGENTS rule 5).
    /// </remarks>
    public static class BattleConstants
    {
        /// <summary>Ticks per second. Pinned to <see cref="FixedTicker.DefaultTickHz"/> so the two cannot drift.</summary>
        public const int TickRate = (int)FixedTicker.DefaultTickHz;

        /// <summary>
        /// Maximum simulation steps one presentation frame may run before the
        /// surplus is dropped (AGENTS rule 6; same policy as
        /// <see cref="FixedTicker"/>).
        /// </summary>
        public const int MaxStepsPerFrame = FixedTicker.DefaultMaxTicksPerAdvance;

        /// <summary>
        /// Scale of a movement axis in <see cref="PlayerInputFrame"/>: the range
        /// [-1, 1] is carried as [-1000, 1000] so the simulation works in integer
        /// space and an input frame never contains a float that could differ
        /// between two runs of the same seed.
        /// </summary>
        public const int InputAxisScale = 1000;

        /// <summary>Capacity of <see cref="BattleEventRing"/>. Older unread events are overwritten.</summary>
        public const int EventRingCapacity = 4096;

        /// <summary>Upper bound on the entities a frame may expose to Presentation.</summary>
        public const int MaxRenderEntities = 1024;
    }
}
