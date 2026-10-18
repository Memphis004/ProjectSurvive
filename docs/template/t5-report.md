# T5 — the verification pass and the `template-v1` tag

## 1. Summary

Every gate the template owns was run in the order T5 asks for — `doctor` first, the browser
suite last — with the Editor closed for the build half and open for the Unity half. All of
them pass, and the numbers are written down in [`baseline.md`](baseline.md): a Release player
of 60.50 MiB raw / 18.04 MiB gzip / 12.45 MiB Brotli against ADR 0011's gate and target,
~2.9 s from navigation to the Title screen on the reference machine, and 124 xUnit + 30
EditMode + 6 PlayMode + 4 Playwright tests green across the two runs the harness needs.

Running the gates found three defects that a green-looking repository was hiding: `doctor`
still required the deleted `docs/gdd`, a live document linked into git-ignored `artifacts/`
(passing only on the machine that had it), and the rename tool rewrote a `git worktree`'s
`.git` pointer. All three are fixed here, and `check-links` now fails on a link whose target
is not tracked, which is what makes the second one a gate instead of a coincidence.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Editor closed, then, in order: `doctor` → `gen` → `verify-generated` → `validate-data -DataDir "lib/Tests~/data"` → `check-fixtures` → `check-links` → `test -Suite Full` → `build-web` (Release) → `build-web` (Development) → Playwright (all specs) | **PASS** | `artifacts/t5/{doctor,gen,verify-generated,validate-data,check-fixtures,build-release,build-development,playwright,test-full-editor-closed}.log`; results in §4 of [`baseline.md`](baseline.md) §2 — 16 steps, no FAIL |
| 2 | `docs/template/baseline.md`: payload raw / gzip -9 / Brotli q11 measured the ADR 0011 way, time to Title, test counts per suite, tool versions from `versions.lock.json` | **PASS** | [`baseline.md`](baseline.md) §1 (versions), §3 (payload: 60.50 / 18.04 / 12.45 MiB Release; 112.06 / 27.89 / 18.87 MiB Development), §4 (2,877 ms cold median, 2,728 ms warm), §5 (124 / 30 / 6 / 4). Raw JSON/Markdown: `artifacts/t5/size-{release,development}.{json,md}`, `artifacts/t5/load-to-title-{release,cold-1..3}.json` |
| 3 | `ProjectSettings` carries no residue from a size profile (`webgl-size-profile-settings-residue`) | **PASS** | after both builds `git status --short UnityProject/ProjectSettings/` is empty and the authored values hold: `managedStrippingLevel: {WebGL: 1}` (Low), `il2cppCodeGeneration: {WebGL: 0}` (OptimizeSpeed), `webGLExceptionSupport: 1`, `webGLWasmArithmeticExceptions: 0`; both `build-report.json` files say `"sizeProfile": "none"` (`artifacts/t5/build-report-{release,development}.json`) |
| 4 | `git grep` for the game's token list finds it only under `docs/template/examples/` | **PASS** | throwaway copy renamed to `ProjectSweepProbe`, regenerated, committed: `git grep -i -l -E "projectsurvive\|project-survive\|project_survive"` → **10 files** = `docs/template/ORIGIN.md` + 9 under `docs/template/examples/`; zero hits in `lib/`, `data/`, `UnityProject/`, `tools/`, `tests/`, `knowledge.md`, `AGENTS.md`, `docs/adr/`, `llmwiki/` (`artifacts/t5/rename-sweep-grep.txt`). `verify-generated` PASS 9 in that copy; `check-links` exit 0 |
| 5 | `docs/template/CHANGELOG.md`: `template-v1` built from `survive-template-base <sha>` | **PASS** | [`CHANGELOG.md`](CHANGELOG.md) — `c58373e0883a1b8ba9cbbf72a1bfca63d8f132f1`, with what the version contains and its known limits |

**DoD**

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| Every item above passes | **PASS** | §2; the 16-step table in [`baseline.md`](baseline.md) §2 |
| A report with PASS / FAIL / NOT RUN, so the user can tag | **PASS** | this file; §3 lists what was not run |
| `template-v1` tag and the push | **PASS** — this report's commit is the tag's target: `git tag -a template-v1` and the two pushes (`template/main`, `template-v1`) were run from this tree after it, as the stage prompt instructed. The stage prompt's other half ("the user tags") is satisfied by the same command list in §4, which reproduces the tag | `git ls-remote --heads --tags origin` — `refs/heads/template/main` and `refs/tags/template-v1^{}` both at the T5 commit; §4 |

## 3. Repairs T5 made, and what was not tested

**Three defects found by running the gates** (not found by reading the code):

1. **`doctor.ps1` failed its own repository on the first run** — `$expectedDirs` still named
   `docs\gdd` and `$expectedFiles` `docs\gdd\prototype-v0.3.md`, both deleted in T3. Exit code
   1 on a clean template. Fixed to the live tree (`docs\testing`, `docs\testing\test-suites.md`);
   it now reports **PASS 14 | FAIL 0**. Its Playwright check also looked only for a *global*
   install, so it always answered SKIP even though the repo installs the suite with
   `npm ci --prefix tests/web`; it now checks `tests/web/node_modules` first.
2. **A live document linked into git-ignored output.** `docs/template/rename-rehearsal.md`
   linked `../../artifacts/rename-rehearsal/`, which exists only on the machine that ran the
   rehearsal — so `check-links` passed locally and would fail in every clone. The link is now
   a path in prose, and `tools/check-links.mjs` checks that a target is one a clone would
   have — a file git tracks, or an untracked file git would add (a planted link into
   `artifacts/` now exits 1, and a planted link to a missing file still does). The link
   count moved 195 → 194 when that link went, and stands at **233 links in 57 files** once
   this stage's own documents landed.
