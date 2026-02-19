# ClearVision 综合开发路线图

> **文档版本**: V1.0  
> **制定日期**: 2026-02-19  
> **输入来源**: 技术债务分析报告 × 算子库缺口分析  
> **核心目标**: 以"自然语言一键生成工业视觉工程"为北极星，分阶段完成基础架构补强、算子扩充与 AI 编排接入

---

## 总览：为什么这个顺序

在开始之前，先明确一个思路：**AI 编排功能的上限，完全取决于底层算子库和执行引擎的能力上限**。

如果现在立刻接入 LLM，它能生成工程，但：
- 遇到"测量 8 个螺钉孔是否都合格"——引擎无法遍历集合，生成失败
- 遇到"判断直径误差是否在 ±0.05mm 内"——没有数值计算算子，生成失败
- 遇到"识别产品喷码日期"——没有 OCR 算子，生成失败
- 长时间运行后——非托管内存泄漏，进程崩溃

所以正确的顺序是：**先把地基打牢，再建楼**。

```
Sprint 1 → Sprint 2 → Sprint 3 → Sprint 4(AI接入)
  修内功      修内功      加功能        AI编排
 (架构债务)  (执行引擎)  (算子扩充)
```

---

## Sprint 1：消灭致命技术债（2~3 周）

> **目标**: 解决会导致系统不稳定或 AI 编排完全失效的 P0/P1 级问题。这一阶段不增加新功能，专注"把现有的做稳"。

---

### Task 1.1 — 非托管内存泄漏治理 ⚠️ 最优先

**优先级**: P0  
**预估工时**: 3~4 天  
**相关文件**: `FlowExecutionService.cs`, `OperatorBase.cs`, `ImageWrapper.cs`

**问题描述**

`ImageWrapper` 持有 OpenCvSharp 的 `Mat` 指针（非托管 C++ 内存）。当前执行引擎依赖 .NET GC 回收这些对象，但 GC 不感知非托管内存压力，在高帧率场景下会造成 OOM 崩溃。

**解决方案**

在 `FlowExecutionService` 中为每次流程执行建立一个 `ExecutionScope`，统一追踪本次执行产生的所有中间 `ImageWrapper`，在最后一个下游算子消费完成后立即 `Dispose()`。

**具体实现步骤**

```csharp
// 1. 在 FlowExecutionService 中引入执行上下文
public class ExecutionContext : IDisposable
{
    private readonly List<IDisposable> _intermediateResources = new();

    public void Track(IDisposable resource) => _intermediateResources.Add(resource);

    public void Dispose()
    {
        foreach (var r in _intermediateResources)
            r.Dispose();
        _intermediateResources.Clear();
    }
}

// 2. 修改算子执行循环
using var ctx = new ExecutionContext();
foreach (var layer in executionLayers)
{
    foreach (var op in layer)
    {
        var result = await ExecuteOperatorAsync(op, inputs, ctx);
        // 算子输出的 ImageWrapper 由 ctx 托管
    }
}
// 离开 using 块，所有中间图像自动释放
```

**验收标准**

- 在 1080P 相机、30fps 下连续运行 1 小时，内存占用稳定（波动 < 50MB）
- 流程执行完毕后，任务管理器中进程内存立即回落到基线水平

---

### Task 1.2 — 数据类型系统重构：增加 List/Array 端口类型

**优先级**: P1  
**预估工时**: 4~5 天  
**相关文件**: `OperatorEnums.cs`（PortDataType 枚举）、`VisionValueObjects.cs`、`flowCanvas.js`（PORT_TYPE_COLORS）

**问题描述**

`BlobAnalysis` 输出的多个 Blob 被打包进 `Contour` 类型，`DeepLearning` 输出的多个检测框也只能用 JSON 字符串传递。下游算子无法通过标准端口拆解这些集合，导致多目标场景完全无法处理。

**解决方案：新增 3 个端口类型**

在 `PortDataType` 枚举中新增：

```csharp
// Acme.Product.Core/Enums/OperatorEnums.cs
public enum PortDataType
{
    Image = 0,
    Integer = 1,
    Float = 2,
    Boolean = 3,
    String = 4,
    Point = 5,
    Rectangle = 6,
    Contour = 7,
    // 新增 ↓
    PointList = 8,       // List<Point>  用于替代 Contour 传递点集
    DetectionResult = 9, // 单个检测框：{ BoundingBox, Label, Confidence }
    DetectionList = 10,  // List<DetectionResult> 替代 Contour 传递多目标结果
    Any = 99
}
```

