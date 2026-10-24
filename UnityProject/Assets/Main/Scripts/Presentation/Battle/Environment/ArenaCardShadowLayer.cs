using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// Draws every card shadow in the arena with one instanced call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 08f needs ~190 shadow quads (one streak per standing card, plus a crown patch under
    /// each border tree). As scene renderers they cost one batch each on this project — static
    /// batching is off in Player Settings, and turning it on would apply to the whole game — which
    /// took the 300-ghost frame from 133 to 205 batches, over the stage's 200 budget. The shadows
    /// never move, so the honest fix is to bake their matrices once and draw them instanced: one
    /// call, no per-frame maths, and the same material and queue as before.
    /// </para>
    /// <para>
    /// It keeps the scene readable: the builder still places one GameObject per shadow with an
    /// <see cref="ArenaShadowCard"/> on it (that is what the direction, zone and sorting tests
    /// read), it only disables that object's renderer so the quad is not drawn twice.
    /// </para>
    /// <para>
    /// Ordering is by render queue, not by sorting order — an instanced draw has none. The material
    /// lives in <see cref="ProjectSurvive.Game.Editor.ArenaArtBuilder.CardShadowQueue"/>, below the
    /// light pools and every actor, which is the same guarantee a per-renderer sorting order gave.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaCardShadowLayer : MonoBehaviour
    {
        /// <summary>The most instances one <c>DrawMeshInstanced</c> call accepts.</summary>
        private const int InstancesPerCall = 1023;

        [Tooltip("The centred quad every shadow is drawn with.")]
        [SerializeField] private Mesh _mesh;

        [Tooltip("The multiply shadow material (one material for the whole arena).")]
        [SerializeField] private Material _material;

        private Matrix4x4[] _matrices = new Matrix4x4[0];
        private bool _disposed;
        private bool _overflowed;

        /// <summary>How many shadow quads the layer draws.</summary>
        public int Count { get; private set; }

        /// <summary>The material the layer draws with.</summary>
        public Material Material => _material;

        /// <summary>Wires the mesh and material. Called once by the builder.</summary>
        public void Configure(Mesh mesh, Material material)
        {
            _mesh = mesh;
            _material = material;
        }

        /// <summary>
        /// Collects every <see cref="ArenaShadowCard"/> under the same root into one matrix array and
        /// disables the individual renderers. Idempotent; called from <see cref="Awake"/>.
        /// </summary>
        public void Collect()
        {
            ArenaShadowCard[] shadows = GetComponentsInChildren<ArenaShadowCard>(includeInactive: true);
            if (_matrices.Length < shadows.Length)
            {
                _matrices = new Matrix4x4[shadows.Length];
            }

            Count = 0;
            for (int i = 0; i < shadows.Length; i++)
            {
                var renderer = shadows[i].GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    // Drawn by the instanced call instead; two paths would double the multiply.
                    renderer.enabled = false;
                }

                _matrices[Count++] = shadows[i].transform.localToWorldMatrix;
            }
        }

        private void Awake()
        {
            if (Count == 0)
            {
                Collect();
            }
        }

        private void LateUpdate()
        {
            if (_disposed || Count == 0 || _mesh == null || _material == null)
            {
                return;
            }

            int batch = Mathf.Min(Count, InstancesPerCall);
            Graphics.DrawMeshInstanced(
                _mesh, 0, _material, _matrices, batch,
                null, ShadowCastingMode.Off, receiveShadows: false,
                gameObject.layer, null, LightProbeUsage.Off);

            if (Count > InstancesPerCall && !_overflowed)
            {
                // One call is all this arena needs (~190 shadows); past it, saying so once is more
                // useful than silently drawing the first thousand.
                _overflowed = true;
                Debug.LogWarning("[ArenaCardShadowLayer] " + Count + " card shadows exceed one instanced call of "
                    + InstancesPerCall + ".", this);
            }
        }

        private void OnDisable() => _disposed = true;
    }
}
