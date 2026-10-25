# Dependency Matrix

> Stage 00 (Discovery & Baseline) with the Stage 01, Stage 02, 02b and Stage 03
> re-verifications folded in. Every version below was read from an **authoritative,
> re-checkable source** (a package registry, a GitHub API response, or the installed
> tool on this machine). Anything not yet confirmed is marked `Proposed` or
> `Unverified` — it is **not** claimed as working.
>
> **Stage 03 closed the IL2CPP/Web question for the whole stack**: the WebGL player
> builds, boots, resolves its VContainer graph, loads the Luban tables, round-trips
> MessagePack through its source-generated formatter and Protobuf, and renders Thai
> text with zero missing glyphs — asserted from the page by Playwright on the real
> build. Rows below say so where it applies.

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
| ~~**VContainer**~~ **Verified on Web at Stage 03** | DI / lifetimes | OpenUPM `jp.hadashikick.vcontainer`, resolved to `1.19.0` in `Packages/manifest.json` | `1.19.0` (commit `5401e5a7ebc4980a2b82141ffc26391a6547edd7`) | MIT | `com.unity.test-framework` (test asmdef only) | **Resolved, with a rule** — its reflected-constructor path *is* stripped by IL2CPP (the first Web build failed with `Type does not found injectable constructor, type: SystemClock`). Register with explicit factories, never `Register<TInterface, TImplementation>(Lifetime)`; see `llmwiki/gotchas/il2cpp-di-and-reflection.md` |
| ~~**R3**~~ **Verified on Web at Stage 03** | UI-level reactive state | OpenUPM `com.cysharp.r3`, resolved to `1.3.1` | `1.3.1` | MIT | `Microsoft.Bcl.TimeProvider` (`8.0.0`, NuGet) | **Verified** — `r3.subscribe` probe (subscribe, receive, dispose, no post-dispose delivery) passes in the WebGL player |
| ~~**UniTask**~~ **Verified on Web at Stage 03** | async, cancellation | OpenUPM `com.cysharp.unitask`, resolved to `2.5.11` | `2.5.11` | MIT | none | **Verified** — `unitask.cancellation` probe passes in the WebGL player; no `ThreadPool` use on the paths shipped so far |
| ~~**MessagePack for C#**~~ **Verified on Web at Stage 03** | save/checkpoint DTOs | NuGet via NuGetForUnity: `Google.Protobuf`, `MessagePack`, `MessagePack.Annotations`, `MessagePackAnalyzer` in `UnityProject/Assets/packages.config` | **`3.1.12`** (annotations + analyzer + `Microsoft.NET.StringTools 17.11.4`) | MIT | **source generator** only; the dynamic resolver is not IL2CPP-safe | **Verified** — `messagepack.roundtrip` probe asserts the *generated* formatter exists and round-trips in the WebGL player (26 bytes) |
| ~~**Google.Protobuf**~~ **Verified on Web at Stage 03** | external contracts only | NuGet via NuGetForUnity (`UnityProject/Assets/packages.config`) | **`3.33.6`** (`protoc` 33.6 generates the matching runtime) | BSD-3-Clause | `System.Memory`, `System.Runtime.CompilerServices.Unsafe` | **Verified** — `protobuf.roundtrip` probe round-trips an `AppInfo` in the WebGL player (87 bytes); keep off the hot path |
| **protoc** | `.proto` → C# contract | installed `tools/vendor/protoc/33.6` | **`libprotoc 33.6`** — verified at Stage 02 | BSD-3-Clause | none (CLI) | **Low** — build-time only |
| ~~**Luban**~~ **Verified at Stage 02** | CSV → generated readers + `.bytes` | installed `tools/vendor/luban/5.1.0` (downloaded from `github.com/focus-creative-games/luban/releases` and SHA-256-checked) | **`v5.1.0`** — CLI syntax verified against the real binary; generates all twelve tables | MIT | bundled CLI (needs the .NET 8 **runtime**) | **Low** — offline code-gen; output is plain C#. See `llmwiki/gotchas/luban-5-strict-and-tables.md` |
| **Input System** | WASD / arrows / mouse | resolved in `Packages/manifest.json` | **`1.20.1`** | Unity Companion License | `com.unity.modules.*` | **Low** — the player initialises it (`Input System module state changed to: Initialized`) in the Web build. No gameplay input is wired yet (Stage 04+) |
| **URP** | renderer | resolved in `Packages/manifest.json` | **`17.3.0`** | Unity Companion License | `com.unity.render-pipelines.core`, `com.unity.shadergraph` | **Verified, with a note** — renders in the WebGL player; three hidden/diagnostic URP shaders (`Hidden/CoreSRP/CoreCopy`, `StencilDitherMaskSeed`, `HDRDebugView`) report "not supported on this GPU" on the headless ANGLE path. No visible-pass impact observed; revisit if a real GPU run disagrees |
| **Unity Test Framework** | EditMode/PlayMode | resolved in `Packages/manifest.json` | **`1.6.0`** | Unity Companion License | `com.unity.ext.nunit` | n/a (test-only) |
| **uGUI + TextMeshPro** | player UI | resolved in `Packages/manifest.json` | **`2.0.0`** | Unity Companion License | TMP essentials (font atlas) | **Verified, with two traps** — Thai renders with 0 missing glyphs in the player, but (a) TMP's pre-build processor empties Dynamic assets' tables at build time and (b) `TryAddCharacters`' return value cannot be trusted. See [ADR 0008](../adr/0008-thai-text-ui-stack.md) |
| **Playwright** | browser tests | npm | **`1.64.0`** (`@playwright/test`, `tests/web/package.json`) | Apache-2.0 | none committed; the suite drives the **installed** Chrome (`channel: "chrome"`) rather than downloading a browser | n/a (test-only) |
| **Spine runtime (spine-unity)** | the player figure's skeleton, UI and world actors | committed beside the game at `UnityProject/Assets/Spine/` (`package.json`, asmdef `spine-unity`; not a UPM dependency in `Packages/manifest.json`) | **`3.8.0`** — installed; the shipped skeleton's own data version is `3.8.75` (`SkeletonData.Version`, read by `ProjectSurvive/Spine/1. Dump player skeleton`), same major.minor | **Spine Runtimes License** (`Assets/Spine/LICENSE.txt`): integration allowed, each user needs their own Spine Editor license, redistribution must carry the notice — that notice is in the repository; the **team's Spine Editor license is unverified** | none (the runtime is copied in) | **Unverified on the Web target.** What Stage 17a measured: it compiles for every platform, loads a real 3.8.75 skeleton from `Resources`, and renders in the Editor under URP 17.3 with `Spine/Skeleton`; the export's own atlas material (`Spine/SkeletonGraphic`) draws **nothing** on a `MeshRenderer`. Not measured: a WebGL player, IL2CPP stripping of the spine types, and the payload cost of `Resources/body_1110901/` (a Release-candidate stage must measure it with `tools/measure-web-size.mjs`). See [ADR 0037](../adr/0037-spine-actors-and-the-player-figure.md). |

