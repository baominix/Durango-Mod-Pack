# Craft / Build — Offline Reconstruction Plan

## เป้าหมาย

เป้าหมายของ `CraftBuildPlugin` คือทำให้ระบบ Craft / Build ใน `Durango_Ver_PC_Final` ที่ทำงานแบบ offline มี behavior และ flow ใกล้เคียงกับตอนเกมทำงานแบบ online ให้มากที่สุด

คำว่า **online-like** ในเอกสารนี้ไม่ได้หมายถึงการนำเกมกลับไปเชื่อมต่อ server จริง แต่หมายถึงการ reconstruct logic ที่เดิมอาจพึ่งพา server แล้วจำลองผลลัพธ์นั้นภายใน local/offline runtime เพื่อให้ client ทำงานเสมือนว่าระบบ online ที่จำเป็นยังมีอยู่

## สถานะปัจจุบัน

`CraftBuildPlugin` ที่มีอยู่ในปัจจุบันยังไม่ควรถูกมองว่าเป็นการแก้ปัญหาที่ต้นเหตุ ระบบบางส่วนอาจเป็น workaround, patch หรือการบังคับ client ให้ผ่านขั้นตอนที่เดิมต้องได้รับข้อมูล/ผลลัพธ์จาก server

ดังนั้นก่อนเพิ่ม workaround ใหม่ ต้องศึกษาว่า Original client ถูกออกแบบให้ทำอะไรเอง และส่วนใดเคยเป็นหน้าที่ของ online/server flow จริง

หลักสำคัญคือ **reconstruct original behavior ก่อน แล้วจึง patch เฉพาะส่วนที่หายไปใน offline environment**

## แหล่งข้อมูลหลัก

การศึกษาไม่ควรอ้างอิงเฉพาะ source ของ plugin ปัจจุบัน แต่ต้องเทียบข้อมูลจากหลายแหล่ง:

1. Original PC AssetRipper export
   - `D:\ProgramData\Durango_Ver_PC_Final\assetRipper_Export_original\AssetRipper_export_20260728_212417\ExportedProject\Assets`

2. XAPK AssetRipper export
   - `D:\ProgramData\Durango_Ver_PC_Final\xapk_export\AssetRipper_export_20260622_190447\ExportedProject\Assets`

3. ข้อมูลตาราง / Excel
   - `D:\ProgramData\Durango_Ver_PC_Final\tools\excel`

4. Source ของ plugins และ reconstruction code ปัจจุบัน
   - `D:\ProgramData\Durango_Ver_PC_Final\tools\durango-mod-original`

ควรแยกหลักฐานให้ชัดว่า behavior ใดมาจาก Original PC, XAPK, table data หรือ logic ที่ plugin สร้างขึ้นเอง

## สิ่งที่ต้องศึกษาใน Craft

ต้อง trace flow ตั้งแต่ผู้เล่นเลือก recipe จนถึงได้รับ item จริง เช่น:

- recipe / blueprint ถูกโหลดจากที่ใด
- requirement และ unlock condition
- material requirement
- inventory validation
- item consumption
- craft duration / progress
- skill / level / proficiency requirement
- quality หรือ random result ถ้ามี
- output item creation
- inventory insertion
- failure / cancellation
- UI state ระหว่าง craft
- request/response ที่เดิมคาดว่าจะมาจาก server
- state หรือ callback ที่ client รออยู่หลังส่งคำสั่ง

เป้าหมายคือหา boundary ว่าอะไรเป็น client-side logic ที่ยังมีครบ และอะไรคือ server-side result ที่ offline runtime ต้องจำลอง

## สิ่งที่ต้องศึกษาใน Build

Build system ต้อง trace แยกจาก Craft แม้จะมี resource/item logic ร่วมกัน เช่น:

- construction recipe / blueprint
- placement mode
- placement validation
- terrain / collision / distance restrictions
- required materials
- material consumption
- construction state
- unfinished -> completed transition
- spawned object / entity / structure
- ownership
- durability / interaction state
- save/load หรือ persistence
- request/response ที่เดิมพึ่ง server

ไม่ควรแก้เพียงให้ UI วางสิ่งปลูกสร้างได้ หาก downstream state ของ structure ยังไม่ตรงกับ original flow

## หลักการ Online-like Offline

Offline reconstruction ควรพยายามรักษา client flow เดิมให้มากที่สุด

แทนที่จะ bypass ทุก validation หรือสร้าง item/object โดยตรง ควรจำลอง response/state transition ที่ client เดิมคาดหวัง เช่น:

`Original Client Request -> Original Online/Server Result -> Client State Transition`

ให้กลายเป็น:

`Original Client Request -> Local Offline Simulation -> Same/Equivalent Client State Transition`

วิธีนี้มีโอกาสทำให้ UI, animation, inventory, crafting state, building state และระบบอื่นที่เชื่อมกันทำงานใกล้ Original มากกว่าการ patch ปลายทางเป็นรายจุด

## ความเกี่ยวข้องกับ Game Mode

มี plugin อื่นที่เกี่ยวข้องบางส่วน เนื่องจากกำลังพยายามแยก mode ของเกมผ่าน:

- `/gamemode 0`
- `/gamemode 1`

ต้องตรวจให้ชัดว่าแต่ละ mode เปลี่ยนอะไรบ้าง เช่น permission, survival rules, resource consumption, crafting/build restrictions, placement rules หรือ debug/developer behavior

