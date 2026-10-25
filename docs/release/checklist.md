# Release checklist — Web release candidate (Stage 15)

One page, in order, with the command that proves each line. A line is only
`PASS` when the command that proves it was run on the artifact being shipped;
`docs/stages/15-report.md` records what was actually run and what was not.

Machine, browser and engine: [ADR 0003](../adr/0003-performance-baseline-machine-and-browser.md)
and [`tools/versions.lock.json`](../../tools/versions.lock.json). Numbers and their
bases: [ADR 0011](../adr/0011-webgl-payload-budget-and-size-measurement.md) and
[ADR 0035](../adr/0035-the-release-candidate-configuration-and-its-numbers.md).

## 0. Before building

- [ ] `tools/doctor.ps1` exits 0 — the locked Unity (`6000.3.9f1`), WebGL module,
      .NET SDK, Node and Playwright are present.
- [ ] No Unity Editor has `UnityProject` open (`UnityProject/Temp/UnityLockfile`
      absent). Batch mode cannot share a project.
- [ ] `git status` is clean apart from intended changes; the build records
      `git rev-parse HEAD` in the performance report.
- [ ] `ProjectSettings.asset` carries the **authored** configuration (stripping Low,
      codegen OptimizeSpeed). A profile's residue here is what
      [the gotcha](../../llmwiki/gotchas/webgl-size-profile-settings-residue.md) warns
      about; `build-web.ps1` now fails a build whose report disagrees with the
      requested profile.

## 1. Clean build from source and the locked toolchain

```powershell
pwsh -NoProfile -File tools/doctor.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1
pwsh -NoProfile -File tools/verify-generated.ps1        # regenerating is byte-identical
Remove-Item -Recurse -Force UnityProject/Builds/Web     # a fresh output directory
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -ReleaseCandidate -Force
```

- [ ] The build exits **0** and prints `build settings match profile` PASS with
      `optimize-size: stripping High, codegen OptimizeSize`.
- [ ] `player settings restored` PASS — the tree is left with the authored settings.
- [ ] `UnityProject/Builds/Web/build-report.json` records `"sizeProfile": "optimize-size"`.
- [ ] `git diff --stat UnityProject/ProjectSettings` is empty.

## 2. Content and assets

- [ ] No non-OFL asset ships: `ATTRIBUTION.md` §"Fonts" has no row marked *must be
      removed*, and `UnityProject/Assets/Resources/Fonts/` is gone.
- [ ] **The Mor's skeleton has a licence.** `ATTRIBUTION.md` marks `body_1110901` as a
      blocker (unknown provenance); a release candidate may not ship it until that row
      turns into a recorded licence or the art is replaced. It ships from `Resources/`, so
      check it is in neither the build nor the size measurement until then.
- [ ] `data/` regenerated and validated (`tools/validate-data.ps1`,
      `tools/check-fixtures.ps1`), `llmwiki/content/` regenerated and committed.
- [ ] The localization table has both languages for every key the runtime asks for
      (`Every_localization_key_the_runtime_asks_for_exists`).

## 3. Size (three bases, ADR 0011)

```powershell
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label release-candidate
node tools/measure-il2cpp-size.mjs --artifacts UnityProject/Library/Bee/artifacts/WebGL --label release-candidate
```

- [ ] Initial payload (five files) **gzip -9 < 30 MiB** (gate).
- [ ] Initial payload **Brotli q11 < 15 MiB** (production target).
- [ ] Both numbers are in `docs/build/performance-report.md` with the build hash.
- [ ] Addressables are **not** added: the size is inside both budgets. (They are the
      response to an oversized initial download, not a default.)

## 4. Stripping and AOT

- [ ] `Assets/link.xml` exists and names `Luban.Runtime`, `MessagePack`,
      `MessagePack.Annotations`, `Google.Protobuf`, `VContainer`, `R3`
      (`ReleaseLinkXmlTests`).
- [ ] The **Release** browser suite passes boot's seven probes (DI, R3, UniTask,
      MessagePack round trip, Protobuf round trip, config hash, Thai glyphs).
- [ ] The persistence spec saves and reloads a real run through IndexedDB — the path
      that deserialises a MessagePack checkpoint in the player.
- [ ] No `MissingMethodException` / `ExecutionEngineException` in the browser console
      (every spec fails on `console.error` and `pageerror`).

## 5. Compression and hosting, over HTTP

```powershell
node tools/compress-web.mjs --dir UnityProject/Builds/Web --label release-candidate
node tests/web/scripts/verify-hosting.mjs --dir UnityProject/Builds/Web --port 8091
```

- [ ] `.br`/`.gz` siblings written; `compression-report.json` totals match §3.
- [ ] `Content-Encoding` and `Vary: Accept-Encoding` correct for `br`, `gzip` and
      `identity`; `.wasm` is `application/wasm`.
- [ ] `index.html` and `/Build/*` are `no-cache` + `ETag`; `304` on revalidation;
      build diagnostics are `no-store`.
- [ ] A redeployed file is visible after a reload (stale-cache case).
- [ ] `deploy/_headers` matches the policy the verification just proved.
- [ ] The suite ran against `http://127.0.0.1:<port>/` — never `file://` (a player
      from `file://` cannot fetch its own `.wasm` and is not a supported deployment).

## 6. Browser performance (Release artifact, real browser)

```powershell
node tests/web/scripts/measure-performance.mjs --url http://127.0.0.1:8092
```

- [ ] Battle floor 1 frame time **p95 ≤ 20 ms**.
- [ ] Wasm heap within the GDD's ~350 MB.
- [ ] Load-to-title time recorded (cold and warm), not asserted against a target the
      GDD has not set for the prototype.
- [ ] The report names machine, browser version, build hash, configuration,
      scenario and entity count.
- [ ] Stress (~300 + 500) is reported with its real basis: the headless sim
      benchmark. A browser stress number is **NOT RUN** unless one was actually
      measured (see [known issues](known-issues.md)).

## 7. Desktop browser behaviours

- [ ] `release-candidate.spec.ts`: background tab dropped, not replayed; floor still
      ticks afterwards.
- [ ] `release-candidate.spec.ts`: resize at 1280×720 and 1920×1080 keeps the player
      running, labels fit, no missing glyphs.
- [ ] Reload/resume and settings persistence (`persistence.spec.ts`) — unchanged from
      Stage 12 but re-run on this artifact.

## 8. Cheats and harness are closed

- [ ] `release.spec.ts` on the shipped artifact: `__projectSurviveHarness`,
      `__projectSurviveHarnessSend`, `__projectSurviveHarnessSnapshot` and
      `__projectSurviveDevMenu` are all `undefined`.
- [ ] `build-info.json` says `"development": false`.
- [ ] No test-only scene is in the build list (`BuildSettings` scenes are the six
      shipped screens).

## 9. Record

- [ ] `docs/stages/15-report.md` — acceptance one line each (PASS / FAIL / NOT RUN),
      evidence paths, what was not tested and its risk, the commands actually run.
- [ ] `docs/build/performance-report.md` — the measured numbers beside the budget.
- [ ] `llmwiki/stage-log.md` — three lines.
- [ ] `knowledge.md` status table — Stage 15 row.
- [ ] `git log -1` is `stage-15: <summary>`; `git status` clean.
