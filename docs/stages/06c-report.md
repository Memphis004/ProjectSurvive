# Stage 06c report — a paid re-roll refills a rented shelf

- **Date:** 2026-10-19
- **Prompt:** change ADR 0016 §2's shop rule so a paid เสี่ยงเซียมซี (`RefreshShop`) also fills the
  shelves that were rented in this sala.
- **ADR:** [0026 — A paid re-roll refills a rented shop slot](../adr/0026-a-paid-reroll-refills-a-rented-slot.md)
  (supersedes the roller-skip half of [ADR 0016](../adr/0016-run-settlement-and-shop-commit-boundaries.md) §2).
- **System note:** [`llmwiki/systems/run-shop.md`](../../llmwiki/systems/run-shop.md) §3.1–3.4.

## 1. Summary

A camp used to keep a rented shelf empty for the whole visit: `ShopSlot.MarkPurchased()` cleared the
offer, and `ShopRoller` skipped it through `CanBeRerolled = !IsLocked && !IsPurchased`, so the paid
re-roll could never put anything back. Rent two of four shelves with 15 bia and half the sala was
dead. The GDD's "เช่าแล้ว … ไม่เติมสินค้าให้ฟรี" is about the **rent** — the piece was taken for the
price paid — while the re-roll is itself a paid command, so the two are not the same thing.

`CanBeRerolled` is now `!IsLocked`, and the roller calls a new `ShopSlot.BeginReroll()` on every slot
it is about to fill: the rent flag and the offer go together, and the roller then writes a fresh
offer or — if the candidate pool ran out — leaves the slot **empty and unrented**. A shelf that has
not been re-rolled is unchanged (rented, empty, a second rent refused), and `SetSlotLocked` now
refuses an empty slot as well as a rented one. Nothing about the price, the shop stream or the
atomicity of `RefreshShop` moved.

## 2. Acceptance, item by item

| # | Acceptance | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | Buying shelf 0, then a paid re-roll, leaves shelf 0 with a fresh offer and no rent flag | **PASS** | `ShopStockTests.A_paid_reroll_refills_a_rented_slot_and_clears_its_rent` (dotnet, real tables); `RunFlowEditModeTests.A_paid_reroll_refills_the_rented_slot_while_a_second_rent_is_refused` (shipped `Resources/Config`) |
| 2 | A locked shelf keeps its piece, its price and its pin, and is not counted in `Filled` | **PASS** | `ShopStockTests.A_paid_reroll_refills_rented_shelves_but_keeps_a_pinned_one_exactly_as_it_was`; `Refresh_costs_five_then_seven_and_keeps_pinned_slots` (price ladder, unchanged) |
| 3 | Renting all four shelves, then one paid re-roll, gives four offers again and clears `IsSoldOut` | **PASS** | `ShopStockTests.Renting_the_whole_panel_and_paying_for_a_reroll_brings_back_four_offers` |
| 4 | Locking a rented or empty slot is still refused, and a second rent still refuses and charges nothing | **PASS** | `ShopStockTests.Pinning_a_rented_slot_is_refused`; the same EditMode case above (`shop.slot_purchased`); `shop.slot_empty` now guards a pin on an empty slot |
| 5 | A purse that cannot pay the re-roll changes no state at all | **PASS** | `Refresh_costs_five_then_seven_and_keeps_pinned_slots`, extended: ids, prices and `ComputeStateHash()` are identical before and after the refused re-roll |
| 6 | Same seed and same commands give the same offers (rent + re-roll included) | **PASS** | `ShopStockTests.The_same_seed_and_state_roll_the_same_offers`, extended with a rent and a re-roll on two runs of one seed |
| 7 | A replay lands on the same run-state hash | **PASS** | `ReplayTests.A_full_run_replays_from_its_log_to_the_same_state_hash` (its log already carries rent + re-roll + layout, so it exercises the new rule); EditMode replay case |
| 8 | PlayMode: rent from a shelf, click the drum, that shelf holds a new piece | **PASS** | `CampWorldPlayModeTests.Clicking_the_drum_refills_a_shelf_that_was_just_rented` — [artifacts/stage06c/playmode-campworld.json](../../artifacts/stage06c/playmode-campworld.json), class **9/9** |
| 9 | No new balance number; prices, RNG order and atomicity unchanged | **PASS** | No `data/` change at all; sim digests identical to before (`shopDigest=0x2d476321` in [artifacts/stage06c/test-fast.log](../../artifacts/stage06c/test-fast.log)) |
| 10 | Tests that asserted the old rule are rewritten, not deleted | **PASS** | `A_refresh_never_refills_slots_that_were_rented` → the two refill cases; `Renting_every_slot_sells_the_panel_out` → `…_brings_back_four_offers`; the EditMode case renamed and re-asserted |

