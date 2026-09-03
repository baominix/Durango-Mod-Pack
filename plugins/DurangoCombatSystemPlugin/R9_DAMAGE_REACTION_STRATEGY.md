# R9 — Damage and Reaction Strategy

วันที่ดำเนินการ: 2026-08-30

สถานะ: **R9A–R9E implemented / R9E รอ field test** — สูตร
Defense/Dodge/Evade, body part, part HP/injury modifier และ reaction runtime
อ่าน metadata ต้นฉบับแล้ว โดย active attack เป็น atomic และคิว reaction ไว้เล่น
ที่ safe boundary หลัง animation จบ

Build/deploy R9E: `2026-08-31 02:18:55` — SHA-256
`9D7EC44140EAD68D5DE0DA1E76AAAF5357F3A7BB8EF00C66413F36C67F985C13`

staging และ deployed DLL มีขนาด `192512` bytes และ hash ตรงกัน ไม่มี legacy
`BepInEx/plugins/DurangoCombatSystemPlugin.dll` ซ้ำที่ plugin root

Field test: **R9C presentation gate passed** — ผู้ทดสอบยืนยันว่าเมื่อ part แตก
client เล่น injury icon/effect จริงผ่าน status flow เดิมของเกม

## 1. ขอบเขต R9A

เส้นทาง player → animal เดิมใช้ค่าประมาณดังนี้:

- Defense = `combat_level * 5`
- Dodge = `combat_level * 5`
- Evade = `0.20`

ค่าดังกล่าวตรงกับผลลัพธ์ของสัตว์ 4 ตัวที่เปิด runtime อยู่โดยบังเอิญ แต่ไม่เก็บ
provenance และจะผิดทันทีเมื่อเพิ่มสัตว์ที่ใช้สูตรต่างกัน R9A จึงให้
`PlayerHitResolver` ประเมิน `defense`, `dodge`, `evade` จาก profile ที่ loader
อ่านจาก animal.json โดยตรงผ่าน `AnimalFormulaEvaluator`

หาก parser อ่านสูตรไม่ได้ ระบบยังใช้ค่าเดิมเป็น fallback และเขียน warning ที่ระบุ
entity type/action/สูตรที่ล้มเหลว จึงไม่ทำให้ combat หยุดทำงานเงียบ ๆ

## 2. สูตรจริงของ species ปัจจุบัน

ทั้ง `2001 raptor`, `2027 zebraceratops`, `2037 elephantulus` และ
`2039 deinonychus_savana` ใช้สูตรเดียวกัน:

```text
defense = (0 + combat_level * 5) * unstable_factor
dodge   = (0 + combat_level * 5)
evade   = (0.2 + combat_level * 0)
```

runtime Single-player ปัจจุบันส่ง `unstable_factor=1.0` ดังนั้น R9A เปลี่ยนแหล่ง
ข้อมูล ไม่เปลี่ยนตัวเลขและไม่ควรเปลี่ยนผล field test ที่ผ่านแล้ว

## 3. Miss และ Dodge

ยังคงแยกเป็นสองผลตามระบบเกม:

1. Accuracy ของ player เทียบ Dodge ของสัตว์ → `Missed`
2. เมื่อ accuracy ผ่าน จึงทอย Evade แยก → `Dodged`
3. เมื่อผ่านทั้งสอง จึงใช้ Attack Rating เทียบ Defense และคำนวณ damage

Evade ที่เกิดระหว่าง animal attack ยังมีผลเป็น Dodge แต่ไม่ตัด animation โจมตี
ที่กำลังเล่น ส่วน reaction จะเริ่มที่ action boundary ตาม contract เดิม

## 4. R9B — การเลือก body part

- โหลด `body_parts` และ `part_probability` แล้วเลือก `Messages.Damage.Part`
  ด้วย deterministic roll จากทิศของ hit
- การคง `DamageEffects=0` ยังเป็น safety gate; ไม่สร้าง Blow/KnockBack/
  KnockDown จากชื่อ action หรือภาพ animation

### ตารางเลือก part ที่เปิดใช้

- Zebra/Raptor/Deinonychus: Front = Body 20%, Head 70%, Leg 10%;
  Flank = Body 30%, Head 10%, Leg 60%; Back = Body 70%, Head 10%, Leg 20%
- Elephantulus: Body 100% ทุกทิศ

roll ใช้ action instance, hit index และ entity id จึงให้ผลซ้ำเดิมสำหรับ hit เดียว
และไม่ใช้ `UnityEngine.Random` ร่วมกับ AI

## 5. R9C — part HP และ injury status

