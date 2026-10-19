# ADR 0017 — The EcsLite battle simulation, and the numbers `data/` does not have yet

- **Status:** Accepted (Stage 07)
- **Date:** 2026-10-15
- **Related:** [ADR 0007](0007-battle-contract-and-asmdef-placement.md) (the battle seam),
  [ADR 0012](0012-prev-cur-frame-double-buffer.md) (the frame is a Prev/Cur double buffer with one
  implementation), [ADR 0010](0010-item-local-effects-deferred.md) (item-local effects),
  [ADR 0016](0016-run-settlement-and-shop-commit-boundaries.md) (the run that drives a floor),
  AGENTS rules 5 (every balance number lives in `data/`), 6 (fixed tick, one RNG stream, determinism
  scope), 8 (no allocation on a hot path) and 12 (ambiguity is settled simply and written down)

## Context

Stage 07 landed the real battle: an EcsLite world inside `lib/Runtime/Simulation`, driven one fixed
30 Hz tick at a time, behind the `IBattleSession` / `IBattleSessionFactory` contract that Stage 02b
fixed and the fake has implemented since. The contract, the components and the system order are the
prompt's; what this ADR records are the choices the prompt left open, plus the places where the
prototype asks for a number that `data/` does not carry.

1. **Tick, backlog and pause.** A browser tab can stall for seconds, and WebGL is single-threaded.
   The simulation can try to catch up, or drop what it owes.
2. **Def ids in events.** `BattleEvent.DefId` is an `int` (so the event ring never allocates and
   presentation never touches config), but `data/` names everything with strings. Stage 02b left the
   mapping open.
3. **Auto-cast.** The GDD says the one class casts by itself and does not describe ability slots,
   priority lists or a target-selection UI. Several weapons can be ready in the same tick.
4. **Elemental status.** Three skills carry an element (`skill.fire_bolt` Fire, `skill.curse_wave`
   Curse, `skill.thread_pierce` None). The GDD describes elemental status; no table describes its
   magnitude, cadence or duration.
5. **Drop value.** A ghost dies, a pickup drops, the pickup is collected — and `BattleResult`
   reports `CurrencyEarned`. No column in `data/` says what a kill pays: `economy` holds the camp's
   prices, `enemy` holds stats, `shop_pool` holds offers.
6. **Magnet speed.** `constant.pickup_radius` exists; how fast a drop is pulled in does not.
   *(Filled by [ADR 0027](0027-end-of-floor-sweep.md) in Stage 07c: the columns are
   `constant.magnet_speed` and `constant.sweep_timeout_seconds`, and they drive the end-of-floor
   sweep. During the fight itself a drop is still collected instantaneously inside the radius.)*
7. **What the contract suite needs.** The shared `BattleSessionContractTests<TFactory>` drives the
   player with `PlayerInputFrame.Idle` (it never moves) and expects a battle that generates more
   events than the 4096-slot ring can hold.

## Decision

**1. The battle owns no clock. A tick happens when `Step` is called, and surplus time is dropped,
not replayed.**

There is no `Time`, no `DateTime`, no internal accumulator in the simulation — an architecture test
forbids every one of them in the rule layers. Consequences, stated as the policy Stage 08 has to
implement:

- **Paused is "not stepped".** A tab that is not visible simply stops calling `Step`; the battle
  freezes mid-tick and nothing accumulates. There is no resume calculation, because there is no
  elapsed-time state to reconcile.
- **Backlog is bounded and counted.** The Present driver feeds real frame deltas to
  `FixedTicker` (Stage 01), which hands out at most `BattleConstants.MaxStepsPerFrame` (4) steps per
  frame and **discards** the surplus, counting it in `FixedTicker.DroppedTicks`. Replaying a
  ten-second stall as 300 steps would be a death sentence in a game about not being touched, and
  would spiral into ever-longer frames on a machine that cannot keep up.
- **The simulation is the same either way.** A dropped tick is not simulated at all, so a replay
  reproduces the battle it recorded (same seed, same ticks, same inputs) — it does not reproduce the
  wall-clock history of the machine that recorded it.

**2. A def id is the row's 1-based position in its table, built once per factory.**

`BattleDefIds` maps enemy / skill / item string ids to the order `IGameConfig` exposes them (the file
order of `data/tables/*.csv`); `0` means "no def". The map is derived at factory construction and is
never persisted, so inserting a row anywhere but the end of a table renumbers ids — acceptable
because an id lives only inside one battle's event stream (a save names string ids). An id that is
not in the table resolves to `0`, never to a guess.

**3. Auto-cast is "ready, affordable, shortest base cooldown, nearest ghost".**

One cast per tick at most, which is what makes the MP cost and the cooldown the two real limits on a
build. Among the casters whose clock has run out and whose `MpCost` the pool can pay, the one with
the shortest base cooldown fires; a tie keeps the earliest loadout entry, so the choice is
deterministic. Cooldowns run down while there is nothing to hit, so casts-per-floor can only ever be
lower than `floor(duration / cooldown)`. There is no priority list or ability slot because the GDD
does not describe one, and inventing a build-order system here would be a design decision smuggled in
as an implementation detail.

**4. Elemental status re-uses the applying skill's own damage, on a one-second cadence, until a
`status` table exists.**

An element-carrying hit leaves a status on the ghost: the element decides which effect, the damage
re-applied is the same damage the hit dealt (`BattleConstants.TickRate` ticks apart), and the effect
expires after one second. A second application refreshes rather than stacks, and keeps the stronger
magnitude, so the effect stays bounded and the state hash stays small.

