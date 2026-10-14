using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using UnityEditor;
using UnityEditor.Build;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 03b: the size profiles are the configuration a published size number
    /// belongs to, so the profile plan and the profile itself must not disagree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The comparison in <c>docs/build/size-baseline.md</c> is only meaningful if each
    /// step differs from the previous one in exactly the setting it is named after,
    /// and if "baseline" stays what Stage 03 actually shipped. Both are asserted here
    /// rather than trusted, because either one silently turning into a lie would
    /// invalidate the table while every number in it stayed green.
    /// </para>
    /// <para>
    /// The one test that applies profiles restores the project's previous settings in a
    /// <c>finally</c>: a test suite must not decide which build configuration the
    /// project is left in.
    /// </para>
    /// </remarks>
    public sealed class WebSizeProfilesTests
    {
        private static readonly NamedBuildTarget WebGl = NamedBuildTarget.WebGL;

        /// <summary>Settings no profile may vary: they are the IL2CPP release configuration.</summary>
        private static readonly string[] InvariantSettings =
        {
            "scriptingBackend",
            "il2cppCompilerConfiguration",
            "stripEngineCode",
            "debugSymbolMode",
        };

        /// <summary>The settings each cumulative step is allowed to change, and nothing else.</summary>
        private static readonly (string From, string To, string[] Changed)[] Steps =
        {
            (WebSizeProfiles.Baseline, WebSizeProfiles.StripHigh, new[] { "managedStrippingLevel" }),
            (WebSizeProfiles.StripHigh, WebSizeProfiles.OptimizeSize, new[] { "il2cppCodeGeneration" }),
            (WebSizeProfiles.OptimizeSize, WebSizeProfiles.Minimal, new[] { "exceptionSupport", "wasmArithmeticExceptions" }),
        };

        [Test]
        public void Names_are_the_cumulative_order_and_start_at_baseline()
        {
            Assert.That(WebSizeProfiles.Names[0], Is.EqualTo(WebSizeProfiles.Baseline), "the first profile is the 'before' build");
            Assert.That(WebSizeProfiles.Names, Does.Contain(WebSizeProfiles.Recommended));
            Assert.That(WebSizeProfiles.Names.Distinct().Count(), Is.EqualTo(WebSizeProfiles.Names.Length), "no duplicate profiles");
        }

        [Test]
        public void Baseline_is_the_configuration_the_project_already_had()
        {
            // The "before" column of the size table is the project's own configuration, so these
            // are not free choices: each one is a WebGL default, read back from the editor by
            // WebSizeProfiles.DumpSettings and recorded in docs/build/size-baseline.md. Changing one
            // silently turns the before column into a different build than the one measured.
            Dictionary<string, string> baseline = Plan(WebSizeProfiles.Baseline);

            Assert.That(baseline["managedStrippingLevel"], Is.EqualTo("Low"), "Low is the project's stripping level");
            Assert.That(baseline["il2cppCodeGeneration"], Is.EqualTo("OptimizeSpeed"), "OptimizeSpeed is the WebGL default");
            Assert.That(baseline["il2cppCompilerConfiguration"], Is.EqualTo("Release"), "Release is the WebGL default C++ compiler configuration");
            Assert.That(baseline["exceptionSupport"], Is.EqualTo("ExplicitlyThrownExceptionsOnly"));
            Assert.That(baseline["wasmArithmeticExceptions"], Is.EqualTo("Throw"));
        }


        [Test]
        public void Each_step_changes_only_the_setting_it_is_named_after()
        {
            foreach ((string from, string to, string[] changed) in Steps)
            {
                Dictionary<string, string> before = Plan(from);
                Dictionary<string, string> after = Plan(to);

                string[] differing = before.Keys
                    .Where(key => !string.Equals(before[key], after[key], StringComparison.Ordinal))
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();

                Assert.That(
                    differing,
                    Is.EqualTo(changed.OrderBy(key => key, StringComparer.Ordinal)),
                    $"'{from}' -> '{to}' must change exactly {string.Join(", ", changed)}");
            }
        }

        [Test]
        public void Invariant_settings_are_invariant()
        {
            foreach (string profile in WebSizeProfiles.Names)
            {
                Dictionary<string, string> plan = Plan(profile);

                Assert.That(plan["scriptingBackend"], Is.EqualTo("IL2CPP"), profile);
                Assert.That(plan["il2cppCompilerConfiguration"], Is.EqualTo("Release"), profile);
                Assert.That(plan["stripEngineCode"], Is.EqualTo("true"), profile);
                Assert.That(plan["debugSymbolMode"], Is.EqualTo("Off"), profile);

                foreach (string invariant in InvariantSettings)
                {
                    Assert.That(plan[invariant], Is.EqualTo(Plan(WebSizeProfiles.Baseline)[invariant]), invariant + " must not vary");
                }
            }
        }

        [Test]
        public void Unknown_profile_is_rejected_rather_than_silently_treated_as_baseline()
        {
            Assert.That(WebSizeProfiles.IsKnown("nope"), Is.False);
            Assert.Throws<ArgumentException>(() => WebSizeProfiles.Apply("nope"));
            Assert.Throws<ArgumentException>(() => WebSizeProfiles.Describe("nope"));
        }

        [Test]
        public void Applying_a_profile_produces_exactly_the_plan_it_documents()
        {
            PlayerSettingsSnapshot saved = PlayerSettingsSnapshot.Capture();
            try
            {
                foreach (string profile in WebSizeProfiles.Names)
                {
                    WebSizeProfiles.Apply(profile);

                    Dictionary<string, string> plan = Plan(profile);
                    Dictionary<string, string> actual = ReadBack();

                    foreach (KeyValuePair<string, string> expected in plan)
                    {
                        Assert.That(actual[expected.Key], Is.EqualTo(expected.Value), $"{profile}: {expected.Key}");
                    }
                }
            }
            finally
            {
                saved.Restore();
            }
        }

        [Test]
        public void A_build_with_no_profile_is_not_labelled_as_one()
        {
            // A plain Release build (no -sizeProfile) uses whatever the project contains, so
            // its report must not carry a profile's name: that would turn "nobody configured
            // this build" into a data point in the comparison table.
            Assert.That(WebSizeProfiles.IsKnown(WebSizeProfiles.None), Is.False, "'none' is the absence of a profile");
            Assert.That(WebSizeProfiles.Names, Does.Not.Contain(WebSizeProfiles.None));

            string json = WebSizeProfiles.ToJson(WebSizeProfiles.None);

            Assert.That(json, Does.Contain("\"sizeProfile\": \"none\""));
            Assert.That(json, Does.Contain("\"sizeProfilePlan\": null"), "a build with no profile has no plan to show");
            Assert.That(json, Does.Contain("\"sizeSettings\""), "the settings it actually ran with are still reported");
        }

        [Test]
        public void A_profiled_build_carries_the_plan_it_was_built_with()
        {
            string json = WebSizeProfiles.ToJson(WebSizeProfiles.OptimizeSize);

            Assert.That(json, Does.Contain("\"sizeProfile\": \"optimize-size\""));
            Assert.That(json, Does.Not.Contain("\"sizeProfilePlan\": null"));
            Assert.That(json, Does.Contain("\"il2cppCodeGeneration\": \"OptimizeSize\""));
        }

        private static Dictionary<string, string> Plan(string profile) =>
            WebSizeProfiles.Describe(profile).ToDictionary(pair => pair.Key, pair => pair.Value);

        private static Dictionary<string, string> ReadBack() =>
            WebSizeProfiles.ReadBack().ToDictionary(pair => pair.Key, pair => pair.Value);

        /// <summary>
        /// The settings this fixture touches, captured so the project is put back the way
        /// it was found. Only the values <see cref="WebSizeProfiles"/> can set are needed.
        /// </summary>
        private readonly struct PlayerSettingsSnapshot
        {
            private readonly ScriptingImplementation _backend;
            private readonly Il2CppCompilerConfiguration _compiler;
            private readonly Il2CppCodeGeneration _codegen;
            private readonly ManagedStrippingLevel _stripping;
            private readonly WebGLExceptionSupport _exceptions;
            private readonly WebGLWasmArithmeticExceptions _arithmetic;
            private readonly WebGLDebugSymbolMode _symbols;
            private readonly bool _stripEngineCode;
            private readonly bool _analyzeBuildSize;

            private PlayerSettingsSnapshot(
                ScriptingImplementation backend,
                Il2CppCompilerConfiguration compiler,
                Il2CppCodeGeneration codegen,
                ManagedStrippingLevel stripping,
                WebGLExceptionSupport exceptions,
                WebGLWasmArithmeticExceptions arithmetic,
                WebGLDebugSymbolMode symbols,
                bool stripEngineCode,
                bool analyzeBuildSize)
            {
                _backend = backend;
                _compiler = compiler;
                _codegen = codegen;
                _stripping = stripping;
                _exceptions = exceptions;
                _arithmetic = arithmetic;
                _symbols = symbols;
                _stripEngineCode = stripEngineCode;
                _analyzeBuildSize = analyzeBuildSize;
            }

            public static PlayerSettingsSnapshot Capture() => new(
                PlayerSettings.GetScriptingBackend(WebGl),
                PlayerSettings.GetIl2CppCompilerConfiguration(WebGl),
                PlayerSettings.GetIl2CppCodeGeneration(WebGl),
                PlayerSettings.GetManagedStrippingLevel(WebGl),
                PlayerSettings.WebGL.exceptionSupport,
                PlayerSettings.WebGL.wasmArithmeticExceptions,
                PlayerSettings.WebGL.debugSymbolMode,
                PlayerSettings.stripEngineCode,
                PlayerSettings.WebGL.analyzeBuildSize);

            public void Restore()
            {
                PlayerSettings.SetScriptingBackend(WebGl, _backend);
                PlayerSettings.SetIl2CppCompilerConfiguration(WebGl, _compiler);
                PlayerSettings.SetIl2CppCodeGeneration(WebGl, _codegen);
                PlayerSettings.SetManagedStrippingLevel(WebGl, _stripping);
                PlayerSettings.WebGL.exceptionSupport = _exceptions;
                PlayerSettings.WebGL.wasmArithmeticExceptions = _arithmetic;
                PlayerSettings.WebGL.debugSymbolMode = _symbols;
                PlayerSettings.stripEngineCode = _stripEngineCode;
                PlayerSettings.WebGL.analyzeBuildSize = _analyzeBuildSize;
            }
        }
    }
}
