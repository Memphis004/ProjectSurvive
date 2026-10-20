# ADR 0030 — A reversed control is published on `HudState`, not reconstructed from events

- **Status:** Accepted (Stage 11c)
- **Date:** 2026-10-21
- **Related:** [ADR 0017](0017-ecslite-battle-simulation.md) (the ECS battle and its system order),
  [ADR 0029](0029-enemy-telegraphed-attacks.md) (the telegraph payload the possession reuses),
  [`llmwiki/systems/battle-contract.md`](../../llmwiki/systems/battle-contract.md) (the event
  catalogue and the HUD contract).

## Context

Pob's possession reverses the player's movement, and Stage 11c's rule is that the reversal lives in
Simulation while the HUD still has to tell the player it is happening — the view must not infer it.
Three ways of publishing it existed:

1. **A new event pair** — `PossessionStarted` / `PossessionEnded` — appended to `BattleEventType`.
2. **A flag on the render entity** — a `Possessed` bit in `RenderEntityFlags`, next to `Boss`/`Elite`.
3. **A field on `HudState`** — the frame the HUD already reads every tick.

The event contract also had to carry two more facts of the same feature: the phase change and the
corpse meal. `CorpseConsumed` (13) and `BossPhaseChanged` (14) were **already reserved** in the enum
by Stage 11b, so those two needed no contract change at all.

## Decision

### 1. The reversal is player state, counted in Simulation

`BossSystem` writes `Possessed.RemainingTicks` on the player entity the tick a warned possession
lands, and counts it down every tick. `PlayerMoveSystem` negates the movement axis while the clock
has ticks. The system runs **before** the movement system, so the tick that lands a reversal is a
flipped tick and the tick that ends it is already unflipped. Killing the boss clears the clock
(`DeathSystem`): the reversal belongs to the ghost that cast it and does not outlive it.

### 2. The HUD learns it from `HudState.ControlFlipTicks`

`FramePublishSystem` copies the player's remaining ticks into `HudState.ControlFlipTicks` on the same
frame it publishes HP and currency. `0` means normal controls; a positive number is the ticks left,
which a presenter divides by `BattleConstants.TickRate` for its seconds label.

- **Rejected: an event pair.** A view that hears "started, for 3 s" and counts frames itself owns a
  second clock. In a fixed-tick simulation that clock drifts the moment a frame is dropped or the
  game is paused, and the HUD can then say "reversed" while the stick is not — the exact failure the
  rule exists to prevent. Events also do not survive a mid-floor join (a fresh frame has no history),
  while a published field is true whenever it is read.
- **Rejected: a `RenderEntityFlags` bit.** Flags are per entity and live in the render snapshot, but
  the possession is one number about the *player*, and the HUD draws it in a text line, not by
  tinting a body. A bit would also lose the remaining time, which the label shows.
- `HudState` already documents that it has no level/XP field; the comment was updated to say the one
  input state that *is* published, so the next reader does not "restore" the bit-only design.

### 3. The warning is the existing telegraph, with the boss tag

The possession warns through `TelegraphStarted` / `TelegraphEnded` with `TelegraphShape.Circle`,
`Radius` = `boss_possess_range` and `BattleEventFlags.Boss`. No new shape, no new payload: "something
is about to happen here" is one fact with one footprint (ADR 0029), and a player who steps out of the
circle before the warning ends is not possessed.

### 4. The numbers are columns on the `enemy` row

`boss_phase2_hp_percent`, `boss_corpse_range`, `boss_corpse_heal`, `boss_possess_telegraph_seconds`,
`boss_possess_seconds`, `boss_possess_interval_seconds`, `boss_possess_range`. `BossRules.FromConfig`
resolves them to an absolute HP threshold and tick counts **once at spawn**, so nothing on the hot
path converts a unit, and two runs of one seed cross the threshold on the same tick. A zero column
means the boss does not do that thing; a boss with neither corpse eating nor possession is still a
boss.

## Consequences

- The HUD contract gained one `int` (`HudState`, a value type handed out by copy — no allocation).
  `BattleEventType` was not touched: the two facts it needed were already numbered.
- The contract test asserts that the published clock and the movement rule agree; the Core tests
  assert the phase threshold, the flip-and-release timing, the heal cap and a replay hash.
- Presentation never compares a time to decide whether the controls are reversed; it draws
  `ControlFlipTicks > 0`.
- Not decided here: whether a future boss debuff also deserves a HUD line (it would need the same
  "publish the clock, not the event" treatment), and phase-2 behaviour beyond the possession
  (Stage 11d's balance pass).
