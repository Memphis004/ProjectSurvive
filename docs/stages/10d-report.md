# Stage 10d — the title screen, and the control that starts a run

## 1. Summary

Boot handed over to the title and stopped there, and **nothing on the title could leave it** — the loop
Stage 10 built was closed on paper and open in practice for the first screen a player sees. The title now
has one control, **เริ่มเล่น**, which starts a run through the same Application command the camp calls and
then asks the `SceneLoader` for the camp ([ADR 0022](../adr/0022-the-title-starts-a-run.md)); the run is
asked for **lazily at the moment of the press**, because the presenter is an App-scope singleton and
arriving at the title is what disposes the run scope. The screen publishes `__projectSurviveTitle`
(strings, one control, its centre in canvas pixels) so a browser test can press what the screen drew —
and that browser check is what found both defects this stage had: a document that named the new canvas
size while still aiming its click at the **old** control position (134 px out), and a control 26 px right
of the centre on the template's own 960×600 canvas, which is what corner anchoring does when the
`CanvasScaler`'s virtual width follows the aspect. Both are fixed at the cause, not at the assertion.

## 2. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| 1 | The title screen shows the game's name and a start control that **starts a run** | **PASS** — `TitleScreenTests` (5, Fast): the heading and caption come from the localization table (`TitleKeys`, both languages), the control starts the run once and refuses a second press, a live run is taken to the camp rather than restarted, and after a disposal the same control builds a **new** run. `TitleScenePlayModeTests.Boot_stops_at_the_title_and_the_start_control_walks_into_the_camp` (5/5 on its own) drives the real scenes: one run, one `RunLifetimeScope`, floor 1, starting purse, and the camp editing *that* object |
| 2 | กดได้ด้วยเมาส์ | **PASS** — the PlayMode test clicks the point the screen published (`TryGetButtonScreenPoint`) and the camp comes up; `tests/web/title.spec.ts` clicks the published point **in a built player** and the scene marker becomes `Camp`; `TitleScreenLayoutTests.The_start_control_can_be_pressed_with_a_pointer` holds the pointer half in the Fast suite (interactable, raycastable target, ≥ 44 px) |
| 3 | ตัวหนังสือ ≥ 22 px ที่ 1080p และไม่ทับข้อความ build/config | **PASS** — `TitleScreenLayoutTests` (4) reads `Title.unity` and judges it with the project's `UiLayoutCheck`: three regions pairwise disjoint and inside the canvas, every label ≥ 22 px in a ≥ 32 px line box, reading order heading → build line → control. The capture tool measures the same three rectangles at both GDD sizes: **3 regions, 0 overlaps, PASS, 1 503 ms** — `artifacts/ui/ui-report.md`, `artifacts/ui/title-{1920x1080,1280x720}.png` |
| 4 | กลับ Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่ | **PASS** — `TitleScenePlayModeTests.A_run_started_after_the_summary_is_a_new_one`: a run is played to its summary (with bia spent), the summary's own **กลับ Title** is pressed, the new title has no run scope and no run, and pressing the control yields a run that is **not the same object**, floor 1, `GhostsDefeated` 0, full HP, the starting purse, and a bag equal to a `RunSession` built from scratch on the same seed |
| 5 | Boot still stops at the title; no screen leaves on its own | **PASS** — the same test asserts the scene marker, `SceneBoot.CompletedBootCount` and the absent run *after* the handover Boot performed itself; `SceneBoot` is unchanged |
| 6 | The screen publishes what it shows and where its control is | **PASS** — `TitleReport.Publish` writes `__projectSurviveTitle` (scene marker, the two localized strings, the build line, canvas size, one named control with its centre); the PlayMode test asserts the document against the state it was published from, `ResultReport`/`CampReport`'s contract shape |
| 7 | A browser check presses the control the screen drew | **PASS** — `tests/web/title.spec.ts` is in the harness's own web step (`tools/test.ps1` now lists it): `web: Playwright (smoke + title + battle + camp + result)` **10 passed in Chrome**, 130.7 s — `artifacts/stage10d/full-editor-closed-web.log`. In the player: the marker becomes `Title`, the two strings are Thai, `diagnostics` carries `build`, exactly one control named `start` is inside the canvas, and the press ends in a camp whose report has `flow` Camp, `floor` 1, `canDepart`, a live purse and HP |
| 8 | (DoD) automated tests for the changed rules, run for real | **PASS** — 14 new Unity methods: EditMode 9 (`TitleScreenTests` 5 + `TitleScreenLayoutTests` 4) taking the assembly 121 → **130**, PlayMode 5 (`TitleScenePlayModeTests`) taking it 40 → **45**. `-Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 3** in 81.6 s — dotnet **468**, EditMode **130/130** (`artifacts/stage10d/fast-after-scene-anchor.log`). PlayMode per class, all 45 tests: **0 failures** ([`artifacts/stage10d/playmode-per-class.md`](../../artifacts/stage10d/playmode-per-class.md)) |
| 9 | (DoD) the layout judged from the Editor capture, and a screenshot of the real game | **PASS** — the capture tool's Title section shoots and measures both sizes (above), and the **player's own** 1280×720 shot of the same screen has ink in the very rectangles that report measures: brighter-than-0.55-luma share **5.24 %** of the heading band, **1.34 %** of the build line's, **1.54 %** of the control's, against **0 %** in the Editor's shots of the same bands, whose labels are empty until the runtime fills them (`artifacts/stage10d/inspect-title-shot.ps1`; `tests/web/test-results/web-title.png`, copied to [`artifacts/stage10d/web-title-player.png`](../../artifacts/stage10d/web-title-player.png)) |
| 10 | (gate) WebGL build + Playwright with the Editor closed | **PASS** — `web: WebGL build (Development)` **PASS** 70.2 s, `web: Playwright (smoke + title + battle + camp + result)` **PASS** 10/10, whole suite **PASS 11 \| FAIL 0 \| NOT RUN 2** in 268.4 s — `artifacts/stage10d/full-editor-closed-web.log`. The two NOT RUNs are EditMode and PlayMode, a closed Editor holding no seat for them; both were measured minutes earlier with it open (EditMode **130/130**, PlayMode **45** per class) |
| 11 | (DoD) docs and commands | **PASS** — `knowledge.md` (status + current state), `docs/testing/test-suites.md` (the four counts, the three new classes, the new spec, the step's own name, §7's flake record), `llmwiki/systems/loop-and-scopes.md` §6 (the screen) and §5 (the two facts), `llmwiki/stage-log.md`, and this report |
| 12 | A decision recorded rather than left in the code | **PASS** — [ADR 0022](../adr/0022-the-title-starts-a-run.md): where a run starts, why the presenter holds no run, the intent/commit split, why Space is read explicitly, and the report's contract — with the alternatives that were refused (`the camp starts the run`, holding the `RunSession`, a `UnityEvent` in the scene, an `InputActionAsset`, a rectangle instead of a centre, and a document that reports a permission) |

## 3. Commands run

```pwsh
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                    # (1) FAIL 1: the pinned row count, below
dotnet test lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj --filter FullyQualifiedName~ConfigLoadTests
                                                                    # 19/19 after the pin moved 187 -> 188
