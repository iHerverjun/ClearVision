# ClearVision 工控机（IPC）性能优化改进计划 V1

本文档针对系统中发现的**致命隐患**（SQLite同步阻塞）和**高危隐患**（LOH大对象堆GC压力）制定了具体的改进方案。将这些优化应用后，可极大提升项目在工控机上的检测帧率、降低延迟抖动。

## 阶段一：消灭数据库同步机制（致命隐患解决）

当前痛点：`InspectionWorker.cs` 每次检测循环结束同步 Await `resultRepository.AddAsync`；`DatabaseWriteOperator.cs` 每帧执行数据库连接创建和建表检查。

### 1.1 启用 SQLite WAL（预写式日志）与异步连接
- **修改文件**：`Acme.Product.Desktop/DependencyInjection.cs` 或 `AppDbContext`
- **操作**：在配置 SQLite 时添加 `PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;`。这将使得读写操作能够并发，并且提升写入性能。

### 1.2 主检测循环解耦（引入后台 Channel 队列）
- **修改文件**：新增 `InspectionResultChannelWriter.cs` 和 `InspectionResultBackgroundService.cs`（继承自 `BackgroundService`）。
- **操作**：
  1. 使用 `System.Threading.Channels.Channel.CreateBounded<InspectionResult>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest })`。
  2. `InspectionWorker.cs` 中，原本的 `await resultRepository.AddAsync` 改为非阻塞的 `channelWriter.TryWrite(result)`。
  3. 后台服务 `InspectionResultBackgroundService` 长连接轮询 Channel 并进行批量 `SaveChangesAsync`。

### 1.3 `DatabaseWriteOperator` 算子连接池化改造
- **修改文件**：`DatabaseWriteOperator.cs`
- **操作**：改写每帧的打开连接与建表逻辑。改为在检测初始化（Scope创建）阶段缓存 `SqliteConnection`，或者是采用 `IServiceProvider` 注入的 `VisionDbContext` 的工厂。去掉运行时的 `CREATE TABLE IF NOT EXISTS`。

---

## 阶段二：消除大对象堆 (LOH) 压力（高危隐患解决）

当前痛点：图像数据过大（很容易超过 85KB），但各个基础算子以及摄像头Mock提供类经常使用 `new byte[...]` 分配缓冲，触发工控机频繁的 STW（Stop-The-World）级别垃圾回收。

### 2.1 全面引入 `ArrayPool<byte>.Shared`
- **目标修改范围**：搜索项目中活跃的 `new byte[` 关键字，排查高频使用的算子。
- **重点重构文件**：
  1. `CodeRecognitionOperator.cs` (行77：`var luminance = new byte[...]`)
  2. `PreviewMetricsAnalyzer.cs` (每次分析图像创建全尺寸数组)
  3. 各类 PLC 通讯 Operator 内部的大数组分配（不过如果未达 85k 就影响稍小）。
- **重构模式**：
  ```csharp
  byte[] buffer = ArrayPool<byte>.Shared.Rent(requiredSize);
  try
  {
      // ... 进行逻辑操作或赋值 ...
  }
  finally
  {
      ArrayPool<byte>.Shared.Return(buffer);
  }
  ```

### 2.2 彻底打通的 `ImageWrapper` 与 OpenCV Mat池
- **修改要求**：利用已有的 `ImageWrapper.cs` 中的 `MatPool.Shared.Rent` 功能。审查是否有算子在执行中间步骤时抛弃了 `ImageWrapper.GetWritableMat()` 而选择自行 `new byte[]`。
- **目标文件**：基础图像处理相关算子（如 `ColorConversionOperator`、`GaussianBlurOperator` 等），确保输入与输出在内存流转中完全依赖池化内建对象。

---

## 验证与验收方案

1. **性能基准测试**：提供一段纯内存模拟的流媒体测试 `MockCamera`，将 `InspectionWorker` 放入循环空转。记录未优化前的最高 TPS（Transaction Per Second），优化后应有大幅提升。
2. **内存Profiler分析**：通过 Visual Studio 或 `dotnet-trace` 持续捕捉 LOH 分配。预期目标：在长时间（> 10分钟）的检测循环中，**0 新增的 `System.Byte[]` LOH 分配**。
