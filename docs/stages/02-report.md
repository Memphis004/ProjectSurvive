# Stage 02 — Static Data Pipeline: รายงาน

- **วันที่:** 2026-10-13
- **ขอบเขต:** Luban 5.1.0 + protoc 33.6 (ล็อก + ติดตั้งแบบ SHA-256), `data/`
  (schema + 12 ตาราง), stable id + reference validation, `tools/gen.ps1` /
  `validate-data.ps1` / `verify-generated.ps1` / `check-fixtures.ps1`,
  generated C# → `lib/Runtime/Generated/Luban`, `.bytes` → `Resources/Config` +
  `lib/Tests~/data`, `IGameConfig` adapter, config hash ลง build metadata,
  `proto/app.proto` → `App.cs`, fixture ข้อมูลผิด 12 ชุด, ADR 0005/0006
- **สถานะรวม:** Acceptance ทั้ง 4 ข้อ **PASS** (ทั้งหมดพิสูจน์ได้โดยไม่ต้องเปิด Unity)
  — ไม่มีข้อไหนต้องรอ Stage 03

---

## 1. สิ่งที่ทำสำเร็จ

1. **ตรวจ CLI จริงก่อนใช้ ไม่เดา syntax** — ดาวน์โหลด Luban `v5.1.0`
   (`df34215d2e22035073d1af83d44e3ac0744dc3b3`) และ protoc `33.6` แล้วรัน
   `--version` และ `--help` จริง ก่อนเขียนคำสั่งใด ๆ จากนั้นบันทึก URL + SHA-256 +
   probe command ลง `tools/versions.lock.json` → `vendoredTools` และให้
   `tools/setup-tools.ps1` เป็นตัวติดตั้ง (ตรวจ hash ก่อนแตกไฟล์, idempotent,
   ไม่ commit ตัวไบน์นารี)
   - **`--strict` จำเป็น**: ถ้าไม่ใส่ validator `#ref`/`#(range)` ที่ fail แค่
     *เตือน* แล้ว CLI ยัง exit 0 → build ที่ข้อมูลพังแต่ผ่าน ตรวจแล้วกับไบน์นารีจริง
2. **`data/` ครบ schema ขั้นต่ำ 12 ตาราง** — `Defines/schema.xml` (8 enum, 12 bean),
   `Defines/__tables__.csv` (registry), `tables/*.csv`, `localization/localization.csv`
   - **footprint เป็นรายการช่อง `"x,y"` คั่นด้วย `|`** ไม่ใช่สี่เหลี่ยม/บิตแมป →
     "รูปทรงว่าง" และ "ช่องซ้ำ" กลายเป็นข้อผิดพลาดที่ตรวจได้ในข้อมูล + normalize
     ที่ origin ให้ Stage 05 หมุนได้โดยไม่ต้องเดา (ADR 0005)
   - ตัวเลขบาลานซ์ทั้งหมดอยู่ใน CSV; ข้อความผู้เล่นทั้งหมดอยู่ในตาราง localization
     (ทั้ง `th` และ `en` ต้องไม่ว่าง) ตาม AGENTS rule 5
3. **`IGameConfig` เป็น port ของ Domain** — Domain/Application/Simulation ไม่แตะ
   generated row เลย; `LubanGameConfig` (Infrastructure) เป็นไฟล์เดียวที่รู้จัก
   `ProjectSurvive.Config.*` และไม่รู้จัก `UnityEngine`/`StreamingAssets`
   (ส่ง `Func<string, byte[]>` เข้ามา) — ADR 0006
4. **config hash ลง build metadata** — SHA-256 (hex ใหญ่) ทับ `.bytes` เรียงตามชื่อ
   ตาราง แล้วเขียนเป็น `ConfigBuildMetadata.g.cs`; ฝั่ง PowerShell (`gen.ps1`) และ
   ฝั่ง C# (`Data.Validator.ConfigHash`) เป็นอัลกอริทึมเดียวกันและมีเทสต์บังคับ
