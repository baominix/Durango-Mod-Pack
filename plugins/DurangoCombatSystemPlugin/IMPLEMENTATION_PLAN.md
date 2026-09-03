# แผนพัฒนา DurangoCombatSystemPlugin ใหม่

> **Roadmap revision 2026-08-26:** Phase 0–5B และ player combat รุ่น `0.3.9`
> ยังคงเป็นฐานเดิม แต่ Phase 6 เป็นต้นไปถือเป็นประวัติการทดลองและถูกแทนด้วย
> [SAURUS_COMBAT_REDESIGN_PLAN.md](SAURUS_COMBAT_REDESIGN_PLAN.md)
> หยุดเริ่ม Phase 7 ตามลำดับเก่าจนกว่า redesign R1–R8 จะผ่านเกณฑ์ของแต่ละช่วง

## ขอบเขตรุ่นแรก

- Single-player/offline เท่านั้น
- ผู้เล่นต่อสู้กับสัตว์ local
- สัตว์ 4 ชนิดใน roadmap ใหม่: 2027, 2037, 2039, 2001
- runtime `0.3.9` ยังควบคุมเพียง 2027, 2037, 2039; `raptor 2001` เริ่มจาก audit/shadow ก่อน
- ไม่มี boss
- ยังไม่รองรับ co-op
- ใช้ message/type/animation/UI เดิมของเกมเท่าที่ทำได้

## Phase 0 — แยกของเก่าออกจากการทดสอบ

- [x] เปลี่ยน source เก่าเป็น `DurangoCombatSystemPlugin.disable` โดยผู้ใช้
- [x] ย้ายหรือเปลี่ยนนามสกุล DLL เก่าใน `BepInEx\plugins` ก่อนติดตั้ง build ใหม่
- [ ] เก็บสำเนา LogOutput และ config สำหรับ regression
- [x] ยืนยันว่าไม่มี active DLL/Harmony owner/GUID เก่าถูกโหลด

เกณฑ์ผ่าน: BepInEx ไม่รายงานการโหลด combat DLL เก่า และไม่มี patch ของเก่าอยู่ใน Harmony patch list

## Phase 1 — Project shell และ diagnostics

- [x] สร้าง project shell ตาม build pipeline .NET 3.5/BepInEx ของ mod ปัจจุบัน
- [x] Plugin GUID: `com.baominix.durango.original.combatsystem`
- [x] bootstrap ที่ไม่ patch จนกว่าข้อมูลจำเป็นโหลดสำเร็จ
- [x] data validator แสดง missing field/asset แบบครั้งเดียว
- [x] ต่อกับ central logging gate; ค่าเริ่มต้น log ปิด
- [ ] มี command/report dump เฉพาะข้อมูลที่อ่านได้ ไม่เปลี่ยน state เกม

เกณฑ์ผ่าน: โหลด plugin ได้โดยไม่เปลี่ยน gameplay และปิด log ภายในได้ทั้งหมด

## Phase 2 — Data adapters

- [x] adapter อ่าน snapshot `animal.json` ต้นฉบับสำหรับสัตว์ชุดแรก
- [x] adapter อ่าน snapshot `AnimalFrameworkResource` จาก Unity YAML
- [x] แปลง `AttackInfo` เป็น immutable `AttackDefinition`
- [ ] แยก `Original`, `Observed`, `Reconstructed` provenance
- [x] validate framework, animation key, motion และ attack หลักของ 3 species
- [x] required data ที่หายทำให้ validation fail ไม่ fallback เงียบ ๆ

ผล build/validation รอบแรก:

- profiles: 3
- frameworks: 3
- framework attacks: Tricera 10, Phenacodus 9, Raptor 5
- hit definitions: Tricera 24, Phenacodus 12, Raptor 6
- errors: 0
- warnings: 0

เกณฑ์ผ่าน: dump profile ของสัตว์ 3 ตัวตรงกับ `ANIMAL_COMBAT_DATA.md` และรายงาน field ที่หายชัดเจน

## Phase 3 — Offline protocol bridge

- [x] ดัก `GetActions`/`UseBattleAction` เฉพาะ local player บน offline connection
- [x] สร้าง `Actions` จาก tag อาวุธ + default action + learned skill reward ของเกม
- [x] refresh `Actions` เมื่อ equipment/skill state อัปเดต
- [x] ตรวจ message-handler ownership ก่อนลงทะเบียน ไม่ replace handler ของปลั๊กอินอื่น
- [x] ส่ง/เรียก flow เดิมของ `BattleBegun`, `Actions`, `AttackAlerted`, `Damaged`, `SurvivalUpdated`, `BattleEnded` สำหรับ player action และ animal action
- [x] มี action instance id และ packet sequence window ป้องกันการประมวลผลซ้ำ
- [x] validate `StartAt` ด้วย Unix/server-compatible time และเก็บ cooldown snapshot ฝั่ง authoritative runtime
- [x] world/player generation guard ป้องกัน callback ข้ามแผนที่

