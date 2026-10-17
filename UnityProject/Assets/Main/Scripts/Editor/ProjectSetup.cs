using System;
using System.Globalization;
using System.IO;
using ProjectSurvive.Game.Presentation;
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

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Creates everything the shell needs that only the Editor can create: the URP
    /// assets, the WebGL player settings, the runtime UI theme and font assets, the
    /// build list — and the building blocks every scene is assembled from.
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
    /// <b>One builder file per scene (Stage 10t).</b> Each scene's construction lives
    /// in its own <c>*SceneBuilder</c> next to this file — <c>BootSceneBuilder</c>,
    /// <c>TitleSceneBuilder</c>, <c>ThaiTextSpikeSceneBuilder</c>,
    /// <c>HubSceneBuilder</c>, <c>EncounterSceneBuilder</c>, <c>ResultSceneBuilder</c> —
    /// together with the menu items that build it. A scene can therefore be removed
    /// (or left out of a template branch) by deleting one file, and the helpers here
    /// are the vocabulary those builders share: a scope object, a camera, an event
    /// system, a canvas, a label, a button, a stretched panel. Nothing in this file
    /// names a concrete scene's content.
    /// </para>
    /// <para>
    /// The setup entry points are idempotent: they reuse whatever already exists and
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

        /// <summary>The loop's preparation screen (between encounters).</summary>
        public const string HubScenePath = ScenesFolder + "/Hub.unity";

        /// <summary>The loop's unit-of-play screen.</summary>
        public const string EncounterScenePath = ScenesFolder + "/Encounter.unity";

        /// <summary>The run-summary scene.</summary>
        public const string ResultScenePath = ScenesFolder + "/Result.unity";

        /// <summary>
        /// Scenes a page URL may request with <c>?scene=&lt;name&gt;</c>. One list, used both when
        /// the Boot scene is created and when a scene that should be reachable is added, so the two
        /// cannot drift.
        /// </summary>
        public static readonly string[] UrlSceneAllowList = { "ThaiTextSpike", "Hub", "Encounter", "Result" };

        private const string SourceFontFile = FontsFolder + "/Sarabun-Regular.ttf";

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
        public const float UiFontSize = 22f;

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

        /// <summary>Saves the assets, refreshes the database and logs one line. Called by every scene menu item.</summary>
        public static void SaveAndRefresh(string message)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(message);
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

        /// <summary>
        /// Returns both baked font assets, creating and baking them when they are missing.
        /// </summary>
        public static (TMP_FontAsset tmp, FontAsset textCore) EnsureFontAssets()
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
        // The build list
        // ------------------------------------------------------------------

        /// <summary>
        /// Rewrites the build list from the scenes that exist on disk.
        /// </summary>
        /// <remarks>
        /// Boot, Title and ThaiTextSpike are always listed; Hub, Encounter and Result join once their
        /// file exists, so an older checkout keeps a build list of scenes that are really there and
        /// a departure to a missing scene is refused with a typed error instead of attempted.
        /// </remarks>
        public static void ConfigureBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(SpikeScenePath, true),
            };

            if (File.Exists(HubScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(HubScenePath, true));
            }

            if (File.Exists(EncounterScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(EncounterScenePath, true));
            }

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
        /// re-run the Boot menu item to get <c>Hub</c> into it. Doing it here means creating a
        /// scene is enough to make it reachable — the two halves of the feature (a scene that
        /// publishes its own name, and an allow-list that permits it) stay in step without a second
        /// manual step nobody remembers.
        /// </remarks>
        public static void RefreshUrlSceneAllowList()
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
        // Scene building blocks
        // ------------------------------------------------------------------

        /// <summary>Creates the scope object every scene except Boot is built around.</summary>
        public static RootLifetimeScope CreateScopeObject()
        {
            var scopeObject = new GameObject("RootLifetimeScope");
            return scopeObject.AddComponent<RootLifetimeScope>();
        }

        /// <summary>The shell's camera: orthographic, the project's clear colour, URP camera data attached.</summary>
        public static Camera CreateCamera()
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

        /// <summary>The scene's event system: uGUI's, driven by the Input System module.</summary>
        public static void CreateEventSystem()
        {
            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>A screen-space canvas at the project's draw order 0, with a raycaster.</summary>
        public static Canvas CreateCanvas(out GameObject canvasObject) =>
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
        public static Canvas CreateCanvas(out GameObject canvasObject, int sortingOrder, bool withRaycaster)
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
        public static TMP_Text CreateTmpLabel(
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
        // UI building blocks
        // ------------------------------------------------------------------

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
        public static Button CreateUiButton(
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
        public static RectTransform CreateUiRect(
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
        public static TMP_Text CreateUiText(
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
        /// Creates a rectangle stretched between two anchors, with explicit insets from each side.
        /// </summary>
        /// <remarks>
        /// The Stage 08c HUD is built out of these: a panel is authored as a <i>band</i> of the
        /// canvas ("a quarter of the width, from 45 % of the height up") rather than as a pixel
        /// rectangle, so the arrangement survives the two sizes the GDD names. The old
        /// <see cref="CreateUiRect"/> is still what an absolutely-placed label inside a panel uses.
        /// </remarks>
        public static RectTransform StretchUiRect(
            RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, worldPositionStays: false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>A label stretched between two anchors, with the same 22 px floor as the rest of the HUD.</summary>
        public static TMP_Text StretchUiText(
            RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, float fontSize, TMP_FontAsset font,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            RectTransform rect = StretchUiRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;
            label.text = string.Empty;
            return label;
        }

        /// <summary>Gives a panel the battle HUD's own background; it never swallows a click.</summary>
        public static void AddPanelBackground(RectTransform panel, Color colour)
        {
            var background = panel.gameObject.AddComponent<Image>();
            background.color = colour;
            background.raycastTarget = false;
        }

        /// <summary>
        /// A track stretched between two anchors and its fill, returning the fill.
        /// </summary>
        /// <remarks>
        /// The stretched form of <see cref="CreateUiBar"/>: the fill is anchored to the track's left
        /// edge, so the HUD sets a bar by writing <c>anchorMax.x</c> and no sprite asset is needed
        /// anywhere in the UI — and the bar is as wide as the panel it lives in, at both sizes.
        /// </remarks>
        public static RectTransform StretchUiBar(
            RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color fillColour)
        {
            RectTransform track = StretchUiRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0f, 0f, 0f, 0.5f);
            trackImage.raycastTarget = false;

            RectTransform fill = StretchUiRect(
                track, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fill.pivot = new Vector2(0f, 0.5f);

            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = fillColour;
            fillImage.raycastTarget = false;
            return fill;
        }

        /// <summary>
        /// Creates a track and its fill, and returns the fill.
        /// </summary>
        /// <remarks>
        /// The fill is a rectangle anchored to the track's left edge, so the HUD sets a bar by
        /// writing <c>anchorMax.x</c> and no sprite asset is needed anywhere in the UI.
        /// </remarks>
        public static RectTransform CreateUiBar(
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
        // Serialized-field helpers
        // ------------------------------------------------------------------

        public static void SetObjectReference(Object target, string fieldName, Object value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.objectReferenceValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string fieldName, bool value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.boolValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string fieldName, string value)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.stringValue = value;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string fieldName, int value)
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
        public static void SetEnum(Object target, string fieldName, int index)
        {
            SerializedProperty property = FindProperty(target, fieldName);
            property.enumValueIndex = index;
            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Replaces a string-array field, e.g. the Boot scene's scene allow-list.</summary>
        public static void SetStringArray(Object target, string fieldName, string[] values)
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

        /// <summary>Creates <paramref name="path"/> and any missing folder above it.</summary>
        public static void EnsureFolder(string path)
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
