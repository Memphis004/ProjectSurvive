# T2 — Data, schema, proto and the validator: status report

Worktree `C:\Workspace\ProjectSurvive-template`, branch `template/main`, on top of T1 (`648dbe0`).
Scope: shrink the data pipeline to the tables a template needs, and make the validator keep the rules a
game will actually copy — each with a fixture that proves it fires.

## 1. Summary

Static data is now three tables: `constant` (one row of tuned numbers), `localization` (the only source of
player-facing text) and `sample` — a worked example of the two ways one table points at another. The
`sample` table exists because the template had no table that could carry a reference, so neither the schema
`#ref` nor the validator's "a data row points at a missing localization key" rule had anything to run on.
`proto/app.proto` is down to `CheckpointProbe` and `SaveHeader`, and the shell's Protobuf probe now
round-trips the former, so the browser still proves the contract. The validator gained four rules and the
fixture set went from two to six, split by which stage must reject each one. 124 xUnit tests pass, the
regenerated tables reproduce, and the built player loads the new config.

## 2. What changed

| Area | Change |
|---|---|
| `data/Defines/schema.xml` | `Sample` bean added: `id`, `name_key` (`string#ref=TbLocalization`), `value` (`int#(range=[0,1000000])`), `note_key` (plain optional string). The comment explains why the two reference kinds are checked by different tools — and notes that an XML comment cannot contain a double dash, which Luban failed on first |
| `data/Defines/__tables__.csv` | `TbSample` row added (`index=id`) |
| `data/tables/sample.csv` | new: three rows, one with an optional `note_key` filled and one with it empty |
| `data/localization/localization.csv` | five rows added: `ui.generic.error` and the four `ui.sample.*` keys the sample table points at (28 rows total) |
| `proto/app.proto` | `Platform`, `AppInfo`, `ClientHello`, `ServerHello` and `RunReport` removed; `CheckpointProbe` (schema version, config hash, tick, note key) and `SaveHeader` (schema version, config hash) added |
| `Domain/Config` | `SampleEntry` added; `IGameConfig` exposes `Samples` |
| `Infrastructure/Config` | `LubanGameConfig.TableNames` is `tblocalization, tbconstant, tbsample`; the adapter maps the sample rows |
| `Data.Validator` | new rules: `localization.missing_language`, `sample.blank_id`, `sample.duplicate_id`, `sample.empty`, `sample.missing_name_key`, `sample.missing_note_key`; a reference is resolved through `IGameConfig.TryGetLocalization`, not a second lookup table |
| `lib/Tests~` | `ConfigRulesTests` +6 cases, `ConfigLoadTests` +1 (and `TableNames` now three), `ProbeSerializationTests` rewritten for the new messages; `Sim` prints `seed` and `configHash` in its header line |
| `lib/Tests~/fixtures` | `duplicate-id`, `missing-ref`, `missing-note-key`, `missing-language` added; `blank-text` had to grow the sample keys, because its overlay localization table left the sample rows dangling and Luban rejected a fixture that is meant to be schema-valid |
| docs | `llmwiki/systems/data-schema.md` rewritten for the three tables; the 13-table version moved to `docs/template/examples/systems/data-schema.md` with an "example, not a rule" header; `validate-data.ps1`'s description no longer lists game rules |

## 3. Acceptance (measured)

