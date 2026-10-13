namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// Monotonic time source. Core code never reads <c>DateTime.Now</c>,
    /// <c>Time.time</c>, or <c>Stopwatch</c> directly (AGENTS.md rule 6); it takes
    /// this port instead, and an outer layer supplies the real implementation.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// Seconds since an unspecified fixed epoch. Never decreases, so callers
        /// may subtract two readings to get an elapsed duration.
        /// </summary>
        double NowSeconds { get; }
    }
}
