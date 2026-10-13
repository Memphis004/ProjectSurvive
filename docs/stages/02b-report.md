# Stage 02b — Battle Contract v1: รายงาน

- **วันที่:** 2026-10-14
- **ขอบเขต:** สัญญาการต่อสู้ v1 (`lib/Runtime/Domain/Combat/Battle/`), `BattleEventRing`,
  `BattleReplay`, `FakeBattleSession` (`lib/Runtime/Application/Battle/Fake/`),
  contract test suite `BattleSessionContractTests<TFactory>`, architecture guard,
  เอกสาร `llmwiki/systems/battle-contract.md` + ADR 0007 + ปรับ DoD ของ Stage
  05/06/07/08/10/11/12
- **สถานะรวม:** Acceptance ที่ตรวจได้โดยไม่ต้องเปิด Unity **PASS** ทั้งข้อ — ข้อที่ต้อง
  เปิด Unity คือ **NOT RUN** (ไม่มี Editor/license ในสโคปของ Stage นี้)

---

## 1. สิ่งที่ทำสำเร็จ

1. **สัญญา v1 อยู่ที่ `Domain`** (`ProjectSurvive.Core.Domain.Combat.Battle`) เพราะเป็นชั้น
   เดียวที่ทั้ง Application (SceneFlow/Run เรียกใช้) และ Simulation (EcsLite implement)
   อ้างได้ — ไม่ได้วางใน `Contracts` (สงวนไว้สำหรับ Save/Proto DTO) และไม่อ้าง
   EcsLite / UnityEngine / R3 / VContainer / serializer เลย (`IRandomSource` อยู่ใน
   `Shared` อยู่แล้ว จึงไม่ต้องย้าย)
2. **ชนิดข้อมูลครบตามข้อ 1–14** — `BattleConstants` (TickRate=30, MaxStepsPerFrame=4,
   InputAxisScale=1000, EventRingCapacity=4096, MaxRenderEntities=1024),
   `BattlePhase` (Running=0, Won=2, Lost=3, Aborted=4 เว้น 1), `BattleEndReason`,
   `PlayerInputFrame`, `BattleCommand`/`BattleCommandKind`/`CommandResult`, `EntityRef`,
   `BattleEventType`/`BattleEventFlags`, `BattleEvent` + `IBattleEventSource`,
   `RenderEntity`, `HudState`, `IBattleFrame` (+ `BattleFrameBuffer`),
   `CombatLoadoutSnapshot`/`PlayerStatBlock`/`WeaponLoadout`/`SummonLoadout`/
   `ConsumableLoadout`, `BattleSetup`, `BattleResult`, `IBattleSession` +
   `IBattleSessionFactory`
3. **ไม่มี level-up / blessing / XP / choice gate** — ตาม GDD v0.3; ค่า `BattlePhase`
   1 ถูกเว้นไว้สำหรับ `AwaitingChoice` ในอนาคตโดยไม่สร้างจริง (AGENTS rule 12)
4. **`BattleEventRing`** — ขนาดคงที่, `Seq` เพิ่มทีละหนึ่งไม่ซ้ำ, หลาย reader ใช้ cursor
   แยกกัน, รายงาน `dropped` เมื่ออ่านไม่ทัน, zero-alloc ทั้ง write และ read
5. **`BattleReplay`** — setup + input ทุก tick + (tick, command) เล่นซ้ำผ่าน factory
   ใดก็ได้แล้วเทียบ `ComputeStateHash`; เป็นส่วนย่อยที่ run-level `ReplayLog` (Stage 06)
   นำไปรวมได้โดยไม่ซ้ำซ้อน
6. **`FakeBattleSession` + `FakeBattleSessionFactory`** —  deterministic ผ่าน
   `IRandomSource` (battle stream = `DeriveStreamSeed(seed, Battle)`), อ่าน duration/
   is_boss จาก floor table, คืน `WinAtTick`/`LoseAtTick`/`TimeoutAtTick`/
   `DummyEntityCount`/`EntityLifetimeTicks`/`DamagePerSecond`, emit
   `WaveStarted`/`EnemySpawned`/`EnemyKilled`/`PlayerDamaged`/`BattleEnded`/
   `TimeWarning`
7. **`BattleSessionContractTests<TFactory>`** (abstract, xUnit) — suite เดียวที่ fake
   และ (Stage 07) EcsLite simulation ต้องผ่านร่วมกัน: step เมื่อไม่ running ไม่เพิ่ม tick,
   Surrender → Aborted/Surrendered, Submit หลังจบ → RejectedWrongPhase, dev command
   เมื่อ cheats ปิด → RejectedNotAllowed, dev god mode arg ผิด → RejectedInvalidArg,
   ชั้นปกติรอดครบ → Won/SurvivedAllWaves, ชั้นบอสหมดเวลา → Lost/Timeout,
   `TryGetResult`/`FinalHp` bounds, replay round-trip hash เท่ากัน, Seq ต่อเนื่อง,
   reader ตกหลัง → `dropped > 0` โดย state ยังถูกต้อง, `Entities` ไม่เกิน cap,
   `FromAxis` normalize/clamp, **Step ไม่ allocate** (`GC.GetAllocatedBytesForCurrentThread`)
