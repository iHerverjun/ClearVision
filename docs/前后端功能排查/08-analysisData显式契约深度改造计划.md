# analysisData 显式契约深度改造计划

## 文档信息

- 回填日期：2026-03-20
- 文档状态：提案
- 触发来源：检测页右侧分析卡片把二值化算子的 `Width` / `Height` / `Text` 等通用输出误判成“距离测量”和“OCR 文本识别”
- 核查方式：静态代码排查、链路梳理、小范围脚本化分类验证；未启动完整桌面程序，未执行端到端 UI 回归
- 相关文件：
  - [`analysisCardsPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js)
  - [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js)
  - [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs)
  - [`FlowExecutionService.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Services/FlowExecutionService.cs)
  - [`WebMessages.cs`](../../Acme.Product/src/Acme.Product.Contracts/Messages/WebMessages.cs)

## 1. 背景与问题定义

当前检测页右侧分析卡片并没有消费“显式分析结果”，而是直接扫描原始 `result.outputData` 并按字段名猜测语义。

这条链路的核心现状如下：

1. [`FlowExecutionService.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Services/FlowExecutionService.cs) 在流程执行完成后，默认只把“最后一个算子输出”作为流程总输出返回。
2. [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs) 直接使用 `flowResult.OutputData` 做状态判定、结果持久化和前端返回。
3. [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js) 会把 `result.outputData` 原样交给 [`analysisCardsPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js)。
4. [`analysisCardsPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js) 仅根据 `Width`、`Height`、`Text`、`RecognizedText`、`CodeType` 等字段名推断该渲染什么卡片。

这会带来几个持续性问题：

- 图像元数据和业务分析结果混在一起，技术字段容易被误判成业务卡片。
- 前端承担了不稳定的业务语义推断，增加了 UI 复杂度和误判率。
- 后端虽已具备较多检测能力，但分析语义没有成为正式契约，结果页、检测页、导出页无法共享一套统一解释。
- 后续每新增算子，前端都可能继续堆黑名单或字段猜测，维护成本会持续升高。

## 2. 改造目标

本次深度改造的目标不是单纯修一个误判，而是建立“原始输出”和“分析语义输出”的正式边界。

### 核心目标

1. 建立显式 `analysisData` 契约，作为检测页分析卡片的唯一正式数据源。
2. 保留 `outputData` 作为原始调试/通用输出，不再让 UI 直接对其做宽泛语义猜测。
3. 把“哪些算子可以产出分析卡片”沉淀成白名单映射和规则，不再依赖裸字段名。
4. 为后续结果页、报表、导出、历史回放复用同一份分析语义结构打基础。

### 非目标

1. 本期不要求一次性重写全部算子输出格式。
2. 本期不要求立即删除 `outputData`。
3. 本期不要求立即改造所有历史数据为新格式。

## 3. 设计原则

### 3.1 分层明确

- `outputData`：保留原始算子输出，服务于调试、预览、兼容和底层能力透传。
- `analysisData`：只保留用户界面真正需要的分析语义，服务于检测页、结果页、报表页。

### 3.2 生产者单一

`analysisData` 不应由前端拼装。推荐由应用层统一构建，前端只做渲染。

推荐生产位置：

- 首选：`InspectionService` 之后、DTO 返回之前
- 原因：
  - 这里已经拿到了 `flowResult.OperatorResults`
  - 这里也拿到了当前执行的 `OperatorFlow`
  - 可以结合“算子类型 + 算子输出 + 流程上下文”统一构建分析语义

### 3.3 显式优先于猜测

- 没有出现在 `analysisData` 中的内容，默认不应渲染为分析卡片
- 技术字段不允许自动冒充分析结果
- `ResultOutput`、`TextSave` 这类输出/保存算子默认不直接生成分析卡片

### 3.4 向后兼容

在一段过渡期内：

- 后端同时返回 `outputData` 和 `analysisData`
- 前端优先消费 `analysisData`
- 仅在 `analysisData` 缺失且开启兼容开关时，才回退旧逻辑

## 4. 目标契约设计

## 4.1 推荐结构

建议采用“统一卡片模型 + 分类字段”的结构，而不是仅按 `measurements` / `ocr` / `defects` 拆分多个数组。这样更适合后续结果页、报表和排序控制。

示例：

```json
{
  "version": 1,
  "cards": [
    {
      "id": "op-width-1",
      "category": "measurement",
      "sourceOperatorId": "guid",
      "sourceOperatorType": "WidthMeasurement",
      "title": "宽度测量",
      "status": "OK",
      "priority": 100,
      "fields": [
        {
          "key": "width",
          "label": "宽度",
          "value": 12.5,
          "unit": "mm",
          "displayHint": "big-number"
        },
        {
          "key": "minWidth",
          "label": "最小宽度",
          "value": 12.1,
          "unit": "mm"
        },
        {
          "key": "maxWidth",
          "label": "最大宽度",
          "value": 12.8,
          "unit": "mm"
        }
      ],
      "meta": {
        "sampleCount": 24
      }
    },
    {
      "id": "op-ocr-1",
      "category": "recognition",
      "sourceOperatorId": "guid",
      "sourceOperatorType": "OcrRecognition",
      "title": "OCR 文本识别",
      "status": "OK",
      "priority": 90,
      "fields": [
        {
          "key": "text",
          "label": "识别文本",
          "value": "ABC123"
        }
      ],
      "meta": {
        "confidence": 98.2
      }
    }
  ],
  "summary": {
    "cardCount": 2,
    "categories": [
      "measurement",
      "recognition"
    ]
  }
}
```

## 4.2 字段说明

### 顶层

- `version`
  - 契约版本号，便于后续演进
- `cards`
  - 分析卡片数组，是主要消费对象
- `summary`
  - 可选汇总信息，便于快速判断页面是否存在某类卡片

### card 级别

- `id`
  - 卡片唯一标识，建议使用 `sourceOperatorId + category`
- `category`
  - 建议固定枚举：`measurement`、`recognition`、`defect`、`match`、`classification`、`generic`
- `sourceOperatorId`
  - 产出该卡片的算子 ID
- `sourceOperatorType`
  - 产出该卡片的算子类型
- `title`
  - UI 标题
- `status`
  - `OK` / `NG` / `Error`
- `priority`
  - 同屏排序用，数值越高越靠前
- `fields`
  - 卡片展示主体
- `meta`
  - 附加信息，例如 `confidence`、`sampleCount`、`thresholds`

### field 级别

- `key`
  - 稳定机器字段名
- `label`
  - 展示文案
- `value`
  - 原始值
- `unit`
  - 单位
- `displayHint`
  - 展示提示，如 `big-number`、`code-text`、`tag`、`json`
- `status`
  - 字段级状态，可选

## 5. 推荐生产链路

## 5.1 推荐责任分布

### FlowExecution 层

- 继续负责算子执行和原始 `OperatorResults`
- 不强行让底层执行引擎理解 UI 卡片语义
- 维持对原始输出的忠实性

### Application 层

- 新增 `AnalysisDataBuilder`
- 输入：
  - `OperatorFlow`
  - `FlowExecutionResult.OperatorResults`
  - 最终状态 `InspectionStatus`
- 输出：
  - `AnalysisData`

### Presentation / Contracts 层

- 在结果 DTO、事件消息中新增 `AnalysisData`
- 前端优先消费 `AnalysisData`

### Frontend 层

- `analysisCardsPanel` 只做渲染器
- `inspectionPanel` 只负责把 `result.analysisData` 传给面板
- 原始 `outputData` 保留给调试区、通用结果区、历史详情弹窗

## 5.2 为什么不建议继续由前端组装

如果前端继续从 `outputData` 拼 `analysisData`，会有这些问题：

- 仍然依赖字段名猜测，误判只是转移位置，不是根治
- 结果页、检测页、导出页会各自形成一套解释逻辑
- 后端和前端对同一结果的理解可能漂移
- 历史记录回放时，前端仍需要重新解释旧数据

## 6. 算子映射策略

## 6.1 白名单映射

首批只支持明确的分析型算子：

### recognition

- `OcrRecognition`
- `CodeRecognition`

### measurement

- `WidthMeasurement`
- `GapMeasurement`
- `AngleMeasurement`
- `CircleMeasurement`
- `LineMeasurement`
- `ContourMeasurement`
- `CaliperTool`
- `PointLineDistance`
- `LineLineDistance`
- `ArcCaliper`
- `GeometricTolerance`
- `ColorMeasurement`
- `PixelToWorldTransform`

### defect / detection

- `BlobDetection`
- `SurfaceDefectDetection`
- `DeepLearning`
- 其他明确输出缺陷/目标列表的算子

### match

- `TemplateMatching`
- `ShapeMatching`
- `PlanarMatching`
- `LocalDeformableMatching`

## 6.2 默认排除

以下算子默认不直接生成分析卡片：

- `Thresholding`
- `AdaptiveThreshold`
- `GaussianBlur`
- `MedianBlur`
- `Morphology`
- `ResultOutput`
- `TextSave`
- 其他预处理、转换、缓存、保存类算子

它们的输出仍保留在 `outputData`，但不进入 `analysisData`。

## 6.3 规则落点

建议新增一层显式注册表：

- `AnalysisCardRegistry`
- 每个支持的算子类型绑定一个 `Mapper`
- `Mapper` 负责把算子输出映射成 `AnalysisCard`

示意：

```csharp
public interface IAnalysisCardMapper
{
    bool CanMap(string operatorType);
    IEnumerable<AnalysisCardDto> Map(OperatorExecutionResult result, InspectionStatus status);
}
```

这样后续新增算子时，不需要去改一大段 if/else。

## 7. 改造方案分期

## Phase A - 契约设计与最小接入

### 目标

- 把 `analysisData` 定义出来并完成一次贯通

### 任务

1. 定义 `AnalysisDataDto`、`AnalysisCardDto`、`AnalysisFieldDto`
2. 在消息契约里为检测结果新增 `AnalysisData`
3. 在 `InspectionService` 增加 `AnalysisDataBuilder`
4. 首批只接入 `OcrRecognition`、`CodeRecognition`、`WidthMeasurement`
5. 前端检测页改成优先消费 `result.analysisData`

### 退出标准

- 检测页分析卡片不再直接扫描 `outputData`
- 二值化单独运行时不再出现 OCR/测量误报
- OCR 与宽度测量基础场景仍能正确展示

## Phase B - 扩展覆盖与历史兼容

### 目标

- 将更多分析型算子纳入统一契约

### 任务

1. 接入更多 measurement / defect / match 类算子
2. 设计 `analysisData.version`
3. 历史详情弹窗增加“优先展示 analysisData，缺失时回退旧逻辑”
4. 为旧数据保留只读回退兼容

### 退出标准

- 主要分析型算子均有映射器
- 新老数据都能稳定展示

## Phase C - 结果页与报表复用

### 目标

- 让 `analysisData` 不只服务检测页

### 任务

1. 结果页详情弹窗复用 `analysisData`
2. 报表导出优先消费 `analysisData`
3. 统一卡片标题、字段 label 和单位来源

### 退出标准

- 检测页、结果页、导出页对同一条结果的分析语义一致

## Phase D - 清理旧逻辑

### 目标

- 收敛历史兼容和前端猜测逻辑

### 任务

1. 删除 `analysisCardsPanel` 里对 `outputData` 的字段猜测逻辑
2. 删除临时兼容开关
3. 清理前端技术字段黑名单补丁

### 退出标准

- 分析卡片只消费显式契约
- 原始 `outputData` 不再承担分析展示职责

## 8. 文件级改造建议

## 8.1 后端

### 合同与 DTO

- [`WebMessages.cs`](../../Acme.Product/src/Acme.Product.Contracts/Messages/WebMessages.cs)
  - 为检测完成事件和相关结果消息新增 `AnalysisData`

### 应用层

- [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs)
  - 在 `ExecuteSingleAsync` 中构建 `analysisData`
  - 与 `outputData` 一起写入结果 DTO

### 新增组件

- `Acme.Product.Application/Analysis/AnalysisDataBuilder.cs`
- `Acme.Product.Application/Analysis/AnalysisCardRegistry.cs`
- `Acme.Product.Application/Analysis/Mappers/*.cs`

### 持久化

可选新增：

- `InspectionResult.AnalysisDataJson`

建议分两步：

1. 先只做传输态，不改数据库
2. 验证稳定后再落库，避免一次性改动过大

## 8.2 前端

### 检测页

- [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js)
  - 改为 `analysisCardsPanel.updateCards(result.analysisData, ...)`

- [`analysisCardsPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/analysisCardsPanel.js)
  - 从“语义猜测器”收敛为“卡片渲染器”

### 结果页

- [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js)
  - 为历史详情和导出接入 `analysisData`

## 9. 验收清单

### 功能验收

1. 仅有二值化、滤波、保存类算子时，不出现 OCR/测量卡片
2. OCR 流程只在存在识别算子且有显式识别卡片时出现 OCR 卡片
3. 宽度测量流程只在存在测量算子且映射器产出卡片时出现测量卡片
4. 结果页与检测页对同一条数据展示一致

### 契约验收

1. `analysisData` 版本号存在
2. `analysisData.cards` 中每张卡片都可追溯 `sourceOperatorId` / `sourceOperatorType`
3. 前端不再通过 `Width` / `Height` / `Text` 裸字段直接判断卡片类型

### 测试验收

建议至少补齐以下测试：

- 单元测试
  - `AnalysisDataBuilder` 映射测试
  - `ResultOutput` / `Thresholding` 等算子不产出分析卡片测试

- 集成测试
  - 单次检测 API 返回 `analysisData`
  - 实时检测事件返回 `analysisData`

- 前端测试
  - `analysisCardsPanel` 纯渲染测试
  - 检测页卡片显隐测试

## 10. 风险与缓释

### 风险 1：改造范围过大，影响现有结果链路

- 缓释：
  - 先双写 `outputData + analysisData`
  - 前端优先新契约，保留短期兼容

### 风险 2：算子覆盖不全，导致部分卡片消失

- 缓释：
  - 先接入核心白名单算子
  - 保留结果详情里的原始 `outputData` 观察区

### 风险 3：历史数据没有 `analysisData`

- 缓释：
  - 检测页只消费实时新数据
  - 结果页对历史数据保留只读兼容分支

### 风险 4：不同页面各自定义 card 结构，导致新一轮漂移

- 缓释：
  - 强制所有 UI 页面复用同一份 DTO
  - 前端不允许自行发明新的分析语义字段

## 11. 推荐决策

建议采用以下策略：

1. 短期继续保留当前已做的“流程语义白名单 + 技术字段过滤”止血补丁
2. 立即启动 `analysisData` 契约设计与应用层构建器
3. 第一阶段只接 OCR、条码、宽度测量三类核心场景
4. 等检测页稳定后，再把结果页和报表迁移到显式契约

## 12. 最小落地版本定义

如果只做一个足够小、但能真正改变架构方向的版本，建议是：

1. 后端返回 `analysisData.version + analysisData.cards`
2. 只支持 `OcrRecognition`、`CodeRecognition`、`WidthMeasurement`
3. 检测页分析卡片只渲染 `analysisData`
4. 原始 `outputData` 仍保留在结果详情或调试面板

做到这一步，就已经完成了从“猜测式卡片系统”到“显式契约卡片系统”的核心转向。
