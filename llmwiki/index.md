# llmwiki — Index

The wiki collects the template's **system notes** and **gotchas** so an agent does
not have to rediscover them. It complements, and never replaces, the architecture
docs.

- Start here: [`../knowledge.md`](../knowledge.md)
- Running log of landed steps: [`stage-log.md`](stage-log.md)
- Code names: [`glossary.md`](glossary.md)
- Architecture: [`../docs/architecture/overview.md`](../docs/architecture/overview.md)
- Dependency risk: [`../docs/architecture/dependencies.md`](../docs/architecture/dependencies.md)
- Which tests run when: [`../docs/testing/test-suites.md`](../docs/testing/test-suites.md)
- The game-era notes and ADRs, kept as **examples**: [`../docs/template/examples/`](../docs/template/examples/)

---

## Systems

| System | Note | Status |
| --- | --- | --- |
| Static data pipeline (Luban 5.1, protoc, the three tables, the validator) | [`systems/data-schema.md`](systems/data-schema.md) | done |
| The loop and the three scopes (App / Run / Scene), `SceneLoader`, the published screens | [`systems/loop-and-scopes.md`](systems/loop-and-scopes.md) | done |
| Unity shell (scenes, DI, the 7-probe smoke suite, page globals, the Web build) | [`systems/unity-shell.md`](systems/unity-shell.md) | done |
| Core primitives (RNG, clock, ticker, `Result`, `EventRing<T>`, `PrevCurBuffer<T>`) | [`../docs/architecture/overview.md`](../docs/architecture/overview.md), [ADR 0004](../docs/adr/0004-dotnet-layer-enforcement.md), [ADR 0024](../docs/adr/0024-shared-generic-buffers.md) | done |
| Save / resume | *(pending — a game's decision)* | not started |

Notes live in [`systems/`](systems/). Create one file per system as it lands.

## Gotchas

Each row is one fact that cost real time. Read the one whose symptom you recognise
before changing the area it names.

| Gotcha | Note | Why it matters |
| --- | --- | --- |
| Determinism scope | [`gotchas/determinism-scope.md`](gotchas/determinism-scope.md) | Guaranteed within one runtime only, not across browsers or CPUs |
| IL2CPP strips constructors reflection-only code needs | [`gotchas/il2cpp-di-and-reflection.md`](gotchas/il2cpp-di-and-reflection.md) | A VContainer registration that works in the Editor can fail in the player; register with factories |
| Luban 5.1 CLI: `--strict`, `__tables__.csv`, loader keys | [`gotchas/luban-5-strict-and-tables.md`](gotchas/luban-5-strict-and-tables.md) | Without `--strict` a broken `#ref` only warns and still exits 0 |
| Dynamic TMP font assets lose their tables at build time | [`gotchas/tmp-font-assets-in-builds.md`](gotchas/tmp-font-assets-in-builds.md) | Bake, then freeze to Static, or the player ships an empty character table |
| `TryAddCharacters`' return value is not coverage | [`gotchas/tmp-tryaddcharacters-lies.md`](gotchas/tmp-tryaddcharacters-lies.md) | A false return with the whole set "missing" usually means the face failed to load |
| A size-profile build leaves its player settings behind | [`gotchas/webgl-size-profile-settings-residue.md`](gotchas/webgl-size-profile-settings-residue.md) | The next build inherits `exceptionSupport: None` and halts at boot with `Uncaught undefined` |
| A GUI Build click rewrites player settings | [`gotchas/gui-build-rewrites-settings.md`](gotchas/gui-build-rewrites-settings.md) | Only a CLI build with a recorded configuration is evidence |
| A scene reference is not compiled | [`gotchas/scene-reference-not-compiled.md`](gotchas/scene-reference-not-compiled.md) | A builder can create a component and never assign the field; only a PlayMode test reading the scene sees it |
| A destroyed Unity object is not a `null` reference | [`gotchas/destroyed-unity-object-not-null.md`](gotchas/destroyed-unity-object-not-null.md) | `is null` and `?.` bypass Unity's overloaded `==` and throw |
| The view must copy the frame before the next step | [`gotchas/copy-frame-before-step.md`](gotchas/copy-frame-before-step.md) | `PrevCurBuffer<T>` reuses two slots; a live span after another step reads memory being overwritten |
| Pool size is per kind, the frame cap is per frame | [`gotchas/pool-size-vs-frame-cap.md`](gotchas/pool-size-vs-frame-cap.md) | A pool equal to the frame budget starves one kind mid-frame |
| Unity's default WebGL template pins the canvas at 960×600 | [`gotchas/webgl-template-canvas-960x600.md`](gotchas/webgl-template-canvas-960x600.md) | A window resize never reaches the player; aim clicks with the size the player published |
| A `CanvasScaler`'s virtual canvas follows the aspect | [`gotchas/canvas-scaler-follows-aspect.md`](gotchas/canvas-scaler-follows-aspect.md) | 1920-wide arithmetic is 26 px off-centre at 960×600; anchor centred content |
| A layout report written in the resize frame is a frame early | [`gotchas/layout-report-one-frame-early.md`](gotchas/layout-report-one-frame-early.md) | The report pairs the new canvas size with the old geometry |
| A `data/` edit is not in the build until `gen.ps1` runs | [`gotchas/data-edit-needs-gen.md`](gotchas/data-edit-needs-gen.md) | The player and the tests read generated bytes, not the CSV |
| `validate-data.ps1` defaults to `data/`, which has no `.bytes` | [`gotchas/validate-data-needs-datadir.md`](gotchas/validate-data-needs-datadir.md) | `table_missing` there is a path bug, not a data bug |
| A green test run may have run a stale assembly | [`gotchas/stale-test-assembly.md`](gotchas/stale-test-assembly.md) | A compile error makes `run_tests` report the previous payload's pass count |
| A scene must publish its own name | [`gotchas/scene-must-publish-name.md`](gotchas/scene-must-publish-name.md) | `__projectSurviveScene` is the only handover a browser test can see |
| `.unity` YAML is not hand-editable | [`gotchas/unity-yaml-not-hand-editable.md`](gotchas/unity-yaml-not-hand-editable.md) | Scenes are created through the Editor API only; re-run the builder instead of merging |

Notes live in [`gotchas/`](gotchas/). Add one when a fact costs time twice.

## Examples (the game the template came from)

`docs/template/examples/` holds the game-era documents: thirteen ADRs, six system
notes, the EcsLite gotcha, the game glossary and the original design/extraction
brief. They are **not rules** — read them for the reasoning, not for the API. See
[`../docs/template/examples/README.md`](../docs/template/examples/README.md).

## External references

| Topic | Where |
| --- | --- |
| Unity `6000.3` docs | https://docs.unity3d.com/6000.3/Documentation/Manual/index.html |
| WebGL build / memory | Unity 6.3 manual → *Web platform* |
| VContainer | https://github.com/hadashiA/VContainer |
| R3 | https://github.com/Cysharp/R3 |
| UniTask | https://github.com/Cysharp/UniTask |
| MessagePack for C# | https://github.com/MessagePack-CSharp/MessagePack-CSharp |
| Luban | https://github.com/focus-creative-games/luban |
| Google.Protobuf | https://github.com/protocolbuffers/protobuf |
| EcsLite (if a game adds an engine) | https://github.com/Leopotam/ecslite |
