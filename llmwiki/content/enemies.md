> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Enemies

All 4 rows of `data/tables/enemy.csv`, in file order. "Appears on" is computed from `data/tables/wave.csv`; the full timed timeline is in [floors.md](floors.md).

<a id="enemy.wandering_spirit"></a>
### enemy.wandering_spirit - วิญญาณเร่ร่อน (Wandering Spirit)

| Field | Value |
| --- | --- |
| role | `Filler` |
| max_hp | 20 |
| move_speed | 1.6 |
| contact_damage | 5 |
| attack_cooldown_seconds | 1.0 |
| move_pattern | `Chase` |
| is_elite | false |
| is_boss | false |
| boss_phase2_hp_percent | 0 |
| drop_value | 1 bia per kill |

**Appears on:**

- [ป่าช้าวัดร้าง ชั้น 1](floors.md#floor.forest_1) - wave `wave.forest_1.a`: 30 spawn(s) from 0s to 60s every 2.0s
- [ป่าช้าวัดร้าง ชั้น 2](floors.md#floor.forest_2) - wave `wave.forest_2.a`: 20 spawn(s) from 0s to 30s every 1.5s
- [ป่าช้าวัดร้าง ชั้น 4](floors.md#floor.forest_4) - wave `wave.forest_4.a`: 20 spawn(s) from 0s to 20s every 1.0s
- [ป่าช้าวัดร้าง ชั้น 5](floors.md#floor.forest_5) - wave `wave.forest_5.spirits`: 26 spawn(s) from 0s to 90s every 3.0s

<a id="enemy.krasue"></a>
### enemy.krasue - กระสือ (Krasue)

| Field | Value |
| --- | --- |
| role | `Fast` |
| max_hp | 26 |
| move_speed | 3.2 |
| contact_damage | 7 |
| attack_cooldown_seconds | 1.2 |
| move_pattern | `Charge` |
| is_elite | false |
| is_boss | false |
| boss_phase2_hp_percent | 0 |
| drop_value | 1 bia per kill |

**Appears on:**

- [ป่าช้าวัดร้าง ชั้น 2](floors.md#floor.forest_2) - wave `wave.forest_2.b`: 10 spawn(s) from 30s to 60s every 3.0s
- [ป่าช้าวัดร้าง ชั้น 3](floors.md#floor.forest_3) - wave `wave.forest_3.a`: 16 spawn(s) from 0s to 25s every 1.5s
- [ป่าช้าวัดร้าง ชั้น 4](floors.md#floor.forest_4) - wave `wave.forest_4.b`: 14 spawn(s) from 20s to 45s every 1.8s

<a id="enemy.nang_tani"></a>
### enemy.nang_tani - นางตานี (Nang Tani)

| Field | Value |
| --- | --- |
| role | `Elite` |
| max_hp | 90 |
| move_speed | 0.8 |
| contact_damage | 10 |
| attack_cooldown_seconds | 2.0 |
| move_pattern | `Stationary` |
| is_elite | true |
| is_boss | false |
| boss_phase2_hp_percent | 0 |
| drop_value | 3 bia per kill |

**Appears on:**

- [ป่าช้าวัดร้าง ชั้น 3](floors.md#floor.forest_3) - wave `wave.forest_3.b`: 3 spawn(s) from 25s to 60s every 10.0s
- [ป่าช้าวัดร้าง ชั้น 4](floors.md#floor.forest_4) - wave `wave.forest_4.c`: 2 spawn(s) from 45s to 60s every 10.0s

<a id="enemy.pob"></a>
### enemy.pob - ปอบ (Pob)

| Field | Value |
| --- | --- |
| role | `Boss` |
| max_hp | 320 |
| move_speed | 1.2 |
| contact_damage | 12 |
| attack_cooldown_seconds | 1.5 |
| move_pattern | `Chase` |
| is_elite | false |
| is_boss | true |
| boss_phase2_hp_percent | 50 |
| drop_value | 10 bia per kill |

**Appears on:**

- [ป่าช้าวัดร้าง ชั้น 5](floors.md#floor.forest_5) - wave `wave.forest_5.boss`: 1 spawn(s) from 0s to 90s every 90.0s

See also: [items](items.md) | [skills](skills.md) | [synergies](synergies.md) | [upgrades](upgrades.md) | [floors](floors.md) | [economy](economy.md) | [content index](index.md)
