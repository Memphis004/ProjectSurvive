# llmwiki — Index

The wiki collects the project's **system notes** and **gotchas** so an agent does
not have to rediscover them. It complements, and never replaces, the GDD and the
architecture docs.

- Start here: [`../knowledge.md`](../knowledge.md)
- How to query, ingest and lint this wiki (agent-facing): [`SCHEMA.md`](SCHEMA.md)
- Turning a stage report into a note: [`INGEST.md`](INGEST.md) · [ingest log](log.md)
- Running log of landed stages: [`stage-log.md`](stage-log.md)
- Thai ↔ code names: [`glossary.md`](glossary.md)
- Rules engine: [`../docs/architecture/overview.md`](../docs/architecture/overview.md)
- Dependency risk: [`../docs/architecture/dependencies.md`](../docs/architecture/dependencies.md)
- Which tests run when (Fast/Full, timings, test → suite): [`../docs/testing/test-suites.md`](../docs/testing/test-suites.md)

---

## Content (generated from `data/`)

[`content/index.md`](content/index.md) is the **synthesised content layer**: pages
written by `tools/gen-wiki.ps1` from `data/`, never by hand. It answers content
questions (`มีไอเทมอะไรบ้าง`, `ชั้น 3 มีผีอะไร`, `คอมโบนี้ทำงานเมื่อไหร่`) without
re-deriving the tables. Numbers there are copies; `data/` remains the source of
truth (see [`SCHEMA.md`](SCHEMA.md)).

| Question | Page |
| --- | --- |
| Items, bags, prices, weights, footprints, which combos/upgrades a piece joins | [`content/items.md`](content/items.md) |
| A skill's behaviour, cooldown, MP, damage, range, pierce, projectiles | [`content/skills.md`](content/skills.md) |
| The five combo rules and the real pieces on each side | [`content/synergies.md`](content/synergies.md) |
| Upgrade variants per tier and their eligible pieces | [`content/upgrades.md`](content/upgrades.md) |
| Enemies and the floors they appear on | [`content/enemies.md`](content/enemies.md) |
| Floor durations, bosses and wave timelines | [`content/floors.md`](content/floors.md) |
| Purse, shop, sell-back, heal, constants | [`content/economy.md`](content/economy.md) |
| Why a system was designed this way | [`../docs/adr/`](../docs/adr/) + [`systems/`](systems/) |
| A build/test/Unity trap | [`gotchas/`](gotchas/) |
| Thai term ↔ code name | [`glossary.md`](glossary.md) |
| The latest number with certainty | read `data/` directly |

`tools/verify-wiki.ps1` fails when a content page drifts from `data/`, when a link
or anchor is broken, when a data id has no section, or when a note below is not
linked from this page. It is the Fast step `wiki: content reproducible`. A
hand-written page that **restates** a skill's numbers raises a `balance_numbers`
**WARN** (never a failure): numbers belong to `content/`, generated from `data/`.

---

## Systems

