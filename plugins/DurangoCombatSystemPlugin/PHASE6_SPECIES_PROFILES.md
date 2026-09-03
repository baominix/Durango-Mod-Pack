# Phase 6 — Species profiles และ original root motion

Phase 6 เพิ่มพฤติกรรมเฉพาะชนิดเหนือ state machine กลางของ Phase 5 โดยไม่แก้
player action runtime ที่ผ่าน field test แล้ว รุ่นปลั๊กอินยังคงเป็น `0.3.9`

## Source of truth

- attack key, animation, hit frame และ geometry: Framework asset ต้นฉบับ
- `attack_cooltime`: `animal.json`
- ตำแหน่งฐานระหว่าง animation: curve `Bip001` ใน AnimationClip ต้นฉบับ
- ระยะเลือกท่า, weight และโอกาส retreat: reconstructed จาก geometry กับคลิป
  gameplay จึงแยกอยู่ใน `SaurusSpeciesProfile`

สคริปต์ `Generate-SaurusRootMotionData.ps1` อ่าน `m_PositionCurves` ของ
AnimationClip แล้วสร้าง `ReferenceData/saurus_root_motion.json` แบบทำซ้ำได้
runtime ใช้ cubic Hermite พร้อม tangent ของ key เดิม ไม่ใช้ระยะ hardcode ต่อ frame

## Species profile

### 2027 Zebraceratops

| attack | ระยะเลือกหลังหักขอบตัว | weight |
|---|---:|---:|
| `tricera_counter` | 0–190 | 1 |
| `tricera_head` | 0–330 | 3 |
| `tricera_once` | 0–380 | 4 |
| `tricera_dash` | 300–1050 | 4 |

`once`, `head`, `dash` และ `counter` ใช้ hit frame/geometry ของ Framework โดยตรง
รวม rectangle หลายช่วงและวงปิดท้ายของ dash

### 2037 Elephantulus

| attack | ระยะเลือกหลังหักขอบตัว | weight |
|---|---:|---:|
| `phenaco_bite` | 0–230 | 5 |
| `phenaco_jump` | 150–520 | 4 |
| `phenaco_gas` | 0–300 | 1 |

Elephantulus เข้า battle เมื่อถูกโจมตีตามปกติ ไม่เริ่มด้วยการหนี
`phenaco_attack_escape` เป็น attack ของ Framework ที่มีสี่ hit และพื้นที่อยู่ด้านหลัง
พร้อม root/yaw ที่หมุนหลังให้ target ก่อนเคลื่อนออก จึงไม่อยู่ใน normal selector
แต่ถูก commit เป็น EscapeStrike เมื่อ low-health retreat trigger สำเร็จ
การโจมตีด้านหน้าเลือกโดยตรง

### 2039 Deinonychus

| attack | ระยะเลือกหลังหักขอบตัว | weight |
|---|---:|---:|
| `raptor_counter` | 0–150 | 1 |
| `raptor_attack` | 0–220 | 5 |
| `raptor_jump` | 120–430 | 5 |
| `raptor_dash` | 180–340 | 3 |

Jump และ dash เคลื่อน logical actor ตาม `Bip001` จึงไม่กระโดดอยู่กับที่หรือวาร์ป
กลับหลัง animation จบ ช่วงเลือก dash อิงตำแหน่งลงพื้น frame 23 และ rear arc ดั้งเดิม
เพื่อไม่ให้เลือกท่านี้ขณะเป้าหมายอยู่นอกพื้นที่หลังการพุ่ง

## Root-motion contract

เมื่อ commit attack/reaction ระบบล็อก yaw และใช้ curve เดียวกันสามจุด:

1. `RootMotionMovable` หักการเลื่อน `Bip001` ออกจาก mesh
2. `SaurusMotionAdapter` นำ delta เดิมไปเลื่อน `AnimalBehavior.CurrentPosition`
   ผ่าน collision sliding
3. `AnimalAttackSnapshot` คำนวณ actor origin ของแต่ละ hit จาก curve ณ
   `hit.frame / clip.frameRate`

ดังนั้นภาพสัตว์ ฐาน logical เส้นเตือน และ hit query อ้างข้อมูลต้นฉบับชุดเดียวกัน
และสัตว์ไม่หันตามเป้าหมายใหม่หลัง commit จนท่าจบ `RootMotionMovable` เปิดการชดเชย
root position ต่อเนื่องข้าม attack → recover เพื่อไม่ให้ mesh โผล่ที่ตำแหน่งอื่นหนึ่งเฟรม
และใช้ actor yaw ที่ล็อกไว้แทน baked Bip001 yaw เพื่อให้ภาพหันตรงกับ hit geometry

## Reaction และ cooldown

- Damage ใช้ `knock_back_motion_map` ของ Framework ครบ Front/Back/Left/Right
- Dodge เล่น `evade` เมื่อ state อนุญาต ถ้าสัตว์กำลัง attack ผล Dodge ยังเกิดแต่
  Evade ไม่ตัด animation โจมตี
- Damage/Blow animation ใช้ root curve เดิมเพื่อลดอาการกระเด็นแล้ววาร์ปกลับ
- Blow routing พร้อมทำงานเมื่อ damage resolver ส่ง `DamageEffects.Blow`; การตัดสิน
  blow/knockdown เต็มรูปแบบยังเป็น Phase 7
- cooldown ต่อท่าเป็น `animal.attack_cooltime + 1.0` วินาที และเล่น battle stand
  ระหว่างรอ
- เมื่อ HP เหลือไม่เกิน 20% จะสุ่ม retreat หนึ่งครั้งต่อ engagement โอกาส 15%;
  ถ้าติดจะวิ่งหนีจากผู้เล่น 6 วินาทีแล้วกลับเข้า battle ทั้งสาม species

## Field-test gate

1. เรียก `2027`, `2037`, `2039` ทีละตัวและปล่อยให้ใช้ท่าครบหลายรอบ
2. ตรวจว่าเส้นเตือนและ damage อยู่ที่ตำแหน่งฐานตาม hit frame ไม่ติดฐานตอนเริ่มท่า
3. ตรวจว่า mesh หันทิศเดียวกับเส้นเตือน โดยเฉพาะ `raptor_dash` และ
   `phenaco_jump`; selector ต้องไม่เรียก `phenaco_attack_escape` เป็นท่าโจมตีด้านหน้า
4. ตรวจ `tricera_head/once/dash`, `phenaco_jump` และ `raptor_jump/dash`
   ว่าไม่วาร์ปกลับหรือวาด mesh กระพริบที่ตำแหน่งอื่นเมื่อ animation จบ
5. โจมตีสัตว์จากหน้า หลัง ซ้าย ขวา และดู damage animation
6. ทำให้เกิด Dodge ระหว่าง idle/recover และระหว่าง attack; กรณีหลังต้องไม่ตัด attack
7. ลด HP ต่ำกว่า 20% หลาย engagement เพื่อยืนยันว่า retreat เกิดน้อยและใช้เวลาราว
   6 วินาที