สถานะย่อยรุ่น `0.3.0`: action availability/validation, stamina, scheduler และ message flow สำหรับ player hit ทำงานแล้ว ส่วน animal action จะต่อใน Saurus AI

เกณฑ์ผ่าน: action ผู้เล่นหนึ่งครั้งลด stamina/start cooldown/สร้าง damage เพียงครั้งเดียว และ UI เดิมรับ message ได้

## Phase 4 — Geometry และ player hit

- [x] รองรับ Melee/Ranged แบบ selected-target และ CircularArea/RectangularArea แบบหลายเป้าหมาย
- [x] คำนวณ offset/yaw แบบเดียวกับ `PlayerActionAttackInfo.MakeAlerted()`
- [x] ใช้ snapshot เดียวกันสำหรับ telegraph กับ hit query
- [x] แยก out-of-range, Miss, Dodge, Hit
- [x] รองรับ multi-hit จาก AttackInfo หลายรายการ
- [x] แยก original developer visualizer ไป `DeveloperModePlugin` และเปิดผ่าน command
- [x] resync HP/SP ตอน action เริ่มและไม่ clamp ค่า `/sp` ที่เกิน Max ใน action แรก
- [x] บวก player root-motion delta ณ `attack_time` เข้ากับ authoritative snapshot
- [x] clamp root motion กับเป้าหมายและ refresh telegraph/hit geometry จากฐานจริงเมื่อ collision ขวาง

สถานะรุ่น `0.3.9`: implementation และ field test ผ่านโดยผู้ใช้เมื่อ 2026-08-25
รวม selected-target, area/multi-hit geometry, telegraph, HP/SP resync,
prohibited-time transition และ rectangular-axis hotfix ของ Sunder

เกณฑ์ผ่าน: เส้นเตือนและพื้นที่ damage ตรงกัน แม้ผู้โจมตีขยับ/หมุนหลัง commit

## Phase 5 — Saurus AI core

- [x] สร้าง state machine กลางโดยไม่ผูกชื่อ animation ใน controller
- [x] Idle/Roam/Alert/Approach/Face/Attack/Recover/Retreat/ReturnHome/Dead
- [x] hysteresis ของระยะและเวลา ลดอาการเดิน-หยุดกระตุก
- [x] attack selection ขั้นต้นจาก geometry/range และ cooldown; species weight รอ Phase 6
- [x] immutable animal attack snapshot ต่อ hit และ exact frame timing จาก runtime clip
- [x] animal telegraph/hit query ใช้ geometry snapshot ชุดเดียวกัน
- [x] ส่ง `AttackAlerted`/`Damaged`/`SurvivalUpdated` และลด HP ผู้เล่น
- [ ] interrupt priority สำหรับ damage/evade/blow/knockdown/dead
- [x] cleanup เมื่อ despawn/change world/title

สถานะ Phase 5B: core, movement ownership, wild-animal eligibility, lifecycle,
animal AttackSnapshot, original alert/message flow และ player HP damage เชื่อม runtime
แล้ว และ field test ผ่านโดยผู้ใช้เมื่อ 2026-08-25 ส่วน reaction, root motion และ
species profile จะต่อโดยไม่ย้าย simulation เข้า state core

เกณฑ์ผ่าน: dummy profile เดินเข้าโจมตี รอ cooldown ด้วย stand และกลับไล่โดย state ไม่ oscillate

## Phase 6 — Species profiles

### Elephantulus 2037

- [x] battle-on-hit ไม่หนีทันที
- [x] jump/bite/escape/gas geometry จาก Framework
- [x] attack root motion ขยับ actor base
- [x] damage displacement ใช้ curve เดิม; blow routing พร้อมและรอ Phase 7 เป็นผู้ตัดสิน effect

### Zebraceratops 2027

- [x] once/head/dash/counter ตามระยะ
- [x] once/head ใช้ root motion จริง
- [x] dash rectangle + final radius ตามหลาย hit frame
- [x] yaw lock ป้องกันเส้นกับตัวสัตว์คนละทิศ

### Deinonychus 2039

- [x] jump เคลื่อนเข้าหาเป้าหมาย
- [x] dash/jump/attack geometry จาก Framework
- [x] mesh forward/root-motion forward/logical yaw ตรงกัน
- [x] chase/recovery ต่อเนื่องด้วย hysteresis และ species range

