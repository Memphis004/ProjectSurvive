# Stage 11d — The balance pass: 200 seeds, four builds, data only

**Date:** 2026-10-21

## 1. Summary

The bot reports four plans instead of one: the Stage 07 greedy shopper (`default`), a ranged caster,
a close-range knife/aura build that walks *toward* its target, and a kuman-thong summoner. All
200-seed runs go through the shipped `IBattleSession` (`RunSession` → `SceneFlow` →
`EcsBattleSessionFactory`), so the numbers are the game's own.

Before tuning the boss floor was a coin flip for the greedy bot (50.5 %) but the spread was
unusable: ranged 67.3 %, familiar 48.7 %, melee **8.0 %** (the knife build never left floor 3).
After eighteen recorded rounds — all `data/` edits, one of them rejected — the builds sit at **31.0 – 58.0 %** boss wins,
every one inside the 30–60 % band, floors 1–4 clear ~100 % (melee 95 % on floor 4), and Pob now
kills the player 12–26 % of boss floors instead of 2–6 %.

The one non-balance finding came from the browser gate: after the ranged nerf, floor 1 killed a
player who never moves (the Stage 10 acceptance), so round 17 thinned that floor's wave and the
standing player wins it again with full HP.

## 2. Acceptance

| # | Item | Result | Evidence |
| --- | --- | --- | --- |
| 1 | 200-seed run through `IBattleSession`; per-floor pass rate and EndReason split reported **before** tuning | **PASS** | `artifacts/stage11d/baseline-default.csv` (200 runs): F1 100 %, F2 100 %, F3 99.5 %, F4 98.5 %, F5 **50.5 %** — BossDefeated 50.5 / Timeout 43.9 / PlayerDied 5.6 |
| 2 | Tune `data/` only (no rule code) to a 30–60 % boss win rate; every round recorded | **PASS** | §3 lists every round; the only code touched is the bot itself (`lib/Tests~/Sim`, the instrument) and two test assertions that had pinned skill damage |
| 3 | ≥ 3 builds with different behaviour; report each win rate; name what is too strong / too weak | **PASS** | ranged 58.0 %, default 45.0 %, melee 37.2 %, familiar 31.0 % — `artifacts/stage11d/r18-*.csv`; melee walks in, ranged and summoner kite (§3) |
| 4 | Economy: income per floor buys about 1–2 pieces (07b target) | **PASS** | income 9.0–23.3 bia a floor (mean 12.9–14.4) against prices 5–14 → **≈1.5 pieces/floor**; the bot buys 2.3–2.5 a floor because the purse carries over (§4) |
| 5 | The Stage 10 browser gate still holds (standing player clears floor 1) | **PASS** | after round 17 the standing player ends floor 1 `Won` at 1806 ticks with **100/100 HP**, 13 kills, 13 bia (`tests/web` battle spec); Playwright **10/10** |

Gates: `-Suite Fast` **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (99.3 s, the three NOT RUN
steps are Full-only) — dotnet **563**, EditMode **159/159**, 24 fixtures rejected,
wiki reproducible. PlayMode in the open Editor is **22/48** — the documented ambient Input System
flake, unchanged from 11b/11c (Battle 6/6, Result 3/3, SceneFlow 5/5, Shell 4/4). The WebGL build
and Playwright ran outside the harness because the open Editor holds the project lock:
`tools/build-web.ps1` **RESULT: OK** (`configHash D5B4813E…`, 120 664 309 B) and **10 passed in
Chrome**.

## 3. Rounds, before and after (200 seeds, boss-floor win rate)

`default` is the Stage 07 greedy bot; the three named builds exist from round 0 onward.

