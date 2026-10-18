# Gotcha — a `data/` edit is not in the build until `gen.ps1` runs

`data/*.csv` is the source; what the game reads is the generated output:
`lib/Runtime/Generated/**` (C# readers), `lib/Tests~/data/*.bytes` (tests and the
validator) and `UnityProject/Assets/Main/Resources/Config/*.bytes` (the player). A
CSV edit alone changes nothing that runs.

The game-era cost: 81 localization rows were added and the tables were never
regenerated, so the first browser evidence of that stage described a player with 64
rows instead of 145 — the new screen's labels were simply absent. `verify-generated`
also failed, because the committed `ConfigBuildMetadata` hash no longer matched the
bytes.

**The loop is always:** edit `data/` → `tools/gen.ps1` →
`tools/verify-generated.ps1` → then build or test. `tools/test.ps1` runs both data
gates in Fast, so a forgotten regeneration fails a check rather than a browser run.

**The exception is intentional:** the generated `.bytes` under
`UnityProject/Assets/Main/Resources/Config` are produced by `gen.ps1`, never copied
by hand, and never edited. If a value looks wrong in a player, change the CSV and
regenerate.
