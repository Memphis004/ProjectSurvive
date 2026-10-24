# ADR 0036 — กระหัง's gale throws the player: a `follow_up_push` column, not a new pattern

- **Status:** Accepted (Stage 16)
- **Date:** 2026-10-23
- **Related:** [ADR 0029](0029-enemy-telegraphed-attacks.md) (the telegraphed-attack machine this
  reuses), [ADR 0017](0017-ecslite-battle-simulation.md) (the simulation and its system order),
  [`llmwiki/systems/battle-simulation.md`](../../llmwiki/systems/battle-simulation.md),
  [`llmwiki/systems/data-schema.md`](../../llmwiki/systems/data-schema.md).

## Context

Stage 16 is the first **post-prototype content** stage: the prototype's set (3 ghosts + Pob, five
floors) is complete and pinned, and the brief was one new enemy. กระหัง is a night flyer, so the
obvious kit is a dive and a downdraft — which is exactly the shape the Stage 11b machine already
runs (`Telegraph → Strike → FollowUpTelegraph → FollowUp → Recover`, driven entirely by columns).

Three ways to give it a place in the game existed:

1. **A fifth stat block only.** Author the row on the existing columns (a long dash, a gale ring as
   the follow-up) and add nothing to the code. Cheapest, and the machine is parameterised precisely
   so a new ghost needs no system. But a burst ring is just a weaker Nang Tani lash: the ghost
   would be a reskin whose only contribution is another health bar.
2. **A new `EnemyAttackPattern` value** (`DiveGale`) with the same machine behind it. It would give
   the row a truthful name, but it would also make two enum values mean one machine, which the enum's
   own contract forbids ("the enum names the *shape of the state machine*").
3. **One new mechanic on the existing machine**: the second attack **throws** a player it hits, so
   Krahang's threat is positional — it can shove you into the ghosts behind you — and no other ghost
   in the game moves the player.

Option 3 needed one number, not a system, and it is the only one of the three that a playtest can
feel. It also had a naming cost: the dash pattern was called `DashDrain`, and Krahang is its second
user with no drain.

## Decision

### 1. `follow_up_push` is one column on `enemy`

`data/tables/enemy.csv` gains `follow_up_push` (world units, `0` = no push), read into
`EnemyConfig.FollowUpPush → EnemyAttack.FollowUpPush` at spawn like every other attack number. The
push belongs to the **second attack** because that is the hit whose footprint is a burst at the
landing point: the dive is the delivery, the gale is the shove.

### 2. The push happens in the tick the gale lands, in Simulation only

`EnemyAttackSystem.ResolveFollowUp` applies the follow-up's damage and then `PushAway` moves the
player `follow_up_push` units along the vector **from the ghost to the player** — i.e. along the dive
rather than back down it — and clamps the result to the arena with the player's body radius. A player
standing exactly on the ghost is pushed along the ghost's locked aim, because a zero vector has no
direction.

- The hit test keeps using the player's position **from the start of the tick**, like every other
  resolve in the system. A push therefore moves the player for the next tick's tests, not for another
  ghost's warning in the same tick. That is a rule, not an accident: it keeps "warnings are resolved
  against where everyone stood" true no matter how many Krahang are on the floor.
- No event, no new component, no frame field. A pushed player is visible as a moving entity the
  Presentation already draws, and the HUD already reads position from the frame.
- No random, no allocation: the push is arithmetic on two positions.

### 3. The dash pattern is renamed `DashDrain` → `DashFollowUp`

Krahang is the pattern's second user and does not drain, so the old name would have become a lie in
`data/` (the numeric value stays `1`). The enum's contract is about the shape — dash, then a
follow-up at the landing point — and the payload is the row's. ADR 0029 keeps its own history and
names the old value; this ADR is the rename's record.

### 4. The validator states the rule

`tools/validate-data.ps1` gained two codes and one amendment:

| Code | Rule |
| --- | --- |
| `enemy.push_negative` | a push moves the player *away* from the hit, so the distance cannot be negative |
| `enemy.push_without_follow_up` | a push belongs to the second attack: the row must also warn (`follow_up_telegraph_seconds > 0`) and deal damage (`follow_up_damage > 0`) |
| `enemy.attack_on_contact_only_ghost` (amended) | a `None` row must leave `follow_up_push` at zero too |

Broken-data fixtures `enemy-push-negative` and `enemy-push-without-follow-up` prove each new code is
reached through `data/ → Luban → .bytes → findings`, not only by a hand-built object.

## Consequences

- **Any future ghost can shove the player by writing one column.** That is deliberate: a second
  positional ghost is then content, not code. A push on a *ticking* follow-up window applies once per
  damage tick, which is why the shipped gale is a single burst (`follow_up_seconds = 0`); a stage
  that wants repeated shoves should say so and measure it.
- **No save migration.** `follow_up_push` is not part of `RunCheckpoint`, and the checkpoint schema
  version does not move. But the generated config hash does, and `CheckpointService` refuses a
  checkpoint whose `ConfigHash` differs (`ConfigMismatch`) — so a run saved by an older build does
  not resume, by the rule Stage 12 already set for any `data/` edit. A new run is unaffected.
- **Presentation does not change.** The battle draws a ghost from its def/flags and the player from
  the frame; a shove is a moved entity. No scene, prefab or art work was needed, which is why the
  stage's browser gate is **NOT RUN** rather than deferred work.
- **The balance is measured, not asserted.** 200 seeds × 4 bot builds before and after; the numbers
  and the rounds that got there are in [`docs/stages/16-report.md`](../stages/16-report.md).

## Alternatives considered and rejected

- **A data-only fifth ghost** (option 1). Rejected because the stage's purpose is content that
  changes play; a stat block that reuses another ghost's payload verbatim adds nothing a player can
  do differently.
- **A `DiveGale` pattern value** (option 2). Rejected because it would give one state machine two
  names, and the enum's contract is that the value names the machine.
- **An event-driven knockback in Presentation.** Rejected outright: a view that moves the player is a
  rule in the wrong layer, and the battle frame would then disagree with the simulation.
