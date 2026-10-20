# Run, wallet, shop and scene flow (Stage 06)

A **run** is the unit of play: one seed, one purse, one bag, five floors. This note is the
map of that machinery — the phase machine, the camp shop, the wallet, the settlement, and
the replay log that can rebuild the whole thing from a seed plus a command list.

Code lives in `lib/Runtime/Domain/Run/` and `lib/Runtime/Domain/Shop/` (rules) and
`lib/Runtime/Application/Run/` (`RunSession`, `SceneFlow`, `ReplayLog`). The rules are pure:
no `UnityEngine`, no R3, no VContainer and no serializer in `Domain`, no `UnityEngine.Random`
anywhere (all randomness goes through `IRandomSource`), and the direction is enforced by
asmdef ([ADR 0004](../../docs/adr/0004-dotnet-layer-enforcement.md)).

## 1. The phase machine

`RunFlowState` (Domain, values fixed because the phase is part of the state hash) and
`SceneId` (the loadable scene) are two enums with **one explicit mapping**,
`SceneFlowMap.SceneOf`:

| `RunFlowState` | Value | `SceneId` | Scene |
| --- | --- | --- | --- |
| `Title` | 0 | `Title` | `Title.unity` |
| `Camp` | 1 | `Camp` | ศาลาพัก — Stage 09 |
| `Battle` | 2 | `Battle` | the arena — Stage 08 |
| `Result` | 3 | `Result` | the summary — Stage 10 |

```
Title --StartRun--> Camp --StartFloor--> Battle --ApplyBattleResult(win, floors left)--> Camp
                       ^                                                              |
                       +--------------------- next floor ------------------------------+
                                          Battle --ApplyBattleResult(loss, or last floor)--> Result --ReturnToTitle--> Title
```

Camp is the **only** phase in which money, the bag or the shop may move. That is one guard,
`RequireCamp()` / an explicit phase check, shared by every camp command; anything else
returns `run.wrong_state` and changes nothing.

The mapping is a function, not a convention: `SceneFlowMap.SceneOf` throws
`ArgumentOutOfRangeException` for an unknown state, so a new phase cannot silently inherit
whatever scene happens to share its number.

## 2. The wallet

`Wallet` is run-scoped and single-currency (เบี้ย). It starts at `economy.start_bia` when the
run starts and dies with the run.

- `TrySpend(amount)` is **atomic**: either the whole amount leaves the purse or the balance is
  untouched. Failures carry `wallet.insufficient_bia` (`ErrorKind.Insufficient`, or
  `ErrorKind.Validation` for a negative amount).
- `Credit(amount)` cannot fail, and a negative amount is ignored rather than turning into a
  silent debt.
- `CanAfford(amount)` is the read-only question a command asks *before* it does anything.

No arithmetic on money exists outside `Wallet`, which is why a Presenter can render a price
without ever computing one.

## 3. The shop

`ShopStock` is created **once**, in the run's constructor, and lives for the whole run. What
changes per camp entry is its contents, not the object: `RunSession.EnterCamp()` calls
`ShopStock.BeginCamp()` — which asks every slot to `ResetForCamp()` (empty the offer, clear
**both** flags, so a rent and a pin belong to the sala they happened in) and sets
`RefreshCount` back to 0 — and then immediately rolls. `OpenShop()` is a **read**: re-opening
the shop in the same camp returns the same `ShopStock` instance and consumes no randomness
("เปิดร้านซ้ำสินค้าเดิม").

> **Stage 06b.** `BeginCamp()` used to call `ShopSlot.Clear()`, which by design leaves
> `IsPurchased` alone (`MarkPurchased()` sets the flag and then clears the offer). The rent
> therefore survived into every later sala of the run and `Roll` skipped the slot forever —
> the player's *"ซื้อแล้วช่องนั้นว่างตลอดในศาลาถัด ๆ ไป"*. The two intents are now separate
> methods: `ResetForCamp()` (a new sala: offer + both flags) and `Clear()` (the offer only,
> what a re-roll and a purchase do).

### 3.1 Rolling — `ShopRoller`

`Eligible(floorIndex)` is the pool filter: a `shop_pool` row counts when `weight > 0` and
`floor_min <= floorIndex <= floor_max`. `Roll(stock, floorIndex, rng)` then:

