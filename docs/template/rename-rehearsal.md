# T4 — the rename rehearsal

A rename tool is easy to *almost* get right: it looks correct in the tree you ran it
on, and the spelling it missed only surfaces as a Unity error, a namespace that does
not exist, or a browser global that no longer answers — in the game that just started
from it. So the tool's acceptance test is not a unit test. It is a **real copy of this
template, renamed to `ProjectRenameProbe`, taken all the way through every gate in a
folder that is then deleted**.

Everything below is from one run on 2026-10-18, on the reference machine
(`knowledge.md` §Environment), worktree `C:\Workspace\ProjectSurvive-template`. Raw
logs: [`artifacts/rename-rehearsal/`](../../artifacts/rename-rehearsal/) (the pair of
`test-summary.md` files, the WebGL build log, the Playwright log, the rename/gen/verify
logs). The throwaway folder was deleted at the end and nothing from it was committed
here.

## 1. The copy

| | |
| --- | --- |
| Source | `C:\Workspace\ProjectSurvive-template` (working tree, not `HEAD`) |
| Destination | `C:\Workspace\_rename-rehearsal` (throwaway, deleted afterwards) |
| Excluded | `.git`, `Library`, `Temp`, `Logs`, `obj`, `bin`, `UserSettings`, `node_modules`, `artifacts`, `Builds` |
| Size / files | 40 MB, 1493 files (the source is 3.5 GB, 3.3 GB of it `UnityProject/Library`) |
| Tool | `robocopy /E /XD …` — 1 s |

Two things the copy taught before the rename even ran:

- **A worktree's `.git` is a *file*.** In this checkout `.git` contains one line
  (`gitdir: C:/Workspace/ProjectSurvive/.git/worktrees/ProjectSurvive-template`), so a
  name-based directory exclusion does not skip it. The copy deleted it and ran
  `git init` — which is what a game does anyway, since the template is copied, not
  forked.
- `node_modules` and `UnityProject/Builds` are build output: excluded, and rebuilt in
  the copy (`npm ci`, 2 s, 3 packages; the WebGL player).

## 2. Steps, and what they took

| # | Step | Command | Result | s |
| --- | --- | --- | --- | --- |
| 1 | Own repository | `git init -b main && git add -A && git commit` | `b8b6888`, 1219 tracked files | 4 |
| 2 | Toolchain | `tools/setup-tools.ps1` | PASS 2 / FAIL 0 | 3 |
| 3 | **Rename** | `tools/rename.ps1 -From ProjectSurvive -To ProjectRenameProbe` | see §3 | 2 |
| 4 | Regenerate | `tools/gen.ps1` | 9 `.cs`, 3 `.bytes`, `configHash DDFDF338…` | 11 |
| 5 | Commit the rename | `git add -A && git commit` | `bf8d924`, 221 files changed | 1 |
| 6 | Reproducibility | `tools/verify-generated.ps1` | **PASS 9 / FAIL 0 / WARN 0** | 55 |
| 7 | Full suite, Editor closed | `tools/test.ps1 -Suite Full` | **PASS 10 / FAIL 0 / NOT RUN 2** | 589 |
| 8 | Open the copy | `unity open …\UnityProject` → `unity status` = `ready` | first import 2 min 4 s | 124 |
| 9 | Full suite, Editor open | `tools/test.ps1 -Suite Full` | **PASS 10 / FAIL 0 / NOT RUN 2** | 75 |
| 10 | DoD grep | `git grep -il projectsurvive` | 10 files, all expected (§4) | <1 |

The two `NOT RUN` lines are the harness working as documented, not gaps: run 7 cannot
run the Unity suites (no Editor attached to the copy) and run 8 cannot build
(`UnityProject/Temp/UnityLockfile` — batch mode cannot share a project). Between the two
runs **all twelve steps pass**:

| Run 7 (Editor closed) | | Run 9 (Editor open) | |
| --- | --- | --- | --- |
| dotnet build / test | PASS, **124** xUnit | unity: EditMode | PASS **30/30** |
| sim: determinism / streams | PASS | unity: PlayMode | PASS **6/6** |
| data: validate / reproducible / fixtures | PASS ×3 | web: WebGL build | NOT RUN (lock) |
| unity: EditMode / PlayMode | NOT RUN (no Editor) | web: Playwright | NOT RUN (no player built) |
| **web: WebGL build (Development)** | PASS, 449.8 s | | |
| **web: Playwright (smoke+title+loop)** | PASS, 4 tests in Chrome | | |

The browser is the real check that the rename is complete, because its smoke report is
produced by the *renamed* build: seven probes pass (`config.tables: hash=DDFDF338
generated=DDFDF338 localization=28`), the MessagePack/protobuf round-trips still work
and `localization.thai` finds its Thai text — i.e. the page bridge, the assembly names
and the config hash all survived the rename. Player: 117,521,059 bytes (Development —
a number about debug symbols, not about the game, ADR 0003).

## 3. What the tool changed in the copy

