# Stage 09m — a piece's colour after a move (bug fix, Presentation only)

## 1. สรุป

- ต้นเหตุ: `OnEndDrag` เรียก `_presenter.Drop()` **ก่อน** ล้าง `_selectedPiece`; `Drop()` → `Refresh()` →
  `PublishBag()` → `RenderBag()` ทำงาน**ในคอลเดียว** และ `CampPieceView.Show` อ่าน `_selectedPiece`
  ตอนวาด → ชิ้นที่เพิ่งวางถูกทาด้วยสี selection `(0.950, 0.850, 0.300, 1.000)` และไม่มี render ตามมาอีก
  จนกว่าจะมีอะไรไป repaint (วัดแล้ว: hover ชิ้นอื่น → กลับเป็น `(0.620, 0.460, 0.240, 0.950)`)
- แก้: สีของชิ้นเป็นฟังก์ชันของสถานะตอนวาด (`CampBagScreenView.PaintFor`) และ **ล้าง selection ก่อน**
  คำสั่งที่ publish บอร์ด (วาง / ยกเลิกด้วย Escape / คลิกวาง / ปิด panel) + repaint ตอนเปิด panel ใหม่
- เทสต์ PlayMode ใหม่ 5 เคส (วางสำเร็จ, วางถูกปฏิเสธ, Escape, หีบ→Board, ข้ามย่าม) — suite **25/25**
  และพิสูจน์ว่าเทสต์มีฟัน: คืนลำดับเดิมแล้วเทสต์แรก fail ด้วยค่าสีจริง
- ไม่แตะ Core, `data/`, localization; Fast suite PASS (dotnet **591**, EditMode **172/172**)

## 2. การวินิจฉัย (A) — ก่อนแก้

เทสต์ pointer จริง (`CampBagDragPlayModeTests`), พิมพ์สีทุก Image ของชิ้น (ที่มา: `GetPiecePaints`):

| ขั้น | `Piece` ของไอเทม | `ShapeCell0..8` | `Piece` ของย่าม |
| --- | --- | --- | --- |
| ก่อนลาก (hover อยู่) | `(0.620, 0.460, 0.240, 0.950)` | ปิดทั้ง 9, `(1,1,1,1)` | `(0.360, 0.520, 0.360, 0.950)` |
| ระหว่างลาก | `(0.620, 0.460, 0.240, 0.950)` | ปิดทั้ง 9 | `(0.360, 0.520, 0.360, 0.950)` |
| **หลังวาง** | **`(0.950, 0.850, 0.300, 1.000)`** ← สี selection | ปิดทั้ง 9 | `(0.360, 0.520, 0.360, 0.950)` |
| หลังเลื่อนเมาส์ออก | `(0.620, 0.460, 0.240, 0.950)` | ปิดทั้ง 9 | `(0.360, 0.520, 0.360, 0.950)` |

จุดที่ตั้งสีทั้งหมด และคำตอบว่า "คืนหรือไม่":

| จุด | ตั้งสีอะไร | คืนหรือไม่ |
| --- | --- | --- |
| `CampPieceView.Show` → `PaintShape` | สีกล่อง/ช่องรูปทรง (เดียวของชิ้น) | ตั้งใหม่ทุกครั้งที่ `Show` — ปัญหาคือ *สถานะที่มันอ่าน* ไม่ใช่ตัวสี |
| selection (`_selectedPiece`) | `_selectedPieceColor` | **ไม่คืน**: ล้างหลังคำสั่งที่ repaint ไปแล้ว |
| hover highlight (`PaintHighlight`) | เฉพาะ **ช่องบอร์ด** (`_cellHighlight`, alpha 0.65) | ไม่แตะ rect ของชิ้น จึงทิ้งสีบนชิ้นไม่ได้ |
| drag-source dim | ไม่มี — ชิ้นที่ลากถูก **ข้ามการวาด** (`Paint` คืน `used`) และ ghost ยืนแทน | ไม่มีการเขียนสี จึงไม่มีอะไรค้าง |
| ghost / carry ghost | `_ghost.color` ทุกเฟรมลาก, `HideGhost`/`HideCarryGhosts` ปิดท้าย | คืน |
| pool ของ view | `RenderBag` เรียก `Show` ให้ทุก view ที่เห็นทุกครั้ง | ไม่ใช่สาเหตุ (ภาพถูกล้างทุก render) |

สาเหตุเดียวกันนี้มี 3 ทางเข้าอีก: auto-drop ใน `Update` (ล้างหลัง `Drop`), Escape (`CancelDrag` แล้ว
**ไม่ล้างเลย** → ชิ้นค้างสี selection จนกว่าจะคลิก/ขยับ) และ `ShowPanel(false)` (ล้างหลังคำสั่ง cancel)
ทาง click-to-place มี `RenderBag` ปิดท้ายเมธอดจึงกลบอาการไว้

## 3. การแก้ (B)

- `CampBagScreenView.PaintFor(piece)` — สีคำนวณที่ตอนวาด: selection ถ้ายังเลือกอยู่ ไม่งั้นสีหมวด
  (`CampPieceView.Show` เรียกตัวนี้ ไม่เก็บค่าจากครั้งก่อน)
- `ClearSelection()` ถูกเรียก **ก่อน** คำสั่งที่ publish บอร์ด: `OnEndDrag`, auto-drop ใน `Update`,
  Escape, click-to-place, และ `ShowPanel(false)` ก่อน cancel
