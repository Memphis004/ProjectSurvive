using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.GenericCute;
using ProjectSurvive.Game.Presentation.SpineActors;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// The one place the backend choice is written: which implementation draws the player, and the
    /// profiles the scene builders assign for each scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Editor-only, on purpose.</b> The runtime never reads this asset: the builders bake both the
    /// component and its profile into the scenes, so a shipped scene carries its choice as ordinary
    /// serialized data and there is nothing to switch at runtime (the stage does not ask for that).
    /// </para>
    /// <para>
    /// <b>Spine is the default.</b> A fresh project has no GenericCute assets, and the live game's look
    /// is the Spine doctor; the generic-cute rig is opt-in per this asset (<c>ProjectSurvive/Player
    /// Figure/3. Use GenericCute backend</c>). The reference document's default is the other way
    /// round because it describes another project — this project's GDD still names the Spine look.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "PlayerFigureBuild", menuName = "ProjectSurvive/Player Figure Build Settings")]
    public sealed class PlayerFigureBuildSettings : ScriptableObject
    {
        /// <summary>Where the tool keeps the single settings asset.</summary>
        public const string AssetPath = "Assets/Main/Presentation/PlayerFigureBuild.asset";

        [Tooltip("Which backend the scene builders put in the Camp and Battle scenes.")]
        [SerializeField] private PlayerFigureBackend backend = PlayerFigureBackend.Spine;

        [Tooltip("The camp's Spine profile (the one 17e made stand with `stand`).")]
        [SerializeField] private SpinePlayerProfile campSpineProfile;

        [Tooltip("The arena's Spine profile (the one 17e made stand with `idle5`).")]
        [SerializeField] private SpinePlayerProfile battleSpineProfile;

        [Tooltip("The camp's 2D Animation profile (its stand is the rig's own idle — both scenes share one idle on this backend).")]
        [SerializeField] private GenericCutePlayerProfile campGenericCuteProfile;

        [Tooltip("The arena's 2D Animation profile.")]
        [SerializeField] private GenericCutePlayerProfile battleGenericCuteProfile;

        /// <summary>Which backend the builders bake in.</summary>
        public PlayerFigureBackend Backend
        {
            get { return backend; }
            set { backend = value; }
        }

        public SpinePlayerProfile CampSpineProfile
        {
            get { return campSpineProfile; }
            set { campSpineProfile = value; }
        }

        public SpinePlayerProfile BattleSpineProfile
        {
            get { return battleSpineProfile; }
            set { battleSpineProfile = value; }
        }

        public GenericCutePlayerProfile CampGenericCuteProfile
        {
            get { return campGenericCuteProfile; }
            set { campGenericCuteProfile = value; }
        }

        public GenericCutePlayerProfile BattleGenericCuteProfile
        {
            get { return battleGenericCuteProfile; }
            set { battleGenericCuteProfile = value; }
        }

        /// <summary>Loads the settings asset, creating it with the Spine defaults when it is absent.</summary>
        public static PlayerFigureBuildSettings Load()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PlayerFigureBuildSettings>(AssetPath);
            if (settings != null)
            {
                return settings;
            }

            string folder = System.IO.Path.GetDirectoryName(AssetPath);
            ProjectSetup.EnsureFolder(folder);

            settings = CreateInstance<PlayerFigureBuildSettings>();
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerFigureBuildSettings] created " + AssetPath + " with the Spine backend.");
            return settings;
        }
    }
}
