# ADR 0035 — What the release candidate is, and what its numbers cover

- Status: accepted (Stage 15)
- Date: 2026-10-23
- Supersedes: nothing. Extends [ADR 0011](0011-webgl-payload-budget-and-size-measurement.md)
  and [ADR 0003](0003-performance-baseline-machine-and-browser.md).

## Context

Stage 15 has to produce a release candidate (RC) that a browser opens, measure it
against the GDD's budget, and record the numbers honestly. Four things were open:

1. **Which configuration ships.** The project's authored WebGL settings are managed
   stripping **Low** / IL2CPP **OptimizeSpeed**; Stage 03b's measured recommendation
   is the `optimize-size` profile (High / OptimizeSize). The profile persists itself
   into `ProjectSettings.asset` (`WebSizeProfiles.ApplyAndPersist`), and a later
   build that applies no profile inherits whatever the last profile left — that
   residue once shipped a player that halted before its first frame
   ([gotcha](../../llmwiki/gotchas/webgl-size-profile-settings-residue.md)).
2. **Where compression lives.** Unity's `WebGLCompressionFormat` can also emit
   `.br`/`.gz` siblings, or the host can compress. The payload's compressed size is
   the same either way (ADR 0011 §2), but the two choices differ in how many
   artifacts exist and where the encoding decision is made.
3. **How caching behaves across a redeploy.** The loader requests stable names
   (`webGLNameFilesAsHashes: 0`), so an `immutable` cache policy would pin players to
   an old build.
4. **How the stress case is measured.** The GDD names ~300 enemies + 500 projectiles.
   Normal floors spawn at most ~27. A release player has no harness and no dev menu
   (Stage 14 item 7, asserted by `release.spec.ts`), so there is no way to inject
   that load into the artifact that ships.

## Decision

**1. The RC is one command: `tools/build-web.ps1 -ReleaseCandidate -Force`.**

It applies the `optimize-size` profile, builds Release, restores
`ProjectSettings.asset` and `GraphicsSettings.asset` afterwards, and fails if
`build-report.json` does not report the settings it was asked for. Consequences:

- the artifact records its configuration (`sizeProfile`, `sizeSettings`);
- the working tree carries the authored configuration, so the next ordinary build
  cannot inherit a profile nobody selected;
- a plain `-Configuration Release` build is **not** the RC. The RC is reproducible
  from the switch, not from whatever the Editor last saved.

**2. Compression is produced from the raw artifact and delivered by the host.**

`tools/compress-web.mjs` writes `.br` (q11) and `.gz` (level 9) siblings plus
`compression-report.json`; `tools/serve-web.mjs` and a static host negotiate them
from `Accept-Encoding`, with `Vary: Accept-Encoding`. Unity's compression setting
stays Disabled. One artifact per build, one place where the encoding is decided,
and the three bases ADR 0011 requires are measured from the bytes that are actually
delivered.

**3. Player files are `no-cache` + `ETag`, not `immutable`.**

The names do not change between builds, so revalidation is the only policy that
cannot serve a stale player. `index.html` and `/Build/*` revalidate; `TemplateData/*`
is cacheable for an hour; the build diagnostics are `no-store`. The policy is
checked over HTTP, including a redeploy under a live browser, by
`tests/web/scripts/verify-hosting.mjs`.

**4. `Assets/link.xml` preserves the reflection-dependent assemblies.**

`Luban.Runtime`, `MessagePack`, `MessagePack.Annotations`, `Google.Protobuf`,
`VContainer` and `R3` are preserved with the reason for each written next to it.
The list is a guard against stripping, not a substitute for evidence: the proof is
the **Release** browser suite (boot's seven probes include the MessagePack and
Protobuf round trips and DI resolution, and the persistence spec saves and reloads a
real run, which deserialises a checkpoint). `ReleaseLinkXmlTests` keeps the file and
the assembly names from being deleted quietly.

**5. Browser numbers cover the normal load; the stress case is a headless
measurement and is reported as a separate basis.**

- Browser (Release, Chrome, 1920×1080): load time, frame time distribution, JS and
  Wasm heap, entity count per scenario, over the title, the camp and battle floor 1.
- Stress (~300 ghosts + ~500 projectiles): the `sim` benchmark
  (`dotnet run --project lib/Tests~/Sim -- benchmark`), which reports per-tick time,
  allocation per tick and entity count for that load. It is a .NET/IL2CPP-independent
  measurement of the simulation, **not** a browser frame time.
- A browser stress number is **NOT RUN**. It would require either a release player
  with a way to inject entities (a harness the release gate forbids) or a
  Development-build measurement (which ADR 0003/A 0011 forbid quoting as release
  performance). It is recorded as an open item, not estimated.

## Alternatives considered

- **Commit the `optimize-size` settings to `ProjectSettings.asset`.** It would make a
  plain Release build the RC, but it would redefine what the `baseline` profile means
  and break the Stage 03b contract (`WebSizeProfilesTests` asserts the authored pair
  is the project's own), so a profile would stop being "a value you pass" and become
  "a value the project already had". Rejected for this stage; a later stage that
  wants High stripping as the project default should change the profile contract
  explicitly.
- **Pre-compress inside Unity.** Adds a second artifact per encoding, makes the
  loader's file names depend on a setting, and moves the cache/encoding decision into
  the engine. Rejected.
- **Cache player files as `immutable` with a query-string version.** The loader's
  requests are generated by Unity, so the version would have to be injected into
  `index.html`; revalidation is simpler and measured to work.
- **Ship a `?stress=300` parameter.** Rejected: it is a harness by another name, and
  Stage 14's release gate asserts the release player has none.
- **Measure stress in a Development build and label it "browser".** Rejected: a
  Development player is unoptimised with debug symbols; its frame time is not a
  property of the shipped artifact.

## Consequences

- A size or performance claim must name its basis: `optimize-size` Release artifact,
  gzip/Brotli/raw, scenario, browser version and machine.
- The RC command leaves the tree clean, and a mismatched build fails loudly instead
  of being reported as the requested profile.
- The stress case is measured where the project can measure it, and the gap is
  visible in the performance report and the known-issues list rather than papered
  over with an inference.
- If a later stage wants browser stress, the honest routes are a Development-only
  stress scene (labelled not-quotable) or accepting a test-only harness in a
  separately-labelled "test" artifact. Neither is done here.
