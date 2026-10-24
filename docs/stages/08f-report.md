# Stage 08f — Dusk/Night สองหน้าตาจาก asset ชุดเดียว, เงา decal และการอ่านออก

Presentation เท่านั้น: `git status --porcelain lib data` ว่าง ไม่มีการแก้ Simulation, Domain, `data/`
หรือกติกาเกมใน Stage นี้

## 1. สรุป

- `ArenaLightingProfile` (ScriptableObject) เป็นเจ้าของบรรยากาศทั้งหมด: ambient, directional light
  ดวงเดียว, สีเงา + มุมเงาเดียวของฉาก, fog, แถบมืดบนจอ, ความเข้มไฟปลอม และ post grade
  shader ของฉากอ่านผ่าน global เท่านั้น → สลับ Dusk ↔ Night ได้โดยไม่แตะ material/texture
- เงาของการ์ดเป็น quad บนพื้น (ไม่มี shadow map): ยาว = `height × ShadowLengthPerHeight`,
  กว้าง = ความกว้างของการ์ด, หันตาม `ShadowDirectionDegrees` 245° เดียวทั้งฉาก และอยู่คิวใต้ actor ทุกตัว
- 187 เงาถูก bake เมทริกซ์ครั้งเดียวแล้ววาดด้วย `DrawMeshInstanced` 1 call (ถ้าเป็น renderer ปกติ
  เฟรม 300 ผีจะขึ้นไป 205 batches เกินงบ 200)
- ความอ่านออกถูกวัดเป็นเลข WCAG luminance contrast บนพิกเซลของ PNG ที่บันทึกจริง ไม่ใช่ความเห็น:
  Dusk/Night และ Dusk 300 ผี ผ่านเกณฑ์ทุกช่อง
- เจอบั๊กเครื่องมือระหว่างปิด Stage: `.br/.gz` ของ build เก่าอยู่ต่อหลัง build ใหม่ ทำให้ Playwright
  แรกทดสอบ Development player ที่ถูกแทนไปแล้ว แก้แล้ว (ดู §5) และ Release gate ผ่านจริง 21/21

## 2. สิ่งที่ทำ

| ข้อ | สิ่งที่ทำ | ไฟล์ |
|---|---|---|
| 1 | profile 2 ตัว (Dusk = ค่าเริ่มต้น, Night = key เย็นลง) + `ArenaEnvironmentView.SetTimeOfDay` | `ArenaLightingProfile.cs`, `Content/Arena/ArenaLightingDusk/Night.asset` |
| 2 | เงา decal ต่อการ์ด + canopy เฉพาะ border tree, ห้ามใน centre | `ArenaShadowCard.cs`, `ArenaCardShadow.prefab`, `ArenaShadow.shader` |
| 3 | วาดเงาทั้งหมดด้วย instanced call เดียว, ปิด renderer รายชิ้น | `ArenaCardShadowLayer.cs` |
| 4 | แถบมืดขอบบนจอแบบ screen-space (sorting −100 ใต้ HUD) | `ArenaScreenGradient.cs` |
| 5 | ตัววัด contrast (histogram, ไม่มี allocation ต่อเฟรม) + เกณฑ์ใน theme | `ArenaContrast.cs`, `ArenaTheme.cs` |
| 6 | ศิลป์ placeholder ใส่ rim ให้ actor อ่านบนพื้นมืด + พื้น/ต้นไม้ปรับตาม profile | `BattlePlaceholderArt.cs`, `ArenaArtBuilder.cs` |

## 3. Acceptance

| # | เกณฑ์ | ผล | หลักฐาน |
|---|---|---|---|
| 1 | ฉากเริ่มที่ Dusk และหน้าตาเดิมยังใช้ได้ | **PASS** | `artifacts/ui/battle-env/stage08f-dusk-*.png` |
| 2 | สลับ Night แล้วไม่มี asset ไหนถูกแก้ | **PASS** | `SwitchingProfilesTouchesNoAsset`, `AProfileCannotChangeAnAsset`, `assets unchanged by the profile switch: True` |
| 3 | เงาทุกอันหันตามมุมเดียวจาก profile และไม่ทับ actor | **PASS** | `EveryShadowCardFollowsTheProfilesSingleDirection`, `ShadowsAreAlwaysDrawnUnderEveryActor` (187 เงา, 74 canopy) |
| 4 | centre ไม่มีเงาใหญ่ | **PASS** | `TheOpenCentreHoldsNoLargeShadow` |
| 5 | Dusk และ Night อ่านออกตามเกณฑ์ WCAG | **PASS** | `stage08f-contrast.txt` (8/8 บรรทัด `pass=True`) |
| 6 | เฟรม 300 ผี ผู้เล่น/ผี/pickup/telegraph อ่านออก | **PASS** | `dusk300` 2 บรรทัดใน `stage08f-contrast.txt` |
| 7 | batches ≤ 200 ที่ 300 ผี | **PASS** | 138 batches / 138 draw calls, 2.45–2.82 ms (`stage08f-metrics.txt`) |
| 8 | ไม่มี view เกิดใหม่ระหว่างเล่น | **PASS** | `viewsGrownDuringPlay=0` |
| 9 | แถบขอบบนจออยู่ใต้ HUD และมาจาก profile | **PASS** | `TheScreenBandIsBelowTheHudAndDrivenByTheProfile` |
| 10 | ภาพรวมสวย/ตรงสไตล์ (คนตัดสิน) | **NOT RUN** | รอผู้ใช้ตรวจ `stage08f-dusk-1920x1080.png` / `stage08f-night-1920x1080.png` |

Automated: `ArenaEnvironmentTests` **27/27**, EditMode ทั้งหมด **234/234**, `-Suite Fast` **13 PASS / 0 FAIL**,
`-Suite Full -Release` **15 PASS / 0 FAIL** (WebGL Release build PASS, Playwright **21 passed**,
hosting **13/13**)

