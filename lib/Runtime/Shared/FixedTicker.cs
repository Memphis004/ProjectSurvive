using System;

namespace ProjectSurvive.Core.Shared
{
    /// <summary>
    /// Turns variable real-time frame deltas into a fixed number of simulation
    /// steps at a constant rate — the battle clock of AGENTS.md rule 6 (30 Hz).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pure and allocation-free: the same sequence of deltas always yields the
    /// same sequence of tick counts. The ticker never calls back into user code,
    /// so the caller owns the tick loop and there is no closure on the hot path.
    /// </para>
    /// <para>
    /// Real time is allowed to fall behind: when one frame is long enough to owe
    /// more ticks than <c>maxTicksPerAdvance</c> (a WebGL tab stall, a breakpoint),
    /// the surplus is discarded instead of replayed, which would otherwise spiral
    /// into ever-longer frames. Discarded ticks are counted in
    /// <see cref="DroppedTicks"/> rather than silently ignored.
    /// </para>
    /// </remarks>
    public sealed class FixedTicker
    {
        /// <summary>The battle simulation rate fixed by the GDD.</summary>
        public const double DefaultTickHz = 30.0;

        /// <summary>Maximum simulation steps one <see cref="Advance"/> may return.</summary>
        public const int DefaultMaxTicksPerAdvance = 4;

        /// <summary>
        /// A frame longer than this is a stall or a pause, not elapsed game time.
        /// Bounding the input also keeps every cast in <see cref="Advance"/> in range.
        /// </summary>
        private const double MaxDeltaSeconds = 3600.0;

        /// <summary>Absorbs double rounding so a delta that is exactly one tick is not lost.</summary>
        private const double Tolerance = 1e-9;

        private readonly double _tickSeconds;
        private readonly int _maxTicksPerAdvance;
        private double _pendingSeconds;

        /// <summary>Creates a ticker running at <paramref name="tickHz"/>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="tickHz"/> is not a positive finite number, or
        /// <paramref name="maxTicksPerAdvance"/> is below 1.
        /// </exception>
        public FixedTicker(double tickHz = DefaultTickHz, int maxTicksPerAdvance = DefaultMaxTicksPerAdvance)
        {
            if (double.IsNaN(tickHz) || double.IsInfinity(tickHz) || tickHz <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickHz), tickHz, "tickHz must be a positive finite number.");
            }

            if (maxTicksPerAdvance < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxTicksPerAdvance), maxTicksPerAdvance, "maxTicksPerAdvance must be at least 1.");
            }

            TickHz = tickHz;
            _tickSeconds = 1.0 / tickHz;
            _maxTicksPerAdvance = maxTicksPerAdvance;
        }

        /// <summary>The configured rate in ticks per second.</summary>
        public double TickHz { get; }

        /// <summary>Length of one tick in seconds.</summary>
        public double TickSeconds => _tickSeconds;

        /// <summary>Total number of ticks handed out so far.</summary>
        public long TickIndex { get; private set; }

        /// <summary>Fractional time left over, always in <c>[0, TickSeconds)</c>.</summary>
        public double PendingSeconds => _pendingSeconds;

        /// <summary>Ticks that were due but discarded because a frame ran over <c>maxTicksPerAdvance</c>.</summary>
        public long DroppedTicks { get; private set; }

        /// <summary>
        /// Feeds one frame delta and returns how many fixed steps to run now.
        /// Returns 0 for a zero, negative, NaN, or infinite delta, so a paused or
        /// broken frame source cannot advance the simulation.
        /// </summary>
        public int Advance(double deltaSeconds)
        {
            if (!(deltaSeconds > 0.0) || double.IsInfinity(deltaSeconds))
            {
                return 0;
            }

            if (deltaSeconds > MaxDeltaSeconds)
            {
                deltaSeconds = MaxDeltaSeconds;
            }

            _pendingSeconds += deltaSeconds;

            double maxPendingSeconds = _maxTicksPerAdvance * _tickSeconds;
            if (_pendingSeconds > maxPendingSeconds)
            {
                long due = (long)((_pendingSeconds - maxPendingSeconds) / _tickSeconds);
                if (due > 0)
                {
                    DroppedTicks += due;
                    _pendingSeconds = maxPendingSeconds;
                }
            }

            int ticks = (int)((_pendingSeconds + (Tolerance * _tickSeconds)) / _tickSeconds);
            if (ticks > _maxTicksPerAdvance)
            {
                ticks = _maxTicksPerAdvance;
            }

            if (ticks <= 0)
            {
                return 0;
            }

            _pendingSeconds -= ticks * _tickSeconds;
            if (_pendingSeconds < 0.0)
            {
                _pendingSeconds = 0.0;
            }

            TickIndex += ticks;
            return ticks;
        }

        /// <summary>Restarts the clock: no pending time, no ticks, no dropped-tick history.</summary>
        public void Reset()
        {
            TickIndex = 0;
            _pendingSeconds = 0.0;
            DroppedTicks = 0;
        }
    }
}
