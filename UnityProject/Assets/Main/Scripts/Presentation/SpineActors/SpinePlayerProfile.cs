using System.Collections.Generic;
using Spine;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.SpineActors
{
    /// <summary>
    /// The player figure as art: which skeleton, which animation is which pose, which skin parts
    /// compose the look, and how tall it stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are picture numbers, not balance numbers: how tall the figure is drawn and the speed
    /// at which the walk animation starts are decisions about an image, not about the run, so they
    /// live beside the art instead of in <c>data/</c> (AGENTS 5 reserves <c>data/</c> for balance).
    /// </para>
    /// <para>
    /// No animation name, skin name or path is written in code: the names are values in this asset,
    /// filled from the skeleton's own contents by <c>ProjectSurvive/Spine/2. Create player profile</c>
    /// and checked against that skeleton by <see cref="TryValidate"/> before anything is drawn. A
    /// name that does not exist is a hard failure — nothing falls back to "play the first animation".
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "SpinePlayerProfile", menuName = "ProjectSurvive/Spine Player Profile")]
    public sealed class SpinePlayerProfile : ScriptableObject
    {
        /// <summary>No SkeletonData was handed to <see cref="TryValidate"/>.</summary>
        public const string SkeletonMissingCode = "spine.profile_skeleton_missing";

        /// <summary>The skeleton resource path is empty.</summary>
        public const string SkeletonPathEmptyCode = "spine.profile_skeleton_path_empty";

        /// <summary>A required animation name is empty or absent from the skeleton.</summary>
        public const string AnimationMissingCode = "spine.profile_animation_missing";

        /// <summary>A skin part names a skin the skeleton does not have.</summary>
        public const string SkinMissingCode = "spine.profile_skin_missing";

        /// <summary>The world height is zero or negative.</summary>
        public const string HeightInvalidCode = "spine.profile_height_invalid";

        /// <summary>The run threshold is zero or negative (the selector needs a positive one).</summary>
        public const string ThresholdInvalidCode = "spine.profile_threshold_invalid";

        /// <summary>No world material is assigned, so nothing can be drawn.</summary>
        public const string MaterialMissingCode = "spine.profile_material_missing";

        /// <summary>An "empty on purpose" slot names a slot the skeleton does not have.</summary>
        public const string SlotsMissingCode = "spine.profile_slots_missing";

        [SerializeField] private string skeletonResourcePath = SpineAssetPaths.PlayerSkeletonData;

        [Header("Animations (names come from the skeleton)")]
        [SerializeField] private string idleAnimation;
        [SerializeField] private string runAnimation;
        [SerializeField] private string hurtAnimation = string.Empty;
        [SerializeField] private string deathAnimation = string.Empty;

        [Header("Look")]
        [Tooltip("One skin per part group (body/head/face); composed into one combined skin at build time.")]
        [SerializeField] private List<SpineActorBase.SkinPart> skinParts = new List<SpineActorBase.SkinPart>();
        [Tooltip("Material the world mesh draws with. It must use a world-space shader (Spine/Skeleton): a page material authored for SkeletonGraphic is a UI shader and draws nothing on a MeshRenderer.")]
        [SerializeField] private Material worldMaterial;
        [Tooltip("Slots this look leaves empty on purpose (the four weapon slots no skin fills). Only an empty slot that is not listed here is reported at build time.")]
        [SerializeField] private List<string> expectedEmptySlots = new List<string>();

        [Header("World")]
        [Tooltip("How tall the figure is drawn, in world units. The actor's root is scaled to hit it.")]
        [SerializeField] private float targetHeightWorld = 1.6f;
        [Tooltip("Speed at which the run animation starts; it stops again below half of it.")]
        [SerializeField] private float runSpeedThreshold = 0.05f;
        [Tooltip("True when the art's own facing is to the right; the view flips it for the other direction.")]
        [SerializeField] private bool artFacesRight = true;

        /// <summary>Resource path of the skeleton, in the form <c>Resources.Load</c> takes.</summary>
        public string SkeletonResourcePath
        {
            get { return skeletonResourcePath; }
            set { skeletonResourcePath = value; }
        }

        public string IdleAnimation
        {
            get { return idleAnimation; }
            set { idleAnimation = value; }
        }

        public string RunAnimation
        {
            get { return runAnimation; }
            set { runAnimation = value; }
        }

        /// <summary>Optional: empty means this figure has no hurt reaction.</summary>
        public string HurtAnimation
        {
            get { return hurtAnimation; }
            set { hurtAnimation = value ?? string.Empty; }
        }

        /// <summary>Optional: empty means the figure keeps its last animation when it dies.</summary>
        public string DeathAnimation
        {
            get { return deathAnimation; }
            set { deathAnimation = value ?? string.Empty; }
        }

        public float TargetHeightWorld
        {
            get { return targetHeightWorld; }
            set { targetHeightWorld = value; }
        }

        public float RunSpeedThreshold
        {
            get { return runSpeedThreshold; }
            set { runSpeedThreshold = value; }
        }

        public bool ArtFacesRight
        {
            get { return artFacesRight; }
            set { artFacesRight = value; }
        }

        /// <summary>The material the world mesh draws with (created by the profile tool).</summary>
        public Material WorldMaterial
        {
            get { return worldMaterial; }
            set { worldMaterial = value; }
        }

        /// <summary>The part list, in the order the parts were chosen.</summary>
        public IReadOnlyList<SpineActorBase.SkinPart> SkinParts
        {
            get { return skinParts; }
        }

        /// <summary>Slots this look is known to leave empty (never a guess: the tool composes the look first).</summary>
        public IReadOnlyList<string> ExpectedEmptySlots
        {
            get { return expectedEmptySlots; }
        }

        /// <summary>Replaces the declared empty-slot list.</summary>
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

        /// <summary>The declared list in the form the actor's setter wants.</summary>
        public string[] ExpectedEmptySlotArray()
        {
            return expectedEmptySlots != null ? expectedEmptySlots.ToArray() : new string[0];
        }

        /// <summary>Replaces the whole part list (the Editor tool fills it from the skeleton's groups).</summary>
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

        /// <summary>The part list as the actor's setter wants it.</summary>
        public SpineActorBase.SkinPart[] SkinPartArray()
        {
            return skinParts != null ? skinParts.ToArray() : new SpineActorBase.SkinPart[0];
        }

        /// <summary>
        /// Checks this profile against the skeleton it names. Returns false with a
        /// <c>noun.problem</c> code and a detail line naming what it looked for and what the
        /// skeleton actually offers, so a failure in a build log says what to fix.
        /// </summary>
        public bool TryValidate(SkeletonData data, out string errorCode, out string detail)
        {
            errorCode = null;
            detail = null;

            if (data == null)
            {
                errorCode = SkeletonMissingCode;
                detail = "no SkeletonData was loaded for this profile";
                return false;
            }

            if (string.IsNullOrEmpty(skeletonResourcePath) || skeletonResourcePath.Trim().Length == 0)
            {
                errorCode = SkeletonPathEmptyCode;
                detail = "skeletonResourcePath is empty";
                return false;
            }

            if (!RequireAnimation(data, idleAnimation, "idle", out detail))
            {
                errorCode = AnimationMissingCode;
                return false;
            }

            if (!RequireAnimation(data, runAnimation, "run", out detail))
            {
                errorCode = AnimationMissingCode;
                return false;
            }

            if (!IsEmptyOrPresent(data, hurtAnimation))
            {
                errorCode = AnimationMissingCode;
                detail = "hurt animation '" + hurtAnimation + "' is not in the skeleton; animations: " + AnimationNames(data);
                return false;
            }

            if (!IsEmptyOrPresent(data, deathAnimation))
            {
                errorCode = AnimationMissingCode;
                detail = "death animation '" + deathAnimation + "' is not in the skeleton; animations: " + AnimationNames(data);
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
                        detail = "skin part '" + skinParts[i].part + "' names skin '" + skin + "', which the skeleton does not have";
                        return false;
                    }
                }
            }

            if (targetHeightWorld <= 0f || float.IsNaN(targetHeightWorld) || float.IsInfinity(targetHeightWorld))
            {
                errorCode = HeightInvalidCode;
                detail = "targetHeightWorld is " + targetHeightWorld.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }

            if (runSpeedThreshold <= 0f || float.IsNaN(runSpeedThreshold) || float.IsInfinity(runSpeedThreshold))
            {
                errorCode = ThresholdInvalidCode;
                detail = "runSpeedThreshold is " + runSpeedThreshold.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                return false;
            }

            if (worldMaterial == null)
            {
                errorCode = MaterialMissingCode;
                detail = "worldMaterial is empty; the figure would draw nothing (a SkeletonGraphic material cannot draw on a MeshRenderer)";
                return false;
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
                        detail = "expectedEmptySlots names slot '" + slot + "', which the skeleton does not have; slots: " + SlotNames(data);
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool RequireAnimation(SkeletonData data, string name, string role, out string detail)
        {
            if (string.IsNullOrEmpty(name) || data.FindAnimation(name) == null)
            {
                detail = role + " animation '" + (name ?? string.Empty) + "' is not in the skeleton; animations: " + AnimationNames(data);
                return false;
            }

            detail = null;
            return true;
        }

        /// <summary>An optional animation: absent is fine, present-but-unknown is not.</summary>
        private static bool IsEmptyOrPresent(SkeletonData data, string name)
        {
            return string.IsNullOrEmpty(name) || data.FindAnimation(name) != null;
        }

        private static string SlotNames(SkeletonData data)
        {
            var names = new List<string>();
            var slots = data.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots.Items[i] != null)
                {
                    names.Add(slots.Items[i].Name);
                }
            }

            return string.Join(", ", names.ToArray());
        }

        private static string AnimationNames(SkeletonData data)
        {
            var names = new List<string>();
            var animations = data.Animations;
            for (int i = 0; i < animations.Count; i++)
            {
                if (animations.Items[i] != null)
                {
                    names.Add(animations.Items[i].Name);
                }
            }

            return string.Join(", ", names.ToArray());
        }
    }
}
