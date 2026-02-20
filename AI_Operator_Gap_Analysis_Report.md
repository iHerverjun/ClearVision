# ClearVision AI 工作流自动生成功能 —— 算子与核心能力差距评估报告

> **生成日期**: 2026-02-21
> **评估目标**: 分析现有项目架构、算子库定义及 AI Prompt 工程的现状，评估是否需要新增算子或功能，以最大化提升大语言模型 (LLM) 生成工业视觉缺陷检测工作流的成功率。
> **评估基准**: `OperatorFactory.cs` (已注册约 63 个算子)、`OperatorEnums.cs`、`AI_WORKFLOW_GENERATION_IMPLEMENTATION_GUIDE.md` 及 V4 开发路线图。

---

## 总体评估摘要

当前项目的算子体系已涵盖从图像采集、预处理、特征提取到深度学习缺陷检测、逻辑判断及通信输出的完整链路，**基本不存在工业流程上的根本性功能缺失**。

然而，从 **"对大语言模型 (LLM) 的友好度"** 和 **"自然语言到工作流的转化效率"** 角度来看，系统存在显著的局限性。主要体现在：元数据不够丰富导致 LLM 选词困难；缺少处理中间状态的"胶水"算子导致连线逻辑异常复杂；以及现有的 Prompt 工程（尤其是 Few-Shot 示例）存在会导致 LLM 产生幻觉的硬性错误。

本报告将问题分为 **A（严重阻碍/基础缺陷）**、**B（能力缺失/胶水算子）**、**C（易错设计/端口定义）** 三个层级，并提供清晰的落地建议。

---

## A 类问题：严重阻碍与 Prompt 基础缺陷（高优先级）

### A1. `Keywords` 字段未实现填充致使 AI 检索失效
* **现状**: 在 `OperatorFactory.cs` 中，所有的 `OperatorMetadata` 均未定义或填充 `Keywords` 属性，但后端逻辑在生成 System Prompt 的算子名录时，极度依赖 `Keywords` 帮助模型建立自然语言到具体算子的映射。
* **影响**: 当用户输入"请帮我建一个**找茬**的工作流"或"需要做**去噪**处理"时，LLM 很难准确关联到 `ImageDiff` 或 `BilateralFilter`、`MedianBlur` 等具体算子，极易产生随机选择或编造算子名的情况。
* **改进建议**: 紧急为所有算子补充 5~15 个中英双语关键词。如：`BlobAnalysis` 补充 `["连通域", "缺陷区域", "斑点", "面积提取", "缺陷分析"]`。

### A2. Few-Shot 示例存在非法的连线指导
* **现状**: 在 `AI_WORKFLOW_GENERATION_IMPLEMENTATION_GUIDE.md` 的示例 4（圆规测量）中，存在将 `CircleMeasurement` 的 `Center` (Point 类型) 连接到 `Measurement` 的 `Image` (Image 类型) 的指导；同时存在将 `Distance` (Float) 连到 `PixelX` (不存在的端口) 的错误配置。
* **影响**: LLM 会严格学习这几个少样本示例，导致它认为可以跨类型连线，从而在后续生成中抛出大量 `FlowLinter` 验证错误（类型不兼容）。
* **改进建议**: 立即修正 Prompt 中引用的示例 JSON，确保其完全符合 `PortDataType` 的连线规则。

### A3. `MathOperation` 算子设计规格未完全兑现
* **现状**: 在路线图任务 3.1 的规划中，`MathOperation` 设计应包含 `Result(Float)` 和 `IsPositive(Boolean)` 两个输出端口，但 `OperatorFactory.cs` 的实现中漏掉了 `IsPositive` 输出。
* **影响**: 在缺乏专用比较算子的情况下，这是一个将数值计算转化为布尔逻辑的重要通道。缺失它会导致后续的 `ConditionalBranch` 无法简便地读取计算走向。
* **改进建议**: 补充添加 `IsPositive` 端口，完善实现细节。

---

## B 类问题：缺失的 AI 友好型"胶水"算子（中优先级）

