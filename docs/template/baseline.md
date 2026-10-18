# template-v1 baseline

What this template weighed, how long it took and which tests passed on the day it was
tagged, measured on the reference machine recorded in
[`tools/versions.lock.json`](../../tools/versions.lock.json). Every number here came from
a command in §7, on the commit the `template-v1` tag points at.

Two reasons this file exists. A game that is slower or bigger than this is comparing
against a *baseline*, not against a promise — the template's timings are the reference
machine's (ADR 0003), and its sizes belong to the configuration `build-report.json`
records, which sits next to the player under the git-ignored `UnityProject/Builds/Web/`
(ADR 0011 §4a). And the extraction's own gate ("everything the template ships passes on
the tagged commit") is a claim that has to be readable after the fact, not just observed
once in a terminal.

| | |
| --- | --- |
| Tag | `template-v1` |
| Branch | `template/main` |
| Extracted from | `survive-template-base` = `c58373e0883a1b8ba9cbbf72a1bfca63d8f132f1` |
| Data configuration | config hash `DDFDF33800712F9D4ECC410EBD24FF407492E1BC5D74A72BF70BCA52AA64E70F` |
| Reference machine | Intel Core i5-14400F (10 cores / 16 logical), 31.8 GiB RAM, NVIDIA RTX 5060 Ti, Windows 11 Home `10.0.26300` |
| Measured | 2026-10-18 |

## 1. Versions this baseline was taken with

From [`tools/versions.lock.json`](../../tools/versions.lock.json); the value in the
`measured` column is what the machine actually answered while T5 ran.

