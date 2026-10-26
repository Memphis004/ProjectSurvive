using System;
using System.Collections.Generic;
using System.IO;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds ศาลาพัก's yard: the four <see cref="ArenaCard"/> styles the camp adds, the two camp
    /// lighting profiles, the <see cref="CampTheme"/> asset, and the cards, ground decals, fake
    /// shadows, lamp pools and environment view authored into <c>Camp.unity</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 09o reuses the Stage 08e/08f systems unchanged — the card and decal prefabs, the quad
    /// meshes, the arena's materials and textures, <see cref="ArenaShadowCard"/> drawn by one
    /// instanced call, and <see cref="ArenaEnvironmentView"/> as the runtime half. What is new is the
    /// <b>shape</b>: a small clear yard around a sala rather than a 40-unit battlefield, which is
    /// <see cref="CampTheme"/> plus <see cref="CampYard"/>'s fixed lamps, signs and pools.
    /// </para>
    /// <para>
    /// <b>The ground is the one asset the camp has to add.</b> The sala's floor is a sprite, and a
    /// sprite is drawn in the transparent queue — which would put every decal, fake shadow and light
    /// pool (queues 2950–2990, authored for the arena's opaque floor) <i>behind</i> it. The camp
    /// therefore renders its ground with one material at queue <see cref="GroundQueue"/> (2000,
    /// opaque); the decals keep the arena's queues and the cards keep its sorting-order ladder, so
    /// the order between them is the same rule the arena uses.
    /// </para>
    /// <para>
    /// Nothing here is Instantiated or Destroyed while the game runs: the yard is authored into the
    /// scene asset, so it is built once by the Editor and destroyed with the scene like any other
    /// scene object.
    /// </para>
    /// </remarks>
    public static class CampEnvironmentBuilder
    {
        public const string ArtRoot = "Assets/Main/Art/Camp";
        public const string TextureFolder = ArtRoot + "/Textures";
        public const string MaterialFolder = ArtRoot + "/Materials";
        public const string ContentFolder = "Assets/Main/Content/Camp";

        public const string RootName = "CampEnvironment";
        public const string ThemePath = ContentFolder + "/CampTheme.asset";
        public const string DuskProfilePath = ContentFolder + "/CampLightingDusk.asset";
        public const string NightProfilePath = ContentFolder + "/CampLightingNight.asset";
        public const string GroundMaterialPath = MaterialFolder + "/camp-ground.mat";
        public const string LampMaterialName = "camp-card-lamp";
        public const string SignMaterialName = "camp-card-sign";

        /// <summary>
        /// Render queue of the camp's ground: opaque, below every arena decal (2950) and card (3000).
        /// </summary>
        public const int GroundQueue = 2000;

        private const string CardShader = "ProjectSurvive/ArenaCard";

        [MenuItem("ProjectSurvive/Stage 09o/1. Rebuild the camp yard in the open Camp scene", priority = 105)]
        private static void BuildFromMenu()
        {
            var camera = Object.FindFirstObjectByType<Camera>();
            var world = Object.FindFirstObjectByType<CampWorldView>();
            if (camera is null || world is null)
            {
                Debug.LogError("[ProjectSurvive] open Assets/Main/Scenes/Camp.unity before rebuilding its yard.");
                return;
            }

            Build(camera, world);
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSurvive] camp yard rebuilt.");
        }

        /// <summary>Builds every asset the camp's yard is made of and returns the theme.</summary>
        public static CampTheme BuildAll()
        {
            EnsureFolders();

            Material lamp = BuildMaterial(
                LampMaterialName, CardShader,
                GenerateCard("camp-lamp", 48, 128, new Color32(148, 96, 46, 255), new Color32(92, 58, 30, 255), 26),
                Color.white, ArenaArtBuilder.CardQueue);
            Material sign = BuildMaterial(
                SignMaterialName, CardShader,
                GenerateCard("camp-sign", 128, 96, new Color32(186, 166, 128, 255), new Color32(86, 70, 48, 255), 20),
                Color.white, ArenaArtBuilder.CardQueue);
            Material ground = BuildGroundMaterial();

            ArenaLightingProfile dusk = BuildDuskProfile();
            ArenaLightingProfile night = BuildNightProfile();
            CampTheme theme = BuildTheme(dusk, night, lamp, sign, ground);

            AssetDatabase.SaveAssets();
            return theme;
        }

        /// <summary>Places the yard into the open scene and wires the camp's views to it.</summary>
        public static void Build(Camera camera, CampWorldView world)
        {
            CampTheme theme = BuildAll();
            ArenaPlacement[] placements = CampYard.Build(theme);

            var problems = ArenaLayout.Validate(theme, placements);
            problems.AddRange(CampYard.Validate(theme, placements, world.WalkArea));
            if (problems.Count > 0)
            {
                var message = new System.Text.StringBuilder("The camp yard is invalid:");
                for (int i = 0; i < problems.Count; i++)
                {
                    message.Append("\n  ").Append(problems[i]);
                }

                throw new InvalidOperationException(message.ToString());
            }

            GameObject previous = GameObject.Find(RootName);
            if (previous is not null)
            {
                Object.DestroyImmediate(previous);
            }

            var root = new GameObject(RootName);
            Transform cards = Child(root.transform, "Cards");
            Transform decals = Child(root.transform, "Decals");
            Transform shadows = Child(root.transform, "CardShadows");
            Transform lights = Child(root.transform, "Lights");
            Transform pools = Child(root.transform, "LightPools");

            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaArtBuilder.CardPrefabPath);
            GameObject decalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaArtBuilder.DecalPrefabPath);
            GameObject shadowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaArtBuilder.CardShadowPrefabPath);
            if (cardPrefab is null || decalPrefab is null || shadowPrefab is null)
            {
                throw new InvalidOperationException(
                    "The ArenaCard/GroundDecal/ArenaCardShadow prefabs are missing; run the arena art builder first.");
            }

            float pitch = camera != null ? camera.transform.eulerAngles.x : 32f;

            int cardIndex = 0;
            int decalIndex = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                ArenaPlacement placement = placements[i];
                if (placement.IsDecal)
                {
                    PlaceDecal(theme, decalPrefab, decals, placement, decalIndex++);
                }
                else
                {
                    PlaceCard(theme, cardPrefab, cards, placement, cardIndex++, pitch);
                }
            }

            // The three fixed pieces: two lamps flanking the shelf row and a sign beside the sala.
            var fixtures = new List<ArenaPlacement>(CampYard.Fixtures.Length);
            for (int i = 0; i < CampYard.Fixtures.Length; i++)
            {
                CampFixture fixture = CampYard.Fixtures[i];
                ArenaCardStyle style = CampYard.StyleOf(theme.FixtureStyles, fixture.StyleId);
                var placement = new ArenaPlacement(
                    ArenaCardZone.Border, 0, style.Id, fixture.X, fixture.Z,
                    style.Width, style.Height, 1f, fixture.Yaw);
                fixtures.Add(placement);
                PlaceCard(style, cardPrefab, cards, placement, cardIndex++, pitch);
            }

            BuildCardShadows(theme, placements, fixtures, shadowPrefab, shadows);

            var shadowLayer = root.AddComponent<ArenaCardShadowLayer>();
            shadowLayer.Configure(
                ArenaArtBuilder.LoadShadowQuad(),
                AssetDatabase.LoadAssetAtPath<Material>(ArenaArtBuilder.MaterialFolder + "/card-shadow.mat"));
            shadowLayer.Collect();

            Light key = BuildKeyLight(lights, theme);
            Light[] statics = BuildStaticLights(lights);
            BuildLightPools(pools);
            BuildPostVolume(root.transform);
            ArenaScreenGradient gradient = BuildScreenGradient(root.transform, theme);

            var environment = root.AddComponent<ArenaEnvironmentView>();
            Wire(environment, "_theme", theme);
            Wire(environment, "_keyLight", key);
            Wire(environment, "_camera", camera);
            Wire(environment, "_volume", Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include));
            Wire(environment, "_screenGradient", gradient);
            WireArray(environment, "_staticLights", statics);

            Wire(world, "_environment", environment);
            Wire(world, "_groundMaterial",
                AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath));
        }

        // ------------------------------------------------------------------
        // placements
        // ------------------------------------------------------------------

        private static void PlaceCard(
            ArenaTheme theme, GameObject prefab, Transform parent, ArenaPlacement placement, int index,
            float pitch)
        {
            PlaceCard(theme.Styles[placement.StyleIndex], prefab, parent, placement, index, pitch);
        }

        private static void PlaceCard(
            ArenaCardStyle style, GameObject prefab, Transform parent, ArenaPlacement placement,
            int index, float pitch)
        {
            var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            card.name = "card-" + index.ToString("000") + "-" + placement.StyleId;
            card.transform.position = new Vector3(placement.X, 0f, placement.Z);

            // Square-on to the *camp's* camera (32°), not the arena's 50°: the cards are authored
            // scenery and a card authored for another camera's pitch would lean away from this one.
            card.transform.rotation = Quaternion.Euler(pitch, placement.Yaw, 0f);
            card.transform.localScale = new Vector3(placement.Width, placement.Height, 1f);

            var renderer = card.GetComponent<MeshRenderer>();
            if (renderer is not null)
            {
                renderer.sharedMaterial = style.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = CampSorting.CardOrder(placement.Z);
            }

            card.GetComponent<ArenaCard>()?.Configure(placement.StyleId, placement.Zone);
            GameObjectUtility.SetStaticEditorFlags(card, StaticEditorFlags.OccludeeStatic);
        }

        private static void PlaceDecal(
            CampTheme theme, GameObject prefab, Transform parent, ArenaPlacement placement, int index)
        {
            ArenaCardStyle style = theme.DecalStyles[placement.StyleIndex];
            var decal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            float height = 0.02f + (index * 0.0004f);
            decal.name = "decal-" + index.ToString("000") + "-" + placement.StyleId;
            decal.transform.position = new Vector3(placement.X, height, placement.Z);
            decal.transform.rotation = Quaternion.Euler(90f, placement.Yaw, 0f);
            decal.transform.localScale = new Vector3(placement.Width, style.Height * placement.Scale, 1f);

            var renderer = decal.GetComponent<MeshRenderer>();
            if (renderer is not null)
            {
                renderer.sharedMaterial = style.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = CampSorting.DecalOrder;
            }

            decal.GetComponent<ArenaCard>()?.Configure(placement.StyleId, ArenaCardZone.Decal);
            GameObjectUtility.SetStaticEditorFlags(
                decal, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>
        /// One flat fake shadow per standing card, in the profile's single direction. The camp adds no
        /// wide canopy patch anywhere the player can walk: the yard's floor is what the shelves and
        /// the figure are read against.
        /// </summary>
        private static void BuildCardShadows(
            CampTheme theme, IReadOnlyList<ArenaPlacement> placements, IReadOnlyList<ArenaPlacement> fixtures,
            GameObject prefab, Transform parent)
        {
            ArenaLightingProfile profile = theme.DuskProfile;
            Quaternion rotation = ArenaShadowCard.Orientation(profile.ShadowDirectionDegrees);
            int index = 0;

            for (int i = 0; i < placements.Count; i++)
            {
                ArenaPlacement placement = placements[i];
                if (placement.IsDecal)
                {
                    continue;
                }

                index = PlaceShadow(prefab, parent, placement, index, profile, rotation);
            }

            for (int i = 0; i < fixtures.Count; i++)
            {
                index = PlaceShadow(prefab, parent, fixtures[i], index, profile, rotation);
            }
        }

        private static int PlaceShadow(
            GameObject prefab, Transform parent, ArenaPlacement placement, int index,
            ArenaLightingProfile profile, Quaternion rotation)
        {
            float length = ArenaShadowCard.LengthFor(placement.Height, profile);
            float width = Mathf.Max(0.18f, placement.Width * 0.78f);
            Vector3 centre = ArenaShadowCard.CentreFor(placement.X, placement.Z, length, profile);

            var shadow = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            shadow.name = "shadow-" + index.ToString("0000") + "-" + placement.StyleId;
            shadow.transform.position = new Vector3(centre.x, 0.035f, centre.z);
            shadow.transform.rotation = rotation;
            shadow.transform.localScale = new Vector3(length, width, 1f);

            var renderer = shadow.GetComponent<MeshRenderer>();
            if (renderer is not null)
            {
                renderer.sortingOrder = CampSorting.CardShadowOrder;
            }

            shadow.GetComponent<ArenaShadowCard>()?.Configure(
                placement.Zone, placement.Width, placement.Height,
                profile.ShadowDirectionDegrees, canopy: false);

            return index + 1;
        }

        // ------------------------------------------------------------------
        // light
        // ------------------------------------------------------------------

        private static Light BuildKeyLight(Transform parent, CampTheme theme)
        {
            var go = new GameObject("KeyLight");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.rotation = Quaternion.Euler(theme.DuskProfile.KeyLightEuler);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = theme.DuskProfile.KeyLightColor;
            light.intensity = theme.DuskProfile.KeyLightIntensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>
        /// The yard's two warm lights, standing at the lamps. Two is the budget: the profile's
        /// <c>StaticLightIntensity</c> plus nothing else, well inside URP's per-object limit of four.
        /// </summary>
        private static Light[] BuildStaticLights(Transform parent)
        {
            var lampPositions = new List<Vector3>(2);
            for (int i = 0; i < CampYard.Fixtures.Length; i++)
            {
                CampFixture fixture = CampYard.Fixtures[i];
                if (fixture.StyleId == "lamp")
                {
                    lampPositions.Add(new Vector3(fixture.X, 1.9f, fixture.Z));
                }
            }

            var lights = new Light[lampPositions.Count];
            for (int i = 0; i < lampPositions.Count; i++)
            {
                var go = new GameObject("LampLight" + (i + 1));
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.localPosition = lampPositions[i];

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.46f, 1f);
                light.range = 9f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                lights[i] = light;
            }

            return lights;
        }

        /// <summary>
        /// The warm pools under the shelves, the drum, the pot and the sala. One material, driven by
        /// the profile's <c>LightPoolIntensity</c>, so the two looks differ without touching an asset.
        /// </summary>
        private static void BuildLightPools(Transform parent)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                ArenaArtBuilder.MaterialFolder + "/light-pool.mat");
            if (material is null)
            {
                throw new InvalidOperationException("The arena's light-pool material is missing.");
            }

            for (int i = 0; i < CampYard.LightPools.Length; i++)
            {
                CampLightPool pool = CampYard.LightPools[i];
                var go = new GameObject("LightPool" + (i + 1));
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.localPosition = new Vector3(pool.X, 0.03f, pool.Z);
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(pool.Width, pool.Depth, 1f);
                go.AddComponent<MeshFilter>().sharedMesh = ArenaArtBuilder.LoadShadowQuad();

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = CampSorting.LightPoolOrder;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        private static ArenaScreenGradient BuildScreenGradient(Transform parent, ArenaTheme theme)
        {
            var go = new GameObject("ScreenGradient");
            go.transform.SetParent(parent, worldPositionStays: false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = ArenaScreenGradient.SortingOrder;

            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArenaArtBuilder.ScreenGradientSpritePath);
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            image.color = Color.white;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var gradient = go.AddComponent<ArenaScreenGradient>();
            gradient.Configure(image);

            // The authored band, for the same reason the arena builder writes it: the runtime
            // Apply only runs from Awake, and a scene left at the Image's white default shows (and
            // photographs) a pale wash the game never draws.
            ArenaLightingProfile dusk = theme != null ? theme.DuskProfile : null;
            if (dusk != null)
            {
                gradient.Apply(dusk.ScreenGradientColor, dusk.ScreenTopGradient);
            }

            return gradient;
        }

        private static void BuildPostVolume(Transform parent)
        {
            var go = new GameObject("PostProcessVolume");
            go.transform.SetParent(parent, worldPositionStays: false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.weight = 1f;
        }

        // ------------------------------------------------------------------
        // assets
        // ------------------------------------------------------------------

        private static void EnsureFolders()
        {
            ProjectSetup.EnsureFolder(TextureFolder);
            ProjectSetup.EnsureFolder(MaterialFolder);
            ProjectSetup.EnsureFolder(ContentFolder);
        }

        private static Material BuildMaterial(
            string name, string shaderName, Texture2D texture, Color tint, int queue)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader is null)
            {
                throw new InvalidOperationException("The shader '" + shaderName + "' is missing");
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
        /// The one camp-only material: the sala's ground, drawn opaque so the arena's decal, shadow
        /// and pool queues sit above it (see the type's remarks).
        /// </summary>
        private static Material BuildGroundMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader is null)
            {
                throw new InvalidOperationException("The sprite shader is missing.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (material is null)
            {
                material = new Material(shader) { name = "camp-ground" };
                AssetDatabase.CreateAsset(material, GroundMaterialPath);
            }

            material.shader = shader;
            material.renderQueue = GroundQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A plain card: a bordered rectangle with one lit edge. Placeholder art in the same sense as
        /// the arena's (AGENTS rule 13) — the pixels are a function of this file, there is nothing to
        /// attribute, and no drawn illustration stands in for art the project has not made.
        /// </summary>
        private static Texture2D GenerateCard(
            string name, int width, int height, Color32 body, Color32 edge, int litHeight)
        {
            string path = TextureFolder + "/" + name + ".png";
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = x < 3 || y < 3 || x >= width - 3 || y >= height - 3;
                    bool lit = y >= height - litHeight;
                    pixels[(y * width) + x] = border
                        ? edge
                        : lit
                            ? Lighten(body, 26)
                            : body;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer is not null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return asset;
        }

        private static Color32 Lighten(Color32 colour, int amount) => new Color32(
            (byte)Mathf.Min(255, colour.r + amount),
            (byte)Mathf.Min(255, colour.g + amount),
            (byte)Mathf.Min(255, colour.b + amount),
            colour.a);

        private static CampTheme BuildTheme(
            ArenaLightingProfile dusk, ArenaLightingProfile night,
            Material lamp, Material sign, Material ground)
        {
            var theme = AssetDatabase.LoadAssetAtPath<CampTheme>(ThemePath);
            if (theme is null)
            {
                theme = ScriptableObject.CreateInstance<CampTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            // The yard, not the battlefield: the centre is big enough to hold the sala's whole walk
            // rectangle (its corner is at radius 9.14), the ring of scrub starts outside it and the
            // trees stand further back still. Counts are lower than the arena's — a camp is a
            // clearing, and the brief asks for a frame, not a wall.
            theme.CenterRadius = 10.2f;
            theme.RingOuterRadius = 14f;
            theme.ArenaRadius = 18f;
            theme.CenterMaxHeight = 0.75f;
            theme.CenterGrassMin = 34;
            theme.CenterGrassMax = 56;
            // The generator fills a full ring; CampYard keeps only the band that can frame this
            // camera (the two sides and the strip behind the sala), so the counts are higher than
            // the kept numbers: a third of the ring is the frame and the rest is filtered out.
            theme.RingBushMin = 40;
            theme.RingBushMax = 54;
            theme.BorderTreeMin = 72;
            theme.BorderTreeMax = 92;
            theme.DirtDecalCount = 4;
            theme.FlowerDecalCount = 14;
            theme.LeafDecalCount = 14;
            theme.BushMinSpacing = 3.4f;
            theme.TreeMinSpacing = 2.6f;
            theme.BorderLayers = 2;
            theme.BorderSlotPitchFactor = 0.9f;
            theme.MaxYawDegrees = 8f;
            theme.GroundMaterial = ground;
            theme.ShadowMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                ArenaArtBuilder.MaterialFolder + "/blob-shadow.mat");
            theme.DuskProfile = dusk;
            theme.NightProfile = night;

            theme.MinPlayerContrast = 2.2f;
            theme.MinPropContrast = 1.9f;
            // The price boards are unlit UI, so their ratio is ink against board at a type size —
            // not a lighting term. The label is drawn near-black at 3.5 font units for this floor.
            theme.MinLabelContrast = 4.5f;
            theme.LabelFloorPixels = 22f;

            // The player, a ghost, a pickup and a telegraph are the arena's thresholds; the camp has
            // no ghosts and no pickups, and inheriting a ghost floor it can never measure would be a
            // number nobody enforces. It keeps them at the arena's values so a stray ghost (a dev
            // spawn) is still held to the same floor.
            theme.MinGhostContrast = 1.9f;
            theme.MinPickupContrast = 2.4f;
            theme.MinTelegraphContrast = 1.3f;

            theme.Styles = new[]
            {
                Style("tree-a", ArenaCardZone.Border, 4.2f, 6.4f, 0.85f, 1.15f, "card-tree-a"),
                Style("tree-b", ArenaCardZone.Border, 5.0f, 7.2f, 0.80f, 1.10f, "card-tree-b"),
                Style("tree-c", ArenaCardZone.Border, 3.6f, 5.4f, 0.90f, 1.20f, "card-tree-c"),
                Style("tree-big-a", ArenaCardZone.Border, 8.2f, 11.0f, 0.95f, 1.20f, "card-tree-a"),
                Style("tree-big-b", ArenaCardZone.Border, 9.0f, 12.4f, 0.90f, 1.15f, "card-tree-b"),
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

            theme.FixtureStyles = new[]
            {
                // Narrow and tall: a lamp post, not a tree.
                new ArenaCardStyle
                {
                    Id = "lamp",
                    Zone = ArenaCardZone.Border,
                    Width = 0.55f,
                    Height = 2.6f,
                    MinScale = 1f,
                    MaxScale = 1f,
                    Material = lamp,
                },
                new ArenaCardStyle
                {
                    Id = "sign",
                    Zone = ArenaCardZone.Border,
                    Width = 1.5f,
                    Height = 1.15f,
                    MinScale = 1f,
                    MaxScale = 1f,
                    Material = sign,
                },
            };

            EditorUtility.SetDirty(theme);
            return theme;
        }

        private static ArenaCardStyle Style(
            string id, ArenaCardZone zone, float width, float height,
            float minScale, float maxScale, string materialName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                ArenaArtBuilder.MaterialFolder + "/" + materialName + ".mat");
            if (material is null)
            {
                throw new InvalidOperationException(
                    "The arena material '" + materialName + "' is missing; run the arena art builder first.");
            }

            return new ArenaCardStyle
            {
                Id = id,
                Zone = zone,
                Width = width,
                Height = height,
                MinScale = minScale,
                MaxScale = maxScale,
                Material = material,
            };
        }

        private static ArenaLightingProfile BuildDuskProfile()
        {
            var profile = LoadOrCreateProfile(DuskProfilePath, ArenaTimeOfDay.Dusk);

            // Warmer and brighter than the arena's dusk, as the brief asks: a lit yard at the end of
            // the day, not a battlefield at the edge of dark.
            profile.SkyColor = new Color(0.18f, 0.15f, 0.10f, 1f);
            profile.EnvironmentTint = new Color(1.10f, 1.02f, 0.84f, 1f);
            profile.AmbientSky = new Color(0.78f, 0.70f, 0.52f, 1f);
            profile.AmbientEquator = new Color(0.56f, 0.51f, 0.36f, 1f);
            profile.AmbientGround = new Color(0.32f, 0.29f, 0.19f, 1f);

            profile.KeyLightColor = new Color(1f, 0.88f, 0.62f, 1f);
            profile.KeyLightIntensity = 1.55f;
            profile.KeyLightEuler = new Vector3(54f, -34f, 0f);

            // One shadow direction for the whole yard, and the same angle in both profiles: the card
            // shadows are baked into the scene, so a profile that disagreed would draw a shadow that
            // does not belong to the look in force.
            profile.ShadowColor = new Color(0.38f, 0.48f, 0.56f, 1f);
            profile.ShadowStrength = 0.42f;
            profile.ShadowDirectionDegrees = 245f;
            profile.ShadowLengthPerHeight = 0.36f;
            profile.CanopyShadowWidthFactor = 0.9f;

            profile.FogEnabled = true;
            profile.FogColor = new Color(0.30f, 0.29f, 0.20f, 1f);
            profile.FogDensity = 0.0035f;

            profile.ScreenTopGradient = 0.30f;
            profile.ScreenGradientColor = new Color(0.06f, 0.05f, 0.03f, 1f);

            profile.LanternIntensity = 0f;
            profile.StaticLightIntensity = 0.9f;
            profile.LightPoolIntensity = 0.34f;
            profile.GlowIntensity = 0f;

            profile.PostExposure = 0.5f;
            profile.ColorFilter = new Color(1.06f, 1f, 0.9f, 1f);
            profile.Contrast = 7f;
            profile.Saturation = 8f;
            profile.WhiteBalance = 26f;
            profile.BloomIntensity = 0.3f;
            profile.BloomThreshold = 0.95f;
            profile.BloomScatter = 0.55f;
            profile.VignetteIntensity = 0.14f;
            profile.VignetteSmoothness = 0.50f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static ArenaLightingProfile BuildNightProfile()
        {
            var profile = LoadOrCreateProfile(NightProfilePath, ArenaTimeOfDay.Night);

            profile.SkyColor = new Color(0.02f, 0.03f, 0.06f, 1f);
            profile.EnvironmentTint = new Color(0.38f, 0.43f, 0.62f, 1f);
            profile.AmbientSky = new Color(0.1f, 0.13f, 0.22f, 1f);
            profile.AmbientEquator = new Color(0.08f, 0.10f, 0.16f, 1f);
            profile.AmbientGround = new Color(0.04f, 0.045f, 0.06f, 1f);

            profile.KeyLightColor = new Color(0.66f, 0.74f, 1f, 1f);
            profile.KeyLightIntensity = 0.55f;
            profile.KeyLightEuler = new Vector3(48f, -58f, 0f);

            profile.ShadowColor = new Color(0.26f, 0.34f, 0.50f, 1f);
            profile.ShadowStrength = 0.62f;
            profile.ShadowDirectionDegrees = 245f;
            profile.ShadowLengthPerHeight = 0.36f;
            profile.CanopyShadowWidthFactor = 0.9f;

            profile.FogEnabled = true;
            profile.FogColor = new Color(0.05f, 0.07f, 0.12f, 1f);
            profile.FogDensity = 0.010f;

            profile.ScreenTopGradient = 0.42f;
            profile.ScreenGradientColor = new Color(0.01f, 0.02f, 0.05f, 1f);

            profile.LanternIntensity = 0f;
            profile.StaticLightIntensity = 1.5f;
            profile.LightPoolIntensity = 0.55f;
            profile.GlowIntensity = 0f;

            profile.PostExposure = -0.35f;
            profile.ColorFilter = new Color(0.78f, 0.86f, 1.03f, 1f);
            profile.Contrast = 12f;
            profile.Saturation = -8f;
            profile.WhiteBalance = -16f;
            profile.BloomIntensity = 0.5f;
            profile.BloomThreshold = 0.9f;
            profile.BloomScatter = 0.6f;

            // Still softer than the arena's 0.40: the camp is a place to read a shop, not a fight.
            profile.VignetteIntensity = 0.24f;
            profile.VignetteSmoothness = 0.50f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static ArenaLightingProfile LoadOrCreateProfile(string path, ArenaTimeOfDay timeOfDay)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ArenaLightingProfile>(path);
            if (profile is null)
            {
                profile = ScriptableObject.CreateInstance<ArenaLightingProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.TimeOfDay = timeOfDay;
            return profile;
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.transform;
        }

        private static void Wire(Object target, string field, Object value) =>
            ProjectSetup.SetObjectReference(target, field, value);

        private static void WireArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty array = serialized.FindProperty(field);
            if (array is null)
            {
                throw new InvalidOperationException(target.GetType().Name + " has no array '" + field + "'.");
            }

            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
