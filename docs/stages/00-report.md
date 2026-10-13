# Stage 00 — Discovery และ Baseline: รายงาน

- **วันที่:** 2026-10-13
- **ขอบเขต:** Discovery + โครง repo + เอกสาร + doctor + dependency matrix
- **สถานะรวม:** เสร็จตาม Acceptance ทั้ง 4 ข้อ (ยังไม่เขียนระบบเกม)

---

## 1. สิ่งที่ทำสำเร็จ

1. **Discovery** ตรวจสภาพ repo จริง: มีเพียง `AGENTS.md` (Master Prompt ครบ),
   `knowledge.md` (ว่าง), ไฟล์ GDD ฉบับรวมที่ root และ `.agents/` (MCP + skills)
   เป็น git repo หรือไม่ → **ยังไม่เป็น** (`fatal: not a git repository`) จึง
   `git init` ใหม่ ไม่มีการลบหรือเขียนทับงานเดิม
2. **ตรวจสภาพเครื่อง** และบันทึกผลจริง (ดู §3, §4):
   - OS Windows 11 `10.0.26300` x64
   - Git `2.55.0.windows.3`, Git LFS `3.7.1`
   - Windows PowerShell `5.1.26100.9444` และ PowerShell `7` (preview)
   - .NET SDK `6.0.428`, `9.0.311`
   - Node.js `v25.6.1`, npm `11.9.0`
   - **Unity `6000.3.9f1`** (revision `7a9955a4f2fa`), **Unity Personal**
   - **WebGL Build Support present** (`Data/PlaybackEngines/WebGLSupport`)
   - **สิทธิ์รัน batchmode: ผ่าน** — `-batchmode -nographics -quit` → exit `0`
   - Chrome `154.0.8037.98`, Edge `145.0.3800.70`, **ไม่มี Firefox**
   - ไม่พบข้อมูลลับใน log ที่บันทึก (เก็บเฉพาะชื่อ product ของ license)
3. **โครง repo** ตามหัวข้อโครงสร้างใน GDD ครบทุกโฟลเดอร์ พร้อม `.gitkeep`
   ในโฟลเดอร์ว่าง
4. **ไฟล์ config ระดับ root**: `.gitignore` (Unity + dotnet + `artifacts/` +
   `lib/Tests~/**/bin,obj`), `.gitattributes` (Git LFS สำหรับ
   `png/psd/fbx/wav/ogg` + binary อื่น), `.editorconfig`
5. **เอกสาร**: `knowledge.md`, `docs/gdd/prototype-v0.3.md` (GDD ฉบับรวม),
   `docs/architecture/overview.md`, `docs/architecture/dependencies.md`,
   `llmwiki/index.md`, `llmwiki/glossary.md`, ADR 0001–0003,
   `UnityProject/SETUP.md`
6. **Dependency matrix** แยก Verified / Proposed / Blocked / Unverified
   พร้อม source ที่ตรวจซ้ำได้, version/revision, license, dependency ต่อเนื่อง
   และความเสี่ยง IL2CPP/Web
7. **`tools/versions.lock.json`** ล็อก Unity patch + toolchain + host machine +
   browser + package ทั้งหมดพร้อม `status`
8. **`tools/doctor.ps1`** ตรวจสภาพเครื่อง/งาน 16 รายการ คืน **exit code ที่
   เชื่อถือได้** (0 = required ผ่านทั้งหมด, 1 = required ล้มเหลว, 2 = doctor เองพัง)
   พร้อม `-Json`, `-Unity` (probe batchmode), `-NoWrite`
9. **MVP แบบบางของโปรเจกต์เอง** — ตัดสินและบันทึกเป็น ADR 0002 (ยังไม่มี
   repository ของ `UI.MVP Lite` จึง **Blocked**)
10. **Baseline เครื่อง/browser** สำหรับ performance — ADR 0003
    (i5-14400F / 32 GB / RTX 5060 Ti, 1920×1080, Chrome เป็น primary)
11. **ไม่เขียนระบบเกม** — ทุกโฟลเดอร์ของ `lib/`, `data/`, `proto/`,
    `tests/web/`, `UnityProject/` มีเพียง `.gitkeep`

---

## 2. ไฟล์สำคัญที่เปลี่ยน / เพิ่ม

**เพิ่มใหม่ (root):** `.gitignore`, `.gitattributes`, `.editorconfig`

