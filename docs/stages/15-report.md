# Stage 15 — Web release candidate

## Summary

The release candidate is one command (`tools/build-web.ps1 -ReleaseCandidate`), it is measured on
the artifact that ships, and the numbers are inside the GDD's budget with room to spare: initial
payload **15.52 MiB gzip / 11.29 MiB Brotli** (gate < 30, target < 15), battle floor 1 frame time
**p95 7.10 ms** (budget ≤ 20 ms), Wasm heap **79.81 MiB** (budget ~350 MB). The stress case is
measured where the project can measure it — 300 ghosts + ~482 projectiles in the headless simulation,
**0 bytes/tick, tick p95 0.154 ms** — and a *browser* stress number is **NOT RUN** rather than
estimated, because a Release player ships no way to inject that load.
Compression and caching moved to the host where they belong (`tools/compress-web.mjs`,
`serve-web.mjs`, `deploy/_headers`), `Assets/link.xml` preserves the reflection-dependent assemblies,
the non-OFL orphan font is gone, and the Release browser suite is **21 passed / 1 skipped** (the skip
is the dev-menu case a shipped player must not have). On the final clean rebuild the suite came back
**20 passed / 1 failed / 1 skipped**, the failure being the camp spec's long-recorded post-resize
pointer flake; that spec re-ran **5/5** immediately afterwards (`artifacts/perf/playwright-camp-rerun.log`).

## Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | A release build opens on the declared browser | **PASS (Chrome) / NOT RUN (Firefox)** | Release player boots, passes its seven probes and hands over to the title in Playwright: `artifacts/perf/playwright-release.json` (run 1: 21 passed / 1 skipped; run 2 on the clean rebuild: 20 passed / 1 failed / 1 skipped, the failure a known pointer flake whose spec re-ran 5/5). Gecko full suite NOT RUN (see §"Not tested") |
| 2 | The required tests pass on it | **PASS for what was run, one gap** | Release Playwright **21/21 + 1 skip** on the measured artifact (run 2: 20 + the flake + 1 skip; `camp.spec.ts` re-run **5/5**); hosting **13/13** (`artifacts/perf/hosting.json`); EditMode **207/207** (`artifacts/stage15/editmode-batch.xml`); PlayMode `BattlePlayModeTests` **6/6** and `SceneFlowPlayModeTests` **6/6** in batch (`artifacts/stage15/playmode-*.xml`); xUnit **604** and the sim/data/wiki gates (`artifacts/stage15/fast-suite.log`). The remaining PlayMode fixtures are **NOT RUN** on this artifact |
| 3 | A performance report with the machine spec and build hash, against the GDD budget | **PASS** | [`docs/build/performance-report.md`](../build/performance-report.md) — machine, Chrome **155.0.8059.85**, viewport/refresh, git `89b7c50` + this commit, config hash `10CF3726…`, per-scenario p50/p95/p99/max, memory, load, payload in three bases |
| 4 | No number that was not measured | **PASS** | Every number in the report traces to `artifacts/perf/*.json|md|log`; the three gaps (browser stress, Firefox, GPU memory) are labelled NOT RUN in the report and in [`docs/release/known-issues.md`](../release/known-issues.md) |

### The five work items

| Item | Verdict | Where |
| --- | --- | --- |
| 1 Profile load/frame/memory/allocation/entity count, normal vs stress | **PASS (normal) / NOT RUN (browser stress)** | `tests/web/scripts/measure-performance.mjs`; normal: `artifacts/perf/performance.json`; stress: `artifacts/perf/benchmark-stress.log` (headless, 300/500) |
| 2 Fix the evidenced bottleneck before adding a library; Addressables only if over budget | **PASS** | Removed the unused, non-OFL `THSarabunPSK` font (`ATTRIBUTION.md`; `.data` −171,415 B). No normal-path bottleneck exists to fix (p95 7.1 ms vs 20 ms) and both size budgets pass, so **no** Addressables were added ([ADR 0035](../adr/0035-the-release-candidate-configuration-and-its-numbers.md) §2) |
| 3 Stripping and AOT in the release build; link.xml for Protobuf, MessagePack, VContainer, R3, Luban | **PASS** | `UnityProject/Assets/link.xml` (six assemblies, a reason each); `ReleaseLinkXmlTests` (2 cases, EditMode 207/207); the RC is stripping **High** / **OptimizeSize** and its boot probes plus a real save/reload ran in Release Chrome |
| 4 Compression and hosting headers matched to the output; HTTP not file://; blur/focus, resize, stale cache | **PASS** | `tools/compress-web.mjs`, `tools/serve-web.mjs`, `deploy/_headers`, [`docs/build/web-hosting.md`](../build/web-hosting.md); `verify-hosting.mjs` **13/13** (br/gzip/identity, `Vary`, `no-cache`+ETag+304, no-store, budget, redeploy under a live browser); `release-candidate.spec.ts` **2/2** (tab switch, resize at 1280×720 and 1920×1080) |
| 5 Clean build from source and locked toolchain; release checklist; known issues; cheats and harness off | **PASS with a note** | `docs/release/checklist.md`, `docs/release/known-issues.md`; `release.spec.ts` proves no harness, no dev menu, `development: false`. The shipped output directory was deleted and rebuilt from the committed source with the locked toolchain and the `-ReleaseCandidate` switch, then re-compressed, re-verified and re-tested. *Note:* Unity's IL2CPP translation cache (`Library/Bee`) was warm, so `buildSeconds 17.4` is a cached link rather than a cold compile; the cold translation was this stage's first RC build (same source plus the orphan font). The rebuild's `Web.wasm` is **byte-identical** to the measured one (`01af230a…`); `Web.data` has the same size and a different hash (asset packing order is not reproducible), which is why the browser suite and the performance run were repeated on the rebuild |