ทุก species:

- [x] directional damage 4 ทิศ
- [x] Evade เมื่อ Dodge และ state อนุญาต
- [x] low-health retreat แบบโอกาสน้อยเมื่อ HP <= 20% นานประมาณ 6 วินาที

สถานะ Phase 6: implementation/build ทำงานเป็น baseline สำหรับ regression แต่พบ
design drift จากการใช้ range/weight เลือก action โดยไม่แยก Turn/Counter/GapCloser/
Escape intent จึงยังไม่ถือว่าปิด Phase และไม่ขยาย behavior ตามแผนเก่า

## Phase 7 — Damage และ reaction

> ย้ายไปเป็น R9 ในแผน redesign และจะเริ่มหลัง context/intent/root-transform/species
> vertical slice ผ่านก่อน

- [ ] ยืนยันสูตร accuracy/dodge/attack/defense จาก runtime เพิ่มเติม
- [x] calculator แบบแยก strategy ได้; expression ของสัตว์อ่านจากข้อมูลจริง แต่สูตร matchup ยังรอยืนยัน
- [ ] direction และ body-part probability จาก animal data
- [ ] normal/groggy directional ratio
- [ ] blow/knock-back resistance
- [ ] groggy/knockdown duration
- [ ] damage reaction ไม่ทับ uninterruptible attack โดยผิดจังหวะ

เกณฑ์ผ่าน: Miss/Dodge/Hit แยกถูก, HP ลดครั้งเดียว, reaction และ logical displacement ตรงกัน

## Phase 8 — Lifecycle และ persistence

- [x] generation + animal object-instance guard ป้องกัน scheduled hit เดิมข้ามแผนที่ (รอ field test)
- [ ] Return to Title เคลียร์ combat state โดยไม่แตะ inventory/world persistence ซ้ำ
- [ ] เข้า character ใหม่ไม่ reuse target/cooldown/entity เก่า
- [ ] process-wide service ไม่ถูกสร้างซ้ำทุก scene
- [ ] shutdown cleanup ไม่มีความจำเป็นต้องเป็นจุดบันทึกเดียว

เกณฑ์ผ่าน: ย้าย map/Return to Title/เข้าใหม่หลายรอบแล้วไม่มี HP, item, spawn หรือ combat state reset/duplicate จากปลั๊กอิน

## ลำดับทดสอบที่แนะนำ

1. เปิดเกมโดยปลั๊กอินโหลดแต่ patch ยังไม่ทำงาน
2. player action กับ dummy ที่ไม่ตอบโต้
3. player hit/miss/dodge และ multi-hit
4. Elephantulus อย่างเดียว
5. Zebraceratops อย่างเดียว
6. Deinonychus อย่างเดียว
7. สัตว์หลายตัวพร้อมกัน
8. เปลี่ยน map ระหว่าง idle/chase/attack/hit callback
9. Return to Title ระหว่าง combat แล้วกลับ character เดิม

ผู้ใช้เป็นผู้ทดสอบ gameplay; งานพัฒนาจะไม่เปิดเกมเองนอกจากได้รับคำสั่งเฉพาะ

## สิ่งที่ต้องเก็บจากแต่ละรอบทดสอบ

- entity id และ level
- action id/motion
- state ก่อน/หลัง
- origin/yaw ณ commit
- geometry และ hit time
- target position ณ commit/hit
- DamageResult, value, direction, part, effects
- logical position กับ visual/root position ก่อนและหลัง motion

ข้อมูลนี้ควรออกเฉพาะเมื่อเปิด log ของ DurangoCombatSystemPlugin เพื่อไม่ทำให้ `LogOutput.log` มีข้อความจำนวนมากในสภาวะปกติ

## Definition of Done รุ่นแรก

- สัตว์ทั้งสี่เริ่ม/ไล่/โจมตี/รอ cooldown/ตอบสนอง/ตายได้เมื่อ migration ครบ
- animation, actor base และ hit geometry อยู่ในทิศเดียวกัน
- player telegraph กับ hit geometry ปรับจากฐานจริงร่วมกันจนถึง hit; animal telegraph ใช้กฎ lock ของ action profile
- Miss กับ Dodge แยกกัน
- directional damage ครบ 4 ทิศ
- Evade ไม่ทับ attack ที่ล็อก แต่ผล Dodge ยังถูกต้อง
- low-health retreat ไม่กลายเป็น flee-on-hit
- ไม่มี callback/state ข้าม world session
- Return to Title ไม่ทำ inventory หรือ world resource สูญหาย/ย้อนกลับเพราะ combat plugin
- ไม่มี Harmony patch เก่าทำงานร่วมกับของใหม่