1. walks the panel's slots and skips any slot where `CanBeRerolled` is false — after
   [ADR 0026](../../docs/adr/0026-a-paid-reroll-refills-a-rented-slot.md) that is the **pinned**
   (`IsLocked`) slot alone; every other slot, a **rented** one included, is drawn over;
2. calls `ShopSlot.BeginReroll()` on it first — the offer *and* the rent flag go, so a shelf with no
   candidate left to draw is simply empty, never empty-and-rented;
3. draws one candidate by weight **without replacement**, so one roll never offers the same
   piece twice;
4. resolves the row to an id and a price through `TryResolve` — the `bag` table first, then
   the `item` table — and writes both onto the slot;
5. returns how many slots it filled (`RefreshOutcome.Filled`).

Two edges are deliberate. If the remaining candidates run out before the panel is full, the
leftover slots are **left empty**, never duplicated. And the price is copied onto the slot at
roll time rather than looked up again at purchase time, so what the player was shown is what
they pay even if the data changes underneath them at runtime.

There is also an explicit-pool overload, `Roll(stock, IReadOnlyList<ShopPoolEntryConfig>, rng)`,
so a caller (or a test) can draw from a subset — and so the "fewer offers than slots" rule is
reachable with shipped data, which never produces it.

### 3.2 One slot — `ShopSlot`

| Member | Meaning |
| --- | --- |
| `OfferedId` / `Kind` / `Price` | what is on offer, and at which table price |
| `IsEmpty` | holds nothing to rent |
| `IsPurchased` | was rented **in this camp**; the money is spent and a second rent is refused — cleared by a paid re-roll (`BeginReroll()`) or by the next camp entry (`ResetForCamp()`) |
| `IsLocked` | pinned so a re-roll keeps it ("ล็อกสินค้าที่อยากเก็บไว้ก่อนสุ่มได้") |
| `CanBeRerolled` | `!IsLocked` — the single rule `Roll` asks ([ADR 0026](../../docs/adr/0026-a-paid-reroll-refills-a-rented-slot.md)) |

**Renting empties the slot.** `MarkPurchased()` sets `IsPurchased` *and* clears the offer, so
a rented slot shows as rented and holds nothing more. What was bought is carried by the
`PurchaseOutcome` and by the run's inventory, not by the panel. `ShopSlotState` is the plain
copy of a slot (index, id, kind, price, both flags) that the state hash and a checkpoint read.

> **Stage 06c.** ADR 0016 §2 made `CanBeRerolled` `!IsLocked && !IsPurchased`, so a paid re-roll
> skipped a rented shelf and the sala kept a hole in it for the rest of the visit. "ไม่เติมสินค้าให้
> ฟรี" is about the **rent** (the piece is taken for the price paid); the re-roll is itself a paid
> command, so it now refills the shelf and clears the flag. A shelf that has not been re-rolled is
> unchanged: rented, empty, and a second rent refused. The re-roll price, the shop stream and the
> atomicity of `RefreshShop` are untouched
> ([ADR 0026](../../docs/adr/0026-a-paid-reroll-refills-a-rented-slot.md)).

### 3.3 The re-roll price ("เสี่ยงเซียมซี")

`ShopStock.RefreshCost(economy)` = `economy.refresh_base_cost + economy.refresh_cost_increment
× RefreshCount`, evaluated **before** the count is incremented. In one camp the price
therefore climbs 5, 7, 9, … (shipped data), and `BeginCamp()` resets it. `MarkRefreshed()` is
called only *after* the wallet has been charged.

### 3.4 Shop failure codes

`shop.unknown_slot`, `shop.slot_empty`, `shop.slot_purchased`, `shop.unknown_piece`. A chest
too full to accept a purchase is reported as **`inventory.chest_full`** rather than a second
code of its own: it is the same condition and the UI must show the same reason.

## 4. Commands — `RunSession`

`RunSession` (Application) is the **only** command surface of a run. A Presenter calls it and
never recomputes a price, a heal or a combo.

