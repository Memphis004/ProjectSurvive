# Stage 07b — bia from battle (drop value + floor-clear reward)

- **Date:** 2026-10-19
- **Prompt:** “ทำ Stage 07b เท่านั้น: เบี้ยจากการต่อสู้” — close the gap ADR 0017 recorded across
  `data/`, Simulation and Run, keep the HUD's bia honest, and prove it with tests plus a 200-seed
  before/after measurement. No number may be embedded in code.
- **Result:** a kill now pays `enemy.drop_value`, the pickup carries that value and credits it to
  `BattleResult.CurrencyEarned` exactly once when collected, and `RunSession.ApplyBattleResult`
  credits the collected drops plus `economy.clear_reward_bia` **on a win only** inside the existing
  once-per-floor settlement. [ADR 0025](../../docs/adr/0025-bia-from-battle.md) supersedes ADR 0017
  §5. Floor 1 now realizes about 4 collected bia + 8 clear ≈ 12 bia per run in the bot, around one
  to two shop pieces; the 200-seed win rates are recorded and deliberately **not tuned** (Stage 11).

## 1. What changed

| Area | Change |
| --- | --- |
| `data/` | `enemy.drop_value` (wandering spirit 1, krasue 1, nang tani 3, Pob 10) and `economy.clear_reward_bia` (8); `SchemaVersion` 3 → 4; validator rejects a negative value (`enemy.drop_value_negative`, `economy.clear_reward_negative`) and the `enemy-drop-value-negative` fixture proves it end to end |
| Simulation | `WaveSlot`/`EnemyTag` carry `DropValue` copied from the enemy row at spawn; `DeathSystem` queues it; `DropSystem` spawns the pickup with it; `PickupSystem` adds it to `CurrencyEarned` and deletes the pickup in the same tick. No other combat rule changed |
| Run | `ApplyBattleResult` credits `CurrencyEarned` on any outcome plus `ClearRewardBia` only when `result.IsWin`; the phase + pending-setup guard still admits exactly one settlement, so a replay/duplicate pays nothing |
| HUD | The top-right bia was already `startingBia + HudState.Currency`; a new EditMode assertion now proves it shows the drops the floor actually collected (15 + 7 = 22), and the PlayMode scene-flow case still checks the same sum on the real scene |

## 2. Acceptance

| # | Criterion | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | `enemy.drop_value` per kind/elite/boss, floor reward in `economy`, simplest shape + short ADR; validation (`>= 0`, every enemy names one); broken fixture; SchemaVersion bump; gen + verify-generated | **PASS** | [`schema.xml`](../../data/Defines/schema.xml), [`enemy.csv`](../../data/tables/enemy.csv), [`economy.csv`](../../data/tables/economy.csv), [ADR 0025](../../docs/adr/0025-bia-from-battle.md), [`fixture`](../../lib/Tests~/fixtures/enemy-drop-value-negative/fixture.json); `gen.ps1` and `verify-generated.ps1` PASS (`artifacts/stage07b/fast.log`) |
| 2 | A drop carries `drop_value`; collecting it adds to `CurrencyEarned` once; no other battle rule changes | **PASS** | `Killing_a_ghost_and_collecting_its_drop_pays_the_enemys_drop_value`, `Collecting_pickups_credits_the_sum_of_their_drop_values_once`, `A_kill_is_rewarded_exactly_once` (510 dotnet cases) |
| 3 | `ApplyBattleResult` credits `CurrencyEarned` and the win-only clear reward under the old once-per-floor settlement | **PASS** | `A_settlement_pays_the_battle_currency_and_the_clear_reward_exactly_once`, `A_lost_floor_pays_the_battle_currency_but_not_the_clear_reward`; `ReplayTests` still reproduce the same `RunState` hash |
| 4 | The HUD's top-right bia reflects the field collections | **PASS** | EditMode `Hud_captions_are_localized_and_the_floor_number_comes_from_the_setup` (155/155); PlayMode `The_run_carries_its_purse_bag_and_hp_into_the_battle_scene` and `BattlePlayModeTests` 6/6 |
| 5 | ADR 0017's “kill pays nothing” is superseded and llmwiki is updated | **PASS** | [ADR 0025](../../docs/adr/0025-bia-from-battle.md), the superseded note in [ADR 0017 §5](../../docs/adr/0017-ecslite-battle-simulation.md), [`battle-simulation.md`](../../llmwiki/systems/battle-simulation.md), [`run-shop.md`](../../llmwiki/systems/run-shop.md), [`data-schema.md`](../../llmwiki/systems/data-schema.md), [`index.md`](../../llmwiki/index.md) |
| — | Requested tests: kill→collect = sum of `drop_value`; the same pickup never pays twice; a loss pays no clear reward; a repeated settlement pays nothing; replay hashes match | **PASS** | the four named cases above + `Collecting_pickups_credits_the_sum_of_their_drop_values_once` + the existing replay suite; dotnet **510/510** |
| — | 200-seed sim before/after, not required to hit 30–60 % | **PASS (measured, untuned)** | table below; [`artifacts/stage07b/balance-summary.md`](../../artifacts/stage07b/balance-summary.md), `stage07b-balance-before.csv`, `stage07b-balance-after.csv` |
| — | gen + validate pass; tests pass | **PASS** | `-Suite Fast` **PASS 10 / FAIL 0 / NOT RUN 3 / WARN 0**, 510 dotnet, EditMode **155/155** (`artifacts/stage07b/fast.log`) |