8. **Architecture guard** — reflection test บน public type ใน
   `Domain.Combat.Battle` ห้ามอ้าง `UnityEngine`/`UnityEditor`/`Leopotam`/`R3`
9. **เอกสาร** — `llmwiki/systems/battle-contract.md` (ตารางความหมาย DefId/Value ของทุก
   event, อายุ span, กฎ "event ใช้แค่ VFX/เสียง/UI"), ADR 0007, `llmwiki/stage-log.md`,
   ปรับ DoD Stage 05/06/07/08/10/11/12 (ทั้ง `docs/gdd/prototype-v0.3.md` และไฟล์
   ฉบับรวมที่ root ให้ตรงกัน)

---

## 2. ไฟล์สำคัญที่เปลี่ยน / เพิ่ม

**Domain — สัญญา** (`lib/Runtime/Domain/Combat/Battle/`)

| ไฟล์ | เนื้อหา |
| --- | --- |
| `BattleConstants.cs` | ค่าคงที่เชิงโครงสร้าง (ไม่ใช่บาลานซ์) |
| `BattlePhase.cs`, `BattleEndReason.cs` | วงจรและเหตุผลจบชั้น |
| `PlayerInputFrame.cs` | อินพุตจำนวนเต็มต่อ tick + `FromAxis` |
| `BattleCommand.cs`, `BattleCommandKind.cs`, `CommandResult.cs` | คำสั่ง + ผลแบบ typed |
| `EntityRef.cs` | handle แบบ slot+version |
| `BattleEventType.cs`, `BattleEvent.cs` | เหตุการณ์ + `IBattleEventSource` |
| `BattleEventRing.cs` | วงแหวนเหตุการณ์ (ใช้ร่วม fake/simulation) |
| `RenderEntity.cs`, `HudState.cs`, `IBattleFrame.cs`, `BattleFrameBuffer.cs` | มุมมองเฟรม |
| `CombatLoadout.cs` | snapshot + stat/weapon/summon/consumable loadouts |
| `BattleSetup.cs`, `BattleResult.cs`, `IBattleSession.cs` | setup/result/port + factory |
| `BattleReplay.cs` | บันทึก–เล่นซ้ำระดับชั้น |

**Application — fake** (`lib/Runtime/Application/Battle/Fake/`)

- `FakeBattleSession.cs`, `FakeBattleSessionFactory.cs`, `FakeBattleOptions.cs`

**เทสต์** (`lib/Tests~/Core.Tests/Battle/`)

- `BattleSessionContractTests.cs` (abstract suite), `FakeBattleSessionContractTests.cs`
  (bind fake + 2 เคสเฉพาะ), `BattleEventRingTests.cs`, `BattleContractValueTests.cs`,
  `BattleTestFixtures.cs`
- `lib/Tests~/Core.Tests/ArchitectureTests.cs` — เพิ่ม
  `Battle_contract_types_reference_no_engine_or_ecs_types`

**เอกสาร**

- `llmwiki/systems/battle-contract.md` (ใหม่), `llmwiki/stage-log.md` (ใหม่),
  `docs/adr/0007-battle-contract-and-asmdef-placement.md` (ใหม่)
- `llmwiki/index.md`, `llmwiki/glossary.md`, `knowledge.md`, DoD ใน GDD

---

## 3. คำสั่งที่รันจริง

```powershell
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Debug   -v q -nologo
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" -c Debug   --no-build -v q -nologo
dotnet build "lib/Tests~/ProjectSurvive.Core.sln" -c Release -v q -nologo
dotnet test  "lib/Tests~/ProjectSurvive.Core.sln" -c Release --no-build -v q -nologo
```

---

## 4. ผลและที่เก็บ log / artifact

| คำสั่ง | ผลจริง |
| --- | --- |
| `dotnet build` (Debug) | Build succeeded — **0 warning, 0 error** |
| `dotnet test` (Debug) | **Passed! 164 / 164**, 0 failed, 0 skipped |
| `dotnet build` (Release) | Build succeeded — **0 warning, 0 error** |
| `dotnet test` (Release) | **Passed! 164 / 164**, 0 failed, 0 skipped |

เดิม Stage 02 มี 130 เคส; Stage 02b เพิ่ม **34** เคส (contract suite 14, ring 5,
value 11 (มี Theory), fake-specific 2, architecture 1) → **164**.
Zero-alloc test (`GC.GetAllocatedBytesForCurrentThread`) ผ่านทั้ง Debug และ Release
สำหรับทั้ง `BattleEventRing` และ `Step` ของ fake

