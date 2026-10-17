using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Thai;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds the Thai text spike scene: the same three strings on the uGUI+TMP stack and the
    /// UI Toolkit stack, at the same nominal size, so the two can be compared.
    /// </summary>
    /// <remarks>
    /// One file per scene (Stage 10t): the construction and the menu item that builds it live
    /// together, so the scene can be dropped by deleting this file. The helpers it uses are
    /// <see cref="ProjectSetup"/>'s.
    /// </remarks>
    public static class ThaiTextSpikeSceneBuilder
    {
        /// <summary>
        /// Nominal size the Thai spike measures both stacks at. The two engines count
        /// size differently at the margins, so the comparison in
        /// <c>ThaiSpikeReport.StacksMeasureEqually</c> is relative (1 %), not exact.
        /// </summary>
        private const int SpikeTmpFontSize = 48;

        [MenuItem("ProjectSurvive/Stage 03/4. Create ThaiTextSpike scene", priority = 103)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: ThaiTextSpike scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RootLifetimeScope scope = ProjectSetup.CreateScopeObject();
            ProjectSetup.CreateCamera();
            ProjectSetup.CreateEventSystem();

            (TMP_FontAsset tmpFont, FontAsset textCoreFont) = ProjectSetup.EnsureFontAssets();
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(ProjectSetup.PanelSettingsPath);

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            TMP_Text tmpLabel = ProjectSetup.CreateTmpLabel(
                canvas, "TmpSpikeLabel", new Vector2(0f, 200f), SpikeTmpFontSize, tmpFont);

            var tmpViewObject = new GameObject("TmpSpike");
            tmpViewObject.transform.SetParent(canvasObject.transform, false);
            ThaiSpikeTmpView tmpView = tmpViewObject.AddComponent<ThaiSpikeTmpView>();
            ProjectSetup.SetObjectReference(tmpView, "_label", tmpLabel);
            ProjectSetup.SetObjectReference(tmpView, "_font", tmpFont);

            var toolkitObject = new GameObject("ToolkitSpike");
            UIDocument document = toolkitObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            ThaiSpikeToolkitView toolkitView = toolkitObject.AddComponent<ThaiSpikeToolkitView>();
            ProjectSetup.SetObjectReference(toolkitView, "_document", document);
            ProjectSetup.SetObjectReference(toolkitView, "_fontAsset", textCoreFont);

            // Both stacks are given the same nominal size, because they are being
            // compared: measured Stage 03 at unequal sizes (48 vs 32) the two stacks
            // disagreed on every string, and the disagreement was the configuration,
            // not the text engine.
            ProjectSetup.SetInt(toolkitView, "_fontSize", SpikeTmpFontSize);

            ThaiSpikeReporter reporter = canvasObject.AddComponent<ThaiSpikeReporter>();
            ProjectSetup.SetObjectReference(reporter, "_scope", scope);
            ProjectSetup.SetObjectReference(reporter, "_tmpView", tmpView);
            ProjectSetup.SetObjectReference(reporter, "_toolkitView", toolkitView);
            ProjectSetup.SetString(reporter, "_fontName", "Sarabun-Regular.ttf");

            EditorSceneManager.SaveScene(scene, ProjectSetup.SpikeScenePath);
        }
    }
}