5. **`proto/app.proto` ขั้นต่ำ → `lib/Runtime/Generated/Proto/App.cs`** มีที่เดียว
   ในเรโป (บังคับด้วยเทสต์) และ commit ไว้ให้ diff ได้
6. **แยกหน้าที่การตรวจสองชั้นอย่างตั้งใจ**
   - **Luban `--strict`** = schema: ชนิดคอลัมน์, primary key ซ้ำ, `#ref`, `#(range=)`
   - **`lib/Tests~/Data.Validator`** = กฎเกมข้ามตารางที่ Luban พูดไม่ได้ เพราะ
     "ช่องว่าง" เป็นค่าที่ถูกต้องในหลายคอลัมน์: id ซ้ำข้าม item/bag, reference ที่
     เป็น optional, footprint ว่าง/ซ้ำ/ผิดรูป, cooldown ต่ำกว่า
     `constant.min_cooldown_seconds`, recipe ข้ามระดับ, floor ที่ไม่มี wave,
     band ของร้านที่ทำให้บางชั้นสุ่มไม่ได้, ราคา ≤ 0, localization ขาดภาษา
7. **ห้าประตูที่รันซ้ำได้จาก cwd ไหนก็ได้** (`tools/*.ps1`) ทุกตัวคืน exit code ตาม
   สัญญาเดิม (`0` ผ่าน, `1` check ไม่ผ่าน, `2` รันไม่ได้) และ `tools/test.ps1`
   เรียกทั้งชุดให้แล้ว (`-NoPipeline` ข้ามได้)
8. **fixture ข้อมูลผิด 12 ชุด** ใต้ `lib/Tests~/fixtures/` — แต่ละชุด overlay
   ตารางเดียวที่จงใจพัง + `fixture.json` บอกว่าชั้นไหนต้องจับได้
   และคาดหวัง finding code อะไร (README อธิบายวิธีเพิ่ม)
9. **เทสต์เพิ่ม 73 เคส (รวมเป็น 130)** — `ConfigLoadTests` (อ่านตารางจริงผ่าน
   generated reader), `ConfigRulesTests` (46 เคส: 1 กฎ = 1 เคส + Theory ของ
   footprint), `ConfigEnumParityTests` (domain enum ↔ generated enum),
   `ArchitectureTests` ที่เพิ่ม guard ของชั้น Generated
10. **เอกสาร**: ADR 0005 (pipeline/รูปแบบข้อมูล), ADR 0006 (port + การกัน generated
    code ออกจาก Domain), `llmwiki/systems/data-schema.md`,
    `llmwiki/gotchas/luban-5-strict-and-tables.md`, อัปเดต overview / dependencies /
    knowledge / index และ `tools/versions.lock.json`

---

## 2. ไฟล์สำคัญที่เปลี่ยน / เพิ่ม

**schema + ข้อมูล (`data/`)**

| ไฟล์ | หมายเหตุ |
| --- | --- |
| `data/luban.conf` | `schemaFiles` ต้องมี `{ "type": "table" }` คู่กับ `__tables__.csv` |
| `data/Defines/schema.xml` | 8 enum + 12 bean; `#ref`/`#(range=)` เท่าที่ใส่ได้จริง |
| `data/Defines/__tables__.csv` | 12 ตาราง (`mode=one` สำหรับ economy/constant) |
| `data/tables/*.csv`, `data/localization/localization.csv` | ข้อมูลตั้งต้น 33 key |

**เครื่องมือ (`tools/`)**

| ไฟล์ | หน้าที่ |
| --- | --- |
| `setup-tools.ps1` | ติดตั้ง Luban + protoc ตาม `versions.lock.json` (ตรวจ SHA-256) |
| `gen.ps1` | Luban `--strict` → C# + `.bytes`, config hash, protoc; `-OutDir` สำหรับ dry-run |
| `validate-data.ps1` | ขับ `Data.Validator` บน `.bytes` |
| `verify-generated.ps1` | generate ใหม่ลง temp แล้วเทียบทุกไฟล์กับของที่ commit |
| `check-fixtures.ps1` | รัน fixture ทั้งหมด + control (ข้อมูลจริงต้องผ่าน) |
| `test.ps1` | เพิ่มส่วนที่ 6: validate / reproduce / reject fixtures |

