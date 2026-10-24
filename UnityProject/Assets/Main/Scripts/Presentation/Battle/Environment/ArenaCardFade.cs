using System.Collections.Generic;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle.Environment
{
    /// <summary>
    /// Dithers away the arena cards that stand between the camera and the player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree wall is 26–40 units out, and the player may walk all the way to the arena's own
    /// bound (40), so at the edge a canopy can sit exactly over the ghost-doctor. The cards write no
    /// depth and draw by sorting order alone, so there is no depth trick that could rescue the
    /// player: the card has to thin out, and it has to do so <i>gradually</i>, because a tree that
    /// pops out of existence is worse than the tree.
    /// </para>
    /// <para>
    /// How much of the screen a card covers is pure geometry, so <see cref="Covers"/> is a static
    /// function and the decision is a unit test rather than a screenshot. The camera is tilted
    /// <c>PitchDegrees</c> from the ground, so a card <c>H</c> tall at depth <c>z</c> covers the
    /// ground rows from <c>z</c> up to <c>z + H / tan(pitch)</c>; a player standing in that band and
    /// inside the card's width is behind it.
    /// </para>
    /// <para>
    /// Cost: one pass over the standing cards per frame, no allocation, and a renderer is written
    /// only on the frames its fade actually moves (<see cref="MaterialPropertyBlock"/> per card).
    /// The cards themselves stay static and are never created, moved or destroyed at runtime.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ArenaCardFade : MonoBehaviour
    {
        /// <summary>Shader property the card reads for its dither.</summary>
        public const string FadeProperty = "_Fade";

        /// <summary>
        /// Ground rows a card covers per unit of its height: <c>1 / tan(pitch)</c>. The camera is a
        /// fixed 50° tilt, so this is a constant and the per-frame pass never calls a trigonometry
        /// function.
        /// </summary>
        public static readonly float ReachPerHeight = 1f / Mathf.Tan(BattleCameraRig.PitchDegrees * Mathf.Deg2Rad);

        private static readonly int FadeId = Shader.PropertyToID(FadeProperty);

        [Tooltip("What has to stay visible: the player. The builder wires the lantern's root, which already follows them.")]
        [SerializeField] private Transform _target;

        [Tooltip("Seconds a card takes to dither from solid to see-through (and back). 0 snaps.")]
        [SerializeField] private float _fadeSeconds = 0.16f;

        [Tooltip("Extra width either side of a card that also fades, so the effect starts before the canopy is dead centre.")]
        [SerializeField] private float _widthMargin = 0.6f;

        [Tooltip("How solid a fully occluding card stays: a faint screen-door silhouette rather than a hole where the tree was.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minFade = 0.22f;

        [Tooltip("Occlusion fade on or off. A settings switch, like the leaf sway.")]
        [SerializeField] private bool _enabled = true;

        private ArenaCard[] _cards = new ArenaCard[0];
        private MeshRenderer[] _renderers = new MeshRenderer[0];
        private float[] _widths = new float[0];
        private float[] _heights = new float[0];
        private float[] _fade = new float[0];
        private MaterialPropertyBlock _block;

        /// <summary>How many cards are currently dithered (fade below 1). Read by tests and reports.</summary>
        public int FadingCount { get; private set; }

        /// <summary>True while the occlusion fade is on.</summary>
        public bool Enabled => _enabled;

        /// <summary>
        /// How much of a card is still drawn while it covers the player. Deliberately not zero:
        /// a tree that vanishes entirely is a hole in the forest, and a hole reads as a bug next
        /// to 140 trees that do not move. A quarter-dense dither is enough for the player to be
        /// obvious through it and keeps the canopy's silhouette in place.
        /// </summary>
        public float MinFade => _minFade;

        /// <summary>
        /// Whether a card at (<paramref name="cardX"/>, <paramref name="cardZ"/>) covers a player at
        /// (<paramref name="playerX"/>, <paramref name="playerZ"/>).
        /// </summary>
        /// <param name="widthMargin">Extra width either side of the card; 0 is the card's own span.</param>
        public static bool Covers(
            float cardX, float cardZ, float cardWidth, float cardHeight,
            float playerX, float playerZ, float widthMargin = 0f)
        {
            // A card further from the camera than the player is drawn behind them (that is what
            // PropSortingOrder means), so it can never hide them.
            if (cardZ > playerZ + 0.05f)
            {
                return false;
            }

            // How far up the screen a card H tall reaches, expressed in ground rows: the screen-up
            // direction of a camera pitched θ is cos θ up and sin θ into the arena, so H of height
            // stands where the ground is H / tan θ further away.
            float reach = cardHeight * ReachPerHeight;
            if (playerZ > cardZ + reach)
            {
                return false;
            }

            float halfWidth = (cardWidth * 0.5f) + widthMargin;
            return Mathf.Abs(cardX - playerX) <= halfWidth;
        }

        /// <summary>Turns the occlusion fade on or off.</summary>
        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            if (!enabled)
            {
                RestoreAll();
            }
        }

        /// <summary>Points the fade at another follow target (the builder's wiring, or a test).</summary>
        public void SetTarget(Transform target) => _target = target;

        /// <summary>Rebuilds the card list. Called once from <c>Awake</c>; the arena never changes.</summary>
        public void Collect()
        {
            var cards = new List<ArenaCard>();
            var renderers = new List<MeshRenderer>();
            var widths = new List<float>();
            var heights = new List<float>();

            // The cards are children of the arena root this component lives on. Decals lie flat and
            // never cover the player, so they are not part of the list at all.
            foreach (ArenaCard card in GetComponentsInChildren<ArenaCard>(includeInactive: true))
            {
                if (card.Zone == ArenaCardZone.Decal)
                {
                    continue;
                }

                var renderer = card.GetComponent<MeshRenderer>();
                if (renderer is null)
                {
                    continue;
                }

                cards.Add(card);
                renderers.Add(renderer);
                widths.Add(card.transform.localScale.x);
                heights.Add(card.transform.localScale.y);
            }

            _cards = cards.ToArray();
            _renderers = renderers.ToArray();
            _widths = widths.ToArray();
            _heights = heights.ToArray();
            _fade = new float[_cards.Length];
            for (int i = 0; i < _fade.Length; i++)
            {
                _fade[i] = 1f;
            }

            _block = new MaterialPropertyBlock();
            FadingCount = 0;
        }

        private void Awake()
        {
            if (_cards.Length == 0)
            {
                Collect();
            }
        }

        private void LateUpdate()
        {
            if (!_enabled || _target is null || _cards.Length == 0)
            {
                return;
            }

            Vector3 player = _target.position;
            float speed = _fadeSeconds <= 0f ? 1f : Time.deltaTime / _fadeSeconds;
            int fading = 0;

            for (int i = 0; i < _cards.Length; i++)
            {
                Vector3 card = _cards[i].transform.position;
                bool hides = Covers(
                    card.x, card.z, _widths[i], _heights[i],
                    player.x, player.z, _widthMargin);

                float goal = hides ? _minFade : 1f;
                float current = _fade[i];
                if (!Mathf.Approximately(current, goal))
                {
                    current = Mathf.MoveTowards(current, goal, speed);
                    Write(i, current);
                }

                if (current < 0.999f)
                {
                    fading++;
                }
            }

            FadingCount = fading;
        }

        private void Write(int index, float value)
        {
            _fade[index] = value;
            _block.Clear();
            _block.SetFloat(FadeId, value);
            _renderers[index].SetPropertyBlock(_block);
        }

        private void RestoreAll()
        {
            if (_fade is null || _renderers is null)
            {
                return;
            }

            for (int i = 0; i < _fade.Length; i++)
            {
                if (_fade[i] < 0.999f)
                {
                    Write(i, 1f);
                }
            }

            FadingCount = 0;
        }
    }
}