| Command | Phase | Rule |
| --- | --- | --- |
| `StartRun()` | Title | Validates the whole starting kit *before* mutating, lays it out, then enters Camp |
| `OpenShop()` | Camp | Read-only; returns the same stock every time in this camp |
| `Purchase(slotIndex)` | Camp | Rent one offer into the **chest** |
| `Sell(piece)` | Camp | `Editor.Recycle` for `economy.sell_back_percent` of the base price, then credits the purse |
| `RefreshShop()` | Camp | Charge the re-roll price, then re-roll every non-pinned slot — a rented one included |
| `SetSlotLocked(slotIndex, locked)` | Camp | Pin/unpin one slot; a rented slot is refused (`shop.slot_purchased`) and an empty one too (`shop.slot_empty`) — there is nothing to pin |
| `StartFloor()` | Camp | The departure gate; returns the `BattleSetup` |
| `ApplyBattleResult(result)` | Battle | Settle the floor once |
| `MovePiece` / `RotatePiece` / `TransferToChest` / `MergeItems` / `Undo` / `Reset` | Camp | Delegated to `BackpackEditor` (Stage 05), then recorded |
| `ReturnToTitle()` | Result | Ends the run's screen |

Every command **validates completely, then mutates**. The two orderings that matter:

- **`Purchase`**: phase → slot in range → not already rented → not empty → the id resolves →
  `CanAfford` → **chest not full** → *pay* → take the piece. Paying before taking is safe
  because a failed `AddBag`/`AddItem` is refunded in the same call, so a purchase is never
  half-applied. The chest is checked *before* the money moves, which is the mechanical form of
  "หีบเต็มต้องไม่หักเงิน".
- **`RefreshShop`**: phase → compute the price → `TrySpend` → *only then* `MarkRefreshed()` and
  roll. A purse that cannot pay leaves the stock, the counter and the wallet exactly as they
  were ("เงินไม่พอ … ไม่เปลี่ยน state").

The purchase outcome is built from values captured **before** the commit (the offer is about
to be taken off the slot), and each accepted command appends one `RunCommand` to the log and
raises the matching `RunEvent`.

### 4.1 The departure gate

`StartFloor()` refuses unless the run is at Camp and a floor is left (`run.no_floor_left`),
then resolves the loadout and refuses with **`run.no_arcane_weapon`** when
`loadout.Weapons.Count == 0` — the reason code Stage 09 shows on the disabled button
("ออกเดินทางไม่ได้ถ้าไม่มีอาวุธอาคมบนช่องใช้งานอย่างน้อย 1 ชิ้น").

On success it draws **one** seed from the run's battle stream, builds a `BattleSetup`
(`Seed`, `StageId`, `DifficultyId`, `FloorIndex`, `IsBossFloor`, `CurrentHp`, `Loadout`,
`DevCheatsAllowed = false`), remembers it as the pending setup together with its
**settlement id**, locks the bag (`Editor.SetLocked(true)` — "ล็อกย่ามระหว่าง Battle") and
moves to `Battle`. The bag is unlocked again by the settlement.

`RunSession` never creates a battle session. Only `SceneFlow` does, through the
`IBattleSessionFactory` it was constructed with, so the fake of Stage 02b and the EcsLite
simulation of Stage 07 are interchangeable with no call site moving (see
[ADR 0007](../../docs/adr/0007-battle-contract-and-asmdef-placement.md)). An architecture test
forbids naming the fake outside `Application/Battle/Fake/`.

### 4.2 Settlement — `ApplyBattleResult` ("CompleteFloor")

One floor, one payout. The guard is the **phase plus the pending setup**: a settlement clears
both, so a second call in the same run returns `run.not_in_battle` rather than paying twice.
The id is a *report* of which settlement was paid (`FloorSettlement.SettlementId`,
`RunState.LastSettlementId`), never the guard itself — see
[ADR 0016](../../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md).

The order, with nothing paid until the whole change is known to be applicable:

1. Validate the consumable bookkeeping. `BattleResult.RemainingConsumables` is aligned with
   the loadout the floor started from, one entry per `CombatLoadoutSnapshot.Consumables` entry;
   a **different length is a desync** and refuses the whole settlement with
   `inventory.not_enough_pieces`. An empty list means the floor reported nothing and is
   accepted. When the length matches, each entry is checked with `Editor.CanConsume`;
   the value is **remaining uses** (Stage 11a), so the pieces used are
   `ceil((Count × UsesPerItem − remainingUses) / UsesPerItem)` and a partly used piece is
   still removed from the bag.
2. Credit the floor's takings: `CurrencyEarned` (the collected pickups' `enemy.drop_value` sum)
   on any outcome, plus `economy.clear_reward_bia` **on a win only** ([ADR 0025](../../docs/adr/0025-bia-from-battle.md)).
