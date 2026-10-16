using System;
using System.Globalization;
using System.IO;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Thai;
using ProjectSurvive.Game.ResultScene;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
using Toggle = UnityEngine.UI.Toggle;

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

        /// <summary>The Stage 09 camp scene (ศาลาพัก).</summary>
        public const string CampScenePath = ScenesFolder + "/Camp.unity";

        /// <summary>The Stage 10 run-summary scene.</summary>
        public const string ResultScenePath = ScenesFolder + "/Result.unity";

        /// <summary>
        /// Scenes a page URL may request with <c>?scene=&lt;name&gt;</c>. One list, used both when
        /// the Boot scene is created and when a scene that should be reachable is added, so the two
        /// cannot drift.
        /// </summary>
        private static readonly string[] UrlSceneAllowList = { "ThaiTextSpike", "Battle", "Camp", "Result" };

        /// <summary>The summary screen's own line size: comfortably above the 22 px Thai floor.</summary>
        private const float ResultLineFontSize = 32f;

        private const string SourceFontFile = FontsFolder + "/Sarabun-Regular.ttf";

        /// <summary>
        /// Nominal size the Thai spike measures both stacks at. The two engines count
        /// size differently at the margins, so the comparison in
        /// <c>ThaiSpikeReport.StacksMeasureEqually</c> is relative (1 %), not exact.
        /// </summary>
        private const int SpikeTmpFontSize = 48;

        /// <summary>
        /// The smallest a Thai label may be authored at, in canvas pixels at the 1920×1080 reference
        /// resolution every canvas is scaled from (Stage 09d work item 2: *"ข้อความไทยทั่วไป ≥ 22 px
        /// ฐาน 1080p"*).
        /// </summary>
        /// <remarks>
        /// Before Stage 09d the panel ran on 14, 15, 16, 17, 18 and 20 px labels — the floor Stage 09b
        /// asked for was 14 px, which is below what Thai needs once its upper and lower marks are
        /// drawn (a Thai line box at 22 px is about 31 px tall, which is why the row pitches below
        /// grew with the type). Display text (the 96 px title, the 56 px battle status) is authored
        /// above the floor and left alone.
        /// </remarks>
        private const float UiFontSize = 22f;

        /// <summary>
        /// The camp tooltip canvas' draw order. Above the camp screen's own canvas (0) and above
        /// everything it contains — the board, the chest, the incense pot and the bottom bar — because
        /// the engine sorts overlay canvases by <c>Canvas.sortingOrder</c> before anything else.
        /// </summary>
        private const int TooltipSortingOrder = 100;

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

        /// <summary>
        /// Creates the Stage 09 camp scene and refreshes the build list.
        /// </summary>
        /// <remarks>
        /// Its own menu item for the same reason Stage 08 has one: re-running the Stage 03 setup on
        /// an older checkout must not silently rewrite the camp scene, and the scene a stage owns is
        /// the scene that stage's menu item builds. Run the Stage 03 items first on a fresh clone:
        /// this one reuses the URP assets, the runtime theme and the baked font they create.
        /// </remarks>
        [MenuItem("ProjectSurvive/Stage 09/5. Create Camp scene", priority = 104)]
        public static void CreateCampSceneAndConfigure()
        {
            EnsureFolder(ScenesFolder);

            CreateCampScene();
            ConfigureBuildSettings();
            RefreshUrlSceneAllowList();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectSurvive: Camp scene created and added to build settings.");
        }

        /// <summary>
        /// Creates the Stage 10 summary scene and refreshes the build list.
        /// </summary>
        /// <remarks>
        /// Its own menu item for the same reason Stage 08 and Stage 09 have one: the scene a stage
        /// owns is the scene that stage's item builds, so re-running an older stage's item on an
        /// existing checkout cannot silently rewrite it. Run the Stage 03 items first on a fresh
        /// clone — this one reuses the URP assets, the runtime theme and the baked font they create.
        /// </remarks>
        [MenuItem("ProjectSurvive/Stage 10/6. Create Result scene", priority = 105)]
        public static void CreateResultSceneAndConfigure()
        {
            EnsureFolder(ScenesFolder);

            CreateResultScene();
            ConfigureBuildSettings();
            RefreshUrlSceneAllowList();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectSurvive: Result scene created and added to build settings.");
        }

        /// <summary>
        /// Rebuilds the title scene with its start control, and refreshes the build list.
        /// </summary>
        /// <remarks>
        /// Its own menu item even though <see cref="CreateTitleScene"/> is also what Stage 03's scene
        /// item calls: a checkout that predates Stage 10d needs the title screen
        /// <i>rebuilt</i> to gain the control, and running this cannot touch Boot, the spike scene or the
        /// build list of the other stages. The two items produce byte-identical scenes — this one
        /// simply says which change made you run it.
        /// </remarks>
        [MenuItem("ProjectSurvive/Stage 10d/7. Create Title scene (start control)", priority = 106)]
        public static void CreateTitleSceneAndConfigure()
        {
            EnsureFolder(ScenesFolder);

            CreateTitleScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ProjectSurvive: Title scene created with its start control.");
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
            // the camp, and `?scene=Camp` is how it reaches the camp. It is an allow-list, not a
            // router: only the scenes named here can be requested from a URL.
            SetStringArray(boot, "_urlSceneOverrides", UrlSceneAllowList);

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        /// <summary>
        /// Builds the title screen: the game's name, the build line under it, and the control that
        /// starts a run (Stage 10d).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The screen is three centred regions with nothing in common: the heading (96 px) at the
        /// canvas' centre +120, the build/config line (28 px) at −40, and a 400×76 control at −160.
        /// Every one of them is authored above the 22 px Thai floor (Stage 09d) and each box is the
        /// line box it holds rather than a 200 px placeholder, so that the layout the capture tool
        /// <b>measures</b> is the layout on screen: the heading's box is 140 px, the build line's 60 px
        /// and the button's 76 px, and the three are disjoint with ≥ 50 px between them.
        /// </para>
        /// <para>
        /// <b>All three are anchored to the canvas' centre</b>, which is what makes the screen centred
        /// at any canvas rather than only at the 1920×1080 reference: the two labels were already
        /// (<see cref="CreateTmpLabel"/>), and the control is given the same anchor here. A corner
        /// anchor would put it ~26 px right of the centre on the template's own 960×600 canvas — measured
        /// in the browser before this was fixed. The control is the same size as the summary screen's two
        /// (400×76), which are laid out from their columns' corners. Its caption comes from the
        /// localization table at runtime (AGENTS rule 5): the scene authors an empty label, like every
        /// other label here.
        /// </para>
        /// <para>
        /// เริ่มเล่น is what leaves the screen. The button records the press, the
        /// <see cref="TitlePresenter"/> starts the run through the Application layer, and
        /// <c>TitleSceneDirector</c> — added here, on the scope object — performs the move through the
        /// <c>SceneLoader</c>. Boot still stops at this screen on purpose.
        /// </para>
        /// </remarks>
        private static void CreateTitleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RootLifetimeScope scope = CreateScopeObject();
            TitleSceneDirector director = scope.gameObject.AddComponent<TitleSceneDirector>();

            CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            (TMP_FontAsset tmpFont, _) = EnsureFontAssets();

            TMP_Text titleLabel = CreateTmpLabel(
                canvas, "TitleLabel", new Vector2(0f, 120f), 96f, tmpFont, new Vector2(1800f, 140f));

            TMP_Text diagnosticsLabel = CreateTmpLabel(
                canvas, "DiagnosticsLabel", new Vector2(0f, -40f), 28f, tmpFont, new Vector2(1800f, 60f));

            // Anchored to the canvas' centre rather than laid out from its top-left corner, because the
            // control belongs to content that is centred: the heading and the build line above it are
            // centre-anchored (CreateTmpLabel), and a CanvasScaler's virtual width follows the screen's
            // aspect, so 1920 px of corner arithmetic would put this control ~26 px right of the centre
            // on the template's own 960×600 canvas while the two labels stayed centred. 160 px below the
            // centre is where 760/-662 put it at the 1920×1080 reference: the same place on screen, now
            // the same place on any canvas (measured in the browser: 480 of 960 and 640 of 1280).
            Button startButton = CreateUiButton(
                canvasRect, "StartButton", new Vector2(0f, -160f), new Vector2(400f, 76f), tmpFont,
                out TMP_Text startLabel, anchor: new Vector2(0.5f, 0.5f));

            TitleView view = canvasObject.AddComponent<TitleView>();
            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_titleLabel", titleLabel);
            SetObjectReference(view, "_diagnosticsLabel", diagnosticsLabel);
            SetObjectReference(view, "_startButton", startButton);
            SetObjectReference(view, "_startLabel", startLabel);

            SetObjectReference(director, "_scope", scope);

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

            // The battle and camp scenes join the list only once they exist, so a Stage 03-only
            // checkout keeps a build list of scenes that are really there.
            if (File.Exists(BattleScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(BattleScenePath, true));
            }

            if (File.Exists(CampScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(CampScenePath, true));
            }

            // The summary scene likewise joins only once it exists: a Stage 09-only checkout keeps
            // a build list of scenes that are really there, and the battle's departure to it is
            // refused (with a typed error) rather than attempted against a missing scene.
            if (File.Exists(ResultScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ResultScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Writes the current URL allow-list into the Boot scene, if it is there.
        /// </summary>
        /// <remarks>
        /// Boot's allow-list is the only way a browser can choose a scene, and Stage 08 had to
        /// re-run the Boot menu item to get <c>Battle</c> into it. Doing it here means creating a
        /// scene is enough to make it reachable — the two halves of the feature (a scene that
        /// publishes its own name, and an allow-list that permits it) stay in step without a second
        /// manual step nobody remembers.
        /// </remarks>
        private static void RefreshUrlSceneAllowList()
        {
            if (!File.Exists(BootScenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            SceneBoot boot = Object.FindFirstObjectByType<SceneBoot>();
            if (boot is null)
            {
                Debug.LogWarning("ProjectSurvive: the Boot scene has no SceneBoot; its URL allow-list was not updated.");
                return;
            }

            SetStringArray(boot, "_urlSceneOverrides", UrlSceneAllowList);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootScenePath);
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

            BuildDevMenu(font, scope, runner);

            EditorSceneManager.SaveScene(scene, BattleScenePath);
        }

        /// <summary>
        /// The dev menu (Stage 10c): a panel of six test controls and a status line, authored on its
        /// own canvas above the HUD and <b>inactive</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// "ตั้งชื่อ test-only ให้ชัดเจน" is why the heading says <c>DEV MENU (test only)</c> in
        /// capitals and why the panel sits in a corner rather than among the run's own controls: it
        /// is not part of the game, and nothing about it should be mistaken for one.
        /// </para>
        /// <para>
        /// The panel starts inactive because that is the honest authored state — a release build
        /// must never show it, and the view is what turns it on, from
        /// <c>DevCheats.Allowed</c> (a development build or the Editor). The captions are written
        /// from <see cref="DevMenuText"/> rather than typed here, so the words exist in one place and
        /// a test can compare what it reads on screen with what the presenter sends.
        /// </para>
        /// <para>
        /// It is on its own canvas with <c>sortingOrder</c> 200: above the HUD (0), below the scene
        /// loader's fade (3000), so a control is never drawn under the HUD and is never drawn over a
        /// transition.
        /// </para>
        /// </remarks>
        private static void BuildDevMenu(TMP_FontAsset font, BattleSceneScope scope, BattleSimulationRunner runner)
        {
            Canvas canvas = CreateCanvas(out GameObject canvasObject, sortingOrder: 200, withRaycaster: true);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform panel = CreateUiRect(
                canvasRect, "DevPanel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(32f, 32f), new Vector2(460f, 320f));

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.10f, 0.13f, 0.88f);
            background.raycastTarget = true;

            TMP_Text heading = CreateUiText(
                panel, "DevHeading", new Vector2(12f, -8f), new Vector2(436f, 30f), UiFontSize, font);
            heading.color = new Color(1f, 0.82f, 0.35f, 1f);
            heading.text = DevMenuText.Title;

            Button godMode = CreateUiButton(panel, "DevGodMode", new Vector2(12f, -44f), new Vector2(210f, 44f), font, out TMP_Text godModeLabel);
            godModeLabel.text = DevMenuText.GodModeOff;

            Button addBia = CreateUiButton(panel, "DevAddBia", new Vector2(234f, -44f), new Vector2(214f, 44f), font, out TMP_Text addBiaLabel);
            addBiaLabel.text = DevMenuText.AddBia;

            Button spawnGhost = CreateUiButton(panel, "DevSpawnGhost", new Vector2(12f, -96f), new Vector2(210f, 44f), font, out TMP_Text spawnGhostLabel);
            spawnGhostLabel.text = DevMenuText.SpawnGhost;

            Button killAll = CreateUiButton(panel, "DevKillAll", new Vector2(234f, -96f), new Vector2(214f, 44f), font, out TMP_Text killAllLabel);
            killAllLabel.text = DevMenuText.KillAll;

            Button winNow = CreateUiButton(panel, "DevWinNow", new Vector2(12f, -148f), new Vector2(210f, 44f), font, out TMP_Text winNowLabel);
            winNowLabel.text = DevMenuText.WinNow;

            Button loseNow = CreateUiButton(panel, "DevLoseNow", new Vector2(234f, -148f), new Vector2(214f, 44f), font, out TMP_Text loseNowLabel);
            loseNowLabel.text = DevMenuText.LoseNow;

            TMP_Text status = CreateUiText(
                panel, "DevStatus", new Vector2(12f, -200f), new Vector2(436f, 60f), UiFontSize, font);
            status.text = DevMenuText.Disabled;

            DevMenuView view = canvasObject.AddComponent<DevMenuView>();
            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_runner", runner);
            SetObjectReference(view, "_panel", panel.gameObject);
            SetObjectReference(view, "_statusLabel", status);
            SetObjectReference(view, "_godModeLabel", godModeLabel);
            SetObjectReference(view, "_godModeButton", godMode);
            SetObjectReference(view, "_addBiaButton", addBia);
            SetObjectReference(view, "_spawnGhostButton", spawnGhost);
            SetObjectReference(view, "_killAllButton", killAll);
            SetObjectReference(view, "_winNowButton", winNow);
            SetObjectReference(view, "_loseNowButton", loseNow);

            // Authored hidden: the one build that must never show it is the one that cannot turn it
            // on, and a scene saved with the panel off says so in the scene itself.
            panel.gameObject.SetActive(false);
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

            TMP_Text timelineCaption = CreateUiText(bottom, "TimelineCaption", new Vector2(0f, -6f), new Vector2(700f, 30f), UiFontSize, font);
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

            TMP_Text spellsCaption = CreateUiText(bottom, "SpellsCaption", new Vector2(0f, -74f), new Vector2(1380f, 30f), UiFontSize, font);
            TMP_Text spellsValue = CreateUiText(bottom, "SpellsValue", new Vector2(0f, -106f), new Vector2(1380f, 30f), UiFontSize, font);

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

        // ------------------------------------------------------------------
        // Stage 09: the Camp scene
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds ศาลาพัก: the sala the player walks (ground, ยายจันทร์'s stall, the four shelf
        /// stations and the เซียมซี cylinder), the figure, the จัดย่าม bag screen, the bottom bar and
        /// the depart button.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything here is created through the Editor API (AGENTS rule 11) — no hand-written
        /// <c>.unity</c> YAML — so the scene is reproducible from the menu item and reviewable as
        /// code. Serialized references are wired through <c>SerializedProperty</c> rather than by
        /// guessing at file ids.
        /// </para>
        /// <para>
        /// <b>The shop is in the world, not in a panel.</b> Stage 09c moved the stall out of the UI:
        /// each shop slot is a shelf standing in the sala with its own price board and lock control,
        /// so there is no second list of offers in the report or on the screen. Everything the
        /// player needs without walking is still reachable without walking — a shelf can be rented
        /// by clicking it (what a phone and a browser test use) and the จัดย่าม button is on the
        /// canvas, not in the sala. The bag screen itself is the one thing that opens and closes,
        /// and it starts closed.
        /// </para>
        /// </remarks>
        private static void CreateCampScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = EnsureFontAssets();

            var scopeObject = new GameObject("Camp");
            CampSceneScope scope = scopeObject.AddComponent<CampSceneScope>();
            CampSceneDirector director = scopeObject.AddComponent<CampSceneDirector>();

            Camera camera = CreateCamera();
            camera.transform.position = new Vector3(0f, 7.2f, -12.7f);
            camera.transform.rotation = Quaternion.Euler(32f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = 6.6f;

            CreateEventSystem();

            // The sala is a view, not editor-authored sprites: it generates its own art at runtime
            // (AGENTS rule 13), so the scene carries no placeholder binaries. It is built once in
            // Awake and the camera follows the player underneath it.
            var worldObject = new GameObject("CampWorld");
            CampWorldView world = worldObject.AddComponent<CampWorldView>();
            SetObjectReference(world, "_scope", scope);
            SetObjectReference(world, "_camera", camera);
            SetObjectReference(world, "_font", font);

            // The walker: a figure the player drives with WASD, standing in the sala. It owns the
            // walking speed and the reach (both from data/tables/constant.csv, AGENTS rule 5).
            var playerObject = new GameObject("CampPlayer");
            CampPlayerView walker = playerObject.AddComponent<CampPlayerView>();
            SetObjectReference(walker, "_scope", scope);
            SetObjectReference(walker, "_world", world);

            Canvas canvas = CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            CampStatusBarView status = BuildCampStatusBar(canvasRect, font, scope);
            CampTooltipView tooltip = BuildCampTooltip(font, scope);
            CampBagScreenView bag = BuildCampBagScreen(canvasRect, canvasObject, font, scope);

            SetObjectReference(walker, "_bag", bag);
            SetObjectReference(director, "_scope", scope);
            SetObjectReference(director, "_world", world);
            SetObjectReference(director, "_player", walker);
            SetObjectReference(director, "_bag", bag);
            SetObjectReference(director, "_status", status);
            SetObjectReference(director, "_tooltip", tooltip);

            EditorSceneManager.SaveScene(scene, CampScenePath);
        }

        /// <summary>
        /// The จัดย่าม screen overlay: the board (left), the chest (right), the altar pot
        /// (bottom-centre), instructions (top-right), restore/lock with its checkbox (top-left),
        /// and navigation/stats (bottom-left).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every region is anchored to a <b>corner or edge of the canvas</b> — top-left, top-right,
        /// bottom-left, bottom-centre — and the canvas scaler (1920×1080, match 0.5) turns that into
        /// the same proportions at any window size, so no element is placed by an absolute screen
        /// pixel. The numbers below are in reference units and are measured rather than guessed: at
        /// 1920×1080 the rows are 20..106 from the top (the top-left controls), 140..720 from the top
        /// (the board) and 232..340, 214..320, 16..206 from the bottom (the panel's own foot, the
        /// pot and the status bar), the columns are 40..780, 822..1032 and 1104..1880, and no two of
        /// the regions the capture command measures intersect — at either size, which
        /// <c>artifacts/ui/ui-report.md</c> records.
        /// </para>
        /// <para>
        /// <b>Nothing here repeats the bottom bar.</b> Stage 09's panel carried its own เบี้ย/น้ำหนัก/HP
        /// readouts while the bar under it carried the same numbers, and the two disagreed the moment
        /// one of them was stale; the panel now has the controls the bar does not (กลับ, ไปชั้นถัดไป
        /// and the reason it is disabled) and the bar stays the only place a run stat is shown.
        /// </para>
        /// <para>
        /// <b>The blocks stand clear of each other and of the bar.</b> The panel's foot sits above the
        /// status bar rather than on it (Stage 09e work item 1: the buttons and the run's own numbers
        /// must not share pixels), and the incense pot sits in the column the bar does not reach —
        /// between the panel's foot (ends at x 780) and the bar's ออกเดินทาง button (starts at 1074) —
        /// so the pot cannot cover the one control that leaves the camp.
        /// </para>
        /// <para>
        /// Every label is ≥ 22 px at the 1080p base (Stage 09d item 2). Nothing here writes player
        /// text: the strings arrive over <c>CampUiState</c> from the localization table
        /// (AGENTS rule 5), and this method only places the widgets that will receive them.
        /// </para>
        /// </remarks>
        private static CampBagScreenView BuildCampBagScreen(
            RectTransform canvasRect, GameObject canvasObject, TMP_FontAsset font, CampSceneScope scope)
        {
            // Fullscreen overlay panel. Translucent rather than opaque: the layout is an overlay on
            // ศาลาพัก, so the camp has to stay visible behind it ("เห็นฉากอยู่ด้านหลัง").
            RectTransform panel = CreateUiRect(
                canvasRect, "BagPanel", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.pivot = new Vector2(0.5f, 0.5f);

            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.06f, 0.08f, 0.10f, 0.62f);
            panelImage.raycastTarget = true;

            // View is attached directly to the panel so it receives all graphic raycasts.
            CampBagScreenView view = panel.gameObject.AddComponent<CampBagScreenView>();

            // --- Top-left row (y 20..160): title, restore + its checkbox, lock, rotate, undo ------
            TMP_Text title = CreateUiText(panel, "BagTitle", new Vector2(40f, -20f), new Vector2(200f, 44f), 26f, font);
            Button reset = CreateUiButton(panel, "ResetButton", new Vector2(250f, -20f), new Vector2(150f, 44f), font, out TMP_Text resetLabel);
            Button lockButton = CreateUiButton(panel, "LockButton", new Vector2(415f, -20f), new Vector2(240f, 44f), font, out TMP_Text lockLabel);
            Button rotate = CreateUiButton(panel, "RotateButton", new Vector2(670f, -20f), new Vector2(90f, 44f), font, out TMP_Text rotateLabel);
            Button undo = CreateUiButton(panel, "UndoButton", new Vector2(775f, -20f), new Vector2(100f, 44f), font, out TMP_Text undoLabel);

            // ล็อกย่ามตอนคืนสภาพ sits directly under the restore button it modifies. A real Toggle,
            // not a caption that looks like a checkbox: the restore button reads this control's
            // value, so an unchecked box and a checked box are two different commands.
            Toggle lockRestore = CreateUiToggle(
                panel, "LockRestoreToggle", new Vector2(250f, -72f), new Vector2(30f, 30f), font,
                new Vector2(292f, -72f), new Vector2(400f, 34f), out TMP_Text lockRestoreLabel);

            // --- Top-right (y 20..180): the instruction scroll, four numbered steps --------------
            RectTransform instr = CreateUiRect(
                panel, "Instructions", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -20f), new Vector2(776f, 176f));
            var instrImage = instr.gameObject.AddComponent<Image>();
            instrImage.color = new Color(0.12f, 0.14f, 0.18f, 0.85f);
            instrImage.raycastTarget = false;

            TMP_Text instrTitle = CreateUiText(instr, "InstrTitle", new Vector2(12f, -8f), new Vector2(752f, 28f), UiFontSize, font);
            instrTitle.color = new Color(1f, 0.82f, 0.35f, 1f);

            TMP_Text step1 = CreateUiText(instr, "Step1", new Vector2(12f, -40f), new Vector2(752f, 28f), UiFontSize, font);
            TMP_Text step2 = CreateUiText(instr, "Step2", new Vector2(12f, -72f), new Vector2(752f, 28f), UiFontSize, font);
            TMP_Text step3 = CreateUiText(instr, "Step3", new Vector2(12f, -104f), new Vector2(752f, 28f), UiFontSize, font);
            TMP_Text step4 = CreateUiText(instr, "Step4", new Vector2(12f, -136f), new Vector2(752f, 28f), UiFontSize, font);

            // --- Left column (y 140..720 from the top): the board. Nine columns of 78 + a 4 gap =
            //     734 wide, seven rows = 574 tall -----------------------------------------------
            RectTransform board = CreateUiRect(
                panel, "Board", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -140f), new Vector2(740f, 580f));

            // The board's own background. Opaque enough that the sala behind the overlay cannot be
            // read through the grid ("พาเนล Board และหีบทึบพอ (alpha ≥ 0.9) ไม่ให้ฉากด้านหลังโผล่ผ่าน"):
            // the cells are drawn on top of this and leave a 4 px gap, and the gaps used to show the
            // 2.5D sala straight through the placement area.
            var boardImage = board.gameObject.AddComponent<Image>();
            boardImage.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);
            boardImage.raycastTarget = false;

            // --- Right column (y 200..600 from the top): the chest panel and its ten slots -------
            RectTransform chestPanel = CreateUiRect(
                panel, "ChestPanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -200f), new Vector2(776f, 400f));
            var chestPanelImage = chestPanel.gameObject.AddComponent<Image>();
            chestPanelImage.color = new Color(0.10f, 0.12f, 0.15f, 0.92f);
            chestPanelImage.raycastTarget = false;

            TMP_Text chestTitle = CreateUiText(chestPanel, "ChestTitle", new Vector2(16f, -10f), new Vector2(744f, 34f), UiFontSize, font);

            // 5 columns of 136 + a 16 gap = 744 wide, two rows = 288 tall.
            RectTransform chest = CreateUiRect(
                chestPanel, "Chest", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -52f), new Vector2(744f, 300f));

            // --- The free column above the bar (y 214..320, x 822..1032): the incense pot. It has
            //     to be above the status bar (which is 190 tall) and clear of both the panel's foot
            //     (ends at x 780) and the bar's ออกเดินทาง button (starts at x 1074), because a pot
            //     drawn over the depart button is a pot a player clicks instead of leaving camp -----
            RectTransform altar = CreateUiRect(
                panel, "Altar", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(822f, 214f), new Vector2(210f, 106f));
            var altarImage = altar.gameObject.AddComponent<Image>();
            altarImage.color = new Color(0.38f, 0.28f, 0.16f, 0.92f);
            altarImage.raycastTarget = false;

            TMP_Text altarCaption = CreateUiText(
                altar, "AltarCaption", new Vector2(6f, -4f), new Vector2(198f, 26f), UiFontSize, font, TextAlignmentOptions.Center);
            TMP_Text altarOffer = CreateUiText(
                altar, "AltarOffer", new Vector2(6f, -34f), new Vector2(198f, 66f), UiFontSize, font, TextAlignmentOptions.Center);

            // --- Bottom-left, above the bar (y 232..340): กลับ, ไปชั้นถัดไป and the reason the
            //     second one is disabled. The panel's foot used to be drawn straight over the status
            //     bar (y 24..214 against the bar's 16..206) with its own copy of the run's numbers ----
            RectTransform statsNav = CreateUiRect(
                panel, "StatsNav", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(40f, 232f), new Vector2(740f, 108f));

            Button closeButton = CreateUiButton(statsNav, "CloseButton", new Vector2(0f, 0f), new Vector2(130f, 50f), font, out TMP_Text closeLabel);
            Button nextFloorButton = CreateUiButton(statsNav, "NextFloorButton", new Vector2(142f, 0f), new Vector2(220f, 50f), font, out TMP_Text nextFloorLabel);

            // The reason the ไปชั้นถัดไป button is disabled ("ปิดพร้อมเหตุผลถ้าไม่มีอาวุธอาคม").
            TMP_Text departReason = CreateUiText(statsNav, "DepartReason", new Vector2(0f, -58f), new Vector2(740f, 28f), UiFontSize, font);
            departReason.color = new Color(0.95f, 0.45f, 0.45f, 1f);

            // The จัดย่าม button lives on the canvas, not in the panel, and is hidden while the
            // panel is open (the panel carries its own กลับ). Bottom-centre per the GDD, clear of
            // the bottom bar underneath it (x 24..984).
            Button open = CreateUiButton(
                canvasRect, "OpenBagButton", new Vector2(0f, 0f), new Vector2(220f, 64f), font, out TMP_Text openLabel);
            RectTransform openRect = open.GetComponent<RectTransform>();
            openRect.anchorMin = new Vector2(0.5f, 0f);
            openRect.anchorMax = new Vector2(0.5f, 0f);
            openRect.pivot = new Vector2(0.5f, 0f);
            openRect.anchoredPosition = new Vector2(0f, 222f);

            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_panelRoot", panel.gameObject);
            SetObjectReference(view, "_boardRect", board);
            SetObjectReference(view, "_chestRect", chest);
            SetObjectReference(view, "_chestPanelRect", chestPanel);
            SetObjectReference(view, "_chestTitleLabel", chestTitle);
            SetObjectReference(view, "_altarRect", altar);
            SetObjectReference(view, "_altarCaptionLabel", altarCaption);
            SetObjectReference(view, "_altarOfferLabel", altarOffer);
            SetObjectReference(view, "_statsRect", statsNav);
            SetObjectReference(view, "_font", font);
            SetObjectReference(view, "_openButton", open);
            SetObjectReference(view, "_openButtonLabel", openLabel);
            SetObjectReference(view, "_closeButton", closeButton);
            SetObjectReference(view, "_closeLabel", closeLabel);
            SetObjectReference(view, "_titleLabel", title);
            SetObjectReference(view, "_rotateButton", rotate);
            SetObjectReference(view, "_rotateLabel", rotateLabel);
            SetObjectReference(view, "_undoButton", undo);
            SetObjectReference(view, "_undoLabel", undoLabel);
            SetObjectReference(view, "_resetButton", reset);
            SetObjectReference(view, "_resetLabel", resetLabel);
            SetObjectReference(view, "_lockButton", lockButton);
            SetObjectReference(view, "_lockLabel", lockLabel);
            SetObjectReference(view, "_lockRestoreToggle", lockRestore);
            SetObjectReference(view, "_lockRestoreLabel", lockRestoreLabel);
            SetObjectReference(view, "_nextFloorButton", nextFloorButton);
            SetObjectReference(view, "_nextFloorLabel", nextFloorLabel);
            SetObjectReference(view, "_departReasonLabel", departReason);
            SetObjectReference(view, "_instructionsRect", instr);
            SetObjectReference(view, "_instructionsTitleLabel", instrTitle);
            SetObjectReference(view, "_instructionStep1Label", step1);
            SetObjectReference(view, "_instructionStep2Label", step2);
            SetObjectReference(view, "_instructionStep3Label", step3);
            SetObjectReference(view, "_instructionStep4Label", step4);
            return view;
        }

        /// <summary>
        /// A checkbox and its caption: a box, a check mark that fades in when it is on, and a label
        /// to the right of it.
        /// </summary>
        /// <remarks>
        /// A real <see cref="Toggle"/> rather than a label that reads like one, because the control it
        /// belongs to (คืนสภาพ) reads its value. The mark is authored at zero alpha so the saved scene
        /// shows the unchecked state the player starts in; <see cref="Toggle"/> drives it from then on.
        /// </remarks>
        private static Toggle CreateUiToggle(
            RectTransform parent, string name, Vector2 boxPosition, Vector2 boxSize, TMP_FontAsset font,
            Vector2 labelPosition, Vector2 labelSize, out TMP_Text label)
        {
            RectTransform box = CreateUiRect(
                parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), boxPosition, boxSize);

            var background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.18f, 0.20f, 0.24f, 1f);

            RectTransform mark = CreateUiRect(
                box, "Checkmark", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(5f, -5f), new Vector2(boxSize.x - 10f, boxSize.y - 10f));

            var markImage = mark.gameObject.AddComponent<Image>();
            markImage.color = new Color(0.95f, 0.82f, 0.35f, 1f);
            markImage.raycastTarget = false;
            markImage.canvasRenderer.SetAlpha(0f);

            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = markImage;
            toggle.isOn = false;

            label = CreateUiText(parent, name + "Label", labelPosition, labelSize, UiFontSize, font);
            label.text = string.Empty;
            return toggle;
        }

        /// <summary>
        /// The bottom bar: น้ำหนัก, สมาธิ, หีบ x/10, เบี้ย, ราคา refresh, the hint, the status line and
        /// the depart button with its reason.
        /// </summary>
        private static CampStatusBarView BuildCampStatusBar(RectTransform canvasRect, TMP_FontAsset font, CampSceneScope scope)
        {
            // Left-anchored and no wider than the gap the bag screen leaves: a bar that ran under
            // the bag panel would hide the depart button it is supposed to justify. It grew from 960
            // to 1400 in Stage 09d because the control hint has to read at 22 px, and Thai does not
            // break inside a run of syllables: the string is one unbreakable word as far as TMP is
            // concerned, so a box narrower than the text does not wrap it — it spills it.
            RectTransform panel = CreateUiRect(
                canvasRect, "StatusPanel", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 16f), new Vector2(1400f, 190f));

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.06f, 0.08f, 0.88f);

            // Row one: the run at a glance.
            TMP_Text floor = CreateUiText(panel, "FloorValue", new Vector2(10f, -6f), new Vector2(140f, 30f), UiFontSize, font);
            TMP_Text chestCaption = CreateUiText(panel, "ChestCaption", new Vector2(160f, -6f), new Vector2(90f, 30f), UiFontSize, font);
            TMP_Text chestValue = CreateUiText(panel, "ChestValue", new Vector2(252f, -6f), new Vector2(110f, 30f), UiFontSize, font);
            TMP_Text biaCaption = CreateUiText(panel, "BiaCaption", new Vector2(370f, -6f), new Vector2(80f, 30f), UiFontSize, font);
            TMP_Text biaValue = CreateUiText(panel, "BiaValue", new Vector2(452f, -6f), new Vector2(70f, 30f), UiFontSize, font);
            TMP_Text refreshCostCaption = CreateUiText(panel, "RefreshCostCaption", new Vector2(600f, -6f), new Vector2(190f, 30f), UiFontSize, font);
            TMP_Text refreshCostValue = CreateUiText(panel, "RefreshCostValue", new Vector2(794f, -6f), new Vector2(110f, 30f), UiFontSize, font);

            // Row two: the weight penalty and the focus rate, with the depart button on the right.
            TMP_Text weightCaption = CreateUiText(panel, "WeightCaption", new Vector2(10f, -40f), new Vector2(120f, 30f), UiFontSize, font);
            TMP_Text weightValue = CreateUiText(panel, "WeightValue", new Vector2(134f, -40f), new Vector2(260f, 30f), UiFontSize, font);
            TMP_Text focusCaption = CreateUiText(panel, "FocusCaption", new Vector2(404f, -40f), new Vector2(120f, 30f), UiFontSize, font);
            TMP_Text focusValue = CreateUiText(panel, "FocusValue", new Vector2(528f, -40f), new Vector2(160f, 30f), UiFontSize, font);
            TMP_Text departReason = CreateUiText(panel, "DepartReason", new Vector2(920f, -6f), new Vector2(460f, 64f), UiFontSize, font);

            // Row three: the penalty bar, the status line, the run's own HP/MP, and the depart button
            // itself. HP/MP used to be a second readout inside the จัดย่าม panel, which meant the camp
            // showed a stat twice and the panel's copy went stale as soon as a battle changed it; the
            // bar is now the only place a run stat appears (Stage 09e work item 1).
            RectTransform weightBar = CreateUiBar(panel, "WeightBar", new Vector2(10f, -76f), new Vector2(370f, 16f), new Color(0.72f, 0.58f, 0.30f, 1f));
            TMP_Text status = CreateUiText(panel, "StatusLabel", new Vector2(392f, -72f), new Vector2(300f, 40f), UiFontSize, font);
            TMP_Text hpMp = CreateUiText(panel, "HpMpLabel", new Vector2(700f, -72f), new Vector2(330f, 40f), UiFontSize, font);
            Button depart = CreateUiButton(panel, "DepartButton", new Vector2(1050f, -76f), new Vector2(320f, 64f), font, out TMP_Text departLabel);

            // Row four: the controls hint, across the bar — and *below* the depart button rather
            // than beside it, because at 22 px the hint is wider than the strip the button leaves.
            TMP_Text hint = CreateUiText(panel, "HintLabel", new Vector2(10f, -146f), new Vector2(1370f, 34f), UiFontSize, font);

            CampStatusBarView view = panel.gameObject.AddComponent<CampStatusBarView>();
            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_panelRect", panel);
            SetObjectReference(view, "_floorLabel", floor);
            SetObjectReference(view, "_weightCaptionLabel", weightCaption);
            SetObjectReference(view, "_weightValueLabel", weightValue);
            SetObjectReference(view, "_weightBar", weightBar);
            SetObjectReference(view, "_focusCaptionLabel", focusCaption);
            SetObjectReference(view, "_focusValueLabel", focusValue);
            SetObjectReference(view, "_chestCaptionLabel", chestCaption);
            SetObjectReference(view, "_chestValueLabel", chestValue);
            SetObjectReference(view, "_biaCaptionLabel", biaCaption);
            SetObjectReference(view, "_biaValueLabel", biaValue);
            SetObjectReference(view, "_refreshCostCaptionLabel", refreshCostCaption);
            SetObjectReference(view, "_refreshCostValueLabel", refreshCostValue);
            SetObjectReference(view, "_hintLabel", hint);
            SetObjectReference(view, "_statusLabel", status);
            SetObjectReference(view, "_hpMpLabel", hpMp);
            SetObjectReference(view, "_departReasonLabel", departReason);
            SetObjectReference(view, "_departButton", depart);
            SetObjectReference(view, "_departButtonLabel", departLabel);
            return view;
        }

        /// <summary>
        /// The piece tooltip: ชื่อ, ระดับ, สถานะ, คำอธิบาย, คอมโบที่ทำงานอยู่ — five lines on a canvas of
        /// its own that draws above the whole camp screen.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Its own <b>canvas</b>, not a panel of the camp's, for one measured reason: the camp screen
        /// is a single canvas, so "on top" there means "added last", and the board's cells, the piece
        /// rectangles and the combo lines are all created at runtime and appended after everything
        /// authored. A second root canvas with a higher <c>Canvas.sortingOrder</c> is the documented
        /// way to draw over another canvas rather than a race with creation order.
        /// </para>
        /// <para>
        /// The canvas carries <b>no</b> <c>GraphicRaycaster</c> and every graphic on it has
        /// <c>raycastTarget</c> off, so a tooltip that follows the pointer cannot swallow the pointer
        /// events that move it. The panel's background is 0.95 alpha, above the 0.92 the stage asks
        /// for, so the board is not readable through it.
        /// </para>
        /// </remarks>
        private static CampTooltipView BuildCampTooltip(TMP_FontAsset font, CampSceneScope scope)
        {
            Canvas canvas = CreateCanvas(out GameObject canvasObject, TooltipSortingOrder, withRaycaster: false);
            var canvasRect = (RectTransform)canvas.transform;

            // Anchored to the canvas' bottom-left corner: the placement rule (CampTooltipPlacement)
            // writes this rect's anchoredPosition in the same units, so what a test measures is what
            // the view set. The initial position is the margin, which is where an unhovered tooltip
            // would sit if anything showed it.
            RectTransform panel = CreateUiRect(
                canvasRect, "TooltipPanel", Vector2.zero, Vector2.zero,
                new Vector2(CampTooltipPlacement.Margin, CampTooltipPlacement.Margin),
                new Vector2(440f, 330f));

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.07f, 0.95f);
            background.raycastTarget = false;

            TMP_Text title = CreateUiText(panel, "TooltipTitle", new Vector2(14f, -10f), new Vector2(412f, 34f), UiFontSize + 4f, font);
            title.color = new Color(1f, 0.85f, 0.45f, 1f);
            TMP_Text tier = CreateUiText(panel, "TooltipTier", new Vector2(14f, -46f), new Vector2(412f, 28f), UiFontSize, font);
            TMP_Text status = CreateUiText(panel, "TooltipStatus", new Vector2(14f, -78f), new Vector2(412f, 30f), UiFontSize, font);
            TMP_Text description = CreateUiText(panel, "TooltipDescription", new Vector2(14f, -114f), new Vector2(412f, 96f), UiFontSize, font);
            description.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            TMP_Text combos = CreateUiText(panel, "TooltipCombos", new Vector2(14f, -214f), new Vector2(412f, 106f), UiFontSize, font);
            combos.color = new Color(0.95f, 0.82f, 0.35f, 1f);

            CampTooltipView view = panel.gameObject.AddComponent<CampTooltipView>();
            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_root", panel.gameObject);
            SetObjectReference(view, "_titleLabel", title);
            SetObjectReference(view, "_tierLabel", tier);
            SetObjectReference(view, "_statusLabel", status);
            SetObjectReference(view, "_descriptionLabel", description);
            SetObjectReference(view, "_combosLabel", combos);

            panel.gameObject.SetActive(false);
            return view;
        }

        /// <summary>A button with a centred label, laid out from its parent's top-left corner.</summary>
        /// <summary>
        /// A button on <paramref name="parent"/>, laid out from its top-left corner unless
        /// <paramref name="anchor"/> says otherwise.
        /// </summary>
        /// <param name="anchor">
        /// The point of the parent the button is placed against, used as both its anchor and its pivot,
        /// so that <paramref name="anchoredPosition"/> is the offset of its <b>centre</b> when it is
        /// <c>(0.5, 0.5)</c>. The default is the parent's top-left corner, which is what every panel and
        /// column in this project lays its controls out from.
        /// </param>
        private static Button CreateUiButton(
            RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size,
            TMP_FontAsset font, out TMP_Text label, Vector2? anchor = null)
        {
            Vector2 corner = anchor ?? new Vector2(0f, 1f);

            RectTransform rect = CreateUiRect(
                parent, name, corner, corner, anchoredPosition, size);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.25f, 0.26f, 0.30f, 1f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = CreateUiText(rect, "Label", Vector2.zero, size, UiFontSize, font, TextAlignmentOptions.Center);
            return button;
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
        // Stage 10: the Result scene
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the summary scene: the scope that shows a finished run, one centred column of
        /// summary lines, and the two ways out (Restart, back to the title).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Everything is created through the Editor API (AGENTS rule 11) — no hand-written
        /// <c>.unity</c> YAML — and every string on the screen arrives at runtime over
        /// <c>ResultUiState</c> from the localization table (AGENTS rule 5), so this method only
        /// places nine empty widgets and wires the references the view needs.
        /// </para>
        /// <para>
        /// <b>One centred column, measured rather than guessed.</b> The <c>Summary</c> rect is
        /// centred on the canvas (anchor and pivot both 0.5) and 1200×1000 at the 1920×1080
        /// reference, so nothing here is positioned in absolute screen pixels: at 1280×720 the
        /// column keeps the same proportions and the same gaps between lines. The rows are 60 px
        /// apart with 44 px line boxes and the headings are 96 px, all above the 22 px Thai floor
        /// (Stage 09d) — a 32 px line box holds a Thai line with its upper and lower marks inside
        /// the box instead of clipping them.
        /// </para>
        /// <para>
        /// The two buttons are 400×76 and centred in the column: the same touch target the camp
        /// author their panel controls at, so the summary screen cannot be the one screen whose
        /// buttons are too small to press on a phone.
        /// </para>
        /// </remarks>
        private static void CreateResultScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = EnsureFontAssets();

            var scopeObject = new GameObject("Result");
            ResultSceneScope scope = scopeObject.AddComponent<ResultSceneScope>();
            ResultSceneDirector director = scopeObject.AddComponent<ResultSceneDirector>();

            CreateCamera();
            CreateEventSystem();

            Canvas canvas = CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform column = CreateUiRect(
                canvasRect, "Summary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 1000f));

            // The column carries a background of its own, at the camp's panel alpha (0.92 — the value
            // `ProjectSurvive/Tools/1. Capture UI` measures on the camp's Board, ChestPanel and Altar,
            // and the one `UiLayoutCheck.OpaqueFloor` names for a panel the reader must not see through).
            // It is not the จัดย่าม screen's 0.62 full-screen overlay: that one is translucent so ศาลาพัก
            // stays readable behind it, and over this scene's flat clear colour 0.62 is invisible
            // (0.06/0.08/0.10 at 62 % over 13/15/20 is a 0.5 %-per-channel shift). A summary is a column
            // of text, so it gets the panel treatment the other screens' panels get.
            var columnBackground = column.gameObject.AddComponent<Image>();
            columnBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);

            // The buttons answer their own clicks, so the column behind them must not swallow one: the
            // camp's own non-blocking panels (Instructions, Board, ChestPanel) are authored the same way.
            columnBackground.raycastTarget = false;

            TMP_Text headline = CreateUiText(
                column, "Headline", new Vector2(0f, -40f), new Vector2(1200f, 140f), 96f, font,
                TextAlignmentOptions.Center);

            TMP_Text floor = CreateUiText(
                column, "FloorLine", new Vector2(0f, -200f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text kills = CreateUiText(
                column, "KillsLine", new Vector2(0f, -260f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text bia = CreateUiText(
                column, "BiaLine", new Vector2(0f, -320f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text hp = CreateUiText(
                column, "HpLine", new Vector2(0f, -380f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            Button restart = CreateUiButton(
                column, "RestartButton", new Vector2(400f, -560f), new Vector2(400f, 76f), font,
                out TMP_Text restartLabel);

            Button title = CreateUiButton(
                column, "TitleButton", new Vector2(400f, -660f), new Vector2(400f, 76f), font,
                out TMP_Text titleLabel);

            ResultView view = canvasObject.AddComponent<ResultView>();
            SetObjectReference(view, "_scope", scope);
            SetObjectReference(view, "_headlineLabel", headline);
            SetObjectReference(view, "_floorLabel", floor);
            SetObjectReference(view, "_killsLabel", kills);
            SetObjectReference(view, "_biaLabel", bia);
            SetObjectReference(view, "_hpLabel", hp);
            SetObjectReference(view, "_restartButton", restart);
            SetObjectReference(view, "_titleButton", title);
            SetObjectReference(view, "_restartLabel", restartLabel);
            SetObjectReference(view, "_titleLabel", titleLabel);

            SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(scene, ResultScenePath);
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
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateCanvas(out GameObject canvasObject) =>
            CreateCanvas(out canvasObject, sortingOrder: 0, withRaycaster: true);

        /// <summary>
        /// A screen-space canvas with the project's scaler: 1920×1080 reference, match 0.5, so a
        /// region anchored to a corner keeps the same proportions at every window size.
        /// </summary>
        /// <param name="sortingOrder">
        /// Draw order among the screen's overlay canvases. The engine documents this as
        /// <i>"Canvas' order within a sorting layer"</i> (<c>UnityEngine.UIModule.xml</c>, Unity
        /// 6000.3.9f1): a higher number draws later, on top.
        /// </param>
        /// <param name="withRaycaster">
        /// False for a canvas that must never receive a click. A tooltip that follows the pointer has
        /// to be the last thing drawn and the first thing ignored, and without a
        /// <c>GraphicRaycaster</c> its graphics are not hit-tested at all.
        /// </param>
        private static Canvas CreateCanvas(out GameObject canvasObject, int sortingOrder, bool withRaycaster)
        {
            canvasObject = new GameObject(sortingOrder == 0 ? "UI" : "UI" + sortingOrder);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (withRaycaster)
            {
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            return canvas;
        }

        /// <summary>
        /// A centred label on <paramref name="canvas"/>, <paramref name="anchoredPosition"/> being the
        /// offset of its <b>centre</b> from the canvas' centre.
        /// </summary>
        /// <param name="size">
        /// The label's box. The default is a roomy 1800×200 placeholder; a screen that is
        /// <i>measured</i> passes the line box it really occupies, because a rectangle three times the
        /// height of the line it holds reports a collision that is not on screen (the title screen's
        /// own heading and build line did exactly that, which is why the parameter is here).
        /// </param>
        private static TMP_Text CreateTmpLabel(
            Canvas canvas, string name, Vector2 anchoredPosition, float fontSize, TMP_FontAsset font,
            Vector2? size = null)
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
            rect.sizeDelta = size ?? new Vector2(1800f, 200f);

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

        private static void SetVector3(Object target, string fieldName, Vector3 value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.vector3Value = value;
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
