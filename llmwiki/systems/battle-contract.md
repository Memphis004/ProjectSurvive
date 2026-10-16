# Battle contract v1

> Stage 02b. The seam between the battle and everything around it, defined
> **before** the EcsLite simulation exists (Stage 07) so Application, the fake and
> the contract tests can be written against a stable interface.

Source of truth for the rules: [`../../docs/gdd/prototype-v0.3.md`](../../docs/gdd/prototype-v0.3.md).
Decision record: [ADR 0007](../../docs/adr/0007-battle-contract-and-asmdef-placement.md).

---

## 1. Where it lives, and why there

| Namespace | Assembly | Contents |
| --- | --- | --- |
| `ProjectSurvive.Core.Domain.Combat.Battle` | `ProjectSurvive.Core.Domain` | every interface, struct and enum of the contract (`IBattleSession`, `IBattleFrame`, `IBattleEventSource`, `BattleEventRing`, `BattleEvent`, `BattleSetup`, `BattleResult`, `CombatLoadoutSnapshot`, …) |
| `ProjectSurvive.Core.Application.Battle.Fake` | `ProjectSurvive.Core.Application` | `FakeBattleSession` + `FakeBattleSessionFactory` (a deterministic stand-in) |

`Domain` is the only layer both **Application** (SceneFlow / Run calls the battle)
and **Simulation** (EcsLite implements it) may reference, so the contract lives
there. It is deliberately **not** in `Contracts` — that assembly is reserved for
Save / Protobuf DTOs (ADR 0001, ADR 0006) — and it references no EcsLite,
`UnityEngine`, `R3`, `VContainer` or serializer. `IRandomSource` was already in
`Shared` (Stage 01), which `Domain` may reference, so nothing had to move.

```
   Application ──► Domain.Combat.Battle ◄── Simulation
     (Fake, Run)        (the contract)        (EcsLite, Stage 07)
```

## 2. Lifecycle

```
   Create(setup) ──► Running ──Step(input)──► Running ──► Won | Lost | Aborted
                       │                                      ▲
                       └──────────── Submit(command) ─────────┘
```

- `BattlePhase.Running = 0`, `Won = 2`, `Lost = 3`, `Aborted = 4`. Value **1 is
  reserved** for a future `AwaitingChoice`; the prototype has no level-up,
  blessing or XP (GDD v0.3) and the contract must not grow one.
- `Step` advances exactly one 30 Hz tick. Once the phase is no longer `Running`,
  `Step` does nothing and the tick does **not** advance.
- `Submit` returns a typed `CommandResult` (`Ok`, `RejectedWrongPhase`,
  `RejectedInvalidArg`, `RejectedNotAllowed`); a rejected command changes nothing.
- `TryGetResult` is `false` while running and `true` once ended.

## 3. Win / loss conditions (read from `data/`, never hardcoded)

| Floor | Win | Loss |
| --- | --- | --- |
| Normal (60 s) | survive `floor.duration_seconds` → `SurvivedAllWaves` | player HP reaches 0 → `PlayerDied` |
| Boss (90 s) | boss dies → `BossDefeated` | timer runs out → `Timeout`; or `PlayerDied` |

Duration and `is_boss` come from the **floor** table through `IGameConfig`. HP is
carried across floors by the caller: `BattleSetup.CurrentHp` in,
`BattleResult.FinalHp` out. The 15% clear heal (`economy.heal_percent_on_clear`)
is Run's job, not the battle's.

## 4. Commands

| Kind | Arg | Effect |
| --- | --- | --- |
| `Surrender` | – | `Aborted` / `Surrendered`. **Not** a dev command — always allowed. |
| `DevKillAll` | – | kill every enemy (and the boss → win on a boss floor) |
| `DevGodMode` | 0 / 1 | invulnerability off / on. Any other arg → `RejectedInvalidArg` |
| `DevWinNow` | – | force a win |
| `DevLoseNow` | – | force a loss. **The HP is spent first**, so the result is the one the rules produce (`Lost` / `PlayerDied` / `FinalHp` 0) — the run settles it and the summary screen prints it, and a forced loss reported at full health is a defeat nobody could have reached |

