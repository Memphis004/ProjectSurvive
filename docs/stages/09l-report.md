# Stage 09l — the จัดย่าม screen: the tome's line, the bag's real shape, and the drag

## 1. สรุป

- **เส้นเชื่อม** เลือกด้วย **แท็กของกฎเอง** (`tome`+`weapon` ใน `data/tables/synergy.csv`) ไม่ใช่ `kind` แล้ว
  และวาดจาก **กึ่งกลางตำราไปกึ่งกลางอาวุธ** (อ่าน rect ที่วาดจริง) จึงขึ้นครบทั้งซ้าย/ขวา/บน/ล่าง คู่ละเส้น
  กฎ adjacency อีกสามข้อไม่เปลี่ยน (ไฮไลต์อย่างเดียว) — [ADR 0032](../adr/0032-a-touching-tome-draws-centre-to-centre.md)
- **ย่ามวาดตามรูปทรงจริง**: `CampPieceView` วาดหนึ่งช่องต่อหนึ่ง cell ที่ `Footprint` ครอบ (ตะกร้า L = 4 ช่อง
  ไม่ใช่กล่อง 2×3) และชื่อย่ามใช้ความกว้างของแถวบนที่ครอบจริง
- **การลากวาง**: grab offset มีอยู่แล้วตั้งแต่ Stage 09k และตรวจซ้ำด้วย pointer จริง — ไม่ต้องแก้ (ข้อ D ไม่เข้าเงื่อนไข)
  ต้นเหตุ "วางบนตะกร้า L ไม่ได้" คือ **กติกา Core** (`inventory.item_not_covered`) ไม่ใช่บั๊ก UI
- Fast suite PASS (dotnet **573**, EditMode **167/167**), PlayMode camp: synergy **9/9**, drag **20/20** (มี flake เดิม)

## 2. การวินิจฉัย (A) — ก่อนแก้

| ข้อ | สิ่งที่พบ | หลักฐาน |
| --- | --- | --- |
| A.1 | **มี grab offset แล้ว** (Stage 09k): `DragStart(piece, rotation, grabbedCell)` เก็บ cell ที่กดใน footprint ของชิ้น แล้ว `OriginFor` = ช่องใต้เมาส์ − offset (หมุนด้วย `FootprintRotation`) — วัดจริง: ตำรา 2×2 ที่ (1,1) จับช่องล่างขวา (2,2) ลากเมาส์ไป (3,2) → `Drag.Origin = (2,1)`, ghost 160×160 ที่ origin เดียวกัน, `ErrorCode` ว่าง | `CampBagDragPlayModeTests.The_tome_drops_across_the_cloth_bag_and_the_L_where_the_ghost_shows_it` (pointer จริง), `CampPresenterTests.The_cell_a_player_grabs_stays_under_the_pointer` (1×1 + 4 มุมของ 2×2), `A_turned_piece_keeps...` (หมุน 90°), `The_L_shaped_bag_keeps_its_grabbed_cell_through_a_turn` |
| A.2 | ตำรา 2×2 บนตะกร้า L **ใบเดียว** ถูกปฏิเสธด้วย `inventory.item_not_covered` (L มี 4 ช่องในกล่อง 2×3) และวางคร่อมย่ามผ้าดิบ+ตะกร้า L ที่ครอบครบ 4 ช่อง **ผ่าน** — เป็นกติกา ไม่ใช่ UI | Core (อ่านอย่างเดียว): `BackpackEditorTests.A_two_by_two_item_on_one_L_basket_alone_is_refused`, `The_same_item_across_two_bags_that_cover_its_four_cells_is_accepted` |
| A.3 | **ใช่** — `CampPieceView.Show` ตั้ง rect เป็น `Shape.Width × Shape.Height` แล้ววาด `Image` เดียว จึงทาช่องมุมที่ L ไม่มี ภาพก่อนแก้จึงเห็นเป็นกล่อง 2×3 | โค้ดก่อนแก้ (diff ของ `CampBagScreenView`), PlayMode `The_L_basket_is_drawn_as_its_four_covered_cells` (อ่าน rect ที่วาดจริง = 4 ช่อง, ไม่มี rect ทับมุมว่าง) |
| A.4 | ไม่ใช่ Core: กฎกลายเป็น `AdjacentPair` ตั้งแต่ 09k-data แต่ `DrawnLinks` มีcase `SameRow` เท่านั้น (พร้อม `TryHorizontalPairCells` ที่บังคับแถวเดียวกัน) → คู่ที่ติดบน/ล่างตกไปที่ `default:` = ไม่มีมาร์ค | โค้ดก่อนแก้ + EditMode `With_the_shipped_touching_rule_the_pair_draws_exactly_one_line` (09k pin ถูกกลับด้าน) |
| A.5 | **ไม่พบ** NullReferenceException ในการรันของ Stage นี้ (console ground truth: `consoleErrors 0`, `compilationFailed false` ตลอดการรัน capture/PlayMode) | `unity command console_status` ท้าย Stage |

## 3. Acceptance

