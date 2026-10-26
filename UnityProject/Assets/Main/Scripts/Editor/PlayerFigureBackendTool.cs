using System.Collections.Generic;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.GenericCute;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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

            // The scenes carry the choice, so choosing is a rebuild of both of them.
            CampSceneBuilder.Create();
            BattleSceneBuilder.Create();

            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.SaveAndRefresh(
                "ProjectSurvive: player figure backend is " + backend + "; Camp and Battle rebuilt.");
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
