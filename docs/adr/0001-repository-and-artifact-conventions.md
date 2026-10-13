# ADR 0001 — Repository and artifact conventions

- **Status:** Accepted
- **Date:** 2026-10-13
- **Stage:** 00

## Context

Stage 00 creates the repository from scratch. Several conventions were open:

- Where the game rules live relative to the Unity project.
- Which files are hand-written vs. generated.
- Which large files go through Git LFS vs. plain Git.
- Where throwaway output (logs, screenshots, traces) goes.

Rules 3, 4, and 5 of `AGENTS.md` already constrain most of this (layering via
asmdef, `Generated/` is never hand-edited, balance numbers live in `data/`), but
the physical layout and the line between tracked and untracked files were not
fixed.

## Decision

1. **Game rules live in `lib/`, a local UPM package** (`com.projectsurvive.core`),
   outside `UnityProject/`. This lets the rules compile and be tested with
   `dotnet test` without the Editor.
2. **`lib/Runtime/Generated/**` is machine output.** It is produced by
   `tools/gen.ps1` from `proto/` and `data/`. Hand edits are forbidden; the
   `verify-generated` tool (Stage 02) fails the build if regeneration produces a
   diff.
3. **`data/` holds all balance numbers**; no numeric balance value is embedded
   in code. Generated `.bytes` config is **ignored by Git** (reproducible from
   `data/`), while generated **C#** is **tracked** so `verify-generated` can
   diff it.
4. **`artifacts/` is untracked** except for its `.gitkeep`. Logs, reports,
   screenshots, and Playwright traces all land here.
5. **Git LFS tracks binary art and audio** (`png`, `psd`, `fbx`, `wav`, `ogg`,
   plus common Unity binaries). Text assets (`.unity`, `.prefab`, `.asset`,
   `.meta`, code, data, docs) stay in normal Git with LF normalization so diffs
   and code review stay readable.
6. **The canonical GDD is `docs/gdd/prototype-v0.3.md`.** The original combined
   document is kept at the repository root as the verbatim source it was copied
   from; if the two ever diverge, `docs/gdd/` wins and the root copy is updated
   by copying again.

## Consequences

- **Positive:** an agent can change a rule and run `dotnet test` without opening
  Unity; generated code drift is detectable; the repo stays small.
- **Negative:** there are two copies of the GDD. This is deliberate (the root
  file is the untouchable original input) but must be watched for drift.
- **Negative:** on a fresh clone, `git lfs install` is required before art
  appears correctly. Documented in `knowledge.md`.
- **Follow-up:** Stage 02 must implement `.gitignore`-aware regeneration so that
  the tracked/ignored split above actually holds.
