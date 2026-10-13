# ADR 0009 — Bag and item footprint: a picture string, parsed in Domain

- **Status:** Accepted (Stage 04)
- **Date:** 2026-10-14
- **Supersedes:** [ADR 0005](0005-data-pipeline-shape-and-format.md) **§1 only** (how a
  footprint is authored). ADR 0005's other decisions — CSV tables, the two generated
  targets, the reproducibility gate, the `--strict` / validator split — all stand.

## Context

Stage 02 had to choose a way to author a bag's shape before any content existed, and
[ADR 0005](0005-data-pipeline-shape-and-format.md) §1 chose an explicit list of
`"x,y"` cell offsets: `"0,0|1,0|0,1"` is the L-tromino. It explicitly rejected the
alternative, a bitmap string.

Two things changed at Stage 04, when the 14 prototype shapes were actually authored:

1. **The canonical GDD specifies the picture form.** The combined GDD (and the
   Stage 04 prompt drawn from it) says shapes are stored as a string such as
   `XX.|.XX` (`X` = a covered cell) and parsed in Domain. The GDD is the canonical
   document, so this is a decision it has already made, not a free choice.
2. **The Stage 02 rejection argument does not hold for this format.** ADR 0005
   rejected bitmaps because "an empty shape and a duplicated cell are then only
   detectable by parsing the picture". In a picture a duplicated cell is not merely
   detected — it is *unrepresentable*, and an all-`.` picture is trivially
   distinguishable from no picture at all.

The cost of the old format was also real and measurable: a 3×3 bag was nine tokens in
a CSV cell, which is exactly the kind of balance value that has to be reviewable in a
diff.

## Decision

`footprint` stays a `list#sep=|,string` column (so the Luban type, the loader and the
generated readers do not change), but **each element is one ROW of a picture**:

- `X` marks a cell the piece covers; `.` marks an empty cell.
- All rows have the same length; the row order is top to bottom, and within a row the
  character order is left to right.
- The value is **normalised on parse** so the minimum `x`/`y` is 0, exactly as before.

```
"XXX|XXX|XXX"   the 3x3 cotton bag (9 cells, 3x3)
"X|X|X"         the 1x3 yant pouch
"X.|X.|XX"      the L-tromino basket - the asymmetric shape the rotation rules need
"XX|XX"         the 2x2 spell tome
"XX"            the 2x1 kuman thong familiar
"X"             every 1x1 piece
```

`Footprint.TryParse` still returns a `CellOffset` set plus a normalised `Width`/
`Height`, so the Domain type is unchanged and the other half of the GDD's sentence —
*footprint เป็นชุดพิกัดช่อง* (a footprint is a set of cell coordinates) — still holds.
Only the authoring syntax moved.

Three failure reasons replace the old three:

| Code | When |
| --- | --- |
| `footprint.empty` | no row at all, or a picture with no `X` |
| `footprint.ragged_row` | rows of different lengths — the picture is not a rectangle |
| `footprint.malformed_cell` | a character that is neither `X` nor `.` |

`footprint.duplicate_cell` is **deleted**, and that is a deliberate gain rather than a
loss of validation: the format makes the bug unrepresentable instead of merely
detected.

`item` also gains a `footprint` column. Items occupy board cells (the GDD's tome is
2×2 and the familiar 2×1, and an item is placeable only where a bag covers every one
of its cells), so without the column two of the fourteen prototype pieces could not be
expressed at all.

## Alternatives considered

- **Keep the `"x,y"` token list (ADR 0005 §1).** Rejected: it contradicts the canonical
  GDD, it was chosen before any shape existed, and the format's only unique benefit
  (detecting a duplicated cell) is a bug class the picture removes by construction.
- **A `#`/`.` bitmap string.** Same shape idea, but `#` is the comment character in the
  CSV convention this project already uses (`##` header rows), and `X` reads as
  "filled" more directly. Rejected for the comment-collision risk.
- **A rectangle (`width` × `height`).** Cannot express the L basket, which is the
  shape Stage 04 requires precisely to make rotation testable. Rejected.
- **A fixed boolean matrix (`list#sep=|,bool` of `width*height`).** Same information,
  but a non-rectangular shape would need padding that has to be declared too. Rejected.

## Consequences

- **Reviewed data is more readable**: 14 shapes are now recognisable at a glance in
  `data/tables/bag.csv` and `data/tables/item.csv`.
- **`SchemaVersion` moves 1 → 2** in `tools/gen.ps1` (a field was added).
- **The Stage 02 fixture set changed shape**: `footprint-duplicate-cell` is replaced by
  `footprint-ragged-row`, and `item-footprint-empty` was added. Both are proven by
  `tools/check-fixtures.ps1`.
- **A future rectangle-shaped piece still works** (every row simply ends with `.`),
  and a shape drawn off-origin in the picture is normalised away, so authors are free
  to draw for readability.
- **A non-rectangular bounding pattern is not expressible** (e.g. a shape that would
  want row 0 three wide and row 1 one wide *with the hole in the middle at a
  different offset*) — but `.` fills that role, so this is a non-limitation.
