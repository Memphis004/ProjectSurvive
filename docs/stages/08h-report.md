# Stage 08h — ผี กระสุน และ pickup ถูกพื้นตัดครึ่งล่าง (Presentation เท่านั้น)

> หมายเหตุเรื่องชื่อไฟล์: `08h-report.md` เดิมเคยเก็บ **survey ของ Stage 09o** (ศาลาพักก่อนทำลาน) เพราะ
> prompt ของ 09o ระบุชื่อไฟล์นั้นไว้ — ย้ายไป [`09o-survey.md`](09o-survey.md) แล้ว และลิงก์ใน
> [`09o-report.md`](09o-report.md) อัปเดตตาม ไฟล์นี้คือรายงานของ Stage 08h จริง

พื้นเปลี่ยนไป **จุดเดียว**: `ArenaGround.shader` จาก `ZWrite On` เป็น `ZWrite Off` (คิวคงเดิม 2000).
ไม่มีอะไรใน `lib/`, `data/` หรือกติกาเกมถูกแตะ (`git status --porcelain lib data` ว่าง)

## 1. สรุป

ผี กระสุน และ pickup ถูกตัดครึ่งล่างเพราะ **พื้นเขียน depth** ขณะที่สไปรต์เป็น billboard pivot
กลางที่วางไว้ที่ `y = 0` ฉะนั้นครึ่งล่างอยู่ใต้ระนาบพื้นและตก `ZTest LEqual` ของ material สไปรต์ —
เป็นกติกาการบังที่สองซึ่งขัดกับ ADR 0018 (บังกันด้วย `BattleWorldMap.SortingOrder`). วัดได้จริง:
**ครึ่งล่างของสไปรต์ = 0.000 ของแบนด์** เมื่อพื้นเปิด และ 0.17–0.59 เมื่อพื้นปิด. แก้โดยให้พื้น **วาดก่อน
แต่ไม่เขียน depth** — ทางเดียวที่ทำให้ครึ่งล่างกลับมาโดยไม่เปลี่ยนพิกัด/รูปทรงของสไปรต์เลย. หลังแก้
ค่าที่พื้นเปิดกับพื้นปิดเท่ากันทุกชนิด ทุกขนาด และทั้ง Dusk/Night ([ADR 0038](../adr/0038-the-floor-writes-no-depth.md))

## 2. การวินิจฉัย (A) — พิมพ์ค่าจริงจากฉากที่รันอยู่ ไม่ได้เดา

`AgentScripts/ArenaStage08hCapture.Run` (Editor Play Mode, Unity 6000.3.9f1) →
`artifacts/stage08h/stage08h-materials-before.txt`

| ของ | ค่าที่อ่านได้ |
| --- | --- |
| พื้น | `arena-floor` (MeshRenderer), material **`ground`**, shader **`ProjectSurvive/ArenaGround`**, `renderQueue` **2000**, `sortingOrder 0`, pos (0,0,0) euler (90,0,0) scale (80,80,1) |
| pass ของพื้น (อ่านจาก shader asset) | `"Queue" = "Geometry"`, **`ZWrite On`**, **`ZTest LEqual`** |
| สไปรต์ (pooled `view-ghost/-bolt/-drop/-player`) | material **`Sprite-Unlit-Default`** (URP 2D), `renderQueue` **3000**, `ZWrite [_ZWrite]` (ปิด), **ไม่ระบุ `ZTest` → Unity default `LEqual`** |
| sprite | `ps-battle-ghost` **pivot (0.5, 0.5)**, ppu 64 (1×1 unit), `ps-battle-bolt`/`ps-battle-drop` 24 px (0.375 unit) |
| การวางบิลบอร์ด | `pos = BattleWorldMap.ToWorld(x, y) = (x, 0, y)`, `rotation = Euler(50, 0, 0)` (`BattleCameraRig.BillboardPitch`) |
| ขอบล่างของสไปรต์ 1 unit | **y = −0.3214** (0.5 × cos 50°) — ใต้ระนาบพื้น |

ยืนยันด้วยการจับภาพเปิด/ปิดพื้นบนฉากจริง (60 ผี, หยุดเวลา, probe วางที่พิกัดกำหนด):
`stage08h-night-1920x1080-floor-on-before.png` กับ `-floor-off-before.png` และคู่ 1280×720 / dusk.

```
ก่อนแก้  night-1920x1080         พื้นเปิด            พื้นปิด
  ghost   lowerHalf/lowerQuarter  0.0198 / 0.0000    0.3840 / 0.1694
  elite                           0.0146 / 0.0000    0.3975 / 0.1776
  boss                            0.0101 / 0.0000    0.3871 / 0.1631
  bolt                            0.0729 / 0.0000    0.7222 / 0.6131
  drop                            0.0729 / 0.0000    0.4896 / 0.2857
```
(`artifacts/stage08h/stage08h-coverage-before.txt`; 1280×720 และ dusk ให้ผลแบบเดียวกัน)

