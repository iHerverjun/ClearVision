# 串口通信 / SerialCommunication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `SerialCommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.SerialCommunication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于 `SerialPort` 完成串口收发，支持文本与 HEX 编解码，适配 RS-232/RS-485 设备通信。
> English: Uses `SerialPort` for serial I/O with text/HEX encoding support for RS-232/RS-485 device communication.

## 实现策略 / Implementation Strategy
> 中文：每次执行按参数创建串口并打开连接，发送数据后短暂等待设备响应，再按编码规则读取返回内容并输出。
> English: Opens a serial connection per execution, writes outgoing data, waits briefly for response, decodes received bytes, and returns structured outputs.

## 核心 API 调用链 / Core API Call Chain
- `new SerialPort(...)` + `Open()`
- 发送：`port.Write(...)`
- 接收：`port.BytesToRead` + `port.Read(...)`
- HEX 解析/格式化：`Convert.ToByte(...,16)` + `BitConverter.ToString`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `PortName` | `string` | COM1 | - | - |
| `BaudRate` | `enum` | 9600 | - | - |
| `DataBits` | `int` | 8 | [5, 8] | - |
| `StopBits` | `enum` | One | - | - |
| `Parity` | `enum` | None | - | - |
| `SendData` | `string` | "" | - | - |
| `Encoding` | `enum` | UTF8 | - | - |
| `TimeoutMs` | `int` | 3000 | [100, 30000] | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | 发送数据 | `Any` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Response` | 接收数据 | `Any` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 串口 I/O 主导，约 `O(P)` |
| 典型耗时 (Typical Latency) | 常见 `5-100 ms`（取决于波特率、设备处理时间） |
| 内存特征 (Memory Profile) | 小型收发缓冲，内存占用低 |

## 适用场景 / Use Cases
- 适合 (Suitable)：串口 PLC、扫码枪、仪表等设备数据采集与指令交互。
- 不适合 (Not Suitable)：高吞吐持续流式通信或复杂帧重组协议场景。

## 已知限制 / Known Limitations
1. 当前实现按调用打开/关闭串口，不提供长连接复用与并发仲裁。
2. 接收前固定 `Sleep(100ms)`，可能导致慢设备丢帧或快设备额外等待。
3. 不包含 CRC/校验帧组包逻辑，协议完整性需上层保证。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |