# Stage 06b — a sala's stock does not follow the run (bug fix)

**Scope:** the reported defect only. No feature, no balance change, no `data/`, no proto.

## 1. สรุป

ผู้เล่นซื้อของจากชั้นวางในศาลาหนึ่งแล้ว **ช่องนั้นว่างตลอดทุกศาลาถัดไปของ run นั้น** และเสี่ยงเซียมซี
ก็ไม่เติมให้ เพราะ `IsPurchased` ไม่ถูกล้างตอนเข้าศาลาใหม่. `ShopSlot.Clear()` ทำหน้าที่แค่
"เอา offer ออก" (ซึ่งถูกต้องสำหรับ `MarkPurchased()` ที่ต้องคงธงเช่าไว้) แต่ `ShopStock.BeginCamp()`
ก็เรียกตัวเดียวกันนี้ทั้งที่ต้องการ "ศาลาใหม่" — ต่างกันสองเจตนา. แยกเป็น `ShopSlot.ResetForCamp()`
(ล้าง offer + `IsPurchased` + `IsLocked`) ให้ `BeginCamp()` เรียกแทน; `Clear()` ยังคงเป็นของ re-roll
และการเช่าเหมือนเดิม. กติกาเดิมทุกข้อไม่ขยับ: ซื้อช่องเดิมซ้ำในศาลาเดียวกันไม่ได้, ช่องที่ซื้อแล้วไม่เติมฟรี
ในศาลาเดียวกัน, เปิดร้านซ้ำสินค้าเดิม, refresh ไม่แตะช่องที่ล็อก. Presentation ไม่มีส่วน —
`CampPresenter.PublishShop()` อ่าน `run.Stock` สด ๆ ทุก `Refresh()` และ `CampWorldView.RenderStations()`
เป็น renderer ล้วนของ array นั้น (ตรวจแล้ว; ไม่มี shelf cache ข้ามฉาก).

## 2. Acceptance ทีละข้อ

| Acceptance | ผล | หลักฐาน |
| --- | --- | --- |
| เทสต์ A **ล้มก่อนแก้** | **PASS** | `artifacts/stage06b/dotnet-before-fix.log` — `Failed: 6, Passed: 15, Total: 21`; ข้อความคือ `Assert.Equal() Failure: Values differ / Expected: 4 / Actual: 3` (Theory ช่อง 0–3), `Actual: 2` (ซื้อ 2 ช่อง), `Actual: 0` (ซื้อทั้ง 4 ช่อง) |
| เทสต์ A **ผ่านหลังแก้** | **PASS** | `artifacts/stage06b/dotnet-after-fix.log` — `Passed! Failed: 0, Passed: 21` |
| เทสต์เดิมทั้งหมดผ่าน | **PASS** | `artifacts/stage06b/dotnet-full-after-fix.log` — dotnet **504/504**; `-Suite Fast` รอบปิด Stage `artifacts/stage06b/fast-final.log` — **PASS 10 \| FAIL 0 \| NOT RUN 3 \| WARN 0** (178.5 s) รวม `unity: EditMode` **155/155** (รอบทำงาน `fast.log` + `fast-summary.md` 153.1 s, ผลเดียวกัน) |
| เทสต์ A อยู่ใน suite Fast | **PASS** | 7 เคสใหม่ใน `lib/Tests~/Core.Tests/Run/ShopStockTests.cs` รันในขั้น `dotnet test (xUnit)` ของ Fast (504 passed ในไฟล์ `fast.log` และ `fast-final.log`) |
| เงื่อนไข stock ศาลาที่ 2 ในลูป PlayMode ของ Stage 10 | **PASS** | ขยาย `ResultScenePlayModeTests.A_run_can_be_won_floor_by_floor_…`: เช่า shelf 0 + ล็อก shelf 1 ในศาลาที่ 1, พอถึงศาลาที่ 2 assert ว่า **เต็มทุกช่อง, ไม่มีช่องติด `IsPurchased`/`IsLocked`, `RefreshCount = 0`** — `artifacts/stage06b/playmode-resultscene-after-fix.json` **3/3 passed** (10.0 s) |
| เงื่อนไขนั้น**ไม่ใช่เทสต์หลอก** | **PASS** | red-check: ย้อน fix ชั่วคราวแล้วรันคลาสเดิม → `sala 2: only 3 of 4 shelves are rentable / Expected: 4 / But was: 3` — `artifacts/stage06b/playmode-resultscene-red-check.json`; แล้ว restore fix + `recompile` + รันซ้ำได้ **3/3** อีกครั้งบนไฟล์เวอร์ชันสุดท้าย |
| `-Suite Full` รอบปิด Stage (Editor เปิด) | **FAIL 1 (PlayMode bulk, flake เดิม)** | `artifacts/stage06b/full-editor-open.log` — PASS 10 / FAIL 1 / NOT RUN 2; ขั้น `unity: PlayMode` รายงาน **22/47** โดย **25 ที่ไม่ผ่านคือ ambient log assert เดิมของ Input System/R3** (12 `IndexOutOfRangeException` + 8 `Cached unprocessed value` + cascade) ตาม `docs/testing/test-suites.md` §7 — เคสที่ Stage นี้แก้ (`A_run_can_be_won_floor_by_floor_…`) **Passed** อยู่ใน payload เดียวกัน (`artifacts/dotnet/unity-playmode-status.json`) และ per-class คือ **3/3** ด้านบน |
| WebGL build / Playwright | **NOT RUN** | Stage 06b ไม่ใช่ gate (10/12/14/15) และไม่มีการเปลี่ยน Presentation/Web; Full รอบ Editor เปิดรายงาน NOT RUN ตามที่ควร (Editor ถือ project lock) |
| determinism ไม่ขยับ | **PASS** | รัน Sim ตรง ๆ seed 20261013 ทั้งก่อนและหลัง fix ได้ `determinismDigest=0x841b4e46` เท่ากัน (`artifacts/stage06b/sim-before-fix.txt`, `sim-fix.txt`) — วัดจริง ไม่ใช่คาดเดา |

