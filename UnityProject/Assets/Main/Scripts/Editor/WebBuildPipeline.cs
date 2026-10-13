using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds the WebGL player the browser tests run against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two entry points, one implementation: a menu item for a human in the Editor,
    /// and a public static method for <c>Unity -executeMethod</c> so CI (or
    /// <c>tools/build-web.ps1</c>) produces the same artifact. Keeping them in one
    /// file is what stops "the build CI makes" and "the build you make" from
    /// drifting.
    /// </para>
    /// <para>
    /// The build is <b>Development</b> by default: it keeps managed stack traces and
    /// assertions, which is what makes a failing smoke probe diagnosable in a
    /// browser. It also means the artifact is not a shippable release build — the
    /// performance numbers a later stage reports must come from a release build.
    /// </para>
    /// </remarks>
    public static class WebBuildPipeline
    {
        /// <summary>Output directory, relative to the project root. Git-ignored.</summary>
        public const string OutputDirectory = "Builds/Web";

        [MenuItem("ProjectSurvive/Stage 03/3. Build Web (Development)", priority = 102)]
        public static void BuildWebDevelopmentFromMenu()
        {
            bool ok = BuildWeb(true);
            if (!ok)
            {
                // A menu item cannot return a failure to the Editor UI, but a
                // non-zero exit code in batch mode is what a script checks.
                throw new InvalidOperationException("WebGL build failed - see the Editor console for the report.");
            }
        }

        /// <summary>
        /// Command-line entry point: <c>-executeMethod ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment</c>.
        /// </summary>
        public static void BuildWebDevelopment() => BuildWeb(true);

        /// <summary>Release variant, kept for the stage that needs real timing numbers.</summary>
        public static void BuildWebRelease() => BuildWeb(false);

        private static bool BuildWeb(bool development)
        {
            string[] scenes = EnabledScenePaths();
            if (scenes.Length == 0)
            {
                Debug.LogError("WebBuildPipeline: no enabled scenes in EditorBuildSettings - run ProjectSurvive/Stage 03/2 first.");
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDirectory,
                target = BuildTarget.WebGL,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "WebBuildPipeline: {0} - {1} bytes, {2} errors, {3} warnings, {4}s -> {5}",
                summary.result,
                summary.totalSize,
                summary.totalErrors,
                summary.totalWarnings,
                summary.totalTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture),
                OutputDirectory));

            if (summary.result != BuildResult.Succeeded)
            {
                return false;
            }

            WriteBuildInfo(scenes, development, summary);
            return true;
        }

        private static string[] EnabledScenePaths()
        {
            var paths = new List<string>();
            EditorBuildSettingsScene[] configured = EditorBuildSettings.scenes;
            for (int i = 0; i < configured.Length; i++)
            {
                if (configured[i].enabled && !string.IsNullOrEmpty(configured[i].path))
                {
                    paths.Add(configured[i].path);
                }
            }

            return paths.ToArray();
        }

        /// <summary>
        /// Records what was built next to the player, so a browser test run (or a bug
        /// report) can name the exact artifact it exercised. Lives in the ignored
        /// build folder - it describes an artifact, it is not one.
        /// </summary>
        private static void WriteBuildInfo(string[] scenes, bool development, BuildSummary summary)
        {
            string path = Path.Combine(OutputDirectory, "build-info.json");
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"unityVersion\": \"").Append(Application.unityVersion).Append("\",\n");
            json.Append("  \"development\": ").Append(development ? "true" : "false").Append(",\n");
            json.Append("  \"configHash\": \"").Append(ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash).Append("\",\n");
            json.Append("  \"totalSizeBytes\": ").Append(summary.totalSize.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"buildSeconds\": ").Append(summary.totalTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"warnings\": ").Append(summary.totalWarnings.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"scenes\": [");

            for (int i = 0; i < scenes.Length; i++)
            {
                if (i > 0)
                {
                    json.Append(", ");
                }

                json.Append('"').Append(scenes[i]).Append('"');
            }

            json.Append("]\n}\n");
            File.WriteAllText(path, json.ToString());
            Debug.Log("WebBuildPipeline: wrote " + path);
        }
    }
}
