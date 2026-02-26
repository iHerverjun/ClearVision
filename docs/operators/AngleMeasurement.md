# 角度测量 / AngleMeasurement

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `AngleMeasurementOperator` |
| 枚举值 (Enum) | `OperatorType.AngleMeasurement` |
| 分类 (Category) | 检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：以点2为顶点，分别构建向量 `p1-p2` 与 `p3-p2`，通过 `atan2` 计算夹角并归一化到 `[0, π]`，根据 `Unit` 输出度或弧度。
> English: Use point-2 as vertex, form vectors `p1-p2` and `p3-p2`, compute angle by `atan2`, normalize to `[0, π]`, and output in degree or radian per `Unit`.

## 实现策略 / Implementation Strategy
> 中文：采用纯几何计算路径，避免额外图像处理步骤，计算确定性强、延迟极低；图像仅用于可视化测量关系。
> English: A pure geometric path is used for deterministic and ultra-low-latency computation. Image operations are only for visualization.

## 核心 API 调用链 / Core API Call Chain
- 向量构建与 `Math.Atan2`
- 角度归一化与单位换算（Degree/Radian）
- `Cv2.Circle` / `Cv2.Line` / `Cv2.PutText`（可视化）

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Point1X` | `int` | 0 | - | - |
| `Point1Y` | `int` | 0 | - | - |
| `Point2X` | `int` | 100 | - | - |
| `Point2Y` | `int` | 100 | - | - |
| `Point3X` | `int` | 200 | - | - |
| `Point3Y` | `int` | 0 | - | - |
| `Unit` | `enum` | Degree | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Angle` | 角度 | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(1) |
| 典型耗时 (Typical Latency) | <0.3 ms（不含上游点位提取） |
| 内存特征 (Memory Profile) | 常数级，主要为结果图拷贝 |

## 适用场景 / Use Cases
- 适合 (Suitable)：已知关键点的夹角判定、装配姿态校验。
- 不适合 (Not Suitable)：需要自动找点/找线的场景（本算子不负责特征提取）。

## 已知限制 / Known Limitations
1. 点位输入质量决定结果精度，不包含亚像素角点优化。
2. 默认输出较小夹角（<=180°），不适用于反向角场景表达。
3. 未进行物理标定补偿，结果默认为像素坐标几何角度。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P0 文档补全 / Completed Phase 2.3 P0 documentation enrichment |
