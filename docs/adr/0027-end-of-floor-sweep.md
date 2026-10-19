# ADR 0027 — An end-of-floor sweep collects the drops still on the ground

- **Status:** Accepted (Stage 07c). Fills the `constant.magnet_speed` gap named by
  [ADR 0017](0017-ecslite-battle-simulation.md) §6, and supersedes its sentence that a drop is
  only ever collected instantaneously inside `pickup_radius`.
- **Date:** 2026-10-19
- **Related:** ADR 0017 (the simulation), [ADR 0025](0025-bia-from-battle.md) (what a drop pays),
  AGENTS rules 5 (numbers live in `data/`), 6 (determinism), 8 (no hot-path allocation) and 12
  (pick the simplest option and write it down)

## Context

A floor ends the moment its win condition is met — the normal floor's timer, or the boss dying. Every
drop still lying on the ground at that instant was lost: the player had killed the ghost and earned the
pickup, but the fade started before it could be walked over. The prompt asks for those drops to be
pulled into the player and counted in `CurrencyEarned` before the floor result is produced.

The rule decides what a floor pays, so it cannot live in a view: a view-driven magnet would make the
result depend on how many frames were drawn, and the same recording would pay differently. It belongs
in the simulation, where the seed and the inputs are the only things that decide it.

## Decision

**1. A win opens a sweep instead of ending the floor.**

`FloorEndSystem` still chooses the win and the reason, but it now sets `BattleContext.SweepActive`
instead of ending. The contract's `BattlePhase` stays `Running` for the whole sweep, so no interface,
enum or event changes: Presentation sees a running floor whose drops are moving, exactly the frame
shape it already draws, and its director leaves when the phase changes (after the sweep).

**2. The sweep is a tick with two systems, and nothing else.**

`EcsBattleSimulation.Step` branches: while `SweepActive`, the tick runs `SweepSystem` and
`FramePublishSystem` only. No wave spawns, no ghost steers, no weapon casts, nothing dies, no contact
damage lands and no projectile moves. That one branch is what makes the rest of the promise
mechanical rather than a set of exceptions scattered through fourteen systems.

**3. A drop drifts at `constant.magnet_speed` and is collected when it arrives.**

`constant.magnet_speed` (world units per second) and `constant.sweep_timeout_seconds` (the cap) are new
columns in the `constant` table, so the speed and the wait are balance numbers and not code. A pickup
moves `magnet_speed × 1/30` toward the player each tick and is paid in the tick it arrives; the
collection path is the same as `PickupSystem`'s — one `PickupCollected` event, one addition to
`CurrencyEarned`, one increment of `PickupsCollected`, and the entity deleted in that tick, so a
pickup can never pay twice.

**4. The ghosts that are left stand still.**

The alternative — deleting them — would have to decide whether a deleted ghost counts as a kill, pays
a drop, or is announced. Freezing keeps every existing rule intact: a ghost is only ever paid by
`DeathSystem`, and the enemies still standing when the result is produced simply never died. The
player is safe during the sweep because no damage system runs, which is what the prompt asks for.

**5. The sweep always ends: at the cap, everything left is collected at once.**

`SweepSystem` collects every remaining pickup when `SweepTicksRemaining` reaches zero, wherever it is,
and sets `EndRequested`. A drop in an unreachable corner therefore costs at most
`sweep_timeout_seconds` of screen time, and the floor always produces its result. A sweep that starts
with no drops ends on its first tick.

**6. A loss never sweeps, and the dev skip is unchanged.**

A player death (or a boss floor timing out) ends the floor immediately: there is nothing to collect on
a floor that was not cleared. `DevWinNow` also still finishes immediately, because it stands for "skip
this floor", not for a won fight; the shared contract suite asserts that behaviour and is unchanged.

**7. No state-hash field was added.**

The sweep's effect is already in the hashed state (pickup positions, the collected counter and
`CurrencyEarned`), and the tick is hashed. Adding a new flag to `ComputeStateHash` would have moved the
pinned seeded hash without making a desync any more detectable, so the pin from Stage 08b still holds.

## Alternatives considered

- **Collect the drops from the view.** Rejected: the result would depend on rendered frames, and a
  replay could pay differently on a slower machine.
- **End the floor immediately and pay the drops at settlement.** Rejected: the run would have to know
  the drop positions or the battle would have to hand over a second result (`pending drops`), which is
  a wider contract change than the sweep itself; it would also skip the visible pull-in the prompt asks
  for.
- **Delete the ghosts so they stop filling the screen.** Rejected as a second change to kill/drop
  accounting for no rule the prompt needs; freezing is smaller and leaves the kill counter honest.
- **A sweep that ends only when the last drop arrives.** Rejected: a drop can be behind an obstacle or
  in a corner the player cannot reach, and the floor would never finish.
- **Put the speed in code because it is "only a magnet".** Rejected by AGENTS rule 5; the values are
  `magnet_speed = 24` and `sweep_timeout_seconds = 2`, both starting points for Stage 11.

## Consequences

- A cleared floor now pays every drop it produced. The 200-seed bot run in
  [`docs/stages/07c-report.md`](../stages/07c-report.md) records the effect (floor 1 collects the drops
  the kiting bot used to leave behind, which raises income and changes the camp policy's purchases);
  the numbers are input to Stage 11, not a tuned economy.
- `BattleResult.SurvivedTicks` includes the sweep ticks, so a floor reports 1800 + (up to 60) ticks.
  Nothing in the run reads it as the floor length; the HUD clamps its clock to `00:00` and its progress
  bar to 1 during the sweep because the existing presenter already clamps.
- The phase stays `Running`, so Presentation needs no new code: it keeps stepping, the pickup views
  move through the frame's interpolated positions, the HUD's bia climbs, and the scene fades when the
  result arrives. The EditMode presentation test measures `viewsGrownDuringPlay = 0` across the sweep.
- The schema revision moves to 5 (two `constant` columns). `SchemaVersion` in
  `ConfigBuildMetadata` and `ConfigLoadTests` follow it.
