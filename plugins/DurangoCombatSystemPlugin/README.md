# DurangoCombatSystemPlugin

พื้นที่ออกแบบใหม่สำหรับระบบต่อสู้แบบ Single-player ของ Durango: Wild Lands รุ่นออฟไลน์

สถานะปัจจุบัน: **Saurus combat redesign R9E (รอ field test)** บนฐานรุ่น `0.3.9` โดย player combat
และ Phase 5B ยังคงเป็น frozen baseline สัตว์ทั้ง 4 species ใช้ Context → Intent →
Action แล้ว R9A เปลี่ยน Defense/Dodge/Evade ให้อ่านสูตรจริง และ R9B เลือก
Damage.Part จาก `animal.json`; R9C เพิ่ม part HP/status, R9D ใช้ derived
EffectDetail เดิม และ R9E เพิ่ม groggy/blow/knockdown พร้อม atomic reaction queue

Presentation hotfix 2026-08-30: วงเลือกเป้าหมาย
`FX_Targeting_Common_01` ใช้ logical entity root เป็น custom particle
simulation space และมี LateUpdate anchor ของตนเอง จึงไม่รับ presentation/root
offset ของ `tricera_head` สอง hit; attack telegraph และ hit geometry ไม่ถูกแก้

R2 ผ่านแล้ว และ R3 implementation เพิ่ม shadow intent resolver ที่อธิบาย
eligible/rejected candidate และเทียบกับ selector เดิม โดยยังไม่เปลี่ยน gameplay

ฐาน `0.3.9` มี hotfix ลดงานซ้ำของ multi-hit spear dash และให้ transition ระหว่าง
Normal/Skill/Defense ทุกทิศทางยึด `prohibited_time` ของเกม แทนการใช้
`action_length` เหมารวม

## เป้าหมาย

- ทำระบบต่อสู้ที่ยึดข้อมูลต้นฉบับของเกมเป็นหลัก
- roadmap ใหม่ครอบคลุมสัตว์ 4 ชนิด: `2027 zebraceratops`, `2037 elephantulus`, `2039 deinonychus_savana`, `2001 raptor`
- `2001 raptor` ใช้ Framework ร่วมกับ 2039 แต่แยก species profile และยังไม่เปิด runtime ระหว่าง R1
- แยก simulation, animation, root motion, พื้นที่โจมตี และ UI ออกจากกัน
- ทำให้การเปลี่ยนแผนที่/Return to Title ไม่สร้าง state ซ้ำหรือทำข้อมูลเสีย
- รองรับการขยายสัตว์ชนิดใหม่ด้วย profile โดยไม่ต้องเขียน AI ใหม่ทั้งชุด

## หลักการของการรื้อใหม่

1. ไม่คัดลอกโค้ดจาก `DurangoCombatSystemPlugin.disable` มาเป็นฐาน
2. ใช้สคริปต์และข้อมูลจาก AssetRipper export ต้นฉบับเป็น source of truth
3. ใช้ไฟล์เก่าเฉพาะตรวจ regression และดูว่าปัญหาเดิมเกิดตรงไหน
4. สถานะ combat มีเจ้าของเพียงชุดเดียว ฝั่งภาพรับผลผ่าน message/event ของเกม
5. พื้นที่โจมตีและเส้นเตือนของผู้เล่นต้องอ้าง geometry ชุดเดียวกัน และปรับจากฐานจริงเมื่อ collision ทำให้ root motion ไปไม่ครบ
6. ไม่ hardcode timing/range ที่มีอยู่แล้วใน `animal.json`, Framework หรือ `player_battle_actions.json`

## เอกสาร

