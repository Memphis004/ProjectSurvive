# Gotcha — a `CanvasScaler`'s virtual canvas follows the aspect

With `ScaleWithScreenSize` and `match = 0.5`, the scaler picks a scale factor from
both axes and the **virtual rect is not the reference rect**. At the template's own
960×600 canvas the scale factor is 0.527 (the geometric mean of 960/1920 and
600/1080), so the virtual rect is **1821.6 × 1138.5** — neither 1920 nor 1080.

A control laid out with 1920-wide arithmetic from the canvas' top-left corner is
therefore **26 px right of the centre** at 960×600, while centre-anchored labels stay
centred. Nothing is wrong with the layout code; it was written for a canvas that does
not exist at that resolution.

**What to do:**

- Anchor content that must be centred at `(0.5, 0.5)` with a zeroed
  `anchoredPosition`, or place it with symmetric anchors — never from a hard-coded
  1920-wide coordinate.
- When a layout must be exact, compute it from the canvas' own rect at runtime rather
  than from the reference resolution.
- In a browser test, assert the control's **published centre** against the canvas
  size the same document published (the game-era title spec did exactly that and
  caught this).
