using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The one place the simulation's 2D plane becomes Unity's XZ ground plane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The battle contract is 2D: <see cref="RenderEntity.X"/> and
    /// <see cref="RenderEntity.Y"/> are plane coordinates. The scene is 2.5D, so
    /// every view maps <c>(x, y)</c> to <c>(x, 0, y)</c> — the simulation's +Y runs
    /// <i>away from</i> the camera along world +Z, which is what makes "further up the
    /// screen" mean "further into the arena" without a second coordinate system
    /// anywhere.
    /// </para>
    /// <para>
    /// Depth is <b>not</b> left to the depth buffer or to painter's-order-by-distance:
    /// a survivor-like stack of identical silhouettes has to be legible, so
    /// <see cref="SortingOrder"/> turns the simulation Y into an explicit sprite
    /// sorting order (further away sorts behind) and every kind gets a small bias so
    /// a projectile still draws over the ghost it just hit. This is the mechanism the
    /// Stage 08 camera experiment relies on: the occlusion of two characters never
    /// depends on which projection is active.
    /// </para>
    /// </remarks>
    public static class BattleWorldMap
    {
        /// <summary>Height sprites stand on; the arena floor is at this Y.</summary>
        public const float GroundHeight = 0f;

        /// <summary>Sorting order of the floor, below every actor.</summary>
        public const int GroundSortingOrder = -10000;

        /// <summary>
        /// Sorting order of the flat fake shadows (Stage 08f).
        /// </summary>
        /// <remarks>
        /// A shadow lies on the floor under an actor and may never draw over one, wherever it stands.
        /// Every actor order is <c>KindBias − round(simY × 16)</c>; the smallest the arena can produce
        /// (a pickup at the far edge, simY = 40) is −640, so one constant far below that is enough for
        /// the whole scene. It is a constant rather than a per-frame comparison because the rule can
        /// never change — and because the arena's shadows are static geometry.
        /// </remarks>
        public const int ShadowSortingOrder = -9000;

        /// <summary>
        /// Sorting orders per world unit of simulation Y. Large enough that two rows a
        /// tenth of a unit apart still order correctly, small enough that the ±40 unit
        /// arena stays far inside <see cref="int"/> range.
        /// </summary>
        private const float SortingUnitsPerRow = 16f;

        /// <summary>Where a simulation position is in the scene.</summary>
        public static Vector3 ToWorld(float simX, float simY) => new Vector3(simX, GroundHeight, simY);

        /// <summary>Where a published entity is in the scene.</summary>
        public static Vector3 ToWorld(in RenderEntity entity) => ToWorld(entity.X, entity.Y);

        /// <summary>
        /// Render-time position between two ticks. Unclamped on purpose: the alpha
        /// comes from the runner's accumulator and an overshoot of a fraction of a tick
        /// is smoother than a snap, and it can never be used as a rule input.
        /// </summary>
        public static Vector3 Interpolate(in Vector3 from, in Vector3 to, float alpha) =>
            new Vector3(
                from.x + ((to.x - from.x) * alpha),
                from.y + ((to.y - from.y) * alpha),
                from.z + ((to.z - from.z) * alpha));

        /// <summary>
        /// The sprite sorting order for one entity: further away sorts behind, ties
        /// broken by kind so a shot reads over the ghost it is hitting.
        /// </summary>
        public static int SortingOrder(RenderEntityKind kind, float simY) =>
            KindBias(kind) - Mathf.RoundToInt(simY * SortingUnitsPerRow);

        /// <summary>
        /// The order a piece of arena scenery standing at <paramref name="simY"/> draws at.
        /// </summary>
        /// <remarks>
        /// Stage 08e's cards are scenery, not actors, but they have to interleave with the actors
        /// correctly: a tree the player walks behind must draw before the player, a tree the player
        /// walks in front of must draw after. The cards therefore use the <i>same</i> row scale as
        /// <see cref="SortingOrder(RenderEntityKind, float)"/> — one step behind the actors of their
        /// own row — rather than the depth buffer, so there is exactly one rule for who hides whom
        /// and it is this one.
        /// </remarks>
        public static int PropSortingOrder(float simY) =>
            SortingOrder(RenderEntityKind.Pickup, simY) - 1;

        /// <summary>
        /// Cosmetics-only lean of the billboard, in degrees, for an entity heading in the
        /// simulation plane. <see cref="RenderEntity.RotationY"/> carries a <i>heading</i>
        /// (the frame publishes <c>atan2(vy, vx)</c>), and a quad square to the camera
        /// cannot show a heading by turning — at ±90° it would be edge-on and invisible.
        /// Leaning the billboard in its own plane is the honest way to show it: it uses
        /// the published field, never hides a sprite, and is never read back by a rule.
        /// </summary>
        public static float HeadingLean(float headingDegrees) =>
            Mathf.Sin(headingDegrees * Mathf.Deg2Rad) * BattleViewCaps.HeadingLeanDegrees;

        /// <summary>
        /// Tie-break inside one row, in draw order: a drop under a ghost, the ghost under the
        /// bolt that is hitting it, and the player on top of everything standing where they are.
        /// </summary>
        /// <remarks>
        /// The order matters because the bias is what decides an <i>exact</i> tie, and an exact
        /// tie is the common case: a bolt and the ghost it just hit are a few hundredths of a
        /// unit apart, which is one sorting unit at most. A bolt drawn under its target would
        /// hide every hit behind the thing that took it.
        /// </remarks>
        private static int KindBias(RenderEntityKind kind)
        {
            switch (kind)
            {
                case RenderEntityKind.Pickup:
                    return 0;
                case RenderEntityKind.Enemy:
                    return 1;
                case RenderEntityKind.Projectile:
                    return 2;
                case RenderEntityKind.Player:
                    return 3;
                default:
                    return 0;
            }
        }
    }
}