**配套修改**

- `flowCanvas.js` 的 `PORT_TYPE_COLORS` 中为新类型配色
- `checkTypeCompatibility` 更新兼容矩阵
- `DeepLearning` 算子的输出端口 `Defects` 从 `Contour(7)` 改为 `DetectionList(10)`
- `BlobAnalysis` 算子的输出端口 `Blobs` 从 `Contour(7)` 改为 `DetectionList(10)`

**验收标准**

- `DeepLearning` 输出的 `DetectionList` 可以被新的 `ArrayIndexer` 算子（见 Sprint 3）正确拆解
- 画布上不同颜色的端口能正确区分新旧类型

---

### Task 1.3 — 复合几何类型升级：Circle / Line 作为一等公民

**优先级**: P1  
**预估工时**: 2~3 天  
**相关文件**: `VisionValueObjects.cs`, `CircleMeasurement.cs`, `LineMeasurement.cs`

**问题描述**

`CircleMeasurement` 输出散装的 `CenterX`、`CenterY`、`Radius` 三个独立端口。当 AI 生成"测量两个圆的圆心距"这类工程时，需要连 6 根线，且极容易出现"A 圆半径连到 B 圆中心"的错连。

**解决方案：新增复合几何端口类型**

```csharp
// Acme.Product.Core/ValueObjects/VisionValueObjects.cs
// 新增 C# 结构体
public record struct CircleData(float CenterX, float CenterY, float Radius);
public record struct LineData(float X1, float Y1, float X2, float Y2, float Angle);

// PortDataType 枚举新增
CircleData = 11,  // 圆（中心点 + 半径）
LineData = 12,    // 直线（两端点 + 角度）
```

**算子输出端口改造**

- `CircleMeasurement`：保留散装输出（向下兼容），**新增** `Circle(11)` 端口输出完整圆对象
- `LineMeasurement`：**新增** `Line(12)` 端口输出完整直线对象
- `Measurement`（距离测量）：**新增** `Point1` 和 `Point2` 输入端口（`Point` 类型），允许从上游算子直接接入圆心坐标，而不必手动填参数

**验收标准**

- "测量两圆圆心距"可以用 3 根线完成（圆1.Circle → 距离测量.Point1，圆2.Circle → 距离测量.Point2，图像 → 距离测量.Image）
- AI 生成此类工程的错连率显著下降

---

## Sprint 2：执行引擎能力突破（2~3 周）

> **目标**: 解决引擎层面"无法遍历集合"的根本限制，解锁多目标检测工程的编排能力。这是 AI 能否生成真实产线工程的核心门槛。

---

### Task 2.1 — ForEach 遍历算子与子图执行机制

**优先级**: P0（AI 编排视角）  
**预估工时**: 7~10 天  
**相关文件**: `FlowExecutionService.cs`, `OperatorFlow.cs`, 前端 `flowCanvas.js`

**问题描述**

当前执行引擎基于纯 DAG 拓扑排序，严格禁止环路。而"对检测到的每个目标执行相同操作"是最常见的需求，无法满足。

**解决方案：SubGraph（子图）机制**

引入 `ForEachOperator`，它内部包含一个独立的子流程（`SubGraph`）。`ForEachOperator` 不破坏外部 DAG 的无环性，循环发生在其内部。

**架构设计**

```
外部流程（DAG，无环）：
[图像采集] → [YOLO检测] → [ForEach] → [结果汇总] → [输出]
                               ↑
                         内部子图（也是 DAG）：
                         [单个检测框] → [ROI裁剪] → [精细分析] → [判断]
```

**实现要点**

```csharp
// 1. ForEachOperator 的输入/输出
// 输入：DetectionList(10) - 上游检测到的目标列表
// 输出：ResultList(Any)  - 对每个目标执行子图后的结果列表

// 2. ForEachOperator.Execute() 的核心逻辑
public override async Task<OperatorExecutionResult> ExecuteAsync(...)
{
    var detectionList = inputs.Get<List<DetectionResult>>("Items");
    var results = new List<object>();

    foreach (var item in detectionList)
    {
        // 为子图注入当前迭代项
        var subInputs = new Dictionary<string, object> { ["CurrentItem"] = item };
        // 执行子图（子图本身也是一个独立的 OperatorFlow，复用现有执行引擎）
        var subResult = await _subFlowExecutor.ExecuteAsync(SubGraph, subInputs);
        results.Add(subResult.Outputs["Result"]);
    }

    return Success(new { Results = results, Count = results.Count });
}
```

