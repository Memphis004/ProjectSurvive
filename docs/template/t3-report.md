# T3 — docs and knowledge

## 1. Summary

The template's documentation now describes the template. `AGENTS.md` carries the
layer contract (Simulation on Shared+Domain only, Infrastructure implementing
Application ports, Contracts independent, Generated never edited) and the ADR
numbering split (template 0001–0099, a game from 0100). `knowledge.md` restarted
around T0–T5 with a table of every `tools/` command; the architecture and test-suite
documents were rewritten without the survivor loop, the grid bag or the ECS engine.
The game's thirteen ADRs, six system notes, EcsLite gotcha, glossary and design brief
moved to `docs/template/examples/` behind a README that says they are examples; its
design document and twenty-five stage reports were deleted. Thirteen general gotchas
were written from the facts that had been living in `llmwiki/index.md`, and a
dependency-free `tools/check-links.mjs` now proves the live docs resolve.

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `AGENTS.md`: Shared ← Domain ← Application ← Presentation; Simulation on Shared+Domain; Infrastructure implements Application ports; Contracts independent DTOs; Generated never edited; template ADRs 0001–0099, game ADRs from 0100 | **PASS** | [`AGENTS.md`](../../AGENTS.md) rules 3 and 12; doc list no longer names `docs/gdd` |
| 2 | `knowledge.md`: status table restarted as T0–T5, and a table covering every command in `tools/` | **PASS** | [`knowledge.md`](../../knowledge.md) §Status, §Commands — 17 rows plus `versions.lock.json`/`vendor/` named as non-tools |
| 3 | `docs/architecture/overview.md` has no survivor/ย่าม/ECS content and a neutral three-scope table; `docs/testing/test-suites.md` matches the remaining suites; `.aiignore` names no deleted path | **PASS** | [`overview.md`](../architecture/overview.md) (3 tables, placeholder loop, `Simulation` described as empty); [`test-suites.md`](../testing/test-suites.md) (124 xUnit / 30 EditMode / 6 PlayMode / 4 Playwright); [`.aiignore`](../../.aiignore) |
| 4 | ADRs 0001–0006, 0008, 0011, 0012, 0022, 0024 kept with an "Inherited by template-v1" line; 0007, 0009, 0010, 0013–0021, 0023 moved to `docs/template/examples/adr/` with a README | **PASS** | 11 files in [`docs/adr/`](../adr/) each carry the line (grep `Inherited by`); 13 files + [`README.md`](examples/adr/README.md) in the archive |
| 5 | Six general gotchas kept, `ecslite-pool-refs` moved, and the thirteen named gotchas created | **PASS** | `llmwiki/gotchas/` holds 19 notes; [`examples/gotchas/ecslite-pool-refs.md`](examples/gotchas/ecslite-pool-refs.md) |
| 6 | `llmwiki/systems` keeps `loop-and-scopes`, `unity-shell`, `data-schema`; the rest move to examples | **PASS** | 3 notes in `llmwiki/systems/`, 7 (six moved + the T2 data-schema example) in [`examples/systems/`](examples/systems/) |
| 7 | `llmwiki/index.md` gotcha table lists only general rows; `stage-log.md` restarted with "Notes carried forward" kept | **PASS** | [`index.md`](../../llmwiki/index.md) (19 general gotchas, examples section); [`stage-log.md`](../../llmwiki/stage-log.md) T0–T3 + §Notes carried forward |
| 8 | `docs/gdd` and the stage reports deleted; `docs/stages/_TEMPLATE.md` created from the old report structure | **PASS** | 25 files removed: the design document and 24 stage reports (`git show --diff-filter=D --name-only`); [`_TEMPLATE.md`](../stages/_TEMPLATE.md) |
| 9 | `docs/template/README.md` explains what the template is, how to start a game and how to upgrade | **PASS** | [`README.md`](README.md) — six sections, including the `generic:` cherry-pick rule and the manual rename list until T4's tool lands |
| 10 | `tools/check-links.mjs` checks markdown links in `docs/` and `llmwiki/` | **PASS** | `node tools/check-links.mjs` → 175 links in 48 files, all resolve, exit 0; a planted broken link makes it exit 1 and name the file |

**DoD**

| Requirement | Verdict | Evidence |
| --- | --- | --- |
| `check-links` passes | **PASS** | command 1 below; the same run over a planted broken link exits 1 |
| No live document links into a deleted file | **PASS** | check-links covers `docs/` (minus the examples archive), `llmwiki/`, `AGENTS.md`, `knowledge.md`; the only remaining mentions of cut paths are inside the T0 plan, which now says it is the plan as written |
| `tools/test.ps1 -Suite Fast` passes | **PASS** | command 2: **PASS 9 \| FAIL 0 \| NOT RUN 3 \| WARN 0**, 171.3 s (`artifacts/test-summary.md`) |

## 3. Not tested, and risks

- **Full suite not re-run.** T3 changed no code, scene, data or web artifact, so the
  T2 Full result (PASS 10 / FAIL 0 / NOT RUN 2, WebGL + 4 Playwright tests) still
  stands; the Full gate for the template is T5.
- **Anchors are not checked.** `check-links.mjs` verifies that a path resolves, not
  that `#section` exists in the target. A renamed heading can still leave a dead
  anchor.
- **`docs/template/examples/` is excluded from the link check on purpose.** Its files
  keep their original links, some of which point at the deleted reports; the folder
  README says so. Nothing outside the archive may link into a deleted file, and the
  tool enforces that half.
- **`.aiignore` is new.** Q12 of the inventory said it did not exist; T3 creates it
  with live paths only (Unity `Library`/`Builds`, `artifacts/`, `tools/vendor/`, test
  output, the examples archive). No tool in the repo consumes it yet, so it is a
  convention rather than an enforced rule.
- **The rename checklist in `docs/template/README.md` is manual** until T4's
  `tools/rename.ps1` lands; the list is the tool's specification, not a tested
  procedure.
- **`docs/template/inventory.md` is kept as written** (a T0 plan that names paths cut
  later) with a header saying so. It is history, not a description of the tree.

## 4. Commands actually run

```powershell
node tools/check-links.mjs                         # 175 links / 48 files, all resolve, exit 0
                                                   # (a probe file with one dead link: 1 BROKEN, exit 1)
pwsh -NoProfile -File tools/test.ps1 -Suite Fast   # PASS 9 | FAIL 0 | NOT RUN 3 | WARN 0, 171.3 s
git status --short                                 # 26 deletions, the ADR/systems/gotcha moves, the new docs
```
