# Stage 04 report — Prototype data (`data/` only)

- **Date:** 2026-10-14
- **Scope:** the prototype's content and nothing else — 14 pieces, the tag vocabulary
  behind the 5 combo rules, 5 cast behaviours, 4 Thai ghosts, the 5-floor run with its
  wave timeline, the shop pool / economy / constants, and th+en strings for all of it.
  `lib/` changed only where the data needed it: the footprint parser, `ItemConfig`,
  the Infrastructure adapter, the validator and tests.
- **Decisions:** [ADR 0009](../adr/0009-footprint-picture-format.md) (footprint is a
  picture string; supersedes ADR 0005 §1) and
  [ADR 0010](../adr/0010-item-local-effects-deferred.md) (charm/offering effects stay
  unmodelled until their consumer exists).
- **Machine:** Intel Core i5-14400F, 31.8 GiB RAM, RTX 5060 Ti, Windows 11
  `10.0.26300`; Unity `6000.3.9f1`; Chrome `154.0.8037.98` (ADR 0003 reference).

## 1. What was accomplished

1. **All 14 prototype pieces exist, every one of them merging to tier 3** — 3 bags
   (`bag.raw_cotton_bag` 3×3, `bag.yant_cloth_pouch` 1×3, `bag.wicker_basket_l` the L
   shape), 4 arcane weapons (one per cast behaviour), 3 charms, 1 spell tome, 2
   offerings, 1 familiar. **28 merge recipes** (2 per piece) carry the ladder, and a
   new validator rule plus a fixture enforce it, so a missing rung fails the build
   instead of stranding a player.
2. **The two required test cases are in the data and asserted:**
   - *คุณไสย line* — `item.kuman_thong` is `Element.Curse` and carries the `curse` tag,
     so `synergy.curse_holy_debuff` (the −30 % power-decay rule) has both sides it
     needs: the curse piece and the four `holy` pieces it must be kept away from.
   - *asymmetric shape* — `bag.wicker_basket_l` is the L-tetromino (`"X.|X.|XX"`), and
     a test asserts its mirror image is a **different** cell set, so Stage 05's
     rotation rule has an orientation that actually changes an outcome.
3. **A real tag vocabulary, not free-form strings.** Eight tags are now an interface
   between `item` and `synergy` (`weapon`, `tome`, `takrut`, `yant`, `curse`, `holy`,
   `offering`, `familiar`), the 5 GDD combo rules are matched by them, and
   `synergy.tag_unknown` makes a rule that can never fire a build failure. This closes
   the "the tag vocabulary lands with the Stage 04 content" note Stage 02 left open.
4. **Footprints became a picture string parsed in Domain** — `"XXX|XXX|XXX"`,
   `"X|X|X"`, `"X.|X.|XX"` — which is what the canonical GDD specifies
   (`docs/gdd/prototype-v0.3.md:284`). `item` gained a `footprint` column, without
   which the 2×2 tome and 2×1 familiar would have no shape at all
   ([ADR 0009](../adr/0009-footprint-picture-format.md)). `SchemaVersion` 1 → 2.
5. **The bad-input surface grew with the content, and every new rule is proven by
   broken data.** Five new findings — `item.footprint.empty`, `synergy.tag_unknown`,
   `merge_recipe.ladder_incomplete`, `shop_pool.piece_unreachable`,
   `wave.spawn_window_too_short` — are each covered by a unit test **and** a fixture.
   Fixtures went 12 → 17; `footprint.duplicate_cell` was retired because a picture
   cannot express a duplicated cell.
6. **The gate caught a real mistake during the stage.** The first run of
   `validate-data.ps1` failed with three `shop_pool.target_missing` findings: I had
   typed the three bag ids into `item_id` instead of `bag_id`. That is exactly the
   class of error the validator exists for, and it is in the log rather than in the
   data.
