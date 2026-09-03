# Phase 5B — Animal attack execution

สถานะ: **implemented และ field test ผ่านโดยผู้ใช้เมื่อ 2026-08-25**

Phase 5B เชื่อม attack intent ของ Saurus AI เข้ากับ offline simulation owner โดยไม่ให้
AI controller ส่ง network message หรือแก้ HP เอง

## Flow

```text
SaurusAnimalController commits attack
  -> SaurusAiSession forwards immutable intent
  -> OfflineCombatSession reads runtime clip frameRate
  -> one AnimalAttackSnapshot per attack_info
  -> send AttackAlerted
  -> scheduler waits until frame / frameRate
  -> query current player position against the same snapshot
  -> Missed / Dodged / Hit
  -> Damaged
  -> on Hit: PlayerContext life + SurvivalUpdated
```

## Original data used directly

- motion and loaded `AnimationClip` from runtime `AnimalFrameworkResource`
- hit frame, sub-action id, damage type, radius/radius-min, angles,
  rectangle half size, offset, damage angle และ `use_target_origin` จาก Framework
- `attack`, `accuracy`, `attack_rating`, animal type และ size level จาก
  `animal.json`
- `AttackAlerted`, `Damaged`, `SurvivalUpdated`, `EntityDied` และ battle message
  contract ของเกม
- player Defense, Dodge และ Evade จาก `StatisticsSystem`

ถ้า runtime clip หรือ frame rate ไม่มี ระบบจะข้าม damage ของท่านั้นพร้อม warning
แทนการเดา 30 fps ทำให้ animation กับ hit time ไม่คลาดแบบเงียบ ๆ

## Geometry contract

กฎสร้าง center ตรงกับ `PlayerActionAttackInfo.MakeAlerted()` ของเกม:

```text
forward = yaw direction
right   = perpendicular(forward)
origin  = target-at-commit เมื่อ use_target_origin; ไม่เช่นนั้น actor-at-commit
center  = origin + forward * offset.y + right * offset.x
hitYaw  = actorYaw + damage_angle
```

- Circle/arc รองรับ `radius_min` ใน damage query
- Rectangle ใช้ half-size แกนแรกตาม forward และแกนสองตาม right
- Melee/Ranged ยังคงเป็นเป้าผู้เล่นคนเดียว แต่ต้องอยู่ใน radius/arc ของ hit
- เส้นเตือนและ damage query อ่าน object snapshot เดียวกัน
- snapshot ถูก lock ตอน commit; attack root-motion compensation เป็นงาน Phase 6

## Timing

```text
hitAt = committedAt + attack_info.frame / runtimeClip.frameRate
```

ทุก hit ของท่า multi-hit ถูก schedule แยกและเรียงด้วย hit time, attack instance และ
hit index จึงไม่รวมหลาย hit เป็น damage เดียว

## Damage provenance

Original expression adapter รองรับเลข, วงเล็บ, `+ - * /`, `combat_level` และ
`unstable_factor` เพื่อคำนวณ Attack, Accuracy และ Attack Rating ของสัตว์สามชนิด
จาก expression จริง โดย Phase 5B ใช้ unstable factor `1.0`

ส่วนต่อไปนี้ยังเป็น **Reconstructed** และแยกไว้ใน `AnimalHitResolver`:

- Accuracy เทียบ player Dodge เพื่อหา Missed
- player Evade normalization เพื่อหา Dodged
- Attack Rating เทียบ player Defense เพื่อหาสัดส่วน damage
- herbivore ใช้ Small/LargeBody; carnivore/scavenger ใช้ Small/LargeTear
- Phase นี้โจมตี `BodyPart.Body` และยังไม่ใส่ critical/effect

เมื่อพบสูตร server เดิม Phase 7 สามารถเปลี่ยน resolver ได้โดยไม่แตะ AI, scheduler,
geometry หรือ message bridge

## Lifecycle guards

scheduled hit จะถูกทิ้งหากอย่างใดอย่างหนึ่งเปลี่ยน:

- world generation
- animal entity/object instance
- `AnimalManager` ไม่ได้ index object เดิม
- animal ตาย/despawn
- local player ตายหรือ session ถูก dispose

การเปลี่ยน world หรือ Return to Title จะ unsubscribe event และล้าง pending animal
hits ทั้งหมด

## ผลที่ควรเห็นในการทดสอบ

1. เปิด `/dev attackalert on` แล้วเส้นสัตว์เริ่มตอน animation attack commit และจบตรง hit frame
2. ยืนในเส้นเมื่อถึง hit: ได้ Missed, Dodged หรือ Hit; เฉพาะ Hit ลด HP
3. ออกจากเส้นก่อน hit: ไม่ได้รับ `Damaged` และ HP ไม่ลด
4. ท่า multi-hit วาด/หมดเวลาทีละ hit และตรวจโดนแยกครั้ง
5. `/hp` ที่เพิ่มไว้ถูกหักจากค่าปัจจุบันและคงอยู่ใน PlayerContext
6. ฆ่าสัตว์/ย้ายแผนที่/Return to Title ก่อน hit: hit เก่าไม่ทำงาน
7. เนื่องจาก attack root motion ยังไม่อยู่ใน Phase 5B ท่าที่ตัวภาพเคลื่อนฐานมากอาจมี
   เส้น lock ต่างจากภาพ; ให้บันทึก action id เพื่อทำ species profile ใน Phase 6
