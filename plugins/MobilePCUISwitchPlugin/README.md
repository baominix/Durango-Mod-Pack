# MobilePCUISwitchPlugin

ปลั๊กอิน BepInEx สำหรับสลับชุด UI ดั้งเดิมของ Durango ระหว่าง Mobile และ PC ขณะเกมกำลังรัน

เวอร์ชัน 1.2.11 ใช้ PC UI ในหน้าเริ่มเกม/เชื่อมต่อ/เลือกตัวละครและ Prologue เสมอ ส่วนโหมด Mobile มีผลเฉพาะฉาก Main เพื่อแยก UI ออกจากขั้นตอน authentication และยังทำงานร่วมกับ `UISizeOptionsPlugin`, `KeybindSettingsPlugin` และ `Keybind2`

## การใช้งาน

- PC UI: เปิด Settings > Screen แล้วเปลี่ยน `UI Mode` เป็น `PC` หรือ `Mobile` ด้วย dropdown โดยแถวนี้อยู่เหนือ `UI Size`
- Mobile UI: เปิด Settings > Basic แล้วเปลี่ยน `UI Mode` ด้วยปุ่มลูกศรแบบ Toggle ที่แถวบนสุด (Mobile prefab ดั้งเดิมไม่มี Dropdown widget และไม่มี Screen category)
- ค่าเริ่มต้นคือ `PC`; ค่า `Auto` จากรุ่นเก่าจะถูกย้ายเป็น `PC` โดยอัตโนมัติ
- เมื่อเลือก `Mobile` จะใช้ Mobile UI จริงตั้งแต่ startup/Title/การเลือกตัวละคร/Prologue ไปจนถึงฉาก `Main`; หน้า Title มี diagnostic log ของ state และ local endpoint เพื่อระบุจุดค้างได้หากการเข้าเกมล้มเหลว
- ก่อนเข้า/ออกฉาก `Main` ปลั๊กอินจะเตรียมโครง Settings, virtual UI size และ anchor policy ให้ตรงกับ UI ของฉากปลายทางก่อนสร้าง `UIManager` เพื่อลดอาการ UI ซ้อนหรือเปลี่ยนขนาดระหว่าง loading
- หลังเปลี่ยนค่า ปลั๊กอินจะโหลดโครง Settings และฉากปัจจุบันใหม่เพื่อแทน prefab ที่สร้างไปแล้ว
- ก่อนเปลี่ยน UI จะมีกล่องยืนยัน และจะเปลี่ยนจริงเฉพาะเมื่อกด Confirm
- `UISizeOptionsPlugin` 0.4.6 แยกชุด `UI Size` ของแต่ละโหมด: PC ใช้ `800`, `1024`, `1280`, `1600`, `1920`; Mobile ใช้ `1400`, `1600`, `1800`, `2000`, `2200`
- ชื่อระดับ Mobile ตรงกับค่าจริงดังนี้: `1400` = Very Large, `1600` = Large, `1800` = Normal, `2000` = Small และ `2200` = Very Small
- PC เก็บค่าที่ `option:ui_size` ส่วน Mobile เก็บแยกที่ `option:ui_size_mobile`; ค่าเริ่มต้นของแต่ละฝั่งคือ `1280` และการเปลี่ยนฝั่งหนึ่งไม่เขียนทับอีกฝั่ง
- Mobile UI แสดง `UI Size` ใน Basic ถัดจาก `UI Mode` และใช้ปุ่มลูกศรแบบ Toggle ตาม widget ดั้งเดิมของ Mobile
- `KeybindSettingsPlugin` 0.2.5 ใช้ resolver ตรงแทน cache ของ `IconMap` กับทั้ง Keybind และ Game Menu PC ทำให้ Map ไม่หายหลังสลับโหมด; Keybind Mobile ใช้ tile ทรงวงกลมและ sprite แบบ PC พร้อม fallback เมื่อ atlas PC ไม่อยู่ในฉาก
- ปลั๊กอินจะโหลดหน้า `Keybind` และ `Keybind2` กลับผ่าน patch chain เดิมทุกครั้งที่สลับ UI
- Hotkey สำรอง `F6`/`F7` ปิดไว้โดยค่าเริ่มต้นเพื่อไม่ให้ชนกับ keybind; เปิดได้ด้วย `EnableHotkeys=true` ในไฟล์ config

