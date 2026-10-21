# Stage 09j — combo lines only between touching pieces

สถานะ: **PASS (ตามที่ทดสอบได้)** — ภาพ 1280×720 ยัง **NOT RUN** (เหตุผลข้อ 4)

## 1. สรุป

- ต้นเหตุของเส้นที่ลากข้ามช่องว่าง: กฎ `synergy.tome_row_damage` เป็น `SameRow` (ตำรา → อาวุธ ทุกคู่ที่อยู่แถวเดียวกัน ไม่จำกัดระยะ) และ View วาดเส้น **ทุกคู่ที่ Core คืนมา** ตั้งแต่กึ่งกลางถึงกึ่งกลาง → อาวุธสองกระบอกในแถวเดียวได้ 2 เส้น เส้นที่สองพาดผ่านช่องว่างและทับตัวตำรากับชื่อไอเทม
- แก้ที่ `CampPresenter.DrawnLinks`: คู่ `SameRow`/`SameColumn` วาดเส้นก็ต่อเมื่อสองชิ้น **ติดกันทางขอบ** (`PieceInstance.IsAdjacentTo` ตัวเดียวกับที่กฎ adjacency ใช้) — ไม่แตะ Core/data/localization
- `CampBagScreenView.PlaceLine` วาดแบบ **ขอบถึงขอบ** (จุดใกล้สุดของ rect ที่วาดจริงสองอัน) จึงไม่ทับชื่อไอเทมและไม่ลากผ่านไอเทมที่สาม
- NullReferenceException: ไม่เกี่ยวกับการวาดเส้น — `CampPlayerView.Bind()` ตั้ง `_bound = true` **ก่อน** `Resolve<IGameConfig>()` ดังนั้น resolve ที่ throw จะทิ้ง view ที่ยัง Update อยู่ด้วย `_config = null` → NRE ที่ `CampPlayerView.cs:202` ทุกเฟรม (พิสูจน์โดยตั้ง `_config = null` บน view ที่ยัง bound แล้วได้ stack เดียวกับที่รายงาน) แก้โดย resolve ให้สำเร็จก่อนแล้วจึง set `_bound` + guard; เพิ่ม guard `Presenter == null` ใน `CampSceneDirector.Publish` (บรรทัด 236)

## 2. Acceptance

| ข้อ | ผล | หลักฐาน |
|---|---|---|
| เส้นปรากฏเฉพาะคู่ที่ติดกันทางขอบ | PASS | `CampPresenterTests` 4 เทสต์ใหม่ (ติด/ห่าง 1 ช่อง/ทแยง/ติด 2 ชิ้น) + `CampSynergyLinePlayModeTests.Only_the_pair_that_touches_gets_a_line` |
| Core ไม่ถูกแตะ (คู่ที่ห่างยังเป็น ActiveSynergy) | PASS | `A_tome_a_cell_away_on_the_same_row_is_a_working_combo_with_no_line`, `The_rule_still_reports_the_pair_that_gets_no_line` |
| เส้นหายทันทีเมื่อคู่ไม่เข้าเงื่อนไข / ลากกลับมาแล้วกลับมา | PASS | `A_line_appears_and_disappears_with_the_pair_it_belongs_to`, `CampBagDragPlayModeTests.Only_a_touching_pair_keeps_its_line_and_the_mark_follows_the_piece` |
| ไม่มี exception ใน Console ตอน Play ฉาก Camp | PASS | เล่นจริง สร้างสถานะครบ: console มีเฉพาะ error จาก path ของ capture tool เอง (1 รายการ) ไม่มี NRE |
| เส้นไม่ทับชื่อไอเทม | PASS (วัดจาก rect จริง) | `Every_line_bridges_the_gap_between_the_two_pieces_it_connects` + `AssertNoLineCrossesAName`; ยืนยันจากภาพด้วยการนับพิกเซลแดง (ข้อ 4) |
| ภาพ 1920×1080: ตำราติดอาวุธ (มีเส้น) และแถวเดียวกันแต่ห่าง (ไม่มีเส้น) | PASS | `artifacts/ui/stage09j-touching-1920x1080.png` (พิกเซลแดง 70), `artifacts/ui/stage09j-apart-no-line-1920x1080.png` (พิกเซลแดง 0) |
| ภาพ 1280×720 | NOT RUN | ตั้งขนาด Game View จากสคริปต์ไม่ได้ (`targetSize`, `SetPlayModeViewSize`, `SetDisplayViewSize` ไม่เปลี่ยน `Screen.width/height` — ยังเป็น 1920×1080) และ render กล้องลง RenderTexture ใน Play Mode ไม่มี UI เลย |
| ภาพ "ก่อนแก้" ที่ตรงกับภาพที่ผู้ใช้ส่งมา | NOT RUN | ต้อง checkout โค้ดก่อน Stage นี้เพื่อถ่ายซ้ำ — บันทึกภาพของผู้ใช้เป็นหลักฐานเดิม |
| WebGL / Playwright | NOT RUN | ตามคำสั่ง Stage |

