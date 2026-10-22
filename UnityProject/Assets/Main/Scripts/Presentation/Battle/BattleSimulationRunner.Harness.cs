#if DEVELOPMENT_BUILD || UNITY_EDITOR
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The battle half of the development harness (Stage 14): advance a floor by a known
    /// number of ticks, and publish what the session says about itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A separate file, and a partial of <see cref="BattleSimulationRunner"/>, so that the
    /// harness' members exist in a development build and the Editor and <b>not</b> in a
    /// release player: the type is the runner's own, so no reflection, no new GameObject and
    /// no second copy of the stepping rules are involved — this is the same
    /// <c>IBattleSession.Step</c> / <c>AcceptTick</c> / <c>DrainEvents</c> sequence the frame
    /// loop runs.
    /// </para>
    /// <para>
    /// <b>Advancing takes the clock.</b> The first command sets the runner's harness flag, and
    /// from then on <c>Update</c> steps nothing: a test that asked for 600 ticks and then had
    /// real time step alongside it would be measuring neither a fixed number of ticks nor
    /// wall-clock play. The flag is cleared by the next <see cref="Begin"/>.
    /// </para>
    /// <para>
    /// <b>It is not a bypass.</b> No command ends the floor, grants HP, spawns an enemy or adds
    /// bia; every tick that runs is a real tick with the real rules, and a floor that ends
    /// during an advance ends because the session said so. Rules the browser test cannot reach
    /// quickly are accelerated, never skipped.
    /// </para>
    /// </remarks>
    public sealed partial class BattleSimulationRunner : IBattleHarnessTarget
    {
        // What the last advance command did, so a page can assert on the command's own work
        // rather than on a timeline the real-time loop also writes to: the frame loop may step a
        // tick or two between two commands, and "tick is exactly 300" would then be a statement
        // about frames, not about the command.
        private long _lastAdvanceTickBefore;
        private int _lastAdvanceTicks;

        partial void OnHarnessBegin()
        {
            HarnessBattleChannel.Register(this);
        }

        partial void OnHarnessShutdown()
        {
            HarnessBattleChannel.Unregister(this);
        }

        /// <summary>True while a floor is stepping and the harness may drive it.</summary>
        public bool IsAvailable => _session is not null && _running && !_paused;

        /// <summary>
        /// Steps the floor by <paramref name="ticks"/> and returns how many ticks actually ran.
        /// </summary>
        /// <remarks>
        /// Runs as many ticks as the session allows: a floor that ends mid-advance stops there
        /// and is settled through the same <see cref="EndFloor"/> a real frame would call. The
        /// view is handed the sprite position of the <b>last</b> tick rather than a blend, since
        /// there is no frame to interpolate towards.
        /// </remarks>
        public int TestOnlyAdvanceTicks(int ticks)
        {
            if (_session is null || !_running || _paused || ticks <= 0)
            {
                return 0;
            }

            _harnessDriven = true;
            _lastAdvanceTickBefore = _session.Tick;
            _lastAdvanceTicks = 0;

            int executed = 0;
            for (int i = 0; i < ticks; i++)
            {
                _session.Step(_view.Input.Current);
                StepsRun++;
                executed++;

                if (_session.Phase != BattlePhase.Running)
                {
                    break;
                }
            }

            if (executed == 0)
            {
                return 0;
            }

            LastSteps = executed;
            _lastAdvanceTicks = executed;

            // Copied after the last Step (the contract's spans die on the next one), then the
            // frame's events, then a render at alpha 1: no interpolation, the state as it is.
            _view.AcceptTick(_session.Frame);
            _view.DrainEvents(_session.Events);
            LastAlpha = 1f;
            _view.Render(1f, executed * (float)TickDelta);

            if (_session.Phase != BattlePhase.Running)
            {
                EndFloor();
            }

            PublishReport(true);

            // The snapshot is written here rather than left to a second command: the floor may be
            // settled and handed back to the camp a few frames after the tick that ended it, so a
            // separate `snapshot` command could arrive at a scene with no runner left. One command,
            // one atomic answer.
            TestOnlyPublishSnapshot();
            return executed;
        }

        /// <summary>
        /// Publishes <see cref="HarnessProtocol.SnapshotGlobal"/>: <c>phase</c>, <c>tick</c> and
        /// <c>endReason</c> read from the <see cref="IBattleSession"/> right now.
        /// </summary>
        /// <remarks>
        /// The report the scene already publishes (<c>__projectSurviveBattle</c>) is throttled to
        /// one write every few seconds, so a test that has just advanced 600 ticks would be
        /// reading a stale document. This is the same three facts, written on demand, from the
        /// session rather than from a cached report.
        /// </remarks>
        public void TestOnlyPublishSnapshot()
        {
            var writer = new JsonReportWriter();
            writer.BeginObject();
            writer.Write("scene", gameObject.scene.name);
            writer.Write("phase", _session is null ? "none" : _session.Phase.ToString());
            writer.Write("tick", _session is null ? "0" : _session.Tick.ToString());
            writer.Write("stepsRun", StepsRun.ToString());
            writer.Write("droppedTicks", DroppedTicks.ToString());
            writer.Write("ended", HasEnded);
            writer.Write("paused", _paused);
            writer.Write("harnessDriven", _harnessDriven);
            writer.Write("ticksAdvanced", _lastAdvanceTicks);
            writer.Write("tickBefore", _lastAdvanceTickBefore.ToString());

            if (_session is null)
            {
                // A session-less snapshot is still published, with endReason "None", so a test
                // that polled before the floor began fails with a value rather than a timeout.
                writer.Write("endReason", BattleEndReason.None.ToString());
            }
            else if (_result is not null)
            {
                writer.Write("outcome", _result.Outcome.ToString());
                writer.Write("endReason", _result.EndReason.ToString());
                writer.Write("finalHp", _result.FinalHp);
            }
            else
            {
                writer.Write("endReason", BattleEndReason.None.ToString());
            }

            writer.EndObject();
            WebGlBridge.Publish(HarnessProtocol.SnapshotGlobal, writer.ToString());
        }
    }
}
#endif
