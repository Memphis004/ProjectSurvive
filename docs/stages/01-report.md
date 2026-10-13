# Stage 01 — Core Library และ dotnet harness: รายงาน

- **วันที่:** 2026-10-13
- **ขอบเขต:** local UPM package `lib/` + asmdef 6 ชั้น, vendor EcsLite (Leopotam),
  dotnet solution (`Core.Tests` xUnit + `Sim` console) บน source ชุดเดียวกับ lib,
  Shared/Infrastructure primitives, `tools/test.ps1`, ADR 0004
- **สถานะรวม:** Acceptance ทั้ง 3 ข้อ **PASS** — `dotnet test` ผ่าน 57/57, determinism
  และการแยกสายสุ่มมีหลักฐานจริง, Core ไม่อ้าง UnityEngine
  (ข้อที่ต้องใช้ Unity จริงยัง **NOT RUN** → Stage 03)

---

## 1. สิ่งที่ทำสำเร็จ

1. **`lib/` เป็น local UPM package จริง** (`com.projectsurvive.core`) พร้อม `package.json`
2. **asmdef แยก 6 ชั้น** ตามเมทริกซ์ใน `docs/architecture/overview.md`
   (`noEngineReferences: true` ทุกไฟล์):

   | asmdef | references |
   | --- | --- |
   | `ProjectSurvive.Core.Shared` | — |
   | `ProjectSurvive.Core.Domain` | Shared |
   | `ProjectSurvive.Core.Application` | Shared, Domain |
   | `ProjectSurvive.Core.Simulation` | Shared, Domain, `Leopotam.EcsLite` |
   | `ProjectSurvive.Core.Infrastructure` | Shared, Domain, Application |
   | `ProjectSurvive.Core.Contracts` | — (ว่างโดยตั้งใจ จนถึง Stage 02) |

3. **vendor EcsLite** ลง `lib/third_party/ecslite` ที่ revision
   `d46f3b209ea163810823aeea7d2610d38ad6cdf8` (version string `2026.4.25`, 2026-04-25)
   - คัดลอก **verbatim** (ตรวจแล้วด้วย `cmp`: LICENSE.md, README.md, package.json,
     asmdef, `src/*.cs` ทั้ง 5 ไฟล์ ตรงกับ upstream ทุกไบต์)
   - `LICENSE.md` (MIT-Red) อยู่ในโฟลเดอร์, จด revision + วิธีอัปเดตใน `VENDOR.md`,
     และบันทึกใน `tools/versions.lock.json`
   - asmdef คงค่า upstream `noEngineReferences: false` **โดยเจตนา** เพราะบล็อก
     `#if ENABLE_IL2CPP` ใช้ `Unity.IL2CPP.CompilerServices` — เป็นข้อยกเว้นเดียว
     และบังคับด้วยเทสต์
4. **`lib/Tests~/` เป็นโซลูชัน dotnet ที่ compile source ชุดเดียวกับ lib**
   - `ProjectSurvive.Core.sln` = 7 library project (6 ชั้น + EcsLite) + `Core.Tests`
     (xUnit) + `Sim` (console)
   - แต่ละ layer csproj glob `lib/Runtime/<Layer>/**/*.cs` ตัวเดียว → **ไฟล์ที่ compile
     คือไฟล์ที่ asmdef ครอบจริง ๆ** ไม่มีรายการไฟล์ให้ดูแลซ้ำ
   - บังคับ layer ด้วย `ProjectReference` → อ้างผิดทิศ **compile ไม่ผ่าน** (ไม่ใช่แค่
     เทสต์จับ) เลือกวิธีนี้และบันทึกใน **ADR 0004**
   - library ทั้งหมด target `netstandard2.1` + `LangVersion 9.0` เพื่อเลียนแบบ
     compiler surface ของ Unity 6.3 (โค้ดที่ใช้ API ใหม่กว่าจะพังใน harness ก่อน)
   - `Directory.Build.props` มี target `VerifyLayerSources` กัน glob ที่ไม่ match อะไร
     (จะได้ไม่ build แอสเซมบลีเปล่าเงียบ ๆ); layer ที่ว่างโดยตั้งใจใช้ `AllowEmptyLayer`
