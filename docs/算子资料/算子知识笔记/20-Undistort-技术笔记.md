# Undistort 技术笔记

> 历史实现笔记（V1/V1.5）
>
> 本文原先记录了旧版 `Undistort` 的实现细节，包括旧文件回退路径、旧 JSON 兼容行为和早期去畸变热路径说明。
> 当前仓库的正式主链已经统一到 `CalibrationBundleV2`，本文不再作为现行接入说明。

## 当前主链入口
- 现行运行时契约：`CalibrationBundleV2`
- 现行操作说明：[`../算子名片/Undistort.md`](../算子名片/Undistort.md)
- 总手册入口：[`../算子手册.md`](../算子手册.md)

## 阅读方式
- 如果你在追查旧行为或历史兼容问题，可以把本文当作旧实现说明。
- 如果你要接入生产链，请只参考当前算子名片、总手册和 `CalibrationLoader -> CalibrationData` 主链。
