# แหล่งข้อมูลระบบต่อสู้

ค้นใหม่เมื่อ 2026-08-24 โดยไม่ถือว่าโค้ดปลั๊กอินเก่าเป็นความจริงของเกม

## ลำดับความน่าเชื่อถือ

1. สคริปต์และ asset จาก export ต้นฉบับล่าสุด
2. JSON/Framework/AnimationClip ที่ bundle มากับเกม
3. message contract และ type ของ client
4. map bytes ต้นฉบับ
5. ภาพหรือวิดีโอ gameplay สำหรับตรวจพฤติกรรมที่ข้อมูลไม่ได้อธิบาย
6. Durango Wildlands Mastersheet สำหรับ cross-check ชื่อ/ความสัมพันธ์
7. export รุ่นเก่าและ source ปลั๊กอิน `.disable` ใช้เป็น fallback/regression เท่านั้น

## Source of truth หลัก

ฐาน export:

`D:\ProgramData\Durango_Ver_PC_Final\assetRipper_Export_original\AssetRipper_export_20260728_212417\ExportedProject\Assets`

ตำแหน่งสำคัญ:

- `Scripts\Assembly-CSharp` — logic/type/message ของ client
- `Resources\offline\assets\entity_types\animal.json` — ค่าสัตว์, stat, framework, model, root-motion set และ AI-factor id
- `Resources\offline\assets\player\player_battle_actions.json` — action ผู้เล่น, cooldown, stamina, timing และพื้นที่โจมตี
- `Resources\offline\assets\survival\status_effects.json` — status effect
- `models\animals\phenacodus\Phenacodus_framework.asset`
- `models\animals\tricera\tricera_framework.asset`
- `models\animals\raptor\Raptor_framework.asset`
- `AnimationClip` — animation clip และ root motion ที่ export ได้

Export สำรอง:

`D:\ProgramData\Durango_Ver_PC_Final\xapk_export\AssetRipper_export_20260622_190447`

ใช้เฉพาะเมื่อไฟล์ใน export ล่าสุดหายหรือเสีย และต้องเขียนกำกับในโค้ด/เอกสารทุกครั้ง

## Class ที่ยืนยันจากตัวเกม

### Combat ฝั่ง client

- `CombatSystem` — combat mode, target, action ที่กำลังใช้, action cooldown และ event combat
- `UsingAction` — state ของการเตรียม/เล่น/ใช้/ยกเลิก/จบ action
- `BattleAction` — action ที่ active พร้อม cooldown/prohibited timer
- `DamagedProcesser` — คิวผล damage, blow, knock-back และการเสีย control
- `DamageableEntity` — จุดรับ damage ของ entity
- `AnimalBehavior` — การแสดง animation/root motion/gauge ของสัตว์
- `AreaOfEffectVisualizer` — วาดวง, arc และสี่เหลี่ยมจาก `AttackAlerted`
- `RootMotionMovable` — นำ root motion ของ animation ไปชดเชย mesh/ฐานตัวละคร

### AI ที่พบจริง

- `StateBasedAI<T>` — state machine พื้นฐาน
- `GrazingPetAI` — AI เดิน/กิน/พักของสัตว์เลี้ยง ชื่อที่ถูกคือ **Grazing** ไม่ใช่ Gazing
- `PetAI` — state ของ pet เช่น normal, chase, battle, ride, cage
- `PrologueAIRaptor` — AI raptor แบบ local เฉพาะฉาก prologue
- กลุ่ม `NpcAI*`

ไม่พบ `WildAnimalAI` ใน client ต้นฉบับ และไม่มี state machine สัตว์ป่าที่สมบูรณ์สำหรับ offline server

`PrologueAIRaptor` ใช้ดูรูปทรง state machine ได้ เช่น stand, chase, leap, flinch, blow, dead แต่ห้ามนำ stat และ timing ไปใช้กับสัตว์ทุกชนิดโดยตรง

## Message contract ที่ระบบใหม่ควรรักษา

- `UseBattleAction` (TypeCode 3440): `ActionId`, `StartAt`, `TargetEntityId`, `TargetTile`
- `Damaged` (TypeCode 12): `VictimId`, `AttackerId`, `Damage`, `EventAt`
- `BattleBegun` (TypeCode 3278): `EntityId`, `EventAt`, `EnemyId`, `StartDamaged`
- `BattleEnded` (TypeCode 3587): `EntityId`, `EventAt`
- `SurvivalUpdated` (TypeCode 183): gauge ที่เปลี่ยนและรายการที่ถูกลบ
- `Actions` (TypeCode 315): รายการ `ActionStatus`
- `ActionStatus`: `Id`, `Stamina`, `Cooltime`

ผลสำรวจ `Durango\Offline\Player.cs` ไม่พบ handler ของ `UseBattleAction`, `BattleBegun`, `BattleEnded` และ `Damaged` จึงต้องมี offline combat bridge ในปลั๊กอินใหม่จริง ไม่ใช่เพียงเรียก refresh UI

## Damage schema

`Damage` มีข้อมูลหลัก:

- `Result`
- `Value`
- `Part`
- `Direction`
- `AttackType`
- `Effects`

