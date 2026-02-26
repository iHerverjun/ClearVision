# Modbus通信 / ModbusCommunication

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `ModbusCommunicationOperator` |
| 枚举值 (Enum) | `OperatorType.ModbusCommunication` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：通过 Modbus 主站模式对线圈/寄存器执行读写，包括读线圈、读保持寄存器、写单寄存器、写多寄存器。
> English: Executes Modbus master read/write operations for coils and holding registers (read coils/holding, write single/multiple).

## 实现策略 / Implementation Strategy
> 中文：TCP 模式使用连接池复用 `TcpClient + IModbusMaster`；按功能码分支执行对应 NModbus API；RTU 模式在当前版本预留但未落地。
> English: TCP mode reuses pooled `TcpClient + IModbusMaster`, dispatches by function code to NModbus APIs; RTU mode is reserved but not implemented yet.

## 核心 API 调用链 / Core API Call Chain
- `ModbusFactory.CreateMaster(client)`
- `ReadCoils` / `ReadHoldingRegisters`
- `WriteSingleRegister` / `WriteMultipleRegisters`
- 连接池：`ConcurrentDictionary` + 活性检查

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Protocol` | `enum` | TCP | - | - |
| `IpAddress` | `string` | 192.168.1.1 | - | - |
| `Port` | `int` | 502 | [1, 65535] | - |
| `SlaveId` | `int` | 1 | [1, 247] | - |
| `RegisterAddress` | `int` | 0 | - | - |
| `RegisterCount` | `int` | 1 | [1, 125] | - |
| `FunctionCode` | `enum` | ReadHolding | - | - |
| `WriteValue` | `string` | "" | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | 数据 | `Any` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Response` | 响应 | `String` | - |
| `Status` | 状态 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(R)`（`R` 为寄存器或线圈数量） |
| 典型耗时 (Typical Latency) | 局域网设备常见 `3-50 ms` |
| 内存特征 (Memory Profile) | 连接池 + 小型寄存器数组 |

## 适用场景 / Use Cases
- 适合 (Suitable)：与 Modbus TCP PLC/仪表交换状态与配方参数。
- 不适合 (Not Suitable)：必须使用 Modbus RTU 的现场总线链路（当前未实现）。

## 已知限制 / Known Limitations
1. `Protocol=RTU` 仅有占位提示，未实现实际串口 RTU 读写。
2. 写多寄存器解析遇到非法值会按 `0` 处理，需上游先校验。
3. NModbus API 为同步调用，长时阻塞设备可能影响线程占用。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |