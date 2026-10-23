# Performance report — Web release candidate (Stage 15)

Every number here was measured on the artifact named below, on the reference
machine, in a real browser. The raw artifacts are
[`artifacts/perf/performance.json`](../../artifacts/perf/performance.json),
[`performance.md`](../../artifacts/perf/performance.md),
[`size-release-candidate.md`](../../artifacts/perf/size-release-candidate.md),
[`benchmark-stress.log`](../../artifacts/perf/benchmark-stress.log) and
[`playwright-release.json`](../../artifacts/perf/playwright-release.json).

## 1. What was measured

| | |
| --- | --- |
| Artifact | `UnityProject/Builds/Web`, built by `tools/build-web.ps1 -ReleaseCandidate -Force` |
| Configuration | **Release**, `sizeProfile: optimize-size` — managed stripping **High**, IL2CPP code generation **OptimizeSize**, exceptions `ExplicitlyThrownExceptionsOnly` (read back from `build-report.json`) |
| Build identity | git `89b7c50f322931f62289fd14c9972ec0d2dee738` + the Stage 15 working tree (this commit); config hash `10CF3726…6FE6` (`build-info.json`); artifact hashes per file in `compression-report.json` |
| Engine | Unity `6000.3.9f1` (`tools/versions.lock.json`), IL2CPP → WebGL2, single-threaded |
| Machine | Intel Core i5-14400F (10 cores / 16 threads), 31.8 GiB RAM, NVIDIA RTX 5060 Ti (driver 32.0.16.1692), Windows 11 `10.0.26300` x64 — [ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md) |
| Browser | Google Chrome **155.0.8059.85** (Playwright `channel: "chrome"`), headful, viewport 1920×1080, canvas resized to 1920×1080 CSS px, devicePixelRatio 1 |
| Display cadence | **144 Hz**, as measured (1,440 `requestAnimationFrame` callbacks per 10 s at the title and in the camp). A 7 ms p95 is therefore "the player keeps up with the display", not a 60 Hz floor. |
| Server | `tools/serve-web.mjs` over HTTP on `127.0.0.1` — never `file://`; Brotli siblings negotiated (`Content-Encoding: br`) |

The tool is `tests/web/scripts/measure-performance.mjs`. It records the report
itself; nothing here was typed from a screenshot.

## 2. Budget, measured

| GDD item | Target | Measured | Verdict |
| --- | --- | --- | --- |
| Frame time, p95, normal floor | ≤ 20 ms | **7.10 ms** (battle floor 1, 6,479 frames, 18 entities peak) | **PASS** |
| Frame time, p95, title / camp | ≤ 20 ms | 7.00 ms / 7.10 ms | **PASS** |
| Memory (Unity/Wasm heap) | ~350 MB | **79.81 MiB** Wasm heap, constant through the floor; JS heap ~21 MiB | **PASS** |
| Initial download, compressed | ≤ 30 MB (gate) | **15.52 MiB gzip**, **11.29 MiB Brotli** | **PASS** |
| Allocation in the hot loop | near zero after warm-up | simulation: **0 bytes/tick** (300 + 482 entities); browser: JS heap +0.74 MiB over 45 s, Wasm heap unchanged | **PASS** (see §5 for what the browser half can see) |
| Scene loop (camp ↔ battle, 20 visits) | no unbounded growth | PlayMode case from Stage 14 (0.70 MiB over 20 visits) — **not re-run** on this artifact | **NOT RUN** |
| Stress case ~300 enemies + 500 projectiles | profile separately | headless: tick p95 **0.154 ms**, 0 B/tick; **browser: NOT RUN** (see §6) | **NOT RUN** (browser) |
| Browsers | Chromium + Firefox, versions recorded | Chrome 155 measured; Firefox boot-probed at Stage 14, full suite **NOT RUN** | **PARTIAL** |

## 3. Frame time distributions

| Scenario | Frames | p50 | p95 | p99 | max | frames > 20 ms | entities (peak) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Title (idle) | 1,434 | 6.90 ms | 7.10 ms | 7.10 ms | 20.80 ms | 1 | — |
| Camp, ศาลาพัก (idle) | 1,440 | 6.90 ms | 7.00 ms | 7.10 ms | 8.00 ms | 0 | — |
| Battle, floor 1 | 6,456 | 6.90 ms | 7.10 ms | 7.60 ms | 21.60 ms | 4 | 18 |

The run before the final clean rebuild measured battle p95 **7.10 ms**, p99 7.20 ms, max 13.90 ms,
0 frames over 20 ms; the spread between the two runs is the honest run-to-run variance of a desktop
browser, and p95 is identical in both.

Sampled with `requestAnimationFrame` deltas in the page — the same callback the
Unity WebGL player is driven by. Frame 1 of each scenario is excluded by
construction (the first delta has no predecessor).

The battle's 18 entities at peak is the normal load the boss-less floors produce
(waves of 20–26 spawned over 60 s with kills in between). Nothing on the normal path
was slow enough to show up: there is **no bottleneck to fix on this load**, so no
optimisation was invented for one (AGENTS rule 12), and Addressables were not added
because the initial payload is inside both budgets.

