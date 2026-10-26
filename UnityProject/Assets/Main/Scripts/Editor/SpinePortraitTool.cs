using System.Collections.Generic;
using System.Text;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.SpineActors;
using Spine;
using Spine.Unity;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Wires the uploaded half-body bust (<c>Resources/Portrait/2110101</c>) into the project: one
    /// project UI material for its atlas page, and the profile the battle HUD's portrait draws from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bundle arrived with an <b>empty material list</b> on its atlas asset and with the source
    /// game's own outline/stencil material. Neither is usable here: the empty list makes
    /// <c>SpineAtlasAsset.GetAtlas()</c> log <c>Materials not set for atlas asset</c> and leaves the
    /// <c>SkeletonGraphic</c> with no texture, and the game's shader is not ours to copy
    /// (ATTRIBUTION, AGENTS 13). The tool therefore creates a material from the project's own
    /// SkeletonGraphic template (<c>SpineGraphic-Normal</c>) over this page's texture and assigns it
    /// to the atlas asset as the texture source. The portrait itself keeps drawing through the
    /// template material, exactly like the Stage 17d figure.
    /// </para>
    /// <para>
    /// <b>The look is composed, not written in the view.</b> One skin per slot group, chosen greedily
    /// in skeleton order so each skin only contributes slots no earlier skin covers — the result is
    /// body + face + face decoration + hair + hat. It is serialized into the profile, so a future
    /// customisation screen changes the asset rather than the code.
    /// </para>
    /// <para>
    /// Everything is read from the real assets. Nothing is invented: when the skeleton is missing an
    /// animation or the atlas page has no texture, the tool fails and lists what it found.
    /// </para>
    /// </remarks>
    public static class SpinePortraitTool
    {
        public const string PortraitProfileAssetPath = "Assets/Main/Spine/SpinePortraitProfile.asset";

        /// <summary>The SkeletonGraphic material this page's texture is loaded through.</summary>
        public const string PortraitPageMaterialPath = "Assets/Main/Spine/Materials/SpinePortrait-2110101.mat";

        private const string PortraitTexturePath = "Assets/Resources/Portrait/2110101.png";
        private const string PortraitAtlasAssetPath = "Assets/Resources/Portrait/2110101_Atlas.asset";

        // The half-body skeleton's own still frame. Written down rather than guessed; the tool fails
        // with the full animation list when it is absent.
        private const string StillAnimation = "stand";

        [MenuItem("ProjectSurvive/Spine/4. Create portrait profile", priority = 173)]
        public static void CreatePortraitProfile()
        {
            SkeletonDataAsset asset;
            string loadCode;
            if (!SpineSkeletonLoader.TryLoad(SpineAssetPaths.PortraitSkeletonData, out asset, out loadCode))
            {
                Debug.LogError("[SpinePortraitTool] " + loadCode + ": could not load '" +
                    SpineAssetPaths.PortraitSkeletonData + "'");
                return;
            }

            SkeletonData data = asset.GetSkeletonData(true);
            if (data == null)
            {
                Debug.LogError("[SpinePortraitTool] " + SpineSkeletonLoader.InvalidCode + ": '" +
                    asset.name + "' produced no SkeletonData");
                return;
            }

            if (data.FindAnimation(StillAnimation) == null)
            {
                Debug.LogError("[SpinePortraitTool] the skeleton has no '" + StillAnimation +
                    "' animation, so no portrait profile was written. Animations (" + data.Animations.Count +
                    "): " + AnimationNames(data));
                return;
            }

            Material pageMaterial = EnsurePageMaterial(asset);
            if (pageMaterial == null)
            {
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<SpinePortraitProfile>(PortraitProfileAssetPath);
            if (existing != null)
            {
                string previous = existing.StillAnimation;
                if (previous != StillAnimation)
                {
                    existing.StillAnimation = StillAnimation;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[SpinePortraitTool] " + PortraitProfileAssetPath + " still animation '" +
                        (previous ?? "(empty)") + "' -> '" + StillAnimation +
                        "'; only that field was written, every other field was left as it was.");
                }
                else
                {
                    Debug.Log("[SpinePortraitTool] " + PortraitProfileAssetPath +
                        " already names still animation '" + StillAnimation + "'; nothing was changed.");
                }

                ReportValidation(existing, data);
                return;
            }

            var profile = ScriptableObject.CreateInstance<SpinePortraitProfile>();
            profile.SkeletonResourcePath = SpineAssetPaths.PortraitSkeletonData;
            profile.StillAnimation = StillAnimation;
            profile.SetSkinParts(PickLook(data));
            profile.SetExpectedEmptySlots(EmptySlotsFor(data, profile.SkinPartArray()));

            if (!ReportValidation(profile, data))
            {
                Object.DestroyImmediate(profile);
                return;
            }

            AssetDatabase.CreateAsset(profile, PortraitProfileAssetPath);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            log.Append("[SpinePortraitTool] created ").Append(PortraitProfileAssetPath).AppendLine();
            log.Append("  skeleton -> ").Append(SpineAssetPaths.PortraitSkeletonData).AppendLine();
            log.Append("  still    -> ").Append(StillAnimation).Append(" (held at time 0)").AppendLine();
            log.Append("  skins    -> ").Append(DescribeParts(profile.SkinPartArray())).AppendLine();
            log.Append("  empty slots on purpose -> ").Append(DescribeNames(profile.ExpectedEmptySlotArray())).AppendLine();
            log.Append("  atlas page material -> ").Append(PortraitPageMaterialPath).AppendLine();
            log.Append("  skeleton: ").Append(data.Slots.Count).Append(" slots, ").Append(data.Skins.Count)
                .Append(" skins, ").Append(data.Animations.Count).Append(" animations, ").Append(data.Width)
                .Append(" x ").Append(data.Height);
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Makes the atlas asset's one page load <c>2110101.png</c> through the project's own UI
        /// material. Reuses a material that already exists, so re-running never clobbers a hand edit.
        /// </summary>
        private static Material EnsurePageMaterial(SkeletonDataAsset asset)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PortraitTexturePath);
            if (texture == null)
            {
                Debug.LogError("[SpinePortraitTool] " + PortraitTexturePath +
                    " is missing, so the portrait page has no texture and no material was written.");
                return null;
            }

            var byPath = AssetDatabase.LoadAssetAtPath<Material>(PortraitPageMaterialPath);
            if (byPath == null)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(
                    SpineMaterialTool.MaterialFolder + "/SpineGraphic-Normal.mat");
                if (template == null)
                {
                    Debug.LogError("[SpinePortraitTool] the UI material template is missing: run " +
                        "ProjectSurvive/Spine/3. Create UI materials first.");
                    return null;
                }

                byPath = new Material(template);
                byPath.name = "SpinePortrait-2110101";
                AssetDatabase.CreateAsset(byPath, PortraitPageMaterialPath);
                EditorUtility.SetDirty(byPath);
                Debug.Log("[SpinePortraitTool] " + PortraitPageMaterialPath + " <- '" + template.shader.name +
                    "' (the project UI material) over texture '" + texture.name + "'");
            }

            if (byPath.mainTexture != texture)
            {
                byPath.mainTexture = texture;
                EditorUtility.SetDirty(byPath);
                Debug.Log("[SpinePortraitTool] " + PortraitPageMaterialPath + " main texture set to '" + texture.name + "'");
            }

            var atlas = AssetDatabase.LoadAssetAtPath<SpineAtlasAsset>(PortraitAtlasAssetPath);
            if (atlas == null)
            {
                Debug.LogError("[SpinePortraitTool] " + PortraitAtlasAssetPath +
                    " is missing, so the page material could not be attached to the atlas.");
                return null;
            }

            if (atlas.materials == null || atlas.materials.Length != 1 || atlas.materials[0] != byPath)
            {
                atlas.materials = new[] { byPath };
                atlas.Clear();
                EditorUtility.SetDirty(atlas);
                AssetDatabase.SaveAssets();
                Debug.Log("[SpinePortraitTool] " + PortraitAtlasAssetPath + " materials -> " + PortraitPageMaterialPath);
            }

            AssetDatabase.SaveAssets();
            return byPath;
        }

        private static bool ReportValidation(SpinePortraitProfile profile, SkeletonData data)
        {
            string code;
            string detail;
            if (profile.TryValidate(data, out code, out detail))
            {
                return true;
            }

            Debug.LogError("[SpinePortraitTool] " + PortraitProfileAssetPath + " does not validate: " +
                code + " (" + detail + ").");
            return false;
        }

        /// <summary>
        /// One skin per slot group, in skeleton order, keeping a skin only when it fills a slot no
        /// earlier skin did. That is the skeleton's own "one complete look" (body, face, face
        /// decoration, hair, hat) without naming any skin in code.
        /// </summary>
        private static List<SpineActorBase.SkinPart> PickLook(SkeletonData data)
        {
            var parts = new List<SpineActorBase.SkinPart>();
            var covered = new HashSet<int>();

            for (int i = 0; i < data.Skins.Count; i++)
            {
                Skin skin = data.Skins.Items[i];
                if (skin == null || skin.Name == "default" || skin.Attachments.Count == 0)
                {
                    continue;
                }

                var fresh = new List<int>();
                int firstSlot = -1;
                foreach (var entry in skin.GetAttachments())
                {
                    if (covered.Contains(entry.SlotIndex))
                    {
                        continue;
                    }

                    fresh.Add(entry.SlotIndex);
                    if (firstSlot < 0 || entry.SlotIndex < firstSlot)
                    {
                        firstSlot = entry.SlotIndex;
                    }
                }

                if (fresh.Count == 0)
                {
                    continue;
                }

                for (int f = 0; f < fresh.Count; f++)
                {
                    covered.Add(fresh[f]);
                }

                string slotName = data.Slots.Items[firstSlot].Name;
                parts.Add(new SpineActorBase.SkinPart
                {
                    part = SpineActorBase.SkinPartKey(slotName),
                    skin = skin.Name
                });
            }

            return parts;
        }

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
