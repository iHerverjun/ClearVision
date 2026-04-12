# 平移旋转标定 / TranslationRotationCalibration

## 定位
- 面向 2D 刚体/相似变换的标定求解。
- 输出统一为 `CalibrationBundleV2`，并使用 `Quality.Accepted` 作为验收位。

## 输入
- `CalibrationPoints`：图像坐标与机械坐标对应关系。
- `Method`：求解方法（例如 `SVD`、`LeastSquares`）。

## 输出
- `CalibrationData`：`CalibrationBundleV2` JSON。
- 可选诊断字段：标定误差、样本数、建议信息。

## 关键契约
- `calibrationKind = rigidTransform2D`。
- `transform2D.model` 必须和方法语义一致。
- 奇异输入或退化点集必须失败，不能输出默认矩阵冒充成功。