`AnimalInjuryRuntime` แยกจาก Saurus AI state machine และเก็บ state ต่อ
`entityId + GameObject instance id` ดังนั้น entity id ที่ถูกนำกลับมาใช้กับ object
รุ่นใหม่จะไม่รับ part HP ของตัวเก่า

- max HP ของแต่ละ part = `animal maximum life * body_parts.hp_ratio`
- landed hit ลด Life รวมตาม flow เดิมหนึ่งครั้ง และลดเฉพาะ part ที่
  `Messages.Damage.Part` เลือกอีกหนึ่ง ledger; part ledger ไม่ลด Life รวมซ้ำ
- part ประกาศ break เพียงครั้งแรกที่ HP ผ่านศูนย์
- status ที่เพิ่มอ่านจาก `status_effects_on_break` ของ part นั้นโดยตรง
- `EffectDetail` ประเมินด้วย `StatusEffectTemplateYaml` ของ client ที่ level จริง
  จึงไม่ hardcode `-0.25 * level`, `-0.2` หรือ `-40` ซ้ำใน plugin
- `Messages.StatusEffects` ส่งเป็น full snapshot ตาม contract เดิม และคง status
  ของระบบอื่นไว้ โดยแทนที่เฉพาะ injury ids ที่ runtime นี้เป็นเจ้าของ
- injury status ถูกล้างเมื่อสัตว์ตาย และ runtime state ถูกล้างเมื่อ session ปิด

R9C เปิด presentation ของ base injury ผ่าน `InjuryEffectEmitter` เดิม พร้อม icon
ของ base/derived statuses โดยยังไม่เปลี่ยน gameplay จน field test ยืนยันว่า client
แสดงผลถูกต้อง

## 6. R9D — derived injury modifiers

หลัง R9C presentation gate ผ่านแล้ว runtime ใช้ `EffectDetail` ชุดเดียวกับที่ส่ง
ให้ client เป็น source of truth และ cache modifier ใหม่เฉพาะเมื่อ part แตก:

- `damage_bonus`: คูณ animal attack ด้วย `1 + bonus`; Head Injury ของ Tricera/
  Raptor จึงเหลือ 80% ก่อนคิด penetration
- `dodge_plus`: บวกกับ Dodge จาก animal formula แล้ว clamp ไม่ต่ำกว่า 0;
  Leg Injury ของ species ปัจจุบันจึงลด Dodge 40
- `hit_rate_plus`: คูณ accuracy ด้วย `1 + bonus`; รองรับข้อมูล Tail Injury เดิม
  แม้สัตว์ 4 ตัวที่เปิดอยู่ยังไม่มี Tail part
- `Survival/life`: เปลี่ยน velocity ของ Life gauge และ ledger authoritative พร้อมกัน
  โดย Zebra = -0.50/s, Elephantulus = -0.25/s, Raptor/Deinonychus = -1.00/s

Life gauge ส่ง node ปัจจุบันและเวลาที่จะถึงศูนย์ให้ client จึงแสดงการลดต่อเนื่อง
โดยไม่ส่ง packet ทุก frame; registry synchronize ค่าเดียวกันก่อน hit ถัดไป และส่ง
`EntityDied` เมื่อ degeneration ถึงศูนย์

## 7. R9E — groggy, blow และ knockdown

R9E เพิ่มสำเนา read-only ของ `player_battle_actions.json` และจับคู่ hit ด้วย
`action id + hit index` เดียวกับ `AttackSnapshot`:

- 56 player actions มี `attack_info`; รวม 74 hit
- 74/74 hit มี `groggy > 0` และ `blow_power > 0`
- 0/74 hit มี `knock_back_force > 0` จึงไม่สร้าง KnockBack เองในชุดข้อมูลนี้
- `hit_force` และ `strong_attack` ถูกเก็บใน snapshot เพื่อ audit แต่ยังไม่เดา
  ความสัมพันธ์ server ที่ไม่มีใน client export

ค่า animal อ่านตรงจาก `animal.json`: `groggy_max`, `groggy_section`,
`groggy_duration`, `knock_down_duration`, `blow_resistance`,
`knock_back_resistance` และ `groggy_damage_ratio_table` ตัวประเมินสูตรรองรับ `**`
เพื่อคำนวณ groggy max แบบกำลังสองได้ตรงไฟล์เดิม

ลำดับ reaction:

