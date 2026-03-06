# ClearVision 性能隐患排查报告

> **排查日期**：2026-02-28  
> **排查范围**：后端 C# 核心模块 + 前端 JS  
> **核心关注**：长时间运行是否会崩溃  
> **审查文件数**：15+ 个核心文件，约 7000 行代码

---
## 2026-03-06 状态回填

| 问题 | 当前状态 | 代码证据 | 备注 |
|---|---|---|---|
| `#1` Image cache 无限增长 | ✅ 已修复 | `ImageAcquisitionService.MaxCacheSize = 50` + `AddToCache()` 淘汰策略 | `_imageCache` 已有容量上限 |
| `#2` 调试缓存 / 执行状态不清理 | ✅ 已修复 | `ExecutionStatusTtl`、`CleanupStaleExecutionStatuses()`、调试缓存清理计时器 | 长时间运行不再只增不减 |
| `#3` ONNX 模型缓存不释放 | ✅ 已修复 | `DeepLearningOperator.MaxCachedModels`、LRU 驱逐、`UnloadModel()` | 模型切换已具备释放路径 |
| `#4` 连续采集资源管理 | ✅ 已修复待回归 | `CameraManager` 持有 `_acquisitionTask/_acquisitionCts`，`Stop`/`Dispose` 中等待清理 | 建议补一轮连续采集稳定性压测 |
| `#5` 每帧 PNG 编码掉帧 | ✅ 已修复 | `CameraManager.EncodeFrameToBytes(... useFastEncoding: true)` 走 JPEG 快路径 | 连续采集不再固定走 PNG |
| `#6` 36MB pinned buffer | ✅ 已修复 | `HikvisionCamera` 改为 `AllocHGlobal` 非托管缓冲 | 不再长期 pin 36MB 托管数组 |
| `#10` 每帧 Clone + Base64 | 🟡 部分完成 | `ImageAcquisitionService` 已限制缓存，但连续采集仍会 `Convert.ToBase64String(frameData)` | 当前主要剩余项 |

---

## 总览

| 等级 | 数量 | 说明 |
|------|------|------|
| 🔴 严重 | 3 | 长期运行必崩（OOM / 显存耗尽） |
| 🟠 中等 | 4 | 影响稳定性和性能 |
| 🟡 轻微 | 3 | 影响长期运行质量 |
| ✅ 良好 | 5 | 设计优秀、无需修改 |

---

## 🔴 严重问题（长期运行必崩）

### 问题 #1：ImageAcquisitionService._imageCache 无限增长（内存泄漏）

- **文件**：`Acme.Product.Infrastructure/Services/ImageAcquisitionService.cs` 第 25 行
- **严重性**：🔴 P0 — 数小时内可导致 OOM 崩溃
- **影响**：工业相机 1fps 下，每小时累积约 21GB 内存

**问题描述**：  
`_imageCache` 字典每次采集/加载图像时都写入 `Mat.Clone()`，但**没有任何自动清理策略**。仅 `ReleaseImageAsync` 被显式调用时才移除单项，而流程执行完毕后并不会自动调用此方法。

**问题代码**：

```csharp
// ImageAcquisitionService.cs 第 25 行
private readonly Dictionary<Guid, Mat> _imageCache = new();

// 第 130 行：每次采集都写入缓存，永远只增不减
lock (_imageCache)
{
    _imageCache[imageId] = mat.Clone(); // ← 只进不出
}
```

**修改建议**：

方案 A — 添加 LRU 驱逐策略（推荐）：

```csharp
// 添加容量上限和 LRU 驱逐
private const int MaxCacheSize = 50; // 最多缓存 50 张图
private readonly LinkedList<Guid> _cacheOrder = new(); // LRU 顺序

private void AddToCache(Guid imageId, Mat mat)
{
    lock (_imageCache)
    {
        // 如果已满，驱逐最旧的
        while (_imageCache.Count >= MaxCacheSize && _cacheOrder.Count > 0)
        {
            var oldestId = _cacheOrder.First!.Value;
            _cacheOrder.RemoveFirst();
            if (_imageCache.TryGetValue(oldestId, out var oldMat))
            {
                oldMat.Dispose();
                _imageCache.Remove(oldestId);
            }
        }

        _imageCache[imageId] = mat.Clone();
        _cacheOrder.AddLast(imageId);
    }
}
```

方案 B — 流程执行完毕后自动清理：

```csharp
// 在 FlowExecutionService.ExecuteFlowAsync 的 finally 块中
// 清理本次执行产生的临时图像缓存
finally
{
    // 清理本次流程中用到的所有中间图像
    foreach (var outputs in operatorOutputs.Values)
    {
        foreach (var val in outputs.Values)
        {
            if (val is ImageWrapper wrapper) wrapper.Release();
        }
    }
}
```