| Check | Result | Evidence |
|---|---|---|
| `tools/gen.ps1` | **PASS** — 8 `.cs`, 3 `.bytes`, config hash `DDFDF338…` | §5 command 1 |
| `dotnet test` Core.Tests | **PASS** — 124 passed, 0 failed (was 115) | §5 command 2 |
| `Data.Validator --data lib/Tests~/data` | **PASS** — no findings (localization 28, sample 3) | §5 command 3 |
| `tools/verify-generated.ps1` | **PASS** — 8 PASS, 1 WARN (the generated files are new on disk, i.e. not yet committed), regeneration reproduces the tracked output | §5 command 4 |
| `tools/check-fixtures.ps1` | **PASS** — 10 PASS / 0 FAIL: six fixtures, each rejected by the stage its `fixture.json` names | §5 command 5 |
| `lib/Runtime/Generated` + `Resources/Config` hold no game table | **PASS** — 9 generated files for three tables (`Constant`, `Language`, `Localization`, `Sample`, `TbConstant`, `TbLocalization`, `TbSample`, `Tables`, `ConfigBuildMetadata`) and three `.bytes` | §5 command 6 |
| `tools/test.ps1 -Suite Full` (Editor closed) | **PASS** — 10 PASS, 0 FAIL, 2 NOT RUN (the two Unity suites) | §5 command 7 |
| `unity: EditMode` / `unity: PlayMode` (Editor open) | **PASS** — 30/30 and 6/6 | §5 commands 8–9 |
| Web build + Playwright (`smoke`, `title`, `loop`) | **PASS** — 4 passed in Chrome; the smoke report carries the new config (`hash=DDFDF338 generated=DDFDF338 localization=28`) | §5 command 7; `artifacts/dotnet/playwright.log` |
| `tools/sim.ps1` prints seed + config hash | **PASS** — `seed=7 configHash=DDFDF338… tables=localization 28, sample 3, maxHp 100` | §5 command 10 |

## 4. Decisions worth recording

- **`sample` is a real table, not a fixture directory.** A table that only a test can reach would leave the
  loader, the adapter and the port untested; a table that ships means every build exercises the reference
  path (the browser smoke probe reads the same hash the data gate produced).
- **Two reference columns, on purpose.** `name_key` is a schema `#ref` (dangling cell ⇒ generation stops);
  `note_key` is plain and checked by the validator, because an empty cell is a legal `#ref` and Luban cannot
  express "if set, then it must exist". A game copies whichever its column needs; the fixtures keep both
  paths honest.
- **`duplicate-id` is a `luban`-stage fixture.** The table is keyed by id, so Luban collapses a duplicate
  *before* the loader runs — the same reason T1 could not keep the old `duplicate-id`. The rule still exists
  for snapshots and is covered by `ConfigRulesTests`.
- **`localization.blank_text` and `localization.missing_language` stay separate.** The runtime falls back to
  the language that exists, so a half-filled row shows a player the wrong language rather than crashing —
  a different cause from a row with no text at all, and a different fix.
- **`CheckpointProbe` in the proto shares a name with the MessagePack DTO.** Both answer "does serialization
  work off the desktop runtime?". The collision is a compile error, not a silent one, and the two Unity call
  sites plus the xUnit file alias both names.

## 5. Commands actually run

```bash
pwsh -NoProfile -File tools/gen.ps1                                       # 1
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -v q --nologo           # (part of 2)
dotnet test  "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj"     # 2. 124 passed
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"  # 3
pwsh -NoProfile -File tools/verify-generated.ps1                          # 4
pwsh -NoProfile -File tools/check-fixtures.ps1                            # 5. 6 fixtures
ls lib/Runtime/Generated/Luban/*.cs ; ls lib/Runtime/Generated/Proto      # 6
pwsh -NoProfile -File tools/test.ps1 -Suite Full                          # 7. PASS 10 | FAIL 0 | NOT RUN 2
# Editor open:
#   unity: EditMode 30/30, unity: PlayMode 6/6                            # 8-9
pwsh -NoProfile -File tools/sim.ps1 --seed 7 --ticks 300                  # 10
```

## 6. Risks

- **`Sample` is dead weight the moment a game has its own table.** It is three rows and one bean, and the
  doc says to delete it — but a project that never deletes it carries a table nothing reads. The validator's
  `sample.empty` rule is the only thing that would ever notice.
- **The `missing-language` fixture is a full copy of the localization table with one cell emptied.** It will
  drift as rows are added; when it does, the fixture still fails for its own reason (the checks only require
  the expected code to be reported), but its "why" line can go stale.
- **`SaveHeader` has no reader yet.** It is the contract Stage 12 will use; until then nothing but a unit
  test touches it, which is exactly the state ADR-free placeholders end up in. It is two fields.
- **The proto trim is a breaking wire change.** Nothing outside this repo consumed `AppInfo` (T0's inventory
  checked), but an external peer from before the cut would not parse the new messages.
- **One SQL-free pipeline still means no uniqueness check across tables.** If two tables ever share an id
  namespace (T0's Q7 territory), no rule today compares them; that arrives with a game's first pair of
  cross-referenced tables.
