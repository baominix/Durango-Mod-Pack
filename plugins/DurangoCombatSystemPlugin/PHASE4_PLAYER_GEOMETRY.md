# Phase 4 — Player geometry และ hit runtime

รุ่นที่เริ่มใช้งาน: `0.3.0` (Offline UI `0.3.1`, initial actions `0.3.2`, player telegraph/Ranged semantics `0.3.3`, Melee targeting/dev separation `0.3.4`, post-battle gauge resync `0.3.5`, action-start gauge resync `0.3.6`, player root-motion snapshot `0.3.7`, collision-aware root motion `0.3.8`, developer-controlled telegraph `0.3.9`)

## Collision-aware player root motion (`0.3.8`)

ก่อนสร้าง `AttackSnapshot` ของแต่ละ `attack_info` runtime อ่าน
`PlayerAnimationClipInfo` จาก `PlayerAnimationClipManager`, เลือก
`PlayerRootMotionPath` ชาย/หญิง และหาค่า delta จากเวลาของ animation ปัจจุบันไปถึง
`attack_time` หาก action motion ยังไม่เริ่มใน frame ที่รับ message จะใช้เวลาที่ผ่าน
จาก `ClientStartAt` คูณ `playback_rate` เป็น fallback

local delta ถูกหมุนด้วย actor yaw เป็น world right/forward แล้วบวกกับ actor origin
ก่อนเรียก `PlayerActionAttackInfo.MakeAlerted()` ดังนั้นลำดับสุดท้ายคือ:

`current actor origin + remaining root motion + attack_info.offset`

แต่ละ hit ของ multi-hit ได้ center ของตนเองตาม path ณ `attack_time` แต่
`LocalMoveOperator` ของเกมเดิมนำ root motion ผ่าน collision capsule เช่นเดียวกับการ
เดิน จึงห้ามถือว่าระยะในคลิปเกิดครบเสมอ รุ่น `0.3.8` clamp forecast เริ่มต้นกับ
bound ของ selected target แล้ว refresh center จากตำแหน่งฐานจริง + remaining root
motion ทุก frame จนถึง hit time เส้นและ damage query ใช้ snapshot object เดียวกัน
และเมื่อถึง hit remaining delta เป็นศูนย์ จึงยึดฐานจริงหลัง collision

## Action-start HP/SP resync (`0.3.6`)

เมื่อ action ผ่าน validation runtime จะหัก stamina จากค่าปัจจุบัน แล้วส่ง
`SurvivalUpdated` ที่รวม life/stamina จาก `PlayerContext` ทันที การหัก stamina ใช้
`max(min, current-cost)` จึงไม่บังคับค่าที่ `/sp` เพิ่มไว้เกิน Max ให้กลับลงมาเป็น
Max ใน action แรก การส่งนี้เป็น snapshot ครั้งเดียว ไม่ใช่การ lock/เติม gauge
อัตโนมัติ

## Post-battle HP/SP resync (`0.3.5`)

`/hp` และ `/sp` เปลี่ยน gauge ใน `PlayerContext`, เรียก
`Player.OnContextChanged()` และส่ง `SurvivalUpdated` ตามเดิม เมื่อจบ battle รุ่น
`0.3.5` ส่ง `BattleEnded` ก่อน แล้วส่ง `SurvivalUpdated` ที่รวม life/stamina ล่าสุด
จาก context ตามหลังทันที เพื่อให้ UI นอก battle ใช้ snapshot เดียวกับ server state
โดยไม่เติมค่า ไม่ล็อกค่า และไม่สร้าง gauge owner ชุดที่สอง

## Melee/Ranged target semantics (`0.3.4`)

ผล audit `player_battle_actions.json` ยืนยันว่า `Melee (0)` และ `Ranged (3)`
เป็น selected-target ส่วน area จริงใช้ `CircularArea (1)` และ
`RectangularArea (2)` เท่านั้น ดูรายการเต็มใน `PLAYER_ACTION_TARGETING.md`

คำสั่ง developer และ patch ของ `SocialSystem.Say` ถูกย้ายออกจาก Combat plugin ไป
`DeveloperModePlugin`; gameplay telegraph จะแสดงเฉพาะ area จริง ส่วน visualizer ดิบ
ของเกมเปิดได้ด้วย `/dev attackalert on`

## Player telegraph และ Ranged semantics (`0.3.3`)

`AreaOfEffectVisualizer.Start()` ของเกมเดิมวาด `AttackAlerted` เฉพาะเมื่อ
developer toggle `CombatSystem.AttackAlertEnabled` เป็น `true` ซึ่งค่าเริ่มต้นเป็น
`false` รุ่นก่อนจึงรับ message ได้แต่ไม่เห็นเส้น รุ่น `0.3.3` วาด alert ที่ปลั๊กอิน
เป็นเจ้าของโดยตรงจาก `CombatSystem.OnAttackAlert` และใช้ชนิด `Player` โดยไม่แก้ค่า
global toggle

ข้อมูล `player_battle_actions.json` มี `DamageType.Ranged` ทั้งหมด 8 action และเป็น
bow/crossbow เท่านั้น ค่า radius `750/800` ตรงกับระยะยิง ไม่ใช่วง AoE ดังนั้น:

- Ranged ต้องมี `TargetEntityId` ที่ valid
- ทุก hit ของ quickshot โดนเฉพาะเป้าหมายเดิม
- สัตว์ตัวอื่นที่ยืนใกล้กันไม่โดน
- Ranged ไม่วาดวง area telegraph
- CircularArea/RectangularArea ใช้ snapshot เดียวกันสำหรับเส้นและ hit query

## Offline combat UI compatibility (`0.3.1`)

`CombatGroup.Start()` ของเกมเดิมปิด UI ทั้งกลุ่ม เมื่อ main scene ใช้
`GameManager.ClusterMode != Online` ทำให้เหลือเพียงช่องเปล่า แต่ปุ่มเปิด combat,
ไอคอน action และ callback ไม่ถูกผูกครบ

`Presentation/OfflineCombatUiPatches.cs` เปลี่ยนค่า cluster เป็น Online ชั่วคราว
เฉพาะระหว่างที่ `CombatGroup.Start()` ทำงาน และคืนค่าโหมดจริงใน Harmony finalizer
จากนั้น interaction แบบ Attack จะเพียงเปิด battle view และเลือกสัตว์เป้าหมาย
โดยไม่กดใช้ action slot 1 ให้อัตโนมัติ

## Initial action loading และคำสั่งทดสอบ (`0.3.2`)

ตอนเข้าแผนที่ `EquipPreset` อาจมี item id แล้ว แต่ item ดังกล่าวยังค้นไม่พบใน
`InventorySystem` รุ่นก่อนหน้าตีความสถานะนี้ผิดว่าไม่มีอาวุธและส่ง `bare_hands`
ทันที รุ่น `0.3.2` แยก `EquipmentDataReady` ออกจากผลรายการ action หากข้อมูลยัง
ไม่พร้อม session จะเก็บ `GetActions` reply sequence และ retry ทุก 0.25 วินาที
จากนั้นส่ง `Actions` เพียงเมื่ออ่านอุปกรณ์จริงได้แล้ว

คำสั่งที่ย้ายเข้าสู่ runtime ใหม่และใช้ได้ใน Phase 4:

- `/hp <amount>` และ `/sp <amount>` เปลี่ยน gauge ใน `PlayerContext` เดิม แล้วแจ้ง
  persistence owner ผ่าน `Player.OnContextChanged()`
- `/combatspawn [2027|2037|2039] [level]`
- `/combatwave [2027|2037|2039] [level] [count] [spacing]`

คำสั่งเหล่านี้อยู่ใน `DeveloperModePlugin` ตั้งแต่ `0.3.4` และต้องเปิดด้วย
`/dev on` ก่อน
- `/combatstatus` และ `/combathelp`

คำสั่งของระบบสัตว์เก่า เช่น Brachio/natural-spawn ยังไม่ย้าย เพราะ Saurus AI ของ
ปลั๊กอินใหม่ยังไม่ถึง phase นั้น

## ข้อมูลจากเกมเดิม (Original)

