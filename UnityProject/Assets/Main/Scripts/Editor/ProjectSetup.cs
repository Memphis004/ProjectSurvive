using System;
using System.Globalization;
using System.IO;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
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

// UnityEngine.UI and UnityEngine.UIElements both declare Image and Button. The shell's
// runtime UI is uGUI, so those names mean the uGUI types everywhere in this file.
using Image = UnityEngine.UI.Image;
using Button = UnityEngine.UI.Button;

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

        /// <summary>The Stage 08 battle scene.</summary>
        public const string BattleScenePath = ScenesFolder + "/Battle.unity";

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

        /// <summary>
        /// Creates the Stage 08 battle scene and refreshes the build list.
        /// </summary>
        /// <remarks>
        /// Its own menu item rather than a fourth call inside Stage 03's, so re-running the
        /// Stage 03 setup on an older checkout cannot silently rewrite the battle scene, and
        /// so the scene that a stage owns is the scene that stage's menu item builds. Run the
        /// Stage 03 items first on a fresh clone: this one reuses the URP assets, the runtime
        /// theme and the baked font they create.
        /// </remarks>
        [MenuItem("ProjectSurvive/Stage 08/3. Create Battle scene", priority = 102)]
        public static void CreateBattleSceneAndConfigure()
        {
            EnsureFolder(ScenesFolder);

            CreateBattleScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectSurvive: Battle scene created and added to build settings.");
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

            // `?scene=Battle` is how a browser test reaches the battle without playing through
            // the camp. It is an allow-list, not a router: only the scenes named here can be
            // requested from a URL.
            SetStringArray(boot, "_urlSceneOverrides", new[] { "ThaiTextSpike", "Battle" });

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
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(SpikeScenePath, true),
            };

            // The battle scene joins the list only once it exists, so a Stage 03-only checkout
            // keeps a build list of scenes that are really there.
            if (File.Exists(BattleScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(BattleScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------
        // Stage 08: the Battle scene
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the 2.5D battle scene: the scope that picks the battle, the tilted camera,
        /// the pooled view root, the runner, and the HUD.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything here is created through the Editor API (AGENTS rule 11) — no
        /// hand-written <c>.unity</c> YAML — so the scene is reproducible from the menu item and
        /// reviewable as code. The serialized references the runtime needs (the view root, the
        /// camera, the HUD and the font) are wired through <c>SerializedProperty</c> rather than
        /// by guessing at file ids.
        /// </para>
        /// <para>
        /// The camera is authored at the Stage 08 decision: see
        /// <c>docs/adr/0018-battle-camera-projection.md</c>. The projection is stored on the
        /// runner's settings as well, so a scene can flip it without editing this file, and the
        /// rig re-applies it at runtime either way.
        /// </para>
        /// </remarks>
        private static void CreateBattleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = EnsureFontAssets();

            var scopeObject = new GameObject("Battle");
            BattleSceneScope scope = scopeObject.AddComponent<BattleSceneScope>();
            BattleSceneDirector director = scopeObject.AddComponent<BattleSceneDirector>();

            Camera camera = CreateBattleCamera();
            CreateEventSystem();

            var viewRoot = new GameObject("ViewRoot");

            Canvas canvas = CreateCanvas(out GameObject canvasObject);
            BattleHudView hud = BuildBattleHud(canvas, canvasObject, font, scope);

            var runnerObject = new GameObject("BattleRunner");
            BattleSimulationRunner runner = runnerObject.AddComponent<BattleSimulationRunner>();

            SetObjectReference(runner, "_viewSettings.ViewRoot", viewRoot.transform);
            SetObjectReference(runner, "_viewSettings.Camera", camera);
            SetObjectReference(runner, "_viewSettings.Hud", hud);
            SetObjectReference(runner, "_viewSettings.Font", font);
            SetEnum(runner, "_viewSettings.Projection", (int)BattleProjection.Orthographic);

            SetObjectReference(director, "_scope", scope);
            SetObjectReference(director, "_runner", runner);

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        /// <summary>
        /// The battle camera: a 50° tilt that follows the player.
        /// </summary>
        /// <remarks>
        /// The camera itself is built by <see cref="BattleCameraProbe.CreateAuthoredCamera"/>, which
        /// poses it with <c>BattleCameraRig</c> — the same rig that re-poses it on the first frame
        /// of a battle. One factory rather than two copies of the arithmetic means the scene an
        /// artist opens is the scene the game runs, and the camera the ADR's experiment measured is
        /// the camera the scene ships.
        /// </remarks>
        private static Camera CreateBattleCamera() => BattleCameraProbe.CreateAuthoredCamera();

        /// <summary>
        /// The battle HUD: health, focus, bia, the clock, the wave cursor, the spells the
        /// loadout resolved to, the timeline bar with its elite and boss markers, and the
        /// 1×/2× button.
        /// </summary>
        private static BattleHudView BuildBattleHud(
            Canvas canvas, GameObject canvasObject, TMP_FontAsset font, BattleSceneScope scope)
        {
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform topLeft = CreateUiRect(
                canvasRect, "StatusPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(32f, -32f), new Vector2(440f, 260f));


            TMP_Text floorLabel = CreateUiText(topLeft, "FloorLabel", new Vector2(0f, -8f), new Vector2(420f, 44f), 30f, font);
            TMP_Text hpCaption = CreateUiText(topLeft, "HpCaption", new Vector2(0f, -60f), new Vector2(200f, 32f), 22f, font);
            TMP_Text hpValue = CreateUiText(topLeft, "HpValue", new Vector2(210f, -60f), new Vector2(200f, 32f), 22f, font);
            RectTransform hpBar = CreateUiBar(topLeft, "HpBar", new Vector2(0f, -96f), new Vector2(420f, 18f), new Color(0.85f, 0.28f, 0.30f, 1f));
            TMP_Text manaCaption = CreateUiText(topLeft, "ManaCaption", new Vector2(0f, -124f), new Vector2(200f, 32f), 22f, font);
            TMP_Text manaValue = CreateUiText(topLeft, "ManaValue", new Vector2(210f, -124f), new Vector2(200f, 32f), 22f, font);
            RectTransform manaBar = CreateUiBar(topLeft, "ManaBar", new Vector2(0f, -160f), new Vector2(420f, 18f), new Color(0.42f, 0.72f, 0.95f, 1f));
            TMP_Text killsValue = CreateUiText(topLeft, "KillsValue", new Vector2(0f, -196f), new Vector2(420f, 32f), 22f, font);

            RectTransform topRight = CreateUiRect(
                canvasRect, "ResourcePanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -32f), new Vector2(420f, 260f));

            TMP_Text biaCaption = CreateUiText(topRight, "BiaCaption", new Vector2(0f, -8f), new Vector2(200f, 32f), 22f, font);
            TMP_Text biaValue = CreateUiText(topRight, "BiaValue", new Vector2(0f, -40f), new Vector2(200f, 40f), 30f, font);
            TMP_Text timeCaption = CreateUiText(topRight, "TimeCaption", new Vector2(210f, -8f), new Vector2(200f, 32f), 22f, font);
            TMP_Text timeValue = CreateUiText(topRight, "TimeValue", new Vector2(210f, -40f), new Vector2(200f, 40f), 30f, font);
            TMP_Text waveCaption = CreateUiText(topRight, "WaveCaption", new Vector2(0f, -92f), new Vector2(200f, 32f), 22f, font);
            TMP_Text waveValue = CreateUiText(topRight, "WaveValue", new Vector2(210f, -92f), new Vector2(200f, 32f), 22f, font);

            // The boss bar appears only on a boss floor; the presenter tells the view when.
            RectTransform bossRoot = CreateUiRect(
                topRight, "BossPanel", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -140f), new Vector2(420f, 70f));
            TMP_Text bossCaption = CreateUiText(bossRoot, "BossCaption", Vector2.zero, new Vector2(200f, 32f), 22f, font);
            RectTransform bossBar = CreateUiBar(bossRoot, "BossBar", new Vector2(0f, -34f), new Vector2(420f, 16f), new Color(0.85f, 0.32f, 0.34f, 1f));

            RectTransform speedObject = CreateUiRect(
                canvasRect, "SpeedButton", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-32f, -300f), new Vector2(160f, 64f));
            Image speedBackground = speedObject.gameObject.AddComponent<Image>();
            speedBackground.color = new Color(0.16f, 0.17f, 0.22f, 0.9f);
            Button speedButton = speedObject.gameObject.AddComponent<Button>();
            speedButton.targetGraphic = speedBackground;
            TMP_Text speedLabel = CreateUiText(
                speedObject, "SpeedLabel", Vector2.zero, new Vector2(160f, 64f), 28f, font,
                TextAlignmentOptions.Center);

            RectTransform bottom = CreateUiRect(
                canvasRect, "TimelinePanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(1400f, 170f));

            TMP_Text timelineCaption = CreateUiText(bottom, "TimelineCaption", new Vector2(0f, -6f), new Vector2(700f, 30f), 20f, font);
            RectTransform track = CreateUiRect(
                bottom, "TimelineTrack", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -44f), new Vector2(1380f, 26f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.12f, 0.13f, 0.17f, 0.85f);

            // Markers stretch across the track and are positioned by their anchors, so the bar
            // needs no per-frame layout maths: the view writes anchorMin/anchorMax and moves on.
            RectTransform timelineMarkers = CreateUiRect(
                track, "Markers", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            timelineMarkers.anchorMin = Vector2.zero;
            timelineMarkers.anchorMax = Vector2.one;
            timelineMarkers.offsetMin = Vector2.zero;
            timelineMarkers.offsetMax = Vector2.zero;

            RectTransform playhead = CreateUiRect(
                track, "Playhead", Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 0f));
            playhead.anchorMin = new Vector2(0f, 0f);
            playhead.anchorMax = new Vector2(0f, 1f);
            playhead.offsetMin = Vector2.zero;
            playhead.offsetMax = Vector2.zero;
            Image playheadImage = playhead.gameObject.AddComponent<Image>();
            playheadImage.color = new Color(0.95f, 0.95f, 0.98f, 0.9f);

            TMP_Text spellsCaption = CreateUiText(bottom, "SpellsCaption", new Vector2(0f, -76f), new Vector2(1380f, 28f), 20f, font);
            TMP_Text spellsValue = CreateUiText(bottom, "SpellsValue", new Vector2(0f, -106f), new Vector2(1380f, 28f), 22f, font);

            TMP_Text statusLabel = CreateUiText(
                canvasRect, "StatusLabel", new Vector2(-600f, 120f), new Vector2(1200f, 90f), 56f, font,
                TextAlignmentOptions.Center);
            statusLabel.gameObject.SetActive(false);

            BattleTimelineView timeline = track.gameObject.AddComponent<BattleTimelineView>();
            SetObjectReference(timeline, "_track", timelineMarkers);
            SetObjectReference(timeline, "_playhead", playhead);

            BattleHudView hud = canvasObject.AddComponent<BattleHudView>();
            SetObjectReference(hud, "_scope", scope);
            SetObjectReference(hud, "_floorLabel", floorLabel);
            SetObjectReference(hud, "_hpCaption", hpCaption);
            SetObjectReference(hud, "_hpValue", hpValue);
            SetObjectReference(hud, "_manaCaption", manaCaption);
            SetObjectReference(hud, "_manaValue", manaValue);
            SetObjectReference(hud, "_biaCaption", biaCaption);
            SetObjectReference(hud, "_biaValue", biaValue);
            SetObjectReference(hud, "_timeCaption", timeCaption);
            SetObjectReference(hud, "_timeValue", timeValue);
            SetObjectReference(hud, "_waveCaption", waveCaption);
            SetObjectReference(hud, "_waveValue", waveValue);
            SetObjectReference(hud, "_killsValue", killsValue);
            SetObjectReference(hud, "_spellsCaption", spellsCaption);
            SetObjectReference(hud, "_spellsValue", spellsValue);
            SetObjectReference(hud, "_timelineCaption", timelineCaption);
            SetObjectReference(hud, "_bossCaption", bossCaption);
            SetObjectReference(hud, "_statusLabel", statusLabel);
            SetObjectReference(hud, "_speedLabel", speedLabel);
            SetObjectReference(hud, "_hpBar", hpBar);
            SetObjectReference(hud, "_manaBar", manaBar);
            SetObjectReference(hud, "_bossBar", bossBar);
            SetObjectReference(hud, "_bossRoot", bossRoot.gameObject);
            SetObjectReference(hud, "_speedButton", speedButton);
            SetObjectReference(hud, "_timeline", timeline);

            return hud;
        }

        /// <summary>Creates an empty UI rectangle (a panel or a track) at an anchored position.</summary>
        private static RectTransform CreateUiRect(
            RectTransform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>A label inside a panel, laid out from the panel's top-left corner.</summary>
        private static TMP_Text CreateUiText(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize,
            TMP_FontAsset font, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rect = CreateUiRect(
                parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size);

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;
            label.text = string.Empty;
            return label;
        }

        /// <summary>
        /// Creates a track and its fill, and returns the fill.
        /// </summary>
        /// <remarks>
        /// The fill is a rectangle anchored to the track's left edge, so the HUD sets a bar by
        /// writing <c>anchorMax.x</c> and no sprite asset is needed anywhere in the UI.
        /// </remarks>
        private static RectTransform CreateUiBar(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, Color fillColour)
        {
            RectTransform track = CreateUiRect(
                parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, size);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.5f);

            RectTransform fill = CreateUiRect(
                track, "Fill", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = fillColour;
            return fill;
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

        /// <summary>Sets an enum field by its index. Enum indices are the authored order.</summary>
        private static void SetEnum(Object target, string fieldName, int index)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.enumValueIndex = index;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Replaces a string-array field, e.g. the Boot scene's scene allow-list.</summary>
        private static void SetStringArray(Object target, string fieldName, string[] values)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = values[i];
            }

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
