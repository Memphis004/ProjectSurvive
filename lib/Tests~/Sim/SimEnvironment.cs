using System;
using System.Collections.Generic;
using System.IO;
using ProjectSurvive.Core.Infrastructure.Config;

namespace ProjectSurvive.Sim
{
    /// <summary>
    /// Where the headless harness finds the repository, the generated config tables and
    /// the artifacts folder.
    /// </summary>
    /// <remarks>
    /// One implementation, resolved once: a harness that guesses a path per mode is how
    /// a bot run silently reads a stale table set. The generated <c>.bytes</c> live in
    /// <c>lib/Tests~/data</c> (git-ignored, produced by tools/gen.ps1) exactly as the
    /// xUnit suite reads them, so the bot and the tests agree on what the data says.
    /// </remarks>
    internal static class SimEnvironment
    {
        /// <summary>The repository root: the nearest ancestor holding AGENTS.md next to lib/Runtime.</summary>
        internal static string RepoRoot { get; } = FindRoot();

        /// <summary><c>lib/Tests~/data</c>, or an explicit override.</summary>
        internal static string DataDirectory(string? overrideDirectory) =>
            string.IsNullOrEmpty(overrideDirectory)
                ? Path.Combine(RepoRoot, "lib", "Tests~", "data")
                : Path.GetFullPath(overrideDirectory);

        /// <summary><c>artifacts/</c>, created when missing (git-ignored by design).</summary>
        internal static string ArtifactsDirectory
        {
            get
            {
                string directory = Path.Combine(RepoRoot, "artifacts");
                Directory.CreateDirectory(directory);
                return directory;
            }
        }

        /// <summary>Resolves an output path against the repository root.</summary>
        internal static string OutputPath(string path) =>
            Path.IsPathRooted(path) ? path : Path.Combine(RepoRoot, path);

        /// <summary>Loads the real config tables the way the game does.</summary>
        /// <exception cref="FileNotFoundException">A generated table is missing.</exception>
        internal static LubanGameConfig LoadConfig(string? dataDirectory)
        {
            string directory = DataDirectory(dataDirectory);
            var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (string name in LubanGameConfig.TableNames)
            {
                string path = Path.Combine(directory, name + ".bytes");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "Generated table missing: " + path + ". Run tools/gen.ps1 (lib/Tests~/data is "
                        + "produced, never committed - ADR 0001).", path);
                }

                tables[name] = File.ReadAllBytes(path);
            }

            return LubanGameConfig.Load(
                name => tables[name], ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash);
        }

        private static string FindRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                    && Directory.Exists(Path.Combine(directory.FullName, "lib", "Runtime")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate the repository root above " + AppContext.BaseDirectory
                + " (looked for AGENTS.md next to lib/Runtime).");
        }
    }
}
