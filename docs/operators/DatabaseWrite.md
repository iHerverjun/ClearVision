# 数据库写入 / DatabaseWrite

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DatabaseWriteOperator` |
| 枚举值 (Enum) | `OperatorType.DatabaseWrite` |
| 分类 (Category) | 数据 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蔺苇君 |

## 算法原理 / Algorithm Principle
> 中文：将上游输入 `Data` 序列化为 JSON，并写入目标数据表；成功时返回写入状态与记录 ID。
> English: Serializes upstream `Data` into JSON, writes it to the target table, and returns write status with record ID.

## 实现策略 / Implementation Strategy
> 中文：参数层先解析 `ConnectionString/TableName/DbType`，空连接串时自动回退到本地 SQLite；表名通过正则白名单校验，防止 SQL 注入。写入链路优先实现 SQLite（建表+参数化 INSERT），`SQLServer/MySQL` 当前为兼容占位分支。
> English: Resolves `ConnectionString/TableName/DbType`, falls back to local SQLite when connection string is empty, validates table name via regex whitelist, and executes SQLite create-if-not-exists + parameterized INSERT. `SQLServer/MySQL` are currently compatibility stubs.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`GetStringParam("ConnectionString"|"TableName"|"DbType")`
- 安全校验：`IsValidTableName()` + `Regex ^[a-zA-Z_][a-zA-Z0-9_]*$`
- 数据序列化：`JsonSerializer.Serialize(data)`
- 数据写入：`WriteToDatabase(...)`
- SQLite 分支：`SqliteConnection.Open()` -> `CreateTableIfNotExists()` -> `SqliteCommand.ExecuteNonQuery()`
- 结果输出：`OperatorExecutionOutput.Success(...)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ConnectionString` | `string` | "" | - | 数据库连接串；为空时回退 `Data Source=inspection_results.db` |
| `TableName` | `string` | InspectionResults | - | 目标表名，仅允许字母/数字/下划线且不能以数字开头 |
| `DbType` | `enum` | SQLite | - | 目标数据库类型：`SQLite/SQLServer/MySQL` |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | 数据 | `Any` | Yes | 待持久化对象 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Status` | 状态 | `Boolean` | 写入是否成功 |
| `RecordId` | 记录ID | `String` | 本次写入记录唯一 ID（GUID） |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(|Data| + DB I/O)`（序列化 + 数据库写入） |
| 典型耗时 (Typical Latency) | 本地 SQLite 常见 `1-20 ms`（不含外部网络数据库 RTT） |
| 内存特征 (Memory Profile) | 主要来自 JSON 序列化缓冲，约随 `Data` 大小线性增长 |

## 适用场景 / Use Cases
- 适合 (Suitable)：检测结果落库追溯、单站点 SQLite 本地归档、流程末端结果持久化。
- 不适合 (Not Suitable)：需要严格事务批量写入、高并发远程数据库生产写入（当前非 SQLite 分支未落地）。

## 已知限制 / Known Limitations
1. 当前仅 SQLite 分支执行真实写入；`SQLServer/MySQL` 分支返回成功占位，不会实际落库。
2. 表结构固定为 `Id/Data/Timestamp`，未提供列级映射与 schema 演进能力。
3. 运行时会额外返回 `TableName/DbType/Timestamp`，但端口元数据仅声明 `Status/RecordId`。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |
| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |
