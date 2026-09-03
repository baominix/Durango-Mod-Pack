# สถาปัตยกรรมระบบต่อสู้

## ภาพรวมของเกมเดิม

ตัว client ถูกออกแบบให้รับผลการต่อสู้จาก server เป็นหลัก:

1. ผู้เล่นเลือก action
2. `CombatSystem`/`UsingAction` ส่ง `UseBattleAction`
3. server ตรวจ action, ระยะ, cooldown, stamina, hit geometry และคำนวณผล
4. server ส่ง `BattleBegun`, `AttackAlerted`, `Damaged`, `SurvivalUpdated`, `Actions` และ `BattleEnded`
5. client เล่น animation, root motion, เส้นเตือน, damage reaction และ UI ตาม message

offline server ที่ bundle มาไม่มีขั้นตอนที่ 3 สำหรับ combat ครบถ้วน ปลั๊กอินใหม่จึงต้องเติมเฉพาะ authoritative simulation ที่หายไปและส่งผลกลับผ่าน contract เดิมให้มากที่สุด

## กฎสำคัญ

### เจ้าของ state หนึ่งราย

- `CombatRuntime` เป็นเจ้าของ battle session, cooldown และ hit resolution
- `AnimalController` เป็นเจ้าของ state/target ของสัตว์
- `CombatSystem` เดิมของเกมเป็น presentation/client state ไม่ใช่ฐานข้อมูล combat ซ้ำอีกชุด
- การลด HP, ใช้ stamina หรือเริ่ม cooldown ต้องเกิดเพียงครั้งเดียว

### แยก simulation ออกจากภาพ

- Simulation ใช้พิกัดเชิงตรรกะและเวลา server
- Animation/root motion ใช้แสดงการเคลื่อนที่ แต่ต้องรายงานตำแหน่งกลับ runtime อย่างมีจุด sync
- `AttackAlerted` และ hit query ใช้ snapshot object ชุดเดียวกัน
- player geometry ปรับตามฐานจริงจนถึง hit time เมื่อ collision ทำให้ root motion ไปไม่ครบ; animal geometry จะกำหนดกฎ lock แยกใน Phase 5
- การหันหลัง lock ท่าต้องไม่เปลี่ยน yaw ของ hit geometry ที่ประกาศแล้ว

### โหลดข้อมูลแทน hardcode

- stat/cooldown จาก `animal.json`
- motion/attack geometry จาก `AnimalFrameworkResource`
- action ผู้เล่นจาก `player_battle_actions.json`
- directional damage จาก framework
- ค่าที่หาย เช่น AI factor เก็บใน profile config พร้อม provenance ว่า reconstructed

## โมดูลปลั๊กอินใหม่

```text
DurangoCombatSystemPlugin
├─ Bootstrap
│  ├─ Plugin entry point
│  ├─ Harmony registration
│  └─ Lifecycle / world-session reset
├─ Data
│  ├─ OriginalGameDataAdapter
│  ├─ AnimalProfileRepository
│  └─ Validation / provenance
├─ Protocol
│  ├─ OfflineCombatBridge
│  ├─ Incoming UseBattleAction
│  └─ Outgoing original Messages
├─ Runtime
│  ├─ CombatRuntime
│  ├─ BattleSession
│  ├─ Scheduler
│  └─ Entity combat state
├─ PlayerCombat
│  ├─ Action validator
│  ├─ Stamina / cooldown
│  └─ Player hit resolver
├─ SaurusAI
│  ├─ AnimalController
│  ├─ Shared states
│  ├─ CombatContext / short-term event memory
│  ├─ AttackIntentResolver / ActionEligibility
│  ├─ Species profiles / immutable ActionPlan
│  └─ Threat / retreat / return-home
├─ Geometry
│  ├─ AttackSnapshot
│  ├─ Circle / arc / rectangle queries
│  └─ Direction / body-part resolver
├─ Movement
│  ├─ Facing controller
│  ├─ Root-motion synchronization
│  └─ Knock-back / blow displacement
├─ Damage
│  ├─ Hit / Miss / Dodge resolver
│  ├─ Damage calculator
│  └─ Reaction selector
├─ Presentation
│  ├─ AttackAlerted
│  ├─ Damage reaction
│  └─ UI message integration
└─ Diagnostics
   ├─ Validation report
   └─ Central plugin logging gate
```

รายละเอียดการย้ายจาก range/weight selector ไปเป็น Context → Intent → Action,
alignment policy และ root rotation contract อยู่ใน
[SAURUS_COMBAT_REDESIGN_PLAN.md](SAURUS_COMBAT_REDESIGN_PLAN.md)

## CombatRuntime

ควรมี runtime หนึ่งชุดต่อ world session ไม่ผูกกับ scene object ที่ถูกทำลายระหว่างเปลี่ยน UI/แผนที่

หน้าที่:

- ลงทะเบียน entity ที่พร้อมต่อสู้
- เปิด/ปิด battle session
- ใช้ monotonic/server-compatible clock เดียว
- schedule hit ตาม frame/time ของ action
- ป้องกัน action ซ้ำจาก message retry
- ยกเลิก event เมื่อ entity ตาย, despawn, เปลี่ยนโลก หรือ session generation เปลี่ยน

ทุก scheduled event ต้องมีอย่างน้อย:

- world generation id
- actor entity id
- action instance id
- expected state

สิ่งนี้ป้องกัน hit เก่าทำงานหลังเปลี่ยนแผนที่หรือ Return to Title

### ส่วนที่ implement แล้วใน 0.2.0

- `OfflineCombatBridge` ครอบครองเฉพาะ message type `GetActions (314)` และ `UseBattleAction (3440)` เมื่อไม่มี owner เดิม
- `OfflineCombatSession` ผูกกับ player/world หนึ่ง generation และ unsubscribe เมื่อ `Player.Closed`
- `AvailableActionProvider` ใช้ `tag_allow_actions.json` ที่เกมโหลดไว้ และ intersect `skill_actions` กับ reward ของ skill level ที่เรียนแล้ว
- `ActionStatus` ใช้ stamina/cooldown จาก `player_battle_actions.json` ผ่าน `Yaml.PlayerAction`
- accepted action มี internal instance id, packet sequence, generation, accepted time และ target id
- phase นี้หยุดก่อน damage execution; `LastAcceptedAction` เป็น boundary สำหรับ geometry/scheduler ใน phase ถัดไป

### ส่วนที่ implement แล้วใน 0.3.0–0.3.9

- `AttackSnapshot` เรียก `PlayerActionAttackInfo.MakeAlerted()` ตอน commit และ refresh center จากฐานผู้เล่นจริงจนถึง hit time โดยล็อก yaw/time/shape เดิม
- scheduler อยู่ใน `CombatRuntime.Process()` ซึ่งถูก tick จาก plugin และไม่ patch `Offline.Player.Process`
- scheduled hit ทุกอันมี world generation, action instance id และ hit index
- geometry query รองรับ circle/arc/oriented rectangle/ranged circle และใช้ target position ณ hit time
- `UsingActionAlert` ของ local player ถูกแทนด้วย visualizer ที่ runtime ขยับด้วย snapshot เดียวกับ hit query เพื่อไม่ให้มีเส้น developer ซ้ำ
- stamina, battle lifecycle, damage result และ life gauge ส่งผ่าน message เดิมของเกม
- damage formula ในรุ่นนี้เป็น reconstructed strategy ชั่วคราวและแยก provenance ไว้ใน `PHASE4_PLAYER_GEOMETRY.md`; สูตรเต็มยังอยู่ใน Phase 7

### Offline UI compatibility ใน 0.3.1

- `CombatGroup.Start()` ได้ original online initialization path ชั่วคราวใน main-scene Offline เพื่อไม่ให้ combat controls ถูกปิดทั้งกลุ่ม
- cluster mode ถูกคืนค่าเดิมใน Harmony finalizer แม้ original method เกิด exception
- Attack interaction เปิด battle view และเลือก target ก่อน โดยไม่ใช้ action slot 1 อัตโนมัติ

## Saurus AI

ชื่อ Saurus AI ใช้เป็นระบบสัตว์ต่อสู้ของปลั๊กอินใหม่ ไม่ใช่ class ที่อ้างว่าอยู่ในเกมเดิม

Shared state ขั้นต้น:

```text
Spawn/Initialize
  -> Idle/Stand <-> Roam
  -> Alert/AcquireTarget
  -> Approach
  -> FaceAndTelegraph
  -> ExecuteAttack
  -> Recover/Stand (รอ cooldown)
  -> Approach หรือเลือกท่าถัดไป
  -> LowHealthRetreat (มีโอกาสเมื่อ HP < 20%)
  -> Reengage
  -> ReturnHome
  -> Dead
```

Reaction เช่น Evade, DirectionalDamage, Blow, KnockDown เป็น interrupt ที่มี priority ชัดเจน ไม่ควรสร้าง state ซ้อนกันหลายชุด

หลักเลือกสัตว์ที่ share framework:

- Framework กำหนดชุด animation และ attack primitive ที่ใช้ได้
- entity id/root-motion set/model กำหนดชนิดจริง
- species profile กำหนดพฤติกรรม เช่น aggression, preferred range, attack weighting, retreat chance
- ห้ามใช้ชื่อ animation เพียงอย่างเดียวแยก species เพราะสัตว์หลายชนิด share framework

## Action lifecycle

1. Validate actor/session/state
2. Validate action availability, stamina, cooldown, target และระยะเริ่มต้น
3. Capture `AttackSnapshot`: origin, yaw, target id/position, geometry, timestamps
4. ส่ง `BattleBegun` หากยังไม่อยู่ใน battle
5. ส่ง `AttackAlerted` จาก snapshot
6. เล่น motion และ root motion
7. เมื่อถึง hit time ทำ geometry query จาก snapshot/กฎ `use_target_origin`
8. resolve Hit/Miss/Dodge ก่อนคำนวณ damage
9. ส่ง `Damaged` และ `SurvivalUpdated`
10. เข้า recovery/stand จนครบ action lock และ cooldown
11. ส่ง action/cooldown update
12. จบ battle เมื่อ timeout, ระยะไกลเกิน, ตาย หรือออกโลก

## Hit, Miss และ Dodge

- Out of geometry = ไม่โดนเป้าหมาย
- Miss = ผ่าน geometry แต่ accuracy check ล้มเหลว
- Dodge = เป้าหมายผ่าน dodge check
- Evade animation เป็น presentation ของ Dodge/Evaded เมื่อ state อนุญาต
- ถ้าสัตว์กำลังเล่น attack แบบห้าม interrupt ให้ผล Dodge ยังเกิด แต่ไม่บังคับเล่น Evade animation
- damage ต้องเป็นศูนย์สำหรับ Miss/Dodge และส่ง `DamageResult` ให้ตรงความหมาย

ลำดับสูตรจริงยังต้องยืนยันเพิ่ม จึงควรทำ calculator แบบ strategy และ log input/output เมื่อเปิด diagnostics

## ทิศทาง damage และ reaction

คำนวณทิศของผู้โจมตีใน local space ของเหยื่อ ณ hit time แล้ว map:

- Front -> `_Damage_S`
- Back -> `_Damage_N`
- Left -> `_Damage_E`
- Right -> `_Damage_W`

ห้ามใช้ yaw ของเส้นเตือนหรือ yaw หลัง animation หมุนผ่านไปแล้วเป็นตัวตัดสินทิศ

Blow/KnockBack ต้องเคลื่อน **logical transform/base** ให้สอดคล้องกับ animation หากขยับเฉพาะ mesh เมื่อ animation จบจะ snap กลับตำแหน่งเดิม

## Root motion

`RootMotionMovable` แสดงว่าตัวเกมแยก owner transform, mesh transform, Bip001 และ RootMotionTransform

กติกาสำหรับปลั๊กอิน:

- เก็บ pose/yaw เริ่มท่าก่อนเล่น motion
- ระหว่างท่า root motion ขยับ actor base ผ่าน adapter เพียงรายเดียว
- ไม่ให้ AI steering และ root motion เขียนตำแหน่งพร้อมกัน
- เมื่อท่าจบ sync logical position แล้ว reset mesh offset
- การกระโดด/พุ่งต้องตรวจ collision/path และ clamp displacement
- damage reaction ที่มี displacement ใช้หลักเดียวกัน

## Facing และ telegraph

แยกเป็น 3 ช่วง:

1. Tracking: หมุนหาเป้าหมายด้วย angular speed ของ profile
2. Commit: ล็อก yaw เมื่อสร้าง `AttackSnapshot`
3. Execute: animation หมุนได้เฉพาะ root motion ของท่า แต่ geometry ไม่คำนวณซ้ำ

วิธีนี้แก้กรณีสัตว์หันเร็ว ทำให้ตัวจริงกับเส้นเตือนไปคนละทาง และแก้เส้นวิ่งตาม root motion

## Lifecycle

- เปลี่ยน map: ยกเลิก entity/action ของ world เก่า แต่ไม่ทำลาย service ระดับ process
- Return to Title: flush state ที่จำเป็น, ยกเลิก session และถอด reference ของ player/world
- เข้า character ใหม่: สร้าง generation ใหม่ ห้าม reuse entity state เดิม
- Exit/process kill: ปล่อย BepInEx/game shutdown จัดการ service; persistence ที่สำคัญต้องบันทึกก่อนจุดนี้ ไม่พึ่ง shutdown อย่างเดียว

## Logging

ใช้ central logging gate ที่มีอยู่ในชุด mod:

```ini
[General]
Log_Enabled = false

[Plugins]
DurangoCombatSystemPlugin = false
```

ข้อความ BepInEx เช่น `Loading [plugin name]` อยู่นอกขอบเขตควบคุม ส่วน log ภายในปลั๊กอินต้องผ่าน wrapper เดียว ห้ามเรียก logger โดยตรงกระจายทุก class
