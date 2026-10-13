# Dependency Matrix

> Stage 00 (Discovery & Baseline). Every version below was read from an
> **authoritative, re-checkable source** (a package registry, a GitHub API
> response, or the installed tool on this machine). Anything not yet confirmed
> is marked `Proposed` or `Unverified` — it is **not** claimed as working.

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
| .NET SDK | Core tests + harness | `dotnet --list-sdks` | `9.0.311` (also `6.0.428`) | MIT (.NET Foundation) | — | n/a (build-time) |
| Node.js | tooling + Playwright | `node --version` | `v25.6.1` | MIT | bundled npm | n/a (build-time) |
| xUnit | Domain/Sim unit tests | `api.nuget.org/v3-flatcontainer/xunit/index.json` | latest stable **`2.9.3`** | Apache-2.0 (`github.com/xunit/xunit/license`) | `xunit.core`, `xunit.assert`, `xunit.analyzers` | n/a (not shipped) |
| Git LFS | binary asset storage | `git lfs version` | see doctor output | MIT | — | n/a |

### 2.2 Proposed (chosen but not yet installed / not yet proven on Web)

| Package | Role | Source (re-verifiable) | Version | License | Transitive deps | IL2CPP/Web risk |
| --- | --- | --- | --- | --- | --- | --- |
| **EcsLite** | Battle/ECS simulation | `github.com/Leopotam/ecslite` (vendored to `lib/third_party/ecslite`) | repo `master` — **pin exact commit in Stage 01** | MIT (MIT-Red variant) | none (single-file library) | **Low** — pure C#, no reflection |
| **VContainer** | DI / lifetimes | OpenUPM `jp.hadashikick.vcontainer` | `1.19.0` (commit `5401e5a7ebc4980a2b82141ffc26391a6547edd7`) | MIT | `com.unity.test-framework` (test asmdef only) | **Medium** — uses reflection for some registration paths; verify stripping |
| **R3** | UI-level reactive state | OpenUPM `com.cysharp.r3` | `1.3.1` | MIT | `Microsoft.Bcl.TimeProvider` (netstandard) | **Medium** — async/observable; confirm AOT |
| **UniTask** | async, cancellation | OpenUPM `com.cysharp.unitask` | `2.5.11` | MIT | none | **Medium** — WebGL single-thread: `UniTask` must not use `ThreadPool`; verify |
| **MessagePack for C#** | save/checkpoint DTOs | `api.nuget.org/v3-flatcontainer/messagepack/index.json` / Unity UPM variant | NuGet latest **`3.1.12`**; UPM variant **TBD** | MIT | `MessagePack.Annotations`, `Microsoft.NET.StringTools`; **source generator** `MessagePack.SourceGenerator` | **High** — must use source generator (no dynamic resolver); verify AOT |
| **Google.Protobuf** | external contracts only | `api.nuget.org/v3-flatcontainer/google.protobuf/index.json` | latest stable observed **`3.33.6`** (`3.34.0-rc1` in flight) | BSD-3-Clause | `System.Memory`, `System.Runtime.CompilerServices.Unsafe` | **Medium** — external contract uses only; keep off the hot path |
| **Luban** | CSV → generated readers + `.bytes` | `github.com/focus-creative-games/luban/releases` | latest **`v5.1.0`** | MIT | bundled CLI (needs .NET) | **Low** — offline code-gen; output is plain C# |
| **Input System** | WASD / arrows / mouse | `packages.unity.com/com.unity.inputsystem` | template min **`1.12.0`** (registry has `1.20.1`) — **resolve with 6000.3 at Stage 03** | Unity Companion License | `com.unity.modules.*` | **Medium** — WebGL input backend |
| **URP** | renderer | `packages.unity.com/com.unity.render-pipelines.universal` | template min **`17.0.1`** — **resolve with 6000.3 at Stage 03** | Unity Companion License | `com.unity.render-pipelines.core`, `com.unity.shadergraph` | **Medium** — WebGL2 feature subset |
| **Unity Test Framework** | EditMode/PlayMode | `packages.unity.com/com.unity.test-framework` | template min **`1.4.2`** (registry has `1.4.6`) — **resolve with 6000.3 at Stage 03** | Unity Companion License | `com.unity.ext.nunit` | n/a (test-only) |
| **uGUI + TextMeshPro** | UI (Stage 03 spike) | Unity package `com.unity.ugui` | ships with 6000.3 | Unity Companion License | TMP essentials (font atlas) | **Medium** — Thai glyph/shaping issues; Stage 03 spike |
| **Playwright** | browser tests | `npm` (`@playwright/test`) | **pin at Stage 03** | Apache-2.0 | browser drivers (~large, cached locally, not committed) | n/a (test-only) |

### 2.3 Blocked / Unverified

| Item | Why blocked | What unblocks it |
| --- | --- | --- |
| **UI.MVP Lite package** | The GDD names `UI.MVP Lite` but **no repository/source has been supplied**. | Project owner provides the repo URL. Until then, ADR 0002 mandates a thin, project-owned MVP. |
| **MessagePack Unity UPM variant version** | NuGet version is confirmed; the Unity-facing distribution (`com.cysharp.messagepack` / `MessagePack.Unity`) version is not. | Confirm the Unity package name + version at Stage 01 when save DTOs are introduced. |
| **EcsLite exact commit** | Repo confirmed to exist; a specific revision is not yet pinned. | Pin the commit in `lib/third_party/ecslite/README` + `tools/versions.lock.json` at Stage 01. |
| **Firefox** | Not installed on this machine. | Install Firefox before the Stage 14 cross-browser run, or record Firefox as NOT RUN. |
| **IL2CPP stripping / AOT behaviour** (MessagePack, Protobuf, R3, VContainer) | Cannot be proven without a real Web build. | Stage 03 first Web build. **Explicitly out of scope for Stage 00.** |
| **IndexedDB flush method on Unity 6.3** | Not yet checked against 6.3 docs. | Stage 12. |
| **Performance numbers** | Never measured. | Stage 03+ with a real browser build. |

## 3. Transitive dependency notes

- **EcsLite** is a single-file library with no external dependencies — that is
  why it is vendored (source in-tree) rather than referenced from a registry.
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
4. MessagePack's Unity UPM package name/version (Stage 01).

## 5. Re-verification

```powershell
# Full environment + dependency re-check with a reliable exit code
pwsh -NoProfile -File tools/doctor.ps1
# or
powershell -NoProfile -ExecutionPolicy Bypass -File tools/doctor.ps1
```

`tools/versions.lock.json` stores the pinned strings this document references.
