# HandEyeCalibrationValidator

## 定位
- 校验手眼标定结果并回写带质量信息的 `CalibrationBundleV2`。

## 输入
- `RobotPoses`
- `CalibrationBoardPoses`
- `CalibrationData`

## 输出
- `CalibrationData`：验证后回写的 `CalibrationBundleV2`
- `MeanError`
- `MaxError`
- `MeanRotationError`
- `Quality`
- `HtmlReport`
- `Suggestions`

## 关键契约
- 运行时只接受 `CalibrationData`
- `schemaVersion = 2`
- `calibrationKind = handEye`
- `transform3D.matrix` 必须可解析为 `4x4`

## 注意
- 不再以旧矩阵旁路作为验证链的正式输入口径。
