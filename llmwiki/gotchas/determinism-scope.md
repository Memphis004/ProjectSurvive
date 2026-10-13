# Gotcha — Determinism scope

AGENTS.md rule 6 promises determinism **within one runtime only**, not across
browsers or CPUs. This note records what that does and does not cover, and what
Stage 01 actually put in place to keep it honest.

## What is guaranteed

- The same run seed produces the same sequence in the same build, on the same
  machine, in the same runtime (`Xoshiro128`, `RandomStreams`, `FixedTicker`).
- Shop, battle and visual randomness are **separate streams**. A stream's seed is
  derived only from `(masterSeed, kind)`, so no amount of shop rolling can shift
  what battle rolls next — verified by `RandomStreamsTests`.
- Conversion from frame deltas to a fixed 30 Hz tick count is pure and
  allocation-free: the same delta sequence yields the same tick sequence.

## What is not guaranteed

- **Across browsers or CPUs.** WebGL's 32-bit wasm heap vs an x64 desktop CPU:
  `Xoshiro128` deliberately uses only 32-bit operations and `FixedTicker` mirrors
  the standard C# `double` semantics, but "same code path" is a design argument,
  not a measurement. It is untested until the Stage 03 Web build.
- **Anything read from a real clock.** `SystemClock` is monotonic but not
  reproducible; never feed it into rules. Time that affects the simulation must
  arrive as tick counts.

## Rules that keep it true

1. No `UnityEngine.Random`, no `System.Random`, no `DateTime.Now`/`UtcNow`, no
   `Stopwatch` in `Shared`/`Domain`/`Application`/`Simulation`. The first four
   are asserted by `ArchitectureTests.Rule_engine_sources_avoid_forbidden_time_and_random_apis`;
   `Stopwatch` lives only in `Infrastructure.SystemClock`.
2. Never add randomness outside `IRandomSource`.
3. Never let the *number* of draws on one stream depend on another stream.

## Recording and replaying

- `RandomStreams` is rebuilt from the run seed, so a replay needs the seed plus
  the input history — not the generator states.
- **Known gap:** `Xoshiro128` exposes `Seed` but not its four state words yet, so
  it cannot be restored mid-sequence. A run checkpoint that saves after an
  arbitrary number of draws needs state export/import; that is Stage 12 work.
- A recorded replay must store **tick counts**, not frame deltas: `FixedTicker`
  deliberately discards a surplus backlog when a frame runs past
  `maxTicksPerAdvance` (counted in `DroppedTicks`), so a long stall does not
  replay identically from raw frame times.

## How it is verified

- **Golden vectors** in `Xoshiro128Tests` / `RandomStreamsTests`, produced by an
  independent reference implementation of xoshiro128++ (`docs/stages/01-report.md`
  records how) — these lock the algorithm against accidental change.
- **Process-level fingerprints**: `ProjectSurvive.Sim --seed N --quiet` prints one
  `determinismDigest`, and `tools/test.ps1` checks that the same seed repeats it
  and a different seed does not.
