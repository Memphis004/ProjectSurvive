# ADR 0023 — The frame carries each loadout entry's cooldown

- **Status:** Accepted (Stage 08b).
- **Date:** 2026-10-17
- **Related:** [ADR 0007](0007-battle-contract-and-asmdef-placement.md) (the battle contract and where
  it lives), [ADR 0012](0012-prev-cur-frame-double-buffer.md) (the frame is a view, and there is
  exactly one implementation of it), AGENTS rules 3 (layers), 5 (no balance number in code), 7
  (state-changing commands are atomic and typed), 8 (no allocation on a hot path) and 10 (a View has
  no logic)

## Context

A HUD has to show, on each piece laid out on the board, how much of its cooldown is left. The
contract did not carry that: `IBattleFrame` exposed the tick, the HUD values and two entity spans,
and a caster's clock lived only inside the simulation (`Caster.CooldownTicks`) and inside the fake's
own head. The bag-to-battle projection (`CombatLoadoutSnapshot`) did not identify which board piece
an entry came from either, so even a frame that carried cooldowns could not say **which item** to
draw one on.

Three answers were available, and two of them are wrong in ways that only show up later:

1. **The View keeps its own clock.** A `Barbecue`-style component starts a timer when it sees a
   `ProjectileFired` event and draws a bar from it. It has the resolved cooldown nowhere (it comes
   from the skill row, the tier and the combo multipliers), it does not know that a floor is paused,
   it does not know a cast was refused for want of MP, and it drifts the moment a tick is dropped.
   This is the answer the stage's own brief forbids ("ห้ามให้ View เดาจากนาฬิกาเอง").
2. **A per-entity flag on `RenderEntity`.** Casters are not rendered entities: they carry no
   position and never appear in the frame's spans, and giving them positions so they could would turn
   bookkeeping into something the renderer, the spatial hash and the entity pool have to care about.
3. **The frame carries it.** The session already builds a view once per tick, out of state it holds,
   with a lifetime the contract already defines.

The values themselves are not new. `Caster.CooldownTicks` is what `AutoCastSystem` runs down;
`Caster.PeriodTicks` is the resolved cooldown; the player's MP is `Mana.Mp`, already published as
`HudState.Mp`. The only thing the frame adds is the one reading a View cannot make for itself — "this
entry is ready and only MP is holding it back", which needs the entry's MP cost.

## Decision

**1. `CombatLoadoutSnapshot` entries name the board piece they came from.** `WeaponLoadout` and
`SummonLoadout` gain `InstanceId` (`0`, `LoadoutCooldown.NoInstance`, when there is no piece), set by
`LoadoutResolver` from `ItemInstance.InstanceId`. Instance ids start at 1 and are never reused, so
the zero value is unambiguous, and it is the same key the rest of the game addresses a piece by.

**2. `IBattleFrame.Cooldowns` is a `ReadOnlySpan<LoadoutCooldown>`, in loadout order.** Weapons
first, exactly as `CombatLoadoutSnapshot.Weapons` lists them, then familiars as `Summons` lists
them; each entry carries its `InstanceId`, `RemainingTicks`, `PeriodTicks` and a `Flags` word whose
only bit today is `ManaShort`. Ticks, not seconds: the simulation's clock is ticks, and a view that
wants seconds or a progress bar divides by the rate it already knows. The span's lifetime is the
entities' — valid until the next `Step`/`Submit`.

**3. It is published by the same `BattleFrameBuffer`.** The buffer gains a bounded cooldown list
(`BattleConstants.MaxLoadoutEntries = 64`, above the 9 × 7 board's cell count) written into the
unpublished slot and swapped by `Publish`, so a reader holding a frame never sees a half-written
list — the reason the entity pair is double-buffered applies to a list just as much. There is still
exactly one `IBattleFrame` implementation, and the reflection test that says so is unchanged.

**4. The simulation reports, and changes nothing.** `FramePublishSystem` reads the casters it already
has; `Caster.InstanceId` is carried alongside the clock so no lookup is needed at publish time. No
cooldown rule was added, no combat outcome moved, and `InstanceId` is deliberately **not** mixed into
`ComputeStateHash` — a field that only answers a question about the bag must not be able to move a
deterministic fingerprint. The seeded hash is pinned by a test whose value was recorded on the
pre-change build (`Battle/SessionContractTests`'s
`The_state_hash_of_a_seeded_floor_with_casters_is_unchanged`, for both factories).

**5. The fake manufactures a predictable cycle.** It does not fight, so it cannot derive a cooldown
from a cast; it runs each entry down one tick per tick and resets it when it could cast, gated by MP
the same way the real rule is. Periods come from the loadout's resolved cooldowns, and only the
progress through them is the fake's own — which is what makes the fake usable to develop the HUD
against before the real floor is tuned.

## Consequences

- A HUD reads `frame.Cooldowns`, matches `InstanceId` to the piece the player placed, and draws
  `RemainingTicks` over `PeriodTicks`. It needs no clock, no event subscription and no knowledge of
  auto-cast, and a paused or refused cast is simply what the frame says it is.
- The contract grew a member, so an `IBattleFrame` implementation outside `BattleFrameBuffer` would
  now break — which is the intended direction (ADR 0012) and what the architecture test enforces.
- `MaxLoadoutEntries` is a structural bound, not a balance number: a loadout cannot exceed the
  board's cells. It is in `BattleConstants` beside `MaxRenderEntities` for the same reason.
- The frame does **not** say whether a target is in reach, which weapon auto-cast would pick, or why
  a ready entry is not casting. Those are the simulation's business; a HUD that showed them would be
  second-guessing the rule instead of reflecting it.
