# ผลตรวจปลั๊กอินที่เกี่ยวข้อง

ตรวจจาก `tools\durango-mod-original` เมื่อ 2026-08-24 โดยไม่นับ source ใน `.disable`, `_combat_system_backup`, `_backups` และ `test` เป็นปลั๊กอินที่ทำงานอยู่

## ขอบเขต ownership

| ปลั๊กอิน | สิ่งที่เกี่ยวข้อง | ข้อตกลงกับ CombatSystem ใหม่ |
|---|---|---|
| GatheringPlugin | patch `Offline.Player` constructor/`Process`/`HandleTouchMsg`, natural entity และ inventory update | Combat ห้ามจัดการ natural collection หรือ inventory reward; constructor postfix อยู่ร่วมกันได้ แต่ message type ต้องไม่ซ้ำ |
| AnimalHandlingPlugin | pet/grazing/taming, patch constructor และอ่าน `AnimalManager` | Saurus AI รับเฉพาะ wild animal entity type/profile ที่ลงทะเบียน ห้าม attach กับ pet/tamed animal |
| NPCFriendListPlugin | ใช้ `AnimalBehavior` เป็นโมเดล NPC บางตัว | ห้ามตัดสินว่าเป็นศัตรูจากการมี `AnimalBehavior` อย่างเดียว ต้องตรวจ `AnimalManager`, entity type และ combat profile |
| SkillSystemPlugin | skill state, action rewards, equipment refresh และ save | เป็นเจ้าของ skill progression; Combat อ่าน action/stat แต่ไม่แก้ skill persistence |
| PlayerProgressionPlugin | player stat/progression และ patch `PlayerContext.Save`/`Server.EndServer` | เป็นเจ้าของ persistence ของ progression; Combat ใช้ค่าที่คำนวณแล้วและไม่บันทึกไฟล์ player เอง |
| ChatCommandPlugin | อ่าน `CombatSystem` และ battle actions สำหรับคำสั่ง stat | read-only consumer; ไม่จำเป็นต้องมี dependency |
| OfflineSurvivalPlugin | survival state และ interaction บางชนิด | Combat ส่ง `SurvivalUpdated` เฉพาะผล damage/stamina ที่ตนเป็นเจ้าของ ไม่ patch survival loop |
| IslandMapRestorationPlugin | terrain alias/region template | ใช้บริบท map เท่านั้น ไม่แก้ terrain หรือ biome |
| HarborSailingMapPlugin | world constructor, harbor spawn, map travel และ `HandleTouchMsg` | เมื่อ world เปลี่ยน Combat ต้อง reset generation; ห้ามเป็นเจ้าของ server lifetime/route |
| TamedIslandRestorationPlugin | offline player constructor, welcome, tamed estate/world state | ไม่ spawn wild combat animal ใน tamed home จนกว่าจะกำหนดกฎชัดเจน |
| SelectCharacterPlugin | เปลี่ยน/ลบ/เลือก player | Combat state ต้องอิง world/player generation และปล่อย reference เมื่อกลับ title |
| CustomTerrainLoaderPlugin / MapEditorPlugin | เปลี่ยน terrain/world context | Combat ต้องยกเลิก scheduled event ของ world เดิมและไม่ spawn สัตว์ใน editor world โดยอัตโนมัติ |
| LogControlPlugin | กรอง disk log ตาม GUID prefix/source name | ใช้ GUID `com.baominix.durango.original.combatsystem` และ ManualLogSource ปกติ |

## Harmony target ที่มีความเสี่ยงร่วมกัน

### `Durango.Offline.Player` constructor

ถูกใช้โดย Gathering, AnimalHandling, SkillSystem, PlayerProgression, Harbor และ TamedIsland เพื่อ register message handlers/state ดังนั้น Combat ใช้ **Postfix registration เท่านั้น** และต้องไม่ skip original/ล้าง handler เดิม

### `Durango.Offline.Player.Process`

Gathering ใช้ส่ง queued outbound message ฝั่ง original client หาก Combat ต้องมี scheduler ให้แยก runtime tick ออกจาก queue ของ Gathering และไม่ Prefix ที่คืน `false`

Phase 4 ใช้ `BaseUnityPlugin.Update()` เรียก `CombatRuntime.Process()` จึงไม่ patch `Durango.Offline.Player.Process`

### `UsingActionAlert` / `AreaOfEffectVisualizer`

ไม่พบปลั๊กอิน active อื่น patch สองจุดนี้ใน source audit ล่าสุด Phase 4 skip เฉพาะ local `UsingActionAlert.Set()` เมื่อ combat plugin เปิด และเปลี่ยนสี `AttackAlerted` ของ local player เป็นชนิด Player ส่วน alert ของสัตว์/ระบบอื่นยังผ่าน original method

### `HandleTouchMsg`

Gathering และ Harbor มี ownership ของ natural/dock interactions Combat ไม่ควรดัก method นี้ทั้งก้อน การเลือกเป้าหมายสัตว์ควร patch จุด client targeting ที่แคบกว่า

### `World` constructor / terrain load

Harbor, CustomTerrainLoader และ MapEditor ใช้อยู่แล้ว Combat ใช้ world lifecycle hook แบบสังเกตการณ์เท่านั้น ห้ามแก้ `WorldContext`, terrain id หรือ artifacts

### Save และ shutdown

SkillSystem/PlayerProgression patch `PlayerContext.Save` และ `Server.EndServer` Combat ไม่ควรบันทึก HP/inventory ผ่านไฟล์อีกชุด เพราะเสี่ยง overwrite ข้อมูลล่าสุด

Phase 4 เปลี่ยน stamina ใน context เดิมแล้วแจ้ง `Player.OnContextChanged()` เพื่อให้ `GameServer`/เจ้าของ persistence เดิมเป็นผู้เรียก save; Combat ไม่เรียก `PlayerContext.Save()` โดยตรง

## SocialSystem command patches (`0.3.2` → ย้ายออกใน `0.3.4`)

ตั้งแต่ `0.3.4` Combat plugin ไม่มี command patch ของ `SocialSystem.Say` แล้ว
คำสั่ง `/hp`, `/sp`, `/combatspawn`, `/combatwave`, `/combatstatus`,
`/combatcontext`, `/combatintent`, `/combathelp` ถูกย้ายไป `DeveloperModePlugin` เพื่อไม่ให้ developer tools ปนกับ
runtime ต่อสู้ คำสั่งที่
ไม่รู้จักคืน `true` ให้ Harmony/original flow ต่อ จึงไม่ครอบครอง command ของปลั๊กอินอื่น

## Dependency ที่กำหนด

- Soft dependency: LogControlPlugin
- ไม่มี hard dependency ต่อ Gathering/Skill/Progression/Harbor
- ตรวจ capability/runtime state แทนการเรียก private API ของปลั๊กอินอื่น

## กฎจำแนก wild animal

สัตว์จะถูก Saurus AI จัดการเมื่อผ่านทุกข้อ:

1. entity type มี profile ใน `CombatDataRegistry`
2. instance อยู่ใน `AnimalManager` ของ world ปัจจุบัน
3. ไม่ใช่ pet/grazing/tamed entity
4. ไม่ใช่โมเดล NPC ที่เพียงใช้ `AnimalBehavior`
5. world generation ตรงกับ runtime generation ปัจจุบัน

## ข้อสรุปสำหรับ Phase 1–2

Phase นี้โหลดและตรวจ reference data เท่านั้น ยังไม่ติดตั้ง gameplay Harmony patch จึงไม่เปลี่ยน combat, spawn, inventory, save หรือ world lifecycle การเพิ่ม protocol bridge ใน Phase ถัดไปต้องเริ่มจาก constructor postfix แบบไม่เป็นเจ้าของ handler ของปลั๊กอินอื่น
