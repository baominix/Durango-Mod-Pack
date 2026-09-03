# Player normal attack auto-approach audit

สถานะ: แก้แล้วบนฐาน `DurangoCombatSystemPlugin 0.3.9`

## ผลตรวจแกน

แกนไม่ได้กลับด้าน:

- `WorldPosition.x` ตรงกับ Unity world X
- `WorldPosition.y` ตรงกับ Unity world Z
- `PlayerActionAttackInfo.offset[0]` คือ local right/X
- `PlayerActionAttackInfo.offset[1]` คือ local forward/Z
- yaw ของเกมสร้าง forward แล้วบวก
  `forward * offset[1] + right * offset[0]`

ดังนั้นห้ามแก้ด้วยการสลับ X/Y เพราะจะทำให้ action area และ root motion ที่ผ่าน
field test แล้วเสียทิศทาง

## สาเหตุของ normal attack เมื่อปล่อย auto-walk

`Durango.Logic.Combat.UsingAction` ของ client หยุดนำทางเมื่อระยะถึงเป้าหมายไม่เกิน:

`ObjectManager.GetBoundRadius(entityType) * target.localScale.x + meta.use_range`

แต่ runtime `0.3.9` ก่อนแก้สร้าง `AnimalCombatTarget.Radius` จาก:

`max(CharacterBehavior.XRadius, CharacterBehavior.YRadius) * lossyScale`

สองค่านี้ไม่ใช่ข้อมูลชนิดเดียวกัน ตัวอย่างจากข้อมูล Original:

| Entity | YAML bound × prefab scale | collider max × prefab scale | ผลก่อนแก้ |
|---|---:|---:|---|
| 2027 Zebraceratops | `200 × 0.42 = 84` | `100 × 0.42 = 42` | server รับระยะสั้นกว่า client 42 หน่วย |
| 2037 Elephantulus | `60 × 1.0 = 60` | `50 × 1.0 = 50` | server รับระยะสั้นกว่า client 10 หน่วย |
| 2039 Deinonychus | `50 × 1.7 = 85` | `60 × 1.7 = 102` | server ผ่อนกว้างกว่า client 17 หน่วย |
| 2001 Raptor | `50 × 2.2 = 110` | `60 × 2.2 = 132` | server ผ่อนกว้างกว่า client 22 หน่วย |

กรณี Zebraceratops ทำให้ client หยุดเดินและส่ง `UseBattleAction` แล้ว แต่ validation
ของ plugin ยังมองว่าเป้าหมายอยู่นอก `use_range` จึงเห็น animation normal attack โดย
ไม่มี damage คล้ายโจมตีไม่ถึงตัวสัตว์

## การแก้ไข

`AnimalCombatTarget.Radius` ใช้ `ObjectManager.GetBoundRadius(entityType) ×
transform.localScale.x` เหมือน client เดิมแล้ว ค่าเดียวกันถูกใช้กับ:

- selected-target `use_range` validation
- target-bound expansion ของ area query
- collision-aware player root-motion clamp

Melee/Ranged ยังคงเป็น selected-target ตามข้อมูลเกม และเมื่อ action ผ่าน validation
จะ resolve เป้าหมายเดิม ไม่เปลี่ยนเป็น AoE

## Regression gate

1. Spawn 2027 แล้วกด normal attack จากนอกระยะ ปล่อยให้เกมเดินเข้าเอง
2. เมื่อผู้เล่นหยุดและ animation เริ่ม ต้องมี `Damaged`/ตัวเลขผลลัพธ์ตาม
   Hit, Missed หรือ Dodged ไม่ใช่ถูกปฏิเสธเพราะระยะ
3. ทำซ้ำกับ 2037 และ 2039
4. ตรวจ area skill เดิมว่าเส้นและเป้าหมายขอบพื้นที่ยังตรงกัน
5. ตรวจว่า player root motion ยังชน bound ของสัตว์โดยไม่ทะลุหรือหยุดไกลผิดปกติ
