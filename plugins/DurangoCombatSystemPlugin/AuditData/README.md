# AuditData

ไฟล์ในโฟลเดอร์นี้เป็นผลวิเคราะห์ที่สร้างซ้ำได้สำหรับงานออกแบบ
`DurangoCombatSystemPlugin` และ **ไม่ถูก deploy เป็น runtime data** โดย build script
รุ่น `0.3.9`

- `saurus_action_audit.json` — entity/profile และ attack ทุกตัวจาก Framework
  `Tricera`, `Phenacodus`, `Raptor` พร้อม intent/alignment/root-yaw policy ที่อยู่ใน
  สถานะ candidate
- `saurus_root_transform_audit.json` — `Bip001` position, quaternion และ planar
  yaw ของ AnimationClip attack ทุก clip ที่ Framework ทั้งสามอ้างถึง

สร้างใหม่ด้วย:

```powershell
.\Generate-SaurusActionAudit.ps1
.\Generate-SaurusRootTransformAudit.ps1
```

ข้อมูลที่อ่านตรงจาก `animal.json`, Framework และ AnimationClip ระบุเป็น
`Original`; การจัด intent และ policy ระบุเป็น `Reconstructed` จนกว่าจะผ่าน
shadow/field test

