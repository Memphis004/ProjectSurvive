# ADR 0005 — Static data pipeline: shape format, placement, and reproducibility

- **Status:** Accepted (Stage 02). **§1 (footprint authoring format) superseded by
  [ADR 0009](0009-footprint-picture-format.md)** at Stage 04 — the canonical GDD
  specifies a footprint picture (`XX.|.XX`) and the picture makes a duplicated cell
  unrepresentable. Every other decision here — CSV tables, the two generated targets,
  the reproducibility gate, the `--strict`/validator split — still stands.
- **Date:** 2026-10-13

## Context

Balance numbers must live in `data/` and must never be embedded in code (AGENTS
rule 5). The GDD wants a Luban-based table pipeline that produces C# readers plus
a compact runtime payload, on a Web (WebGL2) target where every byte downloaded
matters. Three things had to be decided before writing a single table:

1. **How a bag footprint is authored** — the bag grid is the core of the game,
   and its shapes include an L-tromino and a 1×3 strip.
2. **Where the generated artifacts live**, given that the Unity project is only
   created in Stage 03 while the rule engine must already be testable now.
3. **What makes a regeneration trustworthy** — the stage's acceptance criterion
   is literally "generate twice from the same input and `git diff` is empty".

## Decision

### 1. A footprint is an explicit cell list, not a rectangle or a bitmap

> **Superseded by [ADR 0009](0009-footprint-picture-format.md).** Read this section as
> the Stage 02 reasoning, not as the current format: Stage 04 switched to the picture
> string the GDD specifies.

`footprint` is `list#sep=|,string` whose elements are `"x,y"` offsets:
`"0,0|1,0|0,1"` is the L-tromino.

Consequences, all of them deliberate:

- **An empty shape and a duplicated cell become data errors**, not surprises at
  placement time: `Footprint.TryParse` reports `footprint.empty | footprint.duplicate_cell | footprint.malformed_cell`,
  and the data validator turns that into a failing build.
- **Normalisation is mechanical.** `TryParse` translates the shape so its minimum
  x/y is 0 and reports the resulting `Width`/`Height`, so the rotation rules of
  Stage 05 never have to guess whether an author wrote a shape off-origin.
- **It is authored the way it reads on the board**, which a `3x3` rectangle or a
  `#`-bitmap string is not.
- The cost is verbosity: a 3×3 bag is nine tokens. Accepted — a 3×3 bag is the
  largest piece in the prototype, and the shape is a balance number that a human
  must be able to review.

### 2. Tables are CSV, one file per table, schema in `data/Defines/`

`data/Defines/schema.xml` holds enums and beans; `data/Defines/__tables__.csv`
registers the twelve tables; `data/tables/*.csv` and
`data/localization/localization.csv` hold the rows. CSV (not Excel) because a
spreadsheet is a binary blob that cannot be reviewed in a diff, and the GDD's
whole point is that balance changes are reviewable.

### 3. Two generated targets, tracked for C#, generated for bytes

| Artifact | Path | In Git? |
| --- | --- | --- |
| Luban C# readers | `lib/Runtime/Generated/Luban/**/*.cs` | **yes** |
| protoc C# contract | `lib/Runtime/Generated/Proto/App.cs` | **yes** |
| Runtime payload | `UnityProject/Assets/Main/Resources/Config/*.bytes` | no |
| Test payload | `lib/Tests~/data/*.bytes` | no |

- The **C# is tracked** so `verify-generated.ps1` can diff a fresh regeneration
  against it. Reviewing a generated-code diff before a commit is the cheapest
  way to notice that a schema edit changed more than intended.
- The `.bytes` are **not tracked** because they are pure machine output of the
  same inputs and ADR 0001 already decided that generated binaries stay out of
  Git. `verify-generated.ps1` checks them by regenerating and comparing hashes,
  which a tracking-based check could not do for a file that is never committed.
- Both `Resources/Config` (Unity, from Stage 03) and `lib/Tests~/data` (the
  dotnet harness, today) are written from one generation run, so the tests and
  the game can never disagree about the data.

### 4. Reproducibility is a first-class, machine-checked property

`tools/gen.ps1` writes no timestamp, absolute path, or machine name into the
tracked output; the config hash it publishes is a SHA-256 over the generated
payload, not over the inputs, so it identifies a build rather than a checkout.
`tools/verify-generated.ps1` is the gate: regenerate into a temporary directory,
compare every `.cs` and every `.bytes`, and fail if either side has a file the
other does not.

**Measured 2026-10-13:** a second `gen.ps1` run left all 35 generated `.cs` and
all 24 `.bytes` byte-identical (combined SHA-256 `3e034c09…` before and after).

### 5. Schema enforcement lives in Luban `--strict`, game rules live in a validator

Luban is run with `--strict` (without it, a failing `#ref` or range validator only
*warns* and the CLI still exits 0 — verified against the installed binary, see
`llmwiki/gotchas/luban-5-strict-and-tables.md`). It therefore owns column types,
unique primary keys, `#ref` targets and `#(range=…)` bounds.

Everything Luban *cannot* express stays in `lib/Tests~/Data.Validator`, because an
empty cell is a legitimate value there: an optional `skill_id`, "exactly one of
`item_id`/`bag_id`", a floor with no wave, a cooldown below
`constant.min_cooldown_seconds`, a shop-pool weight band that leaves a floor with
nothing to roll. The validator runs on `IGameConfig` (the domain projection), so
those rules are unit-testable without the toolchain and can never accidentally
depend on a generated row type.

## Alternatives considered

- **Bitmask/bitmap footprint string** (`"###|#.."`): compact and human-readable,
  but an empty shape and a duplicated cell are then only detectable by parsing
  the picture, and nothing stops two bag pieces from declaring different
  conventions for the same shape. Rejected.
- **Rectangular bags only** (a `width`/`height` pair): the GDD's L-shaped
  basket and the asymmetric-rotation test case of Stage 04/05 would not be
  expressible. Rejected.
- **Boolean matrix in the schema** (`list#sep=|,bool` of exactly width*height):
  same information as the cell list, but a fixed rectangle can no longer express
  a non-rectangular shape without padding. Rejected.
- **Track the `.bytes` too.** Rejected: they are regenerable from tracked inputs
  and would put thousands of binary bytes in every data-change diff.
- **Generate at build time only, keep no generated C# in Git.** Rejected: the
  dotnet harness (and the reviewer) then has no stable artifact to compile
  against, and `git diff` on generated code — the acceptance signal — disappears.

## Consequences

- A schema edit requires `tools/gen.ps1`; forgetting is caught by
  `tools/verify-generated.ps1` and by the `Generated_sources_carry_a_do_not_edit_banner`
  guard test rather than by a runtime surprise.
- Adding a table means editing `__tables__.csv`, the schema, the CSV, and
  `LubanGameConfig.TableNames` — four places. Accepted: the alternative (dynamically
  discovering tables) would break the "no stub, no reflection-only path"
  requirement and lose compile-time checking of the loader keys.
- Data authors must keep `data/` and `lib/Tests~/data` in step; they never do this
  by hand, because `gen.ps1` writes both.
