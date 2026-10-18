# ProjectSurvive — template knowledge base

A **genre-free game skeleton**: Unity 6.3 + URP shell, an engine-free rule library
(`lib/`, a local UPM package), a data pipeline whose numbers live in `data/`, and a
tooling set that builds, tests and ships a **Web (WebGL2, single-thread)** player.

The loop the template ships is a placeholder:

```
Boot → Title → Hub ⇄ Encounter → Result → Title
```

Three lifetimes (`App`, `Run`, `Scene`) are wired, a run is started by the title
(ADR 0022), and every scene change goes through one loader. A game built from the
template replaces the Hub and Encounter screens and adds its own rules in `Domain`.

This file is the fast on-ramp. Read it first, then follow the links.

---

## Status

The template is extracted from a finished prototype (the game repo, tag
`survive-stage-10`). Its own steps are T0–T5; **reports live in `docs/template/`**.

| Step | Title | State | Evidence |
| --- | --- | --- | --- |
| **T0** | Inventory and plan | done | [`docs/template/inventory.md`](docs/template/inventory.md) |
| **T1** | Cut `lib/` + `data/`, rebuild the Unity shell, re-point the tools | done, green | [`docs/template/t1-report.md`](docs/template/t1-report.md) |
| **T2** | Data pipeline, proto, validator and fixtures | done, green | [`docs/template/t2-report.md`](docs/template/t2-report.md) |
| **T3** | Docs and knowledge: layers, ADR split, gotchas, links | done, green | [`docs/template/t3-report.md`](docs/template/t3-report.md) |
| **T4** | Rename tool + a rehearsal on a throwaway copy | not started | — |
| **T5** | Baseline commit and `template-v1` tag | not started | — |

A game built from this template starts its own stage numbering at 01 and writes
its reports under `docs/stages/` (skeleton: [`docs/stages/_TEMPLATE.md`](docs/stages/_TEMPLATE.md)).

**Current state:** the package has three data tables (`localization`, `constant`,
`sample`), the `IGameConfig` port with its Infrastructure adapter, the shared
deterministic primitives (`IRandomSource`/`Xoshiro128`/`RandomStreams`,
`IClock`/`SystemClock`, `FixedTicker`, `EventRing<T>`, `PrevCurBuffer<T>`,
`Result`/`Error`), the run skeleton (`RunSession`, `SceneFlow`, `ReplayLog`) and a
serialization probe. Unity ships six scenes (Boot, Title, Hub, Encounter, Result, ThaiTextSpike), a
7-probe smoke suite, the page bridge and the Web build pipeline. `dotnet` carries **124** xUnit cases; Unity
carries **30** EditMode and **6** PlayMode cases; Playwright has **4** tests in
three specs (`smoke`, `title`, `loop`).

**Engine lock:** Unity **`6000.3.9f1`** (revision `7a9955a4f2fa`), Unity Personal
license. Locked in [`tools/versions.lock.json`](tools/versions.lock.json).

### What the template deliberately does not have

| Absent | Why | Where the game-era version is kept |
| --- | --- | --- |
| A genre, a theme, a combat loop | the template is the shell, not a game | [`docs/template/examples/`](docs/template/examples/) |
| An ECS engine (EcsLite) | it left with the battle rules; `Simulation` is an empty guarded layer a game fills | [`docs/template/examples/gotchas/ecslite-pool-refs.md`](docs/template/examples/gotchas/ecslite-pool-refs.md) |
| A save format | `Contracts/Save` holds only the serialization probe | ADR 0012 and the probe's own remarks |
| Game screens beyond the placeholder loop | Hub/Encounter are deliberately empty | the inventory |

---

## Commands — every tool in `tools/`

Exit codes are part of the contract: `0` = the required checks passed, `1` = a
required check failed, `2` = the tool itself could not run.

