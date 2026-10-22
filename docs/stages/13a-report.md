# Stage 13a — Content complete, text fit, attribution

**Date:** 2026-10-22

## 1. Summary

Stage 13 was split (AGENTS rule: it touched Core, Unity scenes, UI and Web tests at once) into
**13a audit**, 13b tutorial and 13c audio/pause. This is 13a. The GDD's prototype content was already
in `data/` — 14 pieces (3 bags + 11 items), three ghosts + the two-phase Pob boss, five floors, five
combo rules — and is now **pinned by a test** rather than by inspection. Two player-facing strings
were the only text still embedded in code (the chest panel's heading and the camp's combined HP/MP
line); both moved into `data/localization/localization.csv`, and two architecture tests now refuse a
Thai literal or a label handed a literal in any runtime script. A new in-player audit
(`UiTextFitReport`, `__projectSurviveTextFit`) measures every label on every screen — rectangle,
needed size, missing glyphs — and the browser suite asserts it. It found a **real defect**: the battle
HUD's MP caption ("พลังอาคม", needs 89 px) was authored into a 58 px box and wrapped onto a second
line inside 24 px, i.e. clipped; nine battle labels were also 0.6 px shorter than 22 px Sarabun's line
box. All ten are fixed, and the rebuilt HUD measures 30 labels / 0 overflow / 0 missing glyphs in the
real WebGL player. `ATTRIBUTION.md` records every asset's licence; the one unresolved item is the
unused `THSarabunPSK` font, whose licence is not the OFL and which still ships from `Resources/`.

## 2. Acceptance

| # | Item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Content matches the GDD: ~14 items, 3 ghosts + Pob boss, 5 floors, no new class or meta | **PASS** | `ConfigLoadTests.Gdd_prototype_content_is_complete_and_nothing_more` (category counts 4/3/1/2/1 + 3 bags = 14; every piece merges to tier 3; Filler/Fast/Elite + boss with phase-2 HP, corpse eat and possession; floors 1–5 with 60 s / 90 s boss and the GDD's escalation; 5 synergies) |
| 2 | No player text embedded in code | **PASS** | `ArchitectureTests.Runtime_sources_embed_no_player_facing_text` and `Every_localization_key_the_runtime_asks_for_exists` (601 xUnit green); the sweep's two hits fixed — `camp.chest.title.format`, `camp.status.hpmp.format`, table now **218 rows**; the guard was proved to fail by injecting a Thai literal into `CampPresenter` (FAIL) and reverting (PASS) |
| 3 | Thai font and overflow tested in the Web player | **PASS** | `__projectSurviveTextFit` read by `tests/web/textfit.mjs` in title/camp/battle/result specs: **Title 9 labels (8 Thai), Camp 35 (29), Battle 30 (18), Result 7 (7) — 0 overflow, 0 missing glyphs**, all 13 browser tests pass ([playwright HTML](../../artifacts/stage13a/playwright-report.html), [last-run](../../artifacts/stage13a/playwright-last-run.json)) |
| 4 | Every asset's licence checked and attribution recorded | **PASS** | [`ATTRIBUTION.md`](../../ATTRIBUTION.md) — Sarabun OFL 1.1 with in-repo licence text; art is procedural project-owned; audio none yet (13c); vendored/package notices located; **risk recorded**: `THSarabunPSK` ships from `Resources/` under a non-OFL SIPA licence and is unused |
| 5 | A new player can start the first battle unassisted (≥ 1 real person) | **NOT RUN** | belongs to 13b/13c: the tutorial does not exist yet, so there is nothing for a person to test. The browser suite proves the *loop* is reachable, not that it is self-explanatory |

Other evidence: `tools/test.ps1 -Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3** (601 xUnit, 178/178
EditMode, data gates, wiki lint; [summary](../../artifacts/stage13a/test-summary-fast.md)); the Editor
capture `ProjectSurvive/Tools/1. Capture UI` **PASS — 0 failures, 0 overlaps**, including 37 battle
regions at 1920×1080 and 1280×720 after the HUD change ([report](../../artifacts/ui/ui-report.md),
[battle 1080](../../artifacts/ui/battle-begin-1920x1080.png)); the Web player was rebuilt
(`configHash A9738E31…`) and its smoke report names `localization=218 items=11 enemies=4`.

## 3. What was not tested, and the risks

- **The human playtest is not done** — Stage 13's headline acceptance needs 13b's tutorial, and a real
  person. Nothing here can stand in for that; it must be recorded in 13b's report.
- **Settings-panel labels are only measured while the panel is closed.** Their text is populated by the
  presenter, so the audit sees them, but the open panel's layout is still only checked by the Editor
  capture (0 overlaps), not by the player.
- **Labels that appear later in a floor** (boss band, possession line) are measured only if a text-fit
  publish happens after they are filled; the battle report publishes every 5 s, so a browser run that
  reaches the boss will catch them, but floor 1 in the suite does not.
- **The script scans Thai literals and direct label assignments**, not English user-facing strings in
  general. A translated-looking English phrase assigned through `UiState` would not be caught.
- **Firefox is still not installed**; all Web evidence is Chrome `154.0.8037.98`, a Development build.
- **`THSarabunPSK` must be removed from `Resources/` before a release candidate** (Stage 15): unused,
  4.0 MiB, and its licence is not the OFL the rest of the UI ships under.

## 4. Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" --filter "FullyQualifiedName~Gdd_prototype_content|Runtime_sources_embed|Every_localization_key"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast     # PASS 11 | 0 | 3
# Unity Editor (Pipeline): recompile, ProjectSurvive/Tools/1. Capture UI, play-mode spot checks
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Force
node tools/serve-web.mjs --port 8080          # background
cd tests/web; npx playwright test title.spec.ts camp.spec.ts
cd tests/web; npx playwright test smoke.spec.ts battle.spec.ts result.spec.ts persistence.spec.ts
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1
```
