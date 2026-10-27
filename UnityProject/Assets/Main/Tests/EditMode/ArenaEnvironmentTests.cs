using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";
        private const string MaterialFolder = "Assets/Main/Art/Arena/Materials";
        private const string TextureFolder = "Assets/Main/Art/Arena/Textures";

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
        public void TheBorderWallIsDenseAndStandsThreeToFivePlayersHigh()
        {
            ArenaTheme theme = Theme;
            ArenaPlacement[] placements = ArenaLayout.Build(theme, 20261024u);

            int border = 0;
            float widest = 0f;
            float lowest = float.MaxValue;
            float highest = 0f;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].Zone != ArenaCardZone.Border)
                {
                    continue;
                }

                border++;
                widest = Mathf.Max(widest, placements[i].Width);
                lowest = Mathf.Min(lowest, placements[i].Height);
                highest = Mathf.Max(highest, placements[i].Height);
            }

            // The wall is the arena's edge, so "a few trees" is a failure: the shipped theme builds
            // three layers whose slots overlap. The floor here is loose on purpose — the layout may
            // drop a slot that would violate the minimum tree spacing.
            Assert.That(border, Is.GreaterThanOrEqualTo(Mathf.RoundToInt(theme.BorderTreeMin * 0.75f)),
                "the border wall placed only " + border + " trees, under the " + theme.BorderTreeMin
                + " the theme asks for");

            // Stage 08g replaced "the wall holds a big tree" (a card at least 8 units wide, the
            // 13.5–15.5 unit silhouettes of the first build) with the wall's own rule: 3–5 players
            // high, no taller. A landmark big enough to be seen from the whole arena is a different
            // object, and the stage's answer to "the edge reads as trees" is the camera profile — the
            // player is 9 % of a 17.8-unit frame now, so a 5-player tree already passes the frame's
            // top on its own.
            float shortest = theme.PlayerHeight * theme.WallMinHeightFactor;
            float tallest = theme.PlayerHeight * theme.WallMaxHeightFactor;
            Assert.That(lowest, Is.GreaterThanOrEqualTo(shortest - 0.001f),
                "a border tree stands " + lowest.ToString("0.00") + " units high, under the "
                + shortest.ToString("0.00") + " the theme's " + theme.WallMinHeightFactor.ToString("0")
                + " players asks for");
            Assert.That(highest, Is.LessThanOrEqualTo(tallest + 0.001f),
                "a border tree stands " + highest.ToString("0.00") + " units high, over the "
                + tallest.ToString("0.00") + " the theme's " + theme.WallMaxHeightFactor.ToString("0")
                + " players allows");

            // A wall made of slivers is not a wall either: the cards have to overlap each other at the
            // layer's pitch, which needs real width even though it no longer needs a landmark.
            Assert.That(widest, Is.GreaterThanOrEqualTo(3.5f),
                "the wall holds only slivers (widest border card is " + widest.ToString("0.00") + " units)");
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
        public void ArenaCardShaderUsesItsAlphaClippedShadowCasterPass()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/card-tree-a.mat");
            Assert.That(material, Is.Not.Null, "the arena card material is missing");

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            Assert.That(string.IsNullOrEmpty(shaderPath), Is.False, "the card shader is not a project asset");
            string source = File.ReadAllText(shaderPath);
            StringAssert.Contains("Name \"ShadowCaster\"", source);
            StringAssert.Contains("Tags { \"LightMode\" = \"ShadowCaster\" }", source);
            StringAssert.Contains("clip(alpha - _Cutoff)", source,
                "transparent card texels are not clipped from the shadow map");

            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            Assert.That(card, Is.Not.Null);
            MeshRenderer renderer = card.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),
                "the reusable prefab must not opt every consumer into shadow maps; Battle enables it per instance");
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

        [Test]
        public void TheFloorWritesNoDepthAndIsDrawnBeforeEverythingOnIt()
        {
            // Stage 08h. The floor is the only thing in the arena that is *under* every other
            // material, and it is drawn first by queue. It must not also write depth: a billboard
            // stands on the ground plane with a centre pivot at the camera's 50° tilt, so its lower
            // half hangs below that plane, and a depth-writing floor discards exactly those
            // fragments (ADR 0038). ZWrite is not exposed by the Material API, so the assertion
            // reads the pass states of the shader the material actually uses.
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/ground.mat");
            Assert.That(material, Is.Not.Null, "the arena floor material is missing");

            Assert.That(material.renderQueue, Is.EqualTo(ProjectSurvive.Game.Editor.ArenaArtBuilder.GroundQueue),
                "the floor is not in the opaque queue, so it is not drawn before the decals and actors");
            Assert.That(material.renderQueue,
                Is.LessThan(ProjectSurvive.Game.Editor.ArenaArtBuilder.DecalQueue),
                "the floor is not drawn before the decals that lie on it");

            string path = AssetDatabase.GetAssetPath(material.shader);
            Assert.That(string.IsNullOrEmpty(path), Is.False, "the floor shader is not a project asset");

            string source = File.ReadAllText(path);
            StringAssert.DoesNotContain("ZWrite On", source,
                "the floor writes depth again, which cuts the lower half off every billboard standing on it");
            StringAssert.Contains("ZWrite Off", source, "the floor shader states no depth-write state");
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
        public void DuskAndNightAreTwoProfilesOfOneTheme()
        {
            ArenaTheme theme = Theme;

            Assert.That(theme.DuskProfile, Is.Not.Null, "the theme has no dusk profile");
            Assert.That(theme.NightProfile, Is.Not.Null);
            Assert.That(theme.NightProfile, Is.Not.SameAs(theme.DuskProfile));

            Assert.That(theme.DuskProfile.TimeOfDay, Is.EqualTo(ArenaTimeOfDay.Dusk));
            Assert.That(theme.NightProfile.TimeOfDay, Is.EqualTo(ArenaTimeOfDay.Night));
            Assert.That(theme.DuskProfile.IsNight, Is.False, "the dusk profile lights the arena as night");
            Assert.That(theme.NightProfile.IsNight, Is.True, "the night profile does not light the arena");
        }

        [Test]
        public void NightIsDarkerThanDuskInEveryTermThatMakesItDark()
        {
            // "Night (มืดกว่า)" is a claim a number can hold: the tint, the ambient, the key light
            // and the exposure are all lower, and the two edge treatments are stronger. A profile
            // that only looked darker because of one term would fail here.
            ArenaLightingProfile dusk = Theme.DuskProfile;
            ArenaLightingProfile night = Theme.NightProfile;

            Assert.That(ArenaContrast.RelativeLuminance(Rgb(night.EnvironmentTint)),
                Is.LessThan(ArenaContrast.RelativeLuminance(Rgb(dusk.EnvironmentTint))), "the night tint is not darker");
            Assert.That(ArenaContrast.RelativeLuminance(Rgb(night.AmbientSky)),
                Is.LessThan(ArenaContrast.RelativeLuminance(Rgb(dusk.AmbientSky))), "the night ambient is not darker");
            Assert.That(night.KeyLightIntensity, Is.LessThan(dusk.KeyLightIntensity), "the night key light is not weaker");
            Assert.That(night.PostExposure, Is.LessThan(dusk.PostExposure), "the night exposure is not lower");
            Assert.That(night.ScreenTopGradient, Is.GreaterThanOrEqualTo(dusk.ScreenTopGradient));
            Assert.That(night.VignetteIntensity, Is.GreaterThanOrEqualTo(dusk.VignetteIntensity));
        }

        [Test]
        public void SwitchingProfilesTouchesNoAsset()
        {
            // "สลับ Dusk/Night แล้ว asset เดิมทุกชิ้น": every material, its shader and its texture, and
            // every texture's pixels must be exactly the objects they were before the switch. The
            // signature is identity plus content, so a re-tinted copy of a texture would fail too.
            ArenaTheme theme = Theme;
            var gameObject = new GameObject("arena-profile-switch");
            try
            {
                var view = gameObject.AddComponent<ArenaEnvironmentView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_theme").objectReferenceValue = theme;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                List<string> before = AssetSignatures();
                Assert.That(before, Is.Not.Empty, "there are no arena assets to compare");

                view.SetTimeOfDay(ArenaTimeOfDay.Night);
                Assert.That(view.IsNight, Is.True, "the night switch did not take effect");
                Assert.That(AssetSignatures(), Is.EqualTo(before), "switching to night changed an arena asset");

                view.SetTimeOfDay(ArenaTimeOfDay.Dusk);
                Assert.That(view.IsNight, Is.False);
                Assert.That(AssetSignatures(), Is.EqualTo(before), "switching back to dusk changed an arena asset");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ShadowsAreCoolTealAndNeverBlack()
        {
            // The brief's "teal/น้ำเงินเข้ม (multiply) ห้ามดำสนิท" is a property of the profile, so it
            // can be asserted without a screenshot: the shadow tint stays well above black and the
            // strength never reaches full occlusion.
            foreach (ArenaLightingProfile profile in new[] { Theme.DuskProfile, Theme.NightProfile })
            {
                Color shadow = profile.ShadowColor;
                Assert.That(shadow.r, Is.GreaterThan(0.15f), profile.name + " shadow has no red channel left");
                Assert.That(shadow.g, Is.GreaterThan(0.15f), profile.name + " shadow has no green channel left");
                Assert.That(shadow.b, Is.GreaterThan(shadow.g), profile.name + " shadow is not cool: blue does not exceed green");
                Assert.That(profile.ShadowStrength, Is.LessThan(0.95f), profile.name + " shadow can reach full black");
                Assert.That(profile.ShadowLengthPerHeight, Is.GreaterThan(0f), profile.name + " cards cast no shadow at all");
            }
        }

        // -------------------------------------------------------------------
        // the Battle shadow map
        // -------------------------------------------------------------------

        [Test]
        public void BattleCardsCastAlphaClippedShadowsFromTheSlantedMainLight()
        {
            WithBattleArena(arena =>
            {
                MeshRenderer[] cards = arena.transform.Find("Cards").GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(cards.Length, Is.GreaterThan(100), "the arena has too few scenery cards to verify");
                for (int i = 0; i < cards.Length; i++)
                {
                    Assert.That(cards[i].shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On),
                        cards[i].name + " does not cast its alpha-clipped foliage silhouette");
                    Assert.That(cards[i].receiveShadows, Is.False,
                        cards[i].name + " receives shadows even though cards are unlit sorting layers");
                }

                Light key = arena.transform.Find("Lights/KeyLight").GetComponent<Light>();
                Assert.That(key.shadows, Is.EqualTo(LightShadows.Hard), "Battle's key light does not render the shadow map");
                Assert.That(RenderSettings.sun, Is.EqualTo(key), "the scenery light is not URP's main light");
                Assert.That(Mathf.Abs(key.transform.eulerAngles.x), Is.GreaterThan(0.1f),
                    "the key light is not elevated, so shadows do not travel across the ground");
            });
        }

        [Test]
        public void ArenaFloorAppliesTheMainLightShadowCoverageTint()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/ground.mat");
            Assert.That(material, Is.Not.Null, "the arena floor material is missing");

            string shaderPath = AssetDatabase.GetAssetPath(material.shader);
            Assert.That(string.IsNullOrEmpty(shaderPath), Is.False, "the arena floor shader is not a project asset");
            string source = File.ReadAllText(shaderPath);
            StringAssert.Contains("GetMainLight(TransformWorldToShadowCoord(input.positionWS))", source,
                "the floor does not sample the shadow map even though its shader compiles the variants");
            StringAssert.Contains("shadowCoverage * saturate(_ArenaShadowStrength)", source,
                "the profile's shadow strength does not control the shadow contribution");
            StringAssert.Contains("saturate(_ArenaShadowColor.rgb)", source,
                "the profile's non-black shadow tint is not applied to shadowed floor pixels");
        }

        [Test]
        public void ActorBlobShadowsRemainSeparateFromTheCardShadowMap()
        {
            WithBattleArena(arena =>
            {
                ArenaBlobShadows blobs = arena.GetComponentInChildren<ArenaBlobShadows>(true);
                Assert.That(blobs, Is.Not.Null, "the actors lost their separate blob-shadow renderer");
                Assert.That(blobs.Material, Is.Not.Null);
                Assert.That(blobs.Material.renderQueue, Is.EqualTo(ProjectSurvive.Game.Editor.ArenaArtBuilder.ShadowQueue));
            });
        }

        // -------------------------------------------------------------------
        // readability: thresholds, and the arithmetic the capture measures with
        // -------------------------------------------------------------------

        [Test]
        public void TheReadabilityThresholdsAreSetAndSane()
        {
            ArenaTheme theme = Theme;

            // The brief: thresholds are values in the theme, and they are a floor, not a target.
            Assert.That(theme.MinPlayerContrast, Is.GreaterThan(1.5f));
            Assert.That(theme.MinGhostContrast, Is.GreaterThan(1.5f));
            Assert.That(theme.MinPickupContrast, Is.GreaterThan(1.5f));
            Assert.That(theme.MinTelegraphContrast, Is.GreaterThan(1.1f));
            Assert.That(theme.MinGhostContrast, Is.LessThan(theme.MinPickupContrast),
                "a pickup is a small bright object and has to clear a higher bar than a ghost");
        }

        [Test]
        public void TheContrastMeasureIsTheWcagRatio()
        {
            Assert.That(ArenaContrast.RelativeLuminance(new Color32(255, 255, 255, 255)), Is.EqualTo(1f).Within(0.001f));
            Assert.That(ArenaContrast.RelativeLuminance(new Color32(0, 0, 0, 255)), Is.EqualTo(0f).Within(0.001f));
            Assert.That(ArenaContrast.Ratio(1f, 0f), Is.EqualTo(21f).Within(0.01f));
            Assert.That(ArenaContrast.Ratio(0.4f, 0.4f), Is.EqualTo(1f).Within(0.0001f));

            // A 16 × 16 image: a bright 4 × 4 square on a dark floor. The feature's measurement is
            // the square's luminance, the floor's the surrounding median, and the ratio follows.
            const int size = 16;
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(40, 40, 40, 255);
            }

            var feature = new RectInt(6, 6, 4, 4);
            var floor = ArenaContrast.FloorAround(feature, 4);
            for (int y = feature.yMin; y < feature.yMax; y++)
            {
                for (int x = feature.xMin; x < feature.xMax; x++)
                {
                    pixels[(y * size) + x] = new Color32(220, 220, 220, 255);
                }
            }

            ContrastSample sample = ArenaContrast.Measure(pixels, size, size, feature, floor);
            Assert.That(sample.Feature, Is.GreaterThan(sample.Floor), "the feature was not brighter than the floor");
            Assert.That(sample.Ratio, Is.EqualTo(ArenaContrast.Ratio(sample.Feature, sample.Floor)).Within(0.0001f));
            Assert.That(sample.Ratio, Is.GreaterThan(4f), "a light square on a dark floor measured as low contrast");
            Assert.That(sample.FloorPixels, Is.GreaterThan(0));
        }

        [Test]
        public void TheScreenBandIsBelowTheHudAndDrivenByTheProfile()
        {
            Assert.That(ArenaScreenGradient.SortingOrder, Is.LessThan(0),
                "the band would draw over the HUD's canvases");

            var gameObject = new GameObject("arena-band-test", typeof(Canvas), typeof(UnityEngine.UI.Image));
            try
            {
                var band = gameObject.AddComponent<ArenaScreenGradient>();
                var image = gameObject.GetComponent<UnityEngine.UI.Image>();
                band.Configure(image);

                ArenaTheme theme = Theme;
                band.Apply(theme.DuskProfile.ScreenGradientColor, theme.DuskProfile.ScreenTopGradient);
                Assert.That(band.Visible, Is.True, "the dusk band is not drawn");
                Assert.That(image.color.a, Is.EqualTo(theme.DuskProfile.ScreenTopGradient).Within(0.001f));

                band.Apply(Color.black, 0f);
                Assert.That(band.Visible, Is.False, "a zero-strength band still costs a draw call");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TheMidFieldHazeIsProfileDrivenAndCanBeTurnedOff()
        {
            // The haze across the middle of the frame (Stage 17b follow-up): its own image on the
            // same canvas, because the top band's sprite is a ramp from the top edge and cannot leave
            // the middle strip alone. A zero-strength profile must cost no draw call.
            var gameObject = new GameObject("arena-mid-band-test", typeof(Canvas), typeof(UnityEngine.UI.Image));
            try
            {
                var band = gameObject.AddComponent<ArenaScreenGradient>();
                band.Configure(gameObject.GetComponent<UnityEngine.UI.Image>());

                var midObject = new GameObject("MidBand", typeof(UnityEngine.UI.Image));
                midObject.transform.SetParent(gameObject.transform, worldPositionStays: false);
                var midImage = midObject.GetComponent<UnityEngine.UI.Image>();
                band.ConfigureMid(midImage);

                ArenaTheme theme = Theme;
                Assert.That(theme.DuskProfile.ScreenMidGradient, Is.GreaterThan(0f),
                    "dusk draws no haze across the middle of the frame");

                band.ApplyMid(theme.DuskProfile.ScreenMidColor, theme.DuskProfile.ScreenMidGradient);
                Assert.That(band.MidVisible, Is.True, "dusk's middle haze is not drawn");
                Assert.That(midImage.color.a, Is.EqualTo(theme.DuskProfile.ScreenMidGradient).Within(0.001f));

                band.ApplyMid(Color.white, 0f);
                Assert.That(band.MidVisible, Is.False,
                    "a zero-strength middle band still costs a draw call");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TheShippedSceneCarriesTheMiddleHazeWiredToTheProfile()
        {
            WithBattleArena(arena =>
            {
                var band = arena.GetComponentInChildren<ArenaScreenGradient>(true);
                Assert.That(band, Is.Not.Null, "the arena has no screen band at all");

                Transform mid = band.transform.Find("MidBand");
                Assert.That(mid, Is.Not.Null, "the arena's band has no middle image, so the haze cannot draw");

                var image = mid.GetComponent<UnityEngine.UI.Image>();
                Assert.That(image, Is.Not.Null, "the middle band has no Image");
                Assert.That(image.sprite, Is.Not.Null, "the middle band has no sprite, so it draws a solid rectangle");
                Assert.That(image.raycastTarget, Is.False, "the middle band eats pointer events meant for the HUD");

                // The scene stores the authored band, not the Image's white default: the band is what
                // an Edit-Mode photograph (and the Game view before Play) would otherwise show.
                ArenaTheme theme = Theme;
                Assert.That(image.color.a, Is.EqualTo(theme.DuskProfile.ScreenMidGradient).Within(0.001f),
                    "the scene's middle band does not carry the dusk profile's strength");
            });
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static Color32 Rgb(Color colour) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.b) * 255f),
            255);

        /// <summary>
        /// Opens the shipped Battle scene (or reuses it when it is already open) and hands its Arena
        /// root to the test. Reading the scene is the only way to prove what the builder placed; the
        /// layout tests above prove the placements it placed from.
        /// </summary>
        private static void WithBattleArena(Action<GameObject> action)
        {
            Scene scene = SceneManager.GetSceneByPath(BattleScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject arena = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == ProjectSurvive.Game.Editor.ArenaBuilder.ArenaRootName)
                    {
                        arena = roots[i];
                        break;
                    }
                }

                Assert.That(arena, Is.Not.Null,
                    "the Battle scene has no " + ProjectSurvive.Game.Editor.ArenaBuilder.ArenaRootName + " root");
                action(arena);
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        /// <summary>
        /// Every arena material and texture, by identity and content. Two snapshots of this list are
        /// equal exactly when switching a profile changed nothing on disk.
        /// </summary>
        private static List<string> AssetSignatures()
        {
            var signatures = new List<string>();

            string[] materials = Directory.GetFiles(MaterialFolder, "*.mat");
            Array.Sort(materials, StringComparer.Ordinal);
            for (int i = 0; i < materials.Length; i++)
            {
                string assetPath = materials[i].Replace('\\', '/');
                int index = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                assetPath = assetPath.Substring(index);

                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material is null)
                {
                    continue;
                }

                Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.clear;
                Texture texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                signatures.Add(
                    assetPath + "|" + material.shader.name + "|" + material.renderQueue
                    + "|" + (texture is null ? "none" : texture.GetInstanceID().ToString(CultureInfo.InvariantCulture))
                    + "|" + colour.r.ToString("R", CultureInfo.InvariantCulture)
                    + "," + colour.g.ToString("R", CultureInfo.InvariantCulture)
                    + "," + colour.b.ToString("R", CultureInfo.InvariantCulture)
                    + "," + colour.a.ToString("R", CultureInfo.InvariantCulture));
            }

            string[] textures = Directory.GetFiles(TextureFolder, "*.png");
            Array.Sort(textures, StringComparer.Ordinal);
            for (int i = 0; i < textures.Length; i++)
            {
                string assetPath = textures[i].Replace('\\', '/');
                int index = assetPath.IndexOf("Assets/", StringComparison.Ordinal);
                assetPath = assetPath.Substring(index);

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture is null)
                {
                    continue;
                }

                signatures.Add(
                    assetPath + "|" + texture.width + "x" + texture.height
                    + "|" + texture.GetInstanceID().ToString(CultureInfo.InvariantCulture));
            }

            return signatures;
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
