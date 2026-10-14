using System;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>How the 2.5D battle camera projects.</summary>
    public enum BattleProjection
    {
        /// <summary>Constant scale; occlusion comes from sprite sorting order.</summary>
        Orthographic = 0,

        /// <summary>Foreshortened; distance shrinks a sprite.</summary>
        Perspective = 1,
    }

    /// <summary>
    /// The Battle scene's camera: a 50° tilt over the arena, following the ghost-doctor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The GDD fixes the tilt at roughly 50° and leaves orthographic vs perspective to be
    /// decided by experiment, so <see cref="BattleProjection"/> is an authored choice with
    /// both variants implemented — the stage measures both and
    /// <c>docs/adr/0018-battle-camera-projection.md</c> records the verdict and the evidence.
    /// </para>
    /// <para>
    /// Both variants share one pose: <c>rotation = Euler(pitch, 0, 0)</c> and
    /// <c>position = target + (0, sin(pitch), −cos(pitch)) × distance</c>. The distance is
    /// what differs, because it is what the projection makes meaningful: orthographic needs
    /// only to clear the clip planes, while perspective needs it to frame the same height at
    /// the target plane (the two are matched at the target so the comparison is a comparison
    /// and not two different zooms).
    /// </para>
    /// <para>
    /// The camera follows the player rather than the arena, and it snaps on the first frame
    /// instead of sliding in from wherever the scene left it.
    /// </para>
    /// </remarks>
    public sealed class BattleCameraRig
    {
        /// <summary>The tilt the GDD asks for: "กล้องเฉียงประมาณ 50°".</summary>
        public const float PitchDegrees = 50f;

        /// <summary>Half-height of the orthographic view, in world units.</summary>
        public const float OrthographicSize = 14f;

        /// <summary>Perspective field of view, matching the orthographic framing at the target.</summary>
        public const float FieldOfView = 35f;

        /// <summary>
        /// Distance used to frame <c>2 × OrthographicSize</c> at the target plane under
        /// <see cref="FieldOfView"/>: <c>height / (2 · tan(fov/2))</c>. Computed from the two
        /// constants above so the two projections cannot drift apart.
        /// </summary>
        public static readonly float PerspectiveDistance =
            (2f * OrthographicSize) / (2f * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad));

        /// <summary>Clip distance for the orthographic variant (it only needs to enclose the arena).</summary>
        private const float OrthographicDistance = 60f;

        private readonly Camera _camera;
        private BattleProjection _projection;
        private Vector3 _target;
        private bool _placed;

        /// <summary>Creates a rig over <paramref name="camera"/>.</summary>
        public BattleCameraRig(Camera camera, BattleProjection projection)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _target = Vector3.zero;
            SetProjection(projection);
        }

        /// <summary>The active projection.</summary>
        public BattleProjection Projection => _projection;

        /// <summary>
        /// The pitch a billboard sprite must be tilted at to face this camera square on.
        /// </summary>
        public float BillboardPitch => PitchDegrees;

        /// <summary>Switches projection, keeping the pose.</summary>
        public void SetProjection(BattleProjection projection)
        {
            _projection = projection;

            if (_projection == BattleProjection.Orthographic)
            {
                _camera.orthographic = true;
                _camera.orthographicSize = OrthographicSize;
                _camera.nearClipPlane = 0.3f;
                _camera.farClipPlane = OrthographicDistance * 2f;
            }
            else
            {
                _camera.orthographic = false;
                _camera.fieldOfView = FieldOfView;
                _camera.nearClipPlane = 0.3f;
                _camera.farClipPlane = PerspectiveDistance * 4f;
            }

            Place(_target);
            _placed = true;
        }

        /// <summary>Jumps straight to <paramref name="target"/> (no easing).</summary>
        public void SnapTo(Vector3 target)
        {
            _target = target;
            Place(target);
            _placed = true;
        }

        /// <summary>Follows <paramref name="target"/>. Snaps when nothing was placed yet.</summary>
        public void Follow(Vector3 target)
        {
            if (!_placed)
            {
                SnapTo(target);
                return;
            }

            _target = target;
            Place(target);
        }

        /// <summary>The point the camera is looking at.</summary>
        public Vector3 Target => _target;

        private void Place(Vector3 target)
        {
            float distance = _projection == BattleProjection.Orthographic ? OrthographicDistance : PerspectiveDistance;
            float pitch = PitchDegrees * Mathf.Deg2Rad;

            _camera.transform.SetPositionAndRotation(
                new Vector3(
                    target.x,
                    target.y + (Mathf.Sin(pitch) * distance),
                    target.z - (Mathf.Cos(pitch) * distance)),
                Quaternion.Euler(PitchDegrees, 0f, 0f));
        }
    }
}
