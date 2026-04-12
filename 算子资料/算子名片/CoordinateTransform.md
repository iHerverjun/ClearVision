# CoordinateTransform

## 定位
- 消费 `CalibrationBundleV2` 的 `Transform2D` 做像素到物理坐标转换。

## 输入
- `PixelX`
- `PixelY`
- `CalibrationData`

## 输出
- `PhysicalX`
- `PhysicalY`
- `Image`（可选可视化）

## 支持模型
- `scaleOffset`
- `similarity`
- `affine`
- `homography`

## 关键门槛
- `schemaVersion = 2`
- `quality.accepted = true`
- `transform2D.matrix` 维度必须与模型匹配
- 数值必须为有限值

## 注意
- 正式主路径是 `CalibrationLoader -> CalibrationData -> CoordinateTransform`。
- 不再以旧文件路径或旧 `Origin/Scale` 作为正式验收口径。
