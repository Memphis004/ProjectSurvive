using System;
using System.Collections.Generic;
using System.IO;
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
        public const string CardQuadPath = MeshFolder + "/ArenaCardQuad.asset";
        public const string ShadowQuadPath = MeshFolder + "/ArenaShadowQuad.asset";
        public const string ThemePath = ContentFolder + "/ArenaTheme.asset";
        public const string DayProfilePath = ContentFolder + "/ArenaLightingDay.asset";
        public const string NightProfilePath = ContentFolder + "/ArenaLightingNight.asset";

        private const string CardShader = "ProjectSurvive/ArenaCard";
        private const string DecalShader = "ProjectSurvive/ArenaDecal";
        private const string DirtShader = "ProjectSurvive/ArenaDirt";
        private const string GlowShader = "ProjectSurvive/ArenaGlow";
        private const string GroundShader = "ProjectSurvive/ArenaGround";

        /// <summary>Render queue of the ground decals: above the opaque floor, below the shadows.</summary>
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

        // Daylight palette. Cosmetic only; nothing here is a balance number.
        private static readonly Color LeafDark = new Color(0.20f, 0.30f, 0.16f, 1f);
        private static readonly Color LeafMid = new Color(0.31f, 0.45f, 0.22f, 1f);
        private static readonly Color LeafLight = new Color(0.44f, 0.58f, 0.30f, 1f);
        private static readonly Color Bark = new Color(0.28f, 0.21f, 0.15f, 1f);
        private static readonly Color GrassPale = new Color(0.46f, 0.56f, 0.30f, 1f);
        private static readonly Color DirtWarm = new Color(0.33f, 0.27f, 0.19f, 1f);
        private static readonly Color FlowerWhite = new Color(0.86f, 0.86f, 0.72f, 1f);
        private static readonly Color FlowerWarm = new Color(0.84f, 0.66f, 0.34f, 1f);
        private static readonly Color LeafAutumn = new Color(0.54f, 0.40f, 0.20f, 1f);

        /// <summary>Builds every asset. Returns the theme asset, ready to lay out.</summary>
        public static ArenaTheme BuildAll()
        {
            EnsureFolders();

            Texture2D treeA = GenerateTexture("tree-a", 256, TreeA);
            Texture2D treeB = GenerateTexture("tree-b", 256, TreeB);
            Texture2D treeC = GenerateTexture("tree-c", 256, TreeC);
            Texture2D bush = GenerateTexture("bush", 128, Bush);
            Texture2D grass = GenerateTexture("grass", 64, Grass);
            Texture2D grassSmall = GenerateTexture("grass-small", 64, GrassSmall);
            Texture2D dirt = GenerateTexture("dirt", 256, Dirt);
            Texture2D flower = GenerateTexture("flower", 64, Flower);
            Texture2D leaf = GenerateTexture("leaf", 64, Leaf);
            Texture2D ground = GenerateTexture("ground-macro", 512, Ground);
            Texture2D pool = GenerateTexture("light-pool", 128, LightPool);
            Texture2D glow = GenerateTexture("lantern-glow", 64, Glow);
            Texture2D shadow = GenerateTexture("blob-shadow", 64, BlobShadow);

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
            Material groundMat = BuildMaterial("ground", GroundShader, ground, Color.white, 2000);
            Material poolMat = BuildMaterial("light-pool", GlowShader, pool, new Color(1f, 0.83f, 0.55f, 1f), GlowQueue);
            Material glowMat = BuildMaterial("lantern-glow", GlowShader, glow, new Color(1f, 0.88f, 0.66f, 1f), GlowQueue);
            Material shadowMat = BuildMaterial("blob-shadow", DecalShader, shadow, new Color(0f, 0f, 0f, 1f), ShadowQueue);

            // The shadow layer draws every actor's ellipse in one DrawMeshInstanced call, and Unity
            // refuses that call on a material with instancing off.
            shadowMat.enableInstancing = true;
            EditorUtility.SetDirty(shadowMat);

            Mesh cardQuad = BuildMesh(CardQuadPath, ArenaGeometry.CreateBasePivotedQuad);
            Mesh shadowQuad = BuildMesh(ShadowQuadPath, ArenaGeometry.CreateCenteredQuad);
            BuildPrefab(CardPrefabPath, "ArenaCard", cardQuad, cardA, ArenaCardZone.Border);
            BuildPrefab(DecalPrefabPath, "GroundDecal", shadowQuad, dirtMat, ArenaCardZone.Decal);

            ArenaLightingProfile day = BuildDayProfile();
            ArenaLightingProfile night = BuildNightProfile();
            ArenaTheme theme = BuildTheme(day, night, groundMat, shadowMat);

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
        /// and its static flags include <see cref="StaticEditorFlags.BatchingStatic"/>: the arena never
        /// moves, so Unity can combine cards that share a material into one draw call at build time
        /// while the scene still contains real, inspectable prefab instances.
        /// </summary>
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

                GameObjectUtility.SetStaticEditorFlags(
                    go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

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
            ArenaLightingProfile day, ArenaLightingProfile night, Material ground, Material shadow)
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
            theme.MaxYawDegrees = 9f;
            theme.GroundMaterial = ground;
            theme.ShadowMaterial = shadow;
            theme.DayProfile = day;
            theme.NightProfile = night;

            theme.Styles = new[]
            {
                Style("tree-a", ArenaCardZone.Border, 4.2f, 6.4f, 0.85f, 1.20f, "card-tree-a"),
                Style("tree-b", ArenaCardZone.Border, 5.0f, 7.2f, 0.80f, 1.15f, "card-tree-b"),
                Style("tree-c", ArenaCardZone.Border, 3.6f, 5.4f, 0.90f, 1.25f, "card-tree-c"),

                // The wall's anchors: the same three silhouettes at two to three times the size, so a
                // player who walks to the edge has trees passing in front of them rather than a ring
                // of shrubs at their feet. Placeholder art: no new texture, no new asset.
                Style("tree-big-a", ArenaCardZone.Border, 9.6f, 14.5f, 0.95f, 1.25f, "card-tree-a"),
                Style("tree-big-b", ArenaCardZone.Border, 10.4f, 15.5f, 0.90f, 1.20f, "card-tree-b"),
                Style("tree-big-c", ArenaCardZone.Border, 9.0f, 13.5f, 1.00f, 1.30f, "card-tree-c"),
                Style("bush", ArenaCardZone.Ring, 2.4f, 1.9f, 0.85f, 1.20f, "card-bush"),
                Style("grass", ArenaCardZone.Center, 1.10f, 0.55f, 0.80f, 1.20f, "card-grass"),
                Style("grass-small", ArenaCardZone.Center, 0.90f, 0.42f, 0.80f, 1.20f, "card-grass-small"),
            };

            theme.DecalStyles = new[]
            {
                Style("dirt", ArenaCardZone.Decal, 7f, 5f, 0.80f, 1.20f, "decal-dirt"),
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

        private static ArenaLightingProfile BuildDayProfile()
        {
            ArenaLightingProfile profile = LoadOrCreateProfile(DayProfilePath);
            // The clear colour matches the floor's own tone, because a camera following the player
            // can see past the arena's edge and a bright sky there would read as a hole in the world.
            profile.SkyColor = new Color(0.13f, 0.16f, 0.12f, 1f);
            profile.EnvironmentTint = Color.white;
            profile.AmbientSky = new Color(0.54f, 0.60f, 0.56f, 1f);
            profile.AmbientEquator = new Color(0.42f, 0.46f, 0.40f, 1f);
            profile.AmbientGround = new Color(0.24f, 0.25f, 0.21f, 1f);
            profile.MoonColor = new Color(1f, 0.96f, 0.86f, 1f);
            profile.MoonIntensity = 1.25f;
            profile.MoonEuler = new Vector3(54f, -34f, 0f);
            profile.FogEnabled = false;
            profile.FogColor = new Color(0.63f, 0.66f, 0.58f, 1f);
            profile.FogDensity = 0.005f;
            profile.LanternIntensity = 0f;
            profile.StaticLightIntensity = 0f;
            profile.LightPoolIntensity = 0f;
            profile.GlowIntensity = 0f;
            profile.PostExposure = 0.15f;
            profile.ColorFilter = new Color(1f, 1f, 1f, 1f);
            profile.Contrast = 6f;
            profile.Saturation = 4f;
            profile.WhiteBalance = 0f;
            profile.ColorLookup = null;
            profile.BloomIntensity = 0.12f;
            profile.BloomThreshold = 1.15f;
            profile.BloomScatter = 0.55f;
            profile.VignetteIntensity = 0.14f;
            profile.VignetteSmoothness = 0.45f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static ArenaLightingProfile BuildNightProfile()
        {
            ArenaLightingProfile profile = LoadOrCreateProfile(NightProfilePath);
            profile.SkyColor = new Color(0.035f, 0.045f, 0.085f, 1f);
            profile.EnvironmentTint = new Color(0.30f, 0.38f, 0.58f, 1f);
            profile.AmbientSky = new Color(0.10f, 0.14f, 0.24f, 1f);
            profile.AmbientEquator = new Color(0.06f, 0.09f, 0.16f, 1f);
            profile.AmbientGround = new Color(0.025f, 0.035f, 0.06f, 1f);
            profile.MoonColor = new Color(0.62f, 0.72f, 1f, 1f);
            profile.MoonIntensity = 0.38f;
            profile.MoonEuler = new Vector3(42f, -58f, 0f);
            profile.FogEnabled = true;
            profile.FogColor = new Color(0.045f, 0.065f, 0.12f, 1f);
            profile.FogDensity = 0.016f;
            profile.LanternIntensity = 2.6f;
            profile.StaticLightIntensity = 1.15f;
            profile.LightPoolIntensity = 0.38f;
            profile.GlowIntensity = 0.55f;
            profile.PostExposure = -0.35f;
            profile.ColorFilter = new Color(0.78f, 0.86f, 1f, 1f);
            profile.Contrast = 14f;
            profile.Saturation = -10f;
            profile.WhiteBalance = -12f;
            profile.ColorLookup = null;
            profile.BloomIntensity = 0.5f;
            profile.BloomThreshold = 0.85f;
            profile.BloomScatter = 0.65f;
            profile.VignetteIntensity = 0.36f;
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

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- the drawings -------------------------------------------------
        //
        // Coordinates are normalised: x right, y up from the bottom, everything in 0..1. Shapes
        // blend into the canvas with a soft edge measured in pixels, which is what keeps a generated
        // silhouette from looking like a bitmap at 50°.

        private static void TreeA(ArtCanvas c) => Tree(c, 256, 0.42f, 0.70f, 0.26f, 0.20f, 9, 0.13f);

        private static void TreeB(ArtCanvas c) => Tree(c, 256, 0.46f, 0.76f, 0.22f, 0.24f, 11, 0.11f);

        private static void TreeC(ArtCanvas c) => Tree(c, 256, 0.38f, 0.62f, 0.31f, 0.17f, 7, 0.14f);

        private static void Tree(
            ArtCanvas c, int size, float trunkTop, float canopyY, float canopyX, float canopyYRadius,
            int blobs, float blobRadius)
        {
            c.Clear(new Color(0f, 0f, 0f, 0f));

            // Trunk and two branches. A trunk is what makes a card read as a tree rather than a shrub.
            c.Rect(0.5f, 0f, 0.5f, trunkTop, 0.045f, Bark, 1.2f);
            c.Line(0.5f, trunkTop * 0.55f, 0.30f, trunkTop * 0.95f, 0.030f, Bark, 1.2f);
            c.Line(0.5f, trunkTop * 0.62f, 0.70f, trunkTop * 1.00f, 0.028f, Bark, 1.2f);

            var random = new System.Random(101 + size);
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
            c.Clear(new Color(0f, 0f, 0f, 0f));
            var random = new System.Random(401);
            var petals = new[] { FlowerWhite, FlowerWarm };

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
            c.Clear(new Color(0.20f, 0.22f, 0.17f, 1f));

            for (int y = 0; y < c.Size; y++)
            {
                for (int x = 0; x < c.Size; x++)
                {
                    float u = (x + 0.5f) / c.Size;
                    float v = (y + 0.5f) / c.Size;
                    float macro = (Mathf.PerlinNoise(u * 3.0f, v * 2.6f) - 0.5f) * 2f;
                    float micro = (Mathf.PerlinNoise((u * 8.0f) + 4f, (v * 7.0f) + 9f) - 0.5f) * 2f;
                    float shift = (macro * 0.022f) + (micro * 0.008f);
                    Color baseColour = new Color(0.20f, 0.22f, 0.17f, 1f);
                    c.Set(x, y, new Color(
                        Mathf.Clamp01(baseColour.r + shift),
                        Mathf.Clamp01(baseColour.g + shift),
                        Mathf.Clamp01(baseColour.b + shift), 1f));
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
            c.Clear(new Color(0f, 0f, 0f, 0f));
            c.Radial(0.5f, 0.5f, 0.5f, new Color(0f, 0f, 0f, 0.5f), 1.1f, aspect: 0.68f);
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
