# N点标定 / NPointCalibration

## 定位
- 基于多点对应关系估计 2D 变换，输出 `CalibrationBundleV2`。
- 目标是“全部点参与求解 + 鲁棒验收”，不再是最小点数硬求解。

## 输入
- `PointPairs`：像素/物理点对。
- `CalibrationMode`：`Affine` 或 `Perspective`。

## 输出
- `CalibrationData`：`CalibrationBundleV2` JSON。

## 关键契约
- `calibrationKind = planarTransform2D`。
- `transformModel` 与求解模式一致（`affine` / `homography`）。
- `quality` 输出包含 `accepted`、`meanError`、`maxError`、`inlierCount`。

## 注意事项
- 透视模型下不应输出全局单值 `PixelSize` 作为工业结论。
- 共线、重复点、近奇异矩阵应直接失败，不允许伪成功。
