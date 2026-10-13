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
| `DevLoseNow` | – | force a loss |

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

`IBattleFrame` exposes `Tick`, `Hud` and `Entities`. `Entities` is a
`ReadOnlySpan<RenderEntity>` that is **valid only until the next `Step` or
`Submit`** — copy anything you need to keep. `HudState` names its fields after the
Luban columns (`hp`, `mp`, `currency`) and has no Level/XP/input-inversion field.

`BattleFrameBuffer` is the reusable implementation; `Entities.Length` never
exceeds `BattleConstants.MaxRenderEntities` (1024).

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

`BattleSessionContractTests<TFactory>` (xUnit) is the executable contract. Any
factory — the fake today, the EcsLite simulation from Stage 07 — is bound to it by
a one-line derived class and must pass every case: phase transitions, result
bounds, event sequence continuity and dropped reporting, the render cap,
`PlayerInputFrame.FromAxis` normalisation, replay round-trip, and **no allocation
after warm-up**.

## 9. What is deliberately absent

- No level-up / blessing / XP / choice gate (GDD v0.3; `BattlePhase` value 1 is
  reserved but unused).
- No economy: the battle reports `CurrencyEarned`, Run owns the wallet.
- No bag / Board / combo logic: the battle receives a resolved
  `CombatLoadoutSnapshot` and never recomputes a combo.
- The fake does not fight — it exists to satisfy and exercise the contract, not to
  model combat.