### ระยะเริ่ม action กับตำแหน่ง hit

`UsingAction.State.Prepare` ของเกมเดิมนำทางผู้เล่นจนระยะถึงเป้าหมายไม่เกิน
`meta.use_range + bound radius` แล้วจึงเริ่ม animation ดังนั้น auto-approach เป็น
พฤติกรรมของเกมเดิม แต่ตำแหน่ง hit ไม่ได้ใช้ actor origin ดิบอย่างเดียว:
`UsingActionAlert.Update()` อ่าน `PlayerRootMotionPath` ของ motion และบวก
`path.GetDelta(currentAnimationTime, attack_time)` ก่อนสร้างพื้นที่แต่ละ hit

runtime `0.3.8` ชดเชย remaining root-motion delta ของแต่ละ hit, clamp กับเป้าหมาย
และแก้ center ต่อเนื่องจากฐานจริง จึงรองรับทั้ง action ที่เริ่มจากขอบ `use_range`
และกรณี dash/cleave ถูกสัตว์หยุด ส่วน Melee/Ranged เป็น selected-target และไม่ใช้
area query; หากสองชนิดนี้ไม่เกิด damage ต้องแยกตรวจ target หาย, Missed หรือ Dodged

### Multi-hit telegraph

เมื่อรับ action runtime schedule `attack_info` ทุก record และส่ง `AttackAlerted`
ทั้งหมดทันที แต่แต่ละ record มี `AttackTime` ของตัวเอง `AreaOfEffectVisualizer`
จึงสร้างพื้นที่ทั้งหมดตั้งแต่ต้นและให้แต่ละพื้นที่สิ้นสุดเมื่อถึงเวลา hit ของ record
นั้น การหายอิงเวลา `AttackTime` ไม่ได้รอผล `Hit/Missed/Dodged` กลับมา

`Yaml.PlayerActionAttackInfo.MakeAlerted()` เป็น source of truth สำหรับ snapshot:

1. แปลง yaw เป็น forward `(cos(90-yaw), sin(90-yaw))`
2. สร้าง right จาก `(forward.y, -forward.x)`
3. เลื่อน center ด้วย `forward * offset.y + right * offset.x`
4. กำหนด `EventAt`, `AttackTime`, `Yaw + damage_angle`
5. ส่ง radius/angles สำหรับ Melee, CircularArea, Ranged หรือ rect half size สำหรับ RectangularArea

`AreaOfEffectVisualizer` แสดงผลดังนี้:

- Melee/CircularArea ใช้ circle หรือ arc ตาม `angles`
- Ranged ใช้ circle เต็มวง
- RectangularArea ใช้ขนาดเต็มสองเท่าของ `rect_half_size` และหมุนตาม yaw;
  ค่า `.x` อยู่ตามแกน forward/yaw และ `.y` อยู่ตามแกน right ซึ่งตรงกับ
  `FillBorderAlert.MakeRect` ของเกม

ข้อความเดิมที่ Phase นี้ส่งกลับ client:

- `BattleBegun`
- `AttackAlerted`
- `Damaged`
- `SurvivalUpdated`
- `EntityDied`
- `BattleEnded`

## สิ่งที่ runtime ใหม่ทำ

- ล็อก yaw เมื่อรับ action แต่ refresh actor origin จากฐานจริงจนถึงเวลาของแต่ละ hit
- สร้าง `AttackSnapshot` หนึ่ง object ต่อ `attack_info` และแก้เฉพาะ center/actor origin
- ใช้ snapshot object เดียวกันสร้างเส้นเตือนและ query เป้าหมาย
- scheduler ใช้ `generation + action instance id + hit index`
- รองรับ multi-hit; หลายเป้าหมายใช้เฉพาะ damage type แบบ Area
- target position อ่าน ณ hit time จึงสามารถเดินออกจากพื้นที่ก่อน hit ได้
- target radius ถูกนำมาขยายขอบ circle/arc/rectangle เพื่อไม่บังคับให้จุดกึ่งกลางโมเดลอยู่ในพื้นที่ทั้งหมด
- ปิด `UsingActionAlert` ของ local player เมื่อ Combat plugin ทำงาน เพื่อไม่สร้างเส้นซ้ำกับ authoritative runtime แม้เปิด developer toggle
- `AttackAlerted` ที่มี entity id ของ local player ถูกวาดด้วยรูปแบบ Player และย้ายด้วย visualizer id เดิมตาม snapshot ที่ refresh

