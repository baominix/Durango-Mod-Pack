# Saurus Combat Redesign — Context → Intent → Action

วันที่ทบทวนแผน: 2026-08-27

เอกสารนี้แทน roadmap เดิมตั้งแต่ Phase 6 เป็นต้นไป ส่วน player combat,
offline protocol bridge, geometry และ lifecycle guard ที่ผ่าน field test ในรุ่น
`0.3.9` ยังคงเดิมและไม่อยู่ในขอบเขตการรื้อรอบนี้

## 1. เหตุผลที่ต้องปรับแผน

Phase 5/6 สร้าง state machine, hit snapshot, geometry และ root-position runtime
ได้แล้ว แต่เลือก action ด้วยระยะและน้ำหนักโดยตรง ทำให้ความหมายของ action หายไป:

- `counter` ถูกสุ่มเป็น normal attack
- `escape` เคยถูกสุ่มขณะหันเข้าหา player ทั้งที่พื้นที่อยู่ด้านหลัง
- `turn` มีอยู่ใน Framework แต่ไม่สามารถเลือกตามมุมได้
- controller หันเข้าหา player ก่อนเรียก selector ทำให้ข้อมูล Front/Flank/Rear
  ถูกทำลายก่อนตัดสินใจ
- root-motion data ปัจจุบันเก็บ position แต่ยังไม่มี rotation/yaw contract ต่อ action
- action ที่มี root turn, rear hit หรือ collision behavior เฉพาะถูกบังคับใช้กฎเดียวกัน

สิ่งที่ผิดไม่ใช่ state machine กลาง แต่เป็น boundary ระหว่าง
**Combat Decision**, **Action Selection** และ **Motion Execution**

## 2. ขอบเขตที่คงไว้

- Single-player/offline เท่านั้น
- สัตว์ทดลอง `2027`, `2037`, `2039`, `2001`
- ไม่มี boss และยังไม่ทำ co-op
- ใช้ Framework, AnimationClip, `animal.json` และ message เดิมของเกม
- player action runtime รุ่น `0.3.9` เป็น frozen subsystem
- `DeveloperModePlugin` เป็นเจ้าของ command/overlay สำหรับทดสอบ
- `DurangoCombatSystemPlugin` ไม่เพิ่ม command และ log ยังปิดเป็นค่าเริ่มต้น
- ผู้ใช้เป็นผู้เปิดเกมและ field test

## 3. Source of truth และ provenance

ทุก field ใน profile ใหม่ต้องระบุที่มา:

1. `Original` — อ่านจาก asset/code/data เกมเดิม
2. `Observed` — ยืนยันจาก gameplay footage หรือการทดสอบในเกม
3. `Reconstructed` — อนุมานจาก Original + Observed
4. `Experimental` — ค่าชั่วคราวเพื่อเก็บผลทดสอบ

ห้าม fallback เงียบจากข้อมูลที่หาย และห้ามใช้ชื่อ action เป็นหลักฐานเพียงอย่างเดียว
ตัวอย่าง `counter` บอก semantic intent ได้ แต่ trigger/counter window ยังต้องจัดเป็น
`Reconstructed` จนกว่าจะพบ AI factor เดิม

## 4. Pipeline ใหม่

```text
Perception + Combat Events
          ↓
SaurusCombatContext (immutable snapshot)
          ↓
SaurusAttackIntentResolver
          ↓
Standard / Turn / Counter / GapCloser / EscapeStrike / AreaControl
          ↓
SaurusActionEligibility
          ↓
SaurusAttackSelector (weight เฉพาะ action ใน intent เดียวกัน)
          ↓
SaurusActionPlan (immutable commit)
          ↓
Alignment policy → Motion → Telegraph → Hit → Recovery
```

หลักสำคัญคือ **เลือก intent ก่อนเลือกชื่อ animation** และใช้ weight เฉพาะเมื่อ
มี action หลายรายการที่ตอบ intent เดียวกัน

## 5. SaurusCombatContext

context ถูกสร้างก่อนหันตัวและต้องไม่มี side effect:

- actor position/yaw/radius/HP/state
- target position/yaw/radius/velocity
- ระยะ center-to-center และระยะหลังหัก bounds
- signed relative angle
- sector: `Front`, `LeftFlank`, `RightFlank`, `Rear`
- line of sight และผลตรวจ collision/path
- action/cooldown/animation lock ปัจจุบัน
- event ล่าสุดพร้อม timestamp
- engagement id และ world generation
- deterministic random roll สำหรับ decision ครั้งนั้น

## 6. SaurusCombatMemory และ event

เก็บเฉพาะ event ระยะสั้นที่ใช้ตัดสินใจ:

- `Engaged`
- `DamagedByPlayer`
- `AnimalDodgedPlayerAttack`
- `PlayerAttackMissed`
- `BlownOrKnockedBack`
- `TargetEnteredFlank`
- `TargetEnteredRear`
- `PathBlocked`
- `LowHealthThresholdCrossed`
- `LastActionCompleted`

event ต้องมีเวลา, actor/target id, action instance id และ generation guard
counter window หรือ escape window จึงไม่ทำงานข้าม action/ข้ามโลก

## 7. Attack intent

```text
StandardFront   โจมตีปกติเมื่อเป้าหมายอยู่ด้านหน้า
GapCloser       ปิดระยะด้วย jump/dash
TurnAttack      หมุนตัวโจมตีเป้าด้านข้างหรือด้านหลัง
CounterAttack   ตอบสนอง event ภายใน counter window
EscapeStrike    โจมตีพร้อมถอย/สร้างระยะ
AreaControl     คุมพื้นที่ตาม geometry เฉพาะ
```

`Evade`, `DamageReaction`, `Blow`, `KnockDown` และ `Dead` เป็น reaction intent
ไม่ปะปนกับ attack selector

## 8. ลำดับตัดสินใจ

1. terminal/interrupt: Dead → Blow/KnockDown → forced DamageReaction
2. reaction window: Evade หรือ Counter
3. survival: Retreat/EscapeStrike เมื่อเงื่อนไขถูกต้อง
4. positional: TurnAttack เมื่อ target อยู่ flank/rear
5. distance: GapCloser เมื่อเป้าหมายไกลและ path เหมาะสม
6. StandardFront หรือ AreaControl
7. หากไม่มี action eligible ให้ Approach/Reposition/Stand ไม่สุ่มข้าม intent

ลำดับนี้เป็น policy กลาง แต่ species profile กำหนดว่า intent ใดมีอยู่และ event ใด
เปิด window ได้

## 9. Alignment policy

การหันต้องเกิดหลังเลือก intent:

- `FaceTargetBeforeCommit` — normal/gap closer ส่วนใหญ่
- `KeepCurrentFacing` — turn/rear attack ที่ animation เป็นผู้หมุน
- `BindTargetAtCommit` — action ที่ `bound_enemy` และล็อก target
- `FollowRootYaw` — actor yaw เดินตาม rotation curve
- `CommitFinalRootYaw` — ใช้ yaw สุดท้ายเมื่อ motion จบ
- `FixedCommitYaw` — cancel baked root yaw และใช้ yaw ตอน commit ตลอดท่า

จึงต้องเพิ่ม state/boundary `Decide → Align → Execute` แทนการบังคับ
`Approach → Face → Select → Attack` แบบปัจจุบัน

## 10. Root transform contract

generator ใหม่ต้องอ่านทั้ง:

- `Bip001` position curve
- `Bip001` rotation/quaternion curve
- clip duration/frame rate

แต่ละ action สร้าง `RootTransformCurve` ที่ให้ `deltaPosition + deltaYaw` ตามเวลา
และใช้ transform เดียวกันกับ:

1. logical actor movement ผ่าน collision
2. visual root compensation
3. telegraph origin/yaw ของแต่ละ hit
4. damage query ของแต่ละ hit
5. final actor position/yaw หลัง cross-fade

ห้ามเปิด `tricera_turn` จนกว่า rotation curve และ handoff นี้ผ่าน field test

### Collision policy ต่อ action

- `Slide`
- `StopOnBlock`
- `AbortBeforeActiveFrame`
- `ContactHitThenStop`

path ที่ collision ยอมรับจริงเป็น authoritative trajectory; telegraph และ hit
ต้อง refresh จาก trajectory นี้ ไม่ใช้ root curve ที่ยังไปต่อทะลุสิ่งกีดขวาง

## 11. Action plan

เมื่อ commit ให้สร้าง immutable `SaurusActionPlan`:

- action definition + intent + provenance
- actor/target snapshot
- alignment/root-yaw/collision policy
- predicted root transform ต่อ hit
- hit geometry/timing ทั้งหมด
- cooldown และ interrupt windows
- action instance id + world generation

animation, renderer และ damage resolver เป็น consumer ของ plan เดียวกัน
ไม่คำนวณทิศ/ตำแหน่งแยกคนละรอบ

## 12. Species intent matrix ขั้นต้น

ตารางนี้เป็นจุดเริ่ม audit ไม่ใช่การยืนยัน trigger ของเกมเดิม

### Zebraceratops 2027

