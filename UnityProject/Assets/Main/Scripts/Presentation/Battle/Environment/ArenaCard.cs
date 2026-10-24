using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// Marks a prefab instance as one arena card and records which style it is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The quad's pivot is its base: its local origin is the point that stands on the ground, so the
    /// builder can scale a card vertically without moving it. <see cref="FacingCamera"/> is the one
    /// rotation a card ever gets — applied by the builder as the card is placed, never at runtime —
    /// and it tilts the quad square-on to the battle camera's 50° pitch with an optional yaw for
    /// variety. A card faces a fixed direction forever, which is what lets the whole arena be marked
    /// static and batched.
    /// </para>
    /// <para>
    /// It is a marker, not a system: no collider, no update, no behaviour. The sway that makes the
    /// foliage move lives in the shader and the lighting lives in <see cref="ArenaEnvironmentView"/>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ArenaCard : MonoBehaviour
    {
        [Tooltip("The theme style this card was placed from. Report/debug only.")]
        [SerializeField] private string _styleId = string.Empty;

        [Tooltip("Which band of the arena the card belongs to.")]
        [SerializeField] private ArenaCardZone _zone = ArenaCardZone.Ring;

        /// <summary>The style this card was placed from.</summary>
        public string StyleId => _styleId;

        /// <summary>Which band of the arena the card belongs to.</summary>
        public ArenaCardZone Zone => _zone;

        /// <summary>
        /// The rotation that turns a base-pivoted quad square-on to the battle camera.
        /// </summary>
        /// <remarks>
        /// Exactly the rotation an entity sprite gets: the camera's billboard pitch
        /// (<see cref="BattleCameraRig.PitchDegrees"/>, the value <c>BillboardPitch</c> publishes),
        /// then a few degrees of yaw for variety. The angle is never stored or retyped here, so a
        /// card and a ghost can never end up leaning differently.
        /// </remarks>
        /// <param name="yawDegrees">A small turn either way, for variety; 0 faces the camera exactly.</param>
        public static Quaternion FacingCamera(float yawDegrees) =>
            Quaternion.Euler(BattleCameraRig.PitchDegrees, yawDegrees, 0f);

        /// <summary>Records the style and zone. Called once, by the builder, as the card is placed.</summary>
        public void Configure(string styleId, ArenaCardZone zone)
        {
            _styleId = styleId;
            _zone = zone;
        }
    }
}
