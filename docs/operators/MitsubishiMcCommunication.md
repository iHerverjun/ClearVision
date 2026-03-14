# 三菱MC通信 / MitsubishiMcCommunication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MitsubishiMcCommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.MitsubishiMcCommunication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子不执行图像算法，而是把流程参数映射为通信请求，与外部设备或服务完成读写和响应解析。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 核心流程偏向协议适配：建立连接、发送请求、解析响应、输出状态。

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `IpAddress` | `string` | `"192.168.3.1"` | - | 控制“IpAddress”这一实现参数，建议结合现场样本调节。 |
| `Port` | `int` | `5002` | [1, 65535] | 通信端口。 |
| `Address` | `string` | `"D100"` | - | 控制“Address”这一实现参数，建议结合现场样本调节。 |
| `Length` | `int` | `1` | [1, 960] | 控制“Length”这一实现参数，建议结合现场样本调节。 |
| `DataType` | `enum` | `"Word"` | Bit/位 (Bool)；Word/字 (Word/UInt16)；Int16/短整型 (Int16)；DWord/双字 (DWord/UInt32)；Int32/整型 (Int32)；Float/浮点 (Float) | 操作类型或转换类型。 |
| `Operation` | `enum` | `"Read"` | Read/读取；Write/写入 | 该参数用于在多个实现分支之间切换。 |
| `WriteValue` | `string` | `""` | - | 控制“WriteValue”这一实现参数，建议结合现场样本调节。 |
| `PollingMode` | `enum` | `"None"` | None/不等待；WaitForValue/等待指定值 | 读取时是否启用轮询等待 |
| `PollingCondition` | `enum` | `"Equal"` | Equal/等于；NotEqual/不等于；GreaterThan/大于；LessThan/小于；GreaterOrEqual/大于等于；LessOrEqual/小于等于 | 等待的条件类型 |
| `PollingValue` | `string` | `"1"` | - | 等待的目标值（如触发信号值） |
| `PollingTimeout` | `int` | `30000` | [100, 300000] | 最长等待时间（毫秒） |
| `PollingInterval` | `int` | `50` | [10, 5000] | 每次读取间隔（毫秒） |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | 数据 | `Any` | No | 提供通信请求所需输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Response` | 响应 | `String` | 输出通信状态或响应结果。 |
| `Status` | 状态 | `Boolean` | 输出通信状态或响应结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | CPU 开销通常较小，整体耗时主要由设备、网络或协议往返决定。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合与 PLC、串口设备、TCP 服务或 HTTP 接口联动。
- 适合把视觉结果写入外部控制系统或读取工艺参数。
- 不适合把通信成功直接等同于业务成功。
- 不适合忽略网络与设备延迟。

## 已知限制 / Known Limitations
1. 声明输出 `Response` 与当前运行时附加字段不完全一致，集成时应以实际输出字典为准。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
