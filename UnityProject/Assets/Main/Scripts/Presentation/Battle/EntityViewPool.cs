using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The battle's sprite pool: every view the scene will ever need, built once, then
    /// handed out and taken back for the rest of the floor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Acceptance requirement: "ไม่ Instantiate/Destroy ต่อเนื่องหลัง pool warm-up". The
    /// pool is warmed in <see cref="Warmup"/> to <see cref="BattleViewCaps"/> — the
    /// simulation's own structural caps — and after that a run must create nothing. The
    /// counters (<see cref="CreatedTotal"/>, <see cref="CreatedAfterWarmup"/>) exist so that
    /// claim is <i>measured</i> rather than asserted by reading the code: the PlayMode test
    /// plays a floor and fails if the second counter ever moves.
    /// </para>
    /// <para>
    /// A frame that somehow asked for more views than the cap would grow the pool and count
    /// it, rather than dropping a ghost. Growing is the failure the counter reports; it is
    /// not a system the game relies on.
    /// </para>
    /// </remarks>
    public sealed class EntityViewPool : IDisposable
    {
        private readonly Transform _root;
        private readonly BattlePlaceholderArt _art;
        private readonly List<EntityView>[] _free;
        private readonly List<EntityView> _all;
        private readonly string[] _kindNames;

        private bool _disposed;

        /// <summary>Creates an empty pool parented under <paramref name="root"/>.</summary>
        public EntityViewPool(Transform root, BattlePlaceholderArt art)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _art = art ?? throw new ArgumentNullException(nameof(art));

            _free = new List<EntityView>[5];
            for (int i = 0; i < _free.Length; i++)
            {
                _free[i] = new List<EntityView>(8);
            }

            _all = new List<EntityView>(BattleViewCaps.PlayerViews + BattleViewCaps.EnemyViews
                + BattleViewCaps.ProjectileViews + BattleViewCaps.PickupViews);

            _kindNames = new string[5];
            _kindNames[(int)RenderEntityKind.None] = "none";
            _kindNames[(int)RenderEntityKind.Player] = "player";
            _kindNames[(int)RenderEntityKind.Enemy] = "ghost";
            _kindNames[(int)RenderEntityKind.Projectile] = "bolt";
            _kindNames[(int)RenderEntityKind.Pickup] = "drop";
        }

        /// <summary>Every view ever created by this pool.</summary>
        public int CreatedTotal { get; private set; }

        /// <summary>Views created after <see cref="Warmup"/> finished. Must stay 0 in a floor.</summary>
        public int CreatedAfterWarmup { get; private set; }

        /// <summary>True once <see cref="Warmup"/> has run.</summary>
        public bool WarmupComplete { get; private set; }

        /// <summary>Views currently handed out.</summary>
        public int InUseCount { get; private set; }

        /// <summary>Views the pool owns, free and in use together.</summary>
        public int Capacity => _all.Count;

        /// <summary>
        /// Builds every view the caps allow. Called once, from the scene's start, so the
        /// first ghost of the first wave cannot cause a frame spike.
        /// </summary>
        public void Warmup()
        {
            Warm(RenderEntityKind.Player, BattleViewCaps.PlayerViews);
            Warm(RenderEntityKind.Enemy, BattleViewCaps.EnemyViews);
            Warm(RenderEntityKind.Projectile, BattleViewCaps.ProjectileViews);
            Warm(RenderEntityKind.Pickup, BattleViewCaps.PickupViews);
            WarmupComplete = true;
        }

        /// <summary>Takes a view for an entity of <paramref name="kind"/>.</summary>
        public EntityView Acquire(RenderEntityKind kind)
        {
            if (kind == RenderEntityKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "There is no view for RenderEntityKind.None.");
            }

            List<EntityView> free = _free[(int)kind];
            if (free.Count == 0)
            {
                // Exhausted: grow and count it. Never reached by a floor the caps cover.
                if (WarmupComplete)
                {
                    CreatedAfterWarmup++;
                }

                EntityView grown = Create(kind);
                grown.InUse = true;
                InUseCount++;
                return grown;
            }

            int last = free.Count - 1;
            EntityView view = free[last];
            free.RemoveAt(last);
            view.InUse = true;
            InUseCount++;
            return view;
        }

        /// <summary>Returns a view to its kind's free list and hides it.</summary>
        public void Release(EntityView view)
        {
            if (view is null || !view.InUse)
            {
                return;
            }

            // Read the kind before the view is cleared: a released view belongs back in
            // the list it came from, not in whatever kind it happens to end up as.
            RenderEntityKind kind = view.Kind == RenderEntityKind.None ? RenderEntityKind.Enemy : view.Kind;

            view.Hide();
            view.InUse = false;
            view.Kind = RenderEntityKind.None;
            InUseCount--;

            _free[(int)kind].Add(view);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (int i = 0; i < _all.Count; i++)
            {
                EntityView view = _all[i];

                // `!= null`, not `is not null`: a destroyed Unity object compares equal to null
                // only through Unity's own operator. A scene that is being unloaded has already
                // destroyed the pooled objects by the time this runs.
                if (view.GameObject != null)
                {
                    DestroyObject(view.GameObject);
                }
            }

            _all.Clear();
            for (int i = 0; i < _free.Length; i++)
            {
                _free[i].Clear();
            }

            InUseCount = 0;
        }

        private void Warm(RenderEntityKind kind, int count)
        {
            for (int i = 0; i < count; i++)
            {
                EntityView view = Create(kind);
                _free[(int)kind].Add(view);
            }
        }

        private EntityView Create(RenderEntityKind kind)
        {
            var gameObject = new GameObject("view-" + _kindNames[(int)kind]);
            gameObject.transform.SetParent(_root, worldPositionStays: false);

            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = _art.SpriteFor(kind);
            renderer.sortingOrder = BattleWorldMap.GroundSortingOrder + 1;
            renderer.enabled = false;

            var view = new EntityView(gameObject) { Kind = kind };
            _all.Add(view);
            CreatedTotal++;
            return view;
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}
