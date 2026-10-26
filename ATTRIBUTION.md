# Attribution and asset licences

Every asset in this repository is either **made for this project**, **published under a licence that
allows redistribution**, or **vendored source whose licence text is in the repository**. Nothing is
copied from a game this project references (the survivor-like reference is a genre guide, not an art
source), and no spell, ritual or yant name claims to be a real practice — the names are fantasy written
for this project.

This file is the record Stage 13's audit produced (AGENTS rule 13). A new asset is added here in the
same commit that adds it; an entry with a **bold** risk note is something a release candidate must
resolve first.

## Player-facing assets

| Asset | Where | Licence | Source / attribution |
| --- | --- | --- | --- |
| Sarabun Regular / Bold (`.ttf`) and the baked `Sarabun-Regular SDF` font asset | `UnityProject/Assets/Main/Fonts/` | **SIL Open Font License 1.1** | Sarabun Project Authors, Cadson Demak — <https://github.com/cadsondemak/Sarabun>. The OFL text ships beside the fonts in `Assets/Main/Fonts/OFL.txt`; the baked atlas is a derivative of the same font and carries the same licence. In use by every canvas (ADR 0008, Stage 09d). |
| Character, ghost, prop and HUD art | *(none on disk)* | Project-owned (CC0 by the project) | Drawn procedurally at runtime from primitives: `Presentation/Battle/BattlePlaceholderArt.cs`, `Presentation/Camp/CampPlaceholderArt.cs`, `Presentation/Battle/Hud/BattleHudArt.cs`, plus Unity primitives (`Cube`, `Quad`, `Capsule`) and URP's default material. There are no third-party sprite files. |
| Audio (music and effects) | `UnityProject/Assets/Main/Audio/` | Project-owned (CC0 by the project) | **Synthesised, not sampled.** `tools/gen-audio.py` writes all 13 clips (11 effects, 2 six-second music loops) as mono 22 050 Hz 16-bit PCM from its own oscillators and a seeded LCG noise source — no downloaded sample pack, no third-party loop. The script is the provenance: `python tools/gen-audio.py --check` reports whether the committed bytes still match it (Stage 13c-1). |
| **Halfbody portrait `2110101`** (skeleton + atlas page + `source.json`) | `UnityProject/Assets/Resources/Portrait/` | **UNKNOWN — BLOCKER** | Stage 17e's battle-HUD bust. It arrived as a Spine export extracted from a game bundle (`character/spinelib/halfbody_dizi/jianxiu/2110101_skeletondata.ab`, recorded in `Resources/Portrait/source.json`), with the source game's own outline/stencil material — **not copied**: the project uses its own SkeletonGraphic material over the page texture. No licence, author or purchase record came with it, and its relation to `body_1110901` is **unconfirmed**. It ships from `Resources/`, so it must not enter a release build until the provenance is settled. See [ADR 0037](docs/adr/0037-spine-actors-and-the-player-figure.md) and [the release known issues](docs/release/known-issues.md). |
| **Mor skeleton `body_1110901`** (skeleton + atlas page + material) | `UnityProject/Assets/Resources/body_1110901/` | **UNKNOWN — BLOCKER** | This is a Spine export from outside this project: the skeleton's own data names an authoring path (`E:/.../spine/1110901`) and Chinese part names, and no licence file, author or purchase record came with it. **It must not enter a release build until the provenance is settled** (a written licence, or a project-owned replacement) — it ships from `Resources/`, so any Release build would carry it, and `tools/measure-web-size.mjs` would count it. Works in a development build in the meantime. See [ADR 0037](docs/adr/0037-spine-actors-and-the-player-figure.md) and [the release known issues](docs/release/known-issues.md). |
| `THSarabunPSK.ttf` and its baked `THSarabunPSK SDF` atlas | **removed in Stage 15** | Thai-language font licence (SIPA), not OFL — provenance was never established in this repository | It was unused and still shipped from `Assets/Resources/Fonts/` (no scene, prefab or field referenced it — Stage 09d, measured). **Removed** from the repository for the release candidate; nothing in the game referenced it, and the .data file fell by 171,415 B. If it is ever wanted back, its licence must be established first. |

