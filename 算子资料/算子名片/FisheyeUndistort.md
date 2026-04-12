# FisheyeUndistort

## 定位
- 消费 `CalibrationBundleV2` 中鱼眼内参执行去畸变。
- 使用 map cache 作为性能热路径。

## 输入
- `Image`
- `CalibrationData`

## 输出
- `Image`
- `Applied`
- `Accepted`
- `DistortionModel`
- `UseLutAcceleration`

## 关键契约
- `calibrationKind = fisheyeIntrinsics`
- `distortion.model = kannalaBrandt`
- `quality.accepted = true`

## 注意
- 运行时只接受 `CalibrationData` 或内联 `CalibrationData` 参数。
- 不再通过旧文件路径作为正式生产入口。
