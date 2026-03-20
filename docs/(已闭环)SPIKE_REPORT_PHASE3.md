# Phase 3 Spike 调研报告

**日期**: 2026-03-18  
**调研范围**: 预览子图与调试缓存机制  

---

## Spike-1: 现有调试缓存机制

### 发现

**位置**: `FlowExecutionService.cs`

```csharp
// 调试缓存定义 (line 34)
private readonly ConcurrentDictionary<(Guid DebugSessionId, Guid OperatorId), Dictionary<string, object>> _debugCache = new();

// TTL 策略 (lines 24-26)
private static readonly TimeSpan DebugCleanupInterval = TimeSpan.FromMinutes(10);
private static readonly TimeSpan DebugSessionTtl = TimeSpan.FromMinutes(30);
```

**缓存 Key 结构**: `(Guid DebugSessionId, Guid OperatorId)` - 元组作为复合键

**现有功能**:
- ✅ `ExecuteFlowDebugAsync` 支持调试执行
- ✅ `GetDebugIntermediateResult` 获取中间结果
- ✅ `ClearDebugCacheAsync` 清理指定会话
- ✅ `CleanupStaleDebugSessions` 定时清理过期会话

### 结论
**可以直接复用**现有调试缓存机制，无需新建服务。

---

## Spike-2: 前端预览调用链

### 发现

**文件**: `previewPanel.js`, `propertyPanel.js`

**当前调用**:
```javascript
// POST /api/operators/{type}/preview
const result = await httpClient.post(`/api/operators/${operatorType}/preview`, {
    parameters,
    imageBase64
});
```

**问题**: 前端直接传递 `imageBase64`，不处理上游算子依赖。

### 结论
需要新增端点 `POST /api/flows/preview-node`，接收：
- `projectId`
- `targetNodeId`
- `debugSessionId`
- `parameters`

---

## Spike-3: BreakAtOperatorId 实现

### 评估

**修改范围**: `FlowExecutionService.cs` ~30 行

**实现方式**:
```csharp
public async Task<FlowDebugExecutionResult> ExecuteFlowDebugAsync(...)
{
    foreach (var op in executionOrder)
    {
        // ... 执行算子 ...
        
        // 【新增】检查是否到达断点
        if (op.Id == options.BreakAtOperatorId)
            break;  // 提前退出
    }
}
```

**风险**: 低，只需确保 break 后返回的部分结果仍可用

### 结论
**可行**，2 天可完成。

---

## Spike-4: IImageCacheRepository 扩展性

### 发现

**当前接口**:
```csharp
public interface IImageCacheRepository
{
    Task<Guid> AddAsync(byte[] imageData, string format);
    Task<byte[]?> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task CleanExpiredAsync(TimeSpan expiration);
}
```

**问题**: 不支持字符串 key、Mat 对象、TTL 写入

### 结论
**无需扩展**，复用 `FlowExecutionService._debugCache` 即可满足需求。

---

## 决策: 方案 A（复用现有机制）

### 实施内容

1. **新增端点** `POST /api/flows/preview-node`
2. **扩展 `DebugOptions`** 添加 `BreakAtOperatorId`
3. **前端修改** 预览时调用新端点

### 排期
- 后端: 2 天
- 前端: 2 天
- 测试: 1 天

---

## 附录: 关键代码位置

| 文件 | 行号 | 说明 |
|------|------|------|
| FlowExecutionService.cs | 34 | _debugCache 定义 |
| FlowExecutionService.cs | 985 | ExecuteFlowDebugAsync |
| FlowExecutionService.cs | 1193 | GetDebugIntermediateResult |
| FlowExecutionService.cs | 1227 | CleanupStaleDebugSessions |
