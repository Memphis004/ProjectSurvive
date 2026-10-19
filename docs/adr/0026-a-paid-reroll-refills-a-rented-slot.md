# ADR 0026 — A paid re-roll refills a rented shop slot

- **Status:** Accepted (Stage 06c). Supersedes **the part of §2 of**
  [ADR 0016](0016-run-settlement-and-shop-commit-boundaries.md) that makes a `Roll` skip a rented
  slot through `CanBeRerolled` (`!IsLocked && !IsPurchased`). The rest of ADR 0016 stands: renting a
  slot still empties it, the settlement guard is still the phase plus the pending setup, and the
  other two boundaries are unchanged.
- **Date:** 2026-10-19
- **Related:** [ADR 0016](0016-run-settlement-and-shop-commit-boundaries.md) (the boundary this
  narrows), AGENTS rules 7 (atomic typed commands) and 12 (settle ambiguity the simplest way and
  write it down)

## Context

ADR 0016 §2 decided that renting the camp shop's shelf both marks the slot and **empties** it, and
then that a `Roll` skips every slot with `CanBeRerolled == false` — so a re-roll could never fill a
rented shelf. The reasoning was that "ช่องที่ซื้อแล้วไม่เติมสินค้าให้ฟรี" reads as *this slot must not
refill*, and the safest way to honour it was to leave the shelf untouched for the rest of the sala.

Playing it shows the other side of the same rule. A camp has four shelves. A player who can afford
two pieces leaves half the sala empty for the whole visit, and the paid เสี่ยงเซียมซี — the one
command that exists to change what the sala offers — refuses to touch exactly those shelves. The
GDD's "ไม่เติมให้ฟรี" is about the **rent**, which is free of further charge: the money was paid for
the piece that was taken. It is not a statement that a paid re-roll has to skip a shelf.

Stage 06c therefore changes the rule the prompt names:

1. `RefreshShop` re-rolls **every slot that is not pinned**, a rented slot included.
2. A rented slot that has not been re-rolled still shows as rented, holds nothing, and refuses a
   second rent. The first paid re-roll that reaches it clears the rent flag and puts a fresh offer
   on it.
3. Locking is unchanged in spirit and stricter in wording: a slot with nothing on it — rented, or
   left empty because the pool ran out — is refused, because there is nothing to pin.
4. One roll still never offers the same piece twice, and the new rule needs no new balance number:
   the re-roll price, the shop stream and the atomicity of `RefreshShop` are exactly what they were.

## Decision

**1. `ShopSlot.CanBeRerolled` is `!IsLocked`.**

The pin is the only thing a re-roll respects. The property keeps its name because it still answers
the question the roller asks ("may I touch this slot?"), and the roller is its only caller.

**2. The roller clears the rent before it draws, not after it succeeds.**

`ShopRoller.Roll` calls a new `ShopSlot.BeginReroll()` on every slot it is about to fill: it clears
the rent flag and the offer in one step, and the roller then either writes a new offer or — when the
candidate pool is exhausted — leaves the slot **empty and unrented**. Clearing first is what keeps
the two failure modes honest: a shelf that had no candidate is a shelf with nothing on it, never a
shelf that is simultaneously empty and rented. It is also what makes "ซื้อช่อง 0 → refresh → ช่อง 0
มีสินค้าใหม่ไม่ติดสถานะเช่าแล้ว" one line of state rather than two.

**3. `RunSession.SetSlotLocked` refuses an empty slot with `shop.slot_empty`.**

The refusal order is: phase, index, rented (`shop.slot_purchased`), empty (`shop.slot_empty`). A
rented slot is reported as rented rather than as empty because the rent is the more specific fact
and the more useful message; a slot emptied by a short pool (no candidate for it) now refuses too,
instead of accepting a pin that would keep a hole in the panel. Both refusals change nothing.

**4. Nothing else moves.**

`RefreshCost` (base + increment × refreshes already paid), `MarkRefreshed` after the charge, the
`ShopStock.RefreshCount` reset on `BeginCamp`, the shop random stream, and `RefreshShop`'s
"charge, then roll" order are untouched — a purse that cannot pay still leaves the stock, the
counter and the wallet exactly as they were.

## Alternatives considered

- **Keep ADR 0016's skip (a re-roll never touches a rented shelf).** Rejected: it is what makes half
  a sala dead for the rest of the visit, and the paid re-roll is precisely the command the player
  uses to change the shelf. The "ไม่เติมให้ฟรี" wording it was built on is satisfied by the rent being
  a one-time charge, which the changed rule still honours.
- **Keep the rented offer visible and re-roll it** (the option ADR 0016 itself rejected): rejected
  for the same reason as before — a panel that shows a rentable-looking offer whose `Purchase` fails
  with `shop.slot_purchased` is a UI the rule engine should spare.
- **Refill only the rented slots, leaving the ordinary free offers alone.** Rejected: a re-roll that
  replaced one shelf and left three would be a second rule the player has to learn, and it would
  change nothing about the price the player paid. "สุ่มใหม่ทุกช่องที่ไม่ล็อก" is one rule.
- **Clear the rent when the *next* camp starts instead of on the re-roll.** Rejected: that is what
  Stage 06b already fixed for a *new sala*, and it does nothing for the sala the player is standing
  in.
- **Leave the rent flag set and let the roller fill the offer anyway** (i.e. keep `IsPurchased` as a
  history flag). Rejected: the flag exists to refuse a second rent and to mark the shelf as rented;
  a shelf that holds a fresh offer and still says "เช่าแล้ว" contradicts itself, and the state hash
  would carry a lie.

## Consequences

- **A sold-out panel is no longer a dead end.** All four shelves can be rented, and one paid re-roll
  brings four fresh offers back — which is also what makes the re-roll worth its climbing price late
  in a camp. `ShopStock.IsSoldOut` and `AvailableCount` need no change: they read the slots, and the
  slots now tell the truth.
- **The rent flag's lifetime is the sala, or the next paid re-roll, whichever comes first.** The
  state hash and the checkpoint DTO keep the same five fields per slot (`id`, `kind`, `price`,
  `IsPurchased`, `IsLocked`) — no format change, only different values over time.
- **The replay path is unaffected by construction.** A `RunCommand.RefreshShop` recorded in the log
  replays through the same `RefreshShop`, so the replayed run refills the same shelves and lands on
  the same `RunState` hash; the Stage 06 replay cases already carry a rent followed by a re-roll and
  now exercise the new rule for free.
- **The presentation needs no change.** The camp reads the stock live on every `Refresh()`, and a
  refilled shelf publishes its new piece, price and `IsPurchased = false` through the same
  `CampShopSlotModel`. The rented shelf's own look ("เช่าแล้ว" with no offer) is unchanged while the
  rent is on it.
- **`shop.slot_empty` becomes a code a player can see**, where before it was only reachable through
  `Purchase`. It was already in the error table and already localized.