**generated (`lib/Runtime/Generated/`)**

- `Luban/*.cs` 34 ไฟล์ (33 จาก Luban + `ConfigBuildMetadata.g.cs`), `Proto/App.cs`
- asmdef ของทั้งสองโฟลเดอร์ (`noEngineReferences: true`, `autoReferenced: false`,
  Proto ใช้ `overrideReferences` + `Google.Protobuf.dll`)

**Domain/Infrastructure**

| ไฟล์ | หมายเหตุ |
| --- | --- |
| `Domain/Config/IGameConfig.cs`, `ConfigTables.cs`, `ConfigEnums.cs`, `Footprint.cs`, `CellOffset.cs` | port + ชนิดข้อมูลฝั่ง Domain (ไม่รู้จัก generated) |
| `Infrastructure/Config/LubanGameConfig.cs` | adapter ไฟล์เดียวที่อ่าน generated row; throw `ConfigLoadException` ไม่มี fallback |
| `Infrastructure/ProjectSurvive.Core.Infrastructure.asmdef` + csproj | อ้าง `Generated.Luban` + `Luban.Runtime` (เพิ่มในเมทริกซ์ที่เทสต์ตรวจ) |

**เทสต์ + fixtures**

- `Core.Tests/{ConfigLoadTests,ConfigRulesTests,ConfigEnumParityTests,ConfigFixture,RepoLayout}.cs`
- `Core.Tests/ArchitectureTests.cs` (เพิ่ม 4 เทสต์ของชั้น Generated, ลด RepoRoot ที่ซ้ำ)
- `lib/Tests~/fixtures/**` (12 ชุด + README), `lib/Tests~/Data.Validator/**`

---

## 3. คำสั่งที่รันจริง

```powershell
# 1. ติดตั้ง toolchain ที่ล็อกไว้ (ตรวจ SHA-256 ก่อนแตกไฟล์)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/setup-tools.ps1

# 2. generate (รันซ้ำได้จาก cwd ไหนก็ได้)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen.ps1

# 3. ประตูตรวจข้อมูล (ผ่านสะอาด)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1 -DataDir "lib/Tests~/data" -NoWrite

# 4. ทางล้มเหลวของประตูที่ 3: ชี้ไปโฟลเดอร์ที่ไม่มี
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validate-data.ps1 -DataDir "data/does-not-exist" -NoWrite   # ต้อง exit 1

# 5. พิสูจน์ว่า generate ซ้ำได้ผลเดิม
powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1 -NoWrite

# 6. พิสูจน์ว่า fixture พังถูกปฏิเสธจริง
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1 -NoWrite

# 7. mutation test ของตัวประตูเอง (ชั่วคราว): fixture ที่ "ถูกต้อง" แต่ประกาศ stage=luban ต้อง FAIL
#    (สร้าง zz-selfcheck, รัน -Only zz-selfcheck, ยืนยัน FAIL, ลบทิ้ง)

# 8. เทสต์ทั้งหมด + harness (Debug และ Release)
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" -c Debug --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -NoWrite
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Configuration Release -NoWrite

# 9. วัดว่า generate ซ้ำให้ไฟล์เดิมทุกไบต์
#    sha256 ของ (generated *.cs + ทุก *.bytes) ก่อน/หลัง gen.ps1 รอบที่สอง

# 10. path independence: รัน gen/validate/verify/fixtures จาก cwd อื่น (tools/ และ lib/Tests~)
```

---

## 4. ผลและที่เก็บ log / artifact

