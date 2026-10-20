using System;
using System.Globalization;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Vfx
{
    /// <summary>
    /// Draws the floor warning a telegraphed attack publishes (Stage 11b): one pooled decal
    /// per <see cref="BattleEventType.TelegraphStarted"/>, removed by the matching
    /// <see cref="BattleEventType.TelegraphEnded"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It draws what the rule used.</b> The event carries the shape, the origin, the radius
    /// or half-width, the line's length and its angle, so this pool never re-derives a
    /// footprint from a def it cannot see — a warning that draws smaller than the damage would
    /// be worse than no warning at all. <see cref="BattleEvent.Value"/> is the
    /// <see cref="TelegraphShape"/>; <see cref="TelegraphShape.Circle"/> is a disc under the
    /// attack's centre and <see cref="TelegraphShape.Line"/> a bar along its aim.
    /// </para>
    /// <para>
    /// <b>Pooled like everything else on a frame.</b> One decal per possible warning is built
    /// at scene start and shown by toggling <see cref="SpriteRenderer.enabled"/>, so a wave of
    /// charging ghosts never instantiates a GameObject (AGENTS rule 8). The strength pulse is
    /// presentation-only and reads no simulation state.
    /// </para>
    /// <para>
    /// A warning also expires after <see cref="BattleViewCaps.TelegraphMaxLifetimeSeconds"/>.
    /// That is a safety net, not a rule: the event ring may drop an event when the view falls
    /// behind, and a dropped <c>TelegraphEnded</c> must never leave a hazard painted on the
    /// floor for the rest of the floor.
    /// </para>
    /// </remarks>
    public sealed class TelegraphPool : IBattleEventListener, IDisposable
    {
        private static readonly Color CircleTint = new Color(1f, 0.36f, 0.28f, 1f);
        private static readonly Color LineTint = new Color(1f, 0.52f, 0.24f, 1f);

        private sealed class Decal
        {
            internal GameObject GameObject;
            internal SpriteRenderer Renderer;
            internal EntityRef Entity;
            internal int DefId;
            internal TelegraphShape Shape;
            internal float Remaining;
            internal float Age;
            internal bool Active;
        }

        private readonly Decal[] _decals = new Decal[BattleViewCaps.Telegraphs];
        private readonly Sprite _circle;
        private readonly Sprite _bar;
        private float _clock;
        private bool _disposed;

        /// <summary>Builds and preallocates the pool.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public TelegraphPool(Transform root, BattlePlaceholderArt art)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (art is null)
            {
                throw new ArgumentNullException(nameof(art));
            }

            _circle = art.TelegraphCircle;
            _bar = art.TelegraphBar;

            for (int i = 0; i < _decals.Length; i++)
            {
                _decals[i] = Create(root, i);
            }
        }

        /// <summary>Warnings currently drawn.</summary>
        public int ActiveCount { get; private set; }

        /// <summary>Decals the pool owns (constant after construction).</summary>
        public int Capacity => _decals.Length;

        /// <summary>
        /// Turns a telegraph event into a floor decal, or removes one. Registered on
        /// <see cref="BattleEventChannel.Vfx"/>; it reads the event and nothing else.
        /// </summary>
        public void OnBattleEvent(in BattleEvent battleEvent)
        {
            switch (battleEvent.Type)
            {
                case BattleEventType.TelegraphStarted:
                    Show(battleEvent);
                    break;
                case BattleEventType.TelegraphEnded:
                    Hide(battleEvent);
                    break;
                default:
                    break;
            }
        }

        /// <summary>Advances the pulse and expires any warning whose end never arrived.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f))
            {
                return;
            }

            _clock += deltaSeconds;
            float pulse = 0.7f + (0.3f * Mathf.Sin(_clock * BattleViewCaps.TelegraphPulseRadiansPerSecond));

            for (int i = 0; i < _decals.Length; i++)
            {
                Decal decal = _decals[i];
                if (!decal.Active)
                {
                    continue;
                }

                decal.Age += deltaSeconds;
                decal.Remaining -= deltaSeconds;

                if (decal.Remaining <= 0f)
                {
                    Retire(decal);
                    continue;
                }

                if (decal.Renderer == null)
                {
                    continue;
                }

                float fade = Mathf.Clamp01(decal.Age / BattleViewCaps.TelegraphFadeInSeconds);
                Color tint = decal.Shape == TelegraphShape.Line ? LineTint : CircleTint;
                tint.a = Mathf.Lerp(0.25f, 0.85f, fade) * pulse;
                decal.Renderer.color = tint;
            }
        }

        /// <summary>Removes every warning; called when a new floor starts.</summary>
        public void Clear()
        {
            for (int i = 0; i < _decals.Length; i++)
            {
                if (_decals[i].Active)
                {
                    Retire(_decals[i]);
                }
            }

            ActiveCount = 0;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = 0; i < _decals.Length; i++)
            {
                Decal decal = _decals[i];
                if (decal is null || decal.GameObject == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(decal.GameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(decal.GameObject);
                }

                decal.GameObject = null;
            }

            ActiveCount = 0;
        }

        private void Show(in BattleEvent battleEvent)
        {
            if (_disposed)
            {
                return;
            }

            TelegraphShape shape = (TelegraphShape)battleEvent.Value;
            if (shape != TelegraphShape.Circle && shape != TelegraphShape.Line)
            {
                // An event from a newer build than this view: draw the shape it can draw.
                shape = TelegraphShape.Circle;
            }

            // One warning per ghost at a time: an event that restarts the same attack must not
            // leave the previous decal behind.
            for (int i = 0; i < _decals.Length; i++)
            {
                Decal existing = _decals[i];
                if (existing.Active && existing.Entity == battleEvent.Entity && existing.DefId == battleEvent.DefId)
                {
                    Retire(existing);
                }
            }

            Decal decal = Next();
            if (decal is null)
            {
                // Every slot is in use; one lost warning is a cosmetic loss and the pool never
                // grows. The simulation's damage is unaffected.
                return;
            }

            decal.Entity = battleEvent.Entity;
            decal.DefId = battleEvent.DefId;
            decal.Shape = shape;
            decal.Age = 0f;
            decal.Remaining = BattleViewCaps.TelegraphMaxLifetimeSeconds;
            decal.Renderer.sprite = shape == TelegraphShape.Line ? _bar : _circle;
            decal.Renderer.color = shape == TelegraphShape.Line ? LineTint : CircleTint;
            decal.Renderer.enabled = true;
            decal.GameObject.SetActive(true);
            decal.Active = true;
            ActiveCount++;

            Vector3 origin = BattleWorldMap.ToWorld(battleEvent.X, battleEvent.Y);
            decal.GameObject.transform.SetPositionAndRotation(origin, Orientation(battleEvent.Angle));

            if (shape == TelegraphShape.Line)
            {
                // The bar is one world unit in each direction, so it is scaled by the numbers
                // the rule used: its length, and twice its half-width.
                float length = Mathf.Max(0.1f, battleEvent.Length);
                float width = Mathf.Max(0.1f, battleEvent.Radius * 2f);
                decal.GameObject.transform.localScale = new Vector3(length, width, 1f);
            }
            else
            {
                float diameter = Mathf.Max(0.1f, battleEvent.Radius * 2f);
                decal.GameObject.transform.localScale = new Vector3(diameter, diameter, 1f);
            }
        }

        private void Hide(in BattleEvent battleEvent)
        {
            for (int i = 0; i < _decals.Length; i++)
            {
                Decal decal = _decals[i];
                if (decal.Active && decal.Entity == battleEvent.Entity && decal.DefId == battleEvent.DefId)
                {
                    Retire(decal);
                    return;
                }
            }
        }

        /// <summary>
        /// A warning lies flat on the floor. The sprite's own +X runs along the attack's aim,
        /// rotated about the ground normal; a circle has no aim to show, so it is drawn square.
        /// </summary>
        private static Quaternion Orientation(float angleRadians)
        {
            return Quaternion.Euler(90f, -angleRadians * Mathf.Rad2Deg, 0f);
        }

        private Decal? Next()
        {
            for (int i = 0; i < _decals.Length; i++)
            {
                if (!_decals[i].Active)
                {
                    return _decals[i];
                }
            }

            return null;
        }

        private void Retire(Decal decal)
        {
            decal.Active = false;
            decal.Remaining = 0f;
            decal.Entity = EntityRef.None;

            if (decal.Renderer != null)
            {
                decal.Renderer.enabled = false;
            }

            if (decal.GameObject != null && decal.GameObject.activeSelf)
            {
                decal.GameObject.SetActive(false);
            }

            if (ActiveCount > 0)
            {
                ActiveCount--;
            }
        }

        private static Decal Create(Transform root, int index)
        {
            var gameObject = new GameObject(
                "telegraph-" + index.ToString(CultureInfo.InvariantCulture));
            gameObject.transform.SetParent(root, worldPositionStays: false);

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = BattleViewCaps.TelegraphSortingOrder;
            renderer.enabled = false;

            gameObject.SetActive(false);

            return new Decal
            {
                GameObject = gameObject,
                Renderer = renderer,
            };
        }
    }
}
