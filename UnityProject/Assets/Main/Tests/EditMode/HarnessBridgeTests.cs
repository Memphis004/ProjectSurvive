using System.IO;
using NUnit.Framework;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 14: the development harness is a wire contract between the player and a
    /// browser test, and it must not exist in a release build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three facts are pinned here, none of which needs a WebGL player:
    /// the four page names and the four opcodes (their single other home is
    /// <c>tests/web/globals.mjs</c>), the rule that the page can only send a command the
    /// document advertises, and the compile gate that keeps the whole harness — and the one
    /// line in <c>SceneBoot</c> that installs it — out of a release player.
    /// </para>
    /// <para>
    /// The third test reads source files rather than a build on purpose. A release build takes
    /// minutes and is run by <c>tools/ci.ps1</c>; what can go wrong between stages is that
    /// someone removes the <c>#if</c> and nobody notices for a month. That is a text fact, and
    /// it is checked in milliseconds.
    /// </para>
    /// </remarks>
    public sealed class HarnessBridgeTests
    {
        private static string RepositoryRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test]
        public void The_harness_names_keep_their_published_values()
        {
            // A browser test reads these exact strings; a rename on either side alone leaves a
            // spec waiting for a global nothing publishes.
            Assert.AreEqual("__projectSurviveHarness", HarnessProtocol.Global);
            Assert.AreEqual("__projectSurviveHarnessSend", HarnessProtocol.SendGlobal);
            Assert.AreEqual("__projectSurviveHarnessSnapshot", HarnessProtocol.SnapshotGlobal);
            Assert.AreEqual("__projectSurviveHarnessDiagnostics", HarnessProtocol.DiagnosticsGlobal);
        }

        [Test]
        public void Every_advertised_command_has_one_opcode_and_back()
        {
            Assert.AreEqual(4, HarnessProtocol.Commands.Length);

            foreach (string command in HarnessProtocol.Commands)
            {
                int opcode = HarnessProtocol.OpcodeOf(command);
                Assert.AreNotEqual(HarnessProtocol.None, opcode, command + " has no opcode");
                Assert.IsTrue(HarnessProtocol.IsKnown(opcode), command + " is not known by its opcode");
                Assert.AreEqual(command, HarnessProtocol.NameOf(opcode));
            }

            // The page cannot reach a command the document did not list, and an unlisted opcode
            // is answered with harness.unknown_command rather than ignored.
            Assert.AreEqual(HarnessProtocol.None, HarnessProtocol.OpcodeOf("finishFloor"));
            Assert.IsFalse(HarnessProtocol.IsKnown(99));
            Assert.AreEqual(string.Empty, HarnessProtocol.NameOf(99));
        }

        [Test]
        public void The_opcodes_are_the_numbers_the_browser_suite_sends()
        {
            // These numbers are on the wire: the send function takes (opcode, argument), so a
            // renumbering has to be made in both files at once.
            Assert.AreEqual(1, HarnessProtocol.StartSeededRun);
            Assert.AreEqual(2, HarnessProtocol.AdvanceTicks);
            Assert.AreEqual(3, HarnessProtocol.Snapshot);
            Assert.AreEqual(4, HarnessProtocol.Diagnostics);
        }

        [Test]
        public void A_zero_argument_keeps_the_configured_seed()
        {
            Assert.IsFalse(HarnessProtocol.TryReadSeed(0, out uint none));
            Assert.AreEqual(0u, none);

            Assert.IsTrue(HarnessProtocol.TryReadSeed(20261015, out uint named));
            Assert.AreEqual(20261015u, named);

            // The queue carries int32, so a seed above int.MaxValue travels as its bit pattern.
            Assert.IsTrue(HarnessProtocol.TryReadSeed(unchecked((int)4000000000u), out uint wide));
            Assert.AreEqual(4000000000u, wide);
        }

        [Test]
        public void The_browser_suite_exports_the_same_harness_names_and_opcodes()
        {
            string globals = Path.Combine(RepositoryRoot, "tests", "web", "globals.mjs");
            Assert.IsTrue(File.Exists(globals), "the browser suite's globals file is missing: " + globals);

            string text = File.ReadAllText(globals);
            StringAssert.Contains("export const HARNESS = \"" + HarnessProtocol.Global + "\";", text);
            StringAssert.Contains("export const HARNESS_SEND = \"" + HarnessProtocol.SendGlobal + "\";", text);
            StringAssert.Contains("export const HARNESS_SNAPSHOT = \"" + HarnessProtocol.SnapshotGlobal + "\";", text);
            StringAssert.Contains("export const HARNESS_DIAGNOSTICS = \"" + HarnessProtocol.DiagnosticsGlobal + "\";", text);

            foreach (string command in HarnessProtocol.Commands)
            {
                StringAssert.Contains(
                    command + ": " + HarnessProtocol.OpcodeOf(command),
                    text,
                    "tests/web/globals.mjs does not send " + command + " as its opcode");
            }
        }

        [Test]
        public void The_harness_is_compiled_out_of_a_release_player()
        {
            // The gate is a build condition, not a runtime check: a release player contains no
            // harness type, so there is no command a page could call even by guessing the name.
            string harness = Path.Combine(
                Application.dataPath, "Main", "Scripts", "Presentation", "Web", "HarnessBridge.cs");
            Assert.IsTrue(File.Exists(harness), "the harness bridge is missing: " + harness);

            string text = File.ReadAllText(harness);
            StringAssert.StartsWith("#if DEVELOPMENT_BUILD || UNITY_EDITOR", text.TrimStart());

            // The one line that installs it carries the same condition.
            string boot = Path.Combine(Application.dataPath, "Main", "Scripts", "Presentation", "SceneBoot.cs");
            string bootText = File.ReadAllText(boot);
            int gate = bootText.IndexOf("#if DEVELOPMENT_BUILD || UNITY_EDITOR", System.StringComparison.Ordinal);
            int install = bootText.IndexOf("HarnessBridge.Install", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(gate, 0, "SceneBoot no longer gates the harness install");
            Assert.Greater(install, gate, "the harness install moved above its compile gate");
        }
    }
}