| คำสั่ง | ผลจริง |
| --- | --- |
| `setup-tools.ps1` | ติดตั้งสำเร็จ; `Luban.exe --version` → `Luban 5.1.0+df34215d…`; `protoc.exe --version` → `libprotoc 33.6` |
| `gen.ps1` | `33 .cs, 12 .bytes`; configHash `0010A515D0704880A58B3912D11EA5D4FE96E6986337399CF40D39DEC1941114`; protoc → `App.cs`; `RESULT: OK` |
| `validate-data.ps1` (ข้อมูลจริง) | `PASS 5 / FAIL 0`; rows: items 9, bags 3, skills 4, synergies 5, mergeRecipes 8, enemies 4, floors 5, waves 9, shopPool 5, localization 33 |
| `validate-data.ps1` (โฟลเดอร์ไม่มี) | exit **1**, `[FAIL] data directory` — ยืนยันว่าประตูไม่ผ่านแบบเงียบ |
| `verify-generated.ps1` | `PASS 8 / FAIL 0` — C# 34 ไฟล์, `.bytes` 12×2 ตรงกันทุกไฟล์, `App.cs` มีที่เดียว, hash ใน metadata ตรงกับตารางที่ generate ใหม่ |
| `check-fixtures.ps1` | `PASS 16 / FAIL 0 / SKIP 0` — control 2 ข้อ + fixture 12 ชุด + toolchain/binary |
| mutation test ของ `check-fixtures.ps1` | fixture ที่ถูกต้องแต่ประกาศ `stage=luban` → `[FAIL]`, exit 1 (ลบ fixture ชั่วคราวแล้ว) |
| `dotnet test` | **130 passed, 0 failed** (Stage 01 = 57, Stage 02 เพิ่ม 73) |
| `tools/test.ps1` (Debug) | `PASS 10 / FAIL 0 / WARN 0 / SKIP 0` → `RESULT: OK` |
| `tools/test.ps1` (Release) | ดูหัวข้อ 4.1 |
| idempotency | sha256 รวมของ 35 `*.cs` + 24 `*.bytes` = `3e034c0953eb37693ac6078678f35097cf37b980df8539816d9413ec29069da6` **เท่ากันก่อนและหลัง** `gen.ps1` รอบที่สอง |
| `git diff` หลัง commit แล้ว generate ใหม่ | **ว่าง** — และ `git status --porcelain` ไม่รายงานไฟล์ tracked ใด ๆ เปลี่ยน (หลัง commit `446cd2d` รัน `gen.ps1` อีกครั้ง) |

log: `artifacts/` (ไม่ commit) — `artifacts/config/{gen-report,validate-data,verify-generated-report,check-fixtures-report}.json`,
`artifacts/dotnet/{build,test,validate-data,verify-generated,check-fixtures}.log` และ `artifacts/dotnet/core-tests.trx`
(การรันในรอบนี้ใช้ `-NoWrite` จึงอ่านค่าจาก stdout; ไฟล์จะถูกเขียนเมื่อรันโดยไม่ใส่ flag)

### 4.1 ผล Release (`tools/test.ps1 -Configuration Release -NoWrite`)

```
[PASS] .NET SDK                 6.0.428; 9.0.311
[PASS] dotnet solution          lib/Tests~/ProjectSurvive.Core.sln
[PASS] dotnet build             Release configuration
[PASS] build warnings           none
[PASS] dotnet test              130 passed
[PASS] Sim determinism          seed 20261013 -> determinismDigest=0x841b4e46; seed 20261014 -> 0x23f674fd
[PASS] Sim stream separation    shopDigest=0x2d476321 battleDigest=0xbeb639e1 visualDigest=0xe8742eda
[PASS] data: generated tables validate   no findings
[PASS] data: generated code is reproducible   regeneration reproduces the tracked output
[PASS] data: broken fixtures are rejected   12 fixture(s) rejected
----------------------------------------------------------------------
  PASS 10  |  FAIL 0  |  WARN 0  |  SKIP 0      RESULT: OK
```

Debug ให้ผลเหมือนกันทุกบรรทัด (`PASS 10 / FAIL 0`), ต่างแค่ `Debug configuration`
— สังเกตว่า digest ของ Sim เท่ากันทั้งสอง configuration ซึ่งยืนยันว่า
`Deterministic`/การคิดเลขไม่เปลี่ยนตาม optimization level
(log: `/tmp/ps02-release.log` ระหว่างการรัน; ไม่ได้คัดลอกเข้า `artifacts/`
เพราะรันด้วย `-NoWrite`)

