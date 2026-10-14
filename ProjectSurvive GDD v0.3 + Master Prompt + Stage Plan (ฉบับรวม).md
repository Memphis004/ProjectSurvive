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

Acceptance:
- ซื้อ → วาง → หมุน → รวม → ถวายคืน → คืนสภาพ ผ่าน UI แล้ว state ตรงกับ Core
- invalid drop ไม่ทำของหาย
- ไม่มี GC spike ระหว่างลาก (ดูจาก Profiler)
- Playwright ทดสอบเมาส์และ keyboard focus ใน Web จริง
```

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
