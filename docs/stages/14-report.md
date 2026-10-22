# Stage 14 — Automated verification and CI

## Summary

The verification story is now two commands: `tools/test.ps1 -Suite Fast|Full` is the working loop, and
`tools/ci.ps1` is the gate (`Validate → Test → Sim → Unity → Build → Web`, licence jobs kept strictly
one Unity process at a time). A development-only harness (`HarnessBridge`) lets a browser test start a
run from a named seed and advance a floor by a known tick count **through the same use cases the
screens use**, and it is compiled out of a Release player — `tests/web/release.spec.ts` reads that
absence out of the build that would ship. Playwright now waits for each screen's published handshake
instead of sleeping, and every spec fails on a `console.error` or a `pageerror`.

The one thing this stage did not fix is the PlayMode suite's own two old wounds: a 20-visit heap case
that fails in every environment (measured at 5.07 MB against a 3 MB limit) and a bulk-run flake caused
by Unity's ambient log asserts. Both are classified below with evidence, not waved past.

## Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `ci.ps1` exits ≠ 0 when a required check fails | **PASS** | A `sim` run forced outside its band exited **1** with `RESULT: FAILED` ([log](../../artifacts/stage14/ci-fail-path.log), [summary](../../artifacts/stage14/ci-fail-summary.md)); a blocked gate (port 8080 held by a stale server) exited **2** with `RESULT: BLOCKED`, never as a pass |
| 2 | The report separates PASS / FAIL / NOT RUN | **PASS** | `summary.md` counts and lists the three separately ([Fast](../../artifacts/test-summary.md) `PASS 13 / FAIL 0 / NOT RUN 3`; [failure run](../../artifacts/stage14/ci-fail-summary.md) `PASS 0 / FAIL 1 / NOT RUN 0`; [release web](../../artifacts/stage14/ci-web-release-summary.md) `PASS 1 / FAIL 0 / NOT RUN 0`) |
| 3 | A browser test does not pass merely because a canvas exists | **PASS** | Every spec waits on a document the screen publishes (`__projectSurviveSmoke`, `__projectSurviveScene`, `__projectSurviveBattle`…), asserts its content, and fails on `console.error`/`pageerror`; `release.spec.ts` first proves the player booted, so "no harness global" cannot be true of a blank page ([spec](../../tests/web/release.spec.ts)) |

### The seven work items

| Item | Verdict | Where |
| --- | --- | --- |
| 1 `test.ps1` composes dotnet / EditMode / PlayMode / Web and summarizes | **PASS** | Fast suite: 13 steps PASS, 3 Full-only steps reported NOT RUN with the reason, `artifacts/test-summary.md` + `.json` |
| 2 Development-only WebBridge: ready handshake, seeded start, snapshot, test input, tick advance, diagnostics, all through real use cases | **PASS** | `HarnessBridge.cs` (`HarnessProtocol` opcodes 1–4), `BattleSimulationRunner.Harness.cs`, `RunScopeOwner.Harness.cs`; **no bypass command exists**, so nothing here counts as combat coverage |
| 3 Playwright: smoke, persistence, run-loop; handshake not sleeps; console errors; bridge tests kept apart | **PASS** | `tests/web/harness.spec.ts` is the only bridge-driven file (3 cases: a named seed starts the real run, N ticks play a floor, a command it does not own is refused); the other eight specs press drawn controls |
| 4 screenshots, traces, logs, XML/JSON and build metadata under `artifacts/` | **PASS** | `artifacts/ci/`: `summary.md|json`, per-step logs, `editmode.xml`, `playmode-<fixture>.xml`, `ci-tests.trx`, `balance-50.csv`, `build-info.json`, `playwright.json`, `web-test-results/`, `web-report/` |
| 5 `ci.ps1` = gen → validate → verify-generated → test → sim (50 seeds, band) → build → web | **PASS** | [Validate](../../artifacts/stage14/ci-sim-50.log), [Web release](../../artifacts/stage14/ci-web-release.log); sim refused to leave a win rate outside 0.30–0.60 |
| 6 One Unity process per project; licence jobs apart from pure validation | **PASS** | `-Job Validate` needs no licence; `Unity`/`Build`/`Web` refuse to start while an Editor is connected or `Temp/UnityLockfile` exists (observed: exit 2), and remove the lockfile only when no `Unity` process is alive |
| 7 A Release build has no harness API | **PASS** | `release.spec.ts`: `__projectSurviveHarness`, `…Send`, `…Snapshot` and `__projectSurviveDevMenu` are all `undefined` in the shipped artefacts ([result](../../artifacts/stage14/playwright-release.json), [build metadata](../../artifacts/stage14/build-info-release.json) `development: false`) |

