# Stage 05 — Inventory domain (bag rules, combos, bag → loadout)

- **Date:** 2026-10-14
- **Prompt:** “ทำ Stage 05 เท่านั้น: Pure Inventory Rules (ไม่มี Unity ยังไม่มี UI)”
- **Result:** the bag rules and combo rules exist as tested Domain code. No Unity *UI*, no
  shop, no run flow and no battle simulation were added — the stage's scope is the rules
  plus the one contract the battle already had.

## 1. What was built

**Placement.** `Board` (9×7 from `constant`) keeps two owner maps — bag owner per cell and
item owner per cell — so “outside the board”, “bags overlap”, “item not covered” and “items
overlap” are four distinct typed failures instead of one generic refusal. A bag's cells are
the **ActiveSlots**; an item needs *every* one of its cells covered (it may straddle several
bags). A bag may be moved, rotated, stored or recycled **only if no placed item loses
coverage**, and a command that would strand one is refused **whole**.

**Shapes.** A footprint is a set of `CellOffset` authored as a picture string
(`"XXX|XXX|XXX"`, `"X.|X.|XX"` — ADR 0009) and normalised on parse. `FootprintRotation`
turns a shape a quarter turn clockwise on screen (`(x,y) → (-y,x)`) and re-normalises it;
four turns return the authored shape, and the L-tetromino bag
(`bag.wicker_basket_l`) proves an asymmetric shape really changes cells at 90°/270°.

**Identity.** `DefId` (permanent, data-owned) is separate from `InstanceId` (this run's
handle, never reused). Commands address a piece by `PieceRef(PieceKind, InstanceId)`, so a
bag id and an item id can never be exchanged by accident. Merge is the case that makes the
split necessary.

**Commands** (`BackpackEditor`; `Backpack` owns state and its mutators are `internal`):
`Move`, `Rotate`, `TransferToChest`, `Merge` (same id + same tier → tier + 1, capped by
`constant.max_tier`), `Recycle` (`floor(base price × economy.sell_back_percent / 100)`),
`Undo`, `Reset` (back to the state captured by `MarkCampEntry`, i.e. “คืนสภาพตอนเข้าศาลา”)
and `SetLocked` (freezes every edit *including* undo). Every command validates fully before
its first mutation, logs undo only on success, and returns `Result`/`Result<T>` carrying a
stable `inventory.*` code. **Undo and Reset are MessagePack snapshots** — the state is
captured through `IBackpackSnapshotCodec`, encoded in Infrastructure over a `Contracts` DTO
(`BackpackSnapshotDto`), so `Domain` never sees a serializer and Stage 12 gets checkpoint
bytes for free.

**Weight.** `WeightPenalty` is a linear slope: `constant.weight_speed_percent_per_unit`
percent per weight unit, clamped to `constant.max_speed_penalty_percent`; chest pieces
weigh nothing. Both numbers are data (ADR 0013), and `PlayerStatBlock.SpeedPenaltyPercent`
publishes the result so nothing re-derives it.

**Combos.** `SynergyEvaluator` applies the `synergy` table to a layout in **one pass**:
`AdjacentPair` (4 directions, no diagonals), `SameRow`, `SameColumn` and `BagArea`. Distinct
partners are counted and clamped to the rule's `max_stacks` (“ติดกันหลายช่องนับครั้งเดียว” +
“ซ้อนได้สูงสุด N ชั้น”), same-stat percentages add, `AttackSpeed` flips sign once (it shortens
a cooldown), and `Debuff` hits **both** pieces as the GDD says. Nothing a rule writes can
enable another rule, so there is no fixed point to diverge.

**Loadout.** `LoadoutResolver` is the only bridge from the bag to the battle: it returns the
**Battle v1** `CombatLoadoutSnapshot` (Stats / Weapons / Summons / Consumables) with tier and
combo multipliers already applied, cooldowns floored at `constant.min_cooldown_seconds`,
chest pieces excluded. It is cached against `Backpack.Revision`, which only an *applied*
command moves — combos are recomputed when the set changes and not per tick. Ordering is
canonical (board position for weapons/summons, id for consumables), so no rule reads a
dictionary in iteration order.

