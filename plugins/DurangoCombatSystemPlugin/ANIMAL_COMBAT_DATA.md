# ข้อมูลสัตว์ชุดทดลอง

สัตว์ชุดแรก:

- `2027 zebraceratops`
- `2037 elephantulus`
- `2039 deinonychus_savana`

ตัวเลขในหัวข้อนี้มาจาก `animal.json` และ Framework ต้นฉบับ ไม่ใช่ค่าจากปลั๊กอินเก่า

## ค่าร่วม

สัตว์ทั้งสามใช้สูตรต่อไปนี้ในข้อมูลปัจจุบัน:

- defense: `(0 + combat_level * 5) * unstable_factor`
- attack rating: `(0 + combat_level * 6) * unstable_factor`
- accuracy: `(0 + combat_level * 5) * unstable_factor`
- dodge: `0 + combat_level * 5`
- normal damage ratio by direction: Front 0.8, Right 1.0, Back 1.2, Left 1.0
- groggy damage ratio: Front 1.25, Right 1.0, Back 1.25, Left 1.0

`attack_rating` เป็น stat โจมตี/ความสามารถทำให้การโจมตีผ่านการป้องกัน ไม่ใช่ `animal.defense`; defense มี field และสูตรแยกอยู่แล้ว

## 2027 zebraceratops

| รายการ | ค่า |
|---|---|
| Framework | Tricera |
| Model | `Tricera/ZebraceratopsPrefab` |
| Root motion set | `zebraceratops` |
| AI factor id | `protoceratops_ai` |
| Type | Herbivore |
| Attack | `(23.07 + combat_level * 0.96) * unstable_factor` |
| Life max | `(0.446 * ((combat_level + 24) ** 2)) * unstable_factor` |
| Attack cooltime | 1.3 s |
| Bound radius | 200 |
| Blow resistance | 560 |
| Knock-back resistance | 400 |
| Groggy duration | 8 s |
| Knock-down duration | 8 s |

Framework: `tricera_framework.asset`

Attack ที่พบ:

| Action | Motion | Rotation | Hit geometry |
|---|---|---:|---|
| `tricera_dash` | `Tricera_Attack_Dash` | 45°/s, `bound_enemy=false` | frame 29/33 เป็น rectangle และ frame 41 เป็น radius 200 |
| `tricera_once` | `Tricera_Attack_Once` | 90°/s | frame 12, radius 220, offset (50, 200) |
| `tricera_head` | `Tricera_Attack_Head` | 90°/s | frame 12/23, radius 220, offset (100,150) และ (-150,150) |
| `tricera_turn` | Tricera turn attack | ตาม framework | ใช้ข้อมูล AttackInfo ของ asset |
| `tricera_counter` | Tricera counter | ตาม framework | ใช้ข้อมูล AttackInfo ของ asset |

หมายเหตุออกแบบ:

- `tricera_once`/`tricera_head` ต้องให้ root motion ขยับ actor base ไม่ใช่ mesh อย่างเดียว
- geometry ต้องตรึงเมื่อ commit ท่า ไม่วาดตามฐานที่กำลังเคลื่อน
- ไม่ใช้ `turn/dash` เพื่อแก้ทิศแบบสุ่ม หาก gameplay/animation ของท่านั้นไม่ได้เรียกจริง

## 2037 elephantulus

| รายการ | ค่า |
|---|---|
| Framework | Phenacodus |
| Model | `Phenacodus/Phenacodus_savanaPrefab` |
| Root motion set | `phenacodus_savana` |
| AI factor id | `phenacodus_ai` |
| Type | Herbivore |
| Attack | `(8.61 + combat_level * 0.36) * unstable_factor` |
| Life max | `(0.283 * ((combat_level + 24) ** 2)) * unstable_factor` |
| Attack cooltime | 1.6 s |
| Bound radius | 60 |
| Blow resistance | 140 |
| Knock-back resistance | 100 |
| Groggy duration | 6 s |
| Knock-down duration | 6 s |

Framework: `Phenacodus_framework.asset`

Attack ที่พบ:

| Action | Motion | Hit geometry |
|---|---|---|
| `phenaco_jump` | `Phenaco_Attack_Jump` | frame 24, rectangle half size 200×80, offset (0,-50), damage angle 13° |
| `phenaco_bite` | `Phenaco_Attack_Bite` | frame 14, rectangle half size 120×80 |
| gas variants | Phenacodus gas | frame 42, radius 400, angles 140–220° |
| `phenaco_attack_escape` | escape attack | frame 14/18/22/26; rectangle 250×60, offset y -150; hit แรกมี radius/angles เพิ่มเติม |

หมายเหตุออกแบบ:

- Elephantulus ไม่หนีทันทีเมื่อถูกโจมตี ต้องเข้า battle ปกติ
- low-health retreat เป็นพฤติกรรมชั่วคราวที่มีโอกาสเกิดเมื่อ HP ต่ำกว่า 20% ไม่ใช่ flee-on-hit
- attack และ damage/blow reaction ที่มี displacement ต้อง sync actor base เพื่อไม่ snap กลับ

## 2039 deinonychus_savana