การโหลดฉากใหม่จำเป็นเพราะเกมเลือก prefab ตั้งแต่ตอนสร้าง UI การเปลี่ยน getter เพียงอย่างเดียวไม่สามารถแทนวัตถุที่ถูกสร้างไปแล้วได้ ก่อนสร้าง UI ของฉากปลายทาง ปลั๊กอินจะเรียก loader เดิมของ `ConfigInstance` ใหม่และตั้งค่า anchor/virtual size โดยตรง ทำให้ `config_menu`/`config_menu_pc` และส่วนขยายของปลั๊กอินเก่าตรงกับ UI ที่มีผลจริงโดยไม่ส่ง resize event ไปยังฉากเดิม

ก่อน `ConfigMainWidget.Awake` เวอร์ชัน 1.2.3 ตรวจชนิด schema จาก `ui_size` ดั้งเดิมด้วย: PC ต้องเป็น Dropdown และ Mobile ต้องเป็น Toggle หาก widget กับ schema ไม่ตรงกันจะโหลด settings ของโหมด widget ใหม่ก่อนสร้างรายการ ป้องกันหน้า PC แสดง Basic schema ของ Mobile แม้ปลั๊กอินเก่าจะเพิ่มหมวด Screen เอง

เวอร์ชัน 1.2.4 บังคับ `UIPrefabMap.GetMain` ให้เลือก Main prefab จากค่าที่ปลั๊กอินเก็บโดยตรง และล็อกตัวเลือก PC/Mobile ตลอดช่วง `ConfigInstance.LoadFromJson` ทำให้ prefab กับ `config_menu_pc`/`config_menu` ไม่สามารถแยกโหมดกันเพราะค่า `Platform.UIType` ชั่วคราวได้

เวอร์ชัน 1.2.5 ไม่ reload settings ซ้ำใน `UIManager.Awake` หากหน้า Title เตรียมฉาก Main ไว้แล้ว และเปลี่ยน locale แบบเงียบเฉพาะระหว่าง automatic config load เพื่อไม่เรียก MessageBox ก่อน Popup UI ถูกสร้าง

เวอร์ชัน 1.2.6 สร้าง Mobile `ui_size` Toggle ใหม่ใน release build เพราะรายการดั้งเดิมมี `DebugBuild=true` และถูก `ConfigInstance.LoadFromJson` ตัดออกก่อน Harmony postfix โดยแถวใหม่นี้อยู่ใต้ `UI Mode` และมีตัวเลือก 5 ค่า

เวอร์ชัน 1.2.12 ยกเลิกข้อจำกัดที่บังคับ Title เป็น PC เมื่อเลือก Mobile และเลือก `UIPrefabMap.Category.Title` แบบ Mobile โดยตรง พร้อมบันทึก Title state, local HTTP URL และการได้รับ response เพื่อทดสอบปัญหาค้างหน้าเข้าเกมโดยไม่เปลี่ยน endpoint ของเกม

เวอร์ชัน 1.2.13 แก้ cold startup ใน Mobile ที่ `LocalizeSystem.SetLocale` เรียกติดตั้ง UI Mode/UI Size ระหว่าง `ConfigInstance.LoadConfigValue` กำลังวน `List<Setting>` จนเกิด `InvalidOperationException` และทำให้ `GameManager.OnAwake` หยุดกลางทาง รุ่นนี้เลื่อน structural settings update ไปหลังการโหลดค่าครบ โดยยังใช้ Title Mobile จริงตามเดิม

เวอร์ชัน 1.2.14 ให้ Title Mobile ใช้วิดีโอที่มีอยู่จริงใน `StreamingAssets/Movie/PC` เนื่องจาก prefab เดิมอ้าง `Movie/Mobile/title.mp4` และไฟล์ Mobile ไม่มีในชุดเกมนี้ การแก้เปลี่ยนเฉพาะ path วิดีโอของ TitleOptions โดยไม่เปลี่ยน Mobile prefab, controller หรือ layout

