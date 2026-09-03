# Durango Mod Pack V1.1

ชุด source code ของ BepInEx plugins สำหรับ **Durango_Ver_PC_Final / Durango: Wild Lands**  
โปรเจกต์นี้ใช้สำหรับการกู้คืนระบบของเกมในรูปแบบ offline / co-op และการพัฒนา mod ภายใน PC Final

> โครงการนี้ไม่ได้ออกแบบเป็น private server ของเกมต้นฉบับ แต่เป็นการ restore / emulate ระบบที่จำเป็นสำหรับ offline / co-op

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
```