Every command except `Surrender` is a dev command and is rejected with
`RejectedNotAllowed` when `BattleSetup.DevCheatsAllowed` is false.

## 5. Events

Events exist for **presentation only** — VFX, audio, floating numbers, the HUD.
**Rules must never read the event stream to decide an outcome**; they read
simulation state. A reader owns its own `cursor`; an event type's payload is
`DefId` (a def id, `-1` when unused) and `Value` (an event-specific integer).

| Event | `DefId` | `Value` | Notes |
| --- | --- | --- | --- |
| `WaveStarted` | – | wave index | once per wave |
| `EnemySpawned` | enemy def | – | |
| `EnemyKilled` | enemy def | kill count after this one | `Flags` may carry `Elite`/`Boss` |
| `ProjectileFired` | skill def | – | |
| `DamageDealt` | – | damage | `Flags` may carry `Critical` |
| `PlayerDamaged` | – | damage | remaining HP is in `HudState.Hp` |
| `PlayerHealed` | – | amount | |
| `StatusApplied` | status def | – | |
| `StatusExpired` | status def | – | |
| `PickupSpawned` | – | pickup kind | |
| `PickupCollected` | – | pickup kind | |
| `TelegraphStarted` | skill def | – | shape/time come from the skill def |
| `TelegraphEnded` | skill def | – | |
| `CorpseConsumed` | enemy def (the eater) | HP the eater regained | Pob (Stage 11) |
| `BossPhaseChanged` | – | new phase number | |
| `TimeWarning` | – | seconds remaining | |
| `BattleEnded` | – | `(int)BattleEndReason` | last event of a session |

`BattleEventRing` is the shared implementation: fixed capacity
(`BattleConstants.EventRingCapacity = 4096`), strictly increasing `Seq`, multiple
independent readers, **zero allocation** on write and read. A reader that falls
behind by more than the capacity loses the oldest events and is told how many
through `Read(..., out int dropped)` — the loss is reported, never hidden.

## 6. Frames

`IBattleFrame` exposes `Tick`, `Hud`, `Entities` (Cur), `PreviousEntities` (Prev)
and `Cooldowns` (Stage 08b). Both entity spans are `ReadOnlySpan<RenderEntity>` and
are **valid only until the next `Step` or `Submit`** — copy anything you need to keep.
`HudState` names its fields after the Luban columns (`hp`, `mp`, `currency`) and has
no Level/XP/input-inversion field.

### The cooldown list (Stage 08b)

`Cooldowns` is a `ReadOnlySpan<LoadoutCooldown>` in **loadout order**: weapons as
`CombatLoadoutSnapshot.Weapons` lists them, then familiars as `Summons` lists them.
Same lifetime as the entity spans. Each entry is
`{ InstanceId, RemainingTicks, PeriodTicks, Flags }`:

- `InstanceId` is the **board piece** the entry was resolved from
  (`WeaponLoadout.InstanceId` / `SummonLoadout.InstanceId`, set by `LoadoutResolver`
  from `ItemInstance.InstanceId`), or `LoadoutCooldown.NoInstance` (`0`) for a
  hand-built loadout. It is the key a HUD matches to the piece it drew.
- `RemainingTicks` is that caster's own clock (`Caster.CooldownTicks`), `PeriodTicks`
  its resolved cooldown (`Caster.PeriodTicks`, at least 1). **Ticks, not seconds** —
  the simulation's clock is `BattleConstants.TickRate`; a view divides for seconds or
  for a progress bar.
- `Flags` carries `ManaShort`: the cooldown has elapsed and the pool cannot pay the
  cast's cost (compared against `HudState.Mp`). It says nothing about whether a
  target is in reach or which weapon auto-cast would choose — those stay inside the
  simulation.
- A tick is atomic, so an entry that casts shows `1 → PeriodTicks` (the clock reached
  zero and the cast reset it in the same tick). `0` is what a **blocked** entry shows.
- Bounded by `BattleConstants.MaxLoadoutEntries = 64` (above the 9 × 7 board's cell
  count) and written into the unpublished slot, so a reader never sees half a list.

