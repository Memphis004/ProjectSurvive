> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Skills

One section per row of `data/tables/skill.csv` (5 skills), in file order. A piece casts the skill named by `item.skill_id`; the battle auto-casts it when ready and affordable.

<a id="skill.fire_bolt"></a>
### skill.fire_bolt - คาถาเพลิงกาลี (Kali Flame Bolt)

| Field | Value |
| --- | --- |
| behavior | `Projectile` |
| element | `Fire` |
| cooldown_seconds | 0.9 |
| mp_cost | 4 |
| damage | 7 |
| range | 6.0 |
| pierce_count | 0 |
| projectile_count | 1 |
| projectile_speed | 12.0 |

**Used by:** [ยันต์เพลิงกาลี](items.md#item.kali_flame_yant).

See also: [synergy rules](synergies.md) that change this skill's damage or cooldown through its owner, and the [upgrade variants](upgrades.md) offered when its owner merges.

<a id="skill.thread_pierce"></a>
### skill.thread_pierce - สายสิญจน์ทะลุ (Sai Sin Pierce)

| Field | Value |
| --- | --- |
| behavior | `Pierce` |
| element | `None` |
| cooldown_seconds | 1.2 |
| mp_cost | 5 |
| damage | 7 |
| range | 7.0 |
| pierce_count | 3 |
| projectile_count | 1 |
| projectile_speed | 10.0 |

**Used by:** [สายสิญจน์](items.md#item.sai_sin_thread).

See also: [synergy rules](synergies.md) that change this skill's damage or cooldown through its owner, and the [upgrade variants](upgrades.md) offered when its owner merges.

<a id="skill.knife_thrust"></a>
### skill.knife_thrust - ฟันมีดหมอ (Mor Knife Thrust)

| Field | Value |
| --- | --- |
| behavior | `Melee` |
| element | `None` |
| cooldown_seconds | 0.6 |
| mp_cost | 3 |
| damage | 17 |
| range | 1.7 |
| pierce_count | 0 |
| projectile_count | 1 |
| projectile_speed | 0.0 |

**Used by:** [มีดหมอ](items.md#item.mor_knife).

See also: [synergy rules](synergies.md) that change this skill's damage or cooldown through its owner, and the [upgrade variants](upgrades.md) offered when its owner merges.

<a id="skill.takrut_ward"></a>
### skill.takrut_ward - วงตะกรุดกัน (Takrut Ward)

| Field | Value |
| --- | --- |
| behavior | `Aura` |
| element | `None` |
| cooldown_seconds | 1.4 |
| mp_cost | 5 |
| damage | 9 |
| range | 2.5 |
| pierce_count | 0 |
| projectile_count | 1 |
| projectile_speed | 0.0 |

**Used by:** [ตะกรุดโทน](items.md#item.takrut_tone).

See also: [synergy rules](synergies.md) that change this skill's damage or cooldown through its owner, and the [upgrade variants](upgrades.md) offered when its owner merges.

<a id="skill.curse_wave"></a>
### skill.curse_wave - คลื่นคุณไสย (Curse Wave)

| Field | Value |
| --- | --- |
| behavior | `Aura` |
| element | `Curse` |
| cooldown_seconds | 1.5 |
| mp_cost | 6 |
| damage | 6 |
| range | 2.5 |
| pierce_count | 0 |
| projectile_count | 1 |
| projectile_speed | 0.0 |

**Used by:** [กุมารทอง](items.md#item.kuman_thong).

See also: [synergy rules](synergies.md) that change this skill's damage or cooldown through its owner, and the [upgrade variants](upgrades.md) offered when its owner merges.

See also: [items](items.md) | [synergies](synergies.md) | [upgrades](upgrades.md) | [enemies](enemies.md) | [floors](floors.md) | [economy](economy.md) | [content index](index.md)
