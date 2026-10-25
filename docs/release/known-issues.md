# Known issues — Web release candidate

What is wrong, what it costs, and what would fix it. Anything a reviewer could
discover in ten minutes of play or in reading the reports is here, so that "not
measured" and "not working" are not confused with "fine".

## Open

### 1. The stress case (~300 enemies + 500 projectiles) is not measured in a browser

- **Basis:** the GDD asks for the stress case to be profiled separately from the
  normal floor. The release candidate has no way to inject that load: the harness
  and the dev menu are compiled out of a Release player (Stage 14 item 7), and the
  game's own floors peak at ~27 entities.
- **What is measured instead:** the headless simulation benchmark at exactly that
  load — 300 ghosts + ~482 projectiles, 0 bytes allocated per tick, tick p95
  0.154 ms on the reference machine (`artifacts/perf/benchmark-stress.log`). That is
  a property of the simulation on .NET, **not** a browser frame time.
- **Risk:** the renderer and the WebGL layer under a 25× entity load are unmeasured.
  The event/entity pipelines are pooled and the render pool warms to 1,793 views, so
  there is no known cliff — but "no known cliff" is not a measurement.
- **Fix:** a Development-only stress scene (labelled not-quotable) or a dedicated
  test artifact that carries a stress injector, plus the same frame-time tool run
  against it. See [ADR 0035](../adr/0035-the-release-candidate-configuration-and-its-numbers.md) §5.

### 1b. The Mor's skeleton has no established licence

- **Basis:** `UnityProject/Assets/Resources/body_1110901/` is a Spine export that arrived from outside
  this project; the skeleton's own data still names its authoring path and Chinese part names, and no
  licence, author or purchase record came with it (`ATTRIBUTION.md`).
- **Consequence:** `Resources/` ships everything in it, so a release build would carry this art
  today. Until the provenance is settled (a written licence, or a project-owned replacement), **this
  asset must not go into a release build**; it is fine in a development build.
- **Fix:** establish the licence, or replace the figure. Either way record the outcome in
  `ATTRIBUTION.md` before a release candidate is cut.

### 2. Firefox: installed for Playwright, full suite not run

- Stage 14 booted the Release player once in Playwright's bundled Firefox
  (`firefox-1555`) and it passed the smoke probes. The full browser suite, and any
  Firefox performance number, are **NOT RUN**.
- **Risk:** the GDD names Chromium *and* Firefox as target browsers. Gecko-specific
  audio (Web Audio autoplay), WebGL2 context loss and `requestAnimationFrame`
  behaviours are unverified.
- **Fix:** `npx playwright test` with a Firefox project against the same artifact.

### 3. PlayMode suite is not part of the release-candidate evidence

- The gate was run with the Editor closed (the only way to build and serve the
  player). EditMode and PlayMode both need an Editor connected to the MCP bridge;
  the release-candidate run records them as NOT RUN with that reason.
- **Risk:** the change set that produced this RC touched Presentation (one added
  report field), the link.xml and build tooling; the loop tests that would catch a
  scene-lifetime regression were not re-run for this artifact. They were green at
  Stage 14 on the same code minus those changes.
- **Fix:** run `tools/test.ps1 -Suite Fast` with the Editor open, and the PlayMode
  fixtures per class (`docs/testing/test-suites.md` §7), against this commit.

### 4. Bulk PlayMode runs still fail on a Unity ambient-log flake

- Running the whole PlayMode assembly at once reports ~22 of 76 cases; the failures
  are `IndexOutOfRangeException` + "Cached unprocessed value" ambient messages, not
  failed assertions. Per-fixture runs are green.
- **Risk:** a developer who runs the assembly instead of the harness gets a red
  result that means nothing. The harness runs one fixture per process, so the gate is
  not affected.
- **Fix:** none found in UTF 1.6.0; the per-fixture workaround is the contract.

### 5. Load to title is measured, not budgeted

- Cold load to the title screen is recorded in
  [`performance.md`](../../artifacts/perf/performance.md) but the GDD's prototype
  budget has no load-time target (the ≤10 s figure belongs to the *next* milestone).
- **Risk:** a slow network is not modelled — the measurement is a local static
  server on the reference machine, so it is a floor, not a field number.
- **Fix:** a throttled-network run (CDP `Network.emulateNetworkConditions`) when a
  load-time budget exists.

### 6. Browser memory is two heaps, not a 350 MB dashboard

- The report gives the JS heap (`performance.memory`) and the WebAssembly heap
  separately. GPU/driver memory is not visible to a page and is not measured.
- **Risk:** the GDD's "~350 MB for the Unity/Wasm heap, browser and GPU measured
  separately" is met for the Wasm heap; the GPU half is unmeasured on this machine.
- **Fix:** GPU memory needs an external profiler (Chrome's GPU process metrics or a
  driver tool), which is outside the browser page and not implemented.

### 7. Allocation in the browser is measured as heap growth, not per frame

- `performance.memory` is sampled at the start and end of a scenario, and the Wasm
  heap grows in steps. The report's allocation number is a growth over a window, not
  a per-frame count. The per-tick zero-allocation claim is the headless benchmark's.
- **Risk:** a small per-frame allocation that stays under a GC threshold would not be
  visible. The simulation's own "0 bytes per tick" test covers the hot loop.
- **Fix:** instrument the player (a build-time diagnostic) or use a sampling profiler
  with allocation stacks.

## Closed by this stage

- **The orphan Thai font shipped.** `THSarabunPSK` (non-OFL, unused, 4.2 MB as
  reported by the build) was removed from `Assets/Resources/Fonts/`; `.data` fell by
  171,415 B raw and the licence risk is gone. `ATTRIBUTION.md` records the removal.
- **A profile build left its settings behind.** `tools/build-web.ps1` now restores
  `ProjectSettings.asset` / `GraphicsSettings.asset` after a profile build
  (`-ReleaseCandidate`) and fails a build whose `build-report.json` disagrees with
  the requested profile.
- **`immutable` would have pinned players to an old build.** Player files are served
  `no-cache` + `ETag`; the redeploy case is a test.
