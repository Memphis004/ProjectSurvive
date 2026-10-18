# Gotcha — `validate-data.ps1` defaults to `data/`, which holds no `.bytes`

The validator reads the **generated** tables (`.bytes`), not the CSV: it goes through
the same generated readers the game uses, which is what makes its verdict about the
real data path. Generated tables are published to `lib/Tests~/data`, so the default
`-DataDir data/` finds nothing and reports `table_missing` — which looks like a data
bug and is a path bug.

```powershell
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
```

`tools/test.ps1` passes the right directory itself, so the step inside Fast is always
correct; the trap is running the script by hand. The same argument accepts a fixture
directory (`lib/Tests~/fixtures/<name>/data`) when you want to see why a broken
fixture is rejected.