---

## 5. สิ่งที่ไม่ได้ทดสอบและเหตุผล

1. **ยังไม่เคยเปิด Unity เลย** — Stage 02 ไม่มี Unity editor ในสโคป: asmdef ทั้ง 8
   ไฟล์ยังไม่ถูก resolve จริง และ `.bytes` ใน `Resources/Config` ยังไม่เคยถูกโหลด
   ผ่าน `Resources.Load` ใน Web build → Stage 03
2. **Google.Protobuf บน Web/IL2CPP** — คอมไพล์และ generate ได้แล้ว แต่ stripping/AOT
   บน WebGL2 ยังพิสูจน์ไม่ได้จนกว่าจะมี build แรก (Stage 03)
3. **ไม่มีเบนช์มาร์ก performance** — Stage นี้ไม่มีการอ้างตัวเลข performance ใด ๆ
   (เวลา build/generate ที่วัดได้เป็นเวลาของ process ทั้งหมด ไม่ใช่ตัวเลขเกม)
4. **ความ deterministic ข้าม runtime ของ hash/table bytes** — พิสูจน์แค่ "รันซ้ำบน
   เครื่องเดียวกันได้ไบต์เดิม" ตามขอบเขตที่ AGENTS rule 6 กำหนด ไม่ได้อ้างข้าม CPU/browser
5. **กฎข้ามตารางที่ต้องมีเนื้อหา Stage 04** — เช่น "tag ที่ synergy อ้างไม่มี item ไหน
   ใช้" ยังไม่เป็น error เพราะชุด tag จริงจะมาพร้อมเนื้อหา Stage 04 (บันทึกไว้ใน
   `llmwiki/systems/data-schema.md` แล้วว่าเป็นข้อจำกัดที่รู้ตัว)
6. **Playwright / Firefox / CI** — ยังไม่มี (Stage 03/14) และ Firefox ไม่ได้ติดตั้งบน
   เครื่องนี้ จึงยังไม่มีหลักฐานข้ามเบราว์เซอร์ใด ๆ

---

## 6. Blocker และความเสี่ยง

- **blocker:** ไม่มี — Stage นี้ไม่ต้องใช้ Unity license, editor หรือ browser
- **ความเสี่ยงที่รับไว้ (พร้อมเหตุผล):**
  1. **`lib/Tests~/data` และ `Resources/Config` ไม่ได้ commit** (ADR 0001) → เครื่องที่
     clone ใหม่ **ต้องมี .NET 8 runtime + 7-Zip** แล้วรัน `setup-tools.ps1` + `gen.ps1`
     ก่อน `dotnet test` มิฉะนั้นจะเห็น `FileNotFoundException` ที่บอกให้รัน `gen.ps1`
     (ตั้งใจให้ข้อความกำกวมน้อยที่สุด) — ถ้าอยากให้ CI ไม่ต้องดาวน์โหลด Luban สามารถ
     เพิ่ม cache ของ `tools/vendor` ได้โดยไม่ต้องแก้สัญญาใด ๆ
  2. **CLI version drift** — `setup-tools.ps1` ตรวจ SHA-256 ของ *archive*
     แต่ไม่ได้ล็อก checksum ของไฟล์ที่แตกออกมา (7z/zip ไม่ให้ manifest ของ
     extracted bytes มาโดยตรง); probe `--version` เป็นหลักประกันชั้นที่สอง
  3. **schema increment ยังไม่มี migration story** — `SchemaVersion` เป็น const ที่
     แก้มือใน `gen.ps1`; ยังไม่มี save format ที่อ่านตารางเหล่านี้ (Stage 12)
  4. **เพิ่มตารางหนึ่งตารางต้องแก้ 4 ที่** (`__tables__.csv`, schema, CSV,
     `LubanGameConfig.TableNames`) — แลกมากับการได้ compile-time checking ของ loader key
  5. **รูปทรง footprint เขียนได้ยาว** — ยอมรับเพราะชิ้นที่ใหญ่สุดใน prototype คือ 3×3
     และรูปทรงคือตัวเลขบาลานซ์ที่คนต้องอ่านออก

