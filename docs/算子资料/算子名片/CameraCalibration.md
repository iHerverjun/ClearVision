# 相机标定 / CameraCalibration

## 定位
- 输出统一 `CalibrationBundleV2`（JSON）。
- `Mode=SingleImage` 视为预览模式，默认 `Quality.Accepted=false`。
- `Mode=FolderCalibration` 才是可验收的生产标定路径。

## 输入
- `Image`：单图预览模式输入。
- 文件夹模式通过参数指定数据集目录。

## 输出
- `Image`：角点可视化或统计预览。
- `CalibrationData`：`CalibrationBundleV2` JSON。

## 关键契约
- `schemaVersion` 固定为 `2`。
- `calibrationKind = cameraIntrinsics`。
- `intrinsics.cameraMatrix` 为 3x3。
- `distortion.model` 为 `brownConrady`（当前主路径）。
- `quality.accepted` 为最终验收开关。

## 工业验收建议
- 文件夹模式建议 `>= 10` 张多姿态样本。
- 同批样本分辨率必须一致。
- 同时约束 `meanError` 和 `maxError`，并记录坏样本剔除信息。
