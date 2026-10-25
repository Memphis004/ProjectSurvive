# ProjectSurvive: GDD v0.3 + Master Prompt + Stage Plan (ฉบับรวม)

Oct 13, 2026 · @Holdur

## ที่มาของฉบับรวม

ฉบับนี้ใช้กฎเกมแบบ Opus เป็นแกน เพราะใกล้ระบบของ [Backpack Immortal Tale](https://store.steampowered.com/app/3067850/Backpack_Immortal_Tale/) ที่สุด แล้วเสริมวินัยด้านการทดสอบ การตรวจสอบ และการตัด scope จาก Astra

| ส่วน | มาจาก | เหตุผล |
| --- | --- | --- |
| ชั้น ระดับความยาก ศาลาพัก ย่ามสองชั้น หีบ น้ำหนัก MP ถวายคืน คืนสภาพ/ล็อก merge 3 ขั้น | Opus | ตรงกับเกมอ้างอิงมากกว่า |
| Core เป็น pure C# รันด้วย `dotnet test` และมี bot จำลองการเล่น | Opus | agent วนแก้ได้เร็ว และปรับบาลานซ์ได้ก่อนมีภาพ |
| ทดสอบภาษาไทยใน UI ตั้งแต่ Stage 03 | Opus | Astra เช็กที่ Stage 10 ซึ่งช้าเกินไป |
| ตัดเหลือ 5 ชั้น (ปกติ 4 + บอส), ผี 3 ชนิด + บอส, ไอเทมประมาณ 14 | Astra (ปรับ) | พิสูจน์วงจรให้ได้ก่อนขยาย |
| Build Web และเริ่ม Playwright ตั้งแต่ Stage 03 | Astra | เจอปัญหา IL2CPP/AOT เร็ว |
| Master Prompt: ห้ามเดา API, รายงาน PASS / FAIL / NOT RUN | Astra | กัน agent อ้างว่าผ่านโดยไม่ได้รัน |
| คำสั่ง atomic, typed result, RNG แยกสาย, settlement ID | Astra | กันของและเงินหาย กันรางวัลซ้ำ |
| asmdef แยกตามชั้นใน lib | Astra | ให้ compiler บังคับกฎ layer |
| Save ด้วย MessagePack, Protobuf ไว้ใช้กับ contract ภายนอก | Astra และ GDD เดิม | Opus ใช้ Protobuf กับ save ซึ่งต่างจาก GDD เดิม |
| งบ performance เริ่มที่ \~300 ศัตรู | Astra | ตัวเลข 1,000 ศัตรูของ Opus ยังไม่เคยวัด จึงเก็บเป็นเป้าระยะถัดไป |

**ยืนยันกับหน้า Steam แล้ว:** เกมอ้างอิงเป็น backpack management ผสม roguelike ไต่ชั้น มุม 2.5D top-down มีไอเทมหลายประเภท (法宝 / 功法 / 材料 / 防具 / 策令) มีบอสจำนวนมาก มีโหมดท้าทายภาพสะท้อน build ของผู้เล่นอื่น และมีพื้นที่สำรวจเพิ่ม หน้า Steam มีเฉพาะภาษาจีน

**ยังไม่ยืนยัน:** รายละเอียดหน้าศาลาพัก (ร้าน รีเฟรช หีบ 2/10 น้ำหนัก ถวายคืน คืนสภาพ/ล็อก) มาจากการเทียบภาพของ Opus ซึ่งหน้า Steam ไม่ได้ยืนยัน ให้ถือเป็นข้อเสนอและเทียบกับภาพที่คุณมีก่อนล็อก

ค่าตัวเลขทั้งหมดในเอกสารนี้เป็นค่าตั้งต้นสำหรับทดลอง ต้องอยู่ใน `data/` ไม่ฝังในโค้ด

## GDD v0.3: ขอบเขตและวงจรหลัก

Prototype พิสูจน์วงจรเดียว: จัดย่ามที่ศาลาพัก → สู้แล้วเห็นผลในสนาม → ได้เบี้ยกลับมาปรับย่าม → อยากลองชั้นต่อไป กติกาสำคัญคือย่ามแก้ไขได้เฉพาะที่ศาลาพัก ระหว่างสู้ไม่เปิดย่ามและไม่หยุดเกมมาจัดของ

| หัวข้อ | ขอบเขต Prototype |
| --- | --- |
| ตัวละคร | พ่อหมอ 1 คลาส |
| แพลตฟอร์ม | Desktop browser ผ่าน Unity Web (WebGL2, single-thread) |
| Engine | Unity 6.3 ล็อก patch version ใน Stage 00 |
| มุมมอง | 2.5D: สไปรต์ 2D บนฉาก 3D low-poly กล้องเฉียงประมาณ 50° (เลือก orthographic หรือ perspective ใน Stage 08) |
| การควบคุม | WASD หรือลูกศรเพื่อเดิน เมาส์สำหรับร้านและย่าม คาถายิงอัตโนมัติ |
| ด่าน | ป่าช้าวัดร้าง 5 ชั้น (ปกติ 4 + บอสชั้น 5) |
| ความยาวรอบ | เป้า 8–12 นาที รวมเวลาจัดย่าม |
| ผี | วิญญาณเร่ร่อน กระสือ นางตานี (Elite) และบอสปอบ |
| ไอเทม | ประมาณ 14 ชิ้น มี 3 ขั้น |
| บันทึก | Settings และ checkpoint ที่ศาลาพัก |
| ยังไม่ทำ | EXP/พร ตอนเลเวลอัป, meta-progression ถาวร, หลายคลาส, หลายด่านและระดับความยาก, โหมดท้าทายภาพสะท้อน build |

&#91;embedded content: วงจรหลักของรอบเล่น · 5 ชั้น, 2 จุดตัดสินใจ\]

ชั้น 1–4 วนกลับศาลาพักหลังผ่านด่าน ชั้น 5 เป็นบอส แล้วรอบจบที่ Result ซึ่งสรุปผลและกลับ Title

## GDD v0.3: ศาลาพัก ย่าม คอมโบ และร้านค้า

ศาลาพักเป็นฉากเดียวที่แก้ย่ามได้ และกติกาทุกข้อในส่วนนี้ต้องอยู่ใน Core ไม่ใช่ใน View ฉากเป็น 2.5D ขนาดเล็ก แต่ UI ใช้ได้ทันที ไม่บังคับให้เดินไปมาระหว่างร้านกับย่าม

### องค์ประกอบของศาลาพัก

| องค์ประกอบ | พฤติกรรม |
| --- | --- |
| แผงวัตถุมงคลยายจันทร์ | 4 ช่องสินค้า ราคาเป็นเบี้ย ใช้คำว่า เช่า แทน ซื้อ เช่าแล้วช่องแสดงว่าเช่าแล้วและไม่เติมสินค้าให้ฟรี เปิดร้านซ้ำสินค้าเดิม |
| เสี่ยงเซียมซี (สุ่มสินค้าใหม่) | ครั้งแรก 5 เบี้ย เพิ่มครั้งละ 2 ภายในศาลาเดียวกัน ล็อกสินค้าที่อยากเก็บไว้ก่อนสุ่มได้ |
| จัดย่าม | เปิดหน้า Board และหีบ |
| หีบไม้สัก | 10 ช่อง พักของที่ยังไม่ใช้ ของในหีบไม่ให้โบนัสและไม่โจมตี ตามไปทุกศาลา |
| ถวายคืนศาล | ลากของมาวางเพื่อรับเบี้ยคืน 50% ของราคามาตรฐาน ปัดลง |
| คืนสภาพ / ล็อกย่าม | คืนย่ามไปเหมือนตอนเข้าศาลา / กันลากของผิด |
| น้ำหนักย่าม | ยิ่งหนักยิ่งเดินช้า ค่าลดสูงสุดอยู่ใน constant |
| ออกเดินทาง | กดไม่ได้ถ้าไม่มีอาวุธอาคมบนช่องใช้งานอย่างน้อย 1 ชิ้น และต้องแสดงเหตุผล |
| ฟื้นฟู | หลังผ่านชั้น ฟื้น HP 15% ของ Max HP ไม่เกิน Max HP |

### ย่ามสองชั้น

ต้องวางย่ามลงบน Board ก่อน แล้วจึงวางของบนช่องที่ย่ามครอบอยู่

| ชั้น | หน้าที่ |
| --- | --- |
| Board | ขอบเขตสูงสุดที่จัดวางได้ ค่าตั้งต้น 9×7 |
| ย่าม (Bag piece) | ชิ้นส่วนที่เปิดช่องใช้งานบน Board เริ่มด้วยย่ามผ้าดิบ 3×3 |
| ไอเทม | อาวุธ เครื่องราง ตำรา ของเซ่น ภูติ ต้องอยู่บนช่องใช้งาน |

- ย่ามห้ามทับกัน ไอเทมห้ามทับกัน
- ไอเทมพาดข้ามหลายย่ามได้ ถ้าทุกช่องที่กินพื้นที่มีย่ามครอบ
- หมุนทีละ 90° ด้วยคลิกขวาหรือกด R ระหว่างลาก
- ย้ายหรือถอดย่ามได้ก็ต่อเมื่อไม่ทำให้ไอเทมใดหลุดพื้นที่ใช้งาน ไม่เช่นนั้นปฏิเสธทั้งคำสั่ง ผู้เล่นย้ายของไปหีบก่อนได้ (ต่างจาก Opus ที่ให้ของย้ายตามย่าม เลือกแบบนี้เพราะทดสอบง่ายกว่า)
- รวมไอเทม: วางของที่ id และ tier เดียวกันทับกัน ได้ tier ถัดไป สูงสุด 3 (ธรรมดา → ปลุกเสก → มหาเวท) รูปทรงคงเดิม
- ของที่ซื้อเข้าหีบก่อนเสมอ แล้วผู้เล่นค่อยย้ายขึ้นย่าม

### กติกาคอมโบ

- ติดกัน = บน ล่าง ซ้าย ขวา ไม่รวมแนวทแยง
- สองชิ้นที่ติดกันหลายช่องนับความสัมพันธ์ครั้งเดียว
- โบนัสเปอร์เซ็นต์ชนิดเดียวกันบวกกันก่อน
- cooldown มีค่าต่ำสุดใน constant กันการร่ายทุกเฟรม
- ไม่มีคอมโบวนซ้ำ เช่น A เพิ่ม B แล้ว B เพิ่ม A ไม่สิ้นสุด
- คำนวณใหม่เมื่อชุดอุปกรณ์เปลี่ยนเท่านั้น ผลลัพธ์คือ CombatLoadoutSnapshot ที่ส่งให้ Battle ตอนออกเดินทาง Battle ไม่อ่านตำแหน่ง UI และไม่คำนวณคอมโบเอง

กฎตั้งต้น 5 ข้อ กำหนดด้วย tag ของไอเทมในตาราง synergy ตัวเลขเป็นค่าเริ่มทดลองจาก Opus:

| เงื่อนไข | ผล |
| --- | --- |
| ตะกรุดวางติดผ้ายันต์ | cooldown ของยันต์ −15% |
| ตำราพระเวทอยู่แถวเดียวกับอาวุธอาคม | ดาเมจ +20% ซ้อนได้สูงสุด 2 ชั้น |
| ของสายคุณไสยวางติดพระเครื่อง | ทั้งสองชิ้นเสื่อมพลัง −30% จึงต้องวางให้ห่างกัน |
| น้ำแดงวางติดภูติรับใช้ | ภูติโจมตีเร็วขึ้น 25% |
| ย่ามชนิดพิเศษ | ไอเทมที่อยู่ในพื้นที่ของย่ามนั้นได้บัฟตามชนิดย่าม |

### ร้านและเศรษฐกิจ

| รายการ | ค่าตั้งต้น |
| --- | --- |
| เบี้ยตั้งต้น | 15 |
| ช่องสินค้า | 4 ต่อการเข้าศาลา สร้าง stock ครั้งเดียว |
| Refresh | 5 เบี้ย เพิ่มครั้งละ 2 รีเซ็ตเมื่อเข้าศาลาใหม่ |
| ถวายคืน | 50% ของราคามาตรฐาน ปัดลง |
| ฟื้น HP หลังผ่านชั้น | 15% ของ Max HP |
| หีบ | 10 ช่อง |
| Board | 9×7 |
| เงื่อนไขออกเดินทาง | มีอาวุธอาคมบนช่องใช้งาน ≥ 1 ชิ้น |
| ของเริ่มต้น | ย่ามผ้าดิบ 3×3 + ยันต์เพลิงกาลี |

- เบี้ยมาจากการเก็บในสนามและรางวัลจบชั้น จำนวนอยู่ใน `economy.csv`
- ซื้อเข้าหีบ ถ้าหีบเต็มห้ามหักเงิน
- RNG ของร้านแยกจาก RNG ของการต่อสู้ เพื่อให้จำนวนการโจมตีไม่เปลี่ยนสินค้ารอบถัดไปโดยไม่ตั้งใจ

## GDD v0.3: พ่อหมอ ไอเทม ผีไทย และโครงด่าน

Prototype มีพ่อหมอคนเดียว ไอเทมประมาณ 14 ชิ้น ผี 3 ชนิดกับบอสปอบ และด่านเดียว 5 ชั้น ส่วนที่เหลืออยู่ในคิวหลัง Prototype และขยายผ่าน data ได้โดยไม่ต้องแก้กฎ

### พ่อหมอ

| ค่า | ค่าตั้งต้น | หมายเหตุ |
| --- | --- | --- |
| HP | 100 | คงเหลือข้ามชั้น ฟื้น 15% หลังผ่านชั้น |
| พลังอาคม (MP) | 50 | ร่ายแต่ละครั้งใช้ MP ถ้าไม่พอข้ามรอบนั้น เต็มทุกครั้งที่เข้าชั้น (สมมติฐาน ต้องยืนยัน) |
| สมาธิ | 5 MP ต่อวินาที | อัตราฟื้น MP |
| ความเร็ว | 4.0 | ลดตามน้ำหนักย่าม |
| รัศมีดูดของ | 1.5 |  |

### ไอเทม (ประมาณ 14 ชิ้น มี 3 ขั้น)

| หมวด | จำนวน | ตัวอย่างชื่อ | พฤติกรรม |
| --- | --- | --- | --- |
| ย่าม | 3 | ย่ามผ้าดิบ 3×3, ถุงผ้ายันต์ 1×3, ตะกร้าหวายรูปตัว L | เปิดช่องใช้งานบน Board |
| อาวุธอาคม | 4 | ยันต์เพลิงกาลี, ตะกรุดโทน, มีดหมอ, สายสิญจน์ | ร่ายอัตโนมัติตาม cooldown ใช้ MP: กระสุน ทะลุ ประชิด รอบตัว |
| เครื่องราง | 3 | พระเครื่อง, ผ้ายันต์คาดเอว, เขี้ยวเสือ | บัฟ passive |
| ตำราคาถา | 1 | ตำราพระเวท 2×2 | ขยายพลังของอาวุธที่อยู่แถวเดียวกัน |
| ของเซ่น | 2 | น้ำแดง, ไข่ต้ม | ใช้ได้จำกัดครั้ง ฟื้นฟูหรือเพิ่มเบี้ย |
| ภูติรับใช้ | 1 | กุมารทอง 2×1 | ติดตามและช่วยสู้ |

- tag และธาตุของแต่ละชิ้นกำหนดใน Stage 04 Prototype ใช้ธาตุไฟ (burn DOT) และคุณไสย (curse)
- ต้องมีอย่างน้อย 1 ชิ้นสายคุณไสย และ 1 รูปทรงที่ไม่สมมาตร เพื่อทดสอบกฎเสื่อมพลังกับการหมุน
- ชื่อคาถา ลายสัญลักษณ์ และเรื่องเล่าเป็นการออกแบบแฟนตาซีใหม่ที่ได้แรงบันดาลใจจากคติพื้นบ้าน ไม่อ้างว่าเป็นพิธีหรือคาถาจริง

### ผีไทยใน Prototype

| ผี | บทบาท | กลไก |
| --- | --- | --- |
| วิญญาณเร่ร่อน | ตัวถม | พุ่งตรงเข้าหาผู้เล่น |
| กระสือ | เร็ว ตีแล้วหนี | ลอยพุ่งเข้ามาดูดเลือดแล้วถอยออก |
| นางตานี | Elite คุมพื้นที่ | ยืนนิ่งใต้ต้นกล้วย ปล่อยเถาวัลย์ทำให้ช้าเป็นวงกว้าง มีวงเตือนบนพื้น |
| ปอบ | บอสชั้น 5 สองเฟส | กินศพผีตัวอื่นเพื่อฟื้น HP สิงจนปุ่มเดินสลับทิศชั่วคราว เฟสสองเริ่มที่ HP ตามค่าใน data |

การโจมตีแรงทุกแบบต้องมี telegraph ที่อ่านออก และผีแต่ละชนิดต้องแยกออกจากกันด้วยพฤติกรรม ไม่ใช่แค่หน้าตา

### โครงด่าน: ป่าช้าวัดร้าง

| ชั้น | เนื้อหา |
| --- | --- |
| 1 | วิญญาณเร่ร่อน |
| 2 | วิญญาณเร่ร่อน และกระสือ |
| 3 | Elite นางตานี พร้อมฝูง |
| 4 | ผสมทุกแบบ หนาแน่นขึ้น |
| 5 | บอสปอบ |

ชั้นปกติใช้เวลา 60 วินาที ชั้นบอสชนะเมื่อฆ่าปอบ ถ้าเกิน 90 วินาทีถือว่าแพ้ใน Prototype หลอด timeline ล่างจอแสดง wave พร้อมไอคอน elite และบอส ค่าเวลาทั้งหมดอยู่ใน `floor.csv` และ `wave.csv` ขยายเป็น 8 ชั้นตามแผนเดิมของ Opus ได้ด้วยการเพิ่มแถวข้อมูล

### คิวหลัง Prototype

| หัวข้อ | รายละเอียด |
| --- | --- |
| กระหัง | บินมีเกราะ ปัดกระสุนด้วยกระด้งจากด้านหน้า ต้องตีด้านข้างหรือด้านหลัง |
| ผีนางรำ | โจมตีตามจังหวะ มีท่ารำเตือนก่อนฟาดเป็นแพทเทิร์น |
| นางนาค (แม่นาค) | บอส 3 เฟส: สงบ → เกรี้ยวกราด (แขนยืดฟาดไกล) → คลั่ง (เรียกผีทั้งฝูง) |
| ระดับความยาก | ขั้นต้น 8 ชั้น ขั้นกลาง 11 ขั้นสูง 15 ขั้นครู 20 (บอส 1 / 1 / 2 / 3) ปลดล็อกตามลำดับ |
| EXP และพร | เลเวลอัปเลือกพร 1 ใน 3 เป็นบัฟสถานะเล็ก ไม่หยุดเกมมาจัดย่าม |
| Meta-progression | บารมี ปลดล็อกไอเทม ด่าน และระดับความยาก |
| ธาตุเพิ่ม | น้ำมนต์ เหล็กไหล แสงธรรม |
| ด่านใหม่และคลาสอื่น | สวนกล้วย เรือนไม้เก่า และคลาสจาก GDD เดิม (จอมขลังสายน้ำมนต์ สัปเหร่อ หมอดู) |
| ภาพสะท้อน build ผู้เล่นอื่น | ทำเป็นลำดับท้ายสุด |

ลำดับที่แนะนำหลัง Prototype: ปรับร้านและคอมโบ → เพิ่มศัตรูและชั้น → ระดับความยากและเส้นทาง → progression ถาวร → คลาสใหม่

### ภาพและเสียง

- สไปรต์ 2D วาดมือบนฉาก diorama 3D low-poly โทนมืดหม่น แสงเทียนและแสงจันทร์ แสงเรืองเขียวของวิญญาณ
- เสียงระนาดและพิณผสม ambient สยองขวัญ และเสียงสวดคาถา
- ใช้ placeholder ที่มีสิทธิ์ใช้งานเท่านั้น ห้ามคัดลอก asset จากเกมอ้างอิง และบันทึก attribution

## Tech stack

ใช้แกนเดิมจาก GDD v0.1 แต่ให้แต่ละเทคโนโลยีมีขอบเขตชัด และเลื่อนของที่ยังไม่จำเป็นออกไป เพื่อไม่ให้หลายระบบทำงานซ้ำกัน

### ใช้ตั้งแต่เริ่ม

| เทคโนโลยี | หน้าที่และข้อกำหนด |
| --- | --- |
| Unity 6.3 (ล็อก patch) + URP | งานภาพ 2.5D ใช้ renderer แบบเบาและวัดผลจริง URP ไม่ได้เร็วกว่า Built-in โดยอัตโนมัติ |
| VContainer | DI และอายุของ App / Run / Scene ห้าม inject ทุก entity |
| EcsLite (Leopotam official) | simulation ของศัตรู กระสุน คาถา pickup ล็อก revision ที่ตรวจแล้ว ไม่ใช้แทนทุกระบบของเกม |
| R3 | สถานะระดับ UI เช่น HP เงิน ย่าม ภาษา ห้ามส่งตำแหน่ง entity ผ่าน stream |
| UniTask | โหลด Scene ข้อมูล บันทึก ผูก cancellation กับอายุเจ้าของ โค้ดใหม่ใช้ UniTask ไม่ต้องรื้อ coroutine เดิมที่ใช้ได้ |
| MVP แบบบาง | View ไม่มี logic ยังระบุ package UI.MVP Lite ไม่ได้ จึงเริ่มด้วยของโปรเจกต์เอง แล้วค่อยเปลี่ยน adapter |
| Luban | CSV → generated reader + binary (.bytes) ผ่าน adapter IGameConfig |
| MessagePack (source generator) | save checkpoint และ snapshot ในหน่วยความจำ (undo, reset) ห้ามใช้ dynamic resolver เพราะ IL2CPP ไม่รองรับ |
| Google.Protobuf | contract ภายนอกเท่านั้น: replay log คำสั่งจาก test harness telemetry ในอนาคต ไม่ใช้เป็นชั้นกลางของ config |
| Input System | WASD ลูกศร เมาส์ |
| Unity Test Framework, xUnit, Playwright | EditMode/PlayMode ใน Unity, xUnit ผ่าน `dotnet test` สำหรับ Core, Playwright กับ Web build จริง |

### เลื่อนไว้ก่อน

| รายการ | เหตุผลและเวลาที่จะเพิ่ม |
| --- | --- |
| VitalRouter | ScenePresenter กับ R3 ทำหน้าที่ command และ event ได้อยู่แล้ว |
| Addressables | ใช้ Resources/Config ใน MVP เพิ่มเมื่อขนาด build หรือการโหลดต้องการจริง (Stage 15) |
| ZString | เพิ่มเมื่อ profiling พบว่าการ format ข้อความเป็นปัญหา ระหว่างนี้ใช้ข้อความที่ cache และ pool |
| Tween (LitMotion) | เลือกตัวเดียวหลัง UI หลักใช้งานได้ |
| Networking, backend, multithreading | อยู่นอก Prototype Web build ต้องไม่พึ่ง multithreading เพราะต้องตั้ง header COOP/COEP ที่ server |

### เส้นแบ่ง serialization

```text
Luban        CSV + Defines → generated reader + .bytes → adapter IGameConfig
MessagePack  RunCheckpointDto, SettingsDto, snapshot ในหน่วยความจำ
Protobuf     app.proto → contract ภายนอก (replay, harness, telemetry)
```

ข้อนี้ต่างจาก Opus ที่ใช้ Protobuf กับ save เลือก MessagePack ตาม GDD เดิมและตาม Astra ต้องทดสอบ package ที่ใช้ร่วมกันบน IL2CPP และ Web จริงตั้งแต่ Stage 03 ถ้าเล่นใน Editor ได้ไม่ถือว่า Web จะผ่าน

### ข้อควรระวังเฉพาะ Web

- GC ของ WebGL เป็น single-thread ต้อง pooling เข้มงวดและเลี่ยง allocation ใน hot path
- ใช้ build แบบ single-thread
- save ของผู้เล่นลง IndexedDB ผ่านวิธีที่ Unity 6.3 รองรับจริง ต้องบังคับ flush และพิสูจน์ด้วย Playwright reload ส่วน cache ของ asset ไม่ใช่กลไกเดียวกับ save
- ตัวเลข FPS ใน Editor ไม่ใช่หลักฐานของ Web ให้ตัดสินด้วยการวัดใน browser จริง
- UI ภาษาไทย: TextMeshPro มีปัญหาตำแหน่งสระบนล่างและวรรณยุกต์ จึงมี spike เทียบ uGUI+TMP กับ UI Toolkit ใน Stage 03

## โครงสร้าง repo และสถาปัตยกรรม

`lib/` เป็น local UPM package ที่เก็บกฎเกมทั้งหมดไว้นอก `UnityProject` และแยก asmdef ตามชั้น agent จึงแก้กฎเกมและรัน `dotnet test` ได้โดยไม่ต้องเปิด Unity

### โครง repo

```text
ProjectSurvive/
├── AGENTS.md                     # Master Prompt
├── knowledge.md                  # ภาพรวมสั้น สถานะ คำสั่ง ลิงก์เอกสาร
├── proto/app.proto               # contract ภายนอก
├── lib/                          # local UPM package: com.projectsurvive.core
│   ├── package.json
│   ├── Runtime/
│   │   ├── Shared/               # asmdef: Id, Result, GridShape, Rotation
│   │   ├── Domain/               # asmdef: Backpack, Items, Shop, Run, Combat
│   │   ├── Application/          # asmdef: Ports, use case Camp/Battle/Result, SceneFlow
│   │   ├── Simulation/           # asmdef: EcsLite world, systems, spatial hash
│   │   ├── Infrastructure/       # asmdef: Xoshiro128 RNG, config adapter
│   │   ├── Contracts/            # Save DTO (MessagePack), External (Protobuf)
│   │   └── Generated/            # Luban + Proto output ห้ามแก้มือ
│   ├── third_party/ecslite/      # vendor (MIT) พร้อม LICENSE
│   └── Tests~/                   # dotnet: Core.Tests (xUnit), Sim (headless bot)
├── data/                         # luban.conf, Defines/, ตาราง .csv, localization/th.csv en.csv
├── docs/                         # gdd/, architecture/, adr/, stages/, testing/, build/
├── llmwiki/                      # index.md, glossary.md, systems/, gotchas/
├── tools/                        # versions.lock.json, doctor, bootstrap, gen, validate-data,
│                                 # verify-generated, test, sim, build-web, serve-web, ci
├── tests/web/                    # Playwright: smoke, persistence, run-loop
├── artifacts/                    # ไม่ commit: log, report, screenshot
└── UnityProject/
    ├── SETUP.md
    ├── Packages/manifest.json    # "com.projectsurvive.core": "file:../../lib" (merge ไม่ทับของเดิม)
    └── Assets/Main/
        ├── Scenes/               # Boot, Title, Camp, Battle, Result
        ├── Resources/Config/     # Luban .bytes (MVP)
        ├── Scripts/              # Infrastructure, Presentation, Editor, Testing
        └── Tests/                # EditMode, PlayMode
```

ตารางใน `data/` ตั้งชื่อว่า `item`, `bag`, `skill`, `synergy`, `merge_recipe`, `enemy`, `floor`, `wave`, `shop_pool`, `economy`, `constant` และ localization รูปทรงของย่ามและไอเทมเก็บเป็น string เช่น `XX.|.XX` (X = ช่องที่มี) แล้ว parse ใน Domain ส่วน Presentation แยกโฟลเดอร์ตาม Scene (Presenter, View, LifetimeScope) เช่น `SceneCamp/Shop`, `SceneCamp/Backpack`, `SceneBattle/HUD`

### ทิศการอ้างอิง

กติกาตัดสินด้วย asmdef ไม่ใช่ข้อตกลงปากเปล่า: Domain ไม่อ้าง UnityEngine, VContainer, R3 หรือ serializer และ View ไม่คำนวณราคา คอมโบ หรือดาเมจ

&#91;embedded content: ทิศการอ้างอิงของแต่ละชั้น · 6 ชั้น\]

ชั้นในกรอบ lib เป็น pure C# ทดสอบด้วย `dotnet test` ได้ ส่วนกรอบ UnityProject มีเฉพาะโค้ดที่ผูกกับ Unity Generated และ Contracts อยู่ใน lib แต่ Domain ไม่อ้างถึง ใช้ผ่าน adapter ใน Infrastructure เท่านั้น

- Application ไม่อ้าง Scene, Prefab หรือ MonoBehaviour
- Simulation ไม่รู้จัก UI หรือ GameObject
- Generated types ไม่แพร่เป็นโมเดลหลัก ผ่าน adapter เช่น `IGameConfig` เท่านั้น
- ใน dotnet ที่ไม่มี asmdef ให้ Stage 01 เลือกวิธีบังคับ (csproj แยกตามชั้น หรือ architecture test) แล้วเขียน ADR
- คำสั่งที่เปลี่ยน state คืนผลลัพธ์แบบ typed: รับคำสั่ง → ตรวจเงื่อนไข → เปลี่ยน state ครบชุด → ส่งเหตุการณ์ เช่น `PurchaseItem`, `SellItem`, `RefreshShop`, `PlaceBag`, `MoveItem`, `RotateItem`, `MergeItems`, `StartFloor` แล้วได้ `WalletChanged`, `InventoryChanged`, `ShopStockChanged`, `LoadoutChanged`, `FloorCompleted`, `RunCompleted`

### อายุข้อมูล 3 ระดับ

| Scope | อายุ | เจ้าของ |
| --- | --- | --- |
| App | จนปิดเกม | Config, Settings, Save repository, การเปลี่ยน Scene |
| Run | ตั้งแต่เริ่มรอบจนออกจาก Result | seed, เงิน, HP, ย่าม, สินค้าร้าน, ชั้นปัจจุบัน |
| Scene | จนออกจาก Scene | View, Presenter, ECS world ของ Battle, pooled visuals |

`RunSession` ต้องไม่ถูกสร้างใหม่ทุกครั้งที่กลับศาลาพัก กลับ Title แล้ว dispose Run scope ออกจาก Battle แล้ว dispose ECS world และ subscription ทั้งหมด

### จากย่ามถึง Battle

ตอนกดออกเดินทาง: ตรวจความถูกต้องของย่าม → รวมค่าสถานะและโบนัส → สร้าง `CombatLoadoutSnapshot` → ส่งเข้า Battle → ล็อกการแก้ย่ามจนกลับศาลาพัก

### Checkpoint (บันทึกเฉพาะที่ศาลาพัก)

- schema version และ config version/hash
- run ID, seed และสถานะ RNG แยกสาย
- ชั้นที่กำลังจะเข้า, HP, เงิน
- ย่ามและตำแหน่งไอเทม, ของในหีบ
- stock ร้าน ช่องที่เช่าแล้ว จำนวน refresh
- settlement ID ล่าสุดที่รับรางวัลแล้ว กันให้รางวัลซ้ำ

ถ้า reload ระหว่าง Battle ให้กลับ checkpoint ก่อนเข้าชั้นและเริ่มชั้นนั้นใหม่ โดยไม่รับเงินหรือรางวัลจาก Battle ที่ยังไม่จบ Prototype เป็น offline จึงไม่อ้างว่ากันการโกงได้

## Master Prompt (AGENTS.md)

วางข้อความด้านล่างเป็น `AGENTS.md` ที่ root ของ repo แล้วส่ง prompt ของแต่ละ Stage ทีละอัน รอให้ผ่าน Acceptance และมีรายงานก่อนไป Stage ถัดไป

```text
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

Definition of Done ของทุก Stage:
- compile ผ่าน (ถ้ามี environment รองรับ)
- เพิ่มหรือปรับ automated tests ของกติกาที่เปลี่ยน และรันผ่านจริง
- ไม่มี error หรือ warning ใหม่ที่ปล่อยทิ้งโดยไม่อธิบาย
- อัปเดตเอกสารและคำสั่งทดสอบ บันทึกผลที่ docs/stages/NN-report.md
- commit ด้วยข้อความ stage-NN: <สรุป>

รายงานท้าย Stage:
1. สิ่งที่ทำสำเร็จ
2. ไฟล์สำคัญที่เปลี่ยน
3. คำสั่งที่รันจริง
4. ผลและที่เก็บ log หรือ artifact
5. สิ่งที่ไม่ได้ทดสอบและเหตุผล
6. blocker และความเสี่ยง
7. Acceptance แต่ละข้อ: PASS / FAIL / NOT RUN

ถ้าไม่มี Unity Editor, license, Web module หรือ browser:
ทำส่วนที่ทำได้และระบุ NOT RUN ห้ามอ้างว่า build หรือ test ผ่าน
และห้ามปิด Stage เป็น PASS ถ้า gate ที่จำเป็นยังไม่ผ่าน
ตัวเลข performance ต้องมาจากการวัดจริงพร้อมสเปกเครื่อง ห้ามอ้างตัวเลขที่ไม่ได้วัด
```

## Stage prompts 00–15

Stage 00–07 ทำได้เกือบทั้งหมดโดยไม่แตะงานภาพ ผ่าน Stage 07 จะมีเกมที่ bot เล่นได้โดยไม่มีกราฟิก ใช้ปรับบาลานซ์ก่อนลงทุนกับ Presentation ส่วน build Web ครั้งแรกเกิดที่ Stage 03 เพื่อเจอปัญหา IL2CPP และ AOT ให้เร็วที่สุด

| Stage | ผลลัพธ์ | Gate หลัก |
| --- | --- | --- |
| 00 | Discovery โครง repo doctor dependency matrix | doctor ทำงาน และแยก Verified / Blocked ได้ |
| 01 | Core lib และ dotnet harness | `dotnet test` ผ่าน RNG แยกสายและ seed ได้ |
| 02 | Data pipeline (Luban, protoc) | gen ซ้ำแล้ว diff ว่าง fixture ผิดต้อง fail |
| 03 | Unity shell, Web build แรก, Thai text spike | Web build โหลด config และผ่าน serialization smoke |
| 04 | ข้อมูล Prototype | gen และ validate ผ่าน |
| 05 | Inventory domain | ≥ 30 เคส และ randomized test 10,000 ครั้ง |
| 06 | Shop, economy, run, scene flow | scripted run 5 ชั้น replay ได้ hash เดิม |
| 07 | Battle core แบบ headless และ bot sim | benchmark ที่ \~300 ศัตรู + 500 กระสุน |
| 08 | Battle presentation 2.5D | เล่นชั้น 1 ใน Web ได้ |
| 09 | Camp scene และ UI จัดย่าม | ซื้อ → วาง → หมุน → รวม → ถวายคืน ผ่าน UI |
| 10 | วงจรเต็ม (vertical slice) | เล่นจบทั้งชนะและแพ้ได้ |
| 11 | AI ผีไทย บอสปอบ และบาลานซ์ | อัตราชนะบอสของ bot 30–60% |
| 12 | Save, resume, settings | reload แล้ว resume ถูกต้องใน Playwright |
| 13 | Content complete และ UX | ผู้เล่นใหม่เริ่มเล่นได้โดยไม่ต้องมีคนอธิบาย |
| 14 | Test harness และ CI | `ci.ps1` ผ่านตั้งแต่ต้นจนจบ |
| 15 | Web release candidate | performance report ตามงบใน GDD |

### Stage 00: Discovery และ Baseline

```text
ทำ Stage 00 เท่านั้น: Discovery และ Baseline

งาน:
1. ตรวจไฟล์ใน repo และงานที่มีอยู่โดยไม่แก้ทับ
2. ตรวจ OS, Git, PowerShell, .NET SDK, Node.js, Unity Editor (patch 6000.3.x ที่มี), Web Build Support
   และสิทธิ์รัน batchmode โดยไม่แสดงข้อมูลลับใน log
3. สร้างโครง repo ตามหัวข้อโครงสร้างใน GDD (ใส่ .gitkeep ในโฟลเดอร์ว่าง), .gitignore (Unity + dotnet +
   artifacts/ + lib/Tests~/**/bin,obj), .gitattributes (Git LFS สำหรับ png/psd/fbx/wav/ogg), .editorconfig
4. สร้าง AGENTS.md (วาง Master Prompt), knowledge.md (ภาพรวมสั้น สถานะ คำสั่ง ลิงก์),
   docs/gdd/prototype-v0.3.md (วาง GDD ฉบับรวม), docs/architecture/overview.md และ dependencies.md
5. llmwiki/index.md และ glossary.md (พ่อหมอ=Mor, ย่าม=Bag, ศาลาพัก=Camp, เบี้ย=Bia, หีบ=Chest,
   กระสือ=Krasue, นางตานี=NangTani, ปอบ=Pob, ...)
6. ทำ dependency matrix: หน้าที่ · source ที่ตรวจสอบได้ · version/revision · license ·
   dependency ต่อเนื่อง · ความเสี่ยง IL2CPP/Web แยกเป็น Proposed / Verified / Blocked
7. สร้าง tools/doctor.ps1 (exit code ที่เชื่อถือได้) และ tools/versions.lock.json
8. ใช้ MVP แบบบางของโปรเจกต์เองจนกว่าจะระบุ package UI.MVP Lite ได้
9. กำหนดเครื่องและ browser สำหรับ performance baseline

Acceptance:
- ระบุ Unity patch ได้ หรือมี blocker ชัดเจน
- doctor ให้ผลสำเร็จหรือล้มเหลวด้วย exit code ที่เชื่อถือได้
- ยังไม่เขียนระบบเกม
- มีรายการ dependency ที่ยังไม่ยืนยัน
```

### Stage 01: Core Library และ dotnet harness

```text
ทำ Stage 01 เท่านั้น: Core Library และ dotnet test harness

งาน:
1. lib/ เป็น local UPM package (com.projectsurvive.core) พร้อม asmdef แยก Shared, Domain, Application,
   Simulation, Infrastructure, Contracts (noEngineReferences: true ยกเว้นที่จำเป็นจริง)
2. vendor EcsLite (Leopotam official) ลง lib/third_party/ecslite พร้อม LICENSE และจดว่าล็อก revision ไหน
3. lib/Tests~/: โซลูชัน dotnet (Core.Tests ด้วย xUnit, Sim เป็น console) ที่ compile source ชุดเดียวกับ lib
   เลือกวิธีบังคับ layer ใน dotnet (csproj แยกตามชั้น หรือ architecture test) แล้วเขียน ADR
4. Shared และ Infrastructure: IRandomSource + Xoshiro128 (seed ได้ แยกสายสุ่ม), IClock, FixedTicker 30 Hz,
   Result/Error แบบ typed
5. tools/test.ps1 ส่วน dotnet

Acceptance:
- dotnet test ผ่าน
- เทสต์ seed เดียวกันได้ลำดับเดียวกัน และสายสุ่มต่างกันไม่กระทบกัน
- Core ไม่อ้าง UnityEngine
```

### Stage 02: Static Data Pipeline

```text
ทำ Stage 02 เท่านั้น: Static Data Pipeline

งาน:
1. ตั้ง Luban และ protoc ตาม version ที่ล็อก (ตรวจ CLI จริง ห้ามเดา syntax) ลงใน tools/
2. data/luban.conf, Defines (enums, beans), __tables__.csv และ schema ขั้นต่ำของตาราง:
   item, bag, skill, synergy, merge_recipe, enemy, floor, wave, shop_pool, economy, constant, localization
3. stable ID และ reference validation
4. tools/gen.ps1 (ทำงานจาก root ไหนก็ได้), validate-data.ps1, verify-generated.ps1
5. generate C# ไปที่ lib/Runtime/Generated/Luban และ binary .bytes ไปที่ Resources/Config และ lib/Tests~/data
6. IGameConfig adapter: Domain ห้ามใช้ generated rows โดยตรง
7. config hash ลง build metadata
8. proto/app.proto ขั้นต่ำ (contract ภายนอก) ไปที่ lib/Runtime/Generated/Proto (App.cs มีที่เดียว)
9. fixture ข้อมูลผิดเพื่อทดสอบ validation

ต้องตรวจ: id ซ้ำ, reference หาย, รูปทรงว่างหรือช่องซ้ำ, cooldown ผิด, น้ำหนักสุ่มร้านรวมใช้ไม่ได้,
recipe ข้ามระดับผิดกฎ, wave อ้างศัตรูที่ไม่มี, ราคา ≤ 0

Acceptance:
- generate ซ้ำจาก input เดิมแล้ว git diff ว่าง
- fixture ที่ผิดต้อง fail
- generated code compile ได้ และเทสต์ Core อ่านตารางตัวอย่างได้
- ไม่มี stub ที่คืนค่าคงที่แทน config จริง
```

### Stage 02b: Battle Contract v1 (แทรกระหว่าง Stage 02 และ 03)
```text
บริบท: Stage 02 Static Data Pipeline เสร็จแล้ว สเตจนี้สร้าง "สัญญา" ระหว่าง Battle กับระบบอื่น + Fake + contract tests
ยังไม่ implement EcsLite. อ่าน AGENTS.md/knowledge.md, llmwiki/index.md, llmwiki/stage-log.md, ADR เรื่อง ECS ก่อน

ตำแหน่งและการอ้างอิง (ตาม asmdef ของ v0.3):
- Interface/struct ทั้งหมด → lib/Runtime/Domain/Combat/Battle/ (namespace ProjectSurvive.Domain.Combat.Battle)
  เหตุผล: Application (SceneFlow) ต้องเรียกใช้ และ Simulation (EcsLite) ต้อง implement ได้ ซึ่งทั้งคู่อ้าง Domain ได้
- ห้ามวางใน Contracts (asmdef นั้นสงวนไว้สำหรับ Save/Proto DTO)
- ห้ามอ้าง EcsLite, UnityEngine, R3, VContainer, UniTask
- IRandomSource ให้ใช้ตัวที่มีอยู่แล้วจาก Stage 01/02 (ถ้าอยู่ผิด layer จน Simulation อ้างไม่ได้ ให้ย้ายไป Shared + เขียน ADR)

ขอบเขตตาม GDD v0.3:
- ไม่มี level-up/blessing/XP ใน prototype → ห้ามสร้าง
- Run 5 ชั้น (ชั้นปกติ 4 + บอสปอบ 1); ชั้นปกติ 60s รอดครบ = ชนะ; ชั้นบอส 90s ต้องฆ่าบอส ถ้าหมดเวลา = แพ้
- ระยะเวลาและเงื่อนไขชนะอ่านจาก Luban (stage/wave/constant) ห้าม hardcode
- HP ต่อเนื่องข้ามชั้น: Battle รับ CurrentHp และคืน FinalHp; การฟื้น 15% หลังผ่านชั้นเป็นหน้าที่ Run domain ไม่ใช่ Battle

ไฟล์ที่ต้องสร้าง (อ้างอิงร่างโค้ดที่แนบ โดยมีการแก้ดังนี้):
1. BattleConstants: TickRate=30, MaxStepsPerFrame=4, InputAxisScale=1000, EventRingCapacity=4096, MaxRenderEntities=1024
   (ตัด MaxBlessingOffer)
2. BattlePhase: Running=0, Won=2, Lost=3, Aborted=4 (เว้นค่า 1 ไว้สำหรับ AwaitingChoice ในอนาคต)
3. BattleEndReason: None, SurvivedAllWaves, BossDefeated, PlayerDied, Timeout, Surrendered
4. PlayerInputFrame (เหมือนร่างเดิม)
5. BattleCommand/BattleCommandKind: Surrender, DevKillAll, DevGodMode(Arg 0/1), DevWinNow, DevLoseNow
   CommandResult: Ok, RejectedWrongPhase, RejectedInvalidArg, RejectedNotAllowed
6. EntityRef (เหมือนเดิม)
7. BattleEventType: WaveStarted, EnemySpawned, EnemyKilled, ProjectileFired, DamageDealt, PlayerDamaged,
   PlayerHealed, StatusApplied, StatusExpired, PickupSpawned, PickupCollected, TelegraphStarted, TelegraphEnded,
   CorpseConsumed (ปอบกินศพ: Value=HP ที่ฟื้น), BossPhaseChanged, TimeWarning, BattleEnded (Value=BattleEndReason)
   BattleEventFlags: None, Critical, Elite, Boss
8. BattleEvent struct + IBattleEventSource.Read(ref cursor, Span, out dropped) (เหมือนเดิม)
9. RenderEntity (เหมือนเดิม), HudState: Hp, MaxHp, Mp, MaxMp, Currency (ใช้ชื่อตาม Luban), Kills,
   ElapsedTicks, FloorDurationTicks, WaveIndex, BossHp, BossMaxHp (ตัด Level/Xp/InputInverted)
10. IBattleFrame: Tick, Hud, Entities (Cur) + PreviousEntities (Prev) — double buffer สลับทุก tick เพื่อ interpolate (ตัด BlessingOffer); อายุ span ใช้ได้จนกว่าจะเรียก Step/Submit ครั้งถัดไป
11. PlayerStatBlock, WeaponLoadout, SummonLoadout, ConsumableLoadout, CombatLoadoutSnapshot
    (ถ้า Domain มีโครงของ CombatLoadoutSnapshot อยู่แล้ว ให้ใช้ของเดิมและปรับให้ตรงสัญญา ห้ามสร้างซ้ำ)
12. BattleSetup: Seed (battle RNG stream), StageId, DifficultyId, FloorIndex(1-based), IsBossFloor,
    CurrentHp, Loadout, DevCheatsAllowed (ตัด PlayerLevel/PlayerXp)
13. BattleResult: Outcome, EndReason, SurvivedTicks, FinalHp, CurrencyEarned, Kills, RemainingConsumables
14. IBattleSession: Phase, Tick, Step(in input), Submit(in cmd), Frame, Events, TryGetResult, ComputeStateHash
    IBattleSessionFactory.Create(BattleSetup) โดย config tables ส่งผ่าน constructor ของ factory

Implementation ใน stage นี้:
A. BattleEventRing (Domain/Combat/Battle): ขนาดคงที่, Seq เพิ่มขึ้นเรื่อยๆ, หลาย reader ใช้ cursor แยกกัน, รายงาน dropped, zero-alloc
B. FakeBattleSession + Factory → lib/Runtime/Application/Battle/Fake/
   options: WinAtTick, LoseAtTick, TimeoutAtTick, DummyEntityCount (เดินเป็นวง), DamagePerSecond
   emit: WaveStarted, EnemySpawned/Killed, PlayerDamaged, BattleEnded; deterministic ผ่าน IRandomSource
C. BattleReplay: setup + input ทุก tick + (tick, command) → เล่นซ้ำผ่าน factory ใดก็ได้แล้วเทียบ ComputeStateHash
   ให้ออกแบบเป็นส่วนย่อยที่ ReplayLog ของ Stage 06 นำไปรวมได้ (ห้ามซ้ำซ้อนกับ ReplayLog)
D. Contract test suite แบบ abstract: BattleSessionContractTests<TFactory> (xUnit) ตรวจ:
   - Step() เมื่อ Phase != Running ไม่เพิ่ม Tick
   - Surrender → Aborted + EndReason=Surrendered; Submit หลังจบ → RejectedWrongPhase
   - Dev command เมื่อ DevCheatsAllowed=false → RejectedNotAllowed
   - ชั้นบอสหมดเวลา → Lost + Timeout; ชั้นปกติรอดครบ → Won + SurvivedAllWaves
   - TryGetResult=false ระหว่าง Running และ true หลังจบ; FinalHp ≤ MaxHp และ ≥ 0
   - replay round-trip ได้ hash เท่ากัน
   - Event Seq ต่อเนื่อง; อ่านไม่ทันแล้ว dropped > 0 โดย state ยังถูกต้อง
   - Entities.Length ≤ MaxRenderEntities; FromAxis normalize/clamp ถูกต้อง
   - Step() ไม่ allocate หลัง warm-up (GC.GetAllocatedBytesForCurrentThread)
E. Architecture test (reflection): public type ใน Domain.Combat.Battle ต้องไม่อ้าง namespace Leopotam/EcsLite/UnityEngine/R3

เอกสาร: llmwiki/systems/battle-contract.md (ตารางความหมาย DefId/Value ของแต่ละ event, อายุ span,
กฎ "event ใช้แค่ VFX/เสียง/UI ห้ามใช้ตัดสิน logic"), ADR "Battle contract v1 + ตำแหน่ง asmdef", อัปเดต stage-log

DoD: dotnet test ผ่าน; contract suite ผ่านกับ Fake ทุกข้อ; compile ใน Unity ได้โดยไม่มี warning ใหม่;
architecture test ผ่าน; อัปเดต DoD ของ Stage 05/06/07/08/10/11/12 ใน docs ตามรายการแพตช์ที่แนบ

```

### Fix
```
[แก้ของเดิม]
ตำแหน่งและการอ้างอิง:
- Interface/struct ทั้งหมด → namespace ProjectSurvive.Core.Domain.Combat.Battle
  (แก้จาก ProjectSurvive.Domain.Combat.Battle เดิม ให้ตรงกับ asmdef "Core" ที่ใช้จริงในโปรเจกต์
  และตรงกับที่ Stage 05 อ้างถึง LoadoutResolver)
- วางไฟล์ในโฟลเดอร์ Domain ที่มีอยู่แล้วภายใต้ lib/ ตาม layout จริงของโปรเจกต์
  (ถ้า root ไม่ใช่ lib/Runtime/Domain/Combat/Battle/ ให้ agent ปรับ path ให้ตรงของจริงแล้วบันทึกใน ADR)

[เพิ่มใหม่ ต่อจากข้อ A. BattleEventRing]
A2. BattleFrameBuffer (Domain/Combat/Battle): concrete implementation ของ IBattleFrame
    - double-buffer ของ RenderEntity[] (Prev/Cur สลับกันทุก tick) ความจุคงที่ MaxRenderEntities
    - เขียนได้ฝั่งเดียว (Simulation/Fake เป็นคนเขียน) อ่านได้หลายฝั่ง (Presentation) โดยไม่ allocate
    - Hud เป็น struct เดี่ยวที่ถูก copy ทับทุก tick (ไม่ใช่ double-buffer เพราะไม่ต้อง interpolate)
    - ทั้ง FakeBattleSession และ EcsBattleSimulation (Stage 07) ต้องใช้ type นี้ตัวเดียวกัน
      ห้าม implement IBattleFrame แยกกันคนละแบบ
```

### Stage 03: Unity Shell, Web Build แรก และ Thai Text Spike

```text
ทำ Stage 03 เท่านั้น: Unity Shell, Web Build แรก และ Thai Text Spike

งาน:
1. สร้าง UnityProject (URP, Input System) เชื่อม lib ผ่าน manifest (merge ไม่ทับ dependency เดิม)
   asmdef: Infrastructure, Presentation, Editor, Tests.EditMode, Tests.PlayMode
2. ติดตั้ง VContainer, UniTask, R3 (+ Unity package), MessagePack, Google.Protobuf ตาม version
   ที่ Stage 00 ตรวจแล้ว บันทึก version ทั้งหมดใน SETUP.md
3. RootLifetimeScope, SceneBoot และ SceneTitle ขั้นต่ำ; โหลด .bytes แล้วสร้าง config ผ่าน IGameConfig ได้ใน Web
4. smoke test: DI resolution, R3 subscription/disposal, UniTask cancellation, MessagePack DTO round-trip
   (source generator) และ Protobuf round-trip ทั้งใน EditMode และใน Web build
5. Editor/WebBuildPipeline + tools/build-web.ps1 (Development) และ tools/serve-web.mjs
6. Playwright เบื้องต้น (tests/web/smoke.spec.ts): เปิด build → รอ ready → ตรวจ console ไม่มี error → ถ่าย screenshot
7. SPIKE ภาษาไทย: แสดง พ่อหมอปราบผีกระสือ ฤๅษี ปั้นน้ำตาล ญี่ปุ่น (ฟอนต์ Noto Sans Thai หรือ Sarabun)
   เทียบ uGUI+TMP กับ UI Toolkit ทั้งใน Editor และใน Web แล้วเลือก UI tech ด้วย ADR
8. สร้าง Scene และ Prefab ผ่าน Editor API เท่านั้น

Acceptance:
- clean clone เปิด Unity แล้ว compile ได้
- EditMode และ PlayMode smoke ผ่าน
- Web build เปิดได้ และผ่าน serialization กับ config smoke
- มี ADR เรื่อง UI ภาษาไทย
- ไม่มี Missing Script หรือ DI ที่ resolve ไม่ได้
```

ทำ Stage 03b เท่านั้น: Web Build Size Baseline

1. เพิ่ม tools/build-web.ps1 -Profile Release (ไม่ใช่ Development) และเก็บ Development ไว้เทียบ
2. ตั้งค่าเพื่อลดขนาด: IL2CPP, Managed Stripping สูงสุดที่ใช้ได้, Strip Engine Code, ปิด debug symbols
   และ exception support เท่าที่ Unity 6.3 อนุญาต (ตรวจเอกสาร ห้ามเดาชื่อ setting), optimize for size
3. ดึง Build Report แล้วทำ docs/build/size-baseline.md:
   ขนาด .wasm .data .framework.js .loader.js ทั้งแบบไม่บีบอัดและบีบอัด,
   assembly และ asset ใหญ่สุด 20 อันดับ, ผลก่อนและหลังการแก้แต่ละครั้ง
4. ทำ A/B: build ที่ถอด package ทีละตัว (MessagePack, Protobuf, R3, VContainer, UniTask)
   เพื่อหาว่าตัวไหนทำ .wasm โตผิดปกติ
5. ห้ามเพิ่ม YooAsset หรือ Addressables ใน Stage นี้

Acceptance:
- มีตารางขนาดจากการวัดจริง
- รายงาน PASS / FAIL / NOT RUN เทียบเป้า initial < 30 MiB (เป้าใช้งาน < 15 MiB)
- ระบุสาเหตุหลักของขนาด .wasm


### Stage 04: ข้อมูลของ Prototype

```text
ทำ Stage 04 เท่านั้น: ข้อมูลของ Prototype (แก้ผ่าน data/ เท่านั้น)

เนื้อหา:
- ย่าม 3 แบบ (ผ้าดิบ 3×3, ถุงผ้ายันต์ 1×3, ตะกร้าหวายรูปตัว L), อาวุธอาคม 4, เครื่องราง 3, ตำรา 1,
  ของเซ่น 2, ภูติ 1 (รวม 14) ทุกชิ้นมี tier 1-3 พร้อม merge_recipe
- ต้องมีอย่างน้อย 1 ชิ้นสายคุณไสย (ทดสอบกฎเสื่อมพลัง) และ 1 รูปทรงที่ไม่สมมาตร (ทดสอบการหมุน)
- พฤติกรรมคาถา (skill): projectile, pierce, aura, melee
- synergy 5 กฎตามหัวข้อกติกาคอมโบใน GDD กำหนดด้วย tag
- enemy: วิญญาณเร่ร่อน, กระสือ, นางตานี, บอสปอบ (2 เฟส); floor 1-5 พร้อม wave timeline
  (ชั้นปกติ 60 วินาที, ชั้นบอส timeout 90 วินาทีถือว่าแพ้)
- shop_pool, economy (เบี้ยตั้งต้น 15, ช่องสินค้า 4, refresh 5 +2, ขายคืน 50%, ฟื้น HP 15%),
  constant (cooldown ต่ำสุด, น้ำหนัก → ความเร็ว, ขนาด Board และหีบ), localization th/en
- รูปทรงเป็น string เช่น XX.|.XX และ parse ใน Domain

Acceptance:
- gen และ validate ผ่าน
- เทสต์ Core อ่านทุกตารางได้
- มี llmwiki/systems/data-schema.md
```

### Stage 05: Inventory Domain

```text
ทำ Stage 05 เท่านั้น: Pure Inventory Rules (ไม่มี Unity ยังไม่มี UI)

งาน:
1. Board (9×7), BagPieceInstance, ItemInstance (แยก ItemId ถาวรจาก ItemInstanceId) และ Chest (10 ช่อง)
2. footprint เป็นชุดพิกัดช่อง; หมุน 0/90/180/270; normalize รูปทรงที่ไม่สมมาตร
3. ตรวจการวาง: ย่ามห้ามทับกัน; ไอเทมวางได้เมื่อทุกช่องมีย่ามครอบ (พาดข้ามหลายย่ามได้); ไอเทมห้ามทับกัน
4. Move, Rotate, TransferToChest, Merge (id และ tier เดียวกัน → tier+1 สูงสุด 3), Recycle (50% ปัดลง)
   แบบ atomic พร้อม typed result
5. ย้ายหรือถอดย่ามได้เมื่อไม่ทำให้ไอเทมใดหลุดพื้นที่ใช้งาน (ปฏิเสธทั้งคำสั่ง)
6. น้ำหนักรวม → ค่าลดความเร็ว
7. SynergyEvaluator: adjacency 4 ทิศ (นับครั้งเดียวต่อคู่), แถวและคอลัมน์, กฎเสื่อมพลัง, bag-area bonus;
   โบนัสชนิดเดียวกันบวกกัน; cooldown มีค่าต่ำสุด; ไม่มีคอมโบวน; คำนวณใหม่เมื่อชุดเปลี่ยนเท่านั้น
8. LoadoutResolver → `ProjectSurvive.Core.Domain.Combat.Battle.CombatLoadoutSnapshot` (สัญญา Battle v1 จาก Stage 02b; ไม่ขึ้นกับลำดับการวน dictionary)
9. BackpackEditor: undo และ Reset (กลับสภาพตอนเข้าศาลา) ด้วย MessagePack snapshot, Lock
+ Loadout resolver ต้องคืน CombatLoadoutSnapshot ตามสัญญา Domain.Combat.Battle (ห้ามสร้าง type ซ้ำ)
+ เทสต์: synergy ที่ active ถูกคำนวณเข้าไปใน WeaponLoadout แล้ว (Battle ห้ามคำนวณ synergy ซ้ำ)
+ เทสต์: ย่ามเหมือนกัน → snapshot เหมือนกันทุก field (deterministic)

Tests ขั้นต่ำ:
- หมุน 4 ครั้งกลับรูปเดิม; รูปทรงไม่สมมาตรหมุนและ normalize ถูกต้อง
- วางนอก Board หรือบนช่องที่ไม่มีย่ามไม่ได้; ย่ามและไอเทมทับกันไม่ได้
- adjacency หลายช่องไม่นับโบนัสซ้ำ
- transaction ที่ล้มเหลวไม่เปลี่ยน state; merge ไม่ทำของสูญหายหรือซ้ำ; หีบเต็มจัดการถูก
- randomized operations 10,000 ครั้ง ตรวจ invariant หลังทุกคำสั่ง

Acceptance:
- ≥ 30 เคสผ่าน และ coverage ของ Backpack ≥ 85%
- กติกาตรง GDD และไม่มี UnityEngine
- CombatLoadoutSnapshot ที่ส่งออกตรงสัญญา `Domain.Combat.Battle` v1 (ครบ Stats / Weapons / Summons / Consumables)
```

### Stage 06: Shop, Economy, Run และ Scene Flow

```text
ทำ Stage 06 เท่านั้น: Shop, Economy, Run และ Scene Flow (ใช้ FakeBattleSession จาก Stage 02b แทนผล Battle)

งาน:
1. Wallet (เบี้ย), RunSession และ RunFlowState (Title → Camp → Battle → Camp → ... → Result)
2. ShopRoll: weighted จาก config, RNG แยกสายจาก Battle, สร้าง stock ครั้งเดียวต่อการเข้าศาลา, 4 ช่อง,
   ล็อกสินค้า, Refresh (5 เบี้ย เพิ่มครั้งละ 2 ภายในศาลาเดียวกัน)
3. Purchase, Sell, Refresh แบบ atomic: ซื้อเข้าหีบ, หีบเต็มต้องไม่หักเงิน, ซื้อช่องเดิมซ้ำไม่ได้,
   ช่องที่ซื้อแล้วไม่เติมฟรี
4. StartFloor validation: มีอาวุธอาคมใช้งานได้ ≥ 1 ชิ้น (คืน reason code ให้ UI); ล็อกย่ามระหว่าง Battle
5. CompleteFloor ให้รางวัลได้ครั้งเดียว (settlement ID) พร้อมฟื้น HP 15%
6. SceneId, SceneFlow state machine, Command/Event, ReplayLog (command + seed)
7. initial loadout: ย่ามผ้าดิบ + ยันต์เพลิงกาลี + 15 เบี้ย
+ SceneFlow รับ IBattleSessionFactory ผ่าน port/DI; scripted run ใช้ FakeBattleSessionFactory
+ RunSession.ApplyBattleResult(BattleResult): อัปเดต HP/เงิน/consumable แล้วฟื้น HP 15% ของ MaxHp (ไม่เกิน Max) เมื่อชนะ
+ ReplayLog ใช้ BattleReplay ของ 02b เป็นส่วนย่อย (ห้ามเขียนกลไก replay ซ้ำ)
+ เทสต์: run 5 ชั้นด้วย Fake (ชนะ 4 + บอส) จบที่ Result; Fake timeout ชั้นบอส → Defeat

Tests:
- seed และ state เดิมให้สินค้าเดิม; เปิดร้านซ้ำไม่เปลี่ยน stock; refresh เปลี่ยนสถานะ RNG และคิดราคาถูก
- เงินไม่พอหรือหีบเต็มไม่เปลี่ยน state; กดซื้อช่องเดียวสองครั้งซื้อได้ครั้งเดียว
- รับผล Battle ซ้ำไม่ให้เงินซ้ำ; transition ผิดลำดับถูกปฏิเสธ
- ไม่มีอาวุธอาคมแล้วเริ่ม Battle ไม่ได้

Acceptance:
- scripted run 5 ชั้น (stub ชนะทุกชั้น) จบที่ Result และ replay จาก log ได้ RunState hash เดิม
- ไม่มี economy logic ใน Presenter
- เข้าถึง Battle ผ่าน `IBattleSessionFactory` เท่านั้น (ห้ามอ้าง FakeBattleSession ตรง ๆ); replay ชั้นด้วย `BattleReplay` แล้วได้ `ComputeStateHash` เดิม

ทำ Stage 06b เท่านั้น: ร้านไม่เติมสินค้าในศาลาถัดไป (แก้บั๊ก ไม่เพิ่มฟีเจอร์)

อาการจากผู้ใช้: ซื้อสินค้าจากชั้นวางในศาลาหนึ่งแล้ว ช่องนั้นว่างตลอดในศาลาถัดๆ ไป
และเสี่ยงเซียมซีสุ่มใหม่เฉพาะช่องที่ยังมีของ
กติกา (GDD): ทุกการเข้าศาลาสร้าง stock ใหม่ 4 ช่อง; ช่องที่เช่าแล้วไม่เติมฟรีเฉพาะ ภายในศาลาเดียวกัน

A. เขียนเทสต์ที่ล้มก่อนแก้ (dotnet, ข้อมูลจริง): เริ่ม run → Purchase ช่อง 0 → StartFloor → ApplyBattleResult(ชนะ) → เข้าศาลาถัดไป
   คาดหวัง: 4 ช่องมีสินค้า, ไม่มีช่องใดถูกทำเครื่องหมายว่าเช่าแล้วหรือล็อก, RefreshCount = 0
   ทำซ้ำกับทุกช่อง, ซื้อหลายช่อง, ซื้อทั้ง 4 ช่อง, และกรณีล็อกช่องไว้แล้วออกเดินทาง
B. หาสาเหตุจริง (ห้ามเดา): ตรวจ ShopStock.BeginCamp, ShopSlot (IsPurchased, IsLocked), ShopRoller (CanBeRerolled)
   และ RunSession.EnterCamp ว่า flag ของช่องถูกล้างตอนเข้าศาลาใหม่ไหม ถ้า Core ผ่านเทสต์ A ให้ไล่ต่อที่ Presenter/View
   ของ Camp (shelf cache ที่ไม่ refresh หลังโหลดฉากใหม่) พร้อมหลักฐาน
C. แก้ที่ต้นเหตุ และคงกติกาเดิมทุกข้อ: ซื้อช่องเดิมซ้ำในศาลาเดียวกันไม่ได้, ช่องที่ซื้อแล้วไม่เติมฟรีในศาลาเดียวกัน,
   เปิดร้านซ้ำไม่เปลี่ยนสินค้า, refresh ไม่แตะช่องที่ล็อก
D. ใส่เทสต์ A ไว้ใน suite Fast และเพิ่มเงื่อนไข stock ของศาลาที่ 2 ไม่ว่างและไม่ติดสถานะเช่าแล้ว ในเทสต์ 20 รอบของ Stage 10

Acceptance: เทสต์ A ล้มก่อนแก้และผ่านหลังแก้ (แนบผลทั้งสองครั้ง), เทสต์เดิมทั้งหมดผ่าน, รายงานสั้น PASS / FAIL / NOT RUN

ทำ Stage 06c เท่านั้น: เสี่ยงเซียมซีเติมสินค้าให้ช่องที่เช่าแล้วด้วย (เปลี่ยนกติกาเดิมของ Stage 06)

บริบท: ตอนนี้ช่องที่เช่าแล้วถูกข้ามตอนสุ่มใหม่ จึงว่างตลอดทั้งศาลา (ADR 0016 ข้อ 2)
ผู้ใช้ต้องการให้ refresh เติมของใหม่ให้ช่องนั้นด้วย เหตุผล: GDD ห้ามเติมฟรี แต่ refresh เสียเบี้ย จึงไม่ใช่การเติมฟรี

กติกาใหม่:
1. Refresh สุ่มสินค้าใหม่ให้ทุกช่องที่ไม่ล็อก รวมช่องที่เช่าแล้ว; ช่องที่ล็อกยังคงสินค้าเดิม
2. ช่องที่เช่าแล้วและยังไม่ refresh ยังว่าง ซื้อซ้ำไม่ได้ และแสดงว่าเช่าแล้วเหมือนเดิม;
   เมื่อถูกสุ่มใหม่ ธง เช่าแล้ว ถูกล้างและเป็นสินค้าใหม่
3. ล็อกช่องที่เช่าแล้วหรือว่างยังคงปฏิเสธ (ไม่มีอะไรให้ล็อก)
4. สุ่มแบบไม่ซ้ำภายในรอบเดียว; ถ้าสินค้าที่เข้าเงื่อนไขน้อยกว่าช่องที่ต้องเติม ให้เว้นช่องว่างตามเดิม ไม่ซ้ำ
5. ราคา refresh, ลำดับ RNG ของร้าน, และ atomic (เงินไม่พอ state ไม่เปลี่ยน) ไม่เปลี่ยน

เอกสาร: ADR ใหม่ที่ supersede ADR 0016 ข้อ 2 (ส่วนที่ roller ข้ามช่องที่เช่าแล้ว) และแก้ llmwiki/systems/run-shop.md
แก้เทสต์เดิมที่ยืนยันว่าช่องที่เช่าแล้วไม่ถูกสุ่มทับ ให้เป็นกติกาใหม่ ห้ามลบเทสต์เฉยๆ

Tests: ซื้อช่อง 0 → refresh → ช่อง 0 มีสินค้าใหม่ไม่ติดสถานะเช่าแล้ว; ช่องที่ล็อกคงเดิม;
ซื้อทั้ง 4 ช่องแล้ว refresh ได้ 4 ช่อง; เงินไม่พอ refresh ไม่เปลี่ยน state; seed เดิมได้ผลเดิม; replay ได้ hash เดิม
Presentation: PlayMode: ซื้อจากชั้นวาง → คลิกกลอง → ชั้นวางนั้นมีสินค้าใหม่
Acceptance: เทสต์ผ่าน, รายงานสั้น PASS / FAIL / NOT RUN
```

### Stage 07: Battle Core และ bot sim

```text
ทำ Stage 07 เท่านั้น: Battle Core (EcsLite แบบ headless)

งาน:
1. BattleSimulation รับ config, seed, CombatLoadoutSnapshot และ input frame; fixed tick 30 Hz;
   กำหนดนโยบาย backlog และ pause เมื่อ tab ไม่ active
2. Components: Position, Velocity, Health, Faction, Enemy(kind), Projectile, Caster(cooldown, mpCost),
   Status, Pickup, Lifetime, Collider
3. Systems ตามลำดับ: WaveSpawn → PlayerMove → EnemySteer → AutoCast (MP, cooldown ต่ำสุด) →
   ProjectileMove → SpatialHash → Collision → Damage/Element/Status → Death → Drop → Magnet/Pickup → FloorEnd
4. เริ่มจากขอบเขตเล็ก: ผู้เล่น 1 ตัว, ศัตรูเดินเข้าหา 1 ชนิด, คาถา projectile 1 แบบและ aura 1 แบบ แล้วค่อยเพิ่ม
5. event buffer ให้ Presentation ดึงไปใช้; ไม่อ้าง GameObject, Camera, UI หรือ Unity Physics
6. pool entity และ array; ไม่ allocate ใน Tick หลัง warm-up
7. Sim/: bot เดินหนีแบบ kiting และตัดสินใจร้าน/ย่ามแบบ greedy; tools/sim.ps1 --seeds 200 → artifacts/balance.csv
+ BattleSimulation (EcsLite) ต้อง implement IBattleSession + IBattleSessionFactory

+ ต้องผ่าน BattleSessionContractTests ชุดเดียวกับ Fake ห้ามแก้หรือข้ามเทสต์เพื่อให้ผ่าน

  (ถ้าสัญญาผิดจริง ให้เขียน ADR แล้วแก้สัญญาพร้อม Fake ก่อน)

+ Event buffer ใช้ BattleEventRing ของ 02b; type ของ EcsLite ห้ามหลุดออกนอก lib/Runtime/Simulation

+ Bot sim เรียกผ่าน IBattleSession เท่านั้น

+ Benchmark ~300 ศัตรู + 500 กระสุน: บันทึก avg/p95 ms ต่อ tick; GC alloc ต่อ tick = 0 หลัง warm-up

Tests:
- เคลื่อนที่ถูกตาม tick; cooldown และ MP ไม่ร่ายเกินกติกา
- projectile hit และ despawn ถูกต้อง; ศัตรูตายให้รางวัลครั้งเดียว; ผู้เล่นตายจบชั้นครั้งเดียว
- simulation dispose ได้; seeded replay ซ้ำได้ใน runtime เดียวกัน

Acceptance:
- รันได้โดยไม่มี Scene
- benchmark ที่ ~300 ศัตรู + 500 กระสุน บันทึกเวลาต่อ tick และ GC alloc ต่อ tick (เป้าคือ 0) บน .NET
- ไม่สัญญา bitwise determinism ข้าม browser หรือ CPU
- implement `IBattleSession` / `IBattleSessionFactory` และผ่าน `BattleSessionContractTests<TFactory>` ทุกข้อ; ใช้ `BattleEventRing` และ `BattleFrameBuffer` ร่วมกับ fake; ห้าม implement `IBattleFrame` เอง — `BattleFrameBuffer` ต้องเป็น implementation เดียว (บังคับด้วย architecture test)

ทำ Stage 07b เท่านั้น: เบี้ยจากการต่อสู้ (ปิดช่องว่างที่ ADR 0017 บันทึกไว้) ผ่าน data, Simulation, Run

บริบท: ตอนนี้ฆ่าผีไม่ได้เบี้ย (pickup มีค่า 0, BattleResult.CurrencyEarned = 0) และไม่มีรางวัลจบชั้น
เบี้ยหลังศาลาแรกจึงมีแต่ที่เหลือจาก economy.start_bia (GDD: เบี้ยมาจากการเก็บในสนามและรางวัลจบชั้น จำนวนอยู่ใน data)
ห้ามฝังตัวเลขในโค้ด

1. data/: เพิ่มคอลัมน์ enemy.drop_value (เบี้ยต่อผีหนึ่งตัว แยกตามชนิด/Elite/บอส) และรางวัลจบชั้นใน economy
   เลือกแบบที่เรียบที่สุดแล้วเขียนเหตุผลใน ADR สั้นๆ ตัวเลขเป็นค่าตั้งต้นเพื่อทดลอง:
   ของในร้านราคา 8–11 เบี้ย ชั้น 1 ฆ่าได้ราว 20 ตัว ตั้งให้รายได้ต่อชั้นพอซื้อของได้ราว 1–2 ชิ้น แล้วค่อยจูนด้วย sim ใน Stage 11
   เพิ่ม validation (ค่า ≥ 0, ทุก enemy มีค่า) และ fixture ผิด, bump SchemaVersion, gen + verify-generated
2. Simulation: pickup ที่ผีทิ้งมีค่าตาม drop_value; เก็บแล้วสะสมเข้า CurrencyEarned (ครั้งเดียวต่อ pickup);
   ไม่เปลี่ยนกฎอื่นของการต่อสู้
3. Run: ApplyBattleResult เครดิต CurrencyEarned และรางวัลจบชั้น (เฉพาะชนะ) ตามกติกา settlement ครั้งเดียวเดิม
4. HUD ขวาบน เบี้ยที่ได้ ต้องแสดงค่าที่เก็บได้ในสนามจริง (ถ้ายังไม่ตรงให้แก้ให้ตรง)
5. แก้ ADR 0017 ส่วน kill pays nothing (supersede) และ llmwiki

Tests: ฆ่าผีแล้วเก็บ → CurrencyEarned = ผลรวม drop_value; เก็บ pickup เดิมซ้ำไม่นับซ้ำ; แพ้ไม่ได้รางวัลจบชั้น;
settlement ซ้ำไม่จ่ายซ้ำ; replay ได้ hash เดิม; sim 200 seed รายงานสัดส่วนชนะต่อชั้นก่อนและหลัง (ยังไม่ต้องจูนให้ได้ 30–60%)
Acceptance: gen และ validate ผ่าน, เทสต์ผ่าน, รายงาน PASS / FAIL / NOT RUN

ทำ Stage 07c เท่านั้น: กวาดเบี้ยทั้งหมดเข้าหาผู้เล่นก่อนจบชั้น (data และ Simulation)

บริบท: ตอนครบเวลา (หรือฆ่าบอส) ชั้นจบทันที เบี้ยที่ยังตกอยู่บนพื้นหายไป ผู้ใช้ต้องการให้เบี้ยทั้งหมดบนพื้นลอยเข้าหาผู้เล่น
และนับเข้า CurrencyEarned ก่อนค่อย fade ย้ายฉาก ห้ามคัดลอกโค้ดหรือ asset จากเกมอ้างอิง
กติกานี้กระทบผลรางวัล จึงอยู่ใน Simulation ไม่ใช่ใน View เพื่อให้ replay ได้ผลเดิม

1. data/: เพิ่ม constant.magnet_speed (ADR 0017 เสนอไว้) และเพดานเวลากวาด (วินาที) เป็นค่าใน constant;
   bump SchemaVersion, gen, validate
2. Simulation: เมื่อเงื่อนไขชนะเกิดขึ้น (ครบเวลาชั้นปกติ หรือบอสตาย) ให้เข้าช่วงกวาดก่อน Finish:
   - ทุก pickup ที่เหลือเคลื่อนเข้าหาผู้เล่นด้วย magnet_speed และถูกเก็บเมื่อถึง (นับเข้า CurrencyEarned ครั้งเดียวต่อ pickup)
   - ผีที่เหลือถูกกำจัดโดยไม่ดรอปเพิ่ม (หรือหยุดนิ่ง) และผู้เล่นไม่รับดาเมจระหว่างกวาด เลือกแบบที่เรียบที่สุดแล้วบันทึกใน ADR สั้นๆ
   - จบช่วงกวาดเมื่อ pickup หมดหรือครบเพดานเวลา ที่เพดานให้เก็บส่วนที่เหลือทันที เพื่อให้จบแน่นอน
   - แพ้ (ผู้เล่นตาย) ไม่กวาด; ไม่แตะกติกาอื่น
3. สัญญา Battle: BattlePhase คงเดิม (ช่วงกวาดยังเป็น Running); เทสต์สัญญาที่ใช้ร่วมกับ Fake ต้องผ่านโดยไม่แก้
   (ถ้าต้องแก้จริงให้เขียน ADR ก่อน)
4. Presentation: ไม่ต้องมีโค้ดสั่งการเคลื่อนที่ pickup (เป็น entity ที่ view วาดอยู่แล้ว); ตัวขับฉากรอ phase เปลี่ยนก่อน fade เหมือนเดิม
   ตรวจว่าไม่มี view ใหม่ถูกสร้างระหว่างกวาด (viewsGrownDuringPlay = 0)
5. HUD: ตัวเลขเบี้ยที่ได้เพิ่มระหว่างกวาด (ใช้ช่องทางเดิม)

Tests: ปล่อย pickup หลายค่าไว้บนพื้นแล้วครบเวลา → CurrencyEarned = ผลรวมทั้งหมด; ไม่นับซ้ำ;
ครบเพดานเวลาแล้วที่เหลือถูกเก็บครบ; แพ้ไม่กวาด; ชั้นบอสกวาดหลังบอสตาย;
Step ยังไม่ allocate หลัง warm-up และ benchmark 300 ศัตรู + 500 กระสุนไม่แย่ลงเกินที่รายงาน;
seeded replay ได้ hash เดิม; BattleSessionContractTests ผ่านทั้งสอง factory
Acceptance: gen และ validate ผ่าน, เทสต์ผ่าน, รายงานสั้น PASS / FAIL / NOT RUN
```

### Stage 08: Battle Presentation 2.5D

```text
ทำ Stage 08 เท่านั้น: Battle Presentation 2.5D

งาน:
1. SceneBattle + MVP; map simulation 2D → Unity XZ; กล้องเฉียงประมาณ 50°
   ทดลอง orthographic กับ perspective (depth sorting, การบังตัวละคร) แล้วเลือกด้วย ADR
2. BattleSimulationRunner (fixed tick + render interpolation), BattleRenderBridge และ EntityViewPool
   (pool SpriteRenderer หรือ instancing ตามผลวัดใน Web)
3. InputAdapter → input frame; ปุ่มความเร็ว 1x/2x
4. HUD ด้วย R3: HP/MP, เบี้ย, เวลา, หลอด timeline มี marker elite และบอส
5. ใช้ CombatLoadoutSnapshot จริง; แสดงคาถาตามไอเทมที่ติดตั้ง
6. floating damage number แบบ pool; RNG ภาพแยกจาก RNG เกม
7. ไม่ Instantiate/Destroy ต่อเนื่องหลัง pool warm-up
+ Presentation อ้างได้แค่ namespace Domain.Combat.Battle ห้ามอ้าง asmdef Simulation (เพิ่ม architecture test)

+ BattleSimulationRunner: fixed-step accumulator (TickDelta, MaxStepsPerFrame) + interpolate Prev→Cur ด้วย alpha โดยอ่านคู่จาก `IBattleFrame.PreviousEntities` / `IBattleFrame.Entities`

+ HUD/VFX/Audio มี event cursor ของตัวเอง; ถ้า dropped > 0 ให้ log ใน dev build แต่ห้ามทำให้ state เพี้ยน

+ พัฒนา UI กับ Fake ได้ก่อนต่อเข้ากับ sim จริง (สลับผ่าน LifetimeScope)

Acceptance:
- เล่นชั้น 1 จนชนะหรือแพ้ได้ใน Web
- เปลี่ยนของในย่ามแล้วพฤติกรรม Battle เปลี่ยนจริง
- HUD ไม่ subscribe ตำแหน่งทุก entity
- ออก Battle แล้ว ECS world และ subscription ถูก dispose
- PlayMode: เข้า Battle แล้ว tick 10 วินาทีไม่มี exception
- Presentation อ่านเฉพาะ `IBattleFrame` / `IBattleEventSource` และห้ามใช้ event ตัดสิน logic; span ของ `Entities`/`PreviousEntities` ถูก copy ก่อน `Step`/`Submit` ครั้งถัดไป

ทำ Stage 08b เท่านั้น: เปิดสถานะคูลดาวน์ของอาวุธใน battle frame (สัญญาและ Simulation เท่านั้น ยังไม่แตะ UI)

บริบท: HUD ใหม่ต้องแสดงบนไอเทมแต่ละชิ้นว่าติดคูลดาวน์เท่าไหร่ แต่ Presentation อ่านได้เฉพาะ IBattleFrame / IBattleEventSource
และสัญญาตอนนี้ไม่มีข้อมูลนี้ ห้ามให้ View เดาจากนาฬิกาเอง การเพิ่มต้องผ่านสัญญา

1. ตรวจ CombatLoadoutSnapshot ว่าแต่ละ WeaponLoadout/SummonLoadout แมปกลับไปยังชิ้นบน Board ได้ (instance id) ไหม
   ถ้าไม่ได้ ให้เพิ่ม
2. เพิ่มลงใน IBattleFrame ชุดข้อมูลต่อ loadout entry: ticks คูลดาวน์ที่เหลือ, ticks คูลดาวน์เต็ม,
   และธง "พร้อมร่ายแต่ติด MP"
   - ความจุคงที่ (ค่าคงที่ใน BattleConstants), ไม่ allocate ต่อ tick, อายุ span เหมือน Entities (ใช้ได้ถึง Step/Submit ครั้งถัดไป)
   - BattleFrameBuffer ยังเป็น implementation เดียว; FakeBattleSession และ EcsBattleSimulation เขียนผ่านมัน
3. Simulation: เขียนค่าจาก component ของ caster ที่มีอยู่แล้ว ห้ามเพิ่มกฎคูลดาวน์ใหม่ ห้ามเปลี่ยนผลการต่อสู้
   (state hash ของ seed เดิมต้องไม่เปลี่ยน พิสูจน์ด้วยเทสต์)
4. Fake: สร้างค่าที่เดาได้ (วนคูลดาวน์ตามตัวเลขที่กำหนด) เพื่อใช้พัฒนา UI
5. เขียน ADR สั้นๆ (แก้สัญญา เหตุผล ทางเลือกที่ไม่เลือก) และแก้ llmwiki/systems/battle-contract.md

Tests: BattleSessionContractTests ที่ใช้ร่วมกับทั้งสอง factory: จำนวน entry = จำนวนใน loadout; คูลดาวน์ที่เหลือลดลงทีละ tick
จน 0 แล้วรีเซ็ตเมื่อร่าย; ธง MP ขึ้นเมื่อ MP ไม่พอ; Step ไม่ allocate; state hash ของ seed เดิมเท่าเดิมก่อนและหลังแก้
Acceptance: dotnet test ผ่าน (Fast), architecture test ยืนยัน IBattleFrame มี implementation เดียว,
รายงานสั้น PASS / FAIL / NOT RUN

ทำ Stage 08c เท่านั้น: จัด HUD ของ Battle ใหม่ตามแบบเกมอ้างอิง (Presentation ใช้ข้อมูลจาก 08b)
ห้ามแก้ Core และ Simulation เพิ่มได้เฉพาะแถว localization; ใช้ placeholder ที่สร้างเอง ห้ามคัดลอก asset จากเกมอ้างอิง
ตรวจด้วยภาพจาก Editor ที่ขนาดจริง ไม่ต้อง build Web ระหว่างทำงาน

เลย์เอาต์ (1920×1080 และ 1280×720, ทุกส่วนผูก anchor กับขอบ canvas):
1. ซ้ายบน: ย่ามจิ๋วแบบอ่านอย่างเดียว วาดเฉพาะพื้นที่ช่องที่ย่ามครอบ (bounding box) ไม่ใช่ทั้ง Board 9×7
   - ขนาดช่องคำนวณให้พอดีกรอบไม่เกินราว 1/4 ของความกว้างจอ; ย่ามเป็นพื้นหลัง ไอเทมเป็นกล่องสีตามหมวดพร้อมเลข tier
   - อ่านรูปทรงและตำแหน่งจาก run/loadout เดียวกับที่ส่งให้ Battle (ห้ามคำนวณกฎซ้ำใน View)
   - ไอเทมที่ร่ายได้แสดงคูลดาวน์เป็นแผ่นมืดที่ลดลงตามเวลาที่เหลือ (จาก IBattleFrame ของ 08b);
     ติด MP แสดงโทนน้ำเงินจาง; พร้อมร่ายไม่มีแผ่นมืด
   - สร้าง element ครั้งเดียวตอนเริ่ม (pool) ห้าม Instantiate ต่อเนื่อง ห้าม string concat ต่อเฟรม; อัปเดตเมื่อค่าเปลี่ยนเท่านั้น
   - ยังไม่วาดเส้นเชื่อม synergy ในรอบนี้
2. กลางบน: ชื่อด่านและชั้น (ย้ายจากซ้ายบน)
   ขวาบน: เวลาที่เหลือ, เบี้ยที่ได้, ศัตรูที่กำจัด, ระลอก และปุ่มความเร็ว ตามเดิมทุกอย่าง
3. ซ้ายล่าง: พาเนลสถานะ: รูปตัวละคร placeholder, หลอด HP (แดง) และหลอดพลังอาคม (น้ำเงิน) พร้อมตัวเลขปัจจุบัน/สูงสุด
   (ย้ายหลอดเดิมจากซ้ายบนมาที่นี่)
4. ขวาล่าง: DEV MENU (dev build เท่านั้น) จัดเป็นคอลัมน์เดียว ไม่ทับไทม์ไลน์และไม่ทับพาเนล HP
5. เอาข้อความรายการ คาถาที่ติดตั้ง ออก (ย่ามจิ๋วแทนแล้ว); ไทม์ไลน์กลางล่างคงเดิมแต่ต้องไม่ทับส่วนอื่น
6. ข้อความไทยผ่าน localization ขนาด ≥ 22 px ที่ 1080p; R3 ส่งเฉพาะสถานะระดับ UI;
   คูลดาวน์ต่อไอเทมอ่านจาก frame snapshot ที่ copy แล้วในเฟรมเดียวกัน ไม่ subscribe ต่อ entity

ภาพหลักฐาน → artifacts/ui/ สองขนาด: เริ่มชั้น, กลางการต่อสู้ที่มีคูลดาวน์หลายระดับ (ใช้ Fake battle จาก 08b), และ MP ไม่พอ
เพิ่ม region ของ Battle (ย่ามจิ๋ว ชื่อด่าน สถิติขวาบน พาเนล HP ไทม์ไลน์ dev menu) เข้าเครื่องมือตรวจการทับกันเดิม

Tests: PlayMode: ค่าคูลดาวน์จาก frame ทำให้แผ่นมืดเปลี่ยนตามสัดส่วน; ย่ามจิ๋วตรงกับ loadout;
10 วินาทีของ battle ไม่มี exception และ viewsGrownDuringPlay = 0 เหมือนเดิม
Acceptance: ภาพสองขนาดไม่มีการทับกัน, เทสต์ผ่าน, รายงานสั้น PASS / FAIL / NOT RUN


ทำ Stage 08d เท่านั้น: ขยับย่ามจิ๋ว (MiniBagPanel) ของ Battle HUD ให้ชิดซ้ายบน ใต้ข้อความ "ย่าม"
ขอบเขต: Presentation และการสร้างฉากเท่านั้น ห้ามแก้ Core, Simulation, data/ ห้ามเพิ่ม localization
ตรวจด้วยภาพจาก Editor capture ที่ขนาดจริง ไม่ต้อง build Web

ปัญหาที่เห็นจากภาพ Game view (1920×1080):
- ข้อความ "ย่าม" (BagCaption) อยู่มุมซ้ายบนของ MiniBagPanel ถูกต้องแล้ว
- แต่ตัวย่ามจิ๋ว (MiniBagContent ที่วาดกล่องย่ามและไอเทม) ลอยอยู่ห่างลงมาและเยื้องไปทางขวา
  ไม่ได้ชิดใต้ข้อความ เพราะถูกจัดกึ่งกลางของพาเนลที่ใหญ่กว่า (Rect Transform ของ MiniBagPanel
  Left/Top/Right/Bottom = 24, Pivot 0.5/0.5)
- ผลที่ต้องการ: ย่ามจิ๋วเริ่มจากมุมซ้ายบนของพาเนล ต่ำกว่าข้อความ "ย่าม" ลงมาเล็กน้อย (ช่องว่างราว 8–12 px
  ที่ 1080p) และชิดขอบซ้ายเดียวกับข้อความ

งาน:
1. วินิจฉัยก่อนแก้ แล้วบันทึกสั้นๆ ลง docs/stages/08d-report.md:
   - ตำแหน่งจริงของ BagCaption และ MiniBagContent (anchor, pivot, offset, ขนาด) ที่ 1920×1080
   - จุดที่ทำให้ content อยู่กึ่งกลาง: ดูที่ BattleMiniBagLayout (การคำนวณ origin/ขนาดช่อง)
     BattleMiniBagView และ ProjectSetup (ตอนสร้าง MiniBagContent) ว่าตัวไหนเป็นต้นเหตุ ห้ามเดา
   - ตรวจด้วยว่าคอมโพเนนต์ Image ของ MiniBagPanel ถูกปิดอยู่ (ติ๊กออก) โดยตั้งใจหรือไม่
     รายงานผล แต่ห้ามเปลี่ยนสถานะ ถ้าไม่ใช่สาเหตุของปัญหานี้
2. แก้การจัดตำแหน่ง:
   - ให้ MiniBagContent ยึด anchor/pivot ที่มุมซ้ายบนของพื้นที่ใต้ข้อความ และให้ BattleMiniBagLayout
     วางกริดชิดมุมซ้ายบนแทนการจัดกึ่งกลาง (ขนาดช่องยังคำนวณให้พอดีกรอบเหมือนเดิม
     และพาเนลยังกว้างไม่เกิน 1/4 ของความกว้างจอ)
   - ระยะห่างจากข้อความ และระยะขอบซ้าย เป็นค่าคงที่ที่มีชื่อชัดเจนในโค้ด (ไม่ใช่เลขลอย)
     ใช้ anchor ผูกกับขอบพาเนล ไม่ใช้ตำแหน่งพิกเซลตายตัว ต้องถูกต้องทั้ง 1920×1080 และ 1280×720
   - ย่ามที่รูปทรงไม่ใช่สี่เหลี่ยม (เช่นตะกร้า L) และย่ามหลายใบ ต้องชิดมุมซ้ายบนของ bounding box เช่นกัน
   - ห้ามเปลี่ยนกฎการอ่านย่ามหรือคูลดาวน์: ยังอ่านอย่างเดียวจาก BagSnapshot/CombatLoadoutSnapshot
     และ IBattleFrame.Cooldowns ตามเดิม ห้าม Instantiate เพิ่มระหว่างเล่น (CreatedAfterBuild ต้องเป็น 0)
3. สร้างฉากใหม่ผ่านเมนู ProjectSurvive/Stage 08/3. Create Battle scene (Editor API เท่านั้น
   ห้ามแก้ YAML ของ Battle.unity ด้วยมือ) แล้วตรวจว่า BattleViewSettings.MiniBag ยังถูกผูกอยู่

Tests:
- EditMode (BattleMiniBagTests): ตำแหน่งกล่องแรกของย่ามชิดมุมซ้ายบนของพื้นที่ใช้งาน ที่ขนาดพาเนลทั้งสองขนาด
  รวมย่ามรูปตัว L; ช่องว่างจากข้อความเท่าค่าคงที่; ขนาดช่องเท่าเดิมกับก่อนแก้
- PlayMode (BattlePlayModeTests): ย่ามจิ๋วในฉากจริง built แล้ว, rect ของกล่องย่ามไม่ทับ BagCaption,
  CreatedAfterBuild = 0 และ viewsGrownDuringPlay = 0 เหมือนเดิม
- UiLayoutCheck: ผ่านทั้งชุดเดิม และเพิ่มกฎว่า MiniBagContent ต้องอยู่ใต้ BagCaption และไม่ทับกัน

ภาพหลักฐาน → artifacts/ui/ (1920×1080 และ 1280×720): เริ่มชั้น และกลางการต่อสู้ที่มีแผ่นคูลดาวน์
เทียบกับภาพก่อนแก้ (เก็บภาพก่อนแก้ไว้ก่อนลงมือ) แล้วระบุว่าตำแหน่งย่ามขยับไปกี่ px

รัน: tools/test.ps1 -Suite Fast และ capture UI
Acceptance: ภาพสองขนาดไม่มี element ทับกัน, ย่ามจิ๋วชิดซ้ายบนใต้ข้อความ "ย่าม",
เทสต์ผ่าน, รายงานสั้น PASS / FAIL / NOT RUN (WebGL และ Playwright = NOT RUN ตามที่สั่ง)
```

### Stage 09: ศาลาพัก (ร้าน และ UI จัดย่าม)

```text
ทำ Stage 09 เท่านั้น: Camp Scene และ UI จัดย่าม

งาน:
1. SceneCamp 2.5D placeholder: แท่นวางของพร้อมป้ายราคา, NPC ยายจันทร์, ปุ่มเสี่ยงเซียมซี, ปุ่มจัดย่าม
   UI ใช้งานได้ทันที ไม่บังคับเดินไปมา
2. หน้าจัดย่าม: Board และหีบ; ลากย่ามแล้วลากไอเทม; หมุนด้วย R หรือคลิกขวา (มีปุ่ม UI สำรอง);
   วางทับเพื่อ merge; ลากลงกระถาง ถวายคืน; ปุ่มคืนสภาพและล็อกย่าม
3. preview วางได้และวางไม่ได้ โดยไม่แก้ Domain จนผู้เล่นยืนยัน; Escape ยกเลิก drag แล้วคืนภาพเดิม
4. tooltip: stat, tier, synergy ที่ทำงานอยู่ (highlight ช่องที่เชื่อมกัน)
5. แถบล่าง: น้ำหนัก, สมาธิ, หีบ x/10, เบี้ย, ราคา refresh
6. ปุ่มออกเดินทาง (disable พร้อมแสดงเหตุผล); กันคลิกซ้ำระหว่างประมวลผล;
   รองรับ pointer ออกนอก canvas และ canvas resize
7. Presenter เรียก Application เท่านั้น ห้ามเขียนกฎซ้ำใน View
8. (เพิ่ม) ตัวละครเดินใน SceneCamp ด้วย WASD ได้ เมื่อเข้าใกล้แท่นแสดงป้ายรายละเอียด
   และกด E เพื่อเช่า คำสั่งเรียก use case ตัวเดียวกับปุ่มใน UI
   ปุ่ม UI เดิมต้องใช้งานได้ครบโดยไม่ต้องเดิน (ใช้เป็นทางหลักบนมือถือและใน Playwright)
   ห้ามให้ตัวละครเดินทะลุ UI หรือทำงานตอนหน้าจัดย่ามเปิดอยู่

Acceptance:
- ซื้อ → วาง → หมุน → รวม → ถวายคืน → คืนสภาพ ผ่าน UI แล้ว state ตรงกับ Core
- invalid drop ไม่ทำของหาย
- ไม่มี GC spike ระหว่างลาก (ดูจาก Profiler)
- Playwright ทดสอบเมาส์และ keyboard focus ใน Web จริง
```

ทำ Stage 09b เท่านั้น: แก้หน้าจัดย่มให้ใช้ได้จริง (ไม่เพิ่มฟีเจอร์นอกรายการ)

ปัญหา: ลากไอเทมไม่ได้, องค์ประกอบทับกัน, ไม่มีป้ายกำกับ, ไม่บอกลำดับการใช้งาน

A. วินิจฉัยก่อนแก้ → docs/stages/09b-report.md
   ตรวจ input module ของ EventSystem ว่าตรงกับ Input System, GraphicRaycaster และ Raycast Target,
   element โปร่งใสที่บัง, handler begin/drag/end/drop ถูกเรียกจริงไหม (ใส่ log ชั่วคราวแล้วลบ)
   ระบุสาเหตุพร้อมหลักฐาน ห้ามเดา
B. Layout เป็น overlay ทับฉากศาลาพัก (เห็นฉากอยู่ด้านหลัง):
   - ซ้าย: Board ช่องขนาดเท่ากัน ช่องที่ย่ามครอบต่างจากช่องว่างชัดเจน
   - ขวา: หีบ (x/10) พาเนลใหญ่ มีป้ายหัวข้อ
   - ล่างซ้าย: ปุ่มกลับ และปุ่มไปชั้นถัดไป (ปิดพร้อมเหตุผลถ้าไม่มีอาวุธอาคม)
   - ล่างกลาง: กระถางถวายคืน ไม่ทับ Board
   - บนขวา: ม้วนคำแนะนำ 4 ข้อ: 1 ลากย่ามลง Board 2 ลากไอเทมวางบนย่าม
     3 คลิกขวาหรือกด R ระหว่างลากเพื่อหมุน 4 ไอเทมเหมือนกัน 2 ชิ้นวางซ้อนกันเพื่ออัปเกรด
   - บนซ้าย: ปุ่มคืนสภาพ พร้อมช่องติ๊กล็อกย่ามตอนคืนสภาพ
   - มุมล่างซ้าย: HP/MP, น้ำหนัก, เบี้ย
   ต้องไม่ทับกันที่ 1920×1080 และ 1280×720; ตัวหนังสือ ≥ 14 px ที่ 1080p
C. แสดงเส้นเชื่อม synergy ระหว่างไอเทมที่มีผลต่อกัน
D. Drag feedback: ไอเทมตามเมาส์, ไฮไลต์ช่องวางได้และไม่ได้, วางไม่ได้แล้วของกลับที่เดิม
E. วิธีสำรอง: คลิกเลือกไอเทมแล้วคลิกช่องปลายทาง
F. เทสต์: PlayMode จำลอง pointer events จริง และ Playwright ลากด้วยเมาส์จริงในหน้า Web
   ห้ามนับเทสต์ที่เรียก use case ตรงเป็นเทสต์ drag

Acceptance:
- มีรายงานสาเหตุที่ลากไม่ได้พร้อมหลักฐาน
- ภาพหน้าจอ 1920×1080 และ 1280×720 ไม่มี element ทับกัน
- เทสต์ลากด้วย pointer events จริงผ่าน รายงาน PASS / FAIL / NOT RUN ตามจริง

ทำ Stage 09c เท่านั้น: ศาลาพักแบบเดินได้ (ร้านบนชั้นวางในฉาก)

1. ตัวละครสามารถเดินด้วย WASD ในฉาก 2.5D กล้องเฉียงตามผู้เล่น
2. ชั้นวางสินค้า 4 จุดในฉาก แต่ละจุดแสดงไอเทมและป้ายราคา (world-space)
   ไม่มีรายการร้านใน UI ซ้ำอีกชุด
3. เดินเข้าใกล้ชั้นแล้วกด E หรือคลิกที่ชั้นเพื่อเช่า (use case เดียวกับ Stage 06)
   ของเข้าหีบ; หีบเต็มหรือเงินไม่พอให้แสดงเหตุผลและไม่หักเงิน; ชั้นที่เช่าแล้วว่าง
4. หีบรีเซ็ตสินค้า (เสี่ยงเซียมซี) เป็นวัตถุในฉาก มีป้ายราคา เข้าใกล้แล้วกด E
   ล็อกสินค้าก่อนรีเซ็ตทำได้จากป้ายของชั้น
5. ปุ่ม จัดย่าม กลางล่างของจอ เปิดหน้าจาก 09b; ระหว่างหน้าจัดย่ามเปิดอยู่ ตัวละครหยุดเดิน
6. คลิกชั้นได้โดยไม่ต้องเดิน (สำหรับมือถือและเทสต์)

Acceptance:
- เดิน → เช่า → ของเข้าหีบ → เปิดหน้าจัดย่าม → ลากลง Board ผ่านจริง
- PlayMode จำลองการเดินและกด E; Playwright กด WASD และ E ในหน้า Web จริง
- ภาพหน้าจอไม่มี element ทับกัน

ทำ Stage 09d เท่านั้น: แก้ SceneCamp UI 3 เรื่อง (ห้ามแก้ Core, data หรือกติกา synergy)
ตรวจด้วยภาพจาก Editor ไม่ต้อง build Web ระหว่างทำงาน

1. Layout: ทุกส่วนต้องอยู่ในขอบ canvas ที่ 1920×1080 และ 1280×720 (ตรวจจากภาพที่จับที่ขนาดจริง
   ไม่ใช่จากหน้าต่าง Game view ที่เล็กกว่า) ปรับ anchor/pivot ตามขอบจอ ไม่ใช้ตำแหน่งพิกเซลตายตัว
   - ปุ่ม ปิด / ไปชั้นถัดไป ต้องไม่ทับข้อความสถานะ (เบี้ย น้ำหนัก สมาธิ HP MP) และไม่มีข้อความสถานะซ้ำ
   - กระถางถวายคืนต้องไม่ทับปุ่มออกเดินทาง
   - พาเนล Board และหีบทึบพอ (alpha ≥ 0.9) ไม่ให้ฉากด้านหลังโผล่ผ่านจนอ่านยาก
   - ป้ายชื่อในช่อง: ชื่อย่ามกับชื่อไอเทมต้องไม่ซ้อนกัน (ชื่อไอเทมเป็นหลัก ชื่อย่ามเป็นข้อความรองหรือซ่อนเมื่อที่ไม่พอ)
2. Tooltip อยู่บนสุดเสมอ
   - ใช้ Canvas แยกที่ลำดับการวาดสูงกว่า Board หีบ กระถาง และ HUD ทั้งหมด (ตรวจเอกสาร uGUI ของเวอร์ชันที่ติดตั้ง ห้ามเดา)
   - ปิด raycast target, ตามเมาส์, อยู่ในจอเสมอ (พลิกซ้ายขวาขึ้นลงเมื่อชนขอบ), พื้นหลังทึบ (alpha ≥ 0.92)
   - เนื้อหาเป็นบรรทัดแยกชัดเจน: ชื่อ, ระดับ, สถานะ (ดาเมจ คูลดาวน์ สมาธิ), คำอธิบาย, คอมโบที่ทำงานอยู่
3. เส้นเชื่อม synergy
   - วาดเฉพาะเมื่อมีคู่จริงระหว่างอาวุธอาคมกับตำรา (กฎตำราพระเวทแถวเดียวกัน) เส้นเริ่มจากตำราไปยังอาวุธแต่ละชิ้นที่ได้ผลจริง
   - ห้ามวาดเส้นจากย่ามไปไอเทม (กฎโบนัสพื้นที่ย่าม) ให้แสดงด้วยขอบหรือป้ายเล็กที่ช่องแทน
   - กฎอื่น (ตะกรุดติดยันต์, คุณไสยติดพระเครื่อง, น้ำแดงติดภูติ) ไม่วาดเส้นถาวร ไฮไลต์คู่ที่เกี่ยวข้องเฉพาะตอน hover หรือ drag
   - เส้นต้องไม่ทับข้อความ และหายทันทีเมื่อคู่ไม่เข้าเงื่อนไข ใช้ ActiveSynergy ที่ Core คืนมา ห้ามแก้ SynergyEvaluator

ภาพหลักฐาน → artifacts/ui/ ที่ 1920×1080 และ 1280×720 สามสถานะ:
ปกติ, tooltip เปิดบนไอเทมที่อยู่ใต้ย่าม, และตำรากับอาวุธวางเข้าเงื่อนไข

Acceptance:
- ภาพทั้งสองขนาดไม่มี element หลุดขอบหรือทับกัน และ tooltip อยู่บนสุด
- เทสต์ตรวจว่า tooltip วาดเหนือ Board และเส้นถูกวาดเฉพาะคู่อาวุธ–ตำรา (ไม่มีเส้นย่าม–ไอเทม)
- รายงานสั้น PASS / FAIL / NOT RUN

ทำ Stage 09e เท่านั้น: ย้ายและหมุนย่ามแล้วไอเทมบนย่ามย้ายตามไปด้วย (เปลี่ยนกติกาเดิมของ Stage 05)

บริบท: กติกาเดิม (ADR 0014) ปฏิเสธการย้ายหรือหมุนย่ามถ้ามีไอเทมวางอยู่ ผู้ใช้ต้องการให้ไอเทมติดไปกับย่าม
ห้ามแก้ข้อมูลใน data/ และห้ามแก้กติกา synergy

กติกาใหม่ (อยู่ใน Core ไม่ใช่ View):
1. ย้ายย่าม: ไอเทมที่ทุกช่องของมันอยู่บนย่ามใบนี้ใบเดียวย้ายตามไปพร้อมกัน คงตำแหน่งสัมพัทธ์เดิม
2. หมุนย่าม 90°: ย่ามกับไอเทมที่ติดไปหมุนเป็นวัตถุแข็งชิ้นเดียวรอบจุดอ้างอิงเดียวกัน ไอเทมคงทิศสัมพัทธ์กับย่าม
3. ไอเทมที่พาดข้ามหลายย่ามไม่ย้ายตาม: ถ้าหลังย้ายมีช่องใดของมันไม่มีย่ามครอบ ให้ปฏิเสธทั้งคำสั่ง
   ด้วย reason code ใหม่ (เช่น inventory.item_spans_bags) และ UI แสดงข้อความให้ย้ายไอเทมนั้นไปหีบหรือจัดใหม่ก่อน
4. ปลายทางต้องไม่ทับย่ามอื่น ไม่ออกนอก Board และไอเทมที่ย้ายไปต้องไม่ชนไอเทมอื่นที่ไม่ได้ย้าย
   (ถอดไอเทมที่ย้ายออกก่อนแล้วเช็กปลายทาง เพื่อให้วางทับตำแหน่งเดิมของตัวเองได้)
5. atomic: ล้มเหลวแล้ว state ไม่เปลี่ยน; สำเร็จครั้งเดียว = Revision เพิ่มครั้งเดียว, undo ย้อนทั้งย่ามและไอเทมในก้าวเดียว,
   คำนวณ synergy และ loadout ใหม่ครั้งเดียว (ความติดกันกับไอเทมบนย่ามอื่นอาจเปลี่ยน)
6. ถอดย่ามออกจาก Board ที่ยังมีไอเทมวางอยู่ ให้ปฏิเสธเหมือนเดิม
7. PreviewDrop ระหว่างลากย่ามต้องแสดงเงาของไอเทมที่ติดไปที่ตำแหน่งใหม่ และให้ผลวางได้/ไม่ได้ด้วยกติกาชุดเดียวกับคำสั่งจริง
   ห้ามเขียนกฎซ้ำใน View
8. เส้นเชื่อม synergy คำนวณใหม่หลังย้าย วาดเฉพาะคู่ที่กฎมีผลกับไอเทมชิ้นนั้นจริง (ไม่มีเส้นจากย่ามไปไอเทม
   และไอเทมที่กฎไม่ได้ส่งผลต่อไม่มีเส้น)

เอกสาร: เขียน ADR ใหม่ที่ supersede ข้อนี้ของ ADR 0014 และแก้ llmwiki/systems/backpack.md
แก้เทสต์เดิมที่ยืนยันการปฏิเสธให้เป็นกติกาใหม่ ห้ามลบเทสต์เฉยๆ

Tests (dotnet): ย้ายย่ามที่มีไอเทมตรงกลาง ตำแหน่งสัมพัทธ์คงเดิม; หมุนย่ามพร้อมไอเทมรูปทรงไม่สมมาตร 4 ครั้งกลับสภาพเดิม;
ไอเทมพาดสองย่ามแล้วย้ายย่ามหนึ่งใบ → ปฏิเสธและ state ไม่เปลี่ยน; ปลายทางชนย่ามอื่นหรือออกนอก Board → ปฏิเสธ;
undo ก้าวเดียวคืนทั้งหมด; ตำราพระเวทคนละแถวกับอาวุธไม่มีเส้น; randomized 10,000 คำสั่งที่รวมย้ายและหมุนย่าม
ตรวจ invariant (ไม่มีไอเทมนอกพื้นที่ย่าม ไม่ทับกัน ของไม่หายไม่ซ้ำ)
Presentation: PlayMode ลากย่ามด้วย pointer events จริงแล้วไอเทมตามไป

Acceptance: เทสต์ข้างต้นผ่าน, ภาพ Editor capture 1920×1080 ก่อนและหลังย้าย, รายงาน PASS / FAIL / NOT RUN


ทำ Stage 09f เท่านั้น: ระบบอัปเกรดไอเทมแบบเลือก 1 จาก 3 (Core และ data เท่านั้น ยังไม่ทำหน้าต่าง UI)

บริบท: ตอนนี้การรวมไอเทมสองชิ้นที่ id และ tier เดียวกันได้ tier+1 ทันที ต้องเปลี่ยนเป็นให้ผู้เล่นเลือกหนึ่งใน 3 รุ่นย่อยแบบสุ่ม
ซึ่งปรับค่าสถานะต่างกัน ห้ามคัดลอกโค้ดหรือ asset จากเกมอ้างอิง

1. data/: เพิ่มตาราง upgrade_variant: id, เงื่อนไขที่ใช้ได้ (หมวดหรือ tag), ขั้นที่อัปเกรด (1→2, 2→3), key ชื่อ (localization th/en),
   น้ำหนักสุ่ม, รายการตัวปรับค่า (ค่าสถานะ + วิธีปรับ + ขนาด)
   - ตัวปรับค่าอ้างได้เฉพาะค่าสถานะที่ CombatLoadoutSnapshot มีอยู่แล้ว ห้ามเพิ่มกลไกการต่อสู้ใหม่
   - เริ่มจากอาวุธอาคม 4 ชิ้น อย่างน้อย 4 variant ต่อขั้น (ให้ครบ 3 ตัวเลือกได้เสมอ) ตัวเลขเป็นค่าทดลองใน data เท่านั้น
   - หมวดที่ยังไม่มี variant: รวมทันทีเหมือนเดิม ไม่เปิดตัวเลือก
   - เพิ่ม validation กับ fixture ผิด: variant อ้างค่าที่ไม่มี, น้ำหนัก ≤ 0, หมวดที่มี variant น้อยกว่า 3 โดยไม่ได้ประกาศ fallback
2. RunSession: OfferMerge(a, b) ไม่เปลี่ยน state คืนตัวเลือกสูงสุด 3 ตัวแบบ deterministic;
   ConfirmMerge(a, b, variantId) atomic คืน typed result และบันทึกใน ReplayLog พร้อม variantId
3. สายสุ่มของการอัปเกรดแยกจาก shop/battle/visual เป็นฟังก์ชันของ (run seed, จำนวนครั้งที่รวมแล้ว, id ไอเทม)
   เพื่อให้ยกเลิกหรือ undo แล้วทำซ้ำไม่เปิดช่องรีโรล และ replay ได้ผลเดิม
4. ItemInstance เก็บ variantId ของแต่ละขั้น; LoadoutResolver ใส่ตัวปรับค่าลงใน CombatLoadoutSnapshot (ต้องมีผลจริงในการต่อสู้)
   undo, reset, RunState hash และ snapshot ของ MessagePack ต้องรวม variantId
5. ยังไม่ทำการเลือกชิ้นฐาน: เมื่อรวมสองชิ้นที่มี variant ของขั้นก่อนหน้าต่างกัน ให้ผลลัพธ์เก็บ variant ของชิ้นที่ถูกวางทับ
   และเขียนกติกานี้ลง ADR สั้นๆ
6. bot sim เลือก variant ด้วยกฎง่าย (เช่น ตัวแรก) รัน 200 seed แล้วรายงานสัดส่วนชนะก่อนและหลัง (ไม่ต้องจูน)

Tests: OfferMerge คืนผลเดิมเมื่อเรียกซ้ำด้วย state เดิม; ConfirmMerge ที่ล้มเหลวไม่เปลี่ยน state;
variantId ที่ไม่อยู่ในตัวเลือกถูกปฏิเสธ; undo แล้ว offer เดิมกลับมา; replay ได้ hash เดิม; ตัวปรับค่ามีผลใน snapshot

Acceptance: gen และ validate ผ่าน, fixture ผิดต้อง fail, เทสต์ผ่าน, รายงาน PASS / FAIL / NOT RUN


ทำ Stage 09g เท่านั้น: หน้าต่างเลือกอัปเกรด (UI) บน SceneCamp

ใช้ OfferMerge และ ConfirmMerge จาก 09f เท่านั้น ห้ามเขียนกฎหรือสุ่มใน View/Presenter ใช้ placeholder ห้ามคัดลอก asset จากเกมอ้างอิง

1. เมื่อวางไอเทมทับไอเทม id และ tier เดียวกัน: ถ้า OfferMerge คืนตัวเลือก เปิดหน้าต่าง modal; ถ้าไม่มีตัวเลือก รวมทันทีเหมือนเดิม
2. หน้าต่าง: หรี่ฉากด้านหลัง การ์ด 3 ใบเรียงแนวนอนกลางจอ แต่ละใบมี ชื่อรุ่น (คำนำหน้า + ชื่อไอเทม), ระดับ N → N+1,
   ค่าที่เปลี่ยนทีละบรรทัด (สีเขียวเมื่อดีขึ้น สีแดงเมื่อแย่ลง), ปุ่มเลือก
   แผงซ้ายแสดงค่าปัจจุบันของไอเทม (เนื้อหาเดียวกับ tooltip) และข้อความอธิบายสั้นๆ ด้านล่าง
3. เลือกด้วยเมาส์หรือแตะ และคีย์ 1 2 3; ปุ่มย้อนกลับหรือ Esc ปิดโดยไม่เปลี่ยนอะไร (เปิดซ้ำได้ตัวเลือกเดิมเพราะสุ่มแบบ deterministic)
4. ระหว่างเปิด: ล็อกการลากในหน้าจัดย่าม, tooltip ไม่โผล่ทับ, กันคลิกซ้ำ
5. หลังเลือก: ไอเทมเป็นขั้นใหม่ในช่องเดิม, loadout/synergy/เส้นเชื่อมคำนวณใหม่, เบี้ยและหีบไม่เปลี่ยน
6. ข้อความไทยผ่าน localization ขนาด ≥ 22 px ที่ 1080p; ไม่มี element ทับกันที่ 1920×1080 และ 1280×720
   (ตรวจด้วยเครื่องมือจับภาพ Editor เดิม)

Tests: PlayMode ใช้ pointer events จริง: รวมไอเทม → หน้าต่างเปิด → เลือกใบที่ 2 → ไอเทมเปลี่ยน; Esc แล้วไม่มีอะไรเปลี่ยน
Acceptance: เทสต์ผ่าน, ภาพ Editor capture ของหน้าต่างที่ 2 ขนาด, รายงาน PASS / FAIL / NOT RUN

ทำ Stage 09h เท่านั้น: Tooltip แสดงค่าจริงหลังอัปเกรด (ไม่แก้กติกา loadout ไม่แก้ data)

อาการ: ยันต์เพลิงกาลี ระดับ 3/3 ยังแสดง ดาเมจ 10 และ คูลดาวน์ 0.9 วิ เท่าระดับ 1, และหลังเลือกรุ่นย่อยคาถาไว
(คูลดาวน์ −15%) tooltip ไม่แสดงรายละเอียดเอฟเฟกต์และคูลดาวน์ไม่เปลี่ยน
สมมติฐาน (ยังไม่ยืนยัน): tooltip อ่านค่าจาก config พื้นฐาน ไม่ได้อ่านค่าที่ LoadoutResolver คำนวณแล้ว ตรวจก่อนแก้ ห้ามเดา

A. วินิจฉัย → report: พิมพ์เทียบสำหรับชิ้นเดียวกัน: ค่าที่ tooltip แสดง, ค่าใน WeaponLoadout/SummonLoadout ที่ LoadoutResolver คืน,
   และ constant.min_cooldown_seconds (ถ้าค่าต่ำสุดเท่ากับหรือสูงกว่าคูลดาวน์หลังลด การลดจะไม่เห็นผล ซึ่งอาจเป็นสาเหตุของ 0.9 วิ)
B. แยก read model ใน Application: RunSession ให้ข้อมูลอธิบายชิ้นที่ resolve แล้ว (ดาเมจ, คูลดาวน์, MP, ผลจาก tier,
   ผลจากคอมโบ, และรายการรุ่นย่อยที่เลือกของแต่ละขั้นพร้อมบรรทัดค่าที่เปลี่ยน) จากแหล่งเดียวกับ LoadoutResolver
   ห้ามให้ Presenter/View คำนวณเอง
C. Tooltip แสดง: ชื่อ, ระดับ, ค่า resolved พร้อมค่าพื้นฐานเทียบ (เช่น คูลดาวน์ 0.77 วิ จาก 0.9),
   ชื่อรุ่นย่อยและบรรทัดผลแต่ละขั้น, คอมโบที่ทำงาน; ถ้าค่าถูกจำกัดด้วยค่าต่ำสุด ให้บอกว่าถึงขีดต่ำสุดแล้ว
D. แผงซ้ายของหน้าต่างเลือกอัปเกรดใช้ผู้ผลิตข้อมูลชุดเดียวกัน (ยังคงเนื้อหาเดียวกับ tooltip)
E. ถ้าพบว่ารุ่นย่อยไม่มีผลใน snapshot จริง ให้รายงานเป็นบั๊กของ Core แยกต่างหาก ห้ามซ่อนด้วยการแก้ที่ tooltip

Tests: หลัง ConfirmMerge ด้วยรุ่น X ค่าที่ tooltip = ค่าใน snapshot และต่างจากก่อนอัปเกรดตามตัวปรับของรุ่น X;
ชิ้นที่ไม่มีรุ่นย่อย (รวมทันที) แสดงระดับใหม่โดยไม่มีบรรทัดรุ่นย่อย; undo แล้ว tooltip กลับค่าเดิม
Acceptance: เทสต์ผ่าน, ภาพ Editor capture tooltip ก่อนและหลังอัปเกรด 1920×1080, รายงานสั้น PASS / FAIL / NOT RUN

### Stage 10: เชื่อมวงจรเต็ม (Vertical Slice)

```text
ทำ Stage 10 เท่านั้น: เชื่อมวงจรเต็ม (Vertical Slice)

งาน:
1. Boot → Title → Camp → Battle ชั้น 1 → Camp → ... → ชั้น 5 → Result
2. สร้าง scope App / Run / Scene ให้ชัด: RunSession ไม่ถูกสร้างใหม่ทุกครั้งที่กลับ Camp;
   กลับ Title แล้ว dispose Run scope
3. ใช้ Battle scene เดิม เปลี่ยน FloorDefinition; ล้าง temporary combat state แต่คง HP, เบี้ย, ย่าม
4. Result: Victory และ Defeat พร้อมสรุปชั้นที่ไปถึงและผีที่ปราบ; Restart สร้าง Run ใหม่ ไม่คง state จากรอบก่อน
5. SceneLoader รับ transition ได้ทีละรายการ พร้อม fade และ loading/error recovery ขั้นต่ำ
6. DevMenu (dev build เท่านั้น): อมตะ, เพิ่มเบี้ย, ข้ามชั้น, spawn ผี ตั้งชื่อ test-only ให้ชัดเจน
+ DevMenu ส่งคำสั่งผ่าน BattleCommand (DevKillAll/DevGodMode/DevWinNow/DevLoseNow) เท่านั้น
  และให้ DevCheatsAllowed=false ใน release (มีเทสต์ยืนยัน)
+ dispose IBattleSession ทุกครั้งที่ออกจาก SceneBattle; เทสต์ 20 loop ต้องไม่มี session ค้าง

Acceptance:
- เล่นตั้งแต่เริ่มจนชนะและแพ้ได้
- ไม่มีเงินหรือไอเทมหายตอนเปลี่ยน Scene
- Camp ↔ Battle วน 20 รอบแล้วไม่มี presenter หรือ ECS world ซ้ำ และ memory ไม่โตต่อเนื่อง
- Title ไม่มี Run scope เก่าค้าง
- HP ข้ามชั้นผ่าน `BattleSetup.CurrentHp` → `BattleResult.FinalHp`; การฟื้น 15% เป็นหน้าที่ Run ไม่ใช่ Battle
```

ทำ Stage 10b เท่านั้น: ปุ่มเริ่มเล่นบน Title (ช่องโหว่ของ Stage 10)

ปัญหา: Play จาก Boot แล้วค้างที่ Title ไม่ไป Camp เพราะ Title ไม่มีตัวควบคุมที่พาออกไป
(prompt Stage 10 ระบุแค่ Boot → Title → Camp โดยไม่กำหนดว่า Title ไป Camp ด้วยอะไร)

A. สำรวจก่อนแก้ แล้วบันทึกผลสั้นๆ ลง report: Title มี UI อะไร, SceneBoot ทำอะไรหลังโหลด Title,
   ปัจจุบัน run เริ่มที่ไหน (StartRun ถูกเรียกจาก Camp หรือที่อื่น) ห้ามเดา
B. เพิ่มปุ่มเริ่มเกมบน Title (localization key ใหม่ th/en)
   - กดแล้ว Presenter เรียก Application เพื่อเริ่ม run ผ่านทางเดียวกับที่ใช้อยู่ ห้ามเริ่ม run ซ้ำสองที่
     แล้วสั่ง SceneLoader ไป Camp; กันคลิกซ้ำระหว่างเปลี่ยนฉาก (ใช้ typed refusal ที่มีอยู่)
   - กดได้ด้วยเมาส์และคีย์ Enter/Space; ตัวหนังสือ ≥ 22 px ที่ 1080p; ไม่ทับข้อความ build/config
   - Boot → Title ยังหยุดที่ Title โดยตั้งใจ (ไม่ข้ามไป Camp อัตโนมัติ) เขียนไว้ในเอกสาร
C. กลับมา Title จาก Result แล้วกดเริ่มเกมต้องได้ run ใหม่ (ชั้น 1, เบี้ยเริ่มต้น, ย่ามเริ่มต้น)
D. Title เผยตำแหน่งปุ่มให้หน้าเว็บ (เหมือน Result และ Camp: พิกัดกลางปุ่มเท่านั้น ไม่ใช่การตัดสินใจ)

Tests: PlayMode ใช้ pointer event จริง: Boot → Title → กดปุ่ม → Camp (ชื่อฉาก, run ถูกสร้างครั้งเดียว, phase Camp);
คลิกซ้ำเร็วๆ ไม่ทำให้ transition ซ้อน; Title ที่มาจาก Result มี run ใหม่
Playwright (Full เท่านั้น): เปิดหน้า → Title → คลิกปุ่มจากพิกัดที่รายงาน → Camp
Acceptance: เทสต์ผ่าน, ภาพ Title จาก Editor capture 2 ขนาด, รายงานสั้น PASS / FAIL / NOT RUN

### Stage 11: AI ผีไทย บอสปอบ และบาลานซ์

```text
ทำ Stage 11 เท่านั้น: AI ผีไทยและบอสปอบ

งาน:
1. FSM เล็กๆ แยกตามชนิดใน Simulation:
   - Krasue: dash → drain → retreat
   - NangTani: ยืนนิ่ง ร่ายวงช้าและเถาวัลย์
   - Pob: กินศพผีตัวอื่นเพื่อฟื้น HP, สิงจนทิศควบคุมสลับชั่วคราว, 2 เฟสตาม HP%
2. telegraph data (รูปทรง, เวลาเตือน) ส่งเป็น event ให้ Presentation วาดวงเตือนบนพื้น
   การโจมตีแรงทุกแบบต้องมี telegraph ที่อ่านออก
3. พารามิเตอร์ทั้งหมดมาจาก enemy และ skill ใน data
4. ศัตรูแต่ละชนิดต้องมีพฤติกรรมที่ผู้เล่นแยกออกได้
+ Telegraph ของนางตานีส่งผ่าน TelegraphStarted/Ended; ปอบกินศพส่ง CorpseConsumed; การเปลี่ยนเฟสส่ง BossPhaseChanged
+ ถ้าต้องเพิ่ม event type ใหม่ ให้เพิ่มต่อท้าย enum เท่านั้น แล้วอัปเดต battle-contract.md + เทสต์
+ sim 200 seed รันผ่าน IBattleSession และรายงานสัดส่วน EndReason (PlayerDied/Timeout/BossDefeated)

Tests: เทสต์ Core ราย behavior เช่น ทิศ input สลับตามเวลาที่กำหนด, เฟส 2 เริ่มที่ HP% ที่กำหนด

Acceptance:
- sim 200 seed: อัตราชนะบอสของ bot อยู่ระหว่าง 30–60% (ถ้าไม่ได้ ปรับ constant หรือ enemy data
  แล้วบันทึกผลก่อนและหลัง)
- มีอย่างน้อย 3 build ที่พฤติกรรมต่างกันชัดเจน
- ไอเทมทุกชิ้นมีผลจริง ไม่ใช่ tooltip อย่างเดียว
- boss และ telegraph สื่อสารผ่าน `BattleEventType` catalogue (`BossPhaseChanged`, `TelegraphStarted/Ended`, `CorpseConsumed`)

ทำ Stage 11a เท่านั้น: ผลเฉพาะชิ้นของไอเทม และตารางสถานะ (data, Domain, Simulation; ยังไม่ทำ AI ผี)

บริบท: ADR 0010 กำหนดว่าตัวเลขผลของเครื่องรางและของเซ่นต้องลง data พร้อมตัวที่ใช้ ห้ามเป็นค่าคงที่ในโค้ด
และ ADR 0017 ระบุว่าสถานะธาตุใช้ดาเมจของคาถาแทนชั่วคราวจนกว่าจะมีตาราง status

1. data/: คอลัมน์ผลของเครื่องราง (ตัวปรับค่าสถานะที่ CombatLoadoutSnapshot มีอยู่แล้ว ห้ามเพิ่มกลไกใหม่)
   และของเซ่น (ผลลัพธ์ ฟื้น HP หรือเพิ่มเบี้ย, จำนวนครั้งที่ใช้ได้, เงื่อนไขใช้อัตโนมัติ)
   ค่าตั้งต้น: ใช้เองอัตโนมัติเมื่อ HP ต่ำกว่าเกณฑ์ที่กำหนดใน data (ผู้ใช้ปรับได้ภายหลัง)
2. ตาราง status: id, ธาตุ, ดาเมจต่อครั้ง, ช่วงเวลา, ระยะเวลา, ซ้อนได้สูงสุด แทนกติกาชั่วคราวใน ADR 0017
3. LoadoutResolver ใส่ผลเครื่องรางลง snapshot; Simulation ใช้ของเซ่น (นับครั้งที่ใช้ แล้วส่งกลับใน
   BattleResult.RemainingConsumables) และใช้ตาราง status; ไม่แตะกฎอื่น
4. tooltip ของเครื่องรางและของเซ่นแสดงผลจริง (ใช้ read model เดียวกับ Stage 09h)
5. validation + fixture ผิด, bump SchemaVersion, gen/verify; ADR สั้นๆ supersede ส่วนที่เกี่ยวของ ADR 0010 และ 0017

Tests: เครื่องรางแต่ละชิ้นเปลี่ยนค่าใน snapshot ตามข้อมูล; ของเซ่นใช้ได้ไม่เกินจำนวนครั้ง และ settlement หักของออกจากย่ามถูกต้อง;
status ตาม data; replay ได้ hash เดิม; ไม่ allocate ใน tick
Acceptance: ไอเทมทุกชิ้นมีผลจริงใน snapshot หรือใน battle (มีเทสต์ครอบคลุมทุกชิ้น), gen/validate ผ่าน, รายงานสั้น PASS / FAIL / NOT RUN

ทำ Stage 11b เท่านั้น: AI กระสือและนางตานี พร้อม telegraph (Simulation และ Presentation)

1. FSM เล็กๆ ต่อชนิดใน Simulation: Krasue: dash → drain → retreat; NangTani: ยืนนิ่ง ร่ายวงช้าและเถาวัลย์
2. Telegraph (รูปทรง, เวลาเตือน) ส่งเป็น event (TelegraphStarted/Ended) ให้ Presentation วาดวงเตือนบนพื้น
   การโจมตีแรงทุกแบบต้องมี telegraph ที่อ่านออก
3. พารามิเตอร์ทั้งหมดอยู่ใน enemy และ skill ใน data (เพิ่มคอลัมน์ได้ แต่ต้อง gen/validate/fixture)
4. ผีแต่ละชนิดต้องมีพฤติกรรมที่ผู้เล่นแยกออกได้
5. ถ้าต้องเพิ่ม event type ใหม่ ให้ต่อท้าย enum เท่านั้น แล้วอัปเดต battle-contract.md และเทสต์สัญญา

Tests: เทสต์ Core ราย behavior (ลำดับสถานะ, เวลา telegraph ก่อนโจมตี); contract suite ผ่านทั้งสอง factory; ไม่ allocate ใน tick
Acceptance: เทสต์ผ่าน, ภาพ Editor capture วงเตือนที่ 1920×1080, รายงานสั้น PASS / FAIL / NOT RUN

ทำ Stage 11c เท่านั้น: บอสปอบ

1. FSM ปอบ 2 เฟส เฟสสองเริ่มที่ HP% ใน data; กินศพผีตัวอื่นเพื่อฟื้น HP (CorpseConsumed);
   สิงจนทิศควบคุมสลับชั่วคราว (เวลาและเงื่อนไขอยู่ใน data)
2. การสลับทิศอยู่ใน Simulation ไม่ใช่ใน View; HUD ต้องบอกผู้เล่นว่าทิศถูกสลับอยู่ ผ่าน event หรือสถานะที่มีอยู่
   ถ้าสัญญาไม่มีช่องทาง ให้แก้ด้วย ADR (ต่อท้าย enum) ห้ามให้ View เดา
3. สื่อสารผ่าน BossPhaseChanged, TelegraphStarted/Ended, CorpseConsumed
4. บอสทิ้งเบี้ยตาม enemy.drop_value และการกวาดของ 07c ทำงานหลังบอสตาย

Tests: เฟส 2 เริ่มที่ HP% ที่กำหนด; ทิศ input สลับและกลับตามเวลา; กินศพฟื้น HP ไม่เกิน Max; replay ได้ hash เดิม
Acceptance: เทสต์ผ่าน, ภาพ Editor capture ฉากบอสและสถานะทิศสลับ, รายงานสั้น PASS / FAIL / NOT RUN

ทำ Stage 11d เท่านั้น: จูนบาลานซ์ด้วย sim (data เท่านั้น ห้ามแก้โค้ดกฎเพื่อให้ตัวเลขผ่าน)

1. sim 200 seed ผ่าน IBattleSession รายงานก่อนจูน: อัตราผ่านต่อชั้น และสัดส่วน EndReason (PlayerDied / Timeout / BossDefeated)
2. จูนเฉพาะ data (enemy, floor, wave, economy, constant, synergy, upgrade) ตั้งเป้าอัตราชนะบอสของ bot 30–60%
   บันทึกผลก่อนและหลังทุกรอบลง report
3. อย่างน้อย 3 build ที่พฤติกรรมต่างกันชัดเจน (เช่น ยิงไกล, ประชิดหรือรอบตัว, ภูติ) รายงานอัตราชนะของแต่ละ build
   และชี้ว่าอันไหนเด่นหรือด้อยเกินไป
4. เศรษฐกิจ: รายได้ต่อชั้นต้องพอซื้อของได้ราว 1–2 ชิ้นตามเป้าของ 07b

Acceptance: อัตราชนะบอสของ bot อยู่ระหว่าง 30–60% (หรือรายงานตามจริงว่าทำไม่ได้พร้อมเหตุผล), มีตารางก่อน/หลัง,
รายงานสั้น PASS / FAIL / NOT RUN


```

### Stage 12: Save, Resume และ Settings

```text
ทำ Stage 12 เท่านั้น: Checkpoint, Resume และ Settings

งาน:
1. Save DTO แยกจาก Domain และ ECS (MessagePack source generator, ไม่ serialize GameObject หรือ ECS world):
   schema version, config version/hash, run ID, seed + สถานะ RNG แยกสาย, ชั้นที่กำลังจะเข้า, HP, เบี้ย,
   ย่ามและตำแหน่งของ, หีบ, stock ร้าน, ช่องที่ซื้อแล้ว, จำนวน refresh, settlement ID ล่าสุด
2. บันทึกหลัง transaction ที่ Camp (debounce ได้); ก่อนเข้า Battle ต้องรอ save เสร็จ;
   serialize การเขียนไม่ให้ save เก่าทับใหม่; ไม่พึ่ง unload callback
3. Browser adapter (IndexedDB ผ่านวิธีที่ Unity 6.3 รองรับจริง: ตรวจเอกสารของ version ที่ล็อก ห้ามเดาชื่อฟังก์ชัน)
   และ desktop adapter; storage ใช้ไม่ได้ต้องรายงาน failure ไม่แสดงว่าสำเร็จ
4. reload ระหว่าง Battle กลับ checkpoint ก่อนเข้าชั้น ไม่รับเบี้ยหรือรางวัลของ Battle ที่ไม่จบ;
   reload ไม่ทำให้สุ่มร้านใหม่
5. version validation, corrupt-save handling และ backup; เมื่อ run จบให้ล้าง active checkpoint
6. Settings: เสียง, ภาษา TH/EN (สลับทันทีผ่าน R3), ความเร็วเริ่มต้น
+ ไม่บันทึกสถานะกลางการต่อสู้: ถ้า reload ระหว่าง Battle ให้กลับไปที่ checkpoint ศาลาพักล่าสุด
  (battle seed อยู่ใน checkpoint จึงได้ battle เดิมซ้ำ) และมีเทสต์ยืนยันพฤติกรรมนี้
+ (เพิ่มใน Stage 12) Checkpoint ต้องรวมของที่เพิ่มหลังเขียน prompt นี้: snapshot ย่ามที่มี MergeCount และ variant id
  ต่อขั้นของแต่ละไอเทม (ใช้ BackpackSnapshot schema ปัจจุบัน ห้ามทำรูปแบบที่สองซ้ำ), สถานะร้าน (ช่องที่เช่าแล้ว, ช่องที่ล็อก,
  จำนวน refresh), และ run seed พร้อมสถานะ RNG ของสายร้าน (Xoshiro128 ยังไม่มี export/import ของ state ต้องทำก่อน
  ตาม llmwiki/gotchas/determinism-scope.md)
+ เทสต์: โหลด checkpoint แล้ว OfferMerge ได้ตัวเลือกชุดเดิม, Loadout ได้ค่าเดิม, ร้านสุ่มต่อได้ผลเดียวกับที่ไม่ได้ reload  

Tests: round-trip, schema เก่า (fixture), schema ใหม่กว่าที่รองรับ, config mismatch, payload ขาดหรือเสีย,
เขียนแข่งกัน, storage ใช้ไม่ได้

Acceptance (Playwright persistence.spec):
- refresh ที่ Camp แล้ว Resume ได้ร้าน ย่าม HP และเบี้ยตรง checkpoint
- refresh ระหว่าง Battle แล้วกลับ checkpoint ก่อนเข้าชั้น
- ปิด storage แล้วขึ้นเตือน ไม่รายงานว่าบันทึกสำเร็จ
- Save ไม่ serialize `IBattleSession` หรือ ECS world; เก็บเฉพาะ input ของ `BattleSetup` (seed / HP / ชั้น / loadout)
```

### Stage 13: Content complete และ UX

```text
ทำ Stage 13 เท่านั้น: Content Complete และ UX

งาน:
1. ตรวจเนื้อหาให้ครบตาม GDD (ไอเทมประมาณ 14, ผี 3 + บอสปอบ, 5 ชั้น) ไม่ขยายไปคลาสใหม่หรือ meta-progression
2. tutorial สั้น: ซื้อ → วาง → หมุน → อ่านคอมโบ → ออกเดินทาง
3. เสียงและเอฟเฟกต์ placeholder ที่มีสิทธิ์ใช้งาน, pause, audio settings
4. ใช้ localization key ทุกข้อความ; ทดสอบฟอนต์ไทยและข้อความล้นใน Web
5. ตรวจ license ของ asset ทั้งหมดและบันทึก attribution

Acceptance:
- ผู้เล่นใหม่เริ่ม Battle แรกได้โดยไม่ต้องมีคนอธิบาย (ทดสอบกับคนจริงอย่างน้อย 1 คน และบันทึกผล)
- ไม่มีข้อความผู้เล่นฝังในโค้ด
```

### Stage 14: Test harness และ CI

```text
ทำ Stage 14 เท่านั้น: Automated Verification และ CI

งาน:
1. tools/test.ps1 รวม dotnet, EditMode, PlayMode และ Web (-Suite) แล้วสรุป PASS / FAIL / NOT RUN
   ลง artifacts/test-summary.md
2. WebBridge เฉพาะ development/test build: ready handshake หลังโหลด config และ scene พร้อม;
   คำสั่งจำกัด: start seeded run, read snapshot, set test input, advance test ticks, export diagnostics;
   คำสั่งปกติเรียก use case เดียวกับ UI; คำสั่ง bypass (เช่น จบชั้นทันที) ตั้งชื่อ test-only
   และห้ามนับเป็น combat coverage
3. Playwright (tests/web/): smoke, persistence, run-loop; รอ ready signal ไม่ใช้ fixed sleep อย่างเดียว;
   ตรวจ console error และ unhandled exception; แยก UI interaction tests จริงออกจาก bridge-driven tests
4. เก็บ screenshot, trace, log, test XML/JSON และ build metadata ลง artifacts/
5. tools/ci.ps1: gen → validate → verify-generated → test → sim (50 seed, fail ถ้า winrate หลุดช่วง) →
   build → web tests
6. ไม่ให้ Unity สอง process ใช้ project directory เดียวกันพร้อมกัน; แยก job ที่ต้องใช้ license ออกจาก pure validation
7. ตรวจว่า release build ไม่มี harness API
+ tools/test.ps1 รายงานผล contract suite แยกบรรทัดสำหรับ Fake และ BattleSimulation
+ Web harness (dev build) expose phase/tick/endReason จาก IBattleSession ให้ Playwright ตรวจได้
+ (เพิ่มใน Stage 14) เริ่มจากสำรวจของเดิมก่อนแล้วต่อยอด ห้ามสร้างซ้ำ: tools/test.ps1 -Suite Fast|Full, tests/web/*.spec.ts
  (smoke, battle, camp, title, result) และเอกสารที่ player เผยผ่าน window.__projectSurvive* (พิกัดและสถานะ ไม่ใช่การตัดสินใจ)
  WebBridge ในข้อ 2 ให้เพิ่มเฉพาะส่วนที่ page globals ยังทำไม่ได้ (เช่น seeded start และ advance ticks)
  และต้องเรียก use case เดียวกับ UI
+ งานที่เหลือจริงของ Stage นี้คือ tools/ci.ps1, การพิสูจน์ว่า release build ไม่มี harness และ dev menu, artifacts,
  และรายงาน PASS / FAIL / NOT RUN รวม
+ Firefox: ADR 0003 ระบุว่ายังไม่ได้ติดตั้ง ให้ติดตั้งก่อนรันหรือรายงาน NOT RUN ตามจริง

Acceptance:
- ci.ps1 คืน exit code ≠ 0 เมื่อ required check ล้มเหลว
- report แยก PASS / FAIL / NOT RUN
- browser test ไม่ผ่านเพียงเพราะเจอ canvas
```

### Stage 15: Web release candidate

```text
ทำ Stage 15 เท่านั้น: Web Release Candidate

งาน:
1. profile บนเครื่องและ browser baseline ที่บันทึกไว้: load time, frame time, memory, allocation, entity count
   แยกสถานการณ์ปกติจาก stress test
2. แก้ bottleneck ที่มีหลักฐานก่อนเพิ่ม library; ถ้า initial download ใหญ่เกินงบจึงเพิ่ม Addressables
3. ตรวจ stripping และ AOT ใน release build (link.xml สำหรับ Google.Protobuf, MessagePack generated,
   VContainer, R3, Luban)
4. ตั้ง compression และ hosting headers ให้ตรงกับ output จริง; ทดสอบผ่าน HTTP ไม่ใช้ file://;
   ทดสอบ tab blur/focus, resize และ stale cache หลัง deploy build ใหม่
5. clean build จาก source และ toolchain ที่ล็อก; ทำ release checklist และ known issues; ปิด cheat และ harness
+ (เพิ่มใน Stage 15) ใช้นิยามขนาดของ ADR 0011 (initial payload วัด 3 ฐาน: ไม่บีบอัด / gzip / Brotli) และ profile ที่ Stage 03b
  แนะนำ (optimize-size) วัดใหม่ทั้งหมดเพราะเนื้อหาเพิ่มขึ้นมาตั้งแต่ Stage 08; ก่อนสรุปให้อ่าน build-report.json
  ตาม llmwiki/gotchas/webgl-size-profile-settings-residue.md เพื่อไม่ให้ setting ค้างจาก profile อื่น

Acceptance:
- release build เปิดได้บน browser ที่ประกาศรองรับ
- required tests ผ่านจริง
- มี performance report พร้อมสเปกเครื่องและ build hash เทียบกับงบใน GDD
- ไม่อ้างตัวเลขที่ไม่ได้วัด

```

### Stage 16 ขึ้นไป: เพิ่มเนื้อหาหลัง Prototype

ใช้ prompt นี้ซ้ำได้เรื่อยๆ

```text
ทำ Stage ถัดไปจาก baseline ปัจจุบัน โดยเปลี่ยนเฉพาะ feature ที่ระบุ

Feature: <เช่น กระหัง / ด่านใหม่ / ระดับความยากขั้นกลาง / EXP และพร / ไอเทม 10 ชิ้นสายน้ำมนต์>
เหตุผลหรือปัญหาที่ต้องแก้: <หลักฐานจาก playtest หรือ profiling>

ก่อนลงมือ:
- อ่าน baseline และรายงาน Stage ล่าสุด
- ระบุผลต่อ Domain, Data, Save, UI, Simulation และ Web performance
- เสนอ acceptance criteria และ regression tests
- ระบุว่าต้อง migrate save หรือ config หรือไม่
- ห้ามรวม refactor ใหญ่ที่ไม่จำเป็นกับ feature นี้

ลำดับบังคับ: แก้ data/ → gen → validate → เทสต์ Core → sim เทียบบาลานซ์กับรอบก่อน → Presentation →
ci.ps1 → อัปเดตเอกสาร
ห้ามเพิ่มกฎเกมฝั่ง Presentation

หลังลงมือ: รายงานผลที่วัดได้จริง และ PASS / FAIL / NOT RUN
```


## Stage 17a — โมดูล Spine + โหลด skeleton ผู้เล่น (ยังไม่แตะ Scene)

ทำ Stage 17a เท่านั้น: นำโมดูล Spine actor (SpineActorBase / SpineWorldActor / SpineUiActor /
SpineSkeletonGraphic / SpineActorFactory ที่อยู่ใน workspace แล้ว) เข้ามาอยู่ในสถาปัตยกรรมของโปรเจค
และเตรียม skeleton ของพ่อหมอจาก Resources/body_1110901/body_1110901_SkeletonData.asset
ขอบเขต: Presentation + Editor tool + tests เท่านั้น ห้ามแก้ lib/ (Domain/Application/Simulation), data/,
EcsLite, localization และห้ามแก้ Scene (Battle/Camp/Title) ในสเตจนี้ — การต่อเข้าฉากเป็น 17b–17d

บริบท: ไฟล์ Spine มาจากโปรเจคอื่น (namespace MyClone.SpineSystem) เขียนสำหรับ spine-unity 3.8
ยังไม่เคยผ่าน Release WebGL (IL2CPP stripping) และยังไม่รู้ชื่อ animation / skin ของ skeleton นี้ ห้ามเดา

A. สำรวจก่อนแก้ → docs/stages/17a-report.md (สั้น)
   - ไฟล์ Spine ใน workspace อยู่ที่ไหน, มี asmdef หรือไม่, ชื่อ asmdef ของ spine runtime ที่ติดตั้งจริง
     และ version ของ spine-unity ที่ติดตั้ง เทียบกับ version ที่ export skeleton (ต้องตรงกัน major.minor
     ไม่ตรงให้หยุดและรายงาน blocker ห้ามอัปเกรด/ดาวน์เกรดเอง — AGENTS ข้อ 2)
   - ไฟล์ .asset อยู่ใน Resources โฟลเดอร์ไหนจริง; Resources path ที่ใช้โหลดคือ "body_1110901/body_1110901_SkeletonData"
     (ไม่มีนามสกุล) ยืนยันว่าโหลดได้
   - shader ของ Spine (Spine/Skeleton, Spine/SkeletonGraphic) ทำงานใน URP 17.3 ของโปรเจคนี้หรือไม่
     (ดูจากการ render จริงใน Editor ไม่ใช่เดา)
   - สิทธิ์ใช้งาน: (1) Spine Runtimes License ต้องมี Spine Editor license; (2) asset body_1110901 มาจากไหน
     มีสิทธิ์ใช้หรือไม่ (AGENTS ข้อ 13) ถ้ายืนยันไม่ได้ ให้ลงเป็น blocker ใน ATTRIBUTION.md และห้ามให้ asset นี้
     เข้า Release build จนกว่าจะยืนยัน (ทำงานใน dev ได้)

B. ปรับโมดูลให้เข้ากับโปรเจค (ย้ายไป Assets/Main/Scripts/Presentation/SpineActors/)
   1. namespace = ProjectSurvive.Game.Presentation.SpineActors
      ห้ามใช้ namespace ที่ลงท้าย .Spine (จะบัง namespace `Spine` ของ runtime — บทเรียนเดียวกับ ...Game.Result
      ใน llmwiki/systems/loop-and-scopes.md §5)
   2. แก้ asmdef ของ Presentation / Editor / Tests.EditMode ให้อ้าง spine runtime (ใช้ชื่อที่ตรวจจริงใน A)
      ห้ามให้ Presentation อ้าง ProjectSurvive.Core.Simulation (architecture test เดิมต้องผ่าน)
   3. ห้ามใช้ Shader.Find ในโค้ดที่ ship (คืน null ใน player build ที่ถูก strip — เป็นบั๊กชนิด Editor เขียว/player พัง
      แบบเดียวกับ il2cpp-di-and-reflection.md): materials/shader ต้องเป็น serialized reference
      (SpineUiActor.SetMaterials, SpineWorldActor.SetGrayShader) และถ้าไม่ได้ assign ให้ log error ชัดเจน
      ไม่ fallback เงียบๆ
   4. ลบคอมเมนต์ที่อ้างถึงเกม/โปรเจคต้นทาง (MYCLONE_DESIGN.md ฯลฯ) และเขียนว่า actor เหล่านี้เป็น
      Presentation ล้วน ไม่ตัดสินกฎใดๆ (AGENTS ข้อ 10)
   5. ห้ามพึ่ง Time.timeScale: การหยุด/ความเร็วของ Spine ใช้ Pause()/SetSpeed() ของ actor เท่านั้น
      (หมายเหตุ: Play(...) รีเซ็ต TimeScale เป็นค่า speed ที่ส่งเข้าไป ผู้เรียกต้องส่งค่าปัจจุบันทุกครั้ง)

C. ส่วนเฉพาะผู้เล่น (ไม่มี logic เกม)
   1. SpineAssetPaths.PlayerSkeletonData + SpineSkeletonLoader.TryLoad(path, out asset, out errorCode)
      รหัสผิดพลาดรูปแบบ noun.problem: spine.skeleton_path_empty / spine.skeleton_missing / spine.skeleton_invalid
      (ตรวจจาก SkeletonDataAsset.GetSkeletonData ว่า null ไหม) ห้ามใช้ Result<T> ถ้ายังไม่ได้ตรวจ API จริงของ Shared
   2. SpinePlayerProfile : ScriptableObject (Assets/Main/Spine/SpinePlayerProfile.asset): resource path,
      ชื่อ animation (idle, run จำเป็น; hurt, death ไม่บังคับ), skinParts (body/head/face — skeleton นี้เก็บหน้าตา
      หลาย part skin ดู SpineActorBase.SkinPart/DetectSkinGroups), targetHeightWorld, runSpeedThreshold,
      artFacesRight และเมธอด TryValidate(SkeletonData, out code, out detail) ที่ล้มดังๆ เมื่อชื่อไม่มีจริง
      ค่าพวกนี้เป็นค่าฝั่งภาพ ไม่ใช่ balance (ไม่ลง data/) แต่ชื่อ animation/skin ห้ามฝังเป็น string ในโค้ด
   3. PlayerPose {Idle, Run, Dead} + PlayerPoseSelector (static, pure, ไม่พึ่ง Spine/Unity): Select(speed, current,
      dead, threshold) มี hysteresis (เข้า Run ที่ threshold ออกที่ครึ่งหนึ่ง) และ Facing(dx, current, deadZone)
   4. SpinePlayerView : MonoBehaviour (หุ้ม SpineWorldActor — สร้างผ่าน SpineActorFactory ตอน Begin() ครั้งเดียว)
      API: Configure(profile), bool Begin() (idempotent, ErrorCode เมื่อล้ม), Present(PlayerPose, int facing),
      PlayHurt() (one-shot แล้วกลับ pose ปัจจุบันเมื่อ AnimationComplete — ห้ามเรียก Play/SetAnimation
      ซ้อนใน callback ให้ตั้งธงแล้วทำใน LateUpdate), SetPaused(bool), SetSpeed(float), SetSortingOrder(int),
      Tick(float) สำหรับเทสต์, ตัวนับ PlayCalls/BuildCount
      - Present ต้อง allocation เป็น 0 และเรียก Play เฉพาะตอน pose เปลี่ยน (เทียบค่าเดิมก่อน)
      - scale ให้พอดี targetHeightWorld โดยวัดความสูงจาก MeshFilter.sharedMesh.bounds ของ SkeletonAnimation
        แล้วตั้ง scale ที่ root ของ actor เท่านั้น (ห้ามตั้งที่ SkeletonTransform เพราะ ApplyFlip ใช้ localScale ตรงนั้น)
      - Begin() ล้มเมื่อ bounds ว่าง / animation หาย / skin ไม่ render (ไม่ fallback เงียบ)

D. Editor tool (เมนู ProjectSurvive/Spine/..., ใช้ Editor API เท่านั้น — AGENTS ข้อ 11)
   1. Dump player skeleton → artifacts/spine/player-skeleton.md: spine version, scale, bones/slots,
      ทุก animation + duration, ทุก skin + จำนวน attachment, atlas pages, shader ของ material
   2. Create player profile: เติมชื่อ animation จากข้อมูลจริงของ skeleton (จับคู่ด้วยคีย์เวิร์ด idle/stand,
      run/walk, hurt/hit, death/die) และเลือก skin part ต่อกลุ่ม แล้ว log ว่าเลือกอะไร; ถ้าหา idle หรือ run ไม่เจอ
      ให้ล้มพร้อมรายชื่อทั้งหมด ไม่สร้าง asset; ถ้ามี asset อยู่แล้วไม่เขียนทับ
   3. Create UI materials (Spine/SkeletonGraphic normal + TintBlack) ลง Assets/Main/Spine/Materials
      (Shader.Find ใช้ได้ใน Editor tool เท่านั้น)

E. Tests (EditMode; ทดสอบกับ asset จริง ไม่ใช้ stub)
   - selector: dead ชนะ speed, hysteresis, facing dead zone
   - loader: path ว่าง/ไม่มี → รหัสผิดพลาดถูกต้อง; asset ผู้เล่นโหลดได้จริง
   - profile ที่สร้างผ่าน tool ผ่าน TryValidate กับ skeleton จริง
   - SpinePlayerView.Begin() สำเร็จ; Present pose เดิม 10 000 ครั้ง → PlayCalls = 1 และ 0 bytes allocated;
     PlayHurt → Tick จน animation จบ → กลับ pose เดิม
   - เทสต์ architecture เดิมต้องผ่าน (Presentation ไม่อ้าง Simulation, ไม่มีข้อความไทยฝังในโค้ด)
   ถ้า API ที่ใช้ (เช่น GC.GetAllocatedBytesForCurrentThread ใน Unity Mono) ใช้ไม่ได้ ให้รายงานและเปลี่ยนวิธีวัด

F. เอกสาร
   - ADR 0037 (ทางเลือกจริง ≥ 2: โหลด Resources vs reference ใน scene; Shader.Find vs serialized material;
     Spine ขับ clock เอง vs ตาม Pause/Speed ของ runner) บันทึกผลสำรวจใน A
   - docs/architecture/dependencies.md: เพิ่มแถว Spine runtime (Verified เฉพาะที่ตรวจจริง, ที่เหลือ Unverified)
   - ATTRIBUTION.md: Spine runtime + asset body_1110901 พร้อมสถานะสิทธิ์
   - llmwiki/stage-log.md 3 บรรทัด + แถวใน index.md ถ้ามีโน้ตใหม่ (INGEST.md)

กติกาเพิ่ม: Resources ทุกอย่างถูก ship (unity-shell.md) — ห้ามสรุปว่าขนาดผ่านโดยไม่วัด; ถ้าจะรายงานขนาดต้องวัดด้วย
build-web.ps1 -ReleaseCandidate และ measure-web-size.mjs ตามฐานของ ADR 0011/0035 ไม่เช่นนั้น NOT RUN
commit แยก 2 อัน: `generic: spine actors (presentation)` สำหรับโมดูลที่ไม่อ้าง type ของเกม (AGENTS ข้อ 14)
และ `stage-17a: ...` สำหรับส่วนผู้เล่น/tool/test/เอกสาร

รัน: tools/test.ps1 -Suite Fast; ห้าม build WebGL/Playwright ในสเตจนี้ (NOT RUN)
Acceptance: โมดูล compile ใน Unity; tool ทั้งสามทำงานกับ asset จริง; เทสต์ข้างต้นผ่าน; Fast suite ไม่แดง;
ไม่มี Shader.Find ในโค้ด runtime (grep เป็นหลักฐาน); รายงานสั้น PASS / FAIL / NOT RUN

## Stage 17b — พ่อหมอใน Battle (ต่อเข้า VContainer / frame / events)

ทำ Stage 17b เท่านั้น: ใช้ SpinePlayerView เป็นภาพของพ่อหมอใน Battle scene (ต่อจาก 17a)
ห้ามแก้ lib/, data/, Simulation หรือ contract; Presentation อ่านผ่าน IBattleFrame / IBattleEventSource เท่านั้น

เริ่มจากสำรวจ (ลง report): EntityView / EntityViewPool / BattleRenderBridge วาดผู้เล่นอย่างไร,
BattleSceneScope ลงทะเบียนอะไร, BattleWorldMap.SortingOrder, BattleCameraRig.BillboardPitch, ช่องทาง
BattleEventPump (HUD/VFX/Audio), BattleSimulationRunner.SetPaused / BattleSpeed, report __projectSurviveBattle

1. สร้าง SpinePlayerView ตอนเริ่มชั้น (warm-up) — ห้ามสร้างหลัง warm-up; EntityViewPool.CreatedAfterWarmup
   ต้องยังเป็น 0 และ viewsGrownDuringPlay = 0 (actor ไม่อยู่ใน pool ให้ตัวนับของ Spine แยก)
2. ผู้เล่นใน frame ยังคงเป็น entity เดิม: ความเร็ว = (Cur − Prev) × TickRate จาก BattleFrameSnapshot
   → PlayerPoseSelector; ทิศหันจาก dx; Dead จาก phase Lost; sorting จาก BattleWorldMap.SortingOrder
   (ส่งเฉพาะเมื่อค่าเปลี่ยน); ใช้ billboard pitch เดียวกับ sprite อื่น (ตรวจจากโค้ด ห้ามเดาเครื่องหมาย)
3. Hurt มาจาก event PlayerDamaged ผ่าน listener ของช่องที่เหมาะสม (แบบเดียวกับ BattleAudioListener)
   Presentation ห้ามใช้ event ตัดสินกฎ
4. Pause/ความเร็ว: BattleSimulationRunner.SetPaused → SpinePlayerView.SetPaused, BattleSpeed → SetSpeed
   เรียกเมื่อค่าเปลี่ยน ไม่ใช่ทุกเฟรม; ห้ามแตะ Time.timeScale
5. DI: ลงทะเบียนใน BattleSceneScope ด้วย factory lambda เท่านั้น (ห้าม Register<TInterface,TImpl>(Lifetime) แบบ
   reflect constructor — IL2CPP strip); ไม่สร้าง interface/abstraction ใหม่ถ้าไม่จำเป็น (AGENTS ข้อ 12)
6. ถ้า Begin() ล้ม: dev build ล้มดังๆ (log + แสดงใน report) ส่วน Release ให้ใช้ placeholder sprite เดิม และต้อง
   ระบุใน report ว่าเกิด fallback — ห้ามเงียบ
7. สร้างฉากผ่าน ProjectSurvive/Stage 08/3 (BattleSceneBuilder) ไม่แก้ YAML; เพิ่มฟิลด์ spine ใน
   __projectSurviveBattle (spineReady, spineErrorCode, spinePlayCalls, spineAnimation) และเพิ่มเทสต์เบราว์เซอร์

Tests: PlayMode (BattlePlayModeTests): Begin สำเร็จใน scene จริงและ reference ใน BattleViewSettings ถูก assign
(บทเรียน 08c — ตรวจ wiring ของ scene), เดิน → Run, หยุด → Idle, โดนตี → hurt แล้วกลับ, pause → animation หยุด,
10 วินาทีไม่มี exception, CreatedAfterWarmup/viewsGrownDuringPlay = 0, PlayCalls ไม่โตตามจำนวนเฟรม
Editor capture ชั้น 1 ที่ 1920×1080 (ดูภาพจริง ห้ามประกาศ PASS ด้านภาพจากเทสต์อย่างเดียว)
ปิดสเตจ: -Suite Full ตอนปิด Editor (WebGL Development + Playwright battle spec); ต่อด้วย build -ReleaseCandidate
เพื่อพิสูจน์ stripping (Spine shader/serialization) ถ้ารันไม่ได้ให้ NOT RUN ตามจริง และรายงานขนาด payload เทียบ ADR 0035
Acceptance: เล่นชั้น 1 ได้ด้วย Spine; บันทึก PASS / FAIL / NOT RUN

## Stage 17c — ตัวละครใน Camp (สั้น)

ทำ Stage 17c เท่านั้น: ใช้ SpinePlayerView แทนภาพตัวละครใน Camp (CampPlayerView) — Presentation เท่านั้น
สำรวจ CampPlayerView/CampWorldView/CampSceneBuilder ก่อน; Run/Idle จากการเดิน WASD ที่ view มีอยู่แล้ว
(ไม่คำนวณกฎซ้ำ), หันตามทิศเดิน, ไม่มี hurt/dead; หยุดเดินตอนหน้าจัดย่ามเปิด → Idle; Begin() ตอนเข้าฉาก
สร้างฉากผ่าน CampSceneBuilder; ห้ามกระทบ __projectSurviveCamp / geometry report (พิกัด figure ต้องยังถูก)
Tests: CampWorldPlayModeTests (เดิน → pose Run, หยุด → Idle), เทสต์เดิมของ Camp ผ่านทีละ fixture,
Editor capture 1920×1080 + 1280×720 ไม่มี element ทับ, tests/web/camp.spec.ts (ถ้ารันได้) รายงาน PASS / FAIL / NOT RUN

Stage 17d — รูปตัวละครใน HUD (SpineUiActor)

ทำ Stage 17d เท่านั้น: แทน placeholder รูปตัวละครในพาเนลสถานะซ้ายล่างของ Battle HUD ด้วย SpineUiActor
(SkeletonGraphic) ผ่านวัสดุจาก tool 17a (SetMaterials) ห้ามใช้ Shader.Find
View ไม่มี logic: แสดง Idle วนอย่างเดียว (หรือตาม hurt event ถ้าทำได้ผ่านช่องทางเดิม); anchor ผูกขอบ canvas
ต้องไม่ทับหลอด HP/MP ที่ 1920×1080 และ 1280×720 (UiLayoutCheck: เพิ่ม region ของ portrait),
เปิด Canvas additionalShaderChannels เฉพาะเมื่อ tintBlack; ตรวจ __projectSurviveTextFit ยังผ่าน
Tests: EditMode layout, PlayMode (actor built, CreatedAfterBuild = 0), Editor capture 2 ขนาด
Acceptance: ภาพจริงไม่ทับกัน, เทสต์ผ่าน, รายงาน PASS / FAIL / NOT RUN

## Test harness และ Performance budget

ไม่พึ่งการให้ agent ลองเล่นเอง แบ่งการทดสอบเป็น 4 ระดับ และใช้ Playwright กับ Web build จริงเป็นด่านสุดท้าย ชุด Web test ตามแบบ Astra แต่เริ่มเร็วกว่าที่ Opus วางไว้ (Opus มี Playwright smoke เฉพาะ Stage 14)

### 4 ระดับ

| ระดับ | ทดสอบอะไร | เครื่องมือ | เริ่มที่ |
| --- | --- | --- | --- |
| Domain | ย่าม หมุน รวม คอมโบ ราคา ซื้อขาย | xUnit ผ่าน `dotnet test` | Stage 01–06 |
| Simulation | spawn cooldown hit damage ผล Battle และ bot sim | xUnit และ Sim console | Stage 07 |
| Unity integration | DI, Scene flow, UI, pooling, disposal | PlayMode | Stage 03 และ 08–10 |
| Browser | โหลด Web, input, AOT, save/reload, hosting | Playwright กับ Web build | Stage 03 (smoke), 12 (persistence), 14 (เต็มชุด) |

### กติกาของ Playwright

- ทดสอบผ่าน HTTP server (`tools/serve-web.mjs`) ไม่ใช้ `file://`
- รอ ready handshake ไม่ใช้ fixed sleep อย่างเดียว
- ตรวจ console error และ unhandled exception ทุกเทสต์ ไม่ผ่านเพียงเพราะเจอ canvas
- แยก UI interaction tests จริง (เมาส์ keyboard focus) ออกจาก bridge-driven tests
- bridge มีเฉพาะ development/test build (ตัวอย่างจาก Opus: `window.__ps` มี `ready`, `state()`, `cmd()`, `fps()`) คำสั่งปกติเรียก use case เดียวกับ UI ส่วนคำสั่ง bypass ตั้งชื่อ test-only และห้ามนับเป็น combat coverage
- release build ต้องไม่มี harness และมีเทสต์ยืนยัน
- เก็บ screenshot, trace, log ของเทสต์ที่ล้มลง `artifacts/`

สถานการณ์ Web ที่ต้องผ่าน:

- boot ถึงเมนู
- เริ่ม run ถึงศาลาพัก
- ซื้อและวางไอเทมผ่าน UI
- เข้า Battle และเดินได้
- ผ่านชั้นแล้วกลับศาลาพัก
- reload แล้ว resume
- Victory และ Defeat
- release build ไม่มี harness

### กรณีทดสอบที่ต้องมีตั้งแต่ต้น

| Scenario | ผลที่ต้องได้ |
| --- | --- |
| ซื้อไอเทมตอนหีบเต็ม | เงินและสินค้าไม่เปลี่ยน |
| หมุนของแล้ววางไม่ได้ | ของกลับตำแหน่งเดิม |
| ย้ายย่ามจนของไม่มีพื้นที่รองรับ | ปฏิเสธทั้งคำสั่ง |
| กดซื้อช่องเดียวสองครั้ง | ซื้อได้ครั้งเดียว |
| ส่ง FloorCompleted ซ้ำ | รับรางวัลครั้งเดียว |
| กลับ Title แล้วเริ่มใหม่ | ไม่ใช้ seed เงิน ไอเทมของรอบก่อน |
| เปิดร้านซ้ำ | ไม่เปลี่ยนสินค้า |
| reload ที่ศาลาพัก | ร้าน ย่าม HP และเงินตรง checkpoint |
| reload ระหว่าง Battle | เริ่มจาก checkpoint ก่อนชั้น ไม่รับรางวัลบางส่วน |
| เปลี่ยน Scene ซ้ำหลายรอบ | จำนวน scope และ subscription ไม่โตสะสม |
| ปิด storage ของ browser | แจ้งว่าบันทึกไม่ได้ ไม่แสดงว่าสำเร็จ |

### งบ performance (เป้าที่จะนำไปวัด)

| รายการ | เป้าตั้งต้น |
| --- | --- |
| หน้าจอทดสอบ | Desktop 1920×1080 |
| Browser | Chromium และ Firefox บันทึก version ที่ทดสอบ |
| Frame time | p95 ≤ 20 ms ในชั้นปกติ |
| Stress case | ประมาณ 300 ศัตรู + 500 กระสุน |
| Memory | ราว 350 MB สำหรับ Unity/Wasm heap วัด browser และ GPU แยก |
| Initial download | ไม่เกิน 30 MB หลังบีบอัด |
| Allocation ใน hot loop | ใกล้ศูนย์หลัง warm-up (simulation และ render bridge) |
| Scene loop | Camp ↔ Battle 20 รอบแล้วไม่มีการเติบโตต่อเนื่องแบบผิดปกติ |

ตัวเลขเหล่านี้ยังไม่ใช่ประสิทธิภาพที่รับรองแล้ว ตัดสินจาก browser จริง ไม่ใช้ FPS ใน Editor เมื่องบนี้ผ่านแล้วค่อยขยับไปเป้าระยะถัดไปของ Opus: 1,000 ศัตรูที่ ≥ 55 FPS, โหลดถึง Title ≤ 10 วินาที, initial download ≤ 25 MB

## สิ่งที่ต้องตัดสินใจและยังไม่ยืนยัน

ก่อนเริ่ม Stage 00 มี 7 เรื่องที่ผมเลือกค่าตั้งต้นไว้แทนคุณ ถ้าไม่เห็นด้วยให้แก้ก่อน เพราะกระทบ data และ Stage ถัดไป

### ค่าตั้งต้นที่ผมเลือก

| เรื่อง | ค่าตั้งต้นในเอกสารนี้ | ทางเลือกอื่น |
| --- | --- | --- |
| จำนวนชั้นใน Prototype | 5 ชั้น (ปกติ 4 + บอส) | 8 ชั้นตาม Opus |
| บอส Prototype | ปอบ 2 เฟส | นางนาค 3 เฟสตาม GDD เดิม |
| EXP และพรตอนเลเวลอัป | ไม่ทำ | ทำตาม Opus (พรเป็นบัฟเล็ก ไม่หยุดจัดย่าม) |
| ย้ายย่ามที่มีของวางอยู่ | ปฏิเสธถ้าของหลุดพื้นที่ใช้งาน | ให้ของย้ายตามย่ามแบบ Opus |
| รูปแบบ save | MessagePack | Protobuf แบบ Opus |
| MP ตอนเข้าชั้น | เต็มทุกครั้ง | คงเหลือต่อเนื่องข้ามชั้น |
| กล้อง | ตัดสินใน Stage 08 ด้วยการทดลอง | ล็อก orthographic หรือ perspective ตั้งแต่ต้น |

### ยังไม่ยืนยัน

- รายละเอียดหน้าศาลาพักเทียบกับภาพอ้างอิงจริง หน้า Steam ยืนยันได้เฉพาะโครงใหญ่
- version ของ VContainer, UniTask, R3, MessagePack, Luban, EcsLite ที่ใช้ร่วมกับ Unity 6.3 (Stage 00 ตรวจ)
- พฤติกรรม IL2CPP stripping และ AOT บน Web ของ MessagePack, Protobuf, R3, VContainer (Stage 03 ทดสอบ)
- วิธี flush ลง IndexedDB ที่ Unity 6.3 รองรับ (Stage 12 ตรวจเอกสารและพิสูจน์ด้วย reload)
- package ที่ตั้งใจใช้เป็น UI.MVP Lite ถ้ามี repository ให้ส่งให้ agent ตรวจ
- ผลเลือก UI tech สำหรับภาษาไทย (uGUI+TMP หรือ UI Toolkit) จาก spike ใน Stage 03
- ตัวเลข performance ทุกตัวในเอกสารนี้ยังไม่เคยวัด
