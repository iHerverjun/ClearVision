# 测量 / MeasureDistance

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MeasureDistanceOperator` |
| 枚举值 (Enum) | `OperatorType.Measurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：优先读取 `PointA/PointB` 进行无图测距；若未提供则回退到参数坐标。支持三种测量：点到点欧式距离、水平距离 `|Δx|`、垂直距离 `|Δy|`。
> English: Prefer `PointA/PointB` for image-free measurement; otherwise use configured coordinates. Supports Euclidean point-to-point distance, horizontal distance `|Δx|`, and vertical distance `|Δy|`.

## 实现策略 / Implementation Strategy
> 中文：采用双输入模式（点输入优先 + 图像回退），便于在流程中既能独立做数值计算，也能叠加可视化输出。
> English: Dual input mode (point-priority with image fallback) enables both pure numeric measurement and optional visual overlay in one operator.

## 核心 API 调用链 / Core API Call Chain
- `TryParsePoint`（解析 Point 输入）
- `Math.Sqrt/Math.Abs`（距离计算）
- `Cv2.Line` / `Cv2.Circle` / `Cv2.PutText`（图像模式可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `X1` | `int` | 0 | - | - |
| `Y1` | `int` | 0 | - | - |
| `X2` | `int` | 100 | - | - |
| `Y2` | `int` | 100 | - | - |
| `MeasureType` | `enum` | PointToPoint | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | No | - |
| `PointA` | 起点 | `Point` | No | - |
| `PointB` | 终点 | `Point` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Distance` | 测量距离 | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(1) |
| 典型耗时 (Typical Latency) | <0.3 ms |
| 内存特征 (Memory Profile) | 常数级，图像模式含一次结果图拷贝 |

## 适用场景 / Use Cases
- 适合 (Suitable)：已知点位间距、流程中的快速尺寸判定与规则计算。
- 不适合 (Not Suitable)：需要自动边缘提取或亚像素拟合的高精度场景。

## 已知限制 / Known Limitations
1. 输出单位为像素，不包含标定后的物理单位换算。
2. `Horizontal/Vertical` 模式会约束一个轴向，不反映真实斜向距离。
3. 点输入格式需符合约定（`Point` 或 `(x,y)` 字符串）。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