## รูปทรงที่รองรับ

| DamageType | Query |
|---|---|
| Melee | selected `TargetEntityId` เท่านั้น; radius เป็น reach |
| CircularArea | circle หรือ sector/arc |
| RectangularArea | oriented rectangle |
| Ranged | selected `TargetEntityId` เท่านั้น; radius เป็นระยะยิงและไม่วาด AoE |

angle รองรับทั้งช่วงที่คร่อม 0 องศา เช่น `-80..80` และช่วงด้านหลัง เช่น `140..220`

## การจำแนกผล

- Area action ที่ไม่อยู่ใน geometry: out-of-range และไม่ส่ง `Damaged`
- Ranged: resolve เฉพาะ selected target; ไม่มีการค้นสัตว์ทุกตัวใน radius
- อยู่ใน geometry แต่ accuracy check ไม่ผ่าน: `Missed`, damage 0
- accuracy ผ่านแต่ evade check ผ่าน: `Dodged`, damage 0
- ผ่านทั้งสอง: `Hit`, ลด life แล้วส่ง `SurvivalUpdated`

## ค่าที่ reconstruct ชั่วคราว (Reconstructed)

สูตร server combat เต็มรูปแบบไม่อยู่ใน export จึงแยกส่วนต่อไปนี้ไว้เพื่อแทนใน Phase 7:

- hit chance: `player accuracy / (player accuracy + animal level * 5)`
- evade chance: `0.20` จากสูตรร่วม `evade = 0.2 + combat_level * 0`
- defense: `animal level * 5` โดยใช้ unstable factor = 1
- damage ขั้นต้น: `player attack * attackRating/(attackRating+defense) * directional ratio`
- body part ขั้นต้น: Body
- attack type อนุมานจาก action id
- `use_target_origin=true` ใช้ตำแหน่งเป้าหมาย ณ commit เป็น origin; action ผู้เล่นในข้อมูลปัจจุบันไม่มีรายการที่ตั้งค่า true

ค่ากลุ่มนี้ไม่ถูกอ้างว่าเป็นสูตร server ต้นฉบับ และต้องถูกแทนด้วย strategy ที่ยืนยันแล้วใน Phase 7

## ขอบเขตเป้าหมาย

Phase 4 ทำ damage เฉพาะ wild animal ที่:

- มี entity type `2027`, `2037` หรือ `2039`
- มี profile ที่ validation ผ่าน
- ไม่ใช่ ally/pet
- ยังมีชีวิตและอยู่ใน world ปัจจุบัน

life state ของสัตว์เป็น authoritative ภายใน world session และถูกล้างเมื่อ player/world generation เปลี่ยน ส่วน stamina แก้ใน `PlayerContext` แล้วเรียก `Player.OnContextChanged()` ของเกมเดิมเพื่อให้ owner เดิมบันทึกข้อมูล ไม่เขียนไฟล์ player หรือ world save จาก combat plugin โดยตรง

## Diagnostics

developer visualization ถูกแยกไป `DeveloperModePlugin` และเปิดด้วย
`/dev attackalert on` ส่วนรายละเอียด action instance, hit index, center, yaw,
target position, result และ damage ยังออกผ่าน logger ของ Combat plugin ซึ่งถูกควบคุม
ด้วย LogControlPlugin ตามเดิม

## Static validation ที่ทำแล้ว

- circle inside/outside และ target radius overlap
- forward arc และ side exclusion
- arc ด้านหลัง `140..220`
- rotated arc ที่ yaw 90
- rectangle ที่ yaw 0 และ yaw 90
- offset center ตาม `MakeAlerted()`
- build ด้วย .NET 3.5 สำเร็จ

การทดสอบ gameplay เป็นหน้าที่ผู้ใช้ตามข้อตกลง และรอบนี้ไม่ได้เปิดเกม
