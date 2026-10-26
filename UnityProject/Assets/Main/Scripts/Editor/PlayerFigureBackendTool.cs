using System.Collections.Generic;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.GenericCute;
using ProjectSurvive.Game.Presentation.SpineActors;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// The three menu entries Stage 17f's backend needs: build the 2D Animation profiles from the pack,
    /// choose which backend the scenes use, and rebuild both scenes so the choice is what runs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It edits fields, not assets.</b> An existing profile keeps every value the user tuned: the
    /// tool fills only what is empty (prefab, controller, state names) and logs each field it changed.
    /// A name the controller does not have is refused by the validator — the profile is not written
    /// with a guess in it.
    /// </para>
    /// <para>
    /// <b>Rebuilding is the switch.</b> There is no runtime switching: choosing a backend writes the
    /// settings asset and rebuilds the Camp and Battle scenes, so each scene carries the component and
    /// the profile it will use. A scene whose chosen profile is missing logs an error and draws no
    /// figure — never the other backend's art.
    /// </para>
    /// </remarks>
    public static class PlayerFigureBackendTool
    {
        /// <summary>The pack's rigged prefab (assigned to both profiles).</summary>
        public const string PrefabPath =
            "Assets/Generic Cute 2D/Student/001 Student 1/Prefabs/001 Student 1 Character.prefab";

        /// <summary>The pack's demo controller — the only controller with the rig's states.</summary>
        public const string ControllerPath =
            "Assets/Generic Cute 2D/Student/001 Student 1/Demo/AnimationController/Basic.controller";

        /// <summary>The pack's portrait, drawn by the battle HUD when this backend is selected.</summary>
        public const string PortraitSpritePath =
            "Assets/Generic Cute 2D/Student/001 Student 1/Sprites/Portraits/001 Student 1 Portrait.png";

        /// <summary>The camp's 2D Animation profile.</summary>
        public const string CampProfilePath = "Assets/Main/Presentation/GenericCutePlayerProfile_Camp.asset";

        /// <summary>The arena's 2D Animation profile.</summary>
        public const string BattleProfilePath = "Assets/Main/Presentation/GenericCutePlayerProfile_Battle.asset";

        [MenuItem("ProjectSurvive/Player Figure/1. Create GenericCute profiles", priority = 201)]
        public static void CreateProfiles()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (prefab == null || controller == null)
            {
                Debug.LogError(
                    "[PlayerFigureBackendTool] the pack is missing: prefab=" + (prefab != null) +
                    " controller=" + (controller != null) + " (expected " + PrefabPath + " and " +
                    ControllerPath + "). Nothing was written.");
                return;
            }

            bool campOk = WriteProfile(CampProfilePath, prefab, controller);
            bool battleOk = WriteProfile(BattleProfilePath, prefab, controller);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[PlayerFigureBackendTool] profiles: " + CampProfilePath + "=" + campOk + ", " +
                BattleProfilePath + "=" + battleOk + ".");
        }

        [MenuItem("ProjectSurvive/Player Figure/2. Use Spine backend", priority = 202)]
        public static void UseSpine()
        {
            SetBackend(PlayerFigureBackend.Spine);
        }

        [MenuItem("ProjectSurvive/Player Figure/3. Use GenericCute backend", priority = 203)]
        public static void UseGenericCute()
        {
            SetBackend(PlayerFigureBackend.GenericCute);
        }

        /// <summary>
        /// Switches the backend by <b>editing the two scenes in place</b>, not by rebuilding them.
        /// </summary>
        /// <remarks>
        /// The scene builders create a scene from nothing, and later stages added the arena's
        /// environment, the camp's lighting and every capture-only prop to the authored scenes
        /// afterwards — regenerating would drop all of it (measured: rebuilding Battle lost ~38k lines
        /// of scene content and the arena stood empty). So the switch swaps only the figure component
        /// and its profile on the object that already exists, plus the status panel's portrait, and
        /// saves the scene. A backend switch therefore leaves every other object untouched.
        /// </remarks>
        private static void SetBackend(PlayerFigureBackend backend)
        {
            PlayerFigureBuildSettings settings = PlayerFigureBuildSettings.Load();
            PlayerFigureBackend previous = settings.Backend;

            if (settings.CampSpineProfile == null)
            {
                settings.CampSpineProfile = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(
                    SpineProfileTool.CampProfileAssetPath);
            }

            if (settings.BattleSpineProfile == null)
            {
                settings.BattleSpineProfile = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(
                    SpineProfileTool.BattleProfileAssetPath);
            }

            if (settings.CampGenericCuteProfile == null)
            {
                settings.CampGenericCuteProfile = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(CampProfilePath);
            }

            if (settings.BattleGenericCuteProfile == null)
            {
                settings.BattleGenericCuteProfile = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(BattleProfilePath);
            }

            settings.Backend = backend;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            // Re-read the asset after the save: saving an asset reimports it, and the instance read
            // before the save stops being the live one (the checks below then saw every profile as
            // missing while the asset on disk was correct).
            settings = PlayerFigureBuildSettings.Load();

            Debug.Log(
                "[PlayerFigureBackendTool] backend " + previous + " -> " + backend + " in " + PlayerFigureBuildSettings.AssetPath);

            if (backend == PlayerFigureBackend.GenericCute)
            {
                if (settings.CampGenericCuteProfile == null || settings.BattleGenericCuteProfile == null)
                {
                    Debug.LogError(
                        "[PlayerFigureBackendTool] the GenericCute profiles are missing: run " +
                        "'ProjectSurvive/Player Figure/1. Create GenericCute profiles' before rebuilding, " +
                        "or the scenes will carry a backend with nothing to draw.");
                }
            }

            bool campOk = ApplyToCamp(backend);
            bool battleOk = ApplyToBattle(backend);

            ProjectSetup.SaveAndRefresh(
                "ProjectSurvive: player figure backend is " + backend + "; camp=" + campOk +
                " battle=" + battleOk + ".");
        }

        private static bool ApplyToCamp(PlayerFigureBackend backend)
        {
            Scene scene = EditorSceneManager.OpenScene(ProjectSetup.CampScenePath, OpenSceneMode.Single);

            // Read the settings after the scene swap: opening a scene unloads assets nothing else
            // references, and the instance SetBackend held before the swap came back as null here
            // (measured, not assumed), which silently wrote a figure with no profile.
            PlayerFigureBuildSettings settings = PlayerFigureBuildSettings.Load();

            var world = Object.FindFirstObjectByType<CampWorldView>(FindObjectsInactive.Include);
            if (world == null)
            {
                Debug.LogError(
                    "[PlayerFigureBackendTool] " + ProjectSetup.CampScenePath + " has no CampWorldView: " +
                    "the camp's backend was not switched.");
                return false;
            }

            // Written through the view's own entry point, so the field assignments are the same ones a
            // runtime Configure makes and the scene save persists them (a SerializedObject on a freshly
            // opened scene did not stick for the profile reference).
            world.ConfigurePlayerFigure(
                backend, settings.CampSpineProfile, settings.CampGenericCuteProfile);
            EditorUtility.SetDirty(world);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static bool ApplyToBattle(PlayerFigureBackend backend)
        {
            Scene scene = EditorSceneManager.OpenScene(ProjectSetup.BattleScenePath, OpenSceneMode.Single);

            // Same re-read as the camp: the settings instance from before the scene swap is not
            // guaranteed to be alive afterwards.
            PlayerFigureBuildSettings settings = PlayerFigureBuildSettings.Load();

            GameObject figureObject = GameObject.Find("PlayerFigure");
            if (figureObject == null)
            {
                Debug.LogError(
                    "[PlayerFigureBackendTool] " + ProjectSetup.BattleScenePath + " has no PlayerFigure object: " +
                    "the arena's backend was not switched.");
                return false;
            }

            // One backend on the object: the old component goes before the new one is added, so the
            // scene can never carry two figures (and a stale profile) at once.
            MonoBehaviour existing = figureObject.GetComponent<IPlayerFigure>() as MonoBehaviour;
            if (existing != null)
            {
                Object.DestroyImmediate(existing, true);
            }

            // The component's own Configure writes the profile field, and the scene save persists it;
            // a SerializedObject written straight after AddComponent did not stick (the scene shipped
            // `profile: {fileID: 0}`), so the typed entry point is what this tool uses.
            MonoBehaviour figure;
            if (backend == PlayerFigureBackend.GenericCute)
            {
                if (settings.BattleGenericCuteProfile == null)
                {
                    Debug.LogError(
                        "[PlayerFigureBackendTool] no GenericCute profile for the arena: run '1. Create " +
                        "GenericCute profiles' first. The arena will report generic_cute.player_profile_missing.");
                }

                var cuteFigure = figureObject.AddComponent<GenericCutePlayerView>();
                cuteFigure.Configure(settings.BattleGenericCuteProfile);
                figure = cuteFigure;
            }
            else
            {
                var spineFigure = figureObject.AddComponent<SpinePlayerView>();
                spineFigure.Configure(settings.BattleSpineProfile);
                figure = spineFigure;
            }

            figure.GetComponent<IPlayerFigure>().SetSortingOrder(
                BattleWorldMap.SortingOrder(RenderEntityKind.Player, 0f));
            EditorUtility.SetDirty(figure);

            var runner = Object.FindFirstObjectByType<BattleSimulationRunner>(FindObjectsInactive.Include);
            if (runner != null)
            {
                ProjectSetup.SetObjectReference(runner, "_viewSettings.PlayerFigure", figure);
            }
            else
            {
                Debug.LogError(
                    "[PlayerFigureBackendTool] " + ProjectSetup.BattleScenePath +
                    " has no BattleSimulationRunner: the view still points at the old figure.");
            }

            // Stage 17f: the status panel's bust follows the same choice.
            var portrait = Object.FindFirstObjectByType<BattleHudPortrait>(FindObjectsInactive.Include);
            if (portrait != null)
            {
                ProjectSetup.SetEnum(portrait, "backend", (int)backend);
                ProjectSetup.SetObjectReference(
                    portrait, "spritePortrait",
                    AssetDatabase.LoadAssetAtPath<Sprite>(PortraitSpritePath));
            }
            else
            {
                Debug.LogError(
                    "[PlayerFigureBackendTool] " + ProjectSetup.BattleScenePath +
                    " has no BattleHudPortrait: the status panel keeps its old bust.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        private static bool WriteProfile(string path, GameObject prefab, AnimatorController controller)
        {
            var profile = AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(path);
            var changes = new List<string>();

            if (profile == null)
            {
                string folder = System.IO.Path.GetDirectoryName(path);
                ProjectSetup.EnsureFolder(folder);

                profile = ScriptableObject.CreateInstance<GenericCutePlayerProfile>();
                AssetDatabase.CreateAsset(profile, path);
                changes.Add("created");
            }

            if (profile.Prefab == null)
            {
                profile.Prefab = prefab;
                changes.Add("prefab='" + prefab.name + "'");
            }

            if (profile.Controller == null)
            {
                profile.Controller = controller;
                changes.Add("controller='" + controller.name + "'");
            }

            if (string.IsNullOrEmpty(profile.IdleState))
            {
                profile.IdleState = "idle";
                changes.Add("idle='idle'");
            }

            if (string.IsNullOrEmpty(profile.RunState))
            {
                profile.RunState = "run";
                changes.Add("run='run'");
            }

            // `hit light` is the pack's short one-shot and the name the user approved for this rig;
            // death stays empty because the pack has no death state (Dead then keeps the last pose).
            if (string.IsNullOrEmpty(profile.HurtState))
            {
                profile.HurtState = "hit light";
                changes.Add("hurt='hit light'");
            }

            EditorUtility.SetDirty(profile);

            bool valid = GenericCuteProfileValidator.TryValidate(
                profile, out string code, out string detail, out string warning);

            if (!valid)
            {
                Debug.LogError(
                    "[PlayerFigureBackendTool] " + path + ": " + code + ": " + detail, profile);
            }
            else if (!string.IsNullOrEmpty(warning))
            {
                Debug.LogWarning("[PlayerFigureBackendTool] " + path + ": " + warning, profile);
            }
            else
            {
                Debug.Log(
                    "[PlayerFigureBackendTool] " + path + ": valid" +
                    (changes.Count == 0 ? " (no field changed)" : " — " + string.Join(", ", changes)) + ".",
                    profile);
            }

            return valid;
        }
    }
}
