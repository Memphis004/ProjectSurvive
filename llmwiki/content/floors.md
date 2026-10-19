> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Floors and wave timelines

All 5 rows of `data/tables/floor.csv` and the 9 `wave` rows, in file order. A floor ends when its wave timeline is survived, or (boss floors) when the boss dies.

| Floor | index | duration (s) | boss | waves | total spawns |
| --- | --- | --- | --- | --- | --- |
| [ป่าช้าวัดร้าง ชั้น 1](floors.md#floor.forest_1) | 1 | 60 | no | 1 | 30 |
| [ป่าช้าวัดร้าง ชั้น 2](floors.md#floor.forest_2) | 2 | 60 | no | 2 | 30 |
| [ป่าช้าวัดร้าง ชั้น 3](floors.md#floor.forest_3) | 3 | 60 | no | 2 | 19 |
| [ป่าช้าวัดร้าง ชั้น 4](floors.md#floor.forest_4) | 4 | 60 | no | 3 | 36 |
| [ป่าช้าวัดร้าง ชั้น 5](floors.md#floor.forest_5) | 5 | 90 | yes (`enemy.pob`) | 1 | 1 |

<a id="floor.forest_1"></a>
### floor.forest_1 - ป่าช้าวัดร้าง ชั้น 1 (Graveyard Forest F1)

**Index:** 1. **Duration:** 60s. **Boss floor:** false.

**Wave timeline (data order):**

| Wave | Enemy | start (s) | end (s) | spawn_count | interval (s) | elite |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.forest_1.a` | [วิญญาณเร่ร่อน](enemies.md#enemy.wandering_spirit) | 0 | 60 | 30 | 2.0 | false |

Total spawns in the data: 30. The interval is the gap between spawns inside that wave's window.

<a id="floor.forest_2"></a>
### floor.forest_2 - ป่าช้าวัดร้าง ชั้น 2 (Graveyard Forest F2)

**Index:** 2. **Duration:** 60s. **Boss floor:** false.

**Wave timeline (data order):**

| Wave | Enemy | start (s) | end (s) | spawn_count | interval (s) | elite |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.forest_2.a` | [วิญญาณเร่ร่อน](enemies.md#enemy.wandering_spirit) | 0 | 30 | 20 | 1.5 | false |
| `wave.forest_2.b` | [กระสือ](enemies.md#enemy.krasue) | 30 | 60 | 10 | 3.0 | false |

Total spawns in the data: 30. The interval is the gap between spawns inside that wave's window.

<a id="floor.forest_3"></a>
### floor.forest_3 - ป่าช้าวัดร้าง ชั้น 3 (Graveyard Forest F3)

**Index:** 3. **Duration:** 60s. **Boss floor:** false.

**Wave timeline (data order):**

| Wave | Enemy | start (s) | end (s) | spawn_count | interval (s) | elite |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.forest_3.a` | [กระสือ](enemies.md#enemy.krasue) | 0 | 25 | 16 | 1.5 | false |
| `wave.forest_3.b` | [นางตานี](enemies.md#enemy.nang_tani) | 25 | 60 | 3 | 10.0 | true |

Total spawns in the data: 19. The interval is the gap between spawns inside that wave's window.

<a id="floor.forest_4"></a>
### floor.forest_4 - ป่าช้าวัดร้าง ชั้น 4 (Graveyard Forest F4)

**Index:** 4. **Duration:** 60s. **Boss floor:** false.

**Wave timeline (data order):**

| Wave | Enemy | start (s) | end (s) | spawn_count | interval (s) | elite |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.forest_4.a` | [วิญญาณเร่ร่อน](enemies.md#enemy.wandering_spirit) | 0 | 20 | 20 | 1.0 | false |
| `wave.forest_4.b` | [กระสือ](enemies.md#enemy.krasue) | 20 | 45 | 14 | 1.8 | false |
| `wave.forest_4.c` | [นางตานี](enemies.md#enemy.nang_tani) | 45 | 60 | 2 | 10.0 | true |

Total spawns in the data: 36. The interval is the gap between spawns inside that wave's window.

<a id="floor.forest_5"></a>
### floor.forest_5 - ป่าช้าวัดร้าง ชั้น 5 (Pob's Hollow)

**Index:** 5. **Duration:** 90s. **Boss floor:** true.

**Boss:** [ปอบ](enemies.md#enemy.pob) - phase 2 at 50% HP.

**Wave timeline (data order):**

| Wave | Enemy | start (s) | end (s) | spawn_count | interval (s) | elite |
| --- | --- | --- | --- | --- | --- | --- |
| `wave.forest_5.boss` | [ปอบ](enemies.md#enemy.pob) | 0 | 90 | 1 | 90.0 | true |

Total spawns in the data: 1. The interval is the gap between spawns inside that wave's window.

See also: [items](items.md) | [skills](skills.md) | [synergies](synergies.md) | [upgrades](upgrades.md) | [enemies](enemies.md) | [economy](economy.md) | [content index](index.md)