1. Miss และ Dodge ไม่ลด groggy
2. landed hit ลด groggy gauge จากค่า hit และ directional ratio
3. ผ่าน section `max/9` ครั้งแรกเข้า Groggy; gauge ถึง 0 เข้า KnockDown
4. KnockDown เล่น `begin -> during -> end` ตาม `combat_3states` และ duration จริง
5. Blow/KnockBack เกิดเมื่อ power/force ผ่าน resistance จริงเท่านั้น
6. ถ้าถูก hit ระหว่าง Attack จะไม่ตัดท่า แต่คิว reaction ที่ priority สูงสุดไว้หลังท่าจบ
7. runtime ส่ง `SurvivalUpdated["groggy"]` และ `CombatInteraction.details["status"]`
   ผ่าน protocol เดิม; Blow/Groggy/KnockDown ใช้ enum เดิม และ Battle ใช้ล้างไอคอน

`groggy_velocity` ของสัตว์ทั้ง 4 ตัวเป็น 0 จึงไม่มี passive regeneration/decay
ระหว่างต่อสู้ และ gauge จะ reset หลังจบ Groggy/KnockDown เท่านั้น

## 8. Field-test gate

1. Miss และ Dodge ต้องยังแสดงเป็นคนละผล
2. Dodge ระหว่างสัตว์โจมตีต้องไม่ตัด active attack animation
3. Hit หนึ่งครั้งลด HP เพียงครั้งเดียว
4. log ต้องไม่มี `Player damage used a fallback animal formula` สำหรับสัตว์
   2001/2027/2037/2039
5. วงฐานแดง, telegraph, mesh และ logical base ต้องไม่แยกตำแหน่งหลัง root motion
6. log ของ landed hit ต้องแสดง `part` และ `direction`; Elephantulus ต้องเป็น
   Body เสมอ
7. log `Animal body part broke` ต้องเกิดครั้งเดียวต่อ part/object generation
8. เมื่อ part แตก client ต้องแสดง effect/icon ตามข้อมูลจริง เช่น Zebra Head =
   `head_injury` + `head_injury_tricera`; hit ต่อมาที่ part เดิมต้องไม่เพิ่มซ้ำ
9. การแตก part ต้องไม่ทำให้ Life รวมถูกหักสองครั้ง
10. Head break ของ Zebra/Raptor/Deinonychus ต้องทำให้ damage ครั้งถัดไปของสัตว์
    เหลือ 80% เมื่อเทียบภายใต้ defense/attack เดียวกัน
11. Leg break ต้องลด Dodge ของสัตว์ 40 โดย Miss และ Dodge ยังแยกผลเหมือนเดิม
12. Body break ต้องทำให้ Life gauge ลดต่อเนื่องตาม species และเมื่อถึงศูนย์ต้อง
    ตาย/ยกเลิก pending attack/ล้าง injury status เพียงครั้งเดียว
13. hit ระหว่าง animal Attack ต้องไม่ตัด animation; reaction ต้องเริ่มทันทีหลัง
    action boundary และเหลือเพียง reaction priority สูงสุด
14. groggy gauge ต้องลดตาม hit และเล่น Groggy เมื่อผ่าน section `max/9`
15. gauge ถึง 0 ต้องเล่น KnockDown ครบ begin/during/end ตาม duration ของ species
16. Blow/Groggy/KnockDown icon ต้องปรากฏและถูกล้างเมื่อ reaction จบ
17. log ต้องไม่มี fallback ของ impact/blow resistance/knockback resistance สำหรับ
    action ที่โจมตีโดนสัตว์ 2001/2027/2037/2039

ผลทดสอบปัจจุบัน: ข้อ 8 ฝั่ง icon/effect ผ่านแล้ว ส่วน R9D ต้องยืนยันเพิ่มว่า Head
ลด outgoing damage, Leg ลด Dodge และ Body ทำให้ Life gauge ลดด้วย velocity จริง

## 9. Target-selection ring hotfix

วงฐานแดงมาจาก `Particle/FX_Targeting_Common_01.prefab` ที่ `CombatGroup`
ผูกกับ root transform ของเป้าหมาย ไม่ใช่ renderer ในโมเดล animal ระบบ particle
ใน prefab เดิมเป็น world-space (`moveWithTransform=0`) จึงทิ้ง particle รุ่นก่อน
ไว้ที่ตำแหน่งเก่าระหว่าง root motion และเห็นชัดกับ Zebra

runtime แก้เฉพาะ effect นี้ให้ใช้ logical entity root เป็น custom particle
simulation space พร้อม LateUpdate anchor และล้าง pooled particles รุ่นเก่าเมื่อ
เริ่มติดตามเป้า Attack telegraph, damage area และ particle อื่นไม่ถูกเปลี่ยน