5. **Shared primitives** (`lib/Runtime/Shared`)
   - `IRandomSource` (port เดียวที่ Core ใช้สุ่ม) + `RandomSourceExtensions`
     (`NextInt` แบบ Lemire ไม่มี bias, `NextFloat`)
   - `Xoshiro128` = xoshiro128++ แบบ 32-bit ล้วน, seed ผ่าน splitmix32
   - `RandomStreams` + `RandomStreamKind` = สาย **Shop / Battle / Visual**
     derive จาก run seed เดียวแบบเป็นฟังก์ชันบริสุทธิ์ของ `(seed, kind)`
   - `IClock` (port), `FixedTicker` 30 Hz (accumulator + clamp กัน spiral of death
     + นับ `DroppedTicks`), `Result` / `Result<T>` / `Error` / `ErrorKind` (typed,
     ไม่มีข้อความผู้เล่น — `Error.Code` เป็น identifier ให้ UI ไป map localization)
6. **Infrastructure:** `SystemClock` (`Stopwatch` แบบ monotonic) เป็น implementation
   เดียวของ `IClock`
7. **`tools/test.ps1`** — build → `dotnet test` → ตรวจ determinism ของ `Sim` ระดับ
   process พร้อม exit code `0/1/2` แบบเดียวกับ `doctor.ps1` และเขียน artifact ลง
   `artifacts/dotnet/`
8. **เทสต์ 57 ตัว** (FixedTicker 13, Xoshiro128 12, Result 11, RandomStreams 8,
   Architecture guard 8, Infrastructure 3, EcsLite smoke 2)
9. **`tools/reference/xoshiro128_reference.py`** — implementation อ้างอิงอิสระที่ใช้
   สร้าง/ตรวจ golden vector (กันการ pin ค่าจากโค้ดที่กำลังถูกทดสอบ)
10. **เอกสาร:** ADR 0004, `llmwiki/gotchas/{determinism-scope,ecslite-pool-refs}.md`,
    อัปเดต `knowledge.md`, `docs/architecture/{overview,dependencies}.md`,
    `llmwiki/{index,glossary}.md`, `tools/versions.lock.json`

---

## 2. ไฟล์สำคัญที่เปลี่ยน / เพิ่ม

**เพิ่มใหม่ (lib):**
`lib/package.json`,
`lib/Runtime/Shared/{ProjectSurvive.Core.Shared.asmdef,IRandomSource.cs,Xoshiro128.cs,SplitMix32.cs,RandomStreams.cs,RandomStreamKind.cs,IClock.cs,FixedTicker.cs,Result.cs,ResultOfT.cs,Error.cs,ErrorKind.cs}`,
`lib/Runtime/{Domain,Application,Simulation,Contracts}/ProjectSurvive.Core.*.asmdef`,
`lib/Runtime/Infrastructure/{ProjectSurvive.Core.Infrastructure.asmdef,SystemClock.cs}`

**เพิ่มใหม่ (vendor):**
`lib/third_party/ecslite/{VENDOR.md,LICENSE.md,README.md,package.json,Leopotam.EcsLite.asmdef,src/×5}`
(ลบ `.gitkeep` ของโฟลเดอร์นี้ เพราะมีเนื้อหาจริงแล้ว)

**เพิ่มใหม่ (harness):**
`lib/Tests~/Directory.Build.props`, `lib/Tests~/ProjectSurvive.Core.sln`,
`lib/Tests~/Layers/×7.csproj`, `lib/Tests~/Core.Tests/ProjectSurvive.Core.Tests.csproj`,
`lib/Tests~/Core.Tests/{Xoshiro128Tests,RandomStreamsTests,FixedTickerTests,ResultTests,InfrastructureTests,ArchitectureTests,EcsLiteSmokeTests}.cs`,
`lib/Tests~/Sim/ProjectSurvive.Sim.csproj`, `lib/Tests~/Sim/Program.cs`

**เพิ่มใหม่ (tools/docs):**
`tools/test.ps1`, `tools/reference/xoshiro128_reference.py`,
`docs/adr/0004-dotnet-layer-enforcement.md`, `docs/stages/01-report.md`,
`llmwiki/gotchas/determinism-scope.md`, `llmwiki/gotchas/ecslite-pool-refs.md`

