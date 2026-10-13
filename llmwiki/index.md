# llmwiki — Index

The wiki collects the project's **system notes** and **gotchas** so an agent does
not have to rediscover them. It complements, and never replaces, the GDD and the
architecture docs.

- Start here: [`../knowledge.md`](../knowledge.md)
- Running log of landed stages: [`stage-log.md`](stage-log.md)
- Thai ↔ code names: [`glossary.md`](glossary.md)
- Rules engine: [`../docs/architecture/overview.md`](../docs/architecture/overview.md)
- Dependency risk: [`../docs/architecture/dependencies.md`](../docs/architecture/dependencies.md)

---

## Systems

| System | Note | Stage | Status |
| --- | --- | --- | --- |
| Static data pipeline (Luban, protoc) | [`systems/data-schema.md`](systems/data-schema.md) | 02 | done |
| Battle contract v1 (interfaces, fake, contract tests) | [`systems/battle-contract.md`](systems/battle-contract.md) | 02b | done |
| Unity shell (scenes, DI, smoke suite, WebGL build, browser tests) | [`systems/unity-shell.md`](systems/unity-shell.md) | 03 | done |
| Bag / Board placement | *(folders ready, note pending)* | 05 | not started |
| Combo evaluation | *(pending)* | 05–06 | not started |
| Shop / economy | *(pending)* | 06 | not started |
| Run & scene flow | *(pending)* | 06 | not started |
| Battle simulation (EcsLite) | *(pending)* | 07 | not started |
| 2.5D presentation | *(pending)* | 08 | not started |
| Core foundation (RNG / clock / ticker / Result) | [ADR 0004](../docs/adr/0004-dotnet-layer-enforcement.md) + [`../docs/architecture/overview.md`](../docs/architecture/overview.md) | 01 | done |
| Save / resume | *(pending)* | 12 | not started |

Notes live in [`systems/`](systems/). Create one file per system as its stage
lands.

## Gotchas

| Gotcha | Note | Why it matters |
| --- | --- | --- |
| WebGL2 is single-threaded | *(pending)* | Allocation in hot paths causes GC hitches; pooling is mandatory |
| MessagePack dynamic resolver | *(pending)* | Not IL2CPP-compatible; source generator only |
| Unity Editor FPS ≠ Web FPS | [`../docs/adr/0003-...`](../adr/0003-performance-baseline-machine-and-browser.md) | Only a real browser build counts |
| IL2CPP strips constructors reflection-only code needs | [`gotchas/il2cpp-di-and-reflection.md`](gotchas/il2cpp-di-and-reflection.md) | A VContainer registration that works in the Editor can fail in the player; register with factories |
| Dynamic TMP font assets lose their tables at build time | [`gotchas/tmp-font-assets-in-builds.md`](gotchas/tmp-font-assets-in-builds.md) | The first Web build shipped a font asset with an empty character table; bake, then freeze to Static |
| `TryAddCharacters`' return value is not coverage | [`gotchas/tmp-tryaddcharacters-lies.md`](gotchas/tmp-tryaddcharacters-lies.md) | A false return with the whole set "missing" usually means the face failed to load |
| Luban 5.1 CLI: `--strict`, `__tables__.csv`, loader keys | [`gotchas/luban-5-strict-and-tables.md`](gotchas/luban-5-strict-and-tables.md) | Without `--strict` a broken `#ref` only warns and still exits 0; `#ref` cannot mean "optional" |
| Thai text with TMP | [ADR 0008](../docs/adr/0008-thai-text-ui-stack.md) | Both stacks render Thai with 0 missing glyphs at 182-char coverage; the traps are in the asset and build pipeline, not the shaping |
| Determinism scope | [`gotchas/determinism-scope.md`](gotchas/determinism-scope.md) | Guaranteed within one runtime only, not across browsers/CPUs |
| EcsLite pool refs, liveness, DEBUG checks | [`gotchas/ecslite-pool-refs.md`](gotchas/ecslite-pool-refs.md) | A stale `ref`, or a Release build with sanitize checks compiled out |
| `.unity` YAML is not hand-editable | *(pending)* | Scenes/prefabs are created via the Editor API only |

Notes live in [`gotchas/`](gotchas/).

## External references

| Topic | Where |
| --- | --- |
| Unity `6000.3` docs | https://docs.unity3d.com/6000.3/Documentation/Manual/index.html |
| WebGL build / memory | Unity 6.3 manual → *Web platform* |
| EcsLite (Leopotam, MIT) | https://github.com/Leopotam/ecslite |
| VContainer | https://github.com/hadashiA/VContainer |
| R3 | https://github.com/Cysharp/R3 |
| UniTask | https://github.com/Cysharp/UniTask |
| MessagePack for C# | https://github.com/MessagePack-CSharp/MessagePack-CSharp |
| Luban | https://github.com/focus-creative-games/luban |
| Google.Protobuf | https://github.com/protocolbuffers/protobuf |
