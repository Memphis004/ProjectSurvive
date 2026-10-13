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

## Naming rules

- `PascalCase` for types/commands/events, `camelCase` for locals.
- Commands are verbs: `PurchaseItem`, `SellItem`, `RefreshShop`, `PlaceBag`,
  `MoveItem`, `RotateItem`, `MergeItems`, `StartFloor`.
- Events are past-tense facts: `WalletChanged`, `InventoryChanged`,
  `ShopStockChanged`, `LoadoutChanged`, `FloorCompleted`, `RunCompleted`.
- Thai text never appears in identifiers — only in `data/localization/*.csv`.
