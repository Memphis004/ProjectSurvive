using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 08e's acceptance, in EditMode: the arena is a deterministic function of (theme, seed),
    /// the centre stays low, nothing in the arena is a collider, the camera is untouched, cards lean
    /// exactly like the actor sprites and are ordered by the world map rather than by the depth
    /// buffer, and day/night is a profile rather than a second set of assets.
    /// </summary>
    /// <remarks>
    /// The layout rules are tested on <see cref="ArenaLayout"/> rather than on the saved scene: the
    /// builder is a straight function from the placements to prefab instances, so proving the
    /// placements deterministic and legal proves the scene the builder writes from them. The prefab
    /// checks then cover the half a placement cannot describe — that the thing instantiated has no
    /// collider.
    /// </remarks>
    public sealed class ArenaEnvironmentTests
    {
        private const string ThemePath = "Assets/Main/Content/Arena/ArenaTheme.asset";
        private const string CardPrefabPath = "Assets/Main/Prefabs/Arena/ArenaCard.prefab";
        private const string DecalPrefabPath = "Assets/Main/Prefabs/Arena/GroundDecal.prefab";

        /// <summary>Seeds the acceptance is checked against, including the shipped one.</summary>
        private static readonly uint[] Seeds = { 20261024u, 1u, 7u, 4242u };

        private static ArenaTheme Theme =>
            AssetDatabase.LoadAssetAtPath<ArenaTheme>(ThemePath)
            ?? throw new AssertionException("the arena theme is missing at " + ThemePath);

        // -------------------------------------------------------------------
        // determinism
        // -------------------------------------------------------------------

        [Test]
        public void SameThemeAndSeed_ProduceTheSameArena()
        {
            ArenaPlacement[] first = ArenaLayout.Build(Theme, 20261024u);
            ArenaPlacement[] second = ArenaLayout.Build(Theme, 20261024u);

            Assert.That(second.Length, Is.EqualTo(first.Length), "the two builds placed a different number of pieces");

            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(second[i].Signature, Is.EqualTo(first[i].Signature),
                    "placement " + i + " differs between two builds of the same seed");
            }

            Assert.That(ProjectSurvive.Game.Editor.ArenaBuilder.SignatureHash(second),
                Is.EqualTo(ProjectSurvive.Game.Editor.ArenaBuilder.SignatureHash(first)));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentArenas()
        {
            uint first = ProjectSurvive.Game.Editor.ArenaBuilder.SignatureHash(ArenaLayout.Build(Theme, 20261024u));
            uint second = ProjectSurvive.Game.Editor.ArenaBuilder.SignatureHash(ArenaLayout.Build(Theme, 12345u));

            Assert.That(first, Is.Not.EqualTo(second), "two seeds produced the same arena, so the seed is not reaching the layout");
        }

        [Test]
        public void EverySeed_ObeysTheLayoutRules()
        {
            for (int i = 0; i < Seeds.Length; i++)
            {
                ArenaPlacement[] placements = ArenaLayout.Build(Theme, Seeds[i]);
                List<string> problems = ArenaLayout.Validate(Theme, placements);

                Assert.That(problems, Is.Empty,
                    "seed " + Seeds[i] + " broke the layout rules: " + string.Join("; ", problems));
            }
        }

        // -------------------------------------------------------------------
        // the centre stays readable
        // -------------------------------------------------------------------

        [Test]
        public void TheCentreHasNothingTallerThanItsCeiling()
        {
            ArenaTheme theme = Theme;
            ArenaPlacement[] placements = ArenaLayout.Build(theme, 20261024u);

            int centre = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                ArenaPlacement placement = placements[i];
                if (placement.Zone != ArenaCardZone.Center)
                {
                    continue;
                }

                centre++;
                Assert.That(placement.Height, Is.LessThanOrEqualTo(theme.CenterMaxHeight + 0.001f),
                    placement.StyleId + " is " + placement.Height.ToString("0.00") + " tall inside the centre");
                Assert.That(placement.Radius, Is.LessThanOrEqualTo(theme.CenterRadius + 0.001f));
            }

            Assert.That(centre, Is.GreaterThan(0), "the centre placed nothing at all");
        }

        [Test]
        public void TheBorderIsOutsideTheRingAndInsideTheArena()
        {
            ArenaTheme theme = Theme;
            ArenaPlacement[] placements = ArenaLayout.Build(theme, 20261024u);

            int border = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                ArenaPlacement placement = placements[i];
                Assert.That(placement.Radius, Is.LessThanOrEqualTo(theme.ArenaRadius + 0.001f),
                    placement.StyleId + " stands outside the arena");

                if (placement.Zone != ArenaCardZone.Border)
                {
                    continue;
                }

                border++;
                Assert.That(placement.Radius, Is.GreaterThanOrEqualTo(theme.RingOuterRadius - 0.001f),
                    "a border tree stands inside the bush ring");
            }

            Assert.That(border, Is.GreaterThan(theme.BorderLayers), "the wall is thinner than its layer count");
        }

        [Test]
        public void TheOpenCentreHoldsNoStandingCard()
        {
            // "ไม่มีการ์ดในรัศมีกลางที่กำหนด": the middle of the arena is where the fight is read,
            // and a bush or a tree standing in it would hide a ghost. Only the centre zone's own low
            // grass may be there, and that is what the zone is for.
            ArenaTheme theme = Theme;

            for (int s = 0; s < Seeds.Length; s++)
            {
                ArenaPlacement[] placements = ArenaLayout.Build(theme, Seeds[s]);
                for (int i = 0; i < placements.Length; i++)
                {
                    ArenaPlacement placement = placements[i];
                    if (placement.IsDecal || placement.Zone == ArenaCardZone.Center)
                    {
                        continue;
                    }

                    Assert.That(placement.Radius, Is.GreaterThanOrEqualTo(theme.CenterRadius - 0.001f),
                        placement.StyleId + " stands in the open centre at radius "
                        + placement.Radius.ToString("0.00") + " (seed " + Seeds[s] + ")");
                }
            }
        }

        [Test]
        public void TheBorderWallIsDenseAndMadeOfBigTrees()
        {
            ArenaTheme theme = Theme;
            ArenaPlacement[] placements = ArenaLayout.Build(theme, 20261024u);

            int border = 0;
            float widest = 0f;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].Zone != ArenaCardZone.Border)
                {
                    continue;
                }

                border++;
                widest = Mathf.Max(widest, placements[i].Width);
            }

            // The wall is the arena's edge, so "a few trees" is a failure: the shipped theme builds
            // three layers whose slots overlap. The floor here is loose on purpose — the layout may
            // drop a slot that would violate the minimum tree spacing.
            Assert.That(border, Is.GreaterThanOrEqualTo(Mathf.RoundToInt(theme.BorderTreeMin * 0.75f)),
                "the border wall placed only " + border + " trees, under the " + theme.BorderTreeMin
                + " the theme asks for");

            // Big trees are part of the design: a player who walks to the edge has to see trunks and
            // canopies passing in front of them, not a hedge at their feet.
            Assert.That(widest, Is.GreaterThanOrEqualTo(8f),
                "the wall holds no big tree (widest border card is " + widest.ToString("0.00") + " units)");
        }

        // -------------------------------------------------------------------
        // scenery, not geometry
        // -------------------------------------------------------------------

        [Test]
        public void CardsAndDecalsCarryNoCollider()
        {
            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            GameObject decal = AssetDatabase.LoadAssetAtPath<GameObject>(DecalPrefabPath);

            Assert.That(card, Is.Not.Null, "the ArenaCard prefab is missing");
            Assert.That(decal, Is.Not.Null, "the GroundDecal prefab is missing");

            Assert.That(card.GetComponentsInChildren<Collider>(true), Is.Empty,
                "a card has a collider, so scenery would block movement");
            Assert.That(decal.GetComponentsInChildren<Collider>(true), Is.Empty,
                "a decal has a collider");
        }

        [Test]
        public void TheThemeCannotReachPastTheSimulationArena()
        {
            Assert.That(Theme.ArenaRadius, Is.EqualTo(BattleViewCaps.ArenaRadius),
                "the theme's arena radius and the simulation's structural bound disagree");
        }

        // -------------------------------------------------------------------
        // the camera and the cards agree
        // -------------------------------------------------------------------

        [Test]
        public void CardsLeanAtTheSamePitchAsTheActorSprites()
        {
            // Stage 08e, 2: no second copy of the billboard angle. The sprite path uses
            // BattleCameraRig.BillboardPitch, which is this same constant.
            Quaternion square = ArenaCard.FacingCamera(0f);

            Assert.That(square.eulerAngles.x, Is.EqualTo(BattleCameraRig.PitchDegrees).Within(0.01f));
            Assert.That(square.eulerAngles.y, Is.EqualTo(0f).Within(0.01f),
                "a card with no yaw is turned off the camera's axis");
        }

        [Test]
        public void CardsAreOrderedByTheWorldMapNotByDepth()
        {
            // A card is one step behind an actor standing on its own row...
            Assert.That(BattleWorldMap.PropSortingOrder(0f),
                Is.LessThan(BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, 0f)));

            // ...so a tree further up the arena draws before a ghost nearer the camera...
            Assert.That(BattleWorldMap.PropSortingOrder(20f),
                Is.LessThan(BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, 0f)));

            // ...and a tree in front of the same ghost draws after it.
            Assert.That(BattleWorldMap.PropSortingOrder(0f),
                Is.GreaterThan(BattleWorldMap.SortingOrder(RenderEntityKind.Enemy, 20f)));
        }

        // -------------------------------------------------------------------
        // the occlusion dither
        // -------------------------------------------------------------------

        [Test]
        public void TheOcclusionDitherOnlyCoversWhatStandsInFrontOfThePlayer()
        {
            // A 4-unit-tall card at z = 10 covers ground rows up to 10 + 4/tan(50°) ≈ 13.36.
            Assert.That(ArenaCardFade.Covers(0f, 10f, 4f, 4f, 0f, 10.5f), Is.True,
                "a card the player stands right behind does not fade");
            Assert.That(ArenaCardFade.Covers(0f, 10f, 4f, 4f, 0f, 13f), Is.True,
                "a player standing under the canopy is not covered by it");
            Assert.That(ArenaCardFade.Covers(0f, 10f, 4f, 4f, 0f, 14f), Is.False,
                "a card fades a player who is above the top it can actually cover");

            // A card further from the camera than the player is drawn behind them.
            Assert.That(ArenaCardFade.Covers(0f, 10f, 4f, 4f, 0f, 9f), Is.False,
                "a card behind the player faded for no reason");

            // The card's own width, plus whatever margin the component asks for.
            Assert.That(ArenaCardFade.Covers(5f, 10f, 4f, 4f, 0f, 10.5f), Is.False,
                "a card far to the side of the player faded");
            Assert.That(ArenaCardFade.Covers(2.5f, 10f, 4f, 4f, 0f, 10.5f, 0.6f), Is.True,
                "the fade does not start before the canopy is dead centre");
        }

        [Test]
        public void AnOccludingCardThinsOutButDoesNotBecomeAHole()
        {
            // The fade is a screen-door, so the card stays in the forest as a sparse silhouette.
            // Driving it to zero would delete a tree the moment the player walks past its trunk.
            var go = new GameObject("ArenaCardFadeProbe");
            try
            {
                ArenaCardFade fade = go.AddComponent<ArenaCardFade>();
                Assert.That(fade.MinFade, Is.GreaterThan(0f),
                    "an occluding card fades to nothing instead of to a faint silhouette");
                Assert.That(fade.MinFade, Is.LessThan(0.6f),
                    "an occluding card stays too solid for the player behind it to read");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TheCardShaderHasAFadeToDitherWith()
        {
            // The component writes _Fade per renderer; the shader is the half that has to honour it.
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Art/Arena/Materials/card-bush.mat");
            Assert.That(material, Is.Not.Null);

            Assert.That(material.HasProperty(ArenaCardFade.FadeProperty), Is.True,
                "card materials have no " + ArenaCardFade.FadeProperty + ", so the dither would do nothing");
        }

        [Test]
        public void TheCardShaderDoesNotWriteDepth()
        {
            // The order above is the only ordering the cards have, so the material must live in the
            // sprite queue — an opaque or alpha-test queue would be drawn before every actor.
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Main/Art/Arena/Materials/card-tree-a.mat");
            Assert.That(material, Is.Not.Null);

            Assert.That(material.renderQueue, Is.EqualTo(ProjectSurvive.Game.Editor.ArenaArtBuilder.CardQueue),
                "cards are not in the sprite queue, so sorting order cannot interleave them with actors");
        }

        // -------------------------------------------------------------------
        // day and night are a profile
        // -------------------------------------------------------------------

        [Test]
        public void AProfileCannotChangeAnAsset()
        {
            // "สลับ Day/Night ได้โดยไม่เปลี่ยน asset": the profile type may not name a material, a
            // sprite or a cubemap at all. The one texture it can name is the optional colour LUT,
            // which is a grade, not the arena's art.
            FieldInfo[] fields = typeof(ArenaLightingProfile).GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                if (typeof(Texture).IsAssignableFrom(type))
                {
                    Assert.That(fields[i].Name, Is.EqualTo(nameof(ArenaLightingProfile.ColorLookup)),
                        fields[i].Name + " is a texture on a lighting profile; night must not need another one");
                    continue;
                }

                Assert.That(typeof(Material).IsAssignableFrom(type), Is.False,
                    fields[i].Name + " is a material on a lighting profile");
                Assert.That(typeof(Sprite).IsAssignableFrom(type), Is.False,
                    fields[i].Name + " is a sprite on a lighting profile");
            }
        }

        [Test]
        public void DayAndNightAreTwoProfilesOfOneTheme()
        {
            ArenaTheme theme = Theme;

            Assert.That(theme.DayProfile, Is.Not.Null);
            Assert.That(theme.NightProfile, Is.Not.Null);
            Assert.That(theme.NightProfile, Is.Not.SameAs(theme.DayProfile));

            Assert.That(theme.DayProfile.IsNight, Is.False, "the day profile lights the arena as night");
            Assert.That(theme.NightProfile.IsNight, Is.True, "the night profile does not light the arena");
        }

        [Test]
        public void LeafSway_IsASwitch()
        {
            // A settings switch, not an asset change: it writes one global the card shader reads.
            var gameObject = new GameObject("arena-sway-test");
            try
            {
                var view = gameObject.AddComponent<ArenaEnvironmentView>();

                view.SetLeafSway(false);
                Assert.That(Shader.GetGlobalFloat(ArenaEnvironmentView.ArenaSwayGlobal), Is.EqualTo(0f));

                view.SetLeafSway(true);
                Assert.That(Shader.GetGlobalFloat(ArenaEnvironmentView.ArenaSwayGlobal), Is.EqualTo(1f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