7. **Every number the prompt names matches the GDD**: start 15 bia, 4 shop slots,
   refresh 5 (+2 per refresh), sell-back 50 %, heal 15 %, chest 10, board 9×7,
   min cooldown 0.2 s, HP 100, MP 50, focus 5/s, move speed 4.0, max tier 3, floors
   60 s ×4 + a 90 s boss floor, 51 localization rows in th + en.

## 2. Important files that changed

**`data/` (the deliverable)**

| File | What |
| --- | --- |
| `Defines/schema.xml` | `Bag.footprint` documented as a picture; **new `Item.footprint`** |
| `tables/item.csv` | 11 items with element, tags, skill, price, weight, footprint |
| `tables/bag.csv` | 3 bags as picture footprints |
| `tables/skill.csv` | 4 cast behaviours + the familiar's curse aura (5) |
| `tables/synergy.csv` | the 5 GDD combo rules over the tag vocabulary |
| `tables/merge_recipe.csv` | 28 recipes — a full 1→2→3 ladder for all 14 pieces |
| `tables/shop_pool.csv` | 14 rows; every piece offered, every floor covered |
| `tables/wave.csv` | 9 waves fitted to their floors' windows |
| `localization/localization.csv` | 51 rows (was 36), th + en for everything |

**Rules / plumbing**

| File | What |
| --- | --- |
| `Domain/Config/Footprint.cs` | picture parser + new failure reasons |
| `Domain/Config/ConfigTables.cs` | `ItemConfig.Footprint` |
| `Domain/Config/CellOffset.cs` | doc follows the picture orientation |
| `Infrastructure/Config/LubanGameConfig.cs` | maps `item.footprint` |
| `Tests~/Data.Validator/ConfigValidator.cs` | 5 new rules |
| `Tests~/Core.Tests/ConfigLoadTests.cs` | reads every table; content invariants |
| `Tests~/Core.Tests/ConfigRulesTests.cs` | one test per new rule + picture cases |
| `lib/Tests~/fixtures/**` | 17 cases (12 refreshed/renamed + 5 new) |
| `tools/gen.ps1` | `SchemaVersion` 1 → 2 |

**Docs**

- new: `docs/adr/0009-footprint-picture-format.md`, `docs/adr/0010-item-local-effects-deferred.md`,
  this report
- updated: `docs/adr/0005-...` (§1 marked superseded), `llmwiki/systems/data-schema.md`
  (picture syntax, tag vocabulary, the new rules), `llmwiki/glossary.md` (tag table),
  `llmwiki/index.md`, `llmwiki/stage-log.md`, `knowledge.md`,
  `llmwiki/gotchas/luban-5-strict-and-tables.md`, `tools/validate-data.ps1`,
  `Tests~/Data.Validator/Program.cs` (comments)

