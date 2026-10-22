using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ProjectSurvive.Core.Domain.Config;
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
                    // Stage 05 added Contracts: the save DTOs are reached "through an
                    // adapter in Infrastructure only" (GDD), and the backpack snapshot
                    // codec is that adapter. Domain still sees no serializer.
                    "Infrastructure",
                    new[]
                    {
                        "ProjectSurvive.Core.Shared", "ProjectSurvive.Core.Domain", "ProjectSurvive.Core.Application",
                        "ProjectSurvive.Core.Contracts", "ProjectSurvive.Core.Generated.Luban", "Luban.Runtime",
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
                        "ProjectSurvive.Core.Application.csproj", "ProjectSurvive.Core.Contracts.csproj",
                        "ProjectSurvive.Core.Generated.Luban.csproj",
                        "Luban.Runtime.csproj",
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

        private static string RepoRoot => RepoLayout.Root;

        // -------------------------------------------------------------------
        // Stage 02: the generated data layers
        // -------------------------------------------------------------------

        /// <summary>
        /// The generated assemblies of AGENTS rule 4. Each is compiled from a
        /// machine-written folder, referenced only by Infrastructure, and must
        /// carry a do-not-edit banner.
        /// </summary>
        private static readonly (string Folder, string Asmdef, string Project, string Glob)[] GeneratedLayers =
        {
            (
                "Luban",
                "ProjectSurvive.Core.Generated.Luban.asmdef",
                "ProjectSurvive.Core.Generated.Luban.csproj",
                "$(LibRuntimeDir)Generated/Luban/**/*.cs"
            ),
            (
                "Proto",
                "ProjectSurvive.Core.Generated.Proto.asmdef",
                "ProjectSurvive.Core.Generated.Proto.csproj",
                "$(LibRuntimeDir)Generated/Proto/**/*.cs"
            ),
        };

        [Fact]
        public void Generated_layers_match_the_documented_matrix()
        {
            var problems = new List<string>();

            foreach ((string folder, string asmdef, string project, string glob) in GeneratedLayers)
            {
                string asmdefPath = Path.Combine(RepoRoot, "lib", "Runtime", "Generated", folder, asmdef);
                if (!File.Exists(asmdefPath))
                {
                    problems.Add(folder + ": asmdef missing at " + RelativePath(asmdefPath));
                }
                else
                {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(asmdefPath));
                    JsonElement root = document.RootElement;

                    string? name = root.GetProperty("name").GetString();
                    string expectedName = Path.GetFileNameWithoutExtension(asmdef);
                    if (name != expectedName)
                    {
                        problems.Add(folder + ": asmdef name is '" + name + "', expected '" + expectedName + "'");
                    }

                    if (root.GetProperty("autoReferenced").GetBoolean())
                    {
                        problems.Add(folder + ": autoReferenced must be false, or every assembly sees config types");
                    }

                    if (!root.GetProperty("noEngineReferences").GetBoolean())
                    {
                        problems.Add(folder + ": noEngineReferences must be true");
                    }

                    if (!root.TryGetProperty("overrideReferences", out _))
                    {
                        problems.Add(folder + ": overrideReferences must be declared explicitly");
                    }
                }

                string projectPath = Path.Combine(RepoRoot, "lib", "Tests~", "Layers", project);
                if (!File.Exists(projectPath))
                {
                    problems.Add(folder + ": " + RelativePath(projectPath) + " missing");
                }
                else
                {
                    XDocument document = XDocument.Load(projectPath);
                    string? compiled = document.Descendants("Compile")
                        .Select(element => (string?)element.Attribute("Include"))
                        .FirstOrDefault(value => value is not null);
                    if (compiled != glob)
                    {
                        problems.Add(
                            project + ": Compile Include is '" + compiled + "', expected '" + glob
                            + "' (generated code must not leak into another assembly)");
                    }
                }

                if (!File.ReadAllText(Path.Combine(RepoRoot, "lib", "Tests~", "ProjectSurvive.Core.sln"))
                        .Contains(project, StringComparison.Ordinal))
                {
                    problems.Add(project + " is not in lib/Tests~/ProjectSurvive.Core.sln");
                }
            }

            Assert.Empty(problems);
        }

        [Fact]
        public void Only_infrastructure_references_the_generated_config_assembly()
        {
            // The whole point of the IGameConfig port (ADR 0006): the rule engine
            // must not be able to call a generated row type even by accident.
            var offenders = new List<string>();
            string[] generated = { "ProjectSurvive.Core.Generated.Luban", "Luban.Runtime" };
            string[] ruleLayers =
            {
                "ProjectSurvive.Core.Shared", "ProjectSurvive.Core.Domain",
                "ProjectSurvive.Core.Application", "ProjectSurvive.Core.Simulation",
                "ProjectSurvive.Core.Contracts",
            };

            foreach (string layer in ruleLayers)
            {
                Assembly assembly = Assembly.Load(new AssemblyName(layer));
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    if (reference.Name is not null && generated.Contains(reference.Name, StringComparer.Ordinal))
                    {
                        offenders.Add(layer + " references " + reference.Name);
                    }
                }
            }

            string infrastructureAsmdef = Path.Combine(
                RepoRoot, "lib", "Runtime", "Infrastructure",
                "ProjectSurvive.Core.Infrastructure.asmdef");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(infrastructureAsmdef));
            var references = new List<string>();
            if (document.RootElement.TryGetProperty("references", out JsonElement array))
            {
                references.AddRange(array.EnumerateArray().Select(item => item.GetString() ?? string.Empty));
            }

            foreach (string required in generated)
            {
                if (!references.Contains(required, StringComparer.Ordinal))
                {
                    offenders.Add("Infrastructure asmdef does not reference " + required);
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Generated_sources_carry_a_do_not_edit_banner()
        {
            // AGENTS rule 4: lib/Runtime/Generated/** is machine output. A file
            // without the banner is either hand-written (then it belongs
            // elsewhere) or was copied in with the header stripped.
            var offenders = new List<string>();
            string generatedRoot = Path.Combine(RepoRoot, "lib", "Runtime", "Generated");

            if (!Directory.Exists(generatedRoot))
            {
                offenders.Add("lib/Runtime/Generated is missing - run tools/gen.ps1");
            }
            else
            {
                foreach (string file in Directory.EnumerateFiles(generatedRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string head = File.ReadAllText(file);
                    if (!head.Contains("<auto-generated>", StringComparison.Ordinal))
                    {
                        offenders.Add(RelativePath(file) + " has no <auto-generated> banner");
                    }

                    if (head.Contains("namespace ProjectSurvive.Core.Infrastructure", StringComparison.Ordinal))
                    {
                        offenders.Add(RelativePath(file) + " mixes hand-written code into the generated folder");
                    }
                }
            }

            Assert.Empty(offenders);
        }

        [Fact]
        public void Generated_protobuf_contract_appears_exactly_once()
        {
            var copies = Directory.EnumerateFiles(RepoRoot, "App.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains("/obj/", StringComparison.Ordinal)
                    && !path.Contains("\\obj\\", StringComparison.Ordinal)
                    && !path.Contains("/bin/", StringComparison.Ordinal)
                    && !path.Contains("\\bin\\", StringComparison.Ordinal))
                .Select(RelativePath)
                .ToList();

            Assert.Equal(new[] { "lib/Runtime/Generated/Proto/App.cs" }, copies);
        }

        // -------------------------------------------------------------------
        // Battle contract (Stage 02b)
        // -------------------------------------------------------------------

        /// <summary>
        /// AGENTS rule 3 as applied to the battle contract: the interfaces in
        /// <c>ProjectSurvive.Core.Domain.Combat.Battle</c> are the seam both
        /// Application and Simulation implement against, so they must stay free of
        /// any engine or ECS type. Application can use them, Simulation can
        /// implement them, and neither drags its own dependencies into Domain.
        /// </summary>
        [Fact]
        public void Battle_contract_types_reference_no_engine_or_ecs_types()
        {
            var forbiddenNamespaces = new[] { "UnityEngine", "UnityEditor", "Leopotam", "R3" };
            var offenders = new List<string>();

            Assembly domain = Assembly.Load(new AssemblyName("ProjectSurvive.Core.Domain"));
            const string contractNamespace = "ProjectSurvive.Core.Domain.Combat.Battle";

            foreach (Type type in domain.GetTypes())
            {
                if (!type.IsPublic && !type.IsNestedPublic)
                {
                    continue;
                }

                if (type.Namespace is null || !type.Namespace.StartsWith(contractNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                string owner = type.FullName ?? type.Name;

                CollectForbiddenReferences(type.BaseType, owner, forbiddenNamespaces, offenders);
                foreach (Type implemented in type.GetInterfaces())
                {
                    CollectForbiddenReferences(implemented, owner, forbiddenNamespaces, offenders);
                }

                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    CollectForbiddenReferences(field.FieldType, owner, forbiddenNamespaces, offenders);
                }

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    CollectForbiddenReferences(property.PropertyType, owner, forbiddenNamespaces, offenders);
                }

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    CollectForbiddenReferences(method.ReturnType, owner, forbiddenNamespaces, offenders);
                    foreach (ParameterInfo parameter in method.GetParameters())
                    {
                        CollectForbiddenReferences(parameter.ParameterType, owner, forbiddenNamespaces, offenders);
                    }
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// ADR 0012: exactly one <c>IBattleFrame</c> implementation exists. The fake
        /// (Application) and the EcsLite simulation (Simulation, Stage 07) both
        /// publish through <c>BattleFrameBuffer</c>, so a Presentation validated
        /// against the fake cannot meet different frame behaviour from the real
        /// battle. A second implementation fails here instead of in playtest.
        /// </summary>
        [Fact]
        public void Battle_frame_has_exactly_one_implementation()
        {
            Assembly domain = Assembly.Load(new AssemblyName("ProjectSurvive.Core.Domain"));
            Type frame = domain.GetType(
                "ProjectSurvive.Core.Domain.Combat.Battle.IBattleFrame", throwOnError: true)!;

            var implementations = new List<string>();
            foreach (string name in LibAssemblies)
            {
                Assembly assembly = Assembly.Load(new AssemblyName(name));
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface || !frame.IsAssignableFrom(type))
                    {
                        continue;
                    }

                    implementations.Add(type.FullName ?? type.Name);
                }
            }

            Assert.Equal(
                new[] { "ProjectSurvive.Core.Domain.Combat.Battle.BattleFrameBuffer" }, implementations);
        }

        // -------------------------------------------------------------------
        // Run, shop and scene flow (Stage 06)
        // -------------------------------------------------------------------

        /// <summary>
        /// AGENTS rule 3 as applied to the battle seam: the run and the scene flow reach
        /// a battle through <c>IBattleSessionFactory</c> and never name an implementation.
        /// </summary>
        /// <remarks>
        /// The fake is a test/dev session, so the only code that may mention it is the
        /// <c>Application/Battle/Fake</c> folder that defines it (and the composition root
        /// that binds the port). This is the mechanical half of the acceptance line
        /// "เข้าถึง Battle ผ่าน IBattleSessionFactory เท่านั้น": Stage 07 swaps in the EcsLite
        /// session by changing a binding, and this test is what keeps a call site from
        /// hard-wiring a session instead.
        /// </remarks>
        [Fact]
        public void Battle_sessions_are_reached_only_through_their_factory_port()
        {
            const string fakeNamespace = "Application/Battle/Fake/";
            var offenders = new List<string>();

            foreach (string file in LibSourceFiles("Runtime"))
            {
                string relative = RelativePath(file);
                if (relative.Contains(fakeNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                if (StripComments(File.ReadAllText(file)).Contains("FakeBattleSession", StringComparison.Ordinal))
                {
                    offenders.Add(relative + " names a battle session implementation");
                }
            }

            foreach (string file in UnitySourceFiles("Scripts"))
            {
                if (StripComments(File.ReadAllText(file)).Contains("FakeBattleSession", StringComparison.Ordinal))
                {
                    offenders.Add(RelativePath(file) + " names a battle session implementation");
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// Stage 07 acceptance: "type ของ EcsLite ห้ามหลุดออกนอก lib/Runtime/Simulation".
        /// </summary>
        /// <remarks>
        /// The ECS is an implementation detail of the battle, not a vocabulary the rest of
        /// the project learns. Application, Domain and the engine-facing Unity scripts reach
        /// a battle through <c>IBattleSession</c> / <c>IBattleSessionFactory</c> and read a
        /// frame, so a component, a world or a system type appearing outside the simulation
        /// folder means a caller is driving the ECS directly — and the day the battle is
        /// rebuilt, that call site has to be found by hand.
        /// <para>
        /// The concrete simulation and its factory are checked on the Core side only: a
        /// composition root (Unity, later stage) is allowed to bind
        /// <c>EcsBattleSessionFactory</c> to the port, exactly as it binds the fake today,
        /// while the world types stay unreachable from anything but the simulation.
        /// </para>
        /// </remarks>
        [Fact]
        public void Ecs_types_do_not_leak_outside_the_simulation_layer()
        {
            string[] ecsTokens =
            {
                "Leopotam", "EcsWorld", "EcsPool", "EcsFilter", "EcsSystems", "IEcsRunSystem", "IEcsInitSystem",
            };

            string[] simulationTypes = { "EcsBattleSimulation", "EcsBattleSessionFactory" };
            var offenders = new List<string>();

            foreach (string file in LibSourceFiles("Runtime"))
            {
                string relative = RelativePath(file);
                if (relative.Contains("Runtime/Simulation/", StringComparison.Ordinal))
                {
                    continue;
                }

                string code = StripComments(File.ReadAllText(file));
                foreach (string token in ecsTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add(relative + " names the ECS type " + token);
                    }
                }

                foreach (string token in simulationTypes)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add(relative + " names the session implementation " + token);
                    }
                }
            }

            foreach (string file in UnitySourceFiles("Scripts"))
            {
                string code = StripComments(File.ReadAllText(file));
                foreach (string token in ecsTokens)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add(RelativePath(file) + " names the ECS type " + token);
                    }
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// Stage 06 acceptance "ไม่มี economy logic ใน Presenter" (AGENTS rule 10): a
        /// Presenter renders a number it was given, and the run (Application) is what
        /// decides one.
        /// </summary>
        /// <remarks>
        /// The forbidden tokens are the <i>decisions</i>, not the state: reading
        /// <c>RunSession</c>, <c>ShopStock</c> or <c>Wallet.Bia</c> to fill a label stays
        /// allowed, while rolling the stock, resolving a price, moving bia, recomputing a
        /// combo or a loadout, or re-deriving the heal and the payout percentage all mean
        /// the View owns a rule it does not own. Comments are stripped, so a doc comment
        /// may explain the rule it is forbidden to implement.
        /// </remarks>
        [Fact]
        public void Presentation_sources_do_not_decide_economy_numbers()
        {
            string[] forbidden =
            {
                "ShopRoller",
                "SynergyEvaluator",
                "LoadoutResolver",
                "TrySpend(",
                "RefreshCost(",
                "SellBackPercent",
                "HealPercentOnClear",
            };

            var offenders = new List<string>();

            foreach (string file in UnitySourceFiles("Scripts"))
            {
                if (!RelativePath(file).Contains("/Presentation/", StringComparison.Ordinal))
                {
                    continue;
                }

                string code = StripComments(File.ReadAllText(file));
                foreach (string token in forbidden)
                {
                    if (code.Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add(RelativePath(file) + " decides an economy number with " + token);
                    }
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// Stage 13 acceptance: "ไม่มีข้อความผู้เล่นฝังในโค้ด" — every string a player reads
        /// is a localization row, read by key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The runtime Unity scripts (not the Editor tooling, not the tests) are scanned for
        /// string literals that carry Thai text, and for the places a label is handed a literal
        /// directly (a <c>.text = "…"</c> assignment, a <c>format:</c> argument, an initial
        /// <c>ReactiveProperty</c> value). Either shape means a phrase exists only in code, where
        /// the Thai/English switch cannot reach it and no one can proofread it beside its siblings.
        /// </para>
        /// <para>
        /// The dev menu is the one deliberate exception and its presenter documents why: it is
        /// developer diagnostics in a panel that does not exist in a release build. Its file is
        /// named here rather than the labels being moved into the player's table.
        /// </para>
        /// </remarks>
        [Fact]
        public void Runtime_sources_embed_no_player_facing_text()
        {
            const string devMenu = "Presentation/Battle/DevMenuPresenter.cs";
            var offenders = new List<string>();

            foreach (string file in UnityRuntimeSources().Concat(LibSourceFiles("Runtime")))
            {
                string relative = RelativePath(file);
                if (relative.EndsWith(devMenu, StringComparison.Ordinal))
                {
                    continue;
                }

                string code = StripComments(File.ReadAllText(file));

                // Inspector attributes are developer-facing (the Editor's tooltips and headers),
                // not text the player reads; their Thai is allowed and is what makes the
                // component pair/documentable in the Inspector.
                code = Regex.Replace(
                    code, "\\[(?:Tooltip|Header)\\s*\\(\\s*\"[^\"]*\"\\s*\\)\\]", string.Empty);

                foreach (string literal in StringLiterals(code))
                {
                    if (ContainsThai(literal))
                    {
                        offenders.Add(relative + " embeds Thai text: \"" + literal + "\"");
                        break;
                    }
                }

                if (Regex.IsMatch(code, "\\.text\\s*=\\s*\""))
                {
                    offenders.Add(relative + " assigns a literal to a label");
                }

                if (Regex.IsMatch(code, "\\bformat\\s*:\\s*\""))
                {
                    offenders.Add(relative + " passes a literal format to a label");
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// Every localization key a runtime script asks for is a row in the shipped table.
        /// </summary>
        /// <remarks>
        /// A typo or a deleted row would otherwise print the presentation's missing-key fallback
        /// in a corner of one screen and nowhere else. Keys built by concatenation (`error.` + a
        /// code) cannot be seen by this test; the data validator's cross-table rule covers the
        /// item/enemy/floor name keys, and this covers the hand-written UI keys.
        /// </remarks>
        [Fact]
        public void Every_localization_key_the_runtime_asks_for_exists()
        {
            IGameConfig config = ConfigFixture.Load();
            var keys = new SortedSet<string>(StringComparer.Ordinal);
            var offenders = new List<string>();

            foreach (string file in UnityRuntimeSources())
            {
                string code = StripComments(File.ReadAllText(file));

                foreach (Match match in Regex.Matches(code, "(?:Lookup|Format|\\.Get)\\(\\s*\"([a-z][a-z0-9_.]*)\""))
                {
                    keys.Add(match.Groups[1].Value);
                }
            }

            Assert.NotEmpty(keys);

            foreach (string key in keys)
            {
                if (key.EndsWith(".", StringComparison.Ordinal))
                {
                    // A prefix the code completes (`"error." + code`), not a key of its own.
                    continue;
                }

                if (!config.TryGetLocalization(key, out _))
                {
                    offenders.Add("no localization row for " + key);
                }
            }

            Assert.Empty(offenders);
        }

        /// <summary>
        /// The hand-written runtime Unity sources: the scenes' own code, not the Editor tooling
        /// and not the tests (which legitimately speak Thai in their assertion messages).
        /// </summary>
        private static IEnumerable<string> UnityRuntimeSources()
        {
            foreach (string file in UnitySourceFiles("Scripts"))
            {
                string relative = RelativePath(file);
                if (relative.Contains("/Editor/", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }

        private static IEnumerable<string> StringLiterals(string code)
        {
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] != '"')
                {
                    continue;
                }

                var builder = new System.Text.StringBuilder();
                int cursor = i + 1;
                while (cursor < code.Length && code[cursor] != '"')
                {
                    if (code[cursor] == '\\' && cursor + 1 < code.Length)
                    {
                        builder.Append(code[cursor + 1]);
                        cursor += 2;
                        continue;
                    }

                    builder.Append(code[cursor]);
                    cursor++;
                }

                yield return builder.ToString();
                i = cursor;
            }
        }

        private static bool ContainsThai(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '\u0E00' && text[i] <= '\u0E7F')
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectForbiddenReferences(
            Type? type, string owner, string[] forbiddenNamespaces, List<string> offenders)
        {
            if (type is null)
            {
                return;
            }

            if (type.HasElementType)
            {
                CollectForbiddenReferences(type.GetElementType(), owner, forbiddenNamespaces, offenders);
                return;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                CollectForbiddenReferences(type.GetGenericTypeDefinition(), owner, forbiddenNamespaces, offenders);
                foreach (Type argument in type.GetGenericArguments())
                {
                    CollectForbiddenReferences(argument, owner, forbiddenNamespaces, offenders);
                }
            }

            string? nameSpace = type.Namespace;
            if (nameSpace is null)
            {
                return;
            }

            foreach (string forbidden in forbiddenNamespaces)
            {
                if (nameSpace == forbidden || nameSpace.StartsWith(forbidden + ".", StringComparison.Ordinal))
                {
                    offenders.Add(owner + " references " + type.FullName);
                    return;
                }
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
        /// The hand-written Unity sources under <c>UnityProject/Assets/Main</c>. The
        /// engine-facing half of the project is not compiled by this harness, so a rule
        /// about it can only be checked at the source level.
        /// </summary>
        private static IEnumerable<string> UnitySourceFiles(params string[] relativeDirectories)
        {
            foreach (string relative in relativeDirectories)
            {
                string root = Path.Combine(
                    RepoRoot, "UnityProject", "Assets", "Main", relative.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                {
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