| Round | What changed in `data/` | default | ranged | melee | familiar |
| --- | --- | --- | --- | --- | --- |
| 0 — before | nothing (11c data): pob 320 HP / 12 touch, krasue 3.2 @7, knife 14 @1.5, fire 10, thread 9, takrut 7 | 50.5 | 67.3 | 8.0 | 48.7 |
| 1 | fire 10→9, thread 9→8, knife 14→17 @1.7, takrut 7→9 | 47.2 | 65.3 | 0.0¹ | 45.5 |
| 2 | krasue: speed 3.2→2.7, touch 7→5, dash 9→8; fire 9→8, thread 8→7 | 38.1 | 53.5 | 0.0¹ | 35.4 |
| 3–8 (melee instrument + floor 3 shape) ² | krasue touch 5→3, cooldown 1.2→1.6→2.4, dash 8→6; nang tani ring 12→9 @4.5→3.4; floor-3 krasue wave 16 @1.5 s → 12 @2.0 s | — | — | F3: 0 → 0 → 1 → 3 → 3 → 12 of 184 | — |
| 9 | melee disengages below 45 % HP and from piles >6 (bot, not data) | 38.5 | 55.3 | 44.9 | 36.5 |
| 10 | pob 320→240 HP, touch 12→16, corpse heal 12→16 | 48.5 | 59.8 | 59.1 | 47.0 |
| 11 | pob touch 16→22 | 47.5 | 57.8 | 52.5 | 46.0 |
| 12 (rejected) | floor 5 90→110 s, pob 280 HP | 53.5 | 68.3 | 41.4 | 49.0 |
| 13 | back to 90 s / 240 HP; possession every 14 s → 10 s | 48.5 | 56.3 | 52.0 | 43.0 |
| 14 | krasue speed 2.9, touch 4, cooldown 2.2 | 48.5 | 60.0 | 48.5 | 40.0 |
| 15 | possession range 8→9 | 47.5 | 61.0 | 48.5 | 38.5 |
| 16 | fire bolt 8→7 | 41.0 | 53.3 | 48.2 | 36.0 |
| 17 | floor-1 wave: 30 spirits @2.0 s → 20 @3.0 s (**browser regression fix**, §5) | 48.0 | 61.0 | 44.7 | 35.5 |
| 18 — **final** | pob 240→250 HP | **45.0** | **58.0** | **37.2** | **31.0** |

¹ The melee plan reached floor 3 and died there for 0 wins in ~150–180 attempts, so it had no
boss-floor rate to report; rounds 3–8's column is its floor-3 pass rate.
² Rounds 3–8 each fixed one hypothesis about the floor-3 wall; the intermediate numbers are in
`artifacts/stage11d/r3…r8-melee.csv` (R3 0/175, R4 1/178, R5 1/184, R6 3/184, R7 3/184, R8 12/184).

Boss-floor EndReason (final, per build): default 45 % BossDefeated / 43 % Timeout / 12 % PlayerDied;
ranged 58/29.5/12.5; melee 37/37/26; familiar 31/52.5/16.5. Before tuning it was 50.5/43.9/5.6 for
the greedy bot and 2–6 % PlayerDied for every build.

**What is still lopsided.** Ranged is strongest (58.0 %) and the familiar build weakest (31.0 %):
the summon's aura needs the player near the fight, which costs health the build has no answer for,
while a fire bolt never closes. Floors 1–4 clear ~100 % for three builds (melee loses 9 of 197 on
floor 4 and 3 of 200 on floor 2) — the campaign's difficulty now sits on floor 5, a content question
for Stage 13 and a floor-shape decision rather than a number here.

## 4. Economy

`economy.clear_reward_bia` stays 8 and `enemy.drop_value` stays 1/1/3/10 ([ADR 0025](../adr/0025-bia-from-battle.md));
nothing in the economy tables was touched, so this is what the tuned battles actually pay.

