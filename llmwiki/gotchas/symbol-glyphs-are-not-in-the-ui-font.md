# The UI font has no symbol glyphs — a checklist tick is `[x]`, not `✓`

**Symptom.** A checklist wrote `✓`/`▶`/`•` in front of its rows, and the browser player's own text
audit (`__projectSurviveTextFit`) failed with `missingGlyphs: "▶"` on every row — while Thai text in
the same label was perfect. The label also reported a nonsense `renderedWidth` (`-4294967000`) once
the missing glyph reached `textBounds`.

**Cause.** The shipped face is TH Sarabun New (SIL OFL), baked into a TMP SDF atlas at build time
from the glyphs the project asked for. Sarabun has no geometric-shape or dingbat coverage, and the
atlas is not regenerated for a character nobody asked it for. `▶ U+25B6`, `✓ U+2713` and `• U+2022`
are all outside it.

**Rule.** Text the player sees in a label stays inside Thai + Latin + ASCII punctuation. A state
marker is `[x]`, `[>]`, `[ ]` — three ASCII characters the atlas definitely has — not a tick or a
triangle. If a symbol is genuinely wanted, add the character to the font asset's character set and
re-bake, and prove it with the text audit rather than with a screenshot.

**Second lesson from the same defect.** The audit only sees what the scene *drew*: a label inside a
hidden panel is still measured, so a checklist row that renders fine in the editor can fail the
browser audit for a glyph the atlas never had. Run the audit on the screen whose text you changed.
