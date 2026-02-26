# 结果输出 / ResultOutput

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ResultOutputOperator` |
| 枚举值 (Enum) | `OperatorType.ResultOutput` |
| 分类 (Category) | 输出 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：作为流程末端的透传汇聚节点，将输入结果打包为输出字典并向下游/终端返回。
> English: Works as a terminal pass-through aggregator, collecting input fields into output payload for downstream/end-of-flow consumption.

## 实现策略 / Implementation Strategy
> 中文：对输入字典逐键透传，显式优先处理 `Image/Result`，并对 `ImageWrapper` 执行 `AddRef()` 以避免生命周期管理提前释放。算子本体不做格式化序列化与文件落盘，仅负责数据交付。
> English: Passes through all input key-values, handles `Image/Result` explicitly, and calls `AddRef()` for `ImageWrapper` to preserve lifecycle safety. No built-in serialization or file persistence is performed.

## 核心 API 调用链 / Core API Call Chain
- 输入检测：`inputs?.TryGetValue("Image"|"Result")`
- 值保护：`PreserveOutputValue(value)`
- 图像引用保护：`ImageWrapper.AddRef()`
- 全量透传：遍历 `inputs` 并补充未写入键
- 返回：`OperatorExecutionOutput.Success(output)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Format` | `enum` | JSON | - | 元数据定义的输出格式选项（当前执行逻辑未使用） |
| `SaveToFile` | `bool` | true | - | 元数据定义的是否保存文件开关（当前执行逻辑未使用） |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 图像 | `Image` | No | 图像输出（会保护引用计数） |
| `Result` | 结果 | `Any` | No | 结构化结果对象 |
| `Text` | 文本 | `String` | No | 文本结果 |
| `Data` | 数据 | `Any` | No | 其他数据载荷 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Output` | 输出 | `Any` | 透传后的结果字典 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(K)`（`K` 为输入键数量） |
| 典型耗时 (Typical Latency) | 常见 `<0.2 ms`（不含上游算子时间） |
| 内存特征 (Memory Profile) | 新建输出字典并复制键值引用；图像对象采用引用计数保护 |

## 适用场景 / Use Cases
- 适合 (Suitable)：流程末端汇总输出、调试链路结果透传、统一对接 UI/结果总线。
- 不适合 (Not Suitable)：需要强格式化导出（CSV/JSON 文件）或落盘归档的场景。

## 已知限制 / Known Limitations
1. `Format/SaveToFile` 仅存在于参数元数据，当前执行逻辑未实际使用。
2. 输出端口元数据声明为单个 `Output`，但运行时返回的是动态键字典（`Image/Result/...`）。
3. 算子不做内容校验与格式转换，输入质量由上游保证。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
