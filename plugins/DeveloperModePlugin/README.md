# DeveloperModePlugin

เครื่องมือ developer แยกจากระบบ gameplay ของปลั๊กอินอื่น ค่าเริ่มต้นปิดทั้งหมด

## Commands

- `/dev on|off|toggle|status|reset`
- `/dev attackalert on|off|toggle|status`
- `/dev animalbubble on|off|toggle|status`
- `/hp <amount>` และ `/sp <amount>`
- `/combatspawn [2027|2037|2039|2001] [level]`
- `/combatwave [type] [level] [count] [spacing]`
- `/combatstatus`
- `/combatcontext [nearest|all|entityId]`
- `/combatintent [nearest|all|entityId]`
- `/combathelp`

`/dev` ใช้ได้ตลอดเพื่อเปิด developer mode ส่วนคำสั่งทดสอบอื่นจะถูกปฏิเสธเมื่อ
developer mode ปิดอยู่

## Config

```ini
[General]
Enabled = false

[DeveloperToggles]
AttackAlert = false
AnimalBubble = false
```

`AttackAlert` ควบคุม `CombatSystem.AttackAlertEnabled` ของเกมเดิมเท่านั้น
เมื่อปิด developer mode หรือ unload ปลั๊กอิน จะคืน runtime toggle เป็นค่าก่อนโหลด
ปลั๊กอิน

`AnimalBubble` แสดง state, เหตุผลที่เข้า state, motion ที่ runtime ขอเล่น,
motion ที่กำลังเล่นจริง, yaw error และค่าความยาวที่ `CrossFade` คืนมาเหนือหัวสัตว์
Saurus AI การแสดงผลอยู่ใน Combat plugin แต่ config และ command อยู่ใน Developer Mode
เพื่อไม่ให้เครื่องมือ debug ปนกับ gameplay

คำสั่ง combat เรียก runtime ของ `DurangoCombatSystemPlugin` ผ่าน reflection เพื่อให้
ไม่มี command patch หรือ developer UI อยู่ใน Combat plugin

`/combatcontext` อ่าน snapshot ก่อนตัดสินใจและ short-term event memory ของ Saurus AI
โดยไม่แก้ selector/movement ใช้ `nearest` เป็นค่าเริ่มต้น, `all` สำหรับสรุปทุกตัว หรือ
ระบุ entity id เพื่ออ่านตัวที่ต้องการ

`/combatintent` อ่าน shadow decision ปัจจุบัน, เหตุผลที่แต่ละ action eligible/rejected
และผลเทียบกับ action ล่าสุดที่ selector `0.3.9` เลือกจริง Resolver ไม่ execute action
และไม่เปลี่ยน random stream ของ gameplay

`/hp` และ `/sp` เปลี่ยนค่า authoritative `PlayerContext`; ตั้งแต่
`DurangoCombatSystemPlugin 0.3.5` ค่าเดียวกันจะถูกส่งยืนยันอีกครั้งหลัง
`BattleEnded` เพื่อไม่ให้ HUD คืนไปใช้ snapshot เก่า
