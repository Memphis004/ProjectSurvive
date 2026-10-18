> **Example, not a rule.** This is ProjectSurvive's own `data/` documentation at the moment the template was
> cut (tag `survive-stage-10`). It describes tables the template does not have — bags, items, waves, shop
> pools — and it is kept because the *shape* of it is the useful part: how a project writes down its tables, its
> cross-table rules and the values that are easy to get wrong. The template's own version is
> [`llmwiki/systems/data-schema.md`](../../../llmwiki/systems/data-schema.md).

# Static data schema (`data/`)

Everything the game is balanced with lives here and nowhere else (AGENTS rule 5).
Code never holds a balance number; player-facing text only comes from
`localization.csv`.

> Pipeline decisions: [ADR 0005](../../docs/adr/0005-data-pipeline-shape-and-format.md).
> Footprint format: [ADR 0009](../../docs/adr/0009-footprint-picture-format.md).
> Item-local effects (deliberately absent): [ADR 0010](../../docs/adr/0010-item-local-effects-deferred.md).
> How the runtime reads it: [ADR 0006](../../docs/adr/0006-config-port-and-generated-code-isolation.md).

## Layout

```
data/
  luban.conf                     Luban 5.1.0 config (paths are relative to this file)
  Defines/schema.xml             enums + beans (the shape of every table)
  Defines/__tables__.csv         the table registry (one row per table)
  tables/*.csv                   one CSV per table
  localization/localization.csv  every player-facing string, th + en
```

`SchemaVersion` in `lib/Runtime/Generated/Luban/ConfigBuildMetadata.g.cs` is written
by `tools/gen.ps1` and is currently **2** (Stage 04 added `item.footprint`).

## Commands

```powershell
# data/ -> lib/Runtime/Generated/Luban/*.cs + *.bytes + Proto/App.cs
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1

# game rules over the generated .bytes (fast; no Luban needed)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"

# prove a regeneration reproduces what is committed
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1

# prove broken data is rejected, and for the right reason
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1
```

All four are also run by `tools/test.ps1` (skip them with `-NoPipeline`).
Exit codes everywhere: `0` ok, `1` a required check failed, `2` the tool could
not run.

## Tables

`mode=one` tables hold a single row; the rest are keyed `index=<column>`.

| Table | Key | What it is |
| --- | --- | --- |
| `localization` | `key` | every player-facing string, `th` + `en`. Both must be non-empty. |
| `bag` | `id` | a bag piece; `footprint` opens ActiveSlots on the board |
| `item` | `id` | every non-bag item (weapon, charm, tome, offering, familiar); `footprint` is the cells it occupies |
| `skill` | `id` | cast behaviour shared by weapons and familiars |
| `synergy` | `id` | a combo rule, matched by item **tag** |
| `merge_recipe` | `id` | one merge step: same id + tier+1 (max tier from `constant`) |
| `enemy` | `id` | ghost / elite / boss stats |
| `floor` | `id` | `index` is the 1-based run order; `is_boss` + `boss_enemy_id` |
| `wave` | `id` | spawn window inside a floor |
| `shop_pool` | `id` | one weighted shop offer over a floor range |
| `economy` | one | starting bia, slot count, refresh cost, sell-back, heal, chest |
| `constant` | one | board size, min cooldown, max tier, HP/MP, move speed |

### The Stage 04 prototype content

14 pieces: **3 bags** (`bag.raw_cotton_bag` 3×3, `bag.yant_cloth_pouch` 1×3,
`bag.wicker_basket_l` the L shape), **4 arcane weapons** (one per cast behaviour),
**3 charms**, **1 spell tome** (2×2), **2 offerings**, **1 familiar** (`item.kuman_thong`,
2×1, `Curse` — the คุณไสย piece the 30 % power-decay rule is tested with).
Every piece reaches tier 3, every enemy, floor and wave the 5-floor run needs is
present, and `shop_pool` covers all 14 pieces.

## Footprint syntax

`footprint` is a **picture**: one element per row of the shape, `|`-separated.
`X` is a cell the piece covers, `.` is an empty cell.

```
"XXX|XXX|XXX"   a 3x3 bag            "XX|XX"   a 2x2 tome
"X|X|X"         a 1x3 pouch          "XX"      a 2x1 familiar
"X.|X.|XX"      the L-tromino        "X"       a 1x1 piece
```

- **Normalised on parse**: the minimum x/y of the covered cells becomes 0, and the
  resulting `Width`/`Height` are reported, so an author may draw the shape anywhere in
  the picture and the board sees the same piece.
- **All rows must be the same length.** A ragged picture is
  `footprint.ragged_row`; a character that is neither `X` nor `.` is
  `footprint.malformed_cell`; no row at all, or a picture with no `X`, is
  `footprint.empty`.
- A duplicated cell cannot be expressed, so there is no `footprint.duplicate_cell`
  finding any more (see [ADR 0009](../../docs/adr/0009-footprint-picture-format.md)).
- Parse with `Footprint.TryParse(rows, out footprint, out error)`; it never
  throws, so a validator can report every bad row in one run.
- The parsed value is a set of `CellOffset`, which is what placement, overlap and
  rotation (Stage 05) work on.

