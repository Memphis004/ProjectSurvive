# ADR 0031 — A tome touches its weapon: `synergy.tome_row_damage` is `AdjacentPair`

- **Status:** Accepted (Stage 09k-data)
- **Date:** 2026-10-21
- **Supersedes:** the *shipping choice* of [ADR 0015](0015-same-line-synergy-kinds.md) (the tome
  combo used `SameRow`). The two same-line **kinds** stay in the schema and in the evaluator; no
  shipped row uses them any more.
- **Related:** [ADR 0009](0009-footprint-picture-format.md) (the 2×2 tome picture),
  [ADR 0020](0020-bag-moves-carry-the-items-on-them.md) (a bag carries what stands on it, so the
  pair's cells are read after every gesture)

## Context

The GDD's combination table said “ตำราพระเวทอยู่แถวเดียวกับอาวุธอาคม → ดาเมจ +20% ซ้อนได้สูงสุด
2 ชั้น”, and Stage 04 authored exactly that: `synergy.tome_row_damage` with `kind = SameRow`.
ADR 0015 kept that literal reading and added `SameColumn` as the second half of “อยู่แถวเดียวกัน”,
with no shipped row using it.

Two consequences of `SameRow` turned out to be wrong for the game the players see:

1. **The row reaches across empty cells.** A weapon three cells along the row is empowered by a tome
   that touches nothing. That is a combo the player cannot see and cannot explain from the board.
2. **Above and below are *not* a connection.** A weapon standing directly against the tome's lower
   edge — the most legible “these two are together” arrangement on a 2×2 piece — got nothing.

Stage 09j/09k had to bolt the missing half onto the *screen*: `CampPresenter.DrawnLinks` drew a
permanent line only for a `SameRow`/`SameColumn` pair that also passed `PieceInstance.IsAdjacentTo`,
so the mark said “touching” while the rule said “same row”. The board and the bonus disagreed, and
the screenshot of a line across a gap is what exposed it.

The design intent, stated by the user in the stage prompt, is contact: **left, right, above or
below; no gap; no diagonal.**

## Decision

**Author the shipped tome rule as `AdjacentPair`.** One word changes in
`data/tables/synergy.csv`:

```
,synergy.tome_row_damage,synergy.tome_row_damage.name,AdjacentPair,tome,weapon,Damage,20,2
```

- The **id is unchanged** (it is in the save/replay vocabulary), as are `tag_a = tome`,
  `tag_b = weapon`, `stat = Damage`, `value_percent = 20` and `max_stacks = 2`.
- **No code, no schema, no `SchemaVersion` change.** `AdjacentPair` already existed and the
  evaluator already applied it; this is a data decision, which is where balance and rule shape
  belong (AGENTS rule 5).
- **`SameRow` and `SameColumn` remain valid kinds** (schema values 2 and 4, evaluator cases,
  tests). They are the general “same line, any distance” relations and a future rule may want them;
  the shipping data simply has no row that does.

### What the numbers mean now

The evaluator counts **distinct partner pieces**, never cells, and it visits them in canonical
order until the rule's `max_stacks` is reached:

- A pair of pieces that touch along two or three cells (a 2×2 tome against a 1×1 weapon can share
  only one edge; against another 2×2 it could share two) is **one** pair and pays **once**. This is
  the pre-existing rule (“ติดกันหลายช่องนับความสัมพันธ์ครั้งเดียว”) and it did not move.
- `max_stacks = 2` means **one weapon takes the bonus from at most two tomes**, whichever three or
  four of them it touches. It was a per-*target* count under `SameRow` too, so its meaning is
  unchanged — what changed is *which* tomes qualify (partners the weapon touches, not partners on
  its row).
- `tag_a` still provides and `tag_b` still receives: the tome is untouched, the weapon gets
  `+20 %` per stack, and the two applications add to `+40 %` before being applied.

## Alternatives considered

- **Keep `SameRow`** (literal GDD) and let only the screen filter. Rejected: the bonus would keep
  paying for a pair the player cannot see, and the screen's filter is a second, silent rule.
- **Widen `SameRow` to mean “same row, or touching”**. Rejected: it silently changes every future
  row rule and makes the kind's name a lie; a kind is a relation, not a policy.
- **Add a second rule row** (`tome_adjacent_damage`) beside the row rule. Rejected: two tome rules
  would stack on a touching pair (+40 % from one tome) — a balance change nobody asked for, and the
  old row rule would still pay across a gap.
- **A new `SameLine` kind with a distance column.** Rejected as machinery for a condition the
  existing `AdjacentPair` already expresses exactly.

## Consequences

- **The board and the bonus now say the same thing.** A weapon is empowered by a tome it touches,
  from any of the four edges, and never diagonally or across a gap.
- **The camp screen has a gap until Stage 09l.** `CampPresenter.DrawnLinks` chooses a mark from
  `synergy.kind`, and `AdjacentPair` is drawn as *nothing permanent* (hover highlight only). The
  permanent line the tome rule used to draw (via the `SameRow` case) therefore disappears, even
  for a touching pair. Stage 09l gives `AdjacentPair` its own mark, reading the pair's two cells the
  way the `SameRow` case does today. The truth is pinned by an EditMode test
  (`With_the_shipped_touching_rule_the_pair_is_a_combo_and_draws_no_permanent_mark`), not only by a
  screenshot.
- **The PlayMode line suites describe the old shape.** `CampSynergyLinePlayModeTests` and the two
  line cases in `CampBagDragPlayModeTests` build “touching vs one cell away” layouts and expect one
  line; with the new rule the far pair is no longer a combo at all, so those cases belong to 09l's
  rewrite. They are **not** part of the Fast suite and were not run in this stage.
- **The balance instrument barely notices.** `tools/sim.ps1 --seeds 200` over the four bot builds
  moved the boss win rate from **45.0 % to 44.5 %** (default; one seed of 200 changed outcome) and
  left ranged/melee/familiar identical row for row: the greedy camp bot rarely puts the 2×2 tome
  against a weapon, so this change is a rule the bot cannot exercise. That is a limit of the
  instrument, not evidence that the rule is inert.
- **A data author can still ship a same-line rule** with a CSV row and no code change (ADR 0015's
  reason for keeping both kinds). The evaluator's `SameRow`/`SameColumn` cases remain covered by
  tests that re-point the shipped row at them.