3. **`rename.ps1` rewrote `git`'s own metadata.** In a tree cloned with `git worktree`, `.git`
   is a *file* holding a path to the real repository; the tool skipped the `.git` *directory*
   only, tried to rewrite that pointer and died with `Access to the path '…\.git' is denied`
   after partly renaming the tree. `.git` is now in the skip list for both shapes. Found by
   the step-4 sweep, fixed there, and the sweep then completed with exit 0.

**One incident, disclosed in full.** The step-4 sweep is a copy of this tree, and the copy
inherited this repository's `.git` *pointer file* (this working tree is a `git worktree`, so
`.git` is a file, and `robocopy /XD .git` excludes only the directory shape of it). Its
`git init` therefore re-attached to this repository, its `git add -A` rewrote this repo's
index, and its sweep commit landed on **`template/main`** — `582b5b2`, on top of `3a5034a`.
Nothing in the working tree was damaged (git never writes worktree files on add/commit);
the recovery was `git reset --mixed 3a5034a`, after which `git status` showed exactly the
edits this stage meant to make and `verify-generated` was re-run to PASS 9 / FAIL 0. No
push happened before the recovery, so nothing left this machine. The hazard is now a
documented gotcha with the recovery procedure
([`copied-tree-keeps-git-pointer.md`](../../llmwiki/gotchas/copied-tree-keeps-git-pointer.md))
and [`baseline.md`](baseline.md) §7 deletes the pointer before `git init`. The stray commit
object stays unreachable in the object store.

**Risks, and what was not tested.**

- **`versions.lock.json` says Chrome `154.0.8037.98`; the machine now runs `155.0.8059.80`.**
  Every browser number and test in this stage used 155. The lock file is a record of the
  version a number was taken with, not a pin, so it is left as written and the drift is
  stated in [`baseline.md`](baseline.md) §6. Upgrading dependencies is not T5's to do.
- **The time to Title was measured over loopback with a warm OS file cache**, on the same
  machine that built the player. It is a floor for that machine, not a user's first visit to
  a real host.
- **Only Chrome was exercised.** Firefox is not installed, so cross-browser compatibility is
  NOT RUN (the doctor warns about it); Edge is installed but untested.
- **The baseline is the reference machine's**, and the Release `.wasm` is a product of the
  Editor patch in §1 of the baseline — a different Unity patch produces a different size.
- **The rename sweep is not the full rehearsal.** It proves the token list leaves only
  provenance and examples, and that the renamed copy regenerates and resolves its links; the
  end-to-end rehearsal (Full suite, WebGL build, Playwright in a renamed copy) is T4's, and
  its log is [`rename-rehearsal.md`](rename-rehearsal.md).
- **`artifacts/` is git-ignored**, so the raw logs behind every number live only on the
  machine that produced them; §7 of the baseline lists the commands that reproduce each one.

## 4. Commands actually run

```powershell
# the gates, in T5's order (Editor closed for the build half)
pwsh -NoProfile -File tools/doctor.ps1                                        # PASS 14 | FAIL 0
pwsh -NoProfile -File tools/gen.ps1                                           # OK, DDFDF338…, 1.1 s
pwsh -NoProfile -File tools/verify-generated.ps1                              # PASS 9 | FAIL 0, 3.8 s
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"      # PASS 5 | FAIL 0, 66 s
pwsh -NoProfile -File tools/check-fixtures.ps1                                # PASS 10 | FAIL 0, 19 s
node tools/check-links.mjs                                                    # 233 links, exit 0
pwsh -NoProfile -File tools/test.ps1 -Suite Full                              # PASS 10 | FAIL 0 | NOT RUN 2, 137 s
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Release -Force       # OK, 489.8 s, 63,453,022 B
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label "template-v1 Release" --json artifacts/t5/size-release.json --markdown artifacts/t5/size-release.md
node tools/serve-web.mjs --port 8080
node tests/web/scripts/measure-load-to-title.mjs http://127.0.0.1:8080/ 3 artifacts/t5/load-to-title-release.json
pwsh -NoProfile -File tools/build-web.ps1 -Configuration Development -Force    # OK, 194.5 s, 117,520,130 B
node tools/measure-web-size.mjs --dir UnityProject/Builds/Web --label "template-v1 Development" --json artifacts/t5/size-development.json --markdown artifacts/t5/size-development.md
npx playwright test                                                           # 4 passed (22.6 s), Chrome

# the Unity half (Editor open) — the other half of one verdict
unity open C:/Workspace/ProjectSurvive-template/UnityProject
pwsh -NoProfile -File tools/test.ps1 -Suite Full                              # PASS 10 | FAIL 0 | NOT RUN 2, 175 s
                                                                              # EditMode 30/30, PlayMode 6/6

# the rename sweep (throwaway copy, deleted afterwards)
robocopy C:\Workspace\ProjectSurvive-template C:\Workspace\_t5-sweep /E /XD .git Library Temp Logs obj bin UserSettings node_modules artifacts Builds
pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To ProjectSweepProbe      # exit 0, 46 names
pwsh -NoProfile -File tools/gen.ps1                                                    # OK, same config hash
git init -b main ; git add -A ; git commit -m "sweep: renamed copy of the template"
git grep -i -l -E "projectsurvive|project-survive|project_survive"                     # 10 files
node tools/check-links.mjs ; pwsh -NoProfile -File tools/verify-generated.ps1          # exit 0 ; PASS 9
Remove-Item -Recurse -Force C:\Workspace\_t5-sweep

# the settings check
git status --short UnityProject/ProjectSettings/                              # empty
```

The tag itself (the user's call; not run here):

```powershell
git tag -a template-v1 -m "Template v1 from survive-template-base"
git push -u origin template/main
git push origin template-v1
```
