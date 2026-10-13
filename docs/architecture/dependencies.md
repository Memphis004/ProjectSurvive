# Dependency Matrix

> Stage 00 (Discovery & Baseline) with the Stage 01 and Stage 02 re-verifications
> folded in. Every version below was read from an **authoritative, re-checkable
> source** (a package registry, a GitHub API response, or the installed tool on
> this machine). Anything not yet confirmed is marked `Proposed` or `Unverified` —
> it is **not** claimed as working.

## Status legend

| Status | Meaning |
| --- | --- |
| **Verified** | Confirmed on this machine or from an authoritative registry during Stage 00. The exact string is recorded. |
| **Proposed** | A specific version/decision is chosen and justified, but it has **not** been installed and tested with this project. Must be confirmed in the stage that first uses it. |
| **Blocked** | Cannot proceed until a missing piece (repo access, license, hardware, or an unresolved compatibility question) is provided. |
| **Unverified** | Known to be needed, but no version/source is confirmed yet. |

**IL2CPP/Web risk** refers specifically to the Web (WebGL2) target, which is
AOT + IL2CPP and strips code. A `High` rating means "must be proven in a real
Web build", not "known broken".

## 1. Toolchain environment (Verified on this machine)

Measured by `tools/doctor.ps1` on 2026-10-13.

| Tool | Verified string | Source of truth |
| --- | --- | --- |
| OS | Windows 11 (`10.0.26300`), x64 | `Get-CimInstance Win32_OperatingSystem` |
| Shell for scripts | Git Bash (MINGW64) + Windows PowerShell `5.1.26100.9444` + PowerShell `7` (preview) | `$PSVersionTable` |
| Git | `2.55.0.windows.3` | `git --version` |
| Git LFS | see `tools/doctor.ps1` (`git lfs version`) | `git lfs version` |
| .NET SDK | `6.0.428`, `9.0.311` | `dotnet --list-sdks` |
| .NET runtime | `Microsoft.NETCore.App` up to `9.0.13` | `dotnet --list-runtimes` |
| Node.js | `v25.6.1` | `node --version` |
| npm | `11.9.0` | `npm --version` |
| Unity Editor | **`6000.3.9f1`** (revision `7a9955a4f2fa`), branch `6000.3/staging` | `Unity.exe` version resource + batchmode log |
| Unity license | **Unity Personal** — batchmode `-batchmode -nographics -quit` → exit `0` | `tools/doctor.ps1` |
| Web Build Support | **present** — `Editor/Data/PlaybackEngines/WebGLSupport` (Emscripten, Brotli, WebGLPlayerBuildProgram) | filesystem |
| Browser (Chromium) | Google Chrome `154.0.8037.98` (user install) | `VersionInfo.ProductVersion` |
| Browser (Chromium, alt) | Microsoft Edge `145.0.3800.70` | `VersionInfo.ProductVersion` |
| Browser (Gecko) | **not installed** | filesystem |
| Playwright | **not installed** (expected — added at Stage 03/14) | `npx playwright --version` |
| Luban CLI | **`5.1.0`** (`df34215d2e22035073d1af83d44e3ac0744dc3b3`), installed at `tools/vendor/luban/5.1.0` by `tools/setup-tools.ps1` | `Luban.exe --version` |
| protoc CLI | **`libprotoc 33.6`**, installed at `tools/vendor/protoc/33.6` by `tools/setup-tools.ps1` | `protoc.exe --version` |
| 7-Zip | needed once, to unpack the Luban `.7z` release archive | `tools/setup-tools.ps1` |
| CPU | Intel Core i5-14400F (10C / 16T) | `Win32_Processor` |
| RAM | 31.8 GiB (34,186,145,792 bytes) | `Win32_ComputerSystem` |
| GPU | NVIDIA GeForce RTX 5060 Ti, driver `32.0.16.1692` | `Win32_VideoController` |

## 2. Package matrix

`Source` is the URL/command that can be re-run to confirm the row.

### 2.1 Verified available

| Package | Role | Source | Version | License | Transitive deps | IL2CPP/Web risk |
| --- | --- | --- | --- | --- | --- | --- |
| Unity Editor | Engine / build | installed `…/Hub/Editor/6000.3.9f1` | `6000.3.9f1` | Unity proprietary (Personal) | — | Low (it *is* the target) |
| Web Build Support | WebGL2 export | `Editor/Data/PlaybackEngines/WebGLSupport` | ships with `6000.3.9f1` | Unity Companion License | Emscripten, Brotli | Low |
| .NET SDK | Core tests + harness | `dotnet --list-sdks` | `9.0.311` (also `6.0.428`); libraries target `netstandard2.1` + `LangVersion 9.0` to mirror Unity 6.3 | MIT (.NET Foundation) | — | n/a (build-time) |
| Node.js | tooling + Playwright | `node --version` | `v25.6.1` | MIT | bundled npm | n/a (build-time) |
| xUnit | Domain/Sim unit tests | `api.nuget.org/v3-flatcontainer/xunit/index.json` | **`2.9.3`** — installed and passing at Stage 01 | Apache-2.0 (`github.com/xunit/xunit/license`) | `xunit.core`, `xunit.assert`, `xunit.analyzers` | n/a (not shipped) |
| Microsoft.NET.Test.Sdk / xunit.runner.visualstudio | `dotnet test` host + xUnit runner | versions taken from the installed `dotnet 9.0.311` `dotnet new xunit` template | **`17.12.0`** / **`2.8.2`** | MIT / Apache-2.0 | test host only | n/a (test-only) |
| **EcsLite** (vendored) | Battle/ECS simulation | `github.com/Leopotam/ecslite`, copied into `lib/third_party/ecslite` | commit **`d46f3b209ea163810823aeea7d2610d38ad6cdf8`** (version string `2026.4.25`, 2026-04-25) | **MIT-Red** — MIT plus extra restrictions; see `lib/third_party/ecslite/LICENSE.md` | none (single-file-set library) | **Low** — pure C#, no reflection in the core; the only Unity usage is `#if`-guarded IL2CPP attributes |
| Git LFS | binary asset storage | `git lfs version` | see doctor output | MIT | — | n/a |

### 2.2 Proposed (chosen but not yet installed / not yet proven on Web)

| Package | Role | Source (re-verifiable) | Version | License | Transitive deps | IL2CPP/Web risk |
| --- | --- | --- | --- | --- | --- | --- |
| **VContainer** | DI / lifetimes | OpenUPM `jp.hadashikick.vcontainer` | `1.19.0` (commit `5401e5a7ebc4980a2b82141ffc26391a6547edd7`) | MIT | `com.unity.test-framework` (test asmdef only) | **Medium** — uses reflection for some registration paths; verify stripping |
| **R3** | UI-level reactive state | OpenUPM `com.cysharp.r3` | `1.3.1` | MIT | `Microsoft.Bcl.TimeProvider` (netstandard) | **Medium** — async/observable; confirm AOT |
| **UniTask** | async, cancellation | OpenUPM `com.cysharp.unitask` | `2.5.11` | MIT | none | **Medium** — WebGL single-thread: `UniTask` must not use `ThreadPool`; verify |
| **MessagePack for C#** | save/checkpoint DTOs | `api.nuget.org/v3-flatcontainer/messagepack/index.json` / Unity UPM variant | NuGet latest **`3.1.12`**; UPM variant **TBD** | MIT | `MessagePack.Annotations`, `Microsoft.NET.StringTools`; **source generator** `MessagePack.SourceGenerator` | **High** — must use source generator (no dynamic resolver); verify AOT |
| **Google.Protobuf** | external contracts only | `api.nuget.org/v3-flatcontainer/google.protobuf/index.json` | **`3.33.6`** — installed and compiling at Stage 02 (`protoc` 33.6 generates the matching runtime) | BSD-3-Clause | `System.Memory`, `System.Runtime.CompilerServices.Unsafe` | **Medium** — external contract uses only; keep off the hot path |
| **protoc** | `.proto` → C# contract | installed `tools/vendor/protoc/33.6` | **`libprotoc 33.6`** — verified at Stage 02 | BSD-3-Clause | none (CLI) | **Low** — build-time only |
| ~~**Luban**~~ **Verified at Stage 02** | CSV → generated readers + `.bytes` | installed `tools/vendor/luban/5.1.0` (downloaded from `github.com/focus-creative-games/luban/releases` and SHA-256-checked) | **`v5.1.0`** — CLI syntax verified against the real binary; generates all twelve tables | MIT | bundled CLI (needs the .NET 8 **runtime**) | **Low** — offline code-gen; output is plain C#. See `llmwiki/gotchas/luban-5-strict-and-tables.md` |
| **Input System** | WASD / arrows / mouse | `packages.unity.com/com.unity.inputsystem` | template min **`1.12.0`** (registry has `1.20.1`) — **resolve with 6000.3 at Stage 03** | Unity Companion License | `com.unity.modules.*` | **Medium** — WebGL input backend |
| **URP** | renderer | `packages.unity.com/com.unity.render-pipelines.universal` | template min **`17.0.1`** — **resolve with 6000.3 at Stage 03** | Unity Companion License | `com.unity.render-pipelines.core`, `com.unity.shadergraph` | **Medium** — WebGL2 feature subset |
| **Unity Test Framework** | EditMode/PlayMode | `packages.unity.com/com.unity.test-framework` | template min **`1.4.2`** (registry has `1.4.6`) — **resolve with 6000.3 at Stage 03** | Unity Companion License | `com.unity.ext.nunit` | n/a (test-only) |
| **uGUI + TextMeshPro** | UI (Stage 03 spike) | Unity package `com.unity.ugui` | ships with 6000.3 | Unity Companion License | TMP essentials (font atlas) | **Medium** — Thai glyph/shaping issues; Stage 03 spike |
| **Playwright** | browser tests | `npm` (`@playwright/test`) | **pin at Stage 03** | Apache-2.0 | browser drivers (~large, cached locally, not committed) | n/a (test-only) |

