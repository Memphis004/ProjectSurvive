using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Where a UI rectangle actually is on screen — the one conversion the page reports share.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A browser test needs a point <i>inside</i> a control to click. <c>RectTransform.position</c> is
    /// the transform's position, which for a control whose pivot is a corner is that corner, not its
    /// centre — the defect this helper exists for, measured when the title screen's report aimed 134 px
    /// away from the button. The centre of <c>rect.rect</c> has no such ambiguity.
    /// </para>
    /// <para>
    /// <c>WorldToScreenPoint</c> takes the camera the canvas renders with: none for a screen-space
    /// overlay, the canvas' own camera otherwise. Getting that wrong is invisible in the Editor and
    /// wrong in the player, so it is asked of the canvas rather than assumed.
    /// </para>
    /// </remarks>
    public static class UiScreenRects
    {
        /// <summary>
        /// The centre of <paramref name="rect"/> in screen pixels (origin bottom-left).
        /// </summary>
        /// <returns>False — and a zero point — when the rectangle is null.</returns>
        public static bool TryCentre(RectTransform rect, out Vector2 screen)
        {
            if (rect is null)
            {
                screen = default;
                return false;
            }

            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas is not null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            screen = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            return true;
        }
    }
}
