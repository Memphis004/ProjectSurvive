# ADR 0011 — WebGL payload budget and how build size is measured

- Status: accepted (Stage 03b)
- Date: 2026-10-14
- Supersedes: nothing

## Context

The stage plan sets the WebGL size gate as `initial < 30 MiB` with a production target of
`usable < 15 MiB`, and asks for "the size of `.wasm` `.data` `.framework.js` `.loader.js`,
uncompressed and compressed". It does not say which bytes a budget is measured on, and the
answer changes the verdict: the same Stage 03b build is **44.29 MiB** uncompressed,
**15.53 MiB** as gzip and **10.87 MiB** as Brotli.

Two things also have to be pinned before a number means anything:

1. **Which files are in scope.** A WebGL player is a loader that fetches several files. A figure
   that includes editor-only or template files, or that omits `.data`, is not comparable.
2. **Where compression is measured.** `PlayerSettings.WebGL.compressionFormat` only decides
   whether Unity *also emits* pre-compressed siblings (`Web.wasm.br`) next to the player files.
   The compressed size of the payload is a property of the bytes, not of that checkbox.

## Decision

**1. The measured unit is the initial payload**, defined as exactly the files the loader
requests before the game runs, in load order:

| File | Role |
| --- | --- |
| `index.html` | page and canvas |
| `Build/<player>.loader.js` | bootstrap, decides the encoding to request |
| `Build/<player>.framework.js` | emscripten runtime |
| `Build/<player>.wasm` | IL2CPP-compiled game code |
| `Build/<player>.data` | packed assets and managed metadata |

`TemplateData/*` (template logos, progress-bar art, `style.css`) is deliberately **out**: it is
decoration around the canvas, not something the game needs to start, and it is not part of any
engine version's player payload. Anything a later stage lazy-loads is out by construction.

**2. Every payload is reported in three bases, and a verdict names its basis.**

| Basis | What it is |
| --- | --- |
| uncompressed | bytes on disk — what the build produces, and what CI stores |
| gzip -9 | the conservative, universally supported transfer encoding |
| Brotli q11, lgwin 22 | what a modern static host negotiates for `.wasm` |

Compression is measured **locally, from the raw files** by `tools/measure-web-size.mjs`, not by
toggling `WebGLCompressionFormat`: the setting does not change the payload's compressed size, and
measuring it here keeps one artifact per configuration instead of one per encoding.

**3. The budget is a budget on the transferred payload.** Raw-uncompressed is reported for
comparison but is **not** a passing basis, because a server that ships a WebGL player
uncompressed is misconfigured rather than "large": the bytes on the wire are compressed in every
supported deployment.

* **Gate** — initial payload, gzip -9, `< 30 MiB`.
* **Target** — initial payload, Brotli q11, `< 15 MiB`.

Brotli is the headline because it is what the target encoding costs, and gzip is carried as the
gate because it is the floor a deployment can always reach.

**4. Build size is measured on Release builds only** (ADR 0003). Development builds keep managed
stack traces, assertions and unoptimised output, which is why the same code is 112 MiB
uncompressed in Development and 60.46 MiB raw (18.04 MiB gzip) in the project's default Release
configuration.

**4a. A size figure is only a figure if the artifact records the settings it was built with.** The
"before" player of this stage is the proof: `Builds/Web-before` (48.81 MiB raw) was built before
this pipeline existed, carries no report, and is 12.2 MiB of `.wasm` smaller than the recorded
default configuration with a matching `.data` — a difference nothing on disk can explain. It is
kept as context and excluded from the measured table. `WebBuildPipeline` now writes
`build-report.json` (requested profile, effective settings, packed assets) next to every player it
builds, and `WebSizeProfiles.DumpSettings` reads the project's own defaults so "the configuration
before any profile" is a measurement rather than a memory.

**5. Per-assembly cost is measured, not inferred, and package removal is not attempted.** The
Unity Build Report ranks packed *assets* and says nothing about which managed assembly's code is
in the `.wasm`; `tools/measure-il2cpp-size.mjs` reads IL2CPP's own per-assembly generated C++
and the post-strip managed assemblies instead. A literal "remove one package and rebuild"
ablation is rejected for this stage because every candidate package is load-bearing in
production code (`MessagePack` in `ProjectSurvive.Core.Contracts`, `Google.Protobuf` in
`ProjectSurvive.Core.Generated.Proto`, `R3`/`VContainer`/`UniTask` throughout
`ProjectSurvive.Game.Presentation`), so the ablation would require deleting or stubbing shipping
code — a change no stage may make to obtain a measurement. See
`docs/build/size-baseline.md` for what was measured instead, and what that cannot answer.

## Consequences

- A size claim in any report must name its basis. "The build is 11 MiB" is not a fact; "the
  initial payload is 10.87 MiB as Brotli q11" is.
- The stage gate can pass while the production target fails (gzip). Reports must show both.
- Shipping requires a host that compresses. If the deployment cannot serve Brotli or gzip, the
  gate fails regardless of how the player was built, and that is a deployment fix, not an engine
  setting.
- `.data` is inside the budget, so assets are a size lever with the same standing as code.
  Which is why Stage 03b ranks assets as well as assemblies.
- Measuring per-assembly cost from generated C++ is a proxy for `.wasm` bytes: it ranks
  assemblies against each other but does not apportion the final module. Anything that needs
  exact apportionment (e.g. deciding to drop a package) needs a link-map measurement, which
  this stage does not do.