### 2.3 Blocked / Unverified

| Item | Why blocked | What unblocks it |
| --- | --- | --- |
| **UI.MVP Lite package** | The GDD names `UI.MVP Lite` but **no repository/source has been supplied**. | Project owner provides the repo URL. Until then, ADR 0002 mandates a thin, project-owned MVP. |
| **MessagePack Unity UPM variant version** | NuGet version is confirmed; the Unity-facing distribution (`com.cysharp.messagepack` / `MessagePack.Unity`) version is not. **Still open after Stage 01**, which introduced no save DTOs. | Confirm the Unity package name + version when the first checkpoint DTO lands (Stage 12). |
| ~~**EcsLite exact commit**~~ | **Resolved at Stage 01** — vendored at `d46f3b209ea163810823aeea7d2610d38ad6cdf8` (version string `2026.4.25`), recorded in `lib/third_party/ecslite/VENDOR.md` and `tools/versions.lock.json`, and asserted by a test. | — |
| ~~**Luban CLI syntax**~~ | **Resolved at Stage 02** — the pinned 5.1.0 binary was driven directly (`--conf` / `-t` / `-c` / `-d` / `--strict` / `--errorFormat` / `-x key=value`) and the `__tables__.csv` registry format was read off its own loader; nothing was guessed. Recorded in `llmwiki/gotchas/luban-5-strict-and-tables.md`, asserted by twelve fixtures. | — |
| **Firefox** | Not installed on this machine. | Install Firefox before the Stage 14 cross-browser run, or record Firefox as NOT RUN. |
| **IL2CPP stripping / AOT behaviour** (MessagePack, Protobuf, R3, VContainer) | Cannot be proven without a real Web build. | Stage 03 first Web build. **Explicitly out of scope for Stage 00.** |
| **IndexedDB flush method on Unity 6.3** | Not yet checked against 6.3 docs. | Stage 12. |
| **Performance numbers** | Never measured. | Stage 03+ with a real browser build. |

## 3. Transitive dependency notes

- **EcsLite** is a single-file-set library with no external dependencies — that
  is why it is vendored (source in-tree, `lib/third_party/ecslite`) rather than
  referenced from a registry. It is **MIT-Red**, not plain MIT: the grant adds
  restrictions on military use and on use by parties hostile to Russians. The
  license text is vendored verbatim and the pinned revision is recorded in
  `VENDOR.md`; an upgrade needs owner approval (AGENTS.md rule 2).
- **EcsLite and Unity references:** upstream's `.asmdef` sets
  `noEngineReferences: false` and is kept verbatim, because the `#if ENABLE_IL2CPP`
  blocks use `Unity.IL2CPP.CompilerServices`. Outside Unity those blocks are
  excluded, so the harness compiles it as pure BCL — verified by a guard test
  rather than assumed.
- **VContainer / R3 / UniTask / MessagePack** are pulled through OpenUPM
  (`https://package.openupm.com`) so that scoped registries pin them rather than
  resolving from a moving Git URL.
- **MessagePack** must be used **only** through its **source generator**; the
  dynamic resolver is not supported by IL2CPP (GDD + AGENTS rule 3).
- **Google.Protobuf** is limited to external contracts (`proto/app.proto`),
  never to config or the in-memory save path.

## 4. Open questions carried into later stages

1. Exact versions of `com.unity.inputsystem`, `com.unity.render-pipelines.universal`,
   and `com.unity.test-framework` resolved by Unity `6000.3.9f1` at project
   creation (Stage 03).
2. Whether R3 / VContainer / MessagePack survive IL2CPP + Web stripping
   (Stage 03).
3. The `UI.MVP Lite` package identity (project owner).
4. MessagePack's Unity UPM package name/version (Stage 12 — no save DTO exists yet).
5. Whether `Xoshiro128` / `FixedTicker` behaviour is identical under WebGL's
   32-bit wasm heap and an x64 desktop CPU (Stage 03 first Web build). The
   generator deliberately uses only 32-bit operations, but that is a design
   argument, not a measurement.

## 5. Re-verification

```powershell
# Full environment + dependency re-check with a reliable exit code
powershell -NoProfile -ExecutionPolicy Bypass -File tools/doctor.ps1

# Pinned CLI toolchain: download (by SHA-256) and probe both binaries
powershell -NoProfile -ExecutionPolicy Bypass -File tools/setup-tools.ps1

# The whole data pipeline, including "a fresh generation matches what is committed"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1
```

`tools/versions.lock.json` stores the pinned strings this document references.
Its `vendoredTools` array holds the URL, SHA-256 and probe command for each
archive that `tools/setup-tools.ps1` installs into `tools/vendor/` (never
committed).
