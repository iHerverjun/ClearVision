# HandEyeCalibration

## 定位
- 求解 3D 手眼外参并输出 `CalibrationBundleV2`。
- 支持 `eye_in_hand` / `eye_to_hand` 两种手眼标定类型。

## 输入
- `RobotPoses`
- `CalibrationBoardPoses`

## 输出
- `CalibrationData`：`CalibrationBundleV2` JSON
- `CalibrationQuality`
- `HtmlReport`
- `Suggestions`

## 关键契约
- `calibrationKind = handEye`
- `transformModel = rigid3D`
- `transform3D.matrix` 为 `4x4`
- `quality.accepted` 决定下游是否允许进入生产链

## 注意
- 不再以旧矩阵旁路作为正式公开契约。
- 桌面端二维比例/偏移标定链已从 hand-eye 3D 语义中拆分，不应混用。