---

## 7. Acceptance แต่ละข้อ

| # | Acceptance (Stage 02) | ผล | หลักฐาน |
| --- | --- | --- | --- |
| 1 | generate ซ้ำจาก input เดิมแล้ว `git diff` ว่าง | **PASS** | `verify-generated.ps1` (`PASS 8 / FAIL 0`) + sha256 รวมของ 35 `*.cs` + 24 `*.bytes` เท่ากันก่อน/หลัง `gen.ps1` รอบที่สอง (`3e034c09…`) และหลัง commit `446cd2d` รัน `gen.ps1` อีกครั้ง: `git diff` ว่าง, `git status --porcelain` ไม่มีไฟล์ tracked เปลี่ยน |
| 2 | fixture ที่ผิดต้อง fail | **PASS** | `check-fixtures.ps1` `PASS 16 / FAIL 0`: 5 ชุดถูกปฏิเสธโดย Luban (id ซ้ำ, `#ref` หาย, ค่าเกิน range, wave อ้างศัตรูที่ไม่มี), 7 ชุดถูกปฏิเสธโดย validator พร้อม finding code ตรงตามที่ประกาศ; control = ข้อมูลจริงผ่าน; และ mutation test ยืนยันว่าประตูตัวนี้ FAIL ได้จริง |
| 3 | generated code compile ได้ และเทสต์ Core อ่านตารางตัวอย่างได้ | **PASS** | `dotnet build` 0 error 0 warning; 130/130 tests; `ConfigLoadTests` อ่านตารางจริงผ่าน generated reader (จำนวนแถว, lookup, `GetText` ไทย/อังกฤษ, footprint) |
| 4 | ไม่มี stub ที่คืนค่าคงที่แทน config จริง | **PASS** | `LubanGameConfig.Load` throw `ConfigLoadException` (code: `config.table_missing` / `config.table_unreadable` / `footprint.*`) ไม่มี fallback; เทสต์ครอบทางล้มเหลว 4 กรณี (ตารางหาย, ไบต์ไม่ใช่ตาราง, ตารางสั้นเกิน, hash ว่าง) และ hash ใน metadata ต้องตรงกับ `.bytes` จริง (เทสต์ `Config_hash_matches_the_metadata_the_generator_wrote`) |
| — | งานตามข้อ 1–9 ของ prompt | **PASS** | toolchain ล็อก+ตรวจจริง, `data/` 12 ตาราง, stable id + reference validation, tools 4 ตัว, generated ทั้งสองที่, `IGameConfig`, config hash, `App.cs` ที่เดียว, fixture ข้อมูลผิด 12 ชุด |
| — | "ต้องตรวจ" 8 ข้อ | **PASS** | id ซ้ำ, reference หาย, รูปทรงว่าง, ช่องซ้ำ, cooldown ผิด, น้ำหนักร้านใช้ไม่ได้, recipe ข้ามระดับ, wave อ้างศัตรูที่ไม่มี, ราคา ≤ 0 — ทุกข้อมีทั้งเทสต์ unit และ (ยกเว้นกรณีที่เป็น schema ล้วน) fixture |
| — | บังคับ layer ด้วย asmdef (กติกาถาวรข้อ 3) | **PASS** | `ArchitectureTests` 8 เทสต์: เมทริกซ์ asmdef/csproj, ชั้น Generated, ห้าม Domain/Application/Simulation อ้าง generated, banner `<auto-generated>` |

**สรุป:** Stage 02 ปิดครบทุก Acceptance ด้วยหลักฐานที่รันซ้ำได้ และไม่ต้องรอ Unity
งานที่ต้องใช้ Unity จริง (asmdef resolution, โหลด `.bytes` ใน Web) ถูกยกไป Stage 03
อย่างชัดเจน
