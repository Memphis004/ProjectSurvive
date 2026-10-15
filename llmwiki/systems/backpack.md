# Bag, board and combos (Stage 05; carried-item rule changed in 09f)

The bag is the game's build surface: pieces are bought into a **Chest**, laid out on a
**Board**, and the layout is what turns into the player's combat loadout. This note is the
map of that machinery — what the rules are, which type owns each one, and which mistakes
the data gate already catches.

Code lives in `lib/Runtime/Domain/Inventory/` (rules) and
`lib/Runtime/Infrastructure/Save/` (the snapshot codec). Nothing in this system touches
`UnityEngine`, `Time`, `DateTime` or a serializer — `Domain` is pure, and the dependency
direction is enforced by asmdef
([ADR 0004](../../docs/adr/0004-dotnet-layer-enforcement.md)).

## 1. The two coordinate systems, and the one rule that connects them

| Type | What it is |
| --- | --- |
| `CellOffset(x, y)` | A board cell. `x` is the column (right), `y` is the row (**down**) |
| `Footprint` | A **set** of normalised cells: the shape a piece occupies |
| `Board(9, 7)` | The placement area, sized from `constant.board_width/height` |
| `ActiveSlot` | A board cell a bag piece covers |

`Footprint` is authored in `data/` as a **picture string** (`"XXX|XXX|XXX"`, `"X.|X.|XX"`)
per [ADR 0009](../../docs/adr/0009-footprint-picture-format.md), parsed by
`Footprint.TryParse`, and normalised so the minimum `x`/`y` is 0. It is a *set*, so
`Footprint.FromCells` collapses duplicates and sorts canonically — that is what makes two
equal shapes comparable by value and a rotated shape hashable for a snapshot.

`FootprintRotation.Rotate(shape, rotation)` turns a shape clockwise **on screen**:
`(x, y)` becomes `(-y, x)`, then re-normalises. Four quarter turns return any piece to the
authored shape, and an asymmetric shape gets a *different* cell set at 90°/270° — the
L-tetromino bag (`bag.wicker_basket_l`) exists for exactly that reason.

The re-normalisation moves the turned cell set back to a minimum of `(0, 0)`, and anything
else being turned **with** the shape has to make the same move. That translation is
published by the overload the assembly uses — `Rotate(shape, rotation, out CellOffset shift)`
— and is the only reason a bag's riders stay rigid under a turn
([ADR 0020](../../docs/adr/0020-bag-moves-carry-the-items-on-them.md)).

## 2. Placement ownership (GDD §bag)

Two owner maps live in `Board`: bag owner per cell, item owner per cell. Every placement
question is a read of those maps, and the three failures are three codes:

| Situation | Code |
| --- | --- |
| A footprint cell outside `Board` | `inventory.out_of_board` |
| Two bags share a cell | `inventory.bag_overlap` |
| An item cell no bag covers | `inventory.item_not_covered` |
| Two items share a cell | `inventory.item_overlap` |
| A bag on its way **off the board** still has an item standing on it | `inventory.item_stranded` |
| An item lies across two bags and would lose its cover | `inventory.item_spans_bags` |

**An item may straddle several bags**; it needs *every* one of its cells covered, not one.
What a bag gesture then does depends on which bag the item is standing on
([ADR 0020](../../docs/adr/0020-bag-moves-carry-the-items-on-them.md)):

| The item's cells are … | A bag **move** or **turn** | A bag to the **chest** / **altar** |
| --- | --- | --- |
| all on this one bag | it **rides along**, keeping its place in the bag's frame | refused whole — `item_stranded` |
| split across this bag and another | it stays; refused whole if the move would leave it uncovered — `item_spans_bags` | it stays; refused only if *this* bag was the one covering the cells it keeps |
| not on this bag at all | unaffected | unaffected |

An item can never come along into the chest: a chest piece covers no cell, so a rider there
would be an item outside the build, and the chest has a fixed 10-slot budget.
See [ADR 0014](../../docs/adr/0014-backpack-editing-undo-and-placement-ownership.md) for the
parts of the rule that did not change — refuse whole, never relocate an item silently.

