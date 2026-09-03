# Phase 5 — Saurus AI core

สถานะ: **เริ่มใช้งาน core แล้วบนฐานปลั๊กอิน `0.3.9`**

Phase 4 ผ่าน field test โดยผู้ใช้เมื่อ 2026-08-25 จึงเริ่มเพิ่มระบบสัตว์โดยไม่แก้
player combat runtime ที่ผ่านการทดสอบแล้ว

## ขอบเขตรอบแรก

- Single-player/offline เท่านั้น
- รับเฉพาะ wild enemy ที่อยู่ใน `AnimalManager` ของ world ปัจจุบัน
- entity type ต้องมี profile ใน `CombatDataRegistry`: `2027`, `2037`, `2039`
- ปฏิเสธ pet, grazing animal, ally และ object/NPC ที่เพียงใช้ `AnimalBehavior`
- controller เป็น plain C# object ต่อ world generation ไม่เพิ่ม `MonoBehaviour`
  ลงบน prefab และไม่ใช้ class จากปลั๊กอินเก่า

## State machine

```text
Idle <-> Roam
  -> Alert -> Approach <-> Face -> Attack -> Recover
                                      ^          |
                                      +----------+
  -> ReturnHome -> Idle
  -> Dead
```

มี state `Retreat` สำรองไว้ แต่เงื่อนไข HP ต่ำกว่า 20% และโอกาสเกิดจะกำหนดใน
species profile ของ Phase 6

### กฎลดอาการเดิน–หยุดกระตุก

- `Approach` เข้า `Face` เมื่อถึง attack-enter distance
- จาก `Face/Recover` จะกลับ `Approach` เมื่อเกินระยะเดิมบวก hysteresis เท่านั้น
- ต้องหันเข้า tolerance และนิ่งครบ face-settle time ก่อน commit ท่า
- เมื่อ collision ขวางต่อเนื่องจะยกเลิก pursuit และ `ReturnHome`
- attack cooldown เริ่มหลัง animation จบ และระหว่างรอใช้ `battle_stand`

## ที่มาของข้อมูล

Original:

- move motion, base speed และ rotate speed อ่านจาก runtime
  `AnimalFrameworkResource.move_motion_sets`
- stand/battle stand และ attack motion/geometry อ่านจาก Framework
- `attack_cooltime` และ bound radius อ่านจาก `animal.json`

Reconstructed (รวมไว้ใน `SaurusCoreTuning` เพื่อเปลี่ยนได้ภายหลัง):

- alert/face settle time
- approach hysteresis
- roam/leash/pursuit distance
- idle/roam duration
- blocked timeout

## Ownership และ lifecycle

- `SaurusAiSession` ถูกสร้างพร้อม `OfflineCombatSession` และถือ generation เดียวกัน
- subscribe `AnimalManager.AnimalAppeared/AnimalDisappeared`
- reconcile เป็นระยะเพื่อรองรับ local async spawn
- controller ล้างเมื่อ animal หาย, object instance เปลี่ยน, player/world ปิด หรือ
  Return to Title
- เมื่อ controller รับ movement ownership จะ clear path เก่าและปิด root motion
  ระหว่าง steering; dispose แล้วคืน root-motion ownership ให้เกม

## สิ่งที่ต่อแล้วใน Phase 5B

- attack intent ถูกส่งต่อจาก `SaurusAiSession` ไปยัง owner คือ
  `OfflineCombatSession`
- สร้าง immutable animal attack snapshot ต่อ `attack_info`
- ส่ง `AttackAlerted`, ตรวจ geometry เมื่อถึง hit, ส่ง `Damaged` และหัก HP
  ผู้เล่น
- generation/object-instance guard ยกเลิก scheduled hit ที่ไม่อยู่ใน world เดิม

รายละเอียดอยู่ใน
[PHASE5B_ANIMAL_ATTACK_EXECUTION.md](PHASE5B_ANIMAL_ATTACK_EXECUTION.md)

## สิ่งที่ยังตั้งใจไม่ทำใน core

- species attack weighting/ระยะที่ชอบ
- root motion ของ attack และ logical base synchronization
- directional damage/Evade/Blow/KnockDown interrupt
- low-health retreat

รายการเหล่านี้ต้องต่อบน commit event ของ core ไม่ย้อนกลับไปใส่เงื่อนไขเฉพาะสัตว์
ใน state machine กลาง

ระบบ body-part injury/status effect เป็น simulation อีกชั้นหนึ่งและยังไม่อยู่ใน
Phase 5A ดูผลตรวจ Archive เทียบข้อมูลเกมจริงและขอบเขต implementation ที่
[NEUTRAL_DINOSAUR_STATUS_EFFECTS.md](NEUTRAL_DINOSAUR_STATUS_EFFECTS.md)

## ผลที่ควรเห็นใน field test รอบนี้

1. สัตว์สามชนิดเดิน Idle/Roam โดยใช้ animation ของ Framework ตัวเอง
2. เมื่อผู้เล่นโจมตี สัตว์เข้า Alert, เดินเข้าหาระยะ, หัน แล้วเล่น attack
3. ระหว่าง cooldown เล่น battle stand โดยไม่เดิน–หยุดสลับทุกเฟรม
4. ผู้เล่นเดินออกจากระยะแล้วสัตว์ไล่ต่อเนื่องตาม hysteresis
5. ไกลเกิน leash/ติดสิ่งกีดขวางนาน/เป้าหมายหาย สัตว์กลับจุดเกิด
6. pet และ NPC ไม่ถูก controller จับ
7. เมื่อใช้ build Phase 5A สัตว์ยังไม่ลด HP ผู้เล่น; เมื่อใช้ Phase 5B เส้นเตือน,
   hit result และ HP ต้องทำงานตามเอกสาร Phase 5B