- [RESEARCH_SOURCES.md](RESEARCH_SOURCES.md) — แหล่งข้อมูล ลำดับความน่าเชื่อถือ และสิ่งที่ยังขาด
- [COMBAT_ARCHITECTURE.md](COMBAT_ARCHITECTURE.md) — โครงสร้าง combat เดิมของเกมและสถาปัตยกรรมปลั๊กอินใหม่
- [ANIMAL_COMBAT_DATA.md](ANIMAL_COMBAT_DATA.md) — ค่าจริงและ animation/attack geometry ของสัตว์ 3 ตัวแรก
- [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) — ลำดับการพัฒนา เกณฑ์ผ่าน และรายการทดสอบ
- [PLUGIN_INTEGRATION_AUDIT.md](PLUGIN_INTEGRATION_AUDIT.md) — ขอบเขตการทำงานร่วมกับปลั๊กอินที่ใช้งานอยู่
- [PHASE4_PLAYER_GEOMETRY.md](PHASE4_PLAYER_GEOMETRY.md) — สูตร geometry, scheduler, message flow และส่วนที่ reconstruct ชั่วคราว
- [PLAYER_ACTION_TARGETING.md](PLAYER_ACTION_TARGETING.md) — audit action ทั้ง 56 รายการและการแยก selected-target/area
- [PHASE4_HANDOFF.md](PHASE4_HANDOFF.md) — สถานะและ field-test gate ก่อนเริ่ม Saurus AI
- [PHASE5_SAURUS_AI_CORE.md](PHASE5_SAURUS_AI_CORE.md) — state machine, ownership, provenance และผลที่ควรเห็นใน Phase 5A
- [PHASE5B_ANIMAL_ATTACK_EXECUTION.md](PHASE5B_ANIMAL_ATTACK_EXECUTION.md) — timing, geometry, hit query, damage และ lifecycle ของ animal action
- [NEUTRAL_DINOSAUR_STATUS_EFFECTS.md](NEUTRAL_DINOSAUR_STATUS_EFFECTS.md) — เทียบ Archive กับ body-part injury/status effect ในข้อมูลเกมจริงและช่องว่างของ runtime ปัจจุบัน
- [ANIMAL_STATUS_ICON_FLOW.md](ANIMAL_STATUS_ICON_FLOW.md) — flow `CombatInteraction` ไปยังไอคอน/emoji เหนือหัวสัตว์และ mapping sprite เดิม
- [PHASE6_SPECIES_PROFILES.md](PHASE6_SPECIES_PROFILES.md) — species weight, original root curve, reaction/retreat และ field-test gate
- [SAURUS_COMBAT_REDESIGN_PLAN.md](SAURUS_COMBAT_REDESIGN_PLAN.md) — roadmap ใหม่สำหรับ Combat Context, Attack Intent, root yaw และ species migration
- [R4_ACTION_PLAN_EXECUTION.md](R4_ACTION_PLAN_EXECUTION.md) — action plan กลางสำหรับ root position/yaw, telegraph และ hit
- [R7_RAPTOR_2001_INTENT_EXECUTION.md](R7_RAPTOR_2001_INTENT_EXECUTION.md) — species profile, dash/root-yaw audit และ spatial scale ของ Raptor 2001
- [R8_DEINONYCHUS_2039_INTENT_EXECUTION.md](R8_DEINONYCHUS_2039_INTENT_EXECUTION.md) — intent execution, counter window และ dash/root-yaw contract ของ Deinonychus 2039
- [R9_DAMAGE_REACTION_STRATEGY.md](R9_DAMAGE_REACTION_STRATEGY.md) — สูตร Defense/Dodge/Evade จริง, Miss/Dodge separation และ safety gate ของ reaction/status
- [PLAYER_NORMAL_ATTACK_RANGE_AUDIT.md](PLAYER_NORMAL_ATTACK_RANGE_AUDIT.md) — ผลตรวจแกนและการแก้ auto-approach normal attack
- [R1_ACTION_AUDIT.md](R1_ACTION_AUDIT.md) — inventory action/root transform ของสัตว์ 4 ชนิดและ safety gate ของ Raptor 2001
- [R2_COMBAT_CONTEXT.md](R2_COMBAT_CONTEXT.md) — snapshot ก่อนตัดสินใจ, sector, event memory และคำสั่งตรวจแบบแยก plugin
- [R3_SHADOW_INTENT_RESOLVER.md](R3_SHADOW_INTENT_RESOLVER.md) — intent priority, candidate rejection, deterministic roll และ legacy comparison
- [AuditData/README.md](AuditData/README.md) — ผล audit ที่สร้างซ้ำได้และไม่ถูก deploy
- [ReferenceData/README.md](ReferenceData/README.md) — development/build-time snapshot; final DLL embed เฉพาะข้อมูล combat ที่จำเป็น

## สิ่งที่ทำงานแล้ว

