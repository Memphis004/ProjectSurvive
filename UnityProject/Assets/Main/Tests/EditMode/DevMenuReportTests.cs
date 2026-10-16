using NUnit.Framework;
using ProjectSurvive.Game.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// The dev menu's page document: which controls it can name, and what a build that hides the menu
    /// publishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document exists for one job — letting a browser test press <b>แพ้ทันที / lose now</b>, which
    /// is the only way a browser run can reach the summary screen in reasonable time (Stage 10's report
    /// recorded that gap). A click that misses is a browser failure nobody can debug from a log, so the
    /// names and the controls they stand for are checked here, against the shipped Battle scene, without
    /// entering play mode.
    /// </para>
    /// <para>
    /// Nothing here activates the panel or presses anything: the scene's authored state is the state a
    /// release build has (<c>DevCheats.Allowed</c> is false there, so <c>DevMenuPresenter.Visible</c> is
    /// false and the panel is never shown), and the buttons are read through <c>SerializedObject</c> so a
    /// scene that lost one fails here rather than in a browser. The scene is opened additively and left
    /// untouched, so the Editor's own scene is never disturbed.
    /// </para>
    /// </remarks>
    public sealed class DevMenuReportTests
    {
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";

        /// <summary>Each control the report names, and the field that has to be wired for it to exist.</summary>
        private static readonly (string Name, string Field)[] Controls =
        {
            (DevMenuReport.Control.GodMode, "_godModeButton"),
            (DevMenuReport.Control.AddBia, "_addBiaButton"),
            (DevMenuReport.Control.SpawnGhost, "_spawnGhostButton"),
            (DevMenuReport.Control.KillAll, "_killAllButton"),
            (DevMenuReport.Control.WinNow, "_winNowButton"),
            (DevMenuReport.Control.LoseNow, "_loseNowButton"),
        };

        private Scene _scene;

        /// <summary>
        /// The report's mirror is process-wide, so it starts every test from empty: a PlayMode fixture
        /// that published a panel leaves a count behind, and "this run published once" is only a
        /// statement about this run when the counter started at zero.
        /// </summary>
        [SetUp]
        public void SetUp() => DevMenuReport.Reset();

        [TearDown]
        public void TearDown()
        {
            DevMenuReport.Reset();

            if (_scene.IsValid() && _scene.isLoaded)
            {
                EditorSceneManager.CloseScene(_scene, removeScene: true);
            }
        }

        /// <summary>
        /// Every control the report can name is wired in the shipped scene, and the panel starts
        /// <b>hidden</b> — which is the state a release build never leaves.
        /// </summary>
        [Test]
        public void Every_named_control_is_wired_and_the_panel_starts_hidden()
        {
            DevMenuView view = OpenBattleScene();

            Assert.That(view.PanelVisible, Is.False, "the shipped Battle scene must author the dev menu hidden");

            var serialized = new SerializedObject(view);
            foreach ((string name, string field) in Controls)
            {
                var button = serialized.FindProperty(field)?.objectReferenceValue as Component;
                Assert.That(button, Is.Not.Null, "the dev menu has no '" + name + "' control (" + field + ")");
            }
        }

        /// <summary>
        /// A hidden panel publishes a document that says so and names no control: there is nothing on
        /// screen to click, and a browser test that tried would be aiming at nothing.
        /// </summary>
        [Test]
        public void A_hidden_panel_publishes_no_controls()
        {
            DevMenuView view = OpenBattleScene();

            string json = DevMenuReport.Publish(view);

            Assert.That(DevMenuReport.PublishCount, Is.EqualTo(1));
            Assert.That(DevMenuReport.LastJson, Is.EqualTo(json));
            Assert.That(json, Does.Not.Contain("}{"), "array elements lost their comma: " + json);
            Assert.That(json, Does.Not.Contain(DevMenuReport.Control.LoseNow),
                "a hidden panel published a control nobody can press: " + json);

            var parsed = JsonUtility.FromJson<MenuReportDto>(json);
            Assert.That(parsed, Is.Not.Null, "the dev menu report is not valid JSON: " + json);
            Assert.That(parsed.scene, Is.EqualTo("Battle"));
            Assert.That(parsed.visible, Is.False);
            Assert.That(parsed.buttons, Is.Empty);
        }

        private DevMenuView OpenBattleScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (candidate.isLoaded && candidate.path == BattleScenePath)
                {
                    _scene = candidate;
                    break;
                }
            }

            if (!_scene.IsValid())
            {
                _scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
            }

            Assert.That(_scene.IsValid() && _scene.isLoaded, "the Battle scene could not be opened");

            DevMenuView view = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                view = root.GetComponentInChildren<DevMenuView>(includeInactive: true);
                if (view is not null)
                {
                    break;
                }
            }

            Assert.That(view, Is.Not.Null, "the Battle scene has no DevMenuView");
            return view;
        }

        [System.Serializable]
        private sealed class MenuReportDto
        {
            public string scene;
            public bool visible;
            public int canvasWidth;
            public int canvasHeight;
            public ControlDto[] buttons;
        }

        [System.Serializable]
        private sealed class ControlDto
        {
            public string name;
            public float x;
            public float y;
        }
    }
}
