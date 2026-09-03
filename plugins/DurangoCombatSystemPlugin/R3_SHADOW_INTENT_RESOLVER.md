# R3 — Shadow Intent Resolver

## สถานะ

- implementation/build ผ่านบนฐาน `DurangoCombatSystemPlugin 0.3.9`
- resolver เป็น shadow/read-only และไม่มีสิทธิ์สั่ง animation, movement, telegraph,
  damage หรือเปลี่ยน action ที่ selector เดิมเลือก
- ประเมินเมื่อ selector เดิมถึง selection boundary และเมื่อเรียก diagnostic command
  เท่านั้น ไม่ทำงานทุก frame/context capture
- field-test เหตุผลและการเทียบ legacy selection ยังเป็น gate ก่อนเริ่ม R4
- runtime ยังรับเฉพาะ `2027`, `2037`, `2039`; `raptor 2001` รอ species runtime
  profile ใน R7 ตาม safety gate เดิม

## Decision flow

resolver รับ immutable `SaurusCombatContext`, short-term memory และ Framework
attack definition แล้วประเมินตามลำดับ:

1. Counter event window
2. Escape/low-health policy
3. Turn/Rear policy
4. Area control
5. Gap closer
6. Standard front
7. Approach/Reposition/Stand เมื่อไม่มี attack ที่ผ่าน

Dead และ active action/reaction lock จะหยุด decision ก่อน ส่วน cooldown ปิด normal
intent แต่ยังยอมให้ shadow แสดง Counter ที่ event window เปิด เพื่อให้เห็นว่าระบบใหม่
ควรแทรกต่างจาก selector `0.3.9` ตรงไหน

## Eligibility ที่ตรวจ

- Framework มี attack key, motion และ hit data จริง
- signed sector ตรงกับ rule
- surface distance อยู่ในช่วงของ species rule
- path ไม่ได้รายงาน `Blocked` สำหรับ gap closer
- event อยู่ใน generation และ engagement เดียวกันและยังไม่หมดเวลา
- action ที่หลักฐานไม่ครบถูกปิดด้วย explicit audit block

ทุก candidate คืนเหตุผล `eligible` หรือ `rejected` พร้อม provenance; ไม่มี fallback ที่
สุ่มข้าม intent

## Deterministic roll

เมื่อมี candidate priority เดียวกันหลายตัว ใช้ roll จาก hash ของ:

- actor entity id
- world generation
- engagement id
- shadow decision sequence

roll นี้ไม่เรียก `SaurusRandom.Range()` จึงไม่กินหรือเปลี่ยน random stream ของ
selector เดิม

## Candidate matrix ใน R3

| Species | Action | Shadow intent | สถานะ |
|---|---|---|---|
| 2027 | `tricera_head`, `tricera_once` | StandardFront | ประเมินได้ |
| 2027 | `tricera_dash` | GapCloser | ประเมินได้ |
| 2027 | `tricera_turn` | TurnAttack | ประเมินได้แต่ยังไม่ execute |
| 2027 | `tricera_counter` | CounterAttack | Miss/Dodge window 1.25s; ยังไม่ execute |
| 2037 | `phenaco_bite` | StandardFront | ประเมินได้ |
| 2037 | `phenaco_jump` | GapCloser | ประเมินได้ |
| 2037 | `phenaco_gas` | AreaControl/Rear | ประเมินได้ |
| 2037 | `phenaco_attack_escape` | EscapeStrike | audit-block: trigger ยังไม่ยืนยัน |
| 2039 | `raptor_attack` | StandardFront | ประเมินได้ |
| 2039 | `raptor_jump` | GapCloser | ประเมินได้ |
| 2039 | `raptor_counter` | CounterAttack | Miss/Dodge window 1.25s; ยังไม่ execute |
| 2039 | `raptor_dash` | GapCloser/spacing candidate | audit-block: geometry ด้านหลังทำให้ intent ยังไม่ยืนยัน |
| 2039 | `dilopho_tail` | TurnAttack candidate | audit-block: model compatibility ยังไม่ยืนยัน |

`tricera_turn` และ counter อาจถูก shadow แนะนำ แต่ action ที่ทำงานจริงยังมาจาก
`SaurusAttackSelector.Select()` และ species list ของ `0.3.9` เท่านั้น

## Legacy comparison

ก่อนเรียก selector เดิม controller สร้าง shadow decision จาก context ที่ถูกจับก่อน
Face/Select จากนั้นเก็บ action key ที่ selector เดิมเลือกจริงลง
`LastSelectionShadowDecision`

ผลเทียบมีสองสถานะหลัก:

- `same-action` — shadow และ legacy เลือก key เดียวกัน
- `different` — intent-aware shadow เสนอคนละ key หรือไม่มี action ฝั่งหนึ่ง

ความต่างไม่ใช่ error ใน R3 แต่เป็นข้อมูลสำหรับกำหนด policy R4–R8

## Developer command

คำสั่งอยู่ใน `DeveloperModePlugin 0.1.3`:

- `/combatintent` หรือ `/combatintent nearest`
- `/combatintent all`
- `/combatintent <entityId>`

รายละเอียดแสดง decision ปัจจุบัน, deterministic roll, candidate rejection ทุกตัว และ
`LastSelection` ที่เทียบกับ selector เดิม คำสั่งเรียก API อ่านอย่างเดียว
`CombatRuntime.TryGetSaurusIntentReport()` ผ่าน reflection

## Field-test gate

1. `/dev on`
2. spawn ทีละ species และใช้ `/combatintent nearest` ก่อน/หลังเข้า battle
3. ทดสอบ Front/Flank/Rear และระยะใกล้/กลาง/ไกล
4. ทำให้เกิด Miss หรือ Dodge แล้วเรียกคำสั่งภายใน counter window
5. ตรวจว่า path blocked ไม่เสนอ gap closer
6. ตรวจ `LastSelection` หลังสัตว์ออกท่าจริงหลายครั้ง
7. ใช้ `/combatintent all` กับหลายตัวและตรวจว่า entity ไม่ปนกัน

เกณฑ์ผ่าน R3 คือทุก decision อธิบายได้, audit-block ไม่หลุดเป็น eligible,
generation/engagement/event ไม่ข้ามกัน และ gameplay เทียบกับ `0.3.9` ไม่เปลี่ยน
