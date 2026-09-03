# Durango Mod Pack V1.1

# ภาษาไทย

ชุด source code ของ BepInEx plugins สำหรับ **Durango_Ver_PC_Final / Durango: Wild Lands**

โปรเจกต์นี้ใช้สำหรับการกู้คืนระบบของเกมในรูปแบบ offline / co-op และการพัฒนา mod ภายใน PC Final

> โครงการนี้ไม่ได้ออกแบบเป็น private server ของเกมต้นฉบับ แต่เป็นการ restore / emulate ระบบที่จำเป็นสำหรับ offline / co-op

---
## สถานะ mod ปัจจุบัน: สำหรับทดสอบเท่านั้น ยังใช้เล่นจริง ตามความคาดหวังไม่ได้

## คำสั่งในเกม
Command	ใช้งาน	            รายละเอียด
/help	                    /help	แสดงรายการคำสั่ง

## Requirements

สำหรับการใช้งาน mod ภายในเกม แนะนำ:

- Durango_Ver_PC_Final
- Windows x64
- BepInEx 5.x
- แนะนำ **BepInEx_win_x64_5.4.23.5**

BepInEx:  
https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5

.NET Framework 3.5 C# compiler สำหรับ build source ชุดนี้

Compiler default ที่ build scripts ใช้:

```text
C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
```

ถ้าต้องการใช้ compiler path อื่น สามารถกำหนด environment variable:

```powershell
$env:DURANGO_CSC = "C:\path\to\csc.exe"
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

---

# Recommended Build Environment (นอกเหนือจาก PowerShell)

ปัจจุบันโปรเจกต์สามารถ build ได้ด้วย `.ps1` scripts และ `csc.exe` โดยตรง ซึ่งเหมาะกับงานเร็ว ๆ, automation และการทดสอบ build แบบ lightweight

อย่างไรก็ตาม ในระยะยาว **ควรมี build environment แบบ project-based เพิ่มอีกหนึ่งชุด** ที่ไม่ขึ้นกับ PowerShell เพื่อให้พัฒนา, debug, ตรวจ dependency และดู error ได้ง่ายขึ้น

---

# English

Source code collection of BepInEx plugins for **Durango_Ver_PC_Final / Durango: Wild Lands**

This project is intended for restoring game systems for offline / co-op use and for mod development within PC Final.

> This project is not designed to be a private server for the original game. It focuses on restoring / emulating the systems required for offline / co-op use.

---
## Current mod status: For testing purposes only; it cannot yet be used for actual gameplay as intended.

## In game command
Command	Usage	Description
/help	                    /help	Show available commands

## Requirements

For using the mods in-game, the following are recommended:

- Durango_Ver_PC_Final
- Windows x64
- BepInEx 5.x
- Recommended: **BepInEx_win_x64_5.4.23.5**

BepInEx:  
https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5

.NET Framework 3.5 C# compiler is required to build this source set.

Default compiler used by the build scripts:

```text
C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
```

To use a different compiler path, set the environment variable:

```powershell
$env:DURANGO_CSC = "C:\path\to\csc.exe"
```

---

# Build

## Build a single plugin

Example for building `ChatCommandPlugin`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-ChatCommandPlugin.ps1"
```

Or call the generic builder directly:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-Plugin.ps1" -PluginName "ChatCommandPlugin"
```

Output:

```text
build-output\ChatCommandPlugin.dll
```

### Clean before building a single plugin

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-ChatCommandPlugin.ps1" -Clean
```

---

## Build all plugins

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1"
```

Or clean generated plugin outputs first:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1" -Clean
```

`-Clean` removes only generated plugin DLLs and companion data created by the builder, for example:

```text
build-output\*.dll
```

---

# Recommended Build Environment (Beyond PowerShell)

The project can currently be built directly with `.ps1` scripts and `csc.exe`, which is suitable for quick tasks, automation, and lightweight build testing.

However, in the long term, **a project-based build environment should also be provided** without depending on PowerShell, making development, debugging, dependency inspection, and error review easier.
