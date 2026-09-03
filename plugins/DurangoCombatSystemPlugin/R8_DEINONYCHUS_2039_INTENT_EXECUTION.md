# R8 — Deinonychus 2039 Intent Execution

วันที่ดำเนินการ: 2026-08-30

สถานะ: implementation/build/deploy เสร็จ รอ field test

Build/deploy: `2026-08-30 01:44:02` — SHA-256
`191BAE6CB9DD4B3CC1949CEA76472A5B63389D47130D4B3E03473DB98BBAAD53`

## ขอบเขต

R8 ย้าย `2039 deinonychus_savana` จาก legacy range selector ไปใช้ pipeline
`Combat Context → Intent → Action Plan` โดยไม่แก้ frozen player runtime รุ่น
`0.3.9` และไม่แชร์ species profile กับ `2001 raptor`

ข้อมูลต้นฉบับของ 2039 ยังคงมาจาก:

- entity type `2039`, model `Deinonychus_savanaPrefab`
- AI factor `deinonychus_savana_ai`
- Framework/root-motion id `Raptor` / `deinonychus_savana`
- `attack_cooltime=1.7`, represent scale `1.0`
- geometry, hit frame, root position และ root yaw จาก Framework/AnimationClip เดิม

## Intent ที่เปิดใช้

| Intent | Action | Surface distance | Weight/Priority | Alignment | Provenance |
|---|---|---:|---:|---|---|
| `CounterAttack` | `raptor_counter` | 0–150 | 1 / 100 | Bind target at commit | Original geometry; trigger Reconstructed |
| `GapCloser` | `raptor_jump` | 120–430 | 5 / 60 | Face target before commit | Original + Observed; range Reconstructed |
| `GapCloser` | `raptor_dash` | 180–340 | 3 / 60 | Face target before commit | Original geometry/root yaw; intent Reconstructed |
| `StandardFront` | `raptor_attack` | 0–220 | 5 / 50 | Bind target at commit | Original + Observed |

เมื่อ jump และ dash eligible พร้อมกัน resolver สุ่มตาม weight เฉพาะภายใน
`GapCloser` priority เดียวกัน ไม่สุ่มข้ามไป `StandardFront`

## Counter contract

`raptor_counter` เปิด window 1.25 วินาทีเมื่อเกิดเหตุการณ์ใดเหตุการณ์หนึ่งกับสัตว์
ตัวนั้นและ engagement ปัจจุบัน:

- player attack เป็น `Missed`
- animal ได้ผล `Dodged` จาก player attack

Miss และ Dodge ยังเป็นผลแยกกันตาม damage resolver เดิม หาก Dodge เกิดระหว่าง
active animal attack จะไม่ตัด animation ด้วย Evade; counter จะถูกพิจารณาเมื่อถึง
action boundary เท่านั้น Window 1.25 วินาทีเป็นค่า Reconstructed จนกว่าจะพบ AI
factor เดิมที่ระบุ trigger/timing นี้โดยตรง

## Dash/root-yaw audit

`Raptor_Attack_Dash` มี hit ที่ frame 23/30 และข้อมูล root ดิบใกล้ hit:

- local position ประมาณ `(-57.28, +316.24)`
- local yaw ประมาณ `-201.26°`
- circular area radius `125`, sector `130..250°`

พื้นที่ด้านหลังที่ hit frame ไม่ได้แปลว่าให้เริ่มท่าโดยหันหลังให้ player ใน R8 จึง
จำกัด eligibility ไว้ที่ Front และหันหา target ก่อน commit จากนั้น
`SaurusActionPlan` ใช้ root position/yaw เดิมพาตัวผ่านเป้าหมายและวางพื้นที่จากฐาน
จริงเดียวกับ telegraph/damage query

## Audit gate ที่ยังปิด

`dilopho_tail` ยังไม่ถูก execute แม้อยู่ใน Framework `Raptor` เพราะยังไม่มี
หลักฐานว่า animation/model binding เข้ากับ `Deinonychus_savanaPrefab` อย่างถูกต้อง
และยังไม่ยืนยัน rear-context ของท่านี้ การพบชื่อ action เพียงอย่างเดียวไม่เพียงพอ
ต่อการเปิด runtime

## Cooldown และ reposition

- ใช้ cooldown จริงของ 2039 คือ `1.7` วินาที
- Recover ยืน 1.0 วินาทีและนับซ้อนใน cooldown
- เวลาคงเหลืออยู่ใน Face stateและหันตาม player ต่อเนื่อง
- เมื่อ player อยู่ flank/rear และไม่มี turn action ที่ผ่าน audit จะ
  Reposition-to-Front โดยไม่สลับ Approach/Recover ทุก frame

## Field-test gate

1. ใช้ `/combatspawn 2039 60`
2. ระยะใกล้เห็น `raptor_attack`; ระยะกลางเห็นทั้ง jump และ dash
3. jump ต้องเคลื่อนฐานเข้าหาเป้าหมาย ไม่กระโดดอยู่กับที่และไม่วาร์ปกลับ
4. dash ต้องให้ mesh, วงฐาน, เส้น, logical base และ damage ตรงกันตลอด root turn
5. เดินเป็นวงกลมระหว่าง cooldown: ต้องไม่เกิดเดิน–หยุดถี่หรือ state jitter
6. ทำให้ player Miss หรือ animal Dodge ในระยะประชิด: counter เกิดได้ภายใน
   1.25 วินาที แต่ไม่ตัด active attack
7. ต้องไม่เห็น `dilopho_tail`
8. Evade/damage reaction สี่ทิศและ player normal auto-approach ต้องไม่ regression

## Presentation-base hotfix หลัง R8

พบว่า original `RootMotionMovable` ชดเชย root curve ด้วยการเลื่อน
`MeshObjectTransform.localPosition` ทำให้ mesh อยู่ถูกตำแหน่ง แต่ renderer ลูกที่
ไม่ผูกกระดูก เช่นวงฐานแดง ถูกพาออกจาก logical base ไปด้านหน้า

hotfix จึงทำงานหลัง `AnimalBehavior.LateUpdate`: ย้าย compensation จาก parent
กลับไปยัง `Bip001` โดยรักษา world pose ของกระดูกไว้ และคืน local position ของ
MeshObject เป็นค่าจาก prefab วิธีนี้ไม่เปลี่ยน `AnimalBehavior.CurrentPosition`,
action plan, telegraph, hit geometry หรือ damage snapshot และจำกัดเฉพาะสัตว์ที่
มี Saurus motion adapter ลงทะเบียนอยู่

ค่า local position อ้างอิงถูกอ่านหลัง `ResetRootMotionOffset()` เพื่อไม่เก็บ offset
ชั่วคราวจาก animation ที่กำลังเล่นเป็นฐานใหม่ รุ่นรวม hotfix และ R9A ถูกส่งออกที่
`2026-08-30 02:28:57` — SHA-256
`FF6CB480C2979807C52F22DD2AEBE686AF24C6BE39638EC1CA79946BEC07B048`
