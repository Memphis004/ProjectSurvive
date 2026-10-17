using System;
using System.Threading;
using Google.Protobuf;
using NUnit.Framework;
using ProjectSurvive.Config;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Contracts.Save;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Generated.Proto;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Smoke;
using ProjectSurvive.Game.Presentation.Thai;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 03 acceptance in EditMode: the shell's container resolves, the tables
    /// load, and every serialization/async primitive the project depends on works
    /// on this platform.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These live in the Unity project rather than in <c>lib/Tests~</c> because they
    /// exercise things the pure C# harness does not have: <c>Resources</c>, the
    /// VContainer lifetime scope, R3's Unity integration and UniTask's player loop.
    /// The rule engine's own behaviour stays in the dotnet suite.
    /// </para>
    /// <para>
    /// The container is built from a real <see cref="RootLifetimeScope"/> component,
    /// not a hand-rolled <c>ContainerBuilder</c> — so a registration mistake in the
    /// composition root fails here instead of in a build.
    /// </para>
    /// </remarks>
    public sealed class ShellSmokeTests
    {
        private GameObject _scopeObject;
        private RootLifetimeScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scopeObject = new GameObject("EditModeScope");
            _scope = _scopeObject.AddComponent<RootLifetimeScope>();

            // Awake does not run outside play mode, so the scope is built explicitly.
            _scope.Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scope is not null)
            {
                // DisposeCore releases the container without VContainer's Dispose()
                // calling Destroy(gameObject), which edit mode rejects with an error
                // log (and the test framework fails a test on an unexpected error).
                _scope.DisposeCore();
                _scope = null;
            }

            if (_scopeObject is not null)
            {
                UnityEngine.Object.DestroyImmediate(_scopeObject);
                _scopeObject = null;
            }
        }

        [Test]
        public void Root_scope_resolves_every_registration()
        {
            IObjectResolver container = _scope.Container;

            Assert.That(container.Resolve<IGameConfig>(), Is.InstanceOf<IGameConfig>());
            Assert.That(container.Resolve<LocalizationService>(), Is.Not.Null);
            Assert.That(container.Resolve<IClock>(), Is.Not.Null);
            Assert.That(container.Resolve<UiState>(), Is.Not.Null);
            Assert.That(container.Resolve<TitlePresenter>(), Is.Not.Null);
            Assert.That(container.Resolve<SmokeRunner>(), Is.Not.Null);
            Assert.That(container.Resolve<ThaiTextSpike>(), Is.Not.Null);
        }

        [Test]
        public void Config_tables_load_from_resources_and_match_the_generated_hash()
        {
            IGameConfig config = ResourcesGameConfigLoader.Load();

            Assert.That(config.ConfigHash, Is.EqualTo(ConfigBuildMetadata.ConfigHash));
            Assert.That(config.Localization.Count, Is.GreaterThan(0));
            Assert.That(config.Constant.MaxHp, Is.GreaterThan(0));
            Assert.That(config.Constant.MoveSpeed, Is.GreaterThan(0f));
        }

        [Test]
        public void Missing_config_table_fails_loudly()
        {
            Assert.Throws<InvalidOperationException>(() => ResourcesGameConfigLoader.ReadTable("tbdoesnotexist"));
        }

        [Test]
        public void Localization_serves_thai_text_from_the_table()
        {
            var text = new LocalizationService(ResourcesGameConfigLoader.Load());

            string title = text.Get("ui.title");

            Assert.That(title, Is.Not.EqualTo("ui.title"), "the key was returned instead of the text");
            Assert.That(ContainsThai(title), Is.True, "expected Thai code points in '" + title + "'");
            Assert.That(text.TryGet("ui.does.not.exist", out _), Is.False);
        }

        [Test]
        public void Title_presenter_publishes_localized_text_into_ui_state()
        {
            var container = _scope.Container;
            TitlePresenter presenter = container.Resolve<TitlePresenter>();
            UiState state = container.Resolve<UiState>();

            presenter.Refresh();

            Assert.That(state.Title.Value, Is.EqualTo(container.Resolve<LocalizationService>().Get("ui.title")));
            Assert.That(state.Diagnostics.Value, Is.Not.Empty);
            Assert.That(state.Ready.Value, Is.False, "Refresh must not mark the shell ready before the smoke run");
        }

        [Test]
        public void Messagepack_uses_the_source_generated_formatter()
        {
            Assert.That(
                CheckpointProbeCodec.GeneratedFormatterIsPresent,
                Is.True,
                "the dynamic resolver is not IL2CPP-safe, so a generated formatter must exist");

            CheckpointProbe sample = CheckpointProbe.Sample();
            byte[] bytes = CheckpointProbeCodec.Serialize(sample);
            CheckpointProbe copy = CheckpointProbeCodec.Deserialize(bytes);

            Assert.That(bytes.Length, Is.GreaterThan(0));
            Assert.That(copy.Matches(sample), Is.True, "round-tripped probe differs from the sample");
        }

        [Test]
        public void Protobuf_round_trips_the_external_contract()
        {
            var original = new AppInfo
            {
                AppVersion = "0.1.0-stage03-editmode",
                ConfigHash = ConfigBuildMetadata.ConfigHash,
                Platform = Platform.Editor,
                Locale = "th",
            };

            AppInfo copy = AppInfo.Parser.ParseFrom(original.ToByteArray());

            Assert.That(copy.AppVersion, Is.EqualTo(original.AppVersion));
            Assert.That(copy.ConfigHash, Is.EqualTo(original.ConfigHash));
            Assert.That(copy.Platform, Is.EqualTo(Platform.Editor));
            Assert.That(copy.Locale, Is.EqualTo("th"));
        }

        [Test]
        public void Thai_spike_keys_all_exist()
        {
            var text = new LocalizationService(ResourcesGameConfigLoader.Load());

            foreach (string key in ThaiTextSpike.SpikeKeys)
            {
                Assert.That(text.TryGet(key, out string value), Is.True, "missing localization key " + key);
                Assert.That(ContainsThai(value), Is.True, key + " is not Thai text: '" + value + "'");
            }
        }

        [Test]
        public void Url_scene_override_is_parsed_from_the_query_string_only()
        {
            const string baseUrl = "http://localhost:8080/index.html";

            Assert.That(
                SceneBoot.ParseQueryValue(baseUrl + "?scene=ThaiTextSpike", "scene"),
                Is.EqualTo("ThaiTextSpike"));
            Assert.That(
                SceneBoot.ParseQueryValue(baseUrl + "?scene=ThaiTextSpike&quality=low", "scene"),
                Is.EqualTo("ThaiTextSpike"));
            Assert.That(
                SceneBoot.ParseQueryValue(baseUrl + "?quality=low&scene=ThaiTextSpike", "scene"),
                Is.EqualTo("ThaiTextSpike"));

            // A query is not a name match: only a whole parameter counts, and an
            // absent key is absent rather than an error.
            Assert.That(SceneBoot.ParseQueryValue(baseUrl + "?scene=ThaiTextSpike", "quality"), Is.Empty);
            Assert.That(SceneBoot.ParseQueryValue(baseUrl + "?notscene=Title", "scene"), Is.Empty);
            Assert.That(SceneBoot.ParseQueryValue(baseUrl + "?scene=", "scene"), Is.Empty);
            Assert.That(SceneBoot.ParseQueryValue(baseUrl, "scene"), Is.Empty);
            Assert.That(SceneBoot.ParseQueryValue(string.Empty, "scene"), Is.Empty);
        }

        [Test]
        public void Browser_reports_are_valid_json_with_one_element_per_entry()
        {
            // The page parses these strings with JSON.parse, so a writer that drops a
            // comma between two elements is a build that reports nothing at all.
            // Every array here has more than one element on purpose: a single-element
            // array cannot show the bug.
            SmokeReport smoke = _scope.Container.Resolve<SmokeRunner>()
                .RunAsync(new CancellationTokenSource().Token)
                .GetAwaiter()
                .GetResult();

            string smokeJson = smoke.ToJson();

            // The exact signature of the bug this test was written for: two array
            // elements with no separator between them.
            Assert.That(smokeJson, Does.Not.Contain("}{"), "array elements lost their comma: " + smokeJson);

            var parsedSmoke = JsonUtility.FromJson<SmokeReportDto>(smokeJson);
            Assert.That(parsedSmoke, Is.Not.Null, "the smoke report is not valid JSON: " + smokeJson);
            Assert.That(parsedSmoke.probes.Length, Is.EqualTo(smoke.Results.Count));
            for (int i = 0; i < smoke.Results.Count; i++)
            {
                Assert.That(parsedSmoke.probes[i].name, Is.EqualTo(smoke.Results[i].Name));
                Assert.That(parsedSmoke.probes[i].passed, Is.EqualTo(smoke.Results[i].Passed));
                Assert.That(parsedSmoke.probes[i].detail, Is.EqualTo(smoke.Results[i].Detail));
            }

            var text = new LocalizationService(ResourcesGameConfigLoader.Load());
            string thaiText = text.Get("ui.title");
            string thaiMarks = text.Get(ThaiTextSpike.SpikeKeys[2]);

            var thai = new ThaiSpikeReport("Editor", "Sarabun-Regular.ttf");
            thai.Add(new ThaiSpikeRow("uGUI+TMP", "Sarabun-Regular SDF", thaiText, 0, string.Empty, 100.5f, 20.25f));
            thai.Add(new ThaiSpikeRow("UI Toolkit", "Sarabun-Regular SDF", thaiMarks, 0, string.Empty, 90.5f, 18.5f));

            string thaiJson = thai.ToJson();
            Assert.That(thaiJson, Does.Not.Contain("}{"), "array elements lost their comma: " + thaiJson);

            var parsedThai = JsonUtility.FromJson<ThaiSpikeReportDto>(thaiJson);
            Assert.That(parsedThai, Is.Not.Null, "the Thai spike report is not valid JSON: " + thaiJson);
            Assert.That(parsedThai.rowCount, Is.EqualTo(2));
            Assert.That(parsedThai.rows.Length, Is.EqualTo(2));

            // Thai survives the \uXXXX escaping the report uses to cross the ANSI
            // marshalling boundary into JavaScript.
            Assert.That(parsedThai.rows[0].text, Is.EqualTo(thaiText));
            Assert.That(parsedThai.rows[0].width, Is.EqualTo(100.5f).Within(0.01f));
            Assert.That(parsedThai.rows[1].stack, Is.EqualTo("UI Toolkit"));
            Assert.That(parsedThai.rows[1].text, Is.EqualTo(thaiMarks));
        }

        [Test]
        public void Smoke_runner_passes_every_probe_in_edit_mode()
        {
            SmokeRunner runner = _scope.Container.Resolve<SmokeRunner>();
            using var cts = new CancellationTokenSource();

            SmokeReport report = runner.RunAsync(cts.Token).GetAwaiter().GetResult();

            TestContext.WriteLine(report.ToLog());

            Assert.That(report.AllPassed, Is.True, report.ToLog());
            Assert.That(report.Results.Count, Is.GreaterThanOrEqualTo(7));
            Assert.That(report.Platform, Is.EqualTo("Editor"));
        }

        [Test]
        public void Cancelling_a_unirask_completion_source_throws_operation_canceled()
        {
            // The same shape SceneBoot uses: the owner's token drives completion.
            using var cts = new CancellationTokenSource();
            Exception observed = null;

            var task = CancelOnTokenAsync(cts.Token);
            cts.Cancel();

            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                observed = exception;
            }

            Assert.That(observed, Is.InstanceOf<OperationCanceledException>());
        }

        private static async Cysharp.Threading.Tasks.UniTask CancelOnTokenAsync(CancellationToken token)
        {
            var completion = new Cysharp.Threading.Tasks.UniTaskCompletionSource();
            using (token.Register(() => completion.TrySetCanceled(token)))
            {
                await completion.Task;
            }
        }

        // DTOs for JsonUtility: the Unity-side stand-in for the page's JSON.parse.
        [Serializable]
        private sealed class SmokeReportDto
        {
            public string platform;
            public string configHash;
            public int probeCount;
            public int failedCount;
            public bool passed;
            public ProbeDto[] probes;
        }

        [Serializable]
        private sealed class ProbeDto
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private sealed class ThaiSpikeReportDto
        {
            public string platform;
            public string font;
            public int rowCount;
            public int totalMissingGlyphs;
            public bool coversEveryGlyph;
            public ThaiSpikeRowDto[] rows;
        }

        [Serializable]
        private sealed class ThaiSpikeRowDto
        {
            public string stack;
            public string font;
            public string text;
            public int missingGlyphs;
            public string missingCharacters;
            public float width;
            public float height;
        }

        private static bool ContainsThai(string text)        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '\u0E00' && text[i] <= '\u0E7F')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
