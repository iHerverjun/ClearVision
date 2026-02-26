# 形态学操作 / MorphologicalOperation

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MorphologicalOperationOperator` |
| 枚举值 (Enum) | `OperatorType.MorphologicalOperation` |
| 分类 (Category) | 预处理 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于结构元素执行形态学操作（腐蚀、膨胀、开闭、梯度、顶帽、黑帽），用于形状级噪声处理与区域结构增强。
> English: Applies morphology operations (erode/dilate/open/close/gradient/tophat/blackhat) with configurable structuring elements.

## 实现策略 / Implementation Strategy
> 中文：主算子只负责参数解析与校验，具体操作映射与执行委托给 `MorphologyExecutionHelper`；辅助类统一完成形状/操作归一与 `Cv2.MorphologyEx` 调用。
> English: The operator validates and forwards parameters, while `MorphologyExecutionHelper` normalizes operation/shape names and executes the actual OpenCV call.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`Operation`、`KernelShape`、`KernelWidth/Height`、`Iterations`、`AnchorX/Y`
- `MorphologyExecutionHelper.ParseShape/ParseOperation`
- `Cv2.GetStructuringElement`
- `Cv2.MorphologyEx`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Operation` | `enum` | Close | - | - |
| `KernelShape` | `enum` | Rect | - | - |
| `KernelWidth` | `int` | 3 | [1, 51] | - |
| `KernelHeight` | `int` | 3 | [1, 51] | - |
| `Iterations` | `int` | 1 | [1, 10] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 处理后图像 | `Image` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 近似 `O(W*H*K_w*K_h*Iter)` |
| 典型耗时 (Typical Latency) | 约 `0.3-5 ms`（1920x1080，小核到中核） |
| 内存特征 (Memory Profile) | 一张输出图 + 小型结构元素矩阵 |

## 适用场景 / Use Cases
- 适合 (Suitable)：去毛刺、连通小断点、孔洞填补、轮廓增强和前景提纯。
- 不适合 (Not Suitable)：灰度细节保真要求高、形态学参数难统一的复杂纹理场景。

## 已知限制 / Known Limitations
1. 参数配置不当（核过大或迭代过多）会快速吞噬细小特征。
2. 当前未暴露边界类型与边界值设置，边界区域行为固定。
3. 操作统一走 `MorphologyEx`，未针对特殊场景做算子级优化分支。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P1 文档补全 / Completed Phase 2.3 P1 documentation enrichment |