| Intent | Action | สถานะหลักฐาน |
|---|---|---|
| StandardFront | `tricera_head`, `tricera_once` | Original + Observed |
| TurnAttack | `tricera_turn` | Original; trigger Reconstructed |
| CounterAttack | `tricera_counter` | Original semantic; window Reconstructed |
| GapCloser | `tricera_dash*` | Original + Observed |
| Defensive reaction | `Tricera_Evade` | Original + Observed |

### Elephantulus 2037

| Intent | Action | สถานะหลักฐาน |
|---|---|---|
| StandardFront | `phenaco_bite` | Original + Observed |
| GapCloser | `phenaco_jump` | Original; range Reconstructed |
| EscapeStrike | `phenaco_attack_escape` | Original geometry/root; trigger Reconstructed |
| AreaControl | `phenaco_gas` | Original; direction/trigger ต้อง audit |
| Defensive reaction | evade/damage map | Original |

### Deinonychus 2039

| Intent | Action | สถานะหลักฐาน |
|---|---|---|
| StandardFront | `raptor_attack` | Original + Observed |
| GapCloser | `raptor_jump` | Original + Observed |
| GapCloser/Spacing | `raptor_dash` | Original rear arc; context ต้อง audit |
| CounterAttack | `raptor_counter` | Original semantic; window Reconstructed |
| Turn/Rear candidate | Framework tail action | Original; model compatibility ต้อง audit |
| Defensive reaction | `Raptor_Evade` | Original + Observed |

### Raptor 2001

| Intent | Action | สถานะหลักฐาน |
|---|---|---|
| StandardFront | `raptor_attack` | Original; behavior ต้อง field test แยกจาก 2039 |
| GapCloser | `raptor_jump` | Original; range/trigger Reconstructed |
| EscapeStrike/GapCloser | `raptor_dash` | Original rear sector + root turn; intent ต้อง shadow test |
| CounterAttack | `raptor_counter` | Original semantic; window Reconstructed |
| Turn/Rear candidate | `dilopho_tail` | Original Framework; model compatibility ยังไม่ยืนยัน |
| Defensive reaction | `Raptor_Evade` | Original; behavior ต้อง field test |

`2001` และ `2039` ใช้ Framework `Raptor` ร่วมกัน แต่ห้ามแชร์ species intent
profile โดยอัตโนมัติ เพราะใช้ model, root-motion id และ AI factor คนละรายการ

## 13. Interrupt policy

แต่ละ action แบ่งอย่างน้อยสามช่วง:

- `Windup` — โดยทั่วไป interruptible
- `Active` — โดยทั่วไปไม่ให้ normal reaction ตัด
- `Recovery` — เปิด reaction/counter ตาม policy

priority กลาง:

```text
Dead > Blow/KnockDown > ForcedReaction > Evade > CounterWindow
     > ActiveAttack > Movement/Stand
```

ข้อยกเว้นต้องอยู่ใน action/species profile พร้อม provenance ไม่เขียนเงื่อนไขชื่อท่า
กระจายใน controller

## 14. แผนดำเนินงานใหม่

### R0 — Freeze และ regression baseline

- [x] เก็บ source/DLL/hash/config ของ `0.3.9`
- ยืนยันว่า player combat, protocol, DeveloperMode และ lifecycle guard ไม่ถูกแก้
- [x] บันทึก known issues ของ Saurus selector/root yaw

Baseline: `backups/DurangoCombatSystemPlugin-0.3.9-redesign-baseline-20260827`

เกณฑ์ผ่าน: build เดิมย้อนกลับได้และไม่มีระบบเก่าซ้อน

### R1 — Complete action audit

- [x] ทำ inventory ทุก attack ของ Framework 3 ชุดสำหรับสัตว์ 4 species
- [x] ระบุ intent candidate, geometry, hit frames, bound/rot speed
- [x] สร้าง position + quaternion/yaw root data แบบทำซ้ำได้
- [x] ระบุ Original/Observed/Reconstructed/Experimental ทุก field

ผล audit: [R1_ACTION_AUDIT.md](R1_ACTION_AUDIT.md) โดย `2001` ยังปิด runtime
eligibility และไฟล์ใน `AuditData` ไม่ถูก deploy

เกณฑ์ผ่าน: ไม่มี action ที่ถูกเปิดใช้โดยยังไม่รู้ orientation และ root policy

### R2 — Context และ memory โดยไม่เปลี่ยน gameplay

- [x] เพิ่ม `SaurusCombatContext`, sector และ event memory
- [x] ให้ DeveloperModePlugin dump context ผ่าน API แยก
- [x] runtime เดิมยังใช้ range selector; context ใหม่ทำงานแบบ read-only

Implementation/build และ field-test ผ่านแล้ว:
[R2_COMBAT_CONTEXT.md](R2_COMBAT_CONTEXT.md)

เกณฑ์ผ่าน: Front/Flank/Rear และ event window ตรงกับการทดสอบหลายมุม

### R3 — Intent resolver แบบ shadow

- [x] resolver คำนวณ intent/action candidate แต่ยังไม่ execute
- [x] เปรียบเทียบผลกับ selector เดิมผ่าน DeveloperMode diagnostic แบบเรียกเมื่อต้องการ
- [x] ห้ามเพิ่ม behavior mode ใน config ผู้เล่น

Implementation/build: [R3_SHADOW_INTENT_RESOLVER.md](R3_SHADOW_INTENT_RESOLVER.md)
ผ่านแล้วทั้ง implementation/build และ field-test rationale/legacy comparison;
ก่อนเริ่ม R4 พบและแก้ player normal-attack auto-approach regression โดยคงฐาน
`0.3.9` รายละเอียดอยู่ใน `PLAYER_NORMAL_ATTACK_RANGE_AUDIT.md`

เกณฑ์ผ่าน: decision อธิบายได้ว่าเลือก/ปฏิเสธ action เพราะอะไร

### R4 — Root transform และ action-plan execution

- [x] เพิ่ม rotation curve และ alignment policy
- [x] เปลี่ยน telegraph/hit/motion ให้อ่าน `SaurusActionPlan`
- [x] รักษาชุด action ปัจจุบันก่อน ยังไม่เปิด Turn/Counter semantics ใหม่

Implementation/build และ field test ผ่านแล้วเมื่อ 2026-08-27:
[R4_ACTION_PLAN_EXECUTION.md](R4_ACTION_PLAN_EXECUTION.md)

เกณฑ์ผ่าน: position/yaw/mesh/telegraph/hit ตรงกันและไม่มี one-frame flash

ผลจริง: สัตว์ทดสอบทั้งสามไม่มี flash/วาร์ป และ path/animation/telegraph ตรงกัน
จากนั้นเพิ่ม follow-up ของ Phenacodus โดยยังอยู่บน action-plan contract เดิม:
อ่าน `battle_idle` ที่ตกหล่น และจัดระยะก่อน commit `phenaco_gas`

### R5 — Zebraceratops vertical slice

- [x] StandardFront: head/once
- [x] TurnAttack: turn เมื่อ flank/rear
- [ ] CounterAttack: action/root พร้อม แต่ trigger ยัง audit-block; ยกเลิกการใช้
  Player Miss/Zebra Dodge 1.25s เพราะหลักฐานไม่เพียงพอ
- [x] GapCloser: dash ตาม path/collision policy

Implementation/build เสร็จแล้วและรอ field test พร้อม Directional Evade:
[DIRECTIONAL_EVADE_AND_R5_ZEBRA.md](DIRECTIONAL_EVADE_AND_R5_ZEBRA.md)

Tuning หลัง field feedback: Rear ใช้ turn ตามเดิม; Flank ใช้ activation chance 35%
และ fallback เป็น continuous Reposition-to-Front เพื่อไม่ให้ turn เกิดทุกครั้งหรือ
reroll ทุก frame

เกณฑ์ผ่าน: forced context แต่ละแบบเรียก intent ถูก และไม่สุ่มข้ามประเภท

### R6 — Elephantulus profile

- [x] bite/jump ตาม front/range
- [x] audit gas direction: commit จาก Front แล้ว original root turn ทำให้ rear sector
  กลับไปครอบ target
- [x] gas จัดระยะก่อน commit เมื่อ target อยู่ประชิด
- [x] `phenaco_attack_escape` เป็น EscapeStrike ก่อนเข้าสู่ low-health Retreat
- [x] animation/root เดิมรับผิดชอบการหันหลัง ตะกุยสี่ hit และเคลื่อนหนี

Implementation/build และ field feedback ผ่านแล้ว:
[R6_ELEPHANTULUS_INTENT_EXECUTION.md](R6_ELEPHANTULUS_INTENT_EXECUTION.md)

เกณฑ์ผ่าน: ไม่หันหน้าแล้ววาด escape ด้านหลังโดยไร้เหตุผล

### R7 — Raptor 2001 profile

- [x] ใช้ action จาก Framework `Raptor` แต่แยก intent/weight/trigger จาก 2039
- [x] audit `raptor_dash` ที่ geometry อยู่ด้านหลังและ root yaw หมุนประมาณ 180 องศา
- [x] ยังไม่เปิด `dilopho_tail` จน model compatibility ผ่าน
- [x] chase/reposition ไม่สลับ state ระหว่าง cooldown