## Unity package assets (editor templates, not game art)

`UnityProject/Assets/TextMesh Pro/` holds TMP's bundled fonts, example scenes, sprites and shaders.
They are covered by the **Unity Companion License** for Unity Technologies' own material, and the
individual fonts (Anton, Bangers, Oswald, Roboto, LiberationSans, Unity) carry their own
**SIL OFL 1.1 / Apache-2.0** notices inside that folder. They are not referenced by the game scenes and
do not ship in the player; they exist so TMP's editor tools work.

## Code and tools

| Dependency | Version / pin | Licence | Where the notice lives |
| --- | --- | --- | --- |
| Leopotam EcsLite | vendored, pinned revision | MIT | `lib/third_party/ecslite/LICENSE.md` and `VENDOR.md` |
| Luban runtime | vendored, Luban 5.1.0 | MIT | `lib/third_party/luban-runtime/LICENSE.md`, `tools/versions.lock.json` |
| MessagePack for C# (source generator) | 3.1.12 | MIT | `UnityProject/Assets/Packages/MessagePack.3.1.12/` |
| Google.Protobuf | 3.33.6 | BSD-3-Clause | `UnityProject/Assets/Packages/Google.Protobuf.3.33.6/` |
| R3 | 1.3.1 | MIT | `UnityProject/Assets/Packages/R3.1.3.1/` |
| UniTask | package | MIT | `UnityProject/Packages/manifest.json`, package cache |
| VContainer | package | MIT | `UnityProject/Packages/manifest.json`, package cache |
| NuGetForUnity | 4.x | MIT | `UnityProject/Assets/NuGet/LICENSE` |
| .NET BCL packages (`System.*`, `Microsoft.Bcl.*`) | as pinned in `packages.config` | MIT (Microsoft) | Each package folder carries `LICENSE.TXT` / `THIRD-PARTY-NOTICES.TXT` |
| Unity URP, Test Framework, Input System, Timeline, AI Navigation | as locked in `tools/versions.lock.json` | Unity Companion License | Unity package cache (`Library/PackageCache`, not committed) |
| **Spine Runtimes (spine-unity)** | `3.8.0`, committed at `UnityProject/Assets/Spine/` | **Spine Runtimes License** (Esoteric Software LLC, last updated May 1 2019) | The full notice is committed with the runtime: `UnityProject/Assets/Spine/LICENSE.txt`. The licence permits integrating the runtimes into a product, **provided each user of the product/team holds a Spine Editor license**, and requires that any redistribution carries the notice — which it does. The team's own Spine Editor license is **not** evidenced anywhere in this repository: an open question for whoever ships, not a code problem. |
| Playwright, .NET SDK, Node.js (build and test only) | see `tools/versions.lock.json` / `package.json` | Apache-2.0 / MIT | Tool installs, never shipped in the player |

## How the audit was made (Stage 13a)

1. Every file under `UnityProject/Assets` was classified: font, texture/sprite, audio, shader, package
   payload, or editor template. Audio and sprite files at audit time: **none outside the Unity/TMP
   templates**. The only audio since added is Stage 13c-1's generated set under
   `Assets/Main/Audio/`, whose provenance is the generator script (see the table above).
2. Each font was matched to a licence text in the repository (`Assets/Main/Fonts/OFL.txt`) or flagged.
   The one flagged font (`THSarabunPSK`) has since been removed (Stage 15), so the only font the game
   ships is Sarabun under the OFL.
3. Each package was read from `UnityProject/Packages/manifest.json`, `UnityProject/Assets/packages.config`
   and `lib/third_party/*/VENDOR.md`, and its notice file located.
4. The placeholder art was confirmed to be generated from primitives in the listed `*.cs` files rather
   than loaded from disk — which is what makes "no third-party pixel ships" checkable.
