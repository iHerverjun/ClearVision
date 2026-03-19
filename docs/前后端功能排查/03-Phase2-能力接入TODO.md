# Phase 2 - 能力接入 TODO

## 阶段目标

- 把已经展示在页面上的能力补成“真实可执行”。
- 把后端已实现但前端不可达的高价值能力逐步开放。
- 对暂不开放的能力明确做减法，避免继续保留误导性入口。

## 当前状态总览

- 回填日期：2026-03-20
- 核查方式：静态代码与现有测试文件核查，未启动程序、未执行接口请求。
- 阶段判断：未完成
- 统计：4 项已完成，5 项部分完成，1 项未完成
- 主要阻塞：
  - 运行保护与安全策略大多已做到“能保存”，但尚未全部进入运行时/认证链路。
  - `autotune` 后端能力完整存在，但前端仍没有正式产品化入口。
  - 图片缓存和设置重置都已具备基础设施，但和结果页/设置页的最终用户语义仍未完全打通。
  - “开放 / 内部 / 下线”尚未形成统一清单。

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

- 状态：部分完成
- 来源：报告 B.7
- 判断：
  - 页面已经把 `stopOnConsecutiveNg`、`missingMaterialTimeoutSeconds`、`applyProtectionRules` 纳入表单和保存 payload。
  - 后端配置模型已扩展出对应字段。
  - 但运行时目前仅实际消费了 `autoRun` 与 `stopOnConsecutiveNg`，其他字段未形成真正闭环。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1613-L1644)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2342-L2347)
  - [`AppConfig.cs`](../../Acme.Product/src/Acme.Product.Core/Entities/AppConfig.cs#L123-L143)
  - [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L340-L348)
  - [`inspectionPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionPanel.js#L539-L544)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1598-L1600)
- 主要缺口：
  - `missingMaterialTimeoutSeconds` 与 `applyProtectionRules` 仍未进入真实运行链路。

### 4. `[前后端]` 让安全策略页具备真实配置能力

- 状态：部分完成
- 来源：报告 B.8
- 判断：
  - 页面已有真实字段和保存入口，`security` 模型也已落地。
  - `PasswordMinLength` 已被修改密码、创建用户、重置密码链路真实读取。
  - 但 `SessionTimeoutMinutes` 与 `LoginFailureLockoutCount` 仍停留在模型层，认证服务仍硬编码 8 小时过期且未维护失败锁定状态。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1911-L1928)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2348-L2351)
  - [`AppConfig.cs`](../../Acme.Product/src/Acme.Product.Core/Entities/AppConfig.cs#L146-L161)
  - [`AuthEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AuthEndpoints.cs#L102-L105)
  - [`UserEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/UserEndpoints.cs#L59-L59)
  - [`UserEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/UserEndpoints.cs#L136-L136)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L23-L25)
  - [`AuthService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L54-L60)
- 主要缺口：
  - 需要把会话超时与失败锁定真正接入认证行为。

### 5. `[前后端]` 决定并处理 `autotune` 能力的命运

- 状态：部分完成
- 来源：报告 C.2
- 判断：
  - 后端 `/api/autotune/*` 端点和服务已经完整存在，并有测试覆盖。
  - 前端当前只暴露了“查看自动调参策略”的只读能力，没有任务入口、状态展示和结果应用流程。
  - 这意味着它既没有真正产品化，也没有被明确降级为内部能力。
- 证据：
  - [`Program.cs`](../../Acme.Product/src/Acme.Product.Desktop/Program.cs#L205-L205)
  - [`AutoTuneEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AutoTuneEndpoints.cs#L28-L30)
  - [`AutoTuneEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/AutoTuneEndpoints.cs#L33-L213)
  - [`operatorLibrary.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js#L1004-L1019)
  - [`AutoTuneServiceTests.cs`](../../Acme.Product/tests/Acme.Product.Tests/Services/AutoTuneServiceTests.cs#L15-L15)
- 主要缺口：
  - 需要明确它是“正式开放”还是“内部保留”，并相应补 UI 或撤预期。

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

- 状态：部分完成
- 来源：报告 C.5
- 判断：
  - 结果页前端已经支持 `imageUrl`、`imageId`、`imageData(base64)` 三种来源。
  - 后端已有上传和回取图片缓存的接口，缓存仓储也具备过期清理能力。
  - 但真实检测结果生产链路仍主要输出 base64，历史结果加载字段和结果页读取字段也未完全对齐。
- 证据：
  - [`resultPanel.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/results/resultPanel.js#L441-L455)
  - [`ApiEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/ApiEndpoints.cs#L546-L561)
  - [`ImageCacheRepository.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Repositories/ImageCacheRepository.cs#L24-L39)
  - [`ImageCacheRepository.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Repositories/ImageCacheRepository.cs#L62-L79)
  - [`InspectionService.cs`](../../Acme.Product/src/Acme.Product.Application/Services/InspectionService.cs#L80-L105)
  - [`InspectionMappingProfile.cs`](../../Acme.Product/src/Acme.Product.Application/Profiles/InspectionMappingProfile.cs#L19-L25)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L703-L711)
- 主要缺口：
  - 需要把检测结果主链路从长期内联 `base64` 切到稳定的缓存引用语义。

### 8. `[前后端]` 暴露设置重置能力

- 状态：部分完成
- 来源：报告 C.6
- 判断：
  - 设置页已有二次确认和真实 `/settings/reset` 调用。
  - 后端会生成新的 `AppConfig` 并写回配置文件。
  - 但它只重置 `AppConfig`，并不会覆盖设置页中独立存储的 AI 模型配置。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L2241-L2249)
  - [`SettingsEndpoints.cs`](../../Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs#L49-L53)
  - [`JsonConfigurationService.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/Services/JsonConfigurationService.cs#L62-L66)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L68-L68)
  - [`AiConfigStore.cs`](../../Acme.Product/src/Acme.Product.Infrastructure/AI/AiConfigStore.cs#L43-L47)
- 主要缺口：
  - 需要明确“恢复默认设置”的重置范围是否包含 AI 模型等独立配置域。

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

- 状态：未完成
- 来源：报告 C 类问题整体
- 判断：
  - 当前仓库里没有集中式 capability manifest、feature registry 或统一状态表。
  - 能力状态仍散落在多个页面的硬编码文案里，例如“暂未接入”“暂未开放”。
- 证据：
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1520-L1520)
  - [`settingsView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js#L1582-L1582)
  - [`projectView.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/project/projectView.js#L404-L408)
  - [`operatorLibrary.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js#L1004-L1005)
  - [`app.js`](../../Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L839-L841)
- 主要缺口：
  - 需要形成单一来源的“正式开放 / 内部保留 / 计划下线”清单，作为 Phase 2 和 Phase 3 的衔接资产。

## 阶段完成标准

- 所有保留在 UI 中的按钮、配置项和入口都能真实执行或明确禁用。
- 已决定开放的后端孤儿能力，至少完成一轮入口接入与联调验收。
- 不开放的能力已被文档化并从用户心智中移除。

## 当前对照

- 所有保留在 UI 中的按钮、配置项和入口都能真实执行或明确禁用：部分达成
- 已决定开放的后端孤儿能力，至少完成一轮入口接入与联调验收：部分达成
- 不开放的能力已被文档化并从用户心智中移除：未达成
