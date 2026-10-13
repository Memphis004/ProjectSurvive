# Broken-data fixtures

Each directory here is **one deliberate mistake in `data/`**, and the point of it
is the Stage 02 acceptance criterion *"fixture ที่ผิดต้อง fail"*: broken data must
be rejected, and rejected for the right reason.

## Layout

```
<case>/fixture.json          # what this case is, which stage must catch it, expected codes
<case>/<same path as data/>  # the overlaid table files, e.g. tables/item.csv
```

The harness copies the real `data/` to a temporary directory, overlays every
non-JSON file from the case on top of it (same relative paths), and runs the
pipeline against that copy. `data/` itself is never touched - which is also why
the files here only carry the broken table, not a full copy of the data set.

`lib/Tests~/data/*.bytes` are git-ignored (ADR 0001) and the `.bytes` for a
fixture are generated into a temporary directory per run, so nothing here is a
binary blob that can silently go stale.

## `fixture.json`

| Field | Meaning |
| --- | --- |
| `stage` | `luban` - the **schema** must reject it (type, unique key, `#ref`, `#(range)`), so `tools/gen.ps1` must exit non-zero. `semantic` - Luban accepts the schema but a **game rule** is violated, so `gen.ps1` must succeed and `tools/validate-data.ps1` must exit non-zero. |
| `why` | The rule being exercised, in one line. It ends up in the harness report. |
| `expectedCodes` | For `semantic`: the finding codes that must all appear in the validator report. Empty for `luban`. |

## Running

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1 -Only price-zero
```

`tools/test.ps1` runs the whole set as a required gate (`-NoPipeline` skips it).

## Adding a case

1. Break exactly one thing in a copy of a table. Copy the table from `data/` and
   edit it: do not hand-write a CSV, because a column-order slip would make the
   fixture test the wrong failure.
2. Write `fixture.json` with the stage and the codes you expect.
3. Run `check-fixtures.ps1 -Only <case>` and confirm it passes.
4. Confirm it is a real check: temporarily set `"stage": "luban"` on a
   `semantic` case. The harness must report FAIL. A fixture that passes either
   way is not testing anything.

## What lives where

These fixtures cover the pipeline end to end (`data/` → Luban → `.bytes` →
findings). The rules themselves are also covered at unit speed by
`lib/Tests~/Core.Tests/ConfigRulesTests.cs`, which breaks one value in an
already-loaded snapshot. Both use one implementation of every rule
(`lib/Tests~/Data.Validator`), so a rule cannot pass in one place and fail in
the other.
