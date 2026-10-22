#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Web
{
    /// <summary>
    /// The names and opcodes of the development-only harness, in one place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A page can read a global, but it cannot call into a WebGL player. Stage 14 needs two
    /// things the page globals alone cannot do — start a run with a named seed, and advance a
    /// battle by a known number of ticks — so the page writes <c>(opcode, argument)</c> pairs
    /// into a queue the player polls. The queue and the send function are created by
    /// <see cref="HarnessBridge"/> in a development build or the Editor only, so a release
    /// player never defines either: that absence is what <c>tests/web/release.spec.ts</c>
    /// asserts.
    /// </para>
    /// <para>
    /// Nothing here decides a rule. <c>startSeededRun</c> calls the same
    /// <see cref="TitlePresenter.RequestStart"/> the เริ่มเล่น control calls, and
    /// <c>advanceTicks</c> steps the same <c>IBattleSession</c> the frame loop steps.
    /// <b>There is deliberately no bypass command</b> (finish the floor, grant HP, spawn an
    /// enemy): a browser test that used one would prove nothing about combat, and the brief
    /// says a bypass must be named test-only and must not count as combat coverage.
    /// </para>
    /// </remarks>
    public static class HarnessProtocol
    {
        /// <summary>The harness' state document.</summary>
        public const string Global = "__projectSurviveHarness";

        /// <summary>
        /// The function the page calls to enqueue a command:
        /// <c>window.__projectSurviveHarnessSend(opcode, argument)</c>.
        /// </summary>
        public const string SendGlobal = "__projectSurviveHarnessSend";

        /// <summary>
        /// The battle snapshot, written by the harness: <c>phase</c>, <c>tick</c> and
        /// <c>endReason</c> as the <c>IBattleSession</c> reports them right now.
        /// </summary>
        public const string SnapshotGlobal = "__projectSurviveHarnessSnapshot";

        /// <summary>Build facts and the run's identity, for an artifact a failure can be read from.</summary>
        public const string DiagnosticsGlobal = "__projectSurviveHarnessDiagnostics";

        /// <summary>No command waiting.</summary>
        public const int None = 0;

        /// <summary>Start a run from the configured seed, or from the argument when it is not 0.</summary>
        public const int StartSeededRun = 1;

        /// <summary>Step the running floor by the argument's tick count.</summary>
        public const int AdvanceTicks = 2;

        /// <summary>Re-publish a fresh battle snapshot (phase, tick, endReason).</summary>
        public const int Snapshot = 3;

        /// <summary>Publish the build/run facts document.</summary>
        public const int Diagnostics = 4;

        /// <summary>Every command the harness answers, in the order the page sees them.</summary>
        public static readonly string[] Commands = { "startSeededRun", "advanceTicks", "snapshot", "diagnostics" };

        /// <summary>The opcode <paramref name="command"/> is sent as, or <see cref="None"/>.</summary>
        public static int OpcodeOf(string command)
        {
            switch (command)
            {
                case "startSeededRun": return StartSeededRun;
                case "advanceTicks": return AdvanceTicks;
                case "snapshot": return Snapshot;
                case "diagnostics": return Diagnostics;
                default: return None;
            }
        }

        /// <summary>The command an opcode names, or an empty string.</summary>
        public static string NameOf(int opcode)
        {
            switch (opcode)
            {
                case StartSeededRun: return "startSeededRun";
                case AdvanceTicks: return "advanceTicks";
                case Snapshot: return "snapshot";
                case Diagnostics: return "diagnostics";
                default: return string.Empty;
            }
        }

        /// <summary>True for an opcode this build answers.</summary>
        public static bool IsKnown(int opcode) => !string.IsNullOrEmpty(NameOf(opcode));

        /// <summary>
        /// The seed an argument names: <c>0</c> means "leave the configured seed alone".
        /// </summary>
        /// <remarks>
        /// The queue carries <c>int</c>s, so a <c>uint</c> seed travels as its bit pattern.
        /// <c>0</c> is reserved rather than accepted as a seed so that "no argument" and "seed
        /// zero" cannot be confused; the game's own default seed is non-zero.
        /// </remarks>
        public static bool TryReadSeed(int raw, out uint seed)
        {
            seed = unchecked((uint)raw);
            return raw != 0;
        }
    }

    /// <summary>
    /// What the Battle scene lends the harness while a floor is running.
    /// </summary>
    /// <remarks>
    /// Declared here rather than in the Battle assembly because the harness lives in the App
    /// scope, which outlives the Battle scene: the scene registers itself while its runner is
    /// alive and unregisters on teardown, so a command sent outside a floor is answered with a
    /// typed refusal instead of finding a stale runner from the previous floor.
    /// </remarks>
    public interface IBattleHarnessTarget
    {
        /// <summary>True while a floor is stepping.</summary>
        bool IsAvailable { get; }

        /// <summary>Steps the floor by <paramref name="ticks"/> and returns how many it ran.</summary>
        int TestOnlyAdvanceTicks(int ticks);

        /// <summary>Publishes <see cref="HarnessProtocol.SnapshotGlobal"/> from the session.</summary>
        void TestOnlyPublishSnapshot();
    }

    /// <summary>The one slot a running floor fills; null when no floor is running.</summary>
    public static class HarnessBattleChannel
    {
        private static IBattleHarnessTarget _target;

        /// <summary>The running floor, or null.</summary>
        public static IBattleHarnessTarget Current => _target;

        /// <summary>Lends a running floor to the harness.</summary>
        public static void Register(IBattleHarnessTarget target) => _target = target;

        /// <summary>Takes it back, but only when it is still the target's own.</summary>
        public static void Unregister(IBattleHarnessTarget target)
        {
            if (ReferenceEquals(_target, target))
            {
                _target = null;
            }
        }
    }

    /// <summary>
    /// The development build's command surface: a ready handshake, a seeded start, a battle
    /// snapshot, a tick advance and a diagnostics export.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Compiled only into a development build and the Editor.</b> The whole file is behind
    /// <c>DEVELOPMENT_BUILD || UNITY_EDITOR</c>, and the one line that installs it in
    /// <c>SceneBoot</c> is behind the same condition, so a Release player contains neither the
    /// component nor the page global. That is a stronger statement than "the menu is hidden",
    /// and it is what the release absence test is allowed to assert (brief item 7).
    /// </para>
    /// <para>
    /// <b>Why it exists at all.</b> Every page global the game publishes is a report the player
    /// wrote. What a browser test cannot do is make the player <i>do</i> something with a known
    /// seed or a known number of ticks: renting through the UI takes a minute, and a 60-second
    /// floor at 1× takes a minute. The two commands here are the smallest set that closes that
    /// gap — and both go through the same use case the screen uses, so a test that drives them
    /// is exercising the real run and the real session, not a copy of them.
    /// </para>
    /// <para>
    /// <b>What it refuses.</b> A seeded start only from the title (the one screen whose
    /// departure it can perform, through the title's own presenter), and never over a live run.
    /// An advance only while a floor is stepping and not paused. Every refusal is a typed
    /// string in the document (<c>harness.wrong_scene</c>, <c>harness.run_active</c>,
    /// <c>harness.no_battle</c>, <c>harness.battle_paused</c>, <c>harness.unknown_command</c>),
    /// so a test can tell "refused" from "did nothing".
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HarnessBridge : MonoBehaviour
    {
        private const string TitleSceneName = "Title";

        private static HarnessBridge _instance;

        private RootLifetimeScope _scope;
        private IGameConfig _config;
        private RunScopeOwner _runs;
        private TitlePresenter _title;
        private string _publishedScene = string.Empty;
        private string _lastCommand = string.Empty;
        private string _lastResult = string.Empty;
        private string _lastError = string.Empty;

        // Monotonic, and published: "a command was answered" is a number a page can wait on
        // without matching on a string that an unknown opcode leaves empty.
        private int _commandCount;

        /// <summary>The installed bridge, or null (a release player, or before boot).</summary>
        public static HarnessBridge Instance => _instance;

        /// <summary>True once the page can send a command.</summary>
        public bool Ready { get; private set; }

        /// <summary>
        /// Installs the bridge on the App scope, if the build may have one.
        /// </summary>
        /// <remarks>
        /// Called once by <c>SceneBoot</c>, after the scope has been made persistent and before
        /// the smoke suite hands over to the title. Idempotent: a second boot in the same
        /// process (a PlayMode fixture loading Boot again) reuses the installed bridge instead
        /// of publishing two documents.
        /// </remarks>
        public static HarnessBridge Install(RootLifetimeScope scope)
        {
            if (!Application.isPlaying || scope is null || scope.Container is null)
            {
                return null;
            }

            if (_instance is not null)
            {
                return _instance;
            }

            var host = new GameObject("HarnessBridge");
            host.transform.SetParent(scope.transform, worldPositionStays: false);

            HarnessBridge bridge = host.AddComponent<HarnessBridge>();
            bridge.Initialize(scope);
            _instance = bridge;
            return bridge;
        }

        private void Initialize(RootLifetimeScope scope)
        {
            _scope = scope;

            // Resolved eagerly, not lazily: `ready` means "the config is loaded", and a lazily
            // built config would let the page see ready=true before the tables exist.
            _config = scope.Container.Resolve<IGameConfig>();
            _runs = scope.Container.Resolve<RunScopeOwner>();
            _title = scope.Container.Resolve<TitlePresenter>();

            ProjectSurvive_HarnessEnable(HarnessProtocol.SendGlobal);
            Ready = true;
            PublishDocument();
        }

        private void Update()
        {
            if (!Ready)
            {
                return;
            }

            // The document follows the scene, so a test never has to guess which screen a
            // command will land on: `scene` is always the scene on screen now.
            string scene = SceneManager.GetActiveScene().name;
            if (!string.Equals(scene, _publishedScene, StringComparison.Ordinal))
            {
                PublishDocument();
            }

            int opcode = ProjectSurvive_HarnessPoll();
            if (opcode == HarnessProtocol.None)
            {
                return;
            }

            int argument = ProjectSurvive_HarnessArg();
            ProjectSurvive_HarnessShift();
            Execute(opcode, argument);
        }

        /// <summary>Answers one command. Every path publishes the document.</summary>
        public void Execute(int opcode, int argument)
        {
            _commandCount++;
            _lastCommand = HarnessProtocol.NameOf(opcode);
            _lastError = string.Empty;

            if (!HarnessProtocol.IsKnown(opcode))
            {
                Refuse("harness.unknown_command");
                return;
            }

            try
            {
                switch (opcode)
                {
                    case HarnessProtocol.StartSeededRun:
                        StartSeededRun(argument);
                        return;
                    case HarnessProtocol.AdvanceTicks:
                        AdvanceTicks(argument);
                        return;
                    case HarnessProtocol.Snapshot:
                        PublishSnapshot();
                        return;
                    case HarnessProtocol.Diagnostics:
                        PublishDiagnostics();
                        return;
                    default:
                        Refuse("harness.unknown_command");
                        return;
                }
            }
            catch (Exception exception)
            {
                _lastResult = "error";
                _lastError = exception.GetType().Name + ": " + exception.Message;
                Debug.LogError("[ProjectSurvive] harness " + _lastCommand + " failed: " + _lastError);
                PublishDocument();
            }
        }

        /// <summary>
        /// Starts a run from <paramref name="rawSeed"/> (0 keeps the configured seed) by calling
        /// the same <see cref="TitlePresenter.RequestStart"/> the เริ่มเล่น control calls.
        /// </summary>
        private void StartSeededRun(int rawSeed)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (!string.Equals(scene, TitleSceneName, StringComparison.Ordinal))
            {
                Refuse("harness.wrong_scene");
                return;
            }

            if (_runs.HasRun)
            {
                // Not a convenience: destroying a live run would make the command a bypass of the
                // run's own lifetime rules, and a test that needs that should say so explicitly.
                Refuse("harness.run_active");
                return;
            }

            uint seed = _runs.Seed;
            if (HarnessProtocol.TryReadSeed(rawSeed, out uint requested))
            {
                _runs.TestOnlySetSeed(requested);
                seed = requested;
            }

            if (!_title.RequestStart())
            {
                _lastResult = "refused";
                _lastError = string.IsNullOrEmpty(_title.StartError) ? "harness.start_refused" : _title.StartError;
                PublishDocument();
                return;
            }

            _lastResult = "accepted";
            _lastError = string.Empty;
            Debug.Log("[ProjectSurvive] harness started a seeded run: seed=" + seed);
            PublishDocument();
        }

        /// <summary>Steps the running floor by <paramref name="ticks"/>.</summary>
        private void AdvanceTicks(int ticks)
        {
            IBattleHarnessTarget target = HarnessBattleChannel.Current;
            if (target is null || !target.IsAvailable)
            {
                Refuse("harness.no_battle");
                return;
            }

            int ran = target.TestOnlyAdvanceTicks(ticks);
            _lastResult = "advanced:" + ran;

            if (ran == 0)
            {
                _lastError = "harness.battle_paused";
            }
            else
            {
                _lastError = string.Empty;
            }

            PublishDocument();
        }

        private void PublishSnapshot()
        {
            IBattleHarnessTarget target = HarnessBattleChannel.Current;
            if (target is null)
            {
                Refuse("harness.no_battle");
                return;
            }

            target.TestOnlyPublishSnapshot();
            _lastResult = "snapshot";
            PublishDocument();
        }

        private void PublishDiagnostics()
        {
            var writer = new JsonReportWriter();
            writer.BeginObject();
            writer.Write("unityVersion", Application.unityVersion);
            writer.Write("appVersion", Application.version);
            writer.Write("platform", Application.platform.ToString());
            writer.Write("isDevelopmentBuild", Debug.isDebugBuild);
            writer.Write("configHash", _config is null ? string.Empty : _config.ConfigHash);
            writer.Write("scene", SceneManager.GetActiveScene().name);
            writer.Write("seed", _runs is null ? 0 : unchecked((int)_runs.Seed));
            writer.Write("hasRun", _runs is not null && _runs.HasRun);
            writer.Write("flow", _runs?.Current is null ? "none" : _runs.Current.FlowState.ToString());
            writer.Write("floorIndex", _runs?.Current is null ? 0 : _runs.Current.FloorIndex);
            writer.Write("width", Screen.width);
            writer.Write("height", Screen.height);
            writer.EndObject();

            WebGlBridge.Publish(HarnessProtocol.DiagnosticsGlobal, writer.ToString());
            _lastResult = "diagnostics";
            PublishDocument();
        }

        private void Refuse(string code)
        {
            _lastResult = "refused";
            _lastError = code;
            PublishDocument();
        }

        /// <summary>
        /// Writes <see cref="HarnessProtocol.Global"/>: ready, the scene, the build facts and
        /// the command vocabulary the page may use.
        /// </summary>
        private void PublishDocument()
        {
            _publishedScene = SceneManager.GetActiveScene().name;

            var writer = new JsonReportWriter();
            writer.BeginObject();
            writer.Write("ready", Ready);
            writer.Write("scene", _publishedScene);
            writer.Write("unityVersion", Application.unityVersion);
            writer.Write("appVersion", Application.version);
            writer.Write("isEditor", Application.isEditor);
            writer.Write("configHash", _config is null ? string.Empty : _config.ConfigHash);
            writer.Write("seed", _runs is null ? 0 : unchecked((int)_runs.Seed));
            writer.Write("hasRun", _runs is not null && _runs.HasRun);
            writer.Write("flow", _runs?.Current is null ? "none" : _runs.Current.FlowState.ToString());
            writer.Write("sendGlobal", HarnessProtocol.SendGlobal);
            writer.Write("snapshotGlobal", HarnessProtocol.SnapshotGlobal);
            writer.Write("diagnosticsGlobal", HarnessProtocol.DiagnosticsGlobal);
            writer.Write("battleAvailable", HarnessBattleChannel.Current?.IsAvailable ?? false);
            writer.Write("commandCount", _commandCount);
            writer.Write("lastCommand", _lastCommand);
            writer.Write("lastResult", _lastResult);
            writer.Write("lastError", _lastError);

            writer.BeginArrayProperty("commands");
            for (int i = 0; i < HarnessProtocol.Commands.Length; i++)
            {
                writer.WriteArrayString(HarnessProtocol.Commands[i]);
            }

            writer.EndArray();
            writer.EndObject();

            WebGlBridge.Publish(HarnessProtocol.Global, writer.ToString());
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void ProjectSurvive_HarnessEnable(
            [MarshalAs(UnmanagedType.LPStr)] string sendName);

        [DllImport("__Internal")]
        private static extern int ProjectSurvive_HarnessPoll();

        [DllImport("__Internal")]
        private static extern int ProjectSurvive_HarnessArg();

        [DllImport("__Internal")]
        private static extern int ProjectSurvive_HarnessShift();
#else
        private static void ProjectSurvive_HarnessEnable(string sendName)
        {
            // No page to talk to outside the WebGL player.
        }

        private static int ProjectSurvive_HarnessPoll()
        {
            return HarnessProtocol.None;
        }

        private static int ProjectSurvive_HarnessArg()
        {
            return 0;
        }

        private static int ProjectSurvive_HarnessShift()
        {
            return 0;
        }
#endif
    }
}
#endif
