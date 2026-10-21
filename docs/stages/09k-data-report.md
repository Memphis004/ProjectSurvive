# Stage 09k-data — ตำราพระเวทต้องวางติดอาวุธ ไม่ใช่แถวเดียวกัน

สถานะ: **PASS (ตามที่ทดสอบได้)** — PlayMode ที่อ้างเส้นคอมโบและ WebGL **NOT RUN** (เหตุผลข้อ 3)

## 1. สรุป

- `data/tables/synergy.csv` แถว `synergy.tome_row_damage` เปลี่ยน **`SameRow` → `AdjacentPair`** โดยคง
  id / tag_a=tome / tag_b=weapon / `Damage` +20 / `max_stacks` 2 เดิม — ไม่แตะโค้ด ไม่แตะ schema
  `SchemaVersion` จึงไม่ขยับ (config hash `D5B4813E…` → `F651981D…` ตามข้อมูลที่เปลี่ยนเท่านั้น)
- ความหมายไม่เปลี่ยนจากเดิม: **นับคู่ (ตำรา, อาวุธ) ครั้งเดียว** ไม่ว่าจะติดกันกี่ช่อง (evaluator นับ
  ชิ้น ไม่นับ cell อยู่แล้ว) และ **`max_stacks` 2 = อาวุธหนึ่งชิ้นรับจากตำราได้สูงสุด 2 เล่ม** (เป็น
  per-target อยู่แล้ว) ที่เปลี่ยนคือ *ตำราเล่มไหนเข้าเงื่อนไข* — เล่มที่แตะ ไม่ใช่เล่มที่อยู่แถวเดียวกัน
- ข้อความ `item.spell_tome.desc` (th/en) บอก "วางติดกัน" แทน "แถวเดียวกัน" และชื่ออังกฤษของกฎ
  `Tome Row Surge` → `Touching Tome` (key เดิม ไม่มี key ใหม่; ชื่อไทย "ตำราหนุนอาคม" ไม่ต้องแก้)
- เทสต์ Core เขียนใหม่เป็นกติกาสัมผัส (4 ทิศ / ห่าง 1 ช่อง / ทแยง / ตำรา 2×2 กับอาวุธ 2 ชิ้น / เพดาน 3 เล่ม
  / ย้ายแล้วคำนวณซ้ำ + Revision ครั้งเดียว) และเพิ่ม randomized 10,000 คำสั่งที่คำนวณคู่จาก cell เอง
- **ผลข้างเคียงที่รู้ตัว (งานของ 09l):** เส้นคอมโบถาวรของกฎนี้หายไป เพราะ `CampPresenter.DrawnLinks`
  เลือกสไตล์จาก `synergy.kind` — ดู §5

## 2. Acceptance