## 3. สาเหตุและการแก้ (B) — จุดเดียว

**สาเหตุ:** พื้นเป็น material เดียวในฉากที่เขียน depth. สไปรต์มี pivot กลางและถูกวางที่ `y = 0`
(เพราะ simulation เป็น 2D และ `ToWorld` คือ `(x, 0, y)`) ครึ่งล่างจึงอยู่ใต้ระนาบและถูก depth ของพื้น
ตัด — ไม่เกี่ยวกับ sorting order เลย

**แก้:** `ArenaGround.shader` → `ZWrite Off`; คิวคงที่ 2000 (ก่อน decal 2950, shadow การ์ด 2960,
glow 2970, blob shadow 2990 และสไปรต์ 3000 ทั้งหมด). เพิ่ม `ArenaArtBuilder.GroundQueue = 2000`
ให้เลขคิวมีชื่อและให้ test อ่านได้; แก้คอมเมนต์ที่เขียนผิดใน `BattleViewScene.CreateFloor` และหัว shader

**เหตุผลที่เลือกทางนี้ (ไม่เลือกอีกทาง):** ทางเลือกที่สองคือย้าย pivot/ยกสไปรต์ให้ทั้งชิ้นอยู่เหนือระนาบ
แต่ทั้งหมด 8 ชนิด (pooled views, telegraph, ตัวเลขดาเมจ, host ของ Spine, anchor ของ elite/boss,
blob shadow) อ้างพิกัด simulation เดียวกัน จึงต้องขยับครึ่งความสูงของตัวเองคนละค่า และรูปทรงที่ผู้เล่นอ่าน
จะเปลี่ยน (ผีจะเริ่มที่เท้าไม่ใช่คร่อมพื้น) ซึ่งไม่ใช่ "จุดเดียว"; รายละเอียดใน ADR 0038

**สิ่งที่ได้มาเพิ่มโดยไม่ตั้งใจ:** วงเตือน (telegraph) วางราบที่ `y = 0` เสมอกับพื้น — เดิมอาศัย
`Offset -1,-1` กัน z-fight, ตอนนี้ไม่ z-fight เลยเพราะพื้นไม่เขียน depth แล้ว

## 4. ตรวจครบทุกชนิด (C) — หลังแก้, พื้นเปิด vs พื้นปิด

`artifacts/stage08h/stage08h-coverage-after.txt` (หยุดเวลา; probe วางที่พิกัดกำหนด;
"lowerHalf / lowerQuarter" = สัดส่วนพิกเซลของแบนด์นั้นที่เป็นสีของสไปรต์เอง วัดด้วยการซ่อน/แสดง)

| ชนิด | night 1920 พื้นเปิด | night 1920 พื้นปิด | night 1280 เปิด | dusk 1920 เปิด | dusk 1280 เปิด |
| --- | --- | --- | --- | --- | --- |
| ผี (ghost) | 0.3840 / 0.1694 | 0.3840 / 0.1694 | 0.3926 / 0.1729 | 0.3840 / 0.1694 | 0.3914 / 0.1707 |
| Elite (×1.4) | 0.3951 / 0.1728 | 0.3975 / 0.1776 | 0.3926 / 0.1696 | 0.3921 / 0.1702 | 0.3926 / 0.1696 |
| Boss (×2) | 0.3273 / 0.1448 | 0.3331 / 0.1489 | 0.3340 / 0.1481 | 0.3268 / 0.1443 | 0.3849 / 0.1591 |
| กระสุน | 0.7083 / 0.5893 | 0.7222 / 0.6131 | 0.7422 / 0.5938 | 0.6736 / 0.5417 | 0.7188 / 0.5781 |
| pickup | 0.4896 / 0.2857 | 0.4896 / 0.2857 | 0.5313 / 0.2969 | 0.4861 / 0.2798 | 0.5000 / 0.2656 |
| วงเตือน (ring) | 0.6890 / 0.4693 | 0.6939 / 0.4759 | 0.6929 / 0.4854 | 0.6844 / 0.4634 | 0.6867 / 0.4767 |
| ตัวเลขดาเมจ | 0.3862 / 0.3080 | 0.3863 / 0.3082 | 0.4050 / 0.3347 | 0.3829 / 0.3042 | 0.4038 / 0.3327 |
| ตัวละครผู้เล่น (Spine) | 0.2268 / 0.1485 | 0.2272 / 0.1492 | 0.2265 / 0.1479 | 0.2272 / 0.1492 | 0.2265 / 0.1479 |

ผลต่างระหว่างพื้นเปิด/ปิด ≤ 0.03 ทุกช่อง (ส่วนใหญ่ ≤ 0.005) = พื้นไม่มีผลกับพิกเซลของสไปรต์แล้ว
ค่า boss ที่ต่ำกว่า ghost เล็กน้อยเพราะสไปรต์ทับกันมากขึ้นที่ขนาด 2 unit ไม่ใช่การถูกตัด
(เทียบพื้นเปิด/ปิดของ boss เองก็ยังเท่ากัน)

