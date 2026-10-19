using System;
using System.Collections.Generic;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>What the mini bag is drawing over one piece's box.</summary>
    public enum MiniBagPlateTone
    {
        /// <summary>Nothing: the piece is ready and can pay for its cast.</summary>
        None = 0,

        /// <summary>A dark plate whose height is how much of the cooldown is left.</summary>
        Counting = 1,

        /// <summary>A faint blue plate: ready, but the pool cannot pay (the frame's <c>ManaShort</c>).</summary>
        ManaShort = 2,
    }

    /// <summary>
    /// The left-hand mini ย่าม: the bag's own box at 1:1 with the board, one coloured box per
    /// piece, and a cooldown plate over every piece that can cast.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Everything is created once, at the start of the floor, and only ever rewritten.</b> The
    /// pools are sized from the board's own maximum (<see cref="BattleViewCaps.MiniBagCells"/>,
    /// the 9×7 board), so a floor cannot ask for a view it does not have; a piece that is not on
    /// the board is an <c>SetActive(false)</c> on an object that already exists. Nothing here
    /// formats a string per frame, and the tier numbers are formatted once in
    /// <see cref="MiniBagPiece.TierText"/> when the layout is built (AGENTS rule 8).
    /// </para>
    /// <para>
    /// <b>The cooldowns come from the frame, never from a clock.</b>
    /// <see cref="ApplyCooldowns"/> is handed the cooldown list the runner copied out of the frame
    /// it just stepped — the same list the fight published, keyed by board instance id — and a
    /// plate is written <i>only</i> when its fraction or its tone actually changed. A View with no
    /// clock of its own cannot drift from the battle, and a floor standing still costs nothing.
    /// </para>
    /// <para>
    /// It subscribes to nothing: no entity, no reactive value. What it reads is a span.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleMiniBagView : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // The panel's own geometry (Stage 08d). Named constants, because these are the two
        // numbers the layout reads back: the caption's line box and the gap under it decide where
        // the grid starts, and the grid shares the caption's left edge. They are UI reference
        // pixels (the canvas is 1920x1080 reference at both the sizes the GDD names), and they are
        // applied through the panel's own anchors rather than as a position on the canvas.
        // ------------------------------------------------------------------

        /// <summary>Left inset shared by the caption and by the grid, in UI pixels.</summary>
        public const float LeftInset = 10f;

        /// <summary>Right inset of the grid inside the panel, in UI pixels.</summary>
        public const float RightInset = 10f;

        /// <summary>Bottom inset of the grid inside the panel, in UI pixels.</summary>
        public const float BottomInset = 10f;

        /// <summary>Top inset of the caption from the panel's top edge, in UI pixels.</summary>
        public const float CaptionTop = 6f;

        /// <summary>Height of the caption's line box, in UI pixels.</summary>
        public const float CaptionHeight = 30f;

        /// <summary>
        /// Gap between the caption's bottom edge and the top of the grid's area, in UI pixels —
        /// 8 px at 1080p, and the same 8 reference px at 720p (the canvas scalar draws them 2/3
        /// the size). The brief for Stage 08d asks for 8–12 px; this is the low end, so the mini
        /// ย่าม reads as belonging to the caption without touching it.
        /// </summary>
        public const float CaptionGap = 8f;

        /// <summary>Top inset of the grid's area: the caption's whole line box plus the gap.</summary>
        public const float ContentTop = CaptionTop + CaptionHeight + CaptionGap;

        [Tooltip("The panel the whole mini bag is drawn inside (its own background).")]
        [SerializeField] private RectTransform _panel;

        [Tooltip("The area below the caption the grid may use. Sized by the panel's own anchors.")]
        [SerializeField] private RectTransform _content;

        [Tooltip("Font of the tier numbers. Must be the baked Thai font.")]
        [SerializeField] private TMP_FontAsset _font;

        [Tooltip("How many cells the pool holds. The board's maximum (9x7 = 63) is what fits.")]
        [SerializeField] private int _cellPool = BattleViewCaps.MiniBagCells;

        [Tooltip("How many piece boxes the pool holds. A piece covers at least one cell.")]
        [SerializeField] private int _piecePool = BattleViewCaps.MiniBagPieces;

        [Tooltip("Gap between two cells, so a grid reads as cells rather than a slab.")]
        [SerializeField] private float _cellGap = 1f;

        [Tooltip("Floor for the tier number's size, in pixels (Stage 08c: no HUD text under 22).")]
        [SerializeField] private float _tierFontFloor = 22f;

        [Tooltip("Share of a cell the tier number takes, before the floor and the ceiling are applied.")]
        [SerializeField] private float _tierFontShare = 0.44f;

        [Tooltip("Ceiling for the tier number's size, in pixels.")]
        [SerializeField] private float _tierFontCeiling = 30f;

        private readonly List<Image> _cells = new List<Image>(BattleViewCaps.MiniBagCells);
        private readonly List<Image> _boxes = new List<Image>(BattleViewCaps.MiniBagPieces);
        private readonly List<Image> _plates = new List<Image>(BattleViewCaps.MiniBagPieces);
        private readonly List<TMP_Text> _tiers = new List<TMP_Text>(BattleViewCaps.MiniBagPieces);
        private readonly List<MiniBagPlateTone> _tones = new List<MiniBagPlateTone>(BattleViewCaps.MiniBagPieces);
        private readonly List<bool> _hasPlate = new List<bool>(BattleViewCaps.MiniBagPieces);

        private readonly Dictionary<int, int> _plateOfInstance = new Dictionary<int, int>(BattleViewCaps.MiniBagPieces);
        private readonly float[] _lastFraction = new float[BattleViewCaps.MiniBagPieces];
        private readonly MiniBagPlateTone[] _lastTone = new MiniBagPlateTone[BattleViewCaps.MiniBagPieces];

        private MiniBagLayout _layout = MiniBagLayout.Empty;
        private Backpack _bag;
        private CombatLoadoutSnapshot _loadout;
        private IGameConfig _config;
        private Vector2 _lastContent;
        private bool _built;
        private bool _poolsCreated;
        private int _pieceCount;
        private int _cellCount;
        private int _casterCount;
        private int _plateUpdates;
        private int _createdAfterBuild;

        /// <summary>The layout the mini bag is drawing (empty before <see cref="Build"/>).</summary>
        public MiniBagLayout Layout => _layout;

        /// <summary>How many piece boxes are on screen.</summary>
        public int PieceCount => _pieceCount;

        /// <summary>How many cells are on screen (the bag's own box).</summary>
        public int CellCount => _cellCount;

        /// <summary>How many pieces carry a cooldown plate.</summary>
        public int CasterCount => _casterCount;

        /// <summary>How many times a plate was written since the last floor started.</summary>
        public int PlateUpdates => _plateUpdates;

        /// <summary>How many pooled objects were created <b>after</b> the floor's first build.</summary>
        public int CreatedAfterBuild => _createdAfterBuild;

        /// <summary>True once a floor has built the mini bag.</summary>
        public bool IsBuilt => _built;

        /// <summary>How many pieces the pools can hold.</summary>
        public int PieceCapacity => _piecePool;

        /// <summary>How many cells the pools can hold.</summary>
        public int CellCapacity => _cellPool;

        /// <summary>The panel's own rectangle (null when the scene did not wire one).</summary>
        public RectTransform Panel => _panel;

        /// <summary>
        /// The area the grid is drawn in: the panel minus the caption and the margins. Read by the
        /// Editor capture command, which measures it against the panel it has to sit inside.
        /// </summary>
        public RectTransform Content => _content;

        /// <summary>How many pieces carry the faint blue "ready but no MP" tone right now.</summary>
        public int ManaShortCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _pieceCount && i < _tones.Count; i++)
                {
                    if (_hasPlate[i] && _tones[i] == MiniBagPlateTone.ManaShort)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>The area the grid may use, in pixels (the panel's own content box).</summary>
        public Vector2 ContentSize => _content is null ? Vector2.zero : _content.rect.size;

        /// <summary>Forces the canvas to lay out, so <see cref="ContentSize"/> is the size the screen will really have.</summary>
        public void Prepare() => Canvas.ForceUpdateCanvases();

        /// <summary>
        /// Anchors the grid's area to the panel's own top-left corner, below the caption and with the
        /// caption's left inset (Stage 08d).
        /// </summary>
        /// <remarks>
        /// The rect still stretches to the panel's right and bottom edges, so its size follows the
        /// panel at any screen size; only its <b>top-left</b> is pinned, and the pivot is that corner.
        /// The scene builder calls this through the Editor API when it authors the panel, and the
        /// layout tests call it again on a rect of their own — one implementation, so a test cannot
        /// disagree with the scene about where "under the caption" is.
        /// </remarks>
        public static void AnchorContent(RectTransform content)
        {
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0f, 1f);
            content.offsetMin = new Vector2(LeftInset, BottomInset);
            content.offsetMax = new Vector2(-RightInset, -ContentTop);
        }

        /// <summary>
        /// Builds the bag the floor started from: the panel, its cells, one box per piece and the
        /// plate of every piece the loadout turned into a caster.
        /// </summary>
        /// <remarks>
        /// Called once per floor, before the first tick, with the run's own bag and the loadout the
        /// battle was handed (ADR 0024). Nothing here runs per frame, and the pools are created on
        /// the first call only — a later call is a rewrite of objects that exist.
        /// </remarks>
        public void Build(Backpack bag, CombatLoadoutSnapshot loadout, IGameConfig config)
        {
            _bag = bag;
            _loadout = loadout;
            _config = config;

            Relayout();
        }

        /// <summary>
        /// Re-reads the bag, the loadout and the panel's own size and draws again. Called when the
        /// content area changed — a canvas resize, or a capture at another size — and never per
        /// frame.
        /// </summary>
        public void Relayout()
        {
            if (_bag is null || _config is null)
            {
                return;
            }

            Prepare();

            Vector2 content = ContentSize;
            _lastContent = content;

            Build(MiniBagLayout.Build(_bag, _loadout, _config, content.x, content.y));
        }

        /// <summary>
        /// Re-places the grid when the content area's size changed since the last build, and does
        /// nothing at all otherwise: two float comparisons per frame, no allocation, and a canvas
        /// that never resizes costs a floor nothing.
        /// </summary>
        public void RefreshIfResized()
        {
            if (!_built || _content is null || _bag is null)
            {
                return;
            }

            Vector2 content = ContentSize;
            if (Mathf.Approximately(content.x, _lastContent.x)
                && Mathf.Approximately(content.y, _lastContent.y))
            {
                return;
            }

            Relayout();
        }

        /// <summary>Draws a layout this view was handed (used by tests and by <see cref="Relayout"/>).</summary>
        public void Build(MiniBagLayout layout)
        {
            _layout = layout ?? MiniBagLayout.Empty;

            if (_panel is null || _content is null)
            {
                return;
            }

            _built = true;
            _plateUpdates = 0;

            int cells = _layout.HasBag ? _layout.Columns * _layout.Rows : 0;
            EnsurePools(cells, _layout.PieceCount);

            BuildCells(cells);
            BuildPieces();

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Writes each caster's cooldown plate from the frame's own list, and nothing else.
        /// </summary>
        /// <param name="cooldowns">
        /// The list the runner copied out of the tick it just stepped, keyed by board instance id
        /// (<see cref="LoadoutCooldown.InstanceId"/>). Entries for a piece this bag does not hold —
        /// an id of 0 for a hand-built loadout, or a piece that left the board — are skipped.
        /// </param>
        public void ApplyCooldowns(ReadOnlySpan<LoadoutCooldown> cooldowns)
        {
            if (!_built)
            {
                return;
            }

            for (int i = 0; i < cooldowns.Length; i++)
            {
                LoadoutCooldown entry = cooldowns[i];
                if (!_plateOfInstance.TryGetValue(entry.InstanceId, out int index))
                {
                    continue;
                }

                // A tick where the cooldown elapsed and the cast that reset it happened in the same
                // tick publishes the reset (1 → period), never a 0 a player could catch; 0 is what a
                // *blocked* entry shows.
                float fraction = entry.PeriodTicks <= 0
                    ? 0f
                    : Mathf.Clamp01(entry.RemainingTicks / (float)entry.PeriodTicks);

                MiniBagPlateTone tone = entry.ReadyButManaShort
                    ? MiniBagPlateTone.ManaShort
                    : (entry.IsReady ? MiniBagPlateTone.None : MiniBagPlateTone.Counting);

                if (_lastTone[index] == tone && Mathf.Approximately(_lastFraction[index], fraction))
                {
                    continue;
                }

                _lastTone[index] = tone;
                _lastFraction[index] = fraction;
                _tones[index] = tone;
                ApplyPlate(index, tone, fraction);
                _plateUpdates++;
            }
        }

        /// <summary>The fraction of its cooldown still to run on a piece, by instance id.</summary>
        public bool TryGetPlate(int instanceId, out float fraction, out MiniBagPlateTone tone)
        {
            if (_plateOfInstance.TryGetValue(instanceId, out int index))
            {
                fraction = _lastFraction[index];
                tone = _lastTone[index];
                return true;
            }

            fraction = 0f;
            tone = MiniBagPlateTone.None;
            return false;
        }

        /// <summary>The box of the <paramref name="index"/>-th drawn piece, in the panel's own pixels.</summary>
        public bool TryGetPieceRect(int index, out Rect rect)
        {
            if (index >= 0 && index < _pieceCount)
            {
                rect = _boxes[index].rectTransform.rect;
                return true;
            }

            rect = default;
            return false;
        }

        /// <summary>
        /// The top-left corner of the <paramref name="index"/>-th drawn box, in the content area's
        /// own pixels (x rightwards, y downwards). The first box of a bag at the origin is
        /// <c>(0, 0)</c> — the layout test's "hugs the top-left corner".
        /// </summary>
        public bool TryGetPieceTopLeft(int index, out Vector2 topLeft)
        {
            if (index >= 0 && index < _pieceCount)
            {
                topLeft = _boxes[index].rectTransform.anchoredPosition;
                return true;
            }

            topLeft = default;
            return false;
        }

        private void BuildCells(int used)
        {
            _cellCount = used;

            for (int i = 0; i < _cells.Count; i++)
            {
                Image cell = _cells[i];
                bool draw = i < used;

                cell.gameObject.SetActive(draw);
                if (!draw)
                {
                    continue;
                }

                int column = i % _layout.Columns;
                int row = i / _layout.Columns;
                float size = _layout.CellSize - _cellGap;

                var rect = (RectTransform)cell.transform;
                rect.anchoredPosition = new Vector2(
                    column * _layout.CellSize,
                    -(row * _layout.CellSize));
                rect.sizeDelta = new Vector2(size, size);

                cell.color = _layout.IsBagCell(column, row)
                    ? MiniBagPalette.Bag
                    : MiniBagPalette.EmptyCell;
            }
        }

        private void BuildPieces()
        {
            IReadOnlyList<MiniBagPiece> pieces = _layout.Pieces;
            _pieceCount = pieces.Count < _piecePool ? pieces.Count : _piecePool;
            _casterCount = 0;

            _plateOfInstance.Clear();

            for (int i = 0; i < _boxes.Count; i++)
            {
                Image box = _boxes[i];
                bool draw = i < _pieceCount;

                box.gameObject.SetActive(draw);
                _plates[i].gameObject.SetActive(false);
                _tiers[i].gameObject.SetActive(draw);
                _hasPlate[i] = false;
                _lastTone[i] = MiniBagPlateTone.None;
                _lastFraction[i] = -1f;

                if (!draw)
                {
                    continue;
                }

                MiniBagPiece piece = pieces[i];

                var rect = (RectTransform)box.transform;
                rect.anchoredPosition = new Vector2(
                    piece.Column * _layout.CellSize,
                    -(piece.Row * _layout.CellSize));
                rect.sizeDelta = new Vector2(
                    (piece.Columns * _layout.CellSize) - _cellGap,
                    (piece.Rows * _layout.CellSize) - _cellGap);

                box.color = piece.Colour;
                _tiers[i].text = piece.TierText;
                _tiers[i].fontSize = TierFontSize();

                if (!piece.Casts)
                {
                    continue;
                }

                _casterCount++;
                _hasPlate[i] = true;
                _plateOfInstance[piece.InstanceId] = i;

                // A plate starts life covering the whole box: the frame's first tick is what tells
                // the truth, and a floor's first frame is published before the first render.
                ApplyPlate(i, MiniBagPlateTone.Counting, 1f);
                _lastTone[i] = MiniBagPlateTone.Counting;
                _lastFraction[i] = 1f;
            }
        }

        private void ApplyPlate(int index, MiniBagPlateTone tone, float fraction)
        {
            Image plate = _plates[index];

            if (tone == MiniBagPlateTone.None)
            {
                plate.gameObject.SetActive(false);
                return;
            }

            plate.gameObject.SetActive(true);
            plate.color = tone == MiniBagPlateTone.ManaShort
                ? MiniBagPalette.ManaShort
                : MiniBagPalette.Cooldown;

            float height = _boxes[index].rectTransform.sizeDelta.y;
            float covered = tone == MiniBagPlateTone.ManaShort
                ? height
                : Mathf.Clamp01(fraction) * height;

            var rect = (RectTransform)plate.transform;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, covered);
        }

        private void EnsurePools(int cells, int pieces)
        {
            if (cells > _cells.Count)
            {
                CreateCells(cells);
            }

            if (pieces > _boxes.Count)
            {
                CreatePieces(pieces);
            }

            _poolsCreated = true;
        }

        private void CreateCells(int wanted)
        {
            int limit = Mathf.Max(0, _cellPool);
            int target = wanted < limit ? wanted : limit;

            while (_cells.Count < target)
            {
                var cellObject = new GameObject("MiniBagCell" + _cells.Count, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)cellObject.transform;
                rect.SetParent(_content, worldPositionStays: false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);

                Image image = cellObject.GetComponent<Image>();
                image.raycastTarget = false;
                image.color = MiniBagPalette.EmptyCell;

                _cells.Add(image);
                CountCreation();
            }
        }

        private void CreatePieces(int wanted)
        {
            int limit = Mathf.Max(0, _piecePool);
            int target = wanted < limit ? wanted : limit;

            while (_boxes.Count < target)
            {
                int index = _boxes.Count;

                var boxObject = new GameObject("MiniBagPiece" + index, typeof(RectTransform), typeof(Image));
                var boxRect = (RectTransform)boxObject.transform;
                boxRect.SetParent(_content, worldPositionStays: false);
                boxRect.anchorMin = new Vector2(0f, 1f);
                boxRect.anchorMax = new Vector2(0f, 1f);
                boxRect.pivot = new Vector2(0f, 1f);

                Image box = boxObject.GetComponent<Image>();
                box.raycastTarget = false;

                var plateObject = new GameObject("MiniBagPlate" + index, typeof(RectTransform), typeof(Image));
                var plateRect = (RectTransform)plateObject.transform;
                plateRect.SetParent(boxRect, worldPositionStays: false);
                plateRect.anchorMin = new Vector2(0f, 1f);
                plateRect.anchorMax = new Vector2(1f, 1f);
                plateRect.pivot = new Vector2(0.5f, 1f);

                Image plate = plateObject.GetComponent<Image>();
                plate.raycastTarget = false;
                plate.color = MiniBagPalette.Cooldown;
                plateObject.SetActive(false);

                var labelObject = new GameObject("MiniBagTier" + index, typeof(RectTransform));
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.SetParent(boxRect, worldPositionStays: false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;

                TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
                if (_font is not null)
                {
                    label.font = _font;
                }

                label.color = MiniBagPalette.TierText;
                label.alignment = TextAlignmentOptions.Center;
                label.raycastTarget = false;

                _boxes.Add(box);
                _plates.Add(plate);
                _tiers.Add(label);
                _tones.Add(MiniBagPlateTone.None);
                _hasPlate.Add(false);

                CountCreation();
            }
        }

        /// <summary>
        /// The tier number's size for the cell it sits in: a share of the cell, never below the
        /// 22 px floor every HUD label is held to and never so large that it fills the box.
        /// </summary>
        private float TierFontSize()
        {
            float share = _layout.CellSize * _tierFontShare;
            float floor = _tierFontFloor;
            float ceiling = _tierFontCeiling < floor ? floor : _tierFontCeiling;
            return Mathf.Clamp(share, floor, ceiling);
        }

        private void CountCreation()
        {
            if (_poolsCreated && _built)
            {
                // Only reachable if a floor needs more views than the cap: the counter is what the
                // play-mode test reads to say "the pools were sized right", so it never hides one.
                _createdAfterBuild++;
            }
        }
    }
}
