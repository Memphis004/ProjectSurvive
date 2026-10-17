using System;
using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>
    /// One run: a seed, its random streams, how many encounters were cleared, and how it ended.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the <b>Run</b> scope of the loop (ADR 0022): it is created when the title screen
    /// starts a run, lives across every Hub ⇄ Encounter crossing, and is disposed when the player
    /// arrives back at the title. Nothing here decides game rules — a game built on the template
    /// puts its own rules in <c>Domain</c> and calls them from here, which is why the type is a
    /// state holder with typed commands rather than a rules engine.
    /// </para>
    /// <para>
    /// Every state-changing command (AGENTS rule 7) is atomic: it validates first, mutates in one
    /// step, and returns a <see cref="Result"/> whose failure carries a stable code from
    /// <see cref="RunErrors"/>. A refused command leaves the run exactly as it was — including its
    /// replay log.
    /// </para>
    /// <para>
    /// Determinism (AGENTS rule 6) is the seed plus <see cref="RandomStreams"/>: no clock, no
    /// <c>System.Random</c>, no <c>DateTime</c>. <see cref="ComputeStateHash"/> exists so a replay
    /// can be compared by value instead of by inspecting fields.
    /// </para>
    /// </remarks>
    public sealed class RunSession : IDisposable
    {
        private readonly bool _recording;
        private bool _started;
        private bool _inEncounter;
        private bool _disposed;
        private int _encounterIndex;
        private RunOutcome _outcome;

        /// <summary>Creates a run for a seed, optionally recording its commands.</summary>
        /// <param name="seed">The run seed every stream is derived from.</param>
        /// <param name="recording">
        /// False for a replayed run: it must not grow a second copy of the log it is replaying.
        /// </param>
        public RunSession(uint seed, bool recording = true)
        {
            Seed = seed;
            Streams = new RandomStreams(seed);
            _recording = recording;
            Log = recording ? new ReplayLog(seed) : null;
        }

        /// <summary>The seed every random stream is derived from.</summary>
        public uint Seed { get; }

        /// <summary>The separated random streams of this run (AGENTS rule 6).</summary>
        public RandomStreams Streams { get; }

        /// <summary>How many encounters have been cleared.</summary>
        public int EncounterIndex => _encounterIndex;

        /// <summary>How the run ended, or <see cref="RunOutcome.InProgress"/>.</summary>
        public RunOutcome Outcome => _outcome;

        /// <summary>True once <see cref="Start"/> has succeeded.</summary>
        public bool IsStarted => _started;

        /// <summary>True between <see cref="BeginEncounter"/> and <see cref="EndEncounter"/>.</summary>
        public bool IsInEncounter => _inEncounter;

        /// <summary>True once the run has ended, by completion or by a lost encounter.</summary>
        public bool IsFinished => _outcome != RunOutcome.InProgress;

        /// <summary>The command log, or null when this run is a replay (<c>recording: false</c>).</summary>
        public ReplayLog? Log { get; }

        /// <summary>Starts the run.</summary>
        /// <returns><see cref="RunErrors.AlreadyStarted"/> once it has started.</returns>
        public Result Start()
        {
            if (_disposed)
            {
                return RefuseDisposed();
            }

            if (_started)
            {
                return RunErrors.Refuse(RunErrors.AlreadyStarted);
            }

            _started = true;
            return Record(RunCommandKind.StartRun);
        }

        /// <summary>Enters the next encounter.</summary>
        /// <returns>
        /// <see cref="RunErrors.NotStarted"/> before <see cref="Start"/>,
        /// <see cref="RunErrors.Finished"/> after the run ended, or
        /// <see cref="RunErrors.InEncounter"/> while one is already running.
        /// </returns>
        public Result BeginEncounter()
        {
            if (_disposed)
            {
                return RefuseDisposed();
            }

            if (!_started)
            {
                return RunErrors.Refuse(RunErrors.NotStarted);
            }

            if (IsFinished)
            {
                return RunErrors.Refuse(RunErrors.Finished);
            }

            if (_inEncounter)
            {
                return RunErrors.Refuse(RunErrors.InEncounter);
            }

            _inEncounter = true;
            return Record(RunCommandKind.BeginEncounter);
        }

        /// <summary>
        /// Ends the running encounter. A win counts one cleared encounter and returns to the Hub;
        /// a loss ends the run as <see cref="RunOutcome.Failed"/>.
        /// </summary>
        /// <returns><see cref="RunErrors.NotInEncounter"/> when no encounter is running.</returns>
        public Result EndEncounter(EncounterOutcome outcome)
        {
            if (_disposed)
            {
                return RefuseDisposed();
            }

            if (!_inEncounter)
            {
                return RunErrors.Refuse(RunErrors.NotInEncounter);
            }

            _inEncounter = false;
            if (outcome == EncounterOutcome.Won)
            {
                _encounterIndex++;
                return Record(RunCommandKind.WinEncounter);
            }

            _outcome = RunOutcome.Failed;
            return Record(RunCommandKind.LoseEncounter);
        }

        /// <summary>Finishes the run deliberately (from the Hub).</summary>
        /// <returns>
        /// <see cref="RunErrors.NotStarted"/>, <see cref="RunErrors.Finished"/>, or
        /// <see cref="RunErrors.InEncounter"/> — a run cannot be closed mid-encounter.
        /// </returns>
        public Result EndRun()
        {
            if (_disposed)
            {
                return RefuseDisposed();
            }

            if (!_started)
            {
                return RunErrors.Refuse(RunErrors.NotStarted);
            }

            if (IsFinished)
            {
                return RunErrors.Refuse(RunErrors.Finished);
            }

            if (_inEncounter)
            {
                return RunErrors.Refuse(RunErrors.InEncounter);
            }

            _outcome = RunOutcome.Completed;
            return Record(RunCommandKind.EndRun);
        }

        /// <summary>
        /// Returns the run to its initial state, keeping the seed. The log records the reset, so a
        /// replay of the same command list lands in the same place.
        /// </summary>
        public Result Reset()
        {
            if (_disposed)
            {
                return RefuseDisposed();
            }

            _started = false;
            _inEncounter = false;
            _encounterIndex = 0;
            _outcome = RunOutcome.InProgress;
            return Record(RunCommandKind.Reset);
        }

        /// <summary>
        /// A deterministic fingerprint of the run's state, for replay comparisons.
        /// </summary>
        /// <remarks>
        /// Not a hash of the random streams' positions: a stream's progress is a consequence of
        /// the rules a game adds, and this type does not run any. Seed, flags and counters are
        /// what the skeleton owns.
        /// </remarks>
        public uint ComputeStateHash()
        {
            uint state = Seed ^ 0x9E37_79B9u;
            state = Mix(state);
            state ^= (uint)(_encounterIndex * 0x85EB_CA6Bu);
            state = Mix(state);
            state ^= (uint)_outcome * 0xC2B2_AE35u;
            state = Mix(state);
            state ^= _started ? 0x27D4_EB2Fu : 0u;
            state ^= _inEncounter ? 0x1656_67B1u : 0u;
            return Mix(state);
        }

        /// <summary>
        /// One round of the splitmix32 finalizer, kept local so the run does not depend on the
        /// random-source implementation's visibility (Shared keeps that type internal).
        /// </summary>
        private static uint Mix(uint value)
        {
            value += 0x9E37_79B9u;
            value = (value ^ (value >> 30)) * 0xBF58_476Du;
            value = (value ^ (value >> 27)) * 0x94D0_49BBu;
            return value ^ (value >> 31);
        }

        /// <summary>Disposes the run scope. The type holds no unmanaged state today.</summary>
        public void Dispose() => _disposed = true;

        private Result Record(RunCommandKind kind)
        {
            Log?.Record(kind);
            return Result.Ok();
        }

        private static Result RefuseDisposed() => RunErrors.Refuse(RunErrors.Finished);
    }
}
