using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Camp
{
    /// <summary>
    /// Screen rectangles for the camp's UI, in the convention Unity itself reports: pixels with the
    /// origin at the <b>bottom-left</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One implementation for the three views that publish geometry (<see cref="CampBagScreenView"/>,
    /// <see cref="CampStatusBarView"/> and the shop panel), because the reason it exists is a defect
    /// that appeared three times: a <c>RectTransform</c> whose pivot is its top-left corner reports
    /// <c>transform.position</c> as that <i>corner</i>, and a corner lies on the rectangle's own
    /// boundary — <c>Rect.Contains</c> excludes the top and right edges, so a click aimed at a
    /// published corner can miss the control it was aimed at. A browser test and a PlayMode test
    /// both aim at what the report publishes (Stage 09b work item F).
    /// </para>
    /// <para>
    /// It allocates nothing, which is what lets two different callers read it at two different rates:
    /// the camp publishes a layout report when its picture moved (a canvas resize, a camera move, the
    /// panel opening), and the title's director compares its published point every frame so that a
    /// document written in the frame a resize landed — before the canvas has laid its widgets out of
    /// the new size — is rewritten once the geometry catches up. The corner buffer is a static because
    /// of that: it is shared, it is only ever read on the main thread, and nothing keeps the result.
    /// </para>
    /// </remarks>
    public static class CampScreenRects
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>The screen rectangle of a transform, or false when there is none.</summary>
        public static bool TryOf(RectTransform rect, out Rect screen)
        {
            screen = default;

            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return false;
            }

            rect.GetWorldCorners(Corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, Corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, Corners[2]);

            screen = Rect.MinMaxRect(
                Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
            return true;
        }

        /// <summary>The centre of a transform's screen rectangle, for a click that must land inside.</summary>
        public static bool TryCentre(RectTransform rect, out Vector2 screen)
        {
            screen = default;
            if (!TryOf(rect, out Rect screenRect))
            {
                return false;
            }

            screen = screenRect.center;
            return true;
        }
    }
}
