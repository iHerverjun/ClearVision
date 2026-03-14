# Text Save / TextSave

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TextSaveOperator` |
| 枚举值 (Enum) | `OperatorType.TextSave` |
| 分类 (Category) | Logic Tools |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子负责把流程结果写入文件、数据库或外部系统，或从外围资源获取输入。

> English: This section is completed from the current source implementation and focuses on actual runtime behavior in code.

## 实现策略 / Implementation Strategy
- 实现遵循统一算子框架：参数读取、输入检查、核心处理与结果封装相互分离。
- 实现中存在状态缓存或共享资源，使用时需要关注实例生命周期、缓存一致性与并发访问。

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam / GetBoolParam / GetFloatParam`
2. `JsonSerializer.Serialize`
3. `File.AppendAllText`
4. `File.WriteAllText`
5. `Directory.CreateDirectory`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FilePath` | `file` | `"output_{date}_{time}.txt"` | - | 文件或资源路径。 |
| `Format` | `enum` | `"Text"` | Text/Text；CSV/CSV；JSON/JSON | 该参数用于在多个实现分支之间切换。 |
| `AppendMode` | `bool` | `true` | - | 工作模式选择。 |
| `AddTimestamp` | `bool` | `true` | - | 控制“AddTimestamp”这一实现参数，建议结合现场样本调节。 |
| `Encoding` | `enum` | `"UTF8"` | UTF8/UTF8；GBK/GBK | 文本编码。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | Data | `Any` | No | 提供算法执行所需输入。 |
| `Text` | Text | `String` | No | 提供算法执行所需输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `FilePath` | File Path | `String` | 输出本算子的处理结果。 |
| `Success` | Success | `Boolean` | 输出本算子的处理结果。 |
## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 多数路径近似随输入规模线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；实际延迟受图像尺寸、参数规模、缓存命中率和外部依赖影响。 |
| 内存特征 (Memory Profile) | 主要由中间结果、缓存结构和输出封装决定。 |

## 适用场景 / Use Cases
- 适合文件、结果、采集和外部存储交互。
- 适合把流程结果落盘、输出或接入外围系统。
- 不适合忽略路径、权限和资源可用性检查。
- 不适合把 I/O 成功当成业务成功。

## 已知限制 / Known Limitations
1. 实现包含缓存或内部状态时，需要关注实例共享、并发访问和生命周期管理。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码深化实现行为、性能与限制说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |
