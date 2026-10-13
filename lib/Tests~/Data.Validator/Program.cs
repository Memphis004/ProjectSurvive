using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Config;

namespace ProjectSurvive.Data.Validator
{
    /// <summary>
    /// Validates the generated config tables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs in two stages, matching where each rule can be enforced:
    /// </para>
    /// <list type="number">
    /// <item>LOAD — <see cref="LubanGameConfig.Load"/> turns the .bytes into the
    /// domain snapshot. A failure here is already a validation failure (a table
    /// missing, a ragged or malformed footprint picture); it is reported with
    /// the <c>ConfigLoadException.Code</c>.</item>
    /// <item>SEMANTIC — <see cref="ConfigValidator"/> applies the cross-table game
    /// rules.</item>
    /// </list>
    /// <para>Exit codes: 0 = clean, 1 = findings, 2 = could not run.</para>
    /// </remarks>
    public static class Program
    {
        private const string Usage =
            "usage: Data.Validator --data <dir-with-.bytes> [--expect-hash <hex>] [--json]";

        public static int Main(string[] args)
        {
            string? dataDir = null;
            string? expectedHash = null;
            bool json = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--data":
                        dataDir = i + 1 < args.Length ? args[++i] : null;
                        break;
                    case "--expect-hash":
                        expectedHash = i + 1 < args.Length ? args[++i] : null;
                        break;
                    case "--json":
                        json = true;
                        break;
                    default:
                        Console.Error.WriteLine("unknown argument: " + args[i]);
                        Console.Error.WriteLine(Usage);
                        return 2;
                }
            }

            if (string.IsNullOrEmpty(dataDir) || !Directory.Exists(dataDir))
            {
                Console.Error.WriteLine("--data must point at an existing directory");
                Console.Error.WriteLine(Usage);
                return 2;
            }

            return Validate(dataDir!, expectedHash, json);
        }

        /// <summary>
        /// Loads and validates the tables in <paramref name="dataDir"/>.
        /// Shared with the xUnit tests so the tool and the tests run one code path.
        /// </summary>
        public static int Validate(string dataDir, string? expectedHash, bool json)
        {
            var findings = new List<Finding>();

            var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string name in LubanGameConfig.TableNames)
            {
                string path = Path.Combine(dataDir, name + ".bytes");
                if (File.Exists(path))
                {
                    bytes[name] = File.ReadAllBytes(path);
                }
                else
                {
                    findings.Add(new Finding(ConfigError.TableMissing, name,
                        "no " + name + ".bytes in " + dataDir));
                }
            }

            IGameConfig? config = null;
            if (findings.Count == 0)
            {
                string hash = expectedHash ?? ConfigHash.Compute(bytes);
                try
                {
                    config = LubanGameConfig.Load(name => bytes.TryGetValue(name, out byte[]? table)
                        ? table
                        : throw new InvalidOperationException("missing table " + name), hash);
                }
                catch (ConfigLoadException exception)
                {
                    findings.Add(new Finding(exception.Code, exception.Target, exception.Message));
                }
            }

            if (config is not null)
            {
                findings.AddRange(ConfigValidator.Validate(config));

                if (expectedHash is not null
                    && !string.Equals(config.ConfigHash, expectedHash, StringComparison.Ordinal))
                {
                    findings.Add(new Finding("config.hash_mismatch", "build metadata",
                        "loaded hash " + config.ConfigHash + " != expected " + expectedHash));
                }
            }

            Report(dataDir, config, findings, json);
            return findings.Count == 0 ? 0 : 1;
        }

        private static void Report(string dataDir, IGameConfig? config, List<Finding> findings, bool json)
        {
            if (json)
            {
                var builder = new StringBuilder();
                builder.Append("{\n  \"dataDir\": \"").Append(Escape(dataDir)).Append("\",\n");
                builder.Append("  \"configHash\": ")
                    .Append(config is null ? "null" : "\"" + config.ConfigHash + "\"")
                    .Append(",\n");
                builder.Append("  \"tableCount\": ")
                    .Append(config is null ? 0 : LubanGameConfig.TableNames.Length)
                    .Append(",\n");
                builder.Append("  \"ok\": ").Append(findings.Count == 0 ? "true" : "false").Append(",\n");
                builder.Append("  \"findingCount\": ").Append(findings.Count).Append(",\n");
                builder.Append("  \"findings\": [");
                for (int i = 0; i < findings.Count; i++)
                {
                    builder.Append(i == 0 ? "\n" : ",\n");
                    builder.Append("    { \"code\": \"").Append(Escape(findings[i].Code))
                        .Append("\", \"target\": \"").Append(Escape(findings[i].Target))
                        .Append("\", \"message\": \"").Append(Escape(findings[i].Message)).Append("\" }");
                }

                builder.Append(findings.Count == 0 ? "]\n" : "\n  ]\n");
                builder.Append("}\n");
                Console.Out.Write(builder.ToString());
                return;
            }

            Console.WriteLine("ProjectSurvive - data validation");
            Console.WriteLine(new string('=', 72));
            Console.WriteLine("  dataDir   : " + dataDir);
            if (config is not null)
            {
                Console.WriteLine("  configHash: " + config.ConfigHash);
                Console.WriteLine("  rows      : items " + config.Items.Count
                    + ", bags " + config.Bags.Count
                    + ", skills " + config.Skills.Count
                    + ", synergies " + config.Synergies.Count
                    + ", mergeRecipes " + config.MergeRecipes.Count
                    + ", enemies " + config.Enemies.Count
                    + ", floors " + config.Floors.Count
                    + ", waves " + config.Waves.Count
                    + ", shopPool " + config.ShopPool.Count
                    + ", localization " + config.Localization.Count);
            }

            Console.WriteLine(new string('=', 72));
            if (findings.Count == 0)
            {
                Console.WriteLine("  RESULT: OK (no findings)");
            }
            else
            {
                foreach (Finding finding in findings)
                {
                    Console.WriteLine("  [FAIL] " + finding.Code);
                    Console.WriteLine("         at " + finding.Target);
                    Console.WriteLine("         " + finding.Message);
                }

                Console.WriteLine(new string('=', 72));
                Console.WriteLine("  RESULT: FAILED (" + findings.Count.ToString(CultureInfo.InvariantCulture)
                    + " finding(s))");
            }
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
