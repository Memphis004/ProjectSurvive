# Gotcha — a layout report written in the frame a canvas resizes is a frame early

`Screen.width`/`Screen.height` report the **new** size in the frame the element
changed, while `CanvasScaler` lays the widgets out of it during that frame's render.
A director that publishes its report once per size change therefore publishes the
**new size with the old geometry** and then stops, because the size no longer
changes.

The game-era symptom: `__projectSurviveTitle` said `canvasWidth: 1280` while
`buttons[0].x` was still the 960-wide position — 134 px from where the centre had
moved to, so the browser test clicked empty space and the stage's own gate failed.

**Fix at the cause:** publish, compare the point the report carries with the point the
view computes, and republish while they disagree (or publish on a layout-changed
callback rather than on a resolution poll). Do not add a one-frame delay and hope:
the number of frames depends on the machine.

**Applies to:** any report a browser test uses for coordinates — screens, cells,
buttons, published rectangles.
