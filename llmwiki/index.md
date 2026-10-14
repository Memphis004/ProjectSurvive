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
| Prototype content (14 pieces, tag vocabulary, tiers, floors, shop pool) | [`systems/data-schema.md`](systems/data-schema.md#the-stage-04-prototype-content) | 04 | done |
| Battle contract v1 (interfaces, fake, contract tests) | [`systems/battle-contract.md`](systems/battle-contract.md) | 02b | done |
| Bot simulation + acceptance benchmark (kiting bot, greedy camp, balance CSV) | [`systems/battle-simulation.md#11-running-it`](systems/battle-simulation.md#11-running-it) | 07 | done |
| Unity shell (scenes, DI, smoke suite, WebGL build, browser tests) | [`systems/unity-shell.md`](systems/unity-shell.md) | 03 | done |
| WebGL payload size (profiles, measurement, per-assembly attribution) | [`../docs/build/size-baseline.md`](../docs/build/size-baseline.md) | 03b | done |
| Bag / Board placement, editing commands, undo/lock | [`systems/backpack.md`](systems/backpack.md) | 05 | done |
| Combo (synergy) evaluation + bag → battle loadout | [`systems/backpack.md#6-combos--synergyevaluator`](systems/backpack.md#6-combos--synergyevaluator) | 05 | done |
| Shop / economy (wallet, `shop_pool`, stock per camp, re-roll ladder) | [`systems/run-shop.md`](systems/run-shop.md) | 06 | done |
| Run & scene flow (phase machine, commands, settlement, `ReplayLog`) | [`systems/run-shop.md`](systems/run-shop.md) | 06 | done |
| Battle simulation (EcsLite) | [`systems/battle-simulation.md`](systems/battle-simulation.md) | 07 | done |
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
| Unity Editor FPS ≠ Web FPS | [ADR 0003](../docs/adr/0003-performance-baseline-machine-and-browser.md) | Only a real browser build counts |
| IL2CPP strips constructors reflection-only code needs | [`gotchas/il2cpp-di-and-reflection.md`](gotchas/il2cpp-di-and-reflection.md) | A VContainer registration that works in the Editor can fail in the player; register with factories |
| Dynamic TMP font assets lose their tables at build time | [`gotchas/tmp-font-assets-in-builds.md`](gotchas/tmp-font-assets-in-builds.md) | The first Web build shipped a font asset with an empty character table; bake, then freeze to Static |
| A GUI Build click rewrites player settings | [ADR 0011](../docs/adr/0011-webgl-payload-budget-and-size-measurement.md) | It applies platform profile defaults (`webGLNameFilesAsHashes`, compression, linear lighting) and regenerates the shared IL2CPP tree; only CLI `-SizeProfile` builds are evidence |
| Build size is meaningless without its basis | [ADR 0011](../docs/adr/0011-webgl-payload-budget-and-size-measurement.md) | The same player is 44.29 MiB raw, 15.53 MiB gzip or 10.87 MiB Brotli; a budget is on a compressed payload |
| `TryAddCharacters`' return value is not coverage | [`gotchas/tmp-tryaddcharacters-lies.md`](gotchas/tmp-tryaddcharacters-lies.md) | A false return with the whole set "missing" usually means the face failed to load |
| Luban 5.1 CLI: `--strict`, `__tables__.csv`, loader keys | [`gotchas/luban-5-strict-and-tables.md`](gotchas/luban-5-strict-and-tables.md) | Without `--strict` a broken `#ref` only warns and still exits 0; `#ref` cannot mean "optional" |
| Thai text with TMP | [ADR 0008](../docs/adr/0008-thai-text-ui-stack.md) | Both stacks render Thai with 0 missing glyphs at 182-char coverage; the traps are in the asset and build pipeline, not the shaping |
| A footprint picture cannot express a duplicated cell | [ADR 0009](../docs/adr/0009-footprint-picture-format.md) | The Stage 04 format makes that bug class unrepresentable; `footprint.duplicate_cell` no longer exists |
| A charm's own buff has no data column yet | [ADR 0010](../docs/adr/0010-item-local-effects-deferred.md) | Its magnitude must land in `item`, never as a code constant, when Stage 05–07 lands its consumer |
| Synergies are resolved into the loadout, not inside the battle | [`systems/backpack.md`](systems/backpack.md#7-bag--battle-loadoutresolver) | The battle reads one `CombatLoadoutSnapshot`; recomputing combos there would let two resolvers disagree and would run per tick |
| Tiers do not change weight or price | [ADR 0013](../docs/adr/0013-weight-speed-penalty-curve.md), [ADR 0014](../docs/adr/0014-backpack-editing-undo-and-placement-ownership.md) | `item`/`bag` carry one `weight` and one `price`; merging to tier 3 is stronger, not heavier or more valuable — a data-model gap, not a rule |
| A bag move can be refused because of the *other* bag | [ADR 0014](../docs/adr/0014-backpack-editing-undo-and-placement-ownership.md) | Two bags can both cover one item; the command is refused whole rather than auto-relocating the item, so the Stage 09 UI has to name the item |
| `SameRow` and `SameColumn` are different rules, not one “same line” rule | [ADR 0015](../docs/adr/0015-same-line-synergy-kinds.md) | The GDD's table says *row*; widening the shipped kind would silently strengthen a balance rule |
| A rented shop slot is **emptied**, not merely flagged | [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) | The panel cannot show "what was rented here"; only `PurchaseOutcome` carries it, because a rented slot must not look rentable |
| The settlement id is a report, never the once-per-floor guard | [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) | The guard is the phase plus the pending setup; a second `ApplyBattleResult` is `run.not_in_battle` |
| A `BattleResult` whose consumable list length differs from the loadout refuses the whole settlement | [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) | `RemainingConsumables` is aligned with the loadout; clamping it would pay currency for a floor the run cannot reconcile |
| `validate-data.ps1` defaults to `data/`, which has no `.bytes` | [ADR 0001](../docs/adr/0001-repository-and-artifact-conventions.md) | Generated tables are published to `lib/Tests~/data`; call it with `-DataDir "lib/Tests~/data"` or it reports `table_missing` |
| Determinism scope | [`gotchas/determinism-scope.md`](gotchas/determinism-scope.md) | Guaranteed within one runtime only, not across browsers/CPUs |
| EcsLite pool refs, liveness, DEBUG checks | [`gotchas/ecslite-pool-refs.md`](gotchas/ecslite-pool-refs.md) | A stale `ref`, or a Release build with sanitize checks compiled out |
| A published battle frame only survives until the next tick | [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md) | `Entities` / `PreviousEntities` are two slots reused in place; copy before the next `Step`, and `Prev` is one tick deep, not history |
| `BattleContext.WaveCount` must be assigned from `Waves` | [`systems/battle-simulation.md`](systems/battle-simulation.md#12-facts-that-cost-time-to-learn) | It is the systems' contract; left at `0` the floor spawns nothing and silently times out as a win |
| A battle def id is a **table position**, not a hash or a string | [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) | Inserting a row anywhere but the end renumbers ids; safe only because an id lives inside one battle's event stream |
| The prototype earns no bia inside a battle | [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) | No `data/` column says what a kill pays, so `CurrencyEarned` is 0 and a camp can only spend `economy.start_bia` — this dominates any balance reading of the bot CSV |
| Elemental status re-uses the applying skill's damage | [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) | It is an interim rule, not balance: the `status` table Stage 11 adds is what replaces it |
| The shared contract suite assumes an idle player can survive | [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) | `RunToEnd` never moves, so the EcsLite derived class supplies HP + a churning load through the benchmark knobs; do not "fix" a failing contract case by relaxing it |
| There is only one `IBattleFrame` implementation | [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md) | The fake and the EcsLite session must share `BattleFrameBuffer`, or the presentation validated against the fake stops predicting the real battle |
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
