# Durango Mod Pack V1.1

ชุด source code ของ BepInEx plugins สำหรับ **Durango_Ver_PC_Final / Durango: Wild Lands**  
โปรเจกต์นี้ใช้สำหรับการกู้คืนระบบของเกมในรูปแบบ offline / co-op และการพัฒนา mod ภายใน PC Final

> โครงการนี้ไม่ได้ออกแบบเป็น private server ของเกมต้นฉบับ แต่เป็นการ restore / emulate ระบบที่จำเป็นสำหรับ PC Final

---

## Requirements

สำหรับการใช้งาน mod ภายในเกม แนะนำ:

- Durango_Ver_PC_Final
- Windows x64
- BepInEx 5.x
- แนะนำ **BepInEx_win_x64_5.4.23.5**
- .NET Framework 3.5 C# compiler สำหรับ build source ชุดนี้

BepInEx:

https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5

Compiler default ที่ build scripts ใช้:

```text
C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
```

ถ้าต้องการใช้ compiler path อื่น สามารถกำหนด environment variable:

```powershell
$env:DURANGO_CSC = "C:\path\to\csc.exe"
```

---

## Folder Structure

```text
Durango Mod Pack
├─ build
│  ├─ Build-All.ps1
│  ├─ Build-Plugin.ps1
│  └─ Build-<PluginName>.ps1
│
├─ plugins
│  ├─ ChatCommandPlugin
│  ├─ DurangoCombatSystemPlugin
│  ├─ SkillSystemPlugin
│  └─ ...
│
├─ refs
│  ├─ BepInEx.dll
│  ├─ 0Harmony.dll
│  ├─ Assembly-CSharp.dll
│  ├─ ExternalLibrary.dll
│  ├─ UnityEngine*.dll
│  ├─ MsgPack.dll
│  ├─ NCalc.dll
│  └─ ...
│
├─ build-output
│  ├─ *.dll
│  ├─ MapEditorPlugin
│  └─ Durango_Data
│
├─ img
└─ README.md
```

### `plugins`

เก็บ source code ของ plugin แต่ละตัว

`Build-All.ps1` จะใช้ folder ที่อยู่ภายใน `plugins` เป็น **source of truth**  
หมายความว่า plugin ที่อยู่ใน folder นี้จะถูกนำไป build อัตโนมัติ

### `refs`

เก็บ DLL references ที่จำเป็นสำหรับ compile

Build scripts จะอ่าน DLL ทุกไฟล์ใน `refs` อัตโนมัติ จึงไม่ต้อง hardcode path ไปยัง:

```text
Durango_Original\Durango_Data\Managed
BepInEx\core
```

ในแต่ละ plugin อีกต่อไป

ถ้า plugin ใหม่ต้องใช้ assembly เพิ่ม สามารถใส่ DLL ที่จำเป็นลงใน `refs` ได้

### `build`

เก็บ PowerShell build scripts

ระบบ build หลักมี 2 ตัว:

```text
Build-Plugin.ps1
Build-All.ps1
```

ส่วนไฟล์:

```text
Build-ChatCommandPlugin.ps1
Build-SkillSystemPlugin.ps1
Build-DurangoCombatSystemPlugin.ps1
...
```

เป็น wrapper สำหรับเรียก `Build-Plugin.ps1` ด้วยชื่อ plugin ที่ถูกต้อง

### `build-output`

เป็นปลายทางของไฟล์ที่ build สำเร็จ

Build scripts **จะไม่ deploy DLL เข้าเกมโดยอัตโนมัติ** และจะไม่เขียน DLL กลับเข้า source folder

ตัวอย่าง:

```text
build-output\ChatCommandPlugin.dll
build-output\SkillSystemPlugin.dll
build-output\AnimalHandlingPlugin.dll
```

---

# Build

## Build plugin ตัวเดียว

ตัวอย่าง build `ChatCommandPlugin`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-ChatCommandPlugin.ps1"
```

หรือเรียก generic builder โดยตรง:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-Plugin.ps1" -PluginName "ChatCommandPlugin"
```

