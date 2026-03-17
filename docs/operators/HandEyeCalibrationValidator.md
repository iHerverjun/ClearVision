# 手眼标定验证 / HandEyeCalibrationValidator

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `HandEyeCalibrationValidatorOperator` |
| 枚举值 (Enum) | `OperatorType.HandEyeCalibrationValidator` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Validates a hand-eye calibration matrix and produces quality metrics, HTML report, and pose suggestions.。
> English: Validates a hand-eye calibration matrix and produces quality metrics, HTML report, and pose suggestions..

## 实现策略 / Implementation Strategy
- 中文：
  - 将所有样本统一投影到静态参考系中，对比平移和旋转一致性，生成 `good/fair/poor` 评级。
  - 输出 HTML 报告、改进建议和推荐验证姿态，便于离线审阅与现场复核。
  - 与标定算子解耦后，可单独用于回放历史标定结果或批量验收。
- English:
  - Projects all samples into a common static reference frame, then scores translation and rotation consistency to produce a `good/fair/poor` grade.
  - Emits HTML report, improvement suggestions, and recommended validation poses for offline review and shop-floor verification.
  - Can be used independently to replay and validate historical calibration results.

## 核心 API 调用链 / Core API Call Chain
- `Pose consistency over static-reference transforms`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CalibrationType` | `enum` | eye_in_hand | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `RobotPoses` | Robot Poses | `Any` | Yes | - |
| `CalibrationBoardPoses` | Calibration Board Poses | `Any` | Yes | - |
| `HandEyeMatrix` | Hand Eye Matrix | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `MeanError` | Mean Error | `Float` | - |
| `MaxError` | Max Error | `Float` | - |
| `MeanRotationError` | Mean Rotation Error | `Float` | - |
| `Quality` | Quality | `String` | - |
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
- 适合 (Suitable)：标定结果复核、批量对比不同采样批次、自动生成交付报告。
- 不适合 (Not Suitable)：直接替代图像角点级重投影分析；若需要像素级误差，应在角点检测链路继续细化。

## 已知限制 / Known Limitations
1. 当前质量指标以位姿一致性为主，适合流程级验收，不直接等价于图像级 reprojection RMS。
1. 推荐验证姿态为通用覆盖模板，现场仍应结合工位可达性做二次筛选。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
