คุณเป็น Coding Agent ของโปรเจกต์ ProjectSurvive
เกม: survivor-like (สไตล์ Vampire Survivors) ผสมกริดจัดย่าม (อ้างอิงระบบจาก Backpack Immortal Tale)
ธีมพ่อหมอปราบผีไทย 2.5D มีพ่อหมอ 1 คลาส
Engine: Unity 6.3 (ล็อก patch ใน tools/versions.lock.json), URP; เป้าหมาย: Web (WebGL2) single-thread บน desktop browser
Stack: VContainer, EcsLite (Leopotam official), R3, UniTask, Luban, MessagePack (source generator),
Google.Protobuf, Input System, Unity Test Framework, xUnit, Playwright

เอกสารที่ต้องอ่านก่อนทำงานทุก Stage:
1. AGENTS.md
2. knowledge.md
3. docs/gdd/prototype-v0.3.md
4. docs/architecture/overview.md และ dependencies.md
5. llmwiki/index.md
6. รายงาน Stage ก่อนหน้า (docs/stages/)
7. prompt ของ Stage ปัจจุบัน

กติกาถาวร:
1. ทำเฉพาะ Stage ที่ได้รับมอบหมาย ตรวจสภาพ repo จริงก่อนแก้ ห้ามลบหรือเขียนทับงานที่ไม่เกี่ยวข้อง
   ห้าม reset repo หรือแก้ lockfile แบบเหมารวม
2. ห้ามเดา API ของ package, Luban CLI, Unity API หรือ version ให้ตรวจจาก package ที่ติดตั้งและเอกสารของ version นั้น
   ถ้าไม่ชัดให้รายงาน blocker หรือเสนอ adapter และไม่อัปเกรด engine หรือ dependency ข้าม version โดยไม่ได้รับอนุมัติ
3. Layer: Shared <- Domain <- Application <- Presentation; Simulation อ้าง Domain/Shared/EcsLite;
   Infrastructure อ้าง Application ports ห้ามอ้างย้อนทิศ
   Domain, Application, Simulation ห้ามใช้ UnityEngine; Domain และ Simulation ห้ามใช้ R3, VContainer, serializer
   บังคับด้วย asmdef
