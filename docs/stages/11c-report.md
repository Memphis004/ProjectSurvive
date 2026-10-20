# Stage 11c — Pob, the boss: two phases, corpse eating and a possession that reverses the stick

**Date:** 2026-10-21

## 1. Summary

Pob now runs a small FSM of its own. Phase 2 begins the tick its HP reaches `boss_phase2_hp_percent`
(50 % of 320 = 160 HP), is announced once as `BossPhaseChanged`, and never goes back — a heal cannot
re-open phase 1. From phase 2 on it possesses the player: it warns with a `TelegraphStarted` circle
of `boss_possess_range` (8 units) tagged `BattleEventFlags.Boss`, and if the player is still inside
when the warning ends it negates the movement input for `boss_possess_seconds` (3.0 s = 90 ticks);
`TelegraphEnded` closes the warning on the landing tick. The reversal is `Possessed.RemainingTicks`
on the player and `PlayerMoveSystem` reads it — the view never flips anything.

Every ghost that dies inside `boss_corpse_range` (6) is eaten: Pob regains `boss_corpse_heal` (12) HP,
capped at its max, and the actual regain is published as `CorpseConsumed`. A dying boss ends the
possession it was holding and pays `drop_value` (10 bia) through the ordinary drop queue, so Stage
07c's sweep collects it after the fight. The HUD's charge for the new state is one published field,
`HudState.ControlFlipTicks`, documented in [ADR 0030](../adr/0030-boss-possession-hud-channel.md).

## 2. Acceptance