## Tag vocabulary

A `synergy` matches on item **tags**, so a tag is an interface between the two tables.
These are the tags the shipped data actually uses (a tag no item carries is
`synergy.tag_unknown`, a hard validation failure):

| Tag | Carried by | Matched by |
| --- | --- | --- |
| `weapon` | all 4 arcane weapons | `synergy.tome_row_damage` |
| `tome` | `item.spell_tome` | `synergy.tome_row_damage` |
| `takrut` | `item.takrut_tone` | `synergy.takrut_yant_cooldown` |
| `yant` | `item.kali_flame_yant`, `item.waist_yant` | `synergy.takrut_yant_cooldown` |
| `curse` | `item.kuman_thong` | `synergy.curse_holy_debuff` |
| `holy` | `item.sai_sin_thread`, `item.phra_amulet`, `item.waist_yant`, `item.tiger_fang` | `synergy.curse_holy_debuff` |
| `offering` | `item.red_soda`, `item.boiled_egg` | `synergy.offering_familiar_speed` |
| `familiar` | `item.kuman_thong` | `synergy.offering_familiar_speed` |

Tags that are flavour only today (nothing matches them yet, which is allowed):
`fire`, `knife`, `thread`, `amulet`, `fang`.

Adding a tag is free; **using** one in `synergy.csv` requires some item to carry it,
or the validator fails the build.

## Cross-table rules

`int#(range=[…])`, `#ref=TbX` and unique primary keys are enforced by Luban with
`--strict`. These are enforced by `lib/Tests~/Data.Validator`, because in each case
an empty cell is a legitimate value and Luban cannot express "if set, then…":

- `item` and `bag` share one id namespace (the shop, the merge recipes and the UI
  all address a piece by a bare id).
- optional references: `item.skill_id`, `item.description_key`,
  `floor.boss_enemy_id`, the `item_id`/`bag_id` of `merge_recipe` and `shop_pool`
  (exactly one of the two must be set).
- footprints: every bag and every item must cover at least one cell
  (`footprint.empty`).
- `skill.cooldown_seconds >= constant.min_cooldown_seconds` — combos can only
  lower a cooldown, so an authored value below the floor is already illegal.
- `merge_recipe.to_tier == from_tier + 1` and `<= constant.max_tier`.
- **every piece's merge ladder is complete** from tier 1 to `constant.max_tier`
  (`merge_recipe.ladder_incomplete`) — the prototype has no tier-1-only pieces.
- **every synergy tag is carried by some item** (`synergy.tag_unknown`).
- every floor needs at least one wave, every wave must fit inside its floor's
  duration, floor `index` values form a gapless 1..N ladder.
- every wave's spawns must fit its window:
  `(spawn_count - 1) * spawn_interval_seconds <= end - start`
  (`wave.spawn_window_too_short`).
- every floor must have a shop-pool entry with a positive weight covering it, and
  **every piece must be offered by some positive-weight entry**
  (`shop_pool.piece_unreachable`).
- `price >= 1`, `weight >= 0`, `max_hp >= 1`, a boss's phase-2 threshold strictly
  inside 0..100, `role == Boss` iff `is_boss`.
- a localization row must have both languages.

Add a rule by editing `ConfigValidator.cs` and adding a case to
`ConfigRulesTests.cs` plus, if it can be expressed in data, a fixture under
`lib/Tests~/fixtures/` (see its README).

## Adding or changing a table

1. Edit `Defines/schema.xml` (bean/enum) and `Defines/__tables__.csv` (for a new
   table), then the CSV.
2. `tools/gen.ps1` — this rewrites the generated C#, both `.bytes` trees, and the
   config hash in `ConfigBuildMetadata.g.cs`.
3. If a field/enum is new: update the domain type, the adapter mapping in
   `LubanGameConfig.cs`, and `ConfigEnums.cs` (the parity test will tell you).
4. Bump `SchemaVersion` in `tools/gen.ps1`'s metadata template when a field is
   added or renamed.
5. `tools/test.ps1` (or the four commands above) and commit `data/` **with** the
   regenerated `lib/Runtime/Generated/**`.

## Values that are easy to get wrong

- **IDs are stable strings, not row numbers.** `item.mor_knife`, `floor.forest_5`,
  `skill.fire_bolt`. They appear in saves, so renaming one is a data migration.
- **`tier` is 1..3** and is the tier the row itself represents (always 1 today); the
  ladder to tier 3 is expressed by `merge_recipe`, and the shape does not change when
  a piece merges.
- **`index` on `floor` is the run order**, not the id order, and the shop-pool
  `floor_min`/`floor_max` bands are compared against it.
- **`weight` is a relative weight, not a percentage**: only the sum per floor
  matters. It is a different thing from the `weight` column on `item` / `bag`, which
  is the bag-load that slows the player down.
- **Tags are a controlled vocabulary**: see the table above. An unused tag is fine; a
  synergy that names an unowned tag fails the build.
- **A charm's or offering's own effect has no column yet** — that is deliberate, see
  [ADR 0010](../../docs/adr/0010-item-local-effects-deferred.md). Do not add a
  magnitude to `item` without also landing its consumer, and never as a code constant.