Implementation/build:
[R7_RAPTOR_2001_INTENT_EXECUTION.md](R7_RAPTOR_2001_INTENT_EXECUTION.md)
ผ่าน field test แล้วและใช้เป็น baseline ก่อนเริ่ม R8

เกณฑ์ผ่าน: Raptor ใช้ context ของตนเองและไม่รับ behavior ที่สังเกตจาก
Deinonychus โดยไม่มีหลักฐาน

### R8 — Deinonychus profile

- [x] attack/jump/dash/counter ตาม intent
- [x] audit rear arc ของ dash; `dilopho_tail` ยังปิดด้วย model-compatibility gate
- [x] chase/reposition ไม่สั่นระหว่าง cooldown

Implementation:
[R8_DEINONYCHUS_2039_INTENT_EXECUTION.md](R8_DEINONYCHUS_2039_INTENT_EXECUTION.md)
build/deploy เสร็จแล้ว รอ field test ก่อนเริ่ม R9

เกณฑ์ผ่าน: jump/dash เคลื่อนฐานและเลือก context ตรง footage

### R9 — Damage และ reaction

- [x] R9A: accuracy/defense/dodge strategy ใช้สูตรจริงจาก animal profile
- [x] R9B: โหลด body-part data และเลือก Damage.Part ตามทิศแบบ deterministic
- [x] R9C: part HP ต่อ object generation, one-shot part break และ status snapshot
  (field test ยืนยัน injury icon/effect แล้ว)
- [x] R9D: derived injury modifier สำหรับ damage, hit rate, dodge และ life velocity
- [x] R9E: groggy/blow/knockback resistance และ knockdown three-state จากข้อมูลจริง
- [x] atomic attack + priority reaction queue ที่ safe action boundary
- [x] groggy gauge และ Blow/Groggy/KnockDown status icon event ผ่าน protocol เดิม

R9E build ผ่านแล้วและรอ field test ก่อนปิด R9

Implementation: [R9_DAMAGE_REACTION_STRATEGY.md](R9_DAMAGE_REACTION_STRATEGY.md)

เกณฑ์ผ่าน: reaction ไม่ตัด active attack ผิดช่วงและ HP ลดครั้งเดียว

### R10 — Lifecycle, multi-animal และ cleanup

- หลายสัตว์พร้อมกัน
- despawn/change map/Return to Title
- action/event/context ไม่ข้าม generation
- profiling allocation/log spam

เกณฑ์ผ่าน: ไม่มี state/hit/target ค้างและไม่กระทบ inventory/world persistence

## 15. Developer test surface

คำสั่งอยู่ใน `DeveloperModePlugin` และเรียก diagnostic API ของ combat plugin:

- dump context ของสัตว์เป้าหมาย
- แสดง intent พร้อมเหตุผลที่ action eligible/rejected
- force action key เพื่อทดสอบ motion/geometry โดยข้าม selector
- force relative context สำหรับ Front/Flank/Rear/Blocked

command เหล่านี้เป็นเครื่องมือทดสอบ ไม่เป็นส่วน gameplay และต้องไม่ทำงานเมื่อ
DeveloperMode ปิด

## 16. Migration rules

- ไม่ลบ range selector จน shadow resolver ผ่าน R3
- ไม่แก้ player runtime ระหว่าง R1–R8
- ไม่เปิด `turn`, `counter`, `escape` เป็น semantic behavior พร้อมกันทุก species
- migrate ทีละ species เริ่ม Zebraceratops เพราะมี Standard/Turn/Counter/GapCloser ครบ
- ทุก phase build ได้และ rollback กลับ baseline ได้
- ไม่เพิ่ม Harmony patch หากทำผ่าน service/controller boundary เดิมได้

## 17. Definition of Done ใหม่

- AI เลือก intent จาก context ก่อนเลือก action
- Front/Flank/Rear ยังอยู่ครบก่อน alignment
- Turn/Counter/Escape ไม่ถูกสุ่มเป็น normal attack
- root position และ root yaw มีเจ้าของและ policy ชัดเจนต่อ action
- mesh, logical actor, telegraph และ damage geometry ใช้ action plan เดียวกัน
- collision เปลี่ยน trajectory แล้ว hit ตามตำแหน่งจริง
- reaction/interrupt มี priority และ window ชัดเจน
- provenance ของค่าที่ reconstructed ตรวจสอบย้อนกลับได้
- player combat `0.3.9` ไม่ regression
- lifecycle และ Return to Title ไม่ทิ้ง state หรือแตะ persistence ซ้ำ
