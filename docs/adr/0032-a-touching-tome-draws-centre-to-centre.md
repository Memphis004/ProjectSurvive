# ADR 0032 — A touching tome draws one line, from its middle to the weapon's middle

- **Status:** Accepted (Stage 09l)
- **Date:** 2026-10-21
- **Related:** [ADR 0031](0031-a-tome-touches-its-weapon.md) (the rule became `AdjacentPair`),
  [ADR 0015](0015-same-line-synergy-kinds.md) (the same-line kinds, now unshipped),
  [ADR 0020](0020-bag-moves-carry-the-items-on-them.md) (a bag carries what stands on it)

## Context

After ADR 0031 the tome's rule is `AdjacentPair`, and the same kind now names four rules:

| rule | tags | mark before this stage |
| --- | --- | --- |
| `synergy.tome_row_damage` | tomb / weapon | none (the kind was drawn as "highlight only") |
| `synergy.takrut_yant_cooldown` | takrut / yant | none |
| `synergy.black_magic_amulet…` | black magic / amulet | none |
| `synergy.red_water_spirit…` | red water / spirit | none |

`CampPresenter.DrawnLinks` decided the mark from `synergy.kind`, which was right while exactly one
kind meant "the tome". With the tome moved to the same kind as three other charm pairs, "draw a line
for `AdjacentPair`" would have given all four rules a permanent line — including pairs the stage
explicitly keeps highlight-only.

The second half of the problem is geometry. Stage 09k drew a line between two **cells** on a shared
row, forced level, because the rule it served was a row rule. A pair that touches above or below has
no such pair of cells, and a 2×2 tome's middle sits on the join of its four cells, so a cell-based
mark cannot express "these two pieces are together".

## Decision

**The mark belongs to the rule, chosen by the rule's own tags, and it joins the two pieces' middles.**

1. `DrawnLinks` draws a `CampLinkStyle.Line` for an `AdjacentPair` whose two tags are `tome` and
   `weapon` (either order), read from `data/tables/synergy.csv`. No rule id is written in code, and
   the other three adjacency rules keep their highlight-and-tooltip behaviour.
2. `CampLink` carries only the pair and the style; the board cells it used to carry are gone.
   `CampBagScreenView.PlaceLine` reads the two **drawn rectangles** and draws from
   `sourceRect.center` to `targetRect.center`, so a pair above/below draws as readily as a pair side
   by side, and a 2×2 tome's end sits on the cross of its four cells.
3. "Not touching" needs no code of its own: Core stops reporting the pair, `DrawnLinks` stops
   listing it, and the view hides the mark on the same render.

The alternative — having the Application read model tag a pair as "tome feeding a weapon" — was
rejected as the larger change: it adds a field to a Core-adjacent type to answer a question the
rule's own tags already answer, and it would put a drawing concern into data the battle also reads.

## Consequences

- A line now means exactly one thing: **this ตำรา empowers that อาวุธ**. It appears for all four
  directions, and disappears the moment contact is lost.
- Two weapons beside one tome draw two lines; both end on the tome's middle, which is the only point
  a line can reach without inventing a "which cell of the piece" rule.
- The stroke crosses the piece it belongs to, so the layer matters more than before: names stay in a
  container drawn after the marks (Stage 09k), and the view is asserted to keep that order.
- A rule added later with the tags `tome`/`weapon` arrives with the mark and no code change; a rule
  with any other tag pair arrives with no mark.
