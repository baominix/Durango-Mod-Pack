# R2 — Saurus Combat Context และ Short-term Memory

## สถานะ

- implementation และ build ผ่านบนฐาน `DurangoCombatSystemPlugin 0.3.9`
- context เป็น read-only; selector, movement, animation, telegraph และ damage เดิมไม่อ่าน
  context ชุดนี้
- คำสั่งตรวจอยู่ใน `DeveloperModePlugin 0.1.2` เท่านั้น
- field-test Front/Flank/Rear ผ่านตามผลทดสอบที่ผู้ใช้ยืนยัน; R3 เริ่มบน snapshot ชุดนี้
- runtime eligibility ยังเป็น `2027`, `2037`, `2039`; `raptor 2001` ยังอยู่ใน
  audit และรอ R7

## จุดจับ snapshot

`SaurusAnimalController.Process()` จับ snapshot ที่ต้น tick ก่อน state ปัจจุบันมีโอกาส
เรียก Face, Move หรือ SelectAttack ตัว snapshot จึงแสดงตำแหน่ง/ทิศที่ controller เห็น
ก่อนการตัดสินใจของ tick นั้น

จับข้อมูลไม่เกิน 10 ครั้งต่อวินาทีต่อ controller (`0.10s`) เพื่อลด allocation และ
ไม่เปลี่ยนจังหวะ simulation

## ข้อมูลใน `SaurusCombatContext`

- sequence, world generation และ engagement id
- actor/target entity id, position, yaw, radius, velocity และ HP
- actor state, engaged, active action/action-instance และ action lock
- cooldown/state remaining
- center distance และ surface distance หลังหัก radius ทั้งสองฝ่าย
- bearing, signed relative angle และ sector
- line-of-sight/path observation แบบสามสถานะ
- event ล่าสุดและจำนวน event ใน memory

sector ใช้ทิศหันปัจจุบันของสัตว์เป็นฐาน:

| relative angle | sector |
|---:|---|
| `-45°..45°` | `Front` |
| `45°..135°` | `RightFlank` |
| `-135°..-45°` | `LeftFlank` |
| นอกช่วงข้างต้น | `Rear` |

`LineOfSight` เป็น `Unknown` ใน R2 เพราะยังไม่พบ owner เดิมของเกมที่ยืนยันได้และ
ไม่ควรเพิ่ม Physics raycast ขึ้นเอง ส่วน `PathState` เป็น `Blocked` เฉพาะเมื่อ movement
เดิมรายงานว่าติดขัด มิฉะนั้นเป็น `Unknown`; `Unknown` ไม่ได้แปลว่า Clear

deterministic decision roll ยังไม่ถูกสร้างใน R2 เพราะยังไม่มี decision ใหม่ การเรียก
random ตอน capture จะทำให้ลำดับสุ่มของ selector `0.3.9` เปลี่ยน ส่วนนี้จะสร้างจาก
decision id โดยไม่แตะ random stream เดิมใน R3

## `SaurusCombatMemory`

เก็บ event ล่าสุดสูงสุด 16 รายการต่อ controller พร้อม timestamp, generation,
actor/target id และ source action instance/key:

- `Engaged`
- `DamagedByPlayer`
- `AnimalDodgedPlayerAttack`
- `PlayerAttackMissed`
- `BlownOrKnockedBack`
- `TargetEnteredFlank`
- `TargetEnteredRear`
- `PathBlocked`
- `LowHealthThresholdCrossed`
- `LastActionCompleted`

memory ถูกทำลายพร้อม controller เมื่อ despawn/change world/Return to Title จึงไม่ข้าม
generation การแปลง event เป็นข้อความย้อนหลัง 8 รายการเกิดเฉพาะเมื่อ developer เรียก
คำสั่ง ไม่เกิดใน process loop

## Developer API และคำสั่ง

Combat plugin เปิด API อ่านอย่างเดียว:

`CombatRuntime.TryGetSaurusContextReport(selector, out lines)`

DeveloperMode เรียก API นี้ผ่าน reflection:

- `/combatcontext` หรือ `/combatcontext nearest`
- `/combatcontext all`
- `/combatcontext <entityId>`

เมื่อ Developer Mode ปิด command router จะปฏิเสธคำสั่งตามกฎเดิม Combat plugin ไม่มี
SocialSystem/chat patch และไม่รู้จักคำสั่งนี้

## Field-test gate

1. ใช้ `/dev on`
2. spawn สัตว์ชนิดหนึ่ง เช่น `/combatspawn 2027 60`
3. ใช้ `/combatcontext nearest` ขณะยืนด้านหน้า ขวา หลัง และซ้ายของสัตว์
4. ตรวจ `sector`, signed angle, distance, actor/target velocity และ engagement id
5. โจมตีให้เกิด Hit/Miss/Dodge และย้ายเข้าด้านข้าง/หลัง แล้วตรวจ `RecentEvents`
6. ใช้ `/combatcontext all` เมื่อมีหลายตัว
7. Return to Title/เข้าโลกใหม่ แล้วตรวจว่า generation ใหม่ไม่มี event ของโลกก่อน

เกณฑ์ผ่าน R2 คือ sector ตรงตำแหน่งจริงหลายมุม, event มี source action ถูกชุด และไม่มี
ความเปลี่ยนแปลงของท่าโจมตี การเคลื่อนที่ เส้นเตือน หรือ damage เมื่อเทียบกับ `0.3.9`
ผู้ใช้ยืนยันผล R2 และอนุญาตให้ดำเนิน R3 เมื่อ 2026-08-27
