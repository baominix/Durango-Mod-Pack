# Player action targeting audit

ตรวจจากข้อมูลต้นฉบับ:

`AssetRipper_export_20260728_212417/ExportedProject/Assets/Resources/offline/assets/player/player_battle_actions.json`

และ enum เดิมของเกม `Shared.Battle.DamageType`:

| Value | Name | Target semantics |
|---:|---|---|
| 0 | `Melee` | selected target เดียว; `radius` เป็น reach/range |
| 1 | `CircularArea` | หลายเป้าหมายในวงหรือส่วนโค้ง |
| 2 | `RectangularArea` | หลายเป้าหมายในสี่เหลี่ยมตามทิศทาง |
| 3 | `Ranged` | selected target เดียว; `radius` เป็น projectile/target range |

## Summary

- action ที่มี `attack_info`: 56
- hit records: 74
- selected-target actions: 42 (`Melee` 34 + `Ranged` 8)
- area actions แบบไม่ซ้ำ: 14
- `use_target_origin=true`: 0 records

หมายเหตุ: `PlayerActionAttackInfo.MakeAlerted()` ใส่ `radius` ลงใน
`AttackAlerted` สำหรับ Melee, CircularArea และ Ranged เหมือนกัน และ developer
visualizer วาดทั้งสามเป็นวงได้ สิ่งนี้เป็นภาพตรวจ reach/range ไม่ได้หมายความว่า
Melee/Ranged ทำ damage ทุกเป้าหมายในวง การตัดสินชุดเป้าหมายต้องใช้ `DamageType`
เป็นหลัก

## Selected-target: Melee (34 actions)

- Bare hand: `barehand_combination`, `barehand_default_a`,
  `barehand_default_b`, `barehand_kick_a`, `barehand_kick_b`,
  `barehand_smash`, `melee_tackle`
- One hand defaults: `onehand_default_a/b/c`,
  `onehand_default_axe_a/b/c`, `onehand_default_blunt_a/b/c`
- One hand smash: `onehand_smash`, `onehand_smash_axe`,
  `onehand_smash_blunt`
- Two hand defaults: `twohand_default_a/b/c`,
  `twohand_default_axe_a/b/c`, `twohand_default_blunt_a/b/c`
- Lance defaults: `twohand_lance_default_a/b/c`
- Two hand smash: `twohand_smash`, `twohand_smash_axe`,
  `twohand_smash_blunt`

## Selected-target: Ranged (8 actions)

- Bow: `ranged_bow_default_a/b/c`, `ranged_bow_quickshot`,
  `ranged_bow_aimedshot`
- Crossbow: `ranged_crossbow_default`, `ranged_crossbow_quickshot`,
  `ranged_crossbow_aimedshot`

Quick Shot มี 3 hit records แต่ทั้งสาม hit ใช้ selected target เดิม

## Area actions (14 unique actions)

### CircularArea

- `twohand_sweeping`, `twohand_sweeping_axe`,
  `twohand_sweeping_blunt`
- `onehand_flurry`, `onehand_flurry_axe`, `onehand_flurry_blunt`
  สอง hit แรกเป็นส่วนโค้ง `CircularArea`

### RectangularArea

- `onehand_stab`, `onehand_stab_axe`, `onehand_stab_blunt`
- `twohand_lance_dash`, `twohand_lance_strike`
- `twohand_strike`, `twohand_strike_axe`, `twohand_strike_blunt`
- `onehand_flurry`, `onehand_flurry_axe`, `onehand_flurry_blunt`
  hit สุดท้ายเป็น `RectangularArea`

## Runtime rule ตั้งแต่ 0.3.4

- `Melee` และ `Ranged` ต้องมี `TargetEntityId` ที่ยังมีชีวิตและอยู่ใน
  `meta.use_range`
- hit ทุก hit ลงเฉพาะ selected target เดิม
- ไม่ส่ง gameplay area telegraph สำหรับสองชนิดนี้
- `CircularArea` และ `RectangularArea` เท่านั้นที่ query หลายเป้าหมายและวาด
  authoritative telegraph
- `/dev attackalert on` ยังสามารถเปิด visualizer ดิบของเกมเพื่อดู radius/reach
  ที่เกมเตรียมไว้สำหรับ developer ได้ โดยไม่เปลี่ยนกฎ damage

## Auto-approach และ root motion

เกมเดิมอนุญาตให้เริ่ม action เมื่อเข้า `meta.use_range + target bound radius` และ
`UsingAction` เป็นผู้เดินเข้าหารวมถึงหันหน้าให้เป้าหมาย จากนั้นตำแหน่ง hit/เส้นของ
เกมเดิมยังชดเชยด้วย `PlayerRootMotionPath.GetDelta(..., attack_time)` ของ motion
อีกชั้นหนึ่ง ดังนั้น `use_range` ไม่จำเป็นต้องเท่ากับ radius/rect ของแต่ละ hit

ตั้งแต่ `0.3.8` runtime เริ่มจาก actor origin + remaining root-motion delta ณ
`attack_time` + `attack_info.offset`, clamp เส้นทางกับ bound ของ selected target และ
refresh center จากฐานจริงทุก frame จนถึง hit time จึงไม่ถือว่า root motion เกิดครบ
เมื่อผู้เล่นถูกสัตว์หรือ collision หยุดไว้ ส่วน selected-target ที่ผ่าน validation
ไม่ใช้ area query ซ้ำ ณ hit time
