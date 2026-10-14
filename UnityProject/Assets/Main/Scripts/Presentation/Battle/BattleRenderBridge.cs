using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Binds published entities to pooled views, and interpolates them between the two
    /// ticks a frame publishes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The battle ticks at 30 Hz while the browser renders at 60 or more, so drawing the
    /// current tick straight would show every ghost taking thirty jerky steps a second.
    /// The bridge keeps, per entity slot, the position it came <i>from</i> (the frame's
    /// <c>PreviousEntities</c>) and the position it is heading to (<c>Entities</c>), and
    /// places the view between them with the runner's alpha. That is the whole reason the
    /// contract publishes a previous frame at all ([ADR 0012]).
    /// </para>
    /// <para>
    /// Slots are indexed by <see cref="EntityRef.Index"/> and validated by
    /// <see cref="EntityRef.Version"/>, so a recycled slot starts a new entity cleanly
    /// instead of sliding the new ghost in from where the old one died — the EcsLite
    /// liveness lesson, applied on the view side.
    /// </para>
    /// <para>
    /// Work is split in two on purpose: <see cref="ApplySnapshot"/> runs once per
    /// <i>tick</i> (acquire/release views, take the interpolation endpoints) and
    /// <see cref="Render"/> runs once per <i>rendered frame</i> (place the views). A frame
    /// that ran no tick therefore re-renders the same endpoints at a new alpha instead of
    /// doing the binding work again.
    /// </para>
    /// </remarks>
    public sealed class BattleRenderBridge : IDisposable
    {
        private struct Slot
        {
            internal EntityView View;
            internal int Version;
            internal RenderEntityKind Kind;
            internal RenderEntityFlags Flags;
            internal Vector3 From;
            internal Vector3 To;
            internal float ScaleFrom;
            internal float ScaleTo;
            internal float LeanFrom;
            internal float LeanTo;
            internal float Health;
            internal bool Bound;
        }

        private readonly EntityViewPool _pool;
        private readonly BattlePlaceholderArt _art;
        private readonly Slot[] _slots = new Slot[BattleConstants.MaxRenderEntities + 1];
        private readonly byte[] _seen = new byte[BattleConstants.MaxRenderEntities + 1];

        private float _billboardPitch;
        private int _boundCount;
        private bool _disposed;

        /// <summary>Creates a bridge over <paramref name="pool"/>.</summary>
        public BattleRenderBridge(EntityViewPool pool, BattlePlaceholderArt art)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _art = art ?? throw new ArgumentNullException(nameof(art));
        }

        /// <summary>Views currently bound to an entity.</summary>
        public int BoundCount => _boundCount;

        /// <summary>
        /// The pitch a billboard is tilted at so it faces the camera square on. Set by the
        /// camera rig: with the Stage 08 camera this is 50°, and a sprite left upright at
        /// that pitch would be seen at a 40° grazing angle and lose a third of its height.
        /// </summary>
        public float BillboardPitch
        {
            get => _billboardPitch;
            set => _billboardPitch = value;
        }

        /// <summary>
        /// Rebinds views for one tick's snapshot. Call once per tick, before
        /// <see cref="Render"/>.
        /// </summary>
        public void ApplySnapshot(BattleFrameSnapshot snapshot)
        {
            if (snapshot is null)
            {
                return;
            }

            Array.Clear(_seen, 0, _seen.Length);

            ReadOnlySpan<RenderEntity> current = snapshot.Current;
            for (int i = 0; i < current.Length; i++)
            {
                RenderEntity entity = current[i];

                int index = entity.Entity.Index;
                if (index <= 0 || index > BattleConstants.MaxRenderEntities)
                {
                    continue;
                }

                ref Slot slot = ref _slots[index];

                bool reusable = slot.Bound
                    && slot.Version == entity.Entity.Version
                    && slot.Kind == entity.Kind
                    && slot.View != null;

                if (!reusable)
                {
                    Release(ref slot);
                    slot.View = _pool.Acquire(entity.Kind);
                    slot.View.Entity = entity.Entity;
                    slot.View.Kind = entity.Kind;
                    slot.Bound = true;
                    _boundCount++;
                }

                Vector3 target = BattleWorldMap.ToWorld(entity);

                RenderEntity previous = default;
                bool hadPrevious = reusable && snapshot.TryGetPrevious(entity.Entity, out previous);

                slot.Version = entity.Entity.Version;
                slot.Kind = entity.Kind;
                slot.Flags = entity.Flags;
                slot.From = hadPrevious ? BattleWorldMap.ToWorld(previous) : target;
                slot.ScaleFrom = hadPrevious ? previous.Scale : entity.Scale;
                slot.LeanFrom = hadPrevious
                    ? BattleWorldMap.HeadingLean(previous.RotationY)
                    : BattleWorldMap.HeadingLean(entity.RotationY);
                slot.To = target;
                slot.ScaleTo = entity.Scale;
                slot.LeanTo = BattleWorldMap.HeadingLean(entity.RotationY);
                slot.Health = entity.HealthPercent;

                slot.View.ApplyLook(
                    _art.SpriteFor(entity.Kind),
                    BattlePlaceholderArt.TintFor(entity.Kind, entity.Flags, entity.HealthPercent),
                    (entity.Flags & RenderEntityFlags.FacingLeft) != 0);

                _seen[index] = 1;

                // A freshly acquired view must not be rendered where its previous owner
                // left it for the frame between binding and the first Render call.
                if (!reusable)
                {
                    slot.View.Place(
                        target,
                        Orientation(slot.LeanTo),
                        slot.ScaleTo * BattlePlaceholderArt.VisualScale(entity.Flags));
                }
            }

            // Anything that did not appear this tick died or left the arena; hand its view
            // back. Doing this after the loop keeps the release out of the enumeration.
            for (int index = 1; index <= BattleConstants.MaxRenderEntities; index++)
            {
                if (_seen[index] == 0 && _slots[index].Bound)
                {
                    Release(ref _slots[index]);
                }
            }
        }

        /// <summary>
        /// Places every bound view between its two ticks. <paramref name="alpha"/> is the
        /// runner's accumulator fraction, in [0, 1).
        /// </summary>
        public void Render(float alpha)
        {
            float pitch = _billboardPitch;

            for (int index = 1; index <= BattleConstants.MaxRenderEntities; index++)
            {
                if (_seen[index] == 0)
                {
                    continue;
                }

                ref Slot slot = ref _slots[index];
                if (!slot.Bound || slot.View is null)
                {
                    continue;
                }

                Vector3 position = BattleWorldMap.Interpolate(slot.From, slot.To, alpha);
                float scale = slot.ScaleFrom + ((slot.ScaleTo - slot.ScaleFrom) * alpha);
                float lean = slot.LeanFrom + ((slot.LeanTo - slot.LeanFrom) * alpha);

                slot.View.Place(
                    position,
                    Orientation(lean) * Quaternion.Euler(0f, 0f, -lean),
                    scale * BattlePlaceholderArt.VisualScale(slot.Flags));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int index = 1; index <= BattleConstants.MaxRenderEntities; index++)
            {
                Release(ref _slots[index]);
            }

            _boundCount = 0;
        }

        private Quaternion Orientation(float lean)
        {
            // Face the camera square on (a rotation about X by the camera's pitch), then
            // roll in the sprite's own plane so a walking ghost reads as walking. The roll
            // is multiplied on the right, so it happens in the view's local space. Cosmetic
            // only — never read back by a rule.
            return Quaternion.Euler(_billboardPitch, 0f, 0f) * Quaternion.Euler(0f, 0f, lean);
        }

        private void Release(ref Slot slot)
        {
            if (slot.View is null)
            {
                slot.Bound = false;
                return;
            }

            _pool.Release(slot.View);
            slot.View = null;
            slot.Bound = false;
            slot.Version = 0;
            _boundCount--;
        }
    }
}
