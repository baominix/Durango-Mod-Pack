# จุดอ้างอิงจากไฟล์เกม

ข้อมูลที่ใช้ตรวจสอบมาจาก:

- `ui-dump`
- `assetRipper_Export_original\AssetRipper_export_20260728_212417\ExportedProject\Assets`
- `dnSpy_Export_original`

## เส้นทางเลือก UI

1. `Durango.System.Platform.UsePCUI` ระบุ Mobile หรือ PC
2. `Platform.UIType` แปลงเป็น `UIPrefabMap.Type.Mobile` หรือ `UIPrefabMap.Type.PC`
3. `UIPrefabMap.GetMain/GetTitle/GetPrologue` เลือกรายการ prefab
4. `LinkedPrefabs.Load` สร้าง prefab ใต้ UI root

`Platform_PC.UsePCUI` คืนค่า `true` แบบคงที่ในเกมเดิม จึงต้อง patch getter นี้โดยตรง ไม่ใช่แก้ `DeveloperSettings.UsePCUI` ซึ่งใน build นี้ไม่มีผู้เรียกใช้งานจริง

## รายการใน ui_prefab_map

- Main Mobile: 95, Main PC: 96
- Title Mobile: 3, Title PC: 3
- Prologue Mobile: 11, Prologue PC: 11
- Prologue Additional Mobile: 13, Prologue Additional PC: 13

Main PC มี prefab เฉพาะ PC 28 รายการ เช่น `PlayerHudGroup_PC`, `InventoryGroup_PC`, `ChattingGroup_PC`, `MenuListGroup_PC`, `MinimapGroup_PC` และ `InteractionGroup_PC` ส่วน Mobile ใช้ prefab ชื่อเดียวกันโดยไม่มี `_PC`

## เหตุผลที่โหลดฉากใหม่

`UIManager.LinkedPrefabs` เก็บรายการ prefab และ cache component ตั้งแต่เริ่มฉาก นอกจากนี้ UI หลายชิ้นลงทะเบียน event และ static reference ของตนเอง การลบ/สร้างบาง GameObject แบบกลางฉากเสี่ยงเหลือ delegate และ cache ที่ชี้ไปยังวัตถุเก่า การโหลดฉากปัจจุบันใหม่เป็นวิธีที่แน่นอนกว่าและทำให้ Main, Title และ Prologue สร้าง UI ชุดใหม่ครบถ้วน

`ConfigInstance.LoadFromJson` เลือก `config_menu` หรือ `config_menu_pc` จาก `UsePCUI` เช่นกัน หากเปลี่ยนเฉพาะ prefab ค่า Settings จะยังเป็น schema เก่า เวอร์ชัน 1.1.0 จึงเรียก `LoadFromJson` และ `LoadConfigValue` ก่อนโหลดฉาก ทำให้ Harmony patch ของ UISizeOptions/KeybindSettings/Keybind2 ทำงานกับ schema ใหม่อีกครั้ง

## ความเข้ากันได้กับปลั๊กอินเดิม

