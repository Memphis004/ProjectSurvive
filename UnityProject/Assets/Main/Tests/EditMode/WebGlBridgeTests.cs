using System.IO;
using NUnit.Framework;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 10t: the browser globals the shell publishes have <b>one</b> home on each
    /// side, and the two homes agree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The C# side is <see cref="WebGlBridge"/>'s four constants — the <c>.jslib</c>
    /// now takes the name as an argument instead of writing it into the plugin — and
    /// the browser-test side is <c>tests/web/globals.mjs</c>. A rename that touches
    /// one side and not the other is the failure this class exists to catch: it
    /// would leave a spec waiting for a global nothing publishes, and a page test
    /// that times out says nothing about the cause.
    /// </para>
    /// <para>
    /// These are file-level checks, not a build: they run in the Editor in
    /// milliseconds and need no WebGL player.
    /// </para>
    /// </remarks>
    public sealed class WebGlBridgeTests
    {
        /// <summary>The repository root, the same way <c>ProjectSetup.WriteBakeEvidence</c> finds it.</summary>
        private static string RepositoryRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test]
        public void The_page_globals_keep_their_published_names()
        {
            // The names a browser test reads are a wire contract: the published
            // reports have carried these exact strings since Stage 03, and every
            // spec and every document names them. Stage 12 added the persistence
            // verdict beside them.
            Assert.AreEqual("__projectSurviveSmoke", WebGlBridge.SmokeReportGlobal);
            Assert.AreEqual("__projectSurviveScene", WebGlBridge.SceneGlobal);
            Assert.AreEqual("__projectSurviveThaiSpike", WebGlBridge.ThaiSpikeGlobal);
            Assert.AreEqual("__projectSurvivePersistence", WebGlBridge.PersistenceGlobal);
            Assert.AreEqual("__projectSurviveAudio", WebGlBridge.AudioGlobal);
        }

        [Test]
        public void The_webgl_plugin_hard_codes_no_page_global()
        {
            string plugin = Path.Combine(Application.dataPath, "Main", "Plugins", "WebGL", "ProjectSurviveWeb.jslib");
            Assert.IsTrue(File.Exists(plugin), "the WebGL bridge plugin is missing: " + plugin);

            // The plugin receives the name from WebGlBridge. A literal here would be a
            // second source of truth that a C# rename could not reach.
            StringAssert.DoesNotContain("__projectSurvive", File.ReadAllText(plugin));
        }

        [Test]
        public void The_browser_suite_exports_the_same_names()
        {
            string globals = Path.Combine(RepositoryRoot, "tests", "web", "globals.mjs");
            Assert.IsTrue(File.Exists(globals), "the browser suite's globals file is missing: " + globals);

            string text = File.ReadAllText(globals);
            StringAssert.Contains("export const SMOKE = \"" + WebGlBridge.SmokeReportGlobal + "\";", text);
            StringAssert.Contains("export const SCENE = \"" + WebGlBridge.SceneGlobal + "\";", text);
            StringAssert.Contains("export const THAI_SPIKE = \"" + WebGlBridge.ThaiSpikeGlobal + "\";", text);
            StringAssert.Contains("export const PERSISTENCE = \"" + WebGlBridge.PersistenceGlobal + "\";", text);
            StringAssert.Contains("export const AUDIO = \"" + WebGlBridge.AudioGlobal + "\";", text);
        }
    }
}
