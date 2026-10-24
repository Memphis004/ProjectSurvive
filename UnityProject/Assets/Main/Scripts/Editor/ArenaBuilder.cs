using System;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds the card forest into the Battle scene from an <see cref="ArenaTheme"/> and a seed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The split is deliberate. <see cref="ArenaLayout"/> is a pure function from (theme, seed) to a
    /// list of placements, so "the same seed gives the same arena" is a unit test.
    /// This class is the half that needs an Editor: it generates the assets
    /// (<see cref="ArenaArtBuilder"/>), instantiates the <c>ArenaCard</c>/<c>GroundDecal</c> prefabs
    /// at those placements, wires the day/night profiles and the fake lights, marks the cards static
    /// so Unity can batch them, and saves <c>Battle.unity</c>.
    /// </para>
    /// <para>
    /// Nothing here creates a collider: every card and every decal is an image. Ghosts, the player and
    /// pickups move through the trees because the arena is scenery, not geometry.
    /// </para>
    /// <para>
    /// Entry points: <c>ProjectSurvive/Stage 08e/1. Build arena</c>, and
    /// <c>-executeMethod ProjectSurvive.Game.Editor.ArenaBuilder.BuildAndExit</c> for a scripted run.
    /// The seed comes from <c>PROJECTSURVIVE_ARENA_SEED</c> when set, so a gate can pin one.
    /// </para>
    /// </remarks>
    public static class ArenaBuilder
    {
        /// <summary>The seed the scene ships with.</summary>
        public const uint DefaultSeed = 20261024u;

        /// <summary>The scene the arena is authored into.</summary>
        public const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";

        /// <summary>The one root every generated object lives under, so a rebuild replaces exactly the arena.</summary>
        public const string ArenaRootName = "Arena";

        /// <summary>Where the build report is written, relative to the repository root.</summary>
        public static string ReportPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "arena", "arena-report.json"));

        /// <summary>The menu entry: builds the assets and the scene with <see cref="DefaultSeed"/>.</summary>
        [MenuItem("ProjectSurvive/Stage 08e/1. Build arena (theme + seed)", priority = 150)]
        private static void BuildFromMenu()
        {
            int code = Build(SeedFromEnvironment(), saveScene: true, exitEditor: false);
            if (code == 0)
            {
                Debug.Log("[ProjectSurvive] arena built from " + ArenaArtBuilder.ThemePath + " (seed " + SeedFromEnvironment() + ").");
            }
        }

        /// <summary>Regenerates the art, theme and profiles without touching the scene.</summary>
        [MenuItem("ProjectSurvive/Stage 08e/2. Regenerate arena art + theme only", priority = 151)]
        private static void BuildArtFromMenu()
        {
            ArenaTheme theme = ArenaArtBuilder.BuildAll();
            Debug.Log("[ProjectSurvive] arena art regenerated (" + theme.Styles.Length + " card styles, "
                + theme.DecalStyles.Length + " decal styles).");
        }

        /// <summary>
        /// The <c>-executeMethod</c> entry point. Exits 0 when the arena was built and every layout
        /// rule held, 1 otherwise.
        /// </summary>
        public static void BuildAndExit() => EditorApplication.Exit(Build(SeedFromEnvironment(), true, true));

        /// <summary>
        /// Builds and saves the arena. Public so a test can build a second seed into a throwaway
        /// scene, though the layout tests do not need the scene at all.
        /// </summary>
        public static int Build(uint seed, bool saveScene, bool exitEditor)
        {
            ArenaTheme theme = ArenaArtBuilder.BuildAll();
            ArenaPlacement[] placements = ArenaLayout.Build(theme, seed);
            var problems = ArenaLayout.Validate(theme, placements);

            if (problems.Count > 0)
            {
                var message = new StringBuilder("Arena layout is invalid:");
                for (int i = 0; i < problems.Count; i++)
                {
                    message.Append("\n  ").Append(problems[i]);
                }

                Debug.LogError(message.ToString());
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return 1;
            }

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single);
            }
            catch (Exception error)
            {
                Debug.LogError("[ProjectSurvive] the Battle scene could not be opened: " + error.Message);
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return 1;
            }

            // Opening a scene can unload an asset that nothing referenced yet, and the theme was only
            // created a moment ago. Re-read it from its path before anything uses it; the data is the
            // same asset the placements were computed from.
            theme = AssetDatabase.LoadAssetAtPath<ArenaTheme>(ArenaArtBuilder.ThemePath);
            if (theme is null)
            {
                Debug.LogError("[ProjectSurvive] the arena theme disappeared while the scene was opened.");
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return 1;
            }

            var runner = Object.FindFirstObjectByType<BattleSimulationRunner>(FindObjectsInactive.Include);
            Camera camera = Camera.main;
            if (runner is null || camera is null)
            {
                Debug.LogError("[ProjectSurvive] the Battle scene has no runner or no Main Camera.");
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return 1;
            }

            BuildArenaObjects(theme, placements, runner, camera);

            if (saveScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            WriteReport(theme, placements, seed, problems);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[ProjectSurvive] arena: " + placements.Length + " pieces ("
                + Count(placements, ArenaCardZone.Center) + " centre, "
                + Count(placements, ArenaCardZone.Ring) + " ring, "
                + Count(placements, ArenaCardZone.Border) + " border, "
                + Count(placements, ArenaCardZone.Decal) + " decals), seed " + seed
                + ", signature " + SignatureHash(placements).ToString("x8"));

            return 0;
        }

        // ------------------------------------------------------------------
        // the scene
        // ------------------------------------------------------------------

        private static void BuildArenaObjects(
            ArenaTheme theme, ArenaPlacement[] placements, BattleSimulationRunner runner, Camera camera)
        {
            GameObject previous = GameObject.Find(ArenaRootName);
            if (previous is not null)
            {
                Object.DestroyImmediate(previous);
            }

            var root = new GameObject(ArenaRootName);
            var cardsRoot = Child(root.transform, "Cards");
            var decalsRoot = Child(root.transform, "Decals");
            var lightsRoot = Child(root.transform, "Lights");
            var poolsRoot = Child(root.transform, "LightPools");

            GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaArtBuilder.CardPrefabPath);
            GameObject decalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaArtBuilder.DecalPrefabPath);
            if (cardPrefab is null || decalPrefab is null)
            {
                throw new InvalidOperationException(
                    "The ArenaCard/GroundDecal prefabs are missing; run the art builder first.");
            }

            int cardIndex = 0;
            int decalIndex = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                ArenaPlacement placement = placements[i];

                if (placement.IsDecal)
                {
                    PlaceDecal(theme, decalPrefab, decalsRoot, placement, decalIndex++);
                }
                else
                {
                    PlaceCard(theme, cardPrefab, cardsRoot, placement, cardIndex++);
                }
            }

            Light moon = BuildMoon(lightsRoot, theme);
            Light lantern = BuildLantern(lightsRoot, out Transform lanternRoot, out Renderer lanternGlow);
            Light[] statics = BuildStaticLights(lightsRoot, theme);
            BuildLightPools(poolsRoot, theme);
            BuildPostVolume(root.transform);

            var shadowsObject = new GameObject("Shadows");
            shadowsObject.transform.SetParent(root.transform, worldPositionStays: false);
            ArenaBlobShadows shadows = shadowsObject.AddComponent<ArenaBlobShadows>();
            Wire(shadows, "_mesh", ArenaArtBuilder.LoadShadowQuad());
            Wire(shadows, "_material", theme.ShadowMaterial);
            SetInt(shadows, "_renderQueue", ArenaArtBuilder.ShadowQueue);

            var environment = root.AddComponent<ArenaEnvironmentView>();
            Wire(environment, "_theme", theme);
            Wire(environment, "_moon", moon);
            Wire(environment, "_lantern", lantern);
            Wire(environment, "_lanternRoot", lanternRoot);
            Wire(environment, "_lanternGlow", lanternGlow);
            Wire(environment, "_camera", camera);
            Wire(environment, "_volume", Object.FindFirstObjectByType<Volume>(FindObjectsInactive.Include));
            Wire(environment, "_shadows", shadows);
            WireArray(environment, "_staticLights", statics);

            // The runner is what builds the view; hand it the environment without rebuilding the scene.
            var serialized = new SerializedObject(runner);
            SerializedProperty environmentProperty = serialized.FindProperty("_viewSettings.Environment");
            if (environmentProperty is null)
            {
                throw new InvalidOperationException("BattleViewSettings has no Environment field.");
            }

            environmentProperty.objectReferenceValue = environment;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceCard(
            ArenaTheme theme, GameObject prefab, Transform parent, ArenaPlacement placement, int index)
        {
            ArenaCardStyle style = theme.Styles[placement.StyleIndex];
            var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            card.name = "card-" + index.ToString("000") + "-" + placement.StyleId;
            card.transform.position = new Vector3(placement.X, 0f, placement.Z);
            card.transform.rotation = ArenaCard.FacingCamera(placement.Yaw);
            card.transform.localScale = new Vector3(placement.Width, placement.Height, 1f);

            var renderer = card.GetComponent<MeshRenderer>();
            if (renderer is not null)
            {
                renderer.sharedMaterial = style.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                // Who hides whom is BattleWorldMap's row scale, the same one the actor sprites use,
                // so a tree in front of the doctor covers him and a tree behind him does not. No
                // card relies on the camera's depth buffer for that (Stage 08e, 2).
                renderer.sortingOrder = BattleWorldMap.PropSortingOrder(placement.Z);
            }

            card.GetComponent<ArenaCard>()?.Configure(placement.StyleId, placement.Zone);
            GameObjectUtility.SetStaticEditorFlags(
                card, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void PlaceDecal(
            ArenaTheme theme, GameObject prefab, Transform parent, ArenaPlacement placement, int index)
        {
            ArenaCardStyle style = theme.DecalStyles[placement.StyleIndex];
            var decal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            // Each decal sits a fraction of a millimetre above the previous one. The shader already
            // offsets its depth, so this is a belt-and-braces guard against two coplanar transparent
            // quads sorting by distance and flickering.
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
            }

            decal.GetComponent<ArenaCard>()?.Configure(placement.StyleId, ArenaCardZone.Decal);
            GameObjectUtility.SetStaticEditorFlags(
                decal, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static Light BuildMoon(Transform parent, ArenaTheme theme)
        {
            var go = new GameObject("Moon");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.rotation = Quaternion.Euler(theme.DayProfile.MoonEuler);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = theme.DayProfile.MoonColor;
            light.intensity = theme.DayProfile.MoonIntensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private static Light BuildLantern(Transform parent, out Transform root, out Renderer glow)
        {
            var go = new GameObject("Lantern");
            go.transform.SetParent(parent, worldPositionStays: false);
            root = go.transform;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.84f, 0.58f, 1f);
            light.range = 10f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, worldPositionStays: false);
            glowGo.transform.localRotation = ArenaCard.FacingCamera(0f);
            glowGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            glowGo.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
            glowGo.AddComponent<MeshFilter>().sharedMesh = ArenaArtBuilder.LoadShadowQuad();

            var renderer = glowGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                ArenaArtBuilder.MaterialFolder + "/lantern-glow.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            glow = renderer;
            return light;
        }

        private static Light[] BuildStaticLights(Transform parent, ArenaTheme theme)
        {
            var lights = new Light[2];
            float[] angles = { 2.15f, 5.05f };
            for (int i = 0; i < angles.Length; i++)
            {
                float radius = (theme.CenterRadius + theme.RingOuterRadius) * 0.5f;
                var go = new GameObject("CampLight" + (i + 1));
                go.transform.SetParent(parent, worldPositionStays: false);
                go.transform.localPosition = new Vector3(
                    Mathf.Cos(angles[i]) * radius, 1.4f, Mathf.Sin(angles[i]) * radius);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.78f, 0.48f, 1f);
                light.range = 9f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                lights[i] = light;

                // A fake pool at its base, so the light reads even where the real light is too dim.
                var pool = new GameObject("Pool");
                pool.transform.SetParent(go.transform, worldPositionStays: false);
                pool.transform.localPosition = new Vector3(0f, -1.35f, 0f);
                pool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                pool.transform.localScale = new Vector3(5.5f, 5.5f, 1f);
                pool.AddComponent<MeshFilter>().sharedMesh = ArenaArtBuilder.LoadShadowQuad();
                var renderer = pool.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    ArenaArtBuilder.MaterialFolder + "/light-pool.mat");
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(pool, StaticEditorFlags.BatchingStatic);
            }

            return lights;
        }

        /// <summary>
        /// Six additive pools on the ring: the “แอ่งแสง” that stand in for the real lights the URP
        /// Forward path cannot afford. Their brightness is the night profile's
        /// <see cref="ArenaLightingProfile.LightPoolIntensity"/>; by day they are invisible.
        /// </summary>
        private static void BuildLightPools(Transform parent, ArenaTheme theme)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                ArenaArtBuilder.MaterialFolder + "/light-pool.mat");

            for (int i = 0; i < 6; i++)
            {
                float angle = (i / 6f) * Mathf.PI * 2f + 0.4f;
                float radius = theme.CenterRadius + ((theme.RingOuterRadius - theme.CenterRadius) * ((i % 2 == 0) ? 0.28f : 0.72f));

                var pool = new GameObject("LightPool" + (i + 1));
                pool.transform.SetParent(parent, worldPositionStays: false);
                pool.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.025f, Mathf.Sin(angle) * radius);
                pool.transform.localRotation = Quaternion.Euler(90f, angle * Mathf.Rad2Deg * 0.1f, 0f);
                pool.transform.localScale = new Vector3(5.0f, 3.4f, 1f);
                pool.AddComponent<MeshFilter>().sharedMesh = ArenaArtBuilder.LoadShadowQuad();

                var renderer = pool.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(pool, StaticEditorFlags.BatchingStatic);
            }
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
        // wiring helpers
        // ------------------------------------------------------------------

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.transform;
        }

        private static void Wire(Object target, string field, Object value)
        {
            ProjectSetup.SetObjectReference(target, field, value);
        }

        private static void SetInt(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property is null)
            {
                throw new InvalidOperationException(target.GetType().Name + " has no field '" + field + "'.");
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

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

        private static uint SeedFromEnvironment()
        {
            string text = Environment.GetEnvironmentVariable("PROJECTSURVIVE_ARENA_SEED");
            if (!string.IsNullOrEmpty(text)
                && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint seed))
            {
                return seed;
            }

            return DefaultSeed;
        }

        // ------------------------------------------------------------------
        // report
        // ------------------------------------------------------------------

        private static int Count(ArenaPlacement[] placements, ArenaCardZone zone)
        {
            int count = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].Zone == zone)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// An FNV-1a over every placement's signature. Two runs of the same theme and seed produce
        /// the same number; a different seed produces a different one.
        /// </summary>
        public static uint SignatureHash(ArenaPlacement[] placements)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < placements.Length; i++)
                {
                    string signature = placements[i].Signature;
                    for (int c = 0; c < signature.Length; c++)
                    {
                        hash ^= signature[c];
                        hash *= 16777619u;
                    }
                }

                return hash;
            }
        }

        private static void WriteReport(
            ArenaTheme theme, ArenaPlacement[] placements, uint seed, System.Collections.Generic.List<string> problems)
        {
            string path = ReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"seed\": ").Append(seed).Append(",\n");
            json.Append("  \"signatureHash\": \"").Append(SignatureHash(placements).ToString("x8")).Append("\",\n");
            json.Append("  \"theme\": \"").Append(theme.name).Append("\",\n");
            json.Append("  \"arenaRadius\": ").Append(theme.ArenaRadius.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"centerRadius\": ").Append(theme.CenterRadius.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"centerMaxHeight\": ").Append(theme.CenterMaxHeight.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            json.Append("  \"counts\": { \"center\": ").Append(Count(placements, ArenaCardZone.Center))
                .Append(", \"ring\": ").Append(Count(placements, ArenaCardZone.Ring))
                .Append(", \"border\": ").Append(Count(placements, ArenaCardZone.Border))
                .Append(", \"decals\": ").Append(Count(placements, ArenaCardZone.Decal)).Append(" },\n");
            json.Append("  \"problems\": [");
            for (int i = 0; i < problems.Count; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ").Append('"').Append(problems[i].Replace("\"", "\\\"")).Append('"');
            }

            json.Append(problems.Count > 0 ? "\n  ],\n" : "],\n");

            // The placements themselves, so a reviewer can diff two builds without opening Unity.
            json.Append("  \"placements\": [\n");
            for (int i = 0; i < placements.Length; i++)
            {
                ArenaPlacement p = placements[i];
                json.Append("    { \"zone\": \"").Append(p.Zone)
                    .Append("\", \"style\": \"").Append(p.StyleId)
                    .Append("\", \"x\": ").Append(p.X.ToString("0.0000", CultureInfo.InvariantCulture))
                    .Append(", \"z\": ").Append(p.Z.ToString("0.0000", CultureInfo.InvariantCulture))
                    .Append(", \"scale\": ").Append(p.Scale.ToString("0.0000", CultureInfo.InvariantCulture))
                    .Append(", \"yaw\": ").Append(p.Yaw.ToString("0.0000", CultureInfo.InvariantCulture))
                    .Append(" }").Append(i < placements.Length - 1 ? "," : string.Empty).Append("\n");
            }

            json.Append("  ]\n");
            json.Append("}\n");

            File.WriteAllText(path, json.ToString());
        }
    }
}
