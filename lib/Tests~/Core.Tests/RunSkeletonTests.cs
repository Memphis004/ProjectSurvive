using System.Collections.Generic;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The run skeleton and the loop's state machine, headlessly.
    /// </summary>
    /// <remarks>
    /// These are the two types every screen drives, so the refusal paths matter more than the happy
    /// one: a command that fails must leave the run exactly as it was (AGENTS rule 7), and the flow
    /// must not allow an edge the loop does not have (AGENTS rule 12 keeps that list in one place).
    /// No Unity, no config, no clock — the seed is the whole input.
    /// </remarks>
    public sealed class RunSkeletonTests
    {
        private const uint Seed = 1234u;

        private static RunSession Started(uint seed = Seed)
        {
            var run = new RunSession(seed);
            Assert.True(run.Start().IsSuccess);
            return run;
        }

        [Fact]
        public void A_run_starts_once_and_a_second_start_is_refused_without_changing_it()
        {
            var run = new RunSession(Seed);
            Assert.False(run.IsStarted);

            Assert.True(run.Start().IsSuccess);
            Assert.True(run.IsStarted);

            var again = run.Start();
            Assert.True(again.IsFailure);
            Assert.Equal(RunErrors.AlreadyStarted, again.Error.Code);
            Assert.True(run.IsStarted);
        }

        [Fact]
        public void An_encounter_cannot_begin_before_the_run_starts()
        {
            var run = new RunSession(Seed);

            var refused = run.BeginEncounter();
            Assert.True(refused.IsFailure);
            Assert.Equal(RunErrors.NotStarted, refused.Error.Code);
            Assert.False(run.IsInEncounter);
        }

        [Fact]
        public void A_won_encounter_counts_and_hands_the_run_back_to_the_hub()
        {
            RunSession run = Started();

            Assert.True(run.BeginEncounter().IsSuccess);
            Assert.True(run.IsInEncounter);

            Assert.True(run.EndEncounter(EncounterOutcome.Won).IsSuccess);
            Assert.False(run.IsInEncounter);
            Assert.Equal(1, run.EncounterIndex);
            Assert.False(run.IsFinished);
        }

        [Fact]
        public void A_lost_encounter_ends_the_run_and_a_second_end_is_refused()
        {
            RunSession run = Started();
            Assert.True(run.BeginEncounter().IsSuccess);

            Assert.True(run.EndEncounter(EncounterOutcome.Lost).IsSuccess);
            Assert.Equal(RunOutcome.Failed, run.Outcome);
            Assert.True(run.IsFinished);

            var again = run.EndEncounter(EncounterOutcome.Won);
            Assert.True(again.IsFailure);
            Assert.Equal(RunErrors.NotInEncounter, again.Error.Code);
            Assert.Equal(0, run.EncounterIndex);
        }

        [Fact]
        public void Ending_the_run_mid_encounter_is_refused()
        {
            RunSession run = Started();
            Assert.True(run.BeginEncounter().IsSuccess);

            var refused = run.EndRun();
            Assert.True(refused.IsFailure);
            Assert.Equal(RunErrors.InEncounter, refused.Error.Code);
            Assert.False(run.IsFinished);
        }

        [Fact]
        public void Ending_the_run_from_the_hub_completes_it()
        {
            RunSession run = Started();

            Assert.True(run.EndRun().IsSuccess);
            Assert.Equal(RunOutcome.Completed, run.Outcome);
        }

        [Fact]
        public void The_log_records_every_accepted_command_in_order()
        {
            RunSession run = Started();

            Assert.True(run.BeginEncounter().IsSuccess);
            Assert.True(run.EndEncounter(EncounterOutcome.Won).IsSuccess);
            Assert.True(run.EndRun().IsSuccess);

            ReplayLog log = run.Log!;
            var kinds = new List<RunCommandKind>();
            for (int i = 0; i < log.Commands.Count; i++)
            {
                kinds.Add(log.Commands[i].Kind);
            }

            Assert.Equal(
                new[]
                {
                    RunCommandKind.StartRun, RunCommandKind.BeginEncounter,
                    RunCommandKind.WinEncounter, RunCommandKind.EndRun,
                },
                kinds);
        }

        [Fact]
        public void A_refused_command_does_not_grow_the_log()
        {
            var run = new RunSession(Seed);
            int before = run.Log!.Count;

            Assert.True(run.BeginEncounter().IsFailure);

            Assert.Equal(before, run.Log.Count);
        }

        [Fact]
        public void A_replayed_run_records_nothing()
        {
            var run = new RunSession(Seed, recording: false);

            Assert.Null(run.Log);
            Assert.True(run.Start().IsSuccess);
        }

        [Fact]
        public void The_state_hash_separates_seeds_and_outcomes()
        {
            RunSession completed = Started(Seed);
            Assert.True(completed.EndRun().IsSuccess);

            RunSession failed = Started(Seed);
            Assert.True(failed.BeginEncounter().IsSuccess);
            Assert.True(failed.EndEncounter(EncounterOutcome.Lost).IsSuccess);

            Assert.NotEqual(completed.ComputeStateHash(), failed.ComputeStateHash());
            Assert.NotEqual(completed.ComputeStateHash(), Started(Seed + 1).ComputeStateHash());
        }

        // -- the loop ---------------------------------------------------------

        [Fact]
        public void The_loop_allows_the_edges_it_declares_and_no_others()
        {
            RunSession run = Started();
            var flow = new SceneFlow(run);

            Assert.Equal(SceneId.Title, flow.CurrentScene);
            Assert.True(flow.CanGo(SceneId.Hub));

            // Title never jumps straight into play, and the summary is not a way in.
            Assert.False(flow.CanGo(SceneId.Encounter));
            Assert.False(flow.CanGo(SceneId.Result));

            Assert.True(flow.TryGo(SceneId.Hub).IsSuccess);
            Assert.True(flow.CanGo(SceneId.Encounter));
            Assert.True(flow.TryGo(SceneId.Encounter).IsSuccess);
            Assert.True(flow.TryGo(SceneId.Result).IsSuccess);
            Assert.False(flow.CanGo(SceneId.Encounter));

            var refused = flow.TryGo(SceneId.Hub);
            Assert.True(refused.IsFailure);
            Assert.Equal(RunErrors.TransitionNotAllowed, refused.Error.Code);
            Assert.Equal(SceneId.Result, flow.CurrentScene);
        }

        [Fact]
        public void A_run_scene_needs_a_started_run()
        {
            var run = new RunSession(Seed);
            var flow = new SceneFlow(run);

            Assert.True(flow.TryGo(SceneId.Hub).IsFailure);
            Assert.Equal(SceneId.Title, flow.CurrentScene);
        }

        [Fact]
        public void Syncing_to_a_scene_that_loaded_on_its_own_moves_the_loop_there()
        {
            RunSession run = Started();
            var flow = new SceneFlow(run);

            flow.SyncTo(SceneId.Encounter);

            Assert.Equal(SceneId.Encounter, flow.CurrentScene);
            Assert.True(flow.CanGo(SceneId.Hub));
        }
    }
}
