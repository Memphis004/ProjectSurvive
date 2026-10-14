# Glossary — Thai term ↔ code name

> **Rule (AGENTS #5):** identifiers in code are **English**, taken from this
> table. Player-facing text comes from localization only, never from these
> names. Add a row here *before* introducing a new domain term.

Terminology is a **new fantasy design inspired by Thai folk motifs**; spell and
ritual names are not presented as real practices (AGENTS #13).

## Core concepts

| Thai | Code name | Kind | Notes |
| --- | --- | --- | --- |
| พ่อหมอ | `Mor` | Player class/entity | The single playable class |
| ย่าม | `Bag` | Domain aggregate | The placeable bag piece |
| ย่ามสองชั้น | `Bag` on `Board` | System | Bag must be placed on the Board first |
| กระดาน / บอร์ด | `Board` | Domain | Max placement area, 9×7 default |
| ช่องใช้งาน | `ActiveSlot` | Domain | Board cell covered by a bag |
| ศาลาพัก | `Camp` | Scene/scope | The only place the bag can be edited |
| หีบ (หีบไม้สัก) | `Chest` | Domain | 10 slots, no bonus, no attacking |
| เบี้ย | `Bia` | Currency/Domain | Starting 15 |
| ไอเทม / สินค้า | `Item` | Domain | ~14 items, 3 tiers |
| อาวุธอาคม | `ArcaneWeapon` | Item category | Auto-cast on cooldown, costs MP |
| เครื่องราง | `Charm` | Item category | Passive buff |
| ตำราคาถา | `SpellTome` | Item category | Boosts same-row arcane weapons |
| ของเซ่น | `Offering` | Item category | Limited-use consumable |
| ภูติรับใช้ | `Familiar` | Item category | Follows and helps fight |
| ตะกรุด | `Takrut` | Item | Amulet |
| ผ้ายันต์ | `YantCloth` | Item | Talisman cloth |
| คอมโบ | `Combo` | Domain system | Orthogonal adjacency only |
| synergy | `Synergy` | Data table `synergy` | Combo rules |
| รวมไอเทม | `Merge` | Command | Same id + tier → next tier, max 3 |
| ถวายคืนศาล | `SellBack` | Command | 50% of base price, rounded down |
| เสี่ยงเซียมซี | `RefreshShop` | Command | Randomize shop items |
| คืนสภาพ (ย่าม) | `ResetBag` | Command | Revert bag to entry state |
| ล็อกย่าม | `LockBag` | Command | Prevent accidental drags |
| น้ำหนักย่าม | `BagWeight` | Stat | Heavier → slower movement |
| รัศมีดูดของ | `PickupRadius` | Stat | Default 1.5 |
| สมาธิ | `Focus` | Stat | MP regen, 5 MP/s |
| พลังอาคม | `MP` / `ArcanePower` | Stat | Default 50 |
| ออกเดินทาง | `Depart` / `StartFloor` | Command | Requires ≥1 arcane weapon active |
| ผ่านชั้นแล้ว | `FloorCompleted` | Event | Reward handled once |
| จบรอบ | `RunCompleted` | Event | Run finished |
| ชั้น | `Floor` | Data table `floor` | 5 in prototype (4 + boss) |
| wave | `Wave` | Data table `wave` | Spawn schedule |
| ค่าคงที่ | `Constant` | Data table `constant` | Tuning constants |

## Tiers, elements, combat

| Thai | Code name | Notes |
| --- | --- | --- |
| ธรรมดา | `Common` | Tier 1 |
| ปลุกเสก | `Consecrated` | Tier 2 |
| มหาเวท | `GrandArcane` | Tier 3 (max) |
| ธาตุไฟ | `Fire` | Burn DOT |
| คุณไสย | `Curse` | Black-magic element |
| เสื่อมพลัง | debuff | Curse near holy amulet weakens both by 30% |
| telegraph | `Telegraph` | Warning marker before strong attacks |
| ดาเมจ | `Damage` | Never computed in a View |
| cooldown | `Cooldown` | Has a minimum in `constant` |
| fixed tick 30 Hz | `FixedTicker` | Battle simulation rate |
| สายสุ่ม | `RandomStreamKind` | `Shop` / `Battle` / `Visual` — independent random streams |
| แหล่งสุ่ม | `IRandomSource` | The only randomness port Core may use |
| เครื่องสุ่ม | `Xoshiro128` | xoshiro128++ generator implementing `IRandomSource` |
| ชุดสายสุ่มของรอบ | `RandomStreams` | Derives the three streams from one run seed |
| นาฬิกา | `IClock` / `SystemClock` | Monotonic time port (Core) and its real implementation (Infrastructure) |
| ผลลัพธ์แบบมีชนิด | `Result` / `Result<T>` / `Error` | Typed command outcome; `Error.Code` is a machine identifier, never player text |

## Battle contract (Stage 02b)

| Thai | Code name | Notes |
| --- | --- | --- |
| สนามรบ (หนึ่งชั้น) | `IBattleSession` | one running battle, driven tick by tick |
| วงจรการต่อสู้ | `BattlePhase` | `Running` / `Won` / `Lost` / `Aborted` (1 reserved) |
| เหตุผลจบชั้น | `BattleEndReason` | `SurvivedAllWaves` / `BossDefeated` / `PlayerDied` / `Timeout` / `Surrendered` |
| ตัวสร้างสนามรบ | `IBattleSessionFactory` | creates a session from `BattleSetup`; config via constructor |
| ตั้งค่าก่อนเข้าชั้น | `BattleSetup` | seed, floor, `CurrentHp`, loadout, dev flags |
| ผลของชั้น | `BattleResult` | outcome, reason, `FinalHp`, kills, remaining consumables |
| คำสั่งในสนามรบ | `BattleCommand` | surrender + dev cheats (typed `CommandResult`) |
| อินพุตผู้เล่น | `PlayerInputFrame` | integer movement per tick (`FromAxis` normalises) |
| เฟรมเรนเดอร์ | `IBattleFrame` / `RenderEntity` | per-tick presentation view |
| HUD | `HudState` | HP/MP/currency/kills/timer (no level/XP) |
| เหตุการณ์ต่อสู้ | `BattleEvent` / `BattleEventType` | **presentation only** — never decide rules from these |
| วงแหวนเหตุการณ์ | `BattleEventRing` | fixed-capacity multi-reader stream, reports `dropped` |
| ชุดรบที่แก้แล้ว | `CombatLoadoutSnapshot` | resolved build handed to the battle |
| บันทึกเล่นซ้ำชั้น | `BattleReplay` | battle-level replay (`ReplayLog` = run-level) |

## Enemies (Thai ghosts)

| Thai | Code name | Role |
| --- | --- | --- |
| วิญญาณเร่ร่อน | `WanderingSpirit` | Filler, charges straight at the player |
| กระสือ | `Krasue` | Fast, hit-and-run drain |
| นางตานี | `NangTani` | Elite, area control under a banana tree |
| ปอบ | `Pob` | Floor-5 boss, 2 phases |
| กระหัง | `Krathang` | *Post-prototype*: armored flyer, deflects frontal shots |
| ผีนางรำ | `NangRam` | *Post-prototype*: rhythm dancer |
| นางนาค (แม่นาค) | `NangNak` | *Post-prototype*: 3-phase boss |

## Items (prototype set)

| Thai | Code name | Category |
| --- | --- | --- |
| ย่ามผ้าดิบ | `RawCottonBag` | Bag (3×3) |
| ถุงผ้ายันต์ | `YantClothPouch` | Bag (1×3) |
| ตะกร้าหวายรูปตัว L | `WickerBasketL` | Bag (L shape) |
| ยันต์เพลิงกาลี | `KaliFlameYant` | ArcaneWeapon (starting) |
| ตะกรุดโทน | `TakrutTone` | ArcaneWeapon |
| มีดหมอ | `MorKnife` | ArcaneWeapon |
| สายสิญจน์ | `SaiSinThread` | ArcaneWeapon |
| พระเครื่อง | `PhraAmulet` | Charm |
| ผ้ายันต์คาดเอว | `WaistYant` | Charm |
| เขี้ยวเสือ | `TigerFang` | Charm |
| ตำราพระเวท | `SpellTome` (2×2) | SpellTome |
| น้ำแดง | `RedSoda` | Offering |
| ไข่ต้ม | `BoiledEgg` | Offering |
| กุมารทอง | `KumanThong` (2×1) | Familiar |

## Item tags (the `synergy` interface, Stage 04)

A tag is a code identifier that lives in `data/tables/item.csv` and is matched by
`data/tables/synergy.csv`. The full vocabulary, including which synergy reads each
tag, is in [`systems/data-schema.md`](systems/data-schema.md#tag-vocabulary).

| Thai | Tag | Notes |
| --- | --- | --- |
| อาวุธอาคม (ทุกชิ้น) | `weapon` | matched by the tome's same-row rule |
| ตำราคาถา | `tome` | the tome's own tag |
| ตะกรุด | `takrut` | |
| ผ้ายันต์ / ยันต์ | `yant` | carried by the yant cloth and the waist yant |
| คุณไสย | `curse` | carried by the familiar; the คุณไสย piece |
| ของศักดิ์สิทธิ์ | `holy` | thread, amulet, waist yant, tiger fang |
| ของเซ่น | `offering` | |
| ภูติรับใช้ | `familiar` | |
| ธาตุไฟ | `fire` | flavour today; nothing matches it yet |
| มีดหมอ | `knife` | flavour |
| สายสิญจน์ | `thread` | flavour |
| พระเครื่อง | `amulet` | flavour |
| เขี้ยวเสือ | `fang` | flavour |

## Bag rules (Stage 05)

| Thai | Code name | Kind | Notes |
| --- | --- | --- | --- |
| ชิ้นส่วน (ย่ามหรือไอเทม) | `Piece` | Domain | `PieceKind.Bag` / `PieceKind.Item` |
| ชิ้นส่วนย่ามที่ถือครอง | `BagPiece` / `BagPieceInstance` | Domain | Opens `ActiveSlot`s; no bonus of its own |
| ชิ้นไอเทมที่ถือครอง | `ItemInstance` | Domain | Must be fully covered by bags |
| รหัสถาวรของชิ้น | `DefId` | Identity | `item.mor_knife`, `bag.raw_cotton_bag` |
| รหัสชิ้นในรอบนี้ | `InstanceId` | Identity | Unique, never reused, starts at 1 |
| ที่จับชิ้นของ | `PieceRef` | Identity | `PieceKind` + `InstanceId`; `Move`/`Merge`/`Recycle` address by this |
| รูปทรงการวาง | `Footprint` | Domain | A *set* of `CellOffset`; authored as a picture (`"XX.|.XX"`) |
| การหมุน | `Rotation` | Domain | `Degrees0/90/180/270`, quarter turns clockwise |
| ย่ามทับกัน | `BagOverlap` | Error | `inventory.bag_overlap` |
| ไอเทมหลุดพื้นที่ใช้งาน | `ItemStranded` | Error | `inventory.item_stranded` — refuses the whole command |
| ย้ายชิ้นของ | `Move` | Command | Chest ⇄ board, and within the board |
| หมุนชิ้นของ | `Rotate` | Command | One quarter turn in place (board only) |
| เก็บของเข้าหีบ | `TransferToChest` | Command | Refused when the chest is full |
| ถวายคืนศาล (ทิ้งของ) | `Recycle` | Command | `floor(base price × sell_back_percent / 100)` |
| ย้อนกลับ | `Undo` | Command | Restores the previous snapshot |
| สแนปช็อตย่าม | `BackpackSnapshot` | Data | The undo/checkpoint unit; MessagePack bytes via `IBackpackSnapshotCodec` |
| ตัวแก้ไขย่าม | `BackpackEditor` | Domain | Owns every command; `Backpack` owns state |
| โบนัสพื้นที่ย่าม | `BagArea` | synergy kind | Fires for any item sitting on a bag |
| อยู่ในแถว/คอลัมน์เดียวกัน | `SameRow` / `SameColumn` | synergy kind | Two halves of the GDD's “แถวเดียวกัน” (ADR 0015) |
| กำลังเสื่อม | `Debuff` | synergy stat | Applies to **both** pieces |
| ความเร็วโจมตี | `AttackSpeed` | synergy stat | A positive percent *shortens* the cooldown |
| ลดความเร็วจากน้ำหนัก | `WeightPenalty` | Stat | Linear slope, capped; chest pieces weigh nothing |
| ตัวประเมินคอมโบ | `SynergyEvaluator` | Domain | One pass; counts distinct partners, capped by `max_stacks` |
| ตัวแปลงย่ามเป็นชุดรบ | `LoadoutResolver` | Domain | Produces the Battle v1 `CombatLoadoutSnapshot`, cached on `Backpack.Revision` |

## Scenes / flow

| Thai | Code name | Notes |
| --- | --- | --- |
| ฉาก | `Scene` | Lifetime scope |
| หน้าแรก / จอชื่อเกม | `Title` | Scene |
| ศาลาพัก | `Camp` | Scene |
| สนามรบ | `Battle` | Scene |
| ผลรอบ | `Result` | Scene, ends the run |
| รอบเล่น | `Run` | Lifetime scope |
| บันทึก | `RunCheckpoint` | Saved only at Camp |

## Error codes

`Error.Code` values are stable lowercase identifiers, `noun.problem`, and the UI
resolves them through localization as `error.<code>` (AGENTS.md rule 5 — no
player-facing text in Core). Examples: `bag.overlap`, `shop.insufficient_bia`,
`item.unknown_shape`, `floor.invalid`. Renaming a code is an API change: update
the localization key in the same commit.

The Stage 05 inventory codes are `inventory.` + `unknown_piece`, `wrong_zone`,
`out_of_board`, `bag_overlap`, `item_not_covered`, `item_overlap`, `item_stranded`,
`chest_full`, `locked`, `nothing_to_undo`, `merge_mismatch`, `merge_not_item`,
`merge_max_tier`, `not_on_board` (`Domain/Inventory/Identity.cs`).

## Naming rules

- `PascalCase` for types/commands/events, `camelCase` for locals.
- Commands are verbs. Landed so far: `Move`, `Rotate`, `TransferToChest`, `Merge`,
  `Recycle`, `Undo`, `Reset`, `SetLocked` (Stage 05) — the method names are the command
  names, so the glossary no longer lists invented spellings (`PlaceBag`, `MoveItem`).
  Still to come: `PurchaseItem`, `SellItem`, `RefreshShop`, `StartFloor`.
- Events are past-tense facts: `WalletChanged`, `InventoryChanged`,
  `ShopStockChanged`, `LoadoutChanged`, `FloorCompleted`, `RunCompleted`.
- Thai text never appears in identifiers — only in `data/localization/*.csv`.
