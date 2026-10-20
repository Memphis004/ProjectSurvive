# ADR 0029 — Telegraphed ghost attacks: data on the `enemy` row, one warning payload on the event

- **Status:** Accepted (Stage 11b)
- **Date:** 2026-10-20
- **Related:** [ADR 0017](0017-ecslite-battle-simulation.md) (the ECS battle and its system order),
  [ADR 0012](0012-prev-cur-frame-double-buffer.md) (what the frame is for — telegraphs are *not* on it),
  [`llmwiki/systems/battle-contract.md`](../../llmwiki/systems/battle-contract.md) (the event catalogue).

## Context

Krasue (dash → drain → retreat) and Nang Tani (ring → vines) need behaviour a single "walk at the
player" rule cannot express, and the GDD's rule is that **every strong attack is telegraphed**. Three
questions had to be answered:

1. Where do an attack's numbers live? AGENTS rule 5 forbids balance numbers in code.
2. How does the warning reach the floor? The event payload was `DefId` + `Value` + `X`/`Y`, and the
   contract's table said a telegraph's "shape/time come from the skill def" — which no longer exists
   for a ghost attack.
3. How much state machine does each kind get, and how does steering cooperate?

## Decision

### 1. The numbers are columns on the `enemy` row

```
attack_pattern                        None | DashDrain | RingVines
attack_range                          start the cycle when the player is this close
attack_telegraph_seconds              warning time
attack_seconds                        strike window (a dash's travel); 0 = instant
attack_damage, attack_radius, attack_length
follow_up_telegraph_seconds           the cycle's second attack; 0 = none
follow_up_seconds, follow_up_damage, follow_up_radius, follow_up_length,
follow_up_tick_seconds
retreat_seconds, retreat_speed
```

- **A shape is decided by `length`, not by another column**: `length = 0` is a circle centred on the
  ghost with `radius`; a positive length is a line from the ghost along its aim with `radius` as the
  half-width. The event publishes the resolved shape, so the decal and the damage read the same
  numbers.
- Zero means "this stage does not happen", which is how one component shape describes both ghosts.
  `attack_cooldown_seconds` — already in the row — is reused as the **cycle** cooldown; a ghost that
  attacks on a rhythm has one rhythm, and a second column would let the two disagree.
- Rejected: a separate `enemy_attack` table. It would be one row per attack (more rows to author for
  four ghosts) and would put the ghost's own numbers in a second place; the row is already the
  ghost's stat block. If a future ghost needs three attacks, that table is the migration and this ADR
  is the reason to revisit, not a reason to have started there.

### 2. One FSM, two behaviours

`EnemyAttackSystem` runs the same stages for both ghosts — `Chase → Telegraph → Strike →
FollowUpTelegraph → FollowUp → Retreat → Recover` — and skips the ones whose tick counts are zero.
Krasue fills `Strike` with a dash and `FollowUp` with a drain; Nang Tani leaves the windows empty and
resolves a ring and a vine lash at the end of each warning. A separate machine per kind would be a
second place to get "warn before you hit" wrong.

`EnemySteerSystem` moves a ghost **only** while its brain is `Chase`; every other stage writes the
position itself (the dash, the retreat, or standing still). Two systems writing one position is how a
warning ends up somewhere other than the attack.

The brain (`State`, `StateTicks`, `DamageTicks`, locked `AimX/AimY`) is hashed by `ComputeStateHash`:
where a ghost is in its cycle is a fact of the fight, not a presentation detail.

### 3. The warning is an event with a footprint

`TelegraphStarted` / `TelegraphEnded` keep their numbers (11 and 12) and gain a payload:

| Field | Meaning |
| --- | --- |
| `DefId` | the **enemy** def that is attacking (the contract used to say "skill def") |
| `Value` | `(int)TelegraphShape` — `Circle = 0`, `Line = 1` |
| `X`, `Y` | origin: a circle's centre, a line's start |
| `Radius` | circle radius, or the line's half-width (new field) |
| `Length` | line length, `0` for a circle (new field) |
| `Angle` | line direction in radians, `0` for a circle (new field) |

- The three new fields are **additive** on `BattleEvent`; every existing event leaves them at zero.
  They are not a second event type, because a telegraph is one fact with more numbers, and appending
  to the enum would have made a reader handle two events for the same warning.
- `TelegraphStarted` is emitted in the tick a warning begins and `TelegraphEnded` in the tick its
  stage ends — the same tick the damage lands. A decal can therefore never outlive its hazard.
- Presentation draws what the payload says (`TelegraphPool`), never re-deriving a footprint from a
  def it cannot see: a warning that draws smaller than the damage is worse than no warning.

### 4. Strong damage is the player's queue, not a new path

A resolved attack adds to `BattleContext.PlayerHitDamage`, the queue `DamageStatusSystem` already
applies (god mode included). One application per warning; a drain window applies its damage on the
row's cadence.

## Consequences

- A new telegraphed ghost is data plus (at most) a branch in the FSM; nothing in Presentation
  changes.
- The contract's event table changed for `TelegraphStarted`/`TelegraphEnded`; the contract test
  asserts the payload of both factories and the Core tests assert order, timing and the footprint.
- `BattleEvent` grew by 12 bytes (three floats); the ring's capacity is unchanged.
- Not decided here: boss phases, corpse eating and multi-phase patterns (Stage 11c/d). Those may
  append event types — appending is allowed, renumbering is not.
