# Directional Evade และ R5 Zebraceratops

สถานะ: implementation/build ผ่าน, รอ field test

## 1. เจ้าของข้อมูลทิศทาง

Directional Evade ใช้ `DamageDirection` ซึ่งคำนวณจากตำแหน่งผู้โจมตีเทียบกับ
yaw ของสัตว์ตอน hit resolution ไม่ใช้ `BodyPart`:

- `BodyPart` บอกชิ้นส่วนที่รับ damage
- `DamageDirection` บอกว่าผู้โจมตีอยู่ Front/Back/Left/Right

การแยกสองข้อมูลนี้ทำให้การเปลี่ยน body-part damage ratio ในอนาคตไม่เปลี่ยน
ทิศหลบโดยไม่ตั้งใจ

## 2. Route matrix

| ทิศที่รับการโจมตี | ทางหลบที่เลือกแบบ deterministic random |
|---|---|
| Front | Left หรือ Right |
| Back | Left หรือ Right |
| Left | Forward หรือ Backward |
| Right | Forward หรือ Backward |

แต่ละคู่เลือก 50/50 ด้วย random stream ประจำ entity เดิม ก่อนเริ่ม Evade สัตว์จะ
snap yaw หันหา player ทันที แล้วค่อยเล่น route ที่เลือกจากทิศเดิม การ Dodge ยัง
สำเร็จระหว่าง active attack ได้ตาม damage result แต่จะไม่ snap yaw หรือเล่น Evade
แทรก active attack เพราะจะทำให้ trajectory ของ action ที่ commit แล้วเปลี่ยนกลางท่า

## 3. Root-motion projection

Evade clip เดิมทั้งสาม framework เคลื่อนฐานไปทาง local rear เป็นหลัก:

| Motion | Duration | final local travel โดยประมาณ |
|---|---:|---:|
| `Tricera_Evade` | 1.00s | (0, -307.67) |
| `Phenaco_Evade` | 1.27s | (0, -232.75) |
| `Raptor_Evade` | 1.00s | (18.85, -140.84) |

runtime จึงคำนวณมุมของ final local travel แล้วหมุนเฉพาะ projection basis ให้ไปยัง
route ที่เลือก ไม่หมุน yaw ของสัตว์ตามทิศเคลื่อนที่ และยังผ่าน collision processing
เส้นทางเดียวกับ root motion อื่น

## 4. R5 Zebraceratops execution boundary

Entity type `2027` เป็น species แรกที่เปลี่ยนจาก shadow decision เป็น intent
execution จริง:

| Context | Intent/action |
|---|---|
| Front, ใกล้ | `StandardFront`: `tricera_head` / `tricera_once` |
| Rear (`135–180°`), Bow/Crossbow attack ภายใน 1.5s, surface distance `0–420` | `TurnAttack`: `tricera_turn` โอกาส 80%; ไม่ติดให้หันกลับ Front |
| LeftFlank / RightFlank (`45–135°`) | หันกลับ Front เท่านั้น ไม่ใช้ `tricera_turn` |
| Front, ไกล, path ไม่ blocked | `GapCloser`: `tricera_dash` |
| CounterAttack | `tricera_counter` ยังปิดไว้เพราะยังไม่มี trigger ที่ยืนยันได้ |

กฎสำคัญ:

- `TurnAttack` commit ด้วย facing เดิม แล้วให้ original root position/yaw หมุนตัว
- `tricera_turn` ไม่ใช่ positional attack ทั่วไป: ต้องมี hit resolution จาก action
  `ranged_bow_*` หรือ `ranged_crossbow_*` ใน engagement เดียวกันภายใน 1.5 วินาที
- Rear ใช้ deterministic activation roll 80% และเช็ค surface distance ไม่เกิน
  420 หน่วย; ถ้า event/ระยะไม่ผ่านหรือ roll ไม่ผ่าน จะล็อก Reposition จนหันเข้า
  Front สำเร็จ ไม่สุ่ม `tricera_turn` ใหม่ทุก update frame
- Flank ไม่เข้า rule ของ `tricera_turn` และใช้ Reposition โดยตรง
- Standard/GapCloser/Counter หันหา target ก่อน commit
- ไม่ใช้ Player Miss หรือ Zebra Dodge เป็น trigger ของ counter แล้ว; rule ถูก
  audit-block ด้วย `counter-trigger-unconfirmed` แทนการเดา trigger ใหม่