## Measured results

| Suite | Result |
| --- | --- |
| xUnit (`dotnet test`) | **604 / 604** |
| `contract: FakeBattleSession`, `contract: BattleSimulation` | **24 / 24** each |
| EditMode | **205 / 205** (17.8 s step) |
| Sim, 50 seeds | **22 / 50 boss wins = 44.0 %** inside 0.30–0.60, 250 floor rows |
| WebGL, Release | `development: false`, 66,043,566 B, 338.9 s |
| WebGL, Development | `development: true`, 122,308,949 B, 384.4 s |
| Playwright, Release | **19 passed / 1 skipped / 0 failed** of 20 ([json](../../artifacts/stage14/playwright-release.json)); the skipped case is `result.spec.ts`'s lost-floor run, which needs the dev menu a Release build must not have |
| Playwright, Development | **21 passed / 0 failed** — the same 19 UI cases (the lost-floor one included) plus `harness.spec.ts`'s 3 ([json](../../artifacts/stage14/playwright-harness.json), [summary](../../artifacts/stage14/ci-dev-summary.md)) |
| Firefox | The Release player boots in Playwright's bundled Firefox (`ms-playwright/firefox-1555`; no system Firefox is installed): `WebGL 2.0`, smoke passed, 0 errors, harness `undefined` — [probe](../../artifacts/stage14/firefox-probe.json). The full suite is still Chrome-only |

### PlayMode, one fixture per process ([details](../../artifacts/stage14/unity-playmode-per-class.md))

| Fixture | Batch | Open Editor |
| --- | --- | --- |
| Battle / MergeChooser / ShelfTooltip / CampWorld / ResultScene / TitleScene | 6+4+5+9+3+5 — all pass | not re-run |
| CampBagDrag | 24 / 25 — the layout case reads a 640×480 Game View | **25 / 25** |
| CampSynergyLine | 8 / 9 — same cause | **9 / 9** |
| SceneFlow | **4 / 5** — 20-visit heap case, 17,764,352 B batch / 5,066,752 B open, limit 3,145,728 B | **4 / 5** |
| Shell | 0 cases reported (the framework quirk §7 has recorded since Stage 10) | 0 |

The heap case is **not** this stage's: with the harness install commented out of `SceneBoot` the heap
still grew by 5,865,472 B. It belongs to the stage that owns the camp/battle scene lifetimes.

## Not tested, and risks

- **`tools/test.ps1 -Suite Full` as one command: NOT RUN.** Its PlayMode step is the bulk run that the
  ambient-log flake kills (76 discovered, 0 completed), and its Web step would overwrite the Release
  player with a Development one. The same ground was covered by `ci.ps1 -Job Unity` (per fixture),
  `-Job Build -Configuration Release` and `-Job Web -Configuration Release`.
- **PlayMode batch mode**: the two layout cases need a 1080p-or-larger Game View, which `unity test`
  cannot be given. They pass in the open Editor, which is where all earlier stages measured them.
- **Firefox full suite: NOT RUN** — installed and smoke-probed only.
- The harness answers commands from `Update()`; it never drives a frame the game would not have
  rendered, so a browser test cannot outrun the runner's own clock (that is why `advanceTicks` steps
  the session, not `Time`).
- Risk: the harness shares `SceneBoot`'s boot path, so a compile-gate mistake would be invisible until
  the Release browser gate. `HarnessBridgeTests` pins the `#if` and the opcodes in EditMode.

## Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Sim -Seeds 2 -MinBossWinRate 0.95 -MaxBossWinRate 0.99   # failure path, exit 1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Sim
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Build,Web -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Build,Web -Configuration Development
unity test UnityProject --mode PlayMode --filter <each *PlayModeTests class>
unity command run_tests --mode editor --filter HarnessBridgeTests
```

## Outcome

**CODE DONE, gate 14 green on Chrome + Release.** The PlayMode heap failure predates this stage and
is recorded as a FAIL to be fixed by the stage that owns the scene lifetimes; the WebGL/Playwright
gate itself returned 0.