**Two gaps closed, one ambiguity settled.** Stage 04 flagged the missing weight → speed
slope; ADR 0013 fixes its shape (linear, two data numbers). The GDD's table says the tome
combo is *row* while the Stage 05 prompt says *row and column*; ADR 0015 adds `SameColumn` as
a real, tested data kind and ships **no** programmatic row for it, so no balance number moves.

## 2. Files that matter

| File | What |
| --- | --- |
| `lib/Runtime/Domain/Inventory/Backpack.cs` | State aggregate: board, chest, pieces, `Revision`, derived-state rebuild |
| `lib/Runtime/Domain/Inventory/BackpackEditor.cs` | Command surface: validation, atomicity, undo/Reset/lock, typed outcomes |
| `lib/Runtime/Domain/Inventory/Board.cs`, `Chest.cs` | Placement maps, ActiveSlots, 10 chest slots |
| `lib/Runtime/Domain/Inventory/PieceInstance.cs`, `Identity.cs` | Instance identity, `PieceRef`, `InventoryError.*`, adjacency/row/column relations |
| `lib/Runtime/Domain/Inventory/Rotation.cs` | `Rotation` + `FootprintRotation` (clockwise quarter turn, re-normalised) |
| `lib/Runtime/Domain/Inventory/SynergyEvaluator.cs` | The five combo rules → per-item multipliers + `ActiveSynergy` records |
| `lib/Runtime/Domain/Inventory/LoadoutResolver.cs` | `WeightPenalty` + Bag → **Battle v1** `CombatLoadoutSnapshot` (cached on revision) |
| `lib/Runtime/Domain/Inventory/BackpackSnapshot.cs` | Plain snapshot + `IBackpackSnapshotCodec` port |
| `lib/Runtime/Contracts/Save/BackpackSnapshotDto.cs`, `BackpackSnapshotCodec.cs` | MessagePack DTO + the generated-formatter check |
| `lib/Runtime/Infrastructure/Save/MessagePackBackpackSnapshotCodec.cs` | The adapter (Domain snapshot ⇄ DTO bytes) |
| `lib/Runtime/Domain/Config/ConfigEnums.cs`, `Footprint.cs` | `SynergyKind.SameColumn`, footprint value equality |
| `data/Defines/schema.xml`, `data/tables/constant.csv` | `SynergyKind.SameColumn`, `constant.weight_speed_percent_per_unit` |
| `lib/Tests~/Core.Tests/Inventory/*` | 105 cases over the real tables (incl. 10 000-command randomized invariants) |
| `UnityProject/Assets/Main/Tests/EditMode/BackpackRulesTests.cs` | 9 Unity-side cases on real data (loadout, codec, undo/lock) |
| `docs/adr/0013`, `0014`, `0015` | Weight curve; editing/undo/ownership; same-line synergy kinds |
| `llmwiki/systems/backpack.md` | The system note (rules, ownership, commands, combos, loadout) |
| `tools/coverage-report.mjs`, `lib/Tests~/coverage-domain.runsettings` | The reproducible coverage measurement |

## 3. Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" --nologo -v q
dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" --collect:"XPlat Code Coverage" `
  --settings "lib/Tests~/coverage-domain.runsettings" --results-directory artifacts/coverage-05-domain
