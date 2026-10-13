using System.Diagnostics;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Infrastructure
{
    /// <summary>
    /// The real <see cref="IClock"/>: monotonic elapsed time from
    /// <see cref="Stopwatch"/>, which is unaffected by wall-clock changes
    /// (NTP steps, DST, the player changing the system time).
    /// </summary>
    /// <remarks>
    /// Lives in Infrastructure because it is the platform-facing side of the
    /// port: Core code depends on <see cref="IClock"/> only. Works under WebGL,
    /// where <see cref="Stopwatch"/> is backed by the browser's high-resolution
    /// timer and does not need a thread.
    /// </remarks>
    public sealed class SystemClock : IClock
    {
        private readonly Stopwatch _stopwatch;

        /// <summary>Starts the clock. Epoch is this constructor call.</summary>
        public SystemClock()
        {
            _stopwatch = Stopwatch.StartNew();
        }

        /// <inheritdoc />
        public double NowSeconds => _stopwatch.Elapsed.TotalSeconds;

        /// <summary>Restarts the epoch and returns the elapsed time it discarded.</summary>
        public double Restart()
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            _stopwatch.Restart();
            return elapsed;
        }
    }
}