3. `Hp = clamp(result.FinalHp, 0, MaxHp)`; remove the used offerings (`Editor.Consume`).
4. On a win: `FloorsCleared++`, then heal `(MaxHp × economy.heal_percent_on_clear) / 100`,
   **capped at the remaining headroom** (`MaxHp - Hp`), and add it.
5. Record `LastSettlementId`, clear the pending setup, unlock the bag, then either move to
   `Result` (a defeat, or the last floor cleared) or increment the floor cursor and
   `EnterCamp()` — which empties and re-rolls the shop for the new camp.

The returned `FloorSettlement` reports the settlement id, the floor, the outcome and reason,
the bia credited, the HP healed, the HP after, and whether the run ended.

## 5. The starting kit — `RunDefaults`

`RunDefaults` holds only **ids and placement**: the difficulty (`difficulty.normal`), the bag
(`bag.raw_cotton_bag` at 0,0) and the arcane weapon (`item.kali_flame_yant` at 0,0, i.e. inside
that bag). Every *number* of the start stays in `data/`: the bia from `economy.start_bia` (15)
and the board size from `constant`. No table describes a starting kit, so this is recorded as
a deliberate gap rather than invented as columns.

`StartRun` refuses a kit it cannot lay out — a missing id, an unknown weapon, a chest with
fewer than the two slots the two pieces need, a bag that does not fit the board, or a weapon
whose every cell is not inside the starting bag's own footprint — all **before** the first
mutation, so the run is never left half-equipped.

## 6. Commands, events and state

**`RunCommandKind`** is the replay log's vocabulary: `StartRun` 0, `Purchase` 1, `Sell` 2,
`SetSlotLocked` 3, `RefreshShop` 4, `StartFloor` 5, `MovePiece` 6, `RotatePiece` 7,
`TransferToChest` 8, `MergeItems` 9, `Undo` 10, `Reset` 11. Each `RunCommand` carries every
input that does not come from the seed (slot index, flag, piece handle, board origin,
rotation). The numbers are part of the log's format, so a new kind may be **appended but not
inserted**. A settlement is deliberately *not* a command: a floor's result is reproduced by
replaying its battle recording, never by trusting a stored `BattleResult`.

**`RunEventKind`** is presentation only — `WalletChanged` 0, `InventoryChanged` 1,
`ShopStockChanged` 2, `LoadoutChanged` 3, `FloorCompleted` 4, `RunCompleted` 5,
`FlowChanged` 6 — buffered on the run and dropped by `ClearEvents()`. A rule reads state, never
an event stream.

**`RunState`** is the plain, copyable picture the replay comparison uses: the phase, floor,
HP/max HP, bia, floors cleared, last settlement id, refresh count, backpack revision and next
instance id, every `ShopSlotState`, and every `PieceSnapshot` (kind, instance id, def id, tier,
zone, origin, rotation, chest slot). `ComputeHash()` is a 32-bit FNV-style hash over exactly
those fields, mixed in a fixed order. It is a **dev/verification** tool, not a save format —
Stage 12 builds its checkpoint from the DTOs, because this shape carries no schema version.

## 7. Replay: two levels, on purpose

| Level | Type | Carries | Replayed by |
| --- | --- | --- | --- |
| Battle (Stage 02b) | `BattleReplay` | one floor's `BattleSetup`, one input per tick, its ticked commands | `Replay(factory)` → the same `IBattleSession.ComputeStateHash()` |
| Run (Stage 06) | `ReplayLog` | the run seed, the accepted `RunCommand`s, one embedded `BattleReplay` per started floor | `Replay(config, codec, factory)` → the same `RunState` hash |

`ReplayLog` does **not** reimplement the battle half. It only decides which recording belongs
to which floor and in what order: for each `StartFloor` command it replays the next recording
through the factory it was handed, takes that session's `BattleResult`, and settles it into the
replayed run — the same two steps `SceneFlow` performs live. A log that no longer reproduces
its own run throws `InvalidOperationException` naming the diverging command; that is a bug in
the recorder, not a player-facing failure. The replayed run is constructed with
`recording: false`, so it cannot grow a second copy of the log.

The reason a `BattleResult` is never *recorded* is the whole point of the split: a stored
outcome can disagree with the simulation, while a recording cannot — see
[ADR 0007](../../docs/adr/0007-battle-contract-and-asmdef-placement.md).

