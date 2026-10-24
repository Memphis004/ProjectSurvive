# Stage 16 — กระหัง, the fifth ghost (post-prototype content)

**Date:** 2026-10-23

## 1. Summary

Prototype content was complete at Stage 15; this is the first **content-growth** stage. It adds
**กระหัง**, a flying ghost that dives a line and then beats a gale at its landing point which
**throws the player** `follow_up_push` units away — the first rule in the battle other than input
that moves the player. That is one new column (`enemy.follow_up_push`) and one small push in
`EnemyAttackSystem.ResolveFollowUp`, not a new system: the Stage 11b telegraph machine already runs
the kit, so the ghost needed data, one arithmetic rule and two validator codes. The dash pattern was
renamed `DashDrain` → `DashFollowUp`, because Krahang is its second user and does not drain
([ADR 0036](../adr/0036-krahang-pushes-with-the-gale.md)). Balance was re-measured on 200 seeds ×
4 builds: boss win rate stays inside the 30–60 % band for all four (43.5 / 53.5 / 33.3 / 31.0 %),
floors 1–4 stay at ~100 %, and the added bia is +2.3 a floor.

## 2. Acceptance

| # | Requirement | Verdict | Evidence |
| --- | --- | --- | --- |
| 1 | กระหัง exists as the fifth ghost with its own cycle (dive → gale → recover, no retreat) and a place in the run | **PASS** | `ConfigLoadTests.Stage16_krahang_is_the_fifth_ghost` (5 enemies, pattern, burst gale, push > 0, no retreat, waves on floors 3–4 only); `EnemyAttackBehaviorTests.Krahang_dives_warns_a_gale_and_recovers_without_retreating` and `..._warns_a_circle_where_it_lands_and_the_gale_is_a_burst` |
| 2 | The gale throws a player it hits by the row's distance, and only a row that asks for it moves anyone | **PASS** | `..._gale_throws_the_player_away_from_where_it_landed` (moved exactly `follow_up_push` along −X, Y unchanged) and `A_ghost_that_does_not_ask_for_a_push_leaves_the_player_where_they_stood` (Krasue's drain hits and the player stays at 0,0) |
| 3 | Data pipeline clean, and the two new rules are proved through `data/ → Luban → .bytes → findings` | **PASS** | `tools/gen.ps1` OK (`configHash 411BA005A62F4FDB…`), `validate-data.ps1` 0 findings, `verify-generated.ps1` PASS 8, `check-fixtures.ps1` **PASS 30 \| FAIL 0** including the new `enemy-push-negative` and `enemy-push-without-follow-up` fixtures ([log](../../artifacts/dotnet/check-fixtures.log)) |
| 4 | Balance: same seeds as the previous round, floors 1–4 still clearable, boss rate inside 30–60 % | **PASS** | 200 seeds × 4 builds before and after ([`artifacts/stage16/`](../../artifacts/stage16/), summary in `final-balance.log`), numbers in §3 |
| 5 | No new rule in Presentation, no scene, no UI, no save-schema change | **PASS** | `git diff --stat` touches `data/`, `lib/Runtime/{Domain,Infrastructure,Simulation}`, tests and docs only; nothing under `UnityProject/`; the battle frame already carries positions, so a shove is a moved entity |
| 6 | Existing fights are not disturbed by the new column | **PASS** | the Stage 08b/11c/11d state-hash pin for floor 1 with casters is **unchanged at `3561164121`**, and dotnet is **612/612** |
| 7 | The stage's own tests run | **PASS** | `dotnet test` **612 passed** (8 new: 4 behaviour, 3 validator, 1 content pin); `tools/test.ps1 -Suite Fast` **PASS 10 \| FAIL 0 \| NOT RUN 4** |
| 8 | Unity EditMode | **PASS (batch)** | **207/207** in `6000.3.9f1` batchmode ([`editmode.xml`](../../artifacts/stage16/editmode.xml)); `-Suite Fast`'s own EditMode step was **NOT RUN** because no Editor with the Pipeline connection was open, so the suite was run directly by the Unity CLI instead. |
| 9 | WebGL build + Playwright | **NOT RUN (deliberate)** | the stage's agreed scope was Core/data + simulation, no Web build; the checkout has no `UnityProject/Temp` (project never imported in this environment), so a player build was not attempted. |

## 3. The balance pass

Four rounds of table edits, each measured over 200 seeds and all four bot builds (`default`,
`ranged`, `melee`, `familiar`). `r0…r6` are kept in `artifacts/stage16/`.

