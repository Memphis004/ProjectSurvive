> **GENERATED FROM data/ — DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.

# Economy and constants

The single row of `data/tables/economy.csv` and the single row of `data/tables/constant.csv`. These are the run-wide numbers; item prices and weights are on [items.md](items.md), and enemy drop values on [enemies.md](enemies.md).

## economy.csv

| Field | Value |
| --- | --- |
| `start_bia` | 99 |
| `shop_slot_count` | 4 |
| `refresh_base_cost` | 5 |
| `refresh_cost_increment` | 2 |
| `sell_back_percent` | 50 |
| `heal_percent_on_clear` | 15 |
| `chest_slots` | 10 |
| `clear_reward_bia` | 8 |

How the run consumes these: starting purse, a shop of `shop_slot_count` slots per sala, a re-roll priced `refresh_base_cost + refresh_cost_increment x refreshes this camp`, sell-back at `sell_back_percent`, a clear heal of `heal_percent_on_clear` (win only), a chest of `chest_slots`, and `clear_reward_bia` on a win. The rules live in [`../systems/run-shop.md`](../systems/run-shop.md).

## constant.csv

| Field | Value |
| --- | --- |
| `board_width` | 9 |
| `board_height` | 7 |
| `min_cooldown_seconds` | 0.2 |
| `max_speed_penalty_percent` | 40 |
| `weight_speed_percent_per_unit` | 5 |
| `pickup_radius` | 1.5 |
| `max_hp` | 100 |
| `max_mp` | 50 |
| `focus_per_second` | 5.0 |
| `move_speed` | 4.0 |
| `max_tier` | 3 |
| `upgrade_choice_count` | 3 |
| `magnet_speed` | 24.0 |
| `sweep_timeout_seconds` | 2.0 |

Reading notes: `board_width` x `board_height` is the board the bag is placed on; `max_tier` is the merge ceiling; `min_cooldown_seconds` is a floor the resolver clamps a cooldown to, so an upgrade can be visibly capped; `weight_speed_percent_per_unit` and `max_speed_penalty_percent` are the weight penalty; `magnet_speed` and `sweep_timeout_seconds` belong to the end-of-floor sweep. The consumers are [`../systems/backpack.md`](../systems/backpack.md) and [`../systems/battle-simulation.md`](../systems/battle-simulation.md).

See also: [items](items.md) | [skills](skills.md) | [synergies](synergies.md) | [upgrades](upgrades.md) | [enemies](enemies.md) | [floors](floors.md) | [content index](index.md)