- เพิ่ม soft dependency เพื่อให้ MobilePCUISwitch โหลดหลัง UISizeOptionsPlugin, KeybindSettingsPlugin และ Keybind2
- PC schema ใส่ `ui_platform` แบบ Dropdown (`pc`, `mobile`) ใน `screen` ตรงก่อน `ui_size`
- Mobile schema ไม่มี Dropdown prefab และไม่มี `screen` จึงใช้ Toggle เป็นรายการแรกของ `default/Basic`; `ui_size` ดั้งเดิมเป็น Toggle แบบ debug-only จึงเปิดให้แสดงเป็นรายการถัดไป
- UISizeOptionsPlugin 0.4.6 กำหนดตัวเลือก 5 ค่าแยกตาม widget: Dropdown ฝั่ง PC ใช้ `800/1024/1280/1600/1920` ส่วน Toggle ฝั่ง Mobile ใช้ `1400/1600/1800/2000/2200` และแยกการบันทึกเป็น `option:ui_size` กับ `option:ui_size_mobile`; ระหว่างโหลด Mobile จะเปลี่ยนค่าที่เกมอ่านจาก key ร่วมให้เป็นค่าฝั่ง Mobile ก่อนเรียก `UIManager.SetUISize` และใช้ชนิด setting จริงแทนสถานะ scene ชั่วคราวเมื่อตัดสินว่าจะบันทึกค่าฝั่งใด
- การเลือกโหมดจาก Settings ถูกดักก่อน `ConfigInstance.ChangeValue` จะบันทึกค่า จากนั้นใช้ MessageBox ดั้งเดิมขอคำยืนยัน; Cancel คืนค่าที่แสดงเดิม ส่วน Confirm จึงบันทึกและโหลดฉาก
- ปิด hotkey สำรองโดยค่าเริ่มต้น เพื่อไม่แย่งปุ่มจากระบบ keybind
- KeybindSettingsPlugin 0.2.5 อ่าน `EnumIconAttribute` โดยตรงแทน `IconMap` cache: PC Keybind และ Game Menu ใช้ `IconPC` โดย WorldMap บังคับ `icon_mainhud_map_pc`; Mobile Keybind ขอ sprite PC เช่นกันและวางบนพื้นหลังวงกลมแบบ PC โดย fallback เป็น sprite Mobile/กลางเมื่อ atlas PC ไม่ได้โหลด
- Mobile keybind tile มี collider และ click listener ของ NGUI จริง ใช้ popup จับปุ่มชุดเดียวกับ PC บันทึกผ่าน `ConfigInstance`, `PlayerPrefs` และอัปเดต keyboard map ที่กำลังใช้งาน พร้อมเลื่อน grid ลง/ขวาให้พ้นหัวหน้า Settings และแถบ category

## Patch ที่ปลั๊กอินใช้

- getter `UsePCUI` ของ `Platform` และ subclass ที่ประกาศ override
- getter `SupportPortrait` เพื่อให้ Mobile UI รองรับหน้าต่างแนวตั้ง และบังคับ PC ให้เป็น landscape policy
- `Platform_PC.GetScreenResolution` เฉพาะตอนใช้ Mobile UI เพื่อคำนวณ virtual resolution แบบ Mobile

ตั้งแต่เวอร์ชัน 1.1.4 ถึง 1.2.11 ค่า Mobile มีผลเฉพาะเมื่อ active scene ชื่อ `Main` เท่านั้น ฉาก Title, Prologue และช่วง startup ที่ยังไม่มี active sceneจะคืน `UsePCUI=true` เสมอ วิธีนี้เคยใช้หลีกเลี่ยงอาการค้างหน้าเข้าเกม แต่ยังไม่ได้พิสูจน์ว่าตัว Mobile Title เป็นสาเหตุโดยตรง

เวอร์ชัน 1.2.12 เปิด Mobile UI ตลอด lifecycle และ patch `UIPrefabMap.GetTitle` ให้เลือก Title Mobile โดยตรง จึงเป็นการทดสอบ prefab/controller จริง ไม่ใช่เพียงเปลี่ยน schema หรือ layout ภายใน Main พร้อมเพิ่ม diagnostic log ที่ setter ของ `TitleMenuGroup.CurState`, `RequestHttpUrl` และ `OnRequestSucceed` เพื่อระบุได้ว่าค้างก่อนหรือหลัง local Gateway, frontend connection หรือ Auth/Welcome

ผลทดสอบ cold startup ของ 1.2.12 พบ root cause ที่ `ConfigInstance.LoadConfigValue` ไม่ใช่ network: เมื่อโหลด `locale`, Harmony postfix ของ `LocalizeSystem.SetLocale` เรียก `InstallSettings()` ซึ่ง detach/insert `ui_platform` และ `ui_size` ใน `List<Setting>` ที่กำลังถูก enumerate ทำให้ `InvalidOperationException: Collection was modified` หลุดออกจาก `ConfigInstance.Initialize` และหยุด `GameManager.OnAwake`; local `/knock` จึงไม่มี `Server.Process()` ตอบ รุ่น 1.2.13 เพิ่ม loading guard และอนุญาต structural update หลัง enumeration จบเท่านั้น

Title Mobile มี `MediaPlayerCtrl` และ `_videoWidget` ครบเหมือน PC แต่ `_titleList` ใน prefab อ้าง `Movie/Mobile/title.mp4`, `sailing.mp4`, `warping.mp4` และ `balloon.mp4` ขณะที่ StreamingAssets ของ build นี้มีเฉพาะ `Movie/PC/*` รุ่น 1.2.14 จึง rewrite `TitleOptions.VideoName` จาก Mobile เป็น PC เฉพาะเมื่อไฟล์ปลายทางมีอยู่จริง ก่อน `ApplyEmigrationMode` เรียก `_videoPlayer.Load`

เวอร์ชัน 1.2.1 ย้ายการ reload schema ออกจาก `activeSceneChanged` ไปทำก่อนโหลดฉากและก่อน `UIManager.OnAwake` พร้อมตั้งค่า `_uiSize` โดยไม่ยิง resize notification ไปยัง UI ของฉากเดิม นอกจากนี้จะสร้าง `UIAnchorPolicy_PC` หรือ `UIAnchorPolicy_Mobile` ใหม่ตามฉากปลายทาง แทนการปล่อย static cache จากหน้า Title แบบ PC ให้ค้างเข้า Main แบบ Mobile ซึ่งเป็นสาเหตุหนึ่งของ offset และ UI ซ้อนเป็นครั้งคราว

เวอร์ชัน 1.2.2 ใช้ค่าขนาดที่แยกตามโหมดตั้งแต่ขั้นเตรียมฉาก: PC อ่าน `option:ui_size` และ Mobile อ่าน `option:ui_size_mobile` ค่าเริ่มต้น 1280 ทั้งคู่

เวอร์ชัน 1.2.3 เพิ่ม guard ก่อน `ConfigMainWidget.Awake`: ตรวจ schema จากชนิด `ui_size` (`DropdownSetting` = PC, `ToggleSetting` = Mobile) แล้ว reload ด้วย forced UI selector หากไม่ตรงกับชนิด widget การตรวจด้วยชนิด setting ปลอดภัยกว่าตรวจเพียงชื่อหมวด `screen` เพราะ Keybind/Screen plugin รุ่นเก่าอาจเพิ่มหมวดนั้นให้ Mobile ได้

เวอร์ชัน 1.2.4 แก้กรณีที่ prefab และ schema ถูกเลือกเป็น Mobile พร้อมกันทั้งคู่แม้ค่าที่บันทึกเป็น PC ซึ่ง guard ระดับ widget มองว่าไม่ mismatch โดย patch `UIPrefabMap.GetMain` ให้ใช้ `RequestedMode` โดยตรง และครอบ `ConfigInstance.LoadFromJson` ด้วย forced selector ตลอดการอ่าน JSON

เวอร์ชัน 1.2.5 แก้ root cause ที่พบจาก log: Title เตรียม Main settings สำเร็จแล้ว แต่ `UIManager.Awake` reload ซ้ำใน Main และ `ConfigInstance.ChangeLocale` พยายามเปิด confirmation ผ่าน `MessageBoxInfoWidget` ก่อน Popup UI พร้อม ทำให้เกิด NullReferenceException และ `LoadConfigValue` หยุดกลางทาง รุ่นนี้ส่งต่อ settings ที่เตรียมไว้ให้ Awake ใช้ครั้งเดียว และ bypass popup เฉพาะช่วง automatic scene preparation

เวอร์ชัน 1.2.6 แก้ Mobile UI Size ไม่แสดง: `config_menu.json` กำหนด `ui_size.DebugBuild=true` และ release client ข้าม setting นี้ก่อน plugin postfix ทำงาน จึงสร้าง `ToggleSetting` ใหม่เมื่อหา key ไม่พบ กำหนด options `800/1024/1280/1600/1920` และอ่านค่าจาก `option:ui_size_mobile`

เวอร์ชัน 1.2.7 เปลี่ยน options ของ Mobile เป็น `800/1024/1280/1420/1600` โดยกำหนดชื่อเป็น Very Large/Large/Normal/Small/Very Small ตามลำดับ และย้ายค่า Mobile เดิม `1920` เป็น `1600` อัตโนมัติ UISizeOptionsPlugin 0.4.2 patch `ToggleWidget.OnLocalize` เพิ่มเติมเพื่อเขียนชื่อจากค่าปัจจุบันโดยตรง ป้องกัน resource เดิมของเกมทำให้ `1024` แสดงชื่อ Very Small หรือไม่พบชื่อ Small

เวอร์ชัน 1.2.8 และ UISizeOptionsPlugin 0.4.3 ใช้ชุด Mobile `1420/1280/1024/800/600` เป็น Very Large/Large/Normal/Small/Very Small ตามลำดับ โดยตั้ง Normal เริ่มต้นเป็น `1024` และใช้ profile marker ใน PlayerPrefs เพื่อย้ายค่าจากชุดก่อนหน้าตามระดับเดิมเพียงครั้งเดียว แม้ค่าตัวเลขบางค่าจะซ้ำกันระหว่างสองชุด

เวอร์ชัน 1.2.9 และ UISizeOptionsPlugin 0.4.4 ใช้ชุด Mobile `1024/1280/1420/1600/1920` เป็น Very Large/Large/Normal/Small/Very Small ตามลำดับ ค่าเริ่มต้นของปลั๊กอินเป็น Normal `1420`; profile marker รุ่นใหม่แยกการย้ายจากทั้งชุด 1.2.8 และชุดก่อนมี marker เพื่อรักษาระดับที่ผู้ใช้เคยเลือก

เวอร์ชัน 1.2.10 และ UISizeOptionsPlugin 0.4.5 ใช้ชุด Mobile `1400/1600/1800/2000/2200` เป็น Very Large/Large/Normal/Small/Very Small ตามลำดับ ค่าเริ่มต้นของปลั๊กอินเป็น Normal `1800`; profile marker รุ่นที่สามรองรับการย้ายตามระดับจากทั้งชุด 1.2.8, 1.2.9 และชุดก่อนมี marker

UISizeOptionsPlugin 0.4.6 patch `ToggleWidget.MoveIndex` เพิ่มเติมและกลับเครื่องหมาย offset เฉพาะเมื่อ `Parent.Key == "ui_size"` ทำให้ปุ่มซ้าย/ขวาของ Mobile UI Size ตรงกับขนาดภาพ ขณะที่ `ui_platform` และ Toggle อื่นยังใช้ทิศทางดั้งเดิม

เวอร์ชัน 1.2.11 เรียกการล้าง `Setting.HideOnOffline` ก่อนติดตั้ง UI Mode/UI Size ทุกครั้งที่ `ConfigInstance` โหลด schema จึงแสดงรายการ Auto-Translate และ Chat ในทุก cluster mode และทั้งสอง UI โดยไม่ override เงื่อนไขการซ่อนชนิดอื่น

KeybindSettingsPlugin 0.2.5 patch `MenuWidget.Set(MenuType)` เพื่อเขียน PC icon กลับจาก enum หลังเมธอดดั้งเดิมใช้ `IconMap` cache แก้ WorldMap ว่างเมื่อ cache เคยถูกสร้างใน Mobile; Mobile Keybind จะ clone `MenuWidget_PC` หากพบ template มิฉะนั้นสร้างพื้นหลัง `bg_maincircle_03_pc` (fallback `bg_circle_big`) และใช้ PC icon พร้อม fallback

ปลั๊กอินตั้งใจไม่ patch `UsePCRenderer`, `UsePCCoin`, `Store`, `AssetBundlePlatform` หรือระบบ input เพื่อไม่ให้การสลับหน้าตากระทบ renderer, economy และการเชื่อมต่อของ PC build