## 3. การวินิจฉัย (A)

- **เส้นแต่ละเส้นมาจากคู่ไหน**: `synergy.tome_row_damage` (`SameRow`, tag_a=tome, tag_b=weapon, max_stacks=2) ให้ `ActiveSynergy` หนึ่งรายการต่ออาวุธหนึ่งกระบอกที่อยู่แถวเดียวกับตำรา — ภาพก่อนแก้มีอาวุธ 2 กระบอกในแถวเดียว จึงมี 2 คู่ = 2 เส้น
- **จุดเริ่ม**: ตั้งแต่ Stage 09i ปลายเส้นอ่านจาก `RectTransform` ที่วาดจริงแล้ว (ไม่ใช่ `CentreOf`/`PieceRect`) แต่ **ยังวาดกึ่งกลาง→กึ่งกลาง** จึงพาดผ่านชื่อไอเทมของทั้งสองชิ้น และคู่ที่อยู่ห่างกันทำให้เส้นยาวข้ามช่องว่าง/ทับชิ้นที่สาม
- **เส้นดำแนวตั้งเส้นที่สาม**: ยังไม่ได้ระบุต้นเหตุ — ต้อง dump hierarchy ของ board ตอนเกิด (ยังไม่ทำใน Stage นี้)
- **NRE**: `CampPlayerView.UpdateFocus` บรรทัด 202 (`_config.Constant`), `CampSceneDirector.Publish` บรรทัด 236 (`Presenter.IsDragging`) — ทั้งคู่เป็น lifetime guard ที่ขาด ไม่เกี่ยวกับการวาดเส้น

## 4. ภาพและข้อจำกัด

- `artifacts/ui/stage09j-touching-1920x1080.png` — ตำรา 2×2 + ยันต์ติดขอบขวา + ยันต์อีกกระบอกห่าง 1 ช่อง: เห็นเส้นแดงเส้นเดียวที่รอยต่อ, พิกเซลแดง 70
- `artifacts/ui/stage09j-apart-no-line-1920x1080.png` — ย้ายกระบอกที่ติดออกไปแถวอื่น: ไม่มีเส้น, พิกเซลแดง 0 (วัดจากไฟล์จริง)
- ยังไม่มีภาพ 1280×720 (เหตุผลในตาราง) — ความถูกต้องเชิงเรขาคณิตที่สเกล 720p ถูกคุมด้วยเทสต์ `The_marks_hold_their_geometry_when_the_canvas_is_scaled_to_the_720p_factor`
- **UiLayoutCheck**: ไม่ได้ขยาย `UiLayoutCheck` เอง เพราะมันตรวจ *regions ที่ publish ในรายงาน* และเส้นไม่ได้อยู่ในรายงาน — ข้อ "เส้นไม่ทับชื่อ" จึงถูกตรวจในเทสต์ PlayMode ด้วย rect จริงของชิ้น/ป้าย (`CampScreenRects.TryOf` + การสุ่มจุดบนเส้น) ซึ่งเป็นเงื่อนไขเดียวกัน
- ยังไม่ได้ให้ผู้ใช้ตรวจภาพทั้งสองก่อนปิด Stage

## 5. คำสั่งที่รันจริง

- Unity MCP `run_tests` editor filter `CampPresenterTests` → 28 passed / 0 failed
- Unity MCP `run_tests` playmode filter `CampSynergyLine` → 10 passed / 0 failed
- Unity MCP `run_tests` playmode filter `CampBagDrag` → 13 passed / 0 failed
- Unity MCP `eval` (วินิจฉัย `_config == null` → NRE, สร้างสถานะถ่ายภาพ, นับพิกเซลแดงจาก PNG)
- `capture_game_view` 1920×1080 สองสถานะ → `artifacts/ui/`
- `tools/test.ps1 -Suite Fast` → **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (277.6 s): dotnet 563 passed, sim determinism/streams/bot smoke, data validate/reproducible/fixtures, wiki reproducible, **Unity EditMode 163/163**; PlayMode/WebGL/Playwright NOT RUN ตามชุด Fast
- PlayMode รันแยกผ่าน Unity Test Runner (MCP): `CampSynergyLine` 10/10, `CampBagDrag` 13/13
- ไม่ได้ build WebGL/Playwright ตามคำสั่ง Stage
