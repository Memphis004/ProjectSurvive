# ADR 0020 — A bag move or turn carries the items standing on it

- **Status:** Accepted (Stage 09f). **Supersedes [ADR 0014](0014-backpack-editing-undo-and-placement-ownership.md) §5**
  (“a bag change that would strand an item is refused whole”, for the two board commands only).
- **Date:** 2026-10-16
- **Related:** [ADR 0014](0014-backpack-editing-undo-and-placement-ownership.md) (commands,
  snapshot undo, cell ownership), [ADR 0009](0009-footprint-picture-format.md) (footprint
  picture), [ADR 0015](0015-same-line-synergy-kinds.md) (same-line rules),
  [ADR 0019](0019-camp-screen-read-model-and-gesture-preview.md) (the drag preview),
  AGENTS rules 7 (atomic commands), 10 (the View decides nothing) and 12 (no abstraction
  without a reason)

## Context

Stage 05 decided the bag as a *decorated cell provider*: an item may straddle several bags,
a bag may not move or turn while an item stands on it, and the refusal is whole
(`inventory.item_stranded`). That is safe, but it makes the natural gesture impossible —
a player who wants the whole ย่าม somewhere else has to take every item off it, move the
bag, and put them back, one command at a time, watching the combos flicker off and on
while they do.

Stage 09f changes the rule for the two commands that stay **on** the board. The bag is now
an object that **carries** what stands on it: items ride along and keep their place in the
bag's own frame. Everything else about ADR 0014 stands — commands are atomic, undo is a
snapshot, cell ownership is single-valued, the loadout is cached on `Revision`.

The change is not local. It touches the transform (`FootprintRotation`), the state layer
(`Backpack`), the one predicate every preview and every command goes through
(`BackpackEditor.CanPlace`), the read model the screen draws (`CampDragState`) and the
randomized invariant test. It also needs its own refusal code, because “move that item
first” and “the bag cannot leave the board with an item on it” are two different things to
tell a player.

## Decision

**1. The bag and its riders are one rigid assembly, turned about the bag's own origin.**
`FootprintRotation.Rotate(shape, rotation, out CellOffset shift)` now publishes the
translation its re-normalisation removed, and `Backpack.CollectRiders` maps every rider
cell the same way the bag's own shape was mapped: `board = newOrigin + Turn(cell - bagOrigin) - shift`.
A pure move is the **zero-turn case of the same function**, so “move” and “turn” cannot
disagree, and it is the only version that stays rigid for a chiral shape — a rider on the
L-shaped bag (`bag.wicker_basket_l`) is the case that catches the naive “translate the item
by the bag's delta” implementation.

**2. A rider is an item whose *every* cell lies on that bag.** Bags never overlap
(ADR 0014 §4), so “every cell on this bag” means “this bag and no other”. An item lying
across this bag and a second one does not ride: part of it belongs to a bag that is
staying.

**3. An item that does not ride concedes or refuses the command.** If, after the move, some
cell of a non-rider would have no bag under it, the command is refused **whole** with a new
code `inventory.item_spans_bags` — distinct from `inventory.item_stranded`, which now means
only “this would take a bag off the board with items still on it”. The two codes are
localized separately because the player's next action differs: one says *move that item to
the chest or rearrange it*, the other says *there is an item on that bag*.

**4. Off the board is unchanged.** `TransferToChest` and `Recycle` of a bag an item stands
on are still refused with `inventory.item_stranded`. An item can never come along into the
chest: a chest piece covers no cell, so a rider there would be an item outside the build —
and the chest has a fixed 10-slot budget that an implicit multi-item insert could overflow.

**5. Atomicity is unchanged: one gesture, one revision, one undo step.** `PlaceAssembly`
lifts the bag and every rider **before** it puts any of them down, because a rider may
legally land on a cell another moving piece occupied (a small move overlaps the old
footprint, and the owner maps are single-valued per cell). `Revision` moves once, so the
synergy evaluation and the loadout are recomputed once (ADR 0014 §7) and a single `Undo`
puts the bag and all its riders back.

**6. One plan, read by the preview and applied by the command.**
`BackpackEditor.CanPlace` fills the editor's carry plan as it answers — the plan is a
scratch list, so the predicate stays allocation-free — and the accepted command applies
*exactly* that plan. `CollectCarried` is the read-only accessor for a screen that wants to
draw the riders before the command runs. A preview and a command can therefore not
disagree about where an item lands: there is one computation.

**7. The screen draws the plan; it does not know the rule.** `RunSession.CollectCarried`
fills the presenter's buffers, and `CampDragState.Carry` publishes, per rider, the landing
cell and the footprint it lands with (both read from the data table, never re-derived in
the View). The drag's rider ghosts and the bag's own ghost travel in one published value,
so they cannot be a frame apart, and an unchanged plan publishes nothing. Combos are
recomputed from the new layout once the command lands — a turn takes two items that shared
a row onto different rows, and the drawn line goes with the rule that stopped firing.

## Alternatives considered

- **Auto-parking a non-rider into the chest.** Rejected: silently moving the player's item
  is a worse outcome than a refusal the screen can explain (ADR 0014 already rejected it),
  and the chest can be full.
- **Refusing whenever an item spans two bags, covered or not.** Rejected as too strict: an
  item that keeps its coverage has no reason to block the move, and this is the common case
  for two bags laid side by side.
- **Carrying riders into the chest as well.** Rejected: it would put items in a place the
  build does not read, change the chest's slot arithmetic, and make a refusal (chest full)
  depend on how many items happened to stand on the bag.
- **Moving the bag and translating each rider by the bag's delta.** Rejected: not rigid.
  The bag's cell is re-normalised when its shape turns, so the delta is not the transform a
  rider needs; `FootprintRotation.Rotate(..., out shift)` is what makes the two agree, and
  the L-shaped bag is the failing case.
- **Letting the View rotate each rider's footprint itself.** Rejected: that is the placement
  rule in a second place (AGENTS rule 10 / rule 12), and it would drift the moment the
  transform changed. The presenter publishes the landed footprint instead.
- **A second predicate for the carry case (`CanMoveBag`).** Rejected: two entry points is
  exactly how a preview and a command come to disagree (ADR 0019's whole reason for
  `CanPlace`). One predicate, one plan.
- **Reusing `item_stranded` for the new refusal.** Rejected: the codes are the UI's only
  description of what to do next, and “an item lies across two bags” is not “an item would
  be left off the bag”.

## Consequences

- **`FootprintRotation.Rotate` gained an `out CellOffset shift` overload** (the old
  signature still compiles and forwards it). The re-normalisation is now part of the
  function's contract rather than an internal detail.
- **A bag gesture is no longer a one-piece command.** The revision counts *gestures*, not
  pieces; `LoadoutResolver.ResolveCount` stays 1 per gesture, and undo is one step.
- **An item can be repositioned by proxy** — turning a bag moves everything on it, which is
  now a legitimate (and fast) way to rearrange a build. This is the feature, not a loophole.
- **`inventory.item_stranded` narrowed and `inventory.item_spans_bags` is new.** The
  localization row exists in both languages; the data gate's error-code list grows by one.
- **The randomized invariant test now walks bag gestures.** 10 000 mixed commands including
  move and turn of bags assert the structural invariants after every step: no item outside a
  bag's cells, no overlap, nothing lost or duplicated.
- **The screen's rider ghosts are a published plan, not a View computation.** A pointer move
  inside one cell still allocates nothing — the plan is handed back untouched when the
  placement it was built for has not changed.
