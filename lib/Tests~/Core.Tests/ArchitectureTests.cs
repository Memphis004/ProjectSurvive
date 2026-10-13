using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// Guards the layer rules of AGENTS.md rule 3 and the Stage 01 acceptance
    /// criterion "Core does not reference UnityEngine".
    /// </summary>
    /// <remarks>
    /// The primary enforcement is the build itself: one csproj per layer with
    /// only the allowed ProjectReferences, so a wrong-direction reference does
    /// not compile (see docs/adr/0004-dotnet-layer-enforcement.md). These tests
    /// cover what a compiler cannot: that the asmdefs and csprojs really do
    /// describe the documented matrix, that the compiled assemblies carry no
    /// Unity reference, and that no source file sneaks in the forbidden APIs.
    /// </remarks>
    public sealed class ArchitectureTests
    {
        private static readonly (string Layer, string ProjectFile, string AssemblyName)[] Layers =
        {
            ("Shared", "ProjectSurvive.Core.Shared.csproj", "ProjectSurvive.Core.Shared"),
            ("Domain", "ProjectSurvive.Core.Domain.csproj", "ProjectSurvive.Core.Domain"),
            ("Application", "ProjectSurvive.Core.Application.csproj", "ProjectSurvive.Core.Application"),
            ("Simulation", "ProjectSurvive.Core.Simulation.csproj", "ProjectSurvive.Core.Simulation"),
            ("Infrastructure", "ProjectSurvive.Core.Infrastructure.csproj", "ProjectSurvive.Core.Infrastructure"),
            ("Contracts", "ProjectSurvive.Core.Contracts.csproj", "ProjectSurvive.Core.Contracts"),
        };

        private static readonly string[] LibAssemblies =
        {
            "ProjectSurvive.Core.Shared",
            "ProjectSurvive.Core.Domain",
            "ProjectSurvive.Core.Application",
            "ProjectSurvive.Core.Simulation",
            "ProjectSurvive.Core.Infrastructure",
            "ProjectSurvive.Core.Contracts",
            "Leopotam.EcsLite",
        };

        /// <summary>The reference matrix of docs/architecture/overview.md.</summary>
        private static readonly Dictionary<string, string[]> ExpectedAsmdefReferences =
            new Dictionary<string, string[]>
            {
                { "Shared", Array.Empty<string>() },
                { "Domain", new[] { "ProjectSurvive.Core.Shared" } },
                { "Application", new[] { "ProjectSurvive.Core.Shared", "ProjectSurvive.Core.Domain" } },
                {
                    "Simulation",
                    new[] { "ProjectSurvive.Core.Shared", "ProjectSurvive.Core.Domain", "Leopotam.EcsLite" }
                },
                {
                    "Infrastructure",
                    new[]
                    {
                        "ProjectSurvive.Core.Shared", "ProjectSurvive.Core.Domain", "ProjectSurvive.Core.Application",
                    }
                },
                { "Contracts", Array.Empty<string>() },
            };

        private static readonly Dictionary<string, string[]> ExpectedProjectReferences =
            new Dictionary<string, string[]>
            {
                { "ProjectSurvive.Core.Shared", Array.Empty<string>() },
                { "ProjectSurvive.Core.Domain", new[] { "ProjectSurvive.Core.Shared.csproj" } },
                {
                    "ProjectSurvive.Core.Application",
                    new[] { "ProjectSurvive.Core.Shared.csproj", "ProjectSurvive.Core.Domain.csproj" }
                },
                {
                    "ProjectSurvive.Core.Simulation",
                    new[]
                    {
                        "ProjectSurvive.Core.Shared.csproj", "ProjectSurvive.Core.Domain.csproj",
                        "Leopotam.EcsLite.csproj",
                    }
                },
                {
                    "ProjectSurvive.Core.Infrastructure",
                    new[]
                    {
                        "ProjectSurvive.Core.Shared.csproj", "ProjectSurvive.Core.Domain.csproj",
                        "ProjectSurvive.Core.Application.csproj",
                    }
                },
                { "ProjectSurvive.Core.Contracts", Array.Empty<string>() },
            };

        /// <summary>APIs AGENTS.md rule 6 keeps out of the rule engine.</summary>
        private static readonly string[] ForbiddenRuleEngineApis =
        {
            "System.Random",
            "UnityEngine.Random",
            "DateTime.Now",
            "DateTime.UtcNow",
            "Stopwatch",
        };

        [Fact]
        public void Core_assemblies_do_not_reference_unity()
        {
            var offenders = new List<string>();

            foreach (string name in LibAssemblies)
            {
                Assembly assembly = Assembly.Load(new AssemblyName(name));
                foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
                {
                    string? referencedName = referenced.Name;
                    if (referencedName is null)
                    {
                        continue;
                    }

                    if (referencedName.StartsWith("UnityEngine", StringComparison.Ordinal)
                        || referencedName.StartsWith("UnityEditor", StringComparison.Ordinal))
                    {
                        offenders.Add(name + " references " + referencedName);
                    }
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Runtime_sources_do_not_reference_unity()
        {
            var offenders = new List<string>();

            foreach (string file in LibSourceFiles("Runtime"))
            {
                if (StripComments(File.ReadAllText(file)).Contains("UnityEngine", StringComparison.Ordinal))
                {
                    offenders.Add(RelativePath(file));
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Vendored_ecslite_unity_usage_stays_behind_preprocessor_guards()
        {
            // The vendored ECS is the single deliberate exception (VENDOR.md): it
            // touches Unity for IL2CPP attributes only, and every such use must be
            // inside a #if region - otherwise the engine-free harness build and
            // the Unity build would not both compile.
            var offenders = new List<string>();

            foreach (string file in LibSourceFiles("third_party"))
            {
                string source = File.ReadAllText(file);
                int index = source.IndexOf("UnityEngine", StringComparison.Ordinal);
                while (index >= 0)
                {
                    int lastIf = source.LastIndexOf("#if", index, StringComparison.Ordinal);
                    int lastEndIf = source.LastIndexOf("#endif", index, StringComparison.Ordinal);
                    if (lastIf < 0 || lastIf < lastEndIf)
                    {
                        offenders.Add(RelativePath(file) + " uses UnityEngine outside a #if region");
                    }

                    index = source.IndexOf("UnityEngine", index + 1, StringComparison.Ordinal);
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Rule_engine_sources_avoid_forbidden_time_and_random_apis()
        {
            // Infrastructure is allowed to touch Stopwatch (SystemClock); Shared,
            // Domain, Application and Simulation are not. No layer may use
            // System.Random / UnityEngine.Random.
            var offenders = new List<string>();
            string[] ruleEngineLayers = { "Shared", "Domain", "Application", "Simulation" };

            foreach (string layer in ruleEngineLayers)
            {
                foreach (string file in LibSourceFiles("Runtime/" + layer))
                {
                    string code = StripComments(File.ReadAllText(file));
                    foreach (string api in ForbiddenRuleEngineApis)
                    {
                        if (code.Contains(api, StringComparison.Ordinal))
                        {
                            offenders.Add(RelativePath(file) + " uses " + api);
                        }
                    }
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Layer_asmdefs_match_the_documented_matrix()
        {
            var problems = new List<string>();

            foreach ((string layer, _, string assemblyName) in Layers)
            {
                string path = Path.Combine(RepoRoot, "lib", "Runtime", layer, assemblyName + ".asmdef");
                if (!File.Exists(path))
                {
                    problems.Add(layer + ": asmdef missing at " + RelativePath(path));
                    continue;
                }

                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = document.RootElement;

                string? name = root.TryGetProperty("name", out JsonElement nameElement)
                    ? nameElement.GetString()
                    : null;
                if (name != assemblyName)
                {
                    problems.Add(layer + ": asmdef name is '" + name + "', expected '" + assemblyName + "'");
                }

                bool noEngineReferences = root.TryGetProperty("noEngineReferences", out JsonElement engineElement)
                    && engineElement.GetBoolean();
                if (!noEngineReferences)
                {
                    problems.Add(layer + ": noEngineReferences must be true");
                }

                var actual = new List<string>();
                if (root.TryGetProperty("references", out JsonElement references))
                {
                    actual.AddRange(references.EnumerateArray().Select(item => item.GetString() ?? string.Empty));
                }

                string[] expected = ExpectedAsmdefReferences[layer];
                if (actual.Count != expected.Length || expected.Any(reference => !actual.Contains(reference)))
                {
                    problems.Add(
                        layer + ": references [" + string.Join(", ", actual) + "] != expected ["
                        + string.Join(", ", expected) + "]");
                }
            }

            Assert.Empty(problems);
        }

        [Fact]
        public void Layer_projects_match_the_documented_matrix()
        {
            var problems = new List<string>();

            foreach ((string layer, string projectFile, string assemblyName) in Layers)
            {
                string path = Path.Combine(RepoRoot, "lib", "Tests~", "Layers", projectFile);
                if (!File.Exists(path))
                {
                    problems.Add(assemblyName + ": " + RelativePath(path) + " missing");
                    continue;
                }

                XDocument document = XDocument.Load(path);

                string? compiled = document.Descendants("Compile")
                    .Select(element => (string?)element.Attribute("Include"))
                    .FirstOrDefault(value => value is not null);
                string expectedGlob = "$(LibRuntimeDir)" + layer + "/**/*.cs";
                if (compiled != expectedGlob)
                {
                    problems.Add(assemblyName + ": Compile Include is '" + compiled + "', expected '" + expectedGlob + "'");
                }

                var actualReferences = document.Descendants("ProjectReference")
                    .Select(element => (string?)element.Attribute("Include"))
                    .Where(value => value is not null)
                    .Select(value => value!.Replace('\\', '/'))
                    .ToList();

                string[] expectedReferences = ExpectedProjectReferences[assemblyName]
                    .Select(value => value.Replace('\\', '/'))
                    .ToArray();

                if (actualReferences.Count != expectedReferences.Length
                    || expectedReferences.Any(reference => !actualReferences.Contains(reference)))
                {
                    problems.Add(
                        assemblyName + ": ProjectReference [" + string.Join(", ", actualReferences)
                        + "] != expected [" + string.Join(", ", expectedReferences) + "]");
                }
            }

            Assert.Empty(problems);
        }

        [Fact]
        public void Runtime_layers_are_the_only_ones_that_need_engine_references()
        {
            // Every hand-written layer stays engine-free. The one deliberate
            // exception is the vendored EcsLite assembly, whose IL2CPP attribute
            // blocks need Unity assemblies in a real Web build (VENDOR.md).
            var unexpected = new List<string>();

            foreach (string asmdef in Directory.EnumerateFiles(
                         Path.Combine(RepoRoot, "lib"), "*.asmdef", SearchOption.AllDirectories))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdef));
                JsonElement root = document.RootElement;
                bool noEngineReferences = root.TryGetProperty("noEngineReferences", out JsonElement element)
                    && element.GetBoolean();

                bool isVendoredEcsLite = RelativePath(asmdef)
                    .Replace('\\', '/')
                    .Contains("lib/third_party/ecslite/", StringComparison.Ordinal);

                if (!noEngineReferences && !isVendoredEcsLite)
                {
                    unexpected.Add(RelativePath(asmdef));
                }

                if (noEngineReferences && isVendoredEcsLite)
                {
                    unexpected.Add(RelativePath(asmdef) + " (vendored EcsLite must keep upstream's setting)");
                }
            }

            Assert.Empty(unexpected);
        }

        [Fact]
        public void Vendored_ecslite_revision_is_pinned_and_recorded_in_the_lock_file()
        {
            string vendorDir = Path.Combine(RepoRoot, "lib", "third_party", "ecslite");
            string vendorDocPath = Path.Combine(vendorDir, "VENDOR.md");

            Assert.True(File.Exists(vendorDocPath), "lib/third_party/ecslite/VENDOR.md is missing");

            Match revision = Regex.Match(File.ReadAllText(vendorDocPath), "\\b[0-9a-f]{40}\\b");
            Assert.True(revision.Success, "VENDOR.md does not pin a 40-character revision");

            string lockFilePath = Path.Combine(RepoRoot, "tools", "versions.lock.json");
            Assert.Contains(revision.Value, File.ReadAllText(lockFilePath));

            string licensePath = Path.Combine(vendorDir, "LICENSE.md");
            Assert.True(File.Exists(licensePath), "the vendored license must be kept");
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(licensePath)));

            string[] expectedSources = { "components.cs", "entities.cs", "filters.cs", "systems.cs", "worlds.cs" };
            foreach (string source in expectedSources)
            {
                Assert.True(
                    File.Exists(Path.Combine(vendorDir, "src", source)),
                    "vendored source missing: src/" + source);
            }
        }

        private static string RepoRoot
        {
            get
            {
                DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
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

        private static IEnumerable<string> LibSourceFiles(params string[] relativeDirectories)
        {
            foreach (string relative in relativeDirectories)
            {
                string root = Path.Combine(RepoRoot, "lib", relative.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = file.Replace('\\', '/');
                    if (normalized.Contains("/bin/", StringComparison.Ordinal)
                        || normalized.Contains("/obj/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return file;
                }
            }
        }

        /// <summary>
        /// Removes comments so that documenting a forbidden API (which the code
        /// legitimately does) is not mistaken for using it.
        /// </summary>
        private static string StripComments(string source)
        {
            string withoutBlockComments = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(withoutBlockComments, @"//[^\n]*", string.Empty);
        }

        private static string RelativePath(string fullPath) =>
            Path.GetRelativePath(RepoRoot, fullPath).Replace('\\', '/');
    }
}
