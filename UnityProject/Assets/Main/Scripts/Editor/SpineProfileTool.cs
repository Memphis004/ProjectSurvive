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
    /// Creates <c>Assets/Main/Spine/SpinePlayerProfile.asset</c> from what the skeleton actually
    /// contains, and refuses to guess when it cannot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The animation names are matched with keywords — <c>idle</c>/<c>stand</c>, <c>run</c>/<c>walk</c>,
    /// <c>hurt</c>/<c>hit</c>, <c>death</c>/<c>die</c> — because the skeleton was exported before
    /// this project had a naming convention of its own, and a name written by hand in code would be
    /// a guess (AGENTS 2). The choice is logged, so a wrong pick is visible rather than assumed: the
    /// resolved names are printed, not just "created".
    /// </para>
    /// <para>
    /// Idle and run are required: without them the figure cannot show the two states a survivor
    /// spends the whole run in, so the tool fails and lists every animation instead of writing an
    /// asset that would fail later. An existing asset is never overwritten — a hand-tuned height or
    /// a different skin pick is someone's work.
    /// </para>
    /// </remarks>
    public static class SpineProfileTool
    {
        public const string ProfileAssetPath = "Assets/Main/Spine/SpinePlayerProfile.asset";

        /// <summary>The world material the profile points at (one atlas page, so one material).</summary>
        public const string WorldMaterialPath = "Assets/Main/Spine/Materials/SpinePlayer-World.mat";

        private const string WorldShader = "Spine/Skeleton";

        private const float DefaultTargetHeightWorld = 1.6f;
        private const float DefaultRunSpeedThreshold = 0.1f;

        private static readonly string[] IdleKeywords = { "idle", "stand" };
        private static readonly string[] RunKeywords = { "run", "walk" };
        private static readonly string[] HurtKeywords = { "hurt", "hit" };
        // "dead" is the same word family as "death"/"die" and is what this export calls it; the
        // keyword list is how the tool reads the skeleton instead of guessing a name in code.
        private static readonly string[] DeathKeywords = { "death", "die", "dead" };

        [MenuItem("ProjectSurvive/Spine/2. Create player profile", priority = 171)]
        public static void CreatePlayerProfile()
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

            if (AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(ProfileAssetPath) != null)
            {
                Debug.LogWarning("[SpineProfileTool] " + ProfileAssetPath + " already exists; it was not overwritten.");
                return;
            }

            string idle = FindAnimation(data, IdleKeywords);
            string run = FindAnimation(data, RunKeywords);

            if (idle == null || run == null)
            {
                Debug.LogError(
                    "[SpineProfileTool] the skeleton has no " + (idle == null ? "idle/stand" : "run/walk") +
                    " animation, so no profile was created. Animations (" + data.Animations.Count + "): " +
                    AnimationNames(data));
                return;
            }

            string hurt = FindAnimation(data, HurtKeywords);
            string death = FindAnimation(data, DeathKeywords);
            List<SpineActorBase.SkinPart> parts = PickSkinParts(data);

            Material worldMaterial = EnsureWorldMaterial(asset);
            if (worldMaterial == null)
            {
                Debug.LogError("[SpineProfileTool] no world material could be made for '" + asset.name +
                    "', so no profile was created. The mesh needs a material with a world-space shader;" +
                    " the atlas' own page material is authored for SkeletonGraphic and draws nothing on a MeshRenderer.");
                return;
            }

            var profile = ScriptableObject.CreateInstance<SpinePlayerProfile>();
            profile.SkeletonResourcePath = SpineAssetPaths.PlayerSkeletonData;
            profile.IdleAnimation = idle;
            profile.RunAnimation = run;
            profile.HurtAnimation = hurt;
            profile.DeathAnimation = death;
            profile.SetSkinParts(parts);
            profile.TargetHeightWorld = DefaultTargetHeightWorld;
            profile.RunSpeedThreshold = DefaultRunSpeedThreshold;
            profile.ArtFacesRight = true;
            profile.WorldMaterial = worldMaterial;

            var validateCode = string.Empty;
            if (!profile.TryValidate(data, out validateCode, out string detail))
            {
                Debug.LogError("[SpineProfileTool] the profile this tool just filled does not validate: " +
                    validateCode + " (" + detail + "). No asset was written.");
                UnityEngine.Object.DestroyImmediate(profile);
                return;
            }

            EnsureFolder(ProfileAssetPath);

            AssetDatabase.CreateAsset(profile, ProfileAssetPath);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            log.Append("[SpineProfileTool] created ").Append(ProfileAssetPath).AppendLine();
            log.Append("  idle   -> ").Append(idle).AppendLine();
            log.Append("  run    -> ").Append(run).AppendLine();
            log.Append("  hurt   -> ").Append(hurt ?? "(none)").AppendLine();
            log.Append("  death  -> ").Append(death ?? "(none)").AppendLine();
            log.Append("  skins  -> ").Append(parts.Count == 0 ? "(default skin)" : DescribeParts(parts)).AppendLine();
            log.Append("  world material -> ").Append(WorldMaterialPath).AppendLine();
            log.Append("  height -> ").Append(DefaultTargetHeightWorld.ToString("0.###")).Append(" world units, run threshold ")
                .Append(DefaultRunSpeedThreshold.ToString("0.###"));
            Debug.Log(log.ToString());
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
                    if (pageMaterial == null && atlas.PrimaryMaterial != null)
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

        /// <summary>First animation whose name contains one of the keywords, in skeleton order; null when none does.</summary>
        private static string FindAnimation(SkeletonData data, string[] keywords)
        {
            for (int k = 0; k < keywords.Length; k++)
            {
                for (int i = 0; i < data.Animations.Count; i++)
                {
                    SpineAnimation animation = data.Animations.Items[i];
                    if (animation == null || string.IsNullOrEmpty(animation.Name))
                    {
                        continue;
                    }

                    if (animation.Name.IndexOf(keywords[k], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return animation.Name;
                    }
                }
            }

            return null;
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

        private static string DescribeParts(List<SpineActorBase.SkinPart> parts)
        {
            var text = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }

                text.Append(parts[i].part).Append('=').Append(parts[i].skin);
            }

            return text.ToString();
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
