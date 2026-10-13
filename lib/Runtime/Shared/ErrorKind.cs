namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// Broad classification of a failed command (AGENTS.md rule 7). Callers
    /// switch on this when one reaction covers a family of failures; the exact
    /// reason always lives in <see cref="Error.Code"/>.
    /// </summary>
    public enum ErrorKind
    {
        /// <summary>The request itself is malformed: bad range, bad shape, contradictory input.</summary>
        Validation = 0,

        /// <summary>The request is well-formed but collides with current state (occupied cell, overlapping bag).</summary>
        Conflict = 1,

        /// <summary>The referenced thing does not exist (unknown item, missing floor, empty slot).</summary>
        NotFound = 2,

        /// <summary>Not enough of a resource: Bia, MP, chest slots, bag capacity.</summary>
        Insufficient = 3,

        /// <summary>Not allowed in the current state or scope (editing the bag outside Camp).</summary>
        InvalidState = 4,
    }
}
