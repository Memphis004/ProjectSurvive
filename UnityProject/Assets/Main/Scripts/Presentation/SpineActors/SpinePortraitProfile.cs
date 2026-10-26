using System.Collections.Generic;
using Spine;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// The still bust the battle HUD draws in its status panel: which half-body skeleton, which
    /// animation is its still frame, which skin parts compose the look, and which slots are empty
    /// on purpose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is <b>not</b> a <see cref="SpinePlayerProfile"/>. The bust has no run, no hurt, no death
    /// and no world material, and it is never drawn at a height in world units — it is fitted into a
    /// rectangle in UI pixels. Reusing the player profile would mean inventing four values the art
    /// does not have, so the two looks carry two assets.
    /// </para>
    /// <para>
    /// No name is written in code: the tool (<c>ProjectSurvive/Spine/4. Create portrait profile</c>)
    /// reads them from the skeleton and writes them here, and <see cref="TryValidate"/> refuses a
    /// name the skeleton does not have, listing what it does have.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SpinePortraitProfile", menuName = "ProjectSurvive/Spine Portrait Profile")]
    public sealed class SpinePortraitProfile : ScriptableObject
    {
        /// <summary>No SkeletonData was handed to <see cref="TryValidate"/>.</summary>
        public const string SkeletonMissingCode = "spine.portrait_profile_skeleton_missing";

        /// <summary>The skeleton resource path is empty.</summary>
        public const string SkeletonPathEmptyCode = "spine.portrait_profile_skeleton_path_empty";

        /// <summary>The still animation is not in the skeleton (an empty name means "setup pose").</summary>
        public const string AnimationMissingCode = "spine.portrait_profile_animation_missing";

        /// <summary>A skin part names a skin the skeleton does not have.</summary>
        public const string SkinMissingCode = "spine.portrait_profile_skin_missing";

        /// <summary>An "empty on purpose" slot names a slot the skeleton does not have.</summary>
        public const string SlotsMissingCode = "spine.portrait_profile_slots_missing";

        [SerializeField] private string skeletonResourcePath = SpineAssetPaths.PortraitSkeletonData;

        [Tooltip("Animation held at time 0. Leave empty to draw the setup pose with no animation at all.")]
        [SerializeField] private string stillAnimation = "stand";

        [Tooltip("One skin per part group (body/face/hair/hat), composed into one combined skin at build time.")]
        [SerializeField] private List<SpineActorBase.SkinPart> skinParts = new List<SpineActorBase.SkinPart>();

        [Tooltip("Slots this look leaves empty on purpose. Only an empty slot not listed here is reported at build time.")]
        [SerializeField] private List<string> expectedEmptySlots = new List<string>();

        /// <summary>Resource path of the skeleton, in the form <c>Resources.Load</c> takes it.</summary>
        public string SkeletonResourcePath
        {
            get { return skeletonResourcePath; }
            set { skeletonResourcePath = value; }
        }

        /// <summary>The animation held at time 0, or empty for the setup pose.</summary>
        public string StillAnimation
        {
            get { return stillAnimation; }
            set { stillAnimation = value ?? string.Empty; }
        }

        public IReadOnlyList<SpineActorBase.SkinPart> SkinParts
        {
            get { return skinParts; }
        }

        public IReadOnlyList<string> ExpectedEmptySlots
        {
            get { return expectedEmptySlots; }
        }

        public void SetSkinParts(IEnumerable<SpineActorBase.SkinPart> parts)
        {
            if (skinParts == null)
            {
                skinParts = new List<SpineActorBase.SkinPart>();
            }

            skinParts.Clear();
            if (parts != null)
            {
                skinParts.AddRange(parts);
            }
        }

        public SpineActorBase.SkinPart[] SkinPartArray()
        {
            return skinParts != null ? skinParts.ToArray() : new SpineActorBase.SkinPart[0];
        }

        public void SetExpectedEmptySlots(IEnumerable<string> slots)
        {
            if (expectedEmptySlots == null)
            {
                expectedEmptySlots = new List<string>();
            }

            expectedEmptySlots.Clear();
            if (slots != null)
            {
                expectedEmptySlots.AddRange(slots);
            }
        }

        public string[] ExpectedEmptySlotArray()
        {
            return expectedEmptySlots != null ? expectedEmptySlots.ToArray() : new string[0];
        }

        /// <summary>
        /// Checks this profile against the half-body skeleton it names. False comes with a
        /// <c>noun.problem</c> code and a detail line naming what the skeleton actually offers.
        /// </summary>
        public bool TryValidate(SkeletonData data, out string errorCode, out string detail)
        {
            errorCode = null;
            detail = null;

            if (data == null)
            {
                errorCode = SkeletonMissingCode;
                detail = "no SkeletonData was loaded for the portrait profile";
                return false;
            }

            if (string.IsNullOrEmpty(skeletonResourcePath) || skeletonResourcePath.Trim().Length == 0)
            {
                errorCode = SkeletonPathEmptyCode;
                detail = "skeletonResourcePath is empty";
                return false;
            }

            if (!string.IsNullOrEmpty(stillAnimation) && data.FindAnimation(stillAnimation) == null)
            {
                errorCode = AnimationMissingCode;
                detail = "still animation '" + stillAnimation + "' is not in the skeleton; animations: " + AnimationNames(data);
                return false;
            }

            if (skinParts != null)
            {
                for (int i = 0; i < skinParts.Count; i++)
                {
                    string skin = skinParts[i].skin;
                    if (string.IsNullOrEmpty(skin))
                    {
                        continue;
                    }

                    if (data.FindSkin(skin) == null)
                    {
                        errorCode = SkinMissingCode;
                        detail = "skin part '" + skinParts[i].part + "' names skin '" + skin +
                            "', which the skeleton does not have; skins: " + SkinNames(data);
                        return false;
                    }
                }
            }

            if (expectedEmptySlots != null)
            {
                for (int i = 0; i < expectedEmptySlots.Count; i++)
                {
                    string slot = expectedEmptySlots[i];
                    if (string.IsNullOrEmpty(slot))
                    {
                        continue;
                    }

                    if (data.FindSlot(slot) == null)
                    {
                        errorCode = SlotsMissingCode;
                        detail = "expectedEmptySlots names slot '" + slot +
                            "', which the skeleton does not have; slots: " + SlotNames(data);
                        return false;
                    }
                }
            }

            return true;
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

        private static string SkinNames(SkeletonData data)
        {
            var names = new List<string>();
            for (int i = 0; i < data.Skins.Count; i++)
            {
                if (data.Skins.Items[i] != null)
                {
                    names.Add(data.Skins.Items[i].Name);
                }
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names.ToArray());
        }

        private static string SlotNames(SkeletonData data)
        {
            var names = new List<string>();
            for (int i = 0; i < data.Slots.Count; i++)
            {
                if (data.Slots.Items[i] != null)
                {
                    names.Add(data.Slots.Items[i].Name);
                }
            }

            return names.Count == 0 ? "(none)" : string.Join(", ", names.ToArray());
        }
    }
}
