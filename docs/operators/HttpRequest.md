# HTTP 请求 / HttpRequest

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `HttpRequestOperator` |
| 枚举值 (Enum) | `OperatorType.HttpRequest` |
| 分类 (Category) | 通信 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：封装 HTTP 请求执行流程，支持超时、重试与响应状态回传，用于将流程数据与外部 REST 服务对接。
> English: Wraps HTTP request execution with timeout/retry and structured response outputs for integrating external REST services.

## 实现策略 / Implementation Strategy
> 中文：请求体优先取输入 `Body`，否则可序列化输入字典；每次请求用独立取消令牌控制超时，并按重试策略重发；返回状态码、成功标记与响应正文。
> English: Resolves request body from `Body` input or serialized inputs, executes with per-attempt timeout token, retries on failure, and returns status code/success/body.

## 核心 API 调用链 / Core API Call Chain
- `HttpRequestMessage` + `HttpMethod`
- `StringContent`（支持 `Content-Type`）
- `HttpClient.SendAsync`
- `ReadAsStringAsync`
- 重试控制：`CancellationTokenSource` + `Task.Delay`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `Url` | `string` | http://localhost:5000/api | - | - |
| `Method` | `enum` | POST | - | - |
| `Timeout` | `int` | 5000 | - | - |
| `MaxRetries` | `int` | 3 | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Body` | 请求体 | `String` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Response` | 响应内容 | `String` | - |
| `StatusCode` | 状态码 | `Integer` | - |
| `IsSuccess` | 是否成功 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 主要受网络 I/O 支配，近似 `O(R * Payload)`（`R` 重试次数） |
| 典型耗时 (Typical Latency) | `10 ms` 到秒级，取决于网络、服务端与重试配置 |
| 内存特征 (Memory Profile) | 请求/响应字符串与序列化缓存，随报文大小线性增长 |

## 适用场景 / Use Cases
- 适合 (Suitable)：上报检测结果到 MES/云 API、查询外部配置、触发第三方业务接口。
- 不适合 (Not Suitable)：必须离线执行或要求确定性毫秒级固定时延的链路。

## 已知限制 / Known Limitations
1. 参数命名存在历史差异：属性定义与代码读取键（如 `Timeout` vs `TimeoutMs`）需配置端对齐。
2. 默认 `HttpClient` 复用但未内建鉴权签名、断路器与高级重试退避策略。
3. 大响应体会带来较高字符串内存开销，建议上游限制返回尺寸。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |