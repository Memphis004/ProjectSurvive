# llmwiki — Index

The wiki collects the project's **system notes** and **gotchas** so an agent does
not have to rediscover them. It complements, and never replaces, the GDD and the
architecture docs.

- Start here: [`../knowledge.md`](../knowledge.md)
- Thai ↔ code names: [`glossary.md`](glossary.md)
- Rules engine: [`../docs/architecture/overview.md`](../docs/architecture/overview.md)
- Dependency risk: [`../docs/architecture/dependencies.md`](../docs/architecture/dependencies.md)

---

## Systems

| System | Note | Stage | Status |
| --- | --- | --- | --- |
| Static data pipeline (Luban, protoc) | [`systems/data-schema.md`](systems/data-schema.md) | 02 | done |
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
| Luban 5.1 CLI: `--strict`, `__tables__.csv`, loader keys | [`gotchas/luban-5-strict-and-tables.md`](gotchas/luban-5-strict-and-tables.md) | Without `--strict` a broken `#ref` only warns and still exits 0; `#ref` cannot mean "optional" |
| Thai text with TMP | *(pending)* | Upper/lower vowels + tone marks misplace; Stage 03 spike |
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