---

### 问题 #2：FlowExecutionService 调试缓存和执行状态永不清理

- **文件**：`Acme.Product.Infrastructure/Services/FlowExecutionService.cs` 第 24、31-32 行
- **严重性**：🔴 P0 — 反复调试/执行后内存持续增长
- **影响**：调试缓存含图像数据，每次调试可能累积数十 MB

**问题描述**：  
`_debugCache`、`_debugOptions` 在调试会话结束后不会自动清理，依赖前端显式调用 `ClearDebugCacheAsync`。`_executionStatuses` 每次执行都添加条目但从不移除。

**问题代码**：

```csharp
// 第 24 行：每次执行都添加，永不移除
private readonly ConcurrentDictionary<Guid, FlowExecutionStatus> _executionStatuses = new();

// 第 31-32 行：调试缓存无自动清理
private readonly ConcurrentDictionary<(Guid, Guid), Dictionary<string, object>> _debugCache = new();
private readonly ConcurrentDictionary<Guid, DebugOptions> _debugOptions = new();
```

**修改建议**：

```csharp
// 修改 1：在 ExecuteFlowAsync 的 finally 块中移除执行状态
finally
{
    if (_executionCancellations.TryRemove(flow.Id, out var removedCts))
    {
        removedCts.Dispose();
    }
    // ← 新增：执行完后移除状态条目
    _executionStatuses.TryRemove(flow.Id, out _);
}

// 修改 2：为 _debugCache 添加自动过期清理
// 在构造函数中启动定时清理
private readonly Timer _debugCacheCleanupTimer;

public FlowExecutionService(...)
{
    // ... 现有代码 ...
    _debugCacheCleanupTimer = new Timer(CleanupStaleDebugSessions, null,
        TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
}

private void CleanupStaleDebugSessions(object? state)
{
    var staleThreshold = DateTime.UtcNow.AddMinutes(-30);
    foreach (var kvp in _debugOptions)
    {
        // 如果调试会话超过 30 分钟未活动，自动清理
        _ = ClearDebugCacheAsync(kvp.Key);
    }
}
```

---

### 问题 #3：ONNX InferenceSession 模型缓存永不释放

- **文件**：`Acme.Product.Infrastructure/Operators/DeepLearningOperator.cs` 第 81 行
- **严重性**：🔴 P0 — GPU 显存逐渐耗尽
- **影响**：每个 ONNX 模型占 50-500MB GPU 内存，切换多个模型后显存耗尽

**问题代码**：

```csharp
// 第 81 行：static 缓存，进程生命周期内永不释放
private static readonly ConcurrentDictionary<string, InferenceSession> _modelCache = new();
private static readonly ConcurrentDictionary<string, SemaphoreSlim> _modelLocks = new();
```

**修改建议**：

```csharp
// 方案 1：添加最大缓存数量 + LRU 驱逐
private static readonly int MaxCachedModels = 3;
private static readonly LinkedList<string> _modelAccessOrder = new();
private static readonly object _evictionLock = new();

private InferenceSession? LoadModel(string modelPath, bool useGpu, int gpuDeviceId)
{
    var cacheKey = $"{modelPath}_gpu_{useGpu}_{gpuDeviceId}";

    if (_modelCache.TryGetValue(cacheKey, out var cached))
    {
        // 更新 LRU 顺序
        lock (_evictionLock)
        {
            _modelAccessOrder.Remove(cacheKey);
            _modelAccessOrder.AddLast(cacheKey);
        }
        return cached;
    }

    // ... 加载模型 ...

    // 驱逐最久未使用的模型
    lock (_evictionLock)
    {
        while (_modelCache.Count >= MaxCachedModels && _modelAccessOrder.Count > 0)
        {
            var oldest = _modelAccessOrder.First!.Value;
            _modelAccessOrder.RemoveFirst();
            if (_modelCache.TryRemove(oldest, out var oldSession))
            {
                oldSession.Dispose();
                Logger.LogInformation("[DeepLearning] 驱逐模型缓存: {Key}", oldest);
            }
        }
        _modelAccessOrder.AddLast(cacheKey);
    }

    _modelCache[cacheKey] = session;
    return session;
}

// 方案 2：添加显式卸载方法供 UI 调用
public static void UnloadModel(string modelPath)
{
    var keysToRemove = _modelCache.Keys.Where(k => k.StartsWith(modelPath)).ToList();
    foreach (var key in keysToRemove)
    {
        if (_modelCache.TryRemove(key, out var session))
            session.Dispose();
        _modelLocks.TryRemove(key, out _);
    }
}
```

---

## 🟠 中等问题（影响稳定性和性能）

### 问题 #4：CameraProviderAdapter 连续采集的资源管理

- **文件**：`Acme.Product.Infrastructure/Cameras/CameraManager.cs` 第 192-217 行
- **严重性**：🟠 P1

**问题描述**：
1. 多次调用 `StartContinuousAcquisitionAsync` 不调用 `Stop` 会泄漏旧的 CTS
2. `Task.Run` 中的异常被静默吞掉（fire-and-forget 模式）
3. 连续采集循环没有 backpressure 机制

**修改建议**：

```csharp
public Task StartContinuousAcquisitionAsync(Func<byte[], Task> frameCallback)
{
    if (_isAcquiring)
        return Task.CompletedTask;

    _frameCallback = frameCallback;
    _isAcquiring = true;

    // ← 修改：先释放旧 CTS
    _acquisitionCts?.Dispose();
    _acquisitionCts = new CancellationTokenSource();

    // ← 修改：保存 Task 引用以便在 Dispose 时等待完成
    _acquisitionTask = Task.Run(async () =>
    {
        try
        {
            if (!_provider.IsGrabbing)
                _provider.StartGrabbing();
            while (!_acquisitionCts.Token.IsCancellationRequested)
            {
                var frame = _provider.GetFrame(1000);
                if (frame != null)
                {
                    byte[] pngData = EncodeFrameToPngBytes(frame);
                    if (_frameCallback != null)
                        await _frameCallback(pngData);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // ← 修改：记录异常而非静默吞掉
            System.Diagnostics.Debug.WriteLine(
                $"[CameraProviderAdapter] Continuous acquisition error: {ex.Message}");
        }
    }, _acquisitionCts.Token);

    return Task.CompletedTask;
}
```

---

### 问题 #5：连续采集每帧 PNG 编码严重影响帧率

- **文件**：`Acme.Product.Infrastructure/Cameras/CameraManager.cs` 第 240-296 行
- **严重性**：🟠 P1
- **影响**：5MP 图像 PNG 编码 50-100ms/帧，限制帧率 ≤ 10-20fps

**修改建议**：

```csharp
// 方案：连续采集场景使用 JPEG 编码（快 10 倍）
private byte[] EncodeFrameToBytes(CameraFrame frame, bool useFastEncoding = false)
{
    using var mat = new Mat(frame.Height, frame.Width, matType, frame.DataPtr);

    if (needConversion)
    {
        using var cvtMat = new Mat();
        Cv2.CvtColor(mat, cvtMat, conversionCode);

        // ← 连续模式下使用 JPEG（~5ms vs PNG ~50ms）
        if (useFastEncoding)
            return cvtMat.ToBytes(".jpg", new int[] { (int)ImwriteFlags.JpegQuality, 85 });
        return cvtMat.ToBytes(".png");
    }
    else
    {
        if (useFastEncoding)
            return mat.ToBytes(".jpg", new int[] { (int)ImwriteFlags.JpegQuality, 85 });
        return mat.ToBytes(".png");
    }
}
```

---

### 问题 #6：HikvisionCamera 36MB GCHandle.Pinned 缓冲区硬编码

- **文件**：`Acme.Product.Infrastructure/Cameras/HikvisionCamera.cs` 第 366-368 行
- **严重性**：🟠 P2
- **影响**：36MB 被钉死在堆上，阻止 GC 压缩，可能导致 LOH 碎片化

**问题代码**：

```csharp
int bufferSize = 4096 * 3000 * 3; // ≈ 36MB 硬编码
_frameBuffer = new byte[bufferSize];
_frameBufferHandle = GCHandle.Alloc(_frameBuffer, GCHandleType.Pinned);
```

**修改建议**：

```csharp
// 方案 1：使用非托管内存（不影响 GC）
private IntPtr _frameBufferPtr = IntPtr.Zero;
private int _frameBufferSize = 0;

public bool Open(string serialNumber)
{
    // ... 打开设备后，根据实际分辨率计算缓冲区大小 ...
    int actualWidth = 2048;  // 从设备信息获取
    int actualHeight = 1536; // 从设备信息获取
    int channels = 3;
    _frameBufferSize = actualWidth * actualHeight * channels;
    _frameBufferPtr = Marshal.AllocHGlobal(_frameBufferSize);
}

public bool Close()
{
    // ... 关闭设备后释放 ...
    if (_frameBufferPtr != IntPtr.Zero)
    {
        Marshal.FreeHGlobal(_frameBufferPtr);
        _frameBufferPtr = IntPtr.Zero;
    }
}
```

