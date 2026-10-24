using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using ProjectSurvive.Game.Presentation.Camp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 09o's acceptance, in EditMode: ศาลาพัก's yard is the arena's card system with the camp's
    /// numbers — one deterministic function of (theme, seed), nothing tall or clickable inside the
    /// walking area, the four stations and the เซียมซี cylinder still lit and still clickable, and the
    /// two camp profiles differing in the terms that make dusk warmer than night.
    /// </summary>
    /// <remarks>
    /// Like <see cref="ArenaEnvironmentTests"/>, the layout rules are tested on the placements rather
    /// than on the saved scene — the builder is a straight function from a placement to a prefab
    /// instance — and the scene is opened for the half a placement cannot describe: that the thing
    /// instantiated has no collider and that every shadow follows the profile's one direction.
    /// </remarks>
    public sealed class CampYardTests
    {
        private const string CampScenePath = "Assets/Main/Scenes/Camp.unity";
        private const string ThemePath = "Assets/Main/Content/Camp/CampTheme.asset";
        private const string DuskProfilePath = "Assets/Main/Content/Camp/CampLightingDusk.asset";
        private const string NightProfilePath = "Assets/Main/Content/Camp/CampLightingNight.asset";
        private const string ArenaThemePath = "Assets/Main/Content/Arena/ArenaTheme.asset";
        private const string CardPrefabPath = "Assets/Main/Prefabs/Arena/ArenaCard.prefab";
        private const string DecalPrefabPath = "Assets/Main/Prefabs/Arena/GroundDecal.prefab";
        private const string CardsChild = "Cards";
        private const string DecalsChild = "Decals";
        private const string ShadowsChild = "CardShadows";
        private const string PoolsChild = "LightPools";

        private static CampTheme Theme =>
            AssetDatabase.LoadAssetAtPath<CampTheme>(ThemePath)
            ?? throw new AssertionException("the camp theme is missing at " + ThemePath);

        // -------------------------------------------------------------------
        // the yard is a function of (theme, seed)
        // -------------------------------------------------------------------

        [Test]
        public void TheSameSeedBuildsTheSameYard()
        {
            ArenaPlacement[] first = CampYard.Build(Theme);
            ArenaPlacement[] second = CampYard.Build(Theme);

            Assert.That(first.Length, Is.GreaterThan(8), "the yard placed almost nothing");
            Assert.That(second.Length, Is.EqualTo(first.Length), "two builds placed a different number of pieces");

            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(second[i].Signature, Is.EqualTo(first[i].Signature),
                    "piece " + i + " differs between two builds of the same seed");
            }
        }

        [Test]
        public void TheYardIsTheLayoutGenerator_NotASecondOne()
        {
            // "seed เดิมได้ลานเดิม" has to be the arena's own generator under the camp's numbers: the
            // camp's pieces are a filtered subset of the same seeded draw, so a change to the layout
            // reaches both places or neither.
            var layout = new HashSet<string>();
            ArenaPlacement[] raw = ArenaLayout.Build(Theme, CampYard.YardSeed);
            for (int i = 0; i < raw.Length; i++)
            {
                layout.Add(raw[i].Signature);
            }

            ArenaPlacement[] yard = CampYard.Build(Theme);
            int seeded = 0;
            for (int i = 0; i < yard.Length; i++)
            {
                // The one piece the camp adds itself is the small origin patch.
                if (layout.Contains(yard[i].Signature))
                {
                    seeded++;
                }
            }

            Assert.That(seeded, Is.GreaterThan(yard.Length - 2),
                "the yard holds pieces the seeded layout never placed");
        }

        // -------------------------------------------------------------------
        // nothing tall or blocking where the player walks
        // -------------------------------------------------------------------

        [Test]
        public void NothingTallStandsInsideTheWalkArea()
        {
            WithCamp((root, world) => RulesHold(world.WalkArea));
        }

        private static void RulesHold(Rect walk)
        {
            CampTheme theme = Theme;
            ArenaPlacement[] placements = CampYard.Build(theme);

            Assert.That(CampYard.Validate(theme, placements, walk), Is.Empty,
                "the yard broke its own rules");

            for (int i = 0; i < placements.Length; i++)
            {
                ArenaPlacement placement = placements[i];
                if (placement.IsDecal)
                {
                    continue;
                }

                Rect footprint = CampYard.Footprint(placement.X, placement.Z, placement.Width);
                Assert.That(footprint.Overlaps(walk), Is.False,
                    placement.StyleId + " stands in the walking area at ("
                    + placement.X.ToString("0.00") + ", " + placement.Z.ToString("0.00") + ")");
            }

            for (int i = 0; i < CampYard.Fixtures.Length; i++)
            {
                CampFixture fixture = CampYard.Fixtures[i];
                ArenaCardStyle style = CampYard.StyleOf(theme.FixtureStyles, fixture.StyleId);
                Assert.That(style, Is.Not.Null, "the fixture style '" + fixture.StyleId + "' is not in the camp theme");
                Assert.That(CampYard.Footprint(fixture.X, fixture.Z, style.Width).Overlaps(walk), Is.False,
                    "the fixture '" + fixture.StyleId + "' stands in the walking area");
            }
        }

        [Test]
        public void TheWalkingAreaIsStillTheOneTheWalkerUsedBeforeThisStage()
        {
            // "ห้ามเปลี่ยน... walk area": the rectangle is the pre-change contract, so it is asserted
            // as a literal rather than re-derived from the scene it is being compared with.
            WithCamp((root, world) =>
            {
                Rect walk = world.WalkArea;

                Assert.That(walk.xMin, Is.EqualTo(-8.4f).Within(0.001f));
                Assert.That(walk.xMax, Is.EqualTo(8.4f).Within(0.001f));
                Assert.That(walk.yMin, Is.EqualTo(-3.6f).Within(0.001f));
                Assert.That(walk.yMax, Is.EqualTo(2.6f).Within(0.001f));

                // The furniture row is behind the walk area's far edge, which is why the player can
                // never stand inside a shelf and why the yard may not build anything standing there.
                Assert.That(walk.yMax, Is.LessThan(3.4f - 0.001f),
                    "the walk area now reaches the shelf row");
            });
        }

        // -------------------------------------------------------------------
        // scenery, not geometry and not input
        // -------------------------------------------------------------------

        [Test]
        public void CardsAndDecalsCarryNoCollider()
        {
            GameObject card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            GameObject decal = AssetDatabase.LoadAssetAtPath<GameObject>(DecalPrefabPath);

            Assert.That(card, Is.Not.Null, "the ArenaCard prefab is missing");
            Assert.That(decal, Is.Not.Null, "the GroundDecal prefab is missing");

            Assert.That(card.GetComponentsInChildren<Collider>(true), Is.Empty, "a card has a 3D collider");
            Assert.That(card.GetComponentsInChildren<Collider2D>(true), Is.Empty, "a card has a 2D collider");
            Assert.That(decal.GetComponentsInChildren<Collider>(true), Is.Empty, "a decal has a 3D collider");
            Assert.That(decal.GetComponentsInChildren<Collider2D>(true), Is.Empty, "a decal has a 2D collider");
        }

        [Test]
        public void TheYardInTheSceneIsNeitherSolidNorClickable()
        {
            WithCamp((root, world) =>
            {
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty,
                    "the yard has a collider, so it would block the walker or the mouse");
                Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty,
                    "the yard has a 2D collider");

                Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
                for (int i = 0; i < graphics.Length; i++)
                {
                    Assert.That(graphics[i].raycastTarget, Is.False,
                        graphics[i].name + " inside the yard is a raycast target, so it could swallow a click");
                }

                // The one canvas in the yard is the profile-driven screen band. It is a screen-space
                // overlay whose image is not a raycast target, so it cannot swallow a click; any
                // other canvas here would be a second UI layer the camp did not ask for.
                Canvas[] canvases = root.GetComponentsInChildren<Canvas>(true);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Assert.That(canvases[i].name, Is.EqualTo("ScreenGradient"),
                        canvases[i].name + " is a canvas inside the yard");
                    Assert.That(canvases[i].renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                }
            });
        }

        // -------------------------------------------------------------------
        // what the builder actually placed
        // -------------------------------------------------------------------

        [Test]
        public void TheSceneYardIsBuiltFromTheCampTheme()
        {
            WithCamp((root, world) =>
            {
                Transform cards = Find(root.transform, CardsChild);
                Transform decals = Find(root.transform, DecalsChild);
                Transform shadows = Find(root.transform, ShadowsChild);
                Transform pools = Find(root.transform, PoolsChild);

                Assert.That(cards, Is.Not.Null, "the camp has no " + CardsChild + " group");
                Assert.That(decals, Is.Not.Null, "the camp has no " + DecalsChild + " group");
                Assert.That(shadows, Is.Not.Null, "the camp has no " + ShadowsChild + " group");
                Assert.That(pools, Is.Not.Null, "the camp has no " + PoolsChild + " group");

                ArenaCard[] placed = cards.GetComponentsInChildren<ArenaCard>(true);
                Assert.That(placed.Length, Is.GreaterThan(8), "the yard placed almost no cards");

                for (int i = 0; i < placed.Length; i++)
                {
                    Vector3 at = placed[i].transform.position;
                    Assert.That(IsInTheFrame(at.x, at.z), Is.True,
                        placed[i].name + " (" + placed[i].StyleId + ") stands at ("
                        + at.x.ToString("0.00") + ", " + at.z.ToString("0.00")
                        + "), which is neither the frame of the clearing nor a fixed fixture");

                    Assert.That(placed[i].Zone, Is.Not.EqualTo(ArenaCardZone.Decal),
                        "a decal was placed as a standing card");
                }

                Assert.That(decals.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(3),
                    "the yard has almost no ground decals");

                // One shadow per card is the arena's rule; the camp keeps it rather than inventing
                // a second shadow scheme for a smaller yard.
                Assert.That(shadows.GetComponentsInChildren<ArenaShadowCard>(true).Length,
                    Is.EqualTo(placed.Length), "not every yard card casts one fake shadow");
            });
        }

        [Test]
        public void EveryShadowFollowsTheProfilesOneDirection()
        {
            WithCamp((root, world) =>
            {
                Transform shadows = Find(root.transform, ShadowsChild);
                Assert.That(shadows, Is.Not.Null);

                ArenaShadowCard[] placed = shadows.GetComponentsInChildren<ArenaShadowCard>(true);
                float expected = Theme.DuskProfile.ShadowDirectionDegrees;

                for (int i = 0; i < placed.Length; i++)
                {
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(placed[i].DirectionDegrees, expected)), Is.LessThan(0.05f),
                        placed[i].name + " points at " + placed[i].DirectionDegrees.ToString("0.00")
                        + " but the dusk profile says " + expected.ToString("0.00"));
                }

                Assert.That(placed.Length, Is.GreaterThan(0), "no shadow was placed, so the rule tests nothing");
            });
        }

        [Test]
        public void NoShadowDrawsALargePatchOnTheWalkableFloor()
        {
            // "ไม่มีเงาปื้นใหญ่ในพื้นที่เดิน": the camp builds every card shadow as a narrow cast shadow
            // (canopy: false), so no wide crown patch can land on the sala's floor at all. The rule
            // is asserted as the property that produces it, not just as a rectangle test that would
            // pass on a yard with no shadows in it.
            WithCamp((root, world) =>
            {
                Rect walk = world.WalkArea;
                Transform shadows = Find(root.transform, ShadowsChild);
                Assert.That(shadows, Is.Not.Null, "the camp has no shadow group");

                ArenaShadowCard[] placed = shadows.GetComponentsInChildren<ArenaShadowCard>(true);
                Assert.That(placed.Length, Is.GreaterThan(0), "the yard casts no shadow at all");

                for (int i = 0; i < placed.Length; i++)
                {
                    Assert.That(placed[i].IsCanopy, Is.False,
                        placed[i].name + " is a crown patch, which the camp's cards do not have");

                    Vector3 at = placed[i].transform.position;
                    if (walk.Contains(new Vector2(at.x, at.z)))
                    {
                        // A shadow that lands where the player walks has to be the thin cast shadow
                        // of a tuft, not a pool of shade.
                        Assert.That(placed[i].Width, Is.LessThanOrEqualTo(2f),
                            "a shadow " + placed[i].Width.ToString("0.00") + " wide lies on the walkable floor at " + at);
                    }
                }
            });
        }

        // -------------------------------------------------------------------
        // the stations are still the stations
        // -------------------------------------------------------------------

        [Test]
        public void EveryUsagePointHasWarmLightUnderIt()
        {
            // Work item 4's "แอ่งแสงอุ่นรอบศาลาและชั้นวาง" as a measurement: the four shelves, the
            // เซียมซี cylinder and the incense pot each have a pool of their own, close enough to be
            // the light under that object rather than a lamp somewhere in the yard.
            CampTheme theme = Theme;
            Assert.That(theme.DuskProfile.LightPoolIntensity, Is.GreaterThan(0f),
                "the dusk camp profile has no light pools at all");

            for (int i = 0; i < CampYard.LightPools.Length; i++)
            {
                Assert.That(CampYard.LightPools[i].Width, Is.GreaterThan(1f));
                Assert.That(CampYard.LightPools[i].Depth, Is.GreaterThan(1f));
            }

            WithCamp((root, world) =>
            {
                var points = new List<Vector3>
                {
                    world.ShelfPosition(0), world.ShelfPosition(1), world.ShelfPosition(2), world.ShelfPosition(3),
                    world.ResetPosition,
                };

                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 point = points[i];
                    bool lit = false;
                    for (int p = 0; p < CampYard.LightPools.Length && !lit; p++)
                    {
                        CampLightPool pool = CampYard.LightPools[p];
                        lit = Mathf.Abs(pool.X - point.x) <= 1.6f && Mathf.Abs(pool.Z - point.z) <= 1.8f;
                    }

                    Assert.That(lit, Is.True,
                        "no warm pool stands under the usage point at (" + point.x.ToString("0.00")
                        + ", " + point.z.ToString("0.00") + ")");
                }
            });
        }

        [Test]
        public void TheWorldSpaceLabelsStayAtTheirSizeAndTheirFloor()
        {
            // Work item 5: the boards are reported, not resized or recoloured. The label floor is the
            // 1080p text floor this project states; the contrast floor is the pre-yard measurement,
            // because the boards are unlit UI and a lighting change cannot move them.
            CampTheme theme = Theme;

            Assert.That(theme.LabelFloorPixels, Is.EqualTo(22f), "the project's 1080p text floor changed");
            Assert.That(theme.MinLabelContrast, Is.GreaterThan(1.9f),
                "the non-regression floor is below what the price boards measured before the yard");
            Assert.That(theme.MinLabelContrast, Is.LessThan(2.5f),
                "the price boards cannot reach a higher contrast from lighting, so this is a target, not a floor");
            Assert.That(theme.MinPropContrast, Is.GreaterThan(1.5f));
            Assert.That(theme.MinPlayerContrast, Is.GreaterThan(1.5f));
            Assert.That(theme.MinPropContrast, Is.LessThan(theme.MinPlayerContrast),
                "the player is the thing that must be found first, so its floor is the higher one");
        }

        // -------------------------------------------------------------------
        // dusk-camp and night are two profiles of one theme
        // -------------------------------------------------------------------

        [Test]
        public void TheCampHasItsOwnTwoProfilesOfTheSameType()
        {
            CampTheme theme = Theme;

            Assert.That(AssetDatabase.LoadAssetAtPath<ArenaLightingProfile>(DuskProfilePath), Is.SameAs(theme.DuskProfile),
                "the camp's dusk profile is not the one the theme points at");
            Assert.That(AssetDatabase.LoadAssetAtPath<ArenaLightingProfile>(NightProfilePath), Is.SameAs(theme.NightProfile),
                "the camp's night profile is not the one the theme points at");

            Assert.That(theme.DuskProfile.TimeOfDay, Is.EqualTo(ArenaTimeOfDay.Dusk));
            Assert.That(theme.NightProfile.TimeOfDay, Is.EqualTo(ArenaTimeOfDay.Night));
        }

        [Test]
        public void DuskCampIsWarmerAndBrighterThanTheArenasDusk()
        {
            // "อุ่นและสว่างกว่า Dusk ของ Battle": every term that makes a frame warm and bright moves
            // the same way, so a profile that only looked warmer because of one tint would fail here.
            ArenaLightingProfile camp = Theme.DuskProfile;
            ArenaLightingProfile arena = AssetDatabase.LoadAssetAtPath<ArenaTheme>(ArenaThemePath).DuskProfile;

            Assert.That(ArenaContrast.RelativeLuminance(Rgb(camp.EnvironmentTint)),
                Is.GreaterThan(ArenaContrast.RelativeLuminance(Rgb(arena.EnvironmentTint))),
                "the camp's dusk tint is not brighter than the arena's");
            Assert.That(camp.EnvironmentTint.r, Is.GreaterThan(camp.EnvironmentTint.b),
                "the camp's dusk tint is not warm: red does not exceed blue");
            Assert.That(RelativeWarmth(camp.EnvironmentTint), Is.GreaterThan(RelativeWarmth(arena.EnvironmentTint)),
                "the camp's dusk is no warmer than the arena's");
            Assert.That(camp.KeyLightIntensity, Is.GreaterThan(arena.KeyLightIntensity),
                "the camp's key light is not stronger than the arena's");
            Assert.That(camp.PostExposure, Is.GreaterThan(arena.PostExposure),
                "the camp's exposure is not higher than the arena's");
            Assert.That(camp.LightPoolIntensity, Is.GreaterThan(arena.LightPoolIntensity),
                "the camp has no more light on the ground than the battlefield");
            Assert.That(camp.VignetteIntensity, Is.LessThan(arena.VignetteIntensity),
                "the camp's vignette is not softer than the arena's");
        }

        [Test]
        public void NightIsDarkerThanDuskCampInEveryTermThatMakesItDark()
        {
            ArenaLightingProfile dusk = Theme.DuskProfile;
            ArenaLightingProfile night = Theme.NightProfile;

            Assert.That(ArenaContrast.RelativeLuminance(Rgb(night.EnvironmentTint)),
                Is.LessThan(ArenaContrast.RelativeLuminance(Rgb(dusk.EnvironmentTint))), "the night tint is not darker");
            Assert.That(ArenaContrast.RelativeLuminance(Rgb(night.AmbientSky)),
                Is.LessThan(ArenaContrast.RelativeLuminance(Rgb(dusk.AmbientSky))), "the night ambient is not darker");
            Assert.That(night.KeyLightIntensity, Is.LessThan(dusk.KeyLightIntensity), "the night key light is not weaker");
            Assert.That(night.PostExposure, Is.LessThan(dusk.PostExposure), "the night exposure is not lower");
            Assert.That(night.VignetteIntensity, Is.GreaterThanOrEqualTo(dusk.VignetteIntensity));

            // The night still keeps the warm pools: the sala has to be walkable at night.
            Assert.That(night.LightPoolIntensity, Is.GreaterThan(0f), "the night has no light pools");
        }

        [Test]
        public void OneCallSwitchesTheCampsLook()
        {
            var gameObject = new GameObject("camp-profile-switch");
            try
            {
                var view = gameObject.AddComponent<ArenaEnvironmentView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("_theme").objectReferenceValue = Theme;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(view.IsNight, Is.False, "the camp does not start at dusk");
                view.SetTimeOfDay(ArenaTimeOfDay.Night);
                Assert.That(view.IsNight, Is.True, "the night switch did not take effect");
                view.SetTimeOfDay(ArenaTimeOfDay.Dusk);
                Assert.That(view.IsNight, Is.False, "the switch back to dusk did not take effect");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void TheCampReusesTheArenaEnvironmentView()
        {
            // "ใช้ ArenaCard / GroundDecal / ShadowCard / LightingProfile ของเดิม ห้ามเขียนระบบซ้ำ":
            // the theme is the arena's type, and the camp scene's environment is the arena's view.
            Assert.That(typeof(ArenaTheme).IsAssignableFrom(typeof(CampTheme)), Is.True,
                "the camp theme is not an ArenaTheme");

            WithCamp((root, world) =>
            {
                Assert.That(world.Environment, Is.Not.Null, "the camp scene has no environment view");
                Assert.That(world.Environment.GetType(), Is.EqualTo(typeof(ArenaEnvironmentView)),
                    "the camp grew a second environment view type");
                Assert.That(world.Environment.Theme, Is.SameAs(Theme), "the scene's environment is not on the camp theme");
            });
        }

        // -------------------------------------------------------------------
        // helpers
        // -------------------------------------------------------------------

        private static bool IsInTheFrame(float x, float z)
        {
            if (CampYard.IsFrame(x, z))
            {
                return true;
            }

            for (int i = 0; i < CampYard.Fixtures.Length; i++)
            {
                CampFixture fixture = CampYard.Fixtures[i];
                if (Mathf.Abs(fixture.X - x) <= 0.05f && Mathf.Abs(fixture.Z - z) <= 0.05f)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform Find(Transform parent, string name)
        {
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == name)
                {
                    return children[i];
                }
            }

            return null;
        }

        /// <summary>Warmth as red over blue: the one number a warm tint moves and a cool one does not.</summary>
        private static float RelativeWarmth(Color colour) => (colour.r + 0.001f) / (colour.b + 0.001f);

        private static Color32 Rgb(Color colour) => new Color32(
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.r) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.g) * 255f),
            (byte)Mathf.RoundToInt(Mathf.Clamp01(colour.b) * 255f),
            255);

        /// <summary>
        /// Opens the shipped Camp scene (or reuses it when it is already open) and hands its
        /// <c>CampEnvironment</c> root and the scene's <see cref="CampWorldView"/> to the test.
        /// </summary>
        private static void WithCamp(Action<GameObject, CampWorldView> action)
        {
            Scene scene = SceneManager.GetSceneByPath(CampScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(CampScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject root = null;
                CampWorldView world = null;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].name == ProjectSurvive.Game.Editor.CampEnvironmentBuilder.RootName)
                    {
                        root = roots[i];
                    }

                    world = world != null ? world : roots[i].GetComponentInChildren<CampWorldView>(true);
                }

                Assert.That(root, Is.Not.Null,
                    "the Camp scene has no " + ProjectSurvive.Game.Editor.CampEnvironmentBuilder.RootName + " root");
                Assert.That(world, Is.Not.Null, "the Camp scene has no CampWorldView");

                action(root, world);
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