- ระหว่าง cooldown Zebraceratops ไม่ track yaw ของ player ตลอดเวลา เพื่อรักษา
  flank/rear context ให้ `tricera_turn` มีโอกาสทำงาน
- ถ้า flank/rear อยู่นอกระยะ turn จะทำ alignment/reposition ก่อน ไม่สุ่มข้าม intent
- Elephantulus ใช้ R6 intent execution แล้ว; Deinonychus ยังใช้ selector ที่ผ่าน
  field test เดิม

## 5. Tricera rotate motion

framework เดิมกำหนด `Tricera_Rotate_CW`, `Tricera_Rotate_CCW`,
`rot_speed = 150°/s` และ `rot_playback_rate = 1` ไว้แล้ว คลิปทั้งคู่ยาว 1 วินาที
และเป็น in-place motion ดังนั้น runtime ใช้ animation กับการหมุนฐานแบบเดิมพร้อมกัน:

- yaw error มากกว่า 45° (Flank/Rear): เล่น `Rotate_CW` เมื่อ signed yaw เป็นบวก และ
  `Rotate_CCW` เมื่อเป็นลบ
- ไม่เกิน 45° (Front): ใช้ BattleStand แต่ `TurnToYaw` ยังหมุนฐานต่อด้วย 150°/s
- `AnimalBehavior` หยุดหมุนจริงเมื่อ error ต่ำกว่า 1°; controller อนุญาต commit
  เมื่อไม่เกิน face tolerance 8°
- เฉพาะ Rotate loop จะเก็บ local root yaw ของ animation เพื่อไม่ให้
  `RootMotionMovable` ลบท่าหมุนทิ้ง แต่ยังชดเชย root position ตามเดิม
- ทุก handoff จาก Rotate กลับ Stand/Move/Attack จะ reset visual root offset และ
  คืน yaw-compensation ปกติ เพื่อให้วงเป้าหมายกับ `CurrentPosition` อยู่จุดเดียวกัน

ในแกนของ Unity ค่า yaw บวกคือ clockwise เมื่อมองจากด้านบน จึงจับคู่
`+DeltaAngle = CW`, `-DeltaAngle = CCW`

## 6. Field-test checklist

1. โจมตี Zebra จากหน้าและหลังจนเกิด Dodge: ตัวต้อง snap หันหา player ทันที
   แล้วหลบซ้ายหรือขวา
2. โจมตีจากด้านข้างจนเกิด Dodge: ตัวต้องหลบหน้า หรือหลัง
3. เดินอ้อม Zebra ระหว่าง cooldown ไปด้านข้าง: ต้องเล่น Rotate_CW/CCW ตาม
   ทิศสั้นสุดและหันกลับ Front โดยไม่ใช้ `tricera_turn`
4. อยู่ด้านหลังโดยไม่ยิง Bow/Crossbow: ต้องไม่ใช้ `tricera_turn`
5. ยิง Bow/Crossbow จากด้านหลังภายใน surface distance 420: `tricera_turn` ต้องเกิด
   ประมาณ 80%; กรณีที่เหลือต้องหันกลับ Front และเส้น/ตัว/hit ต้องใช้ trajectory เดียวกัน
6. ยิง Bow/Crossbow จากด้านหลังเกิน surface distance 420: ต้องไม่ใช้
   `tricera_turn` และต้องเข้าการ approach/reposition ตามปกติ
7. อยู่ด้านหน้าใกล้: ต้องไม่สุ่ม `turn`, `counter` หรือ `dash`
8. อยู่ด้านหน้าไกล: `tricera_dash` ต้องทำงานเมื่อ path ไม่ถูก block
9. เมื่อ player attack เป็น Miss หรือ Zebra Dodge: `tricera_counter` ต้องไม่ถูก
   เรียกจากสอง event นี้
10. ตรวจ regression Elephantulus/Deinonychus ว่ายังไม่มี flash/warp
11. ลอง Dodge ระหว่าง active attack: damage result ยังเป็น Dodge แต่ต้องไม่ snap
   หันหรือขัดจังหวะ action ที่กำลังเล่น
