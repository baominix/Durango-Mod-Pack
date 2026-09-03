# R6 Elephantulus intent execution

สถานะ: implementation/build และ field feedback ผ่าน

## ขอบเขต

Entity type `2037` เปลี่ยนจาก range-only selector ไปใช้ context/intent execution
โดยไม่เปลี่ยน player combat runtime `0.3.9` และไม่เปลี่ยน behavior ของ `2039`

## Intent ที่เปิดใช้

| Context | Intent/action | หลักฐาน |
|---|---|---|
| Front, surface 0–230 | `StandardFront` / `phenaco_bite` | Original geometry + Observed |
| Front, surface 150–520 | `GapCloser` / `phenaco_jump` | Original geometry/root; range Reconstructed |
| Front, surface 0–300 | `AreaControl` / `phenaco_gas` weight ต่ำ | Original geometry/root; selection Reconstructed |
| HP ≤20%, retreat roll 15% | `EscapeStrike` / `phenaco_attack_escape` แล้ว Retreat 6s | Original geometry/root + Observed behavior reconstruction |

เมื่อ `jump` eligible จะมี priority เหนือ bite/gas ส่วนช่วงประชิด bite และ gas
อยู่ priority เดียวกันและใช้น้ำหนัก 5:1 เพื่อรักษาความถี่ต่ำของ gas

## เหตุผลที่ gas ต้องเริ่มจาก Front

Framework ระบุ gas hit ที่ frame 42 เป็น sector 140..220 องศา ขณะที่ original
root ณ hit เคลื่อน local Z ประมาณ +150.77 และหมุน yaw ประมาณ -177.94 องศา
ดังนั้น target ต้องอยู่ด้านหน้าตอน commit; เมื่อ animation หมุนตัว rear sector ที่ hit
จึงกลับมาครอบ target

ถ้า surface distance ต่ำกว่า 200:

1. จอง `phenaco_gas`
2. ถอยโดยยังหันหา target ไปหา preferred distance 250
3. commit ด้วย `FaceTargetBeforeCommit`
4. motion, telegraph และ damage ใช้ action plan/root transform เดียวกัน

ถ้าถอยติด collision หรือครบ timeout จะ commit จากตำแหน่งที่ collision ยอมรับจริง
ไม่เริ่ม prepare loop ใหม่

## Counter policy ที่แก้พร้อม R6

`tricera_counter` ยังเป็น action เดิมที่อ่าน geometry/root ได้ แต่ trigger แบบ
Player Miss หรือ Zebra Dodge ภายใน 1.25 วินาทีถูกยกเลิก เพราะเป็น reconstruction
ที่ไม่มีหลักฐานเพียงพอ ขณะนี้ rule ถูก audit-block และ runtime ไม่แทนด้วย trigger ใหม่

## Field-test checklist

1. Elephantulus ด้านหน้าใกล้ควรใช้ bite เป็นหลัก และ gas เกิดน้อยกว่า
2. เมื่อ gas ถูกเลือกที่ระยะประชิด ต้องถอยโดยยังหันหา player แล้วพื้นที่ AoE ครอบ
   player ตาม original rear sector หลัง root turn
3. ด้านหน้าไกลควรใช้ jump; เส้น/ตัว/hit ต้องตรงกันและไม่ flash ตอนจบ
4. เดินไป flank/rear ระหว่าง cooldown: Elephantulus ต้องจัดแนวกลับก่อนเลือก
   front action ไม่วาดพื้นที่ผิดด้าน
5. low-health EscapeStrike ต้องหันหน้าเข้า player ก่อน animation หมุนหลัง ตะกุยสี่
   hit แล้วเข้าสู่ Retreat
6. Miss/Dodge ต่อ Zebra ต้องไม่เรียก `tricera_counter`
