namespace ProjectSurvive.Core.Domain.Config
{
    /// <summary>
    /// A language the localization table carries.
    /// </summary>
    /// <remarks>
    /// A hand-written mirror of the Luban enum with the same names and values, converted by a
    /// plain cast in the Infrastructure adapter — Domain must not reference generated code
    /// (ADR 0006). <c>ConfigEnumParityTests</c> is what keeps the two copies honest.
    /// </remarks>
    public enum Language
    {
        /// <summary>Thai.</summary>
        Th = 1,

        /// <summary>English.</summary>
        En = 2,
    }
}
