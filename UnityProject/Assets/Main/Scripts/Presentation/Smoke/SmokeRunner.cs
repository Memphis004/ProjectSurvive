using System;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using ProjectSurvive.Config;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Generated.Proto;
using ProjectSurvive.Core.Shared;
using R3;

namespace ProjectSurvive.Game.Presentation.Smoke
{
    /// <summary>
    /// Runs the Stage 03 smoke suite: the things the shell must be able to do on
    /// the real target, not only in a unit test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These probes exist because a green Editor build proves very little about a
    /// WebGL2 player: the serializer must run through source-generated formatters
    /// (the dynamic resolver needs <c>Reflection.Emit</c>, which IL2CPP does not
    /// have), async must work without a thread pool (WebGL2 is single-threaded),
    /// and player text must come from generated tables rather than a fallback.
    /// </para>
    /// <para>
    /// The same <see cref="SmokeRunner"/> runs in the EditMode tests, in the
    /// PlayMode tests and inside the Web build (through
    /// <see cref="Web.WebGlBridge"/>), so the three verdicts are comparable
    /// instead of being three different tests.
    /// </para>
    /// <para>
    /// Dependencies arrive through the constructor, so being constructible at all
    /// <i>is</i> the DI assertion: VContainer had to resolve the config port, the
    /// clock port, the Application-layer text service and the UI state.
    /// </para>
    /// </remarks>
    public sealed class SmokeRunner
    {
        private readonly IGameConfig _config;
        private readonly LocalizationService _text;
        private readonly IClock _clock;
        private readonly UiState _ui;

        public SmokeRunner(IGameConfig config, LocalizationService text, IClock clock, UiState ui)
        {
            _config = config;
            _text = text;
            _clock = clock;
            _ui = ui;
        }

        /// <summary>
        /// Runs every probe. A probe failure is a result, never an exception — the
        /// report is what the tests and the browser assert on.
        /// </summary>
        public async UniTask<SmokeReport> RunAsync(CancellationToken token)
        {
            var report = new SmokeReport(DetectPlatform(), _config.ConfigHash);

            report.Add(ProbeDiResolution());
            report.Add(ProbeConfig());
            report.Add(ProbeR3());
            report.Add(await ProbeCancellationAsync(token));
            report.Add(ProbeMessagePack());
            report.Add(ProbeProtobuf());
            report.Add(ProbeThaiText());

            return report;
        }

        /// <summary>
        /// The four constructor dependencies came from the container. Recording
        /// their runtime types turns "it resolved" into an observation.
        /// </summary>
        private ProbeResult ProbeDiResolution()
        {
            string detail = string.Format(
                CultureInfo.InvariantCulture,
                "resolved {0}, {1}, {2}, {3}",
                _config.GetType().Name,
                _text.GetType().Name,
                _clock.GetType().Name,
                _ui.GetType().Name);

            bool ok = _config is not null && _text is not null && _clock is not null && _ui is not null
                      && _clock.NowSeconds >= 0.0;

            return ok
                ? ProbeResult.Pass("di.resolution", detail)
                : ProbeResult.Fail("di.resolution", "a dependency was null or the clock was negative: " + detail);
        }

        /// <summary>
        /// The tables really loaded: the SHA-256 the generator baked into the
        /// library must match the one the adapter reports, and the tables must have
        /// rows. A stub returning constants would pass "not null" and fail this.
        /// </summary>
        private ProbeResult ProbeConfig()
        {
            string generated = ConfigBuildMetadata.ConfigHash;
            bool hashMatches = string.Equals(generated, _config.ConfigHash, StringComparison.Ordinal);
            int rows = _config.Localization.Count;

            string detail = string.Format(
                CultureInfo.InvariantCulture,
                "hash={0} generated={1} localization={2}",
                Short(_config.ConfigHash),
                Short(generated),
                _config.Localization.Count);

            if (!hashMatches)
            {
                return ProbeResult.Fail("config.tables", "config hash does not match the generated metadata: " + detail);
            }

            return rows > 0
                ? ProbeResult.Pass("config.tables", detail)
                : ProbeResult.Fail("config.tables", "tables loaded but are empty: " + detail);
        }

        /// <summary>
        /// R3 must deliver values and must actually stop on dispose — a leaked
        /// subscription in a scene that reloads is the bug this catches.
        /// </summary>
        private static ProbeResult ProbeR3()
        {
            var subject = new Subject<int>();
            int sum = 0;
            IDisposable subscription = subject.Subscribe(value => sum += value);

            subject.OnNext(1);
            subject.OnNext(2);
            subject.OnNext(3);
            int afterPush = sum;

            subscription.Dispose();
            subject.OnNext(4);
            int afterDispose = sum;

            subject.OnCompleted();
            subject.Dispose();

            if (afterPush != 6)
            {
                return ProbeResult.Fail("r3.subscribe", "subscriber did not receive 1+2+3, got " + afterPush);
            }

            return afterDispose == afterPush
                ? ProbeResult.Pass("r3.subscribe", "received 6, then stayed at " + afterDispose + " after dispose")
                : ProbeResult.Fail("r3.subscribe", "a disposed subscription still fired: " + afterDispose);
        }