This is **not** a balance claim — it is the only data-derived magnitude available today. The
replacement is a `status` table (id, element, damage per tick, tick interval, duration, max stacks)
in `data/`, which Stage 11 owns along with its balance pass; the component already carries the def id
that would become the status row's own id (`BattleComponents.Status.DefId`).

**5. A kill pays nothing today, and says so, because no column says what it pays.**

> **Superseded by [ADR 0025](0025-bia-from-battle.md) (Stage 07b).** The column named below,
> `enemy.drop_value`, now exists, and `economy.clear_reward_bia` pays a cleared floor. This
> paragraph is kept as the Stage 07 record of the gap.

The pipeline is real end to end — a death queues a drop, `DropSystem` spawns the pickup at the spot,
`PickupSystem` collects it inside `constant.pickup_radius` and raises `PickupCollected` — but the
pickup's value is `0`, so `BattleResult.CurrencyEarned` stays `0`, exactly as the Stage 02b fake
reported. The column Stage 11 has to add is **`enemy.drop_value`** (per enemy row, so a boss and a
filler ghost can pay differently); an alternative, a per-floor clear reward, is the run's business
and would go in `economy`.

The same reasoning covers two smaller gaps: a drop is collected **instantaneously** inside
`pickup_radius` rather than drifting toward the player, because a drifting magnet needs a speed and
there is no column for one (Stage 07c added `constant.magnet_speed` for the end-of-floor sweep —
[ADR 0027](0027-end-of-floor-sweep.md) — but the radius is still the rule during the fight), and
item-local effects are still unmodelled ([ADR 0010](0010-item-local-effects-deferred.md)),
so `RemainingConsumables` always reports "nothing was used".

**6. The arena's shape and memory bounds are structural, and live in code.**

`BattleArena` holds the disc radius, body radii, the spatial-hash cell size, the entity/projectile
/pickup caps and the ECS capacities. None of it is balance: the values are memory bounds and
geometry a simulation needs in order to exist, and the pattern is the one Stage 02b established for
the fake's orbit constants. The largest number a player could feel — the arena radius — is
explicitly Stage 11's to move into `data/` if the balance pass wants it.

**7. The contract suite is satisfied by supplying what it assumes, not by relaxing it.**

The shared suite is the gate, and the EcsLite session passes every case of it unchanged. Two of its
premises are supplied by the derived test class (`EcsBattleSessionContractTests`), both through the
load generator that exists for benchmarks:

- an idle player must survive a floor to its timer, and the real floors are tuned for a *kiting*
  player, so the contract loadout hands in a large HP pool;
- the event-overflow case needs a battle that generates more events than a 4096-slot ring holds, and
  a boss floor with nothing to kill never does, so the contract factory turns on a small churning
  load (`LoadTestEntityLifetimeTicks`, the real simulation's mirror of the fake's
  `EntityLifetimeTicks`).

Neither knob touches a balance number, and the battle's own balance is measured by the bot harness
(`tools/sim.ps1`), not by the contract suite.

## Alternatives considered

- **Catch up on the backlog.** Rejected: a stall would replay as a burst of ticks with the player
  unable to react, and on a machine that cannot keep up the backlog would grow every frame.
- **Pause the simulation when `Time.deltaTime` is zero.** Rejected: it would put a clock inside the
  rule engine, which AGENTS rule 6 forbids, and it would make the "paused" case untestable headless.
- **Pass string ids through events.** Rejected: a string in an event means an allocation per hit and
  a config lookup in presentation, both forbidden on this path.
- **Hash the def ids instead of numbering them.** Rejected: a hash is not orderable or debuggable,
  and an insertion would silently change the meaning of every stored event.
- **Invent a damage-per-second for each element.** Rejected: AGENTS rule 5. The applying skill's
  number is at least a number a designer wrote.
- **Pay a fixed sum per kill from code.** Rejected for the same reason. A number that a balance pass
  must move has to be a column.
- **Let the boss floor's single ghost satisfy the event-overflow case by dropping the assertion.**
  Rejected: the prompt forbids weakening the shared suite, and the honest move is to supply the
  missing premise.
- **Unify the arena constants into `constant`.** Rejected as premature: the columns would have no
  consumer beyond this stage, and Stage 11 owns the arena size.

## Consequences

- `lib/Runtime/Simulation` is the only place EcsLite types may appear, enforced by an architecture
  test that scans both the `lib/` sources and the Unity scripts. Application and Domain reach a
  battle through `IBattleSession` / `IBattleSessionFactory`, and Presentation reads `IBattleFrame`.
- `BattleFrameBuffer` and `BattleEventRing` are shared with the fake — the same instances, the same
  classes — so a Presentation written against the fake sees identical frame and event behaviour from
  the real battle (the `IBattleFrame` single-implementation test is what keeps it that way).
- A future `status` table is an additive data change: the components already carry the def ids the
  events would report, and the systems read them through `IGameConfig` like every other number.
  `enemy.drop_value` landed that way in Stage 07b ([ADR 0025](0025-bia-from-battle.md)) and
  `constant.magnet_speed` in Stage 07c ([ADR 0027](0027-end-of-floor-sweep.md)).
- The balance picture Stage 07's bot harness produces (200 seeds: floor 1 200/200, floor 2 198/200,
  floor 3 91/198, floor 4 71/91, floor 5 71/71) is a *symptom* of these gaps as much as of the
  floor tables — at the time a run earned no bia in a battle, so the camp could only spend
  `economy.start_bia` (Stage 07b closed that half; [ADR 0025](0025-bia-from-battle.md)). That is the
  input Stage 11 needs, and it is recorded in `docs/stages/07-report.md`.