Why it is here and not in the View: a View has no resolved cooldown (tier and combo
modifiers are applied in `LoadoutResolver`), cannot know a floor is paused, and cannot
tell a refused cast from a cast that has not happened — see
[ADR 0023](../../docs/adr/0023-cooldown-state-on-the-battle-frame.md). The two
implementations publish it identically in shape: the EcsLite session reports its
casters' own components, the fake manufactures a predictable cycle for UI work.

Why a pair: the battle ticks at 30 Hz while rendering runs faster, so Stage 08's
`BattleSimulationRunner` interpolates `Prev` → `Cur` with its own alpha
(`accumulator / TickDelta`). Presentation reads only this port, so both ends of the
pair have to be on it.

`BattleFrameBuffer` is **the** implementation (ADR 0012) and provides both slots:

```
   BeginFrame(tick) -> SetHud(hud) -> Add(entity)… -> AddCooldown(entry)… -> Publish()
                                                                              │
                          swaps the two slots: written becomes Cur,
                          the old Cur becomes Prev with its count intact,
                          and the cooldown list swaps with them
```

- Two fixed slots of at most `BattleConstants.MaxRenderEntities` (1024); a frame
  that would exceed the cap stops at it, because the frame is a *view* and never the
  source of truth.
- `Prev` is the **immediately preceding** tick, not history, and is empty on a
  session's first frame. It is only meaningful *between* ticks — the next tick
  writes over that slot in place.
- `HudState` is deliberately **not** double-buffered: a HUD is a single by-value
  snapshot overwritten each tick, so between `BeginFrame` and `Publish` it can run a
  tick ahead of `Tick` / `Entities`. The **cooldown list is** (Stage 08b) — it is
  filled entry by entry rather than copied over, so it is written into the
  unpublished slot.
- One writer (the session's tick), any number of readers, one thread, no
  synchronization — the Web target is single-threaded.
- Publishing and reading allocate nothing once the buffer is constructed, and the
  fake and the Stage 07 EcsLite session must both use this same class. There is
  exactly one `IBattleFrame` implementation and
  `ArchitectureTests.Battle_frame_has_exactly_one_implementation` fails if a second
  appears.

## 7. Determinism and replay

- One fixed tick per `Step`, at 30 Hz; `BattleConstants.MaxStepsPerFrame = 4`.
- Battle randomness comes only from `IRandomSource`, seeded from
  `BattleSetup.Seed` via `RandomStreams.DeriveStreamSeed(seed, RandomStreamKind.Battle)`
  — the shop and visual streams are untouched.
- Determinism is guaranteed **within one runtime only** (AGENTS rule 6 /
  [gotchas/determinism-scope.md](../gotchas/determinism-scope.md)).
- `BattleReplay` records a setup, one input per step and ticked commands, and
  replays them through any `IBattleSessionFactory`. Comparing
  `ComputeStateHash` before and after detects a desync. It is the **battle** half
  of replay; the run-level `ReplayLog` of Stage 06 embeds one `BattleReplay` per
  floor rather than duplicating it.

## 8. Contract tests

`BattleSessionContractTests<TFactory>` (xUnit) is the executable contract. Both
factories — `FakeBattleSessionFactory` and, since Stage 07, the real
`EcsBattleSessionFactory` — are bound to it by a one-line derived class and must
pass every case: phase transitions, result bounds, event sequence continuity and
dropped reporting, the render cap, `PlayerInputFrame.FromAxis` normalisation,
replay round-trip, and **no allocation after warm-up**.

The real simulation's derived class visibly supplies what the suite's idle-input
premises need (a large HP pool, a churning benchmark load) — see
[ADR 0017](../../docs/adr/0017-ecslite-battle-simulation.md), §7. What it must
never do is relax a case.

## 9. What is deliberately absent

- No level-up / blessing / XP / choice gate (GDD v0.3; `BattlePhase` value 1 is
  reserved but unused).
- No economy: the battle reports `CurrencyEarned`, Run owns the wallet. (It reports
  `0` today: no `data/` column says what a kill pays — ADR 0017.)
- No bag / Board / combo logic: the battle receives a resolved
  `CombatLoadoutSnapshot` and never recomputes a combo.
- The fake does not fight — it exists to satisfy and exercise the contract, not to
  model combat. The real session does, and lives in
  [`battle-simulation.md`](battle-simulation.md).