        /// <summary>
        /// Cancellation must propagate through UniTask on the target platform. The
        /// token is already cancelled, so the await completes without needing a
        /// player-loop frame — which is what makes this probe valid in EditMode.
        /// </summary>
        private static async UniTask<ProbeResult> ProbeCancellationAsync(CancellationToken ownerToken)
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
            cts.Cancel();

            try
            {
                await AwaitCancellationAsync(cts.Token);
                return ProbeResult.Fail("unitask.cancellation", "await completed instead of throwing");
            }
            catch (OperationCanceledException)
            {
                return ProbeResult.Pass("unitask.cancellation", "await threw OperationCanceledException as expected");
            }
        }

        /// <summary>
        /// The project's cancellable-await shape: a completion source whose
        /// completion is driven by the owner's token (AGENTS rule 9 — cancellation
        /// is tied to the owner's lifetime, never to a global).
        /// </summary>
        private static async UniTask AwaitCancellationAsync(CancellationToken token)
        {
            var completion = new UniTaskCompletionSource();
            using (token.Register(() => completion.TrySetCanceled(token)))
            {
                await completion.Task;
            }
        }

        /// <summary>
        /// MessagePack must round-trip <b>through the generated formatter</b>. The
        /// dynamic resolver also round-trips in the Editor but needs runtime code
        /// generation, which IL2CPP/WebGL does not have, so the generated formatter
        /// is asserted to exist rather than assumed.
        /// </summary>
        private static ProbeResult ProbeMessagePack()
        {
            if (!CheckpointProbeCodec.GeneratedFormatterIsPresent)
            {
                return ProbeResult.Fail(
                    "messagepack.roundtrip",
                    "no source-generated formatter for CheckpointProbe - the dynamic resolver is not IL2CPP-safe");
            }

            CheckpointProbe sample = CheckpointProbe.Sample();
            byte[] bytes = CheckpointProbeCodec.Serialize(sample);
            CheckpointProbe copy = CheckpointProbeCodec.Deserialize(bytes);

            return copy.Matches(sample)
                ? ProbeResult.Pass("messagepack.roundtrip", "generated formatter, " + bytes.Length + " bytes round-tripped")
                : ProbeResult.Fail("messagepack.roundtrip", "the round-tripped value differs from the sample");
        }

        /// <summary>
        /// Protobuf is the external-contract format (AGENTS rule 3); it must
        /// survive the Web player too, not only the Editor.
        /// </summary>
        private ProbeResult ProbeProtobuf()
        {
            var original = new AppInfo
            {
                AppVersion = "0.1.0-stage03",
                ConfigHash = _config.ConfigHash,
                Platform = ProjectSurvive.Core.Generated.Proto.Platform.Web,
                Locale = "th",
            };

            byte[] bytes = original.ToByteArray();
            AppInfo copy = AppInfo.Parser.ParseFrom(bytes);

            bool ok = copy.AppVersion == original.AppVersion
                      && copy.ConfigHash == original.ConfigHash
                      && copy.Platform == original.Platform
                      && copy.Locale == original.Locale;

            return ok
                ? ProbeResult.Pass("protobuf.roundtrip", "AppInfo " + bytes.Length + " bytes round-tripped")
                : ProbeResult.Fail("protobuf.roundtrip", "the parsed AppInfo differs from the original");
        }

        /// <summary>
        /// Player text must come from the generated localization table (AGENTS rule
        /// 5) and must still be Thai after that trip. The probe asserts the
        /// <i>shape</i> — resolved, not a key, contains Thai code points — rather
        /// than a literal string, so the data file stays the only source of text.
        /// </summary>
        private ProbeResult ProbeThaiText()
        {
            if (!_text.TryGet("ui.title", out string title))
            {
                return ProbeResult.Fail("localization.thai", "key 'ui.title' is missing from tblocalization");
            }

            if (string.Equals(title, "ui.title", StringComparison.Ordinal))
            {
                return ProbeResult.Fail("localization.thai", "the key was returned instead of the text");
            }

            int thai = CountThaiCodePoints(title);
            if (thai == 0)
            {
                return ProbeResult.Fail("localization.thai", "'ui.title' resolved but contains no Thai code point");
            }

            string keyList = string.Join(",", Thai.ThaiTextSpike.SpikeKeys);
            return ProbeResult.Pass(
                "localization.thai",
                "ui.title = " + title + " (" + thai + " Thai code points); spike keys " + keyList);
        }

        private static int CountThaiCodePoints(string text)
        {
            int count = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '\u0E00' && text[i] <= '\u0E7F')
                {
                    count++;
                }
            }

            return count;
        }

        private static string Short(string hash) =>
            string.IsNullOrEmpty(hash) || hash.Length < 8 ? hash : hash.Substring(0, 8);

        private static string DetectPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return "WebGLPlayer";
#elif UNITY_EDITOR
            return "Editor";
#else
            return UnityEngine.Application.platform.ToString();
#endif
        }
    }
}
