# R4 — Root transform และ SaurusActionPlan execution

สถานะ: implementation/build และ field test ผ่านบนฐาน
`DurangoCombatSystemPlugin 0.3.9` เมื่อ 2026-08-27

## เป้าหมาย

R4 แก้การที่ animation, logical movement, เส้นเตือน และ damage เคยคำนวณ root
transform แยกกัน โดยสร้าง `SaurusActionPlan` แบบ immutable หนึ่งชุดตอน commit ท่า
แล้วให้ทุกระบบอ่าน plan เดียวกัน

R4 ยังใช้ selector และชุด action ของ `0.3.9` เดิม ไม่เปิด Turn/Counter/Escape
semantics ใหม่ และไม่ให้ shadow resolver สั่ง gameplay

## Source of truth

`Generate-SaurusRootMotionData.ps1` รุ่น format 2 อ่านจาก AnimationClip ต้นฉบับ:

- `Bip001` position X/Z พร้อม tangent เดิม
- `Bip001` quaternion rotation
- แปลง quaternion เป็น planar yaw บน Unity XZ
- unwrap yaw ต่อเนื่องจาก key แรก และเก็บ delta yaw ตามเวลา

runtime ใช้ cubic Hermite กับ position เหมือนเดิม และ interpolate yaw ระหว่าง key
ที่ export ไว้หนาแน่นตาม animation curve

## Action-plan contract

plan ล็อกข้อมูลต่อไปนี้เพียงครั้งเดียวที่ selection boundary:

- generation / engagement / controller action instance
- Framework attack definition และ motion
- committed time
- actor position/yaw ตอน commit
- target position ตอน commit
- original root position/yaw curve
- alignment policy

policy ของ R4 คือ `CommitFacingThenFollowOriginalRootYaw`: controller หันหาเป้าก่อน
commit เหมือน baseline จากนั้นท่าติดตาม yaw ของ `Bip001` ต้นฉบับ ไม่หันตามตำแหน่ง
player ใหม่ระหว่าง animation

## ผู้ใช้ plan

1. `SaurusMotionAdapter` เล่น animation และนำ position delta จาก plan ผ่าน collision
   sliding ไปยัง `AnimalBehavior.CurrentPosition`
2. adapter ตั้ง logical actor yaw จาก root-yaw sample เดียวกัน ขณะที่
   `RootMotionMovable` หัก baked root transform ออกจาก mesh
3. `AnimalAttackSnapshot` สร้าง center/yaw ของแต่ละ hit จาก plan ณ
   `hit.frame / frameRate`
4. ระหว่างรอ hit runtime วัดส่วนต่างระหว่าง planned root กับฐานจริงหลัง collision
   แล้วชดเชยตำแหน่งของ hit ที่ยังเหลือ
5. เส้น animal เป็น plugin-owned visualizer และ snapshot เดียวกันถูกใช้ query damage

เมื่อถึง hit runtime refresh snapshot จากฐาน/yaw จริงหลัง AI process ของ frame นั้น
จึงไม่ใช้ตำแหน่งที่คาดไว้ก่อน collision ตัดสิน damage

## Telegraph ownership

animal alert ไม่วนผ่าน `AttackAlerted` กลับเข้า default renderer แล้ว แต่สร้างผ่าน
`AnimalAttackTelegraph` โดยตรงใน offline process นี้ เพราะมี consumer เดียวในเกมเดิม
คือ `AreaOfEffectVisualizer`

- pending hit มี visualizer id แยกด้วย generation/entity/instance/hit
- center เลื่อนตาม collision correction โดยไม่สร้างพื้นที่ซ้ำ
- yaw ถูกกำหนดจาก plan ณ hit timeตั้งแต่สร้างเส้น
- `/dev attackalert on/off` เปิด/ปิด pending animal area ได้
- dispose/change world ล้างทั้ง player และ animal telegraph owners

## Normal-attack regression gate ที่ทำก่อน R4

แกน player ไม่ได้กลับด้าน ปัญหา auto-walk มาจาก client กับ runtime ใช้ target
radius คนละแหล่ง จึงแก้ให้ `AnimalCombatTarget` ใช้ YAML `bound_radius ×
localScale.x` เหมือน `Durango.Logic.Combat.UsingAction` รายละเอียดใน
`PLAYER_NORMAL_ATTACK_RANGE_AUDIT.md`

## Field-test gate

1. Player normal attack กับ 2027/2037/2039: ปล่อย auto-walk แล้ว action ต้องไม่ถูก
   ปฏิเสธเพราะ `use_range`
2. `tricera_head`, `tricera_once`, `tricera_dash`: mesh/ฐาน/เส้น/hit ตรงกัน
3. `phenaco_bite`, `phenaco_jump`, `phenaco_gas`: ท่าที่หมุนต้องหมุนตาม animation
   และเส้นชี้ตาม yaw ณ hit
4. `raptor_attack`, `raptor_jump`, `raptor_dash`, `raptor_counter`: โดยเฉพาะ dash
   ที่ root yaw ราว -190° ต้องไม่มีเส้นอยู่ด้านหลังผิดจังหวะ
5. ท่าชน player/สิ่งกีดขวาง: เส้นที่ยัง pending ต้องเลื่อนชดเชยตำแหน่ง และ damage
   อิงฐานจริงตอน hit
6. จบท่าทุกชนิดต้องไม่มี one-frame mesh flash/วาร์ปไปตำแหน่งอื่น
7. `/dev attackalert off` ต้องลบเส้น player/animal ที่ยัง pending และเปิดกลับระหว่าง
   ท่าแล้วต้องเห็นเฉพาะ hit ที่ยังไม่เกิด
8. `/combatintent` ต้องยังรายงาน legacy comparison เหมือน R3 และ shadow decision
   ต้องไม่เปลี่ยนท่าที่ execute

## ผล field test

ผู้ใช้ยืนยันกับสัตว์ทดสอบทั้งสามชนิดว่า:

- ไม่มี mesh flash หรือวาร์ปหลังจบ animation
- เส้นทางเดิน, ฐาน logical, animation, telegraph และทิศ hit ตรงกัน
- normal attack แบบ auto-approach เข้าถึงสัตว์และโจมตีได้ตามปกติ

R4 จึงผ่าน gate และคง version ที่ `0.3.9`

## Phenacodus follow-up

หลังผ่าน gate พบข้อมูลที่ baseline ยังไม่ได้ใช้สองส่วน:

1. ท่าตะกุยดินคือ `phenaco_attack_escape` ไม่ใช่ `Phenaco_Battle_Idle`:
   animation หมุนหลังให้ target, โจมตีด้านหลังสี่ hit แล้วเคลื่อนออกจาก target
2. `phenaco_gas` มี hit ที่ frame 42 (1.4 วินาที), root เดินหน้า 150.77 หน่วย
   และหมุนประมาณ -177.94 องศาก่อนใช้ sector 140..220 องศา

runtime ไม่ใช้ battle-idle แทน Escape อีกต่อไป เมื่อ low-health retreat trigger
สำเร็จ Elephantulus จะหันหา player เพื่อ commit `phenaco_attack_escape` แล้วให้
root/yaw เดิมของ clip ทำการหันหลัง, ตะกุย และเคลื่อนหนี ก่อนต่อด้วย Retreat

gas ยังมี pre-commit spacing แยกต่างหาก: เมื่อ target อยู่ประชิดมาก สัตว์ถอยโดย
ยังหันหา target ก่อน แล้วค่อยสร้าง immutable action plan จากตำแหน่งจริง

รายละเอียดใน `PHENACO_ESCAPE_STRIKE_AND_GAS_SPACING.md`
