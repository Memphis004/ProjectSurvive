# Stage NN — <title>

> Copy this file to `docs/stages/NN-report.md`. Keep it to about one page: this is a
> verdict, not a log. Long output belongs in `artifacts/` with a link from here.

## 1. Summary (3–5 lines)

What the stage set out to do, what actually landed, and the one thing a reader should
remember. Name the files or systems that changed, not every line of the diff.

## 2. Acceptance

One row per acceptance criterion, in the order the stage prompt lists them. Every row
is **PASS**, **FAIL** or **NOT RUN** — never blank, never "probably".

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | <the criterion, quoted or paraphrased exactly> | PASS / FAIL / NOT RUN | `artifacts/<path>` or a command and its result |
| 2 | … | … | … |

Rules for this table:

- **PASS** needs a measurement, a test name, or a file that was opened and read — not
  an intention.
- **FAIL** stays in the table. Do not delete a failed row or reword the criterion to
  match what was built.
- **NOT RUN** names what was missing (no Editor, no browser, no licence). NOT RUN is
  not a pass and cannot close a stage whose gate it is.
- A number (size, time, count) quotes the configuration and the machine it came from.

## 3. Not tested, and risks

- What was **not** exercised, and why (missing environment, out of scope, blocked).
- What is now riskier than before: a new dependency, a wire-format change, a rule that
  only one test covers, a fixture that will drift.
- Anything a later stage must not forget. Link the ADR if one was written.

## 4. Commands actually run

The exact commands, in order, with a one-line result each. No pasted logs.

```powershell
pwsh -NoProfile -File tools/test.ps1              # PASS 10 | FAIL 0 | NOT RUN 3
pwsh -NoProfile -File tools/test.ps1 -Suite Full  # PASS 10 | FAIL 0 | NOT RUN 2
```

---

Optional, only when they add something a reader needs:

- **Decisions:** links to the ADRs this stage wrote or amended (a game's ADRs start at
  **0100**).
- **Screenshots:** links to `artifacts/ui/…` when the stage changed a screen, with the
  size and the build configuration they were taken at.