### 200-seed balance (before → after)

| Run | Floor 1 | Floor 2 | Floor 3 | Floor 4 | Floor 5 | Runs cleared |
| --- | --- | --- | --- | --- | --- | --- |
| before | 200/200 (100 %) | 198/200 (99.0 %) | 88/198 (44.4 %) | 69/88 (78.4 %) | 69/69 (100 %) | 69/200 |
| after | 200/200 (100 %) | 197/200 (98.5 %) | 75/197 (38.1 %) | 53/75 (70.7 %) | 52/53 (98.1 %) | 52/200 |

Mean **collected** bia per floor after: 3.91 / 5.07 / 4.72 / 6.69 / 0.00 (the clear reward is
added at settlement and is not part of `currency_earned`). Floor 1 kills ≈15.9 ghosts but the bot
walks over only ≈3.9 drops, which is why the realized income is ≈12 bia rather than ≈24. The
full-collection reading, the 8–11 bia shop prices and the 1–2-pieces-per-floor target are in
[ADR 0025](../../docs/adr/0025-bia-from-battle.md); this stage records the numbers and does not tune
them.

## 3. Not tested, and why

1. **WebGL build + Playwright: NOT RUN.** The stage changes data, the battle rule and one EditMode
   assertion — no scene, prefab or layout — and it is not one of the gate stages (10/12/14/15).
   The Full run was made with the Editor open, so the build step correctly reported
   `a Unity Editor holds the project lock`.
2. **The bulk PlayMode run is red on the known ambient Input System log asserts** — `22/47` in this
   Full run, with no failed assertion of the project's own. The classes this stage can affect were
   re-run one by one and are green: `BattlePlayModeTests` **6/6**, `SceneFlowPlayModeTests` **5/5**
   (including the settlement and HUD-purse cases) and `ResultScenePlayModeTests` **3/3**
   ([`playmode-per-class.md`](../../artifacts/stage07b/playmode-per-class.md)). This is the same flake
   recorded in [`docs/testing/test-suites.md`](../testing/test-suites.md) §7.
3. **Balance is not tuned.** The prompt explicitly postpones the 30–60 % win-rate target to Stage 11;
   floor 1 is still 100 % and the sample sizes past floor 2 shrink quickly.
4. **`DevKillAll` still drops nothing collectable.** It queues drops inside `Submit`, and the next
   `Step` clears the per-tick queues — pre-existing behaviour, untouched here to honour “do not
   change another battle rule”. It is named in the stage log for Stage 11.
5. **The magnet is still instantaneous** and the remaining ADR 0017 gaps (`status` table,
   `constant.magnet_speed`) are unchanged.

## 4. Commands actually run

```powershell
# baseline before any edit
pwsh -NoProfile -File tools/sim.ps1 --seeds 200 --output artifacts/stage07b-balance-before.csv

# data pipeline after the schema/table edits
pwsh -NoProfile -File tools/gen.ps1
pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"
pwsh -NoProfile -File tools/verify-generated.ps1
pwsh -NoProfile -File tools/check-fixtures.ps1 -Only enemy-drop-value-negative

# focused and full core checks
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" --no-build
pwsh -NoProfile -File tools/test.ps1 -Suite Fast
pwsh -NoProfile -File tools/test.ps1 -Suite Full        # Editor open: PlayMode + WebGL lock

# the Editor bridge, per class (the bulk flake)
# run_tests --mode playmode --filter BattlePlayModeTests   -> 6/6
# run_tests --mode playmode --filter SceneFlowPlayModeTests -> 5/5
# run_tests --mode playmode --filter ResultScenePlayModeTests -> 3/3

# after
pwsh -NoProfile -File tools/sim.ps1 --seeds 200 --output artifacts/stage07b-balance-after.csv
```

Artifacts: `artifacts/stage07b/fast.log`, `artifacts/stage07b/full-editor-open.log`,
`artifacts/stage07b/balance-summary.md`, `artifacts/stage07b-balance-before.csv`,
`artifacts/stage07b-balance-after.csv`, `artifacts/sim-bot.txt`.
