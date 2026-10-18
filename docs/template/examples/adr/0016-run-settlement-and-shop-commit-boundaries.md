# ADR 0016 — Run settlement and shop commit boundaries

- **Status:** Accepted (Stage 06)
- **Date:** 2026-10-14
- **Related:** [ADR 0007](0007-battle-contract-and-asmdef-placement.md) (the battle seam and
  the two-level replay), [ADR 0014](0014-backpack-editing-undo-and-placement-ownership.md)
  (atomic commands, `internal` mutators), AGENTS rules 7 (atomic typed commands) and 12
  (no abstraction without a reason)

## Context

Stage 06 landed the run: a wallet, a camp shop, a settlement, and a replay log. Three of its
rules could each have been written two ways, and the wrong choice is invisible until much
later:

1. **"ให้รางวัลได้ครั้งเดียว" — paying a cleared floor once.** The prompt names a *settlement
   ID*, which reads like a guard: remember the paid id and refuse a repeat. A run already has
   a phase machine and a pending-floor slot, either of which could be the guard instead.
2. **"ช่องที่ซื้อแล้วไม่เติมสินค้าให้ฟรี" — a rented slot.** The slot must not refill for free.
   It may keep showing what was rented and simply refuse to be drawn over, or it may go empty.
3. **A battle result that disagrees with the loadout.** `BattleResult.RemainingConsumables`
   is one entry per `CombatLoadoutSnapshot.Consumables` entry. A floor that reports a
   different number of entries is a desync — a bug in a recorder, a hand-built result, a
   version skew between the battle and the run. It can be clamped to fit, or refused whole.

## Decision

**1. The single-payout guard is the phase plus the pending setup, and the settlement id is a
report, not a lock.**

