> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Items

Every piece in `data/tables/item.csv` (11 items) and `data/tables/bag.csv` (3 bags), in file order. A piece is authored once at the tier its row carries; `data/tables/merge_recipe.csv` takes it to `constant.max_tier` (3). Floors are the `shop_pool` band for the piece, not a promise it appears in every run.

## Contents

- [ยันต์เพลิงกาลี (item.kali_flame_yant)](#item.kali_flame_yant)
- [ตะกรุดโทน (item.takrut_tone)](#item.takrut_tone)
- [มีดหมอ (item.mor_knife)](#item.mor_knife)
- [สายสิญจน์ (item.sai_sin_thread)](#item.sai_sin_thread)
- [พระเครื่อง (item.phra_amulet)](#item.phra_amulet)
- [ผ้ายันต์คาดเอว (item.waist_yant)](#item.waist_yant)
- [เขี้ยวเสือ (item.tiger_fang)](#item.tiger_fang)
- [ตำราพระเวท (item.spell_tome)](#item.spell_tome)
- [น้ำแดง (item.red_soda)](#item.red_soda)
- [ไข่ต้ม (item.boiled_egg)](#item.boiled_egg)
- [กุมารทอง (item.kuman_thong)](#item.kuman_thong)
- [ย่ามผ้าดิบ (bag.raw_cotton_bag)](#bag.raw_cotton_bag)
- [ถุงผ้ายันต์ (bag.yant_cloth_pouch)](#bag.yant_cloth_pouch)
- [ตะกร้าหวายรูปตัว L (bag.wicker_basket_l)](#bag.wicker_basket_l)

## Items

<a id="item.kali_flame_yant"></a>
### item.kali_flame_yant - ยันต์เพลิงกาลี (Kali Flame Yant)

| Field | Value |
| --- | --- |
| id | `item.kali_flame_yant` |
| category | `ArcaneWeapon` |
| tier | 1 (of 3) |
| element | `Fire` |
| price | 10 bia |
| weight | 0.8 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `yant`, `weapon`, `fire` |
| skill | [คาถาเพลิงกาลี](skills.md#skill.fire_bolt) |
| merges | 1->2, 2->3 |
| shop | floor 1-5 (weight 30) |

**Kali Flame Yant.** ยันต์ที่ยิงกระสุนเพลิงใส่ผีที่ใกล้ที่สุด

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ตะกรุดหนุนผ้ายันต์](synergies.md#synergy.takrut_yant_cooldown) - holds `yant` as **tag_b**: `Cooldown` -15%, max 1 stack(s). Partner side: [ตะกรุดโทน](items.md#item.takrut_tone).
- [ตำราหนุนอาคม](synergies.md#synergy.tome_row_damage) - holds `weapon` as **tag_b**: `Damage` +20%, max 2 stack(s). Partner side: [ตำราพระเวท](items.md#item.spell_tome).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades that can be offered for this piece:**

- [คมอาคม](upgrades.md#variant.arcane.keen_edge) - tier 1->2
- [คาถาไว](upgrades.md#variant.arcane.swift_chant) - tier 1->2
- [มนต์ประหยัด](upgrades.md#variant.arcane.frugal_mantra) - tier 1->2
- [ญาณไกล](upgrades.md#variant.arcane.far_sight) - tier 1->2
- [ผนึกเพลิง](upgrades.md#variant.fire.blazing_seal) - tier 1->2
- [คมอาคมชั้นสูง](upgrades.md#variant.arcane.great_keen_edge) - tier 2->3
- [คาถาไวชั้นสูง](upgrades.md#variant.arcane.great_swift_chant) - tier 2->3
- [คาถาคู่](upgrades.md#variant.arcane.twin_bolt) - tier 2->3
- [ทะลุทะลวง](upgrades.md#variant.arcane.deep_pierce) - tier 2->3
- [ผนึกไฟนรก](upgrades.md#variant.fire.inferno_seal) - tier 2->3

<a id="item.takrut_tone"></a>
### item.takrut_tone - ตะกรุดโทน (Takrut Tone)

| Field | Value |
| --- | --- |
| id | `item.takrut_tone` |
| category | `ArcaneWeapon` |
| tier | 1 (of 3) |
| element | `None` |
| price | 9 bia |
| weight | 0.4 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `takrut`, `weapon` |
| skill | [วงตะกรุดกัน](skills.md#skill.takrut_ward) |
| merges | 1->2, 2->3 |
| shop | floor 1-5 (weight 20) |

**Takrut Tone.** ตะกรุดศักดิ์สิทธิ์ที่ปล่อยวงกันรอบตัวพ่อหมอ

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ตะกรุดหนุนผ้ายันต์](synergies.md#synergy.takrut_yant_cooldown) - holds `takrut` as **tag_a**: `Cooldown` -15%, max 1 stack(s). Partner side: [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ผ้ายันต์คาดเอว](items.md#item.waist_yant).
- [ตำราหนุนอาคม](synergies.md#synergy.tome_row_damage) - holds `weapon` as **tag_b**: `Damage` +20%, max 2 stack(s). Partner side: [ตำราพระเวท](items.md#item.spell_tome).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades that can be offered for this piece:**

- [คมอาคม](upgrades.md#variant.arcane.keen_edge) - tier 1->2
- [คาถาไว](upgrades.md#variant.arcane.swift_chant) - tier 1->2
- [มนต์ประหยัด](upgrades.md#variant.arcane.frugal_mantra) - tier 1->2
- [ญาณไกล](upgrades.md#variant.arcane.far_sight) - tier 1->2
- [คมอาคมชั้นสูง](upgrades.md#variant.arcane.great_keen_edge) - tier 2->3
- [คาถาไวชั้นสูง](upgrades.md#variant.arcane.great_swift_chant) - tier 2->3
- [คาถาคู่](upgrades.md#variant.arcane.twin_bolt) - tier 2->3
- [ทะลุทะลวง](upgrades.md#variant.arcane.deep_pierce) - tier 2->3

<a id="item.mor_knife"></a>
### item.mor_knife - มีดหมอ (Mor Knife)

| Field | Value |
| --- | --- |
| id | `item.mor_knife` |
| category | `ArcaneWeapon` |
| tier | 1 (of 3) |
| element | `None` |
| price | 11 bia |
| weight | 0.7 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `knife`, `weapon` |
| skill | [ฟันมีดหมอ](skills.md#skill.knife_thrust) |
| merges | 1->2, 2->3 |
| shop | floor 1-4 (weight 25) |

**Mor Knife.** มีดหมอสำหรับฟันผีระยะประชิด หนักแต่แรง

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ตำราหนุนอาคม](synergies.md#synergy.tome_row_damage) - holds `weapon` as **tag_b**: `Damage` +20%, max 2 stack(s). Partner side: [ตำราพระเวท](items.md#item.spell_tome).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades that can be offered for this piece:**

- [คมอาคม](upgrades.md#variant.arcane.keen_edge) - tier 1->2
- [คาถาไว](upgrades.md#variant.arcane.swift_chant) - tier 1->2
- [มนต์ประหยัด](upgrades.md#variant.arcane.frugal_mantra) - tier 1->2
- [ญาณไกล](upgrades.md#variant.arcane.far_sight) - tier 1->2
- [คมอาคมชั้นสูง](upgrades.md#variant.arcane.great_keen_edge) - tier 2->3
- [คาถาไวชั้นสูง](upgrades.md#variant.arcane.great_swift_chant) - tier 2->3
- [คาถาคู่](upgrades.md#variant.arcane.twin_bolt) - tier 2->3
- [ทะลุทะลวง](upgrades.md#variant.arcane.deep_pierce) - tier 2->3

<a id="item.sai_sin_thread"></a>
### item.sai_sin_thread - สายสิญจน์ (Sai Sin Thread)

| Field | Value |
| --- | --- |
| id | `item.sai_sin_thread` |
| category | `ArcaneWeapon` |
| tier | 1 (of 3) |
| element | `None` |
| price | 8 bia |
| weight | 0.5 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `thread`, `weapon`, `holy` |
| skill | [สายสิญจน์ทะลุ](skills.md#skill.thread_pierce) |
| merges | 1->2, 2->3 |
| shop | floor 1-5 (weight 20) |

**Sai Sin Thread.** สายสิญจน์ที่ทะลุผีได้หลายตัวในครั้งเดียว

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ตำราหนุนอาคม](synergies.md#synergy.tome_row_damage) - holds `weapon` as **tag_b**: `Damage` +20%, max 2 stack(s). Partner side: [ตำราพระเวท](items.md#item.spell_tome).
- [คุณไสยปะทะของศักดิ์สิทธิ์](synergies.md#synergy.curse_holy_debuff) - holds `holy` as **tag_b**: `Debuff` -30%, max 1 stack(s). Partner side: [กุมารทอง](items.md#item.kuman_thong).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades that can be offered for this piece:**

- [คมอาคม](upgrades.md#variant.arcane.keen_edge) - tier 1->2
- [คาถาไว](upgrades.md#variant.arcane.swift_chant) - tier 1->2
- [มนต์ประหยัด](upgrades.md#variant.arcane.frugal_mantra) - tier 1->2
- [ญาณไกล](upgrades.md#variant.arcane.far_sight) - tier 1->2
- [คมอาคมชั้นสูง](upgrades.md#variant.arcane.great_keen_edge) - tier 2->3
- [คาถาไวชั้นสูง](upgrades.md#variant.arcane.great_swift_chant) - tier 2->3
- [คาถาคู่](upgrades.md#variant.arcane.twin_bolt) - tier 2->3
- [ทะลุทะลวง](upgrades.md#variant.arcane.deep_pierce) - tier 2->3

<a id="item.phra_amulet"></a>
### item.phra_amulet - พระเครื่อง (Phra Amulet)

| Field | Value |
| --- | --- |
| id | `item.phra_amulet` |
| category | `Charm` |
| tier | 1 (of 3) |
| element | `None` |
| price | 9 bia |
| weight | 0.3 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `amulet`, `holy` |
| skill | (none - this piece does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 1-5 (weight 22) |

**Phra Amulet.** ของศักดิ์สิทธิ์ วางติดคุณไสยแล้วทั้งคู่เสื่อมพลัง

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [คุณไสยปะทะของศักดิ์สิทธิ์](synergies.md#synergy.curse_holy_debuff) - holds `holy` as **tag_b**: `Debuff` -30%, max 1 stack(s). Partner side: [กุมารทอง](items.md#item.kuman_thong).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

<a id="item.waist_yant"></a>
### item.waist_yant - ผ้ายันต์คาดเอว (Waist Yant)

| Field | Value |
| --- | --- |
| id | `item.waist_yant` |
| category | `Charm` |
| tier | 1 (of 3) |
| element | `None` |
| price | 7 bia |
| weight | 0.2 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `yant`, `holy` |
| skill | (none - this piece does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 1-4 (weight 18) |

**Waist Yant.** ผ้ายันต์เส้นเล็ก วางติดตะกรุดแล้วคาถายันต์ร่ายไวขึ้น

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ตะกรุดหนุนผ้ายันต์](synergies.md#synergy.takrut_yant_cooldown) - holds `yant` as **tag_b**: `Cooldown` -15%, max 1 stack(s). Partner side: [ตะกรุดโทน](items.md#item.takrut_tone).
- [คุณไสยปะทะของศักดิ์สิทธิ์](synergies.md#synergy.curse_holy_debuff) - holds `holy` as **tag_b**: `Debuff` -30%, max 1 stack(s). Partner side: [กุมารทอง](items.md#item.kuman_thong).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

<a id="item.tiger_fang"></a>
### item.tiger_fang - เขี้ยวเสือ (Tiger Fang)

| Field | Value |
| --- | --- |
| id | `item.tiger_fang` |
| category | `Charm` |
| tier | 1 (of 3) |
| element | `None` |
| price | 8 bia |
| weight | 0.3 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `fang`, `holy` |
| skill | (none - this piece does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 2-5 (weight 16) |

**Tiger Fang.** เขี้ยวเสือของศักดิ์สิทธิ์ที่พกไว้กันผี

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [คุณไสยปะทะของศักดิ์สิทธิ์](synergies.md#synergy.curse_holy_debuff) - holds `holy` as **tag_b**: `Debuff` -30%, max 1 stack(s). Partner side: [กุมารทอง](items.md#item.kuman_thong).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

<a id="item.spell_tome"></a>
### item.spell_tome - ตำราพระเวท (Spell Tome)

| Field | Value |
| --- | --- |
| id | `item.spell_tome` |
| category | `SpellTome` |
| tier | 1 (of 3) |
| element | `None` |
| price | 13 bia |
| weight | 1.2 |
| footprint | `XX|XX` (2x2, 4 cell(s)) |
| tags | `tome` |
| skill | (none - this piece does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 3-5 (weight 10) |

**Spell Tome.** ตำราที่ขยายพลังอาวุธอาคมที่วางติดกัน

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ตำราหนุนอาคม](synergies.md#synergy.tome_row_damage) - holds `tome` as **tag_a**: `Damage` +20%, max 2 stack(s). Partner side: [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

<a id="item.red_soda"></a>
### item.red_soda - น้ำแดง (Red Soda)

| Field | Value |
| --- | --- |
| id | `item.red_soda` |
| category | `Offering` |
| tier | 1 (of 3) |
| element | `None` |
| price | 6 bia |
| weight | 0.4 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `offering` |
| skill | (none - this piece does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 1-5 (weight 15) |

**Red Soda.** ของเซ่นรสหวานที่เป็นของโปรดของภูติรับใช้

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ของเซ่นเลี้ยงภูติ](synergies.md#synergy.offering_familiar_speed) - holds `offering` as **tag_a**: `AttackSpeed` +25%, max 1 stack(s). Partner side: [กุมารทอง](items.md#item.kuman_thong).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

<a id="item.boiled_egg"></a>
### item.boiled_egg - ไข่ต้ม (Boiled Egg)

| Field | Value |
| --- | --- |
| id | `item.boiled_egg` |
| category | `Offering` |
| tier | 1 (of 3) |
| element | `None` |
| price | 5 bia |
| weight | 0.3 |
| footprint | `X` (1x1, 1 cell(s)) |
| tags | `offering` |
| skill | (none - this piece does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 1-5 (weight 12) |

**Boiled Egg.** ของเซ่นง่าย ๆ ที่ผีพอใจ

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [ของเซ่นเลี้ยงภูติ](synergies.md#synergy.offering_familiar_speed) - holds `offering` as **tag_a**: `AttackSpeed` +25%, max 1 stack(s). Partner side: [กุมารทอง](items.md#item.kuman_thong).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

<a id="item.kuman_thong"></a>
### item.kuman_thong - กุมารทอง (Kuman Thong)

| Field | Value |
| --- | --- |
| id | `item.kuman_thong` |
| category | `Familiar` |
| tier | 1 (of 3) |
| element | `Curse` |
| price | 14 bia |
| weight | 0.6 |
| footprint | `XX` (1x2, 2 cell(s)) |
| tags | `familiar`, `curse` |
| skill | [คลื่นคุณไสย](skills.md#skill.curse_wave) |
| merges | 1->2, 2->3 |
| shop | floor 3-5 (weight 8) |

**Kuman Thong.** ภูติรับใช้สายคุณไสย คอยตามพ่อหมอและปล่อยคลื่นคุณไสย

**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**

- [คุณไสยปะทะของศักดิ์สิทธิ์](synergies.md#synergy.curse_holy_debuff) - holds `curse` as **tag_a**: `Debuff` -30%, max 1 stack(s). Partner side: [สายสิญจน์](items.md#item.sai_sin_thread), [พระเครื่อง](items.md#item.phra_amulet), [ผ้ายันต์คาดเอว](items.md#item.waist_yant), [เขี้ยวเสือ](items.md#item.tiger_fang).
- [ของเซ่นเลี้ยงภูติ](synergies.md#synergy.offering_familiar_speed) - holds `familiar` as **tag_b**: `AttackSpeed` +25%, max 1 stack(s). Partner side: [น้ำแดง](items.md#item.red_soda), [ไข่ต้ม](items.md#item.boiled_egg).

- [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) - applies while this item stands on **any** bag: `Damage` +10%, max 1 stack(s). The bag itself gets no bonus.

**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.

## Bags

Bags open usable slots; they carry no combo tags and no upgrade variants. They are placed on the board first, and the items above must be fully covered by a bag.

<a id="bag.raw_cotton_bag"></a>
### bag.raw_cotton_bag - ย่ามผ้าดิบ (Raw Cotton Bag)

| Field | Value |
| --- | --- |
| id | `bag.raw_cotton_bag` |
| category | `Bag` |
| tier | 1 (of 3) |
| element | `None` |
| price | 12 bia |
| weight | 1.0 |
| footprint | `XXX|XXX|XXX` (3x3, 9 cell(s)) |
| tags | (none - bags are not matched by a synergy tag) |
| skill | (none - a bag does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 1-2 (weight 15) |

**Raw Cotton Bag.** ย่ามผ้าดิบขนาด 3x3 เปิดช่องใช้งานเก้าช่อง หนักที่สุดในสามแบบ

**Combos this piece joins:** none - the `synergy` tags live on items, and a bag is not a tag carrier. The bag-area rule is resolved for the items standing on it, not for the bag.

**Upgrades:** no `upgrade_variant` row names `Bag`, so a bag merge has an empty choice pool at both tiers and folds immediately.

<a id="bag.yant_cloth_pouch"></a>
### bag.yant_cloth_pouch - ถุงผ้ายันต์ (Yant Cloth Pouch)

| Field | Value |
| --- | --- |
| id | `bag.yant_cloth_pouch` |
| category | `Bag` |
| tier | 1 (of 3) |
| element | `None` |
| price | 8 bia |
| weight | 0.3 |
| footprint | `X|X|X` (3x1, 3 cell(s)) |
| tags | (none - bags are not matched by a synergy tag) |
| skill | (none - a bag does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 1-3 (weight 12) |

**Yant Cloth Pouch.** ถุงผ้ายันต์ทรงแคบ 1x3 เบาที่สุดในสามแบบ

**Combos this piece joins:** none - the `synergy` tags live on items, and a bag is not a tag carrier. The bag-area rule is resolved for the items standing on it, not for the bag.

**Upgrades:** no `upgrade_variant` row names `Bag`, so a bag merge has an empty choice pool at both tiers and folds immediately.

<a id="bag.wicker_basket_l"></a>
### bag.wicker_basket_l - ตะกร้าหวายรูปตัว L (Wicker Basket L)

| Field | Value |
| --- | --- |
| id | `bag.wicker_basket_l` |
| category | `Bag` |
| tier | 1 (of 3) |
| element | `None` |
| price | 10 bia |
| weight | 0.6 |
| footprint | `X.|X.|XX` (3x2, 4 cell(s)) |
| tags | (none - bags are not matched by a synergy tag) |
| skill | (none - a bag does not cast) |
| merges | 1->2, 2->3 |
| shop | floor 2-4 (weight 12) |

**Wicker Basket L.** ตะกร้าหวายรูปตัว L ต้องหมุนให้เข้าท่าก่อนใช้

**Combos this piece joins:** none - the `synergy` tags live on items, and a bag is not a tag carrier. The bag-area rule is resolved for the items standing on it, not for the bag.

**Upgrades:** no `upgrade_variant` row names `Bag`, so a bag merge has an empty choice pool at both tiers and folds immediately.

See also: [skills](skills.md) | [synergies](synergies.md) | [upgrades](upgrades.md) | [enemies](enemies.md) | [floors](floors.md) | [economy](economy.md) | [content index](index.md)
