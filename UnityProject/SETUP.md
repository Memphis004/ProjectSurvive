# UnityProject — SETUP

> **Status: not created yet.** This folder is a placeholder from Stage 00.
> The Unity project is created in **Stage 03** (Unity Shell, first Web build,
> Thai text spike). Nothing here should be hand-authored in the meantime.

## Why the folder exists now

`docs/architecture/overview.md` and the GDD fix `UnityProject/` as the location
of the Unity shell, separate from `lib/` (the pure C# rules package). Creating
the directory now keeps the repository layout stable and lets `tools/doctor.ps1`
verify the structure.

## Hard rules for whoever creates it (Stage 03)

1. **Create the project with the locked Editor.** Use Unity **`6000.3.9f1`**
   (`tools/versions.lock.json` → `engine.unity.version`). Do not let the Hub
   silently pick a different patch.
2. **URP.** Use the Universal Render Pipeline template, then trim unused
   packages.
3. **Scenes and prefabs come from the Unity Editor API or a verified template.**
   Never hand-write `.unity` / `.prefab` YAML (AGENTS #11).
4. **Reference the rules package by local path, without clobbering existing
   entries.** `Packages/manifest.json` must gain:

   ```json
   {
     "dependencies": {
       "com.projectsurvive.core": "file:../../lib"
     }
   }
   ```

   Merge into the existing `dependencies` object — do not overwrite the file.
   (The manifest itself is also produced by Unity, so it is fine for Unity to
   write it; the point is not to lose the other entries.)
5. **Add the scoped registries** needed by the proposed dependencies
   (VContainer, R3, UniTask) — see
   [`../docs/architecture/dependencies.md`](../docs/architecture/dependencies.md).
   Npm-style packages resolve from OpenUPM:

   ```json
   {
     "scopedRegistries": [
       {
         "name": "OpenUPM",
         "url": "https://package.openupm.com",
         "scopes": ["jp.hadashikick.vcontainer", "com.cysharp"]
       }
     ]
   }
   ```

   Verify each resolved version after import and record it in
   `tools/versions.lock.json` (`status: verified`).
6. **Build target = WebGL.** Confirm the WebGL module is present
   (`tools/doctor.ps1` checks it) and record whether the first build succeeds.

## Planned layout (from the GDD)

```text
UnityProject/
├── Packages/manifest.json        # references com.projectsurvive.core (file:../../lib)
└── Assets/Main/
    ├── Scenes/                   # Boot, Title, Camp, Battle, Result
    ├── Resources/Config/         # Luban .bytes (MVP loading path)
    ├── Scripts/                  # Infrastructure, Presentation, Editor, Testing
    └── Tests/                    # EditMode, PlayMode
```

Scene and prefab authoring happens in Stage 03+ (Boot/Title), Stage 08 (Battle),
Stage 09 (Camp), Stage 10 (Result).

## Generated and ignored

- `UnityProject/Library/`, `Temp/`, `Logs/`, and `UserSettings/` are
  **git-ignored** (see the root `.gitignore`) and must never be committed.
- `UnityProject/Packages/packages-lock.json` is ignored; `manifest.json` is the
  source of truth.
- Generated `.bytes` config is ignored; regenerate with `tools/gen.ps1`
  (Stage 02).
