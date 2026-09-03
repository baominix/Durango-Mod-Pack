# ReferenceData

> **Development / build-time only**  
> `DurangoCombatSystemPlugin 0.3.12+` ไม่อ่าน folder นี้ตอน runtime และไม่ต้องแจก
> `ReferenceData` ไปกับ DLL ข้อมูลที่จำเป็นจะถูกคัดและ embed เข้า
> `DurangoCombatSystemPlugin.dll` ตอน build

สำเนาข้อมูลต้นฉบับสำหรับค้นคว้าและพัฒนา `DurangoCombatSystemPlugin`

คัดลอกเมื่อ 2026-08-24 จาก:

`D:\ProgramData\Durango_Ver_PC_Final\assetRipper_Export_original\AssetRipper_export_20260728_212417\ExportedProject\Assets`

## เนื้อหา

- `models\animals\<animal>\*_framework.asset` — Framework สัตว์ 27 ไฟล์ โดยรักษาโครงสร้างโฟลเดอร์เดิม
- `entity_types\animal.json` — ข้อมูล entity สัตว์ต้นฉบับ
- `player\player_battle_actions.json` — attack impact metadata ของผู้เล่น เช่น
  frame, groggy, blow power, knock-back force, hit-force และ strong-attack;
  สำเนาตรงจาก `Resources\offline\assets\player`
- `saurus_root_motion.json` — derivative ที่สร้างจาก `Bip001` position curve ของ
  AnimationClip ต้นฉบับด้วย `Generate-SaurusRootMotionData.ps1`; เก็บ key/tangent
  เดิมในรูปแบบที่ runtime รุ่น .NET 3.5 อ่านได้

ไฟล์ `.meta` ไม่ได้คัดลอก เพราะงานนี้ใช้ข้อมูลใน asset/JSON เป็น reference ไม่ได้นำเข้า Unity project

## กฎการใช้งาน

- ไฟล์ Framework, `animal.json` และ `player_battle_actions.json` ถือเป็น
  read-only snapshot
- ห้ามแก้ค่าในสำเนาเพื่อปรับ gameplay
- ค่าที่ reconstruct เช่น AI factor ต้องเก็บแยกจาก `ReferenceData`
- `saurus_root_motion.json` ห้ามแก้ด้วยมือ ให้ regenerate จาก AnimationClip เท่านั้น
- หากต้นฉบับเปลี่ยน ให้คัดลอกใหม่และตรวจ checksum ก่อนใช้งาน