| Tool | What it does | Typical use |
| --- | --- | --- |
| [`doctor.ps1`](tools/doctor.ps1) | environment / dependency health check with a reliable exit code | after cloning, or when a tool cannot be found |
| [`test.ps1`](tools/test.ps1) | the Fast and Full suites over dotnet, Unity and the browser; writes `artifacts/test-summary.md` | the working loop (`-Suite Fast`) and stage gates (`-Suite Full`) |
| [`setup-tools.ps1`](tools/setup-tools.ps1) | downloads Luban 5.1.0 + protoc 33.6 into `tools/vendor/` and checks their SHA-256 | once per clone, before `gen.ps1` |
| [`gen.ps1`](tools/gen.ps1) | runs the data pipeline: `data/` + `proto/` → `lib/Runtime/Generated/**`, both `.bytes` sets, the config hash | after any edit under `data/` or `proto/` |
| [`validate-data.ps1`](tools/validate-data.ps1) | runs `lib/Tests~/Data.Validator` over the generated tables (cross-row rules Luban cannot express) | after `gen.ps1`; needs `-DataDir` (see the gotcha) |
| [`verify-generated.ps1`](tools/verify-generated.ps1) | regenerates into a temp directory and compares byte for byte with what is committed | the reproducibility gate, inside Fast |
| [`check-fixtures.ps1`](tools/check-fixtures.ps1) | proves every deliberately broken fixture under `lib/Tests~/fixtures/` is rejected **for the reason its `fixture.json` names** | after adding a data rule |
| [`sim.ps1`](tools/sim.ps1) | builds and runs the headless determinism harness (`lib/Tests~/Sim`): one fingerprint per random stream + `determinismDigest` | determinism and stream-separation checks |
| [`build-web.ps1`](tools/build-web.ps1) | batch-mode WebGL build through the project's own pipeline; Development or Release, optionally with a size profile | makes the player the browser tests need |
| [`serve-web.mjs`](tools/serve-web.mjs) | static server that sets the content types a WebGL player needs | serving `UnityProject/Builds/Web` to Playwright or a person |
| [`measure-web-size.mjs`](tools/measure-web-size.mjs) | raw / gzip -9 / Brotli q11 size of every file in a build | any payload claim |
| [`measure-il2cpp-size.mjs`](tools/measure-il2cpp-size.mjs) | attributes `.wasm` bytes to the managed assembly they came from | deciding whether a package is worth its bytes |
| [`coverage-report.mjs`](tools/coverage-report.mjs) | per-file line coverage from a Cobertura report | measuring a coverage target |
| [`font-metrics.py`](tools/font-metrics.py) | measures a font file's glyph metrics at a fixed display size | an on-screen text-size claim about the face |
| [`screen-text-metrics.py`](tools/screen-text-metrics.py) | measures the ink height of text inside rectangles a player published, from a screenshot | an on-screen text-size claim about the build |
| [`check-links.mjs`](tools/check-links.mjs) | checks that every relative markdown link under `docs/` and `llmwiki/` resolves | before committing a docs change |
| [`reference/xoshiro128_reference.py`](tools/reference/xoshiro128_reference.py) | an independent reference implementation of the RNG | cross-checking `Xoshiro128Tests` |

Two paths in `tools/` are not tools: `versions.lock.json` (the engine, toolchain and
package lock) and `vendor/` (the downloaded CLIs — never committed).

`test.ps1 -Suite Fast` (the default) runs dotnet build + xUnit + the Sim modes + the
four data gates + Unity EditMode. `-Suite Full` adds PlayMode, a WebGL build and
Playwright. Fast needs the **Editor open**; Full's web steps need it **closed**, so
the complete verdict is two runs. Which test lives in which suite, with timings:
[`docs/testing/test-suites.md`](docs/testing/test-suites.md).

```powershell
pwsh -NoProfile -File tools/doctor.ps1
pwsh -NoProfile -File tools/test.ps1                 # Fast
pwsh -NoProfile -File tools/test.ps1 -Suite Full     # Full (add -Release for a gate)
pwsh -NoProfile -File tools/setup-tools.ps1          # once
pwsh -NoProfile -File tools/gen.ps1
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
pwsh -NoProfile -File tools/verify-generated.ps1
pwsh -NoProfile -File tools/check-fixtures.ps1
pwsh -NoProfile -File tools/sim.ps1 --seed 7 --trace 5
node tools/check-links.mjs
```

`tools/rename.ps1` (T4) is the one tool the template still owes: renaming the
assembly, package, scenes and `ProjectSurvive` identifiers for a new game.

---

## Environment (verified 2026-10-13)

| Tool | Version |
| --- | --- |
| OS | Windows 11 `10.0.26300` x64 |
| Git / Git LFS | `2.55.0.windows.3` / `3.7.1` |
| PowerShell | Windows PowerShell `5.1.26100.9444`; PowerShell `7` (preview) |
| .NET SDK | `6.0.428`, `9.0.311` |
| Node.js / npm | `v25.6.1` / `11.9.0` |
| Unity Editor | `6000.3.9f1` + WebGL Build Support |
| Browsers | Chrome `154.0.8037.98`; Edge `145.0.3800.70`; **Firefox not installed** |