**แก้/เติม:** `knowledge.md` (เดิมว่าง → ภาพรวม สถานะ คำสั่ง ลิงก์)

**เพิ่มใหม่ (docs):**
`docs/gdd/prototype-v0.3.md`, `docs/architecture/overview.md`,
`docs/architecture/dependencies.md`, `docs/stages/00-report.md`,
`docs/adr/0001-repository-and-artifact-conventions.md`,
`docs/adr/0002-thin-mvp-ui-approach.md`,
`docs/adr/0003-performance-baseline-machine-and-browser.md`

**เพิ่มใหม่ (อื่น):** `llmwiki/index.md`, `llmwiki/glossary.md`,
`tools/versions.lock.json`, `tools/doctor.ps1`, `UnityProject/SETUP.md`,
`.gitkeep` หลายตำแหน่ง

**ไม่แตะ:** `AGENTS.md` (มี Master Prompt ครบแล้ว), ไฟล์ GDD ที่ root,
`.agents/**` (ตรวจอย่างเดียว ไม่แก้)

---

## 3. คำสั่งที่รันจริง

```bash
# Discovery
git status / git log                     # -> not a git repository
ls -la ; cat AGENTS.md ; cat knowledge.md

# Environment
git --version                            # 2.55.0.windows.3
git lfs version                          # git-lfs/3.7.1
powershell -Command '$PSVersionTable.PSVersion.ToString()'
dotnet --list-sdks ; dotnet --list-runtimes
node --version ; npm --version
(Get-Item '...\6000.3.9f1\Editor\Unity.exe').VersionInfo
ls '.../Editor/Data/PlaybackEngines/WebGLSupport'
Get-CimInstance Win32_Processor / Win32_ComputerSystem / Win32_VideoController
(Get-Item chrome.exe).VersionInfo.ProductVersion

# บันทึก: Unity batchmode ครั้งแรก (ตรวจสิทธิ์ + license)
"<Unity.exe>" -batchmode -nographics -quit -logFile /tmp/ps_disco/unity_batch.log
                                         # EXIT=0, "Product: Unity Personal"

# ตรวจ version/license ของ dependency จาก registry (authoritative)
#   api.nuget.org/v3-flatcontainer/{leopotam.ecslite,messagepack,google.protobuf,xunit}
#   api.openupm.com/packages/{jp.hadashikick.vcontainer,com.cysharp.r3,com.cysharp.unitask}
#   packages.unity.com/{com.unity.inputsystem,com.unity.render-pipelines.universal,com.unity.test-framework}
#   api.github.com/repos/{Leopotam/ecslite,hadashiA/VContainer,Cysharp/R3,Cysharp/UniTask,
#     MessagePack-CSharp/MessagePack-CSharp,focus-creative-games/luban,protocolbuffers/protobuf,xunit/xunit}/license
#   api.github.com/repos/focus-creative-games/luban/releases/latest  # v5.1.0

# ตรวจ doctor
powershell -NoProfile -ExecutionPolicy Bypass -File tools/doctor.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/doctor.ps1 -Unity -Json
powershell ... -File tools/doctor.ps1 -UnityPath "C:\does-not-exist\Unity.exe" -NoWrite -Json
```

---

## 4. ผลและที่เก็บ log / artifact

| การรัน | ผล | exit code |
| --- | --- | --- |
| `doctor.ps1` (เร็ว) | PASS 13 / FAIL 0 / WARN 1 (Firefox) / SKIP 2 | **0** |
| `doctor.ps1 -Unity` | PASS 14 / FAIL 0 / WARN 1 / SKIP 1 | **0** |
| `doctor.ps1 -UnityPath <bogus>` (negative test) | FAIL 1 (required) | **1** |
| `Unity.exe -batchmode -nographics -quit` | license product: Unity Personal | **0** |

Artifact (ไม่ commit, อยู่ใน `artifacts/doctor/`):
- `artifacts/doctor/doctor-report.json` — ผลแบบ machine-readable
- `artifacts/doctor/doctor-report.txt` — สรุปแบบอ่านง่าย
- `artifacts/doctor/unity-batchmode.log` — log ของ Unity (เก็บเฉพาะชื่อ
  license product ลง report; ไม่ echo ค่า license/serial)