| Build | income per floor F1–F5 (bia) | mean | purchases per floor | bia left at run end |
| --- | --- | --- | --- | --- |
| default | 10.7 / 16.1 / 11.1 / 18.2 / 10.5 | 13.3 | 2.49 | 19.6 |
| ranged | 10.9 / 16.6 / 11.5 / 20.7 / 12.1 | 14.4 | 2.46 | 21.8 |
| melee | 13.9 / 23.3 / 5.2 / 13.3 / 9.0 | 12.9 | 2.31 | 16.2 |
| familiar | 10.8 / 15.7 / 11.4 / 17.9 / 9.2 | 13.0 | 2.43 | 16.2 |

Prices are 5–14 (offerings 5–6, charms 7–9, weapons 8–11, tome 13, familiar 14), so a floor's income
buys **≈1.5 pieces** — inside the 1–2 target. The bot buys 2.3–2.5 a floor only because the purse
carries over and four slots are offered per visit; the run still ends holding 16–22 bia it never had
a slot for. That is a shop-supply limit, not an income surplus, and it is the number to watch if a
later stage cuts shop visits.

## 5. Not tested, and the risks

- **The bot is not a player.** The melee plan walks to arm's length, circles, and disengages under
  45 % HP; a human weaves differently. Tuning was done against this policy, so a human melee run may
  feel easier than 37.2 % and a human ranged run harder than 58.0 %.
- **Round 16 broke a shipped acceptance and the browser caught it.** With fire bolt at 7, a player who
  never moves stopped clearing floor 1 (lost at tick 1386, 46 s, 9 kills, MP drained) — the Stage 10
  `battle.spec.ts` gate that expects the standing player to win floor 1 with full HP. Round 17 thinned
  the floor-1 wave and the gate passes again (Won at 1806 ticks, 100/100 HP). **Lesson for the next
  pass: run the browser gate after any player-damage change**, not only the 200-seed bot.
- **Timeouts are still 30–52 % of boss floors.** Pob loses by clock as often as by killing the player.
  That is legal (the floor is a 90 s survival), but if the boss should feel lethal the next lever is a
  telegraphed attack for Pob (it still has `attack_pattern = None`), not more contact damage.
- **Floors 1–4 are formalities.** Nerfing krasue to make melee possible took the deaths out of the
  early floors; restoring tension is wave/elite timing work for the content stage and would move
  these numbers again.
- **Flaky checks seen once each, passing on re-run:** the camp layout Playwright case (it retried its
  own click and passed alone: `test-results` log `click was not registered (attempt 1 of 3)`), and
  xUnit `Step_with_a_caster_loadout_does_not_allocate_after_warm_up` (552 B under the harness,
  0 in isolation and in the next full run). Both are measurement flakes, not regressions, but they
  are why a single red run is re-checked before it is believed.
- `tests/web`'s `battle.spec.ts` asserts floor 1 is *won* by a stationary player, so the floor-1 wave
  is now pinned by a browser test as well as by the data.

## 6. Commands actually run

```powershell
# baseline, then each round: edit data/ -> regenerate -> rebuild -> four 200-seed runs
powershell -NoProfile -File tools/sim.ps1 --seeds 200 --output artifacts/stage11d/baseline-default.csv
powershell -NoProfile -File tools/gen.ps1
for b in default ranged melee familiar:
  powershell -NoProfile -File tools/sim.ps1 --mode bot --seeds 200 --build $b --out artifacts/stage11d/r18-$b.csv
powershell -NoProfile -File tools/gen-wiki.ps1            # data changed: enemies/skills/floors pages
# gates (the open Editor runs the Unity steps; the build needs the Editor closed)
powershell -NoProfile -File tools/test.ps1 -Suite Full    # artifacts/stage11d/full-3.log
powershell -NoProfile -File tools/build-web.ps1 -Configuration Development -Force
npx playwright test smoke.spec.ts title.spec.ts battle.spec.ts camp.spec.ts result.spec.ts
```

`r0…r18` hold every round and `baseline-default.csv` the untuned before-state; `webgl-3.log` and
`playwright-final.log` are the built-player evidence for the final data.
