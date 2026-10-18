# ADR 0014 — Backpack editing: state vs. commands, snapshot undo, and cell ownership

- **Status:** Accepted (Stage 05). **§5 is superseded by [ADR 0020](0020-bag-moves-carry-the-items-on-them.md)**
  (Stage 09f): a bag move or turn now carries the items standing on it instead of being
  refused. §1–§4 and §6–§8 stand unchanged.
- **Date:** 2026-10-14
- **Related:** [ADR 0007](0007-battle-contract-and-asmdef-placement.md) (Domain contract,
  Application fake), [ADR 0009](0009-footprint-picture-format.md) (footprint picture),
  [ADR 0012](0012-prev-cur-frame-double-buffer.md) (a published frame is only valid until
  the next tick — the same “one owner of the bytes” idea), AGENTS rules 3, 7 and 12

## Context

Stage 05 is the first stage with **player commands**, and the GDD puts five requirements
on them:

- Move / Rotate / TransferToChest / Merge / Recycle are **atomic** and return a typed
  result; a failed command must change nothing.
- **Undo** exists, and **Reset** returns the bag to the state it had on entering Camp
  (“คืนสภาพตอนเข้าศาลา”).
- A bag may be moved or removed only if no item loses its usable area — and if one would,
  the *whole* command is refused.
- **Lock** (“ล็อกย่าม”) prevents stray edits.
- The loadout is recomputed **only when the set changes**.

None of this was decided by an earlier stage, and each choice is expensive to reverse
once the UI (Stage 09) and the save file (Stage 12) depend on it.

## Decision

**1. State and commands are two types.** `Backpack` owns state (`Board`, `Chest`, the
piece instances, `Revision`) and its mutators are `internal`; `BackpackEditor` owns the
command surface, performs every check *before* the first mutation, and is the only thing
that may push undo. This is what makes “a rejected command changes nothing” structurally
true rather than a promise: validation cannot be forgotten in the middle of a mutation
because no mutation is reachable from outside `Backpack`.

**2. Pieces have two identities.** `DefId` is the permanent data-owned id
(`item.mor_knife`); `InstanceId` is this run's handle, unique and never reused. Commands
address a piece by `PieceRef` (`PieceKind` + instance id), never by a raw int, for the
same reason `EntityRef` exists in the battle contract: a bag instance and an item
instance come from one counter and must not be interchangeable by accident. Merge is
what makes the split necessary — it needs one of two instances *of the same def id* to
disappear, which an id-is-the-piece model cannot express.

**3. Undo and Reset are MessagePack snapshots of the same wire type.** Entering Camp
encodes the current state once (`MarkCampEntry`); every accepted command first pushes a
snapshot onto a bounded stack (64 entries by default); `Undo` and `Reset` decode a
snapshot back over the live state. The encoding goes through `IBackpackSnapshotCodec`,
whose implementation lives in Infrastructure, so `Domain` keeps its rule that it never
depends on a serializer (rule 3) while the bytes are still real MessagePack that Stage 12
can put inside a checkpoint.

**4. Cell ownership is explicit and single-valued.** A bag's cells are *ActiveSlots*. An
item may be placed only when **every** one of its cells is an ActiveSlot; it may straddle
several bags. Bags never overlap, and items never overlap. `Board` keeps two owner maps
(bag owner per cell, item owner per cell) and every placement check is a read of those
maps, so “overlap”, “not covered” and “outside the board” are three different error codes
rather than one generic refusal.

**5. A bag change that would strand an item is refused whole.** Move / Rotate /
TransferToChest / Recycle of a bag all run the same check: recompute whether every placed
item would still be fully covered *without* this bag's contribution; if one would not,
the command fails with `inventory.item_stranded` and nothing moves. There is no
relocation, no auto-park and no partial move.

> **Superseded for Move and Rotate by [ADR 0020](0020-bag-moves-carry-the-items-on-them.md)
> (Stage 09f).** On the board the items standing on the bag now ride along, and the refusal
> is reserved for the two cases that cannot be carried: taking the bag **off** the board
> (`inventory.item_stranded`, as above) and an item lying across two bags that would lose
> its cover (`inventory.item_spans_bags`). The reasoning that survives is the one behind the
> rule: refuse the whole command, never relocate an item silently.

**6. Lock freezes every edit, including undo.** Locking is itself never undoable and
always accepted, because its whole purpose is to be the state the player can rely on
(“a stray drag during a battle cannot rearrange a build”).

**7. The loadout is cached against `Backpack.Revision`.** Every applied change bumps the
revision; a rejected command does not. `LoadoutResolver.Resolve` returns the cached
`CombatLoadoutSnapshot` while the revision is unchanged, so “recompute only when the set
changes” is measurable (`ResolveCount`) instead of aspirational. Ordering inside the
snapshot is canonical — board position for weapons/summons, id for consumables — never
dictionary iteration order, so two backpacks holding the same layout resolve to the same
values.

**8. Recycle pays a percentage of the *base* price.** `floor(price * economy.sell_back_percent
/ 100)`, using the piece's tier-1 price, because the data model has one price per piece
and no per-tier price column. This is recorded as a gap rather than patched in code: if
selling a tier-3 piece should return more, a price column has to land in `data/` first.

## Alternatives considered

- **Undo as an inverse-command log.** Rejected: `Merge` and `Recycle` are not invertible
  without reconstructing what was destroyed (a second model of the state), and a
  hand-written inverse is one more place for the rules to drift out of sync.
- **A bespoke undo struct rather than encoded bytes.** Rejected: it would be a *second*
  encoding of the same state, and the snapshot bytes are exactly what Stage 12 needs to
  persist. One representation, serialized, is what keeps Save honest.
- **Auto-relocating a stranded item into the chest.** Rejected: the GDD says the command
  is refused, and silently moving the player's item is a worse outcome than a refusal the
  UI can show.
- **Recomputing the loadout on every tick.** Rejected: the GDD says “เมื่อชุดอุปกรณ์เปลี่ยน
  เท่านั้น”, and the battle is fixed-tick — the snapshot is computed at Camp and on the
  frame a change happens, never at 30 Hz.
- **Storing each piece's footprint in the snapshot.** Rejected: the shape is a function of
  the def id and the rotation, and a stored shape is a second source of truth that a
  rotation bug could desync ([ADR 0009](0009-footprint-picture-format.md) already owns the
  format).

## Consequences

- **`BackpackSnapshot.SchemaVersion` exists from day one** so a future format change can
  migrate; the snapshot is a plain Domain type (no serializer attribute), mapped to a
  MessagePack DTO in `Contracts` by the Infrastructure adapter.
- **A rejected command costs one validation pass**, not a snapshot encode: undo is pushed
  only after the checks pass.
- **Two bags that overlap the same item can block each other.** Rotating or moving either
  one may be refused while the *other* is what would actually cover the cell — the error
  names the item, and the UI (Stage 09) has to say so in Thai. This is a real usability
  consequence of “refuse the whole command”, accepted deliberately.
- **`BackpackEditor` is not thread-safe** and is not meant to be: it is edited at Camp,
  in the presentation frame, while the battle is a separate fixed-tick owner.
- **`Infrastructure` now references `Contracts`** (it already did for the checkpoint
  probe) — the ADR 0007 direction, unchanged: `Contracts` holds the wire DTOs, the
  adapter lives in Infrastructure, and Domain never sees either.