`ReplayLog` is a development and verification path, so it is allowed to allocate (unlike
`IBattleSession.Step`).

## 8. Randomness: two streams, never one

`RunSession` derives its streams from the run seed through `RandomStreams`:

- `_streams.Shop` — every shop roll and re-roll;
- `_streams.Battle` — exactly **one** draw per departure, used as `BattleSetup.Seed`.

Keeping them apart is what makes "seed และ state เดิมให้สินค้าเดิม" and "refresh เปลี่ยนสถานะ
RNG" both true without one side moving the other: a re-roll cannot shift a floor's battle
seed, and a battle cannot shift the shop. Guaranteed within one runtime only, never across
browsers or CPUs (AGENTS rule 6).

## 9. Who owns what

| File | Owns |
| --- | --- |
| `Domain/Run/Wallet.cs` | The purse; the only place bia changes |
| `Domain/Run/RunFlowState.cs`, `SceneId.cs` | The phase enum and the phase → scene mapping |
| `Domain/Run/RunCommand.cs` | The recorded command vocabulary and its inputs |
| `Domain/Run/RunEvent.cs` | The presentation event kinds |
| `Domain/Run/RunState.cs` | The hashable picture of a run |
| `Domain/Run/RunOutcomes.cs`, `FloorSettlement.cs` | Typed results: `PurchaseOutcome`, `RefreshOutcome`, `FloorSettlement` |
| `Domain/Run/RunDefaults.cs` | The starting kit's ids and placement |
| `Domain/Run/RunError.cs` | `run.*` codes and `wallet.insufficient_bia` |
| `Domain/Shop/ShopStock.cs` | Slots, the re-roll counter, the re-roll price, camp reset |
| `Domain/Shop/ShopSlot.cs` | One slot and the flags a re-roll reads |
| `Domain/Shop/ShopRoller.cs` | The weighted draw and the id → price resolution |
| `Domain/Shop/ShopError.cs` | `shop.*` codes |
| `Application/Run/RunSession.cs` | The whole command surface and the atomicity of each command |
| `Application/Run/SceneFlow.cs` | Camp ↔ Battle driving; the only place a session is created |
| `Application/Run/ReplayLog.cs` | Seed + commands + one recording per floor; the replay itself |

## 10. Error codes

- `run.` — `wrong_state`, `no_arcane_weapon`, `not_in_battle`, `battle_running`,
  `unknown_piece` are the refusals a caller can trigger; `no_floor_left` (a guard on the floor
  cursor) and `unknown_floor` (a degenerate floor table) are named **preconditions** and say so
  in `RunError` rather than pretending to be player-facing outcomes
- `wallet.` — `insufficient_bia`
- `shop.` — `unknown_slot`, `slot_empty` (empty when rented, or after a paid re-roll found no
  candidate for it), `slot_purchased`, `unknown_piece`
- plus `inventory.chest_full` and `inventory.not_enough_pieces` reused from Stage 05.

All of them are API: player-facing text is localization keyed `error.<code>`, and renaming a
code is a change to the UI's message table in the same commit.

## 11. Where the evidence is

- `lib/Tests~/Core.Tests/Run/` — 42 xUnit cases over the **real** generated tables (no stub
  config): `RunFixture.cs` (the runs and the scripted battle factory, no cases of its own),
  `RunEconomyTests.cs` (8), `ShopStockTests.cs` (14), `RunFlowTests.cs` (14), `ReplayTests.cs`
  (6). They cover the seed →
  same offers case, re-opening the shop, the re-roll price ladder and the stream split, the
  "full chest / short purse changes nothing" cases, buying one slot twice, a repeated battle
  result paying once, out-of-order transitions, a run with no arcane weapon, a scripted
  five-floor run that wins every floor and one that times out against the boss, and — for
  both of them — a replay from the log that lands on the same `RunState` hash.
- `UnityProject/Assets/Main/Tests/EditMode/RunFlowEditModeTests.cs` — the same acceptance
  through the Unity-compiled assemblies over the **shipped** `Resources/Config` tables: the
  scripted five-floor run, the log replay, shop atomicity, and the departure refusal reason code.
- Architecture tests: `Presentation_sources_do_not_decide_economy_numbers` and
  `Battle_sessions_are_reached_only_through_their_factory_port`, both in
  `lib/Tests~/Core.Tests/ArchitectureTests.cs`.
