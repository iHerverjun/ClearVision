# Planar Matching / PlanarMatching

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `PlanarMatchingOperator` |
| 枚举值 (Enum) | `OperatorType.PlanarMatching` |
| 分类 (Category) | 匹配定位 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Feature-based planar matching with homography verification. Suitable for textured planar targets under perspective change.。
> English: Feature-based planar matching with homography verification. Suitable for textured planar targets under perspective change..

## 实现策略 / Implementation Strategy
> 中文：TODO：补充实现策略与方案对比。
> English: TODO: Add implementation strategy and alternatives comparison.

## 核心 API 调用链 / Core API Call Chain
- TODO：补充关键 API 调用链

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `TemplatePath` | `file` | "" | - | - |
| `DetectorType` | `enum` | ORB | - | - |
| `MaxFeatures` | `int` | 1000 | [100, 5000] | - |
| `ScaleFactor` | `double` | 1.2 | [1.01, 2] | - |
| `NLevels` | `int` | 8 | [1, 16] | - |
| `MatchRatio` | `double` | 0.75 | [0.5, 0.95] | - |
| `RansacThreshold` | `double` | 3 | [0.5, 10] | - |
| `MinMatchCount` | `int` | 10 | [4, 100] | - |
| `MinInliers` | `int` | 8 | [4, 100] | - |
| `MinInlierRatio` | `double` | 0.25 | [0.1, 1] | - |
| `ScoreThreshold` | `double` | 0.5 | [0, 1] | - |
| `UseRoi` | `bool` | false | - | - |
| `RoiX` | `int` | 0 | - | - |
| `RoiY` | `int` | 0 | - | - |
| `RoiWidth` | `int` | 0 | - | - |
| `RoiHeight` | `int` | 0 | - | - |
| `EnableMultiScale` | `bool` | true | - | - |
| `ScaleRange` | `double` | 0.2 | [0, 1] | - |
| `EnableEarlyExit` | `bool` | false | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Search Image | `Image` | Yes | - |
| `Template` | Template Image | `Image` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Result Image | `Image` | - |
| `IsMatch` | Is Match | `Boolean` | - |
| `Score` | Score | `Float` | - |
| `MatchCount` | Match Count | `Integer` | - |
| `Method` | Method | `String` | - |
| `FailureReason` | Failure Reason | `String` | - |
| `CandidateScore` | Candidate Score | `Float` | - |
| `InlierCount` | Inlier Count | `Integer` | - |
| `InlierRatio` | Inlier Ratio | `Float` | - |
| `VerificationPassed` | Verification Passed | `Boolean` | - |
| `MatchResult` | Match Result | `Any` | - |
| `Homography` | Homography Matrix | `Any` | - |
| `Corners` | Detected Corners | `PointList` | - |

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
| 1.1.1 | 2026-04-09 | 自动生成文档骨架 / Generated skeleton |