---

## 5. สิ่งที่ไม่ได้ทดสอบและเหตุผล

1. **คอมไพล์ใน Unity — NOT RUN** — Stage 02b ไม่มี Unity Editor/license ในสโคป
   asmdef ของ `Domain`/`Application` ครอบโฟลเดอร์ใหม่โดยอัตโนมัติ (ไม่มี asmdef ใหม่)
   แต่การ resolve จริงพิสูจน์ที่ Stage 03 เท่านั้น ตามที่ Stage 02 ระบุไว้แล้ว
2. **ไม่มีตัวเลข performance ที่วัดจาก Unity/Web** — การทดสอบ zero-alloc เป็นการวัด
   บน .NET (`GC.GetAllocatedBytesForCurrentThread`) ตามที่ Stage 07 กำหนดไว้ ไม่ใช่
   ตัวเลขในเบราว์เซอร์
3. **ยังไม่มี EcsLite implementation จริง** — Stage นี้ตั้งใจแค่สัญญา; simulation จริง
   และ benchmark ~300 ศัตรู + 500 กระสุน เป็นของ Stage 07
4. **ไม่มี benchmark `ComputeStateHash`** — เป็น dev/desync tool ไม่ใช่ hot path
5. **ยังไม่ทดสอบข้าม runtime** — determinism ของ fake ยืนยันเฉพาะใน runtime เดียวกัน
   (เทียบ hash ของสอง session ใน process เดียว) ตามขอบเขต AGENTS rule 6

---

## 6. Blocker และความเสี่ยง

- **blocker:** ไม่มี — Stage นี้ไม่ต้องใช้ Unity license, editor หรือ browser
- **ความเสี่ยงที่รับไว้:**
  1. **สัญญาเป็นสิ่งที่สองที่ต้องคอยซิงก์กับ simulation** — บรรเทาด้วย abstract
     contract suite ที่ Stage 07 จะ bind เข้ากับ EcsLite factory
  2. **`BattlePhase` เว้นค่า 1 ไว้** — ถ้าอนาคตไม่ทำ `AwaitingChoice` ค่าจะว่างถาวร
     (ยอมรับได้; บันทึกใน ADR 0007)
  3. **`BattleEvent.DefId` เป็น `int`** — mapping จาก string id ของ config เป็นตัวเลข
     ยังไม่มีจริง; fake ใช้ -1 และ Simulation (Stage 07) จะกำหนดกติกา mapping
  4. **DoD ของ Stage 05–12 ถูกแก้ในเอกสาร** — ถ้าทีมมี "รายการแพตช์ที่แนบ" ฉบับอื่น
     ให้เทียบอีกครั้ง; ที่นี่เพิ่มเฉพาะพันธะที่สัญญา v1 บังคับ

---

## 7. Acceptance แต่ละข้อ

| # | Acceptance (Stage 02b) | ผล | หลักฐาน |
| --- | --- | --- | --- |
| 1 | `dotnet test` ผ่าน | **PASS** | 164/164 Debug และ Release, 0 warning |
| 2 | contract suite ผ่านกับ Fake ทุกข้อ | **PASS** | `FakeBattleSessionContractTests` ผ่านครบ (14 เคสใน base + 2 เคสเฉพาะ) |
| 3 | compile ใน Unity ได้โดยไม่มี warning ใหม่ | **NOT RUN** | ไม่มี Unity Editor/license ในสโคป Stage นี้ |
| 4 | architecture test ผ่าน | **PASS** | `Battle_contract_types_reference_no_engine_or_ecs_types` + suite เดิมทั้งชุดผ่าน |
| 5 | เพิ่ม/ปรับ automated test ของกติกาที่เปลี่ยน | **PASS** | +34 เคส (`Battle/` 4 ไฟล์ + ArchitectureTests 1 เคส) |
| 6 | เอกสาร battle-contract + ADR + stage-log | **PASS** | `llmwiki/systems/battle-contract.md`, ADR 0007, `llmwiki/stage-log.md` |
| 7 | ปรับ DoD Stage 05/06/07/08/10/11/12 | **PASS** | เพิ่มพันธะสัญญา v1 ใน `docs/gdd/prototype-v0.3.md` (และไฟล์ฉบับรวมที่ root ให้ตรงกัน) |

**สรุป:** Stage 02b ปิดทุก Acceptance ที่พิสูจน์ได้โดยไม่ต้องเปิด Unity (ด้วยหลักฐานที่
รันซ้ำได้) ส่วนข้อที่ต้องใช้ Unity ถูกทำเครื่องหมาย **NOT RUN** อย่างชัดเจน