## 3. Identity: `DefId` vs `InstanceId`

`PieceInstance` carries both.

- `DefId` (e.g. `item.mor_knife`) is the permanent, data-owned definition; many instances
  share it.
- `InstanceId` is the run's handle: unique, never reused, handed out by `Backpack`.
- Commands address a piece by `PieceRef(PieceKind, InstanceId)` — a bag id and an item id
  come from one counter, so the kind is part of the handle.

Merge is why the split exists: two instances of the same `DefId` at the same tier become
one at tier + 1 (capped by `constant.max_tier`), and the survivor keeps its exact
placement, so the footprint never changes. A save file that stored only def ids could not
tell a tier-3 weapon from three tier-1 ones.

## 4. The command surface — `BackpackEditor`

`Backpack` owns **state**; `BackpackEditor` owns **commands**. Mutators on `Backpack` are
`internal`, so a caller cannot skip validation: that is what makes “a rejected command
changes nothing” structural rather than a promise.

| Command | Rule |
| --- | --- |
| `Move(piece, origin, rotation)` | Place/move from chest or board; a bag brings the items standing on it along (ADR 0020) |
| `Rotate(piece)` | One clockwise quarter turn in place; a board-only operation, carrying the same riders |
| `TransferToChest(piece)` | Refused when the chest (`economy.chest_slots`, 10) is full, and for a bag an item is standing on |
| `Merge(source, target)` | Same item id **and** same tier → `tier + 1`, max `constant.max_tier`; refuses bags, mismatches and the cap |
| `Recycle(piece)` | Leaves the run; pays `floor(price × economy.sell_back_percent / 100)` of the **tier-1** price |
| `Undo()` | Re-applies the previous snapshot |
| `Reset()` | Returns to the state captured on `MarkCampEntry()` (“คืนสภาพตอนเข้าศาลา”) |
| `SetLocked(bool)` | Freezes every edit **including undo**; never itself undoable |

Every one of them returns `Result` / `Result<T>` (`MergeOutcome`, `RecycleOutcome`) with a
stable `InventoryError.*` code; player-facing text stays in localization as
`error.<code>`.

**Acquisition always lands in the chest** (`AddBag` / `AddItem`), per the GDD's
“ของที่ซื้อเข้าหีบก่อนเสมอ”.

**Undo and Reset are MessagePack snapshots.** Entering Camp encodes the state once; each
accepted command pushes a snapshot (bounded to 64) *after* validation passes. The bytes
come from `IBackpackSnapshotCodec`, implemented in Infrastructure
(`MessagePackBackpackSnapshotCodec` → `BackpackSnapshotDto` in `Contracts`), so the rule
engine never sees a serializer while the bytes are real MessagePack Stage 12 can persist.
The snapshot deliberately does **not** store a shape: it stores the rotation, and the
shape is re-derived from the data table, so a rotation bug cannot desync two copies.

## 5. Weight → movement penalty

`WeightPenalty.Percent(totalWeight, constant)` is linear: **ballast percent per weight
unit**, clamped to the cap. Both numbers are data
(`constant.weight_speed_percent_per_unit` = 5, `constant.max_speed_penalty_percent` = 40),
so the cap is reached at 8.0 weight units, and **chest pieces weigh nothing** (a stored
piece is not carried). The curve and its alternatives are
[ADR 0013](../../docs/adr/0013-weight-speed-penalty-curve.md). Tier does not change weight
— a data-model gap, not a rule.

## 6. Combos — `SynergyEvaluator`

Input: the pieces on the board. Output: `SynergyEvaluation` — per-item
`ItemModifiers` (multipliers around 1) plus the `ActiveSynergy` records a Stage 09 tooltip
will use to highlight connected cells. Nothing here allocates per tick: it runs when the
layout changes, not at 30 Hz.

The rules are the `synergy` table, and `kind` picks the relation:

