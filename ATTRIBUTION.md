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
| Audio (music and effects) | *(none yet — Stage 13c adds them)* | Project-owned (CC0) | Stage 13c synthesises the placeholder SFX and ambience with a committed script; no downloaded sample packs. Recorded here when it lands. |
| `THSarabunPSK.ttf` and its baked `THSarabunPSK SDF` atlas | `UnityProject/Assets/Resources/Fonts/` | **Thai-language font licence (SIPA), not OFL** — provenance not established in this repository | **Risk: unused and shipped.** No scene, prefab or field references it (Stage 09d, measured); it is a template leftover from the Stage 03 spike recorded in `UnityProject/SETUP.md` §8, and being under `Resources/` it is included in every build (4.0 MiB). It must be removed from `Resources/` (or the repository) before a release candidate, because its licence terms are not the OFL the rest of the UI ships under. |

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
| Playwright, .NET SDK, Node.js (build and test only) | see `tools/versions.lock.json` / `package.json` | Apache-2.0 / MIT | Tool installs, never shipped in the player |

## How the audit was made (Stage 13a)

1. Every file under `UnityProject/Assets` was classified: font, texture/sprite, audio, shader, package
   payload, or editor template. Audio and sprite files: **none outside the Unity/TMP templates**.
2. Each font was matched to a licence text in the repository (`Assets/Main/Fonts/OFL.txt`) or flagged
   (`THSarabunPSK`, above).
3. Each package was read from `UnityProject/Packages/manifest.json`, `UnityProject/Assets/packages.config`
   and `lib/third_party/*/VENDOR.md`, and its notice file located.
4. The placeholder art was confirmed to be generated from primitives in the listed `*.cs` files rather
   than loaded from disk — which is what makes "no third-party pixel ships" checkable.