`DamageResult` แยก `Hit`, `Guarded`, `Dodged`, `Missed`, `Evaded`, `Countered` และผล auto ต่าง ๆ ดังนั้น **Miss กับ Dodge ต้องเป็นคนละผล**

`DamageDirection`:

- Front = 0
- Back = 1
- Left = 2
- Right = 3

`DamageEffects` เป็น flags เช่น Critical, KnockBack, Blow, Tamed, CrossCounter, Incapacitate

## Attack geometry ที่เกมรองรับ

`Shared.Battle.DamageType`:

- `Melee = 0`
- `CircularArea = 1`
- `RectangularArea = 2`
- `Ranged = 3`

ทั้ง `PlayerActionAttackInfo` และ animal `AttackInfo` มีแนวคิดร่วมกัน:

- timing/frame
- radius และ angles
- rect half size
- offset
- damage angle
- use target origin

`PlayerActionAttackInfo.MakeAlerted()` คำนวณ center จากตำแหน่งและ yaw ณ เวลาสร้าง alert แล้วสร้าง `AttackAlerted` ส่วน `AreaOfEffectVisualizer` เพียงแสดง geometry จาก message นี้ แสดงว่าฝั่ง simulation ต้องส่ง snapshot ที่ถูกต้อง และ UI ไม่ควรเดาตำแหน่งใหม่จากตัวสัตว์ทุก frame

### ข้อค้นพบเพิ่มใน Phase 4

- `UsingActionAlert.Update()` ของ client เดิมบวก root-motion delta แล้วเรียก `AreaOfEffectVisualizer.Move()` ทุก frame จึงไม่เหมาะเป็น authoritative telegraph ของ runtime ใหม่
- `Durango.Offline.Player.HandleMoveMsg()` เก็บ movement ล่าสุดไว้ใน `PlayerContext.AppearPlayer.Move`; runtime ใช้ local player pose ณ commit และมี context เป็น fallback
- `AnimalManager`/`AnimalBehavior` ให้ entity id, entity type, position, yaw, bound และ life gauge ที่ใช้สร้าง target adapter ได้โดยไม่แก้ private field
- `ObjectManager` รับ `SurvivalUpdated` แล้วอัปเดต life/gauges ของ `CharacterBehavior`
- `CombatSystem` รับ `Damaged`, `BattleBegun`, `BattleEnded` และ `AttackAlerted` ผ่าน message contract เดิม
- player action ทั้งหมดใน `player_battle_actions.json` ชุดปัจจุบันไม่มีรายการ `use_target_origin=true`; semantics ของ true จึงถูกระบุเป็น reconstructed จนกว่าจะพบข้อมูลเพิ่ม

## ข้อมูลประกอบ

### Workbook

`D:\ProgramData\Durango_Ver_PC_Final\tools\excel\Durango Wildlands Mastersheet.xlsx`

มีชีต Animal(WIP), Stat, Stats Detail, Ability, Skill Tree ฯลฯ และใช้ตรวจความสัมพันธ์เชิงออกแบบได้ เช่น Str/Agi/Dex กับ stat รอง แต่เป็นเอกสารชุมชน/WIP ไม่ใช่สูตร runtime ที่ยืนยันจากโค้ด

### Map bytes

`D:\ProgramData\Durango_Ver_PC_Final\tools\DurangoOriginalMapBytes`

พบแผนที่:

`pe10gr_1`–`pe10gr_5`, `ra60sw`, `ri35de`, `ri35te`, `ri40tr`, `ri45sa`, `ri50sn`, `ri55tu`, `ua60vol`

ใช้สำหรับระบุ spawn/context ของโลก ไม่ควรเป็นแหล่ง stat combat หลัก

## ข้อมูลที่ไม่มีในไฟล์ปัจจุบัน

- definition ของ `protoceratops_ai`
- definition ของ `phenacodus_ai`
- definition ของ `deinonychus_savana_ai`
- wild-animal server state machine ต้นฉบับ
- authoritative damage formula ฝั่ง server แบบครบทุกกรณี
- กฎ threat/aggro/pack behavior ต้นฉบับทั้งหมด

ชื่อ AI factor ข้างต้นถูกอ้างใน `animal.json` แต่ค้นไม่พบ definition ใน export ปัจจุบัน จึงต้องสร้าง profile ที่ปรับค่าได้และระบุชัดว่าเป็นค่าที่ reconstruct ขึ้น ไม่ควรปลอมว่าเป็นค่าที่กู้จากเกม

## แหล่งที่ห้ามใช้เป็น source of truth

`D:\ProgramData\Durango_Ver_PC_Final\tools\durango-mod-original\DurangoCombatSystemPlugin.disable`

อนุญาตให้อ่านภายหลังเพื่อ:

- สร้างรายการ bug regression
- ตรวจ Harmony target ที่เคยทดลอง
- ตรวจ code path ที่เคยทำให้ patch ซ้ำ

ไม่อนุญาตให้ copy class/constant/timing เข้าระบบใหม่ก่อนตรวจเทียบกับข้อมูลต้นฉบับ
