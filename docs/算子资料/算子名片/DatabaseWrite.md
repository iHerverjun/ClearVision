# 数据库写入 / DatabaseWrite

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DatabaseWriteOperator` |
| 枚举值 (Enum) | `OperatorType.DatabaseWrite` |
| 分类 (Category) | 数据 |
| 成熟度 (Maturity) | 稳定 Stable |

## 算法原理 / Algorithm Principle
该算子把输入数据持久化到数据库表中，当前正式支持 `SQLite`、`SQLServer`、`MySQL` 三种后端。

## 实现策略 / Implementation Strategy
- 输入 `Data` 为待落库载荷，算子内部统一序列化为 JSON。
- 输入 `RecordId` 为可选幂等键；提供后按同一 `Id` 执行 upsert/merge，不提供则自动生成 GUID。
- 建表与写入按 provider 分层实现，并带固定瞬态重试与超时控制。

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ConnectionString` | `string` | `""` | - | 目标数据库连接字符串。不能为空。 |
| `TableName` | `string` | `"InspectionResults"` | - | 目标表名。仅允许字母、数字、下划线，且必须以字母或下划线开头。 |
| `DbType` | `enum` | `"SQLite"` | `SQLite` / `SQLServer` / `MySQL` | 数据库类型。三种值都是真实现，不允许占位成功。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Data` | 数据 | `Any` | Yes | 待写入的业务载荷。 |
| `RecordId` | 记录ID | `String` | No | 可选幂等键；提供后相同 `Id` 会被更新而不是重复插入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Status` | 状态 | `Boolean` | 写入是否成功。 |
| `RecordId` | 记录ID | `String` | 实际落库使用的记录标识。 |

## 已知限制 / Known Limitations
1. 当前统一表结构为 `Id / Data / Timestamp`，更复杂的列映射不在本算子内展开。
2. 多库真实回归依赖 Docker/Testcontainers 环境。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.0 | 2026-04-12 | 收口为真实三库实现，新增 `RecordId` 输入与幂等 upsert/merge 语义。 |
