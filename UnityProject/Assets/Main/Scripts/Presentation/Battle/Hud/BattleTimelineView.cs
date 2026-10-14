using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Presentation.Battle.Hud
{
    /// <summary>
    /// The floor's timeline bar: one marker per spawn window, an elite and a boss marker
    /// among them, and a playhead for how far through the floor the battle is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A View, so it holds no rules: it is handed a list of normalized windows once per
    /// floor and a progress value every frame, and it turns them into anchored rectangles.
    /// Which window is elite or the boss is the model's answer
    /// (<see cref="BattleTimelineModel"/>), not this class's.
    /// </para>
    /// <para>
    /// Markers are pooled. The count is known before the first tick, so the rectangles are
    /// created once at the start of a floor and merely re-anchored afterwards — a floor never
    /// allocates a marker while it runs, and re-entering the scene reuses whatever the view
    /// already has.
    /// </para>
    /// </remarks>
    public sealed class BattleTimelineView : MonoBehaviour
    {
        private static readonly Color NormalColour = new Color(0.55f, 0.58f, 0.66f, 0.85f);
        private static readonly Color EliteColour = new Color(0.95f, 0.74f, 0.35f, 0.95f);
        private static readonly Color BossColour = new Color(0.85f, 0.32f, 0.34f, 1f);

        [Tooltip("Full-width track the windows are drawn inside.")]
        [SerializeField] private RectTransform _track;

        [Tooltip("Moves along the track as the floor runs.")]
        [SerializeField] private RectTransform _playhead;

        [Tooltip("Vertical inset of a marker inside the track, in pixels.")]
        [SerializeField] private float _markerInset = 2f;

        private readonly List<Image> _markers = new List<Image>(8);

        /// <summary>How many markers the bar currently shows.</summary>
        public int MarkerCount => _markers.Count;

        /// <summary>Creates or re-anchors the markers for a floor.</summary>
        public void SetMarkers(IReadOnlyList<BattleTimelineMarker> markers)
        {
            if (_track is null)
            {
                return;
            }

            int count = markers?.Count ?? 0;
            EnsureMarkers(count);

            for (int i = 0; i < _markers.Count; i++)
            {
                Image marker = _markers[i];
                bool used = i < count;

                marker.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                BattleTimelineMarker model = markers[i];
                RectTransform rect = marker.rectTransform;
                rect.anchorMin = new Vector2(model.Start01, 0f);
                rect.anchorMax = new Vector2(Mathf.Max(model.End01, model.Start01 + 0.002f), 1f);
                rect.offsetMin = new Vector2(0f, _markerInset);
                rect.offsetMax = new Vector2(0f, -_markerInset);
                marker.color = ColourFor(model.Kind);
            }
        }

        /// <summary>Moves the playhead to <paramref name="progress01"/> along the track.</summary>
        public void SetProgress(float progress01)
        {
            if (_playhead is null)
            {
                return;
            }

            float value = progress01 < 0f ? 0f : (progress01 > 1f ? 1f : progress01);
            _playhead.anchorMin = new Vector2(value, 0f);
            _playhead.anchorMax = new Vector2(value, 1f);
        }

        private static Color ColourFor(BattleTimelineMarkerKind kind)
        {
            switch (kind)
            {
                case BattleTimelineMarkerKind.Elite:
                    return EliteColour;
                case BattleTimelineMarkerKind.Boss:
                    return BossColour;
                default:
                    return NormalColour;
            }
        }

        private void EnsureMarkers(int count)
        {
            while (_markers.Count < count)
            {
                var markerObject = new GameObject(
                    "marker-" + _markers.Count, typeof(RectTransform), typeof(Image));

                var rect = (RectTransform)markerObject.transform;
                rect.SetParent(_track, worldPositionStays: false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                _markers.Add(markerObject.GetComponent<Image>());
            }
        }
    }
}
