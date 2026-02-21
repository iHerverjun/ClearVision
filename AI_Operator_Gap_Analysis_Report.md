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

---

## 进展更新（2026-02-21）

> 本节为对本报告任务项的代码实况核验结果（基于当前仓库实现）。

### 已完成/部分完成清单（代码核验）

| 编号 | 任务项 | 当前状态 | 说明 |
|------|--------|----------|------|
| A1 | `Keywords` 字段补齐 | **部分完成** | `OperatorMetadata` 已定义 `Keywords`，`PromptBuilder` 已消费该字段；但 `OperatorFactory` 各算子元数据尚未实际填充关键词。 |
| A2 | Few-Shot 示例修正（非法连线） | **未完成** | 示例 4 仍存在 `Center(Point) -> Measurement.Image(Image)` 与 `Distance -> PixelX` 的错误连线示例。 |
| A3 | `MathOperation` 增加 `IsPositive` 输出端口 | **部分完成** | `MathOperationOperator` 执行结果已输出 `IsPositive`；但 `OperatorFactory` 中 `MathOperation` 元数据输出端口仍仅声明 `Result`。 |
| B1 | 新增 `Comparator` 算子 | **部分完成** | 已有 `ComparatorOperator` 实现文件；但未在 `OperatorType` 枚举与 `OperatorFactory` 元数据中完成注册接入。 |
| B2 | 新增 `Aggregator` 算子 | **部分完成** | 已有 `AggregatorOperator` 实现文件；但未在 `OperatorType` 枚举与 `OperatorFactory` 元数据中完成注册接入。 |
| B3 | 新增 `Delay` / `Comment` 算子 | **部分完成** | 已有 `DelayOperator`、`CommentOperator` 实现文件；但未在 `OperatorType` 枚举与 `OperatorFactory` 元数据中完成注册接入。 |
| C1 | `Measurement` 增加 `PointA/PointB` 输入 | **未完成** | 目前 `Measurement` 仍仅支持 `Image` 输入。 |
| C2 | 端口命名统一（如 `InputA/InputB/Result`） | **未完成** | 当前 `LogicGate` 仍使用 `In1/In2/Out` 命名。 |
| C3 | `Description` 语义增强 | **未完成** | 多数描述仍偏简略，尚未完成面向 AI 语义检索的系统增强。 |

### 已落地能力（可复用基础）

1. AI Prompt 目录已支持输出 `keywords` 字段（为后续关键词补齐提供通路）。
2. `GetAllMetadata()` 已在工厂接口与实现中可用，便于 Prompt 动态读取算子目录。
3. `MathOperationOperator` 已具备 `IsPositive/IsZero/IsNegative` 等布尔派生输出（执行层已具备能力）。
4. `Comparator/Aggregator/Delay/Comment` 的算子执行类已存在，可在完成枚举+元数据注册后快速接入工作流。

---

## 下一步 TODO 计划（按优先级）

### P0（当天完成，最高优先级）

- [ ] **修复 A2（Prompt/Few-Shot 硬错误）**
  - [ ] 修正示例 4 中跨类型非法连线。
  - [ ] 删除不存在端口引用（如 `PixelX`）。
  - [ ] 以 `PortDataType` 规则逐条复核全部 Few-Shot 示例。
  - 验收标准：AI 首次生成的 `FlowLinter` 端口类型错误率显著下降，示例 JSON 全量可通过校验。

- [ ] **推进 A1（关键词补齐第一批）**
  - [ ] 为高频算子先补齐关键词（建议首批 20 个：采集/滤波/阈值/测量/识别/通信类）。
  - [ ] 每个算子补充 5~15 个中英双语关键词。
  - 验收标准：Prompt 算子目录中的 `keywords` 不再普遍为空；典型中文口语描述可稳定命中目标算子。

- [ ] **闭环 A3（元数据端口与执行输出一致）**
  - [ ] 在 `OperatorFactory` 的 `MathOperation` 元数据中新增 `IsPositive(Boolean)` 输出端口。
  - [ ] 增加/更新端口一致性测试（元数据端口 vs 执行输出键）。
  - 验收标准：`MathOperation` 可直接向 `ConditionalBranch` 输出布尔结果，无额外转换。

### P1（第 2 天）

- [ ] **实现 B1 + C1 的最小可用闭环**
  - [ ] 在 `OperatorType` 增加 `Comparator`，并在 `OperatorFactory` 注册其端口/参数元数据。
  - [ ] 为 `Measurement` 增加可选 `PointA(Point)`、`PointB(Point)` 输入端口。
  - [ ] 更新 Prompt 示例，加入“两点距离测量”标准范式。
  - 验收标准：支持“如果半径 > 阈值则 NG”“测两检测点间距离”等自然语言一次生成通过。

### P2（第 3 天）

- [ ] **接入 B2/B3 胶水算子**
  - [ ] 在 `OperatorType` 与 `OperatorFactory` 注册 `Aggregator/Delay/Comment`。
  - [ ] 完成参数定义与默认值（`Mode`、`Milliseconds`、`Text` 等）。
  - [ ] 补充最小单元测试与一条端到端 AI 生成样例。
  - 验收标准：AI 可在通信流程中自动插入延时，可在多值场景生成聚合节点。

### P3（第 4 天及以后）

- [ ] **语义一致性优化（C2/C3）**
  - [ ] 设计端口命名统一方案，评估兼容策略（别名映射或版本迁移）。
  - [ ] 批量增强 `Description` 语义深度，强化场景可解释性。
  - 验收标准：在不破坏历史流程的前提下，Prompt 可读性与 LLM 选算子稳定性提升。

### 建议的执行顺序

1. 先修 Prompt（A2）再补关键词（A1），可最快提升 AI 首次生成成功率。
2. 再做端口闭环（A3 + C1），降低“能算但连不上”的结构性失败。
3. 最后扩胶水算子（B 类）并做语义优化（C 类）。
