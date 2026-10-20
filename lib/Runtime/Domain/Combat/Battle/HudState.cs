namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// The values a battle HUD shows, as a snapshot of one frame.
    /// </summary>
    /// <remarks>
    /// A mutable value type owned by the session and handed out by copy, so a HUD
    /// read allocates nothing. Names follow the Luban columns (<c>hp</c>,
    /// <c>mp</c>, <c>currency</c>) rather than inventing a second vocabulary.
    /// There is no level / XP field: the prototype has no level-up or blessing
    /// (GDD v0.3). <see cref="ControlFlipTicks"/> is the one input state that
    /// <i>is</i> published — a boss's possession reverses the player's movement,
    /// and the HUD has to say so without the view guessing (Stage 11c, ADR 0030).
    /// </remarks>
    public struct HudState
    {
        /// <summary>Player HP.</summary>
        public int Hp;

        /// <summary>Player max HP.</summary>
        public int MaxHp;

        /// <summary>Player MP.</summary>
        public int Mp;

        /// <summary>Player max MP.</summary>
        public int MaxMp;

        /// <summary>Currency earned in this battle (เบี้ย).</summary>
        public int Currency;

        /// <summary>Enemies killed in this battle.</summary>
        public int Kills;

        /// <summary>Ticks elapsed in this floor.</summary>
        public int ElapsedTicks;

        /// <summary>Ticks the floor lasts (from the floor table).</summary>
        public int FloorDurationTicks;

        /// <summary>Index of the wave currently running, 0-based.</summary>
        public int WaveIndex;

        /// <summary>Boss HP; 0 on a floor with no boss.</summary>
        public int BossHp;

        /// <summary>Boss max HP; 0 on a floor with no boss.</summary>
        public int BossMaxHp;

        /// <summary>True when this floor has a boss.</summary>
        public bool HasBoss;

        /// <summary>
        /// Ticks the player's movement input is still reversed by a boss's possession, or
        /// <c>0</c> when the controls are normal (Stage 11c).
        /// </summary>
        /// <remarks>
        /// Published from the player's own <c>Possessed</c> component in the tick the movement
        /// rule reads it, so this is a fact about the simulation rather than a hint a view
        /// reconstructs from an event's start and end. A HUD asks "is this above zero" to draw
        /// its warning and divides by <see cref="ProjectSurvive.Core.Domain.Combat.Battle.BattleConstants.TickRate"/> for seconds.
        /// </remarks>
        public int ControlFlipTicks;

        /// <summary>Seconds remaining on the floor timer, for a HUD clock.</summary>
        public float RemainingSeconds(int tickRate) =>
            tickRate <= 0 ? 0f : (FloorDurationTicks - ElapsedTicks) / (float)tickRate;
    }
}
