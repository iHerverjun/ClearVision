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
> 中文：将输入消息组织为字符串负载并发布到指定 MQTT 主题，返回发布状态与元数据。
> English: Packs input payload into a message and publishes it to a target MQTT topic, returning publish status metadata.

## 实现策略 / Implementation Strategy
> 中文：消息优先取输入 `Message`，否则序列化输入字典；参数控制 Broker/Topic/QoS/超时；当前版本通过占位实现模拟发布，保留后续接入 MQTT 客户端库接口。
> English: Resolves message from `Message` input or serialized inputs, uses broker/topic/qos/timeout parameters, and currently runs a stub publish path pending real MQTT client integration.

## 核心 API 调用链 / Core API Call Chain
- 参数解析：`Broker/Port/Topic/QoS/Retain/TimeoutMs`
- 负载构建：字符串读取或 `JsonSerializer.Serialize`
- `PublishAsync(...)`（当前为模拟实现，`Task.Delay`）
- 输出：`Success/Broker/Topic/MessageLength`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Broker` | `string` | localhost | - | - |
| `Port` | `int` | 1883 | - | - |
| `Topic` | `string` | cv/results | - | - |
| `Qos` | `int` | 1 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Payload` | 消息负载 | `Any` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `IsSuccess` | 是否成功 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 当前实现近似 `O(P)`（`P` 为消息长度） |
| 典型耗时 (Typical Latency) | 当前占位实现约 `<1 ms` 到数毫秒；真实网络发布取决于 Broker |
| 内存特征 (Memory Profile) | 主要为消息字符串与序列化缓冲 |

## 适用场景 / Use Cases
- 适合 (Suitable)：流程联调阶段的 MQTT 发布占位、IoT 通道接口预留。
- 不适合 (Not Suitable)：需要真实 MQTT QoS 交付保证的生产环境直连。

## 已知限制 / Known Limitations
1. 当前 `PublishAsync` 为模拟逻辑，未真正连接 MQTT Broker。
2. 输入端口定义为 `Payload`，代码读取键优先使用 `Message`，配置需统一。
3. 未实现断线重连、会话保持、认证与 TLS 配置。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |