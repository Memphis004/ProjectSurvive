# Stage 06 — Shop, economy, run and scene flow

- **Date:** 2026-10-14
- **Prompt:** “ทำ Stage 06 เท่านั้น: Shop, Economy, Run และ Scene Flow (ใช้ FakeBattleSession จาก Stage 02b
  แทนผล Battle)”
- **Result:** the run exists as tested Domain + Application code — a wallet, a four-slot camp shop fed
  by the `shop_pool` table, an atomic purchase/sell/re-roll surface, a departure gate, a one-shot floor
  settlement, and a two-level replay that rebuilds a whole run from its seed plus its commands. No
  battle simulation, no scene, no Camp UI: those are Stages 07–09. Every number still comes from
  `data/`; this stage changed **no** data row.

## 1. What was built

**The phase machine.** `RunFlowState` (`Title` / `Camp` / `Battle` / `Result`) is the run's rule phase
and `SceneId` is the loadable scene, joined by **one explicit function**, `SceneFlowMap.SceneOf`, which
throws on an unknown state instead of letting a new phase inherit whatever scene shares its number.
Both enum's values are hash input, so they are fixed. Camp is the only phase in which money, the bag
or the shop may move; every camp command asks, and answers `run.wrong_state` otherwise.

**The wallet.** `Wallet` is run-scoped, single-currency, and the only place bia changes. `TrySpend` is
atomic — the whole amount leaves or nothing does — `CanAfford` is the question a command asks *before*
it does anything, and `Credit` ignores a negative amount rather than inventing a debt.

**The shop.** `ShopStock` is created once, in the run constructor; entering Camp calls `BeginCamp()`
(empty the slots, clear the pins, reset the re-roll counter) and immediately rolls. `ShopRoller` draws
from the `shop_pool` rows whose `floor_min ≤ floor ≤ floor_max` with `weight > 0`, **without
replacement**, so one panel never offers the same piece twice; when the candidates run out the
remaining slots stay **empty** rather than duplicating an offer. The price is copied onto the slot at
roll time (from the `bag` table, else the `item` table), so what the player was shown is what they pay.
A re-roll (`RefreshShop`) costs `refresh_base_cost + increment × refreshes already paid` in the same
camp — 5, 7, 9, … — and replaces **only** the slots that are neither pinned nor already rented, which
is the mechanical form of “ล็อกสินค้าที่อยากเก็บไว้ก่อนสุ่มได้” and “ไม่เติมสินค้าให้ฟรี”.

**Atomic commands.** `RunSession` (Application) is the run's only command surface: `StartRun`,
`OpenShop` (a read — the same stock every time in one camp), `Purchase`, `Sell`, `RefreshShop`,
`SetSlotLocked`, `StartFloor`, `ApplyBattleResult`, `ReturnToTitle`, plus the Stage 05 bag commands
(`MovePiece`, `RotatePiece`, `TransferToChest`, `MergeItems`, `Undo`, `Reset`) which the run now gates
on Camp and records. Every one validates completely and only then mutates, so a rejection leaves the
run byte-identical: the chest is checked *before* the money moves (“หีบเต็มต้องไม่หักเงิน”), tax is paid
after the affordability and range checks, and the piece is added after payment with an in-call refund
if the add still fails. A refused command records nothing and emits nothing.

**The departure gate.** `StartFloor` resolves the loadout and refuses with **`run.no_arcane_weapon`**
when it carries no usable arcane weapon — the reason code Stage 09 shows on the disabled button — then
draws exactly one battle seed from the run's own battle stream, builds the `BattleSetup`, locks the bag
(`Editor.SetLocked(true)`, “ล็อกย่ามระหว่าง Battle”) and moves to Battle.

**Settlement.** `ApplyBattleResult` is “CompleteFloor”: it validates the consumable bookkeeping first,
then credits the battle's currency, clamps HP, removes the used offerings, adds the clear heal
(`(MaxHp × economy.heal_percent_on_clear) / 100`, capped at the remaining headroom), unlocks the bag,
and either returns to Camp (re-rolling the shop for the new visit) or ends the run at Result. One
payout per floor is guarded by **the phase plus the pending setup**, not by comparing ids; the
settlement id is a report (`FloorSettlement.SettlementId`, `RunState.LastSettlementId`), which the
state hash sees move.