ชื่อภายในคือ `deinonychus_savana` ส่วนชื่อที่แสดงใน build ปัจจุบันอาจเป็น Acernychus จึงใช้ entity id 2039 เป็นตัวระบุหลัก

| รายการ | ค่า |
|---|---|
| Framework | Raptor |
| Model | `Raptor/Deinonychus_savanaPrefab` |
| Root motion set | `deinonychus_savana` |
| AI factor id | `deinonychus_savana_ai` |
| Type | Carnivore |
| Attack | `(9.84 + combat_level * 0.41) * unstable_factor` |
| Life max | `(0.306 * ((combat_level + 24) ** 2)) * unstable_factor` |
| Attack cooltime | 1.7 s |
| Bound radius | 50 |
| Blow resistance | 420 |
| Knock-back resistance | 300 |
| Groggy duration | 8 s |
| Knock-down duration | 8 s |

Framework: `Raptor_framework.asset`

Attack ที่พบ:

| Action | Motion | Bind/Rotation | Hit geometry |
|---|---|---|---|
| `raptor_dash` | `Raptor_Attack_Dash` | `bound_enemy=true`, rot speed 0 | frame 23, radius 125, angles 130–250° |
| `raptor_jump` | `Raptor_Attack_Jump` | ตาม framework | frame 19, radius 70, offset (-20,80) |
| `raptor_attack` | `Raptor_Attack` | ตาม framework | frame 13, rectangle 80×60, offset (0,30) |
| `dilopho_tail` | tail attack | ตาม framework | ใช้ AttackInfo ของ asset |
| `raptor_counter` | counter | ตาม framework | ใช้ AttackInfo ของ asset |

หมายเหตุออกแบบ:

- jump ต้องขยับ actor เข้าหาเป้าหมายตาม root motion ไม่เล่นกระโดดอยู่กับที่
- orientation ของ mesh, root-motion forward และ logical yaw ต้องผ่าน adapter เดียว เพื่อไม่วาด animation กลับด้าน
- ระหว่าง chase/recover ต้องมี stand/recovery state ที่ต่อเนื่อง ไม่สลับเดิน-หยุดทุก frame
- reaction ที่กระเด็นต้องขยับ actor base มิฉะนั้น animation จบแล้วจะวาร์ปกลับ

## Directional damage animation

Framework ต้นฉบับของสัตว์ทั้งสามมี mapping สี่ทิศเหมือนกัน:

| DamageDirection | Motion suffix | Phenacodus | Tricera | Raptor |
|---|---|---|---|---|
| Front | `_Damage_S` | `Phenaco_Damage_S` | `Tricera_Damage_S` | `Raptor_Damage_S` |
| Back | `_Damage_N` | `Phenaco_Damage_N` | `Tricera_Damage_N` | `Raptor_Damage_N` |
| Left | `_Damage_E` | `Phenaco_Damage_E` | `Tricera_Damage_E` | `Raptor_Damage_E` |
| Right | `_Damage_W` | `Phenaco_Damage_W` | `Tricera_Damage_W` | `Raptor_Damage_W` |

ชื่อ N/S/E/W เป็นชื่อ motion ใน asset ไม่ควรเดาความหมายจากชื่อแกนโดยตรง ให้ใช้ mapping ของ `AnimationElemDirectional` เป็นหลัก

## Evade และ low-health retreat

ใช้กับสัตว์ทั้งสาม:

- คำนวณ Hit/Miss/Dodge ก่อนเลือก animation
- เมื่อ Dodge และ state interrupt ได้ ให้เล่น motion Evade ของ Framework
- ถ้า Dodge เกิดระหว่าง attack ที่ล็อก animation ให้ damage เป็นศูนย์/ผล Dodge แต่ไม่เล่น Evade ทับ attack
- เมื่อ HP < 20% ให้มีโอกาสน้อยเข้า `LowHealthRetreat`
- retreat ราว 6 วินาที แล้วกลับเข้า battle หากเป้าหมายยังถูกต้อง
- ค่าโอกาส/ระยะ/เวลาที่ไม่มีใน asset ต้องอยู่ใน reconstructed profile ไม่ฝังใน controller

## Cooldown และ Stand

ค่า `attack_cooltime` ดิบคือ 1.3 / 1.6 / 1.7 วินาทีตามลำดับ ไม่ควรบวกค่าคงที่จาก code เก่าโดยไม่ระบุเหตุผล

ช่วงที่ action lock จบแต่ skill ยัง cooldown ให้เล่น stand/battle-stand ของ Framework และคง state เดิมจน scheduler อนุญาต action ถัดไป ห้ามใช้การสลับ chase/idle ทุก frame เป็นตัวจับเวลา

## ข้อมูล AI factor ที่ต้องสร้างใหม่

ไฟล์ปัจจุบันมีเพียง id แต่ไม่มี definition จึงต้องสร้าง profile เองสำหรับ:

- aggression/alert radius
- preferred/min/max attack range
- turn rate ก่อน commit
- attack weights และเงื่อนไขระยะ
- chase hysteresis
- recovery behavior
- low-health retreat chance/duration
- return-home distance

ทุกค่าที่สร้างต้องมี `Source = Reconstructed` และเปิดปรับใน config/data file ได้ เพื่อแก้ตามผลทดสอบโดยไม่แก้ state machine

