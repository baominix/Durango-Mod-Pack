# Neutral Dinosaur Status Effects — Archive vs original data

สถานะเอกสาร: **reference/audit สำหรับ Phase 5–7**  
ตรวจเมื่อ: **2026-08-25**

เอกสารนี้แยกหลักฐานออกเป็น 3 ชั้นเพื่อไม่ให้ข้อมูลจาก Wiki ถูกใช้แทนโค้ดเกม:

- **Archive** — คำอธิบายจากหัวข้อ
  [Neutral Dinosaur Status Effects](https://durango-archive.fandom.com/wiki/Status_Effects#Neutral_Dinosaur_Status_Effects)
  ซึ่งเป็นข้อมูลชุมชนและมีช่องที่ยังระบุ `<?>`
- **Original data/client** — `animal.json`, `status_effects.json`,
  `modifiers.json` และ C# ที่ AssetRipper export จากเกม
- **Current plugin** — สิ่งที่ `DurangoCombatSystemPlugin 0.3.9` ทำจริงในขณะนี้

ข้อสรุปสั้น: Archive อธิบายแนวคิดหลักได้ตรงกับข้อมูลเกมหลายรายการ แต่ไม่ครบ
โดยเฉพาะ `Internal Injury` ซึ่งแรงตาม level และ `Balance Loss` ที่มีในข้อมูลเกม
แต่ไม่มีในตาราง Archive ส่วนปลั๊กอินปัจจุบันยังไม่มีระบบ body-part injury/status effect

## 1. รายการจาก Archive

ในหัวข้อ Neutral Dinosaur Status Effects ช่อง Buffs และ Neutral ไม่มีรายการ ส่วน
Debuffs มี 10 รายการต่อไปนี้ (สรุปความ ไม่คัดลอกคำบรรยายเต็ม):

| สถานะใน Archive | ความหมาย/ผลที่ Archive ระบุ | ความมั่นใจจากหน้า Archive |
|---|---|---|
| Body Injury | ร่างกายส่วนลำตัวเสียหาย และสัมพันธ์กับ Internal Injury | กลไก/ระยะเวลาไม่ระบุ |
| Concussion | ลดความแม่นยำ 20% | ระบุผลตัวเลข |
| Head Injury | ศีรษะเสียหาย และอาจนำไปสู่ Horn/Concussion/Teeth Injury | ชนิดสัตว์และระยะเวลาไม่ระบุ |
| Heavy Footed | ลดความสามารถหลบ 40 | ระบุผลตัวเลข |
| Horn Injury | ลดความเสียหาย 20% | หน้า Archive ระบุว่ายังควรตรวจข้ามชนิดสัตว์ |
| Internal Injury | Life ลด 0.25 ต่อวินาที | หน้า Archive ระบุว่ายังควรตรวจข้ามชนิดสัตว์ |
| Leg Injury | ขาเสียหาย และสัมพันธ์กับ Heavy Footed | ชนิดสัตว์และระยะเวลาไม่ระบุ |
| Tail Injury | หางเสียหาย และสัมพันธ์กับ Tail Muscle Injury | ชนิดสัตว์และระยะเวลาไม่ระบุ |
| Tail Muscle Injury | ลดความเสียหาย 20% | หน้า Archive ระบุว่ายังควรตรวจข้ามชนิดสัตว์ |
| Teeth Injury | ลดความเสียหาย 20% | หน้า Archive ระบุว่ายังควรตรวจข้ามชนิดสัตว์ |

คำว่า “สัมพันธ์กับ” สำคัญ: ข้อมูลจริงใน `animal.json` ไม่ได้ให้ parent status
สร้าง derived status ภายหลัง แต่ใส่ทั้ง base injury และ derived effect ไว้ใน
`status_effects_on_break` ของชิ้นส่วนนั้นโดยตรง

## 2. โครงสร้างในข้อมูลเกมจริง

### 2.1 Base injury flags

`status_effects.json` มีสถานะฐาน 4 ชนิด:

| ID | ประเภท effect | Key | หน้าที่ |
|---|---:|---|---|
| `head_injury` | 7 (`Flag`) | `head_injury` | ทำเครื่องหมายว่าศีรษะแตก |
| `body_injury` | 7 (`Flag`) | `body_injury` | ทำเครื่องหมายว่าลำตัวแตก |
| `leg_injury` | 7 (`Flag`) | `leg_injury` | ทำเครื่องหมายว่าขาแตก |
| `tail_injury` | 7 (`Flag`) | `tail_injury` | ทำเครื่องหมายว่าหางแตก |

ทั้ง 4 รายการมี tag `clear_on_death` และมีไอคอนเฉพาะ แต่ไม่มีตัวเลขลด stat
ด้วยตัวเอง ผลทางสถิติมาจาก derived status ที่จับคู่ใน `animal.json`

ฝั่ง C# ยังมี `Shared.Animal.Flags` สำหรับ Head/Body/Arm/Leg/Tail/Back injury
และ `Messages.Damage.Part` ส่ง `Shared.Battle.BodyPart` มากับผลโจมตี จึงยืนยันว่า
body part เป็นส่วนหนึ่งของ protocol เดิม ไม่ใช่แนวคิดที่ Wiki สร้างขึ้นเอง

### 2.2 Derived status effects

| ID ในข้อมูลจริง | ชื่อที่เทียบกับ Archive | ผลจริงจาก `status_effects.json` | หมายเหตุ |
|---|---|---|---|
| `head_injury_raptor` | Teeth Injury | `damage_bonus = -0.2` | ลด damage 20% |
| `head_injury_direwolf` | Teeth Injury | `damage_bonus = -0.2` | ลด damage 20% |
| `head_injury_stego` | Concussion | `hit_rate_plus = -0.2` | ลด hit rate 20% |
| `head_injury_tricera` | Horn Injury | `damage_bonus = -0.2` | ลด damage 20% |
| `head_injury_brachio` | Concussion | `hit_rate_plus = -0.2` | ลด hit rate 20% |
| `body_injury_default` | Internal Injury | `life = -0.25 * level` | max level 6; ไม่ใช่ค่าคงที่ทุกตัว |
| `leg_injury_*` | Heavy Footed | `dodge_plus = -40.0` | มี raptor/direwolf/stego/tricera/brachio |
| `tail_injury_stego` | Tail Muscle Injury | `damage_bonus = -0.2` | ลด damage 20% |
| `tail_injury_brachio` | Tail Muscle Injury | `damage_bonus = -0.2` | ลด damage 20% |
| `tail_injury_raptor` | **Balance Loss** | `hit_rate_plus = -0.2` | ไม่มีในตาราง Archive |
| `tail_injury_direwolf` | **Balance Loss** | `hit_rate_plus = -0.2` | ไม่มีในตาราง Archive |
| `tail_injury_tricera` | **Balance Loss** | `hit_rate_plus = -0.2` | ไม่มีในตาราง Archive |

`modifiers.json` ยืนยันความหมายของ key: `damage_bonus` คือ damage,
`hit_rate_plus` คือ hit rate และ `dodge_plus` คือ dodge ability

### 2.3 สิ่งที่ Archive ระบุไม่ครบหรือคลาดจากข้อมูลจริง

1. `Internal Injury` เป็น `-0.25 * level` ไม่ใช่ `-0.25` ตายตัว
2. Tail injury ไม่ได้กลายเป็น Tail Muscle Injury ทุก framework; Raptor,
   Direwolf และ Tricera ใช้ Balance Loss ลด hit rate 20%
3. Base injury เป็น flag แยกจาก derived status และ `animal.json` สั่งเพิ่มทั้งคู่
   เมื่อ part แตก
4. ระยะเวลาของ injury ไม่ได้กำหนดใน entry เหล่านี้ และทุก entry มี
   `clear_on_death`; การรักษา/ล้างนอกเหนือจากการตายต้องตรวจ flow ฝั่ง server เพิ่ม
5. client code ที่พบทำหน้าที่รับ/แสดง status; การตัดสิน part HP แตกและสร้าง status
   เป็น authoritative logic ฝั่ง server ซึ่งไม่มี implementation เต็มอยู่ใน client
   decompile

## 3. ผลสำหรับสัตว์ทดลอง 3 ตัว

### 2027 Zebraceratops (`framework = Tricera`)

| Part | HP ratio | เมื่อแตก | ผล derived |
|---|---:|---|---|
| Body | 0.50 | `body_injury` + `body_injury_default` level 2 | Life `-0.50/s` |
| Head | 0.20 | `head_injury` + `head_injury_tricera` level 1 | Damage `-20%` |
| Leg | 0.30 | `leg_injury` + `leg_injury_tricera` level 1 | Dodge ability `-40` |

ไม่มี Tail part ใน entity นี้ จึงไม่ควรสร้าง Tail Injury/Balance Loss แม้ Framework
Tricera จะมี derived tail status อยู่ในฐานข้อมูลรวม

Part probability ตามทิศผู้โจมตี:

| ทิศ | Body | Head | Leg |
|---|---:|---:|---:|
| Front | 20% | 70% | 10% |
| Right/Left | 30% | 10% | 60% |
| Back | 70% | 10% | 20% |

### 2037 Elephantulus (`framework = Phenacodus`)

มีเฉพาะ Body part:

- Body HP ratio `0.50`
- ทุกทิศเลือก Body 100%
- เมื่อแตกเพิ่ม `body_injury` และ `body_injury_default` level 1
- Internal Injury จึงลด Life `0.25/s`

ไม่มี Head/Leg/Tail injury สำหรับ entity นี้ตาม `animal.json`

### 2039 Deinonychus Savanna (`framework = Raptor`)

| Part | HP ratio | เมื่อแตก | ผล derived |
|---|---:|---|---|
| Body | 0.50 | `body_injury` + `body_injury_default` level 4 | Life `-1.00/s` |
| Head | 0.20 | `head_injury` + `head_injury_raptor` level 1 | Damage `-20%` |
| Leg | 0.30 | `leg_injury` + `leg_injury_raptor` level 1 | Dodge ability `-40` |

ใช้ part probability ชุดเดียวกับ Zebraceratops และไม่มี Tail part ดังนั้น
`tail_injury_raptor`/Balance Loss ไม่ควรเกิดกับ entity 2039 จากข้อมูลปัจจุบัน

## 4. Flow ฝั่ง client เดิมที่ยืนยันได้

```text
authoritative server decides hit part / part break / active effects
  -> Messages.Damage.Part
  -> Messages.StatusEffects per entity
  -> StatusEffectSystem stores and raises Added/Removed/Updated
  -> UI reads template + EffectDetail
  -> InjuryEffectEmitter plays part-break particle/sound for base injury
```

หลักฐานสำคัญ:

- `StatusEffectSystem` รับ `Messages.StatusEffects` จาก Frontend และเก็บตาม entity ID
- `StatusEffect` ใช้ `Messages.EffectDetail` ซึ่งประกอบด้วย EffectType, Key, Value
- `InjuryEffectEmitter` ฟัง `StatusEffectAdded` และแสดง particle/sound ให้
  `head_injury`, `body_injury`, `leg_injury`, `tail_injury`
- `Messages.Damage` มี `BodyPart Part` อยู่ใน packet ผลโจมตี

## 5. เทียบกับ DurangoCombatSystemPlugin 0.3.9 ปัจจุบัน

| ส่วน | สถานะปัจจุบัน | ผลที่ตามมา |
|---|---|---|
| เลือก part | R9B ใช้ deterministic roll จาก `part_probability` ตามทิศ | ส่ง `Damage.Part` จริงแล้ว |
| HP แยกส่วน | R9C เก็บต่อ entity/object generation จาก `MaximumLife * hp_ratio` | แตกครั้งเดียวต่อ part และไม่ลด Life รวมซ้ำ |
| โหลดข้อมูล | profile เก็บ `body_parts`, defense/dodge ratio, break statuses และ `part_probability` แล้ว | ใช้ hp ratio และ break status จริงแล้ว |
| Status state | R9D cache active modifier ต่อสัตว์จาก EffectDetail เดิม | damage/hit/dodge/life velocity ทำงานแล้ว |
| Status protocol | R9C ส่ง full `Messages.StatusEffects` จาก template เดิม | UI/ไอคอน/`InjuryEffectEmitter` พร้อม field test |
| Phase 5A AI | state/เดิน/หัน/attack intent เท่านั้น | ไม่ควรผูก injury เข้ากับ AI state โดยตรง |

ดังนั้น build R9D ต้องเห็น part ตาม probability, injury เมื่อ part HP ผ่านศูนย์ และ
derived effect เปลี่ยน damage/dodge/life ตามตัวเลขจริงโดยไม่เพิ่ม status ซ้ำ

## 6. แนวทางเพิ่มใน Phase ถัดไป

ให้ทำเป็นระบบ `AnimalInjuryRuntime` แยกจาก Saurus state machine:

1. เพิ่ม parser สำหรับ `body_parts`, `part_probability`, `hp_ratio`,
   `defense_ratio`, `dodge_ratio` และ `status_effects_on_break`
2. เลือก part ด้วย deterministic roll จากทิศของ hit ตาม probability table
3. เก็บ part HP แยกต่อ entity/object generation และลดเพียง part ที่ถูกเลือก
4. เมื่อ part ผ่าน break threshold ครั้งแรก เพิ่ม base + derived status จากข้อมูล
5. ใช้ active modifier กับ animal damage, hit rate, dodge และ life velocity
6. ส่ง `Messages.StatusEffects` เพื่อให้ client UI และ `InjuryEffectEmitter` ทำงาน
7. ล้าง injury/status state เมื่อสัตว์ตาย, despawn, เปลี่ยนโลก หรือ Return to Title

กฎสำคัญ: status effect ต้องเป็นผลของ simulation authoritative; animation/UI เป็น
presentation ที่รับผล ไม่ควรเป็นผู้ตัดสินว่า part แตกหรือ stat ลด

## 7. ไฟล์หลักที่ใช้ตรวจ

- `ReferenceData/animal.json` — entity `2027`, `2037`, `2039`, body parts,
  probability และ `status_effects_on_break`
- Original `Resources/offline/assets/survival/status_effects.json` — template และ
  effect formula ของ injury
- Original `Resources/offline/assets/skill/modifiers.json` — ความหมาย modifier key
- Original `Scripts/Assembly-CSharp/StatusEffectSystem.cs` — message/store/event flow
- Original `Scripts/Assembly-CSharp/Durango/Render/Particle/InjuryEffectEmitter.cs`
- Original `Scripts/Assembly-CSharp/Messages/Damage.cs`
- Current `Damage/PlayerHitResolver.cs`
- Current `Runtime/AnimalCombatTargetRegistry.cs`
