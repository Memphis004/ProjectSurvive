# Stage 09k — ลากจากช่องที่กด, ghost ตรงที่วาง, เส้นคอมโบนอน และชื่ออยู่เหนือเส้น

สถานะ: **PASS (ตามที่ทดสอบได้)** — ภาพ 720p และ Full suite **NOT RUN**, PlayMode ทั้งชุดในครั้งเดียว **FAIL** (เหตุผลข้อ 3)

## 1. สรุป

- **ต้นเหตุ "ลากแล้วเพี้ยนไปหนึ่งช่อง"**: `CampPresenter.DragStart` ใช้ *ช่องใต้ pointer* เป็น `Origin` ของชิ้นเสมอ ชิ้น 2×2 ที่กดมุมล่างขวาจึงถูกวางโดยให้มุมล่างขวานั้นเป็นมุมบนซ้าย → ghost กับผลลัพธ์เยื้องขึ้น-ซ้าย 1 ช่อง และ ghost ที่เห็นเหนือช่องว่าง 4 ช่องอาจถูกวางด้วย origin นอกย่าม
- แก้ที่ Presenter: `DragStart(piece, rotation, grabbedCell)` เก็บ *ช่องที่กด* เป็น cell ของ footprint ของชิ้นนั้น (`_grabShape/_grabCell/_grabRotation`) แล้วทุก `DragMove`/`RotateDrag` คำนวณ origin ผ่าน `OriginFor` = pointer − ช่องที่กด (หมุนตามชิ้นด้วย `FootprintRotation` ตัวเดียวกับที่ bag rider ใช้, ADR 0020) — ghost และคำสั่งวางอ่าน origin เดียวกันจึงตรงกันเสมอ; `DragStart(piece, rotation)` เดิมยังอยู่สำหรับ click-to-place และชิ้นที่ลากออกจากหีบ (ไม่มี grab offset)
- **เส้นคอมโบ**: `CampLink` พกสอง cell (`SourceCell`/`TargetCell`) ที่ Presenter เลือกเอง (คู่ `SameRow` ที่ติดขอบ: ช่องขวาสุดของตำรา → ช่องซ้ายสุดของอาวุธ) View แปลง cell → จุดแล้วลากเส้น จึงเป็นแนวนอนโดยโครงสร้าง ไม่ใช่ "เส้นสั้นระหว่างขอบ" แบบ 09j; คู่ `SameColumn` ไม่ได้เส้นแล้ว (ยังเป็นคอมโบและยังขึ้น tooltip/hover) — เปลี่ยนพฤติกรรมจาก 09j โดยตั้งใจ
- **ชื่อไอเทม**: ป้ายชื่อย้ายไปคอนเทนเนอร์ของตัวเอง (`PieceLabels`/`ChestLabels`) ที่อยู่เหนือคอนเทนเนอร์เส้นในลำดับ sibling และถูกยกขึ้นทุกครั้งที่วาด (`EnsureLinkRoots`) → เส้นทับไม่ได้แม้จะพาดผ่านกลางแถว
- **ปล่อยแล้วค้าง selection**: `Drop` ล้าง `_selected` ก่อนจบ gesture ไม่ว่าผลจะสำเร็จหรือถูกปฏิเสธ (ก่อนหน้านี้ `OnClick` ที่ยิงตามหลัง drag ทำให้ชิ้นใต้ pointer ถูกเพ้นต์เป็น "เลือกอยู่" ทั้งที่ไม่มี gesture)

## 2. Acceptance

| ข้อ | ผล | หลักฐาน |
|---|---|---|
| ช่องที่กดอยู่ใต้ pointer ตลอดการลาก และยังอยู่หลังหมุนระหว่างลาก | PASS | `CampPresenterTests.The_cell_a_player_grabs_stays_under_the_pointer`, `A_turned_piece_keeps_the_grabbed_cell_under_the_pointer`, `The_L_shaped_bag_keeps_its_grabbed_cell_through_a_turn` (33/33) |
| ghost อยู่ที่ตำแหน่งที่วางจริง (กดมุมล่างขวาของตำรา 2×2) | PASS | `CampBagDragPlayModeTests.A_tome_grabbed_by_its_lower_corner_drops_where_the_ghost_showed` (16/16) + `artifacts/ui/stage09k-drag-ghost-1920x1080.png`: ghost 2×2 ช่วง x 368–526, y 304–462 (6400 พิกเซลเขียวที่สุ่ม) |
| เส้นคอมโบเป็นแนวนอนระหว่างสองช่องที่ติดกัน ไม่ลากผ่านช่องว่าง/ชิ้นที่สาม | PASS | `CampSynergyLinePlayModeTests.Every_line_is_level_and_joins_the_two_cells_it_names`, `Only_the_pair_that_touches_gets_a_line` (10/10) + ภาพ: แดง 4 แถวที่ y 259–262 กว้าง 82 px (= 1 cell pitch) |
| ชื่อไอเทมไม่ถูกเส้นทับ | PASS (ด้วยลำดับ layer) | `The_lines_are_drawn_after_every_piece_and_before_every_name` + `AssertTheNamesAreDrawnOverTheLines` (คอนเทนเนอร์เส้นอยู่ก่อนทุก `LabelBox`) |
| ปล่อยแล้วไม่ค้าง ghost/highlight/selection ทั้งกรณีสำเร็จและถูกปฏิเสธ | PASS | `A_refused_drop_clears_the_ghost_the_highlight_and_the_selection`, `A_refused_drop_leaves_the_piece_where_it_was` |
| คู่ที่ห่างกันยังเป็นคอมโบ (ไม่มีเส้น) — ไม่ regress | PASS | `The_rule_still_reports_the_pair_that_gets_no_line`, `A_tome_a_cell_away_on_the_same_row_is_a_working_combo_with_no_line` |
| ภาพจริง 1920×1080 สามสถานะ (ลาก/หลังปล่อย/เส้น) | PASS — **รอผู้ใช้ตรวจภาพ** | `artifacts/ui/stage09k-drag-ghost-1920x1080.png`, `stage09k-after-drop-1920x1080.png`, `stage09k-lines-1920x1080.png` |
| ภาพ 1280×720 | NOT RUN | ตั้งขนาด Game View จากสคริปต์ไม่ได้ และ render กล้องลง RenderTexture ใน Play Mode ไม่มี UI (วัดซ้ำใน Stage นี้แล้ว); เรขาคณิตที่สเกล 720p คุมด้วย `The_marks_hold_their_geometry_when_the_canvas_is_scaled_to_the_720p_factor` |
| Full suite / WebGL / Playwright | NOT RUN | ตามคำสั่ง Stage (gate อยู่ที่ 12/14/15) |

## 3. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **PlayMode ทั้ง assembly ในครั้งเดียว FAIL (30/62)** — ทุกกรณีที่ล้มล้ม *ตอน setup* ด้วยข้อผิดพลาดภายใน Input System (`IndexOutOfRangeException` ใน `InputManagerStateMonitors.AddStateChangeMonitor` ตอน `InputSystemUIInputModule.OnEnable`, และ `Cached unprocessed value unexpectedly became outdated`) ไม่ใช่ assertion ของเทสต์; fixture ที่ล้ม ได้แก่ `CampBagDrag`, `CampMergeChooser`, `CampWorld` และ `TitleScene` ซึ่ง **ไม่มีไฟล์ไหนถูกแก้ใน Stage นี้** รันแยกทีละ fixture ผ่านหมด (`CampBagDrag` 16/16, `TitleScene` 5/5, `CampSynergyLine` 10/10) ยังไม่ได้พิสูจน์ว่าเป็นปัญหาก่อน Stage นี้ — ต้องรัน Full suite บน commit ก่อนหน้าเพื่อยืนยัน
- **คู่ `SameColumn` ไม่มีเส้นแล้ว** (เดิม 09j วาดเส้นแนวตั้งเมื่อติดกัน) เป็นการตีความ "เส้นต้องนอนและอยู่บนรอยต่อของสองช่อง" — ถ้าต้องการเส้นแนวตั้งด้วยต้องเปิดเป็นงานใหม่
- ภาพ 1920×1080 เท่านั้น; `stage09k-after-drop` ไม่มีพิกเซลแดงเพราะตำราถูกวางห่างอาวุธ (ไม่มีคู่ติดกัน) — ภาพจึงยืนยัน "ghost หาย" แต่ไม่ได้ยืนยันเส้น
- 09k ไม่แตะ Core/data/localization/ฉาก — ไม่มี `tools/gen-wiki.ps1` ที่ต้องรัน

## 4. คำสั่งที่รันจริง

- `tools/test.ps1 -Suite Fast` → **PASS 11 | FAIL 0 | NOT RUN 3 | WARN 0** (273.7 s): dotnet **563**, sim determinism/streams/bot smoke, data validate/reproducible/fixtures, wiki reproducible, Unity EditMode **168/168** (`artifacts/stage09k/fast.log`)
- Unity Test Runner (MCP): EditMode `CampPresenterTests` **33/33**; PlayMode `CampBagDragPlayModeTests` **16/16**, `CampSynergyLinePlayModeTests` **10/10**, `TitleScenePlayModeTests` **5/5**; PlayMode ทั้ง assembly **32/62** (เหตุผลข้อ 3)
- `capture_game_view` 1920×1080 กลาง Play Mode (ฉาก Camp ที่เดินเข้าจาก Title จริง) → `artifacts/ui/stage09k-*.png`
- วัดพิกเซลด้วย PIL: ghost bbox/จำนวนพิกเซลเขียว, แถวพิกเซลแดงของเส้น, ขนาดภาพ
