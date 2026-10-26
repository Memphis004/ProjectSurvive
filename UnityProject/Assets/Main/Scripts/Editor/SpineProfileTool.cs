using System;
using System.Collections.Generic;
using System.Text;
using ProjectSurvive.Game.Presentation.SpineActors;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;
using SpineAnimation = Spine.Animation;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Writes the two player profiles — one per scene — from what the skeleton actually contains,
    /// and refuses to guess when it cannot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One asset per scene, because the idle differs.</b> The camp stands with <c>stand</c> (a
    /// 1.6 s breathing loop) and the arena with <c>idle5</c>; every other pose is the same figure.
    /// Two assets are the smallest way to say that: <c>CampSceneBuilder</c> and
    /// <c>BattleSceneBuilder</c> each assign their own, and no view learns a scene rule.
    /// </para>
    /// <para>
    /// <b>The animation names are written explicitly.</b> Stage 17a matched them by keyword
    /// (<c>idle</c>/<c>stand</c>), which is a guess that happens to be right; this tool is told the
    /// four names and fails loudly — listing every animation the skeleton has — when one is absent.
    /// </para>
    /// <para>
    /// <b>An existing asset keeps its hand-tuned fields.</b> Re-running the tool writes the idle
    /// name only and logs the old and new value, so a height or a skin pick someone adjusted is
    /// never overwritten and a stale idle can never stay behind silently.
    /// </para>
    /// </remarks>
    public static class SpineProfileTool
    {
        /// <summary>The camp's profile: the doctor stands still with the <c>stand</c> loop.</summary>
        public const string CampProfileAssetPath = "Assets/Main/Spine/SpinePlayerProfile_Camp.asset";

        /// <summary>The arena's profile: the doctor stands still with <c>idle5</c>.</summary>
        public const string BattleProfileAssetPath = "Assets/Main/Spine/SpinePlayerProfile_Battle.asset";

        /// <summary>
        /// The Stage 17a asset, kept only as the source of the fields a human tuned. It is not
        /// assigned to anything after this stage; the tool's update path is what moves its values
        /// into the two per-scene assets.
        /// </summary>
        public const string LegacyProfileAssetPath = "Assets/Main/Spine/SpinePlayerProfile.asset";

        /// <summary>The world material the profile points at (one atlas page, so one material).</summary>
        public const string WorldMaterialPath = "Assets/Main/Spine/Materials/SpinePlayer-World.mat";

        private const string WorldShader = "Spine/Skeleton";

        private const float DefaultTargetHeightWorld = 1.6f;
        private const float DefaultRunSpeedThreshold = 0.1f;

        // The names this skeleton has, written down rather than matched by keyword (AGENTS 2: no
        // guessed API, and a name is data). A missing one fails with the full list.
        private const string CampIdleAnimation = "stand";
        private const string BattleIdleAnimation = "idle5";
        private const string RunAnimation = "run";
        private const string HurtAnimation = "hit";
        private const string DeathAnimation = "dead";

        [MenuItem("ProjectSurvive/Spine/2. Create player profiles", priority = 171)]
        public static void CreatePlayerProfiles()
        {
            SkeletonDataAsset asset;
            string loadCode;
            if (!SpineSkeletonLoader.TryLoad(SpineAssetPaths.PlayerSkeletonData, out asset, out loadCode))
            {
                Debug.LogError("[SpineProfileTool] " + loadCode + ": could not load '" + SpineAssetPaths.PlayerSkeletonData + "'");
                return;
            }

            SkeletonData data = asset.GetSkeletonData(true);
            if (data == null)
            {
                Debug.LogError("[SpineProfileTool] " + SpineSkeletonLoader.InvalidCode + ": '" + asset.name + "' produced no SkeletonData");
                return;
            }

            if (!RequireAnimation(data, CampIdleAnimation, "camp idle") ||
                !RequireAnimation(data, BattleIdleAnimation, "battle idle") ||
                !RequireAnimation(data, RunAnimation, "run") ||
                !IsEmptyOrPresent(data, HurtAnimation) ||
                !IsEmptyOrPresent(data, DeathAnimation))
            {
                return;
            }

            Material worldMaterial = EnsureWorldMaterial(asset);
            if (worldMaterial == null)
            {
                Debug.LogError("[SpineProfileTool] no world material could be made for '" + asset.name +
                    "', so no profile was written. The mesh needs a material with a world-space shader;" +
                    " the atlas' own page material is authored for SkeletonGraphic and draws nothing on a MeshRenderer.");
                return;
            }

            WriteProfile(data, CampProfileAssetPath, CampIdleAnimation, worldMaterial);
            WriteProfile(data, BattleProfileAssetPath, BattleIdleAnimation, worldMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Creates the asset when it is absent, or rewrites only its idle name when it exists. In both
        /// cases the result is validated against the skeleton before anything is saved.
        /// </summary>
        private static void WriteProfile(SkeletonData data, string path, string idleAnimation, Material worldMaterial)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(path);
            if (existing != null)
            {
                string previous = existing.IdleAnimation;
                if (previous != idleAnimation)
                {
                    existing.IdleAnimation = idleAnimation;
                    EditorUtility.SetDirty(existing);
                    Debug.Log("[SpineProfileTool] " + path + " idle '" + (previous ?? "(empty)") + "' -> '" +
                        idleAnimation + "'; only the idle field was written, every other field was left as it was.");
                }
                else
                {
                    Debug.Log("[SpineProfileTool] " + path + " already names idle '" + idleAnimation + "'; nothing was changed.");
                }

                ReportValidation(existing, data, path);
                return;
            }

            // A fresh asset inherits the hand-tuned fields of the Stage 17a asset when it is still
            // there, so a height or a skin pick is carried over rather than reset to a default.
            var legacy = AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(LegacyProfileAssetPath);
            var profile = ScriptableObject.CreateInstance<SpinePlayerProfile>();
            profile.SkeletonResourcePath = SpineAssetPaths.PlayerSkeletonData;
            profile.IdleAnimation = idleAnimation;
            profile.RunAnimation = RunAnimation;
            profile.HurtAnimation = HurtAnimation;
            profile.DeathAnimation = DeathAnimation;
            profile.SetSkinParts(PickSkinParts(data));
            profile.TargetHeightWorld = legacy != null ? legacy.TargetHeightWorld : DefaultTargetHeightWorld;
            profile.RunSpeedThreshold = legacy != null ? legacy.RunSpeedThreshold : DefaultRunSpeedThreshold;

            // The material is an art decision too: a profile someone pointed at the outlined world
            // material (Stage 17b) must not be quietly reset to the plain one.
            profile.WorldMaterial = legacy != null && legacy.WorldMaterial != null
                ? legacy.WorldMaterial
                : worldMaterial;

            // Measured in Stage 17e: every one of stand / idle5 / run is drawn facing the viewer's
            // left (artifacts/stage17e/zoom-*-run-*.png, zoom-C-stand-right.png), so the view mirrors
            // it for +x and leaves it for -x.
            profile.ArtFacesRight = false;
            profile.SetExpectedEmptySlots(EmptySlotsFor(data, profile.SkinPartArray()));

            if (!ReportValidation(profile, data, path))
            {
                UnityEngine.Object.DestroyImmediate(profile);
                return;
            }

            EnsureFolder(path);

            AssetDatabase.CreateAsset(profile, path);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var log = new StringBuilder();
            log.Append("[SpineProfileTool] created ").Append(path).AppendLine();
            log.Append("  idle   -> ").Append(idleAnimation).AppendLine();
            log.Append("  run    -> ").Append(RunAnimation).AppendLine();
            log.Append("  hurt   -> ").Append(HurtAnimation).AppendLine();
            log.Append("  death  -> ").Append(DeathAnimation).AppendLine();
            log.Append("  skins  -> ").Append(DescribeParts(profile.SkinPartArray())).AppendLine();
            log.Append("  empty slots on purpose -> ").Append(DescribeNames(profile.ExpectedEmptySlotArray())).AppendLine();
            log.Append("  world material -> ").Append(WorldMaterialPath).AppendLine();
            log.Append("  height -> ").Append(profile.TargetHeightWorld.ToString("0.###")).Append(" world units, run threshold ")
                .Append(profile.RunSpeedThreshold.ToString("0.###")).Append(", art faces right = ").Append(profile.ArtFacesRight);
            Debug.Log(log.ToString());
        }

        private static bool ReportValidation(SpinePlayerProfile profile, SkeletonData data, string path)
        {
            string code;
            string detail;
            if (profile.TryValidate(data, out code, out detail))
            {
                return true;
            }

            Debug.LogError("[SpineProfileTool] " + path + " does not validate: " + code + " (" + detail + ").");
            return false;
        }

        /// <summary>
        /// The slots none of the chosen skins fills. Declaring them is what keeps the build log about
        /// the one empty slot that was not a decision (Stage 17e item 4).
        /// </summary>
        private static List<string> EmptySlotsFor(SkeletonData data, SpineActorBase.SkinPart[] parts)
        {
            var covered = new HashSet<int>();
            if (parts != null)
            {
                for (int p = 0; p < parts.Length; p++)
                {
                    Skin skin = string.IsNullOrEmpty(parts[p].skin) ? null : data.FindSkin(parts[p].skin);
                    if (skin == null)
                    {
                        continue;
                    }

                    foreach (var entry in skin.GetAttachments())
                    {
                        covered.Add(entry.SlotIndex);
                    }
                }
            }

            var empty = new List<string>();
            for (int i = 0; i < data.Slots.Count; i++)
            {
                if (!covered.Contains(i))
                {
                    empty.Add(data.Slots.Items[i].Name);
                }
            }

            return empty;
        }

        /// <summary>
        /// Creates (or reuses) the single material the mesh draws with: a world-space shader over the
        /// atlas page's texture. The atlas' own page material is authored for <c>SkeletonGraphic</c>,
        /// a UI shader that draws nothing on a <see cref="MeshRenderer"/> (measured in the Stage 17a
        /// survey), so the world path needs its own asset rather than a shader swapped at runtime.
        /// </summary>
        private static Material EnsureWorldMaterial(SkeletonDataAsset asset)
        {
            int pages = 0;
            Material pageMaterial = null;
            if (asset.atlasAssets != null)
            {
                for (int i = 0; i < asset.atlasAssets.Length; i++)
                {
                    AtlasAssetBase atlas = asset.atlasAssets[i];
                    if (atlas == null)
                    {
                        continue;
                    }

                    pages += atlas.MaterialCount;
                    if (pageMaterial == null && atlas.MaterialCount > 0)
                    {
                        pageMaterial = atlas.PrimaryMaterial;
                    }
                }
            }

            if (pages != 1)
            {
                Debug.LogError("[SpineProfileTool] the atlas has " + pages +
                    " page(s); this tool writes one world material, which is only correct for a single page.");
                return null;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(WorldMaterialPath);
            if (existing != null)
            {
                Debug.Log("[SpineProfileTool] " + WorldMaterialPath + " already exists; it was reused.");
                return existing;
            }

            Shader shader = Shader.Find(WorldShader);
            if (shader == null)
            {
                Debug.LogError("[SpineProfileTool] shader '" + WorldShader + "' is not in the project.");
                return null;
            }

            EnsureFolder(WorldMaterialPath);

            var material = new Material(shader);
            material.name = "SpinePlayer-World";
            if (pageMaterial != null)
            {
                material.mainTexture = pageMaterial.mainTexture;
            }

            AssetDatabase.CreateAsset(material, WorldMaterialPath);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpineProfileTool] " + WorldMaterialPath + " <- shader '" + WorldShader + "' over texture '" +
                (material.mainTexture != null ? material.mainTexture.name : "(none)") + "'");
            return material;
        }

        private static bool RequireAnimation(SkeletonData data, string name, string role)
        {
            if (!IsEmptyOrPresent(data, name) || string.IsNullOrEmpty(name))
            {
                Debug.LogError("[SpineProfileTool] the " + role + " animation '" + (name ?? string.Empty) +
                    "' is not in the skeleton, so no profile was written. Animations (" + data.Animations.Count +
                    "): " + AnimationNames(data));
                return false;
            }

            return true;
        }

        private static bool IsEmptyOrPresent(SkeletonData data, string name)
        {
            return string.IsNullOrEmpty(name) || data.FindAnimation(name) != null;
        }

        /// <summary>
        /// One skin per part group: the first skin of each group that carries attachments, so a
        /// look left with an empty variant does not render nothing. A skeleton with no part groups
        /// keeps its default skin (an empty list) and says so in the log.
        /// </summary>
        private static List<SpineActorBase.SkinPart> PickSkinParts(SkeletonData data)
        {
            var parts = new List<SpineActorBase.SkinPart>();
            List<SpineActorBase.SkinGroupInfo> groups = SpineActorBase.DetectSkinGroups(data);

            if (groups.Count == 0)
            {
                Debug.Log("[SpineProfileTool] the skeleton has no part skins (one look, no body/head/face groups); " +
                    "the default skin is used.");
                return parts;
            }

            for (int g = 0; g < groups.Count; g++)
            {
                SpineActorBase.SkinGroupInfo group = groups[g];
                string chosen = null;
                for (int i = 0; i < group.skins.Count; i++)
                {
                    Skin skin = group.skins[i];
                    if (skin != null && skin.Attachments.Count > 0)
                    {
                        chosen = skin.Name;
                        break;
                    }
                }

                if (chosen == null)
                {
                    Debug.LogWarning("[SpineProfileTool] part group '" + group.part +
                        "' has no skin with attachments; it was left out.");
                    continue;
                }

                parts.Add(new SpineActorBase.SkinPart { part = group.part, skin = chosen });
            }

            return parts;
        }

        private static string DescribeParts(SpineActorBase.SkinPart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return "(default skin)";
            }

            var text = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(parts[i].part).Append('=').Append(parts[i].skin);
            }

            return text.ToString();
        }

        private static string DescribeNames(string[] names)
        {
            return names == null || names.Length == 0 ? "(none)" : string.Join(", ", names);
        }

        /// <summary>Creates the folders an asset path needs, one level at a time.</summary>
        private static void EnsureFolder(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        private static string AnimationNames(SkeletonData data)
        {
            var names = new List<string>();
            for (int i = 0; i < data.Animations.Count; i++)
            {
                if (data.Animations.Items[i] != null)
                {
                    names.Add(data.Animations.Items[i].Name);
                }
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names.ToArray());
        }
    }
}
