# ADR 0015 — “อยู่แถวเดียวกัน” is two kinds: `SameRow` and `SameColumn`

- **Status:** Accepted (Stage 05)
- **Date:** 2026-10-14
- **Related:** [ADR 0009](0009-footprint-picture-format.md) (footprint picture, and the
  `SchemaVersion` convention), GDD v0.3 §combination table vs. the Stage 05 rule list

## Context

Two documents describe the tome combo and they do not agree on its scope:

- The GDD's **combination table** — the design's own statement of the rule — says
  “ตำราพระเวทอยู่แถวเดียวกับอาวุธอาคม → ดาเมจ +20% ซ้อนได้สูงสุด 2 ชั้น”, i.e. **the same
  row**. Stage 04 authored exactly that as `synergy.tome_row_damage` with
  `kind = SameRow`.
- The Stage 05 rule list asks the evaluator for “แถวและคอลัมน์” — **rows and columns**.

The two readings differ in whether a tome above/below a weapon is connected to it. The
requirement is ambiguous (AGENTS rule 12), and the resolution has a permanent
consequence: if a column rule is ever authored, the data has to be able to say so.

## Decision

**Keep both relations, as two data kinds, and ship only the row rule.**

- `SynergyKind` gains `SameColumn = 4` (schema, generated enum, Domain enum — one
  authored number, changed in `data/Defines/schema.xml` and regenerated).
- `SynergyEvaluator` handles it as a fourth relation: the same pair loop as
  `SameRow`, with the column test. No other rule machinery changes.
- **No programmatic row uses it.** The five shipped synergy rules stay exactly as
  Stage 04 authored them, so **no balance number changes**: the tome combo is still
  row-only, which is what the GDD's combination table says.
- The two kinds are the two halves of the GDD's single sentence: a piece “on the same
  line” shares a row *or* a column with the source, and the rule's `kind` chooses which.

`SchemaVersion` moves 2 → 3 (`data/Defines` changed), and the config hash changes with
it — a schema revision is a data-pipeline event, tracked by `tools/verify-generated.ps1`
like every other one.

## Alternatives considered

- **Implement only `SameRow`** (literal GDD table). Rejected as the sole answer: it
  leaves the Stage 05 requirement unmet whenever “แถวและคอลัมน์” is read as a capability
  list, and the next author of a column rule would have to touch the schema anyway.
- **Change `SameRow`'s meaning to “same row or column”.** Rejected: the shipped rule would
  silently get stronger (a tome diagonally-adjacent-ish to a weapon would start counting),
  which is a balance change decided by a code edit — exactly what rule 5 forbids.
- **Add a column rule *row* to `data/`** so the kind is exercised by shipped data.
  Rejected: it invents a combo the GDD does not list, and it changes balance. The kind is
  exercised by a test that re-points the shipped tome rule at it (proving the relation
  works end to end) with the shipped data untouched.
- **A generic “line” kind with a direction column.** Rejected as more machinery than two
  relations need; a third relation (diagonal, say) is a new kind either way.

## Consequences

- **A data author can add a column synergy with a CSV row and no code change.** That is
  what makes the second kind worth having rather than being dead code: the whole path —
  schema value, generated enum, adapter cast, evaluator relation, validator acceptance —
  exists and is tested; only a balance row is missing, deliberately.
- **`SynergyKind` values are a data contract.** They are the numbers in
  `data/Defines/schema.xml`; the Domain enum repeats them and a mismatch would silently
  reinterpret every authored rule (the adapter casts the generated value). The Domain enum
  carries a comment saying so. A kind the schema does not declare cannot even reach the
  engine: the CSV holds the enum *name*, so Luban rejects an unknown one at generation
  time, not at run time.
- **The evaluator stays total.** An unknown `kind` is skipped rather than thrown on (the
  validator is what rejects it), so a data bug cannot take the battle down mid-tick.
- **A future “same diagonal” or “distance” rule is one more kind**, not a change to the
  five rules that exist.
