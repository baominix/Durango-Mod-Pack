# R7 — Raptor 2001 intent execution

สถานะ: **implementation/build ผ่าน, รอ field test** บนฐาน
`DurangoCombatSystemPlugin 0.3.9`

## ขอบเขต

R7 เปิด entity type `2001 raptor` ใน combat runtime เป็นชนิดที่สี่ โดยใช้
Framework `Raptor` ร่วมกับ `2039 deinonychus_savana` แต่ไม่แชร์ species profile,
intent rules หรือน้ำหนักของ action

ข้อมูลต้นฉบับของ `2001` ที่ใช้:

| Field | ค่า |
|---|---:|
| model | `Raptor/RaptorPrefab` |
| AI factor id | `raptor_ai` |
| root-motion id | `raptor` |
| bound radius | 50 |
| represent scale | 2.2 |
| attack cooldown | 1.7s |

## Intent ที่เปิดใช้

| Context | Intent/action | Surface range | Weight | หลักฐาน |
|---|---|---:|---:|---|
| Front ใกล้ | `StandardFront` / `raptor_attack` | 0–484 | 6 | Original geometry; range Reconstructed |
| Front กลาง–ไกล | `GapCloser` / `raptor_jump` | 264–946 | 4 | Original root/geometry; range Reconstructed |
| Front กลาง | `GapCloser` / `raptor_dash` | 396–748 | 2 | Original rear arc/root yaw; trigger Reconstructed |

ระยะ R7 เป็น distance band เดิมของ Raptor framework ที่คูณ
`represent_scale 2.2` และเก็บ provenance เป็น Reconstructed ส่วน motion, hit frame,
พื้นที่และ root transform ยังคงอ่านจากข้อมูล Original

ยังไม่เปิดสอง action ต่อไปนี้:

- `raptor_counter`: motion/geometry พร้อม แต่ trigger Player Miss/Dodge ยังไม่มี
  หลักฐาน จึงคง `counter-trigger-unconfirmed`
- `dilopho_tail`: อยู่ใน Framework ร่วม แต่ model compatibility ของ
  `RaptorPrefab` ยังไม่ยืนยัน จึงคง `model-compatibility-unconfirmed`

## ผล audit ของ raptor_dash

`Raptor_Attack_Dash` มี hit ที่ frame 23 และข้อมูล root ดิบ ณ hit ประมาณ:

- local position `(-57.28, +316.24)`
- local yaw `-201.26°`
- circular area radius `125`, sector `130..250°`

ดังนั้น R7 ไม่ตีความพื้นที่ด้านหลังว่าเป็นท่าหันธรรมดา: controller ต้องให้ target
อยู่ Front, หันหา target ก่อน commit แล้ว `SaurusActionPlan` จึงติดตาม root yaw
ของ animation เอง เมื่อ scale 2.2 ตำแหน่ง/รัศมีเดียวกันจะถูกขยายเป็น world-space
พร้อมกัน ไม่ขยายเฉพาะตัว model

collision ยังใช้ trajectory ที่ `ProcessSimpleSliding` ยอมรับจริง และ pending
telegraph/hit refresh จากฐานจริงเหมือน R4

## Spatial-scale contract

R7 เพิ่ม `SpatialScale` ลงใน immutable action plan โดย snapshot ค่า horizontal
`lossyScale` ตอน commit เพียงครั้งเดียว แล้วใช้กับ:

1. root-position delta ที่ขยับ logical actor
2. hit offset
3. circle radius/radius-min
4. rectangle half-size
5. telegraph และ damage query จาก snapshot เดียวกัน

สัตว์สามชนิดเดิมมี dev represent scale 1.0 จึงไม่เปลี่ยน trajectory ที่ผ่าน field
test แล้ว ส่วน `/combatspawn 2001 ...` อ่าน `RepresentScale=2.2` จาก combat profile
แทนการบังคับ `BaseScale=1`

## Cooldown/chase policy

- ใช้ `attack_cooltime=1.7` จาก `animal.json` โดยไม่มีโบนัส `+1`
- Recover แสดง stand 1.0 วินาที และเวลานี้นับซ้อนอยู่ใน cooldown
- เวลาที่เหลืออยู่ใน Face state; หันหา player ต่อเนื่องโดยไม่สลับ
  Approach/Stand ทุก frame
- target อยู่ flank/rear จะ Reposition-to-Front ก่อนเลือก action จึงไม่ commit
  front attack ผิดทิศ

## Field-test gate

1. ใช้ `/combatspawn 2001 60` และยืนยันว่า model เป็น Raptor ขนาด 2.2
2. ระยะใกล้ต้องเห็น `raptor_attack`; ระยะกลาง/ไกลต้องเห็น jump และ dash
3. `raptor_dash` ต้องให้ animation, ฐานจริง, วงฐาน, เส้น และ damage ตรงกันตลอด
   root turn โดยเฉพาะหลัง frame 23
4. เมื่อ dash ชน player/สิ่งกีดขวาง เส้น pending ต้องตาม trajectory จริงและไม่ flash
5. ระหว่าง cooldown ให้เดินวงกลมรอบ Raptor: ต้องหัน/ไล่ต่อเนื่อง ไม่เดิน–หยุดถี่
6. ต้องไม่เห็น `raptor_counter` หรือ `Raptor_Attack_Tail`
7. Evade และ damage reaction สี่ทิศต้องไม่ตัด active attack และฐานไม่วาร์ป
8. player normal attack แบบ auto-approach ต้องหยุดตาม bound ที่ scale แล้วและตีถึง

