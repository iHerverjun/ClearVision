# Phase 2 - 能力接入 TODO

## 阶段目标

- 把已经展示在页面上的能力补成“真实可执行”。
- 把后端已实现但前端不可达的高价值能力逐步开放。
- 对暂不开放的能力明确做减法，避免继续保留误导性入口。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有测试文件核查，未启动程序、未执行接口请求。
- 阶段判断：已完成
- 统计：10 项已完成
- 主要说明：
  - 本阶段范围内的能力接入与入口去伪已经完成；后续现场联调与长期维护转入联调基线和功能矩阵持续跟踪。

## 状态清单

### 1. `[前后端]` 处理“更改目录”按钮

- 状态：已完成
- 来源：报告 B.5
- 判断：
  - 当前完成方式是“明确不可用”，不是接入真实目录选择。
  - 按钮已禁用并标注“暂未接入”，同时允许用户直接手填路径并持久化到设置。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1514-L1520)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2336-L2340)
  - [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L379-L379)
  - [`WebMessageHandler.cs`](../../Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L651-L651)
- 备注：
  - 该项按“按钮要么真实可用、要么明确不可用”判断为完成。

### 2. `[前后端]` 处理“立即清理过期文件”按钮

- 状态：已完成
- 来源：报告 B.6
- 判断：
  - 当前也是按“明确未开放”处理。
  - 按钮已禁用并明确说明“即时清理动作暂未接入执行链路”。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1582-L1583)
- 备注：
  - 该项按“避免继续伪装成功”判断为完成。

### 3. `[前后端]` 让运行保护配置进入正式保存模型

