# R1 — Saurus Action และ Root Transform Audit

วันที่สร้างข้อมูล: 2026-08-27  
ฐานเปรียบเทียบ: `DurangoCombatSystemPlugin 0.3.9`

## สถานะ

- R0 ผ่าน: เก็บ source, DLL, config และ hash ของ `0.3.9` แล้ว
- R1 data extraction ผ่าน: inventory attack และ root transform สร้างซ้ำได้
- R1 ยังไม่เปลี่ยน gameplay: `raptor (2001)` ยังไม่ถูกเพิ่มใน runtime eligibility
- intent/alignment/root-yaw policy ใน audit เป็น candidate สำหรับ R2/R3 ไม่ใช่
  หลักฐานว่า AI เกมเดิมเลือกท่านั้นด้วย trigger แบบเดียวกัน

จุดย้อนกลับอยู่ที่:

`tools\durango-mod-original\backups\DurangoCombatSystemPlugin-0.3.9-redesign-baseline-20260827`

## Entity ที่อยู่ในแผนใหม่

| ID | Entity | Framework | AI factor | Model | Cooldown | Bound | Runtime ตอน R1 |
|---:|---|---|---|---|---:|---:|---|
| 2027 | `zebraceratops` | `Tricera` | `protoceratops_ai` | `Tricera/ZebraceratopsPrefab` | 1.3 | 200 | baseline 0.3.9 |
| 2037 | `elephantulus` | `Phenacodus` | `phenacodus_ai` | `Phenacodus/Phenacodus_savanaPrefab` | 1.6 | 60 | baseline 0.3.9 |
| 2039 | `deinonychus_savana` | `Raptor` | `deinonychus_savana_ai` | `Raptor/Deinonychus_savanaPrefab` | 1.7 | 50 | baseline 0.3.9 |
| 2001 | `raptor` | `Raptor` | `raptor_ai` | `Raptor/RaptorPrefab` | 1.7 | 50 | ปิดไว้ระหว่าง R1 |

`2001` กับ `2039` ใช้ Framework และชุด animation เดียวกัน แต่ต้องมี species
intent profile คนละชุด เพราะ entity, model, root-motion id และ AI factor ต่างกัน

## Framework attack inventory

### Tricera — Zebraceratops

| Action | Motion | Hit | Geometry จากเกม | Intent candidate |
|---|---|---:|---|---|
| `tricera_once` | `Tricera_Attack_Once` | 12 | circle r220 offset (50,200) | StandardFront |
| `tricera_head` | `Tricera_Attack_Head` | 12, 23 | circle r220 สองตำแหน่ง | StandardFront |
| `tricera_turn` | `Tricera_Attack_Turn` | 32, 55 | front sector แล้ว rectangle | TurnAttack |
| `tricera_counter` | `Tricera_Attack_Counter` | 30 | selected-target/melee, bound | CounterAttack |
| `tricera_dash` | `Tricera_Attack_Dash` | 29, 33, 41 | rectangle, rectangle, circle | GapCloser |
| `tricera_dash_f` | `Tricera_Attack_Dash` | 29, 33, 41 | dash variant | GapCloser |
| `tricera_active_dash_s/c/b/a` | `Tricera_Active_Attack_Dash` | 29, 33, 41 | active dash variants | GapCloser; eligibility unresolved |

### Phenacodus — Elephantulus

| Action | Motion | Hit | Geometry จากเกม | Intent candidate |
|---|---|---:|---|---|
| `phenaco_bite` | `Phenaco_Attack_Bite` | 14 | rectangle 120×80, bound | StandardFront |
| `phenaco_jump` | `Phenaco_Attack_Jump` | 24 | rectangle 200×80 | GapCloser |
| `phenaco_gas` / `phenaco_gas_f` | `Phenaco_Attack_Gas` | 42 | circle sector 140..220° | AreaControl ด้านหลัง |
| `phenaco_active_gas_s/c/b/a` | `Phenaco_Active_Attack_Gas` | 42 | circle sector 140..220° | AreaControl; eligibility unresolved |
| `phenaco_attack_escape` | `Phenaco_Attack_Escape` | 14, 18, 22, 26 | rectangle ด้านหลังสี่ hit | EscapeStrike |

### Raptor framework — Raptor 2001 และ Deinonychus 2039

