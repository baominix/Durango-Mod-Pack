# Durango Mod Pack

BepInEx plugin source pack for the Original PC client of **Durango: Wild Lands**.

ชุด source plugin BepInEx สำหรับ PC client ต้นฉบับของ **Durango: Wild Lands**

---

## 📁 Project Structure / โครงสร้างโปรเจกต์

<<<<<<< HEAD
```
Durango Mod Pack/
├── plugins/          # C# source code for each plugin / ซอร์สโค้ด C# ของแต่ละ plugin
├── refs/             # Reference DLLs (BepInEx, Unity, etc.) / ไฟล์ DLL อ้างอิง
├── build/            # Build scripts (.ps1) / สคริปต์ build
├── build-output/     # Compiled plugin DLLs / ไฟล์ DLL ที่ build แล้ว
└── artifacts/        # Additional assets / ไฟล์เสริมอื่น ๆ
=======
## Build

Build one plugin entirely inside this repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-GatheringPlugin.ps1"
>>>>>>> dd33197f2330c4c89d484e48ba79d2d10aad4e48
```

> All paths are self-contained within this folder — no external game folder dependencies.
>
> ทุก path อยู่ภายในโฟลเดอร์นี้ทั้งหมด ไม่ต้องพึ่งพาโฟลเดอร์เกมภายนอก

---

## 🔨 Build / วิธี Build

### Build All Plugins / Build ทุก Plugin

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1"
```

### Build Single Plugin / Build Plugin เดี่ยว

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-<PluginName>.ps1"
```

**Example / ตัวอย่าง:**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-GatheringPlugin.ps1"
```

> Output DLLs are written to `build-output/`.
>
> ไฟล์ DLL ที่ build ได้จะอยู่ในโฟลเดอร์ `build-output/`

### Compiler / คอมไพเลอร์

```
C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
```

---

## 📦 Included Plugins / รายชื่อ Plugin ทั้งหมด (28)

| Category | Plugin | Description |
|----------|--------|-------------|
| **Combat / การต่อสู้** | `DurangoCombatSystemPlugin` | Combat system / ระบบการต่อสู้ |
| **Character / ตัวละคร** | `CharacterCreationFixPlugin` | Character creation fix / แก้ไขการสร้างตัวละคร |
| | `SelectCharacterPlugin` | Character selection / เลือกตัวละคร |
| | `PlayerProgressionPlugin` | Player progression / ระบบความก้าวหน้าของผู้เล่น |
| | `SkillSystemPlugin` | Skill system / ระบบสกิล |
| **World / โลก** | `IslandMapRestorationPlugin` | Terrain aliases & simulation templates for restored islands / กู้คืนแผนที่เกาะ |
| | `HarborSailingMapPlugin` | All 24 sailing-map destinations / จุดหมายเดินเรือทั้ง 24 แห่ง |
| | `TamedIslandRestorationPlugin` | Tamed island restoration / กู้คืนเกาะที่เลี้ยงแล้ว |
| | `CustomTerrainLoaderPlugin` | Custom terrain loader / โหลด terrain แบบกำหนดเอง |
| **Gathering & Crafting / เก็บของ & คราฟต์** | `GatheringPlugin` | Resource gathering / ระบบเก็บทรัพยากร |
| | `CraftBuildPlugin` | Crafting & building / ระบบคราฟต์และก่อสร้าง |
| | `AnimalHandlingPlugin` | Animal handling / ระบบจัดการสัตว์ |
| **Economy / เศรษฐกิจ** | `CashShopRestorationPlugin` | Cash shop restoration / กู้คืนร้านค้า Cash Shop |
| | `IslandMarketEnablePlugin` | Island market / ตลาดเกาะ |
| | `TradeAvailablePlugin` | Trading system / ระบบการค้า |
| | `PCCurrencyGroupRestorationPlugin` | PC currency group restoration / กู้คืนกลุ่มสกุลเงิน PC |
| **Social / สังคม** | `PartySystemPlugin` | Party system / ระบบปาร์ตี้ |
| | `OfflineClanRestorationPlugin` | Offline clan restoration / กู้คืนแคลนออฟไลน์ |
| | `SupportOrganizationRestorationPlugin` | Support organization / กู้คืนองค์กรสนับสนุน |
| | `ChatCommandPlugin` | Chat commands. try use /help / คำสั่งแชท ลอง /help |
| **Quest / เควส** | `TaskSystemRestorationPlugin` | Task/quest system / กู้คืนระบบเควส |
| | `CareerGuideEnablePlugin` | Career guide / ระบบแนะนำอาชีพ |
| **UI / อินเทอร์เฟซ** | `GameMenuPlugin` | Game menu / เมนูเกม |
| | `SelectGameMode` | Game mode selection / เลือกโหมดเกม |
| | `TitleBarMenuDisablePlugin` | Disable title bar menu / ปิดเมนู Title Bar |
| | `UISizeOptionsPlugin` | UI size options / ตัวเลือกขนาด UI |
| | `KeybindSettingsPlugin` | Keybind settings / ตั้งค่าปุ่มกด |
| | `Keybind2` | Additional keybinds / ปุ่มกดเพิ่มเติม |

---

## 📝 Notes / หมายเหตุ

- Each plugin has its own build script in `build/Build-<PluginName>.ps1`.
  แต่ละ plugin มีสคริปต์ build ของตัวเองใน `build/Build-<PluginName>.ps1`

- Reference DLLs in `refs/` are shared across all plugins.
  ไฟล์ DLL อ้างอิงใน `refs/` ใช้ร่วมกันทุก plugin

- Build scripts use `$PSScriptRoot` for relative paths — portable and self-contained.
  สคริปต์ build ใช้ relative path ผ่าน `$PSScriptRoot` — พกพาได้ อยู่ในตัว
