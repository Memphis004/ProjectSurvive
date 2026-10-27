# Stage 08g — Look-dev ฉาก Battle (Presentation เท่านั้น)

Presentation และเครื่องมือ Editor เท่านั้น: ไม่มีการแก้ `lib/`, `data/` หรือกติกาเกม
(`git status --porcelain lib data` ว่าง) ทุกการตรวจทำใน Editor ที่ 1920×1080 — ไม่มี WebGL build ใน Stage นี้

> **สถานะสั้น: ทำได้ 5 จาก 7 ข้อ — ข้อ 1 (สำรวจ), 2 (ความคม), 3 (Night profile), 4 (เงา alpha clip
> จริง), 5 (การจัดวาง + กล้อง + ลำต้น foreground) ปิดด้วยตัวเลขจริง. ข้อ 6 (HUD ≤ 15 %) **ยังไม่ได้ทำ**
> และข้อ 3 เทียบกับ `docs/art/target_keyart.png` **ไม่ได้เพราะไฟล์เป้าหมายไม่มีใน repo** (§6, §7).
> Acceptance ข้อ "UI ≤ 15 %" จึงเป็น **FAIL** — ปิด Stage เป็น PASS ไม่ได้**
>
> ข้อ 4 เข้ามาในรอบหลัง (2026-10-28): [ADR 0039](../adr/0039-the-arena-scenery-casts-a-shadow-map.md)
> — พื้นที่ Battle เลิกใช้ decal ต่อการ์ด แล้วให้การ์ดฉายเงาผ่าน shadow map ของ main light แทน (§5.1)
>
> **รอบที่สองของวันเดียวกัน (§5.2): เงาเดิมไม่ถึงกลางเฟรม — แก้ `shadowDistance` 50→80 และปรับมุม/ความแรงของแสง
> แล้วเงาเข้าเฟรมจริง (2.80 % → 5.56 % ของพิกเซล) แต่การตรวจภาพซูมพบว่าเงาที่ได้เป็น *สี่เหลี่ยมเต็ม quad*
> ไม่ใช่ silhouette ของใบไม้ ข้อ 4 จึง **PASS เฉพาะการต่อสาย (wiring) — รูปร่างเงายัง FAIL/OPEN****

## 1. สาเหตุที่ภาพเบลอ (วัดจริง ไม่เดา)

หลักฐาน: `artifacts/stage08g/survey.cs` (รันใน Editor), `artifacts/stage08g/quality-probe.txt`,
ภาพ `stage08g-*-before.png`

| ปัจจัย | ค่าที่วัดได้ | เป็นสาเหตุไหม |
|---|---|---|
| Render Scale | `m_RenderScale: 1` | **ไม่ใช่** — 1.0 อยู่แล้ว |
| Depth of Field | ไม่มี DoF ใน volume ที่โหลด | **ไม่ใช่** — ไม่มีอยู่แล้ว |
| MSAA / FXAA | `m_MSAA: 1` (ปิด), renderer features 0 | **ไม่ใช่ความเบลอ** — เป็นเรื่องขอบหยัก |
| Fog ยามค่ำ | `FogDensity 0.015` → `1−exp(−(0.015×60)²) = 55.5 %` ของทุกพิกเซลเป็นสีหมอก | **ใช่ — ตัวหลัก** |
| ความละเอียดเท็กซ์เจอร์การ์ด | การ์ดที่ใหญ่สุด 10.4 × 15.5 u = 401 × 598 px บนจอ 1080p แต่ต้นทาง 256 px → ขยาย **1.57×** | **ใช่ — ตัวที่สอง** |
| Aniso / filter | `anisoLevel 1`, Bilinear | **ใช่ — ตัวเสริม** |
| Bloom | `intensity 0.55` ที่ `threshold 0.85` | **ใช่ — ตัวเสริม** |

**แก้ตัวเลขจากรายงานรอบก่อน:** ตารางเดิมเขียนความสูงการ์ดเป็น `383 px` โดยคูณ `cos 50°` — **ผิด**.
การ์ดหันเข้าหากล้องตรง ๆ (`ArenaCard.FacingCamera` หมุนตาม pitch ของกล้อง) ฉะนั้นภายใต้กล้อง orthographic
มันถูกวาดเท่าขนาดโลกทั้งสองแกน ความสูงจริงคือ `15.5 × 38.57 = 598 px` และเทสต์เดิมที่คูณ cos อยู่ก็แก้แล้ว
(`ArenaSharpnessTests.ScreenHeight`) — การ์ดจึงถูกขยายมากกว่าที่รายงานรอบก่อนบอก ไม่ใช่น้อยกว่า

