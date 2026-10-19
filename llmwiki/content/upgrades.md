> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Upgrade variants

All 10 rows of `data/tables/upgrade_variant.csv` and their 12 modifier rows, in file order. A merge offers at most `constant.upgrade_choice_count` (3) weighted choices from the variants that apply at that tier. Eligibility follows `UpgradePool.Applies`: a non-empty `category` matches the item's category; otherwise a non-empty `tag` matches one of the item's tags. Only items have variants; `bag` rows have no category and no tag, so a bag merge has an empty pool.

## Tier 1 -> 2

| Variant | Applies by | Weight | Modifiers |
| --- | --- | --- | --- |
| [คมอาคม](upgrades.md#variant.arcane.keen_edge) | category `ArcaneWeapon` | 12 | `Damage` +25% |
| [คาถาไว](upgrades.md#variant.arcane.swift_chant) | category `ArcaneWeapon` | 12 | `Cooldown` -15% |
| [มนต์ประหยัด](upgrades.md#variant.arcane.frugal_mantra) | category `ArcaneWeapon` | 10 | `MpCost` -2 |
| [ญาณไกล](upgrades.md#variant.arcane.far_sight) | category `ArcaneWeapon` | 10 | `Range` +20% |
| [ผนึกเพลิง](upgrades.md#variant.fire.blazing_seal) | tag `fire` | 8 | `Damage` +20%; `MpCost` -1 |

## Tier 2 -> 3

| Variant | Applies by | Weight | Modifiers |
| --- | --- | --- | --- |
| [คมอาคมชั้นสูง](upgrades.md#variant.arcane.great_keen_edge) | category `ArcaneWeapon` | 12 | `Damage` +35% |
| [คาถาไวชั้นสูง](upgrades.md#variant.arcane.great_swift_chant) | category `ArcaneWeapon` | 12 | `Cooldown` -20% |
| [คาถาคู่](upgrades.md#variant.arcane.twin_bolt) | category `ArcaneWeapon` | 10 | `ProjectileCount` +1 |
| [ทะลุทะลวง](upgrades.md#variant.arcane.deep_pierce) | category `ArcaneWeapon` | 10 | `PierceCount` +1 |
| [ผนึกไฟนรก](upgrades.md#variant.fire.inferno_seal) | tag `fire` | 8 | `Damage` +4; `Range` +15% |

<a id="variant.arcane.keen_edge"></a>
### variant.arcane.keen_edge - คมอาคม (Keen Edge)

**Tier:** 1 -> 2. **Weight:** 12.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Damage` | `Percent` | 25 | +25% |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.arcane.swift_chant"></a>
### variant.arcane.swift_chant - คาถาไว (Swift Chant)

**Tier:** 1 -> 2. **Weight:** 12.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Cooldown` | `Percent` | -15 | -15% |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.arcane.frugal_mantra"></a>
### variant.arcane.frugal_mantra - มนต์ประหยัด (Frugal Mantra)

**Tier:** 1 -> 2. **Weight:** 10.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `MpCost` | `Add` | -2 | -2 |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.arcane.far_sight"></a>
### variant.arcane.far_sight - ญาณไกล (Far Sight)

**Tier:** 1 -> 2. **Weight:** 10.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Range` | `Percent` | 20 | +20% |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.fire.blazing_seal"></a>
### variant.fire.blazing_seal - ผนึกเพลิง (Blazing Seal)

**Tier:** 1 -> 2. **Weight:** 8.

**Applies by:** tag `fire` (checked only when `category` is empty).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Damage` | `Percent` | 20 | +20% |
| `MpCost` | `Add` | -1 | -1 |

**Eligible pieces (1):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant).

<a id="variant.arcane.great_keen_edge"></a>
### variant.arcane.great_keen_edge - คมอาคมชั้นสูง (Greater Keen Edge)

**Tier:** 2 -> 3. **Weight:** 12.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Damage` | `Percent` | 35 | +35% |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.arcane.great_swift_chant"></a>
### variant.arcane.great_swift_chant - คาถาไวชั้นสูง (Greater Swift Chant)

**Tier:** 2 -> 3. **Weight:** 12.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Cooldown` | `Percent` | -20 | -20% |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.arcane.twin_bolt"></a>
### variant.arcane.twin_bolt - คาถาคู่ (Twin Bolt)

**Tier:** 2 -> 3. **Weight:** 10.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `ProjectileCount` | `Add` | 1 | +1 |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.arcane.deep_pierce"></a>
### variant.arcane.deep_pierce - ทะลุทะลวง (Deep Pierce)

**Tier:** 2 -> 3. **Weight:** 10.

**Applies by:** category `ArcaneWeapon` (the category wins when it is set).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `PierceCount` | `Add` | 1 | +1 |

**Eligible pieces (4):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant), [ตะกรุดโทน](items.md#item.takrut_tone), [มีดหมอ](items.md#item.mor_knife), [สายสิญจน์](items.md#item.sai_sin_thread).

<a id="variant.fire.inferno_seal"></a>
### variant.fire.inferno_seal - ผนึกไฟนรก (Inferno Seal)

**Tier:** 2 -> 3. **Weight:** 8.

**Applies by:** tag `fire` (checked only when `category` is empty).

**Modifiers:**

| stat | method | magnitude | displayed |
| --- | --- | --- | --- |
| `Damage` | `Add` | 4 | +4 |
| `Range` | `Percent` | 15 | +15% |

**Eligible pieces (1):** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant).

See also: [items](items.md) | [skills](skills.md) | [synergies](synergies.md) | [enemies](enemies.md) | [floors](floors.md) | [economy](economy.md) | [content index](index.md)
