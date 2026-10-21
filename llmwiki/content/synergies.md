> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Synergy (combo) rules

All 5 rows of `data/tables/synergy.csv`, in file order. The rules are resolved into the loadout before a battle starts; the battle does not re-evaluate them. `AdjacentPair` counts orthogonal neighbours only; `SameRow` and `SameColumn` are different rules, not one "same line" rule.

| Rule | kind | tag_a | tag_b | stat | value_percent | max_stacks |
| --- | --- | --- | --- | --- | --- | --- |
| [ตะกรุดหนุนผ้ายันต์](synergies.md#synergy.takrut_yant_cooldown) | `AdjacentPair` | `takrut` | `yant` | `Cooldown` | -15 | 1 |
| [ตำราหนุนอาคม](synergies.md#synergy.tome_row_damage) | `AdjacentPair` | `tome` | `weapon` | `Damage` | 20 | 2 |
| [คุณไสยปะทะของศักดิ์สิทธิ์](synergies.md#synergy.curse_holy_debuff) | `AdjacentPair` | `curse` | `holy` | `Debuff` | -30 | 1 |
| [ของเซ่นเลี้ยงภูติ](synergies.md#synergy.offering_familiar_speed) | `AdjacentPair` | `offering` | `familiar` | `AttackSpeed` | 25 | 1 |
| [อานุภาพย่ามพิเศษ](synergies.md#synergy.bag_area_bonus) | `BagArea` | - | - | `Damage` | 10 | 1 |

<a id="synergy.takrut_yant_cooldown"></a>
### synergy.takrut_yant_cooldown - ตะกรุดหนุนผ้ายันต์ (Takrut Yant Bond)

**Kind:** `AdjacentPair`. **Stat:** `Cooldown` -15%. **Max stacks:** 1.

**tag_a `takrut` pieces (1):** [ตะกรุดโทน](items.md#item.takrut_tone)

**tag_b `yant` pieces (2):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ผ้ายันต์คาดเอว](items.md#item.waist_yant)

Pairs: 1 x 2 = 2 possible side-by-side combinations (orthogonal neighbours only).

See the effect on each piece in [items.md](items.md); the stat is applied to both sides where the rule says so (`Cooldown`).

<a id="synergy.tome_row_damage"></a>
### synergy.tome_row_damage - ตำราหนุนอาคม (Touching Tome)

**Kind:** `AdjacentPair`. **Stat:** `Damage` +20%. **Max stacks:** 2.

**tag_a `tome` pieces (1):** [ตำราพระเวท](items.md#item.spell_tome)

**tag_b `weapon` pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread)

Pairs: 1 x 4 = 4 possible side-by-side combinations (orthogonal neighbours only).

See the effect on each piece in [items.md](items.md); the stat is applied to both sides where the rule says so (`Damage`).

<a id="synergy.curse_holy_debuff"></a>
### synergy.curse_holy_debuff - คุณไสยปะทะของศักดิ์สิทธิ์ (Curse Meets the Sacred)

**Kind:** `AdjacentPair`. **Stat:** `Debuff` -30%. **Max stacks:** 1.

**tag_a `curse` pieces (1):** [กุมารทอง](items.md#item.kuman_thong)

**tag_b `holy` pieces (4):** [สายสิญจน์](items.md#item.sai_sin_thread), [พระเครื่อง](items.md#item.phra_amulet), [ผ้ายันต์คาดเอว](items.md#item.waist_yant), [เขี้ยวเสือ](items.md#item.tiger_fang)

Pairs: 1 x 4 = 4 possible side-by-side combinations (orthogonal neighbours only).

See the effect on each piece in [items.md](items.md); the stat is applied to both sides where the rule says so (`Debuff`).

<a id="synergy.offering_familiar_speed"></a>
### synergy.offering_familiar_speed - ของเซ่นเลี้ยงภูติ (Offering Feeds the Familiar)

**Kind:** `AdjacentPair`. **Stat:** `AttackSpeed` +25%. **Max stacks:** 1.

**tag_a `offering` pieces (2):** [น้ำแดง](items.md#item.red_soda), [ไข่ต้ม](items.md#item.boiled_egg)

**tag_b `familiar` pieces (1):** [กุมารทอง](items.md#item.kuman_thong)

Pairs: 2 x 1 = 2 possible side-by-side combinations (orthogonal neighbours only).

See the effect on each piece in [items.md](items.md); the stat is applied to both sides where the rule says so (`AttackSpeed`).

<a id="synergy.bag_area_bonus"></a>
### synergy.bag_area_bonus - อานุภาพย่ามพิเศษ (Special Bag Aura)

**Kind:** `BagArea`. **Stat:** `Damage` +10%. **Max stacks:** 1.

**tag_a / tag_b:** none - this rule is not tag-scoped. It applies to every **item** standing on a bag (not to the bag itself).

Affected pieces: every item in [items.md](items.md) while it is fully covered by a bag; a piece in the chest gets nothing.

See also: [items](items.md) | [skills](skills.md) | [upgrades](upgrades.md) | [enemies](enemies.md) | [floors](floors.md) | [economy](economy.md) | [content index](index.md)