- 状态：已完成
- 来源：报告 B.7
- 判断：
  - 页面已经把 `stopOnConsecutiveNg`、`missingMaterialTimeoutSeconds`、`applyProtectionRules` 纳入表单和保存 payload。
  - 后端配置模型已扩展出对应字段。
  - `inspectionPanel` 在加载设置后会真实消费这三个字段：`applyProtectionRules` 决定是否启用 watchdog，`missingMaterialTimeoutSeconds` 决定超时阈值，`stopOnConsecutiveNg` 决定连续 NG 自动停机阈值。
  - 连续运行保护触发后会实际调用停止链路，不再只是停留在文案或提示层。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2420-L2486)
  - [`AppConfig.cs`](../../Acme.Product/src/Acme.Product.Core/Entities/AppConfig.cs#L123-L143)
  - [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L72-L83)
  - [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L162-L224)
  - [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L345-L352)
  - [`inspectionController.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionController.js#L378-L392)
- 备注：
  - 当前已完成“保存 -> 加载 -> 运行时消费 -> 停止动作”闭环；后续仍建议补一条现场联调记录，确认真实设备/触发节奏下的超时停机体验。

### 4. `[前后端]` 让安全策略页具备真实配置能力

- 状态：已完成
- 来源：报告 B.8
- 判断：
  - 页面已有真实字段和保存入口，`security` 模型也已落地。
  - `PasswordMinLength` 已被修改密码、创建用户、重置密码链路真实读取。
  - `SessionTimeoutMinutes` 已被 `AuthService` 用于生成会话过期时间，并在 `ValidateTokenAsync` / `GetSessionAsync` 中真实执行过期判断。
  - `LoginFailureLockoutCount` 已被 `AuthService` 用于失败计数和临时锁定判定，鉴权链路通过 `GetSessionAsync` / `AuthMiddleware` 真实消费。
  - 已存在单测覆盖会话超时、失败锁定与成功登录后清空失败计数。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2071-L2075)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2428-L2489)
  - [`AppConfig.cs`](../../Acme.Product/src/Acme.Product.Core/Entities/AppConfig.cs#L146-L161)
  - [`AuthEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AuthEndpoints.cs#L102-L105)
  - [`UserEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/UserEndpoints.cs#L59-L59)
  - [`UserEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/UserEndpoints.cs#L136-L136)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L66-L98)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L129-L177)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L237-L245)
  - [`AuthMiddleware.cs`](../../Acme.Product/src/Acme.Product.Desktop/Middleware/AuthMiddleware.cs#L47-L83)
  - [`AuthServiceTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/Services/AuthServiceTests.cs#L17-L105)
- 备注：
  - 当前页面展示的三项安全字段已具备“保存 / 回显 / 生效”闭环；现阶段剩余边界主要是锁定时长仍采用服务端固定 15 分钟窗口，而不是单独暴露为配置项。

### 5. `[前后端]` 决定并处理 `autotune` 能力的命运

- 状态：已完成
- 来源：报告 C.2
- 判断：
  - 后端 `/api/autotune/*` 端点和服务已经完整存在，并有测试覆盖。
  - 当前已经明确把该能力定义为“内部能力”：前端保留只读策略查看入口，但不再伪装成正式产品化任务流。
  - 相关入口的状态、提示文案和按钮启用态已收敛到统一 `featureRegistry`，不再散落硬编码。
- 证据：
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L205-L205)
  - [`AutoTuneEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AutoTuneEndpoints.cs#L28-L30)
  - [`AutoTuneEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AutoTuneEndpoints.cs#L33-L213)
  - [`featureRegistry.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/shared/featureRegistry.js#L26-L33)
  - [`operatorLibrary.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js#L1011-L1027)
  - [`AutoTuneServiceTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/Services/AutoTuneServiceTests.cs#L15-L15)

### 6. `[前后端]` 增加 Demo 工程创建 / 引导入口

- 状态：已完成
- 来源：报告 C.3
- 判断：
  - 新建工程对话框已提供 `demo` 和 `simple-demo` 选项。
  - 前端管理器已对接 `/demo/create`、`/demo/create-simple`、`/demo/guide`。
  - 后端端点、服务和测试均已存在。
- 证据：
  - [`projectView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/project/projectView.js#L397-L412)
  - [`projectManager.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/project/projectManager.js#L83-L104)
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L233-L249)
  - [`DemoProjectService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/DemoProjectService.cs#L28-L28)
  - [`DemoProjectServiceTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/Integration/DemoProjectServiceTests.cs#L28-L34)

### 7. `[前后端]` 把图片缓存链路接入结果页

- 状态：已完成
- 来源：报告 C.5
- 判断：
  - 结果页前端已经支持 `imageUrl`、`imageId`、`imageData(base64)` 三种来源，并会优先使用 `imageId`。
  - 检测结果主链路现在会为单次检测和实时检测结果写入 `ImageId`，实时事件和历史结果都会透传该字段。
  - `base64` 图像仍保留为兼容 fallback，用于缓存不可用或历史旧数据场景。
- 证据：
  - [`InspectionResult.cs`](../../Acme.Product/src/Acme.Product.Core/Entities/InspectionResult.cs#L142-L147)
  - [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L490-L490)
  - [`InspectionWorker.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Services/InspectionWorker.cs#L438-L438)
  - [`InspectionWorker.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Services/InspectionWorker.cs#L641-L641)
  - [`InspectionRealtimeEventMapper.cs`](../../Acme.Product/src/Acme.Product.Desktop/Inspection/InspectionRealtimeEventMapper.cs#L44-L56)
  - [`inspectionController.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionController.js#L220-L232)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L647-L655)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L796-L802)
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L686-L695)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L546-L561)
  - [`ImageCacheRepository.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Repositories/ImageCacheRepository.cs#L24-L60)
  - [`InspectionRealtimeEventMapperTests.cs`](../../Acme.Product/tests/Acme.Product.Desktop.Tests/InspectionRealtimeEventMapperTests.cs#L13-L30)

### 8. `[前后端]` 暴露设置重置能力

- 状态：已完成
- 来源：报告 C.6
- 判断：
  - 设置页已有二次确认和真实 `/settings/reset` 调用。
  - 后端现在会同时重置 `AppConfig` 和 AI 模型配置，并把重置范围随响应一起返回前端。
  - 前端确认文案、按钮状态和成功提示都已同步到新的重置语义。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1420-L1487)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2399-L2409)
  - [`SettingsEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L49-L59)
  - [`AiConfigStore.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/AI/AiConfigStore.cs#L192-L233)
  - [`AiConfigStoreTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/AI/AiConfigStoreTests.cs)
  - [`SettingsResetEndpointTests.cs`](../../Acme.Product/tests/Acme.Product.Desktop.Tests/SettingsResetEndpointTests.cs)

### 9. `[前后端]` 接入算子类型与元数据细粒度接口

- 状态：已完成
- 来源：报告 C.7
- 判断：
  - 前端算子库已优先走 `/operators/types`，再逐个拉 `/operators/{type}/metadata`。
  - 后端已正式提供这两个公共接口。
  - 已有针对 metadata 覆盖和 catalog 清理的测试。
- 证据：
  - [`operatorLibrary.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js#L292-L299)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L352-L362)
  - [`OperatorMetadataMigrationTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/Operators/OperatorMetadataMigrationTests.cs#L7-L15)
  - [`OperatorMetadataMigrationTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/Operators/OperatorMetadataMigrationTests.cs#L51-L56)

### 10. `[联调]` 为 Phase 2 能力建立“开放 / 内部 / 下线”清单

- 状态：已完成
- 来源：报告 C 类问题整体
- 判断：
  - 当前前端已经新增集中式 `featureRegistry`，作为“开放 / 内部 / 下线 / 未开放”状态的单一来源。
  - `更改目录`、`立即清理过期文件`、`autotune`、`demo 工程入口`、`恢复默认设置` 等关键入口已接入这份统一状态表。
- 证据：
  - [`featureRegistry.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/shared/featureRegistry.js)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L259-L261)
  - [`projectView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/project/projectView.js#L388-L418)
  - [`operatorLibrary.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js#L1011-L1027)

## 阶段完成标准

- 所有保留在 UI 中的按钮、配置项和入口都能真实执行或明确禁用。
- 已决定开放的后端孤儿能力，至少完成一轮入口接入与联调验收。
- 不开放的能力已被文档化并从用户心智中移除。

## 当前对照

- 所有保留在 UI 中的按钮、配置项和入口都能真实执行或明确禁用：已达成
- 已决定开放的后端孤儿能力，至少完成一轮入口接入与联调验收：已达成
- 不开放的能力已被文档化并从用户心智中移除：已达成
