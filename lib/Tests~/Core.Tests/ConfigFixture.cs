using System;
using System.Collections.Generic;
using System.IO;
using ProjectSurvive.Core.Infrastructure.Config;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Loads the generated config tables the way the game does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every test goes through <see cref="LubanGameConfig.Load"/> over the real
    /// <c>lib/Tests~/data/*.bytes</c> produced by tools/gen.ps1 - not over a
    /// hand-written stub. That is the point: a stub would prove nothing about the
    /// generated readers, the binary format or the adapter's mapping.
    /// </para>
    /// <para>
    /// No caching. A test may build a snapshot of its own to inject exactly one violation, and a
    /// shared instance would leak that mutation into every other test; re-reading two small
    /// files is cheaper than that class of bug.
    /// </para>
    /// </remarks>
    internal static class ConfigFixture
    {
        /// <summary>A valid SHA-256 hex string, for loads that are not about the hash.</summary>
        internal const string DummyHash = "0000000000000000000000000000000000000000000000000000000000000000";

        /// <summary>Reads the generated tables from disk.</summary>
        internal static Dictionary<string, byte[]> ReadTables()
        {
            var tables = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string name in LubanGameConfig.TableNames)
            {
                string path = Path.Combine(RepoLayout.TestDataDir, name + ".bytes");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "Generated table missing: " + path + ". Run tools/gen.ps1 "
                        + "(lib/Tests~/data is produced, never committed - ADR 0001).", path);
                }

                tables[name] = File.ReadAllBytes(path);
            }

            return tables;
        }

        /// <summary>The real tables, loaded with the build's own hash.</summary>
        internal static LubanGameConfig Load()
        {
            Dictionary<string, byte[]> tables = ReadTables();
            return LubanGameConfig.Load(name => tables[name], ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash);
        }

        /// <summary>Loads with a table source of your own, for the failure paths.</summary>
        internal static LubanGameConfig LoadWith(Func<string, byte[]> readTable) =>
            LubanGameConfig.Load(readTable, DummyHash);
    }
}