`StartFloor` stores the `BattleSetup` and its settlement id; `ApplyBattleResult` reads both,
settles, and clears both. A second call in the same run has no pending setup and returns
`run.not_in_battle` (its documented meaning: "a battle result was offered while no floor was
in progress"), which is also what a result offered at Camp or at Result gets. The id is
published on `FloorSettlement.SettlementId` and tracked as `RunState.LastSettlementId`, so a
UI can show *which* settlement paid and the state hash sees it move — but nothing compares
ids to decide whether to pay.

The declared-but-unreturned code `run.floor_already_settled` was therefore **deleted**
rather than left in the error table: an error code no path returns is API that lies to
Stage 09's localization and to the next reader.

**2. Renting a slot empties it.**

`ShopSlot.MarkPurchased()` sets `IsPurchased` **and** clears kind, id and price, so
`IsEmpty` means exactly "holds nothing to rent" and the panel shows a rented slot as rented.
A `Roll` skips it through `CanBeRerolled` (`!IsLocked && !IsPurchased`), so a re-roll can
never fill it. What was rented is carried by `PurchaseOutcome` (and by the piece that landed
in the chest), captured **before** the commit, so the event and the outcome still name it.

**3. The composition root still binds no battle session.**

The prompt's requirement is that `SceneFlow` receives `IBattleSessionFactory` "through a
port / DI". That is satisfied by the constructor seam plus whoever constructs the flow —
today the test harness and the Unity EditMode suite, at Stage 07–09 the run-level scope
together with the real factory. `RootLifetimeScope` stays scene-scoped and registers no
`RunSession`, no `SceneFlow` and no `IBattleSessionFactory`, so the fake of Stage 02b cannot
become the shipped battle by accident in a registration nobody reads, and the scope that
survives scene loads is designed once the run loop (Stage 08–10) exists to need it.

**4. A result whose consumable list does not line up with the loadout refuses the whole
settlement.**

`ValidateConsumables` runs before anything is paid and rejects `remaining.Count !=
loadout.Consumables.Count` with `inventory.not_enough_pieces`; HP, bia and the clear heal are
not applied. An **empty** list is still accepted as "this floor reports no consumable
bookkeeping", because that is what a hand-built `BattleResult` in a test looks like and what
the fake would send for a loadout with no offerings. When the counts match, each entry is
checked with `Editor.CanConsume` before the removal.

## Alternatives considered

- **Compare settlement ids** (`if (result.SettlementId == LastSettlementId) refuse`) — rejected:
  a `BattleResult` carries no settlement id, so the id would have to be threaded into the
  battle and back, or inferred. That is a new field on the battle contract in both
  implementations, in exchange for a check that the phase already performs; and the phase
  check also catches the genuinely different cases (a result at Camp, a result after the run
  ended). Recorded here because the prompt's wording invites the weaker design.
- **Keep `run.floor_already_settled` "because a future build might return it"** — rejected
  (AGENTS rule 12): a code with no producer is a localization key for an impossible state.
  `run.not_in_battle` is the accurate code, and it is what the tests assert.
- **Keep the rented offer visible and refuse to refill it** — rejected: `CanBeRerolled` would
  stay correct, but the panel would show a rentable-looking offer whose `Purchase` fails with
  `shop.slot_purchased`. A UI that has to special-case "this offer is display-only" is a UI
  the rule engine is allowed to spare. The cost is that "what was rented here" is only in the
  purchase outcome, and a Stage 09 panel that wants it must keep that outcome (or show
  "เช่าแล้ว" with no name, which is what the GDD's own wording describes).
- **Clamp a mismatched `RemainingConsumables`** (`for i in min(counts)`) — rejected: the
  settlement moves HP, bia and the clear heal, and the only thing that keeps those three
  consistent with the bag is that they are applied together or not at all. A clamped result
  would pay the currency for a floor whose bookkeeping the run does not understand.
- **Treat any empty list as a desync too** (refuse when the loadout has consumables and the
  result reports none) — not adopted: it would make `BattleResult`'s default state illegal
  and force every hand-built test result to enumerate a loadout it does not care about. The
  asymmetries are one-sided on purpose: matching counts are checked, an empty list is a
  "no report", and a *different non-zero* count is refused.

## Consequences

- **A run cannot pay twice, whatever a caller does.** The guard is a phase, so it also covers
  the paths the prompt does not mention: a result delivered at Camp, a result delivered after
  the run already ended, and a `SceneFlow` that settles a floor it already settled.
- **The settlement id stays useful without being load-bearing.** `LastSettlementId` is part of
  `RunState` (so a replay proves it moved) and of `FloorSettlement` (so the summary screen can
  label the floor it just paid). It is monotonic per run, and a settlement is exactly one
  floor's payout.
- **The shop's state is fully described by `ShopSlotState`.** Because a rented slot is empty,
  the hash over (id, kind, price, `IsPurchased`, `IsLocked`) cannot hide a rented slot behind
  a stale offer.
- **No registration can hide a battle implementation.** The only way to get a session is to
  ask the factory you were given, which an architecture test enforces at the source level
  (nothing outside `Application/Battle/Fake/` may name the fake).
- **A desync fails loudly and cheaply.** `ApplyBattleResult` returns a typed failure that
  leaves the run in `Battle`, so the caller can retry with a corrected result, and the failure
  happens before the purse or the HP moved — the same invariant the shop commands hold.
- **The `run.` error table is the reachable refusals plus two named preconditions**: the five
  a caller can trigger — `wrong_state`, `no_arcane_weapon`, `not_in_battle`, `battle_running`,
  `unknown_piece` — are each asserted by a test that triggers them, and the two that guard the
  floor cursor and a degenerate floor table (`no_floor_left`, `unknown_floor`) say so in their
  own doc comments rather than being presented as player-facing outcomes. `no_floor_left` is
  additionally pinned by an invariant test: every Camp the phase machine enters still has a
  floor to start. No code in this table is redundant with another, which is why
  `run.floor_already_settled` is gone instead of sitting next to `not_in_battle`.
