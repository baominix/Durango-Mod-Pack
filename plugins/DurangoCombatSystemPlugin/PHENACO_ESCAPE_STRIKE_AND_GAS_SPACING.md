# Phenacodus EscapeStrike และ gas pre-commit spacing

สถานะ: implementation/build และ field feedback ผ่าน

## ข้อสรุปเรื่องท่าตะกุยดิน

ท่าตะกุยดินคือ `phenaco_attack_escape` / `Phenaco_Attack_Escape` ไม่ใช่
`Phenaco_Battle_Idle` ชื่อ Escape สื่อถึง context ที่สัตว์พยายามหนี แต่ตัว motion
เป็นการโจมตีป้องกันตัว: หันหลังให้ player แล้วตะกุยโจมตีด้านหลัง ก่อนเคลื่อนออก

Framework ให้ข้อมูลเดิมดังนี้:

- hit frames: 14, 18, 22, 26
- hit rectangles: half size 250×60, offset Y -150
- hit แรกมี radius 300 และ sector 240..150 เพิ่มเติม
- root yaw ช่วง frame 14 ประมาณ -179.62 องศา
- root Z ช่วงสี่ hit อยู่ประมาณ +6.67 ถึง -29.39 หน่วย
- root จบ clip: Z -318.16 และ yaw กลับใกล้ 0 องศา

ความหมายคือ action plan ต้อง commit ขณะสัตว์หันหา player แล้วปล่อย original
root-yaw หมุนสัตว์กลับหลัง พื้นที่ด้านหลังจึงชี้หา player ระหว่างสี่ hit จากนั้น
original root-position เคลื่อนฐานออกจาก player เอง

## EscapeStrike flow

1. สัตว์เข้า battle ตามปกติ
2. เมื่อ HP ต่ำกว่า 20% ใช้ retreat chance เดิม 15% หนึ่งครั้งต่อ engagement
3. ถ้าเป็น Elephantulus ให้จอง `phenaco_attack_escape` โดยไม่ส่งเข้า normal selector
4. state `PrepareEscape` หันหน้าเข้าหา player และรอ face-settle
5. commit immutable `SaurusActionPlan`; animation/root/telegraph/hit ใช้ plan เดียวกัน
6. เมื่อ Escape animation จบ จึงเข้า Retreat ต่ออีก 6 วินาที

ถ้า PrepareEscape ถูก reaction/evade ขัดจังหวะ จะยกเลิก prepared action และลอง
เริ่มลำดับหนีใหม่หลัง reaction จบ ถ้า target หายหรือหลุด leash จะไม่เก็บ action
ข้าม engagement

## Gas spacing

`phenaco_gas` เป็น action แยกจาก Escape:

- damage frame 42 หรือ 1.4 วินาทีที่ 30 fps
- radius 400, sector 140..220 องศา
- root ณ hit: Z 150.77, yaw -177.94 องศา

เมื่อ selector เลือก gas และ surface distance ต่ำกว่า 200 หน่วย สัตว์จะจองท่าไว้,
ถอยหลังโดยยังหันหา target ไปยัง 250 หน่วย แล้วจึง commit action plan เป้าหมายคือ
ลดกรณี root เดินชน player ก่อนถึง hit และวาง player ใกล้กลาง sector เดิม

timeout 1.25 วินาทีและ speed multiplier 0.65 เป็น `Reconstructed`; clip, hit frame,
root curve และ geometry เป็น `Original Data`

## Field-test gate

1. ลด HP Elephantulus ต่ำกว่า 20% จนสุ่ม retreat สำเร็จ
2. ต้องเห็นมันหันหลังและตะกุยด้วย `Phenaco_Attack_Escape` สี่ hit ก่อนวิ่งหนี
3. เส้นทั้งสี่ต้องอยู่ฝั่ง player ตาม root yaw และ damage ต้องตรงเส้น
4. หลัง clip จบต้องไม่ flash/วาร์ป และต้องต่อเข้า Retreat
5. `Phenaco_Battle_Idle` ต้องไม่ถูกใช้แทน Escape ระหว่าง cooldown
6. gas ระยะประชิดต้องถอยจัดระยะก่อน commit โดยไม่กระทบ bite/jump