## 3. Commands run

```powershell
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
# Unity Editor (open, MCP): run_tests per class
unity command run_tests --mode playmode --filter CampWorldPlayModeTests --filter_type testName --async_tests
unity command run_tests --mode playmode --filter CampBagDragPlayModeTests --filter_type testName --async_tests
unity command run_tests --mode playmode --filter CampMergeChooserPlayModeTests --filter_type testName --async_tests
unity command run_tests --mode playmode --filter ResultScenePlayModeTests --filter_type testName --async_tests
```

Results: dotnet **511/511**, `-Suite Fast` **PASS 10 | FAIL 0 | NOT RUN 3 | WARN 0** (82.4 s, EditMode
**155/155**), PlayMode per class — CampWorld **9/9**, CampBagDrag **12/12**, CampMergeChooser **4/4**,
ResultScene **3/3**. Logs: [artifacts/stage06c/test-fast.log](../../artifacts/stage06c/test-fast.log),
[playmode-campworld.json](../../artifacts/stage06c/playmode-campworld.json),
[playmode-resultscene.json](../../artifacts/stage06c/playmode-resultscene.json).

## 4. Not tested, and the risks

- **No screenshot of the refilled shelf was taken.** The presentation acceptance above is a
  PlayMode assertion (real pointer events, the scene's own views), not an eye check: the shelf's
  rendering path is unchanged — it draws whatever `CampShopSlotModel` publishes — but the rule change
  is player-visible (a shelf that used to stay "เช่าแล้ว" for the rest of the sala now shows a piece
  again), so this is called out rather than counted as a visual PASS.
- **WebGL build and Playwright are NOT RUN.** This stage is not one of the browser gates (10, 12, 14,
  15) and no shipped browser spec casts the sticks after a rent, so nothing on the web path changed —
  but that is an argument, not a measurement. The camp spec's own rent assertions (`camp.spec.ts`
  "the rented shelf is empty") are unchanged and still true immediately after a rent.
- **The new `shop.slot_empty` guard on `SetSlotLocked` has no direct test that reaches it** with
  shipped data: the panel's pool covers every floor, so an empty *and* unrented shelf is only
  reachable through the roller's explicit-pool overload, which bypasses `RunSession`. The rented case
  (which is also `IsEmpty`) is asserted.
- **A first re-roll can re-offer the very piece that was just rented** — the roller no longer knows
  which piece a shelf held, and it draws without replacement only *within* one roll. This is a
  deliberate consequence, not a defect; the tests assert the offer is fresh and priced, not that its
  id differs.
- **PlayMode was run class by class, not as a bulk run**: the bulk PlayMode suite has the documented
  ambient Input System log-assert flake ([docs/testing/test-suites.md](../testing/test-suites.md) §7),
  unrelated to this change.
- **One dotnet run reported 1 failure that never reproduced**: it happened while a Unity PlayMode
  run was in flight on the same machine, and the suite then passed **14 consecutive runs**
  (511/511 each). The failing test's name was not captured by the filtered output, so this is
  recorded as an observed flake rather than as a pass.
- One environment note: the Editor was left in **Play Mode** when the first `-Suite Fast` was
  attempted on this machine, which makes the EditMode run report 0/1 (the test runner cannot save or
  restore scenes during play). The run above is the one taken with the Editor idle; EditMode was
  re-run after the last source edits and reported **155/155** again.