## 2. asset ที่ฉากใช้จริง — อาร์ตจริง กับ placeholder

`Battle.unity` วาดเท็กซ์เจอร์ 15 ไฟล์:

- **อาร์ตจริงจาก `Incoming/`: 0 ไฟล์** — ไม่มีโฟลเดอร์ `Incoming` ใด ๆ ใน repo นี้
- **placeholder: 15/15 ไฟล์** ทั้งหมดสร้างโดย `ArenaArtBuilder` (ภาพ procedural, ไม่มีลิขสิทธิ์บุคคลที่สาม):
  `tree-a/b/c`, `bush`, `grass`, `grass-small`, `dirt`, `flower`, `leaf`, `ground-macro`,
  `light-pool`, `lantern-glow`, `blob-shadow`, `mote`, `screen-gradient`, `screen-mid-band`
- mesh/prefab ก็สร้างเอง: `ArenaCardQuad`, `ArenaShadowQuad`, `ArenaCard.prefab`, `GroundDecal.prefab`,
  `ArenaCardShadow.prefab`
- ตัวละคร/HUD ใช้ Spine แพ็กที่ซื้อ (Stage 17a/17e) — ยังเป็น BLOCKER ด้านลิขสิทธิ์ตาม `ATTRIBUTION.md`

## 3. Acceptance

| # | เกณฑ์ | ผล | หลักฐาน |
|---|---|---|---|
| 1 | ภาพไม่เบลอที่ Game view 1x และไม่มี Depth of Field | **PASS** | fog 55.5 %→12.1 %; การ์ด 256→512 px และการ์ดที่ใหญ่สุดถูกวาด 508 px ≤ 512 (เดิม 401 px จาก 256 px = ขยาย 1.57×); aniso 1→8; bloom 0.55/0.85→0.32/1.05; `ArenaSharpnessTests` 6/6 |
| 2 | โทนกลางคืนตัวเลขใกล้เป้ากว่าเดิม | **NOT RUN** | วัดก่อน/หลังได้ (§4) แต่เทียบ `target_keyart` ไม่ได้ — ไฟล์เป้าหมายไม่มีใน repo (§7) |
| 3 | กลางลานผ่านเทสต์โซน; UI ≤ 15 % ของจอ | **FAIL** | โซน **PASS** (`arena-report.json` → `problems: []`; กลาง 15 / ring 24 / foreground 8 / border 79); **UI 37.86 % → ยังไม่ได้รื้อ (ข้อ 6)** |
| 4 | รายงาน placeholder ชัดเจน | **PASS** | §2 (0 อาร์ตจริง / 15 placeholder) |

