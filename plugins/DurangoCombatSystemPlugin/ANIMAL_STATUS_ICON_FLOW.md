# Animal status icon / emoji flow

เอกสารนี้ระบุทางเดินจริงของไอคอนสี่เหลี่ยมเหนือหัวสัตว์ในเกมเดิม ไอคอนนี้ไม่ใช่
chat emoticon และไม่ควรสร้าง GameObject UI ใหม่ใน Saurus AI

## ทางเดินข้อมูลเดิม

1. ฝั่ง server ส่ง `CombatInteraction` และใส่เลขสถานะไว้ใน
   `details["status"]`
2. `ObjectManager` แปลงเลขนั้นเป็น `Shared.Animal.AnimalStatus` และตั้งค่า
   `AnimalBehavior.Status` ตามเวลาของ packet
3. `Durango.UI.AnimalFloatingGroup` ติดตามสัตว์จาก `AnimalManager.AnimalAppeared`
   และ pet จาก `PetManager.PetAppeared`
4. ใน `LateUpdate` กลุ่ม UI ใช้ค่าตัวเลขของ `animal.Status` เลือก sprite จาก
   `_animalStatusIconList`
5. `AnimalFloatingControl.SetStatusIcon()` แสดงหรือซ่อนไอคอน แล้วผูกตำแหน่งกับ
   `BodyPart.Head` โดย prefab กำหนด offset แกน Y เท่ากับ `50`

ไฟล์ต้นฉบับที่ยืนยัน flow:

- `Assets/Scripts/Assembly-CSharp/ObjectManager.cs`
- `Assets/Scripts/Assembly-CSharp/Shared/Animal/AnimalStatus.cs`
- `Assets/Scripts/Assembly-CSharp/Durango/UI/AnimalFloatingGroup.cs`
- `Assets/Scripts/Assembly-CSharp/Durango/UI/AnimalFloatingControl.cs`
- `Assets/GameObject/AnimalFloatingGroup.prefab`

## ตารางสถานะกับ sprite ใน prefab

ตำแหน่งใน array ตรงกับเลข enum ไม่ใช่ลำดับประกาศของ enum ดังนั้นช่องว่าง
`1`, `2`, `3`, `7`, `23` ยังมี sprite ได้แม้ชื่อ enum ไม่ปรากฏใน DLL ที่ export

| ค่า | สถานะที่มีชื่อ | sprite |
|---:|---|---|
| 0 | Acting | ซ่อน |
| 1 | ช่องว่าง | `animal_emoticon_angry` |
| 2 | ช่องว่าง | `animal_emoticon_attack` |
| 3 | ช่องว่าง | `animal_emoticon_attack` |
| 4 | AvoidCollide | ซ่อน |
| 5 | AvoidHot | `animal_emoticon_avoidhot` |
| 6 | Blow | `emoticon_003` |
| 7 | ช่องว่าง | `animal_emoticon_confront` |
| 8 | Dead | `emoticon_3` |
| 9 | Eat | `animal_emoticon_eat` |
| 10 | KnockDown | `emoticon_3` |
| 11 | FoundBody | `emoticon_2` |
| 12 | Battle | ซ่อน |
| 13 | GiveUp | `animal_emoticon_giveup` |
| 14 | Groggy | `animal_emoticon_groggy` |
| 15 | Help | `animal_emoticon_help` |
| 16 | Hungry | `animal_emoticon_hungry` |
| 17 | Move | ซ่อน |
| 18 | NotReachable | `animal_emoticon_notreachable` |
| 19 | Peace | `emoticon_1` |
| 20 | Runaway | ซ่อน |
| 21 | RunwayNoticed | `emoticon_004` |
| 22 | Sleep | `animal_emoticon_sleep` |
| 23 | ช่องว่าง/รับความเสียหาย | `animal_emoticon_takedamage` |
| 24 | Thirsty | `animal_emoticon_thirsty` |
| 25 | Scared | `animal_emoticon_scared` |
| 26 | Alert | `animal_emoticon_scared` |

Pet hunger ใช้ array แยก: ค่า `0` ซ่อน, `1` เป็น hungry สีขาว และ `2` เป็น
hungry สีเทา ส่วนไอคอนในหน้าฝึกสัตว์ที่ `PetUtil` เลือก
`animal_emoticon_angry/happy` เป็นอีก flow หนึ่ง

## ข้อสรุปสำหรับปลั๊กอิน

- R9E ส่ง `CombatInteraction` แบบเดิมและตั้ง `details["status"]`; ไม่มี renderer
  หรือ UI ซ้อนจาก plugin
- ส่งเฉพาะ reaction ที่มี enum/data ยืนยันแล้ว: `Blow`, `Groggy`, `KnockDown`
  และ `Dead`; เมื่อ reaction จบส่ง `Battle` ซึ่ง client ซ่อนไอคอนอยู่แล้ว
- Normal directional damage ไม่เดาค่าช่องว่าง 23 และไม่ส่ง status icon
- รูปหน้าตาเป็นกากบาทในภาพอ้างอิงมีแนวโน้มอยู่ในกลุ่ม Blow/KnockDown หรือช่อง
  take-damage มากกว่า chat emoji แต่ต้อง field-test packet status เพื่อยืนยัน sprite
  ที่เห็นจริงใน atlas
