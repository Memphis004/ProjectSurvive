using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// The WebGL player settings that move build size, grouped into named profiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 03b compares builds against each other, so the configuration a number
    /// came from has to be a value that can be written down, re-applied and diffed —
    /// not Editor state somebody toggled by hand. Every profile sets the whole
    /// size-relevant surface (including the settings it leaves at their stage-03
    /// value), so a build's configuration is never a mix of "what this profile says"
    /// and "what the project happened to contain".
    /// </para>
    /// <para>
    /// Profiles are cumulative along the list in <see cref="Names"/>: each one differs
    /// from the previous by exactly one setting, which is what makes a per-change
    /// before/after table possible at all. The engine-side settings that are the
    /// IL2CPP release configuration (backend, strip engine code, debug symbols) are
    /// the same in every profile and are asserted, not varied.
    /// </para>
    /// </remarks>
    public static class WebSizeProfiles
    {
        /// <summary>
        /// The project's configuration before any size work: what the editor already has — WebGL
        /// defaults, read back by <see cref="DumpSettings"/> rather than assumed — with every value
        /// this stage can move pinned to it explicitly so the profile is reproducible.
        /// </summary>
        /// <remarks>
        /// Measured on a clean checkout: IL2CPP, Release C++ compiler, strip engine code on,
        /// debug symbols off, managed stripping <see cref="ManagedStrippingLevel.Low"/>, IL2CPP code
        /// generation <see cref="Il2CppCodeGeneration.OptimizeSpeed"/>, exceptions explicitly
        /// thrown only, arithmetic exceptions thrown.
        /// </remarks>
        public const string Baseline = "baseline";

        /// <summary>Baseline + managed stripping at <see cref="ManagedStrippingLevel.High"/>.</summary>
        public const string StripHigh = "strip-high";

        /// <summary>Strip-high + IL2CPP code generation optimised for size.</summary>
        public const string OptimizeSize = "optimize-size";

        /// <summary>Optimize-size + the lowest exception support Unity offers.</summary>
        public const string Minimal = "minimal";

        /// <summary>
        /// The label for a build that applied no profile at all: the project's own settings,
        /// read back and reported but not chosen by this stage. Deliberately not a member of
        /// <see cref="Names"/> — it is the absence of a configuration, not a fourth one to
        /// compare, and a build wearing it must never be read as a data point.
        /// </summary>
        public const string None = "none";

        /// <summary>Profiles in cumulative order — the order the size table is read in.</summary>
        public static readonly string[] Names = { Baseline, StripHigh, OptimizeSize, Minimal };

        /// <summary>The profile this stage recommends shipping; see docs/build/size-baseline.md.</summary>
        public const string Recommended = OptimizeSize;

        private static readonly NamedBuildTarget WebGl = NamedBuildTarget.WebGL;

        public static bool IsKnown(string name) => Array.IndexOf(Names, name) >= 0;

        /// <summary>
        /// Applies a profile to the project's player settings.
        /// </summary>
        /// <remarks>
        /// The settings live in memory until the Editor saves them; a batch-mode build
        /// with <c>-quit</c> writes them at exit, and <see cref="Persist"/> forces the
        /// same write from a running Editor. Calling this is what makes a build's
        /// configuration the profile's, whichever state the project was left in.
        /// </remarks>
        public static void Apply(string name)
        {
            if (!IsKnown(name))
            {
                throw new ArgumentException(
                    "unknown size profile '" + name + "' - expected one of " + string.Join(", ", Names),
                    nameof(name));
            }

            // The IL2CPP release configuration: identical in every profile, asserted by
            // ApplyAll so a build can never silently run a different backend.
            PlayerSettings.SetScriptingBackend(WebGl, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(WebGl, Il2CppCompilerConfiguration.Release);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.analyzeBuildSize = false;

            // Step 1: how much of the class library and the packages survives. Low is the project's
            // own default (asserted by WebSizeProfilesTests), so this step is the one that moves.
            PlayerSettings.SetManagedStrippingLevel(
                WebGl,
                name == Baseline ? ManagedStrippingLevel.Low : ManagedStrippingLevel.High);

            // Step 2: whether IL2CPP generates C++ for speed or for size. OptimizeSpeed is the WebGL
            // default, so pinning it is what makes step 1 and step 2 comparable at all: without it a
            // stripping-only build would silently also change the code generation.
            PlayerSettings.SetIl2CppCodeGeneration(
                WebGl,
                name == Baseline || name == StripHigh
                    ? Il2CppCodeGeneration.OptimizeSpeed
                    : Il2CppCodeGeneration.OptimizeSize);

            // Step 3: how much exception machinery the module carries. None removes the
            // try/catch support itself, so it is only correct for a build that can never
            // observe an exception - which is why it is a separate, last step.
            bool none = name == Minimal;
            PlayerSettings.WebGL.exceptionSupport = none
                ? WebGLExceptionSupport.None
                : WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.wasmArithmeticExceptions = none
                ? WebGLWasmArithmeticExceptions.Ignore
                : WebGLWasmArithmeticExceptions.Throw;
        }

        /// <summary>
        /// The four steps a profile walks through, as name/value pairs. This is both the
        /// applied plan the report quotes and the checklist a reader re-applies by hand.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Describe(string name)
        {
            if (!IsKnown(name))
            {
                throw new ArgumentException("unknown size profile '" + name + "'", nameof(name));
            }

            bool stripped = name != Baseline;
            bool sizeCodegen = name == OptimizeSize || name == Minimal;
            bool none = name == Minimal;

            return new[]
            {
                new KeyValuePair<string, string>("scriptingBackend", ScriptingImplementation.IL2CPP.ToString()),
                new KeyValuePair<string, string>("il2cppCompilerConfiguration", Il2CppCompilerConfiguration.Release.ToString()),
                new KeyValuePair<string, string>("stripEngineCode", "true"),
                new KeyValuePair<string, string>("debugSymbolMode", WebGLDebugSymbolMode.Off.ToString()),
                new KeyValuePair<string, string>("managedStrippingLevel", stripped ? "High" : "Low"),
                new KeyValuePair<string, string>("il2cppCodeGeneration", sizeCodegen ? "OptimizeSize" : "OptimizeSpeed"),
                new KeyValuePair<string, string>("exceptionSupport", none ? "None" : "ExplicitlyThrownExceptionsOnly"),
                new KeyValuePair<string, string>("wasmArithmeticExceptions", none ? "Ignore" : "Throw"),
            };
        }

        /// <summary>
        /// Every size-relevant setting read back from the Editor, so the report records the
        /// configuration a number actually came from rather than the one that was requested.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> ReadBack()
        {
            return new[]
            {
                new KeyValuePair<string, string>("scriptingBackend", PlayerSettings.GetScriptingBackend(WebGl).ToString()),
                new KeyValuePair<string, string>("il2cppCompilerConfiguration", PlayerSettings.GetIl2CppCompilerConfiguration(WebGl).ToString()),
                new KeyValuePair<string, string>("stripEngineCode", PlayerSettings.stripEngineCode ? "true" : "false"),
                new KeyValuePair<string, string>("debugSymbolMode", PlayerSettings.WebGL.debugSymbolMode.ToString()),
                new KeyValuePair<string, string>("managedStrippingLevel", PlayerSettings.GetManagedStrippingLevel(WebGl).ToString()),
                new KeyValuePair<string, string>("il2cppCodeGeneration", PlayerSettings.GetIl2CppCodeGeneration(WebGl).ToString()),
                new KeyValuePair<string, string>("exceptionSupport", PlayerSettings.WebGL.exceptionSupport.ToString()),
                new KeyValuePair<string, string>("wasmArithmeticExceptions", PlayerSettings.WebGL.wasmArithmeticExceptions.ToString()),
                new KeyValuePair<string, string>("compressionFormat", PlayerSettings.WebGL.compressionFormat.ToString()),
                new KeyValuePair<string, string>("analyzeBuildSize", PlayerSettings.WebGL.analyzeBuildSize ? "true" : "false"),
                new KeyValuePair<string, string>("emscriptenArgs", PlayerSettings.WebGL.emscriptenArgs ?? string.Empty),
            };
        }

        /// <summary>Applies a profile and writes the project settings to disk.</summary>
        public static void ApplyAndPersist(string name)
        {
            Apply(name);
            Persist();
        }

        /// <summary>
        /// Writes ProjectSettings to disk from a running Editor. Marking the settings dirty
        /// is not the same as saving them, and a size baseline nobody can rebuild is not a
        /// baseline.
        /// </summary>
        /// <remarks>
        /// Batch mode is left alone on purpose: it has no File menu to drive and writes
        /// ProjectSettings itself on a clean <c>-quit</c>, so driving the menu item there
        /// would only log a failure.
        /// </remarks>
        public static void Persist()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            EditorApplication.ExecuteMenuItem("File/Save Project");
        }

        /// <summary>
        /// Logs the size-relevant settings the project currently has, plus what every profile
        /// would set, without building anything.
        /// </summary>
        /// <remarks>
        /// This exists because a profile is only meaningful next to the value it replaces:
        /// <c>baseline</c> claims to be the configuration Stage 03 shipped, and the way to check
        /// that claim is to read the project's own settings on a clean checkout. Run it with
        /// <c>Unity.exe -batchmode -quit -executeMethod
        /// ProjectSurvive.Game.Editor.WebSizeProfiles.DumpSettings</c>.
        /// </remarks>
        public static void DumpSettings()
        {
            Debug.Log("WebSizeProfiles: project settings as found:");
            foreach (KeyValuePair<string, string> pair in ReadBack())
            {
                Debug.Log("WebSizeProfiles:   " + pair.Key + " = " + pair.Value);
            }

            foreach (string profile in Names)
            {
                Debug.Log("WebSizeProfiles: plan " + profile + " -> " + DescribeOneLine(profile));
                foreach (KeyValuePair<string, string> pair in Describe(profile))
                {
                    Debug.Log("WebSizeProfiles:   " + pair.Key + " = " + pair.Value);
                }
            }
        }

        /// <summary>Menu entry, so a human can put the project into a profile without a build.</summary>
        [MenuItem("ProjectSurvive/Stage 03b/Apply size profile: strip-high", priority = 120)]
        private static void ApplyStripHighFromMenu() => ApplyAndPersist(StripHigh);

        [MenuItem("ProjectSurvive/Stage 03b/Apply size profile: optimize-size (recommended)", priority = 121)]
        private static void ApplyOptimizeSizeFromMenu() => ApplyAndPersist(OptimizeSize);

        [MenuItem("ProjectSurvive/Stage 03b/Apply size profile: minimal", priority = 122)]
        private static void ApplyMinimalFromMenu() => ApplyAndPersist(Minimal);

        [MenuItem("ProjectSurvive/Stage 03b/Apply size profile: baseline (stage 03)", priority = 123)]
        private static void ApplyBaselineFromMenu() => ApplyAndPersist(Baseline);

        /// <summary>The applied settings as JSON, for <c>build-report.json</c>.</summary>
        public static string ToJson(string profile)
        {
            var json = new StringBuilder();
            json.Append("    \"sizeProfile\": \"").Append(profile).Append("\",\n");
            json.Append("    \"sizeSettings\": {\n");
            IReadOnlyList<KeyValuePair<string, string>> settings = ReadBack();
            for (int i = 0; i < settings.Count; i++)
            {
                json.Append("      \"")
                    .Append(settings[i].Key)
                    .Append("\": \"")
                    .Append(settings[i].Value)
                    .Append(i == settings.Count - 1 ? "\"\n" : "\",\n");
            }

            json.Append("    },\n");
            json.Append("    \"sizeProfilePlan\": ");

            if (!IsKnown(profile))
            {
                // No profile was applied — the settings above are the project's, not this
                // stage's. Reporting the plan of some other profile here would make a build
                // nobody configured look like one that was measured.
                json.Append("null");
                return json.ToString();
            }

            json.Append("{\n");
            IReadOnlyList<KeyValuePair<string, string>> plan = Describe(profile);
            for (int i = 0; i < plan.Count; i++)
            {
                json.Append("      \"")
                    .Append(plan[i].Key)
                    .Append("\": \"")
                    .Append(plan[i].Value)
                    .Append(i == plan.Count - 1 ? "\"\n" : "\",\n");
            }

            json.Append("    }");
            return json.ToString();
        }

        /// <summary>A human-readable one-liner, for the build log.</summary>
        public static string DescribeOneLine(string profile)
        {
            var parts = new List<string>();
            foreach (KeyValuePair<string, string> pair in Describe(profile))
            {
                if (pair.Key is "managedStrippingLevel" or "il2cppCodeGeneration" or "exceptionSupport")
                {
                    parts.Add(pair.Key + "=" + pair.Value);
                }
            }

            return string.Format(CultureInfo.InvariantCulture, "size profile '{0}': {1}", profile, string.Join(", ", parts));
        }
    }
}