เกณฑ์ของ prompt ข้อ 4 ("การ์ดต้นไม้/พุ่ม/landmark cast shadow ผ่าน alpha clip, แสงจันทร์เอียงให้เงาทอดยาว,
ตัวละครยังใช้ blob shadow") — **PASS เฉพาะการต่อสาย, FAIL ด้าน silhouette** (§5.2): การ์ดฉายเงาผ่าน main light จริง
แสงเอียงและทอดยาวจริง ตัวละครยังใช้ blob shadow แต่เงาที่ออกมาเป็นสี่เหลี่ยมเต็ม quad ไม่ใช่รูปใบไม้
หลักฐานใน §5.1, §5.2 และ ADR 0039 — **ไม่นับว่าข้อนี้ปิด**

## 4. ตัวเลขก่อน/หลัง (วัดบน PNG ที่บันทึกจริง + ผล build)

Batches / draw / set-pass / ms ต่อเฟรม (Editor Play Mode, i5-14400F + RTX 5060 Ti):

```
                    ก่อน            หลัง
night-empty 1920   128 / 2.69 ms    62 / 2.45 ms
night-empty 1280   129 / 2.38 ms    63 / 2.24 ms
dusk-empty  1920   130 / 2.84 ms    63 / 2.27 ms
night-ghosts 1920  134 / 2.40 ms    80 / 2.39 ms
night-300   1920   134 / 2.39 ms    81 / 2.33 ms
night-300   1280   151 / 2.28 ms    81 / 2.41 ms
```

งบ ≤ 200 batches ของ Stage 08f **ผ่านทุกกรณี** และตัวเลข 261 ที่รายงานรอบก่อนเจอที่ 300 ผี
**ไม่ reproduce ในรอบนี้** (81) — รอบนั้นวัดที่กล้องเดิมและสาเหตุยังไม่ทราบ รอบนี้ยืนยันได้แค่ว่าตัวเลขปัจจุบัน

Tone (`stage08g-tone-*.txt`, L = luminance เชิงเส้นเฉลี่ย, hue = ค่าเฉลี่ยวงกลมของพิกเซล sat > 0.12):

```
night-empty 1920  whole L 0.0404 → 0.0375 | hue 105.26 → 187.50 | sat 0.313 → 0.202
                  centre L 0.0377 → 0.0356 | hue 100.06 → 158.08
night-300   1920  whole L 0.0408 → 0.0401 | hue 104.99 → 188.04
dusk-empty  1920  whole L 0.0919 → 0.0772 | hue  74.33 →  69.88
```

Readability (เกณฑ์ Stage 08f เดิม ไม่ลดเกณฑ์):

```
night-300 1920  player 9.31→10.59  ghost 1.65→8.23  pickup 11.63→11.77  telegraph 1.79→1.88  FAIL→PASS
night-300 1280  player 8.08→10.05  ghost 1.37→2.53  pickup 11.63→11.77  telegraph 1.79→2.24  FAIL→PASS
night-ghosts    ghost 1.52 = FAIL (ตัวเก็บตัวอย่างจับผี 3 ตัวที่บังเอิญกลืนกับพื้น — วัดซ้ำได้ค่าไม่คงที่)
```

โซน (`artifacts/arena/arena-report.json`, seed 20261024): `problems: []`,
counts `center 15 / ring 24 / foreground 8 / border 79 / decals 28`, signature `72304a6f`;
กำแพงสูง **4.86–7.98 u = 3.0–4.99 เท่าของผู้เล่น** (ผู้เล่นที่เกมประกาศไว้ 1.6 u)

## 5. สิ่งที่เปลี่ยนใน Stage นี้

- **ความคม (ข้อ 2):** ต้นไม้ 256→512 px, พุ่ม 128→256, หญ้า 64→128, ดิน 256→512, ดอก/ใบ 64→128;
  `anisoLevel 8`, `maxTextureSize 2048`, `CompressedHQ` (≈1 ไบต์/พิกเซล เท่าของเดิม ขนาดหน่วยความจำไม่โต),
  mipmap ยังเปิด; seed ต้นไม้ตรึงที่ 357 เพื่อให้เป็นต้นไม้ต้นเดิม
- **Night profile (ข้อ 3):** tint `(0.34,0.42,0.62)→(0.32,0.38,1.00)`, fog `0.015→0.006`, mid band `0.10→0.05`,
  exposure `−0.30→−0.05`, contrast `15→20`, sat `−8→0`, bloom `0.55/0.85→0.32/1.05`, vignette `0.40→0.44`;
  แอ่งแสง `(1.00,0.83,0.55)` (ส้มหม่น) → `(1.00,0.13,0.34)` (แดงชมพู hue ≈348°)
- **การจัดวาง/กล้อง (ข้อ 5):**
  - `BattleCameraRig` เปลี่ยนจากค่าคงที่ `OrthographicSize = 14` เป็น **camera profile**:
    `PlayerHeight 1.6 u` และ `PlayerScreenHeightFraction 0.09` → `OrthographicSize = 8.889 u`
    (1080p = 60.75 px/หน่วย แทน 38.57) เทสต์ `ThePlayerStandsEightToTenPercentOfTheScreenHeight` ตรึงค่า
  - กำแพงต้นไม้: style ชายขอบทุกตัวถูกจัดใหม่ให้อยู่ในย่าน 3–5 เท่าของผู้เล่น (เดิมสูงถึง 20 u = 12.5 เท่า)
    และ `ArenaLayout.Validate` บังคับย่านนี้จริง (ไม่ใช่แค่ค่าใน asset) — เทสต์ `EveryArenaTreeStandsThreeToFivePlayersHigh`
  - **โซน `Foreground` ใหม่:** ลำต้นผอม ๆ เขตรัศมี 13–16 u (แถวล่างของเฟรมอยู่ที่ `OrthographicSize / sin 50°`
    = 11.6 u หน้าผู้เล่น) 8 ต้นต่อ seed; การ์ดที่ยืนระหว่างกล้องกับผู้เล่นถูกวาดทับและ `ArenaCardFade`
    ไล่ dither เมื่อมันบังผู้เล่น — เทสต์ `TheForegroundBeltStandsAtTheBottomEdgeOfTheFrame`,
    `TheLayoutPlacesItsForegroundTrunksInsideTheBelt`, `AForegroundTrunkDithersWhenThePlayerWalksBehindIt`
  - แก้ `MaxScale` ของดิน 1.20→1.10 เพื่อให้การ์ดที่กว้างสุดยังพอดี 512 px ที่ 60.75 px/หน่วย

### 5.1 ข้อ 4 — เงาจริง (รอบ 2026-10-28)

เดิม Battle ใช้ **decal ต่อการ์ด** (หนึ่ง quad ต่อการ์ด + crown patch ใต้ต้นขอบสนาม) เรียงเป็น call เดียว
ด้วย `ArenaCardShadowLayer` การ์ดทั้ง 126 ตัว `shadowCastingMode = Off`, key light `LightShadows.None`,
`RenderSettings.sun` ไม่ได้ตั้ง และ `ArenaCard.shader` ไม่มี pass `ShadowCaster` เลย → **ทั้งฉากสร้าง
shadow map ไม่ได้** (ยืนยันด้วยการพิมพ์ค่าจากฉากที่รันอยู่ ไม่ได้เดา)

- `ArenaCard.shader` ได้ pass `ShadowCaster` ที่ใช้ alpha ของ `_BaseMap` ตัวเดียวกับที่วาดจริง
  (`clip(alpha - _Cutoff)`) มุมโปร่งใสของการ์ดจึงไม่เข้า shadow map และล้อ sway ตามการ์ดที่วาด
- `ArenaBuilder` ตั้งการ์ดทุกตัว `shadowCastingMode = On`, `receiveShadows = false`, key light เป็น
  `LightShadows.Hard` และตั้งเป็น `RenderSettings.sun`; moon เอียงอยู่แล้วจากข้อ 3 — Dusk `(58, −40)`,
  Night `(46, −62)` องศา → เงาทอดยาว
- `ArenaGround.shader` รับ coverage จาก `GetMainLight(TransformWorldToShadowCoord(...))` แล้วคูณ tint
  ของพื้นด้วย `_ArenaShadowColor` / `_ArenaShadowStrength` (ค่ามาจาก profile เดิม: Dusk `(0.33,0.50,0.66)`
  0.60, Night `(0.16,0.24,0.44)` 0.74) — สลับ Dusk/Night ยังไม่แตะ asset
- ถอด `BuildCardShadows` / root `CardShadows` / `ArenaCardShadowLayer` ออกจากฉาก **Battle**
  (คลาสและ prefab ยังอยู่เพราะ `CampEnvironmentBuilder` ยังใช้)
- ตัวละครไม่แตะ: ยังใช้ `ArenaBlobShadows` และการ์ดไม่รับเงา การ์ดจึงแบนเหมือนเดิม เปลี่ยนแค่พื้น

วัดจากเฟรมเดียวกันที่หยุดเวลา (1920×1080, Editor Play Mode, i5-14400F + RTX 5060 Ti) —
`artifacts/stage08g/shadow-map-check.txt`, คู่ภาพ `shadow-map-shipped-on/off.png` และ `-wide-on/off.png`:

```
                          เปิดเงา        ปิดเงา (LightShadows.None)
cost (framing ที่เกมใช้) 65 batches     61 batches | +4
                          23 set-pass   21 set-pass | +2
                           2.32 ms       2.29 ms    | +0.03 ms

pixels ที่ขยับ (framing ที่เกมใช้)  2.80 % (58 058 / 2 073 600)  mean |Δ| 0.142  max Δ 19/255
pixels ที่ขยับ (wide 16.00 u)       5.39 % (111 668 / 2 073 600)  mean |Δ| 0.365  max Δ 19/255
```

ตัวเลข 65 batches ยังอยู่ต่องบ **≤ 200** ของ Stage 08f และส่วนต่างของข้อนี้คือ **+4 batches / +2 set-pass
/ +0.03 ms** (decal ที่ถอดออกเคยเป็น 1 call แต่ shadow pass วาดการ์ดทั้ง 126 ตัว) — พื้นที่ framing
ที่เกมใช้เงื่อนิด เพราะเงาของกำแพงต้นไม้ส่วนใหญ่ทอดไปนอกเฟรม; ที่ wide จะเห็นเงาต้นไม้บนพื้นชัด

- **เทสต์ของข้อ 4 (4 เคสใน `ArenaEnvironmentTests`):** `ArenaCardShaderUsesItsAlphaClippedShadowCasterPass`
  (pass มีจริง clip บน alpha และ prefab ต้นแบบยังเป็น `Off` เพื่อไม่ให้กระทบ Camp),
  `BattleCardsCastAlphaClippedShadowsFromTheSlantedMainLight` (การ์ดทุกตัวฉาย ไม่มีตัวรับ แสงเป็น `Hard`
  และเป็น `RenderSettings.sun` และมุมเอียงจริง), `ArenaFloorAppliesTheMainLightShadowCoverageTint`,
  `ActorBlobShadowsRemainSeparateFromTheCardShadowMap` — EditMode **311/311**

- **เทสต์:** ใหม่ `ArenaLookDevTests` (5 เคส) + แก้ `ArenaSharpnessTests` (สูตรความสูง) และ
  `ArenaEnvironmentTests.TheBorderWallIsDenseAndStandsThreeToFivePlayersHigh` (แทนข้อกำหนด "big tree ≥ 8 u"
  ด้วยย่านความสูงของกำแพง) — EditMode **309/309** ตอนปิดรอบนั้น (+ เทสต์ของข้อ 4 อีก 4 เคส — §5.1)

**ข้อจำกัดที่ต้องบอกตรง ๆ:** `PlayerHeight 1.6 u` คือความสูงที่โปรไฟล์ตัวละคร *ประกาศ* (`SpinePlayerProfile`,
`GenericCutePlayerProfile`) แต่ภาพที่วาดจริงสูงกว่า — วัดจากเฟรมได้ 95 px ก่อน (8.8 % ของ 1080) → ~150 px
หลัง (≈13.9 %) ขณะที่ 17c บันทึกไว้ว่า silhouette วาด ~2.6 u ทั้งที่โปรไฟล์สั่ง 1.6
ฉะนั้น **กล้องนี้วางกรอบให้ผู้เล่นตามที่โปรไฟล์ประกาศ (9.0 %) แต่ภาพที่เห็นคือ ~14 %** —
เมื่อแก้ silhouette ของ Spine ให้ตรงโปรไฟล์ (งานของสาย 17) เฟรมนี้จะได้ 8–10 % ตามที่ prompt ขอทันที

### 5.2 รอบที่สอง (2026-10-28): เงาไปถึงกลางเฟรมไหม — และรูปร่างของมัน

§7.5 ของรอบก่อนค้างคำถามเดียว: ที่ framing ที่เกมใช้ พิกเซลที่ขยับมีเพียง 2.80 % — เงาอยู่แค่แถบล่างของจอ
รอบนี้วัดหาสาเหตุแทนการเดา (`artifacts/stage08g/reach-before.txt`, `reach-after.txt`, `shadow-map-check.txt`):

- **สาเหตุที่หนึ่ง (แก้แล้ว): `shadowDistance` สั้นกว่าที่กล้องมอง.** กล้อง orthographic ถอยหลังผู้เล่น ~60 u
  และมองลึกไปอีก `OrthographicSize / sin 50°` = 11.6 u แต่ URP ตั้ง `m_ShadowDistance: 50` → map ถูก fade
  ทิ้งก่อนถึงกรอบ. หลักฐานคือ reach sweep: ที่ elevation 42°, 32°, 28°, 24°, 20° ค่าที่จุดพื้นทุกจุดได้
  **35/255 เท่ากันหมด** ซึ่งเป็นอาการของเงาที่ถูกตัดด้วย volume ไม่ใช่เงาที่สั้น/ยาวตามมุมแสง
  แก้เป็น **80** และเพิ่มเทสต์ `TheShadowDistanceReachesTheFarEdgeOfTheGroundTheFrameShows`
- **สาเหตุที่สอง (แก้แล้ว): azimuth พาเงาออกจากเฟรม.** euler.y −40° ทิ้งเงาไปทางซ้ายบนจนครึ่งหนึ่งหลุดจอ
  → Dusk `(58, −40)` เปลี่ยนเป็น `(62, −22)`, Night `(46, −62)` เปลี่ยนเป็น `(58, −26)` และเพิ่ม
  `ShadowStrength` Dusk 0.60 → **0.85** (สี `(0.22, 0.35, 0.52)`), Night 0.74 → **0.90**
  (`(0.17, 0.27, 0.47)`) เพราะเงาที่ทอดยาวขึ้นแต่ยังจางเท่าเดิมอ่านเป็นคราบ ไม่ใช่เงา

ตัวเลขหลังแก้ (เฟรมหยุดเวลา 1920×1080, i5-14400F + RTX 5060 Ti):

```
                     ก่อน (50, −40)      หลัง (80, −22)
shipped 5.56 %→        2.80 %              5.56 % (mean|Δ| 0.71, strong >10: 1.72 %, max 36/255)
wide (16 u)            5.39 %             13.01 % (mean|Δ| 2.10)
night shipped          2.80 %              6.23 %
night wide             5.39 %             14.78 %
```

การกระจายของพิกเซลที่ขยับ (shipped): แถบบน z +5.8..+11.6 = 6.88 %, กลาง z 0..+5.8 = 3.52 %,
แถบล่าง z −11.6..−5.8 = 9.91 % — และในสี่เหลี่ยมการเล่น `|x| ≤ 10, |z| ≤ 6` มีเพียง 1.67 % คือ
ลานกลางยังโล่งตามกติกาของ `ArenaLayout` ( canopy patch ห้ามอยู่กลางลาน)

**การอ่านของผู้เล่นไม่ลดลง:** annulus 90–210 px รอบตัวผู้เล่น (`readability.txt`) เปลี่ยน **0.00 %**
(mean luminance 0.1255 → 0.1255 ทั้ง Dusk และ 0.0484 → 0.0484 ทั้ง Night, worst pixel 0/255 จาก 12 562 ตัวอย่าง)
— เงาใหม่ตกในแถบขอบ ไม่ได้ทับพื้นที่ที่ตัวละครถูกอ่าน

**แต่รูปร่างยังผิด (OPEN).** ซูมภาพ wide (`zoom-shadow-shape.png`) และเฟรมที่เอา texture ของการ์ด
ออกทั้งใบ (`alpha-clear-on.png` — การ์ดมองไม่เห็นแล้ว แต่เงาบนพื้นยังเป็นสี่เหลี่ยม) ชี้ตรงกันว่า
**pass `ShadowCaster` ฉายเป็น quad เต็มใบ ไม่ได้ตัดด้วย alpha** แม้ source จะมี `clip(alpha - _Cutoff)`
ก็ตาม. สมมติฐานที่ตัดออกไปแล้วด้วยตัวเลข: mip ของ shadow map (2048 px / 80 u → การ์ด 10 u ≈ 256 texels
= mip 1 ไม่ใช่ mip ที่ทำให้ alpha เฉลี่ย), `shadowDistance` (แก้แล้ว), และ CBUFFER ของ pass (เหมือน forward
ทุกฟิลด์). รอบนี้ยัง *ไม่* หาสาเหตุได้ จึงยังไม่แก้

**สรุปสถานะข้อ 4 ของ prompt:** การต่อสาย PASS (การ์ดฉาย, แสงเป็น main light, พื้นรับผ่าน
`TransformWorldToShadowCoord`, ค่าใช้จ่าย +4 batches / +2 set-pass / +0.03 ms) แต่ **silhouette ของเงา
ยังไม่ผ่าน** — ปิด Stage เป็น PASS ไม่ได้จนกว่าจะแก้ แล้ววัดซ้ำว่าภาพรวมดีขึ้นจริง

## 6. ข้อ 6 — ยังไม่ได้ทำ (และข้อเสนอแตก Stage)

- **ข้อ 4 เงา — ทำแล้วในรอบหลัง (2026-10-28),** ดู §5.1 และ
  [ADR 0039](../adr/0039-the-arena-scenery-casts-a-shadow-map.md). รอบก่อนหน้าของรายงานนี้ประเมินว่า
  การกลับโมเดลเงาของ 08f "ควรเป็น Stage ย่อยของตัวเอง" — การวัดรอบนี้บอกว่าค่าใช้จ่ายจริงคือ
  **+4 batches / +2 set-pass / +0.03 ms** ซึ่งอยู่ในงบ ≤ 200 ของ 08f อยู่แล้ว จึงปิดเป็นส่วนหนึ่งของ
  08g ได้โดยไม่ต้องแยก Stage (แต่ยังต้องให้ผู้ใช้ตรวจด้วยตาก่อนประกาศ PASS ด้านภาพ)
- **ข้อ 6 HUD:** ต้องลบ `MiniBagPanel` (11.38 %) ออกทั้งสาย (ฉาก + `BattleHudView`/`BattleViewSettings` +
  `BattleCaptureState` + `UiCaptureTool`/`BossCaptureTool`/`TelegraphCaptureTool` + `UiLayoutCheck` +
  `BattleMiniBagTests` + PlayMode) แล้วจัดวาง Status/Timeline/Resource/Floor ใหม่ให้รวมกัน ≤ 15 %
  พร้อมเทสต์พื้นที่ — เป็นการรื้อ HUD เต็มรอบที่กระทบ ~10 ไฟล์และเทสต์ที่ยืนยันมันอยู่ **ไม่ใช่การปรับค่า**
  (พื้นที่ปัจจุบัน 37.86 %)

ตามกติกา AGENTS ข้อ 15: Stage นี้มี 7 ข้อ แตะ Unity scene + UI + เทสต์พร้อมกัน — **เหลือข้อ 6 (HUD)
ข้อเดียวที่ยังไม่ทำ** จึงเสนอเป็น `08g-2` โดยไม่เพิ่ม Stage เองจนกว่าจะได้รับอนุมัติ (ข้อ 1–5 ปิดแล้ว;
ข้อ 4 ที่รายงานรอบก่อนเสนอให้แยกเป็น `08g-3` ปิดในรอบนี้โดยไม่ต้องแยก — §6)

## 7. Blocker และสิ่งที่ยังไม่ได้ทดสอบ

1. `docs/art/target_keyart.png` **ไม่มีใน repo** (ตรวจทั้งไฟล์และประวัติ git) → ข้อ 3 ของ prompt (ปรับให้ใกล้
   target + บันทึกตัวเลขเทียบ) และการวางภาพเทียบในข้อ 7 ทำไม่ได้ตามที่ขอ; ที่รายงานได้คือ before/after ของตัวเอง
2. "ตารางค่าเริ่มต้นที่แนบ" ของ Night profile **ไม่ได้แนบมา** → ค่าที่ใช้เป็นค่าที่เลือกเองพร้อมเหตุผล
   (คอมเมนต์ใน `BuildNightProfile`)
3. `tools/test.ps1 -Suite Fast` **รันไม่จบในรุ่น pipeline ที่ติดตั้ง**: สคริปต์เรียกคำสั่งเดิม
   `unity command run_tests` ซึ่งถูกเปลี่ยนชื่อเป็น `unity command test run` และ payload ของ `test_status`
   เปลี่ยนจาก JSON เป็นข้อความ → Fast ได้ **PASS 12 | FAIL 1 | NOT RUN 3** โดย FAIL ที่ขั้น `unity: EditMode`
   เท่านั้น (`artifacts/stage08g/fast.log`) ขั้นที่ผ่านจริง: toolchain, dotnet build, xUnit 612, contract 48,
   sim 3, data 3, wiki 1. **แก้ tools/test.ps1 ไม่ใช่ขอบเขตของ Stage นี้** (แตะแล้ว suite ค้างเพราะ payload
   แบบใหม่ parse ไม่ได้) จึงคืนไฟล์เป็น HEAD และรัน EditMode ตรง ๆ ผ่าน CLI แทน: **309/309** (ข้อ 1–5)
   และ **311/311** หลังเพิ่มเทสต์ของข้อ 4 (ยังไม่ได้รัน `-Suite Fast` ใหม่ในรอบนี้)
   *(รายงานรอบก่อนเขียนว่า `tools/test.ps1 -Suite Fast` ผ่าน — นั่นเป็นการอ่าน `tail` ที่ตัดขั้น unity ออกไป
   ไม่ใช่ผลของ suite: ต้องถือว่ารายงานรอบก่อนยืนยันส่วนนี้ผิด)*
4. ไม่ได้ทดสอบ: WebGL build และ Playwright (Stage นี้ไม่ใช่ gate), PlayMode ผ่าน CLI (ค้นพบ 0 เทสต์ —
   ข้อจำกัดของ pipeline ในรุ่นนี้), และ WebGL path ของ `CompressedHQ` (ยังไม่เคย build)
5. **เงาของข้อ 4 ยังไม่ผ่านตาเจ้าของงาน — และรอบที่สองยังเจอ defect ใหม่.** ความเข้ม/การเข้าถึงเฟรมแก้แล้ว
   (§5.2: 2.80 % → 5.56 %) แต่ **เงาที่ได้เป็นสี่เหลี่ยมเต็ม quad ไม่ใช่ silhouette ของใบไม้**
   (`artifacts/stage08g/zoom-shadow-shape.png`, `alpha-clear-on.png`) แปลว่า `ShadowCaster` pass
   ยังไม่ตัด alpha ตอนรันจริง ต้องแก้ก่อนจึงจะเรียกว่าข้อ 4 ผ่าน และต้องให้เจ้าของงานดูภาพก่อนประกาศ PASS
   ด้านภาพ
6. **ยังไม่ได้ทดสอบ:** `tools/test.ps1 -Suite Fast` ยังพังเหมือนเดิม (สคริปต์เรียกคำสั่ง `unity command run_tests`
   ที่ถูกเปลี่ยนชื่อแล้ว — ไม่ได้อยู่ในขอบเขต Stage นี้) รอบนี้จึงรัน `unity command test run --mode editor`
   ตรง ๆ ได้ **312/312**; ไม่ได้รัน WebGL build / Playwright (ไม่ใช่ gate stage)

## 8. คำสั่งที่รันจริง

```text
unity command editor invoke_menu "ProjectSurvive/Stage 08e/1. Build arena (theme + seed)"
unity command --project-path UnityProject script recompile  →  completed
unity command --project-path UnityProject script run --file AgentScripts/ArenaStage08gCapture.cs \
       --entry AgentScripts.ArenaStage08gCapture.Run          (tag = before, then after)
unity command --project-path UnityProject test run --mode editor        → 309/309
powershell -File tools/test.ps1 -Suite Fast                             → PASS 12 | FAIL 1 | NOT RUN 3

# รอบปิดข้อ 4 (2026-10-28)
unity command editor.playmode stop / start
unity command --project-path UnityProject script run --file AgentScripts/ArenaShadowMapCheck.cs \
       --entry AgentScripts.ArenaShadowMapCheck.Run      → ok (cost + คู่ภาพ on/off, 2 framing)
unity command --project-path UnityProject test run --mode editor        → 311/311

# รอบที่สอง (2026-10-28): shadow distance + มุมแสง + รูปร่างเงา
unity command editor.playmode stop / start
unity command --project-path UnityProject script run --file AgentScripts/ArenaShadowMapCheck.cs \
       --entry AgentScripts.ArenaShadowMapCheck.Run      → ok (cost, reach sweep, 4 คู่ภาพ: shipped/wide/night)
python artifacts/stage08g/reach.py ""        → reach-after.txt (การกระจายของพิกเซลที่ขยับตามแถว z)
python artifacts/stage08g/readability.py      → readability.txt (annulus รอบผู้เล่น Δ 0.00 %)
python artifacts/stage08g/zoom.py            → zoom-shadow-shape.png
python artifacts/stage08g/compare.py         → shadow-on-off-diff-sheet.png
unity command --project-path UnityProject test run --mode editor        → 312/312
```
