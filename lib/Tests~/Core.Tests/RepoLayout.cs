using System;
using System.IO;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Locates repository paths from a test process, which starts in
    /// <c>lib/Tests~/Core.Tests/bin/&lt;config&gt;/net9.0</c>.
    /// </summary>
    /// <remarks>
    /// One implementation on purpose: the alternative - each test file walking
    /// up until it finds a known file - is the classic way a suite ends up
    /// silently reading a stale copy after the layout moves.
    /// </remarks>
    internal static class RepoLayout
    {
        /// <summary>
        /// The repository root: the nearest ancestor that holds <c>AGENTS.md</c>
        /// next to <c>lib/Runtime</c>.
        /// </summary>
        internal static string Root { get; } = FindRoot();

        /// <summary>
        /// <c>lib/Tests~/data</c> - the generated <c>.bytes</c> tables the dotnet
        /// harness reads. Written by tools/gen.ps1, git-ignored (ADR 0001).
        /// </summary>
        internal static string TestDataDir { get; } = Path.Combine(Root, "lib", "Tests~", "data");

        /// <summary><c>lib/Tests~/fixtures</c> - deliberately broken data copies.</summary>
        internal static string FixturesDir { get; } = Path.Combine(Root, "lib", "Tests~", "fixtures");

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
