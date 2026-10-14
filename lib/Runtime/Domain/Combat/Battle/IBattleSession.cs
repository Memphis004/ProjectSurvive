using System;

namespace ProjectSurvive.Core.Domain.Combat.Battle
{
    /// <summary>
    /// One running battle (one floor), driven tick by tick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The only seam between the battle and everything around it. Application
    /// (SceneFlow / Run) creates a session through
    /// <see cref="IBattleSessionFactory"/> and drives it; Presentation reads
    /// <see cref="Frame"/> and <see cref="Events"/>. The prototype's real
    /// implementation is the EcsLite simulation in <c>Simulation</c> (Stage 07);
    /// <c>FakeBattleSession</c> (Application) stands in until then.
    /// </para>
    /// <para>
    /// The session is deterministic: same <see cref="BattleSetup"/> and same
    /// sequence of <see cref="Step"/>/<see cref="Submit"/> calls produce the same
    /// <see cref="ComputeStateHash"/> within one runtime (AGENTS rule 6).
    /// </para>
    /// <para>
    /// <see cref="IDisposable"/> is part of the contract since Stage 08. A session owns
    /// whatever it built to run (the EcsLite world and its pools, the event ring, the frame
    /// slots), and the GDD's lifetime rule — "leaving Battle disposes the ECS world" — can
    /// only be a property of the seam if the seam can be disposed. Leaving it to a cast at
    /// the call site would make the guarantee a convention, and the one call site that
    /// forgot would leak a world per floor.
    /// </para>
    /// </remarks>
    public interface IBattleSession : IDisposable
    {
        /// <summary>Current lifecycle phase.</summary>
        BattlePhase Phase { get; }

        /// <summary>Ticks elapsed; does not advance once <see cref="Phase"/> leaves <see cref="BattlePhase.Running"/>.</summary>
        long Tick { get; }

        /// <summary>The current frame. Its <see cref="IBattleFrame.Entities"/> span is valid until the next Step/Submit.</summary>
        IBattleFrame Frame { get; }

        /// <summary>The event stream, for VFX/audio/HUD only — never for rules.</summary>
        IBattleEventSource Events { get; }

        /// <summary>
        /// Advances the simulation by exactly one fixed tick, unless the session
        /// is no longer running (in which case it does nothing, and the tick does
        /// not advance). Must not allocate on the hot path after warm-up.
        /// </summary>
        void Step(in PlayerInputFrame input);

        /// <summary>Submits a command. See <see cref="CommandResult"/> for the possible rejections.</summary>
        CommandResult Submit(in BattleCommand command);

        /// <summary>Returns the result once the battle has ended; false while it is still running.</summary>
        bool TryGetResult(out BattleResult result);

        /// <summary>A hash of the current simulation state, for replay / desync checks (never for save files).</summary>
        uint ComputeStateHash();
    }

    /// <summary>
    /// Creates battle sessions. Config tables are supplied to the factory's
    /// constructor, not to <see cref="Create"/> (which only takes the run-time
    /// setup), so an implementation knows its balance data from construction.
    /// </summary>
    public interface IBattleSessionFactory
    {
        /// <summary>Starts a new session for <paramref name="setup"/>.</summary>
        IBattleSession Create(BattleSetup setup);
    }
}