**แก้ไข:** `tools/versions.lock.json` (pin EcsLite + เพิ่ม test SDK/runner),
`knowledge.md`, `docs/architecture/overview.md`, `docs/architecture/dependencies.md`,
`llmwiki/index.md`, `llmwiki/glossary.md`

**ไม่แตะ:** `AGENTS.md`, `tools/doctor.ps1`, ไฟล์ GDD, ADR 0001–0003, `lib/Runtime/Generated/**`

---

## 3. คำสั่งที่รันจริง

```bash
# 1. vendor EcsLite (ตรวจก่อนคัดลอก)
git clone https://github.com/Leopotam/ecslite /tmp/ecslite-vendor
git -C /tmp/ecslite-vendor rev-parse HEAD        # d46f3b209ea163810823aeea7d2610d38ad6cdf8
git -C /tmp/ecslite-vendor log -1 --date=iso --format='%H %ad %s'
cp .../LICENSE.md .../README.md .../package.json .../Leopotam.EcsLite.asmdef lib/third_party/ecslite/
cp .../src/*.cs lib/third_party/ecslite/src/
cmp lib/third_party/ecslite/<f> /tmp/ecslite-vendor/<f>   # ตรวจ byte-identical ทุกไฟล์

# 2. golden vector จาก implementation อ้างอิงอิสระ (Python)
python tools/reference/xoshiro128_reference.py

# 3. solution + build + test
dotnet new sln -n ProjectSurvive.Core     # ใน lib/Tests~
dotnet sln ProjectSurvive.Core.sln add Layers/*.csproj Core.Tests/*.csproj Sim/*.csproj
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -v minimal
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" -v minimal --nologo

# 4. Sim (deterministic headless)
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 12345
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 12345 --quiet  # ×2
dotnet run --project "lib/Tests~/Sim/ProjectSurvive.Sim.csproj" -- --seed 999   --quiet

# 5. harness (ทางผ่าน + ทางล้มเหลว + Release)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Configuration Release
#   negative test: ใส่เทสต์ที่ fail ชั่วคราว → ต้องได้ exit 1 (ลบไฟล์ทิ้งหลังรัน)

# 6. ตรวจ acceptance ข้อ "Core ไม่อ้าง UnityEngine" แบบอ่านตรง ๆ
grep -rn "UnityEngine" lib/Runtime/
find lib -name "*.asmdef" -exec grep -o '"noEngineReferences": [a-z]*' {} \;
```

---

## 4. ผลและที่เก็บ log / artifact

| การรัน | ผล | exit code |
| --- | --- | --- |
| `dotnet build` (Debug) | 0 error, **0 warning** | 0 |
| `dotnet test` (Debug) | **Passed 57 / Failed 0** | 0 |
| `dotnet test` (Release) | Passed 57 / Failed 0 (ยืนยันว่าเทสต์ไม่พึ่ง DEBUG-only check) | 0 |
| `tools/test.ps1` | PASS 7 / FAIL 0 / WARN 0 / SKIP 0 | **0** |
| `tools/test.ps1 -Configuration Release` | PASS 7 / FAIL 0 | **0** |
| `tools/test.ps1` + เทสต์ที่ fail (negative) | FAIL 1 → `dotnet test` | **1** |
| `dotnet run … Sim --seed 12345` | `ticksProduced=300` จาก 600 เฟรม 60 fps, `droppedTicks=0` | 0 |
| `python tools/reference/xoshiro128_reference.py` | ค่าตรงกับ golden vector และ digest ของ Sim ทุกตัว | 0 |

**หลักฐาน acceptance (ข้อ 2 — seed เดียวกันได้ลำดับเดียวกัน / สายสุ่มไม่กระทบกัน):**