ยืนยันว่า **exit code เชื่อถือได้ทั้งเส้นทางสำเร็จและล้มเหลว** (tests ด้านบน)

---

## 5. สิ่งที่ไม่ได้ทดสอบและเหตุผล

| เรื่อง | เหตุผล | จะทดสอบเมื่อ |
| --- | --- | --- |
| การผูก asmdef / layer ใน Unity | ยังไม่สร้าง UnityProject (Stage 03) | Stage 01 (dotnet) และ 03 (Unity) |
| `dotnet test` ของ Core | ยังไม่มีโค้ด Core (Stage 01) | Stage 01 |
| Luban / protoc CLI จริง | ยังไม่ติดตั้ง (Stage 02) | Stage 02 |
| Web build + R3/MessagePack/Protobuf บน IL2CPP | ยังไม่มีโปรเจกต์ Unity | Stage 03 |
| Playwright | ยังไม่ติดตั้ง และไม่มี Web build | Stage 03/14 |
| Firefox | เครื่องนี้ไม่ได้ติดตั้ง | ก่อน Stage 14 (ถ้าไม่ติดตั้ง = NOT RUN) |
| IndexedDB flush บน Unity 6.3 | ต้องตรวจเอกสาร 6.3 | Stage 12 |
| ตัวเลข performance ทุกตัว | ยังไม่วัด และไม่มี build | Stage 03+ |

---

## 6. Blocker และความเสี่ยง

**Blocker**
1. **`UI.MVP Lite` ไม่มี repository** → ใช้ thin MVP ของโปรเจกต์เอง (ADR 0002)
   ต้องได้ URL เพื่อ adopt จริง
2. **Firefox ไม่ได้ติดตั้ง** → cross-browser run จะเป็น NOT RUN จนกว่าจะติดตั้ง

**ความเสี่ยง**
- **`EcsLite` revision ยังไม่ pin** — ยืนยันว่า repo มีจริงและ license MIT
  (MIT-Red) แต่ยังไม่ล็อก commit → Stage 01 ต้อง pin
- **version ของ VContainer / R3 / UniTask / MessagePack / Luban "ที่ใช้กับ
  Unity 6.3" ยังเป็น Proposed** — ตรวจแค่ว่ามีบน registry ไม่ได้พิสูจน์ว่า
  เข้ากับ 6000.3 (Stage 01/03)
- **MessagePack Unity UPM variant** ยังไม่รู้ชื่อ/version ของ package ฝั่ง Unity
- **ความเสี่ยง IL2CPP/Web สูง** สำหรับ MessagePack (source generator เท่านั้น)
  และปานกลางสำหรับ R3 / VContainer / UniTask (single-thread) → Stage 03
- **มี GDD สองชุด** (root + `docs/gdd/`) — เลือกให้ `docs/gdd/` เป็น canonical
  (ADR 0001) แต่ต้องระวัง drift
- **Chrome เป็นแบบ user-install** ไม่ใช่ system-wide — script ต้องค้นหลาย path
  (doctor ทำแล้ว)
- Node.js `v25` ใหม่กว่า LTS มาก — Playwright อาจต้อง pin version ตอน Stage 03

---

## 7. Acceptance แต่ละข้อ

| Acceptance | ผล | หลักฐาน |
| --- | --- | --- |
| ระบุ Unity patch ได้ หรือมี blocker ชัดเจน | **PASS** | `6000.3.9f1` (revision `7a9955a4f2fa`), WebGL module present, batchmode exit 0 |
| doctor ให้ผลสำเร็จหรือล้มเหลวด้วย exit code ที่เชื่อถือได้ | **PASS** | สำเร็จ exit 0, negative test exit 1 (รายงานใน §4) |
| ยังไม่เขียนระบบเกม | **PASS** | ไม่มีไฟล์ `.cs` / asmdef / proto / data table — มีแต่ `.gitkeep` |
| มีรายการ dependency ที่ยังไม่ยืนยัน | **PASS** | `docs/architecture/dependencies.md` §2.3 (Blocked/Unverified) + `tools/versions.lock.json` (`status: proposed/blocked/unverified`) |

**สรุป: Stage 00 ผ่าน Acceptance ทั้ง 4 ข้อ** — โดยยังไม่ปิด gate ของ Stage
ถัดไป (Core lib, data pipeline, Web build ยังไม่เริ่ม)
