# 标定加载 / CalibrationLoader

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CalibrationLoaderOperator` |
| 枚举值 (Enum) | `OperatorType.CalibrationLoader` |
| 分类 (Category) | 标定 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Loads calibration data from JSON/XML/YAML file.。
> English: Loads calibration data from JSON/XML/YAML file..

## 实现策略 / Implementation Strategy
> 中文：采用“输入校验 -> 参数解析 -> 核心算法执行 -> 结果封装”的统一链路，优先复用 OpenCV 与现有算子基类能力，确保与主项目运行时行为一致。
> English: Uses a consistent pipeline of input validation, parameter parsing, core algorithm execution, and result packaging, reusing OpenCV and existing operator infrastructure for runtime consistency.

## 核心 API 调用链 / Core API Call Chain
- 输入图像与参数校验
- 核心视觉处理链路执行
- 结果图像/结构化结果输出

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FilePath` | `file` | "" | - | - |
| `FileFormat` | `enum` | JSON | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| - | - | - | - | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `TransformMatrix` | Transform Matrix | `Any` | - |
| `CameraMatrix` | Camera Matrix | `Any` | - |
| `DistCoeffs` | Dist Coeffs | `Any` | - |
| `PixelSize` | Pixel Size | `Float` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(N)` 到 `O(N*I)`（样本点数与迭代次数相关） |
| 典型耗时 (Typical Latency) | 离线标定秒级；在线应用通常 `<5 ms` |
| 内存特征 (Memory Profile) | 主要为标定参数矩阵与点集缓存，额外开销较小 |

## 适用场景 / Use Cases
- 适合 (Suitable)：相机内外参建立、像素到物理坐标映射、畸变矫正。
- 不适合 (Not Suitable)：样本点数量不足或采样覆盖不均的快速一次性标定。

## 已知限制 / Known Limitations
1. 当前实现遵循统一输入/输出协议，输入类型、维度或关键字段不符合约定时会返回失败。
2. 标定质量受样本覆盖度影响明显，建议覆盖全视场并分布均匀。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.5 文档补全 / Completed Phase 2.5 documentation enrichment |