| การตรวจ | ผล |
| --- | --- |
| `Xoshiro128` seed 0 / 12345 / `0xDEADBEEF` เทียบ golden vector จาก Python | ตรงทั้ง 8 ค่า × 3 seed |
| `RandomStreams.DeriveStreamSeed` เทียบ golden vector | ตรง 3 สาย × 2 seed |
| เทสต์ `Same_seed_produces_the_same_sequence` (4096 ค่า × 2 instance) | ผ่าน |
| เทสต์ `Drawing_from_one_stream_does_not_affect_the_others` (ดึง Shop 10,000 ครั้ง แล้วเทียบ Battle/Visual) | ผ่าน |
| `Sim --seed 20261013 --quiet` รันซ้ำ 2 ครั้ง | `determinismDigest=0x841b4e46` เท่ากัน |
| `Sim --seed 20261014 --quiet` | `0x23f674fd` ต่างจาก seed ข้างบน |
| digest แยกสายที่ seed 20261013 | shop `0x2d476321`, battle `0xbeb639e1`, visual `0xe8742eda` (ไม่ซ้ำกัน) |

**Artifact (ไม่ commit, อยู่ใน `artifacts/dotnet/`):**
`test-report.json`, `test-report.txt`, `core-tests.trx`, `test.log`, `build.log`, `sim-full.txt`

---

## 5. สิ่งที่ไม่ได้ทดสอบและเหตุผล

| เรื่อง | เหตุผล | จะทดสอบเมื่อ |
| --- | --- | --- |
| ให้ Unity compile `lib/` จริง (asmdef resolution, `noEngineReferences`) | ยังไม่มี `UnityProject` (Stage 03) | Stage 03 |
| Unity Test Framework (EditMode/PlayMode) | ต้องมีโปรเจกต์ Unity | Stage 03 |
| determinism บน WebGL/wasm (32-bit heap) เทียบ desktop x64 | ยังไม่มี Web build | Stage 03 |
| MessagePack/Protobuf/R3/VContainer บน IL2CPP + AOT | ยังไม่มี Web build และยังไม่ได้ติดตั้ง | Stage 03 |
| save/restore ค่ากลางลำดับของ RNG | `Xoshiro128` ยังไม่เปิด state export (Stage 12) | Stage 12 |
| Firefox / cross-browser | เครื่องนี้ไม่ได้ติดตั้ง | Stage 14 (ถ้าไม่ติดตั้ง = NOT RUN) |
| ตัวเลข performance ทุกตัว | ยังไม่วัด และไม่มี build | Stage 03+ |
| Luban / protoc | ยังไม่ติดตั้ง | Stage 02 |

---

## 6. Blocker และความเสี่ยง

**Blocker:** ไม่มี blocker ที่หยุดงาน Stage 01

**ความเสี่ยง / เรื่องที่ต้องตัดสินใจต่อ**
1. **EcsLite เป็น MIT-Red ไม่ใช่ MIT ธรรมดา** — เพิ่มข้อจำกัด (ห้ามใช้ในทางทหาร,
   ห้ามฝ่ายที่เป็นปฏิปักษ์ต่อชาวรัสเซีย) อยู่เหนือ MIT ธรรมดา ได้คัดลอก license ไว้ครบ
   และบันทึกไว้แล้ว แต่ควรให้เจ้าของโปรเจกต์รับทราบอย่างชัดเจน (ไม่ใช่ blocker ในทาง
   เทคนิค เพราะ vendor source แล้ว)
2. **EcsLite หยุดพัฒนาแล้ว (feature-frozen)** — ถ้าเจอบั๊กต้อง patch เองหรือเปลี่ยน
   library ต้อง pin revision ต่อไป
3. **`Xoshiro128` ยังไม่มี state export/import** — checkpoint ที่บันทึกกลางลำดับจะ
   restore ไม่ได้ (ปัจจุบันเก็บได้แค่ seed) ต้องแก้ตอน Stage 12
4. **Unity ยังไม่เคย compile โค้ดชุดนี้เลย** — harness เลียนแบบ C# 9 /
   netstandard2.1 แล้ว แต่ไม่ได้แปลว่า asmdef และ Unity จะถูกใจทั้งหมด