เวอร์ชัน 1.2.7 แยกชุดขนาด Mobile ออกจาก PC โดยจำกัดค่าสูงสุดที่ `1600` และใช้ `1420` เป็นระดับ Small; `UISizeOptionsPlugin` 0.4.2 เขียนชื่อระดับลงใน Toggle โดยตรงเพื่อให้ `1024` แสดงเป็น Large และ `1600` แสดงเป็น Very Small เสมอ ค่า Mobile เดิมที่บันทึกเป็น `1920` จะถูกย้ายเป็น `1600` อัตโนมัติ

เวอร์ชัน 1.2.8 กลับทิศสเกล Mobile ให้ตรงกับขนาดที่เห็นจริง: `1420/1280/1024/800/600` ตรงกับ Very Large/Large/Normal/Small/Very Small ตามลำดับ ค่าเริ่มต้น Mobile เปลี่ยนเป็น `1024` และค่าจากชุด 1.2.7 จะถูกย้ายตามระดับเดิมเพียงครั้งเดียว

เวอร์ชัน 1.2.9 เปลี่ยนชุดทดลอง Mobile เป็น `1024/1280/1420/1600/1920` ตรงกับ Very Large/Large/Normal/Small/Very Small ตามลำดับ ค่าเริ่มต้นของปลั๊กอินเป็น `1420` (Normal) และค่าจากชุดก่อนหน้าจะถูกย้ายตามชื่อระดับเดิมเพียงครั้งเดียว

เวอร์ชัน 1.2.10 ใช้ชุด Mobile `1400/1600/1800/2000/2200` ตรงกับ Very Large/Large/Normal/Small/Very Small ตามลำดับ ค่าเริ่มต้นของปลั๊กอินเป็น `1800` (Normal) และย้ายค่าจากชุดทดลอง 1.2.8/1.2.9 หรือชุดเก่ากว่าตามชื่อระดับเดิมเพียงครั้งเดียว

UISizeOptionsPlugin 0.4.6 กลับทิศปุ่มลูกศรเฉพาะ Mobile `ui_size` ให้ซ้าย/ขวาตรงกับการเพิ่มและลดขนาดที่เห็นจริง โดยไม่เปลี่ยนทิศของ `UI Mode` หรือ Toggle อื่น

เวอร์ชัน 1.2.11 ปลด `HideOnOffline` หลังโหลด Settings ทุก schema ทำให้ Auto-Translate และกลุ่ม Chat แสดงใน Online, Offline และ Editable ทั้ง PC/Mobile โดยยังคงเงื่อนไข Prologue, platform, country, locale และ release อื่นไว้

> ควรปิดหน้าต่างเมนูและบันทึกความคืบหน้าก่อนสลับในฉาก Main เพราะ Unity จะโหลดฉากนั้นใหม่

## Build

จาก PowerShell:

```powershell
.\Build-MobilePCUISwitchPlugin.ps1
```

คำสั่งนี้สร้าง `MobilePCUISwitchPlugin.staging.dll` ไว้ในโฟลเดอร์ source โดยยังไม่ติดตั้งเข้าเกม

Build และติดตั้งเข้า `Durango_Original\BepInEx\plugins\MobilePCUISwitchPlugin`:

```powershell
.\Build-MobilePCUISwitchPlugin.ps1 -Deploy
```

ไฟล์ตั้งค่าจะถูกสร้างหลังเปิดเกมครั้งแรกที่:

`Durango_Original\BepInEx\config\com.baominix.durango.original.mobilepcuiswitch.cfg`

## ขอบเขตความปลอดภัย

ปลั๊กอินเปลี่ยนเฉพาะตัวเลือก UI, portrait policy และ virtual resolution ของ Mobile UI เท่านั้น โดย Mobile มีค่าเริ่มต้นดั้งเดิม 1280 และเลือกขนาดอื่นได้จาก Basic ไม่แก้ `UsePCRenderer`, asset-bundle platform, store, wallet หรือชนิดเงินของเกม ดังนั้นยังใช้ renderer และระบบแพลตฟอร์ม Windows ตามเดิม