虽然专用算子丰富，但将离散算子拼接成复杂业务流时，缺少适合 AI 理解的中介控制节点。

### B1. 强烈建议新增：`Comparator`（数值比较算子）
* **现状**: 评估是否超限（如"如果半径大于 5mm"）当前必须依赖 `ConditionalBranch` 或间接方式，但 `ConditionalBranch` 的定义较为重型。
* **需求**: 需要一个纯粹处理数值大小关系的算子。AI 模型非常擅长处理典型的对比逻辑。
* **规格建议**: 
  * **输入**: `ValueA(Float)`, `ValueB(Float)`
  * **输出**: `Result(Boolean)`, `Difference(Float)`
  * **参数**: `Condition` (大于/等于/小于/在范围内等), `CompareValue` (ValueB悬空时的默认值)

### B2. 建议新增：`Aggregator`（数据聚合分析算子）
* **现状**: 虽然有针对单个数值的 `Statistics` 计算，但若用户要求"统计这三个圆里最大的直径"，目前的系统需通过 `ForEach` 组合，这大幅超出了通常 LLM 在单次 JSON 输出中的规划能力。
* **需求**: 简单合并与聚合数据的节点。
* **规格建议**:
  * **输入**: `Value1(Any)`, `Value2(Any)`, `Value3(Any)`...
  * **输出**: `MergedList(Any)`, `MaxValue(Float)`
  * **参数**: `Mode` (合并数组 / 提取极值)

### B3. 建议新增：`Delay`（延时算子）与 `Comment`（注释算子）
* **需求**: 工业场景下涉及 `Modbus` / `TCP` 等外部通信时，常需要几百毫秒延时以等待下位机准备。同时，提供 `Comment` 算子能让 LLM 在生成结果中写入设计意图，方便人类排查。

---

## C 类问题：端口语义定义与规范（低优先级优化）

此类问题不会引发阻断错误，但容易增加 LLM 的计算负荷：

### C1. `Measurement` 算子输入不接受 `Point`
* **现状**: 该算子当前需依赖 `Image` 才能执行，且通过参数设置起点和终点坐标。但是工业流程中常常需要"测量两个前置检测输出点之间的距离"（如 `CircleMeasurement.Center` 和 `TemplateMatching.Position`）。
* **改进建议**: 为 `Measurement` 增加 `PointA(Point)` 和 `PointB(Point)` 两个可选输入端口，实现无图物理测量。

### C2. 端口命名空间未统一
* **现状**: `LogicGate` 使用 `In1`, `In2`, `Out`；而 `MathOperation` 使用 `ValueA`, `ValueB`, `Result`。
* **改进建议**: 统一使用语义化更强的 `InputA`/`InputB`/`Result`，在提示词中减少模型的混淆。注意这属于可能引发旧数据兼容性问题的 Breaking Change。

### C3. `Description` 的解释深度不足
* **现状**: 多数 `Description` 过度简略，如 `Filtering` 只描述了 "图像滤波降噪"。
* **改进建议**: 扩展为 "利用均值/高斯核去除高斯噪声，适用于金属表面检测预处理"，以强化模型对真实场景的使用关联。

---

## 最佳演进建议 (Next Steps)

1. **Step 1 (Day 1)**: 优先解决 **A 类问题**。这是 ROI 最高的改动：不涉及大面积重构，只需在 `OperatorFactory` 补充 `Keywords`、少许扩充 `Description`，并修改 Prompt 提供的高质量示例。预计此项可立即使 AI 工作流生成的 First-Time-Truth (FTT) 成功率提高 15% 以上。
2. **Step 2 (Day 2)**: 实现 **B1 (`Comparator`)** 和 **C1 (`Measurement` 改造)**，打通纯参数流运算瓶颈。
3. **Step 3 (Day 3+)**: 继续扩展其他胶水算子及增强整体稳定性。

**总结**: 核心工作应转向 **"优化 AI 对现有算子能力的理解"**，而非盲目增加新的核心图像处理算子。
