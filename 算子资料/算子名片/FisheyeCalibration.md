# 鱼眼标定 / FisheyeCalibration

## 定位
- 输出鱼眼内参 `CalibrationBundleV2`。
- `SingleImage` 仅用于预览/检查角点，正式验收应使用多图模式。

## 输入
- 单图或文件夹标定图像（按参数模式控制）。

## 输出
- `CalibrationData`：`CalibrationBundleV2` JSON。
- `Image`：可视化结果。

## 关键契约
- `calibrationKind = fisheyeIntrinsics`。
- `distortion.model = kannalaBrandt`。
- `quality.accepted` 为是否可投产的唯一标志。

## 约束
- 不允许静默回退成普通 pinhole 标定并仍标注为鱼眼结果。
