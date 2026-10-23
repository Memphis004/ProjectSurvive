using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 15: the release candidate is a High-stripping IL2CPP build, and the
    /// linker's allow-list is part of the source it is built from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Managed stripping is the setting that can remove a type nothing statically
    /// references but the runtime looks up by name. The five packages named here are
    /// exactly the ones whose reflection paths the game reaches (see the comments in
    /// <c>Assets/link.xml</c>), so the file must exist, must name all five, and must
    /// be inside the assets the build packs.
    /// </para>
    /// <para>
    /// This is a guard, not the proof: the proof is the *Release* browser suite -
    /// boot's seven probes plus the persistence spec's real save and reload, which is
    /// the path that deserialises a checkpoint. This test only makes sure the guard
    /// cannot be deleted in a refactor that leaves the browser gate unwatched.
    /// </para>
    /// </remarks>
    public sealed class ReleaseLinkXmlTests
    {
        private const string LinkXmlPath = "Assets/link.xml";

        private static readonly string[] ReflectionDependentAssemblies =
        {
            "Luban.Runtime",
            "MessagePack",
            "MessagePack.Annotations",
            "Google.Protobuf",
            "VContainer",
            "R3",
        };

        [Test]
        public void The_release_link_xml_exists_and_names_every_reflection_dependent_assembly()
        {
            Assert.IsTrue(File.Exists(LinkXmlPath), "Assets/link.xml is missing from the project");

            string xml = File.ReadAllText(LinkXmlPath);
            var missing = new List<string>();
            foreach (string assembly in ReflectionDependentAssemblies)
            {
                if (!xml.Contains("fullname=\"" + assembly + "\""))
                {
                    missing.Add(assembly);
                }
            }

            Assert.IsEmpty(missing, "link.xml does not preserve: " + string.Join(", ", missing));
        }

        [Test]
        public void Every_preserved_assembly_is_one_the_game_actually_loads()
        {
            // A typo in `fullname` is silent - Unity accepts the file and preserves
            // nothing. The assembly names are checked against the compiled assemblies,
            // which is the only list that cannot drift from the build.
            var known = new HashSet<string>();
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                known.Add(assembly.GetName().Name);
            }

            foreach (string name in ReflectionDependentAssemblies)
            {
                Assert.IsTrue(known.Contains(name), $"link.xml preserves '{name}', which is not loaded in the Editor");
            }
        }
    }
}
