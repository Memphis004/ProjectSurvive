# ADR 0012 — The battle frame is a Prev/Cur double buffer with exactly one implementation

- **Status:** Accepted
- **Date:** 2026-10-14
- **Stage:** 02b (amendment to the contract, landed after Stage 04)
- **Related:** [ADR 0007](0007-battle-contract-and-asmdef-placement.md) (battle contract v1),
  [`llmwiki/systems/battle-contract.md`](../../llmwiki/systems/battle-contract.md)

## Context

Stage 02b defined `IBattleFrame` with a single entity span (`Entities`) and a
`BattleFrameBuffer` that cleared one array and refilled it every tick.

Two later stages need more than one array:

- **Stage 08** renders 30 Hz simulation state on a faster display, and the GDD
  states it as *"fixed-step accumulator (TickDelta, MaxStepsPerFrame) + interpolate
  Prev → Cur ด้วย alpha"* while *"Presentation อ่านเฉพาะ `IBattleFrame` /
  `IBattleEventSource`"*. Interpolating between two ticks needs **two ticks of state
  at once**, and the port is the only thing Presentation may read.
- **Stage 07** must reuse *"`BattleEventRing` และ `BattleFrameBuffer` ร่วมกับ fake"*,
  so the EcsLite session may not publish frames its own way.

A caller clearing and refilling the live array each tick also means the array a
reader just read is the array being rewritten, so any reader that holds the span a
tick too long loses the frame it was describing rather than the frame it was
already done with.

## Decision

### 1. `IBattleFrame` exposes the pair: `Entities` (Cur) and `PreviousEntities` (Prev)

`PreviousEntities` is the addition. `Entities` keeps its name and meaning (the tick
the frame describes) so every existing reference — the GDD's Stage 08 acceptance
line, the contract tests, `HudState` docs — stays true.

Both are `ReadOnlySpan<RenderEntity>` and both stay valid only until the next
`Step` / `Submit`, exactly as `Entities` already did. `PreviousEntities` is empty on
a session's first frame: there is no earlier tick, and an interpolating reader must
treat that case as "no pair yet" rather than as "all entities at the origin".

### 2. `BattleFrameBuffer` is a real double buffer

Two fixed arrays of at most `BattleConstants.MaxRenderEntities`, with **the slot
references swapped on `Publish()`** — not a copy of the entities:

```
   BeginFrame(tick)  set the tick, rewind the write cursor  (published frame untouched)
   SetHud(hud)       copy the single HudState over
   Add(entity)       append into the non-published slot
   Publish()         swap: what was written becomes Cur, what Cur was becomes Prev
```

After a publish, `Entities` is the tick just simulated and `PreviousEntities` is the
one before it *with its count intact*, because the frame was never overwritten — only
the slot roles changed. The slot being written **is** the slot that holds Prev, so
`PreviousEntities` is only meaningful *between* ticks; that is the same lifetime the
contract already documents, and reading mid-tick was never allowed.

Consequence to keep in mind: two slots means Prev is the immediately preceding tick,
never history. Nothing in the prototype needs more than one tick back.

### 3. `HudState` is deliberately *not* double-buffered

The HUD is a by-value struct replaced as soon as it is set. Nothing interpolates a HP
number, a kill count or a floor timer, so a second copy would be state nobody reads
that also has to stay in step. The cost is honest and documented: between
`BeginFrame` and `Publish`, `Hud` can run a tick ahead of `Tick` / `Entities`.

### 4. Exactly one implementation, and a test says so

`BattleFrameBuffer` is the only `IBattleFrame`. The fake (Application) publishes
through it today and the EcsLite simulation (Simulation, Stage 07) must publish
through the same class. Rationale: Stage 08 develops the presentation against the
fake, and a second implementation is exactly how "works with the fake, differs with
the real sim" becomes a playtest discovery.

`ArchitectureTests.Battle_frame_has_exactly_one_implementation` reflects over every
core assembly and fails unless the implementation set is exactly
`{ BattleFrameBuffer }`. When Stage 07 lands, it adds no implementation — it reuses
this one.

### 5. The contract stays in `ProjectSurvive.Core.Domain.Combat.Battle`, under `lib/Runtime/Domain/Combat/Battle/`

Re-verified rather than assumed, because a wrong namespace or path here is invisible
until Unity resolves the assemblies:

- the layer asmdefs are `Core`-prefixed (`ProjectSurvive.Core.Domain.asmdef`,
  `ProjectSurvive.Core.Application.asmdef`, …), so every contract type is
  `ProjectSurvive.Core.Domain.Combat.Battle` — the shape Stage 05 already names when
  it points `LoadoutResolver` at
  `ProjectSurvive.Core.Domain.Combat.Battle.CombatLoadoutSnapshot`;
- the physical home is the existing `lib/Runtime/Domain/Combat/Battle/` (the layout
  ADR 0007 recorded) and the fake stays in `lib/Runtime/Application/Battle/Fake/`.

No path or namespace change was needed. Both are asserted by the reflection tests
over the namespace, so a future move fails the suite instead of drifting silently.

## Consequences

- **Positive:** one frame type carries both ends of the interpolation pair, so Stage
  08's runner reads `Prev`/`Cur` off the port it is already required to use, and each
  reference to an `IBattleFrame` still names the tick it came from.
- **Positive:** publishing is now explicit (`Publish`), so a half-built frame is
  unobservable instead of merely "unlikely to be read".
- **Positive:** swapping two references is cheaper than clearing and refilling, and
  the zero-allocation guarantee is unchanged (asserted in both the unit test and the
  contract suite).
- **Negative:** `IBattleFrame` grew a member, so any implementation that is not
  `BattleFrameBuffer` would now fail to compile. That is the intent.
- **Negative:** `PreviousEntities` is only one tick deep and, like `Entities`, is
  invalid after the next `Step` / `Submit`. A reader that wants a longer history must
  copy — the same rule as before, now with two spans instead of one.
- **Superseded:** `battle-contract.md` §6 described one span over one array and has
  been rewritten for the pair. ADR 0007 is unchanged — it never documented the frame’s
  internals, only that the frame contract lives in `Domain`.
