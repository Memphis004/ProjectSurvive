# Stage 09i — Synergy lines on SceneCamp

สถานะ: **FAIL (ยังบัค)** — commit ไว้เพื่อเก็บงาน แต่ยังไม่ปิด Stage

## 1. สรุป

- วินิจฉัยแล้วว่าปลายเส้นเดิมคำนวณจากสูตรกริดช่อง (`top + height/2` บน space ที่ y ชี้ลง) ทำให้ปลายทุกเส้นอยู่สูงกว่ากึ่งกลางชิ้นจริงหนึ่งความสูงชิ้น (ตำรา 2×2 ≈ 102 px, ยันต์ ≈ 51 px) ตรงกับอาการที่รายงาน
- เปลี่ยน `PlaceLine`/`PlaceBadge` ให้อ่าน rect ที่วาดจริง (`RectTransform.GetWorldCorners` → `_linkRoot.InverseTransformPoint`) แทนการคำนวณซ้ำจากสูตรช่อง
- ย้าย `SynergyLines` เป็น child สุดท้ายของ board (`SetAsLastSibling`) วาดเหนือชิ้นและป้าย ใต้ badge/tooltip/ghost
- เปลี่ยนสไตล์เส้นเป็นแดงสด (0.95, 0.12, 0.10, 0.95) หนา 4 หน่วยแคนวาส + ขอบดำ bleed 3 หน่วย/ข้าง
- ป้ายในช่อง: autosize 22 → 14 px, ellipsis, margin — ไม่ล้นกรอบชิ้น
- **แต่ภาพจาก Editor ที่ 1920×1080 ยังไม่ถูกต้องตามที่ผู้ใช้ตรวจ** (เส้นยังไม่วิ่งจากกึ่งกลางตำราถึงกึ่งกลางอาวุธอย่างที่ควร และยังเห็นเส้นดำแนวตั้งเส้นที่สาม) → acceptance ด้านภาพ **FAIL**

## 2. Acceptance

| ข้อ | ผล | หลักฐาน |
|---|---|---|
| วินิจฉัยสาเหตุแกน Y ก่อนแก้ (พิมพ์ปลายเส้นเทียบ RectTransform) | PASS | วัดด้วย eval ใน Play Mode; ก่อนแก้ปลายอยู่สูงกว่ากึ่งกลางชิ้น 1 เท่าความสูงชิ้น |
| หาที่มาของเส้นดำแนวตั้งเส้นที่สาม | NOT RUN | ยังไม่ได้ระบุ — ต้องวินิจฉัยต่อ |
| คำนวณปลายเส้นจาก RectTransform ที่วาดจริง | PASS | `CampBagScreenView.TryGetDrawnRect` / `PlaceLine` |
| ลำดับการวาด: เส้นเหนือชิ้น/ป้าย ใต้ tooltip/ghost/modal | PASS (geometry) | `CampSynergyLinePlayModeTests.The_lines_are_drawn_after_every_piece_and_its_name` |
| สีแดงสด / หนา ≥3 px / ทึบ ≥0.9 / มีขอบตัดกับพื้น | PASS (ค่าที่ตั้ง) / FAIL (ภาพจริง) | `A_line_is_a_thick_opaque_red_drawn_over_a_dark_edge` |
| ป้ายในช่องไม่ล้นกรอบ 14–22 px | PASS | `A_name_in_a_cell_stays_inside_its_piece_and_is_never_below_14_px` |
| ทดสอบทุกคู่ ActiveSynergy: ปลายอยู่ในการ์ดของชิ้น ±1 px (ย้ายย่าม/หมุนชิ้น) | PASS | 7/7 tests, `CampSynergyLinePlayModeTests` |
| ภาพหลักฐานสองขนาด (1920×1080 + 1280×720) | FAIL / NOT RUN | `artifacts/ui/stage09i-diagnose-1920x1080.png`, `stage09i-after-fix-1920x1080.png` (1080p เท่านั้น) |

## 3. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **ภาพ 1280×720 ไม่มี**: การ capture ใน Play Mode ที่ render กล้องลง RenderTexture ไม่ได้ UI เลย (วัดแล้ว: ภาพเหมือนกันทุกพิกเซลเมื่อปิด canvas ทั้งสอง) ต่างจากโหมด Edit ที่ยังใช้ได้; การเปลี่ยนขนาด Game View จากสคริปต์ (`GameView.SetMainPlayModeViewSize`) ไม่มีผล — ยังต้องหาวิธี capture สองขนาดใน Play Mode
- ยังไม่รู้ที่มาของเส้นดำแนวตั้งเส้นที่สาม (อาจเป็น artefact ของ badge/ghost/เส้นที่ซ้อน) — ต้องวินิจฉัยด้วย hierarchy dump ก่อนแก้รอบถัดไป
- `ProjectSurvive/Tools/5. Capture the combo lines in Play Mode (Camp)` (เพิ่มใน `UiCaptureTool.cs`) ใช้งานไม่ได้ในโหมด Play ด้วยเหตุผลข้างต้น — ภาพที่ได้จะไม่มี UI
- เทสต์ geometry ผ่านแต่ภาพยังผิด: แปลว่า invariant ที่ assert ยังไม่ครอบสิ่งที่ตาเห็น (เช่น ทิศ/ความยาวของแท่งที่วาด หรือการที่เส้นถูกชิ้นอื่นทับ)

## 4. คำสั่งที่รันจริง

- Unity MCP `run_tests` (playmode, filter `CampSynergyLine`) → 7 passed / 0 failed
- Unity MCP `eval` (วินิจฉัยปลายเส้น, วัดการ render UI ลง RenderTexture)
- `capture_game_view` 1920×1080 → `artifacts/ui/stage09i-diagnose-1920x1080.png`, `stage09i-after-fix-1920x1080.png`