**前端改造要点**

- 画布上 `ForEach` 节点视觉上呈现为可展开/折叠的"容器节点"
- 双击 `ForEach` 节点进入子图编辑模式（嵌套画布）
- 子图有独立的 `CurrentItem` 输入源节点（自动注入，不需要连线）

**验收标准**

- 可以构建"检测图中所有螺钉孔并对每个孔执行圆度测量"的完整流程
- ForEach 内部的子图执行结果正确汇总
- 嵌套层级支持至少 2 层（ForEach 内部嵌套另一个 ForEach）

---

### Task 2.2 — ArrayIndexer 与 JsonExtractor 配套算子

**优先级**: P1（依赖 Task 1.2 完成）  
**预估工时**: 2 天

**ArrayIndexer 算子**

```
功能：从 DetectionList 中按索引提取单个 DetectionResult
输入：Items(DetectionList), Index(Integer)
输出：Item(DetectionResult), BoundingBox(Rectangle), Label(String), Confidence(Float)
典型用法：提取"置信度最高的那个目标"用于后续处理
```

**JsonExtractor 算子**

```
功能：从 JSON 字符串中按 JSONPath 提取字段值
输入：Json(String), Path(String，如 "$.results[0].score")
输出：Value(Any), ValueAsString(String), ValueAsFloat(Float)
典型用法：解析 ResultOutput 输出的 JSON 做后续判断
```

---

## Sprint 3：核心算子扩充（2~3 周）

> **目标**: 补齐 AI 编排时出现频率最高但当前完全缺失的算子。按投入产出比排序执行。

---

### Task 3.1 — MathOperation（数值计算算子）🔴 最高优先级

**预估工时**: 2 天  
**解锁场景**: 所有涉及公差判断的测量工程

**设计规格**

```
算子类型: MathOperation
显示名称: 数值计算

输入端口:
  ValueA (Float) - 必需，操作数A
  ValueB (Float) - 可选，操作数B（单目运算时不需要）

输出端口:
  Result (Float) - 计算结果
  IsPositive (Boolean) - 结果是否为正数

参数:
  Operation (enum):
    Add      → A + B
    Subtract → A - B
    Multiply → A × B
    Divide   → A ÷ B（B=0时输出0并记录警告）
    Abs      → |A|
    Min      → min(A, B)
    Max      → max(A, B)
    Power    → A^B
    Sqrt     → √A
    Round    → round(A, Decimals)
  Decimals (int, default=3) - 保留小数位数（Round时使用）
```

**典型工程示例（AI 生成后的样子）**

```
圆测量#1.Radius ──→ MathOperation(Subtract).ValueA
圆测量#2.Radius ──→ MathOperation(Subtract).ValueB
                    MathOperation.Result → MathOperation#2(Abs).ValueA
                                          MathOperation#2.Result → ConditionalBranch(LessThan 0.05).Value
```

---

### Task 3.2 — LogicGate（逻辑门算子）🔴 最高优先级

**预估工时**: 1 天  
**解锁场景**: 多条件综合判断（AND/OR）

**设计规格**

```
算子类型: LogicGate
显示名称: 逻辑运算

输入端口:
  InputA (Boolean) - 必需
  InputB (Boolean) - 可选（NOT 运算时不需要）

输出端口:
  Result (Boolean)

参数:
  Operation (enum): AND / OR / NOT / XOR / NAND / NOR
```

---

### Task 3.3 — TypeConvert（类型转换算子）🔴 最高优先级

**预估工时**: 1 天  
**解锁场景**: 不同类型端口之间的数据流转

**设计规格**

```
算子类型: TypeConvert
显示名称: 类型转换

输入端口:
  Value (Any) - 任意类型输入

输出端口:
  AsString  (String)
  AsFloat   (Float)
  AsInteger (Integer)
  AsBoolean (Boolean)

参数:
  Format (string, default="") - 转字符串时的格式（如 "F2" 保留两位小数）
```

---

### Task 3.4 — StringFormat（字符串格式化算子）

**预估工时**: 1 天  
**解锁场景**: 生成检测报告、日志记录

**设计规格**

```
算子类型: StringFormat
显示名称: 字符串格式化

输入端口:
  Arg0 ~ Arg4 (Any) - 最多 5 个参数，可选

输出端口:
  Result (String)

参数:
  Template (string) - 模板字符串，使用 {0}~{4} 占位符
                      示例: "零件ID:{0}, 直径:{1:F2}mm, 状态:{2}"
```

