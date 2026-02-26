# 畸变校正 / Undistort

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `UndistortOperator` |
| 枚举值 (Enum) | `OperatorType.Undistort` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于相机内参与畸变系数进行镜头去畸变，对输入图像执行径向/切向畸变补偿。
> English: Applies lens undistortion using camera intrinsics and distortion coefficients.

## 实现策略 / Implementation Strategy
> 中文：优先读取输入端口 `CalibrationData`，否则从 `CalibrationFile` 加载；解析 `CameraMatrix`（支持扁平 9 元素或 3x3）与 `DistCoeffs` 后调用 `Cv2.Undistort`。
> English: Resolves calibration JSON from input first or file fallback, parses camera matrix and distortion coefficients, then runs `Cv2.Undistort`.

## 核心 API 调用链 / Core API Call Chain
- `TryResolveCalibrationData`（输入端口优先，文件回退）
- `JsonDocument.Parse` + `TryParseCameraMatrix` / `TryParseDistCoeffs`
- `new Mat(3,3,CV_64FC1, cameraMatrix)` 与畸变系数 `Mat`
- `Cv2.Undistort`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CalibrationFile` | `file` | "" | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |
| `CalibrationData` | 标定数据 | `String` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 校正图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | `O(W*H)` |
| 典型耗时 (Typical Latency) | 约 `1-5 ms`（1920x1080） |
| 内存特征 (Memory Profile) | 一张输出图 + 小型标定矩阵/畸变向量 |

## 适用场景 / Use Cases
- 适合 (Suitable)：宽角镜头校正、测量前几何预矫正、下游视觉算法对直线几何敏感的流程。
- 不适合 (Not Suitable)：没有有效标定数据、标定分辨率与运行分辨率差异过大且未重标定的场景。

## 已知限制 / Known Limitations
1. 要求 JSON 中存在 `CameraMatrix`，键名或格式不匹配会直接失败。
2. 当前未缓存重映射表，多帧连续处理时相较 `initUndistortRectifyMap+remap` 方案可能更慢。
3. 未暴露新相机矩阵与 ROI 裁剪参数，输出视场控制能力有限。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |