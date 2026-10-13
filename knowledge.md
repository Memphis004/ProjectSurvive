# ProjectSurvive — Knowledge Base

Thai-ghost-hunter survivor-like (Vampire Survivors style) with a grid **bag**
inventory (inspired by *Backpack Immortal Tale*). Theme: a Thai folk
ghost-doctor (`Mor`), 1 class, 2.5D. Target: **Web (WebGL2, single-thread) on
desktop browsers.**

This file is the fast on-ramp. Read it first, then follow the links.

---

## Status

| Stage | Title | Status |
| --- | --- | --- |
| **00** | **Discovery & Baseline** | **DONE** — see [`docs/stages/00-report.md`](docs/stages/00-report.md) |
| **01** | **Core library & dotnet harness** | **DONE** — see [`docs/stages/01-report.md`](docs/stages/01-report.md) |
| **02** | **Static data pipeline (Luban, protoc)** | **DONE** — see [`docs/stages/02-report.md`](docs/stages/02-report.md) |
| **02b** | **Battle contract v1 (interfaces + fake + contract tests)** | **DONE** — see [`docs/stages/02b-report.md`](docs/stages/02b-report.md) |
| **03** | **Unity shell, first Web build, Thai text spike** | **DONE** — see [`docs/stages/03-report.md`](docs/stages/03-report.md) |
| **04** | **Prototype data (14 pieces, 5 combos, 5 floors)** | **DONE** — see [`docs/stages/04-report.md`](docs/stages/04-report.md) |
| 05 | Inventory domain | not started |
| 06 | Shop, economy, run, scene flow | not started |
| 07 | Battle core (headless) & bot sim | not started |
| 08 | Battle presentation 2.5D | not started |
| 09 | Camp scene & bag UI | not started |
| 10 | Full loop (vertical slice) | not started |
| 11 | Thai ghost AI, Pob boss, balance | not started |
| 12 | Save, resume, settings | not started |
| 13 | Content complete & UX | not started |
| 14 | Test harness & CI | not started |
| 15 | Web release candidate | not started |

**Current state:** Stages 00–03. `lib/` is a real UPM package
(`com.projectsurvive.core`) with six hand-written layer `.asmdef`s plus two
generated ones, the vendored EcsLite, the deterministic `Shared`/`Infrastructure`
primitives (`IRandomSource` / `Xoshiro128`, `RandomStreams`, `IClock` /
`SystemClock`, `FixedTicker`, `Result` / `Error`), and the Stage 02 data pipeline:
twelve Luban tables under `data/`, generated C# under `lib/Runtime/Generated/`,
the `IGameConfig` port with its Infrastructure adapter, and the config hash in
build metadata. Stage 02b added the **battle contract v1**
(`Domain/Combat/Battle`: `IBattleSession`, `IBattleFrame`, `IBattleEventSource`,
`BattleEventRing`, `BattleReplay`, the loadout/result/setup types), a deterministic
`FakeBattleSession` in Application, and the executable
`BattleSessionContractTests<TFactory>`. **186 xUnit tests pass** (57 Stage 01, 73
Stage 02, 34 Stage 02b, 10 Stage 03, 12 Stage 04). **No gameplay rule exists yet** —
`Domain`, `Application`, `Simulation` and `Contracts` hold the battle *contract* but
no combination/combat rule.

Stage 03 built the **Unity shell** (`UnityProject/`, Unity `6000.3.9f1` + URP,
referencing `lib/` as a local package): a composition root, three scenes
(Boot / Title / ThaiTextSpike) built through the Editor API, a 7-probe smoke suite,
and a WebGL2 build that a Playwright suite asserts on through page globals. It
settled the Thai text question in [ADR 0008](docs/adr/0008-thai-text-ui-stack.md)
(uGUI + TextMeshPro, **Static** baked font assets, 182 characters verified in the
player). Evidence: 12/12 Unity EditMode, 5/5 PlayMode, 2/2 Playwright on the real
build, `totalMissingGlyphs=0` in the browser. The two failures it caught are written
down in [`llmwiki/gotchas/`](llmwiki/gotchas/):IL2CPP strips constructors only reflection can see, and TMP's pre-build processor
empties **Dynamic** font assets at build time.

