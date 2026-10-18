# ADR 0013 — Weight → movement penalty: a linear slope whose two numbers live in `data/`

- **Status:** Accepted (Stage 05)
- **Date:** 2026-10-14
- **Related:** [ADR 0006](0006-config-port-and-generated-code-isolation.md) (balance values
  reach the rules only through `IGameConfig`), GDD v0.3 (“ยิ่งหนักยิ่งเดินช้า”,
  `max_speed_penalty_percent` in `constant`)

## Context

Stage 04 authored `constant.max_speed_penalty_percent = 40` — the GDD says the *cap*
lives in the `constant` table — and then wrote down, as a known gap, that the GDD never
states the **curve** between “no weight” and that cap. Deliberately no coefficient was
invented, because inventing one would put a balance number in code (AGENTS rule 5).

Stage 05 must answer the question, because `LoadoutResolver` has to hand the battle a
`PlayerStatBlock`. Two things were open:

1. **Which function** turns carried weight into a lost percentage.
2. **Who owns the arithmetic** — the resolver, or the battle that actually moves the
   player.

## Decision

**A linear slope, with both of its numbers authored in `constant`:**

```
percent_lost = floor(total_weight * constant.weight_speed_percent_per_unit)
percent_lost = min(percent_lost, constant.max_speed_penalty_percent)
move_speed   = constant.move_speed * (1 - percent_lost / 100)
```

- `constant.weight_speed_percent_per_unit` is new in Stage 05 and is **5** percent per
  weight unit; `constant.max_speed_penalty_percent` stays **40**, so the cap is reached
  at 8.0 weight units.
- **Weight is board-only.** Chest contents are excluded, exactly as they are excluded
  from combos and from attacking (“ของในหีบไม่ให้โบนัสและไม่โจมตี”): a piece that is not
  laid out is not carried.
- **The resolver, not the battle, applies it.** The lost percentage is published on the
  loadout (`PlayerStatBlock.SpeedPenaltyPercent`) *and* already folded into
  `MoveSpeed`, so the battle reads one number and re-derives nothing. This is the same
  rule the combo bonuses follow: the battle never recomputes bag maths
  ([llmwiki/systems/backpack.md](../../llmwiki/systems/backpack.md)).
- The floor of the product is taken with a `1e-4` tolerance. The tolerance is not part
  of the rule — it absorbs `float` summation error (0.8 + 0.4 + …) that would otherwise
  cost the player a whole percent for landing one ulp below an integer. It is
  deterministic: the same inputs always produce the same integer.

## Alternatives considered

- **Exponential / soft cap** (diminishing returns approaching the cap). Rejected at this
  stage: it needs a curvature parameter the GDD does not state, and there is no played
  data to fit one to. A linear rule with two authored numbers keeps 100 % of the rule in
  `data/`, which is what rule 5 asks for.
- **A per-weight breakpoint table in `data/`** (e.g. weight → penalty rows). Rejected as
  premature: it adds a table and a validator rule for a curve with one segment, and it
  cannot be authored meaningfully before the numbers are tuned (Stage 11).
- **The battle computes it from a weight it is handed.** Rejected: it would put bag maths
  in the battle, and the battle would then need `IGameConfig`; the whole reason the
  loadout snapshot exists is that the battle reads exactly one resolved object.
- **A `Math.Round` instead of `floor`.** Rejected: it would make a 0.1-weight nudge
  worth a full percent sooner than the author expected from a “percent per unit” slope.

## Consequences

- **A weight change in `data/` changes movement** with no code change, and the cap is
  enforceable by the validator (slope ≥ 0; cap within 0–100; a non-zero slope means a
  heavy build is always slower). The validator gained those rules in the same stage.
- **Tier does not change weight.** `item`/`bag` carry one weight per piece, so merging to
  tier 3 makes a piece stronger without making it heavier. That is a data-model gap, not
  an intentional rule; if merging should cost mobility, a per-tier weight column has to
  land in `data/` first.
- **A future non-linear curve is an ADR amendment plus data columns**, not a code
  rewrite: the seam is the single `WeightPenalty.Percent(weight, constant)` call.
- **`PlayerStatBlock` gained a field** (`SpeedPenaltyPercent`) so the number is
  observable in tests and in a HUD without re-deriving it.
