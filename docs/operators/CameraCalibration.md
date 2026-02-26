# 相机标定 / CameraCalibration

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CameraCalibrationOperator` |
| 枚举值 (Enum) | `OperatorType.CameraCalibration` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于针孔相机模型，通过棋盘格/圆点阵列角点与理想物理坐标的对应关系，最小化重投影误差，联合估计相机内参矩阵与畸变系数。
> English: Uses chessboard/circle-grid correspondences to estimate intrinsics and distortion by minimizing reprojection error in a pinhole camera model.

## 实现策略 / Implementation Strategy
> 中文：支持 `SingleImage` 与 `FolderCalibration` 两种模式；先做角点检测（棋盘格可选亚像素细化），再构建世界坐标点并调用 OpenCV 标定。文件夹模式会统计失败图片并要求至少 3 张有效样本。
> English: Supports single-image and folder modes; detects pattern points (with optional subpixel refinement for chessboards), builds object points, and calibrates with OpenCV. Folder mode tracks failed files and requires at least 3 valid images.

## 核心 API 调用链 / Core API Call Chain
- `Cv2.CvtColor`（彩色转灰度）
- `Cv2.FindChessboardCorners` / `Cv2.FindCirclesGrid`（标定点检测）
- `Cv2.CornerSubPix`（棋盘格角点亚像素优化）
- `Cv2.CalibrateCamera`（求解 `CameraMatrix` 与 `DistCoeffs`）
- `JsonSerializer.Serialize` + `File.WriteAllText`（输出标定 JSON）
- `Cv2.DrawChessboardCorners` / `Cv2.PutText`（结果可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `PatternType` | `enum` | Chessboard | - | - |
| `BoardWidth` | `int` | 9 | [2, 30] | - |
| `BoardHeight` | `int` | 6 | [2, 30] | - |
| `SquareSize` | `double` | 25 | [0.1, 1000] | - |
| `Mode` | `enum` | SingleImage | - | - |
| `ImageFolder` | `string` | "" | - | - |
| `CalibrationOutputPath` | `string` | calibration_result.json | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `CalibrationData` | 标定数据 | `String` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 `O(N * P * I)`，`N` 为有效图像数，`P` 为角点数，`I` 为迭代求解轮次 |
| 典型耗时 (Typical Latency) | 单图模式约 `20-120 ms`；文件夹模式约 `0.3-3 s`（取决于样本数量与角点质量） |
| 内存特征 (Memory Profile) | 需缓存多组角点与物理点，约 `O(N * P)`，另外包含若干中间 `Mat` |

## 适用场景 / Use Cases
- 适合 (Suitable)：离线相机标定、镜头更换后重标定、需要输出可复用 `CameraMatrix/DistCoeffs` 的产线场景。
- 不适合 (Not Suitable)：无稳定标定板、样本少于 3 张（文件夹模式）、图像模糊/过曝导致角点检测不稳定的场景。

## 已知限制 / Known Limitations
1. 文件夹模式至少需要 3 张成功检测到角点的图像，否则直接失败。
2. 当前使用标准 `CalibrateCamera` 流程，不包含更高阶畸变模型选择与鲁棒外点剔除。
3. 角点顺序与板型参数（宽高、方格尺寸）配置错误会直接影响标定精度。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |