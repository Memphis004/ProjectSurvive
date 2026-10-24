# Stage 08e — Card Forest Arena (โครงฉาก การ์ด พื้น โซน)

Presentation เท่านั้น: ไม่มีการแก้ `lib/`, `data/` หรือกติกาเกมใน Stage นี้
(ยืนยันด้วย `git status --porcelain lib data` = ว่าง)

## 1. การสำรวจก่อนแก้ (วัดจริง ไม่ได้เดา)

### 1.1 กล้อง Battle — อ่านจาก rig + วัดจากกล้องที่รันอยู่

| ค่า | ค่าจริง | ที่มา |
|---|---|---|
| Projection | **Orthographic** | `BattleViewSettings.Projection`, ADR 0018 |
| Pitch | **50°** | `BattleCameraRig.PitchDegrees`, `Euler(50,0,0)` |
| Orthographic size | **14** | `BattleCameraRig.OrthographicSize` |
| Yaw | **0** (ไม่หมุนแกน Y) | `Quaternion.Euler(PitchDegrees, 0, 0)` |
| Billboard pitch ของสไปรต์ | **50°** | `BattleCameraRig.BillboardPitch` → `BattleRenderBridge.BillboardPitch` |
| near / far | 0.3 / 120 | `SetProjection(Orthographic)` |
| ระยะกล้องจากเป้า | 60 (orthographic standoff) | `OrthographicDistance` |

วัดจากกล้องที่กำลังเล่นจริง (Play Mode, `Camera.main`):

```
projection=orthographic   orthographicSize=14
euler=50,0,0              position=0,45.963,-38.567  (target 0,0)
near=0.3 far=120          billboardPitch=50
```

ADR 0018 ยังมีผลครบทุกตัว จึง **ไม่แตะ projection / pitch / ortho size**

### 1.2 ขนาดสนาม: Simulation เป็นเจ้าของ, Presentation สะท้อน

| ที่มา | ค่า | หมายเหตุ |
|---|---|---|
| `BattleArena.Radius` (Simulation) | **40** | เจ้าของตัวเลข, enemy spawn ที่ `0.95 × Radius` |
| `BattleViewCaps.ArenaRadius` (Presentation) | **40** | ค่าสะท้อนที่มีอยู่ + EditMode test คู่กัน |
| `ArenaTheme.ArenaRadius` (asset) | **40** | ผูกกับ `BattleViewCaps.ArenaRadius` ด้วย test |

ฉากใช้ค่าสะท้อนเดิม ไม่ได้กำหนดขอบเขตเอง — placement ทุกชิ้นอยู่ภายใน (`max radius 39.1 ≤ 40`)

### 1.3 ระยะที่กล้องมองเห็นรอบผู้เล่น

วัดด้วยการยิง ray จากมุม viewport ทั้งสี่ลงระนาบพื้น (world Y = 0):

| Resolution | aspect | X ที่มองเห็น | simY ที่มองเห็น | ความลึก (Z) | px/unit X | px/unit Z |
|---|---|---|---|---|---|---|
| 1920×1080 | 1.778 | −24.889 … +24.889 | −18.276 … +18.276 | 36.551 | 38.571 | 29.547 |
| 1280×720 | 1.778 | −24.889 … +24.889 | −18.276 … +18.276 | 36.551 | 25.714 | 19.698 |

Orthographic + 16:9 ทั้งคู่ → **พื้นที่โลกที่มองเห็นเท่ากัน** ต่างกันแค่ pikselต่อหน่วย
ผีเกิดที่ `0.95 × 40 = 38` หน่วย ซึ่งไกลกว่าขอบจอ (18.28) → ศัตรูเดินเข้ามาจากนอกภาพ
ดังนั้น "ความหนาแน่นที่มองเห็น" จึงคิดจากระยะรอบผู้เล่น ไม่ใช่จากขอบสนาม

### 1.4 ภาพ "ก่อนแก้"

`artifacts/ui/battle-env/art001-before-1920x1080.png`, `art001-before-1280x720.png`
(ก่อนเปลี่ยนโซนความหนาแน่น: Border มีต้นไม้ 35 ต้นรอบเส้นรอบวง 251 หน่วย ≈ 1 ต้นต่อ 7 หน่วย
จึงยังไม่เป็นกำแพงป่า)

### 1.5 สภาพฉากก่อนแก้ (Play Mode)

```
cards=102   Border=35  Center=15  Ring=24  Decal=28
distinct eulerX = 50 (cards), 90 (decals flat)
max |yaw| = 177.953        max radius = 39.147
sortingOrder -587..566     mismatches vs BattleWorldMap.PropSortingOrder = 0
player eulerX=50   ghost eulerX=50   rigPitch=50
```

## 2. กล้องและการเรียงลำดับ (ข้อ 2)

- ไม่มีการเปลี่ยน projection / pitch / ortho size; `BattleCameraRig` เป็นแหล่งเดียวของมุม
- การ์ดเอียงด้วย `ArenaCard.FacingCamera()` ซึ่งเรียก `BattleCameraRig.PitchDegrees` ตรง ๆ
  (ไม่มีค่ามุมชุดที่สอง) และสไปรต์ผีได้มุมเดียวกันผ่าน `BillboardPitch` — วัดในซีนจริง 50° ทั้งคู่
- ลำดับการบังมาจาก `BattleWorldMap.PropSortingOrder(z)` ที่ builder เขียนลง
  `MeshRenderer.sortingOrder` เท่านั้น; shader อยู่คิว Transparent, `ZWrite Off`, `ZTest Always`
  → ไม่พึ่ง depth ของกล้อง; ตรวจในซีนจริง mismatch = 0
- test คุมไว้: `CardsLeanAtTheSamePitchAsTheActorSprites`,
  `CardsAreOrderedByTheWorldMapNotByDepth`, `TheCardShaderDoesNotWriteDepth`

## 3. สิ่งที่ทำ (ข้อ 3–6)

### 3.1 ArenaCard / GroundDecal (ข้อ 3)

- `ArenaCard.prefab`: quad pivot ที่โคน (`ArenaGeometry.CreateBasePivotedQuad`), mesh + material
  เดียวต่อ style, `BatchingStatic`, ไม่มี collider (`CardsAndDecalsCarryNoCollider`)
- `ArenaCard.shader`: `clip(colour.a - _Cutoff)` (alpha clip), sway เฉพาะส่วนบน
  (`upper = uv.y`, `sway ∝ upper²`), ปิดได้ด้วย global `_ArenaSway` ผ่าน
  `ArenaEnvironmentView.SetLeafSway` (test `LeafSway_IsASwitch`)
- เอียงหันกล้องครั้งเดียวตอนวาง (builder) ไม่มีงานต่อเฟรม
- `GroundDecal.prefab`: quad แบนที่ `y = 0.02`, `Offset -1,-1`, `ZWrite Off` → ไม่ z-fight กับพื้น
  และไม่มี collider
- แก้คอมเมนต์ใน shader ที่ขัดกับโค้ด (เดิมเขียนว่าบังด้วย depth buffer ทั้งที่ `ZWrite Off`)

### 3.2 ArenaTheme + ArenaBuilder + โซนความหนาแน่น (ข้อ 4)

- menu `ProjectSurvive/Stage 08e/1. Build arena (theme + seed)` และ
  `-executeMethod ProjectSurvive.Game.Editor.ArenaBuilder.BuildAndExit` (seed จาก
  `PROJECTSURVIVE_ARENA_SEED`) — มีอยู่แล้วและใช้จริงในรอบนี้
- โซน (คำนวณจากข้อ 1.3): Center 0–12 (หญ้าเตี้ย ≤ 0.75 หน่วย + decal), Ring 12–26 (พุ่มระยะห่างขั้นต่ำ
  3.2), Border 26–40 (กำแพงต้นไม้ 3 ชั้น), Decal (ปื้นดิน/ดอกไม้/ใบไม้)
- **ใหม่รอบนี้:** กำแพงป่าคำนวณ slot จาก *ความกว้างของการ์ด* ไม่ใช่จากตัวเลข spacing —
  `pitch = AverageWidth(border styles) × theme.BorderSlotPitchFactor` (0.85) แล้ว
  `slots = 2πr / pitch` ต่อชั้น ทำให้การ์ดข้างเคียงคาบเกี่ยวกันเป็นกำแพง
- **ใหม่รอบนี้:** การ์ดต้นไม้ใหญ่ 3 แบบ (`tree-big-a/b/c`, 9.0–10.4 หน่วยกว้าง, 13.5–15.5 สูง,
  scale 0.90–1.30) ใช้ texture ชุดเดียวกับ tree-a/b/c — placeholder สีเดิม ไม่มี asset ใหม่
- จำนวนต่อโซนยังจำกัดใน theme: Center 12–18, Ring 16–24, **Border 72–96**, decals 5/12/10
- ผลการสร้างจริง: **141 ชิ้น (Center 15, Ring 24, Border 74, Decal 28), signature `c1ffaa84`**
  (seed 20261024) — สร้างซ้ำได้ค่าเดียวกัน
- ตรวจเพิ่มใน `ArenaLayout.Validate` + test ใหม่: การ์ดยืน (Ring/Border) ห้ามอยู่ในรัศมีกลาง 12
- test ใหม่: `TheOpenCentreHoldsNoStandingCard`, `TheBorderWallIsDenseAndMadeOfBigTrees`

### 3.3 พื้น (ข้อ 5)

- พื้น = mesh แบน opaque 1 ชิ้น สีพื้นเรียบ + macro noise จาง (`Ground()`: ±0.022 / ±0.008)
  ไม่มีลายถี่
- ปื้นดินใหญ่: decal 7×5 คูณ 0.6–1.1 วาง 6 จุด (รวมจุดเกิด) — **ปรับให้อ่อนลงรอบนี้**:
  texture ellipse alpha 0.42→0.18, central 0.20→0.09, material tint `0.88,0.84,0.76 / 0.55`
  เดิมเข้มจนตอนกลางคืนอ่านเป็นควันดำ ไม่ใช่พื้นดิน
- ดอกไม้/ใบไม้โปรยบาง ๆ 12 + 10 ชิ้น รัศมี ≤ 26

### 3.4 ไม่มี Instantiate/Destroy ระหว่างเล่น (ข้อ 6)

- การ์ด/decal ถูกสร้างโดย builder ตอน build ฉาก ไม่มีโค้ด runtime สร้างหรือลบ (grep แล้ว:
  ไม่มี `Instantiate` ใน `Environment/`)
- วัดในเกมจริง: `viewsGrownDuringPlay = 0` (จาก `EntityViewPool.CreatedAfterWarmup`)

## 4. หลักฐาน

ภาพจากเกมจริง (Editor Play Mode, 300 ผี, กลางคืน, `ArenaEvidenceCapture`):

| ไฟล์ | สถานะ |
|---|---|
| `artifacts/ui/battle-env/stage08e-center-1920x1080.png` | ผู้เล่นกลางสนาม 1920×1080 |
| `artifacts/ui/battle-env/stage08e-center-1280x720.png` | ผู้เล่นกลางสนาม 1280×720 |
| `artifacts/ui/battle-env/stage08e-edge-1920x1080.png` | ผู้เล่นที่ขอบ (0, 34.18) 1920×1080 |
| `artifacts/ui/battle-env/stage08e-edge-1280x720.png` | ผู้เล่นที่ขอบ (0, 34.18) 1280×720 |
| `artifacts/ui/battle-env/art001-before-*.png` | ภาพก่อนแก้ (เทียบ) |
| `artifacts/ui/battle-env/stage08e-web-battle-1280x720.png` | **เกมจริงในเบราว์เซอร์** (WebGL Release build ผ่าน serve-web) |
| `artifacts/ci/web-test-results/battle-*/test-finished-1.png` | ภาพจาก Playwright (battle spec) |

ตัวเลขจากรอบเดียวกัน (`artifacts/ui/battle-env/metrics.txt`), เครื่อง i5-14400F / RTX 5060 Ti /
32 GB, Unity 6000.3.9f1 Editor Play Mode:

| สถานะ | resolution | batches | draw | setPass | ms/frame (median 30) | entities |
|---|---|---|---|---|---|---|
| center | 1920×1080 | 133 | 133 | 28 | 2.40 | 314 |
| center | 1280×720 | 148 | 148 | 29 | 2.48 | 314 |
| edge | 1920×1080 | 167 | 167 | 16 | 2.60 | 318 |
| edge | 1280×720 | 172 | 172 | 16 | 2.59 | 317 |

`viewsGrownDuringPlay = 0`

## 5. Acceptance

| ข้อ | ผล | หลักฐาน |
|---|---|---|
| seed เดิมได้ฉากเดิม | **PASS** | signature `c1ffaa84` ทั้งสอง build (`artifacts/arena/arena-report.json`) + `SameThemeAndSeed_ProduceTheSameArena` |
| โซนกลางไม่มีของสูงเกินค่า theme | **PASS** | `TheCentreHasNothingTallerThanItsCeiling` (ceiling 0.75) |
| ไม่มีการ์ดในรัศมีกลางที่กำหนด | **PASS** | `TheOpenCentreHoldsNoStandingCard` (CenterRadius 12, ทุก seed) |
| เทสต์ผ่าน | **PASS** | EditMode 222/222 และ `tools/test.ps1 -Suite Fast` = 13 PASS / 0 FAIL |
| ภาพ Editor 1920×1080 และ 1280×720 (กลาง + ใกล้ขอบ) | **PASS** | 4 ไฟล์ใน §4 |
| batches ≤ 200 | **PASS** | 133–172 (แย่สุด 172 ที่ edge 1280×720, 300 ผี) |
| ไม่แก้ Core/data | **PASS** | `git status --porcelain lib data` ว่าง |
| ไม่มี UiLayoutCheck ใหม่ที่ล้ม | **PASS** | WebGL Release build ผ่าน + Playwright **19 passed** (Chrome, พอร์ต 8080, รันกับ build สด) |

## 6. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **ผู้เล่นหายใต้ต้นไม้ที่ขอบ:** การ์ดต้นไม้อยู่ในวง 26–40 ซึ่งเป็นพื้นที่ที่ผู้เล่นเดินได้
  (clamp ที่ 40) จึงมีจังหวะที่ต้นไม้อยู่ระหว่างกล้องกับผู้เล่น — **แก้แล้วใน §8** (dither ตามการ์ดที่บัง
  ผู้เล่น); ยังไม่ได้ทดสอบบน WebGL build จริงของรอบติดตามนี้
- ปื้นดินยังเป็น ellipse โปร่งแสงแบบ placeholder ไม่ใช่ texture ดินจริง — Stage 08c/13 ค่อยเปลี่ยน
- ยังไม่ได้วัดบน browser จริง (WebGL) ในรอบนี้; ตัวเลข frameMs มาจาก Editor Play Mode
- **พบปัญหาที่เครื่องมือ build ไม่ใช่ที่ฉาก:** `tools/build-web.ps1` เขียน `Web.data`/`Web.wasm` ใหม่
  แต่ทิ้งไฟล์ `.br`/`.gz` ของ build เก่าไว้ และ `tools/serve-web.mjs` เลือก `.br` ก่อน → รอบแรก
  Playwright ทั้ง 19 ตัวรันกับ **build เก่า** (ภาพจึงยังเป็นฉากพื้นตาราง ไม่มีป่า) ทั้งที่ขึ้น PASS
  ลบ sibling ที่ค้างแล้วรันใหม่ ได้ภาพป่าในเบราว์เซอร์และ 19 passed อีกครั้ง ควรแก้ build/serve
  ให้ลบหรือสร้าง compressed sibling ใหม่ทุกครั้ง (ยังไม่ได้แก้ใน Stage นี้)