| Round | Change | default boss | melee floors 3/4 | Why it was rejected |
| --- | --- | --- | --- | --- |
| r0 | 55 HP, dash 8, gale 7, push 3.0, 4 + 3 spawns | **21.5 %** | 177/200, 120/177 | one ghost halved the boss rate; floors 3–4 stopped being formalities |
| r1 | dash 6, gale 4, push 2.0, cooldown 3.5 | 30 % | 196/200, 172/196 | still 24 deaths on floor 4 |
| r2 | 45 HP, dash 5, gale 3, push 1.5 | 36 % | 198/200, 185/198 | melee boss rate 7 % (the walking bot cannot dodge a 9-unit dive) |
| r3 | + range 9, length 8, contact 3, 2 spawns/floor | 42 % | 200, 199 / 190, 164 | melee boss 20.5 % |
| r4 | 30 HP, dash 4 | 43 % | 197→192, 192→172 | melee boss 24 % |
| **r5 (shipped)** | + range 6.5, length 5.5 | **43.5 %** | **197→196, 196→186** | — |
| r6 | 3 spawns/floor instead of 2 | 43 % | 196, 183 | melee boss 29.5 % of attempts, below the 30 % band, so the extra spawn was given back |

Shipped numbers against the pre-stage baseline (same seeds, same builds):

| Build | Boss win, before | Boss win, after | Floors 1–4, after | Mean bia, floor 3 → 4 |
| --- | --- | --- | --- | --- |
| default | 89/200 (44.5 %) | **87/200 (43.5 %)** | 200 / 200 / 200 / 200 | 24.0 → 26.3, 30.5 → 31.3 |
| ranged | 116/200 (58.0 %) | **107/200 (53.5 %)** | 200 / 200 / 200 / 200 | — |
| melee | 71/188 (37.8 %) | **62/186 (33.3 %)** | 200 / 197 / 196 / 186 | 18.3 → 19.6 |
| familiar | 62/200 (31.0 %) | **62/200 (31.0 %)** | 200 / 200 / 200 / 200 | — |

The ghost's shipped row: 30 HP, speed 2.6, contact 3, drop 2, `DashFollowUp`, range 6.5, warning
0.7 s, dive 5.5 units in 0.4 s for 4, gale warning 0.5 s, one burst of 3 inside 4.2 units that pushes
1.5 units, cooldown 4.0 s, no retreat; two spawns on floor 3 (from 34 s) and two on floor 4 (from
40 s).

## 4. What was not tested, and the risks

- **EditMode ran in batch, not through the harness.** `tools/test.ps1 -Suite Fast` needs an Editor
  with the Pipeline connection open; none was connected here, so the suite was invoked as
  `Unity.exe -batchmode -runTests -testPlatform EditMode`. **`-quit` must not be passed** — with it
  Unity compiles and exits before the runner starts (the first attempt produced a log and no XML).
  The count is unchanged at 207, i.e. this stage added no EditMode test.
- **WebGL and Playwright were not run** (scope decision). The next player build will report
  `enemies=5` in the smoke report; any spec that reads that payload should be re-checked then.
- **A checkpoint saved before this build will not resume.** No save-schema field was added, but the
  generated `ConfigHash` changed, and `CheckpointService` refuses a checkpoint whose hash differs
  (`ConfigMismatch`). That is the Stage 12 rule for any `data/` edit, not a new behaviour, and a new
  run is unaffected.
- **The push is the first rule that moves the player without input**, and it is deliberately resolved
  against the position at the start of the tick. With two Krahang landing on the same tick a player
  is hit by both if they stood inside both gales, and pushed by both; that is documented in the ADR
  and in `EnemyAttackSystem`, but no test drives two gales in one tick.
- **The bot is not a player.** The melee decline (−4.5 points of boss rate) is the walking bot being
  unable to sidestep a dive; a human can step out of a 0.7 s line. The stage keeps the ghost inside
  the band rather than tuning to the bot's weakness.
- **The shipped gale is a single burst.** `follow_up_seconds = 0`, so the push lands once. A future
  row that makes the gale tick will push once per damage tick — a real behaviour change that would
  need its own measurement.

## 5. Commands actually run

```powershell
# baseline before any edit
pwsh -File tools/sim.ps1 --seeds 200 --build default --output artifacts/stage16/baseline-default.csv
pwsh -File tools/sim.ps1 --seeds 200 --build {ranged,melee,familiar} -NoBuild --output artifacts/stage16/baseline-*.csv

# data → gen → validate
pwsh -File tools/gen.ps1
pwsh -File tools/verify-generated.ps1
pwsh -File tools/validate-data.ps1 -DataDir 'lib/Tests~/data'
pwsh -File tools/check-fixtures.ps1

# core tests
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug --nologo
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" -c Debug --no-build

# balance rounds r0..r6, then the shipped configuration
pwsh -File tools/sim.ps1 --seeds 200 --build {default,ranged,melee,familiar} -NoBuild --output artifacts/stage16/final-*.csv

# documentation and gates
pwsh -File tools/gen-wiki.ps1
pwsh -File tools/verify-wiki.ps1
pwsh -File tools/test.ps1 -Suite Fast

# EditMode, because no Pipeline-connected Editor was open (no -quit: it skips the runner)
"C:\Program Files\Unity\Hub\Editor\6000.3.9f1\Editor\Unity.exe" -batchmode `
  -projectPath UnityProject -runTests -testPlatform EditMode `
  -testResults artifacts/stage16/editmode.xml -logFile artifacts/stage16/editmode2.log
```
