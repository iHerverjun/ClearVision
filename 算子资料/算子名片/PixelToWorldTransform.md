# 像素-世界坐标变换 / PixelToWorldTransform

## 定位
- 使用 `CalibrationBundleV2` 在像素坐标与世界坐标之间做双向变换。

## 输入
- `Points`：待变换点集。
- `CalibrationData`：`CalibrationBundleV2` JSON。
- 可选 `Image`：用于可视化。

## 输出
- `TransformedPoints`
- `TransformResult`（误差/条件数等诊断）
- `Image`（可视化）

## 支持路径
- `Transform2D` 平面映射路径（`scaleOffset/similarity/affine/homography`）。
- `intrinsics + extrinsics + worldPlaneZ` 投影路径（实现支持时）。

## 验收门槛
- `schemaVersion=2`。
- `quality.accepted=true`。
- 模型与矩阵维度匹配。