## 7. คำสั่งที่รันจริง

```
tools/test.ps1 -Suite Fast                     → PASS 13 | FAIL 0 | NOT RUN 3
Unity EditMode (filter ArenaEnvironmentTests)  → 15/15 passed
ProjectSurvive/Stage 08e/1. Build arena (theme + seed)  → 141 pieces, signature c1ffaa84
AgentScripts/ArenaEvidenceCapture.Run          → 4 ภาพ + metrics.txt (viewsGrownDuringPlay=0)
tools/test.ps1 -Suite Full                     → PASS 23 | FAIL 0 | NOT RUN 3
                                                 (WebGL/Playwright NOT RUN: Editor ถือ project lock)
tools/ci.ps1 -Job Build,Web                    → PASS 2 | FAIL 0 (WebGL 465s, Playwright 19 passed
                                                 — แต่เสิร์ฟ .br ของ build เก่า, ดู §6)
tools/ci.ps1 -Job Web                          → PASS 1 | FAIL 0 (Playwright 19 passed กับ build สด)
```

## 8. รอบติดตาม — dither ต้นไม้ที่บังผู้เล่น (ไม่ใช่การเปลี่ยน Stage)

**ปัญหา:** การ์ดต้นไม้วง 26–40 คือพื้นที่ที่ผู้เล่นเดินได้ (clamp 40) การ์ดที่อยู่ *ใกล้กล้องกว่า*
ผู้เล่นถูกวาดทับด้วย `PropSortingOrder` และ shader เขียน depth ไม่ได้ (`ZWrite Off`, `ZTest Always`)
จึงไม่มีกลไก depth ใดช่วยได้ — ผู้เล่นหายใต้พุ่มทั้งต้น

**วิธีแก้:** `ArenaCardFade` (component เดียวบน root `Arena`) + `_Fade` ใน `ArenaCard.shader`

- `Covers(cardX, cardZ, w, h, playerX, playerZ, margin)` เป็นฟังก์ชันบริสุทธิ์: การ์ดบังได้เฉพาะเมื่อ
  `cardZ ≤ playerZ` และผู้เล่นอยู่ในแถบ `cardZ … cardZ + h / tan(50°)` (≈ `0.84h`, ค่าคงที่
  `ReachPerHeight` — ไม่มี `tan` ในลูปต่อเฟรม) และอยู่ในความกว้าง ±`w/2 + margin` (0.6)
- การ์ดที่บังถูก dither ด้วย 4×4 Bayer discard ใน shader เขียนผ่าน `MaterialPropertyBlock` ต่อ renderer
  (`_Fade` อยู่ใน `UnityPerMaterial` CBUFFER) — ไล่จาก 1 → **0.22** ใน 0.16 s ไม่ลงถึง 0
  เพราะต้นไม้ที่หายไปทั้งต้นกลายเป็นรูในป่าที่ต้นอื่นไม่ขยับ; 0.22 พอให้เห็นผู้เล่นทะลุและยังเห็นเงาต้น
- **ถอด `BatchingStatic` ออกจาก prefab การ์ดและทุก placement** (`OccludeeStatic` ยังอยู่):
  static batching วาดผ่าน mesh รวมและทิ้ง property block → block อ่านกลับได้แต่ต้นไม้ยังทึบ
  ไม่เสียอะไรเพราะทุกการ์ดมี `sortingOrder` ของตัวเองอยู่แล้ว; decal ยัง batching ได้ (ไม่เคยบังผู้เล่น)
- สวิตช์: `ArenaEnvironmentView.SetOcclusionFade(bool)`; เป้าหมายคือ transform ของตะเกียงที่ตามผู้เล่นอยู่แล้ว
- ลูปต่อเฟรม: 141 การ์ด, ไม่มี allocation/LINQ/closure, เขียน renderer เฉพาะเฟรมที่ค่า fade เปลี่ยนจริง