Stage 04 filled `data/` with the **prototype content and nothing else** (no gameplay
code): 14 pieces — 3 bags, 4 arcane weapons (one per cast behaviour), 3 charms, 1 tome,
2 offerings, 1 familiar — every one of them merging to tier 3, the 5 GDD combo rules
matched by a real **tag vocabulary**, 4 Thai ghosts (incl. the 2-phase Pob boss), the
5-floor run with its wave timeline, a 14-row shop pool covering every piece, and 51
localization rows (th + en). Bag and item shapes are a **picture string** parsed in
Domain (`"XXX|XXX|XXX"`, `"X.|X.|XX"`) — the format the canonical GDD specifies, which
supersedes the Stage 02 cell-token choice ([ADR 0009](docs/adr/0009-footprint-picture-format.md)).
Five new cross-table rules came with it (item footprint, unknown tag, incomplete merge
ladder, unreachable piece, wave spawn window), each proven by a real broken-data
fixture; the chain is now **17 fixtures / 21 checks**.


**Data is the only source of balance numbers** (AGENTS rule 5): `data/` →
`tools/gen.ps1` (Luban 5.1.0 + protoc 33.6) → generated C# + `.bytes` →
`IGameConfig`. See [`llmwiki/systems/data-schema.md`](llmwiki/systems/data-schema.md).

**Engine lock:** Unity **`6000.3.9f1`** (revision `7a9955a4f2fa`), Unity Personal
license. Locked in [`tools/versions.lock.json`](tools/versions.lock.json).

---

## Environment (verified 2026-10-13)

| Tool | Version |
| --- | --- |
| OS | Windows 11 `10.0.26300` x64 |
| Git / Git LFS | `2.55.0.windows.3` / run `tools/doctor.ps1` |
| PowerShell | Windows PowerShell `5.1.26100.9444`; PowerShell `7` (preview) |
| .NET SDK | `6.0.428`, `9.0.311` |
| Node.js / npm | `v25.6.1` / `11.9.0` |
| Unity Editor | `6000.3.9f1` + WebGL Build Support |
| Browsers | Chrome `154.0.8037.98`; Edge `145.0.3800.70`; **Firefox not installed** |
| Playwright | not installed (added Stage 03/14) |

Reference machine for all performance work: Intel Core i5-14400F, 32 GB RAM,
NVIDIA RTX 5060 Ti — see [ADR 0003](docs/adr/0003-performance-baseline-machine-and-browser.md).

---

## Commands

```powershell
# Environment + dependency health check (reliable exit code)
pwsh -NoProfile -File tools/doctor.ps1

# Clone setup (once, after cloning)
git lfs install

# Core: build + xUnit tests + Sim determinism + the Stage 02 data gates — no Unity needed
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -NoPipeline   # skip the slow data gates
pwsh -NoProfile -File tools/test.ps1 -Configuration Release

# Static data pipeline (one-time setup downloads Luban + protoc into tools/vendor/)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/setup-tools.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1            # data/ + proto/ -> generated
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1  # game rules over the .bytes
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1   # regeneration is identical
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1     # broken data is rejected

# Focused runs
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter FullyQualifiedName~Xoshiro128
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 12345 --trace 5
```

Exit codes are part of the contract: `0` = all required checks passed,
`1` = a required check failed, `2` = the tool itself could not run.

New commands are added as their stage lands (`tools/build-web.ps1` /
`serve-web.mjs` at Stage 03).

---

## Repository map

```text
AGENTS.md              Master Prompt / permanent rules
knowledge.md           ← you are here
proto/                 external contracts (app.proto) — Stage 02
lib/                   local UPM package (com.projectsurvive.core): all game rules
  Runtime/{Shared,Domain,Application,Simulation,Infrastructure,Contracts,Generated}
                       one .asmdef per layer, all noEngineReferences: true
  third_party/ecslite/ vendored Leopotam EcsLite (MIT-Red) at a pinned revision
    Generated/{Luban,Proto}/   machine output (Luban readers, protoc App.cs) - never edited
  third_party/luban-runtime/ vendored Luban C# runtime used by the generated readers
  Tests~/              dotnet harness: one csproj per layer + xUnit Core.Tests
                       + Sim console + Data.Validator + fixtures/ (broken data)
data/                  Luban tables, Defines, localization (th + en in one table)
docs/                  gdd/ architecture/ adr/ stages/ testing/ build/
llmwiki/               index.md glossary.md systems/ (data-schema) gotchas/
tools/                 versions.lock.json doctor.ps1 setup-tools.ps1 gen.ps1
                       validate-data.ps1 verify-generated.ps1 check-fixtures.ps1 test.ps1
                       vendor/ (downloaded CLI toolchain, never committed)
tests/web/             Playwright specs — Stage 03+
artifacts/             untracked logs/reports/screenshots
UnityProject/          Unity shell — Stage 03
```