| # | Item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Two-phase boss FSM; phase 2 starts at the HP percent in `data/` | **PASS** | `BossRules.FromConfig` (threshold resolved once at spawn), `BossSystem.AdvancePhase`, `BossBehaviourTests.Phase_two_begins_at_the_rows_hp_percent_and_is_announced_once` (one HP above → phase 1, at the threshold → phase 2 + one event, 30 more ticks at 1 HP → still one event) |
| 2 | Eats other ghosts' corpses to regain HP (`CorpseConsumed`), never above max | **PASS** | `DeathSystem.EatCorpse` + `BossRules.HealFromCorpse`, `BossBehaviourTests.A_corpse_heals_the_boss_without_ever_passing_its_max` (pays the row's 12, clips to 1 at max−1 and publishes the 1, eats nothing at full HP, nothing out of reach) |
| 3 | Possesses the player, reversing the controls for a while; time and conditions in `data/` | **PASS** | `BossSystem.AdvancePossession` (`boss_possess_telegraph_seconds` 0.8, `boss_possess_seconds` 3.0, `boss_possess_interval_seconds` 14.0, `boss_possess_range` 8.0), `BossBehaviourTests.A_possession_warns_reverses_the_input_for_the_rows_ticks_and_gives_it_back` (warning ≥ 24 ticks before landing, exactly 90 flipped frames, +X walks left then right again) |
| 4 | The reversal lives in Simulation; the HUD says so through an existing channel, or an ADR appends to the contract; the view never guesses | **PASS** | `Possessed` + `PlayerMoveSystem` (negated axis); `HudState.ControlFlipTicks` published by `FramePublishSystem`; `BattleHudPresenter` renders `hud.possession` on `ControlFlipTicks > 0`; [ADR 0030](../adr/0030-boss-possession-hud-channel.md) records the three options and why the field beat an event pair |
| 5 | Communication through `BossPhaseChanged`, `TelegraphStarted`/`TelegraphEnded`, `CorpseConsumed` | **PASS** | `BossSystem` + `DeathSystem` emit; the enum was **not** extended (13/14 were already reserved by 11b); `llmwiki/systems/battle-contract.md` §5.1 rows for the two new payloads |
| 6 | The boss drops bia per `enemy.drop_value`; the 07c sweep still runs after it dies | **PASS** | `DeathSystem` queues `tag.DropValue` on the shared queue, `BossBehaviourTests.A_dead_boss_drops_the_rows_bia_and_releases_the_possession` (paid = `drop_value` = 10, `BossEntity` = −1, flip ticks back to 0); the sweep itself is `EcsBattleRulesTests.A_boss_floor_sweeps_after_the_boss_dies` |
| 7 | Replay reaches the same hash | **PASS** | `BossBehaviourTests.A_boss_floor_with_a_possession_replays_to_the_same_hash` (1 920 ticks of a real floor 5 with phase 2 forced by a lowered threshold, phase change and flip both asserted reached, `ComputeStateHash` == replay hash) |
| 8 | Editor capture of the boss floor and the flipped-control state | **PASS (needs the user's eyes)** | `artifacts/ui/stage11c-boss-possession.json` + `…-before-1920x1080.png` / `…-flipped-1920x1080.png`: floor 5 of a real run, tick 594, boss 155/320, boss 0.04 units from the player, 90 flip ticks left, 2 383 px changed inside the 400 × 56 possession line (2 509 in the whole frame) |

All parameters are columns on the `enemy` row (`boss_possess_telegraph_seconds`,
`boss_possess_seconds`, `boss_possess_interval_seconds`, `boss_possess_range`, `boss_corpse_range`,
`boss_corpse_heal`), declared in `data/Defines/schema.xml` and validated by
`ConfigValidator.ValidateBossBehaviour` with seven codes (`enemy.boss_behaviour_on_a_ghost`, `boss_corpse_negative`, `boss_corpse_incomplete`,
`boss_possess_negative`, `boss_possess_telegraph_missing`, `boss_possess_not_positive`,
`boss_possess_range_missing`) and one broken fixture (`boss-possess-without-telegraph`).
`data/tables/wave.csv` gained `wave.forest_5.spirits` (26 wandering spirits over the 90 s boss floor)
so Pob has corpses to eat on its own floor.

Gates: `-Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (155.2 s, dotnet **563 passed**,
EditMode **159/159**, 24 fixtures rejected) — `artifacts/stage11c/fast-summary.md`; `-Suite Full` with the
Editor closed **PASS 12 | FAIL 0 | NOT RUN 2** (WebGL build 276.6 s, configHash `6A800447…`,
120 664 309 B; Playwright **10 passed in Chrome**) — `artifacts/stage11c/full.log`. PlayMode in the open
Editor is **NOT RUN as a suite**: 22 passed / 25 failed of 47, with `BattlePlayModeTests` (the class
this stage touches) **6/6** and the scene-flow, result and shell classes green; every failure is in
camp-drag / merge-chooser / camp-world / title and every one is an ambient Input System log assertion
(`IndexOutOfRangeException` or `Cached unprocessed`), the same shape the 11b report and the notes in
`llmwiki/stage-log.md` recorded. It is the documented open-Editor flake, not a result of this stage.

## 3. Not tested, and the risks

- **Balance is untuned.** 50 % phase, 3 s flip on a 14 s rhythm, 12 HP a corpse and 26 spirits on
  the floor are first values; `tools/sim.ps1` was not re-run, so nothing here says the boss floor is
  winnable or that eating outpaces the player's damage. Stage 11d's job.
- **The pictures are Editor captures, not browser shots.** The pair is rendered from a real floor-5
  session through `BattleViewScene` into a 1920 × 1080 target with the overlay canvases borrowed for
  the frame; `DevGodMode` was on so the shutter was not racing the player's death. No browser
  screenshot of a possession was taken.
- **The boss stands on the player in the shot** (0.04 units): the capture walks straight in, and the
  chase closes the gap while the warning is counting down. It is a true frame, but the two sprites
  overlap; a reviewer wanting a wider composition would need the capture to retreat instead of stop.
- **A possession that is already running when the player dies** is not specially handled: the floor
  ends, so the remaining clock is simply never read again. No test pins that ordering.
- The phase threshold is resolved once at spawn, so a future row that changes max HP mid-fight
  (nothing does today) would leave the threshold stale.

## 4. Commands actually run

```powershell
powershell -NoProfile -File tools/gen-wiki.ps1      # 8 pages; llmwiki/content/{enemies,floors}.md changed
powershell -NoProfile -File tools/verify-wiki.ps1   # PASS 5 | FAIL 0 | WARN 0
dotnet test lib\Tests~\ProjectSurvive.Core.sln      # 563 passed (5 boss tests in BossBehaviourTests)
unity recompile                                     # clean
unity command run_tests --mode editor --async_tests # 159/159
unity command menu "ProjectSurvive/Stage 11c/Capture boss possession (1920x1080)"
powershell -NoProfile -File tools/test.ps1 -Suite Fast   # PASS 11 | FAIL 0 | NOT RUN 3
powershell -NoProfile -File artifacts/close-unity-editor.ps1
powershell -NoProfile -File tools/test.ps1 -Suite Full   # PASS 12 | FAIL 0 | NOT RUN 2, 523.3 s
#   PlayMode, Editor reopened: BattlePlayModeTests 6/6; 25 ambient Input System log failures
#   in the camp/title classes (see §2), so the suite is reported NOT RUN rather than green.
```
