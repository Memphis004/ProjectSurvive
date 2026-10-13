# Static data schema (`data/`)

Everything the game is balanced with lives here and nowhere else (AGENTS rule 5).
Code never holds a balance number; player-facing text only comes from
`localization.csv`.

> Pipeline decisions: [ADR 0005](../../docs/adr/0005-data-pipeline-shape-and-format.md).
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
| `item` | `id` | every non-bag item (weapon, charm, tome, offering, familiar) |
| `skill` | `id` | cast behaviour shared by weapons and familiars |
| `synergy` | `id` | a combo rule, matched by item **tag** |
| `merge_recipe` | `id` | one merge step: same id + tier+1 (max tier from `constant`) |
| `enemy` | `id` | ghost / elite / boss stats |
| `floor` | `id` | `index` is the 1-based run order; `is_boss` + `boss_enemy_id` |
| `wave` | `id` | spawn window inside a floor |
| `shop_pool` | `id` | one weighted shop offer over a floor range |
| `economy` | one | starting bia, slot count, refresh cost, sell-back, heal, chest |
| `constant` | one | board size, min cooldown, max tier, HP/MP, move speed |

### Cross-table rules (they live in the tool, not in the schema)

`int#(range=[…])`, `#ref=TbX` and unique primary keys are enforced by Luban with
`--strict`. These are enforced by `lib/Tests~/Data.Validator`, because in each case
an empty cell is a legitimate value and Luban cannot express "if set, then…":

- `item` and `bag` share one id namespace (the shop, the merge recipes and the UI
  all address a piece by a bare id).
- optional references: `item.skill_id`, `item.description_key`,
  `floor.boss_enemy_id`, the `item_id`/`bag_id` of `merge_recipe` and `shop_pool`
  (exactly one of the two must be set).
- `skill.cooldown_seconds >= constant.min_cooldown_seconds` — combos can only
  lower a cooldown, so an authored value below the floor is already illegal.
- `merge_recipe.to_tier == from_tier + 1` and `<= constant.max_tier`.
- every floor needs at least one wave, every wave must fit inside its floor's
  duration, floor `index` values form a gapless 1..N ladder.
- every floor must have a shop-pool entry with a positive weight covering it.
- `price >= 1`, `weight >= 0`, `max_hp >= 1`, a boss's phase-2 threshold strictly
  inside 0..100, `role == Boss` iff `is_boss`.
- a localization row must have both languages.

Add a rule by editing `ConfigValidator.cs` and adding a case to
`ConfigRulesTests.cs` plus, if it can be expressed in data, a fixture under
`lib/Tests~/fixtures/` (see its README).

## Footprint syntax

`footprint` is a `|`-separated list of `x,y` cell offsets: `"0,0|1,0|0,1"`.

- Normalised on parse: the minimum x/y becomes 0, so an author may write a shape
  anywhere and the board sees the same piece.
- An empty list, a malformed token and a duplicated cell are all load failures
  (`footprint.empty`, `footprint.malformed_cell`, `footprint.duplicate_cell`).
- Parse with `Footprint.TryParse(tokens, out footprint, out error)`; it never
  throws, so a validator can report every bad row in one run.

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
- **`tier` is 1..3** and matches the tier ladder of `merge_recipe`; a piece that
  cannot reach tier 3 does not need a `.2_3` recipe, but the recipes that exist
  must be sequential.
- **`index` on `floor` is the run order**, not the id order, and the shop-pool
  `floor_min`/`floor_max` bands are compared against it.
- **`weight` is a relative weight, not a percentage**: only the sum per floor
  matters.
- **Tags are free-form strings**; a synergy that references a tag no item carries
  is a dead rule (not yet a validator error — the tag vocabulary lands with the
  Stage 04 content).