ผลลัพธ์:

```text
build-output\ChatCommandPlugin.dll
```

### Clean ก่อน build ตัวเดียว

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-ChatCommandPlugin.ps1" -Clean
```

---

## Build plugin ทั้งหมด

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1"
```

หรือ clean generated plugin outputs ก่อน:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1" -Clean
```

`-Clean` จะลบเฉพาะ generated plugin DLL และ companion data ที่ builder สร้างขึ้น เช่น:

```text
build-output\*.dll
build-output\MapEditorPlugin
```

รวมถึงล้าง `build-output\ReferenceData` หากเป็น output เก่าจาก builder รุ่นก่อน

โดยจะ **ไม่ลบ** `build-output\Durango_Data`

---

## Local Plugin Dependencies

บาง plugin ใช้ source/type จาก plugin อื่นตอน compile

ปัจจุบันมี dependency ที่ builder จัดการให้อัตโนมัติ:

```text
TamedIslandRestorationPlugin
└─ HarborSailingMapPlugin.dll
```

ดังนั้นถ้าเรียก:

```powershell
.\build\Build-TamedIslandRestorationPlugin.ps1
```

และยังไม่มี:

```text
build-output\HarborSailingMapPlugin.dll
```

builder จะ build `HarborSailingMapPlugin` ก่อนให้เอง

---

## Source Filtering

`Build-Plugin.ps1` compile ไฟล์ `*.cs` ภายใน plugin แบบ recursive เพื่อรองรับ plugin ที่แบ่ง source เป็นหลาย sub-folder เช่น:

```text
DurangoCombatSystemPlugin
├─ Actions
├─ Damage
├─ Data
├─ Geometry
├─ Presentation
├─ Runtime
├─ SaurusAI
└─ Statistics
```

ไฟล์ backup จะไม่ถูกนำไป compile เช่น:

```text
*.backup_*.cs
*.backup.cs
*.bak.cs
```

รวมถึง source ที่อยู่ใน child directory ประเภท:

```text
backup
backups
_backups
test
tests
disabled
```

จึงไม่เกิด duplicate class จากไฟล์ source สำรอง

---

# Runtime Companion Data

บาง plugin ต้องมี data เพิ่มนอกจาก DLL

## DurangoCombatSystemPlugin

**ไม่ต้องใช้ companion data ภายนอกแล้ว**

หลัง build ใช้งานเพียง:

```text
build-output\DurangoCombatSystemPlugin.dll
```

combat data ที่จำเป็นถูก embed เข้า DLL ตอน compile ได้แก่:

- animal profiles ที่ runtime รองรับ: `2001 / 2027 / 2037 / 2039`
- Framework: `Tricera / Phenacodus / Raptor`
- Saurus root-motion เฉพาะ clip ที่ framework เหล่านี้อ้างถึง
- `player_battle_actions` สำหรับ impact metadata ของ player combat

folder:

```text
plugins\DurangoCombatSystemPlugin\ReferenceData
```

เป็น **development/build-time snapshot เท่านั้น** และไม่ต้องแจกไปกับ DLL

## MapEditorPlugin

หลัง build จะมี:

```text
build-output
├─ MapEditorPlugin.dll
└─ MapEditorPlugin
   └─ model_catalog.tsv
