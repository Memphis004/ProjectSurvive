# Changelog

One entry per released template version. A game that copied the tree reads this to see
what changed *after* its copy — with the `generic:` commit prefix (AGENTS rule 14) as
the mechanism, and [`README.md`](README.md) §Upgrade as the procedure.

## template-v1 — 2026-10-18

Created from the game repository at **`survive-template-base`** =
`c58373e0883a1b8ba9cbbf72a1bfca63d8f132f1` — the finished prototype tagged
`survive-stage-10`, with the game removed. Branch `template/main`, tagged
`template-v1`.

**What it is.** A genre-free Unity 6.3 + URP game skeleton targeting Web (WebGL2,
single-thread) on desktop browsers: seven asmdef-enforced layers under `lib/`, a Luban
data pipeline whose numbers live in `data/`, six scenes built by one builder each, a
placeholder loop (`Boot → Title → Hub ⇄ Encounter → Result → Title`) wired through three
lifetimes, the tooling that builds and tests all of it, and the documentation a game
starts from. None of the original game's rules, tables or screens survive in the live
tree; its ADRs, system notes and design brief are kept as **examples** under
[`examples/`](examples/).

**How it was built**, one report per step:

| Step | Report |
| --- | --- |
| T0 inventory and plan | [`inventory.md`](inventory.md) |
| T1 the cut and the rebuilt shell | [`t1-report.md`](t1-report.md) |
| T2 data pipeline, proto, validator, fixtures | [`t2-report.md`](t2-report.md) |
| T3 docs, knowledge, gotchas, link check | [`t3-report.md`](t3-report.md) |
| T4 `tools/rename.ps1` and its rehearsal | [`t4-report.md`](t4-report.md) · [`rename-rehearsal.md`](rename-rehearsal.md) |
| T5 the verification pass and this tag | [`t5-report.md`](t5-report.md) · [`baseline.md`](baseline.md) |

**Verified at the tag** (see [`baseline.md`](baseline.md) for the numbers): 124 xUnit,
30 EditMode, 6 PlayMode and 4 Playwright tests green across the two Editor states the
harness needs; doctor, `gen`, `verify-generated`, `validate-data`, `check-fixtures` and
`check-links` all green; a Release player of 60.50 MiB raw / 18.04 MiB gzip / 12.45 MiB
Brotli against ADR 0011's `< 30 MiB` gate and `< 15 MiB` target; ~2.9 s from navigation
to the Title screen; and a throwaway copy renamed end to end with the old name left only
in `ORIGIN.md` and `examples/`.

**Starting a game from it.** Copy the tree, then
`tools/rename.ps1 -From ProjectSurvive -To <YourName>`, `tools/gen.ps1`, `tools/test.ps1`.
[`README.md`](README.md) walks the first hour; [`ORIGIN.md`](ORIGIN.md) is the one file
the rename leaves alone.

**Known limits at the tag.** No save format (only the serialization probe); no game
systems — `Simulation` is an empty guarded layer; Firefox is not installed on the
reference machine, so cross-browser compatibility is NOT RUN; the host browser version
recorded in `versions.lock.json` (Chrome `154.0.8037.98`) has since auto-updated to
`155.0.8059.80`, which is the version the browser numbers were taken with.
