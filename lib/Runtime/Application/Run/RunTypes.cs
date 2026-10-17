using ProjectSurvive.Core.Shared;

namespace ProjectSurvive.Core.Application.Run
{
    /// <summary>How a run ended, if it has.</summary>
    public enum RunOutcome
    {
        /// <summary>The run is still playable (or has not started).</summary>
        InProgress = 0,

        /// <summary>The player finished the run deliberately.</summary>
        Completed = 1,

        /// <summary>The run ended because an encounter was lost.</summary>
        Failed = 2,
    }

    /// <summary>The result of one encounter, as reported by whoever played it.</summary>
    public enum EncounterOutcome
    {
        /// <summary>The encounter was cleared; the run continues.</summary>
        Won = 0,

        /// <summary>The encounter was lost; the run ends.</summary>
        Lost = 1,
    }

    /// <summary>
    /// The commands a run accepts. They are the vocabulary of <see cref="ReplayLog"/>: a run is
    /// reproducible from its seed plus this sequence, which is what makes "replay the same seed,
    /// get the same state" a test rather than a wish.
    /// </summary>
    public enum RunCommandKind
    {
        /// <summary>Starts a run.</summary>
        StartRun = 0,

        /// <summary>Enters the next encounter.</summary>
        BeginEncounter = 1,

        /// <summary>Ends the current encounter, cleared.</summary>
        WinEncounter = 2,

        /// <summary>Ends the current encounter, lost.</summary>
        LoseEncounter = 3,

        /// <summary>Finishes the run deliberately.</summary>
        EndRun = 4,

        /// <summary>Returns the run to its initial state.</summary>
        Reset = 5,
    }

    /// <summary>One recorded command, with its position in the run.</summary>
    public readonly struct RunCommand
    {
        /// <summary>Creates a recorded command.</summary>
        public RunCommand(int sequence, RunCommandKind kind)
        {
            Sequence = sequence;
            Kind = kind;
        }

        /// <summary>Zero-based position in the run's command list.</summary>
        public int Sequence { get; }

        /// <summary>What was asked for.</summary>
        public RunCommandKind Kind { get; }

        /// <inheritdoc />
        public override string ToString() => Sequence + ":" + Kind;
    }

    /// <summary>
    /// Machine-readable refusal codes of the run and the scene flow (AGENTS rule 7).
    /// </summary>
    /// <remarks>
    /// A code is API: the UI maps it to <c>error.&lt;code&gt;</c> in the localization table, so
    /// renaming one is a localization change too.
    /// </remarks>
    public static class RunErrors
    {
        /// <summary>A command arrived before <c>Start</c>.</summary>
        public const string NotStarted = "run.not_started";

        /// <summary>The run was already started.</summary>
        public const string AlreadyStarted = "run.already_started";

        /// <summary>An encounter is already running.</summary>
        public const string InEncounter = "run.in_encounter";

        /// <summary>No encounter is running.</summary>
        public const string NotInEncounter = "run.not_in_encounter";

        /// <summary>The run has already ended.</summary>
        public const string Finished = "run.finished";

        /// <summary>The scene transition is not one the loop allows.</summary>
        public const string TransitionNotAllowed = "transition.not_allowed";

        /// <summary>A <see cref="Result"/> carrying <see cref="ErrorKind.InvalidState"/>.</summary>
        public static Result Refuse(string code) => Result.Fail(code, ErrorKind.InvalidState);
    }
}
