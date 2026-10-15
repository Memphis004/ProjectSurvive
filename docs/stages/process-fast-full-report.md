# Process — Fast/Full test suites, and a UI check that does not need a Web build

Not a game Stage: no game code, no `data/`, no scene was touched. The work is the loop itself —
`tools/test.ps1` is now a Fast/Full runner, the layout can be judged from an Editor command instead of a
WebGL build, and `AGENTS.md` asks for a one-page report instead of a long one.

## 1. Summary

Five prompts' worth of round-tripping through `dotnet` + EditMode + PlayMode + a WebGL build + Playwright
is now **Fast ≈ 72–77 s warm (229 s cold)** for the coding loop and **Full** for closing a Stage.
Nothing was dropped moving the tests: 514 → **522 tests** in the two suites together (the 8 new ones are
the overlap rule's own tests), and every test has a named home in
[`docs/testing/test-suites.md`](../testing/test-suites.md). A new Editor command renders Camp and Battle
at 1920×1080 and 1280×720 in **~1 s** and fails when two panels that must stand apart share pixels —
proved by running it with a deliberate collision. The one Full run of record with the Editor closed
finished **11 PASS / 0 FAIL / 2 NOT RUN** in 204.5 s.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Real per-step timing table in `docs/testing/test-suites.md`, and a complete test → suite mapping | **PASS** | [`docs/testing/test-suites.md`](../testing/test-suites.md) §2 (cold and warm per step, machine spec) and §3 (every dotnet/EditMode/PlayMode/Playwright/Sim/data test and its suite) |
| 2 | `-Suite Fast` and `-Suite Full` run, with PASS / FAIL / NOT RUN separated | **PASS** | `artifacts/proc10/fast-run1.log` (cold, `PASS 10 / FAIL 0 / NOT RUN 3`), `fast-run3.log` (final script, Editor open, 65/65 EditMode, 72.1 s), `full-editor-open.log` (`FAIL 1`: PlayMode §3), `full-editor-closed2.log` (`PASS 11 / FAIL 0 / NOT RUN 2`); summaries in `artifacts/test-summary.md` and `artifacts/proc10/*-summary.md` |
| 3 | Fast + Full run no fewer tests than before | **PASS** | before: 425 dotnet + 57 EditMode + 24 PlayMode + 8 Playwright = 514. now: 425 + **65** + 24 + 8 = **522** (`unity: EditMode 65/65`, `dotnet test 425 passed`, `unity: PlayMode 24`, `Playwright 8 passed`) |
| 4 | UI capture produces both sizes of Camp and Battle, and the overlap check can really fail | **PASS** | `artifacts/ui/{camp,battle}-{1920x1080,1280x720}.png` (+ `camp-closed-*`), exact sizes verified from the PNG headers, `artifacts/ui/ui-report.md`; the deliberate-overlap run wrote `artifacts/ui/ui-report-selftest.md` with **4 failures** naming the colliding pair at both sizes; `UiLayoutCheckTests` (8 cases) is in Fast |
| 5 | `AGENTS.md` per the prompt, and this report in the new short format | **PASS** | `AGENTS.md` (Fast/Full rule, Editor layout check, ADR and stage-log rules, the >7-item split rule, 4-part ≤1-page report) — this file is that format |

## 3. Not tested, and the risks

- **The bulk PlayMode run still fails, and the Full suite reports it as FAIL** (12/24, no failed
  assertion: Unity's ambient Input System log asserts). This is the unsolved item already recorded in
  [`llmwiki/stage-log.md`](../../llmwiki/stage-log.md) from Stage 09d, not something this change caused
  or fixed. It is deliberately *not* papered over — the harness names the failing tests and leaves
  `artifacts/dotnet/unity-playmode-status.json`; §7 of the suite doc says how to tell a real failure
  from it.
- **The capture's images contain no text.** Every label in `Camp.unity` is empty until the runtime fills
  it from the localization tables, so an EditMode shot shows panel geometry and backgrounds; the
  runtime layout is measured by `CampBagDragPlayModeTests`. Documented in the report itself
  (`ui-report.md → Notes`). A play-mode capture would fix it and is not built.
- **One Full run cannot report everything.** Unity steps need the Editor open, the WebGL build needs it
  closed, so the verdict takes two runs (both kept, `artifacts/proc10/full-*-summary.md`). The harness
  says which steps were NOT RUN and why, but a reader still has to read two files.
- **The batch `-executeMethod` path needs the Editor closed** (one process owns the project lock) — same
  constraint as `tools/build-web.ps1`. It was verified this way: 6 images, **byte-identical** to the menu
  run's, exit 0.
- **`data: broken fixtures rejected` is the slow step (47–52 s, the largest part of Fast)** because it
  spawns one validation process per fixture. The suggested fix (one process walking all 17) is not
  implemented.
- **Two things are still measured by hand:** the cold-vs-warm numbers above come from the runs named in
  §2, not from a benchmark harness, and there is no CI wiring for either suite yet.

## 4. Commands actually run

```powershell
# the two suites (Editor open for EditMode; Editor closed for the WebGL step)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1                 # Fast  (3 runs: cold, warm, final)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full     # Full  (Editor open, then closed)
powershell -NoProfile -ExecutionPolicy Bypass -File artifacts/close-unity-editor.ps1

# the UI capture: menu item through the open Editor, then the scripted batch form
unity command menu --path "ProjectSurvive/Tools/1. Capture UI (Camp + Battle, 1920x1080 + 1280x720)"
unity command menu --path "ProjectSurvive/Tools/2. Capture UI self-test (expected to FAIL)"
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode `
  -projectPath C:/Workspace/ProjectSurvive/UnityProject `
  -executeMethod ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit `
  -logFile artifacts/proc10/executeMethod.log

# how the capture mechanism was chosen and checked before anything was written (measured, not assumed)
unity command run_tests --mode editor --async_tests / test_status --result-only   # 57 -> 65 EditMode, ~10 s, not 600 s
# + an eval probe that rendered the camp through a RenderTexture at 1280x720 and read the board's
#   rectangle back as {26.667, 213.333, 493.333, 386.667} — the number the browser published in Stage 09
python -c "..."   # PNG header sizes, and per-region colour counts that caught the empty-label issue
```
