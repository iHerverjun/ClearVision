# 手眼标定 / HandEyeCalibration

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `HandEyeCalibrationOperator` |
| 枚举值 (Enum) | `OperatorType.HandEyeCalibration` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Solves eye-in-hand or simplified eye-to-hand calibration from robot poses and calibration-board poses.。
> English: Solves eye-in-hand or simplified eye-to-hand calibration from robot poses and calibration-board poses..

## 实现策略 / Implementation Strategy
- 中文：
  - `eye_in_hand` 模式使用 `OpenCvSharp.Cv2.CalibrateHandEye` 求解，并输出 `CameraToToolMatrix`。
  - `eye_to_hand` 提供轻量级可运行版本，默认假设标定板坐标系与工具坐标系重合，输出 `CameraToBaseMatrix`。
  - 求解完成后会自动联动验证模块，产出一致性误差、质量分级、HTML 报告和推荐复核姿态。
- English:
  - `eye_in_hand` uses `OpenCvSharp.Cv2.CalibrateHandEye` and returns a `CameraToToolMatrix`.
  - `eye_to_hand` provides a lightweight assumption-driven variant where the target frame is assumed to coincide with the tool frame, returning `CameraToBaseMatrix`.
  - Validation is executed immediately after solving to produce consistency metrics, quality grade, HTML report, and suggested verification poses.

## 核心 API 调用链 / Core API Call Chain
- `OpenCvSharp.Cv2.CalibrateHandEye`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CalibrationType` | `enum` | eye_in_hand | - | - |
| `Method` | `enum` | TSAI | - | - |
| `CameraMatrix` | `string` | "" | - | - |
| `DistortionCoeffs` | `string` | "" | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `RobotPoses` | Robot Poses | `Any` | Yes | - |
| `CalibrationBoardPoses` | Calibration Board Poses | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `HandEyeMatrix` | Hand Eye Matrix | `Any` | - |
| `InverseHandEyeMatrix` | Inverse Hand Eye Matrix | `Any` | - |
| `ReprojectionError` | Reprojection Error | `Float` | - |
| `CalibrationQuality` | Calibration Quality | `String` | - |
| `MatrixConvention` | Matrix Convention | `String` | - |
| `HtmlReport` | HTML Report | `String` | - |
| `Suggestions` | Suggestions | `Any` | - |
| `SuggestedValidationPoses` | Suggested Validation Poses | `String` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(N) |
| 典型耗时 (Typical Latency) | ~?ms (1920x1080) |
| 内存特征 (Memory Profile) | O(N) |

## 适用场景 / Use Cases
- 适合 (Suitable)：机器人抓取引导、相机装在末端执行器的视觉定位、离线标定数据回放验证。
- 不适合 (Not Suitable)：多相机联合标定、带复杂柔性末端执行器的在线自适应标定。

## 已知限制 / Known Limitations
1. `eye_to_hand` 当前基于“标定板坐标系与工具坐标系重合”的简化假设，更复杂场景需扩展外参链路。
1. `CameraMatrix` 与 `DistortionCoeffs` 参数目前保留为接口兼容位，核心解算依赖输入位姿而非图像角点检测。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
