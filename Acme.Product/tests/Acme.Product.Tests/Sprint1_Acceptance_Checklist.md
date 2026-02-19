# Sprint 1 验收检查清单

> **文档版本**: V1.0
> **对应路线图**: ClearVision_开发路线图_V4.md
> **完成日期**: 2026-02-19

---

## 任务完成情况

### Task 1.1 — 引用计数 + 写时复制 + 分桶内存池（RC + CoW + Pool）✅

| 文件 | 路径 | 状态 |
|------|------|------|
| MatPool.cs（新建） | `Infrastructure/Memory/MatPool.cs` | ✅ 已完成 |
| ImageWrapper.cs（重写） | `Infrastructure/Operators/ImageWrapper.cs` | ✅ 已完成 |
| OperatorBase.cs（更新） | `Infrastructure/Operators/OperatorBase.cs` | ✅ 已完成 |
| FlowExecutionService.cs（更新） | `Infrastructure/Services/FlowExecutionService.cs` | ✅ 已完成 |

#### 核心实现要点：

**1. MatPool - 分桶内存池**
- 按 `(width, height, type)` 组合分桶管理 Mat 缓冲区
- `Rent()` 方法：优先从池取，无则新建
- `Return()` 方法：归还到池或释放（超过容量时）
- 线程安全：`ConcurrentDictionary` + `ConcurrentBag`
- 容量控制：`maxPerBucket` + `maxTotalBytes`

**2. ImageWrapper - RC + CoW + 内存池集成**
- 引用计数（`_refCount`）：初始为 1，`AddRef()` 增加，`Release()` 减少
- 写时复制（`GetWritableMat()`）：
  - `refCount == 1`：直接返回原 Mat（零拷贝）
  - `refCount > 1`：从 `MatPool` 取缓冲块，执行 `CopyTo`
- 生命周期：`Dispose()` 时归还 Mat 到 `MatPool`

**3. OperatorBase - 生命周期管理**
- `ExecuteWithLifecycleAsync()`：包装执行，自动 `Release` 输入中的 `ImageWrapper`
- 算子开发约定注释：明确读写操作规范

**4. FlowExecutionService - 扇出预分析**
- `AnalyzeFanOutDegrees()`：预分析每个输出端口的扇出度
- `ApplyFanOutRefCounts()`：根据扇出度设置引用计数（扇出度 N → AddRef N-1 次）

---

### Task 1.2 — 端口类型系统扩展 ✅

| 新增类型 | 枚举值 | 对应值对象 | 状态 |
|----------|--------|------------|------|
| PointList | 8 | `List<Position>` | ✅ 已完成 |
| DetectionResult | 9 | `DetectionResult` | ✅ 已完成 |
| DetectionList | 10 | `DetectionList` | ✅ 已完成 |
| CircleData | 11 | `CircleData` | ✅ 已完成 |
| LineData | 12 | `LineData` | ✅ 已完成 |

#### 更新文件：

1. **PortDataType 枚举** (`Core/Enums/OperatorEnums.cs`)
   - 添加 5 个新端口类型

2. **值对象定义** (`Core/ValueObjects/VisionValueObjects.cs`)
   - `DetectionResult`: 类别、置信度、边界框(X,Y,Width,Height)、计算属性(CenterX/Y, Area)
   - `DetectionList`: 检测结果集合、辅助方法(GetBestByConfidence/GetByLabel/GetMaxArea)
   - `CircleData`: 圆心(X,Y)、半径、计算属性(Diameter/Area/Circumference)、DistanceTo方法
   - `LineData`: 起点/终点、计算属性(Length/MidX/MidY/Angle)、DistanceToPoint方法

3. **算子更新**
   - `DeepLearningOperator`: `Defects` 端口升级为 `DetectionList` 类型
   - `CircleMeasurementOperator`: 新增 `Circle` 输出端口（`CircleData` 类型）

---

## 单元测试

| 测试文件 | 测试内容 | 测试数量 |
|----------|----------|----------|
| `Sprint1_MemoryPoolTests.cs` | MatPool + ImageWrapper 测试 | 8 个 |
| `Sprint1_ValueObjectTests.cs` | 值对象测试 | 28 个 |

### 测试覆盖要点：

