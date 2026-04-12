# 语义分割 / SemanticSegmentation

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `SemanticSegmentationOperator` |
| 枚举值 (Enum) | `OperatorType.SemanticSegmentation` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：Runs an ONNX semantic segmentation model and returns class map, colored visualization, and per-class masks.。
> English: Runs an ONNX semantic segmentation model and returns class map, colored visualization, and per-class masks..

## 实现策略 / Implementation Strategy
- 中文：
  - 支持 `ModelPath` 直接加载 ONNX，也支持通过 `ModelId + ModelCatalogPath` 从 `models/model_catalog.json` 解析模型仓库。
  - 当 `InputSize / NumClasses / ClassNames` 仍处于默认值时，会优先回填模型仓库中的元数据，减少重复配置。
  - 推理阶段输出类别图、着色图和逐类掩码，便于后续测量、逻辑判定或结果存档。
  - `ExecutionProvider` 支持 `cpu/cuda`；在未检测到 CUDA 时自动回退到 CPU，避免流程直接失败。
- English:
  - Supports direct ONNX loading by `ModelPath` and repository-driven resolution by `ModelId + ModelCatalogPath`.
  - Repository metadata can hydrate input size, class count, and class names when operator parameters remain at defaults.
  - Produces class map, colorized visualization, and per-class masks for downstream measurement and decision steps.
  - `ExecutionProvider` supports `cpu/cuda` with automatic CPU fallback when CUDA is unavailable.

## 核心 API 调用链 / Core API Call Chain
- `SemanticSegmentationOperator.ExecuteCoreAsync`
- `ModelCatalog.ResolveExplicitOrCatalogPath(...)`
- `SemanticSegmentationOperator.GetOrCreateSessionAsync(...)`
- `InferenceSession.Run(...)`
- `BuildColorizedMap(...) / BuildClassMasks(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ModelId` | `string` | "" | - | - |
| `ModelCatalogPath` | `file` | "" | - | - |
| `ModelPath` | `file` | "" | - | - |
| `InputSize` | `string` | 512,512 | - | Width,Height |
| `NumClasses` | `int` | 21 | [2, 4096] | - |
| `ClassNames` | `string` | "" | - | JSON array or comma-separated names |
| `ExecutionProvider` | `enum` | cpu | - | - |
| `ScaleToUnitRange` | `bool` | true | - | - |
| `ChannelOrder` | `enum` | RGB | - | - |
| `Mean` | `string` | 0,0,0 | - | - |
| `Std` | `string` | 1,1,1 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Image | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `SegmentationMap` | Segmentation Map | `Image` | - |
| `ColoredMap` | Colored Map | `Image` | - |
| `ClassMasks` | Class Masks | `Any` | - |
| `ClassCount` | Class Count | `Integer` | - |
| `PresentClasses` | Present Classes | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(?) |
| 典型耗时 (Typical Latency) | ~?ms (1920x1080) |
| 内存特征 (Memory Profile) | ? |

## 适用场景 / Use Cases
- 适合 (Suitable)：对工件区域进行前景/背景分离、涂胶范围检查、语义区域裁切、后续 ROI 联动。
- 不适合 (Not Suitable)：需要实例级别目标分离、跟踪 ID 或复杂后处理的场景；这类需求更适合实例分割或检测模型。

## 已知限制 / Known Limitations
1. 当前实现以单输出张量的典型语义分割模型为主，复杂多分支输出需要在模型侧对齐。
1. GPU 路径依赖部署环境已安装对应 ONNX Runtime CUDA 运行时；仓库默认包仍以 CPU 运行时为主。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-03-17 | 自动生成文档骨架 / Generated skeleton |
