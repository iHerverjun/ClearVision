# 异常捕获 / TryCatch

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `TryCatchOperator` |
| 枚举值 (Enum) | `OperatorType.TryCatch` |
| 分类 (Category) | 流程控制 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：该算子作为流程级异常控制节点，负责标记 Try/Catch 路由意图，本身不执行业务逻辑捕获。
> English: Acts as a flow-level exception control node that marks Try/Catch routing intent rather than performing actual business logic handling.

## 实现策略 / Implementation Strategy
> 中文：执行时主要透传输入并附加 `TryCatch_*` 元信息；图像对象通过引用计数安全透传；真实异常捕获与分支跳转由 `FlowExecutionService` 负责。
> English: Primarily passes through inputs with `TryCatch_*` metadata and preserves image references safely; actual exception catching/routing is handled by flow execution service.

## 核心 API 调用链 / Core API Call Chain
- 参数读取：`EnableCatch` / `CatchOutputError` / `CatchOutputStackTrace`
- 输入透传：遍历 `inputs` 输出
- 图像保活：`ImageWrapper.AddRef()`
- 输出标记：`TryCatch_Enabled` 等运行标志

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `EnableCatch` | `bool` | true | - | 是否启用异常捕获 |
| `CatchOutputError` | `bool` | true | - | - |
| `CatchOutputStackTrace` | `bool` | false | - | - |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Input` | 输入 | `Any` | No | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Try` | Try分支 | `Any` | - |
| `Catch` | Catch分支 | `Any` | - |
| `Error` | 错误信息 | `String` | - |
| `HasError` | 是否有错 | `Boolean` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 约 `O(K)`（`K` 为输入键数量） |
| 典型耗时 (Typical Latency) | 约 `<0.1 ms` |
| 内存特征 (Memory Profile) | 透传输出字典拷贝，开销与输入规模线性相关 |

## 适用场景 / Use Cases
- 适合 (Suitable)：在流程图中建立主流程与异常流程的控制边界。
- 不适合 (Not Suitable)：期望该节点独立完成异常恢复逻辑的场景。

## 已知限制 / Known Limitations
1. 算子内部不执行真正 `try-catch` 语义，依赖引擎支持才能生效。
2. 参数仅作为路由提示，不会自动生成错误对象或堆栈。
3. 若流程引擎未配置异常分支，节点不会产生预期的 Catch 行为。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |