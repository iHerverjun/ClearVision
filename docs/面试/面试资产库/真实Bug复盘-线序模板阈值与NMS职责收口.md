# 真实 Bug 复盘：线序模板阈值与 NMS 职责收口

> 用途：回答“说一个你真实踩过的坑，以及怎么爬出来的”。
> 最佳来源：`线序检测/线序检测结果可观测性与NMS收口执行记录-2026-03-23.md`

---

## 1. 现象

在线序检测模板已经有固定骨架后，现场调试仍出现两个明显问题：

1. 结果侧只有一句 `Message`，很难判断 NG 到底是没检出、重复框、错序还是阈值过严。
2. 阈值职责分散在三层：
   - `DeepLearning` 内部 NMS
   - 下游 `BoxNms`
   - `DetectionSequenceJudge.MinConfidence`

最终表现就是：

- 现场能看到 NG，但很难第一时间知道该调哪里。
- 不同人会去改不同层参数，结果越来越不可控。

---

## 2. 复现条件

复现并不依赖一个特殊 Bug 输入，而是在线序模板进入真实调参与解释阶段时稳定暴露：

- 流程里同时存在 `DeepLearning -> BoxNms -> DetectionSequenceJudge`
- 线序判断依赖候选框排序与筛选
- `ResultOutput` 没有接入足够的结构化诊断

只要出现这些条件，问题就会反复出现。

---

## 3. 定位工具与观察材料

这次定位不是“肉眼看一眼代码就知道”，而是用了三类材料：

### 3.1 场景包与模板文件

- `线序检测/scenario-package-wire-sequence/template/terminal-wire-sequence.flow.template.json`
- `线序检测/scenario-package-wire-sequence/rules/sequence-rule.v1.json`
- `线序检测/scenario-package-wire-sequence/manifest.json`

它们帮助确认业务契约和当前骨架到底是什么。

### 3.2 运行时算子职责检查

- `DeepLearningOperator`
- `BoxNmsOperator`
- `DetectionSequenceJudgeOperator`
- `ResultOutputOperator`

它们帮助确认谁在做筛选、谁在做去重、谁在做解释。

### 3.3 结构化诊断与定向测试

- 为 `BoxNms` 和 `DetectionSequenceJudge` 增加结构化诊断输出
- 使用定向测试覆盖：
  - `BoxNmsOperatorTests`
  - `DetectionSequenceJudgeOperatorTests`
  - `ResultOutputOperatorTests`
  - `WireSequenceScenarioPackageTests`

---

## 4. 根因

根因不是某一行代码写错，而是职责划分不清：

- `DeepLearning` 已经做一轮内部 NMS
- `BoxNms` 又做一轮业务层 NMS
- `DetectionSequenceJudge.MinConfidence` 还在继续过滤

这意味着：

- 阈值调整没有唯一 owner
- 排查时很难知道某个框到底死在哪一层
- 业务层和模型层职责混在一起

本质上，这是一个“系统边界设计问题”，不是单纯的实现细节 Bug。

---

## 5. 修复思路

核心不是再加更多参数，而是把职责收口：

1. 在线序模板中关闭 `DeepLearning` 内部 NMS
2. 把 `DeepLearning.Confidence` 降为低置信度地板
3. 把 `DetectionSequenceJudge.MinConfidence` 固定为 `0.0`
4. 把现场优先调参收口到：
   - `BoxNms.ScoreThreshold`
   - `BoxNms.IouThreshold`
5. 给 `BoxNms` 和 `DetectionSequenceJudge` 增加结构化诊断输出
6. 让 `ResultOutput` 同时携带图像、NMS 诊断、线序诊断和文本消息

---

## 6. 回归验证

公开记录里已经明确写到：

- 定向测试：
  - `BoxNmsOperatorTests`
  - `DetectionSequenceJudgeOperatorTests`
  - `ResultOutputOperatorTests`
  - `Sprint7_AiEvolutionTests`
  - `WireSequenceScenarioPackageTests`
- 结果：`33 passed, 0 failed`

这比“我觉得改对了”更有说服力，因为它至少证明：

- 模板骨架升级没有把已有契约打破
- 诊断输出能被结果侧消费
- 参数边界和职责口径被统一下来

---

## 7. 这次 Bug 复盘最能体现什么

- 我不是只会说“先复现再看代码”，而是知道要把问题拆成“业务语义、模板骨架、算子职责、诊断输出”几层。
- 我知道很多所谓 Bug，本质上是职责边界没定义清。
- 我能把“多层同时起作用”的系统，收敛成“有唯一 owner 的系统”。

---

## 8. 面试里 1 分钟讲法

> 我现在最愿意讲的一个真实坑，是线序模板里阈值和 NMS 职责分散的问题。最开始 `DeepLearning`、`BoxNms` 和 `DetectionSequenceJudge` 三层都在影响结果，导致现场看到 NG 以后根本不知道该调哪一层。  
>  
> 后来我没有继续加参数，而是先补结构化诊断，再把职责收口：关闭 `DeepLearning` 内部 NMS，把 `DetectionSequenceJudge.MinConfidence` 固定，把现场优先调参收口到 `BoxNms.ScoreThreshold` 和 `BoxNms.IouThreshold`。这样一来，不仅结果更可解释，排查路径也清楚了。  
>  
> 这个例子对我来说很重要，因为它说明我不是只会把功能做出来，而是开始把业务语义、算子职责和调参边界真正理顺。

