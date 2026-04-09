# PPF表面匹配 / PPFMatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PPFMatchOperator` |
| 枚举值 (Enum) | `OperatorType.PPFMatch` |
| 分类 (Category) | 3D |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Simplified PPF-based 3D coarse surface matching (model -> scene pose). Intended for coarse pose alignment diagnostics.。
> English: Simplified PPF-based 3D coarse surface matching (model -> scene pose). Intended for coarse pose alignment diagnostics..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `NormalRadius` | `double` | 0.03 | >= 1E-06 | - |
| `FeatureRadius` | `double` | 0.08 | >= 1E-06 | - |
| `NumSamples` | `int` | 120 | [10, 5000] | - |
| `ModelRefStride` | `int` | 3 | [1, 50] | - |
| `Seed` | `int` | 123 | [-1, 2147483647] | -1 = random sampling (non-deterministic). |
| `RansacIterations` | `int` | 800 | [50, 100000] | - |
| `InlierThreshold` | `double` | 0.005 | >= 1E-06 | - |
| `MinInliers` | `int` | 80 | [3, 1000000] | - |
| `DistanceStep` | `double` | 0.01 | >= 1E-06 | - |
| `AngleStepDeg` | `double` | 5 | [0.1, 90] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `ModelPointCloud` | Model Point Cloud | `Any` | Yes | - |
| `ScenePointCloud` | Scene Point Cloud | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsMatch` | Is Match | `Boolean` | - |
| `IsMatched` | Is Matched | `Boolean` | - |
| `Score` | Score | `Float` | - |
| `MatchCount` | Match Count | `Integer` | - |
| `Method` | Method | `String` | - |
| `FailureReason` | Failure Reason | `String` | - |
| `VerificationPassed` | Verification Passed | `Boolean` | - |
| `AmbiguityDetected` | Ambiguity Detected | `Boolean` | - |
| `AmbiguityScore` | Ambiguity Score | `Float` | - |
| `StabilityScore` | Stability Score | `Float` | - |
| `NormalConsistency` | Normal Consistency | `Float` | - |
| `TransformMatrix` | Transform Matrix | `Any` | - |
| `InlierCount` | Inlier Count | `Integer` | - |
| `InlierRatio` | Inlier Ratio | `Float` | - |
| `CorrespondenceCount` | Correspondence Count | `Integer` | - |
| `RmsError` | RMS Error | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(?) |
| 典型耗时 (Typical Latency) | ~?ms (1920x1080) |
| 内存特征 (Memory Profile) | ? |

## 适用场景 / Use Cases
- 适合 (Suitable)：TODO
- 不适合 (Not Suitable)：TODO

## 已知限制 / Known Limitations
1. TODO

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.4 | 2026-04-09 | 自动生成文档骨架 / Generated skeleton |