node tools/coverage-report.mjs artifacts/coverage-05-domain/*/coverage.cobertura.xml "Inventory/" "Config/Footprint.cs"
```

Unity (Editor `6000.3.9f1`, via the MCP bridge): an asset refresh + compile, then the
EditMode and PlayMode suites.

## 4. Results and where the artifacts are

| Gate | Result | Evidence |
| --- | --- | --- |
| `tools/gen.ps1` | `RESULT: OK` — 33 `.cs`, 12 `.bytes`, config hash `00F97F62…` | console |
| `tools/verify-generated.ps1` | `PASS 8 / FAIL 0 / WARN 1` — regeneration reproduces the tracked output byte-for-byte (the WARN is “generated output is uncommitted”, cleared by the commit) | console |
| `tools/validate-data.ps1` | `PASS 5 / FAIL 0` — no findings | `artifacts/dotnet/validate-data.json` |
| `tools/test.ps1` | `PASS 10 / FAIL 0` — build + **341 tests** + Sim determinism + the 4 data gates (17 broken fixtures rejected) | `artifacts/dotnet/` |
| Core test count | **341** total, **105** of them the Stage 05 inventory suite | `artifacts/coverage-05-domain/…/coverage.cobertura.xml` |
| Coverage of the backpack rule set | **90.92 %** (1382/1520 lines across `Inventory/*.cs` + `Config/Footprint.cs`); whole `Domain` assembly 89.66 % | the `coverage-report.mjs` run above |
| Unity EditMode | **29/29** pass (including the 9 new `BackpackRulesTests`) | MCP test run |
| Unity PlayMode | **4/4** pass | MCP test run |

Per-file coverage of the rule set (from `coverage-report.mjs`):

| File | Lines | Coverage |
| --- | --- | --- |
| `Inventory/Backpack.cs` | 462/519 | 89.02 % |
| `Inventory/BackpackEditor.cs` | 192/200 | 96.00 % |
| `Inventory/BackpackSnapshot.cs` | 12/12 | 100.00 % |
| `Inventory/Board.cs` | 117/120 | 97.50 % |
| `Inventory/Chest.cs` | 63/74 | 85.14 % |
| `Inventory/Identity.cs` | 13/16 | 81.25 % |
| `Inventory/LoadoutResolver.cs` | 134/144 | 93.06 % |
| `Inventory/PieceInstance.cs` | 96/105 | 91.43 % |
| `Inventory/Rotation.cs` | 24/27 | 88.89 % |
| `Inventory/SynergyEvaluator.cs` | 147/160 | 91.88 % |
| `Config/Footprint.cs` | 122/143 | 85.31 % |

## 5. What was not tested, and why

1. **No WebGL build, no browser, no performance number.** Stage 05 adds no scene, no UI and
   no hot path; a player build would exercise the same rule-engine code the EditMode suite
   already runs under Unity's compiler. Size and frame timing remain Stage 03b/15 work.
   *Nothing in this report is a performance claim.*
2. **Root-cause note on the coverage command:** the XPlat collector's default module list did
   not instrument `ProjectSurvive.Core.Domain` at all (the report simply had no Domain
   package). `lib/Tests~/coverage-domain.runsettings` pins the assembly explicitly; without
   it a “covered” claim would have been unfounded. Recorded here because the same trap will
   hit any future per-assembly measurement.
3. **Every randomized invariant run is one runtime deep.** The 10 000-command test uses the
   project's `Xoshiro128` through `IRandomSource`; determinism is only guaranteed within a
   single runtime (AGENTS rule 6, `llmwiki/gotchas/determinism-scope.md`), never across
   browsers or CPUs.
4. **No UI, so the Thai strings for the new error codes are not rendered.** The 14
   `inventory.*` codes are API and Stage 09 owns their localization keys; only the codes are
   tested today.
5. **`SameColumn` has no shipped data row** (ADR 0015), so it is exercised by a test that
   re-points the shipped tome rule at it rather than by content.
6. **Merge/Recycle payouts are not integrated with a wallet** — no wallet exists. The
   commands return the number (`RecycleOutcome.PayoutBia`) and Stage 06 credits it.

## 6. Blockers and risks

- **No blocker.** No Unity license, browser or network was needed beyond the already
  vendored toolchain.
- **Risks accepted, with reasons:**
  1. **Weight and price are tier-independent** (`item`/`bag` carry one of each). Merging to
     tier 3 is stronger but not heavier or more valuable. A deliberate non-invention:
     the column must land in `data/` before either can scale.
  2. **Two bags sharing an item can block each other.** Rotating one may be refused because
     the *other* would stop covering a cell the item also occupies — the error names the
     item, and Stage 09 has to say so in Thai.
  3. **Undo stores whole snapshots (64 deep) rather than inverse commands.** Simpler and
     provably lossless for merge/recycle, at the cost of memory proportional to state; the
     depth is a constructor parameter, so a size problem is a tuning problem.
  4. **`LoadoutResolver`'s cache is keyed on `Backpack.Revision` only.** A caller that
     mutates a piece's tier behind the aggregate's back would get a stale snapshot; the
     mutators are `internal` specifically so that cannot happen from outside `Domain`.
  5. **`ProjectSettings.asset` is dirty in the working tree and is deliberately *not* part of
     this commit.** `WebSizeProfilesTests` applies a size profile to the real
     `PlayerSettings`, so running the EditMode suite rewrites it
     (`il2cppCodeGeneration: WebGL: 0`). It is a test side effect, not a Stage 05 decision;
     flagged here rather than silently committed.
  6. **`tools/validate-data.ps1` defaults `-DataDir` to `data/`, which holds no `.bytes`.** A
     bare invocation reports twelve `config.table_missing` findings that are not data bugs.
     Pre-existing (Stage 02) and left alone — it is fixed by passing
     `-DataDir "lib/Tests~/data"`, which is how the pipeline and the Stage 04 report call it.

## 7. Acceptance criteria

| # | Criterion | Verdict |
| --- | --- | --- |
| 1 | Board 9×7, `BagPieceInstance`, `ItemInstance` (permanent id ≠ instance id), Chest 10 slots | **PASS** — `Board`/`Chest` sized from `constant`/`economy`; `DefId` vs `InstanceId` split with `PieceRef` handles |
| 2 | Footprint = a set of cells; rotate 0/90/180/270; normalise asymmetric shapes | **PASS** — `FootprintRotation` + `Footprint.FromCells` re-normalisation; 4-turn test over every shipped shape; the L basket is proven chiral |
| 3 | Placement: bags never overlap; an item needs every cell covered (may straddle bags); items never overlap | **PASS** — four distinct codes; straddling covered by test; `Board` owner maps |
| 4 | `Move`, `Rotate`, `TransferToChest`, `Merge` (same id+ tier → +1, max 3), `Recycle` (50 %, floored) — atomic, typed | **PASS** — all five + `Undo`/`Reset`/`SetLocked`; `inventory.*` codes; “rejected command changes nothing” asserted on bytes, not on a getter |
| 5 | A bag may be moved/removed only if no item loses usable area (refuse the whole command) | **PASS** — `FindStrandedItem` gates move/rotate/chest/recycle; `inventory.item_stranded` |
| 6 | Total weight → speed penalty | **PASS** — `WeightPenalty` linear slope, capped, chest excluded (ADR 0013) |
| 7 | `SynergyEvaluator`: 4-way adjacency counted once per pair, row and column, decay rule, bag-area bonus; same-kind bonuses add; minimum cooldown; no combo word; recomputed only when the set changes | **PASS** — all four kinds wired (ADR 0015); per-pair counting and stacking caps; cooldown floor in `LoadoutResolver`; cache keyed on `Revision` with `ResolveCount` |
| 8 | `LoadoutResolver` → `Domain.Combat.Battle.CombatLoadoutSnapshot` (Battle v1), independent of dictionary order | **PASS** — no second loadout model; canonical ordering; two build orders resolve identically (test) |
| 9 | `BackpackEditor`: undo + `Reset` via MessagePack snapshot, `Lock` | **PASS** — snapshot stack + Camp-entry snapshot through the Infrastructure codec; lock also freezes undo |
| — | Loadout resolver returns the `Domain.Combat.Battle` contract (no duplicated type) | **PASS** — `CombatLoadoutSnapshot`/`WeaponLoadout`/`SummonLoadout`/`ConsumableLoadout` reused as-is |
| — | Test: an active synergy is already folded into `WeaponLoadout` (the battle must not recompute it) | **PASS** — `Damage_bonuses_add_before_they_are_applied` (14 → 21) and the Unity-side loadout test |
| — | Test: identical backpacks → identical snapshot field for field | **PASS** — layout determinism test + byte-identical snapshot on identical histories |
| — | ≥ 30 cases and ≥ 85 % coverage of Backpack | **PASS** — **105** cases; **90.92 %** of the 11-file rule set (every individual rule file except the small `Identity.cs`, 81.25 %, is ≥ 85 %; the acceptance number is the aggregate) |
| — | Rules match the GDD and no `UnityEngine` in the layer | **PASS** — architecture tests assert `Domain`/`Application`/`Simulation` reference no Unity/engine assembly, and no `Domain`/`Simulation` file references R3 or VContainer |
| — | Compile + Unity suites (DoD) | **PASS** — `tools/test.ps1` `PASS 10 / FAIL 0`; Unity EditMode **29/29**, PlayMode **4/4** |
| — | Commit `stage-05:` | **PASS** — see the commit carrying this report |

**Gate that mattered most:** the randomized invariant run. It caught a real bug the example
tests did not — `Footprint` equality was comparing by reference, so a rotated shape could
disagree with itself — and the fix (structural equality over the cell set) is the reason the
rotation tests and the snapshot determinism test agree today.
