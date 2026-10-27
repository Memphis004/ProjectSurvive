using System;
using System.Collections.Generic;
using System.IO;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Generates every asset the arena is made of: the placeholder textures, their materials, the two
    /// quad meshes, the <c>ArenaCard</c> and <c>GroundDecal</c> prefabs, the <see cref="ArenaTheme"/>
    /// and its day/night profiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The art is generated rather than imported, for the same reason Stage 08's battle art is:
    /// placeholder pixels must be licensed for use and must not be copied from a referenced game
    /// (AGENTS rule 13). Everything here is a function of this file, so there is nothing to
    /// attribute and no third-party asset in the repository. The textures are saved as PNG assets
    /// because the cards are prefab instances with material references; a texture generated at
    /// runtime could not be referenced by a saved prefab.
    /// </para>
    /// <para>
    /// Every texture is authored in <b>daylight colours</b>. The night look is the
    /// <see cref="ArenaLightingProfile"/>'s global tint and grade, never a second set of assets —
    /// which is the stage's rule and what makes the day/night switch one call.
    /// </para>
    /// <para>
    /// The whole pipeline is idempotent: running it twice leaves the same assets (same paths, same
    /// GUIDs, same pixels) and the same theme, so a rebuild never duplicates a material or a prefab.
    /// </para>
    /// </remarks>
    public static class ArenaArtBuilder
    {
        public const string ArenaRoot = "Assets/Main/Art/Arena";
        public const string TextureFolder = ArenaRoot + "/Textures";
        public const string MaterialFolder = ArenaRoot + "/Materials";
        public const string MeshFolder = ArenaRoot + "/Meshes";
        public const string PrefabFolder = "Assets/Main/Prefabs/Arena";
        public const string ContentFolder = "Assets/Main/Content/Arena";

        public const string CardPrefabPath = PrefabFolder + "/ArenaCard.prefab";
        public const string DecalPrefabPath = PrefabFolder + "/GroundDecal.prefab";
        public const string CardShadowPrefabPath = PrefabFolder + "/ArenaCardShadow.prefab";
        public const string CardQuadPath = MeshFolder + "/ArenaCardQuad.asset";
        public const string ShadowQuadPath = MeshFolder + "/ArenaShadowQuad.asset";
        public const string ThemePath = ContentFolder + "/ArenaTheme.asset";
        public const string DuskProfilePath = ContentFolder + "/ArenaLightingDusk.asset";
        public const string NightProfilePath = ContentFolder + "/ArenaLightingNight.asset";
        public const string ScreenGradientSpritePath = TextureFolder + "/screen-gradient.png";

        /// <summary>
        /// The mid-field haze's alpha ramp: one soft band across the vertical middle of the frame,
        /// transparent at both ends (<c>screen-mid-band.png</c>). A separate sprite from the top
        /// band's because that one is a ramp and this one is a bump — the same texture cannot be both.
        /// </summary>
        public const string ScreenMidBandSpritePath = TextureFolder + "/screen-mid-band.png";

        private const string CardShader = "ProjectSurvive/ArenaCard";
        private const string DecalShader = "ProjectSurvive/ArenaDecal";
        private const string DirtShader = "ProjectSurvive/ArenaDirt";
        private const string GlowShader = "ProjectSurvive/ArenaGlow";
        private const string GroundShader = "ProjectSurvive/ArenaGround";
        private const string ShadowShader = "ProjectSurvive/ArenaShadow";

        /// <summary>
        /// Render queue of the floor: the opaque group, so it is drawn before everything that
        /// stands on it (decals 2950, card shadows 2960, glow 2970, blob shadows 2990, actors
        /// 3000). It writes no depth — ADR 0038 — so this queue is the whole of "the floor is
        /// underneath".
        /// </summary>
        public const int GroundQueue = 2000;

        /// <summary>Render queue of the ground decals: above the floor, below the shadows.</summary>
        public const int DecalQueue = 2950;

        /// <summary>
        /// Render queue of the standing cards: the same transparent group as the actor sprites, so
        /// the cards and the actors are ordered against each other by sorting order alone
        /// (<c>BattleWorldMap.SortingOrder</c>) and never by the camera's depth buffer.
        /// </summary>
        public const int CardQueue = 3000;

        /// <summary>Render queue of the additive light pools: above the decals, below the actors.</summary>
        public const int GlowQueue = 2970;

        /// <summary>Render queue of the instanced blob shadows: above decals, below actors.</summary>
        public const int ShadowQueue = 2990;

        /// <summary>
        /// Render queue of the fake card shadows: above the ground decals, below the light pools and
        /// every actor. Their sorting order is <c>BattleWorldMap.ShadowSortingOrder</c>.
        /// </summary>
        public const int CardShadowQueue = 2960;

        // Daylight palette. Cosmetic only; nothing here is a balance number.
        //
        // Stage 08f moved the palette from a cool forest green to the warm green-yellow a jungle
        // reads as at dusk, and made the small accents red-orange. Every texture here is still a
        // daylight asset: the Dusk/Night difference is the lighting profile's tint and grade alone.
        private static readonly Color LeafDark = new Color(0.24f, 0.33f, 0.15f, 1f);
        private static readonly Color LeafMid = new Color(0.40f, 0.50f, 0.22f, 1f);
        private static readonly Color LeafLight = new Color(0.60f, 0.66f, 0.30f, 1f);
        private static readonly Color Bark = new Color(0.30f, 0.22f, 0.14f, 1f);
        private static readonly Color GrassPale = new Color(0.60f, 0.66f, 0.30f, 1f);
        private static readonly Color DirtWarm = new Color(0.38f, 0.31f, 0.20f, 1f);
        private static readonly Color FlowerWhite = new Color(0.96f, 0.93f, 0.70f, 1f);

        // The one hot colour in the arena. A handful of red-orange blossoms and the two ember pools
        // are what makes the warm highlights read against the green; nothing else in the palette is
        // saturated red.
        private static readonly Color FlowerWarm = new Color(0.95f, 0.33f, 0.16f, 1f);
        private static readonly Color EmberWarm = new Color(1.00f, 0.36f, 0.14f, 1f);

        /// <summary>
        /// The colour of every additive light pool on the floor (Stage 08g): a saturated pink-red
        /// (hue ≈ 348°) instead of the soft amber the arena shipped with, so a lit patch reads as
        /// candle-and-paper-lantern light in a haunted clearing rather than as sunlight on grass.
        /// </summary>
        private static readonly Color LightPoolColour = new Color(1.00f, 0.13f, 0.34f, 1f);
        private static readonly Color LeafAutumn = new Color(0.70f, 0.42f, 0.16f, 1f);

        /// <summary>The small blue-white floating lights: cool, so they never read as fire.</summary>
        private static readonly Color MoteCool = new Color(0.72f, 0.88f, 1.00f, 1f);

        /// <summary>Builds every asset. Returns the theme asset, ready to lay out.</summary>
        public static ArenaTheme BuildAll()
        {
            EnsureFolders();

            // Stage 08g: the sizes are a **screen** budget, not a taste. The stage's camera is
            // orthographic with a half-height of 8.889 world units (`BattleCameraRig.OrthographicSize`,
            // derived from "the player is 9 % of the screen"), so a 1080 px frame is 60.75 px per world
            // unit. A card is a billboard turned square-on to that camera (`ArenaCard.FacingCamera`),
            // so it is drawn at its own size on both axes — no foreshortening to discount: the widest
            // card the theme may place is 7.6 units (462 px) and the tallest 7.92 (481 px), against the
            // 512 px sources below. `ArenaSharpnessTests` holds that invariant instead of this comment.
            Texture2D treeA = GenerateTexture("tree-a", 512, TreeA);
            Texture2D treeB = GenerateTexture("tree-b", 512, TreeB);
            Texture2D treeC = GenerateTexture("tree-c", 512, TreeC);
            Texture2D bush = GenerateTexture("bush", 256, Bush);
            Texture2D grass = GenerateTexture("grass", 128, Grass);
            Texture2D grassSmall = GenerateTexture("grass-small", 128, GrassSmall);
            Texture2D dirt = GenerateTexture("dirt", 512, Dirt);
            Texture2D flower = GenerateTexture("flower", 128, Flower);
            Texture2D leaf = GenerateTexture("leaf", 128, Leaf);
            Texture2D ground = GenerateTexture("ground-macro", 512, Ground);
            Texture2D pool = GenerateTexture("light-pool", 128, LightPool);
            Texture2D glow = GenerateTexture("lantern-glow", 64, Glow);
            Texture2D shadow = GenerateTexture("blob-shadow", 64, BlobShadow);
            Texture2D mote = GenerateTexture("mote", 32, Mote);
            Sprite screenGradient = GenerateSprite(ScreenGradientSpritePath, "screen-gradient", 4, 128, ScreenGradient);
            Sprite screenMidBand = GenerateSprite(ScreenMidBandSpritePath, "screen-mid-band", 4, 128, ScreenMidBand);

            Material cardA = BuildMaterial("card-tree-a", CardShader, treeA, Color.white, CardQueue);
            Material cardB = BuildMaterial("card-tree-b", CardShader, treeB, Color.white, CardQueue);
            Material cardC = BuildMaterial("card-tree-c", CardShader, treeC, Color.white, CardQueue);
            Material cardBush = BuildMaterial("card-bush", CardShader, bush, Color.white, CardQueue);
            Material cardGrass = BuildMaterial("card-grass", CardShader, grass, Color.white, CardQueue);
            Material cardGrassSmall = BuildMaterial("card-grass-small", CardShader, grassSmall, Color.white, CardQueue);

            // The dirt patch multiplies the floor instead of blending over it: a soft, dark patch that
            // can never lift the floor the ghosts are read against (see ArenaDirt.shader).
            // A patch has to be a change of ground, not a hole in it: a strong multiply at night reads
            // as smoke hanging over the floor. This tint takes a handful of percent of the light away
            // and no more, which is what keeps the dirt from competing with the ghosts for the eye.
            Material dirtMat = BuildMaterial("decal-dirt", DirtShader, dirt, new Color(0.88f, 0.84f, 0.76f, 0.55f), DecalQueue);
            Material flowerMat = BuildMaterial("decal-flower", DecalShader, flower, new Color(1f, 1f, 1f, 0.95f), DecalQueue);
            Material leafMat = BuildMaterial("decal-leaf", DecalShader, leaf, new Color(1f, 1f, 1f, 0.9f), DecalQueue);
            Material groundMat = BuildMaterial("ground", GroundShader, ground, Color.white, GroundQueue);
            // Stage 08g: the light pools are the arena's one hot colour, and it is now a hard
            // pink-red rather than a soft warm orange. The profile still decides how strongly they
            // show (dusk 0.15, night 0.42 of this tint), so one number serves both looks.
            Material poolMat = BuildMaterial("light-pool", GlowShader, pool, LightPoolColour, GlowQueue);
            Material emberMat = BuildMaterial("ember-pool", GlowShader, pool, EmberWarm * 1.35f, GlowQueue);
            Material glowMat = BuildMaterial("lantern-glow", GlowShader, glow, new Color(1f, 0.88f, 0.66f, 1f), GlowQueue);
            // The motes are authored brighter than 1.0 so what the profile adds crosses the Bloom
            // threshold in both looks: they read as small lights, not as pale dots painted on the air.
            Material moteMat = BuildMaterial("mote", GlowShader, mote, MoteCool * 3.2f, GlowQueue);
            Material shadowMat = BuildMaterial("blob-shadow", ShadowShader, shadow, Color.white, ShadowQueue);
            Material cardShadowMat = BuildMaterial("card-shadow", ShadowShader, shadow, Color.white, CardShadowQueue);

            // The light-pool channel drives the pools; the glow channel drives the lantern and the
            // motes. One shader, two profile numbers, no material swap when the profile changes.
            SetChannel(poolMat, poolChannel: 1f, glowChannel: 0f);
            SetChannel(emberMat, poolChannel: 1f, glowChannel: 0f);
            SetChannel(glowMat, poolChannel: 0f, glowChannel: 1f);
            SetChannel(moteMat, poolChannel: 0f, glowChannel: 1f);

            // The actor shadows and the card shadows are each drawn in one DrawMeshInstanced call, and
            // Unity refuses that call on a material with instancing off.
            shadowMat.enableInstancing = true;
            cardShadowMat.enableInstancing = true;
            EditorUtility.SetDirty(shadowMat);
            EditorUtility.SetDirty(cardShadowMat);

            Mesh cardQuad = BuildMesh(CardQuadPath, ArenaGeometry.CreateBasePivotedQuad);
            Mesh shadowQuad = BuildMesh(ShadowQuadPath, ArenaGeometry.CreateCenteredQuad);
            BuildPrefab(CardPrefabPath, "ArenaCard", cardQuad, cardA, ArenaCardZone.Border);
            BuildPrefab(DecalPrefabPath, "GroundDecal", shadowQuad, dirtMat, ArenaCardZone.Decal);
            BuildCardShadowPrefab(shadowQuad, cardShadowMat);

            ArenaLightingProfile dusk = BuildDuskProfile();
            ArenaLightingProfile night = BuildNightProfile();
            ArenaTheme theme = BuildTheme(dusk, night, groundMat, shadowMat);

            // SaveAssets, deliberately not Refresh: a Refresh reimports and can reload the very theme
            // instance this method is about to hand back, which would return a destroyed object to the
            // scene builder.
            AssetDatabase.SaveAssets();

            return theme;
        }

        /// <summary>The card prefab's mesh (base-pivoted quad). Read by the builder and tests.</summary>
        public static Mesh LoadCardQuad() => AssetDatabase.LoadAssetAtPath<Mesh>(CardQuadPath);

        /// <summary>The centred quad used by the floor and the shadow layer.</summary>
        public static Mesh LoadShadowQuad() => AssetDatabase.LoadAssetAtPath<Mesh>(ShadowQuadPath);

        private static void EnsureFolders()
        {
            ProjectSetup.EnsureFolder(TextureFolder);
            ProjectSetup.EnsureFolder(MaterialFolder);
            ProjectSetup.EnsureFolder(MeshFolder);
            ProjectSetup.EnsureFolder(PrefabFolder);
            ProjectSetup.EnsureFolder(ContentFolder);
        }

        // ------------------------------------------------------------------
        // materials, meshes, prefabs
        // ------------------------------------------------------------------

        private static Material BuildMaterial(
            string name, string shaderName, Texture2D texture, Color tint, int queue)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader is null)
            {
                throw new InvalidOperationException(
                    "The arena shader '" + shaderName + "' is missing from the project.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material is null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", tint);
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Selects which lighting-profile number drives one glow material: the ground pools listen to
        /// the light-pool intensity, the lantern and the motes to the glow intensity.
        /// </summary>
        private static void SetChannel(Material material, float poolChannel, float glowChannel)
        {
            material.SetFloat("_PoolChannel", poolChannel);
            material.SetFloat("_GlowChannel", glowChannel);
            EditorUtility.SetDirty(material);
        }

        private static Mesh BuildMesh(string path, Func<Mesh> factory)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh source = factory();

            if (mesh is null)
            {
                source.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(source, path);
                return source;
            }

            mesh.vertices = source.vertices;
            mesh.uv = source.uv;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            EditorUtility.SetDirty(mesh);
            Object.DestroyImmediate(source);
            return mesh;
        }

        /// <summary>
        /// The prefab every placed card is an instance of. Its pivot is the bottom centre of the quad
        /// and it is <see cref="StaticEditorFlags.OccludeeStatic"/> but <b>not</b> batching-static,
        /// because static batching draws a card through a combined mesh and ignores the per-renderer
        /// <c>MaterialPropertyBlock</c> the occlusion fade writes. The arena never moves either way;
        /// each card already carries its own sorting order, so batching could not have merged them.
        /// </summary>
        /// <summary>
        /// The prefab every fake card shadow is an instance of: one quad lying on the floor with the
        /// multiply shadow shader. Unlike a card it needs no occlusion dither, so it is batching
        /// static — every shadow in the arena shares one material and one sorting order, which is what
        /// keeps 140 of them inside a single draw call.
        /// </summary>
        private static void BuildCardShadowPrefab(Mesh mesh, Material material)
        {
            var go = new GameObject("ArenaCardShadow");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = BattleWorldMap.ShadowSortingOrder;

                go.AddComponent<ArenaShadowCard>();

                GameObjectUtility.SetStaticEditorFlags(
                    go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

                PrefabUtility.SaveAsPrefabAsset(go, CardShadowPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void BuildPrefab(
            string path, string name, Mesh mesh, Material material, ArenaCardZone zone)
        {
            var go = new GameObject(name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var card = go.AddComponent<ArenaCard>();
                card.Configure(name, zone);

                // No BatchingStatic, for the same reason ArenaBuilder.PlaceCard omits it: static
                // batching draws through a combined mesh and drops the renderer's
                // MaterialPropertyBlock, which is what the per-card occlusion fade is written with.
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);

                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------
        // theme and lighting
        // ------------------------------------------------------------------

        private static ArenaTheme BuildTheme(
            ArenaLightingProfile dusk, ArenaLightingProfile night, Material ground, Material shadow)
        {
            ArenaTheme theme = AssetDatabase.LoadAssetAtPath<ArenaTheme>(ThemePath);
            if (theme is null)
            {
                theme = ScriptableObject.CreateInstance<ArenaTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.CenterRadius = 12f;
            theme.RingOuterRadius = 26f;
            theme.ArenaRadius = 40f;
            theme.CenterMaxHeight = 0.75f;
            // Counts are a draw-call budget as much as a look: a card carries its own sort order
            // (Stage 08e, 2), so it cannot be merged with its neighbours and every piece is close to
            // one batch. The wall is therefore built from cards wide enough to overlap at the
            // shipped pitch instead of from an endless row of small ones; 300 ghosts plus the arena
            // stays under 200 batches (see the stage report).
            theme.CenterGrassMin = 12;
            theme.CenterGrassMax = 18;
            theme.RingBushMin = 16;
            theme.RingBushMax = 24;
            // The wall has to be dense: a layer's slot count comes from the card widths
            // (ArenaLayout.AverageWidth × BorderSlotPitchFactor), so the cap is what decides how
            // thick the forest reads rather than a spacing number. Three layers of cards that
            // overlap slightly is ~90 pieces at the shipped pitch — still a small number next to
            // the entity pools, and the arena is static, so it is batched and culled.
            theme.BorderTreeMin = 72;
            theme.BorderTreeMax = 96;
            theme.DirtDecalCount = 5;
            theme.FlowerDecalCount = 12;
            theme.LeafDecalCount = 10;
            theme.BushMinSpacing = 3.2f;
            theme.TreeMinSpacing = 2.1f;
            theme.BorderLayers = 3;
            theme.BorderSlotPitchFactor = 0.85f;

            // Stage 08g: the arena's own scale is the player's, and everything is measured against it.
            // The camera profile (`BattleCameraRig.OrthographicSize`) frames the figure at 9 % of the
            // screen height, the wall stands 3–5 of them, and the foreground belt begins at the first
            // radius outside the open centre — the bottom edge of the frame is 11.6 units in front of
            // the player, so a trunk in 13–16 rises from the bottom sixth of it.
            theme.PlayerHeight = BattleCameraRig.PlayerHeight;
            theme.WallMinHeightFactor = 3f;
            theme.WallMaxHeightFactor = 5f;
            theme.ForegroundInnerRadius = 13f;
            theme.ForegroundOuterRadius = 16f;
            theme.ForegroundTrunkMin = 8;
            theme.ForegroundTrunkMax = 13;

            theme.MaxYawDegrees = 9f;
            theme.GroundMaterial = ground;
            theme.ShadowMaterial = shadow;
            theme.DuskProfile = dusk;
            theme.NightProfile = night;

            // The readability floors, measured on a rendered frame by the capture tool
            // (ArenaContrast). They are the acceptance numbers and they are not to be lowered to
            // make a capture pass — the light is adjusted instead.
            theme.MinPlayerContrast = 2.2f;
            theme.MinGhostContrast = 1.9f;
            theme.MinPickupContrast = 2.4f;
            theme.MinTelegraphContrast = 1.3f;

            theme.Styles = new[]
            {
                // Stage 08g resized the wall: it stands 3–5 players high (4.8–8.0 units at a 1.6-unit
                // player) instead of the 5.4–20 the first build placed, which read as a forest the
                // fight happened *under* rather than an edge the fight is kept inside. The widths are
                // held to what a 512 px texture can cover at the stage's camera (60.75 px per unit,
                // so 8.4 units) — see `ArenaSharpnessTests`.
                Style("tree-a", ArenaCardZone.Border, 4.2f, 6.4f, 0.85f, 1.20f, "card-tree-a"),
                Style("tree-b", ArenaCardZone.Border, 5.0f, 6.6f, 0.80f, 1.15f, "card-tree-b"),
                Style("tree-c", ArenaCardZone.Border, 3.6f, 5.4f, 0.90f, 1.25f, "card-tree-c"),

                // The wall's anchors: the same three silhouettes at the top of the band, so a player
                // who walks to the edge has trees passing in front of them rather than a ring of
                // shrubs at their feet. Placeholder art: no new texture, no new asset.
                Style("tree-big-a", ArenaCardZone.Border, 7.0f, 7.2f, 0.80f, 1.10f, "card-tree-a"),
                Style("tree-big-b", ArenaCardZone.Border, 7.6f, 7.6f, 0.78f, 1.00f, "card-tree-b"),
                Style("tree-big-c", ArenaCardZone.Border, 6.6f, 6.8f, 0.85f, 1.15f, "card-tree-c"),

                // The foreground trunks: the stage's "ลำต้น foreground ที่ขอบล่างของจอ". The narrowest
                // of the three tree textures squashed to a trunk's aspect, so the belt costs no new
                // pixel — it is the same placeholder art, drawn thin.
                Style("trunk-foreground-a", ArenaCardZone.Foreground, 1.9f, 6.2f, 0.90f, 1.15f, "card-tree-c"),
                Style("trunk-foreground-b", ArenaCardZone.Foreground, 2.3f, 6.6f, 0.85f, 1.10f, "card-tree-b"),
                Style("bush", ArenaCardZone.Ring, 2.4f, 1.9f, 0.85f, 1.20f, "card-bush"),
                Style("grass", ArenaCardZone.Center, 1.10f, 0.55f, 0.80f, 1.20f, "card-grass"),
                Style("grass-small", ArenaCardZone.Center, 0.90f, 0.42f, 0.80f, 1.20f, "card-grass-small"),
            };

            theme.DecalStyles = new[]
            {
                // The dirt patch's scale is capped at 1.10 so the widest one (7.7 units) still fits a
                // 512 px texture at the stage's 60.75 px per unit.
                Style("dirt", ArenaCardZone.Decal, 7f, 5f, 0.80f, 1.10f, "decal-dirt"),
                Style("flower", ArenaCardZone.Decal, 0.90f, 0.90f, 0.70f, 1.30f, "decal-flower"),
                Style("leaf", ArenaCardZone.Decal, 0.70f, 0.70f, 0.70f, 1.30f, "decal-leaf"),
            };

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static ArenaCardStyle Style(
            string id, ArenaCardZone zone, float width, float height,
            float minScale, float maxScale, string materialName)
        {
            return new ArenaCardStyle
            {
                Id = id,
                Zone = zone,
                Width = width,
                Height = height,
                MinScale = minScale,
                MaxScale = maxScale,
                Material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + materialName + ".mat"),
            };
        }

        /// <summary>
        /// Dusk: the shipped look. A warm green-yellow key over a cool teal shadow, brightest in the
        /// middle of the screen and falling away at the corners (vignette) and along the top edge
        /// (the screen band). The values are a look, not a rule.
        /// </summary>
        private static ArenaLightingProfile BuildDuskProfile()
        {
            ArenaLightingProfile profile = LoadOrCreateProfile(DuskProfilePath);
            profile.TimeOfDay = ArenaTimeOfDay.Dusk;

            // The clear colour matches the floor's own tone, because a camera following the player
            // can see past the arena's edge and a bright sky there would read as a hole in the world.
            profile.SkyColor = new Color(0.10f, 0.13f, 0.07f, 1f);
            profile.EnvironmentTint = new Color(1.04f, 1.02f, 0.80f, 1f);
            profile.AmbientSky = new Color(0.62f, 0.64f, 0.47f, 1f);
            profile.AmbientEquator = new Color(0.47f, 0.49f, 0.33f, 1f);
            profile.AmbientGround = new Color(0.26f, 0.27f, 0.16f, 1f);
            profile.KeyLightColor = new Color(1f, 0.93f, 0.68f, 1f);
            profile.KeyLightIntensity = 1.35f;
            profile.KeyLightEuler = new Vector3(58f, -40f, 0f);

            // The cool end: a deep teal rather than black, so a shadowed floor still reads as floor.
            profile.ShadowColor = new Color(0.33f, 0.50f, 0.66f, 1f);
            profile.ShadowStrength = 0.60f;
            profile.ShadowDirectionDegrees = 245f;
            profile.ShadowLengthPerHeight = 0.42f;
            profile.CanopyShadowWidthFactor = 0.95f;

            profile.FogEnabled = true;
            profile.FogColor = new Color(0.29f, 0.33f, 0.19f, 1f);
            profile.FogDensity = 0.0055f;
            profile.ScreenTopGradient = 0.42f;
            profile.ScreenGradientColor = new Color(0.04f, 0.06f, 0.03f, 1f);

            // The haze the fight is read through, in screen space and only across the middle.
            // 0.16 did the job but veiled the middle of the frame more than the look wanted: the
            // same frozen frame read as a grey strip over the floor. 0.10 keeps the break in the
            // ground's tint and costs the doctor less of his robe-to-floor ratio (5.5 : 1 against
            // 4.9 : 1 at 0.16, both well over the 08f player threshold of 2.2), so the number is a
            // look with a price tag, not a free effect.
            profile.ScreenMidGradient = 0.10f;
            profile.ScreenMidColor = new Color(0.62f, 0.63f, 0.55f, 1f);

            // The lantern's halo is the brightest thing on the floor wherever the doctor stands:
            // additive over an olive floor it lifts the ground from ~0.09 to ~0.31 relative
            // luminance, so a white robe reads at 2.3 : 1 against its own lantern instead of the
            // 6 : 1 it gets a step away. Dimmed at Stage 17b's follow-up for exactly that reason;
            // the pools are dimmed with it because they wash the same middle at a larger radius.
            profile.LanternIntensity = 1.10f;
            profile.StaticLightIntensity = 0.45f;
            profile.LightPoolIntensity = 0.15f;
            profile.GlowIntensity = 0.45f;

            profile.PostExposure = 0.30f;
            profile.ColorFilter = new Color(1.02f, 1f, 0.94f, 1f);
            profile.Contrast = 9f;
            profile.Saturation = 7f;
            profile.WhiteBalance = 16f;
            profile.ColorLookup = null;
            profile.BloomIntensity = 0.30f;
            profile.BloomThreshold = 0.95f;
            profile.BloomScatter = 0.55f;
            profile.VignetteIntensity = 0.26f;
            profile.VignetteSmoothness = 0.45f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// Night: the same assets under a cold, much darker key, with all of the frame's detail left
        /// in it (Stage 08g).
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What the stage changed and why.</b> The profile this replaces veiled 55 % of every
        /// pixel at the camera's own distance: <c>FogDensity = 0.015</c> exponential-squared, and the
        /// orthographic rig sits 60 units from the arena
        /// (<c>1 − exp(−(0.015 × 60)²) = 0.555</c>). 55 % of a flat dark blue over the whole frame is
        /// exactly the muddy, bottomless look the stage was opened for — nothing was wrong with the
        /// textures; the arena was being washed. Density is 0.006 now, which veils 12 % and leaves the
        /// tree wall as a silhouette rather than a fog bank.
        /// </para>
        /// <para>
        /// <b>Why the tint is what changes the colour.</b> The arena's shaders are <b>unlit</b> on
        /// purpose (<c>ArenaCard</c>, <c>ArenaGround</c>: texture × base × <c>_ArenaTint</c>, then
        /// fog). A directional light cannot reach them, so <see cref="ArenaLightingProfile.AmbientSky"/>,
        /// <c>KeyLight*</c> and the rest of the light block do not move the arena's tone — the tint
        /// does. The previous tint (0.34, 0.42, 0.62) left the result green-dominant over the
        /// yellow-green textures and measured hue 105° on a real frame: a dark *daylight* arena, not
        /// a moonlit one. The new tint puts blue above green in the product (0.34, 0.46, 0.92), which
        /// is what makes a leaf read as moonlit teal instead of olive.
        /// </para>
        /// <para>
        /// <b>Bloom was a second softener.</b> 0.55 intensity at a 0.85 threshold bled a halo off
        /// every lit card; it is 0.32 at 1.05 now, so the glow stays on the things that are actually
        /// bright (the motes, the lantern, the pink pools) and the foliage keeps its edge.
        /// </para>
        /// </remarks>
        private static ArenaLightingProfile BuildNightProfile()
        {
            ArenaLightingProfile profile = LoadOrCreateProfile(NightProfilePath);
            profile.TimeOfDay = ArenaTimeOfDay.Night;

            profile.SkyColor = new Color(0.020f, 0.030f, 0.070f, 1f);

            // Blue above green: the one number that turns "dark daylight" into moonlight. The
            // daylight textures are green-dominant by design (the floor is 0.31, 0.34, 0.19 and a leaf
            // is 0.40, 0.50, 0.22), so blue has to *exceed* green in the product — a tint that merely
            // cools the green (0.46 on green against 0.92 on blue) still leaves green on top and
            // measured hue 134°, i.e. a dark daytime arena. At 0.38 against 1.00 the product's blue
            // wins on both the floor and the leaves, which is what a moonlit clearing reads as.
            profile.EnvironmentTint = new Color(0.32f, 0.38f, 1.00f, 1f);

            // The light block is kept for the things that *are* lit (actor sprites, the HUD's figure)
            // and for the shadow work item 4 of the stage asks for; the arena's own shaders are unlit
            // and read the tint instead.
            profile.AmbientSky = new Color(0.12f, 0.17f, 0.30f, 1f);
            profile.AmbientEquator = new Color(0.07f, 0.10f, 0.19f, 1f);
            profile.AmbientGround = new Color(0.030f, 0.045f, 0.080f, 1f);
            profile.KeyLightColor = new Color(0.66f, 0.76f, 1f, 1f);
            profile.KeyLightIntensity = 0.48f;
            profile.KeyLightEuler = new Vector3(46f, -62f, 0f);

            profile.ShadowColor = new Color(0.16f, 0.24f, 0.44f, 1f);
            profile.ShadowStrength = 0.74f;
            profile.ShadowDirectionDegrees = 250f;
            profile.ShadowLengthPerHeight = 0.42f;
            profile.CanopyShadowWidthFactor = 0.95f;

            profile.FogEnabled = true;
            profile.FogColor = new Color(0.055f, 0.080f, 0.150f, 1f);
            profile.FogDensity = 0.006f;
            profile.ScreenTopGradient = 0.55f;
            profile.ScreenGradientColor = new Color(0.010f, 0.020f, 0.050f, 1f);

            // The mid band is the second veil: 0.10 of a pale blue across the middle of the frame.
            // Halved, so the arena is read through light rather than through haze.
            profile.ScreenMidGradient = 0.05f;
            profile.ScreenMidColor = new Color(0.10f, 0.14f, 0.22f, 1f);

            profile.LanternIntensity = 2.4f;
            profile.StaticLightIntensity = 1.05f;
            profile.LightPoolIntensity = 0.42f;
            profile.GlowIntensity = 0.70f;

            profile.PostExposure = -0.05f;
            profile.ColorFilter = new Color(0.88f, 0.94f, 1f, 1f);
            profile.Contrast = 20f;
            profile.Saturation = 0f;
            profile.WhiteBalance = -22f;
            profile.ColorLookup = null;
            profile.BloomIntensity = 0.32f;
            profile.BloomThreshold = 1.05f;
            profile.BloomScatter = 0.55f;
            profile.VignetteIntensity = 0.44f;
            profile.VignetteSmoothness = 0.48f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static ArenaLightingProfile LoadOrCreateProfile(string path)
        {
            ArenaLightingProfile profile = AssetDatabase.LoadAssetAtPath<ArenaLightingProfile>(path);
            if (profile is not null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<ArenaLightingProfile>();
            profile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        // ------------------------------------------------------------------
        // textures
        // ------------------------------------------------------------------

        private static Texture2D GenerateTexture(string name, int size, Action<ArtCanvas> draw)
        {
            var canvas = new ArtCanvas(size);
            draw(canvas);

            string path = TextureFolder + "/" + name + ".png";
            File.WriteAllBytes(Path.GetFullPath(path), canvas.EncodePng());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // Stage 08g import settings, and why each one:
            //
            // * <b>anisotropic 8, forced.</b> The cards are vertical quads seen from a 50° camera, so
            //   the sampler is looking at them along a very stretched footprint. At 1× (the default)
            //   the hardware picks a mip that is far too coarse along one axis, which is exactly the
            //   "everything is soft" that the stage was opened for. 8× is four times the stage's floor
            //   of 4 and costs nothing measurable at this triangle count.
            // * <b>max size 2048.</b> The source is now up to 512; without an explicit cap the
            //   importer's own default could resample it down on the platform override.
            // * <b>CompressedHQ.</b> Four times the texels at one byte per pixel: the texture memory
            //   and the Web payload stay where Stage 15 measured them (the previous set was
            //   256² × 4 B uncompressed, this one is 512² × 1 B compressed).
            // * <b>Mipmaps stay on.</b> The arena is 80 units across and the camera only shows 50, so
            //   the far rows are minified hard; without mips they shimmer. Sharpness came from texel
            //   density and anisotropy, not from removing mips.
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = AnisoLevel;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Anisotropy every arena texture is imported with. The stage's floor is 4; the arena's cards
        /// are seen edge-on from a 50° camera, which is where anisotropy earns its keep. Exposed so a
        /// test can read the number it requires instead of restating it.
        /// </summary>
        public const int AnisoLevel = 8;

        /// <summary>
        /// The same pixel canvas, imported as a Sprite for a canvas Image (the screen-top band). A
        /// sprite asset rather than a runtime texture keeps the overlay wiring readable in the saved
        /// scene and adds nothing to a frame.
        /// </summary>
        private static Sprite GenerateSprite(
            string path, string name, int width, int height, Action<ArtCanvas> draw)
        {
            // The canvas is square; the gradient only varies along one axis, so a square source
            // stretched by the Image is the same picture at 4 × 128.
            int size = Mathf.Max(width, height);
            var canvas = new ArtCanvas(size);
            draw(canvas);

            File.WriteAllBytes(Path.GetFullPath(path), canvas.EncodePng());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite is null)
            {
                throw new InvalidOperationException("The generated sprite '" + name + "' did not import.");
            }

            return sprite;
        }

        // ---- the drawings -------------------------------------------------
        //
        // Coordinates are normalised: x right, y up from the bottom, everything in 0..1. Shapes
        // blend into the canvas with a soft edge measured in pixels, which is what keeps a generated
        // silhouette from looking like a bitmap at 50°.

        // The random seed is the one the 256 px art shipped with (101 + 256). Stage 08g raised the
        // resolution; if the seed had followed the size, every tree would have been re-composed into a
        // *different* silhouette, which is new art rather than the same art at more pixels.
        private const int TreeSeed = 357;

        private static void TreeA(ArtCanvas c) => Tree(c, 0.42f, 0.70f, 0.26f, 0.20f, 9, 0.13f);

        private static void TreeB(ArtCanvas c) => Tree(c, 0.46f, 0.76f, 0.22f, 0.24f, 11, 0.11f);

        private static void TreeC(ArtCanvas c) => Tree(c, 0.38f, 0.62f, 0.31f, 0.17f, 7, 0.14f);

        private static void Tree(
            ArtCanvas c, float trunkTop, float canopyY, float canopyX, float canopyYRadius,
            int blobs, float blobRadius)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));

            // Trunk and two branches. A trunk is what makes a card read as a tree rather than a shrub.
            c.Rect(0.5f, 0f, 0.5f, trunkTop, 0.045f, Bark, 1.2f);
            c.Line(0.5f, trunkTop * 0.55f, 0.30f, trunkTop * 0.95f, 0.030f, Bark, 1.2f);
            c.Line(0.5f, trunkTop * 0.62f, 0.70f, trunkTop * 1.00f, 0.028f, Bark, 1.2f);

            var random = new System.Random(TreeSeed);
            // Dark mass first, then a mid layer and a few lit leaves: three values are enough to give
            // a flat alpha-cut card some volume.
            for (int i = 0; i < blobs; i++)
            {
                float angle = (i / (float)blobs) * Mathf.PI * 2f;
                float radius = blobRadius * (0.72f + (float)random.NextDouble() * 0.55f);
                float x = 0.5f + (Mathf.Cos(angle) * canopyX * (0.55f + (float)random.NextDouble() * 0.6f));
                float y = canopyY + (Mathf.Sin(angle) * canopyYRadius * (0.55f + (float)random.NextDouble() * 0.6f));
                c.Disc(x, y, radius, LeafDark, 2.2f);
            }

            for (int i = 0; i < Mathf.Max(3, blobs - 3); i++)
            {
                float angle = (i / (float)(blobs - 3)) * Mathf.PI * 2f + 0.7f;
                float radius = blobRadius * (0.48f + (float)random.NextDouble() * 0.3f);
                float x = 0.5f + (Mathf.Cos(angle) * canopyX * 0.55f);
                float y = canopyY + (Mathf.Sin(angle) * canopyYRadius * 0.55f) + 0.02f;
                c.Disc(x, y, radius, LeafMid, 2.0f);
            }

            for (int i = 0; i < 5; i++)
            {
                float x = 0.5f + (((float)random.NextDouble() - 0.5f) * canopyX * 1.1f);
                float y = canopyY + (((float)random.NextDouble() - 0.3f) * canopyYRadius * 1.0f);
                c.Disc(x, y, blobRadius * (0.20f + (float)random.NextDouble() * 0.12f), LeafLight, 2.0f);
            }
        }

        private static void Bush(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(211);

            c.Disc(0.5f, 0.30f, 0.26f, LeafDark, 2.0f);
            for (int i = 0; i < 4; i++)
            {
                float x = 0.28f + (i * 0.15f);
                float y = 0.28f + ((float)random.NextDouble() * 0.12f);
                c.Disc(x, y, 0.17f + ((float)random.NextDouble() * 0.05f), LeafDark, 2.0f);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = 0.30f + (i * 0.135f);
                float y = 0.36f + ((float)random.NextDouble() * 0.10f);
                c.Disc(x, y, 0.13f, LeafMid, 1.8f);
            }

            c.Disc(0.42f, 0.46f, 0.09f, LeafLight, 1.8f);
        }

        private static void Grass(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(307);
            for (int i = 0; i < 9; i++)
            {
                float x = 0.18f + (i * 0.08f);
                float top = 0.55f + ((float)random.NextDouble() * 0.42f);
                float lean = ((float)random.NextDouble() - 0.5f) * 0.22f;
                c.Line(x, 0.02f, x + lean, top, 0.026f, GrassPale * new Color(1f, 1f, 1f, 0.92f), 1.2f);
            }
        }

        private static void GrassSmall(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(311);
            for (int i = 0; i < 7; i++)
            {
                float x = 0.22f + (i * 0.09f);
                float top = 0.45f + ((float)random.NextDouble() * 0.35f);
                float lean = ((float)random.NextDouble() - 0.5f) * 0.18f;
                c.Line(x, 0.02f, x + lean, top, 0.024f, GrassPale * new Color(1f, 1f, 1f, 0.85f), 1.1f);
            }
        }

        private static void Flower(ArtCanvas c)
        {
            // The hot accents: four of the five blossoms are red-orange, so a flower decal is a
            // saturated red mark on a green floor rather than another pale pebble.
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(401);
            var petals = new[] { FlowerWarm, FlowerWarm, FlowerWhite, FlowerWarm, FlowerWarm };

            for (int i = 0; i < 5; i++)
            {
                float x = 0.20f + ((float)random.NextDouble() * 0.62f);
                float y = 0.22f + ((float)random.NextDouble() * 0.55f);
                c.Line(x, 0.04f, x, y, 0.018f, GrassPale * new Color(1f, 1f, 1f, 0.8f), 1.0f);
                c.Disc(x, y, 0.055f, petals[i % petals.Length], 1.4f);
            }
        }

        private static void Leaf(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(409);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.25f + ((float)random.NextDouble() * 0.5f);
                float y = 0.25f + ((float)random.NextDouble() * 0.5f);
                float angle = (float)random.NextDouble() * Mathf.PI;
                c.Ellipse(x, y, 0.13f, 0.055f, angle, LeafAutumn * new Color(1f, 1f, 1f, 0.9f), 1.6f);
            }
        }

        private static void Dirt(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(503);

            // A big, very low-contrast patch: five overlapping ellipses, no outline, no pattern. The
            // alphas are low because the ellipses stack where they overlap, and the material's own
            // alpha multiplies the result again — a patch that reaches full strength in its middle
            // looks like a hole in a night floor, not like dirt.
            for (int i = 0; i < 5; i++)
            {
                float x = 0.30f + ((float)random.NextDouble() * 0.40f);
                float y = 0.30f + ((float)random.NextDouble() * 0.40f);
                c.Ellipse(
                    x, y,
                    0.20f + ((float)random.NextDouble() * 0.16f),
                    0.15f + ((float)random.NextDouble() * 0.12f),
                    (float)random.NextDouble() * Mathf.PI,
                    new Color(DirtWarm.r, DirtWarm.g, DirtWarm.b, 0.18f), 18f);
            }

            c.Ellipse(0.5f, 0.5f, 0.30f, 0.24f, 0.3f, new Color(0.30f, 0.24f, 0.17f, 0.09f), 26f);
        }

        private static void Ground(ArtCanvas c)
        {
            // A warm green-yellow floor: high enough in green to read as sunlit grass, low enough in
            // blue that the teal shadows and the cool motes cannot be mistaken for more grass. The
            // profile's tint is what makes it Dusk or Night; this texture is the daylight base.
            var baseColour = new Color(0.31f, 0.34f, 0.19f, 1f);
            c.Clear(baseColour);

            for (int y = 0; y < c.Size; y++)
            {
                for (int x = 0; x < c.Size; x++)
                {
                    float u = (x + 0.5f) / c.Size;
                    float v = (y + 0.5f) / c.Size;
                    float macro = (Mathf.PerlinNoise(u * 3.0f, v * 2.6f) - 0.5f) * 2f;
                    float micro = (Mathf.PerlinNoise((u * 8.0f) + 4f, (v * 7.0f) + 9f) - 0.5f) * 2f;
                    float shift = (macro * 0.020f) + (micro * 0.008f);
                    c.Set(x, y, new Color(
                        Mathf.Clamp01(baseColour.r + shift),
                        Mathf.Clamp01(baseColour.g + shift),
                        Mathf.Clamp01(baseColour.b + (shift * 0.6f)), 1f));
                }
            }
        }

        private static void LightPool(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            c.Radial(0.5f, 0.5f, 0.48f, new Color(1f, 0.86f, 0.62f, 0.85f), 0.9f);
        }

        private static void Glow(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            c.Radial(0.5f, 0.5f, 0.5f, new Color(1f, 0.92f, 0.72f, 0.9f), 1.4f);
        }

        private static void BlobShadow(ArtCanvas c)
        {
            // Shape only: the colour is the profile's teal, written as a global. A soft edge over
            // most of the radius is what makes the multiply look like a shadow and not like a decal.
            c.Clear(new Color(0f, 0f, 0f, 0f));
            c.Radial(0.5f, 0.5f, 0.5f, new Color(1f, 1f, 1f, 0.85f), 1.6f, aspect: 0.62f);
        }

        private static void Mote(ArtCanvas c)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));
            c.Radial(0.5f, 0.5f, 0.5f, new Color(1f, 1f, 1f, 0.9f), 2.2f);
        }

        /// <summary>
        /// The screen-top band: a vertical alpha ramp, transparent at the lower two thirds and
        /// opaque at the top edge. Drawn into a canvas Image, so it is projection-independent and
        /// covers the same fraction of the frame at every resolution.
        /// </summary>
        /// <summary>
        /// The mid-field band: opaque in the middle of the frame, transparent above 18 % and below
        /// 86 % of its height, with a sine falloff so the band has no edge to notice. The arena's
        /// fight is read in the middle of the screen under an orthographic rig, so a band that lifts
        /// that strip is the one part of the “bright middle” the top band cannot give (it only knows
        /// the top edge).
        /// </summary>
        private static void ScreenMidBand(ArtCanvas c)
        {
            c.Clear(new Color(1f, 1f, 1f, 0f));
            for (int y = 0; y < c.Size; y++)
            {
                float v = (y + 0.5f) / c.Size;
                float t = Mathf.Clamp01((v - 0.18f) / (0.86f - 0.18f));
                float alpha = t <= 0f || t >= 1f ? 0f : Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.2f);

                for (int x = 0; x < c.Size; x++)
                {
                    c.Set(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        private static void ScreenGradient(ArtCanvas c)
        {
            c.Clear(new Color(1f, 1f, 1f, 0f));
            for (int y = 0; y < c.Size; y++)
            {
                float v = (y + 0.5f) / c.Size;
                float t = Mathf.Clamp01((v - 0.45f) / 0.55f);
                float alpha = Mathf.Pow(t, 1.7f);
                for (int x = 0; x < c.Size; x++)
                {
                    c.Set(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
        }

        // ------------------------------------------------------------------
        // the pixel canvas
        // ------------------------------------------------------------------

        /// <summary>
        /// A tiny straight-alpha canvas. Deliberately not a texture generator with anti-aliasing
        /// settings: shapes take a softness in pixels and blend, which is all the placeholder art
        /// needs.
        /// </summary>
        private sealed class ArtCanvas
        {
            private readonly Color[] _pixels;

            public ArtCanvas(int size)
            {
                Size = size;
                _pixels = new Color[size * size];
            }

            public int Size { get; }

            public void Clear(Color colour)
            {
                for (int i = 0; i < _pixels.Length; i++)
                {
                    _pixels[i] = colour;
                }
            }

            public void Set(int x, int y, Color colour)
            {
                if (x < 0 || y < 0 || x >= Size || y >= Size)
                {
                    return;
                }

                _pixels[(y * Size) + x] = colour;
            }

            public void Blend(int x, int y, Color colour, float coverage)
            {
                if (coverage <= 0f || x < 0 || y < 0 || x >= Size || y >= Size)
                {
                    return;
                }

                int index = (y * Size) + x;
                Color destination = _pixels[index];
                float alpha = Mathf.Clamp01(colour.a * coverage);
                float outAlpha = alpha + (destination.a * (1f - alpha));

                if (outAlpha <= 0.0001f)
                {
                    _pixels[index] = new Color(0f, 0f, 0f, 0f);
                    return;
                }

                float r = ((colour.r * alpha) + (destination.r * destination.a * (1f - alpha))) / outAlpha;
                float g = ((colour.g * alpha) + (destination.g * destination.a * (1f - alpha))) / outAlpha;
                float b = ((colour.b * alpha) + (destination.b * destination.a * (1f - alpha))) / outAlpha;
                _pixels[index] = new Color(r, g, b, outAlpha);
            }

            /// <summary>A disc in normalised coordinates. <paramref name="softness"/> is in pixels.</summary>
            public void Disc(float cx, float cy, float radius, Color colour, float softness)
            {
                Ellipse(cx, cy, radius, radius, 0f, colour, softness);
            }

            /// <summary>An ellipse in normalised coordinates, optionally rotated (radians).</summary>
            public void Ellipse(
                float cx, float cy, float rx, float ry, float rotation, Color colour, float softness)
            {
                float cos = Mathf.Cos(-rotation);
                float sin = Mathf.Sin(-rotation);
                float pixelSoftness = Mathf.Max(0.5f, softness);

                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = ((x + 0.5f) / Size) - cx;
                        float dy = ((y + 0.5f) / Size) - cy;
                        float rxr = (dx * cos) - (dy * sin);
                        float ryr = (dx * sin) + (dy * cos);
                        float distance = Mathf.Sqrt(
                            ((rxr * rxr) / Mathf.Max(0.0001f, rx * rx))
                            + ((ryr * ryr) / Mathf.Max(0.0001f, ry * ry)));

                        // Distance 1 is the edge; convert the overshoot into pixels so the softness
                        // is the same at any radius.
                        float edgePixels = (1f - distance) * Mathf.Min(rx, ry) * Size;
                        float coverage = Mathf.Clamp01((edgePixels / pixelSoftness) + 0.5f);
                        if (coverage > 0f)
                        {
                            Blend(x, y, colour, coverage);
                        }
                    }
                }
            }

            /// <summary>A filled rectangle from (cx0, y0) to (cx1, y1), with a width in normalised units.</summary>
            public void Rect(float cx0, float cy0, float cx1, float cy1, float halfWidth, Color colour, float softness)
            {
                float dx = cx1 - cx0;
                float dy = cy1 - cy0;
                float length = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (length <= 0.0001f)
                {
                    return;
                }

                int steps = Mathf.CeilToInt(length * Size);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    Disc(cx0 + (dx * t), cy0 + (dy * t), halfWidth, colour, softness);
                }
            }

            public void Line(float x0, float y0, float x1, float y1, float halfWidth, Color colour, float softness) =>
                Rect(x0, y0, x1, y1, halfWidth, colour, softness);

            /// <summary>A radial falloff, used by the light pool, the lantern glow and the shadow.</summary>
            public void Radial(float cx, float cy, float radius, Color colour, float falloff, float aspect = 1f)
            {
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = ((x + 0.5f) / Size) - cx;
                        float dy = (((y + 0.5f) / Size) - cy) / Mathf.Max(0.001f, aspect);
                        float distance = Mathf.Sqrt((dx * dx) + (dy * dy)) / radius;
                        if (distance >= 1f)
                        {
                            continue;
                        }

                        float strength = Mathf.Pow(1f - distance, falloff);
                        Blend(x, y, new Color(colour.r, colour.g, colour.b, colour.a * strength), 1f);
                    }
                }
            }

            public byte[] EncodePng()
            {
                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                try
                {
                    texture.SetPixels(_pixels);
                    texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                    return texture.EncodeToPNG();
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
