# Undistort

## 定位
- 基于 `CalibrationBundleV2` 执行标准相机去畸变。
- 生产链要求 `quality.accepted = true`。

## 输入
- `Image`
- `CalibrationData`

## 输出
- `Image`
- `Applied`
- `Accepted`
- `CalibrationKind`
- `DistortionModel`

## 关键契约
- `calibrationKind = cameraIntrinsics`
- `intrinsics.cameraMatrix` 必须为有效 `3x3`
- `distortion.model` 必须与系数数量匹配
- 标定尺寸必须与运行图像尺寸一致

## 注意
- 运行时只接受 `CalibrationData` 或内联 `CalibrationData` 参数。
- 不再通过旧文件路径或旧散字段作为正式生产入口。
