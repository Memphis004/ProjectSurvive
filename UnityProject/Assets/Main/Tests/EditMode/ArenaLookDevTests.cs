using NUnit.Framework;
using ProjectSurvive.Game.Editor;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Environment;
using UnityEditor;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 08g's framing invariants: how tall the player stands in the frame, how tall the wall
    /// stands beside them, and where the foreground belt's trunks land.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All four are arithmetic on authored numbers, which is the point: the stage's pictures are in
    /// <c>artifacts/ui/battle-lookdev/</c> and the numbers behind them are what a future change would
    /// silently break. <c>ArenaSharpnessTests</c> is the same idea for the texture budget.
    /// </para>
    /// <para>
    /// The zone rules themselves (nothing tall in the centre, the wall outside the ring, the belt
    /// outside the centre) are enforced by <see cref="ArenaLayout.Validate"/> and asserted by
    /// <c>ArenaEnvironmentTests</c>; this file only adds what the stage introduced.
    /// </para>
    /// </remarks>
    public sealed class ArenaLookDevTests
    {
        private static ArenaTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ArenaTheme>(ArenaArtBuilder.ThemePath);
            Assert.IsNotNull(theme, "the arena theme is missing: " + ArenaArtBuilder.ThemePath);
            return theme;
        }

        /// <summary>
        /// The stage's camera profile: the player stands 8–10 % of the screen's height. The camera
        /// derives <c>OrthographicSize</c> from the pair of constants below, so this asserts the
        /// derivation as well as the range — a frame typed as a size would drift from the number the
        /// report quotes.
        /// </summary>
        [Test]
        public void ThePlayerStandsEightToTenPercentOfTheScreenHeight()
        {
            float fraction = BattleCameraRig.PlayerHeight / (2f * BattleCameraRig.OrthographicSize);

            Assert.That(fraction, Is.InRange(0.08f, 0.10f),
                "the player stands " + (fraction * 100f).ToString("0.0") + " % of the screen height");
            Assert.That(BattleCameraRig.PlayerScreenHeightFraction, Is.EqualTo(fraction).Within(0.0001f),
                "the camera's published fraction and the frame it produces disagree");
        }

        /// <summary>
        /// How high the frame's <b>near</b> edge stands on the ground: with the camera pitched 50°
        /// and an orthographic half-height of H, the bottom row of the frame is H / sin(50°) in front
        /// of the player. 11.6 units at the stage's camera.
        /// </summary>
        private static float NearEdgeRow() =>
            BattleCameraRig.OrthographicSize / Mathf.Sin(BattleCameraRig.PitchDegrees * Mathf.Deg2Rad);

        /// <summary>
        /// The wall — border trees and the foreground trunks alike — stands 3–5 players high in the
        /// theme's own numbers, at every scale the layout may pick for it.
        /// </summary>
        [Test]
        public void EveryArenaTreeStandsThreeToFivePlayersHigh()
        {
            ArenaTheme theme = LoadTheme();
            float shortest = theme.PlayerHeight * theme.WallMinHeightFactor;
            float tallest = theme.PlayerHeight * theme.WallMaxHeightFactor;

            int checkedStyles = 0;
            foreach (ArenaCardStyle style in theme.Styles)
            {
                if (style.Zone != ArenaCardZone.Border && style.Zone != ArenaCardZone.Foreground)
                {
                    continue;
                }

                checkedStyles++;
                float low = style.Height * style.MinScale;
                float high = style.Height * style.MaxScale;

                Assert.That(low, Is.GreaterThanOrEqualTo(shortest - 0.001f),
                    style.Id + " stands " + low.ToString("0.00") + " units at its smallest scale, under the "
                    + shortest.ToString("0.00") + " a " + theme.WallMinHeightFactor.ToString("0") + "-player wall needs");
                Assert.That(high, Is.LessThanOrEqualTo(tallest + 0.001f),
                    style.Id + " stands " + high.ToString("0.00") + " units at its largest scale, over the "
                    + tallest.ToString("0.00") + " a " + theme.WallMaxHeightFactor.ToString("0") + "-player wall allows");
            }

            Assert.GreaterOrEqual(checkedStyles, 6, "the theme has fewer wall styles than the stage builds");
        }

        /// <summary>
        /// The foreground belt is where the bottom edge of the frame is: its inner radius clears the
        /// centre's own ceiling (<see cref="ArenaTheme.CenterMaxHeight"/> is enforced out to
        /// <see cref="ArenaTheme.CenterRadius"/>, so nothing tall may stand nearer) and it is close
        /// enough to the frame's near edge to be read as foreground rather than as scenery.
        /// </summary>
        [Test]
        public void TheForegroundBeltStandsAtTheBottomEdgeOfTheFrame()
        {
            ArenaTheme theme = LoadTheme();
            float nearRow = NearEdgeRow();

            Assert.That(theme.ForegroundInnerRadius, Is.GreaterThanOrEqualTo(theme.CenterRadius),
                "the belt starts inside the open centre at " + theme.ForegroundInnerRadius.ToString("0.00")
                + " units, where the centre's own ceiling would already have refused a tree");
            Assert.That(theme.ForegroundOuterRadius, Is.GreaterThan(theme.ForegroundInnerRadius),
                "the belt has no depth");

            // A trunk at the belt's outer edge is at most this far up the frame when the player stands
            // at the arena's centre: the camera's near edge is `nearRow` in front of them, and every
            // screen unit is `1 / OrthographicSize` of the half-frame.
            float lowestOnScreen = nearRow - theme.ForegroundOuterRadius;
            float fraction = lowestOnScreen / (2f * BattleCameraRig.OrthographicSize);
            Assert.That(fraction, Is.LessThan(0.25f),
                "the belt's nearest trunks stand " + (fraction * 100f).ToString("0")
                + " % of the screen up from the bottom edge, which is scenery rather than foreground");
        }

        /// <summary>
        /// The layout really places the belts: at the stage's seed there are trunks, every one of them
        /// inside the belt, and the layout as a whole is legal.
        /// </summary>
        [Test]
        public void TheLayoutPlacesItsForegroundTrunksInsideTheBelt()
        {
            ArenaTheme theme = LoadTheme();
            ArenaPlacement[] placements = ArenaLayout.Build(theme, 20261024u);

            int trunks = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].Zone != ArenaCardZone.Foreground)
                {
                    continue;
                }

                trunks++;
                Assert.That(placements[i].Radius, Is.GreaterThanOrEqualTo(theme.ForegroundInnerRadius - 0.001f),
                    placements[i].StyleId + " stands at radius " + placements[i].Radius.ToString("0.00")
                    + ", inside the belt's inner edge");
                Assert.That(placements[i].Radius, Is.LessThanOrEqualTo(theme.ForegroundOuterRadius + 0.001f),
                    placements[i].StyleId + " stands at radius " + placements[i].Radius.ToString("0.00")
                    + ", outside the belt's outer edge");
            }

            Assert.That(trunks, Is.GreaterThanOrEqualTo(1),
                "the layout placed no foreground trunks, so the belt is authored but never built");

            var problems = ArenaLayout.Validate(theme, placements);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>
        /// The point of the belt: a trunk that stands between the camera and the player is dithered out
        /// of the way (<c>ArenaCardFade</c>), and one that stands behind them is not. The rule is
        /// geometry, so it is a unit test rather than a screenshot.
        /// </summary>
        [Test]
        public void AForegroundTrunkDithersWhenThePlayerWalksBehindIt()
        {
            ArenaTheme theme = LoadTheme();
            const float width = 2.3f * 1.10f;
            float height = theme.PlayerHeight * theme.WallMaxHeightFactor;
            float reach = height * ArenaCardFade.ReachPerHeight;

            // A trunk on the belt, facing the far side of the arena, with the player walking up
            // behind it: inside the card's ground reach and its width, so it hides them.
            Assert.IsTrue(
                ArenaCardFade.Covers(0f, -theme.ForegroundInnerRadius, width, height, 0f, -theme.ForegroundInnerRadius + (reach * 0.5f)),
                "a trunk the player stands behind does not dither");

            // One frame's walk further on and the trunk is behind the player, where it can hide nobody.
            Assert.IsFalse(
                ArenaCardFade.Covers(0f, -theme.ForegroundInnerRadius, width, height, 0f, -theme.ForegroundInnerRadius + reach + 1f),
                "a trunk the player has walked past still dithers");
        }
    }
}
