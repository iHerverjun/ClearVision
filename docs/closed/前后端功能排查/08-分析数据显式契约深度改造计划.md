---
title: "analysisData 显式契约深度改造计划"
doc_type: "note"
status: "closed"
topic: "前后端功能排查"
created: "2026-03-20"
updated: "2026-03-20"
---
# analysisData 显式契约深度改造计划

## 文档信息

- 回填日期：2026-03-20
- 文档状态：已完成（核心转向已落地，后续扩展转入常规增强项）
- 触发来源：检测页右侧分析卡片曾把二值化算子的 `Width` / `Height` / `Text` 等通用输出误判成“距离测量”和“OCR 文本识别”
- 核查方式：静态代码排查与现有测试文件核查，未启动完整桌面程序，未执行端到端 UI 回归
- 相关文件：
  - [`AnalysisDataBuilder.cs`](../../../Acme.Product/src/Acme.Product.Application/Analysis/AnalysisDataBuilder.cs)
  - [`AnalysisCardRegistry.cs`](../../../Acme.Product/src/Acme.Product.Application/Analysis/AnalysisCardRegistry.cs)
  - [`InspectionService.cs`](../../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs)
  - [`InspectionWorker.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/Services/InspectionWorker.cs)
  - [`WebMessages.cs`](../../../Acme.Product/src/Acme.Product.Contracts/Messages/WebMessages.cs)
  - [`analysisCardsPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js)
  - [`inspectionPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js)
  - [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js)
  - [`AnalysisDataBuilderTests.cs`](../../../Acme.Product/tests/Acme.Product.Tests/Services/AnalysisDataBuilderTests.cs)
  - [`InspectionRealtimeEventMapperTests.cs`](../../../Acme.Product/tests/Acme.Product.Desktop.Tests/InspectionRealtimeEventMapperTests.cs)

## 1. 当前结论

这份文档最初是为“把分析卡片从 `outputData` 字段猜测，收敛到显式 `analysisData` 契约”而写的方案稿。结合当前仓库代码，这一轮架构转向已经完成；剩余工作已从“是否落地”转为“覆盖范围扩展与体验增强”。

当前状态可以概括为：

1. 后端已经具备显式 `analysisData` 生产能力，不再只是停留在设计层。
2. 检测页主分析卡片链路已经改为优先消费 `result.analysisData`。
3. 结果页预览与详情也已经开始复用 `analysisData`。
4. 实时消息与历史结果都已经具备携带 / 透传 `analysisData` 的基础设施。
5. 剩余问题主要集中在“覆盖范围继续扩展”“结果页/导出继续向同一语义收敛”“端到端联调验证补齐”三件事上，但这些已不再阻塞本次显式契约改造计划闭环。

## 2. 已落地事实

### 2.1 后端契约与生产链路已接通

- `InspectionService` 在单次检测完成后会调用 `IAnalysisDataBuilder` 构建 `analysisData`，并和 `outputData` 一起序列化持久化。
- `InspectionWorker` 在实时检测链路里也执行同样的 `analysisData` 构建与持久化。
- `WebMessages.InspectionCompletedEvent` 已新增 `AnalysisData` 字段，桌面实时消息具备正式契约承载位。

证据：

- [`InspectionService.cs`](../../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L35-L35)
- [`InspectionService.cs`](../../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L156-L160)
- [`InspectionWorker.cs`](../../../Acme.Product/src/Acme.Product.Infrastructure/Services/InspectionWorker.cs#L564-L566)
- [`WebMessages.cs`](../../../Acme.Product/src/Acme.Product.Contracts/Messages/WebMessages.cs#L178-L230)

### 2.2 检测页主卡片链路已切到显式契约

- `inspectionPanel` 会优先读取 `result.analysisData` 并传给 `analysisCardsPanel`。
- `analysisCardsPanel` 已被收敛成显式 `analysisData` 渲染器，旧的 `outputData` 猜测分类和历史卡片分支已删除。

证据：

- [`inspectionPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L301-L308)
- [`inspectionPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L934-L941)
- [`analysisCardsPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js#L162-L173)
- [`analysisCardsPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js#L189-L225)

### 2.3 结果页已经开始复用 `analysisData`

- 结果页列表预览现在直接展示 `analysisData` 摘要，不再把 `outputData` 当成主预览来源。
- 结果详情弹窗也已经具备单独渲染 `analysisData` 的区块。
- `outputData` 仍然保留在详情页中，承担调试 / 兼容观察用途，而不是再次充当主分析语义来源。
- `serverPaged` 模式下的实时结果已改为回源刷新服务端历史，不再直接手工写入本地历史数组。

证据：

- [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L909-L909)
- [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L1172-L1172)
- [`resultPanel.js`](../../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L1233-L1319)

### 2.4 自动化测试已覆盖第一轮改造

- `AnalysisDataBuilderTests` 已验证白名单算子能产出卡片，非白名单算子即使输出了 `Width` / `Text` 这类字段也不会进入 `analysisData`。
- `InspectionRealtimeEventMapperTests` 已验证实时结果消息会透传 `analysisData`。

证据：

- [`AnalysisDataBuilderTests.cs`](../../../Acme.Product/tests/Acme.Product.Tests/Services/AnalysisDataBuilderTests.cs#L13-L187)
- [`InspectionRealtimeEventMapperTests.cs`](../../../Acme.Product/tests/Acme.Product.Desktop.Tests/InspectionRealtimeEventMapperTests.cs#L12-L30)

## 3. 原始问题与当前剩余问题

### 原始问题

最初的问题是：前端直接扫描原始 `outputData`，按字段名猜测语义，导致技术字段容易被误判成业务卡片。

### 当前已经解决的部分

- 显式 `analysisData` 契约已经存在，不再只是设计概念。
- 业务分析卡片的主生产者已经转到后端应用层。
- 检测页主卡片渲染器已经改为优先消费显式契约。
- 结果页已经开始复用同一份显式分析语义。

### 当前后续增强项

- 结果详情中原始 `outputData` 仍保留为调试区，这部分边界后续可继续约束。
- `analysisData` 的 mapper 覆盖范围仍是“首批核心场景优先”，后续可继续扩展更多 measurement / defect / match 类算子。
- 报表 / 导出链路仍可继续向 `analysisData` 收敛。
- 若要进一步提高置信度，仍建议补一轮完整桌面 UI 联调。

## 4. 分期状态更新

## Phase A - 契约设计与最小接入

### 当前状态：已完成

- `analysisData` DTO / 消息契约已存在。
- `InspectionService` / `InspectionWorker` 已接入 `AnalysisDataBuilder`。
- 检测页主分析卡片链路已改为优先消费 `result.analysisData`。
- 已有白名单与回归测试覆盖 OCR、条码、宽度测量及“非白名单不产卡”场景。

退出标准对照：

- 检测页分析卡片不再直接扫描 `outputData`：已达成（主链路）
- 二值化单独运行时不再出现 OCR/测量误报：已有 builder 白名单测试兜底，待完整联调
- OCR 与宽度测量基础场景仍能正确展示：已通过静态代码和单测核查

## Phase B - 扩展覆盖与历史兼容

### 当前状态：已完成（首轮落地）

- `analysisData.version` 已存在。
- 历史 / 实时结果都已经具备承载 `analysisData` 的字段基础。
- 但 mapper 覆盖仍需继续扩展，历史兼容策略也还没有收缩到最终形态。

当前主要缺口：

1. 扩大白名单映射范围，覆盖更多 measurement / defect / match 类算子。
2. 明确哪些页面 / 弹窗仍允许兼容 `outputData`，哪些地方必须只认 `analysisData`。

## Phase C - 结果页与报表复用

### 当前状态：已完成（主链路落地）

- 结果页预览与详情已开始复用 `analysisData`，最近一轮又继续收紧了主展示对 `outputData` 的依赖。
- 但“报表导出是否完全以 `analysisData` 为正式语义源”在本轮核查里还没有看到完整闭环证据。

当前主要缺口：

1. 报表 / 导出链路继续向 `analysisData` 收敛。
2. 检测页、结果页、导出页的标题、字段 label、单位来源继续统一。

## Phase D - 清理旧逻辑

### 当前状态：已完成（旧主逻辑已退场）

- `analysisCardsPanel.js` 中旧的 `outputData` 分类方法、技术字段过滤和历史卡片渲染函数已经删除。
- 检测页最近结果摘要和结果页列表预览也已继续向显式 `analysisData` 收敛。
- 当前剩余的 `outputData` 主要保留在结果详情调试区，不再承担主分析展示职责。

当前主要缺口：

1. 继续把“显式契约渲染器”和“原始输出调试展示”拆清边界。
2. 确认报表 / 导出链路不再绕回旧语义。
3. 补一轮端到端联调，验证实时结果与历史详情展示一致性。

## 5. 当前文件级落点

### 后端

- `AnalysisDataBuilder`、`AnalysisCardRegistry` 和内置 mapper 已经存在，不再属于“建议新增”。
- `InspectionService` 与 `InspectionWorker` 已经把 `analysisData` 作为正式输出的一部分写入结果。
- `WebMessages` 已有 `AnalysisData` 契约。

### 前端

- `inspectionPanel` 已把 `result.analysisData` 传给 `analysisCardsPanel`。
- `analysisCardsPanel` 已完成从“猜测器”到“显式契约渲染器”的收敛。
- `resultPanel` 已具备 `analysisData` 预览与详情展示。

## 6. 结论与后续建议

当前最准确的判断不是“analysisData 仍是提案”，而是：

- 架构方向已经切换成功。
- 第一轮最小落地版本已经进主链路。
- 本次显式契约改造计划已经完成闭环。

后续增强建议：

1. 先做 Phase D 的旧逻辑删除，避免旧猜测逻辑继续成为隐性维护面。
2. 再扩展 mapper 覆盖范围，逐步把更多算子纳入显式契约。
3. 最后补一轮端到端联调与结果页 / 导出复核，把“代码已接通”补成“回归已落账”。