---

## Architecture in one breath

`Shared ← Domain ← Application ← Presentation`. `Simulation` uses
`Shared/Domain/EcsLite`. `Infrastructure` implements Application ports.
**Domain / Application / Simulation never touch `UnityEngine`; Domain and
Simulation never touch R3, VContainer, or serializers.** Enforced by asmdef.

Battle runs at a **fixed 30 Hz**, randomness only through `IRandomSource` with
separate **shop / battle / visual** streams. Every mutating command is **atomic**
and returns a **typed result**.

Full detail: [`docs/architecture/overview.md`](docs/architecture/overview.md).

---

## Document links

| Document | Purpose |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | Master Prompt + permanent rules for every stage |
| [`docs/gdd/prototype-v0.3.md`](docs/gdd/prototype-v0.3.md) | **Canonical GDD** (v0.3 combined) |
| [`docs/architecture/overview.md`](docs/architecture/overview.md) | Layers, data lifetimes, command/event flow |
| [`docs/architecture/dependencies.md`](docs/architecture/dependencies.md) | Dependency matrix (Verified / Proposed / Blocked) |
| [`tools/versions.lock.json`](tools/versions.lock.json) | Engine + tool version lock |
| [`docs/adr/`](docs/adr/) | Architecture decision records |
| [`llmwiki/systems/data-schema.md`](llmwiki/systems/data-schema.md) | **Static data schema + pipeline commands** |
| [`llmwiki/systems/battle-contract.md`](llmwiki/systems/battle-contract.md) | **Battle contract v1 — the interface between the battle and the rest** |
| [`llmwiki/index.md`](llmwiki/index.md) | Wiki index |
| [`llmwiki/glossary.md`](llmwiki/glossary.md) | Code names ↔ Thai terms |
| [`docs/stages/`](docs/stages/) | Per-stage reports |
| [`docs/testing/`](docs/testing/) | Test strategy (fills in from Stage 01) |
| [`docs/build/`](docs/build/) | Build instructions (fills in from Stage 03) |

---

## Known gaps carried forward

- `UI.MVP Lite` package has **no repository** → project-owned thin MVP for now
  ([ADR 0002](docs/adr/0002-thin-mvp-ui-approach.md)).
- Versions of VContainer, R3, UniTask and MessagePack "that work with Unity 6.3"
  are **proposed, not verified** — confirmed in Stage 03. (EcsLite is settled:
  vendored source at a pinned revision, Stage 01. Luban 5.1.0 and protoc 33.6
  are settled: pinned, installed by SHA-256, and exercised end to end, Stage 02.)
- Generated code and the `.bytes` payloads are only ever **compiled outside Unity**
  so far. That `.bytes` in `Resources/Config` load in a real Web build, and that
  the asmdef graph resolves as the csprojs do, is proven at Stage 03.
- The data schema has **no versioned migration story** yet: `SchemaVersion` in
  `ConfigBuildMetadata.g.cs` is bumped by hand in `tools/gen.ps1`, and no save
  format reads these tables yet (Stage 12).
- Nothing in `lib/` has been compiled by Unity yet. The `dotnet` harness mirrors
  the Unity compiler surface (C# 9 / netstandard2.1), but asmdef resolution
  itself is only proven in Stage 03.
- IL2CPP stripping/AOT behaviour of MessagePack, Protobuf, R3, VContainer on Web
  is **unproven** — Stage 03.
- IndexedDB flush method on Unity 6.3 — Stage 12.
- Firefox not installed → cross-browser runs are NOT RUN until it is.
- **Charm passives and offering consumable effects have no columns yet.**
  `item` records a charm's tags but not its own buff magnitude, and an offering's
  heal/bia/uses likewise. Deliberate at Stage 04
  ([ADR 0010](docs/adr/0010-item-local-effects-deferred.md)): the consumers arrive in
  Stage 05–07. **The numbers must land in `data/` when they do — never in code**
  (rule 5).
- **No performance number has been measured.** All budget figures are targets.