## 4. Load

| Load | Wall clock | Boot self-check ready | Title screen ready |
| --- | ---: | ---: | ---: |
| Cold (fresh browser context, empty HTTP cache) | 3,118 ms | 2,847 ms | 3,104 ms |
| Warm (reload, cache live, Brotli revalidated) | 2,761 ms | 2,496 ms | 2,754 ms |

The previous run measured 2,976 ms cold / 2,565 ms warm on the same configuration.

Boot dominates: the seven self-check probes run before the title scene is loaded,
and the 2.8 s includes fetching and instantiating a 33.8 MB `.wasm` (7.2 MB on the
wire) from a local server. This is a **local floor**, not a field number: no network
throttling was applied, and the GDD has no load-time budget for the prototype (the
≤ 10 s figure belongs to the next milestone).

## 5. Memory and allocation

| Scenario | JS heap before | JS heap after | growth | Wasm heap before | Wasm heap after |
| --- | ---: | ---: | ---: | ---: | ---: |
| Title | 21.42 MiB | 20.53 MiB | −0.90 MiB | 79.81 MiB | 79.81 MiB |
| Camp | 21.68 MiB | 21.10 MiB | −0.58 MiB | 79.81 MiB | 79.81 MiB |
| Battle floor 1 | 20.95 MiB | 21.69 MiB | +0.74 MiB | 79.81 MiB | 79.81 MiB |

- The **Wasm heap** is read from the `memory` export of the instantiated module
  (captured by an init script; a Release player publishes no harness). It is
  79.81 MiB and does not grow during a floor — the pools were warmed at floor start.
- The **JS heap** is `performance.memory.usedJSHeapSize`. It falls at the title and
  camp (GC) and grows by 0.74 MiB over a 45 s floor (0.09 MiB in the earlier run). This is a window measurement,
  not a per-frame allocation count: a browser page cannot see per-frame allocations
  without instrumenting the player, and the honest per-tick number comes from the
  simulation benchmark below.
- **GPU/driver memory is not measured.** A page cannot see it, and no external GPU
  profiler run was made. The GDD asks for "browser and GPU measured separately"; the
  browser half is here, the GPU half is **NOT RUN**.

## 6. Stress case (~300 ghosts + ~500 projectiles)

Measured with the project's own stress instrument, the headless simulation
benchmark (`dotnet run --project lib/Tests~/Sim -c Release -- --mode benchmark`,
.NET 9.0.13 on the same machine):

| Metric | Value |
| --- | ---: |
| Enemies alive (avg / max) | 299.1 / 301 |
| Projectiles alive (avg / max) | 482.2 / 492 |
| Warm-up / measured ticks | 900 / 600 |
| Tick time avg / p50 / p95 / p99 / max | 0.134 / 0.131 / **0.154** / 0.199 / 0.272 ms |
| Allocated bytes per tick | **0** (0 total over 600 ticks) |

This is a property of the simulation on .NET, **not** a browser frame time. A browser
stress number is **NOT RUN**: the release candidate ships no way to inject 300
entities (the harness and dev menu are compiled out — Stage 14 item 7), and a
Development-build measurement would not be a number about the artifact that ships.
See [ADR 0035](../adr/0035-the-release-candidate-configuration-and-its-numbers.md) §5
and [known issues](../release/known-issues.md) #1.

## 7. Initial payload — three bases (ADR 0011)

| Basis | Size | Gate / target | Verdict |
| --- | ---: | --- | --- |
| uncompressed | 46,818,490 B (**44.65 MiB**) | reported, not a verdict | — |
| gzip -9 | **15.52 MiB** | gate < 30 MiB | **PASS** (5.30 MiB unspent, 51 % of the gate) |
| Brotli q11 | **11.29 MiB** | target < 15 MiB | **PASS** (3.71 MiB unspent, 25 % of the target) |

Per file: `Web.wasm` 32.25 MiB raw / 10.10 MiB gzip / 7.07 MiB Brotli; `Web.data`
11.94 MiB raw / 5.32 MiB gzip / 4.14 MiB Brotli; `Web.framework.js` 0.43 MiB;
`index.html` + `Web.loader.js` ~32 KB. Details: `docs/build/size-baseline.md` §8 and
`artifacts/perf/size-release-candidate.md`.

## 8. What this report does not cover

1. **Browser stress** (§6) — the largest gap; recorded as a known issue, not
   estimated.
2. **Firefox** — the full suite and any Gecko number are NOT RUN.
3. **GPU memory** (§5).
4. **Network conditions** — a local static server only; no throttled profile.
5. **PlayMode scene-loop numbers** — Stage 14's 0.70 MiB / 20 visits was not re-run
   on this artifact (the editor-side suite was not part of the release-candidate run).
6. **Other machines.** These are numbers for the reference machine. The GDD and
   [ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md) scope
   performance claims to it.
