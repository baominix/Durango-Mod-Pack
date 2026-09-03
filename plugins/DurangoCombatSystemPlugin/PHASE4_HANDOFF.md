# Phase 4 handoff — before Saurus AI

สถานะ build: `DurangoCombatSystemPlugin 0.3.9`

Hotfix ภายในฐาน `0.3.9`:

- multi-hit root-motion sampling อ่าน animation/path/เป้าหมายหนึ่งครั้งต่อ action
  ต่อเฟรม เพื่อลดอาการกระตุกของ `twohand_lance_dash` ที่มี 4 hits
- ทุก transition ระหว่าง Normal/Skill/Defense ใช้ `prohibited_time` จาก
  `player_battle_actions.json` ครบทั้ง matrix (`0.3s` สำหรับ lance normal ไป
  Skill/Defense) แทนการใช้ `action_length` เหมารวม และยกเลิก pending hit/เส้น
  ของ action เดิมเมื่อถูก action ใหม่ตัด

ผล field test: **ผ่านโดยผู้ใช้เมื่อ 2026-08-25**

Phase 4 ปิดแล้ว และเริ่ม Phase 5 โดยเพิ่ม animal state machine แยกในโฟลเดอร์
`SaurusAI` เอกสารนี้เก็บไว้เป็น regression gate ของ player combat

## สิ่งที่ implementation เสร็จแล้ว

- offline protocol bridge สำหรับ `GetActions` และ `UseBattleAction`
- action list จากอาวุธ/สกิล พร้อม retry เมื่อ equipment ยังโหลดไม่ครบ
- action validation, packet deduplication, cooldown, action lock และ stamina cost
- player attack snapshot และ multi-hit scheduler ที่ refresh center ได้จนถึง hit time
- Melee/Ranged เป็น selected-target
- CircularArea/RectangularArea เป็น multi-target geometry
- authoritative area telegraph ใช้ snapshot เดียวกับ hit query
- player root motion ถูกคำนวณตาม `attack_time`, clamp กับเป้าหมาย และชดเชยจากฐานจริงเมื่อ collision ทำให้เคลื่อนที่ไปไม่ครบ
- Miss/Dodge/Hit แยกผลกัน
- world/session generation guard และ cleanup handler
- Combat UI initialization สำหรับ Offline
- developer commands/toggles แยกไป `DeveloperModePlugin`
- `/hp` และ `/sp` เปลี่ยน `PlayerContext`, แจ้ง persistence owner และ resync ตอน
  action เริ่ม/หลัง `BattleEnded`; ค่า SP ที่สูงกว่า Max ถูกหักตาม cost โดยไม่ clamp
  กลับ Max

## ส่วนที่ยังเป็น reconstruction

- accuracy/dodge/damage/defense/critical เต็มรูปแบบรอ Phase 7
- animal action, AI, animation state, attack root motion และ reaction ยังไม่เริ่มใน plugin ใหม่
- lifecycle test แบบย้าย map/Return to Title หลายรอบยังเป็น gate ของ Phase 8
- co-op และ authoritative network server อยู่นอกขอบเขตรุ่นแรก

## Field-test gate ก่อนเริ่ม Phase 5

1. ใช้ `/dev on`, `/hp <amount>` และ `/sp <amount>` แล้วเริ่ม action/ปล่อยให้ battle
   จบ ค่า HUD ต้องไม่คืนเป็น snapshot เก่า; SP ลดเฉพาะ stamina cost ของ action
2. Melee และ Bow/Crossbow ต้องทำ damage เฉพาะสัตว์ที่เลือก แม้มีสัตว์อีกตัวยืน
   ใกล้กัน
3. CircularArea/RectangularArea ต้องวาดเส้น และสัตว์ที่โดนต้องตรงกับพื้นที่นั้น
4. ระหว่างท่า เส้นและ damage area ต้องเลื่อนด้วยกันเฉพาะเมื่อฐานผู้เล่นเคลื่อนจริงหรือ
   collision ทำให้ root motion ไปไม่ครบ; yaw ของ geometry ยังคงล็อกตอนเริ่มท่า
5. Quick Shot ต้องออกครบหลาย hit แต่ลง selected target เดิมเพียงตัวเดียว
6. ถอด/สวมอาวุธและเข้าแผนที่ใหม่ action bar ต้องไม่ค้างเป็น bare-hand
7. `/combatspawn 2027`, `2037`, `2039` ต้องสร้าง dummy สำหรับทดสอบได้ โดย command
   ทำงานเฉพาะเมื่อ Developer mode เปิด
8. `twohand_sweeping`: hit 2 ซ้อน hit 1 ตาม root motion ที่ผ่าน collision,
   `twohand_lance_dash`: เมื่อไม่ชนต้องเรียงพื้นที่ตาม dash แต่เมื่อชนสัตว์ geometry
   ที่เหลือต้องย้ายกลับตามฐานจริง, และ `onehand_flurry` ต้องไม่ regression
9. `/dev attackalert off` และ `/dev off` ต้องหยุด player area telegraph ที่กำลัง
   แสดงอยู่; เปิดกลับระหว่าง action ต้องแสดงเฉพาะ hit ที่ยังไม่ถึงเวลา
10. `twohand_lance_dash` ต้องไม่ทำให้เฟรมค้างระหว่างติดตาม 4 hit areas และเมื่อกด
    skill หลัง lance normal attack พ้น `0.3s` skill ต้องเกิด damage โดยไม่ต้องรอ
    normal animation จบ; pending normal hit ที่ถูกตัดต้องไม่เกิด damage ซ้ำ
11. transition Normal/Skill/Defense ทุกทิศทางต้องยึดค่า `prohibited_time` ของ
    action ปัจจุบัน; ตัวอย่าง Charge → Normal ต้องรอ `3.0s` และ Sweeping →
    Normal ต้องรอ `4.7s` ตามข้อมูลเกม
12. RectangularArea ต้องตีความ `rect_half_size.x` เป็นแกน forward และ `.y`
    เป็นแกน right เหมือน renderer; Sunder `[350,100]` ต้องโดนสัตว์ตลอดแนวยาว
    ภายในเส้น และ Charge/Stab ต้องไม่มีแกนสลับ

Phase 4 ถือว่าผ่านภาคสนามแล้ว รายการนี้ยังใช้เป็น regression checklist ทุกครั้งที่
Saurus AI เพิ่ม message, scheduler หรือ damage flow ใหม่

## Normal attack auto-approach regression fix

หลัง R3 พบว่า client และ offline runtime เคยใช้ target radius คนละแหล่ง ทำให้
auto-walk โดยเฉพาะ Zebraceratops หยุดตาม YAML `bound_radius` แต่ server ตรวจด้วย
collider radius ที่เล็กกว่า การแก้บนฐาน `0.3.9` เปลี่ยน
`AnimalCombatTarget.Radius` ให้ตรงกับสูตร `UsingAction` ของเกมเดิม ดูรายละเอียดใน
[PLAYER_NORMAL_ATTACK_RANGE_AUDIT.md](PLAYER_NORMAL_ATTACK_RANGE_AUDIT.md)
