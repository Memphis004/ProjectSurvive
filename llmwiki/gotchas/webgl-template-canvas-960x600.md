# Gotcha — Unity's default desktop WebGL template pins the canvas at 960×600

The desktop branch of Unity's default template writes `canvas.style.width/height =
960px/600px`. The player therefore renders 960×600 in a letterbox on every desktop
browser: a 1920×1080 **viewport** changes nothing, the player is not told about it,
and any measurement taken in "the browser" is a measurement at 960×600.

**What a page can change** is the canvas **element** — the template's own fullscreen
button does exactly that, and a HiDPI backing store changes the render resolution.
The player follows that within one publish (the game-era camp was measured through
960×600 → 1280×720 → 1920×1080 → back).

**Consequences for a browser test:**

- Never assume the viewport is the canvas. Read the size the **player published**
  (the `__projectSurvive*` documents carry `canvasWidth`/`canvasHeight`) and aim
  clicks with it. A document that says 1280 wide while the code still uses
  960-specific arithmetic aims 134 px off.
- Resizing the window is not a resize of the game. If a test must exercise a larger
  canvas, style the element (or use the template's fullscreen control).

**If a game wants a window-filling canvas**, that is a small change to the template
plus a project-wide re-measurement — do it once, deliberately, and record it.
