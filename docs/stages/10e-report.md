# Stage 10e — a synthesised LLM wiki: generated content, schema, ingest, lint

## 1. Summary

`llmwiki/` is no longer only hand-written system notes. It has a **content layer**
(`llmwiki/content/`, 8 pages) generated from `data/` by `tools/gen-wiki.ps1`, so a
question like "มีไอเทมอะไรบ้าง / ชั้น 3 มีผีอะไร / คอมโบนี้ทำงานเมื่อไหร่" is answered
by reading one page instead of re-deriving `data/tables/*.csv` on every ask.
`llmwiki/SCHEMA.md` states the three layers (raw sources → wiki → schema), the
routing table, the query/ingest/lint cycles, and the hard rule that **the wiki is
never the source of truth for a number**: `data/` wins, and a hand-written page may
explain a number but not restate it. `tools/verify-wiki.ps1` enforces that
structurally (regeneration diff, links/anchors, data-id coverage, orphan notes, a
WARN-only balance-number heuristic) and is the new Fast step
`wiki: content reproducible`. `llmwiki/INGEST.md` + `log.md` give an agent a
deterministic procedure for turning one stage report into a note. No file under
`lib/`, `UnityProject/` or `data/` was touched and no game rule changed.

## 2. Acceptance

| # | Item | Verdict |
| --- | --- | --- |
| 1 | `tools/gen-wiki.ps1` rerun leaves `git diff` empty | **PASS** — two runs produced identical SHA-256 for all 8 pages; it runs from any cwd (root from `$PSScriptRoot`); the post-commit rerun left `llmwiki/content/` clean. |
| 2 | `tools/verify-wiki.ps1` passes the real data and FAILs 3 broken fixtures for the right reason | **PASS** — `PASS 5 \| FAIL 0 \| WARN 0`, 8 pages identical, 498 links/anchors, 43 data ids, 0 orphans, 0 WARN, **1.21 s**. Fixtures: `content_differs`, `id_missing`, `link_missing`, 3/3 rejected ([`artifacts/stage10e/wiki-fixtures.log`](../../artifacts/stage10e/wiki-fixtures.log)). The lint is read-only by default (`-WriteReport` opts in); a planted `cooldown 0.9` in a hand-written page produced a `balance_numbers` **WARN** and still exited 0 ([`warn-probe.json`](../../artifacts/stage10e/warn-probe.json)); `gen-wiki.ps1` returned **2** on a missing data directory and **1** on a missing table. |
| 3 | Every id in `data/tables` has a section, checked by lint | **PASS** — 43 ids: 11 `item`, 3 `bag`, 5 `skill`, 5 `synergy`, 4 `enemy`, 5 `floor`, 10 `upgrade_variant`; no unknown anchor. |
| 4 | Fast has the new wiki step and the other test counts did not drop | **PASS** — `PASS 11 \| FAIL 0 \| NOT RUN 3 \| WARN 0`, 237.4 s (cold; the step itself is steady); `dotnet test` **525** and EditMode **157/157** were already the counts before the stage ([`artifacts/stage10e/fast-final.log`](../../artifacts/stage10e/fast-final.log)). The wiki step is **1.4 s** in the harness, **1.15 s** inside the tool, under the ~5 s warm budget. |
| 5 | The five questions answer correctly from `llmwiki/content/` alone | **PASS** — 9/9 machine checks comparing page strings to `data/` ([`artifacts/stage10e/answers-evidence.md`](../../artifacts/stage10e/answers-evidence.md)); the table below names the pages used. |
| 6 | No file in `lib/`, `UnityProject/`, `data/` changed | **PASS for `lib/` and `data/`** — `git diff --stat -- lib data` is empty. `UnityProject/` has one **pre-existing** modified font asset that was already modified when the stage started (recorded in the session's initial `git status`); this stage wrote nothing there. |
| 7 | Report + commit | **PASS** — this file and `stage-10e: ...` (the `llmwiki/content/` pages are committed with it, per the new AGENTS DoD line). |
| 8 | (extra) Full suite with the Editor closed | **PASS** — WebGL build 17.5 s, Playwright **10/10** in Chrome, 130.6 s; suite `PASS 12 \| FAIL 0 \| NOT RUN 2`, exit 0. |

### The five questions

| # | Question | Answer (from the pages) | Pages used |
| --- | --- | --- | --- |
| ก | มีไอเทมอะไรบ้าง ชิ้นละกี่ tier | 14 pieces = 11 items + 3 bags; each authored at tier 1, merging 1→2→3 (max tier 3). | `content/index.md`, `content/items.md` |
| ข | ยันต์เพลิงกาลีทำอะไรได้และเข้าคอมโบอะไรบ้าง | Kali Flame Bolt: Projectile/Fire, damage 10, cooldown 0.9 s, 4 MP, range 6, 1 projectile. Joins Takrut–Yant (cooldown −15%), Tome Row Surge (damage +20%, max 2) and the bag-area aura (damage +10%). | `content/items.md#item.kali_flame_yant`, `content/skills.md#skill.fire_bolt`, `content/synergies.md` |
| ค | กุมารทองกับพระเครื่องวางติดกันเกิดอะไร | Curse Meets the Sacred (adjacent `curse` × `holy`): both pieces get **Debuff −30%**, 1 stack. | `content/synergies.md#synergy.curse_holy_debuff`, `content/items.md` |
| ง | ชั้น 3 มีผีอะไร กี่ตัว เมื่อไหร่ | 60 s, no boss: Krasue 16 spawns 0–25 s every 1.5 s; Nang Tani 3 spawns 25–60 s every 10 s (19 total). | `content/floors.md#floor.forest_3`, `content/enemies.md` |
| จ | อาวุธอาคม tier 1→2 มีตัวเลือกอะไรบ้าง | 5 variants: Keen Edge (+25 % damage), Swift Chant (−15 % cooldown), Frugal Mantra (−2 MP), Far Sight (+20 % range), and fire-tag Blazing Seal (+20 % damage, −1 MP). At most 3 are offered per merge; Kali sees all 5, other arcane weapons 4. | `content/upgrades.md#tier-1---2`, `content/items.md#item.kali_flame_yant` |

The wiki fixtures live under `tools/wiki-fixtures/`, not `lib/Tests~/fixtures/`: this stage
forbade writes under `lib/`. The runner copies the wiki, overlays one deliberate defect and
requires the expected finding codes — the same copy/overlay/expected-code shape as
`tools/check-fixtures.ps1`.

**Ingest with an LLM:** the workflow was exercised once, manually, on
`docs/stages/10d-report.md` → `llmwiki/systems/loop-and-scopes.md` (the facts were
already current; the missing source back-link was added) and the run was logged in
`llmwiki/log.md`. It is **not** an automated test and cannot be: it needs an LLM
and is not deterministic. The automated half is the lint.

## 3. Not tested / risks

- **Full suite, Editor closed (the web half):** `PASS 12 | FAIL 0 | NOT RUN 2`,
  225.8 s — WebGL Development build **PASS** 17.5 s and Playwright **10 passed in
  Chrome** 130.6 s; the two NOT RUNs are EditMode and PlayMode, which need the
  Editor this run keeps closed.
  [`artifacts/stage10e/full-editor-closed.log`](../../artifacts/stage10e/full-editor-closed.log).
  (An earlier Editor-open Full was `PASS 11 | FAIL 1 | NOT RUN 2`, 221.7 s; its one
  FAIL is the **known bulk PlayMode ambient-log flake** of
  [`docs/testing/test-suites.md`](../testing/test-suites.md) §7, **22/48**, not a
  change from this stage.)
- **WARN-only balance heuristic:** it catches "cooldown 0.9" beside a skill name in a
  hand-written page; it is intentionally not a hard failure, so a paraphrase that
  avoids the pattern is not detected.
- **Fragment checking** uses GitHub-style heading slugs plus explicit `<a id>`;
  an exotic heading could need an explicit anchor to be linkable.
- Wiki content is only as fresh as the last `gen-wiki.ps1` run. The new Fast step
  turns a stale page into a red build, which is the point.
- The lint found a real defect while being built: `llmwiki/stage-log.md` linked
  `../../artifacts/...`, one `..` outside the repo; it is now `../artifacts/...`.

## 4. Commands actually run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1 -Fixtures
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full
cd /tmp ; powershell -NoProfile -ExecutionPolicy Bypass -File <repo>/tools/gen-wiki.ps1 -Quiet
git diff --stat -- lib data
```