---

### Task 3.5 — ImageSave（图像存档算子）

**预估工时**: 0.5 天  
**解锁场景**: 保存 NG 图像、建立缺陷数据集

**设计规格**

```
算子类型: ImageSave
显示名称: 图像保存

输入端口:
  Image (Image) - 要保存的图像
  FileName (String) - 可选，自定义文件名（不含扩展名）

输出端口:
  FilePath (String) - 实际保存路径
  Success (Boolean)

参数:
  SaveDirectory (string) - 保存目录
  Format (enum: jpg/png/bmp, default=jpg)
  Quality (int, 1-100, default=90)
  AutoNaming (bool, default=true) - 自动用时间戳命名
  NamingTemplate (string, default="{yyyy-MM-dd_HH-mm-ss-fff}")
  OnlyOnNg (bool, default=false) - 仅在上游判断为 NG 时保存
```

---

### Task 3.6 — OcrRecognition（字符识别算子）

**预估工时**: 5~7 天（集成 PaddleOCR 或 Tesseract）  
**解锁场景**: 喷码日期识别、铭牌文字识别、芯片字符检测

**设计规格**

```
算子类型: OcrRecognition
显示名称: OCR字符识别

输入端口:
  Image (Image) - 含文字的图像

输出端口:
  Image (Image) - 标注识别结果的图像
  Text (String) - 完整识别文本
  Lines (String) - 按行分割的结果（换行符分隔）
  Confidence (Float) - 平均置信度
  Count (Integer) - 识别到的文字块数量

参数:
  Language (enum: Chinese/English/ChineseEnglish, default=ChineseEnglish)
  Mode (enum: Print/Handwriting, default=Print)
  CharFilter (string, default="") - 正则过滤，如 "\d{8}" 只提取8位数字
  MinConfidence (double, 0-1, default=0.5) - 最低置信度过滤
```

**推荐集成方案**

优先选 PaddleOCR（`PaddleOCRSharp` NuGet 包）：中文识别效果好，速度快，ONNX 导出的模型可离线使用，与现有 ONNX Runtime 基础设施一致。

---

### Task 3.7 — ImageDiff（图像差异检测算子）

**预估工时**: 3 天  
**解锁场景**: 与参考良品对比的缺陷检测，无需训练 AI 模型

**设计规格**

```
算子类型: ImageDiff
显示名称: 图像差异检测

输入端口:
  Image (Image) - 待检测图像
  Reference (Image) - 参考良品图像

输出端口:
  DiffImage (Image) - 差异热图（高亮差异区域）
  DiffScore (Float) - 整体差异分数（0=完全相同，100=完全不同）
  DiffContours (Contour) - 差异区域轮廓列表
  DiffCount (Integer) - 差异区域数量

参数:
  Threshold (double, 0-255, default=30) - 像素差异阈值
  MinDiffArea (int, default=100) - 最小差异区域面积过滤
  AlignMode (enum: None/FeatureAlign, default=None) - 是否自动对齐后再比较
  Method (enum: Absolute/SSIM, default=Absolute)
```

---

### Task 3.8 — NPointCalibration（九点标定算子）

**预估工时**: 4~5 天  
**解锁场景**: 机械臂视觉引导（相机坐标系 → 机械臂基坐标系转换）

**设计规格**

```
算子类型: NPointCalibration
显示名称: N点手眼标定

输入端口:
  Image (Image) - 标定图像
  RobotX (Float) - 当前点的机械臂X坐标（标定时逐点输入）
  RobotY (Float) - 当前点的机械臂Y坐标

输出端口:
  CalibMatrix (String) - 仿射变换矩阵（JSON 格式，保存到文件）
  IsCalibComplete (Boolean) - 是否已采集足够标定点
  PointCount (Integer) - 已采集点数量
  ReprojectionError (Float) - 重投影误差（越小越好）

参数:
  PointCount (int, default=9) - 需要采集的标定点数量
  CalibFile (file) - 标定结果保存路径
  DetectMode (enum: ManualClick/AutoFiducial) - 图像点获取方式
```

---

### Task 3.9 — Statistics（统计算子）

**预估工时**: 2 天  
**解锁场景**: 生产质量统计（CPK、良品率、均值趋势）

**设计规格**