`CraftBuildPlugin` ไม่ควรเป็นเจ้าของระบบ game mode ทั้งหมด หากไม่จำเป็น ควรมี integration boundary ที่ชัดเจน เช่น Craft/Build อ่านสถานะ mode จากระบบกลาง แล้วเลือก rules ที่เหมาะสม

ต้องหลีกเลี่ยงการ hardcode `/gamemode 0` และ `/gamemode 1` กระจายอยู่ตาม Craft/Build logic เพราะจะทำให้ reconstruction ผูกกับ plugin อื่นมากเกินไปและตรวจสอบ Original behavior ได้ยาก

## แนวทางการศึกษา

### Phase 1 — Inventory ของระบบปัจจุบัน

อ่าน `CraftBuildPlugin` ทั้งหมดและทำรายการ Harmony patches, hooks, reflection, injected behavior, bypass และ fake responses ที่มีอยู่

แยกว่าแต่ละส่วนเป็น:

- confirmed original behavior
- reconstruction
- workaround
- debug/test code
- unknown

### Phase 2 — Trace Original Structures

ค้นหา class, method, enum, packet/message, data model และ resource ที่เกี่ยวกับคำสำคัญ เช่น craft, recipe, manufacture, build, construction, placement, blueprint, material, inventory และ result/response

เทียบ Original PC กับ XAPK เพื่อหาส่วนที่ export ฝั่งหนึ่งมีข้อมูลมากกว่าอีกฝั่ง

### Phase 3 — Data Mapping

เชื่อม code กับข้อมูลใน Excel/table เพื่อระบุ ID และความสัมพันธ์ เช่น recipe -> materials -> result item หรือ build recipe -> structure/entity

ห้ามเดาความหมายของ field หากยังมีหลักฐานไม่เพียงพอ

### Phase 4 — Online Boundary

ระบุจุดที่ client ส่ง request แล้วรอ state/response จาก server

สำหรับแต่ละ flow ให้บันทึก:

- input
- validation ก่อน request
- request type
- expected response/result
- state ที่เปลี่ยนหลัง response
- UI callback
- inventory/world side effects

นี่คือจุดหลักที่ offline simulation ควรเข้าไปแทนที่

### Phase 5 — Reconstruction

สร้าง local implementation ที่คืนผลลัพธ์ในรูปแบบหรือ state transition ที่ใกล้ Original ที่สุด โดย reuse original client classes/methods ก่อนสร้างระบบใหม่

ควรหลีกเลี่ยงการ duplicate logic ที่เกมมีอยู่แล้ว

### Phase 6 — Game Mode Integration

หลัง Craft/Build core flow ทำงานแล้วจึงเชื่อม `/gamemode 0` และ `/gamemode 1`

กำหนดให้ mode เป็น policy/input ต่อ Craft/Build ไม่ใช่ฝัง mode switching เข้าไปใน core reconstruction

## Validation Criteria

การถือว่าระบบได้รับการแก้ไขจริง ไม่ควรดูเพียงว่า "กด Craft ได้" หรือ "วาง Build ได้" แต่ควรตรวจอย่างน้อยว่า:

- UI flow ถูกต้อง
- validation ถูกต้อง
- material ถูกใช้ตาม rules
- output ถูกสร้างด้วย original/equivalent path
- inventory state ถูกต้อง
- animation/progress/callback ทำงาน
- build placement และ structure state ถูกต้อง
- cancel/fail path ไม่ทำให้ state ค้าง
- save/load หรือ persistence ที่เกี่ยวข้องไม่เสีย
- game mode เปลี่ยน rules ตามที่ตั้งใจโดยไม่ทำลาย core flow
- ไม่เกิด duplicate item / duplicate structure
- ไม่มี workaround ที่ bypass ระบบสำคัญโดยไม่มีเหตุผล

## ข้อควรระวัง

AssetRipper export ไม่ได้หมายความว่า source ที่เห็นคือ source code ดั้งเดิมแบบสมบูรณ์ ต้องระวังข้อมูลที่หายจาก serialization, stripped code, generated structures และ behavior ที่เดิมอยู่ฝั่ง server

XAPK และ Original PC อาจเป็นคนละ revision จึงไม่ควร assume ว่า ID, field หรือ flow ตรงกันทุกจุด

Plugin ปัจจุบันเป็นหลักฐานของสิ่งที่เราเคยทดลอง ไม่ใช่หลักฐานว่าเกม Original ทำงานแบบนั้น

เมื่อพบ behavior ที่ยังยืนยันไม่ได้ ควรบันทึกเป็น hypothesis และหา evidence เพิ่มก่อนเปลี่ยนเป็น production logic

## หลักการสำหรับการแก้ Source ต่อจากนี้

ก่อนแก้ Craft/Build แต่ละส่วน ให้ตอบให้ได้ก่อนว่า:

1. Original client มี logic นี้อยู่แล้วหรือไม่
2. ถ้ามี ทำไม offline จึงไปไม่ถึง logic นั้น
3. จุดที่ขาดคือ server response, state initialization, data, permission หรือ callback ใด
4. สามารถจำลองเฉพาะส่วนที่หายแล้วปล่อยให้ original client flow ทำงานต่อได้หรือไม่
5. การแก้นี้มีผลกับ `/gamemode 0` / `/gamemode 1` หรือ plugin อื่นหรือไม่

เป้าหมายระยะยาวคือให้ `CraftBuildPlugin` มีหน้าที่เป็น **offline compatibility/reconstruction layer** มากกว่าการเป็นชุด bypass สำหรับ Craft/Build