**Replay, two levels.** `ReplayLog` (run) records the seed, the accepted commands and one Stage 02b
`BattleReplay` per started floor, and rebuilds a run by re-applying the commands and re-running each
floor's recording through an `IBattleSessionFactory` — the same two steps `SceneFlow` performs live. A
`BattleResult` is never recorded: a stored outcome can disagree with the simulation, a recording
cannot. The log throws when it stops reproducing its own run, because that is a recorder bug, not a
player failure.

**The seam.** `SceneFlow` is the only place a battle session is created, from the
`IBattleSessionFactory` it was handed; `RunSession` never names a session type. Two architecture tests
keep it that way (see §7).

**What verification caught.** Two real bugs, both in the “atomic + honest state” family:

1. **A rented slot still advertised its piece.** `ShopSlot.MarkPurchased` only raised the flag, so a
   re-roll correctly skipped the slot but the panel still showed a rentable-looking offer whose
   `Purchase` would fail with `shop.slot_purchased`. `MarkPurchased` now also clears the offer, and
   `Purchase` captures the id/price *before* the commit so the outcome and the event still name what
   was rented.
2. **A desynced `BattleResult` was accepted.** `RemainingConsumables` is aligned with the loadout, but
   the settlement iterated `min(counts)`, so a result claiming two offering slots while the run carried
   one was settled happily — paying HP, bia and the heal for a floor whose bookkeeping the run does not
   understand. A length mismatch now refuses the whole settlement with `inventory.not_enough_pieces`
   before anything is paid.

Both are recorded as decisions in [ADR 0016](../../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md),
together with “the settlement id is not the guard” and “the composition root deliberately binds no
battle session yet”.

## 2. Files that matter

| File | What |
| --- | --- |
| `lib/Runtime/Domain/Run/Wallet.cs` | The purse; the only place bia changes |
| `lib/Runtime/Domain/Run/RunFlowState.cs`, `SceneId.cs` | The phase enum and the explicit phase → scene map |
| `lib/Runtime/Domain/Run/RunCommand.cs` | The recorded command vocabulary and every input that does not come from the seed |
| `lib/Runtime/Domain/Run/RunEvent.cs` | Presentation event kinds (never a rule input) |
| `lib/Runtime/Domain/Run/RunState.cs` | The hashable picture of a run (`ComputeHash()`) |
| `lib/Runtime/Domain/Run/RunOutcomes.cs`, `FloorSettlement.cs` | Typed results: `PurchaseOutcome`, `RefreshOutcome`, `FloorSettlement` |
| `lib/Runtime/Domain/Run/RunDefaults.cs` | The starting kit's **ids and placement** only (numbers stay in `data/`) |
| `lib/Runtime/Domain/Run/RunError.cs` | `run.*` codes, `wallet.insufficient_bia`, and two named preconditions |
| `lib/Runtime/Domain/Shop/ShopStock.cs` | Slots, the re-roll counter and price ladder, camp reset, sold-out reads |
| `lib/Runtime/Domain/Shop/ShopSlot.cs` | One slot; a rented slot is emptied and never refilled |
| `lib/Runtime/Domain/Shop/ShopRoller.cs` | Weighted draw without replacement; id → price resolution |
| `lib/Runtime/Domain/Shop/ShopError.cs` | `shop.*` codes |
| `lib/Runtime/Application/Run/RunSession.cs` | The whole command surface and the atomicity of each command |
| `lib/Runtime/Application/Run/SceneFlow.cs` | Camp ↔ Battle driving; the only place a session is created |
| `lib/Runtime/Application/Run/ReplayLog.cs` | Seed + commands + one `BattleReplay` per floor; the replay itself |
| `lib/Runtime/Domain/Inventory/BackpackEditor.cs` | `CanConsume` / `Consume` added for the settlement (Stage 05 module) |
| `lib/Tests~/Core.Tests/Run/*` | 42 cases over the real tables: run economy, shop, flow, replay |
| `UnityProject/Assets/Main/Tests/EditMode/RunFlowEditModeTests.cs` | 4 cases through the shipped `Resources/Config` tables |
| `lib/Tests~/Core.Tests/ArchitectureTests.cs` | 2 new tests: no economy decision in Presentation; battle only via the factory |
| `docs/adr/0016-run-settlement-and-shop-commit-boundaries.md` | The four boundary decisions above |
| `llmwiki/systems/run-shop.md` | The system note (phases, wallet, shop, settlement, replay, streams, file map) |
| `knowledge.md`, `llmwiki/index.md`, `llmwiki/glossary.md` | Status, links and the Stage 06 term tables |

