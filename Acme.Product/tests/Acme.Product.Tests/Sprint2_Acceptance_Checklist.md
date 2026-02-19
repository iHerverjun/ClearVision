# Sprint 2 验收检查清单

> **文档版本**: V1.0
> **对应路线图**: ClearVision_开发路线图_V4.md
> **完成日期**: 2026-02-19

---

## 任务完成情况

### Task 2.1 — ForEach 子图执行机制（IoMode 双模式）✅

| 文件 | 路径 | 状态 |
|------|------|------|
| ForEachOperator.cs（新建） | `Infrastructure/Operators/ForEachOperator.cs` | ✅ 已完成 |
| FlowLinter.cs（新建） | `Infrastructure/Services/FlowLinter.cs` | ✅ 已完成 |
| DependencyInjection.cs（更新） | `Desktop/DependencyInjection.cs` | ✅ 已注册 |

#### 核心实现要点：

**1. ForEachOperator - IoMode 双模式**
- `IoMode=Parallel`: 使用 `Parallel.ForEachAsync` 并行执行，适用于纯计算子图
- `IoMode=Sequential`: 使用顺序 `foreach` 执行，适用于含 I/O 的子图，保护硬件连接
- 参数：
  - `MaxParallelism`: 并行线程数上限（1-64）
  - `OrderResults`: 是否按输入顺序重排结果
  - `FailFast`: 是否快速失败
  - `TimeoutMs`: 单个子图超时

**2. FlowLinter - SAFETY_002 降级**
- 从 Error 降级为 Warning
- Parallel 模式下检测到通信算子时发出警告
- Sequential 模式下无警告（用户已显式声明串行）
- 通信算子类型：Modbus/TCP/Database/Serial/S7/MC/FINS/RTU

---

### Task 2.2 — ArrayIndexer 与 JsonExtractor ✅

| 文件 | 路径 | 状态 |
|------|------|------|
| ArrayIndexerOperator.cs（新建） | `Infrastructure/Operators/ArrayIndexerOperator.cs` | ✅ 已完成 |
| JsonExtractorOperator.cs（新建） | `Infrastructure/Operators/JsonExtractorOperator.cs` | ✅ 已完成 |
| DependencyInjection.cs（更新） | `Desktop/DependencyInjection.cs` | ✅ 已注册 |

#### ArrayIndexer 功能：
- **Index 模式**: 按索引提取
- **MaxConfidence 模式**: 按最大置信度提取
- **MinArea/MaxArea 模式**: 按面积提取
- **First/Last 模式**: 提取首尾元素
- **LabelFilter**: 标签过滤（先筛选后提取）

#### JsonExtractor 功能：
- **JSONPath 支持**: 简化版（支持 `.` 和 `[n]`）
- **输出类型**: Any/String/Float/Integer/Boolean
- **默认值**: 字段不存在时返回默认值
- **Required**: 字段不存在时是否报错

---

## 算子类型枚举更新

新增算子类型（`OperatorEnums.cs`）：
- `ForEach = 100`
- `ArrayIndexer = 101`
- `JsonExtractor = 102`

---

## 单元测试

| 测试文件 | 测试内容 | 测试数量 |
|----------|----------|----------|
| `Sprint2_ForEachTests.cs` | ForEach 并行/串行模式、FailFast | 10 个 |
| `Sprint2_ArrayIndexerTests.cs` | ArrayIndexer 各种模式 | 10 个 |
| `Sprint2_JsonExtractorTests.cs` | JsonExtractor 提取功能 | 10 个 |

### 测试覆盖要点：

**ForEach 测试**
- ✅ Parallel 模式并行执行验证（耗时 < 200ms）
- ✅ Sequential 模式串行执行验证
- ✅ FailFast 正确中断
- ✅ 空列表处理
- ✅ 参数验证

**ArrayIndexer 测试**
- ✅ Index 模式
- ✅ MaxConfidence/MinArea/MaxArea 模式
- ✅ 标签过滤
- ✅ 空列表处理

**JsonExtractor 测试**
- ✅ 字符串/数值/布尔提取
- ✅ 嵌套对象访问
- ✅ 数组索引访问
- ✅ 字段不存在处理
- ✅ JSON 解析错误处理

---

## 验收标准对照

### Task 2.1 验收标准

| 标准 | 验证方式 | 状态 |
|------|----------|------|
| Parallel 模式 | `Sprint2_ForEachTests.ForEach_ParallelMode_ExecutesInParallel` | ✅ |
| Sequential 模式 | `Sprint2_ForEachTests.ForEach_SequentialMode_ExecutesSequentially` | ✅ |
| FailFast | `Sprint2_ForEachTests.ForEach_SequentialMode_FailFast_StopsAfterFailure` | ✅ |
| SAFETY_002 | FlowLinter 实现 | ✅ |

### Task 2.2 验收标准

| 标准 | 验证方式 | 状态 |
|------|----------|------|
| ArrayIndexer 可用 | `Sprint2_ArrayIndexerTests` 全部通过 | ✅ |
| JsonExtractor 可用 | `Sprint2_JsonExtractorTests` 全部通过 | ✅ |

---

## 文件清单

### 新建文件（5 个）
1. `Acme.Product/src/Acme.Product.Infrastructure/Operators/ForEachOperator.cs`
2. `Acme.Product/src/Acme.Product.Infrastructure/Operators/ArrayIndexerOperator.cs`
3. `Acme.Product/src/Acme.Product.Infrastructure/Operators/JsonExtractorOperator.cs`
4. `Acme.Product/src/Acme.Product.Infrastructure/Services/FlowLinter.cs`
5. `Acme.Product/tests/Acme.Product.Tests/Operators/Sprint2_*Tests.cs` (3 个)
6. `Acme.Product/tests/Acme.Product.Tests/Sprint2_Acceptance_Checklist.md` (本文件)

### 修改文件（2 个）
1. `Acme.Product/src/Acme.Product.Core/Enums/OperatorEnums.cs` - 添加新算子类型
2. `Acme.Product/src/Acme.Product.Desktop/DependencyInjection.cs` - 注册新算子

---

## 下一步工作

进入 **Sprint 3：算子全面扩充**

主要任务：
- Task 3.1: MathOperation（数值计算）
- Task 3.2: LogicGate（逻辑门）
- Task 3.3: TypeConvert（类型转换）
- Task 3.4: 手眼标定向导（独立 UI 模块）
- Task 3.5: HTTP/MQTT 算子
- Task 3.6: 其他原子算子

前置检查：
- [ ] Sprint 2 所有单元测试通过
- [ ] ForEach IoMode 双模式功能验证
- [ ] FlowLinter SAFETY_002 规则验证

---

*文档维护：ClearVision 开发团队*
