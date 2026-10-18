# ADR 0024 — Shared generic buffers: `EventRing<T>` and `PrevCurBuffer<T>`

- **Status:** Accepted
- **Date:** 2026-10-18
- **Inherited by:** template-v1 — the decision still applies; the numbering below is the template's own (0001–0099).
- **Stage:** 10t (the step before `main` and `template` are split)
- **Related:** [ADR 0007](../template/examples/adr/0007-battle-contract-and-asmdef-placement.md) (battle contract v1;
  §5 argued the ring's *semantics* are what the fake and the simulation share),
  [ADR 0012](0012-prev-cur-frame-double-buffer.md) (the Prev/Cur frame pair),
  [`docs/template/examples/systems/battle-contract.md`](../template/examples/systems/battle-contract.md)

## Context

Stage 10t prepares the repository for a `main` / `template` split: after this stage
`template` becomes the base of a new game, and both branches keep the **same files**
wherever the file does not name a game type, so a change can be cherry-picked across
them. Today two battle types build generic machinery around a game type:

| Type (Domain) | Generic behaviour | Game-typed part |
| --- | --- | --- |
| `BattleEventRing` | fixed capacity, strictly increasing `Seq`, one cursor per reader, honest `dropped` count, zero allocation after warm-up | `BattleEvent` |
| `BattleFrameBuffer` | the Prev/Cur double buffer of ADR 0012: two slots swapped on `Publish`, counts carried with the slot | `RenderEntity`, `LoadoutCooldown`, `HudState` |

The alternatives were:

1. **Copy the behaviour per game type.** Each branch would then own a private copy and
   the two copies would drift — exactly what the split wants to avoid.
2. **Move the battle types into `Shared`.** Impossible without inverting the layers:
   `Shared` is referenced by `Domain` and must not know a `Domain` type.
3. **Make the mechanics generic in `Shared` and have the battle types compose them.**
   Chosen.

ADR 0007 §5 and ADR 0012 both justify their decisions in terms of *mechanics* (a ring
with independent cursors; a pair of slots swapped per tick), not in terms of the
payload. Nothing in either mechanic needs to know what an event or an entity is.

## Decision

### 1. `EventRing<T>` in `ProjectSurvive.Core.Shared`

```text
EventRing<T>(capacity)        capacity >= 1; throws below it
long Write(in T item)         appends, overwrites the oldest when full, returns the assigned Seq
int Read(ref long cursor, Span<T> destination, out int dropped)
long OldestSeq / NextSeq      strictly increasing, never reused
int ReadableCount / Capacity
void Clear()                  empties the ring and restarts Seq at 0
```

The semantics are the ones ADR 0007 §5 fixed for the battle contract, unchanged: a
reader further behind than the capacity loses the oldest items and is **told** how many
(`dropped`), and `Write`/`Read` allocate nothing once warmed up. `T` is stored as-is;
the ring owns no seq field, so a payload that carries its own sequence number gets it
from the caller (see 2).

### 2. `BattleEventRing` contains `EventRing<BattleEvent>`

`BattleEventRing`'s public API is **unchanged** (`Capacity`, `OldestSeq`, `NextSeq`,
`ReadableCount`, `Write(in BattleEvent)`, `Clear()`, `Read(...)`), because
`IBattleEventSource` and both implementations (fake and EcsLite) are built on it. It
stamps `Seq` itself before handing the event to the ring, using the ring's own
`NextSeq`: writes are single-threaded (AGENTS rule 6), so the number the ring assigns
is the number that was read. This keeps `buffer[i].Seq == i` true — the observable the
existing suite and the browser reports depend on.

### 3. `PrevCurBuffer<T>` in `Shared`

```text
PrevCurBuffer<T>(capacity)    capacity >= 0
BeginFrame()                  rewinds the write cursor; the published slot is untouched
Add(in T item)                appends into the slot the next Publish swaps in; ignores surplus at capacity
Publish()                     swaps the slots: written -> Current, Current -> Previous
Current / Previous            ReadOnlySpan<T>, valid until the next BeginFrame/Add/Publish
CurrentCount / PreviousCount / Capacity
```

This is exactly the arrangement ADR 0012 chose, with the payload made generic:
`Prev` is one tick deep, never history, and the slot being written **is** the slot that
holds `Prev` — so `Previous` is only meaningful between ticks.

### 4. `BattleFrameBuffer` composes two `PrevCurBuffer<T>`s and keeps its API

`BattleFrameBuffer` now holds `PrevCurBuffer<RenderEntity>` (entities) and
`PrevCurBuffer<LoadoutCooldown>` (the Stage 08b cooldown list), both published in the
same `Publish()`, plus the **non-swapped** `HudState` and the tick, which ADR 0012
deliberately kept out of the pair. `IBattleFrame` is untouched, `BattleFrameBuffer` is
still the only implementation (the architecture test that reflects over every core
assembly is unchanged), and capacity clamping to
`BattleConstants.MaxRenderEntities` / `MaxLoadoutEntries` stays in `BattleFrameBuffer`
because it is a *battle contract* bound, not a property of a generic buffer.

### 5. Tests own both halves

- `EventRingTests` and `PrevCurBufferTests` (new, `Core.Tests`) assert the generic
  mechanics — independent cursors, `Seq` monotonicity, dropped counts, slot swap, the
  capacity bound — and measure **0 bytes allocated** over 10 000 warmed-up iterations
  with `GC.GetAllocatedBytesForCurrentThread()`.
- `BattleEventRingTests` and `BattleFrameBufferTests` stay as they are; they now cover
  the composition (seq stamping, `HudState` not swapping, the contract caps).
- `BattleSessionContractTests` is **not modified** — it is the seam that proves the
  game behaviour did not change, and a passed contract suite on the pre-change build
  must stay passed on the post-change one.

## Consequences

- **Positive:** the two mechanics now exist once, in a layer both branches share, so
  the same file survives the split and a fix to either buffer is interchangeable.
- **Positive:** the battle types keep their public shape, so nothing in
  `Domain.Combat.Battle`'s API moves and the contract tests still predict the game.
- **Positive:** the generic types are testable with trivial payloads, which is how the
  zero-allocation claim is measured independently of the battle's own benchmark.
- **Negative:** `BattleEventRing.Write` now produces the `BattleEvent` before calling
  the ring, so it relies on "single writer, single thread" — already an AGENTS rule
  and already true of every session.
- **Negative:** one more layer of indirection in the frame's hot path. It is a field
  access on a class-typed slot, and the zero-allocation test plus the battle
  benchmark's tick budget are the checks that it stays free.
- **Not changed:** no rule, no balance number, no scene content and no browser global
  moved because of this ADR; the Stage 10t report records the measured evidence.
