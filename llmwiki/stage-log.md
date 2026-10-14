# Stage log

A one-paragraph-per-stage running log. The full report for each stage lives in
[`../docs/stages/`](../docs/stages/); this file is the fast "what landed, in what
order" view.

| Stage | Landed | What it added | Report |
| --- | --- | --- | --- |
| 00 | 2026-10-13 | repo skeleton, `AGENTS.md`, `knowledge.md`, docs, `tools/doctor.ps1`, `versions.lock.json`, dependency matrix | [`00-report`](../docs/stages/00-report.md) |
| 01 | 2026-10-13 | `lib/` UPM package + six layer asmdefs + vendored EcsLite, the `dotnet` harness, `Shared` primitives (`IRandomSource`/`Xoshiro128`/`RandomStreams`, `IClock`, `FixedTicker`, `Result`/`Error`) | [`01-report`](../docs/stages/01-report.md) |
| 02 | 2026-10-13 | Luban 5.1.0 + protoc 33.6, `data/` (12 tables), generated C#, `IGameConfig` port + adapter, config hash, 4 gate tools, 12 broken fixtures | [`02-report`](../docs/stages/02-report.md) |
| 02b | 2026-10-14 | **Battle contract v1**: `Domain/Combat/Battle` interfaces + `BattleEventRing` + `BattleReplay`, `FakeBattleSession` in Application, `BattleSessionContractTests<TFactory>`; amended later (see the note below and [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md)) so the frame is a **Prev/Cur double buffer** with exactly one implementation | [`02b-report`](../docs/stages/02b-report.md) |
| 03 | 2026-10-14 | **Unity shell + first Web build + Thai text decision**: `UnityProject/` on `6000.3.9f1`/URP referencing `lib/` as a local package; Boot/Title/ThaiTextSpike scenes; 7-probe smoke suite published to the page; Playwright suite on the real WebGL build; [ADR 0008](../docs/adr/0008-thai-text-ui-stack.md) (uGUI+TMP, Static baked font assets) | [`03-report`](../docs/stages/03-report.md) |

| 03b | 2026-10-14 | **WebGL payload budget + size baseline**: [ADR 0011](../docs/adr/0011-webgl-payload-budget-and-size-measurement.md) fixes the payload unit and the three bases a size may be quoted in; `WebSizeProfiles` + `-SizeProfile` make each configuration a value applied in-process and recorded in `build-report.json`; the project's own configuration measured first (60.46 MiB raw, 18.04 MiB gzip, 12.41 MiB Brotli), then stripping High −26.7 % raw and IL2CPP `OptimizeSize` −23.8 % raw (player 60.46 → 33.75 MiB raw, 18.04 → **13.13 MiB gzip**, 12.41 → **9.70 MiB Brotli**); the recommended build browser-verified (Playwright 2/2); per-assembly attribution shows 67.91 % of the `.wasm` is generic sharing + IL2CPP runtime, so dropping a package is the wrong lever | [`03b-report`](../docs/stages/03b-report.md) + [`docs/build/size-baseline.md`](../docs/build/size-baseline.md) |

| 04 | 2026-10-14 | **Prototype data, `data/` only**: 14 pieces (3 bags, 4 arcane weapons one per cast behaviour, 3 charms, 1 tome, 2 offerings, 1 familiar) each merging to tier 3, a real tag vocabulary behind the 5 GDD combo rules, 4 enemies + 2-phase Pob, the 5-floor run with its wave timeline, a 14-row shop pool, 51 localization rows; `item.footprint` column; the **footprint picture format** (`XX.|.XX`, parsed in Domain) per [ADR 0009](../docs/adr/0009-footprint-picture-format.md); rules for item footprint / unknown tag / incomplete merge ladder / unreachable piece / wave spawn window; fixtures 12 → 17 | [`04-report`](../docs/stages/04-report.md) |

| 05 | 2026-10-14 | **Bag rules, no Unity UI yet**: `Domain/Inventory` — `Board` (9×7 with bag/item owner maps), `Chest` (10), `PieceInstance`/`BagPieceInstance`/`ItemInstance` with `DefId` ≠ `InstanceId`, `PieceRef` handles, `Footprint` rotation + normalisation, `Backpack` (state + `Revision`) and `BackpackEditor` (atomic `Move`/`Rotate`/`TransferToChest`/`Merge`/`Recycle`/`Undo`/`Reset`/`SetLocked`, typed `Result` + `InventoryError.*`); `SynergyEvaluator` (adjacency counted once per pair, row **and** column kinds, power decay both ways, bag-area bonus, cooldown floor) and `LoadoutResolver` → the **Battle v1** `CombatLoadoutSnapshot` cached on the backpack revision; MessagePack snapshot undo through a `Contracts` DTO; `constant.weight_speed_percent_per_unit` added ([ADR 0013](../docs/adr/0013-weight-speed-penalty-curve.md)), editing/ownership [ADR 0014](../docs/adr/0014-backpack-editing-undo-and-placement-ownership.md), `SameColumn` kind [ADR 0015](../docs/adr/0015-same-line-synergy-kinds.md); `SchemaVersion` 2 → 3 | [`05-report`](../docs/stages/05-report.md) |

