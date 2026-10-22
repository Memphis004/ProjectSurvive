# A label's rectangle is not its text's size, and TMP will not tell you about the width

Stage 13a measured every label in the built player and found ten battle labels that did not fit the
rectangle they were authored into — with no error, no warning, and a scene that looks fine in a
capture. Three facts are worth carrying forward.

## 1. 22 px Sarabun needs a 28.61 px line box

`Sarabun-Regular` at TMP size 22 reports `preferredHeight = 28.61`. The HUD's labels were authored
with heights of 28, 26 and 24, so nine of them were a fraction of a pixel short. Nothing clips (the
overflow mode is `Overflow` and no `RectMask2D` is involved), but the rendered text leaves the box the
layout check reasons about, and `UiLayoutCheck` measures rectangles, so it cannot see this.

Fix applied: every battle HUD label box is ≥ 30 px tall, and the two rows below it were shifted so the
bars keep a 2 px gap.

## 2. `isTextOverflowing` only covers the *vertical* case, and only when wrapping

`TMP_Text.isTextOverflowing` is true when `m_firstOverflowCharacterIndex != -1`, i.e. when the
wrapped text ran past the container's height. Thai has no spaces, so a *narrow* box does not wrap at a
sensible place: the MP caption "พลังอาคม" (88.95 px) in a 58 px box wrapped to **two** lines and
rendered 57.2 px tall in a 24 px box. That was visible clipping in the HUD, and again no warning.

The audit therefore checks both: `isTextOverflowing` **or** the rendered `textBounds` larger than the
rect (with a 2 px tolerance for glyph advance and descenders).

## 3. Coverage must be asked per character, from the label's own font

`UiTextFitReport` counts missing glyphs with `font.HasCharacter(c)` per distinct character, skipping
spaces — the same probe the Thai text spike settled on
([`tmp-tryaddcharacters-lies.md`](tmp-tryaddcharacters-lies.md)). Do **not** use `TryAddCharacters`'
return value for a coverage check.

## How it is enforced now

`UnityProject/Assets/Main/Scripts/Presentation/Web/UiTextFitReport.cs` publishes
`window.__projectSurviveTextFit` from each scene director (title, camp, battle, result). Each row is
`{name, text, rectWidth/Height, neededWidth/Height, renderedWidth/Height, overflows, missingGlyphs}`.
`tests/web/textfit.mjs` asserts `overflows == []`, `missingGlyphCount == 0`, and that the screen drew
Thai at all. When adding or rewording a label, run the browser specs — a string that grew is a test
failure with the label's name and both sizes in the message.

Measured Stage 13a (Development WebGL, Chrome): Title 9 labels / Camp 35 / Battle 30 / Result 7, all
fit, 0 missing glyphs.