| | |
| --- | --- |
| Scanned | 1211 files, 319 folders |
| Content rewritten | **201 files, 833 replacements** |
| Renamed | **46 files, 2 folders** (36 of them detected by `git` as renames) |
| Binary / skipped | 13 files not text; 2 excluded folders, 13 excluded paths |
| `.meta` pairs | renamed with their file, GUIDs unchanged — confirmed by the fact that the renamed scenes still load in the Editor and the player still builds |
| Old name left behind | **none** |

The 46 renamed files are the identity, and they add up: **14** `.asmdef` + **14**
`.meta` (28), **11** `.csproj`, the `.sln` (1), the WebGL plugin and its `.meta` (2),
and the two URP assets with their `.meta` (4). Two of the 14 asmdefs live inside
`lib/Runtime/Generated/` — the pair that made the first attempt fail (§5). The two
renamed folders were
Playwright's `test-results/` directories, named after a previous run — which is how a
folder rename gets exercised at all, since no source folder is named after the project.

Two overwrites to know about: `docs/template/README.md` and
`knowledge.md` are live documents, so their `ProjectSurvive` mentions became
`ProjectRenameProbe` in the copy — a game is expected to rewrite them anyway, and the
provenance they would otherwise lose is `docs/template/ORIGIN.md`, which the tool skips.

## 4. The DoD check: the old name is gone

```
$ git grep -il projectsurvive
docs/template/ORIGIN.md
docs/template/examples/adr/0007-battle-contract-and-asmdef-placement.md
docs/template/examples/adr/0018-battle-camera-projection.md
docs/template/examples/game-gdd-and-extraction-plan.md
docs/template/examples/systems/{backpack,battle-contract,battle-presentation,
                                  battle-simulation,camp,data-schema}.md
```

Ten files: the provenance note and the nine examples. Nothing in `lib/`, `data/`,
`UnityProject/`, `tools/`, `tests/` or the live docs. The same check over the untracked
working tree (minus `.git`, `Library`, `node_modules`, `artifacts`, `Builds`) returns the
same ten.

## 5. The bug the rehearsal caught

The first run of step 3 **failed the DoD** in a way no unit fixture would have shown:

```
$ git grep -il projectsurvive          # first run, after the rename
lib/Runtime/Generated/Luban/ProjectSurvive.Core.Generated.Luban.asmdef
lib/Runtime/Generated/Proto/ProjectSurvive.Core.Generated.Proto.asmdef
```

T4's brief says to exclude `lib/Runtime/Generated/**` "because `gen.ps1` recreates it".
That is true of the `.cs` files — and only of them. The two `.asmdef` files in that
folder are hand-written, `gen.ps1` never writes them, and they declare an assembly by
name; had they kept the old name while every referencing `.asmdef` changed, Unity would
have failed to resolve `ProjectRenameProbe.Core.Generated.Luban` and the WebGL build
would have died with a compile error rather than a rename error.

The fix is in the tool, not in the report: the exclusion is now
`lib/Runtime/Generated/**/*.cs` (exactly what `gen.ps1` owns), and the `.asmdef` files
follow the rename like every other hand-written file. Re-running the rehearsal from a
fresh copy produced §3 and §4 — 46 renamed files instead of 42, and no residual name.

This is the whole reason the step exists: the failure was found in a folder that was
about to be deleted, not in a game.

## 6. `-PatchFile` against a real `generic:` commit

The upgrade path in [`README.md`](README.md) hands a game a `generic:` commit's diff, so
the tool's patch mode was exercised on one — `5f5118a`, a real commit of this
repository:

```
git format-patch -1 5f5118a -o /tmp/patchcheck          # 34 occurrences of the old name
tools/rename.ps1 -From ProjectSurvive -To ProjectRenameProbe -PatchFile …0001-*.patch
→ …0001-….renamed.patch, 34 replacements, 0 left
git apply --stat …0001-….renamed.patch                  # 28 files changed, exit 0
```

Both halves matter: the header paths and the hunk text are rewritten together (so
`diff --git a/lib/ProjectSurvive.Core.csproj …` and the `RootNamespace` inside the hunk
agree), and the result is still a patch `git apply` can parse. Whether it *applies* is
the game's business — context lines drift.

## 7. What the rehearsal does not prove

- **The download path of `setup-tools.ps1`.** `tools/vendor/` was copied with the tree,
  so step 2 reported "already installed" (3 s). A fresh clone downloads ~19 MB and
  verifies two SHA-256s; that was proven in T1/T2, not here.
- **`npm ci`'s network path.** It took 2 s and installed 3 packages because the npm
  cache and the Playwright browser are machine-level. The *result* (Playwright drives
  Chrome) is proven by step 7.
- **A Release build.** Step 7 built Development, whose size and timing may not be
  quoted (ADR 0003), and no size profile was selected. This is a correctness rehearsal,
  not a measurement.
- **The two Unity suites in one process.** The harness cannot run EditMode/PlayMode and
  a WebGL build in the same run — one needs the Editor, the other needs it gone. That is
  a documented property of `test.ps1`, not something the rename caused.
- **A game's own content.** The copy still had the placeholder loop and the three
  template tables; a rename in month three, with a game's own asmdefs and tables, is a
  bigger diff (and the reason the tool refuses to run when the new name already exists).
- **The `--proxy`/licence side of Unity.** The Editor on this machine is licensed and
  the copy opened without a dialog.
