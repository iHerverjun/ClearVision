# 审计报告交叉核查勘误表

**核查时间**：2026-02-26
**核查范围**：7 份审计报告（Batch1 ~ Batch7）中所有关键断言
**核查方式**：回到算子源代码逐行比对报告中描述的代码行为

---

## 🔴 发现的误报 (False Positives)

### 误报 #1：Batch6 报告中称 `DeepLearningOperator` 存在 ArrayPool 张量踩踏（P1 → 降级为 ✅ 无此问题）

**原报告声称**：`PreprocessImage` 方法在 `finally` 块中调用了 `ArrayPool<float>.Shared.Return(tensorData, clearArray: true)` 后，下游的 `DenseTensor` 仍然共享同一块数组内存，导致数据被覆写或清零。

**实际代码（第 369 行）**：
```csharp
return new DenseTensor<float>(tensorData[..tensorSize].ToArray(), new[] { 1, 3, inputSize, inputSize });
```

**核查结论**：`.ToArray()` 方法会**创建并返回一个全新的数组副本**。因此 `DenseTensor` 持有的是独立的数据副本，与 `ArrayPool` 返还的原始数组没有任何关系。`finally` 中的 `Return` 操作不会影响到张量数据。**此 P1 缺陷不存在，属于误报。**

---

### 误报 #2：Batch6 报告中称 `DeepLearningOperator` 的 ONNX 推理结果未释放导致内存泄漏（P1 → 降级为 ✅ 无此问题）

**原报告声称**：`RunInference` 中 `session.Run(inputs)` 的返回的 `IDisposableReadOnlyCollection` 在代码中没有被 `Dispose()`，导致非托管内存泄漏。

**实际代码（第 393 行）**：
```csharp
using var results = session.Run(inputs);
```

**核查结论**：代码使用了 C# 8.0 的 `using var` 声明式语法。`results` 会在 `RunInference` 方法返回时自动调用 `Dispose()`。同时第 400 行在返回张量时也调用了 `.ToArray()` 进行了数据深拷贝，不依赖原始非托管内存。**此 P1 缺陷不存在，属于误报。**

---

## 🟡 需要修正措辞的断言

### 修正 #1：Batch5 对 `UndistortOperator` 的描述需要细化

**原报告声称**：`catch {}` 吞掉异常后，`calInfo.CameraMatrix != null` 永远为 false，导致算子静默使用硬编码假矩阵。

**实际情况更加微妙**：
- `catch {}` 确实吞掉了异常（第 76 行）。
- 但在第 78-80 行，代码检查了 `if (calInfo == null)` 并在 calInfo 为 null 时直接返回了失败。
- **真正的问题**是：如果 JSON 的顶层结构能被解析为 `CalibrationInfo`（例如 JSON 中有 `ImageWidth` 等字段），但 `CameraMatrix` 字段因为 `double[,]` → `double[]` 的类型不匹配而被赋值为 `null`，那么 `calInfo` 本身不为 null，但 `calInfo.CameraMatrix` 为 null。此时代码会执行到第 84-89 行的硬编码伪矩阵。
- 所以核心结论"标定数据被静默丢弃、使用伪造矩阵"是**基本正确**的，但触发条件需要更准确地描述为："当 JSON 整体结构可解析但 CameraMatrix 字段类型不匹配时"。如果 JSON 完全无法解析（比如格式错误），`calInfo` 直接为 null，算子会正确返回失败。

**数据契约不兼容问题（P1）本身依然成立**，只是故障模式的描述需要更精确。

---

### 修正 #2：Batch5 对 `CameraCalibrationOperator` 单图模式的描述补充

**原报告声称**：单图模式"仅仅调用了 FindChessboardCorners 后就直接返回了，没有调用 CalibrateCamera"。

**实际代码核查**：确认无误。单图模式 `ExecuteSingleImageCalibration`（第 67-171 行）的输出 JSON 中包含了 `Corners` 和 `ObjectPoints`，但**没有** `CameraMatrix` 或 `DistCoeffs`。因此即使用户从单图模式获得了 `CalibrationData` JSON，送入 `UndistortOperator` 后，反序列化出的 `CameraMatrix` 也必定为 null。

