namespace ProjectSurvive.Core.Domain.Run
{
    /// <summary>
    /// The phase of one run (GDD v0.3: Title → Camp → Battle → Camp → … → Result).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is part of the run state hash, so the numbers are fixed: a
    /// reordered enum would change a replayed hash without changing any rule.
    /// </para>
    /// <para>
    /// This is the <b>rule</b> phase. Which Unity scene renders it is
    /// <see cref="SceneId"/>, and the mapping between the two is deliberately
    /// explicit (see <see cref="SceneFlowMap"/>) so a new phase cannot silently
    /// inherit whatever scene happened to share its number.
    /// </para>
    /// </remarks>
    public enum RunFlowState
    {
        /// <summary>Before the run starts: the title screen.</summary>
        Title = 0,

        /// <summary>At the camp (ศาลาพัก): the only place the bag, the shop and the wallet may change.</summary>
        Camp = 1,

        /// <summary>A floor is being fought; the bag is locked.</summary>
        Battle = 2,

        /// <summary>The run is over — won or lost. The summary screen.</summary>
        Result = 3,
    }
}
