namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// Everything needed to start one battle (one floor).
    /// </summary>
    /// <remarks>
    /// Created by the Run/Scene flow (Stage 06/10) and handed to
    /// <see cref="IBattleSessionFactory.Create"/>. HP is carried <b>across</b>
    /// floors: the caller passes <see cref="CurrentHp"/> in and reads
    /// <see cref="BattleResult.FinalHp"/> back out. The 15% heal after clearing a
    /// floor is Run's job, not the battle's. Maximum HP lives in
    /// <see cref="Loadout"/>; the battle never invents it.
    /// </remarks>
    public sealed class BattleSetup
    {
        /// <summary>Run-level seed for the battle RNG stream (AGENTS rule 6). The shop/visual streams are untouched.</summary>
        public uint Seed { get; set; }

        /// <summary>Floor id from the floor table, e.g. <c>floor.forest_1</c>. Duration and win condition come from config.</summary>
        public string StageId { get; set; } = string.Empty;

        /// <summary>Difficulty id; the prototype has a single difficulty but the field is carried for later.</summary>
        public string DifficultyId { get; set; } = string.Empty;

        /// <summary>1-based run order of the floor, matching the floor table's <c>index</c>.</summary>
        public int FloorIndex { get; set; }

        /// <summary>True when this is the boss floor (must also agree with the floor table).</summary>
        public bool IsBossFloor { get; set; }

        /// <summary>Player HP entering the floor.</summary>
        public int CurrentHp { get; set; }

        /// <summary>The resolved build. Never read from the bag by the battle.</summary>
        public CombatLoadoutSnapshot Loadout { get; set; } = new CombatLoadoutSnapshot();

        /// <summary>When false, every dev command is rejected with <see cref="CommandResult.RejectedNotAllowed"/>.</summary>
        public bool DevCheatsAllowed { get; set; }
    }
}