## 4. ตัวเลขที่วัดได้ (i5-14400F / RTX 5060 Ti / 32 GB, Unity 6000.3.9f1 Editor Play Mode)

```
dusk 1920x1080   player=6.67 ghost=5.08 pickup=6.92 telegraph=1.43  pass=True
dusk 1280x720    player=6.58 ghost=3.07 pickup=6.92 telegraph=1.43  pass=True
night 1920x1080  player=9.77 ghost=7.90 pickup=11.36 telegraph=1.88 pass=True
night 1280x720   player=9.63 ghost=4.73 pickup=11.36 telegraph=1.88 pass=True
dusk300 1920x1080 player=6.67 ghost=5.81 pickup=6.92 telegraph=1.43 pass=True
dusk300 1280x720  player=6.58 ghost=5.69 pickup=6.92 telegraph=1.43 pass=True
```

| สถานะ | batches | draw calls | ms/frame | entities |
|---|---|---|---|---|
| dusk 1920×1080 | 126 | 126 | 2.21 | 21 |
| dusk 1280×720 | 127 | 127 | 2.29 | 21 |
| night 1920×1080 | 128 | 128 | 2.33 | 22 |
| night 1280×720 | 128 | 128 | 2.29 | 22 |
| dusk300 1920×1080 | 138 | 138 | 2.82 | 311 |
| dusk300 1280×720 | 138 | 138 | 2.45 | 311 |

Stage 08e เทียบ: centre 133/148, edge 167/172 batches ที่ 300 ผี → Stage นี้ถูกกว่าเพราะเงาเหลือ call เดียว
WebGL Release candidate: **15.72 MiB gzip / 11.49 MiB Brotli** (gate < 30 / target < 15)

## 5. บั๊กเครื่องมือที่เจอและแก้ (commit `generic:`)

รัน `-Suite Full -Release` ครั้งแรกแล้ว Playwright FAIL 2 เคส (`a release build published the harness
document`, `a release build published the dev menu`) ทั้งที่ build ที่เพิ่งเสร็จเป็น Release:
`Build/Web.wasm` เป็น Release (33.84 MB) แต่ `Web.wasm.br` ยังเป็นของ Development จากรอบก่อน
(16.8 MB) และตัวที่เบราว์เซอร์ได้คือ sibling ที่ค้างอยู่ — อาการเดียวกับที่ Stage 08e เคยรายงานไว้

แก้ที่ต้นเหตุสองจุด:

- `tools/build-web.ps1` ลบ `*.br` / `*.gz` ใน output directory **ก่อน** build (เป็น derived data
  ที่ `tools/compress-web.mjs` เขียนใหม่เสมอ) → sibling เก่าไม่มีทางอยู่รอดข้าม build
- `tools/test.ps1` เรียก `compress-web.mjs` **หลัง build ก่อน Playwright** → เบราว์เซอร์อ่านไบต์ของ
  build ที่เพิ่งเสร็จ ไม่ใช่ของรอบก่อน (hosting step ยังอัดซ้ำแบบ idempotent)

หลังแก้: Playwright 21/21, hosting 13/13, payload 15.72 MiB gzip / 11.49 MiB Brotli

## 6. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง

- **ภาพยังไม่ประกาศ PASS** — ตัวเลขผ่าน แต่ความรู้สึกของฉาก (สีเขียวมะกอกแบน ๆ, เงาเป็นดวงใหญ่
  ใต้ต้นไม้ชายขอบที่ดูหลุดจากต้น) ต้องให้ผู้ใช้ดูภาพก่อน
- pickup/telegraph วัดจาก probe ที่วาดด้วยสไปรต์จริงและ sorting order จริงในตำแหน่งคงที่
  ไม่ได้สุ่มวัด pickup ที่เกิดจริงในเกม → ถ้า pickup จริงไปเกิดบน decal สว่าง ค่าอาจต่ำกว่านี้
- เงาเป็นดวงในที่ที่การ์ดอยู่นอกจอ (ความยาวเงาอิงความสูงต้นไม้) จึงเห็นเป็นรอยคล้ำลอย ๆ ใกล้ขอบล่าง
- Development build (debug symbols) มี payload **30.81 MiB gzip** ซึ่งเกิน gate เดียวกัน; การวัดที่
  มีความหมายคือ configuration ที่ ship จริง (`-Release`, ADR 0011/0035) — Full suite ที่ไม่ใส่
  `-Release` จะ FAIL ที่ hosting step ด้วยเหตุนี้
- EditMode/PlayMode ใน Full -Release เป็น NOT RUN เพราะต้องปิด Unity Editor ให้ WebGL build ออก
  (build ไม่สามารถแชร์ project กับ Editor ได้) — ทั้งคู่รันแยกแล้วในรอบที่ Editor เปิด: EditMode
  234/234 และ PlayMode TitleScene 5/5

## 7. คำสั่งที่รันจริง

```text
unity-cli run_tests ArenaEnvironmentTests (EditMode)              -> 27/27 passed
tools/test.ps1 -Suite Fast                                        -> PASS 13 | FAIL 0 | NOT RUN 3 (EditMode 234/234)
unity-cli run_script AgentScripts/ArenaStage08fCapture.cs          -> 6 ภาพ + stage08f-contrast.txt + stage08f-metrics.txt
tools/test.ps1 -Suite Full                                        -> Editor เปิด: EditMode 234/234, PlayMode 5/5, web NOT RUN (project lock)
tools/test.ps1 -Suite Full -Release                               -> PASS 15 | FAIL 0 | NOT RUN 1 (EditMode), Playwright 21/21
```