**此断言正确**，但未来可以补充说明：单图模式的设计意图可能本身就是"角点检测预览"而非"标定"，问题在于 UI 上将两者混在同一个算子的同一个参数下，缺乏明确指引。

---

## ✅ 已确认正确的关键断言

| 批次 | 断言 | 核查结果 |
|------|------|----------|
| Batch1 | `BoxFilterOperator` 是候选框筛选而非均值滤波 | ✅ 确认正确 |
| Batch1 | `FrameAveragingOperator` Median 模式三层循环逐像素排序 | ✅ 确认正确（第 149-161 行） |
| Batch2 | 两个形态学算子几乎 100% 功能重复 | ✅ 确认正确 |
| Batch3 | `ShapeMatchingOperator` 的 `NumLevels` 参数未被使用 | ✅ 确认正确 |
| Batch4 | `StatisticsOperator` 内部持有有状态 `_historyValues` 列表 | ✅ 确认正确 |
| Batch5 | `CameraCalibration` 输出 `double[,]`，`Undistort` 期望 `double[]` | ✅ 确认正确（第 392 行 vs 第 131 行） |
| Batch5 | `UndistortOperator` 硬编码伪造相机矩阵（焦距 = 宽度×0.8） | ✅ 确认正确（第 84-89 行） |
| Batch5 | `PerspectiveTransformOperator` 有 16 个独立坐标参数 | ✅ 确认正确 |
| Batch6 | `OcrRecognitionOperator` 使用全局单例引擎，无线程安全 | ✅ 确认正确 |
| Batch7 | `TextSaveOperator` 同步写文件无并发锁 | ✅ 确认正确 |
| Batch7 | `ConditionalBranchOperator` 正确使用了 `ImageWrapper.AddRef()` | ✅ 确认正确（第 94 行） |

---

## 🔍 补充发现的遗漏问题

### 遗漏 #1：`TypeConvertOperator` 输入端口名称不匹配（Batch7 遗漏）
- **代码第 49 行**：`inputs.TryGetValue("Value", out var value)` 尝试从字典中读取 key 为 `"Value"` 的输入。
- **但定义的 InputPort 名称**（第 34 行）：`[InputPort("Input", "输入", ...)]`，端口名为 `"Input"` 而非 `"Value"`。
- **后果**：这个算子永远读不到输入数据，用 `TryGetValue("Value")` 必定返回 false，算子每次都会返回失败。
- **严重程度**：P1（功能失效）。

### 遗漏 #2：`ImageSubtractOperator` 对多通道图像的 `MinMaxLoc` 调用（Batch7 遗漏）
- **代码第 95 行**：`Cv2.MinMaxLoc(dst, out minVal, out maxVal)`。
- **问题**：`MinMaxLoc` 只能处理单通道图像。如果输入和输出都是 BGR 三通道图像，OpenCV 将抛出异常。应该先转为灰度或者使用 `Cv2.MinMaxIdx`。
- **严重程度**：P2（特定条件崩溃）。

---

## 修订后的 P1 级缺陷全局清单（去除误报，补充遗漏）

| # | 算子 | 问题 | 来源 |
|---|------|------|------|
| 1 | `UndistortOperator` | JSON 2D→1D 数组不兼容 + 硬编码伪矩阵 | Batch5（确认） |
| 2 | `CameraCalibrationOperator` | 单图模式不输出标定矩阵 | Batch5（确认） |
| 3 | `OcrRecognitionOperator` | 全局引擎无线程安全 | Batch6（确认） |
| 4 | `TypeConvertOperator` | InputPort 名 "Input" 与读取 key "Value" 不匹配，功能失效 | Batch7（**新发现**） |
| 5 | `FrameAveragingOperator` | Median 逐像素三层循环排序，性能灾难 | Batch1（确认） |
| 6 | ~~`DeepLearningOperator`~~ | ~~ArrayPool 踩踏~~ | ~~Batch6~~（**⚠️ 已撤销，误报**） |
| 7 | ~~`DeepLearningOperator`~~ | ~~ONNX 张量内存泄漏~~ | ~~Batch6~~（**⚠️ 已撤销，误报**） |