| Tool | Locked | Measured |
| --- | --- | --- |
| Unity Editor | `6000.3.9f1` (revision `7a9955a4f2fa`), Unity Personal, WebGL module | `6000.3.9f1` (doctor, batch mode) |
| Luban CLI | `5.1.0` (`df34215d2e22035073d1af83d44e3ac0744dc3b3`) | present under `tools/vendor/luban/5.1.0` |
| protoc | `33.6` | present under `tools/vendor/protoc/33.6` |
| .NET SDK | `6.0.428`, `9.0.311` (tests `net9.0`, libraries `netstandard2.1`, C# 9) | `6.0.428`; `9.0.311` |
| Node.js / npm | `v25.6.1` / `11.9.0` | `v25.6.1` / `11.9.0` |
| Git / Git LFS | `2.55.0.windows.3` / `3.7.1` | same |
| PowerShell | Windows PowerShell `5.1.26100.9444`, `pwsh` 7 | `pwsh` `7.6.0-rc.1` |
| Google Chrome | `154.0.8037.98` (the browser the browser tests drive) | **`155.0.8059.80`** — see §6 |
| Microsoft Edge | `145.0.3800.70` (fallback) | present |
| Mozilla Firefox | `blocked` (not installed) | not installed → cross-browser runs are NOT RUN |

Packages, as resolved by the locked Editor for `UnityProject` and by NuGet/npm for
`lib/Tests~` and `tests/web`: VContainer `1.19.0`, R3 `1.3.1`, UniTask `2.5.11`,
MessagePack `3.1.12` (source generator only), Google.Protobuf `3.33.6`, Unity Input
System `1.20.1`, URP `17.3.0`, uGUI + TextMeshPro `2.0.0`, Unity Test Framework
`1.6.0`, xUnit `2.9.3`, Microsoft.NET.Test.Sdk `17.12.0`,
xunit.runner.visualstudio `2.8.2`, Playwright `1.64.0` (driving installed Chrome).

## 2. The gates, in the order T5 ran them

| # | Command | Result |
| --- | --- | --- |
| 1 | `tools/doctor.ps1` | **PASS 14 \| FAIL 0 \| WARN 1 \| SKIP 1** — the WARN is the missing Firefox, the SKIP is the opt-in Unity batch-mode license probe (`-Unity`) |
| 2 | `tools/gen.ps1` | **OK** — 8 `.cs`, 3 `.bytes`, hash `DDFDF338…`, 1.1 s (nothing to regenerate) |
| 3 | `tools/verify-generated.ps1` | **PASS 9 \| FAIL 0 \| WARN 0** — generated C#, both `.bytes` sets and `App.cs` reproduce byte for byte, 3.8 s |
| 4 | `tools/validate-data.ps1 -DataDir "lib/Tests~/data"` | **PASS 5 \| FAIL 0** — no findings, 66 s (the validator project builds first) |
| 5 | `tools/check-fixtures.ps1` | **PASS 10 \| FAIL 0** — six fixtures, each rejected by the stage its `fixture.json` names, 19 s |
| 6 | `node tools/check-links.mjs` | **PASS** — 233 links in 57 files resolve **and** every target is one a clone would have, exit 0 |
| 7 | `tools/test.ps1 -Suite Full` (Editor **closed**) | **PASS 10 \| FAIL 0 \| NOT RUN 2 \| WARN 0** — 137.4 s of steps; the two NOT RUNs are the Unity suites, which need the Editor |
| 8 | `tools/build-web.ps1 -Configuration Release` | **OK** — `index.html` + one `.wasm`, `0` `error CS`, config hash `DDFDF338…`, **489.8 s** (full IL2CPP compile) |
| 9 | `node tools/measure-web-size.mjs --dir UnityProject/Builds/Web` | **exit 0** — §3 |
| 10 | `tests/web/scripts/measure-load-to-title.mjs` (Release player) | **exit 0** — §4 |
| 11 | `tools/build-web.ps1 -Configuration Development` | **OK** — same checks, **194.5 s** (incremental) |
| 12 | `tools/measure-web-size.mjs` (Development player) | **exit 0** — §3 |
| 13 | `npx playwright test` (all specs, against the Development player) | **4 passed** in Chrome, 22.6 s |
| 14 | `tools/test.ps1 -Suite Full` (Editor **open**) | **PASS 10 \| FAIL 0 \| NOT RUN 2 \| WARN 0** — 175.1 s; EditMode **30/30**, PlayMode **6/6**, the web pair NOT RUN |
| 15 | `git grep -i projectsurvive` in a renamed throwaway copy | **10 files** — `docs/template/ORIGIN.md` and nine under `docs/template/examples/`, nothing else (§5) |
| 16 | `UnityProject/ProjectSettings/ProjectSettings.asset` after all builds | **clean** — `git status` reports no change and the player settings are the authored ones (§5) |

Steps 7 and 14 are the two halves of one verdict: `test.ps1` cannot report all of
EditMode, PlayMode, the WebGL build and the browser in one run, because the build needs
the project lock and the Unity suites need the Editor that owns it
([`../testing/test-suites.md`](../testing/test-suites.md) §4). Across both runs every
step passed; nothing was skipped that could have run.

## 3. Web payload

The unit is ADR 0011 §1: the five files the loader fetches before the game runs
(`index.html` + `Build/Web.{loader.js,framework.js,wasm,data}`), measured from the bytes
on disk with gzip level 9 and Brotli quality 11 / lgwin 22.

**Release — the configuration size and timing claims may be made about (ADR 0003).**

| File | Uncompressed | gzip -9 | Brotli q11 |
| --- | ---: | ---: | ---: |
| `index.html` | 5,586 B | 2,137 B | 1,579 B |
| `Build/Web.loader.js` | 26,982 B | 9,326 B | 8,133 B |
| `Build/Web.framework.js` | 430,574 B | 86,330 B | 72,236 B |
| `Build/Web.wasm` | 50,004,375 B | 13,176,383 B | 8,600,938 B |
| `Build/Web.data` | 12,971,609 B | 5,643,375 B | 4,368,857 B |
| **initial payload** | **63,439,126 B (60.50 MiB)** | **18.04 MiB** | **12.45 MiB** |
| whole build directory | 63,462,420 B (60.52 MiB) | | |

* **Gate — initial payload, gzip -9, `< 30 MiB`:** 18.04 MiB → **PASS**.
* **Target — initial payload, Brotli q11, `< 15 MiB`:** 12.45 MiB → **PASS**.
* Raw uncompressed is reported for comparison only; it is not a passing basis
  (ADR 0011 §3).

**Development — context only, never a quoted number.** Same pipeline, `development: true`:

| | Uncompressed | gzip -9 | Brotli q11 |
| --- | ---: | ---: | ---: |
| initial payload | 117,506,234 B (112.06 MiB) | 27.89 MiB | 18.87 MiB |

The two configurations differ by 53.6 MiB raw / 9.85 MiB gzip / 6.43 MiB Brotli, and
that difference is debug symbols and unoptimised output — the reason ADR 0003 lets only
Release numbers be quoted. Both players were built with the authored player settings
(`managedStrippingLevel: Low`, `il2cppCodeGeneration: OptimizeSpeed`,
`exceptionSupport: ExplicitlyThrownExceptionsOnly`, `wasmArithmeticExceptions: Throw`,
`sizeProfile: "none"`), which is what §5 checks for.

## 4. Time to Title

The template's "load time" is a property of the player, not of the page: the number
quoted is from navigating to the moment the **Title screen** has published its report
(`__projectSurviveTitle`), the same handover `title.spec.ts` asserts on. Measured on the
**Release** player, served locally by `tools/serve-web.mjs`, in the installed Chrome
through [`tests/web/scripts/measure-load-to-title.mjs`](../../tests/web/scripts/measure-load-to-title.mjs).

| Measurement | Value |
| --- | ---: |
| First load in a fresh browser profile (3 separate launches) | 2,879 / 2,868 / 2,877 ms → **median 2,877 ms** |
| Repeat load in the same profile (3 pages in one launch) | 2,884 / 2,719 / 2,728 ms → **median 2,728 ms** |
| Time to `DOMContentLoaded` (network + parse half) | ~20 ms |
| Page errors during those runs | 0 |

So a visitor pays about **2.9 s** from navigation to a usable Title screen, of which the
page itself is ~20 ms: essentially all of it is downloading the 63 MB payload over
loopback and compiling the WebAssembly. The 149 ms between a cold and a warm profile is
the compiled-module cache, not the first-visit path.

## 5. What the repository was checked for

**Test counts.** 124 xUnit facts, 30 EditMode tests, 6 PlayMode tests, 4 Playwright
tests in three specs (2 `smoke`, 1 `title`, 1 `loop`), 6 broken-data fixtures, and the
two process-level Sim checks — all green in the two Full runs of §2.

**No size-profile residue.** `tools/build-web.ps1 -SizeProfile` persists player settings
(`ProjectSettings.asset`) and a later profile-less build inherits them
([`../../llmwiki/gotchas/webgl-size-profile-settings-residue.md`](../../llmwiki/gotchas/webgl-size-profile-settings-residue.md)).
After the Release and Development builds of §2, `git status` reports
`ProjectSettings/` **clean**, and the authored values are in place:
`managedStrippingLevel: WebGL: 1` (Low), `il2cppCodeGeneration: WebGL: 0`
(OptimizeSpeed), `webGLExceptionSupport: 1` (ExplicitlyThrownExceptionsOnly),
`webGLWasmArithmeticExceptions: 0` (Throw). Both `build-report.json` files agree
(`"sizeProfile": "none"`), so the players measured in §3 and §4 are the project's own
configuration, not a leftover profile.

**The identity is renameable.** A throwaway copy of the tree was renamed with
`tools/rename.ps1 -From ProjectSurvive -To ProjectSweepProbe`, regenerated, and swept
with `git grep -i projectsurvive` (case-insensitive, which covers all six spellings the
tool knows). Result: **10 files** — `docs/template/ORIGIN.md` plus nine under
`docs/template/examples/`, and nothing in `lib/`, `data/`, `UnityProject/`, `tools/`,
`tests/`, `knowledge.md`, `AGENTS.md` or `docs/adr/`. `verify-generated` in that copy was
PASS 9 / FAIL 0 and `check-links` exit 0. The copy was deleted; nothing from it is in this
repository. T4's rehearsal covers the same ground with a full suite run
([`rename-rehearsal.md`](rename-rehearsal.md)).

The first attempt at that sweep committed its result onto *this* repository's
`template/main`: the copy had inherited the worktree's `.git` pointer file, so the copy's
git was this repo's git. It was recovered with `git reset --mixed 3a5034a` — the working
tree was never touched, so no work was lost — and §7 deletes the pointer before `git init`
now. The trap is written up in
[`../../llmwiki/gotchas/copied-tree-keeps-git-pointer.md`](../../llmwiki/gotchas/copied-tree-keeps-git-pointer.md).

## 6. What this baseline is not

- **Not a promise for a game.** These are the reference machine's numbers for a
  placeholder loop with three data tables. A game's sizes and timings are its own, and
  they are earned with `tools/test.ps1 -Suite Full` and
  `tools/measure-web-size.mjs`, not inherited from here.
- **The locked Chrome version has drifted.** `versions.lock.json` records
  `154.0.8037.98`; the machine now runs **`155.0.8059.80`**, and every browser number in
  §4 is from that newer build. Chrome updates itself, so the lock is a record of the
  version a number was taken with, not a pin. This baseline's browser rows should be read
  as "installed Chrome 155", and the lock file is left as written (T5 does not upgrade
  dependencies).
- **Release size on this machine only.** ADR 0011 §4: byte counts do not depend on the
  host, but the *compiled* output does depend on the Editor and toolchain versions in §1.
  A different Unity patch produces a different `.wasm`.
- **`§4` was measured with a warm OS file cache.** The player was served from local disk
  moments after it was built, over loopback rather than a network. It is a floor for this
  machine, not a user's first-visit time on a real host — a host that cannot serve Brotli
  or gzip pays more (ADR 0011 §Consequences).
- **Only Chrome was measured.** Firefox is not installed on the reference machine, so
  cross-browser compatibility is **NOT RUN** and the doctor warns about it; Edge is
  installed but no suite ran against it.
- **`artifacts/` is git-ignored.** The raw logs of every measurement above
  (`artifacts/t5/`) live on the machine that produced them. The commands in §7 are what
  reproduces each number.

## 7. Commands that produced this file

```powershell
# 1-6: the cheap gates, in T5's order
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File tools/gen.ps1
pwsh -NoProfile -File tools/verify-generated.ps1
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
pwsh -NoProfile -File tools/check-fixtures.ps1
node tools/check-links.mjs

# 7: the web half of the Full suite (Editor closed)
pwsh -NoProfile -File tools/test.ps1 -Suite Full

# 8-10: the Release player, its size, and its time to Title
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Release -Force
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label release `
     --json artifacts/t5/size-release.json --markdown artifacts/t5/size-release.md
node tools/serve-web.mjs --port 8080
node tests/web/scripts/measure-load-to-title.mjs http://127.0.0.1:8080/ 3 artifacts/t5/load-to-title-release.json

# 11-13: the Development player, its size, and the browser suite against it
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Development -Force
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label development `
     --json artifacts/t5/size-development.json --markdown artifacts/t5/size-development.md
npx --prefix tests/web playwright test

# 14: the Unity half of the Full suite (Editor open)
pwsh -NoProfile -File tools/test.ps1 -Suite Full

# 15: the rename sweep (throwaway copy, deleted afterwards)
robocopy C:\Workspace\ProjectSurvive-template C:\Workspace\_t5-sweep /E /XD .git Library Temp Logs obj bin UserSettings node_modules artifacts Builds
#    robocopy excludes a .git *directory*, so a worktree's .git *pointer file* is copied:
#    delete it before any git command in the copy, or the copy's git writes to the
#    original repository. See llmwiki/gotchas/copied-tree-keeps-git-pointer.md.
Remove-Item -Force C:\Workspace\_t5-sweep\.git
pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To ProjectSweepProbe
pwsh -NoProfile -File tools/gen.ps1
git init -b main ; git add -A ; git commit -m "sweep: renamed copy of the template"
git grep -i -l -E "projectsurvive|project-survive|project_survive"

# 16: the settings check
git status --short UnityProject/ProjectSettings/ ; git diff -- UnityProject/ProjectSettings/
```