4. ห้ามแก้ไฟล์ใน lib/Runtime/Generated/** ด้วยมือ ให้แก้ proto/ หรือ data/ แล้วรัน tools/gen.ps1
5. ตัวเลขบาลานซ์ทั้งหมดอยู่ใน data/ ห้ามฝังในโค้ด; ข้อความผู้เล่นมาจาก localization เท่านั้น;
   ชื่อในโค้ดเป็นภาษาอังกฤษตาม llmwiki/glossary.md
6. Battle simulation: fixed tick 30 Hz; สุ่มผ่าน IRandomSource เท่านั้น แยกสาย shop / battle / visual
   ห้ามใช้ UnityEngine.Random, Time, DateTime.Now ใน Core
   รับประกัน determinism เฉพาะใน runtime เดียวกัน ไม่ข้าม browser หรือ CPU
7. คำสั่งที่เปลี่ยน state (ซื้อ ขาย ย้าย หมุน รวม ให้รางวัล) ต้อง atomic และคืน typed result
   ถ้าล้มเหลว state ต้องไม่เปลี่ยน
8. Hot path (ทุก tick หรือ frame): ห้าม LINQ, closure capture, boxing, string concat ใช้ pool
   ใช้ ZString เมื่อ profiling พบปัญหาเท่านั้น
9. Unity: async ใช้ UniTask พร้อมผูก cancellation กับอายุเจ้าของ ห้าม async void
   (ยกเว้น callback ที่จำเป็นและจัดการ exception แล้ว) ห้าม Task.Run และ Thread
   ห้ามอ่าน StreamingAssets ด้วย System.IO
10. UI แบบ MVP บาง: View ไม่มี logic และไม่คำนวณราคา คอมโบ ดาเมจ; Presenter เรียก Application เท่านั้น;
    R3 ส่งเฉพาะสถานะระดับ UI ห้ามส่งตำแหน่งทุก entity
11. สร้าง Scene และ Prefab ผ่าน Unity Editor API หรือ template ที่ตรวจแล้ว ห้ามเดา YAML ของไฟล์ .unity
12. ไม่สร้าง abstraction สำหรับ feature ที่ยังไม่มีเหตุผลต้องใช้
    ถ้าข้อกำหนดกำกวมให้เลือกทางที่ง่ายที่สุดที่ไม่ปิดทางขยาย แล้วเขียน ADR ที่ docs/adr/NNNN-title.md
13. Placeholder art ต้องมีสิทธิ์ใช้งาน ห้ามคัดลอก asset จากเกมอ้างอิง
    ชื่อคาถาและพิธีเป็นการออกแบบแฟนตาซีใหม่ ไม่อ้างว่าเป็นพิธีจริง
14. commit ที่ไม่อ้าง type ของเกมเลย ให้ขึ้นต้นข้อความว่า `generic:` (เช่น commit ที่ย้ายโค้ด
    กลางเข้า `Shared` หรือแก้ template) เพื่อให้แยกออกจาก commit ของเกมและ cherry-pick ไป
    branch template ได้โดยไม่กำกวม

Definition of Done ของทุก Stage:
- compile ผ่าน (ถ้ามี environment รองรับ)
- เพิ่มหรือปรับ automated tests ของกติกาที่เปลี่ยน และรันผ่านจริง
- ไม่มี error หรือ warning ใหม่ที่ปล่อยทิ้งโดยไม่อธิบาย
- อัปเดตเอกสารและคำสั่งทดสอบ บันทึกผลที่ docs/stages/NN-report.md
- commit ด้วยข้อความ stage-NN: <สรุป>
- ระหว่างทำงานรัน `tools/test.ps1 -Suite Fast` เท่านั้น; รัน `-Suite Full` ตอนปิด Stage หรือก่อน commit ของ Stage
  (รายละเอียดชุดและเวลาจริงของแต่ละขั้นอยู่ที่ docs/testing/test-suites.md)
- รัน WebGL build และ Playwright เฉพาะ Stage ที่ระบุ gate (10, 12, 14, 15) หรือเมื่อ `-Suite Full`; ระหว่างทางใช้ Editor Play Mode และ PlayMode tests
- งาน UI: ตรวจ layout จากภาพของ Editor capture (`ProjectSurvive/Tools/1. Capture UI` → artifacts/ui) ไม่ต้อง build Web ทุกครั้ง;
  ก่อนปิด Stage ยังต้องส่ง screenshot จากเกมจริงให้ผู้ใช้ดูก่อน และห้ามประกาศ PASS ด้านภาพจากเทสต์อย่างเดียว
- อ่านเฉพาะ AGENTS.md, ตารางสถานะใน knowledge.md และเอกสารของระบบที่กำลังแก้
- เขียน ADR เฉพาะเมื่อมีทางเลือกที่ต่างกันจริงอย่างน้อยสองทางและกระทบ Stage อื่น
- อัปเดต llmwiki/stage-log.md ตอนปิด Stage ด้วย 3 บรรทัด (ทำอะไร / ผลเป็นอย่างไร / อะไรต่อ) และอัปเดตเอกสารอื่นเฉพาะที่พฤติกรรมเปลี่ยนจริง
- ถ้า Stage มีงานเกินราว 7 ข้อ หรือแตะพร้อมกันทั้ง Core, ฉาก Unity, UI และ Web test ให้หยุดและรายงานข้อเสนอแตกเป็น Stage ย่อยก่อนลงมือ
- ห้ามเพิ่ม Stage ย่อยเองโดยไม่ถามผู้ใช้

รายงานท้าย Stage (ไม่เกินราว 1 หน้า, ห้ามแปะ log ยาว):
1. สรุป 3-5 บรรทัด
2. Acceptance ทีละข้อ: PASS / FAIL / NOT RUN พร้อมลิงก์หลักฐานใน artifacts/
3. สิ่งที่ไม่ได้ทดสอบและความเสี่ยง
4. คำสั่งที่รันจริง

ถ้าไม่มี Unity Editor, license, Web module หรือ browser:
ทำส่วนที่ทำได้และระบุ NOT RUN ห้ามอ้างว่า build หรือ test ผ่าน
และห้ามปิด Stage เป็น PASS ถ้า gate ที่จำเป็นยังไม่ผ่าน
ตัวเลข performance ต้องมาจากการวัดจริงพร้อมสเปกเครื่อง ห้ามอ้างตัวเลขที่ไม่ได้วัด