### 2.3 Blocked / Unverified

| Item | Why blocked | What unblocks it |
| --- | --- | --- |
| **UI.MVP Lite package** | The GDD names `UI.MVP Lite` but **no repository/source has been supplied**. | Project owner provides the repo URL. Until then, ADR 0002 mandates a thin, project-owned MVP. |
| ~~**MessagePack Unity UPM variant version**~~ | **Resolved at Stage 12** — no UPM package is used. The checkpoint's DTOs compile against the same NuGetForUnity distribution as the rest of `lib/`: `MessagePack` / `MessagePack.Annotations` / `MessagePackAnalyzer` **3.1.12** (`UnityProject/Assets/packages.config`), with the source generator producing every formatter (asserted by `SettingsCodec.GeneratedFormatterIsPresent` and the checkpoint round-trip tests). | — |
| ~~**EcsLite exact commit**~~ | **Resolved at Stage 01** — vendored at `d46f3b209ea163810823aeea7d2610d38ad6cdf8` (version string `2026.4.25`), recorded in `lib/third_party/ecslite/VENDOR.md` and `tools/versions.lock.json`, and asserted by a test. | — |
| ~~**Luban CLI syntax**~~ | **Resolved at Stage 02** — the pinned 5.1.0 binary was driven directly (`--conf` / `-t` / `-c` / `-d` / `--strict` / `--errorFormat` / `-x key=value`) and the `__tables__.csv` registry format was read off its own loader; nothing was guessed. Recorded in `llmwiki/gotchas/luban-5-strict-and-tables.md`, asserted by twelve fixtures. | — |
| **Firefox** | Not installed on this machine. | Install Firefox before the Stage 14 cross-browser run, or record Firefox as NOT RUN. |
| ~~**IL2CPP stripping / AOT behaviour** (MessagePack, Protobuf, R3, VContainer)~~ | **Resolved at Stage 03** — all four round-trip in a real WebGL2 player (see the probes above), and the one real failure (VContainer's reflected constructors) is written down with its rule in `llmwiki/gotchas/il2cpp-di-and-reflection.md`. | — |
| **Release-build performance** | Stage 03 built and shipped only a **Development** build; its numbers are not evidence of shipped performance. | A Release build (`WebBuildPipeline.BuildWebRelease`) before any timing claim (ADR 0003). |
| ~~**IndexedDB flush method on Unity 6.3**~~ | **Resolved at Stage 12** — `Application.persistentDataPath` is an Emscripten IDBFS mount; the flush is `FS.syncfs(false, cb)` (`WebGLSupport/BuildTools/prejs/IdbFs.js` in 6000.3.9f1). The callback does **not** resume a Unity `Task` continuation, so the page records the result and the player polls it once per frame ([ADR 0033](../adr/0033-application-awaits-capture-the-caller-context.md)). | — |
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

1. ~~Exact versions of `com.unity.inputsystem`, `com.unity.render-pipelines.universal`,
   and `com.unity.test-framework` resolved by Unity `6000.3.9f1`~~ — **resolved at
   Stage 03**: `1.20.1`, `17.3.0`, `1.6.0` (see §2.2 and `UnityProject/SETUP.md`).
2. ~~Whether R3 / VContainer / MessagePack survive IL2CPP + Web stripping~~ —
   **resolved at Stage 03** (see the probe results above and the two gotcha notes).
3. The `UI.MVP Lite` package identity (project owner) — still open; ADR 0002
   mandates a project-owned thin MVP until it is supplied.
4. ~~MessagePack's Unity UPM package name/version (Stage 12 — no save DTO exists yet)~~ —
   **resolved at Stage 12**: no UPM package; NuGetForUnity 3.1.12 is what both the harness
   and the Unity project compile the DTOs against.
5. Whether `Xoshiro128` / `FixedTicker` behaviour is identical under WebGL's
   32-bit wasm heap and an x64 desktop CPU. Stage 03 proved the *shell* runs on
   WebGL2 and that the config hash matches between Editor and player, but no
   battle-tick hash comparison has been run: **still open**, and it is the one that
   matters for replay determinism (Stage 07).

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
