# ADR 0028 — Item-local effects in `item`, and a data-authored `status` table

- **Status:** Accepted (Stage 11a)
- **Date:** 2026-10-19
- **Supersedes:** [ADR 0010](0010-item-local-effects-deferred.md) (effect columns are now in
  `data/`), and §4 of [ADR 0017](0017-ecslite-battle-simulation.md) (elemental status no longer
  re-uses the applying skill's damage).
- **Related:** [ADR 0016](0016-run-settlement-and-shop-commit-boundaries.md) (the settlement's
  one-entry-per-consumable rule is unchanged; the unit of an entry is now a *use*).

## Context

ADR 0010 deferred a charm's passive and an offering's use until a consumer existed. Both consumers
exist now: `LoadoutResolver` builds the battle loadout, and the EcsLite battle spends consumables
and applies elemental effects. ADR 0017's interim status rule (the applying hit's own damage, one
second) was explicitly waiting for a `status` table.

The columns must be in `data/` with the code that reads them (AGENTS rule 5), and an effect may only
touch a number `CombatLoadoutSnapshot` already carries (the Stage 09f upgrade rule).

## Decision

### 1. Charm and offering columns live on the `item` row

```
charm_stat, charm_method, charm_magnitude
offering_effect, offering_amount, offering_uses, offering_auto_hp_percent
```

- `charm_stat` / `charm_method` use the **existing** upgrade vocabulary (`MaxHp`, `MoveSpeed`, …
  / `Add`, `Percent`, `Multiply`). A charm may only name a player stat the loadout carries:
  `MaxHp`, `MaxMp`, `MoveSpeed`, `PickupRadius`, `FocusPerSecond`. `LoadoutResolver` sums every
  `Add`, sums every `Percent` and folds every `Multiply` across the board before touching the base,
  exactly as it does for upgrades, so board order cannot change a result.
- `offering_effect` is `None | Heal | Bia`; `offering_amount` is the HP or bia per use;
  `offering_uses` is how many uses **one carried piece** has; `offering_auto_hp_percent` is the HP
  percentage at or below which the battle spends one use automatically (`0` = never).
- Only the owning category may fill each set; the validator rejects an inert charm, an offering
  with no effect/amount/uses, and either set on the wrong category.

The alternative — a separate `charm_effect` table — was rejected: this is one effect per piece, and
a second table would add a join and an optional reference for no expressiveness.

### 2. `status` is its own table

```
status(id, element, damage_per_tick, tick_interval_seconds, duration_seconds, max_stacks)
```

One row per element. A hit whose skill carries an element applies the row for that element; damage
per application is `damage_per_tick × stacks`, the row's interval decides cadence, its duration
decides expiry, and `max_stacks` caps the stack count (`1` = refresh only). The component's `DefId`
is now the **status row's** def id, not the applying skill's. The validator requires every element a
skill uses to have a row and rejects a duplicate element.

### 3. `RemainingConsumables` counts uses, not pieces

`BattleResult.RemainingConsumables` stays one entry per `CombatLoadoutSnapshot.Consumables` entry
(ADR 0016). The value is now **remaining uses**; the initial value is `Count × UsesPerItem`. The
settlement converts the difference back to whole pieces with
`ceil(usedUses / UsesPerItem)`, so a partly used piece still leaves the bag. `UsesPerItem` defaults
to `1`, which keeps every pre-existing caller and test reading the old way.

## Consequences

- `SchemaVersion` moves 5 → 6; `tools/gen.ps1` rewrites the generated readers and bytes.
- `item.csv` puts `footprint` last: Luban 5.1.0 rejects an empty `list` cell that is followed by
  other columns, and the existing empty-footprint fixture relies on a trailing empty cell.
- Charm effects are balance starting points (three charms cover three distinct stats) and are
  expected to move in Stage 11's balance pass.
- The offering auto-use rule is one use per threshold crossing; a heal that lifts the player back
  over the line re-arms the entry. A Bia offering is therefore spent once per fall, not once per
  tick.
