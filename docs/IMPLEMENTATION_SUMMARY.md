# 架构修复实施总结

**日期**: 2026-03-18  
**版本**: v2.0  
**状态**: Phase 1 & 2 已完成，Phase 3 & 4 框架待审  

---

## 交付清单

### Phase 1: 核心稳定 ✅

| 文件 | 说明 | 状态 |
|------|------|------|
| `IInspectionRuntimeCoordinator.cs` | 协调器接口 | ✅ |
| `InspectionRuntimeCoordinator.cs` | 协调器实现 | ✅ |
| `IInspectionWorker.cs` | Worker 接口 | ✅ |
| `InspectionWorker.cs` | Worker 实现 | ✅ |
| `InspectionService.cs` | 重写门面模式 | ✅ |
| `InspectionMetrics.cs` | 可观测性 | ✅ |
| `DependencyInjection.cs` | DI 注册更新 | ✅ |

**构建状态**: ✅ 成功

### Phase 2: 事件机制 ✅

| 文件 | 说明 | 状态 |
|------|------|------|
| `IInspectionEvent.cs` | 事件基础接口 | ✅ |
| `InspectionEvents.cs` | 事件定义 | ✅ |
| `IInspectionEventBus.cs` | 事件总线接口 | ✅ |
| `InMemoryInspectionEventBus.cs` | 事件总线实现 | ✅ |
| `InMemoryEventStore.cs` | 事件存储 | ✅ |
| `InspectionEventEndpoints.cs` | SSE 端点 | ✅ |
| `SseHeartbeatService.cs` | 心跳服务 | ✅ |
| `WebMessageHandler.cs` | 订阅事件总线 | ✅ |
| `inspectionController.js` | 双栈适配 | ✅ |

**构建状态**: ✅ 成功

### Phase 3: Spike 调研 ✅

| 文档 | 说明 | 状态 |
|------|------|------|
| `SPIKE_REPORT_PHASE3.md` | 调研报告 | ✅ |

**决策**: 方案 A（复用现有调试机制）

### Phase 4: LLM 闭环框架 ✅

| 文档 | 说明 | 状态 |
|------|------|------|
| `LLM_CLOSED_LOOP_FRAMEWORK.md` | 框架设计 | ✅ |

---

## 架构变更对比

### Before (Bug)
```
InspectionService (Scoped)
├── _realtimeCtsMap ❌ 实例字段
├── _realtimeTasks ❌ 实例字段
└── 不同请求 = 不同实例 = 状态丢失
```

### After (Fixed)
```
InspectionService (Scoped) - 门面
    │
    ├── IInspectionRuntimeCoordinator (Singleton) - 状态管理
    │       ├── _sessions ✅ 进程级
    │       └── 线程安全锁
    │
    └── IInspectionWorker (HostedService) - 执行
            ├── 独立 Scope
            ├── 三重异常保护
            └── 优雅关机
```

---

## 关键技术决策

### 1. Scoped Bug 修复
- ✅ 协调器(Singleton) + Worker(Singleton + IServiceScopeFactory)
- ✅ 独立 CancellationTokenSource，与 HTTP 请求解耦
- ✅ 线程安全：ConcurrentDictionary + 每 projectId 独立锁

### 2. 事件总线
- ✅ 支持精确类型订阅 + 接口订阅
- ✅ 异常隔离：单个 handler 失败不影响其他
- ✅ 包装器委托避免类型转换失败

### 3. 双栈过渡
- ✅ SSE 优先，WebMessage 回退
- ✅ 断线重连支持（Last-Event-ID + 事件回放）
- ✅ 30 秒心跳保活

### 4. 事件存储
- ✅ 环形缓冲区：每项目 100 条，全局 50 项目
- ✅ 自动清理老数据

---

## 待审批项

### Phase 3 实施（复用现有机制）
- [ ] 新增端点 `POST /api/flows/preview-node`
- [ ] 扩展 `DebugOptions.BreakAtOperatorId`
- [ ] 前端调用新端点

### Phase 4 实施（LLM 闭环）
- [ ] `PreviewMetricsAnalyzer`
- [ ] `AutoTuneService`
- [ ] API 端点
- [ ] 科学实验验证

---

## 如何测试

```bash
# 构建
cd Acme.Product
dotnet build src/Acme.Product.Desktop

# 运行
./src/Acme.Product.Desktop/bin/Debug/net8.0-windows/win-x64/Acme.Product.Desktop.exe
```

### 测试场景
1. **跨请求状态一致性**: 启动实时检测 → 关闭浏览器 → 重新打开 → 状态正确
2. **优雅关机**: 运行检测时关闭应用 → 30 秒内正常停止
3. **SSE 双栈**: 支持浏览器和普通 HTTP 客户端
4. **断线重连**: 断开 SSE → 重连 → 回放缺失事件

---

## 文件变更统计

| 类型 | 数量 |
|------|------|
| 新增文件 | 12 |
| 修改文件 | 4 |
| 删除代码行 | ~50 (实例字段) |
| 新增代码行 | ~3000 |

---

**审批后下一步**: 
1. Phase 3 实施（4 天）
2. Phase 4 实施（13 天）
3. 端到端测试