**เทสต์ใหม่ (EditMode, `ArenaEnvironmentTests`)**

| test | ครอบอะไร |
|---|---|
| `TheOcclusionDitherOnlyCoversWhatStandsInFrontOfThePlayer` | การ์ดหลังผู้เล่น/สูงเกินแถบที่บังได้/อยู่ข้างไกลไม่จาง และ margin |
| `AnOccludingCardThinsOutButDoesNotBecomeAHole` | `MinFade` อยู่ใน (0, 0.6) — ต้นไม้ไม่หายทั้งต้น |
| `TheCardShaderHasAFadeToDitherWith` | วัสดุการ์ดมีการ์ด `_Fade` จริง (shader ฝั่งที่ต้องอ่านค่า) |

ผลรวม suite นี้ **18/18 passed** (เดิม 15 + 3 ใหม่)

**หลักฐาน** (`artifacts/ui/battle-env/`)

| ไฟล์ | คืออะไร |
|---|---|
| `stage08e-dither-compare.png` | ซ้าย fade ON / ขวา fade OFF — ครอป 2× ของเฟรมเดียวกัน (timeScale = 0) |
| `stage08e-fade-on.png`, `stage08e-fade-off.png` | เฟรมเต็ม 1920×1080 คู่เดียวกัน |
| `crop-stage08e-fade-on.png`, `crop-stage08e-fade-off.png` | ครอป 1× ของคู่เดียวกัน |

วัดจากการเล่นจริง (Editor Play Mode, 300 ผี, ผู้เล่นที่ `(0, 0, 34.29)`):

```
fade component=found enabled=True
card card-076-tree-big-b zone=Border pos=3.04,0.00,33.55 size=12.07,18.00
     covers=True blockFade=0.22 materialFade=1.00 order=-538
cardsTotal=141 matched=1  fadingCount=1
```

- `blockFade = 0.22` และ `materialFade = 1.00` → ค่าถูกเขียนที่ property block ต่อ renderer จริง
  ไม่ใช่การแก้ shared material (ถ้า static batching ยังเปิด ค่านี้จะอ่านได้แต่ภาพยังทึบ)
- เทียบสองเฟรมแบบหยุดเวลา: พิกเซลต่างกัน **47,053 px** จำกัดในกรอบ `x 930–1241, y 0–562`
  ซึ่งคือการ์ดต้นไม้ต้นนั้นต้นเดียว — ไม่มีพิกเซลอื่นในจอเปลี่ยน (HUD/ต้นอื่น/พื้นนิ่งสนิท)
- `fadingCards=1` ที่ขอบยืนยันว่าไม่มีต้นไม้ต้นอื่นถูกจางพร้อมกัน; ตัวเลข batches ที่ขอบยังเป็น
  166/171 (1920×1080 / 1280×720) เท่ารอบก่อนที่มี `fadingCards=1` — ไม่มี regression จาก MPB

**ยังไม่ได้ทดสอบ:** WebGL build + Playwright ของรอบติดตามนี้ (ไม่ได้ build ใหม่ในรอบนี้) และ
ภาพต้องให้ผู้ใช้ตรวจด้วยตาก่อนปิดเป็น PASS ด้านภาพ

**คำสั่งที่รันจริง**

```
Unity EditMode (filter ArenaEnvironmentTests)  → 18/18 passed
AgentScripts/ArenaFadeProbe.Run                → blockFade 0.22, 1/141 การ์ดบัง, order -538
AgentScripts/ArenaFadeCompare.Run              → stage08e-fade-on/off.png (timeScale=0 คู่เดียว)
python (PIL) diff on/off                       → 47,053 px, bbox x 930–1241 y 0–562
tools/test.ps1 -Suite Fast                     → PASS 13 | FAIL 0 | NOT RUN 3
                                                 (unity EditMode 225/225, xUnit 612 passed;
                                                  PlayMode/WebGL/Playwright NOT RUN ตามกติกา Fast)
```
