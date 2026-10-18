# ProjectSurvive — game template

A **genre-free game skeleton** on Unity 6.3 + URP, targeting **Web (WebGL2,
single-thread) on desktop browsers**, with every rule in pure C# so it can be tested
without opening the Editor.

It is not a prototype of a game. It is the shell, the data pipeline, the tooling and
the documentation a game starts from, with the original game's systems cut out and
kept as [examples](examples/).

## What ships

| Piece | What it gives you |
| --- | --- |
| `lib/` (`com.projectsurvive.core`) | seven layers with enforced asmdefs; `Shared` primitives (`IRandomSource`/`Xoshiro128`/`RandomStreams`, `IClock`, `FixedTicker`, `EventRing<T>`, `PrevCurBuffer<T>`, `Result`/`Error`); `IGameConfig` + its Luban adapter; `RunSession`/`SceneFlow`/`ReplayLog`; the MessagePack/protobuf probes |
| `data/` | three Luban tables (`constant`, `localization`, `sample`), a schema, a validator with testable rules, six broken-data fixtures |
| `UnityProject/` | six scenes (Boot, Title, Hub, Encounter, Result, ThaiTextSpike) built by one builder each; the App/Run/Scene scopes; one scene loader; the 7-probe smoke suite; the page-global bridge; the Web build pipeline |
| the loop | Boot → Title → Hub ⇄ Encounter → Result → Title, with a run created by the title and disposed by arriving back at it |
| `tools/` | every gate in [the table](../../knowledge.md#commands--every-tool-in-tools): test suites, data pipeline, Web build, size and coverage measurement, link check |
| `tests/web/` | Playwright specs that drive a real build through the documents each screen publishes |
| `docs/` + `llmwiki/` | the layer contract, the data schema, the test-suite contract, the gotchas, and the extraction's own plan and reports |

## Requirements

- Windows with **Unity `6000.3.9f1`** (the patch is locked in
  [`tools/versions.lock.json`](../../tools/versions.lock.json)) + **WebGL Build
  Support**, Unity Personal licence is enough.
- **.NET SDK** 8+, **Node.js** 18+ (Playwright drives the installed Chrome),
  **7-Zip** once (to unpack Luban).
- The data toolchain is not committed: `tools/setup-tools.ps1` downloads Luban 5.1.0
  and protoc 33.6 into `tools/vendor/` and verifies their SHA-256.

```powershell
git lfs install
pwsh -NoProfile -File tools/doctor.ps1        # environment verdict, exit code 0/1/2
pwsh -NoProfile -File tools/setup-tools.ps1   # once
pwsh -NoProfile -File tools/test.ps1          # Fast (Editor open)
```

## Start a game from the template

1. **Copy** the tree (not a fork of the branch): the game gets its own repository.
2. **Rename the identity** with the tool, which covers all six spellings at once —
   file contents, file names and folder names (asmdefs, csproj, the `.sln`,
   `package.json`, `manifest.json`, `luban.conf`, the `csharp_namespace` in
   `app.proto`, `link.xml`, `InternalsVisibleTo`, `ProjectSettings`, the `.jslib`,
   `tests/web`), and keeps each `.meta` with its file so GUIDs survive:

   ```powershell
   pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To MyGameName -DryRun
   pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To MyGameName
   pwsh -NoProfile -File tools/gen.ps1        # the generated readers carry the new namespace
   pwsh -NoProfile -File tools/test.ps1       # Fast
   ```

   `-DryRun` lists every file, folder and replacement count without touching
   anything, and a second real run changes nothing (it is idempotent).
   `docs/template/ORIGIN.md` and `docs/template/examples/**` keep the old name on
   purpose, so `git grep -i projectsurvive` in the new game finds provenance and
   examples — nothing else. `tools/rename.ps1 -PatchFile <p>` rewrites a
   `generic:` commit's diff instead, for the upgrade path below.
3. **Keep the layer contract.** The asmdef/csproj matrix and the guard tests are the
   reason the rules are testable; do not relax them to make a game compile.
4. **Keep the pipeline.** `data/` → `tools/gen.ps1` → `Generated` is the only path
   for a balance number (AGENTS rule 5). Delete the `sample` table with the game's
   first real table — it exists only to exercise both reference mechanisms.
5. **Start the game's own history:**
   - `docs/stages/` from [`_TEMPLATE.md`](../stages/_TEMPLATE.md), stages numbered
     from 01;
   - ADRs from **0100** (AGENTS rule 12) — 0001–0099 belong to the template;
   - a `llmwiki/glossary.md` with the game's own terms;
   - replace the Hub/Encounter/Result placeholders and add the game's systems in
     `Domain` / `Application`.
6. **Measure before promising:** the template's timings and sizes are the reference
   machine's, not the game's. `tools/test.ps1 -Suite Full` and
   `tools/measure-web-size.mjs` are how a claim is earned.

## Upgrade a game from the template

The template's commits are prefixed **`generic:`** (AGENTS rule 14) precisely so they
can be cherry-picked into a game without dragging game work along.

- **Take:** commits whose message starts with `generic:` — shared code, template
  fixes, tooling. They are written to name no game type.
- **Do not take:** anything under `docs/template/` (the extraction's own history) or
  the examples; a game has its own ADRs and notes by then.
- **Resolve by rule, not by file:** if the template changed the layer matrix, the data
  pipeline or the test suites, follow the rule and re-apply it to the game's files.
  A `git checkout --theirs` of `knowledge.md`, `AGENTS.md` or a suite doc is not an
  upgrade.
- **Re-run the gates** after an upgrade: `tools/test.ps1 -Suite Fast`, then Full at
  the game's own stage boundary. A template change that compiles but breaks a game's
  data is what `verify-generated` and the fixtures are for.

## What is in this folder

| Path | Contents |
| --- | --- |
| [`inventory.md`](inventory.md) | T0: every file classified KEEP / GENERALIZE / DROP / MOVE / REGEN, with the cut order |
| [`t1-report.md`](t1-report.md) | T1: the cut and the rebuilt shell, with measurements |
| [`t2-report.md`](t2-report.md) | T2: the data pipeline and its fixtures |
| [`t3-report.md`](t3-report.md) | T3: this documentation pass |
| [`t4-report.md`](t4-report.md) | T4: the rename tool, and the rehearsal that proves it |
| [`rename-rehearsal.md`](rename-rehearsal.md) | T4: a real rename of a throwaway copy, with the suites that followed it |
| [`ORIGIN.md`](ORIGIN.md) | where this tree came from, and the one file the rename leaves alone |
| [`examples/`](examples/) | the game's ADRs, system notes, EcsLite gotcha, glossary and design brief — **examples, not rules** |

A live document must never link into `examples/`; the folder is excluded from
`tools/check-links.mjs` on purpose (see its README).
