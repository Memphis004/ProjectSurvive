using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Drives a battle: a fixed-step accumulator, one step per tick, and one interpolated
    /// render per frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole clock policy of Stage 07 lives here, on the one side of the seam that is
    /// allowed a clock (AGENTS rule 6 keeps <c>Time</c> out of the rule engine). Real frame
    /// deltas go into <see cref="FixedTicker"/>, which hands out at most
    /// <c>BattleConstants.MaxStepsPerFrame</c> steps and counts the rest as dropped; the same
    /// number is <see cref="TickDelta"/>, and the accumulator's leftover fraction is the
    /// interpolation alpha handed to the view. A stalled tab therefore drops ticks instead of
    /// replaying them — a ten-second stall replayed as 300 steps is a death sentence in a game
    /// about not being touched.
    /// </para>
    /// <para>
    /// The frame is copied <b>once</b>, immediately after the last step of the frame and
    /// before anything else can step again: the contract's spans are valid only until the next
    /// <c>Step</c>/<c>Submit</c> (acceptance line), and the copy is what makes rendering safe.
    /// </para>
    /// <para>
    /// It never names a session implementation. The factory arrives as the
    /// <see cref="IBattleSessionFactory"/> port, so the Battle scene runs against the Stage 02b
    /// fake or the EcsLite simulation purely by changing a binding in the scene's scope, which
    /// is how the HUD was developed before the real battle was wired in.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleSimulationRunner : MonoBehaviour
    {
        /// <summary>On-screen name of the page global that carries the battle's progress.</summary>
        public const string BattleReportGlobal = "__projectSurviveBattle";

        [Tooltip("Scene wiring: the view root, camera, HUD and font. Authored by ProjectSetup.")]
        [SerializeField] private BattleViewSettings _viewSettings = new BattleViewSettings();

        [Tooltip("Seconds between progress reports published to the page (0 disables them).")]
        [SerializeField] private float _reportIntervalSeconds = 5f;

        [Tooltip("Publish __projectSurviveBattle. Only a browser test reads it.")]
        [SerializeField] private bool _publishReport = true;

        private BattleViewScene _view;
        private BattleRunnerContext _context;
        private IBattleSession _session;
        private FixedTicker _ticker;
        private bool _running;
        private bool _paused;
        private bool _reportedFirst;
        private bool _resultAnnounced;
        private float _reportTimer;
        private BattleResult _result;

        /// <summary>Fixed steps the runner has asked of the session.</summary>
        public long StepsRun { get; private set; }

        /// <summary>Ticks the ticker discarded because frames ran long.</summary>
        public long DroppedTicks => _ticker?.DroppedTicks ?? 0;

        /// <summary>Steps the most recent frame ran.</summary>
        public int LastSteps { get; private set; }

        /// <summary>Length of one tick, in seconds.</summary>
        public float TickDelta => _ticker is null ? 1f / BattleConstants.TickRate : (float)_ticker.TickSeconds;

        /// <summary>True while a floor is being stepped.</summary>
        public bool IsRunning => _running;

        /// <summary>The view (null before <see cref="Begin"/>).</summary>
        public BattleViewScene View => _view;

        /// <summary>The session, as the port. Never a concrete implementation.</summary>
        public IBattleSession Session => _session;

        /// <summary>The last interpolation alpha written to the view.</summary>
        public float LastAlpha { get; private set; }

        /// <summary>True once the floor has ended and its result was taken.</summary>
        public bool HasEnded => _result is not null;

        /// <summary>The floor's result, once it ended (null while running).</summary>
        public BattleResult Result => _result;

        /// <summary>
        /// Sends an out-of-band command to the running floor — surrender today, and the dev menu's
        /// cheats from Stage 10c.
        /// </summary>
        /// <remarks>
        /// The session answers, and its answer is passed through untouched: a command the floor does
        /// not accept (or one the setup did not allow) is refused with the session's own typed
        /// result rather than silently dropped. Movement never goes through here; it is part of
        /// <see cref="PlayerInputFrame"/>.
        /// </remarks>
        /// <returns><see cref="CommandResult.RejectedWrongPhase"/> when no floor is running.</returns>
        public CommandResult Submit(in BattleCommand command)
        {
            if (_session is null)
            {
                return CommandResult.RejectedWrongPhase;
            }

            return _session.Submit(command);
        }

        /// <summary>
        /// Starts a floor: builds the view, creates the session through the port, and begins
        /// stepping. Called by the scene's composition root.
        /// </summary>
        public void Begin(BattleRunnerContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Shutdown();

            _context = context;
            _ticker = new FixedTicker(BattleConstants.TickRate, BattleConstants.MaxStepsPerFrame);

            _view = new BattleViewScene(
                _viewSettings,
                context.HudState,
                context.Hud,
                context.Speed,
                context.VisualSeed);

            _session = _view.Begin(context.Factory, context.Setup, context.StartingBia);

            StepsRun = 0;
            LastSteps = 0;
            _result = null;
            _resultAnnounced = false;
            _reportedFirst = false;
            _reportTimer = 0f;
            _paused = false;
            _running = true;

            PublishReport(true);
        }

        /// <summary>
        /// Tears the floor down: disposes the session (which tears down the ECS world), the
        /// view's subscriptions and its pooled objects. Idempotent, and the only teardown path,
        /// so leaving the scene cannot leave a session alive.
        /// </summary>
        public void Shutdown()
        {
            _running = false;

            // The view owns the session it began (and disposes it), so the session reference
            // is dropped rather than disposed twice. There is exactly one teardown path, which
            // is what makes "leaving the Battle scene releases the ECS world" checkable.
            _view?.Dispose();
            _view = null;
            _session = null;

            _context = null;
            _ticker?.Reset();
        }

        private void OnDestroy() => Shutdown();

        /// <summary>
        /// The pause policy, in the browser's terms: an inactive tab is "not stepped", so
        /// nothing accumulates and nothing is owed when it comes back.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (!paused)
            {
                _ticker?.Reset();
            }
        }

        private void Update()
        {
            if (!_running || _paused || _session is null)
            {
                return;
            }

            float delta = Time.deltaTime * (_context.Speed?.Multiplier.Value ?? 1f);
            int steps = _ticker.Advance(delta);
            LastSteps = steps;

            for (int i = 0; i < steps; i++)
            {
                _session.Step(_view.Input.Current);
            }

            StepsRun += steps;

            if (steps > 0)
            {
                // Copy now: the spans die on the next Step.
                _view.AcceptTick(_session.Frame);
            }

            _view.DrainEvents(_session.Events);

            LastAlpha = Alpha();
            _view.Render(LastAlpha, delta);

            if (_session.Phase != BattlePhase.Running)
            {
                EndFloor();
            }

            TickReport(delta);
        }

        /// <summary>
        /// Where the last tick is, as a fraction of a tick: <c>accumulator / TickDelta</c>,
        /// the alpha the view interpolates with.
        /// </summary>
        private float Alpha()
        {
            if (_ticker is null)
            {
                return 0f;
            }

            double alpha = _ticker.PendingSeconds / _ticker.TickSeconds;
            if (alpha < 0.0)
            {
                return 0f;
            }

            return alpha >= 1.0 ? 0.9999f : (float)alpha;
        }

        private void EndFloor()
        {
            if (_resultAnnounced)
            {
                return;
            }

            _resultAnnounced = true;
            _running = false;

            if (_session.TryGetResult(out BattleResult result))
            {
                _result = result;
                _view.End(result);
            }

            PublishReport(true);
        }

        private void TickReport(float delta)
        {
            if (!_publishReport || _reportIntervalSeconds <= 0f)
            {
                return;
            }

            _reportTimer += delta;
            if (_reportTimer < _reportIntervalSeconds)
            {
                return;
            }

            _reportTimer = 0f;
            PublishReport(false);
        }

        /// <summary>
        /// Publishes the battle's progress as <c>window.__projectSurviveBattle</c>, the same
        /// shape the smoke suite uses: a WebGL build cannot run Unity's test framework, so a
        /// browser test asserts on what the player published rather than on a log line.
        /// </summary>
        private void PublishReport(bool force)
        {
            if (!_publishReport)
            {
                return;
            }

            if (!force && _reportedFirst && !_running)
            {
                return;
            }

            _reportedFirst = true;

            var _report = new JsonReportWriter();
            _report.BeginObject();
            _report.Write("scene", gameObject.scene.name);
            _report.Write("phase", _session is null ? "none" : _session.Phase.ToString());
            _report.Write("tick", _session is null ? "0" : _session.Tick.ToString());
            _report.Write("stepsRun", StepsRun.ToString());
            _report.Write("droppedTicks", DroppedTicks.ToString());
            _report.Write("speed", _context?.Speed?.Multiplier.Value ?? 1f);
            _report.Write("viewsCreated", _view is null ? 0 : _view.Pool.CreatedTotal);
            _report.Write("viewsGrownDuringPlay", _view is null ? 0 : _view.Pool.CreatedAfterWarmup);
            _report.Write("viewsBound", _view is null ? 0 : _view.BoundViews);
            _report.Write("poolCapacity", _view is null ? 0 : _view.Pool.Capacity);
            _report.Write("damageNumbers", _view is null ? 0 : _view.ActiveDamageNumbers);
            _report.Write("timelineMarkers", _context?.HudState is null ? 0 : _context.HudState.Timeline.Count);
            _report.Write("droppedEvents", _view is null ? 0L.ToString() : _view.Events.DroppedTotal.ToString());
            _report.Write("ended", HasEnded);

            if (_view is not null && _view.Snapshot.HasFrame)
            {
                HudState hud = _view.Snapshot.Hud;
                _report.Write("hp", hud.Hp);
                _report.Write("maxHp", hud.MaxHp);
                _report.Write("mp", hud.Mp);
                _report.Write("bia", hud.Currency);
                _report.Write("kills", hud.Kills);
                _report.Write("waveIndex", hud.WaveIndex);
                _report.Write("remainingSeconds", hud.RemainingSeconds(BattleConstants.TickRate));
            }

            if (_result is not null)
            {
                _report.Write("outcome", _result.Outcome.ToString());
                _report.Write("endReason", _result.EndReason.ToString());
                _report.Write("finalHp", _result.FinalHp);
            }

            _report.EndObject();
            WebGlBridge.Publish(BattleReportGlobal, _report.ToString());
        }
    }
}