```
算子类型: Statistics
显示名称: 统计分析

输入端口:
  Value (Float) - 每次检测的数值（流式输入，每帧更新一次）
  Reset (Boolean) - 可选，收到 True 时清空历史数据

输出端口:
  Mean (Float) - 均值
  StdDev (Float) - 标准差
  Min (Float) - 最小值
  Max (Float) - 最大值
  Count (Integer) - 样本数量
  Cpk (Float) - 过程能力指数（需要配置 USL/LSL 后才有效）

参数:
  WindowSize (int, default=0) - 滑动窗口大小（0=全量统计）
  USL (double) - 上规格限（用于 Cpk 计算）
  LSL (double) - 下规格限（用于 Cpk 计算）
```

---

## Sprint 4：AI 编排接入（1~2 周）

> **目标**: 在前三个 Sprint 完成后，接入 LLM 实现自然语言生成工程。此时基础已稳固，AI 编排的成功率将大幅提升。
>
> **注意**: 此阶段的具体实现方案已在《ClearVision_AI工程生成_开发指导文档.md》中完整描述，本文档不重复，只记录前置条件检查。

**Sprint 4 开始前的检查清单**

- [ ] Task 1.1 内存管理完成（7×24 运行无 OOM）
- [ ] Task 1.2 DetectionList 类型已上线（DeepLearning 输出已更新）
- [ ] Task 1.3 复合几何类型已上线（CircleMeasurement 新增 Circle 端口）
- [ ] Task 2.1 ForEach 算子可用（多目标场景可编排）
- [ ] Task 3.1 MathOperation 可用（公差判断可编排）
- [ ] Task 3.2 LogicGate 可用（多条件判断可编排）
- [ ] Task 3.3 TypeConvert 可用（跨类型数据流可编排）

满足以上 7 条，AI 编排的覆盖率可达到约 90%。

---

## 优先级与工时汇总

### 按优先级排序

| 优先级 | Task | 内容 | 预估工时 |
|--------|------|------|----------|
| 🔴 P0 | 1.1 | 非托管内存泄漏治理 | 3~4 天 |
| 🔴 P0 | 2.1 | ForEach 子图执行机制 | 7~10 天 |
| 🟠 P1 | 1.2 | List/Array 端口类型 | 4~5 天 |
| 🟠 P1 | 1.3 | 复合几何类型（Circle/Line） | 2~3 天 |
| 🟠 P1 | 3.1 | MathOperation 数值计算 | 2 天 |
| 🟠 P1 | 3.2 | LogicGate 逻辑门 | 1 天 |
| 🟠 P1 | 3.3 | TypeConvert 类型转换 | 1 天 |
| 🟡 P2 | 2.2 | ArrayIndexer / JsonExtractor | 2 天 |
| 🟡 P2 | 3.4 | StringFormat 字符串格式化 | 1 天 |
| 🟡 P2 | 3.5 | ImageSave 图像存档 | 0.5 天 |
| 🟡 P2 | 3.6 | OcrRecognition OCR识别 | 5~7 天 |
| 🟡 P2 | 3.7 | ImageDiff 图像差异检测 | 3 天 |
| 🟢 P3 | 3.8 | NPointCalibration 九点标定 | 4~5 天 |
| 🟢 P3 | 3.9 | Statistics 统计分析 | 2 天 |
| —— | S4 | AI 编排接入 | 5~7 天 |

**总计预估工时**: 47~60 天（约 10~12 工作周）

---

### 按 Sprint 排列的交付节奏

```
Week 1-2        Week 3-4        Week 5-6          Week 7-9         Week 10-12
  Sprint 1        Sprint 1         Sprint 2           Sprint 3         Sprint 4
  [内存治理]   [类型系统重构]   [ForEach引擎]    [算子扩充×7个]    [AI接入+联调]
  Task 1.1       Task 1.2         Task 2.1           Task 3.1-3.7     Task AI
                 Task 1.3         Task 2.2
```

---

## 附录：每个 Sprint 完成后，AI 编排能力的变化

| 阶段 | 可成功编排的场景类型 | 预估成功率 |
|------|---------------------|-----------|
| 当前（Sprint 0） | 简单线性流程（单目标检测、条码识别） | ~60% |
| Sprint 1 完成后 | 数值运算、多条件判断、稳定长期运行 | ~70% |
| Sprint 2 完成后 | 多目标遍历检测（每个螺钉孔单独测量） | ~80% |
| Sprint 3 完成后 | OCR识别、差异检测、完整报告生成 | ~92% |
| Sprint 4 完成后 | 自然语言一键生成以上所有类型工程 | ~92%（AI接入） |

---

*文档维护：ClearVision 开发团队*  
*下次评审节点：Sprint 1 完成后*