| 06 | 2026-10-14 | **Run, economy, camp shop and scene flow (no UI, still no battle sim)**: `Domain/Run` — `Wallet` (the only place bia changes), `RunFlowState` + `SceneId` joined by `SceneFlowMap`, `RunCommand`/`RunEvent`, the hashable `RunState`, `PurchaseOutcome`/`RefreshOutcome`/`FloorSettlement`, `RunDefaults` (starting ids only) and `RunError`; `Domain/Shop` — `ShopStock` (4 slots, one stock per camp entry, re-roll 5 then +2 per re-roll in the same camp), `ShopSlot` (a rented slot is **emptied**) and `ShopRoller` (weighted, floor-banded, no repeat, price captured at roll time); `Application/Run` — `RunSession` (the only command surface: `StartRun`/`OpenShop`/`Purchase`/`Sell`/`RefreshShop`/`SetSlotLocked`/`StartFloor`/`ApplyBattleResult`/`ReturnToTitle` plus the Stage 05 bag commands, each validating fully before its first mutation), `SceneFlow` (the only place a battle session is created, through `IBattleSessionFactory`) and `ReplayLog` (seed + accepted commands + one `BattleReplay` per floor); the one-payout, rented-slot, desynced-result and composition-root boundaries in [ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md); no `data/` change at all | [`06-report`](../docs/stages/06-report.md) |

## Notes carried forward

- **Stage 02b was amended after Stage 04** (2026-10-14) to give `IBattleFrame` a
  Prev/Cur pair (`Entities` + `PreviousEntities`) and to make `BattleFrameBuffer` a
  real double buffer — [ADR 0012](../docs/adr/0012-prev-cur-frame-double-buffer.md).
  The trigger was Stage 08's "interpolate Prev → Cur ด้วย alpha" while reading only
  `IBattleFrame`, which a single array cannot satisfy. Contract change, fake, tests
  and GDD DoD lines moved together; no gameplay number changed.
- Stage 02b is an insertion between the original Stage 02 and Stage 03. It does
  not change `data/` and does not open Unity.
- **Stage 05 is the first gameplay rule set**: the bag rules and the combo rules exist and
  are measured (105 dotnet cases, 90.92 % of the backpack rule set, 9 EditMode cases).
  The shop/economy flow and the run/scene flow followed in Stage 06; the battle simulation
  is still absent — the *contract* it implements has existed since Stage 02b.
- **Stage 06 is the run, and still no battle**: the economy and the phase machine are real
  and measured (42 dotnet cases over the real tables + 2 architecture tests + 4 EditMode
  cases over the shipped `Resources` tables). What still does not exist: the battle
  *simulation* (Stage 07), any Camp/Battle/Result scene or UI (Stages 08–10), and a
  composition-root binding for the run — the last one deliberate
  ([ADR 0016](../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md)), so the Stage
  02b fake cannot become the shipped battle by accident.
- Determinism is guaranteed within one runtime only (see the scope gotcha).
- `lib/` is now compiled twice: by `dotnet` (lib/Tests~) and by Unity as the local
  package `com.projectsurvive.core`. A change to a rule-engine file must keep both
  green; `Assets/csc.rsp` exists only to keep the Unity compile quiet about the
  nullable annotations the `dotnet` build already enables.
- **Stage 04 is data only.** No rule, system or scene was added; `lib/` changed only in
  `Domain/Config` (the footprint parser and `ItemConfig.Footprint`), the Infrastructure
  adapter, the validator and tests. `data/` is now the canonical description of the
  prototype's content, so a balance change is a CSV edit, a regeneration and a diff.
- **Item-local effects are still unmodelled** (charm passive size, offering heal/bia
  and uses) — [ADR 0010](../docs/adr/0010-item-local-effects-deferred.md). The numbers
  must land in `data/` with their consumer, never in code.
- **Size is now a measured, defined quantity** ([ADR 0011](../docs/adr/0011-webgl-payload-budget-and-size-measurement.md)):
  the gate (`< 30 MiB` gzip) and the target (`< 15 MiB` Brotli) are met with 5.30 MiB of Brotli
  headroom (17 % on the project's default configuration), and any new size claim must name its
  basis. The "before" player Stage 03 left behind (48.81 MiB) turned out to carry no record of its
  settings and is 12.2 MiB of `.wasm` below the configuration the project actually has, so it is out
  of the table. Only `-SizeProfile` CLI builds count — a
  Build-Settings click rewrites player settings and the shared IL2CPP tree. Nothing yet *enforces*
  the budget with content landing from Stage 04 on; a CI size gate on `measure-web-size.mjs` is the
  follow-up.
- Nothing verifies behavior in a *player* except the browser test. Two Stage 03 bugs
  (stripped DI constructors, emptied font tables) were invisible to Mono-based
  EditMode/PlayMode tests and only the real build caught them — `tests/web/` is the
  gate for anything IL2CPP-, AOT- or asset-serialization-sensitive.