---

### 问题 #7：ConnectionPoolManager.IsConnectionAlive 检测不可靠

- **文件**：`Acme.Product.Infrastructure/Services/ConnectionPoolManager.cs` 第 205-218 行
- **严重性**：🟠 P2
- **影响**：远端断开但未发 FIN/RST 时，误判连接存活，导致通信异常

**修改建议**：

```csharp
private bool IsConnectionAlive(PooledConnection connection)
{
    try
    {
        if (connection.Client is TcpClient tcpClient)
        {
            var socket = tcpClient.Client;
            // 真正的活性探测：Poll + 零字节读
            if (socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0)
            {
                // 可读但无数据 → 对端已关闭
                return false;
            }
            return socket.Connected;
        }
        return false;
    }
    catch
    {
        return false;
    }
}
```

---

## 🟡 轻微问题

### 问题 #8：MatPool 的 ConcurrentBag.Count 竞态

- **文件**：`Acme.Product.Infrastructure/Memory/MatPool.cs` 第 108 行
- **严重性**：🟡 P3

`ConcurrentBag<T>.Count` 内部需获取所有线程本地列表的锁（O(n)），高并发时影响性能。建议使用 `Interlocked` 原子计数器替代。

### 问题 #9：前端 setInterval 未保存引用

- **文件**：`Acme.Product.Desktop/wwwroot/src/app.js` 第 1675 行
- **严重性**：🟡 P3

```javascript
setInterval(updateStatusBar, 1000); // 无法 clearInterval
```

WebView2 桌面应用中影响极小，但建议保存引用以便需要时清理。

### 问题 #10：ImageAcquisitionService 连续采集时每帧 Clone + Base64

- **文件**：`Acme.Product.Infrastructure/Services/ImageAcquisitionService.cs` 第 196-214 行
- **严重性**：🟡 P3

连续采集循环每帧都做 `mat.Clone()`（写入缓存）和 `Convert.ToBase64String()`（传给回调），双重 GC 压力。

---

## ✅ 表现良好的设计

| 模块 | 评价 |
|------|------|
| **ImageWrapper** 引用计数 + CoW | 精确控制 Mat 生命周期，避免不必要拷贝，写时复制从内存池取缓冲块 |
| **MatPool** 分桶内存池 | 总量上限（4GB）+ 每桶上限（32）双重防护，有 Trim 接口供空闲时回收 |
| **FlowExecutionService** 取消/超时 | CTS 链接 + finally 清理 + 30 秒算子超时保护，鲁棒性好 |
| **MindVisionCamera.ForceRelease** | 无条件释放模式，避免 SDK 句柄残留，异常安全 |
| **OperatorPreviewService** | 有 `DisposeImageCarriers` 递归释放输出中所有 Mat/ImageWrapper |

---

## 📋 修复优先级总表

| 优先级 | 问题编号 | 描述 | 崩溃风险 | 修复难度 |
|--------|----------|------|----------|----------|
| **P0** | #1 | ImageCache 无限增长 | 数小时 OOM | ⭐⭐ 中 |
| **P0** | #3 | ONNX 模型缓存不释放 | GPU 显存耗尽 | ⭐⭐ 中 |
| **P0** | #2 | DebugCache / ExecutionStatuses 不清理 | 累积泄漏 | ⭐ 低 |
| **P1** | #4 | CTS 泄漏 + Fire-and-forget | 静默失败 | ⭐⭐ 中 |
| **P1** | #5 | PNG 编码瓶颈 | 帧率低 | ⭐ 低 |
| **P2** | #6 | 36MB Pinned 缓冲区 | LOH 碎片化 | ⭐ 低 |
| **P2** | #7 | TCP 连接活性检测 | 通信异常 | ⭐ 低 |
| **P3** | #8 | ConcurrentBag.Count | 高并发性能差 | ⭐ 低 |
| **P3** | #9 | 前端 setInterval | 影响极小 | ⭐ 低 |
| **P3** | #10 | 连续采集 Clone + Base64 | GC 压力 | ⭐⭐ 中 |

---

## 建议修复顺序

1. **第一轮（紧急）**：修复 #1 ImageCache + #3 ONNX 缓存 → 消除 OOM 崩溃风险
2. **第二轮（重要）**：修复 #2 DebugCache + #4 CTS 管理 + #5 PNG 编码 → 提升长期稳定性
3. **第三轮（优化）**：修复 #6 #7 #8 → 精细化资源管理
