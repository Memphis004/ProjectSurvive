# Static data schema (`data/`)

Everything a build is balanced with lives here and nowhere else (AGENTS rule 5). Code holds keys, never
numbers or sentences; player-facing text only comes from `localization.csv`.

> Pipeline decisions: [ADR 0005](../../docs/adr/0005-data-pipeline-shape-and-format.md).
> How the runtime reads it: [ADR 0006](../../docs/adr/0006-config-port-and-generated-code-isolation.md).
> ProjectSurvive's own 13-table version is kept as an example:
> [docs/template/examples/systems/data-schema.md](../../docs/template/examples/systems/data-schema.md).

## Layout

```
data/
  luban.conf                     Luban 5.1.0 config (paths are relative to this file)
  Defines/schema.xml             enums + beans (the shape of every table)
  Defines/__tables__.csv         the table registry (one row per table)
  tables/*.csv                   one CSV per table
  localization/localization.csv  every player-facing string, th + en
```

`SchemaVersion` and the `ConfigHash` in `lib/Runtime/Generated/Luban/ConfigBuildMetadata.g.cs` are
written by `tools/gen.ps1`. The hash covers the generated `.bytes`, so it changes when data changes and
not before.

## Commands

```powershell
# data/ -> lib/Runtime/Generated/Luban/*.cs + *.bytes + Proto/App.cs
pwsh -NoProfile -File tools/gen.ps1

# the rules over the generated .bytes (fast; no Luban needed)
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"

# prove a regeneration reproduces what is committed
pwsh -NoProfile -File tools/verify-generated.ps1

# prove broken data is rejected, and for the right reason
pwsh -NoProfile -File tools/check-fixtures.ps1
```

All four are also run by `tools/test.ps1`. Exit codes everywhere: `0` ok, `1` a required check failed,
`2` the tool could not run.

## Tables

| Table | Key | What it is |
| --- | --- | --- |
| `localization` | `key` | every player-facing string, `th` + `en` |
| `constant` | one row | the numbers every build needs: starting HP and MP, move speed, pickup radius |
| `sample` | `id` | a worked example of the two reference mechanisms — **delete it** once a game has a table of its own |

`constant` is `mode=one`; the other two are keyed by their first column (`index=key`, `index=id`).

### Why `sample` exists

A pipeline that nothing exercises is a pipeline that breaks silently. `sample` is the smallest table that
can carry the two ways one table points at another, and the difference decides *who* reports a broken
pointer:

| Column | Type | Checked by | Fixture that proves it |
| --- | --- | --- | --- |
| `name_key` | `string#ref=TbLocalization` | Luban, during `gen.ps1` (strict) | `lib/Tests~/fixtures/missing-ref` |
| `note_key` | `string` (optional) | `Data.Validator`, over the generated tables | `lib/Tests~/fixtures/missing-note-key` |

An empty cell is a legal `#ref`, so Luban cannot express "if set, then it must exist" — that half has to
live in `Data.Validator`. The sample table also carries its own id and a `value` with a schema range, so a
new table has a shape to copy.

## Reference mechanisms

```xml
<var name="name_key" type="string#ref=TbLocalization"/>   <!-- required: fail at generate time -->
<var name="note_key" type="string"/>                      <!-- optional: rule in Data.Validator -->
<var name="value"    type="int#(range=[0,1000000])"/>     <!-- range: fail at generate time -->
```

- `#ref=TbX` points at another table's **key column** (a `map` table's index, or a `one` table). It is
  validated with the strict flag in `tools/gen.ps1`, so a dangling cell stops the build before any `.bytes`
  are written.
- `#(range=[a,b])` is Luban's own value validator; the error names the table and column.
- A field that is a localization key but *optional* stays a plain `string`, and the rule that it resolves
  lives in `ConfigValidator`.

## Cross-table rules

`lib/Tests~/Data.Validator` owns what the schema cannot express. The template keeps these:

| Code | What it means |
| --- | --- |
| `localization.duplicate_key` | two rows share a key (the table's index collapses them, so only a snapshot sees it) |
| `localization.blank_key` | a row has no key |
| `localization.blank_text` | both languages are empty, so the row can never be shown |
| `localization.missing_language` | one language is filled and the other is not |
| `localization.empty` | the table has no rows at all |
| `sample.blank_id`, `sample.duplicate_id`, `sample.empty` | the sample table's key column |
| `sample.missing_name_key`, `sample.missing_note_key` | a localization key a data row points at does not exist |
| `constant.non_positive`, `constant.negative`, `constant.out_of_range` | a tuned number that a division or a comparison would break on |

Rules are about **shape, never balance**: a number being small is a design decision, a number being zero
where the code divides by it is a bug. Two codes for "the sentence is missing" (`blank_text`) and "half the
sentence is missing" (`missing_language`) stay separate because the causes are separate — the runtime falls
back to the language that exists, so a half-translated row shows a player the wrong language rather than
crashing.

Add a rule by editing `ConfigValidator.cs`, adding a case to `ConfigRulesTests.cs`, and — when data can
express it — a fixture under `lib/Tests~/fixtures/` whose `fixture.json` names the expected code. A fixture
whose violation the *schema* catches is dismissed at the `luban` stage instead, and that is a real
distinction: `<fixture>/fixture.json` has `"stage": "luban"` or `"stage": "semantic"`.

## Adding or changing a table

1. Edit `Defines/schema.xml` (bean/enum) and `Defines/__tables__.csv` (registry row), then the CSV.
2. `pwsh tools/gen.ps1` — rewrites the generated C#, both `.bytes` trees and the config hash.
3. Add the domain type (`Domain/Config/ConfigTables.cs`), the port member
   (`IGameConfig`), the mapping in `Infrastructure/Config/LubanGameConfig.cs`, and the table name in
   `LubanGameConfig.TableNames` — `Data.Validator` and the Sim iterate that list, so a table missing from it
   is a table nothing checks.
4. `tools/test.ps1 -Suite Fast` and commit `data/` **together with** the regenerated
   `lib/Runtime/Generated/**` and `UnityProject/Assets/Main/Resources/Config/**`.

## Values that are easy to get wrong

- **An XML comment in `schema.xml` may not contain a double dash.** Luban's XML reader refuses the file and
  the error names a line and column inside the comment, not the flag being described.
- **`SchemaVersion` is hand-bumped**, not derived: raise it in `tools/gen.ps1` when a field is added or
  renamed, and leave the hash alone — that one is computed.
- **IDs are stable strings, not row numbers** (`sample.alpha`). They appear in saves, so renaming one is a
  data migration.
- **A table keyed by `id` hides duplicates from the loader**: Luban collapses them during generation. The
  validator's duplicate rules exist for snapshots, and the fixture for that case is a `luban`-stage one.
- **`constant.csv` is one row.** Adding a second row is not "more balance", it is a table with two rows and
  a loader that reads the first.
