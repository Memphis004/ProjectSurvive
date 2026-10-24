using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// One flat shadow decal belonging to a standing card: the fake shadow Stage 08f draws instead
    /// of a URP shadow map.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A survivor-like arena cannot afford real shadows. Every card would need a shadow pass and a
    /// receiver, the cards write no depth at all (they are ordered by
    /// <see cref="BattleWorldMap.PropSortingOrder"/>), and 300 ghosts on a WebGL2 target leave no
    /// room for another render pass. So a card's shadow is a soft quad lying on the floor: its length
    /// is the card's height times the profile's <see cref="ArenaLightingProfile.ShadowLengthPerHeight"/>,
    /// its width is the card's own width (or, for a border tree's canopy patch, the canopy's), and
    /// every one of them points along the profile's single
    /// <see cref="ArenaLightingProfile.ShadowDirectionDegrees"/>. There is exactly one shadow angle
    /// in the whole scene and a test reads it off the placed transforms.
    /// </para>
    /// <para>
    /// <b>Never over an actor.</b> Card shadows share one render queue (below the actors' sprite
    /// queue) and one sorting order, <see cref="BattleWorldMap.ShadowSortingOrder"/>, which is below
    /// every order <see cref="BattleWorldMap.SortingOrder"/> can produce for an actor anywhere in the
    /// arena. A shadow therefore never covers a ghost, a pickup or the player, no matter where they
    /// stand — the rule is a constant, not a per-frame comparison.
    /// </para>
    /// <para>
    /// <b>One big patch, and only at the edge.</b> <see cref="IsCanopy"/> marks the wide ellipse
    /// under a border tree's crown. The open centre and the bush ring get the narrow directional
    /// streak only: a large dark patch in the middle of the arena would sit under the fight and
    /// compete with the ghosts, which is exactly what the readability rule forbids.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaShadowCard : MonoBehaviour
    {
        /// <summary>
        /// The narrowest streak, so grass inside the centre still has a shadow instead of none.
        /// </summary>
        public const float MinimumLength = 0.3f;

        /// <summary>
        /// A shadow wider than this (across the light direction) is a canopy patch, not a streak.
        /// The centre may never hold one; a test enforces it from the placed scene.
        /// </summary>
        public const float LargeShadowWidth = 2.5f;

        [Tooltip("The zone of the card this shadow belongs to.")]
        [SerializeField] private ArenaCardZone _sourceZone = ArenaCardZone.Ring;

        [Tooltip("The card's width, in world units.")]
        [SerializeField] private float _sourceWidth;

        [Tooltip("The card's height, in world units.")]
        [SerializeField] private float _sourceHeight;

        [Tooltip("Where the shadow points, in the profile's convention: 0 = +X, 90 = +Z.")]
        [SerializeField] private float _directionDegrees;

        [Tooltip("True for the wide crown patch under a border tree.")]
        [SerializeField] private bool _canopy;

        /// <summary>The zone of the card this shadow belongs to.</summary>
        public ArenaCardZone SourceZone => _sourceZone;

        /// <summary>The card's width in world units.</summary>
        public float SourceWidth => _sourceWidth;

        /// <summary>The card's height in world units.</summary>
        public float SourceHeight => _sourceHeight;

        /// <summary>The direction the shadow is drawn toward.</summary>
        public float DirectionDegrees => _directionDegrees;

        /// <summary>True for the wide crown patch under a border tree.</summary>
        public bool IsCanopy => _canopy;

        /// <summary>How far the shadow reaches, in world units (its long axis).</summary>
        public float Length => transform.localScale.x;

        /// <summary>How wide the shadow is, in world units (across the light direction).</summary>
        public float Width => transform.localScale.y;

        /// <summary>
        /// The rotation that lays a centred quad on the floor with its local +X along the shadow
        /// direction. The quad's uv runs along that axis, so the texture is stretched by the scale
        /// alone — no per-shadow rotation of the art and no second mesh.
        /// </summary>
        public static Quaternion Orientation(float directionDegrees) =>
            Quaternion.Euler(90f, -directionDegrees, 0f);

        /// <summary>
        /// The direction a placed shadow actually points, read off its transform: the world angle of
        /// its local +X. This is what the "every shadow follows the profile" test compares with the
        /// profile, so a mismatch between the authored field and the drawn quad cannot hide.
        /// </summary>
        public static float DirectionDegreesOf(Transform shadow)
        {
            Vector3 axis = shadow.right;
            return Mathf.Atan2(axis.z, axis.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The centre of a shadow that starts at its card's base and reaches
        /// <paramref name="length"/> along the profile's direction.
        /// </summary>
        public static Vector3 CentreFor(
            float cardX, float cardZ, float length, ArenaLightingProfile profile)
        {
            Vector3 direction = profile.ShadowDirection;
            return new Vector3(
                cardX + (direction.x * length * 0.5f),
                0f,
                cardZ + (direction.z * length * 0.5f));
        }

        /// <summary>The length a card of <paramref name="height"/> throws at this profile.</summary>
        public static float LengthFor(float height, ArenaLightingProfile profile) =>
            Mathf.Max(MinimumLength, height * profile.ShadowLengthPerHeight);

        /// <summary>Records what was placed. Called once, by the builder.</summary>
        public void Configure(
            ArenaCardZone zone, float width, float height, float directionDegrees, bool canopy)
        {
            _sourceZone = zone;
            _sourceWidth = width;
            _sourceHeight = height;
            _directionDegrees = directionDegrees;
            _canopy = canopy;
        }
    }
}