- `ShowPanel(true)` repaint จาก model ปัจจุบัน
- เพิ่ม accessor อ่านอย่างเดียวสำหรับหลักฐาน: `GetPiecePaints` / `CampPiecePaint`

## 4. Acceptance

| ข้อ | ผล | หลักฐาน |
| --- | --- | --- |
| A. รายงานสาเหตุ + หลักฐาน RGBA | **PASS** | §2 (ค่าจริง 4 ขั้น, ก่อน/หลังแก้) |
| B. สีเป็นฟังก์ชันของสถานะปัจจุบัน ไม่สะสม | **PASS** | `PaintFor` + `ClearSelection` ก่อนทุกคำสั่ง; `git diff` ของ `CampBagScreenView.cs` |
| C. เทสต์ 5 เคส (วางสำเร็จ / ปฏิเสธ / Escape / หีบ→Board / ข้ามย่าม) | **PASS** | `CampBagDragPlayModeTests` **25/25** (`An_item_keeps_its_category_colour_across_a_successful_drop`, `A_refused_drop_leaves_the_piece_in_its_category_colour`, `Escape_puts_the_dragged_piece_back_in_its_category_colour`, `A_piece_moved_out_of_the_chest_keeps_its_category_colour`, `A_piece_moved_across_two_bags_keeps_its_category_colour`) |
| เทสต์มีฟัน (fail บนโค้ดเดิม) | **PASS** | คืนลำดับเดิมแล้ว: `straight after a successful drop: expected RGBA(0.620, 0.460, 0.240, 0.950) but the piece reads RGBA(0.950, 0.850, 0.300, 1.000)` |
| D. ภาพ Editor capture 1920×1080 ก่อน/หลัง | **PASS** | `artifacts/ui/stage09m-before-a-move-1920x1080.png`, `stage09m-bug-after-a-move-1920x1080.png`, `stage09m-fixed-after-a-move-1920x1080.png` (+ `stage09m-before/after-1920x1080.png` จาก tool) |
| พิกเซลในภาพยืนยัน | **PASS** | พิกเซลใน rect ของชิ้น (10 px ใต้ขอบบน): ก่อน `(0.608, 0.463, 0.247)` / บั๊ก `(0.949, 0.851, 0.294)` / หลังแก้ `(0.608, 0.463, 0.247)` |
| Fast suite | **PASS** | `tools/test.ps1 -Suite Fast`: dotnet **591**, EditMode **172/172**, PASS 11 / FAIL 0 / NOT RUN 3 |
| WebGL + Playwright | **NOT RUN** | Stage นี้ไม่ใช่ gate 10/12/14/15 และไม่ต้อง build Web |

## 5. ภาพ / tool

- เพิ่ม `ProjectSurvive/Tools/6. Capture a moved piece's colour in Play Mode (Camp)` (`-piece-colour` report)
  — ลากด้วย handler ของ view เอง (press → move → release) แล้วถ่าย **Game View** ที่ 1920×1080
  พร้อมพิมพ์สีทุก Image ของชิ้นและพิกเซลใน rect ของชิ้น
- เหตุที่ใช้ Game View ไม่ใช่ off-screen `Capture`: วัดแล้วว่า path off-screen สูญเสีย panel ทั้งอันใน
  Play Mode (ภาพออกมาเหมือนภาพไม่มี panel ของ Stage 09l pixel ต่อ pixel) เพราะ canvas ของ panel ไม่ใช่
  root overlay canvas เมื่อ shell scope อยู่ข้าม scene — บันทึกไว้ ไม่ได้แก้ใน Stage นี้ (นอกขอบเขตสี)

## 6. สิ่งที่ไม่ได้ทดสอบ / ความเสี่ยง

- **ไม่เปลี่ยนสี selection เอง** (`0.95, 0.85, 0.30` = เหลืองอำพัน): ชิ้นที่ถูกเลือกค้างไว้ (คลิกครั้งแรก) ยังเหลือง
  ตามฟีเจอร์เดิม — ที่แก้คือ "จบ gesture แล้วต้องไม่เหลือ selection"
- flake เดิมของ suite ยังอยู่: ประมาณ 1 ใน 3 ของ run ติดกัน เทสต์ตัวแรกไม่ได้รับ pointer event
  (`clicking จัดย่าม did not open the panel`, drag previews 0) — เจอ 1 ครั้งระหว่างงานนี้, รายงานไว้เหมือน 09l
- **bulk** `--filter Camp` (49 เทสต์) ยัง fail บน ambient Input System log assert
  (`Cached unprocessed value...`) ตาม `docs/testing/test-suites.md` §7 — รันแยกคลาสผ่านทุกชุด
- ไม่ได้วัด performance (งานนี้ไม่แตะ hot path; `PaintFor` เป็นการย้ายการอ่าน field เดิม ไม่มี allocation)

## 7. คำสั่งที่รันจริง

```
tools/test.ps1 -Suite Fast                                             # PASS 11 / FAIL 0 / NOT RUN 3
unity command recompile / recompile_status
unity command run_tests --mode playmode --filter CampBagDragPlayModeTests   # 25/25
unity command run_tests --mode playmode --filter CampSynergyLinePlayModeTests  # 9/9
unity command run_tests --mode playmode --filter CampMergeChooserPlayModeTests # 4/4
unity command menu --path "ProjectSurvive/Tools/6. Capture a moved piece's colour in Play Mode (Camp)"
```