## 3. Commands that were actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1 -NoWrite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1 -DataDir "lib/Tests~/data" -NoWrite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1 -NoWrite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1 -NoWrite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -NoWrite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Configuration Release -NoWrite
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
```

```text
# Unity (Editor open; batch mode refuses to share the project)
unity command menu "Assets/Refresh"
unity command recompile && unity command recompile_status
unity command eval '../../ResourcesGameConfigLoader.Load() ...'   # runtime load probe
unity command run_tests editor
unity command run_tests playmode --async_tests
unity command run_tests playmode --filter Thai_spike_scene_measures_both_stacks_without_missing_glyphs
unity command eval 'WebBuildPipeline.BuildWebDevelopment()'
node tools/serve-web.mjs --port 8080
cd tests/web && npx playwright test
```

## 4. Results and where the artifacts live

| Check | Result |
| --- | --- |
| `gen.ps1` | **OK** — `33 .cs, 12 .bytes`; configHash `73B27F7E4A8C6BA5ACEF5B4EAA7354F7D02C0BE4209CAE5E1926206D30642A27` |
| `validate-data.ps1` | **PASS 5 / FAIL 0** — `items 11, bags 3, skills 5, synergies 5, mergeRecipes 28, enemies 4, floors 5, waves 9, shopPool 14, localization 51` |
| ↳ mid-stage failure | **FAIL 3** — `shop_pool.target_missing` ×3 (bag ids in `item_id`); fixed, then clean |
| `verify-generated.ps1` | **PASS 8 / FAIL 0 / WARN 1** — regeneration is byte-identical (34 `.cs`, 12 `.bytes` ×2 trees). The WARN names the 2 generated files not yet committed — expected before this commit |
| `check-fixtures.ps1` | **PASS 21 / FAIL 0** — 2 controls + **17 fixtures** + toolchain/binary |
| `dotnet test` | **186 passed, 0 failed** (baseline before this stage: 174) |
| `tools/test.ps1` Debug | **PASS 10 / FAIL 0 / WARN 0 / SKIP 0** — `RESULT: OK` |
| `tools/test.ps1` Release | **PASS 10 / FAIL 0 / WARN 0 / SKIP 0** — identical Sim digests to Debug, so the data change moved nothing deterministic |
| Unity compile | `recompile` → **up_to_date**, `compilationFailed: false` (no new errors/warnings) |
| **Unity runtime load** | `bags=3 items=11 skills=5 synergies=5 recipes=28 waves=9 shop=14 loc=51 cottonCells=9 basket=4/2x3 tome=item.spell_tome:4 ragged=footprint.ragged_row` — Unity compiled the new code *and* loaded the new `Resources/Config` bytes (hash `73B27F7E`) |
| Unity **EditMode** | **12 / 12 passed** |
| Unity **PlayMode** | **5 / 5 passed** (4 in the aggregate + the Thai-spike case run by filter) |
| **WebGL build** (Development) | **Succeeded** — `117,478,257 bytes`, `208.8 s`, 2 warnings (both pre-existing), `configHash 73B27F7E` |
| **Playwright on the served player** | **2 / 2 passed** — the player's own probe: `hash=73B27F7E generated=73B27F7E localization=51 items=11 enemies=4`, `probeCount=7, failedCount=0`; Thai spike `totalMissingGlyphs=0` |
| Logs | `artifacts/serve-web-04.log`, the `unity command`/`dotnet` stdout captured above |

The WebGL build matters here beyond Stage 03's purposes: it is the only evidence that
the **new** `.bytes` reach a real player. The probe reports the *new* config hash and
the new counts, which a stale build could not do.

## 5. What was not tested, and why

1. **No gameplay, by design.** Stage 04 authors data. Nothing consumes `synergy`,
   `merge_recipe` or `wave` yet — combination, merging, shop rolls and the battle are
   Stages 05–07. The data is *validated* and *readable*, not *played*.
2. **Charm passives and offering effects are not representable** — there is no column
   for them, deliberately ([ADR 0010](../adr/0010-item-local-effects-deferred.md)).
   Their magnitudes must land in `data/` with their consumer, never in code.
3. **The weight → speed *slope* is not authored.** `constant.max_speed_penalty_percent`
   (40) is the cap the GDD says lives in `constant`; the GDD does not specify the
   per-unit curve, so no coefficient was invented. Flagged for Stage 05.
4. **Release-build size and frame timing — NOT RUN.** A Release/compressed build and
   any performance number are Stage 03b/15 work (ADR 0003 requires a Release build
   first). **No performance number is claimed in this report.**
5. **Batch-mode tooling** (`tools/build-web.ps1`, `unity test`) was not exercised: the
   interactive Editor owns the project. The same code paths ran through the pipeline.
6. **Nothing in `data/` was tuned by play.** The numbers are the GDD's starting values
   ("ค่าเริ่มทดลองจาก Opus"); balance is Stage 11.

## 6. Blockers and risks

- **No blocker.** The stage needed no Unity license, browser or network beyond the
  already-vendored toolchain.
- **Risks accepted, with reasons:**
  1. **A footprint picture cannot express a duplicated cell.** That bug class is gone
     rather than detected, which is a gain, but it is a change to what Stage 02
     promised to check. Recorded in ADR 0009 and replaced by `ragged_row`.
  2. **The merge-ladder and shop-reachability rules are stricter than the future may
     need.** They assume every piece is tier-1..3 and obtainable from the shop. True
     for the prototype; a post-prototype piece that is a quest reward would need the
     rule relaxed (and a new obtainability source in the data model).
  3. **The tag vocabulary is only existence-checked.** A tag carried by an item but
     read by no synergy is allowed and unflagged — fine now (5 flavour tags), but it
     means a typo'd tag in `item.csv` that no synergy happens to use is silent.
  4. **`item` and `bag` share one id namespace** and the shop pool addresses pieces by
     a bare id; the validator catches collisions, but a rename is still a migration
     once saves exist (Stage 12).
  5. **The Thai strings are authored, not proofread by a Thai speaker.** They are
     short, factual descriptions of the piece's GDD behaviour. Glyph coverage is
     proven (the baked 182-char set covers every string used in the shipped UI paths;
     the descriptions are not yet rendered by any view, so their *coverage* is only
     implied by the character set rule — Stage 09 renders them).

## 7. Acceptance criteria

| # | Criterion | Verdict |
| --- | --- | --- |
| 1 | `gen` ผ่าน | **PASS** — `RESULT: OK`, 33 `.cs` / 12 `.bytes`, `verify-generated.ps1` reproduces them byte-identically |
| 2 | `validate` ผ่าน | **PASS** — `PASS 5 / FAIL 0`, no findings; and it demonstrably fails on bad data (17 fixtures, 3 of my own findings mid-stage) |
| 3 | เทสต์ Core อ่านทุกตารางได้ | **PASS** — `ConfigLoadTests` reads all 12 tables through the generated readers over real `.bytes`: row counts, lookups, tags, footprints, th/en text, config hash vs metadata |
| 4 | มี `llmwiki/systems/data-schema.md` | **PASS** — updated for the picture format, `item.footprint`, the tag vocabulary and the 5 new rules |
| — | ย่าม 3 / อาวุธ 4 / เครื่องราง 3 / ตำรา 1 / ของเซ่น 2 / ภูติ 1 = 14 | **PASS** — matches the glossary's item list exactly |
| — | ทุกชิ้นมี tier 1-3 พร้อม merge_recipe | **PASS** — 28 recipes; rule + unit test + `merge-ladder-incomplete` fixture |
| — | ≥1 ชิ้นสายคุณไสย / ≥1 รูปทรงไม่สมมาตร | **PASS** — `item.kuman_thong` (`Curse` + `curse`); L-tetromino basket, asserted chiral |
| — | skill: projectile, pierce, aura, melee | **PASS** — 5 skills covering all four (+ the familiar's curse aura); test asserts representation |
| — | synergy 5 กฎ ด้วย tag | **PASS** — the GDD's 5 rules, all tags resolve |
| — | enemy 4 ชนิด (ปอบ 2 เฟส), floor 1-5 + wave timeline | **PASS** — 4 enemies, Pob `boss_phase2_hp_percent=50`, 9 waves, 60 s floors + a 90 s boss floor |
| — | shop_pool / economy / constant / localization | **PASS** — 14 shop rows covering every piece; 15 / 4 / 5+2 / 50 % / 15 % / chest 10; board 9×7, min cooldown, weight cap, max tier |
| — | รูปทรงเป็น string `XX.|.XX` และ parse ใน Domain | **PASS** — `Footprint.TryParse` parses the picture; ADR 0009 records the supersession of the Stage 02 format |
| — | Compile + Unity suites (DoD) | **PASS** — Unity compile clean, EditMode 12/12, PlayMode 5/5, WebGL player boots with the new data |
| — | Commit `stage-04:` | **PASS** — see the commit carrying this report |

**Gate that mattered most:** criterion 2. The validator rejected my own three-row data
mistake before any test or build ran, which is the whole point of putting game rules in
a machine-checked gate instead of in a reviewer's eye.