| ข้อ | ผล | หลักฐาน |
|---|---|---|
| 1. `synergy.tome_row_damage` เป็น `AdjacentPair`, id/stat/value/max_stacks เดิม, semantics 2×2 = นับคู่ครั้งเดียว, `max_stacks` per target เท่าเดิม | PASS | `data/tables/synergy.csv`; `A_tome_touching_a_weapon_on_any_of_the_four_edges_adds_damage`, `A_weapon_touching_three_tomes_takes_at_most_two_stacks`, `A_tome_touching_two_weapons_on_different_rows_empowers_both` |
| 2. localization th/en ของ `item.spell_tome.desc` เป็น "วางติดกัน", ไม่เพิ่ม key | PASS | `data/localization/localization.csv` (th `ตำราที่ขยายพลังอาวุธอาคมที่วางติดกัน`, en `…placed against it.`; ชื่อกฎ en `Touching Tome`) |
| 3. gen / validate-data / verify-generated / check-fixtures / gen-wiki + commit `llmwiki/content/` ; `SchemaVersion` ไม่เปลี่ยน | PASS | `RESULT: OK` ทั้ง 4 gate (validate: 0 findings, fixtures: 24/24 rejected), `wiki: content reproducible` ใน Fast; diff ของ `ConfigBuildMetadata.g.cs` มีแต่ `ConfigHash` |
| 4. เทสต์ SameRow ของกฎนี้ถูกแก้เป็นกติกาใหม่ + เทสต์ dotnet ครบ 7 กรณี + randomized 10,000 | PASS | dotnet **571** passed; `SynergyEvaluatorTests` (four-edge `[Theory]`, gap, corner, two-weapons, cap, move+revision), `BackpackRandomizedTests.Ten_thousand_random_weapon_positions_re_derive_the_touching_pair_from_the_cells` (+ invariant ใน 10,000-command walk เดิม), `LoadoutResolverTests` comment; EditMode **169/169** (line cases re-point ที่ `SameRow` เอง + 1 case ใหม่). `UpgradeOfferTests` ไม่ต้องแก้: ตัวเลขที่เขียนมือ (19/18) เป็นเลย์เอาต์ที่ไม่มีตำรา จึงไม่เคยผ่านกฎนี้ — ตรวจแล้วว่า 571 ผ่านทั้งหมด |
| 5. `tools/sim.ps1 --seeds 200` 4 build ก่อน-หลัง + ตาราง win rate / EndReason | PASS — ผลใน §4 ด้านล่าง; หลุดช่วง 30–60 % เฉพาะ familiar 31.0 % และ melee 37.8 % (ไม่จูนตามคำสั่ง) | `artifacts/tome-adjacent/{before,after}-{default,ranged,melee,familiar}.csv` |
| 6. GDD / glossary / backpack.md / data-schema.md / ADR ใหม่ที่ supersede 0015 / stage-log / knowledge | PASS | `docs/gdd/prototype-v0.3.md` (2 บรรทัด), `llmwiki/glossary.md` (2 แถว), `llmwiki/systems/backpack.md`, `llmwiki/systems/data-schema.md`, [ADR 0031](../adr/0031-a-tome-touches-its-weapon.md) + สถานะใหม่ของ [ADR 0015](../adr/0015-same-line-synergy-kinds.md), `llmwiki/stage-log.md`, `knowledge.md` |
| 7. บันทึกว่า 09l ต้องตามแก้อะไร + สภาพ UI ตอนนี้ | PASS (บันทึก) — ยังไม่แก้ UI | §5 ด้านล่าง + EditMode `With_the_shipped_touching_rule_the_pair_is_a_combo_and_draws_no_permanent_mark` |
| `tools/test.ps1 -Suite Fast` | PASS | `artifacts/test-summary.md` (dotnet 571, data gates, wiki, Unity EditMode 169/169) |
| WebGL / Playwright | NOT RUN | ตามคำสั่ง Stage (ไม่มี gate) |

## 3. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **PlayMode ที่วัดเส้นคอมโบจะแดงจนถึง 09l**: `CampSynergyLinePlayModeTests` (10) และ 2 เคสเส้นใน
  `CampBagDragPlayModeTests` สร้างเลย์เอาต์ "ติด vs ห่างหนึ่งช่อง" แล้วคาดหวังเส้นจากกฎนี้ — ตอนนี้คู่ที่ห่าง
  *ไม่ใช่คอมโบเลย* และคู่ที่ติดไม่มีเส้นถาวร ทั้งสองไฟล์ไม่ถูกแก้ใน Stage นี้ (ห้ามแตะ UI) และไม่ได้รัน
  (Fast ไม่รัน PlayMode)
- **บอท greedy แทบไม่วางตำราให้ติดอาวุธ** — sim จึงเป็นเครื่องมือที่อ่อนมากกับกฎนี้: 4 build × 200 seed
  มีเพียง 3 seed ของ default ที่แถว CSV เปลี่ยน (ผลสุทธิ -1 ชนะ) และ ranged/melee/familiar เหมือนเดิม
  ทุกแถว ⇒ "เกมไม่เปลี่ยน" เป็นข้อจำกัดของเครื่องมือ ไม่ใช่ข้อสรุปของบาลานซ์
- **ห้ามจูน data ในรอบนี้** ตามคำสั่ง; ตัวเลขที่หลุดช่วง 30–60 % (familiar 31.0 %, melee 37.8 %) เป็น
  สถานะเดิมก่อน Stage นี้ (ก่อน/หลังเท่ากัน) ไม่ได้แก้
- ยังไม่มีภาพหน้าจอของสถานะ UI ใหม่ (ข้อ 7) — Stage นี้ไม่ต้อง build Web และ UI อยู่ใน 09l; ข้อสรุปเรื่อง
  "เส้นหาย" มาจากโค้ด `DrawnLinks` + EditMode test ไม่ใช่จากภาพ

