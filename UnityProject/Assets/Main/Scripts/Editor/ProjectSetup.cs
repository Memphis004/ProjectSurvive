using System;
using System.Globalization;
using System.IO;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Thai;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.UI;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;
using Font = UnityEngine.Font;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Creates everything the shell needs that only the Editor can create: the URP
    /// assets, the WebGL player settings, the runtime UI theme and font assets, and
    /// the three scenes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scenes, prefabs and pipeline assets are never hand-written as YAML (AGENTS
    /// rule 11). They are built here through the Editor API, which means the whole
    /// project state is reproducible from <c>ProjectSurvive/Stage 03/...</c> menu
    /// items (or from a batch-mode <c>-executeMethod</c>) instead of being a
    /// one-off click nobody can repeat.
    /// </para>
    /// <para>
    /// Both entry points are idempotent: they reuse whatever already exists and
    /// only create what is missing, so running them again after a partial failure
    /// repairs the project instead of duplicating assets.
    /// </para>
    /// </remarks>
    public static class ProjectSetup
    {
        public const string SettingsFolder = "Assets/Main/Settings";
        public const string UiFolder = "Assets/Main/UI";
        public const string FontsFolder = "Assets/Main/Fonts";
        public const string ScenesFolder = "Assets/Main/Scenes";

        public const string UrpAssetPath = SettingsFolder + "/ProjectSurviveURP.asset";
        public const string UrpRendererPath = SettingsFolder + "/ProjectSurviveURP_Renderer.asset";
        public const string ThemePath = UiFolder + "/UnityDefaultRuntimeTheme.tss";
        public const string PanelSettingsPath = UiFolder + "/SpikePanelSettings.asset";
        public const string TmpFontPath = FontsFolder + "/Sarabun-Regular SDF.asset";
        public const string TextCoreFontPath = FontsFolder + "/Sarabun-Regular FontAsset.asset";

        public const string BootScenePath = ScenesFolder + "/Boot.unity";
        public const string TitleScenePath = ScenesFolder + "/Title.unity";
        public const string SpikeScenePath = ScenesFolder + "/ThaiTextSpike.unity";

        private const string SourceFontFile = FontsFolder + "/Sarabun-Regular.ttf";

        /// <summary>
        /// Nominal size the Thai spike measures both stacks at. The two engines count
        /// size differently at the margins, so the comparison in
        /// <c>ThaiSpikeReport.StacksMeasureEqually</c> is relative (1 %), not exact.
        /// </summary>
        private const int SpikeTmpFontSize = 48;

        // ------------------------------------------------------------------
        // Menu entry points
        // ------------------------------------------------------------------

        [MenuItem("ProjectSurvive/Stage 03/1. Configure project (URP + WebGL)", priority = 100)]
        public static void ConfigureProject()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(UiFolder);

            ConfigureRenderPipeline();
            ConfigurePlayerSettings();

            ThemeStyleSheet theme = EnsureRuntimeTheme();
            EnsurePanelSettings(theme);
            EnsureFontAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectSurvive: project configured (URP asset assigned, WebGL player settings, fonts, runtime theme).");
        }

        [MenuItem("ProjectSurvive/Stage 03/2. Create scenes (Boot, Title, ThaiTextSpike)", priority = 101)]
        public static void CreateScenes()
        {
            EnsureFolder(ScenesFolder);

            CreateBootScene();
            CreateTitleScene();
            CreateSpikeScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectSurvive: Boot, Title and ThaiTextSpike scenes created and added to build settings.");
        }

        // ------------------------------------------------------------------
        // Render pipeline
        // ------------------------------------------------------------------

        private static void ConfigureRenderPipeline()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(UrpRendererPath);
            if (rendererData is null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, UrpRendererPath);
            }

            // A freshly created renderer data asset has every shader/resource slot
            // empty. URP fills them from its package resources with this call; skip
            // it and rendering fails at runtime with missing-blit-shader errors.
            ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
            EditorUtility.SetDirty(rendererData);

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (pipeline is null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, UrpAssetPath);
            }

            EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline;

            // Quality levels each carry their own pipeline override; a level left
            // pointing at nothing renders with the built-in pipeline.
            int previous = QualitySettings.GetQualityLevel();
            string[] levelNames = QualitySettings.names;
            for (int i = 0; i < levelNames.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(previous, false);
        }

        // ------------------------------------------------------------------
        // Player settings
        // ------------------------------------------------------------------

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "ProjectSurvive";
            PlayerSettings.productName = "ProjectSurvive";

            // URP is authored for linear colour; the WebGL2 target supports it.
            PlayerSettings.colorSpace = ColorSpace.Linear;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // IL2CPP is the only scripting backend WebGL accepts; setting it makes
            // the intent explicit instead of relying on it being implied.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);

            // Keep stripping Low for the first Web build: VContainer, R3 and the
            // Protobuf descriptor tables are reflection-adjacent, and a stripped
            // build fails at runtime rather than at build time. Raising it is a
            // measurement-driven task for a later stage, not a Stage 03 guess.
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);

            // The local dev server (tools/serve-web.mjs) is a plain static file
            // server and cannot set Content-Encoding, so the build must not be
            // gzip/brotli compressed.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;

            // AGENTS rule 6: the web target is WebGL2 single-threaded. Threads also
            // require COOP/COEP response headers, which a plain static server does
            // not send.
            PlayerSettings.WebGL.threadsSupport = false;

            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // UI Toolkit runtime theme + panel settings
        // ------------------------------------------------------------------

        private static ThemeStyleSheet EnsureRuntimeTheme()
        {
            if (!File.Exists(ThemePath))
            {
                // Exactly what Unity's "Assets > Create > UI Toolkit > Default
                // Runtime Theme" writes: a one-line theme that imports the default
                // theme shipped with the UI Toolkit module.
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            }

            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme is null)
            {
                throw new InvalidOperationException("Could not import " + ThemePath + " as a ThemeStyleSheet.");
            }

            return theme;
        }

        private static PanelSettings EnsurePanelSettings(ThemeStyleSheet theme)
        {
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel is null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            panel.themeStyleSheet = theme;

            // The spike compares glyph shapes and metrics, so the panel must not
            // rescale text with the window: constant pixels keep the numbers
            // comparable between resolutions.
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;

            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ------------------------------------------------------------------
        // Font assets
        // ------------------------------------------------------------------

        private static (TMP_FontAsset tmp, FontAsset textCore) EnsureFontAssets()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontFile);
            if (source is null)
            {
                throw new InvalidOperationException("Source font not found at " + SourceFontFile + ".");
            }

            TMP_FontAsset tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (tmp is null)
            {
                tmp = TMP_FontAsset.CreateFontAsset(source);
                tmp.name = "Sarabun-Regular SDF";
                AssetDatabase.CreateAsset(tmp, TmpFontPath);
                PersistTmpSubAssets(tmp);
            }

            FontAsset textCore = AssetDatabase.LoadAssetAtPath<FontAsset>(TextCoreFontPath);
            if (textCore is null)
            {
                textCore = FontAsset.CreateFontAsset(source);
                textCore.name = "Sarabun-Regular FontAsset";
                AssetDatabase.CreateAsset(textCore, TextCoreFontPath);
                PersistTextCoreSubAssets(textCore);
            }

            BakeGlyphCoverage(tmp, textCore);

            AssetDatabase.SaveAssets();
            return (tmp, textCore);
        }

        // ------------------------------------------------------------------
        // Glyph coverage
        // ------------------------------------------------------------------

        /// <summary>
        /// The character set the build must be able to draw without touching the
        /// font file at runtime: printable ASCII plus the <b>assigned</b> Thai code
        /// points.
        /// </summary>
        /// <remarks>
        /// The Thai block U+0E00-U+0E7F reserves 128 code points but only assigns 87
        /// (U+0E01-U+0E3A and U+0E3F-U+0E5B); the other 41 are unassigned and no font
        /// can supply them. Asking for the whole block therefore reports 41
        /// "missing" glyphs that mean nothing — measured on this machine during
        /// Stage 03 before the set was narrowed. Restricting the set to the assigned
        /// ranges makes a non-zero missing count mean a real gap.
        /// </remarks>
        public static string BuildBakedCharacterSet()
        {
            var characters = new System.Text.StringBuilder(200);
            AppendRange(characters, 0x20, 0x7E);   // printable ASCII
            AppendRange(characters, 0x0E01, 0x0E3A); // Thai consonants, vowels, tone marks
            AppendRange(characters, 0x0E3F, 0x0E5B); // Thai currency, digits, vowels, signs
            return characters.ToString();
        }

        private static void AppendRange(System.Text.StringBuilder characters, int first, int last)
        {
            for (int codePoint = first; codePoint <= last; codePoint++)
            {
                characters.Append((char)codePoint);
            }
        }

        /// <summary>
        /// Bakes the character set into both font assets, verifies the result, and
        /// leaves each asset <b>Static</b> so the baked tables ship.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the Stage 03 Thai text decision in executable form: a Dynamic font
        /// asset rasterises a glyph on first display, which pays the cost inside the
        /// player, on whatever machine and browser the player runs on, and makes glyph
        /// coverage unprovable — "is this glyph already rasterised?" is not "can the
        /// shipped font draw it?". Baking moves the cost to build time and makes
        /// coverage a fact of the asset.
        /// </para>
        /// <para>
        /// Three measured traps shape the code below:
        /// </para>
        /// <list type="number">
        /// <item><description>
        /// <c>TryAddCharacters</c> refuses to run on a <b>Static</b> asset (it warns
        /// and returns false with no missing list — see
        /// <c>TMP_FontAsset.TryAddCharacters</c>), so the bake flips the asset to
        /// Dynamic, bakes, then flips it back to Static.
        /// </description></item>
        /// <item><description>
        /// Unity's TMP pre-build processor calls
        /// <c>ClearCharacterAndGlyphTablesInternal()</c> on every <b>Dynamic</b> asset
        /// with <c>clearDynamicDataOnBuild</c>, <b>at build time, on the project
        /// asset</b>. The first Stage 03 Web build therefore emptied the baked
        /// character and glyph tables on disk and the shipped player could not look up
        /// a single Thai character. A Static asset is not touched by that processor
        /// (its condition is Dynamic/DynamicOS).
        /// </description></item>
        /// <item><description>
        /// The first population of a fresh asset can fail while the source font is
        /// still loading and then report the whole set as missing, so it is retried
        /// once — and the <i>observed</i> result, retry included, is what gets written
        /// to <c>artifacts/thai-font-bake.json</c> and verified.
        /// </description></item>
        /// </list>
        /// <para>
        /// This method is idempotent and it verifies itself: it bakes from a cleared
        /// asset, re-reads the tables, and <b>throws</b> if either asset cannot draw
        /// the required set. A silently empty font asset is exactly the failure that
        /// reached a browser build once; it must not be able to do so again.
        /// </para>
        /// </remarks>
        private static void BakeGlyphCoverage(TMP_FontAsset tmp, FontAsset textCore)
        {
            string characters = BuildBakedCharacterSet();

            bool tmpAdded = BakeIntoTmp(tmp, characters, out bool tmpRetried, out string tmpMissing);
            EditorUtility.SetDirty(tmp);

            bool textCoreAdded = BakeIntoTextCore(textCore, characters, out bool textCoreRetried, out string textCoreMissing);
            EditorUtility.SetDirty(textCore);

            // Verify what actually landed in the assets, not what the add call claimed.
            tmp.ReadFontAssetDefinition();
            textCore.ReadFontAssetDefinition();

            int tmpCount = tmp.characterTable?.Count ?? 0;
            int textCoreCount = textCore.characterTable?.Count ?? 0;
            bool tmpCovered = tmp.HasCharacters(characters);
            bool textCoreCovered = textCore.HasCharacters(characters);

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "ProjectSurvive: baked {0} characters - TMP added={1} retried={2} missing={3} table={4} coversAll={5} mode={6} | TextCore added={7} retried={8} missing={9} table={10} coversAll={11} mode={12}",
                characters.Length,
                tmpAdded,
                tmpRetried,
                tmpMissing?.Length ?? -1,
                tmpCount,
                tmpCovered,
                tmp.atlasPopulationMode,
                textCoreAdded,
                textCoreRetried,
                textCoreMissing?.Length ?? -1,
                textCoreCount,
                textCoreCovered,
                textCore.atlasPopulationMode));

            WriteBakeEvidence(
                characters.Length,
                tmpAdded,
                tmpMissing,
                tmpRetried,
                tmpCount,
                tmpCovered,
                textCoreAdded,
                textCoreMissing,
                textCoreRetried,
                textCoreCount,
                textCoreCovered);

            if (!tmpCovered || !textCoreCovered)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Font bake did not cover the required set: TMP coversAll={0} (table {1}), TextCore coversAll={2} (table {3}). Delete the font asset under {4} and run the menu item again.",
                    tmpCovered,
                    tmpCount,
                    textCoreCovered,
                    textCoreCount,
                    FontsFolder));
            }
        }

        /// <summary>
        /// bakes the required set into the TMP atlas and freezes the asset Static.
        /// </summary>
        private static bool BakeIntoTmp(
            TMP_FontAsset font,
            string characters,
            out bool retried,
            out string missing)
        {
            // TMPro and TextCore each declare their own AtlasPopulationMode, so the
            // two bakes name theirs explicitly rather than relying on a using alias.
            font.atlasPopulationMode = TMPro.AtlasPopulationMode.Dynamic;  // the only mode that accepts additions
            font.ReadFontAssetDefinition();
            font.ClearFontAssetData();                                     // bake from scratch, so a re-run cannot depend on the previous state

            bool added = font.TryAddCharacters(characters, out missing);
            retried = false;
            if (!added && string.Equals(missing, characters, StringComparison.Ordinal))
            {
                retried = true;
                font.TryAddCharacters(" ", out _);
                added = font.TryAddCharacters(characters, out missing);
            }

            font.atlasPopulationMode = TMPro.AtlasPopulationMode.Static;   // ships the tables; the pre-build processor leaves Static assets alone
            return added;
        }

        /// <summary>Same bake-and-freeze for the TextCore atlas UI Toolkit renders with.</summary>
        private static bool BakeIntoTextCore(
            FontAsset font,
            string characters,
            out bool retried,
            out string missing)
        {
            font.atlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic;
            font.ClearFontAssetData();

            bool added = font.TryAddCharacters(characters, out missing);
            retried = false;
            if (!added && string.Equals(missing, characters, StringComparison.Ordinal))
            {
                retried = true;
                font.TryAddCharacters(" ", out _);
                added = font.TryAddCharacters(characters, out missing);
            }

            font.atlasPopulationMode = UnityEngine.TextCore.Text.AtlasPopulationMode.Static;
            return added;
        }

        /// <summary>
        /// Writes the bake result next to the other Stage 03 artifacts, so the ADR
        /// cites a measured run instead of a remembered one.
        /// </summary>
        private static void WriteBakeEvidence(
            int characters,
            bool tmpAdded,
            string tmpMissing,
            bool tmpRetried,
            int tmpTableCount,
            bool tmpCovered,
            bool textCoreAdded,
            string textCoreMissing,
            bool textCoreRetried,
            int textCoreTableCount,
            bool textCoreCovered)
        {
            string repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string artifactsFolder = Path.Combine(repositoryRoot, "artifacts");
            Directory.CreateDirectory(artifactsFolder);

            var json = new System.Text.StringBuilder();
            json.Append("{\n");
            json.Append("  \"unityVersion\": \"").Append(Application.unityVersion).Append("\",\n");
            json.Append("  \"font\": \"Sarabun-Regular.ttf\",\n");
            json.Append("  \"characterSetSize\": ").Append(characters.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"ranges\": [\"U+0020..U+007E\", \"U+0E01..U+0E3A\", \"U+0E3F..U+0E5B\"],\n");
            json.Append("  \"shippedMode\": \"Static\",\n");
            json.Append("  \"tmp\": { \"added\": ").Append(tmpAdded ? "true" : "false")
                .Append(", \"missingCount\": ").Append((tmpMissing?.Length ?? -1).ToString(CultureInfo.InvariantCulture))
                .Append(", \"retried\": ").Append(tmpRetried ? "true" : "false")
                .Append(", \"characterTableCount\": ").Append(tmpTableCount.ToString(CultureInfo.InvariantCulture))
                .Append(", \"coversRequiredSet\": ").Append(tmpCovered ? "true" : "false").Append(" },\n");
            json.Append("  \"textCore\": { \"added\": ").Append(textCoreAdded ? "true" : "false")
                .Append(", \"missingCount\": ").Append((textCoreMissing?.Length ?? -1).ToString(CultureInfo.InvariantCulture))
                .Append(", \"retried\": ").Append(textCoreRetried ? "true" : "false")
                .Append(", \"characterTableCount\": ").Append(textCoreTableCount.ToString(CultureInfo.InvariantCulture))
                .Append(", \"coversRequiredSet\": ").Append(textCoreCovered ? "true" : "false").Append(" }\n");
            json.Append("}\n");

            File.WriteAllText(Path.Combine(artifactsFolder, "thai-font-bake.json"), json.ToString());
        }

        /// <summary>
        /// TMP creates the atlas texture and material as runtime objects; without
        /// this they stay non-persistent and the font asset breaks on reload.
        /// </summary>
        private static void PersistTmpSubAssets(TMP_FontAsset font)
        {
            if (font.material is not null)
            {
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            if (font.atlasTextures is not null)
            {
                for (int i = 0; i < font.atlasTextures.Length; i++)
                {
                    Texture2D atlas = font.atlasTextures[i];
                    if (atlas is null)
                    {
                        continue;
                    }

                    atlas.name = font.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }
        }

        /// <summary>Same persistence step for the TextCore (UI Toolkit) font asset.</summary>
        private static void PersistTextCoreSubAssets(FontAsset font)
        {
            if (font.material is not null)
            {
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
            }

            if (font.atlasTextures is not null)
            {
                for (int i = 0; i < font.atlasTextures.Length; i++)
                {
                    Texture2D atlas = font.atlasTextures[i];
                    if (atlas is null)
                    {
                        continue;
                    }

                    atlas.name = font.name + " Atlas " + i;
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }
        }

        // ------------------------------------------------------------------
        // Scenes
        // ------------------------------------------------------------------

        private static void CreateBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Boot");
            RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
            SceneBoot boot = root.AddComponent<SceneBoot>();

            SetObjectReference(boot, "_scope", scope);
            SetBool(boot, "_continueToTitleScene", true);
            SetString(boot, "_titleSceneName", "Title");

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreateTitleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RootLifetimeScope scope = CreateScopeObject();
            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas(out GameObject canvasObject);

            (TMP_FontAsset tmpFont, _) = EnsureFontAssets();

            TMP_Text titleLabel = CreateTmpLabel(canvas, "TitleLabel", new Vector2(0f, 120f), 96f, tmpFont);
            TMP_Text diagnosticsLabel = CreateTmpLabel(canvas, "DiagnosticsLabel", new Vector2(0f, -60f), 28f, tmpFont);

            TitleView view = canvasObject.AddComponent<TitleView>();
            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_titleLabel", titleLabel);
            SetObjectReference(view, "_diagnosticsLabel", diagnosticsLabel);

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void CreateSpikeScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RootLifetimeScope scope = CreateScopeObject();
            CreateCamera();
            CreateEventSystem();

            (TMP_FontAsset tmpFont, FontAsset textCoreFont) = EnsureFontAssets();
            PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            Canvas canvas = CreateCanvas(out GameObject canvasObject);
            TMP_Text tmpLabel = CreateTmpLabel(canvas, "TmpSpikeLabel", new Vector2(0f, 200f), SpikeTmpFontSize, tmpFont);

            var tmpViewObject = new GameObject("TmpSpike");
            tmpViewObject.transform.SetParent(canvasObject.transform, false);
            ThaiSpikeTmpView tmpView = tmpViewObject.AddComponent<ThaiSpikeTmpView>();
            SetObjectReference(tmpView, "_label", tmpLabel);
            SetObjectReference(tmpView, "_font", tmpFont);

            var toolkitObject = new GameObject("ToolkitSpike");
            UIDocument document = toolkitObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            ThaiSpikeToolkitView toolkitView = toolkitObject.AddComponent<ThaiSpikeToolkitView>();
            SetObjectReference(toolkitView, "_document", document);
            SetObjectReference(toolkitView, "_fontAsset", textCoreFont);

            // Both stacks are given the same nominal size, because they are being
            // compared: measured Stage 03 at unequal sizes (48 vs 32) the two stacks
            // disagreed on every string, and the disagreement was the configuration,
            // not the text engine.
            SetInt(toolkitView, "_fontSize", SpikeTmpFontSize);

            ThaiSpikeReporter reporter = canvasObject.AddComponent<ThaiSpikeReporter>();
            SetObjectReference(reporter, "_scope", scope);
            SetObjectReference(reporter, "_tmpView", tmpView);
            SetObjectReference(reporter, "_toolkitView", toolkitView);
            SetString(reporter, "_fontName", "Sarabun-Regular.ttf");

            EditorSceneManager.SaveScene(scene, SpikeScenePath);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(SpikeScenePath, true),
            };
        }

        // ------------------------------------------------------------------
        // Scene building blocks
        // ------------------------------------------------------------------

        private static RootLifetimeScope CreateScopeObject()
        {
            var scopeObject = new GameObject("RootLifetimeScope");
            return scopeObject.AddComponent<RootLifetimeScope>();
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;

            // URP expects its per-camera data component; adding it here keeps the
            // scene identical to one created through the URP menu.
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            return camera;
        }

        private static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static Canvas CreateCanvas(out GameObject canvasObject)
        {
            canvasObject = new GameObject("UI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static TMP_Text CreateTmpLabel(Canvas canvas, string name, Vector2 anchoredPosition, float fontSize, TMP_FontAsset font)
        {
            var labelObject = new GameObject(name);
            labelObject.transform.SetParent(canvas.transform, false);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.text = string.Empty;

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(1800f, 200f);

            return label;
        }

        // ------------------------------------------------------------------
        // Serialized-field helpers
        // ------------------------------------------------------------------

        private static void SetObjectReference(Object target, string fieldName, Object value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object target, string fieldName, bool value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Object target, string fieldName, int value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.intValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindProperty(Object target, string fieldName)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);
            if (property is null)
            {
                throw new InvalidOperationException(
                    "Serialized field '" + fieldName + "' not found on " + target.GetType().Name + ".");
            }

            return property;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                throw new InvalidOperationException("Cannot create folder at " + path + ".");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