### The ADR 0011 re-measurement (added by the stage prompt)

| Basis | Stage 03b `optimize-size` | Stage 15 RC | Delta |
| --- | ---: | ---: | ---: |
| uncompressed | 33.75 MiB | **44.65 MiB** | +10.90 MiB |
| gzip -9 | 13.13 MiB | **15.52 MiB** | +2.39 MiB |
| Brotli q11 | 9.70 MiB | **11.29 MiB** | +1.59 MiB |

Measured with `tools/measure-web-size.mjs` on the RC, whose `build-report.json` was read **before**
concluding anything: `sizeProfile: optimize-size`, stripping `High`, codegen `OptimizeSize`,
compression `Disabled` — no residue from another profile ([gotcha](../../llmwiki/gotchas/webgl-size-profile-settings-residue.md)).
Full table and attribution: [`docs/build/size-baseline.md`](../build/size-baseline.md) §8.

## Not tested, and the risk it carries

- **Browser stress (300 enemies + 500 projectiles): NOT RUN.** The artifact ships no injector, and a
  Development player's numbers are not quotable. The headless benchmark covers the simulation; the
  renderer/WebGL layer at 25× the normal entity count is unmeasured. This is the largest gap.
- **Firefox: NOT RUN** beyond Stage 14's boot probe. The GDD names Gecko as a target browser.
- **PlayMode: only `BattlePlayModeTests` and `SceneFlowPlayModeTests`** were run on this artifact (both
  green). The other eight fixtures — drag, chooser, camp world, title, result, shell — were not
  re-run; the change set touches one report field, `link.xml` and build tooling.
- **`tools/test.ps1 -Suite Full -Release` as one command: NOT RUN.** Its web step rebuilds the player
  and, before this stage, would have rebuilt a *plain* Release player (Low stripping) rather than the
  RC. Stage 15 changes that step to call `-ReleaseCandidate`, but the gate was not re-run end to end:
  the same checks were run individually against the RC artifact instead (Playwright, hosting,
  EditMode, two PlayMode fixtures, Fast). This is a process gap, not a green claim.
- **GPU memory: NOT RUN.** A page cannot see it; no external GPU profiler run.
- **One browser flake is still in the suite.** `camp.spec.ts`'s resize case lost its `closeBag`
  press once in the run on the clean rebuild (after five retries) and passed 5/5 immediately after; it
  is the post-resize pointer flake the spec's own comment has recorded since Stage 09l, not a
  behaviour this stage changed. A release gate that treats one run as the verdict would have failed on
  it.
- **Network conditions: local server only.** Load time is a floor, not a field number.
- **The Stage 13 human playtest** is still the one acceptance nothing automated replaces.

## Commands actually run

```powershell
# release candidate (twice: before and after removing the orphan font)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -ReleaseCandidate -Force

# size, three bases (ADR 0011)
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label release-candidate

# compression + hosting contract over HTTP
node tools/compress-web.mjs --dir UnityProject/Builds/Web --label release-candidate
node tests/web/scripts/verify-hosting.mjs --dir UnityProject/Builds/Web --port 8091

# release browser suite (Chrome, Release)
node tools/serve-web.mjs --root UnityProject/Builds/Web --port 8080
#   in tests/web: PROJECTSURVIVE_BUILD=release npx playwright test <ten specs> --reporter list,json

# performance baseline (real browser, Release artifact)
node tests/web/scripts/measure-performance.mjs --url http://127.0.0.1:8080

# stress case, headless
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -c Release -- --mode benchmark

# Unity suites, batch (Editor closed for the web gate)
Unity.exe -batchmode -projectPath UnityProject -runTests -testPlatform EditMode -testResults artifacts/stage15/editmode-batch.xml
Unity.exe -batchmode -projectPath UnityProject -runTests -testPlatform PlayMode -testFilter ProjectSurvive.Game.Tests.PlayMode.BattlePlayModeTests  -testResults artifacts/stage15/playmode-battle.xml
Unity.exe -batchmode -projectPath UnityProject -runTests -testPlatform PlayMode -testFilter ProjectSurvive.Game.Tests.PlayMode.SceneFlowPlayModeTests -testResults artifacts/stage15/playmode-sceneflow.xml

# fast suite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
```

## Outcome

**DONE, with two named gaps.** The release candidate opens in Chrome, passes the browser gate
(21/21 + 1 skip), the hosting contract (13/13), EditMode (207/207) and the two PlayMode fixtures that
cover the changed code, and every budget in the performance report is met on measured bytes and
measured frames. What is **not** measured is the browser stress case, Firefox beyond a boot probe, and
GPU memory — each is recorded as an open item with the reason and the fix, not as a pass.