## 3. Commands actually run

```powershell
# Core: build + xUnit + Sim determinism + the data gates
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1

# Focused during development
dotnet build "lib/Tests~/ProjectSurvive.Core.sln"
dotnet test "lib/Tests~/ProjectSurvive.Core.sln"
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~ArchitectureTests"
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~Run."

# Coverage of the Stage 06 rule set (not a gate this stage; measured for the report)
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" --collect:"XPlat Code Coverage" `
  --settings "lib/Tests~/coverage-domain.runsettings" --results-directory artifacts/coverage-06-domain
node tools/coverage-report.mjs artifacts/coverage-06-domain/*/coverage.cobertura.xml "Run/" "Shop/"
```

Unity (Editor `6000.3.9f1`, through the Editor bridge): a script recompile, `list_tests`, then the
EditMode suite and the PlayMode suite (`--async_tests`, polled to completion).

## 4. Results and where the artifacts are

| Gate | Result | Evidence |
| --- | --- | --- |
| `tools/test.ps1` | `PASS 10 / FAIL 0 / WARN 0 / SKIP 0` — build with **no warnings**, **385 tests**, Sim determinism, the three streams separated, and the three data gates | console; `artifacts/dotnet/test-report.txt`, `core-tests.trx`, `verify-generated.log`, `validate-data.json`, `check-fixtures.log`, `sim-full.txt` |
| Core test count | **385** total, **44** of them Stage 06 (42 in `Run/`, 2 architecture) | the run above |
| Coverage of the Stage 06 rule set | **88.47 %** (376/425 lines across `Domain/Run/*` + `Domain/Shop/*`); `Wallet`, `ShopStock` and `SceneId` at 100 %, `ShopRoller` 98.82 % | `artifacts/coverage-06-domain/…/coverage.cobertura.xml` + `coverage-report.mjs` |
| Unity EditMode | **33/33** pass (4 new `RunFlowEditModeTests`) | Editor bridge test run |
| Unity PlayMode | **4/4** pass (unchanged — this stage adds no play-mode code) | Editor bridge test run |
| Unity compile | clean: `compilationFailed: false`, no new console errors | `recompile_status` / `console_status` |
| Data | **no `data/` change this stage** — `verify-generated.ps1` reports regeneration reproduces the tracked output byte-for-byte | `artifacts/dotnet/verify-generated.log` |

The uncovered lines are the value types' `ToString`/`Equals`/`GetHashCode` bodies
(`RunCommand`, `RunEvent`, `RunOutcomes`, `ShopSlot`, `FloorSettlement`, `RunState`) and one defensive
no-op guard; the rule branches are covered. Two of them were uncovered until this report was being
written (`TrySpend` with a negative amount, and a pool row resolving to neither table), which is why
the suite grew from 383 to 385.

## 5. What was not tested, and why

1. **No WebGL build, no browser, no Playwright run.** Stage 06 adds no scene, no prefab and no UI, so a
   player build would exercise the same rule code the EditMode suite already runs under Unity's
   compiler. The size and frame-timing gates remain Stage 03b/15 work.
2. **No performance number is claimed.** Nothing in this stage is a hot path (`RunSession` runs on a
   command, not per tick, and `ReplayLog` is explicitly allowed to allocate). *No figure in this report
   is a measured performance result.*
3. **The determinism guarantee is one runtime deep.** “Same seed and state → same offers” and “a replay
   lands on the same hash” are asserted inside a single runtime; nothing here claims bitwise equality
   across browsers or CPUs (AGENTS rule 6).
4. **The Unity composition root is not wired to the run.** Nothing resolves `RunSession`, `SceneFlow` or
   `IBattleSessionFactory` from the container, so “the container can hand a run its battle factory” is
   **NOT RUN** — the run and the flow are constructed by the tests. Deliberate, with the reason in
   [ADR 0016](../../docs/adr/0016-run-settlement-and-shop-commit-boundaries.md).
5. **No Camp UI exists**, so the `run.*` / `shop.*` / `wallet.*` codes are not rendered; their Thai
   strings are Stage 09 work. The localization table was not touched.
6. **`run.no_floor_left` and `run.unknown_floor` are not triggered by a test.** They are named
   preconditions (a guard on the floor cursor, and a degenerate floor table) rather than refusals a
   caller can reach; an invariant test instead proves every Camp the run enters still has a floor to
   start. `run.floor_already_settled` was **deleted** in this stage for exactly this reason: it named a
   state that `run.not_in_battle` already covers accurately.
7. **`ProjectSettings.asset` is dirty in the working tree and is deliberately *not* part of this
   commit.** `WebSizeProfilesTests` applies a size profile to the real `PlayerSettings`, so running the
   EditMode suite rewrites it. Pre-existing (Stage 03/05) test side effect, not a Stage 06 decision.

## 6. Blockers and risks

- **No blocker.** No Unity license, browser or network was needed beyond the already vendored toolchain.
- **Risks accepted, with reasons:**
  1. **The starting kit's ids are code, not data.** `RunDefaults` holds `bag.raw_cotton_bag`,
     `item.kali_flame_yant` and their placement because no table describes a starting loadout; the
     *numbers* (15 bia, board size) are read from `economy`/`constant`. The right home for a starting
     kit is a content table, and this class becomes the place it is read into when one exists.
  2. **A rented slot no longer says what it sold.** Clearing the offer is what makes `IsEmpty` mean
     what it says and keeps a rented slot from looking rentable, but a Stage 09 panel that wants
     “what was rented here” must keep the `PurchaseOutcome` (or show “เช่าแล้ว” with no name). ADR 0016.
  3. **`ShopStock`'s commit methods are public and trusting by design.** `MarkPurchased`,
     `MarkRefreshed` and `SetSlotLocked` assume the run has already validated and paid; they can never
     move money, but a caller outside `RunSession` could desync the panel from the purse. There is
     exactly one caller, and no other consumer exists yet.
  4. **`ReplayLog.Replay` throws on divergence.** Correct for a verification path; Stage 12 must not
     build a player-facing load on it, and its DTO-shaped checkpoint is separate for that reason.
  5. **Economy balance is unspecified beyond the prototype.** The re-roll ladder (5, 7, 9) and the
     15 % clear heal come from `data/`, but nobody has played a run: whether 15 bia supports the first
     shop visit is a Stage 11 balancing question, not a rule.

## 7. Acceptance criteria

| # | Criterion | Verdict |
| --- | --- | --- |
| 1 | Wallet, `RunSession`, `RunFlowState` (Title → Camp → Battle → Camp → … → Result) | **PASS** — `Domain/Run` + `Application/Run/RunSession`; `The_flow_phase_picks_the_scene`, `Commands_out_of_order_are_refused_and_change_nothing` |
| 2 | Weighted roll from config, its own RNG stream, stock created once per camp, 4 slots, lock, re-roll at 5 with +2 per re-roll in the same camp | **PASS** — `ShopRoller` / `ShopStock`; `Every_offer_comes_from_the_pool_row_for_that_floor_and_no_piece_repeats`, `Re_opening_the_shop_returns_the_same_stock`, `Refresh_costs_five_then_seven_and_keeps_pinned_slots`, `A_refresh_moves_the_shop_stream_but_a_re_open_does_not` |
| 3 | `Purchase` / `Sell` / `Refresh` atomic; into the chest; a full chest must not take the money; the same slot twice; a rented slot never refills free | **PASS** — `Renting_pays_the_table_price_and_lands_the_piece_in_the_chest`, `A_full_chest_refuses_the_rent_without_taking_the_money`, `An_expensive_rent_leaves_the_purse_and_the_shop_untouched`, `Renting_the_same_slot_twice_only_works_once`, `A_refresh_never_refills_slots_that_were_rented`, `Renting_every_slot_sells_the_panel_out` |
| 4 | `StartFloor` validation (≥ 1 usable arcane weapon, reason code for the UI) and the bag locked during Battle | **PASS** — `A_floor_cannot_start_without_a_usable_arcane_weapon` asserts `run.no_arcane_weapon`; `The_bag_is_locked_between_departure_and_settlement` |
| 5 | `CompleteFloor` pays once (settlement id) and heals 15 % | **PASS** — `A_settlement_pays_the_battle_currency_exactly_once` (+ `not_in_battle` on the second call), `Clearing_a_floor_heals_the_data_percentage_capped_at_max_hp`, `A_cleared_floor_with_full_hp_does_not_heal_past_the_cap`; the guard's shape and the id's role are ADR 0016 |
| 6 | `SceneId`, `SceneFlow` state machine, command/event, `ReplayLog` (command + seed) | **PASS** — `SceneId`/`SceneFlowMap`, `RunCommand`/`RunEventKind`, `ReplayLog`; `The_log_starts_empty_and_records_only_accepted_commands` |
| 7 | Initial loadout: ผ้าดิบ bag + ยันต์เพลิงกาลี + 15 bia | **PASS** — `The_run_starts_with_the_data_balance_the_starting_bag_and_one_arcane_weapon` (bag and weapon from `RunDefaults`, bia from `economy.start_bia`) |
| — | `SceneFlow` takes `IBattleSessionFactory` through a port; a scripted run uses the fake factory | **PASS** — constructor injection; the test's own `ScriptedBattleSessionFactory` implements the port; `SceneFlow_requires_its_inputs` |
| — | `ApplyBattleResult(BattleResult)` updates HP / money / consumables and heals 15 % of MaxHp capped at Max on a win | **PASS** — `A_settlement_pays_the_battle_currency_exactly_once`, `A_used_offering_leaves_the_bag_at_settlement`, `A_consumable_the_bag_cannot_cover_refuses_the_whole_settlement`, `Clearing_a_floor_heals_the_data_percentage_capped_at_max_hp` |
| — | `ReplayLog` uses `BattleReplay` as a sub-part; no second replay mechanism | **PASS** — `A_recorded_floor_replays_through_the_factory_to_the_same_battle_hash`; `BattleReplay` is reused unchanged |
| — | Test: a scripted 5-floor run (winning) ends at Result, and the log replays to the same `RunState` hash | **PASS** — `A_scripted_run_clears_five_floors_and_ends_at_the_result_scene`, `A_full_run_replays_from_its_log_to_the_same_state_hash`, `A_defeated_run_also_replays_to_the_same_state_hash`; the same two assertions run in Unity over the shipped tables |
| — | Test: seed + state → the same offers; re-opening the shop changes nothing; a re-roll moves the RNG state and costs the right price | **PASS** — `The_same_seed_and_state_roll_the_same_offers`, `Re_opening_the_shop_returns_the_same_stock`, `A_refresh_moves_the_shop_stream_but_a_re_open_does_not` |
| — | Test: not enough money or a full chest changes no state; one slot bought twice is bought once | **PASS** — both `…leaves_the_purse_and_the_shop_untouched` cases and `Renting_the_same_slot_twice_only_works_once` (asserted on the purse, the revision and the panel) |
| — | Test: a repeated battle result does not pay twice; a wrong-order transition is refused | **PASS** — `A_settlement_pays_the_battle_currency_exactly_once`, `Entering_a_floor_twice_is_refused_until_the_first_is_settled`, `Commands_out_of_order_are_refused_and_change_nothing` |
| — | Test: a run with no arcane weapon cannot start a battle | **PASS** — `A_floor_cannot_start_without_a_usable_arcane_weapon` |
| — | Acceptance: **no economy logic in a Presenter** | **PASS** — no Camp Presenter exists yet, and `ArchitectureTests.Presentation_sources_do_not_decide_economy_numbers` scans every file under `Assets/Main/Scripts/Presentation/**` for the *decisions* (`ShopRoller`, `SynergyEvaluator`, `LoadoutResolver`, `TrySpend(`, `RefreshCost(`, `SellBackPercent`, `HealPercentOnClear`), green today and a guard for Stage 09 |
| — | Acceptance: **the battle is reached only through `IBattleSessionFactory`** | **PASS** — `SceneFlow` is the only creator; `ArchitectureTests.Battle_sessions_are_reached_only_through_their_factory_port` forbids naming `FakeBattleSession` outside `Application/Battle/Fake/` in `lib/Runtime` and in the Unity scripts |
| — | Acceptance: a floor replayed through `BattleReplay` gives the same `ComputeStateHash` | **PASS** — `A_recorded_floor_replays_through_the_factory_to_the_same_battle_hash` |
| — | Compile + tests (DoD) | **PASS** — `tools/test.ps1` `PASS 10 / FAIL 0` with **no build warnings**; Unity EditMode **33/33**, PlayMode **4/4** |
| — | Commit `stage-06:` | **PASS** — see the commit carrying this report |

**Gate that mattered most:** exercising the full command surface over the *real* tables rather than a
stub config. It is what caught the two bugs in §1 — a rented slot that still advertised its offer, and a
settlement that accepted a battle result disagreeing with the loadout — neither of which a
hand-written config would have produced.