- โหลดและตรวจ `animal.json`/Framework ของสัตว์ 3 ตัวแรก
- รับ `GetActions` และส่ง `Actions` ผ่าน offline connection เดิม
- สร้างรายการ action จาก `tag_allow_actions.json`, อุปกรณ์ปัจจุบัน และสกิลที่เรียนแล้ว
- ใช้ bare-hand เป็น fallback ชั่วคราวหากข้อมูลอุปกรณ์ยังไม่มา
- refresh action อัตโนมัติเมื่อข้อมูล equipment/skill โหลดหรือเปลี่ยน
- หาก `EquipPreset` มาก่อน item ใน `InventorySystem` จะรอและ retry ทุก 0.25 วินาที โดยไม่เปลี่ยนเป็น `bare_hands` ก่อนเวลา
- รับและ validate `UseBattleAction` ด้วย availability, timestamp, action lock, cooldown และ packet deduplication
- ทุก session มี generation ใหม่ และยกเลิก reference/event เมื่อ connection ปิดหรือเปลี่ยนโลก
- ตรวจ handler owner ก่อนลงทะเบียน เพื่อไม่เขียนทับปลั๊กอินอื่น
- ลด stamina เพียงครั้งเดียวหลัง action ผ่าน validation
- Melee และ Ranged ใช้ `TargetEntityId` เป้าเดียว; พื้นที่หลายเป้าหมายมีเฉพาะ CircularArea และ RectangularArea
- ใช้ `AttackSnapshot` ชุดเดียวกันสำหรับเส้นเตือนกับ hit query และ refresh center จนถึง hit time
- คำนวณตำแหน่ง actor ของแต่ละ hit จาก root-motion path ชาย/หญิง, clamp กับ bound ของเป้าหมาย และแก้ตามฐานจริงหาก collision หยุด/ไถลผู้เล่น
- วาด player telegraph โดยตรงจาก authoritative runtime และย้ายเส้นเดิมด้วย visualizer id เดิมเมื่อ `CombatSystem.AttackAlertEnabled` เปิด; เมื่อ Developer mode ปิด toggle เส้นที่ค้างอยู่จะถูกหยุดทันทีและ Ranged ไม่วาดวง AoE
- รองรับ multi-hit/หลายเป้าหมาย พร้อม generation และ action-instance guard
- แยก out-of-range, Missed, Dodged และ Hit
- ส่ง `BattleBegun`, `AttackAlerted`, `Damaged`, `SurvivalUpdated`, `EntityDied` และ `BattleEnded`
- เปิด original `CombatGroup.Start()` initialization ใน main-scene Offline เพื่อคืนปุ่มเปิด combat, action icon และ callback โดยคืนค่า cluster mode ทันทีหลัง initialization
- ย้ายคำสั่งทดสอบ `/hp`, `/sp`, `/combatspawn`, `/combatwave`, `/combatstatus`, `/combathelp` ไป `DeveloperModePlugin`
- หลัง `BattleEnded` ส่ง HP/SP ล่าสุดจาก `PlayerContext` ซ้ำ เพื่อไม่ให้ HUD คืนไปใช้ snapshot ก่อนเรียก `/hp` หรือ `/sp`
- ตอนรับ action ส่ง HP/SP ชุดล่าสุดพร้อมกัน และหัก stamina จากค่าปัจจุบันโดยไม่บังคับค่าที่ `/sp` เพิ่มเกิน Max ให้กลับลงมาเป็น Max
- คัดเฉพาะ wild enemy ที่มี profileและอยู่ใน `AnimalManager`; ไม่จับ pet/ally/NPC
- state กลาง Idle/Roam/Alert/Approach/Face/Attack/Recover/Retreat/ReturnHome/Dead
- เดินและหันด้วย MoveSet ของ Framework จริง พร้อม range/time hysteresis
- baseline เลือก attack จาก geometry/range/weight และรอ `attack_cooltime` ด้วย battle stand; ส่วน intent-aware selector อยู่ในแผน redesign
- เวลา hit ของสัตว์ใช้ `attack_info.frame / AnimationClip.frameRate` จาก Framework ที่โหลดจริง; ไม่มี fallback 30 fps แบบเงียบ ๆ
- สร้าง immutable snapshot ต่อ hit โดยใช้กฎ offset/yaw เดียวกับ `PlayerActionAttackInfo.MakeAlerted()`
- ส่ง animal `AttackAlerted` ผ่าน message เดิมของเกม และใช้ snapshot ชุดเดียวกันตรวจพื้นที่เมื่อถึง hit
- รองรับ CircularArea, RectangularArea และ selected target แบบ Melee/Ranged พร้อม inner radius และ multi-hit
- แยก out-of-area, Missed, Dodged และ Hit; ลด/บันทึก HP ผู้เล่นผ่าน `PlayerContext` แล้วส่ง `SurvivalUpdated`
- ป้องกัน hit ค้างข้าม despawn/object instance/world generation/Return to Title
- ล้าง controller/path/reference เมื่อ despawn เปลี่ยน world หรือ Return to Title
- จำกัดชุด attack ของสัตว์แต่ละชนิดและสุ่มตามระยะ/weight ที่แยกจาก state core
- ใช้ curve `Bip001` ต้นฉบับกับ actor base และคำนวณตำแหน่ง hit ของ animal snapshot จาก curve เดียวกัน
- ใช้ damage animation 4 ทิศและ Evade จาก Framework; Evade ไม่ตัด attack ที่กำลังเล่น
- อ่าน groggy/blow metadata ของ player hit จาก `player_battle_actions.json` และ
  resistance/gauge/duration จาก `animal.json`; attack ที่กำลังเล่นไม่ถูกตัดและ
  reaction priority สูงสุดจะเริ่มหลัง action boundary