| Action | Motion | Hit | Geometry จากเกม | Intent candidate |
|---|---|---:|---|---|
| `raptor_attack` | `Raptor_Attack` | 13 | rectangle 80×60, bound | StandardFront |
| `raptor_jump` | `Raptor_Attack_Jump` | 19 | circle r70, bound | GapCloser |
| `raptor_dash` | `Raptor_Attack_Dash` | 23 | circle r125 sector 130..250°, bound | EscapeStrike/GapCloser ต้องแยกตาม context |
| `raptor_counter` | `Raptor_Attack_Counter` | 30 | circle r65, bound | CounterAttack |
| `dilopho_tail` | `Raptor_Attack_Tail` | 20, 41 | rear sectors สอง hit, bound | TurnAttack/AreaControl; compatibility ยังไม่ยืนยัน |

ชื่อ `dilopho_tail` เป็นเพียง key ที่อยู่ใน Framework ร่วม ไม่ใช่หลักฐานว่า
Raptor/Deinonychus ทุก model ควรใช้ท่านี้

## Root transform summary

หน่วย position เป็นหน่วยดิบของ AnimationClip และ yaw คือมุมบนระนาบ Unity XZ
ที่ unwrap จาก quaternion ของ `Bip001`

| Motion | Duration | ΔX | ΔZ | ΔYaw |
|---|---:|---:|---:|---:|
| `Tricera_Attack_Once` | 1.50 | 0.00 | 216.04 | 0.00° |
| `Tricera_Attack_Head` | 2.07 | 0.00 | 165.68 | 0.00° |
| `Tricera_Attack_Dash` | 1.77 | 0.00 | 1052.93 | 0.00° |
| `Tricera_Active_Attack_Dash` | 1.77 | 0.00 | 1052.93 | 0.00° |
| `Tricera_Attack_Counter` | 1.73 | 0.00 | 227.37 | 0.00° |
| `Tricera_Attack_Turn` | 2.80 | -194.23 | -500.00 | -183.30° |
| `Phenaco_Attack_Bite` | 0.97 | 0.00 | 79.95 | 0.00° |
| `Phenaco_Attack_Jump` | 1.93 | -71.52 | 508.29 | -193.79° |
| `Phenaco_Attack_Gas` | 5.03 | -13.17 | 179.73 | -360.00° |
| `Phenaco_Active_Attack_Gas` | 5.03 | -13.17 | 179.73 | -360.00° |
| `Phenaco_Attack_Escape` | 2.00 | 0.00 | -318.16 | 0.00° |
| `Raptor_Attack` | 1.33 | 0.00 | 0.00 | 0.00° |
| `Raptor_Attack_Jump` | 1.43 | 0.00 | 243.54 | 0.00° |
| `Raptor_Attack_Dash` | 1.20 | -73.68 | 341.06 | -190.88° |
| `Raptor_Attack_Counter` | 1.40 | 0.37 | 31.87 | -0.52° |
| `Raptor_Attack_Tail` | 2.43 | 0.00 | 112.64 | -180.00° |

ค่าที่หมุนประมาณ 180°/360° ยืนยันว่าห้ามใช้กฎ face-target/fixed-yaw เดียวกับทุก
action แต่ยังไม่ควรนำ ΔYaw สุดท้ายไปขยับ runtime ตรง ๆ จนกว่า R2 context และ R3
shadow resolver จะบอกได้ว่าท่าเริ่มจาก sector ใดและ animation handoff จบอย่างไร

## ไฟล์ผลลัพธ์และการสร้างซ้ำ

- `AuditData/saurus_action_audit.json`
- `AuditData/saurus_root_transform_audit.json`
- `Generate-SaurusActionAudit.ps1`
- `Generate-SaurusRootTransformAudit.ps1`

ไฟล์ audit ไม่ถูก deploy และ runtime `0.3.9` ไม่อ่านไฟล์เหล่านี้

## Gate ก่อน R2

- [x] inventory attack ครบ 24 key จาก Framework 3 ชุด
- [x] root position + quaternion/yaw ครบ 16 motion ที่ไม่ซ้ำ
- [x] แยก `raptor 2001` ออกจาก `deinonychus_savana 2039` ที่ระดับ species policy
- [x] ระบุ Original/Reconstructed และ runtime safety gate
- [x] ไม่แก้ player combat, selector หรือ runtime eligibility