unity open UnityProject ; unity status                              # the Editor the Unity steps need
unity command menu --path "ProjectSurvive/Stage 10d/7. Create Title scene (start control)"
unity command menu --path "ProjectSurvive/Tools/1. Capture UI (Title + Camp + Battle + Result, 1920x1080 + 1280x720)"
                                                                    # PASS, 0 failures, 1 503 ms, 3 regions, 0 overlaps
unity command run_tests --mode editor --filter TitleScreen          # 9/9, 3.6 s
unity command run_tests --mode playmode --async_tests --filter TitleScenePlayModeTests   # 5/5, 6.6 s
unity command run_tests --mode playmode --async_tests               # the bulk run: 25 of 45 failed (§5)
# ...then every class on its own: CampBagDrag 12/12, CampMergeChooser 4/4, CampWorld 8/8,
#    Battle 4/4, ResultScene 3/3, SceneFlow 5/5, Shell 4 reported/4 passed
pwsh -NoProfile -File artifacts/stage10d/inspect-title-shot.ps1 -Path tests/web/test-results/web-title.png -Scale 1280x720
pwsh -NoProfile -File tools/test.ps1 -Suite Fast                    # PASS 10 | FAIL 0 | NOT RUN 3, 81.6 s, EditMode 130/130
unity close UnityProject                                            # the WebGL build needs the project lock
pwsh -NoProfile -File tools/test.ps1 -Suite Full                    # (1) FAIL 1: the browser's two findings, §5
pwsh -NoProfile -File tools/test.ps1 -Suite Full                    # PASS 11 | FAIL 0 | NOT RUN 2, 268.4 s
```

## 4. Evidence

- `artifacts/stage10d/fast.log` — the **first** Fast run, kept because it failed: `dotnet test` exit 1,
  `ConfigLoadTests.Sample_tables_load_and_expose_every_row` expected 187 and got 188. The table gained the
  one string the title's control draws (`title.start`), so the pin moved and its comment now names the row
  — the same correction the pin's own comment says it exists for.
- `artifacts/stage10d/fast-after-scene-anchor.log` — the closing Fast run: **PASS 10 | FAIL 0 | NOT RUN 3**,
  81.6 s, dotnet **468**, EditMode **130/130**.
- `artifacts/stage10d/full-editor-closed-web.log` — the closing Full gate: **PASS 11 | FAIL 0 | NOT RUN 2**,
  268.4 s, WebGL **PASS** 70.2 s, Playwright **10 passed in Chrome** 130.7 s.
- `artifacts/stage10d/playmode-per-class.md` — the 45 PlayMode tests, class by class, and what the bulk
  payload does instead (and why a cascade of that flake looks like a real defect).
- `artifacts/ui/ui-report.md` + `artifacts/ui/title-{1920x1080,1280x720}.png` — the screen measured at both
  GDD sizes; `artifacts/stage10d/web-title-player.png` — the same screen in the built player, the file the
  pixel measurement above reads.
- `artifacts/stage10d/inspect-title-shot.ps1` — the pixel check, in the report's own coordinate convention.

## 5. The two defects the gate found, and what the bulk suite is not

The first closed-Editor Full run **failed**: WebGL **PASS**, Playwright **8 passed, 2 failed**. Both
failures are kept as facts even though the second run overwrote their logs at the same path
(`artifacts/dotnet/playwright.log`, `artifacts/stage10d/full-editor-closed-web.log`), so the numbers below
are quoted from the failing run as it was read.

1. **`title.spec.ts` — `the control is not where the canvas centre is`: expected ≤ 2, received 134.036.**
   The report the player published said `canvasWidth: 1280` and `buttons[0].x = 505.964`, which is the
   control's position at the fixture's **960×600** — the document was written for the new size with the
   old geometry. The cause is a frame boundary, not arithmetic: `Screen` reports a new canvas size in the
   frame the element changed, and the `CanvasScaler` lays the widgets out of it during that same frame's
   render, so the one publish triggered by the size change is a frame early and, before the fix, was the
   last word. `TitleSceneDirector` now also republishes while the point it published and the point the
   view reports disagree (`CampScreenRects.TryCentre` allocates nothing, which is what makes a per-frame
   read acceptable, and the camp reads the same helper at its own, slower rate). The spec polls the
   *point at the new size* rather than the size alone, so a document that never settles still fails.
2. **The same assertion's other half: `505.964` where `480` was expected at 960×600.** The control was
   laid out from the canvas' **top-left corner** with 1920-wide arithmetic (760 + 200 = the centre at the
   reference aspect) while the two labels above it were centre-anchored — and a `CanvasScaler` in
   `ScaleWithScreenSize` mode gives the canvas a *virtual width that follows the aspect*, 1821.6 units at
   960×600, so the corner arithmetic lands ~26 px right of the centre. `ProjectSetup.CreateUiButton` now
   takes an optional `anchor` (default: the corner every panel and column in this project uses) and the
   title passes the centre; the scene was rebuilt from the builder, not edited. After the fix the player's
   own report reads `buttons[0].x = 480` of 960 and `640` of 1280.

Two things this stage did **not** treat as its own defects, recorded so the next reader does not chase
them: the same run's `camp.spec.ts` resize test failed on `bagVisible` staying true after a resize click —
the **documented** click-after-resize flake that spec's own `setCanvasSize` comment describes, and it
passed on the rerun and in the per-class run; and the **bulk** PlayMode run failed 25 of 45 for the Input
System / R3 ambient log-assert flake of `docs/testing/test-suites.md` §7. The second one is worth naming
here because its *cascade* reads like real defects in this stage's own class: a test that dies on the log
assert skips its teardown, so the next fixture inherits a half-finished transition and reports
`the boot flow did not stop at the title ... But was: "Boot"`, `no RootLifetimeScope is loaded`, or a
`NullReferenceException` from a `Find<T>()`. Run on its own the class is **5/5**; every class is green on
its own; the suite, run as a suite, still does not pass.

## 6. Not tested, and risks

- **The title's strings in a Release build are not checked.** Every browser step here runs the Development
  configuration, so what is proven is that the scene, the scope and the localization lookup survive IL2CPP
  in *that* build; a stripped Release build is the stage-15 question, as for the Result screen.
- **The un-centred-at-16:10 half of defect 2 was found by reading the number, not by looking at a
  picture.** The capture tool's two sizes are both the reference aspect, so nothing in the Editor's own
  images shows it — which is the general lesson: a layout rule about *centring* cannot be judged at 16:9
  alone. The browser fixture's 960×600 is what made it visible, and the new assertion at that size is now
  what holds it.
- **The per-frame cost of the republish check is unmeasured.** It is two int comparisons, one
  `GetWorldCorners` and two `WorldToScreenPoint` calls on a three-object UI, with the report string built
  only when something changed — but no frame-time number was taken for it, and none is claimed.
- **`Screen.width` is the whole trigger for a republish.** A canvas resized while the *screen* size stays
  the same (a scaled backing store, a device-pixel-ratio change) would not be noticed; no browser in this
  suite does that, and the point comparison would republish on the frame after the geometry moved, but the
  case is not covered by a test.
- **`ShellPlayModeTests` still under-reports itself**: five `[UnityTest]`s declared, four reported, and on
  2026-10-17 its own per-class run carried a summary row of `total 0` beside four passing results. That is
  a pre-existing runner oddity (the missing one is
  `Thai_spike_scene_measures_both_stacks_without_missing_glyphs`, not marked `[Explicit]`), not this
  stage's, and the Thai coverage it would assert is measured instead by
  `artifacts/thai-font-bake.json` (`missingCount 0`, `coversRequiredSet true`).
- **The screen's *look* is the user's to accept, not a test's.** The capture and the pixel check say the
  three regions do not overlap, sit at or above the 22 px Thai floor, and the player's own shot has ink
  inside the measured rectangles — none of that judges whether the screen reads well. The shot is
  [`artifacts/stage10d/web-title-player.png`](../../artifacts/stage10d/web-title-player.png) (the
  browser suite's own `tests/web/test-results/web-title.png`, taken at 1280×720, copied because the spec
  overwrites it on every run); reading a passing assertion as approval of the look is exactly what the
  Definition of Done forbids.
- **`data: broken fixtures rejected` is still the slowest Fast step** (~57 s of the 82 s), unchanged from
  Stage 10's note.
- Unrelated working-tree file left untouched and **not** part of this stage's commit:
  `UnityProject/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset` — a
  Unity 6 re-serialisation of a Dynamic fallback asset that holds one character, as the 09f report
  recorded.