## 5. Acceptance

| ข้อ | ผล | หลักฐาน |
| --- | --- | --- |
| ภาพ Editor 1920×1080 และ 1280×720 มีผีหลายสิบตัว+กระสุน เห็นครบตัว | **PASS** (รอผู้ใช้ตรวจภาพ) | `artifacts/stage08h/stage08h-{night,dusk}-{1920x1080,1280x720}-floor-on-after.png` (60 ผี, ~33 ตัวในเฟรม, HUD ครบ) |
| เทสต์ฉากจริงจับภาพ ตรวจครึ่งล่าง ≥ เกณฑ์ — **ล้มก่อนแก้** | **PASS** | `artifacts/stage08h/playmode-before-final.json`: `probe-ghost's lower quarter is missing at 1920x1080 (Night): coverage 0.000` (0/1 passed) |
| **ผ่านหลังแก้** | **PASS** | `artifacts/stage08h/playmode-after-final.json`: 1/1 passed; ตัวเลขทุกช่องใน `playmode-after-numbers.txt` |
| เทสต์ยืนยันว่าพื้นไม่เขียน depth (วิธีแก้ที่ material) | **PASS** | `ArenaEnvironmentTests.TheFloorWritesNoDepthAndIsDrawnBeforeEverythingOnIt` |
| ตรวจครบทุกชนิด ทั้ง Dusk/Night | **PASS** | §4 (8 ชนิด × 2 ขนาด × 2 profile) |
| เทสต์ผ่าน | **PASS** | EditMode **310/310** (`artifacts/stage08h/editmode-final.json`), PlayMode fixture 1/1, ArenaEnvironmentTests 30/30 |
| ไม่แตะ Core/data | **PASS** | `git status --porcelain lib data` ว่าง |

## 6. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **`tools/test.ps1 -Suite Fast` ยังล้มที่ขั้น Unity เหมือน Stage 08g §7.3** — สคริปต์เรียก
  `unity command run_tests` ซึ่งรุ่น pipeline ที่ติดตั้งเปลี่ยนชื่อเป็น `test run` แล้ว: ผลบนต้นไม้
  สุดท้ายคือ **PASS 12 | FAIL 1 | NOT RUN 3** (270.5 s) โดย FAIL เดียวคือ `unity: EditMode`
  และเป็นความล้มเหลวเดิม (ก่อน Stage นี้ก็ FAIL ข้อเดียวกัน) ผมจึงรัน EditMode ตรงผ่าน CLI
  (**310/310**) และ PlayMode fixture เดี่ยวแทน. ไม่ได้แก้ `tools/test.ps1` (เครื่องมือกลาง ไม่ใช่
  ขอบเขต Stage นี้). `artifacts/test-summary.md` คือผลรอบนั้น
- **ไม่ได้รัน WebGL build หรือ Playwright** — Stage นี้ไม่ใช่ gate (gate คือ 10/12/14/15) และ
  `-Suite Full` ต้องปิด Editor. shader change ไม่ได้แตะโครงข้อมูลเกม จึงไม่คาดว่ามีผลกับ payload
- **ภาพต้องให้ผู้ใช้ตรวจด้วยตา** ก่อนปิดเป็น PASS ด้านภาพ (AGENTS: ห้ามประกาศ PASS ด้านภาพจากเทสต์เดียว)
- **ความเสี่ยงที่เหลือ:** พื้นไม่เขียน depth แล้วจึงบังอะไรที่อยู่ใต้ระนาบไม่ได้อีก — วันนี้ไม่มีของใต้ระนาบ
  นอกจากครึ่งล่างของ billboard (ซึ่งต้องเห็น) แต่ถ้าอนาคตมีหลุม/ทางลาดจริง จะต้องเปิด `ZWrite` เฉพาะ
  วัตถุนั้น (ADR 0038 §Revisit)
- ตรวจเฉพาะ Battle scene; `Camp` ใช้ `Sprites/Default` เป็นพื้น (ไม่เขียน depth อยู่แล้ว) จึงไม่กระทบ

## 7. คำสั่งที่รันจริง

```text
unity command --project-path UnityProject scene open --path Assets/Main/Scenes/Battle.unity
unity command --project-path UnityProject editor.playmode start
unity command --project-path UnityProject script run --file AgentScripts/ArenaStage08hCapture.cs \
       --entry AgentScripts.ArenaStage08hCapture.Run          (tag = before, then after)
unity command --project-path UnityProject test run --mode playmode \
       --filter ArenaSpriteFloorDepth --filter_type testName --async_tests   → 0/1 FAIL (ZWrite On)
                                                                             → 1/1 PASS (ZWrite Off)
unity command --project-path UnityProject test run --mode editor --async_tests        → 310/310
unity command --project-path UnityProject test run --mode editor \
       --filter ArenaEnvironmentTests --filter_type testName --async_tests            → 30/30
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Fast        → ดู §6
```