| `kind` | Meaning |
| --- | --- |
| `AdjacentPair` | 4-direction edge contact (no diagonals) between the two tags |
| `SameRow` | the two pieces share a board **row** |
| `SameColumn` | the two pieces share a board **column** (available; no shipped row uses it — ADR 0015) |
| `BagArea` | the item sits on a bag at all |

Non-negotiable details, each with a test:

1. **Counted once per pair.** A neighbour touching along three cells is *one* neighbour, so
   `AdjacentPair` iterates targets × distinct sources and stops at the rule's `max_stacks`.
   “ติดกันหลายช่องนับครั้งเดียว” and “ซ้อนได้สูงสุด N ชั้น” are the same mechanism.
2. **One pass, no fixed point.** Rules read positions and write multipliers; nothing a rule
   writes can enable another rule, so there is no iteration to diverge and evaluating twice
   gives the same answer.
3. **Direction comes from the data.** `tag_a` provides the condition, `tag_b` receives it —
   except `Debuff`, which the GDD says hits both pieces (“ทั้งสองชิ้นเสื่อมพลัง”), so it is
   applied in both directions.
4. **Same-stat percentages add** before being applied (“โบนัสเปอร์เซ็นต์ชนิดเดียวกันบวกกัน”).
5. **`AttackSpeed` is a negative cooldown delta** — the sign flips in exactly one place
   (`Accumulate`).
6. **Unknown kind is skipped, not thrown on** (the data gate rejects it at generation time).

## 7. Bag → battle: `LoadoutResolver`

`Resolve(backpack)` returns the **Battle v1 contract**
(`ProjectSurvive.Core.Domain.Combat.Battle.CombatLoadoutSnapshot`) and is the *only* bridge
from the bag to the battle. No second loadout model exists.

- `Stats` = `constant` values, with `SpeedPenaltyPercent` from the weight rule already
  folded into `MoveSpeed`.
- `Weapons` (arcane weapons) / `Summons` (familiar) are built from the item's `skill_id`
  with tier and synergy multipliers applied to damage and cooldown.
- `Consumables` (offerings) are aggregated per item id — the contract has no tier on a
  consumable, so N identical offerings is “one entry, N uses”.
- **Cooldown has a floor**: never below `constant.min_cooldown_seconds`, whatever the combo
  bonus says (“cooldown มีค่าต่ำสุด”).
- **Chest contents contribute nothing** to the loadout.
- **Recompute policy**: cached against `Backpack.Revision`, which only an *applied* command
  moves — “คำนวณใหม่เมื่อชุดอุปกรณ์เปลี่ยนเท่านั้น”. `ResolveCount` exposes cache misses so
  the policy is testable.
- **Canonical order**: weapons/summons by board position, consumables by id. No rule reads
  a `Dictionary` in iteration order, so two backpacks holding the same layout resolve to
  identical values (and their snapshots are byte-identical).

## 8. Error codes (all under `inventory.`)

`unknown_piece`, `wrong_zone`, `out_of_board`, `bag_overlap`, `item_not_covered`,
`item_overlap`, `item_spans_bags`, `item_stranded`, `chest_full`, `locked`, `nothing_to_undo`,
`merge_mismatch`, `merge_not_item`, `merge_max_tier`, `not_on_board`.

## 9. Where the evidence is

- `lib/Tests~/Core.Tests/Inventory/` — the rule suite over the **real** generated tables
  (no stub config), including a 10 000-operation randomized run that mixes bag gestures
  with every other command and checks the structural invariants after each one.
- `UnityProject/Assets/Main/Tests/EditMode/BackpackRulesTests.cs` — the same real data
  through the Unity-compiled assemblies (loadout contract, codec, undo/lock, the carried
  item and both refusals).
- `UnityProject/Assets/Main/Tests/PlayMode/CampBagDragPlayModeTests.cs` — the gesture with
  real pointer events, including the rider's ghost drawn where the item really lands
  (Stage 09f).
- `node tools/coverage-report.mjs <cobertura> "Inventory/" "Config/Footprint.cs"` — the
  measurement behind the acceptance number (90.92 % of the backpack rule set).