```

หมายเหตุ: MapEditor เป็น optional tooling; ถ้าไม่ได้ใช้งาน ไม่จำเป็นต้องติดตั้ง DLL นี้ในเกม

---

# References Included in This Pack

ระบบ build ปัจจุบันทดสอบกับ references ภายใน `refs` ซึ่งรวมถึง:

```text
0Harmony.dll
Assembly-CSharp.dll
BepInEx.dll
ExternalLibrary.dll
ICSharpCode.SharpZipLib.dll
MsgPack.dll
NCalc.dll
UnityEngine.dll
UnityEngine.CoreModule.dll
UnityEngine.AnimationModule.dll
UnityEngine.PhysicsModule.dll
UnityEngine.IMGUIModule.dll
UnityEngine.InputModule.dll
UnityEngine.ParticleSystemModule.dll
```

อย่าลบ reference เหล่านี้หากยังมี plugin ใช้งานอยู่

---

# Plugins

ณ โครง source ปัจจุบัน `plugins` มี 36 build targets:

```text
AnimalHandlingPlugin
CareerGuideEnablePlugin
CashShopRestorationPlugin
CharacterCreationFixPlugin
ChatCommandPlugin
CraftBuildPlugin
DecorativeGearPlugin
DeveloperModePlugin
DurangoCombatSystemPlugin
FoodConsumptionPlugin
GameMenuPlugin
GatheringPlugin
HarborSailingMapPlugin
InventoryLockPlugin
IslandMapRestorationPlugin
IslandMarketEnablePlugin
KeybindSettingsPlugin
LogControlPlugin
MapEditorPlugin
MobilePCUISwitchPlugin
NPCFriendListPlugin
OfflineClanRestorationPlugin
OfflineSurvivalPlugin
PartySystemPlugin
PCCurrencyGroupRestorationPlugin
PlayerProgressionPlugin
SelectCharacterPlugin
SelectGameMode
SkillSystemPlugin
SupportOrganizationRestorationPlugin
TamedIslandRestorationPlugin
TaskSystemRestorationPlugin
TitleBarMenuDisablePlugin
TradeAvailablePlugin
UISizeOptionsPlugin
WeatherModePlugin
```

---

# Build Test

ระบบ build แบบ self-contained ภายใน folder นี้ได้รับการทดสอบด้วย:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1" -Clean
```

ผลการทดสอบ:

```text
Plugins : 36
Success : 36
Failed  : 0
Output  : Durango Mod Pack\build-output
```

ตัวอย่าง output ที่ตรวจสอบแล้ว:

```text
build-output\ChatCommandPlugin.dll
build-output\DurangoCombatSystemPlugin.dll
build-output\MobilePCUISwitchPlugin.dll
build-output\NPCFriendListPlugin.dll
build-output\TamedIslandRestorationPlugin.dll
build-output\WeatherModePlugin.dll
```

---

# การติดตั้ง BepInEx เบื้องต้น

1. แตกไฟล์ BepInEx x64 ลงใน folder เกม Durango
2. เปิดเกมอย่างน้อย 1 ครั้ง เพื่อให้ BepInEx สร้าง directory/config ที่จำเป็น
3. ปิดเกม
4. นำ DLL ที่ต้องการจาก `build-output` ไปวางใน:

```text
BepInEx\plugins
```

5. สำหรับ plugin ที่มี companion data จริง เช่น `MapEditorPlugin` ให้ copy data ที่เกี่ยวข้องไปด้วย
6. `DurangoCombatSystemPlugin` ใช้เฉพาะ DLL ไม่ต้อง copy `ReferenceData`
7. เปิดเกมและตรวจสอบ `BepInEx\LogOutput.log`

> `build-output` เป็น build staging area ไม่ใช่การ deploy เข้าเกมอัตโนมัติ

---

## Development Notes

- หลีกเลี่ยงการ hardcode absolute path ใน build scripts
- source plugin ต้องอยู่ใต้ `plugins\<PluginName>`
- compile references ต้องอยู่ใน `refs`
- generated binaries ต้องออกไป `build-output`
- อย่า commit `*.staging.dll` หรือ DLL ที่ build ชั่วคราวจาก source folder
- หากเพิ่ม plugin ใหม่ ให้สร้าง folder ใน `plugins` และเพิ่ม wrapper `Build-<PluginName>.ps1` เมื่ออยาก build แบบ shortcut
- `Build-All.ps1` ไม่จำเป็นต้องแก้รายชื่อ plugin เมื่อเพิ่ม folder ใหม่ เพราะอ่านรายการจาก `plugins` โดยตรง
