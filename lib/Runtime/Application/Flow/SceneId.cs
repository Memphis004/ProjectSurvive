namespace ProjectSurvive.Core.Application.Flow
{
    /// <summary>
    /// The scenes the shell can show, in the order a session visits them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scene name is an Application concept, not a rule: Domain never needs to know that a
    /// "Hub" or an "Encounter" exists, while Presentation and the scene loader do. That is why
    /// this enum lives here rather than in <c>Domain</c> — see
    /// <c>docs/template/inventory.md</c> (row <c>SceneId</c>).
    /// </para>
    /// <para>
    /// The template's loop is <c>Boot → Title → Hub ⇄ Encounter → Result → Title</c>. A game
    /// built from this template renames the middle scenes (a rest area, an arena, a shop) but
    /// keeps the shape: one place where a run is prepared, one where it is played, one where it
    /// is summarised.
    /// </para>
    /// </remarks>
    public enum SceneId
    {
        /// <summary>Composition root only; never shown to the player.</summary>
        Boot = 0,

        /// <summary>Where a run is born (ADR 0022) and where arriving ends one.</summary>
        Title = 1,

        /// <summary>Between encounters: the run's preparation screen.</summary>
        Hub = 2,

        /// <summary>One unit of play, stepped by <c>FixedTicker</c>.</summary>
        Encounter = 3,

        /// <summary>The run's summary, reachable from Hub and Encounter.</summary>
        Result = 4,
    }
}
