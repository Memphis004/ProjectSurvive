# INGEST — turn a stage report into a wiki note

This is a **workflow for an agent that can call tools**. It is not part of the
automated test suite (it needs an LLM and is not deterministic); the automated
half is `tools/verify-wiki.ps1`.

Trigger it with a sentence such as: **"ingest docs/stages/10d-report.md"**. One
report per run. Do the steps in order.

## Steps

1. **Read one source.** Open the named report under `docs/stages/` (and the ADR it
   cites only if the report says the decision lives there). Do not read the whole
   `docs/` tree.

2. **Name the affected pages.** Decide which hand-written pages under
   `systems/` or `gotchas/` the report changes, and which ADR/`glossary.md` row it
   touches. If no existing page fits, stop and report a proposal instead of
   creating a page — AGENTS rule 12 says do not add a page while a related one
   exists, and a new page is a user decision.

3. **Update those pages to what is true now.** Add or correct the **reason, the
   context, the gotcha and the links**. Do **not** copy the report's tables of
   numbers: a hand-written page must not become a second balance table (SCHEMA
   §1). Point at `content/` or `data/` for numbers instead.

4. **Add a back-link** from the touched page to the source report, e.g.
   `Source: [10d-report](../docs/stages/10d-report.md)`. The wiki must be
   traceable to what justified it.

5. **Retire what the report supersedes.** If a sentence in an older page is now
   false, fix it in place; if it is still useful history, mark it
   `superseded by <page>` or `superseded by <10d-report>`. Never leave an
   outdated claim unmarked.

6. **Update `llmwiki/index.md`** if a page or a routing row changed, and update
   `llmwiki/stage-log.md` with the three-line entry the AGENTS Definition of
   Done requires (what landed / result / what is next).

7. **Append one line to [`log.md`](log.md)** (append-only, oldest first):

   ```
   | 2026-10-19 | docs/stages/10d-report.md | systems/loop-and-scopes.md (added the title's start-control note + back-link) |
   ```

8. **Run the lint.** `powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1`.
   It must stay green. If the report changed `data/`, run `tools/gen-wiki.ps1`
   first and commit `llmwiki/content/` in the same change (AGENTS DoD).

## Does an ingest need a new page?

No, by default. Prefer extending the page that already covers the system. A new
page is justified only when a *new system* landed and no existing note owns it;
propose it to the user first.