| System | Note | Stage | Status |
| --- | --- | --- | --- |
| Static data pipeline (Luban, protoc) | [`systems/data-schema.md`](systems/data-schema.md) | 02 | done |
| Prototype content (14 pieces, tag vocabulary, tiers, floors, shop pool) | [`systems/data-schema.md`](systems/data-schema.md#the-stage-04-prototype-content) | 04 | done |
| Battle contract v1 (interfaces, fake, contract tests, the frame's cooldown list) | [`systems/battle-contract.md`](systems/battle-contract.md) | 02b, 08b | done |
| Bot simulation + acceptance benchmark (kiting bot, greedy camp, balance CSV) | [`systems/battle-simulation.md#11-running-it`](systems/battle-simulation.md#11-running-it) | 07 | done |
| Unity shell (scenes, DI, smoke suite, WebGL build, browser tests) | [`systems/unity-shell.md`](systems/unity-shell.md) | 03 | done |
| WebGL payload size (profiles, measurement, per-assembly attribution) | [`../docs/build/size-baseline.md`](../docs/build/size-baseline.md) | 03b | done |
| Bag / Board placement, editing commands, undo/lock, the carried-item rule | [`systems/backpack.md`](systems/backpack.md) | 05, carried rule 09f | done |
| Combo (synergy) evaluation + bag → battle loadout | [`systems/backpack.md#6-combos--synergyevaluator`](systems/backpack.md#6-combos--synergyevaluator) | 05 | done |
| Shop / economy (wallet, `shop_pool`, stock per camp, re-roll ladder, clear reward) | [`systems/run-shop.md`](systems/run-shop.md) | 06, 07b | done |
| Run & scene flow (phase machine, commands, settlement, `ReplayLog`) | [`systems/run-shop.md`](systems/run-shop.md) | 06 | done |
| Battle simulation (EcsLite), bia from kills, the end-of-floor sweep | [`systems/battle-simulation.md`](systems/battle-simulation.md) | 07, 07b, 07c | done |
| 2.5D battle presentation (scene, clocks, pools, HUD, camera, the mini ย่าม and its cooldown plates, the pause overlay and the retreat) | [`systems/battle-presentation.md`](systems/battle-presentation.md) | 08, 08c, 13c-2b | done |
| Camp screen (ศาลาพัก: the sala, walkable, shelves as the shop, the จัดย่าม drag, the two published reports) | [`systems/camp.md`](systems/camp.md) | 09 | done |
| The full loop: three scopes (App / Run / Scene), `SceneLoader`, the floor loop, the title's start control, the summary screen, dev cheats | [`systems/loop-and-scopes.md`](systems/loop-and-scopes.md) | 10, 10d | done |
| Core foundation (RNG / clock / ticker / Result) | [ADR 0004](../docs/adr/0004-dotnet-layer-enforcement.md) + [`../docs/architecture/overview.md`](../docs/architecture/overview.md) | 01 | done |
| Audio (generated clips, cue channels, the one device, Web autoplay, import settings) | [`systems/audio.md`](systems/audio.md) | 13c-1 | done |
| Save / resume | *(pending)* | 12 | not started |

Notes live in [`systems/`](systems/). Create one file per system as its stage
lands; [`INGEST.md`](INGEST.md) is the procedure for updating one from a stage
report.

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
| A scene reference is not compiled | [`systems/battle-presentation.md`](systems/battle-presentation.md#11-facts-that-cost-time-to-learn) | Stage 08c's mini bag was built by `ProjectSetup` but never assigned to `BattleViewSettings.MiniBag`: a clean compile, a green EditMode suite and correct-looking capture shots (that tool finds the component by type) all missed it. Only a PlayMode test that reads the **scene's** wiring sees this class of defect |
| A paused runner writes no report, so it cannot prove it paused | [`systems/battle-presentation.md`](systems/battle-presentation.md#12-pause-and-the-retreat-stage-13c-2b) | `BattleSimulationRunner` publishes `__projectSurviveBattle` from its `Update`, which returns before it does anything while paused. The pause overlay therefore publishes its own document twice a second while it is open; the freeze is read there as an unchanging `tick` |
| A live gesture is invisible in the camp's published report | [`systems/camp.md`](systems/camp.md#2-three-rules-that-bite) | `CampSceneDirector` freezes the state document while a drag is active, so `dragging: true` can never be read from the page - assert the release counters instead |
| Unity's default desktop WebGL template pins the canvas at 960×600 | [`systems/camp.md`](systems/camp.md), [09-report §5](../docs/stages/09-report.md) | A browser *window* resize never reaches the player; styling the canvas element (what the template's fullscreen button does) does |
| A `data/` edit is not in the build until `gen.ps1` runs | [09-report §6](../docs/stages/09-report.md) | Stage 09 added 81 localization rows the generated tables never received, so a browser run described a player with the old tables; `verify-generated` is the gate |
| The UI font has no symbol glyphs (`✓`, `▶`, `•`) | [`gotchas/symbol-glyphs-are-not-in-the-ui-font.md`](gotchas/symbol-glyphs-are-not-in-the-ui-font.md) | Sarabun is baked without dingbats: a checklist marker is `[x]`/`[>]`/`[ ]`, and the text audit names the missing character |
| A TMP label's box is not its text's size, and `isTextOverflowing` misses the width | [`gotchas/tmp-label-fit-and-font-coverage.md`](gotchas/tmp-label-fit-and-font-coverage.md) | 22 px Sarabun needs a 28.61 px line box; Thai has no spaces, so a narrow box wraps mid-word (the MP caption needed 89 px in a 58 px box) and clips with no warning. `__projectSurviveTextFit` measures every label in the player and the browser suite asserts it |
| A footprint picture cannot express a duplicated cell | [ADR 0009](../docs/adr/0009-footprint-picture-format.md) | The Stage 04 format makes that bug class unrepresentable; `footprint.duplicate_cell` no longer exists |
| A charm's own buff has no data column yet | [ADR 0010](../docs/adr/0010-item-local-effects-deferred.md) | Its magnitude must land in `item`, never as a code constant, when Stage 05–07 lands its consumer |
| Synergies are resolved into the loadout, not inside the battle | [`systems/backpack.md`](systems/backpack.md#7-bag--battle-loadoutresolver) | The battle reads one `CombatLoadoutSnapshot`; recomputing combos there would let two resolvers disagree and would run per tick |
| Tiers do not change weight or price | [ADR 0013](../docs/adr/0013-weight-speed-penalty-curve.md), [ADR 0014](../docs/adr/0014-backpack-editing-undo-and-placement-ownership.md) | `item`/`bag` carry one `weight` and one `price`; merging to tier 3 is stronger, not heavier or more valuable — a data-model gap, not a rule |
| A bag move can be refused because of the *other* bag | [ADR 0020](../docs/adr/0020-bag-moves-carry-the-items-on-them.md) | Two bags can both cover one item; that item does **not** ride, and the command is refused whole (`inventory.item_spans_bags`) rather than auto-relocating it, so the Stage 09 UI has to name the item |
| A rider's new cell is *not* the bag's delta, and the bag's own `shift` is why | [ADR 0020](../docs/adr/0020-bag-moves-carry-the-items-on-them.md), [`systems/backpack.md`](systems/backpack.md#1-the-two-coordinate-systems-and-the-one-rule-that-connects-them) | A turned shape is re-normalised back to `(0,0)`, so `newOrigin + (cell - bagOrigin)` drifts; the L-shaped `bag.wicker_basket_l` is the case that exposes it. `FootprintRotation.Rotate(shape, rotation, out shift)` is the one map both the bag and its riders go through |
| The tome combo is contact (`AdjacentPair`), not a shared row | [ADR 0031](../docs/adr/0031-a-tome-touches-its-weapon.md) | A row rule paid across empty cells and missed the tome's own lower edge; `SameRow`/`SameColumn` remain kinds, unshipped |
| A rented shop slot is **emptied**, not merely flagged | [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) | The panel cannot show "what was rented here"; only `PurchaseOutcome` carries it, because a rented slot must not look rentable |
| The settlement id is a report, never the once-per-floor guard | [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) | The guard is the phase plus the pending setup; a second `ApplyBattleResult` is `run.not_in_battle` |
| A `BattleResult` whose consumable list length differs from the loadout refuses the whole settlement | [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md) | `RemainingConsumables` is aligned with the loadout; clamping it would pay currency for a floor the run cannot reconcile |
| `validate-data.ps1` defaults to `data/`, which has no `.bytes` | [ADR 0001](../docs/adr/0001-repository-and-artifact-conventions.md) | Generated tables are published to `lib/Tests~/data`; call it with `-DataDir "lib/Tests~/data"` or it reports `table_missing` |
| Determinism scope | [`gotchas/determinism-scope.md`](gotchas/determinism-scope.md) | Guaranteed within one runtime only, not across browsers/CPUs |
| EcsLite pool refs, liveness, DEBUG checks | [`gotchas/ecslite-pool-refs.md`](gotchas/ecslite-pool-refs.md) | A stale `ref`, or a Release build with sanitize checks compiled out |
| A published battle frame only survives until the next tick | [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md) | `Entities` / `PreviousEntities` are two slots reused in place; copy before the next `Step`, and `Prev` is one tick deep, not history |
| `BattleContext.WaveCount` must be assigned from `Waves` | [`systems/battle-simulation.md`](systems/battle-simulation.md#12-facts-that-cost-time-to-learn) | It is the systems' contract; left at `0` the floor spawns nothing and silently times out as a win |
| A battle def id is a **table position**, not a hash or a string | [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) | Inserting a row anywhere but the end renumbers ids; safe only because an id lives inside one battle's event stream |
| A battle's bia has two owners | [ADR 0025](../docs/adr/0025-bia-from-battle.md) | Collected pickups are the battle's (`enemy.drop_value` → `BattleResult.CurrencyEarned`); the flat clear reward is the run's (`economy.clear_reward_bia`, paid in `ApplyBattleResult` on a win only). The HUD's bia is `startingBia + Currency`, so the clear reward appears only after settlement |
| A win is not the end of the floor; it opens a sweep | [ADR 0027](../docs/adr/0027-end-of-floor-sweep.md) | The phase stays `Running` while the remaining pickups drift in at `constant.magnet_speed` and pay once; the cap `constant.sweep_timeout_seconds` collects whatever is left so a won floor always produces a result. A loss never sweeps, and only the sweep and the frame publisher run during it (the ghosts stand still, no damage) |
| Elemental status comes from the `status` table | [ADR 0028](../docs/adr/0028-item-effects-and-status-table.md) | Damage per application, cadence, duration and the stack cap are data; ADR 0017's interim rule is superseded |
| A charm's effect is a player stat the loadout carries | [ADR 0028](../docs/adr/0028-item-effects-and-status-table.md) | No new combat mechanic; the magnitude is an `item` column applied by `LoadoutResolver` |
| `RemainingConsumables` counts uses, not pieces | [ADR 0028](../docs/adr/0028-item-effects-and-status-table.md) | One carried offering may have several uses; the settlement rounds the difference up to whole pieces |
| The shared contract suite assumes an idle player can survive | [ADR 0017](../docs/adr/0017-ecslite-battle-simulation.md) | `RunToEnd` never moves, so the EcsLite derived class supplies HP + a churning load through the benchmark knobs; do not "fix" a failing contract case by relaxing it |
| There is only one `IBattleFrame` implementation | [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md) | The fake and the EcsLite session must share `BattleFrameBuffer`, or the presentation validated against the fake stops predicting the real battle |
| A cooldown on the frame is display state, and a tick is atomic | [ADR 0023](../docs/adr/0023-cooldown-state-on-the-battle-frame.md), [`systems/battle-contract.md`](systems/battle-contract.md#the-cooldown-list-stage-08b) | `Cooldowns` is built from `Caster` components that already existed and is deliberately **not** hashed: an entry that casts reads `1 → PeriodTicks` in one step (the clock hit zero and the cast reset it in the same tick), so `0` means *blocked*, not *about to cast*. A HUD must not infer a cast from a 1 → 0 step, and must match `InstanceId` (the board piece) rather than assume entry order after a bag edit |
| A `$symbol: value` member of a `.jslib` is dropped from the build | [`gotchas/jslib-dollar-symbols-are-dropped.md`](gotchas/jslib-dollar-symbols-are-dropped.md) | The generated framework references the name and never defines it, so the first read throws `$symbol is not defined` and halts the player; initialise it with a `typeof` guard instead |
| A size-profile build leaves its player settings behind | [`gotchas/webgl-size-profile-settings-residue.md`](gotchas/webgl-size-profile-settings-residue.md) | `WebSizeProfiles.ApplyAndPersist` persists `exceptionSupport: None` + stripping High; the next Release build inherits them and halts at boot with `Uncaught undefined` (no message — that is what exceptions-off looks like) |
| A green test run may have run a stale assembly | [`stage-log.md`](stage-log.md#notes-carried-forward) | A test-assembly compile error makes `run_tests` report the previous assembly's pass count with `success: true`; check the new test is in the results (or `compilationFailed`) |
| A scene must publish its own name | [`systems/unity-shell.md`](systems/unity-shell.md) | `__projectSurviveScene` is the only way a browser test observes a handover; a scene that forgets looks like a boot failure |
| A destroyed Unity object is not a `null` reference | [`systems/battle-presentation.md`](systems/battle-presentation.md#11-facts-that-cost-time-to-learn) | Scene unload destroys the pooled renderers before `OnDestroy` releases them, so `view != null` passes and the next line throws; compare through the `UnityEngine.Object` operator |
| The view must copy the frame before the next `Step` | [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md), [`systems/battle-presentation.md`](systems/battle-presentation.md#3-the-frame-copy-is-not-optional) | `BattleFrameSnapshot` is that copy, and it is taken inside the frame that stepped — rendering a live span is rendering a slot being overwritten |
| Pool size is per kind, the frame cap is per frame | [`systems/battle-presentation.md`](systems/battle-presentation.md#5-pooling-nothing-is-created-after-warm-up-and-that-is-measured) | 1793 pooled views against `MaxRenderEntities` 1024: the pool must be the larger or a legal frame asks for a view that does not exist |
| `.unity` YAML is not hand-editable | *(pending)* | Scenes/prefabs are created via the Editor API only |
| A `CanvasScaler`'s virtual canvas **follows the aspect**, so 1920-wide arithmetic is not centred off 16:9 | [`systems/loop-and-scopes.md`](systems/loop-and-scopes.md#5-facts-that-cost-time-to-learn) | At the template's own 960×600 the reference rect is 1821.6 units wide, so a control placed 760 px from the canvas' top-left corner sits 26 px right of the centre while centre-anchored labels stay centred. Anchor centred content at `(0.5, 0.5)`; `ProjectSetup.CreateUiButton`'s optional `anchor` is how |
| A layout report written in the frame a canvas resizes is a frame early | [`systems/loop-and-scopes.md`](systems/loop-and-scopes.md#5-facts-that-cost-time-to-learn) | `Screen` reports the new size in the frame the element changed; the canvas lays its widgets out of it during that frame's render. Publishing once per size change publishes the **new size with the old geometry** and stops — the title's director republishes while the point it published and the point the view reports disagree |

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