5. **`.editorconfig` ตั้ง `csharp_style_namespace_declarations = file_scoped:warning`**
   แต่ Stage 01 ล็อก `LangVersion 9.0` (file-scoped namespace คือ C# 10) — เป็นเพียง
   suggestion ของ IDE ซึ่งจะไม่เสนอเมื่อ language version ไม่รองรับ จึงไม่ทำให้ warning
   โผล่ แต่ควรปรับข้อความใน `.editorconfig` ให้ตรงเมื่อมีโอกาส (ยังไม่แก้ใน Stage นี้
   เพื่อไม่แตะไฟล์ config ของ Stage 00 โดยไม่จำเป็น)
6. **harness ต้องมี .NET SDK ≥ 8** — ถ้าเครื่องเหลือแค่ 6.0.428 จะ build ไม่ได้
   (test.ps1 ตรวจและ FAIL พร้อมข้อความชัดเจนแล้ว) แต่ CI Stage 14 ต้องล็อกไว้

---

## 7. Acceptance แต่ละข้อ

| Acceptance | ผล | หลักฐาน |
| --- | --- | --- |
| `dotnet test` ผ่าน | **PASS** | 57/57 (Debug) และ 57/57 (Release); `artifacts/dotnet/core-tests.trx`, `test.log` |
| เทสต์ seed เดียวกันได้ลำดับเดียวกัน | **PASS** | golden vector จาก Python ตรงทุกค่า + `Sim --quiet` ซ้ำสองครั้งได้ digest เดียวกัน |
| สายสุ่มต่างกันไม่กระทบกัน | **PASS** | เทสต์ดึง Shop 10,000 ครั้งแล้ว Battle/Visual ไม่ขยับ + digest แยกสายไม่ซ้ำ |
| `lib/` เป็น UPM package + asmdef แยก 6 ชั้น (`noEngineReferences: true`) | **PASS** | `lib/package.json`, `find lib -name *.asmdef` = 7 ไฟล์ (6 ชั้น + vendor) |
| vendor EcsLite + LICENSE + จด revision | **PASS** | `cmp` ตรงกับ upstream ทุกไฟล์, `VENDOR.md`, `tools/versions.lock.json`, เทสต์ `Vendored_ecslite_revision_is_pinned_and_recorded_in_the_lock_file` |
| dotnet solution compile source ชุดเดียวกับ lib + เลือกวิธีบังคับ layer + เขียน ADR | **PASS** | `lib/Tests~/Layers/*.csproj` (Compile glob + ProjectReference), ADR 0004, guard tests 8 ตัว |
| Shared/Infrastructure: `IRandomSource` + `Xoshiro128` (seed ได้ แยกสาย), `IClock`, `FixedTicker` 30 Hz, `Result`/`Error` typed | **PASS** | โค้ด + 44 เทสต์ใน 4 คลาส (`Xoshiro128Tests`, `RandomStreamsTests`, `FixedTickerTests`, `ResultTests`) + `InfrastructureTests` |
| `tools/test.ps1` ส่วน dotnet | **PASS** | PASS 7/0, exit 0 ทางผ่าน และ exit 1 ทางล้มเหลว (พิสูจน์ด้วย negative test) |
| **Core ไม่อ้าง UnityEngine** | **PASS** (สำหรับสิ่งที่รันได้จริง) | (ก) asmdef ทุกชั้นของ `lib/Runtime` เป็น `noEngineReferences: true`; (ข) เทสต์โหลดแอสเซมบลีที่ build แล้วทั้ง 7 ตัว แล้วยืนยันว่าไม่มี reference ใดขึ้นต้นด้วย `UnityEngine`/`UnityEditor`; (ค) เทสต์สแกน source ของ `lib/Runtime` (ตัดคอมเมนต์ก่อนสแกน) ไม่พบ `UnityEngine` — ข้อความเดียวที่ grep เจอคือ doc comment ใน `IRandomSource.cs` ที่เขียนกฎนี้ไว้; (ง) `#include`-style ยกเว้นเดียวคือ **vendored** EcsLite ซึ่งบังคับด้วยเทสต์ให้ `UnityEngine` ทุกจุดอยู่ใน `#if` |
| ~~Unity ตรวจ asmdef/noEngineReferences ด้วยตัว Unity เอง~~ | **NOT RUN** | ไม่มี UnityProject จนถึง Stage 03 — ห้ามนับเป็น PASS |

**สรุป: Stage 01 ผ่าน Acceptance ทั้ง 3 ข้อของ prompt และผ่าน Definition of Done
ในส่วนที่รันได้** โดยยัง **ไม่ปิด gate** ของงานที่ต้องใช้ Unity จริง (Stage 03)