Reference machine for all performance work: Intel Core i5-14400F, 32 GB RAM,
NVIDIA RTX 5060 Ti — see [ADR 0003](docs/adr/0003-performance-baseline-machine-and-browser.md).

---

## Repository map

```text
AGENTS.md              permanent rules for every stage
knowledge.md           ← you are here
proto/app.proto        external contract (CheckpointProbe, SaveHeader)
lib/                   local UPM package (com.projectsurvive.core)
  Runtime/{Shared,Domain,Application,Simulation,Infrastructure,Contracts,Generated}
                       one .asmdef per layer
  Runtime/Generated/   machine output (Luban readers, protoc App.cs) — never edited
  third_party/luban-runtime/   vendored Luban C# runtime
  Tests~/              dotnet harness: one csproj per layer + xUnit Core.Tests
                       + Sim console + Data.Validator + fixtures/ (broken data)
data/                  the only place a balance number lives (3 tables)
docs/
  adr/                 template decisions, 0001–0099
  architecture/        layers and dependencies
  testing/             which test runs when
  build/               payload size method
  stages/              a game's stage reports (skeleton: _TEMPLATE.md)
  template/            the extraction's own plan and reports
    examples/          the game-era ADRs, notes and gotchas — read-only examples
llmwiki/               index.md glossary.md stage-log.md systems/ gotchas/
tools/                 every script in the table above, + vendor/ (not committed)
tests/web/             Playwright specs against a real WebGL build
artifacts/             untracked logs/reports/screenshots
UnityProject/          the Unity shell
```

---

## Architecture in one breath

`Shared ← Domain ← Application ← Presentation`.

- `Simulation` may reference **Shared and Domain only**.
- `Infrastructure` implements **Application** ports (Application never references it).
- `Contracts` holds DTOs and references nothing of ours.
- `Generated` is machine output and is never hand-edited.

**Domain / Application / Simulation never touch `UnityEngine`; Domain and
Simulation never touch R3, VContainer or a serializer.** Enforced by asmdef *and*
by guard tests in `lib/Tests~`.

Time and randomness are explicit: a **fixed 30 Hz tick** (`FixedTicker`), and
randomness only through `IRandomSource` with separate **run / encounter / reward /
visual** streams. Every mutating command is **atomic** and returns a **typed result**.

Full detail: [`docs/architecture/overview.md`](docs/architecture/overview.md).

---

## Document links

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | permanent rules for every stage |
| [`docs/architecture/overview.md`](docs/architecture/overview.md) | layers, lifetimes, command flow, data pipeline |
| [`docs/architecture/dependencies.md`](docs/architecture/dependencies.md) | dependency matrix (Verified / Proposed / Blocked) |
| [`docs/testing/test-suites.md`](docs/testing/test-suites.md) | Fast vs Full, every test's home, timings, NOT RUN rules |
| [`docs/template/README.md`](docs/template/README.md) | what the template is and how to start or upgrade a game |
| [`docs/adr/`](docs/adr/) | template decisions (0001–0099) |
| [`docs/template/examples/`](docs/template/examples/) | the game's ADRs and notes — examples, not rules |
| [`llmwiki/index.md`](llmwiki/index.md) | wiki index (systems + gotchas) |
| [`llmwiki/glossary.md`](llmwiki/glossary.md) | code names ↔ Thai terms |
| [`llmwiki/systems/data-schema.md`](llmwiki/systems/data-schema.md) | the three tables, their rules, the pipeline |
| [`llmwiki/systems/loop-and-scopes.md`](llmwiki/systems/loop-and-scopes.md) | the loop and the three scopes |
| [`llmwiki/systems/unity-shell.md`](llmwiki/systems/unity-shell.md) | scenes, DI, the published globals, the Web build |
| [`tools/versions.lock.json`](tools/versions.lock.json) | engine + tool version lock |

---

## Known gaps carried forward

- `UI.MVP Lite` has **no repository**; the project owns a thin MVP
  ([ADR 0002](docs/adr/0002-thin-mvp-ui-approach.md)).
- `Simulation` is an empty, guarded layer: a game that needs an ECS engine adds one
  (the game-era EcsLite note is kept under `docs/template/examples/`).
- `Contracts/Save` holds only `CheckpointProbe`; the first real save format is the
  game's decision, and it should start at schema version 1 with a migration story.
- `SaveHeader` has no reader yet.
- The template's scene capture tool was removed with the game screens; UI layout is
  checked from Play Mode screenshots until a game adds one back.
- Firefox is not installed, so cross-browser runs are NOT RUN.
- No CI: every gate is a local command. Stage T5 records the baseline the first
  game starts from.