## 4. บาลานซ์ (ก่อน → หลัง, 200 seeds ต่อ build)

| build | ชนะบอส ก่อน | หลัง | Lost/PlayerDied | Lost/Timeout | Won/BossDefeated |
|---|---|---|---|---|---|
| default | 90/200 = **45.0 %** | 89/200 = **44.5 %** | 24 → 23 | 86 → 88 | 90 → 89 |
| ranged | 116/200 = **58.0 %** | 116/200 = **58.0 %** | 29 → 29 | 55 → 55 | 116 → 116 |
| melee | 71/188 = **37.8 %** | 71/188 = **37.8 %** | 48 → 48 | 69 → 69 | 71 → 71 |
| familiar | 62/200 = **31.0 %** | 62/200 = **31.0 %** | 33 → 33 | 105 → 105 | 62 → 62 |

(melee: 188 คือจำนวน seed ที่ไปถึงชั้น 5; ตัวส่วนของอัตราชนะบอสคือผู้ที่ไปถึงบอส)

## 5. 09l ต้องตามแก้อะไร (ยังไม่แก้ใน Stage นี้)

- `CampPresenter.DrawnLinks` เลือกสไตล์จาก `synergy.kind`: `SameRow` → `CampLinkStyle.Line`, `SameColumn`
  → ไม่มี, `BagArea` → `Badge`, **`AdjacentPair` → ไม่มีอะไรถาวร** (มีแต่ hover highlight)
  ⇒ กฎตำราไม่มีเส้นถาวรแล้ว แม้คู่จะติดกัน; 09l ต้องให้ `AdjacentPair` มีเส้นของตัวเอง (อ่านสอง cell
  แบบเดียวกับที่เคส `SameRow` ทำวันนี้ — `TryHorizontalPairCells` รองรับแค่แนวนอน ถ้าต้องรองรับบน/ล่างต้อง
  ขยาย)
- **สิ่งที่ยังทำงานอยู่:** hover highlight ยังสว่างทั้งคู่ (BuildHighlight ไม่ดู kind) และ tooltip ยังบอกชื่อ
  คอมโบ (อ่านจาก `ActiveSynergy`) — คู่ที่ติดกันจึงยัง "เห็นได้" แต่ไม่มีเส้นถาวร
- ไฟล์เทสต์ที่ 09l ต้องเขียนใหม่: `CampSynergyLinePlayModeTests` (10) และ 2 เคสเส้นใน
  `CampBagDragPlayModeTests`

## 6. คำสั่งที่รันจริง

- `pwsh -NoProfile -File tools/gen.ps1` → configHash `F651981D…` (รันสองครั้ง: kind+desc แล้วชื่อกฎ; hash หลังการแก้ครั้งสุดท้าย)
- `pwsh -NoProfile -File tools/validate-data.ps1 -DataDir "lib/Tests~/data"` → PASS 5 / FAIL 0
- `pwsh -NoProfile -File tools/verify-generated.ps1` → PASS 8 / WARN 1 ก่อน commit (ไฟล์ generated ที่ยังไม่ commit); รันซ้ำบน tree ที่ commit แล้วได้ PASS 9 / WARN 0
- `pwsh -NoProfile -File tools/check-fixtures.ps1` → PASS 28 / FAIL 0
- `pwsh -NoProfile -File tools/gen-wiki.ps1` → 8 หน้า (content เปลี่ยน 2 หน้า)
- `dotnet test "lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj" -c Debug` → **571 passed / 0 failed** (ทั้ง solution ถูก build ในขั้น `dotnet build` ของ Fast suite)
- Unity Test Runner (MCP): EditMode ทั้ง assembly **169/169**; `CampPresenterTests` **34/34**
- `pwsh -NoProfile -File tools/sim.ps1 --seeds 200 --build {default,ranged,melee,familiar}` (ก่อนและหลัง)
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast` → **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (136.8 s; dotnet 571, EditMode 169/169 — รันซ้ำหลังแก้ชื่อกฎ; ดู `artifacts/test-summary.md` และ `artifacts/stage09k-data-fast.log`)
- ไม่ได้ build WebGL / ไม่ได้รัน Playwright ตามคำสั่ง Stage
