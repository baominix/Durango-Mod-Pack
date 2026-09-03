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