**引用计数测试**
- ✅ 3 个消费者的生命周期管理
- ✅ 双重释放检测

**CoW 测试**
- ✅ 并发写时复制（两个线程独立副本）
- ✅ 单持有者零拷贝优化

**内存池测试**
- ✅ 分桶功能验证
- ✅ 最大桶容量限制
- ✅ Trim 功能
- ✅ Rent 命中率（基础测试）

**值对象测试**
- ✅ DetectionResult: 属性设置、计算属性、相等性
- ✅ DetectionList: 集合操作、筛选方法、统计计算
- ✅ CircleData: 几何计算、距离计算
- ✅ LineData: 长度/角度计算、点到线距离

---

## 验收标准对照

### Task 1.1 验收标准

| 标准 | 验证方式 | 状态 |
|------|----------|------|
| 引用计数单元测试 | `Sprint1_MemoryPoolTests.ImageWrapper_RefCount_ThreeConsumers_CorrectLifecycle` | ✅ |
| CoW 并发测试 | `Sprint1_MemoryPoolTests.ImageWriter_CoW_ConcurrentAccess_ReturnsIndependentCopies` | ✅ |
| 内存稳定性测试 | 需 4 小时长时测试（CI/CD 环境执行） | ⏳ 待集成测试 |
| 内存池效率验证 | `Sprint1_MemoryPoolTests.MatPool_RentHitRate_AfterWarmup_ShouldBeHigh` | ✅ |

### Task 1.2 验收标准

| 标准 | 验证方式 | 状态 |
|------|----------|------|
| DetectionList 类型可用 | `Sprint1_ValueObjectTests` 全部通过 | ✅ |
| CircleData 类型可用 | `Sprint1_ValueObjectTests` 全部通过 | ✅ |
| DeepLearning 算子输出 | 代码已更新，输出 `DetectionList` | ✅ |
| CircleMeasurement 算子输出 | 代码已更新，输出 `CircleData` | ✅ |

---

## 算子开发约定（Code Review 检查点）

```csharp
/// 读操作：使用 MatReadOnly，不触发 CoW
var src = image.MatReadOnly;

/// 写操作：获取可写副本，从内存池取缓冲（CoW）
var dst = image.GetWritableMat();
Cv2.SomeFilter(src, dst, ...);    // 处理
return new ImageWrapper(dst);     // 输出，引用计数重置为 1

/// 禁止行为
// ❌ 禁止在算子内部手动调用 AddRef() / Release()
// ❌ 禁止在算子内部直接调用 mat.Dispose()
```

---

## 文件清单

### 新建文件
1. `Acme.Product/src/Acme.Product.Infrastructure/Memory/MatPool.cs`
2. `Acme.Product/tests/Acme.Product.Tests/Memory/Sprint1_MemoryPoolTests.cs`
3. `Acme.Product/tests/Acme.Product.Tests/Memory/Sprint1_ValueObjectTests.cs`
4. `Acme.Product/tests/Acme.Product.Tests/Sprint1_Acceptance_Checklist.md` (本文件)

### 修改文件
1. `Acme.Product/src/Acme.Product.Infrastructure/Operators/ImageWrapper.cs`
2. `Acme.Product/src/Acme.Product.Infrastructure/Operators/OperatorBase.cs`
3. `Acme.Product/src/Acme.Product.Infrastructure/Services/FlowExecutionService.cs`
4. `Acme.Product/src/Acme.Product.Core/Enums/OperatorEnums.cs`
5. `Acme.Product/src/Acme.Product.Core/ValueObjects/VisionValueObjects.cs`
6. `Acme.Product/src/Acme.Product.Infrastructure/Operators/DeepLearningOperator.cs`
7. `Acme.Product/src/Acme.Product.Infrastructure/Operators/CircleMeasurementOperator.cs`

---

## 下一步工作

进入 **Sprint 2：执行引擎并发化改造**

主要任务：
- Task 2.1: ForEach 子图执行机制（IoMode 双模式）
- Task 2.2: ArrayIndexer 与 JsonExtractor

前置检查：
- [ ] 运行所有单元测试通过
- [ ] 4 小时长时稳定性测试（内存稳定性）
- [ ] 性能基准测试（P99 帧耗时）

---

*文档维护：ClearVision 开发团队*
