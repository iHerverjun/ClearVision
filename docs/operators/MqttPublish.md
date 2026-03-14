# MQTT 发布 / MqttPublish

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `MqttPublishOperator` |
| 枚举值 (Enum) | `OperatorType.MqttPublish` |
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
2. `JsonSerializer.Serialize`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Broker` | `string` | `"localhost"` | - | 控制“Broker”这一实现参数，建议结合现场样本调节。 |
| `Port` | `int` | `1883` | - | 通信端口。 |
| `Topic` | `string` | `"cv/results"` | - | 控制“Topic”这一实现参数，建议结合现场样本调节。 |
| `Qos` | `int` | `1` | - | 控制“Qos”这一实现参数，建议结合现场样本调节。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Payload` | 消息负载 | `Any` | Yes | 提供通信请求所需输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsSuccess` | 是否成功 | `Boolean` | 输出通信状态或响应结果。 |
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
1. 参数 `Qos` 已在元数据中声明，但从源码看当前没有明显被执行逻辑实际使用。
2. 声明输出 `IsSuccess` 与当前运行时附加字段不完全一致，集成时应以实际输出字典为准。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