| ข้อ | ผล | หลักฐาน |
| --- | --- | --- |
| ก. เส้นขึ้นเฉพาะคู่ติดกัน 4 ทิศ, ห่าง/ทแยงไม่มีเส้น, ผลโบนัสตรงกฎ | **PASS** | EditMode `A_weapon_on_each_of_the_tomes_four_sides_draws_one_line_per_pair` (4 ทิศ ในลูป), `A_tome_one_cell_away...`/`A_tome_touching...corner...`; PlayMode `The_tome_gets_one_line_per_weapon_on_its_four_sides` |
| ข. เส้นจากกึ่งกลางตำรา→กึ่งกลางอาวุธ, คู่ละเส้น, ตำรา 2×2 กับอาวุธ 2 ชิ้น = 2 เส้น | **PASS** | PlayMode `Every_line_runs_between_the_two_pieces_middles` (ปลายอยู่ที่ centre ของ rect ทั้งสอง ขนาด 1 px), `A_tome_that_touches_two_weapons_draws_two_lines` |
| ค. ภาพย่ามตรงกับช่องที่ครอบจริง (L = 4 ช่อง) | **PASS** | PlayMode `The_L_basket_is_drawn_as_its_four_covered_cells` + `GetPieceCellScreenRects` |
| ง. ลากวางตรงที่ ghost แสดง, คร่อมสองย่ามได้, L ใบเดียวถูกปฏิเสธ, ไม่มี ghost/selection ค้าง | **PASS** | PlayMode (pointer จริง): `The_tome_drops_across_the_cloth_bag_and_the_L_where_the_ghost_shows_it` (origin = (2,1) = ใต้ ghost, วางแล้ว rect ตรง ghost), `A_tome_placed_on_the_L_basket_alone_is_refused_and_leaves_nothing_behind` (refused, ghost/selection ถูกล้าง, ชิ้นกลับที่เดิม), `A_weapon_dragged_above_the_tome_gets_a_line_and_loses_it_when_dragged_away` |
| จ. ไม่มี exception ใน Console | **PASS** | `console_status`: `consoleErrors 0`, ไม่มี NRE |
| ฉ. สองขนาดไม่มี element ทับกัน | **PASS (ตัวเลข)** | capture tool: 0 overlaps ทุกขนาด; PlayMode `The_marks_hold_their_geometry_when_the_canvas_is_scaled_to_the_720p_factor` |
| ช. Fast suite | **PASS** | `tools/test.ps1 -Suite Fast`: dotnet 573, EditMode 167/167, PASS 11 / FAIL 0 |
| ซ. WebGL / Playwright | **NOT RUN** | ตามคำสั่ง Stage (ไม่ต้อง build Web) |

## 4. ภาพหลักฐาน (`artifacts/ui/`)

- `stage09l-four-sides-1920x1080.png` — ตำราที่มีอาวุธ 4 ด้าน (อ่านกลับจากพิกเซล: 1 724 px สีแดงของเส้น)
- `stage09l-mid-drag-1920x1080.png` — ระหว่างลากตำราไป (5,1) ข้ามรอยต่อย่ามผ้าดิบ+ตะกร้า L; `Drag.Origin=(5,1)`,
  `IsValid=true`, ghost 160×160
- **ไม่มีภาพ "ก่อนแก้"**: tool `Tools/5. Capture …` ใน environment นี้เรนเดอร์ภาพโดย **ไม่มี panel** (ภาพ 3 สถานะ
  ออกมา byte-identical, พิกเซลเป็นสีพื้นโลก ไม่มีสี panel) จึงใช้ `unity capture_game_view` (Game View จริง) แทน
  — ข้อบกพร่องของ tool ถูกบันทึกไว้ ไม่ได้แก้ใน Stage นี้ (นอกขอบเขต Presentation)

## 5. สิ่งที่ไม่ได้ทดสอบ / ความเสี่ยง

- **`CampBagDragPlayModeTests` มี flake เดิม**: ประมาณ 1 ใน 3 การรัน ตอนรันติดกัน เทสต์ตัวแรกของ run ไม่ได้รับ
  pointer event เลย (จุดมุ่งตรง = จุดที่ปุ่มประกาศเอง (960,254), `dragPreviews 0`; คลิกซ้ำ/สร้าง Mouse ใหม่/
  เปิด-ปิด module ไม่ช่วย) — ทุกเทสต์ผ่านเมื่อรันเดี่ยวหรือเมื่อ run ไม่โดน; บันทึกไว้ ไม่ใช่ PASS ของ "เสถียรภาพ suite"
- `CampSynergyLinePlayModeTests` ถูกเขียนใหม่ทั้งไฟล์ตามกฎใหม่ (9 เทสต์) — เคส "กฎยังรายงานคู่ที่ไม่มีเส้น" ถูกถอด
  เพราะ Core ไม่รายงานคู่นั้นแล้วตั้งแต่ ADR 0031
- ghost ของย่าม L ยังเป็นกล่อง 2×3 ระหว่างลาก (ไม่ใช่รูปทรง) — นอกคำขอของ Stage นี้
- ไม่มีการวัด performance เพิ่ม (งานนี้ไม่แตะ hot path; ไม่มี allocation ใหม่ต่อ pointer move:
  strip ของย่ามถูกสร้างตอน pool สร้าง view เท่านั้น)

## 6. คำสั่งที่รันจริง

```
tools/test.ps1 -Suite Fast                       # PASS 11 / FAIL 0 / NOT RUN 3
unity command menu "ProjectSurvive/Tools/1. Capture UI"   # new steps: four sides, L basket mid-drag/dropped
unity command run_tests --mode playmode --filter CampSynergyLinePlayModeTests   # 9/9
unity command run_tests --mode playmode --filter CampBagDragPlayModeTests       # 20/20 (run ที่ผ่าน)
dotnet test lib/Tests~/ProjectSurvive.Core.sln --filter BackpackEditorTests     # 20/20
```
