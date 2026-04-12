# CameraCalibration 技术笔记

> 历史实现笔记（V1/V1.5）
>
> 本文原先记录了旧版 `CameraCalibration` 的实现细节，包括旧 `JSON/XML` 输出口径、`SingleImage` 的旧行为和早期验收阈值。
> 当前仓库的正式主链已经统一到 `CalibrationBundleV2`，本文不再作为现行接入说明。

## 当前主链入口
- 现行运行时契约：`CalibrationBundleV2`
- 现行操作说明：[`../算子名片/CameraCalibration.md`](../算子名片/CameraCalibration.md)
- 总手册入口：[`../算子手册.md`](../算子手册.md)

## 阅读方式
- 如果你在排查历史行为差异，可以把本文当作旧版本实现背景资料。
- 如果你要接入、验收或编写新流程，请只参考当前算子名片、总手册和生成目录。