- Groggy ส่ง gauge, KnockDown เล่น begin/during/end จาก Framework และส่ง
  Blow/Groggy/KnockDown status ผ่าน `CombatInteraction` เดิมของเกม
- cooldown ใช้ `attack_cooltime` จริง พร้อม Recover stand 1 วินาทีที่นับซ้อนใน cooldown และมี retreat โอกาสต่ำเมื่อ HP <= 20% นาน 6 วินาที
- เก็บ read-only combat context ที่ 10 Hz ก่อน Face/Move/Select พร้อม Front/Flank/Rear, engagement/generation guard และ event memory 16 รายการ
- เปิด diagnostic API ให้ DeveloperMode ใช้ `/combatcontext` โดยไม่เพิ่ม command patch ใน Combat plugin
- ประเมิน shadow intent เฉพาะ selection boundary/diagnostic request และใช้ `/combatintent` ดูเหตุผล โดย selector เดิมยัง execute ท่าจริง

## ตำแหน่ง build/deploy

- source: `tools/Durango Mod Pack/plugins/DurangoCombatSystemPlugin`
- build script: `tools/Durango Mod Pack/build/Build-DurangoCombatSystemPlugin.ps1`
- build output: `tools/Durango Mod Pack/build-output/DurangoCombatSystemPlugin.dll`
- runtime ใช้ **DLL เดี่ยว** ไม่ต้องมี `ReferenceData` ข้าง DLL

ข้อมูลสัตว์ที่ runtime รองรับ (`2001/2027/2037/2039`), Framework ที่จำเป็น,
root-motion ที่ใช้งาน และ player battle-action impact metadata ถูก embed เข้า DLL
ตอน compile

## ขอบเขตที่ยังไม่ทำหลัง R9E implementation

- Phase 6 ไม่ถือว่าผ่านเชิงสถาปัตยกรรม: Turn/Counter/GapCloser/Escape ยังต้องย้ายไป context-aware intent resolver
- สูตร attack/accuracy/attack-rating อ่าน expression จริงจาก `animal.json` แล้ว แต่สมการจับคู่กับ Defense/Dodge/Evade ของผู้เล่นยังเป็น reconstructed strategy ที่แยกไว้สำหรับแทนที่ใน Phase 7
- critical และความสัมพันธ์ server ของ `hit_force`/`strong_attack` ยังไม่ถูกเดา;
  R9E ใช้เฉพาะ groggy/blow/knock-back fields และ resistance ที่ยืนยันจากข้อมูลจริง
- damage ทำงานกับสัตว์ทดลอง entity type `2027`, `2037`, `2039`, `2001`
- ยังไม่ออกแบบ co-op หรือ authoritative network server