## 3. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **NOT RUN:** WebGL build และ Playwright (ตามกติกา: ไม่ build Web ระหว่างทำงาน). ความเสี่ยงต่ำ —
  การแก้อยู่ที่ Domain, ไม่แตะสัญญา Web bridge.
- **PlayMode bulk ไม่ผ่านเป็นชุด** ด้วย flake ที่บันทึกไว้แล้ว (ไม่ใช่ assertion ของเรา): Full รอบปิด Stage
  รายงาน 22/47 โดย 25 เคสมี ambient log assert; per-class ของคลาสที่แก้คือ 3/3. สองรอบแรก
  หลัง red-check ถูก **teardown ของ test framework ตัดกลางด้วย** `InvalidOperationException: This
  cannot be used during play mode` (SaveModifiedSceneTask) → `Playmode tests were aborted because the
  player was stopped`; แก้ด้วย `save_all` แล้วรันซ้ำได้ 3/3. เป็นความเปราะของ pipeline ไม่ใช่ของเทสต์.
- เทสต์ A ใน dotnet ครอบ **Domain/Application** เท่านั้น; การยืนยันว่าศาลาที่ 2 บนจอเต็มจริงเป็นงานของ
  PlayMode (ด้านบน) ซึ่งเช็คที่ model ไม่ได้อ่านตัวอักษรบนชั้นวาง — ข้อความของผู้เล่นมาจาก localization
  และไม่ได้เปลี่ยนใน Stage นี้.

## 4. คำสั่งที่รันจริง

```bash
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~ShopStockTests"   # ก่อนแก้: 6 failed
dotnet test "lib/Tests~/ProjectSurvive.Core.sln" --filter "FullyQualifiedName~ShopStockTests"   # หลังแก้: 21 passed
dotnet test "lib/Tests~/ProjectSurvive.Core.sln"                                                # 504 passed
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast                  # PASS 10 | FAIL 0 | NOT RUN 3 (รันสองครั้ง: fast.log, fast-final.log)
unity command run_tests --mode playmode --async_tests --filter ResultScenePlayModeTests         # 3/3 (หลัง fix, และ 2/3 ตอน red-check)
dotnet exec lib/Tests~/Sim/bin/Debug/net9.0/ProjectSurvive.Sim.dll --seed 20261013 --quiet       # determinismDigest ก่อน/หลัง fix เท่ากัน
```

## 5. ไฟล์ที่แก้

- `lib/Runtime/Domain/Shop/ShopSlot.cs` — `ResetForCamp()` ใหม่; `Clear()` และ `MarkPurchased()` เท่าเดิม
- `lib/Runtime/Domain/Shop/ShopStock.cs` — `BeginCamp()` เรียก `ResetForCamp()` แทน `Clear()`+`SetLocked(false)`
- `lib/Tests~/Core.Tests/Run/ShopStockTests.cs` — เทสต์ A (4 Theory + 3 Fact) + helper 2 ตัว
- `UnityProject/Assets/Main/Tests/PlayMode/ResultScenePlayModeTests.cs` — เงื่อนไขศาลาที่ 2 ในลูป victory
