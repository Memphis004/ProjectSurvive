# Luban 5.1.0: the CLI facts you cannot guess

Everything below was verified by running the pinned binary
(`tools/vendor/luban/5.1.0/Luban/Luban.exe`, built from
`df34215d2e22035073d1af83d44e3ac0744dc3b3`) — not read off a web page. Luban's
CLI has changed between 4.x and 5.x and its docs lag the code, so treat this file
as the primary reference. Re-verify with `Luban.exe --help` before trusting any
new flag.

## `--strict` is not optional

Without it, a **failing `#ref` or `#(range=…)` validator only logs a warning and
the CLI still exits 0** — a build that produces silently broken tables. With
`--strict`, the same input exits non-zero.

`tools/gen.ps1` always passes `--strict`; `tools/check-fixtures.ps1` asserts that
both an out-of-range value and a dangling `#ref` are actually rejected.

## The command line that works

```powershell
Luban.exe `
  --conf         data/luban.conf `
  -t             client `
  -c             cs-bin `
  -d             bin `
  --strict `
  --errorFormat  json `
  -x outputCodeDir=<dir for .cs> `
  -x outputDataDir=<dir for .bytes>
```

- `-t` is the **target** name (`client`, declared in `luban.conf`'s `targets`),
  `-c` the **code target** (`cs-bin` = C# + binary data), `-d` the data target.
- Output directories are passed as `-x key=value` extras, not as flags.
- `--errorFormat json` is what makes a failure machine-readable; without it the
  error text is decorated and harder to grep.

## The table registry is a CSV, and it needs `type: "table"`

`schemaFiles` in `luban.conf` is a list of **objects**, each with a `fileName` and
a `type`:

```json
"schemaFiles": [
  { "fileName": "Defines/schema.xml",  "type": "" },
  { "fileName": "Defines/__tables__.csv", "type": "table" }
]
```

A bare string list (which older examples show) does not register the tables, and
`__tables__.csv` without `type: "table"` is treated as schema-less. Both were
confirmed the hard way.

`__tables__.csv` columns: `full_name,value_type,index,mode,group,comment,read_schema_from_file,input,output,tags`.
`mode=one` makes a single-row table (`economy`, `constant`); `index` names the
primary-key column.

## `#ref` cannot mean "optional"

`type="string#ref=TbLocalization"` rejects an **empty** cell. So a column that is
legitimately blank (`item.skill_id` on a charm, `floor.boss_enemy_id` on a normal
floor, `merge_recipe.item_id` vs `bag_id`) must be a plain `string`, and its
integrity has to be checked by `tools/validate-data.ps1` instead. Using `#ref`
there makes valid data unbuildable.

## Lists and the empty element

`list#sep=|,string` on an empty CSV cell yields an **empty list**, not a list with
one empty string. That is what makes `footprint=""` distinguishable from a
malformed cell — and it is why the empty footprint is reported as
`footprint.empty` rather than a parse error.

The same applies to a footprint picture whose rows are all `.`: there is no
covered cell, so it is `footprint.empty` too, not a valid zero-cell shape.

## Generated layout

- `-c cs-bin` writes one `.cs` per bean plus `Tables.cs` and `Tb<Name>.cs`, all
  under `topModule` from `luban.conf` (`ProjectSurvive.Config`).
- The runtime's constructor is `new Tables(Func<string, ByteBuf> loader)`, keyed by
  the **lower-case table name** (`tbitem`, `tbmergetrecipe` …). The loader keys are
  not the `full_name` strings: `TbMergeRecipe` → `tbmergerecipe`. Read them off the
  generated `Tables.cs`, never guess.
- `Tables.cs` is what `LubanGameConfig.TableNames` mirrors, and a guard test
  compares the two.

## Runtime prerequisite

`Luban.exe` is a framework-dependent `net8.0` build: it needs a **.NET 8+ runtime**
(`dotnet --list-runtimes`), not an SDK. `tools/gen.ps1` checks for it and fails
with exit 2 instead of producing a confusing error.

## The C# runtime library

`lib/third_party/luban-runtime/` vendors the generated-code runtime (`ByteBuf`
and friends) so `using Luban;` resolves in both Unity and the dotnet harness.
It is third-party code: no hand edits, license and upstream revision recorded in
its `VENDOR.md` (AGENTS rule 2).
