# ADR 0025 — Bia from battle: a per-enemy drop value and a flat floor-clear reward

- **Status:** Accepted (Stage 07b). Supersedes **§5 (a kill pays nothing today)** of
  [ADR 0017](0017-ecslite-battle-simulation.md).
- **Date:** 2026-10-19
- **Related:** ADR 0017 (the battle that left the gap), [ADR 0016](0016-run-settlement-and-shop-commit-boundaries.md)
  (one settlement per floor), AGENTS rules 5 (numbers live in `data/`), 7 (atomic commands and
  typed results) and 12 (settle ambiguity the simplest way and write it down)

## Context

ADR 0017 shipped the whole drop pipeline — death → queued drop → pickup → `PickupCollected` —
but left the value at `0`, because no `data/` column said what a kill pays. The GDD says bia comes
from two places, *"เก็บในสนามและรางวัลจบชั้น"*, and that both numbers live in `data/`. Without
them a run could only ever spend `economy.start_bia` (15), so no build could grow after the first
camp and the Stage 07 balance run was a reading of a broken economy.

The prompt for Stage 07b therefore asks for **both** sources, with the smallest shape that lets a
designer move each independently, and for validation that catches a broken row.

## Decision

**1. `enemy.drop_value` (int, per row) is what one kill of that ghost pays.**

The value is copied onto the spawned entity (`EnemyTag.DropValue`, and `WaveSlot` for the
pre-resolved wave) at battle start, exactly like the other per-enemy stats, so the death and drop
systems never look a def up on a tick. `DropSystem` spawns the pickup with that value, and
`PickupSystem` adds it to `BattleContext.CurrencyEarned` in the tick it collects it and deletes
the entity in that same tick, which is what makes the payment **once per pickup**. Collecting
inside `constant.pickup_radius` is unchanged; so is every other battle rule. `BattleResult.CurrencyEarned`
is now the sum of the collected drops, and nothing else.

**2. `economy.clear_reward_bia` (int, one row) is what clearing a floor pays.**

It is paid by `RunSession.ApplyBattleResult` **only when `BattleResult.IsWin`**, in the same
settlement that credits the field takings. The once-per-floor guard is unchanged: the phase plus
the pending setup admit exactly one settlement, so a second `ApplyBattleResult` is still
`run.not_in_battle` and pays nothing. A defeat credits the collected drops but not this reward.
`FloorSettlement.RewardBia` reports the sum of the two.

**3. Why two columns rather than one.**

The two numbers have different owners. A pickup is a fact of the battle: a boss and a filler ghost
can pay differently, and the number has to be on the enemy row to express that. The clear reward is
a fact of the run: it is paid after the floor is over, it must not be paid on a loss, and it has
nothing to do with how many ghosts were touched. A single global "kill pays N" constant would make
a boss worth the same as the cheapest ghost; a per-floor reward alone would make killing
meaningless. Both alternatives were rejected for those reasons, not for code shape.

**4. First values, to be tuned by the Stage 11 sim.**

| Enemy | `drop_value` |
| --- | --- |
| `enemy.wandering_spirit` (filler) | 1 |
| `enemy.krasue` (fast) | 1 |
| `enemy.nang_tani` (elite) | 3 |
| `enemy.pob` (boss) | 10 |

`economy.clear_reward_bia` is **8**. Floor 1 kills roughly 16 wandering spirits in the bot run,
but only about 4 of those drops are actually walked over (the bot kites, and a kill usually happens
outside the 1.5-unit pickup radius), so realized income is about `4 × 1 + 8 = 12` bia — one or two
shop pieces at the shipped 8–11 bia prices. A player who collects every drop would earn about
`16 × 1 + 8 = 24` (two to three pieces), which is exactly the kind of gap Stage 11 measures and
tunes. These are deliberately round starting values, not a balance claim.

**5. Validation.**

`drop_value` and `clear_reward_bia` must be `>= 0` (`enemy.drop_value_negative`,
`economy.clear_reward_negative`); `0` is legal and means "this ghost pays nothing". Every enemy row
in the shipped table names a value. A broken row is a fixture (`enemy-drop-value-negative`) as well
as a unit case, and the schema revision moves to 4 because two fields were added.

## Alternatives considered

- **A single global kill value in `constant`.** Rejected: it cannot express a boss or an elite paying
  more, and the GDD distinguishes ghosts.
- **A per-floor reward only (no pickups).** Rejected: the GDD explicitly says field pickups are a
  source, and it would remove the reason to walk over a drop.
- **A percentage of the shop price.** Rejected: no data supports the percentage, and it ties the
  battle's income to an unrelated table.
- **Paying on the death event inside the battle.** Rejected: the GDD says bia is *collected*, and the
  existing drop/pickup pipeline already represents that; paying on death would make the pickup
  cosmetic and skip the once-per-pickup guarantee.
- **Letting the battle pay the clear reward.** Rejected: the battle does not know whether the run
  continues, and a loss must not pay it. The run already owns the settlement boundary (ADR 0016).

## Consequences

- A run's purse grows in battle and at settlement; the Stage 07b sim measures the new win rates per
  floor and records them in `docs/stages/07b-report.md`. Those numbers are inputs to Stage 11, not a
  claim that the economy is tuned.
- The HUD's top-right bia is `startingBia + HudState.Currency`, i.e. the purse the player walked in
  with plus the drops collected so far. The clear reward is not part of it because it is paid after
  the floor; it shows up in the camp purse and the summary screen.
- `elite` and `boss` can pay differently without a new table or a new component: the value travels
  on the entity that already carries the def id.
- The remaining ADR 0017 gaps are untouched: elemental status still re-uses the applying skill's
  damage, and the magnet is still instantaneous. `constant.magnet_speed` and a `status` table remain
  Stage 11's.
