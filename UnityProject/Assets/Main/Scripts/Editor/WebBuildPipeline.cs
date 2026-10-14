using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
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

        /// <summary>
        /// Size-measurement build: applies the size profile named by <c>-sizeProfile</c> on the
        /// command line and writes the Release player to <c>-outputDir</c>, so one invocation is
        /// one reproducible data point instead of one Editor session's opinion.
        /// </summary>
        /// <remarks>
        /// The profile is applied in the same process as the build, never read back from the
        /// project: a size number whose configuration depends on Editor state cannot be
        /// reproduced, and <c>tools/build-web.ps1 -SizeProfile</c> is how a reader re-runs it.
        /// </remarks>
        public static void BuildWebForSize()
        {
            string profile = CommandLineValue("-sizeProfile") ?? WebSizeProfiles.Baseline;
            string outputDirectory = CommandLineValue("-outputDir") ?? OutputDirectory;

            if (!WebSizeProfiles.IsKnown(profile))
            {
                Debug.LogError("WebBuildPipeline: unknown -sizeProfile '" + profile + "'. Known profiles: " + string.Join(", ", WebSizeProfiles.Names));
                throw new ArgumentException("unknown size profile '" + profile + "'", nameof(profile));
            }

            WebSizeProfiles.ApplyAndPersist(profile);
            Debug.Log("WebBuildPipeline: " + WebSizeProfiles.DescribeOneLine(profile) + " -> " + outputDirectory);

            if (!BuildWeb(false, outputDirectory, profile))
            {
                throw new InvalidOperationException("WebGL size build failed - see the Editor console for the report.");
            }
        }

        /// <summary>
        /// The value following a bare <c>-flag value</c> pair on the Unity command line, or null.
        /// Unity forwards arguments it does not recognise, which is how a calling script
        /// parameterises an <c>-executeMethod</c> entry point.
        /// </summary>
        private static string CommandLineValue(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.Ordinal))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static bool BuildWeb(bool development, string outputDirectory = OutputDirectory, string sizeProfile = WebSizeProfiles.None)
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
                locationPathName = outputDirectory,
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
                outputDirectory));

            if (summary.result != BuildResult.Succeeded)
            {
                return false;
            }

            WriteBuildInfo(outputDirectory, scenes, development, summary);
            WriteSizeReport(outputDirectory, development, sizeProfile, options, report);
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
        private static void WriteBuildInfo(string outputDirectory, string[] scenes, bool development, BuildSummary summary)
        {
            string path = Path.Combine(outputDirectory, "build-info.json");
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

        /// <summary>
        /// Writes <c>build-report.json</c> next to the player: the player files, the largest
        /// packed assets, and the player settings that produced them.
        /// </summary>
        /// <remarks>
        /// The settings travel with the numbers on purpose. Stage 03b compares one build
        /// against another, and a size figure whose configuration is not recorded is not
        /// evidence of anything. A build that applied no profile is labelled
        /// <see cref="WebSizeProfiles.None"/> rather than being given a profile's name it
        /// never ran. Same hand-rolled JSON as <see cref="WriteBuildInfo"/> so the
        /// build folder needs no serializer.
        /// </remarks>
        private static void WriteSizeReport(string outputDirectory, bool development, string sizeProfile, BuildPlayerOptions options, BuildReport report)
        {
            BuildSummary summary = report.summary;
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"unityVersion\": \"").Append(Application.unityVersion).Append("\",\n");
            json.Append("  \"development\": ").Append(development ? "true" : "false").Append(",\n");
            json.Append("  \"buildOptions\": \"").Append(options.options).Append("\",\n");
            json.Append("  \"outputDirectory\": \"").Append(Escape(outputDirectory)).Append("\",\n");
            json.Append("  \"result\": \"").Append(summary.result).Append("\",\n");
            json.Append("  \"totalSizeBytes\": ").Append(summary.totalSize.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"totalTimeSeconds\": ").Append(summary.totalTime.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"totalErrors\": ").Append(summary.totalErrors.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"totalWarnings\": ").Append(summary.totalWarnings.ToString(CultureInfo.InvariantCulture)).Append(",\n");

            // The settings travel with the numbers: the profile that was asked for, the
            // settings that were actually in effect, and the values each step set.
            json.Append(WebSizeProfiles.ToJson(sizeProfile)).Append(",\n");

            json.Append("  \"playerFiles\": [");
            BuildFile[] files = report.GetFiles();
            for (int i = 0; i < files.Length; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ");
                json.Append("{ \"path\": \"").Append(Escape(files[i].path)).Append("\", \"role\": \"").Append(files[i].role).Append("\", \"sizeBytes\": ").Append(files[i].size.ToString(CultureInfo.InvariantCulture)).Append(" }");
            }

            json.Append("\n  ],\n");

            // Packed assets are per-object, not per-file: one source asset can own a whole
            // hierarchy, so the rows are summed per source asset before they are ranked.
            var byAsset = new Dictionary<string, AssetSizeEntry>();
            var byType = new Dictionary<string, long>();
            PackedAssets[] bundles = report.packedAssets;
            for (int b = 0; b < bundles.Length; b++)
            {
                string bundle = bundles[b].shortPath;
                PackedAssetInfo[] contents = bundles[b].contents;
                for (int c = 0; c < contents.Length; c++)
                {
                    string assetPath = string.IsNullOrEmpty(contents[c].sourceAssetPath) ? "(internal)" : contents[c].sourceAssetPath;
                    string type = contents[c].type.Name;
                    long size = (long)contents[c].packedSize;

                    if (byAsset.TryGetValue(assetPath, out AssetSizeEntry entry))
                    {
                        entry.Size += size;
                        entry.ObjectCount++;
                        byAsset[assetPath] = entry;
                    }
                    else
                    {
                        byAsset.Add(assetPath, new AssetSizeEntry(assetPath, type, bundle, size));
                    }

                    byType.TryGetValue(type, out long typeTotal);
                    byType[type] = typeTotal + size;
                }
            }

            var assets = new List<AssetSizeEntry>(byAsset.Values);
            assets.Sort(LargestFirst);
            json.Append("  \"packedAssetCount\": ").Append(assets.Count.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"largestAssets\": [");
            int take = Math.Min(20, assets.Count);
            for (int i = 0; i < take; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ");
                json.Append("{ \"assetPath\": \"").Append(Escape(assets[i].Path)).Append("\", \"type\": \"").Append(assets[i].Type).Append("\", \"bundle\": \"").Append(Escape(assets[i].Bundle)).Append("\", \"objectCount\": ").Append(assets[i].ObjectCount.ToString(CultureInfo.InvariantCulture)).Append(", \"sizeBytes\": ").Append(assets[i].Size.ToString(CultureInfo.InvariantCulture)).Append(" }");
            }

            json.Append("\n  ],\n");

            var types = new List<TypeSizeEntry>();
            foreach (KeyValuePair<string, long> pair in byType)
            {
                types.Add(new TypeSizeEntry(pair.Key, pair.Value));
            }

            types.Sort((a, b) => b.Size.CompareTo(a.Size));
            json.Append("  \"sizeByObjectType\": [");
            int typeTake = Math.Min(12, types.Count);
            for (int i = 0; i < typeTake; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ");
                json.Append("{ \"type\": \"").Append(types[i].Type).Append("\", \"sizeBytes\": ").Append(types[i].Size.ToString(CultureInfo.InvariantCulture)).Append(" }");
            }

            json.Append("\n  ]\n}\n");

            string path = Path.Combine(outputDirectory, "build-report.json");
            File.WriteAllText(path, json.ToString());
            Debug.Log("WebBuildPipeline: wrote " + path);
        }

        private static int LargestFirst(AssetSizeEntry a, AssetSizeEntry b) => b.Size.CompareTo(a.Size);

        private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

        private struct AssetSizeEntry
        {
            public AssetSizeEntry(string path, string type, string bundle, long size)
            {
                Path = path;
                Type = type;
                Bundle = bundle;
                Size = size;
                ObjectCount = 1;
            }

            public readonly string Path;
            public readonly string Type;
            public readonly string Bundle;
            public long Size;
            public int ObjectCount;
        }

        private readonly struct TypeSizeEntry
        {
            public TypeSizeEntry(string type, long size)
            {
                Type = type;
                Size = size;
            }

            public readonly string Type;
            public readonly long Size;
        }
    }
}
