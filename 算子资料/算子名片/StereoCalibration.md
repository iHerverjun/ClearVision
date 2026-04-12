# 双目标定 / StereoCalibration

## 定位
- 生成双目系统标定结果，输出 `CalibrationBundleV2`。
- `SinglePair` 用于预览，`FolderCalibration` 才是可验收路径。

## 输入
- `LeftImage`、`RightImage` 或标定数据集目录（按模式）。

## 输出
- `CalibrationData`：包含 `stereo` 结构的 `CalibrationBundleV2` JSON。
- 可视化图像与误差统计（实现相关）。

## 关键契约
- `calibrationKind = stereoRig`。
- `transformModel = stereoRig`。
- `stereo` 节点包含双侧内参与外参（R/T/E/F/Q）。
- `quality.accepted` 作为交付开关。
