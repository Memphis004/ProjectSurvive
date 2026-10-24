using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// A soft dark ellipse under every player, ghost and pickup, drawn as <b>one instanced call</b>
    /// rather than as one sprite per character.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stage asks for blob shadows so a character is not floating over a dark floor. The obvious
    /// implementation — a child sprite on every pooled view — would add up to a draw call per entity,
    /// which is exactly what this scene cannot afford (the entity sprites already cost one each; see
    /// the stage report). Instancing hundreds of identical quads costs one call per 1023 shadows.
    /// </para>
    /// <para>
    /// It reads only the frame's published entities: a shadow is at the entity's own world XZ, sized
    /// by its kind, and elite/boss shadows grow by the same scale their bodies do. It never reads a
    /// rule and never changes what is drawn.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaBlobShadows : MonoBehaviour
    {
        /// <summary>The most instances one <c>DrawMeshInstanced</c> call accepts.</summary>
        private const int InstancesPerCall = 1023;

        /// <summary>Height above the floor. Above the decals (2 cm), below any actor.</summary>
        private const float Height = 0.035f;

        [Tooltip("The flat quad drawn for one shadow. Centred at its origin.")]
        [SerializeField] private Mesh _mesh;

        [Tooltip("Transparent black material with a soft radial texture.")]
        [SerializeField] private Material _material;

        [Tooltip("Sorting/render queue of the shadow material. Below decals and actors.")]
        [SerializeField] private int _renderQueue = 2990;

        private readonly Matrix4x4[] _matrices = new Matrix4x4[InstancesPerCall];
        private bool _configured;

        /// <summary>The material the layer draws with (null before the scene is wired).</summary>
        public Material Material => _material;

        /// <summary>How many shadows the last <see cref="Draw"/> submitted.</summary>
        public int Drawn { get; private set; }

        /// <summary>Wires the mesh and material. Idempotent; called by the builder's scene and by tests.</summary>
        public void Configure(Mesh mesh, Material material, int renderQueue = 2990)
        {
            _mesh = mesh;
            _material = material;
            _renderQueue = renderQueue;
            _configured = false;
            ApplyQueue();
        }

        private void Awake() => ApplyQueue();

        /// <summary>
        /// Draws one shadow per published actor. Called once per rendered frame with the frame the
        /// view has already copied.
        /// </summary>
        public void Draw(ReadOnlySpan<RenderEntity> entities)
        {
            if (_mesh is null || _material is null)
            {
                return;
            }

            Drawn = 0;
            ApplyQueue();

            int count = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                RenderEntity entity = entities[i];
                float size;
                switch (entity.Kind)
                {
                    case RenderEntityKind.Player:
                        size = 1.05f;
                        break;
                    case RenderEntityKind.Enemy:
                        size = 0.85f * BattlePlaceholderArt.VisualScale(entity.Flags);
                        break;
                    case RenderEntityKind.Pickup:
                        size = 0.45f;
                        break;
                    default:
                        continue;
                }

                _matrices[count++] = Matrix4x4.TRS(
                    new Vector3(entity.X, Height, entity.Y),
                    Quaternion.Euler(90f, 0f, 0f),
                    new Vector3(size, size, 1f));

                if (count == InstancesPerCall)
                {
                    Flush(count);
                    count = 0;
                }
            }

            if (count > 0)
            {
                Flush(count);
            }
        }

        private void Flush(int count)
        {
            Graphics.DrawMeshInstanced(
                _mesh, 0, _material, _matrices, count,
                null, ShadowCastingMode.Off, receiveShadows: false,
                gameObject.layer, null, LightProbeUsage.Off);

            Drawn += count;
        }

        private void ApplyQueue()
        {
            if (_configured || _material is null)
            {
                return;
            }

            _material.renderQueue = _renderQueue;

            // DrawMeshInstanced throws on a material without this flag, once per frame. The builder
            // sets it on the asset; this keeps a hand-made or older material working too.
            _material.enableInstancing = true;
            _configured = true;
        }
    }
}
