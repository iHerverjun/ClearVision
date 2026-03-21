# 系统配置功能审计报告

- 审计日期: 2026-02-27
- 审计范围: `settingsView.js` 对应的"系统配置"页面及其后端调用链
- 审计方式: 静态代码深度追踪（前端控件 -> 事件绑定 -> API -> 持久化 -> 运行时消费）
- 审计目标: 区分"真实有效功能"与"占位伪代码/后续待开发功能"，发掘隐患并提供优化建议
- 最后更新: 2026-03-06 — 状态回填、运行时消费复核、剩余优先级重排

---
## 0. 2026-03-06 回填摘要

1. `SavePolicy / ImageSavePath` 已被 `InspectionService` 真实消费，文件存储不再属于“只保存不生效”。
2. `AutoRun / StopOnConsecutiveNg` 已分别在前端运行入口和后端检测流程中接线。
3. PLC 页面已具备连接测试链路，其余未完成按钮改为显式 disabled + tooltip，避免误导。
4. 设置页全局保存已完成 AI 隐式保存隔离；当前最高优先级剩余项是保存流程事务化。

---

## 1. 判定标准

### 1.1 真实有效

1. 前端存在可触发交互（按钮/输入事件）。
2. 事件绑定存在，且能调用后端 API。
3. 后端存在对应接口与逻辑。
4. 数据被持久化，且在运行时被实际消费（不仅仅是保存）。

### 1.2 半有效

1. 交互和保存链路存在，但运行时未消费。
2. 前端局部生效（例如只写 localStorage），未纳入统一配置闭环。
3. 部分字段可保存，其他字段仍是硬编码。

### 1.3 占位伪代码

1. 仅有 UI 元素，无事件绑定。
2. 仅静态示例数据，无真实数据源/硬编码。
3. 无后端 API 或无运行时落地逻辑。

---

## 2. 执行摘要

1. "PLC 通讯配置"已从纯占位升级为“半有效/占位混合”：连接测试链路已接，地址映射/同步配置仍是显式占位。
2. 真实有效程度最高的模块: `AI 大模型管理`、`相机管理`、`用户管理（管理员）`。
3. 原“可保存但不生效”问题已显著收敛，当前主要剩余为常规设置运行时消费者、保存事务化与安全策略区接线。
4. 第一梯队修复外，文件存储与运行策略本轮已完成运行时消费回填。

---

## 3. 总览矩阵

| 模块 | 判定 | 结论 |
|---|---|---|
| 全局保存（保存所有更改） | 部分有效 | 保存链路稳定；`activeCameraId` 覆写与 AI 隐式保存问题 ✅ 已修复；仍缺事务化批量提交 |
| 常规设置 | 半有效 | 统一保存入口已收敛；`softwareTitle` / `autoStart` 仍缺运行时消费者 |
| 通讯连接（PLC） | 半有效/占位混合 | 基础配置可保存，连接测试链路已接；地址映射/同步按钮改为显式 disabled 占位 |
| 文件存储 | 真实有效（部分运维占位） | `SavePolicy` / `ImageSavePath` 已接入 `InspectionService`；磁盘容量卡已改为真实接口数据 |
| 执行与运行 | 真实有效（部分次级项占位） | `AutoRun` 前端已消费，`StopOnConsecutiveNg` 后端已消费；其余保护项仍待开发 |
| 相机管理 | 真实有效（部分占位） | 搜索/绑定/删除/参数/软触发闭环完整；帧率宽高为占位 |
| AI 大模型 | 真实有效（性能卡占位） | CRUD/激活/测试/持久化完整；右侧性能卡仍是假数据 |
| 用户管理 | 真实有效（策略区占位） | CRUD/启停/重置密码完整；安全策略区仍未接后端 |

---

## 4. 详细审计结果

### 4.1 全局保存

#### 4.1.1 保存所有更改

- 判定: 半有效
- 前端证据:
  - 按钮渲染: `settingsView.js:140`
  - 监听绑定: `settingsView.js:206-208`
  - 保存实现: `settingsView.js:1755-1854`
- 后端证据:
  - 获取/保存设置接口: `SettingsEndpoints.cs:26-43`
  - 相机绑定保存接口: `SettingsEndpoints.cs:204-233`
- 已修复风险:
  - ~~`activeCameraId` 硬编码 `''`~~ → ✅ 通过 `resolveActiveCameraId()` 解析
  - ~~`heartbeatIntervalMs` 硬编码 `1000`~~ → ✅ 从 config 透传
  - ~~`retentionDays/minFreeSpaceGb` 硬编码~~ → ✅ 从 DOM 读取
- 残留风险:
  - 多步骤保存（settings → bindings）仍为非事务式，存在部分成功窗口。
  - AI 表单已改为仅在 AI 页显式保存时提交，不再由全局保存隐式触发。

---

### 4.2 常规设置

#### 4.2.1 软件标题、主题、开机自启动

- 判定: 半有效
- `theme` 由前端 `localStorage` 驱动 UI，非后端配置主导。
- `softwareTitle` 与 `autoStart` 未检索到运行时消费者（仅保存）。

#### 4.2.2 "保存常规设置"按钮

- 判定: ✅ 已收敛
- 当前页面仅保留顶部“保存所有更改”入口，不再保留空按钮。

---

### 4.3 通讯连接（PLC）

#### 4.3.1 协议/IP/端口输入

- 判定: 半有效
- 字段可保存入配置；系统配置页已具备 PLC 连接测试链路，但与流程 PLC 算子仍未统一到同一配置轴。
- `heartbeatIntervalMs`: ✅ 已修复，从 config 透传（`settingsView.js`）。

#### 4.3.2 连接测试/地址映射/同步按钮

- 判定: 半有效/占位混合
- 连接测试已有事件绑定；地址映射与同步按钮已改为 `disabled + tooltip` 的显式占位，不再误导用户。

#### 4.3.3 与流程 PLC 算子的关系

- 平台 PLC 通讯能力由流程算子参数直接驱动，与"系统配置 → 通讯连接"属于平行逻辑轴，尚未整合。

---

### 4.4 文件存储

#### 4.4.1 路径与策略

- 判定: ✅ 真实有效
- `InspectionService` 已根据 `SavePolicy` 判断是否写盘，并使用 `ImageSavePath` 解析落盘根目录。

#### 4.4.2 保留天数、磁盘阈值

- 判定: ✅ 已修复（原为硬编码占位）
- 输入框已补充 `id="cfg-retentionDays"` / `id="cfg-minFreeSpaceGb"`（`settingsView.js:1139, 1147`）。
- 保存时从 DOM 读取并含 fallback 兜底（`settingsView.js:1769-1776`）。

#### 4.4.3 目录变更、过期清理、容量图表

- 判定: 部分有效。目录运维按钮仍待开发，但磁盘容量卡已通过 `/api/settings/disk-usage` 加载真实数据。

---

### 4.5 执行与运行

#### 4.5.1 AutoRun、连续NG阈值

- 判定: ✅ 真实有效。`inspectionPanel.js` 已消费 `AutoRun`，`InspectionService` 已消费 `StopOnConsecutiveNg`。

#### 4.5.2 缺料超时、保护规则按钮、保护状态徽章

- 判定: 占位伪代码。输入无 `id`，按钮无事件，徽章为静态文案。

---

### 4.6 相机管理

#### 4.6.1 搜索、绑定、删除、参数保存、软触发

- 判定: 真实有效
- 品牌搜索（华睿/海康）、弹窗绑定、删除、参数保存闭环完整。
- `/api/cameras/soft-trigger-capture` 端点功能完备。
- 启动时 `Program.cs:144-147` 加载绑定至 `CameraManager`，`ImageAcquisitionOperator` 实时应用参数。

#### 4.6.2 手眼标定向导

- 判定: 有效（依赖运行环境）。动态 `import` 加载，非占位。

#### 4.6.3 帧率/宽/高 & 连接状态

- 帧率/宽/高: 占位伪代码（无 `id`，保存逻辑未读取）。
- 连接状态: 半有效（依据 `isEnabled` 推断，非实时链路探测）。

---

### 4.7 AI 大模型管理

#### 4.7.1 CRUD、激活、测试

- 判定: 真实有效。全链路打通，`AiConfigStore` 持久化迁移稳定。

#### 4.7.2 API Key 安全设计

- 判定: 有效。列表仅返回 `hasApiKey`，空 key 更新时后端保留原值。

#### 4.7.3 性能概览卡

- 判定: 占位伪代码。注释明确 `Fake Donut Chart`。

---

### 4.8 用户管理

#### 4.8.1 用户 CRUD/启停/重置密码

- 判定: 真实有效（管理员权限下）。

#### 4.8.2 死代码问题

- ✅ 已修复。原 L877-881 旧空壳 `bindUserManagementEvents` 已删除，仅保留 L1573 处实际实现。

#### 4.8.3 安全策略区

- 判定: 占位伪代码（仅渲染，未绑定事件或后端）。

---

## 5. 后端与配置持久化结论

### 5.1 配置基建完备

- `AppConfig` 模型完整，`JsonConfigurationService.cs` 读写健全，设置 API 成立。

### 5.2 "被消费"范围仍然有限

- 启动时明确消费的仅有相机绑定（`Program.cs:144-147`）。
- General / Communication / Storage / Runtime 多数字段仍未发现运行时消费者。

---

## 6. 风险评级（修复后更新）

### 6.1 🔴 高风险 → 已全部降级

| 原始问题 | 修复状态 | 当前风险 |
|---|---|---|
| `activeCameraId` 保存时清空 | ✅ 已修复 | 🟢 已消除 |
| `heartbeatIntervalMs` 硬编码覆写 | ✅ 已修复 | 🟢 已消除 |
| `retentionDays/minFreeSpaceGb` 硬编码 | ✅ 已修复 | 🟢 已消除 |

### 6.2 🟡 中风险（当前最高级别）

1. 全局保存仍是分步提交（settings → bindings），存在部分成功状态。
2. PLC 地址映射 / 同步等能力仍未接入真实后端，仅做了显式占位隔离。
3. 安全策略区与 AI 性能卡仍未接后端，存在“可见但不驱动系统行为”的范围。

### 6.3 🟢 低风险

1. 相机连接状态为推断值而非实时探测。
2. AI 性能概览卡仍为演示数据。

---

## 7. 第一梯队修复质量核查

> 核查时间: 2026-02-27 19:00 | 提交: `8c540eb`

### 7.1 `activeCameraId` 覆写修复

- 修复方式: 新增 `resolveActiveCameraId()` 方法 (`settingsView.js:1720-1726`)
- 核查结论: **✅ 优良**
- 逻辑: 优先使用 `this.config.activeCameraId`，若该 ID 不在当前绑定列表中则 fallback 到第一台相机。
- 同步: `saveCameraBindings()` (L1729) 和 `save()` (L1777) 均统一调用此方法。
- 评价: 方法提取合理，fallback 策略兼顾了绑定被删除的边界情况。

### 7.2 `heartbeatIntervalMs` 硬编码修复

- 修复方式: 从 `this.config.communication.heartbeatIntervalMs` 读取 (`settingsView.js:1765-1768`)
- 核查结论: **✅ 优良**
- 含 `Number.isFinite` + `> 0` 校验，无效值回退到 `defaultConfig`。不再强制覆写。

### 7.3 `retentionDays` / `minFreeSpaceGb` 硬编码修复

- 修复方式: 渲染模板添加 `id="cfg-retentionDays"` / `id="cfg-minFreeSpaceGb"`，保存时从 DOM 读取 (`settingsView.js:1139, 1147, 1769-1776`)
- 核查结论: **✅ 优良**
- 含类型校验（`parseInt`/`parseFloat` + `isFinite` + `>= 0`），无效值三级 fallback（DOM → config → defaultConfig）。

### 7.4 死代码清理

- 修复方式: 删除 L877-881 旧空壳 `bindUserManagementEvents`
- 核查结论: **✅ 合格**
- 原位置改为注释行 (L877-878)，实际函数定义仅保留 L1573 处。文件中不再存在同名重复定义。
- 小建议: L877 处残留的旧注释 `// （演示用空壳...）` 可在后续清理中移除，保持代码整洁。

---

## 8. 下一步修改规划（第二梯队）

> 2026-03-06 回填：`8.1` / `8.2` / `8.3` / `8.4` / `8.5` / `8.8` 已完成或关闭；当前剩余最高优先级为 `8.7` 保存流程事务化，其次是 `8.9 ~ 8.11` 长期基建。

### P0 — 运行时消费接入（让"可保存"变为"可生效"）

#### 8.1 `SavePolicy` / `ImageSavePath` 接入图像保存链路

- 目标: 检测流程保存结果图像时，根据 `SavePolicy`（All / NgOnly / None）决定是否写盘，存储路径使用 `ImageSavePath`。
- 涉及文件:
  - 读取端: 需在图像持久化服务（如 `ResultImageService` 或相关算子）中注入 `IConfigurationService`，启动或每次保存时读取 `AppConfig.Storage`。
  - 若不存在独立的图像保存服务，需新建 `ImageStorageService`。
- 预估: 中等（0.5 ~ 1 天）

#### 8.2 `AutoRun` / `StopOnConsecutiveNg` 接入流程控制

- 目标: 应用启动时若 `AutoRun = true`，自动进入连续运行模式；运行中连续 NG 达到 `StopOnConsecutiveNg` 阈值时暂停并报警。
- 涉及文件:
  - `Program.cs` 或主窗体启动逻辑中读取 `AppConfig.Runtime.AutoRun`。
  - 流程执行引擎（`FlowExecutionService` 或类似）中维护 NG 计数器并对比阈值。
- 预估: 中等（0.5 ~ 1 天）

### P1 — UI 占位清理与用户引导

#### 8.3 PLC 模块添加"开发中"标识

- 目标: 在连接测试、地址映射、同步按钮上添加 `disabled` 属性 + tooltip "功能开发中"，避免用户误以为可用。
- 涉及文件: `settingsView.js` — `renderCommunicationTab()`
- 预估: 小（< 0.5 天）

#### 8.4 "保存常规设置"按钮处理

- 方案 A: 删除该按钮（统一由顶部"保存所有更改"处理）。
- 方案 B: 给予 `id` 并绑定事件，仅保存 General 部分。
- 推荐: 方案 A（减少用户困惑）。
- 涉及文件: `settingsView.js` — `renderGeneralTab()`
- 预估: 小（< 0.5 天）

#### 8.5 磁盘容量卡接入真实数据

- 目标: 后端新增 `/api/system/disk-usage` 接口返回指定磁盘的使用/剩余空间，前端替代硬编码 `85% / 425GB / 75GB`。
- 涉及文件:
  - 后端: 新增 `SystemEndpoints.cs` 或在 `SettingsEndpoints` 中添加。
  - 前端: `renderStorageTab()` 改为异步加载。
- 预估: 小（< 0.5 天）

#### 8.6 清理残余注释

- 目标: 移除 `settingsView.js:877` 处的旧注释 `// （演示用空壳...）`。
- 预估: 极小

### P2 — 全局保存事务化与 AI 保存隔离

#### 8.7 保存流程事务化改造

- 目标: 将 `save()` 中的三步串行（settings → bindings → AI form）改为"先收集全部 → 单一 API 提交"模式，或在任一步骤失败时 rollback + 通知。
- 涉及文件:
  - 前端: `settingsView.js` — `save()`
  - 后端: 可考虑新增 `/api/settings/batch` 聚合端点
- 预估: 中等（1 天）

#### 8.8 隐式 AI 保存隔离

- 目标: `save()` 中的 `_saveCurrentForm()` 仅在用户当前处于 AI tab 时执行，或在执行前弹出确认。
- 涉及文件: `settingsView.js` — `save()`
- 预估: 小（< 0.5 天）

### P3 — 长期基建

#### 8.9 PLC 通讯配置与算子打通

- 目标: 让 `AppConfig.Communication` 作为 PLC 算子的默认参数源（算子未单独配置时 fallback 读取全局配置）。
- 涉及文件: `ModbusCommunicationOperator.cs`, `SiemensS7CommunicationOperator.cs`, `MitsubishiMcCommunicationOperator.cs`
- 预估: 中等（1 天）

#### 8.10 安全策略区后端接入

- 目标: 密码最小长度、会话超时、失败锁定次数写入配置并在 `AuthMiddleware` 中消费。
- 涉及文件:
  - 模型: `AppConfig.cs` 新增 `SecurityConfig`
  - 前端: `renderUserManagementTab()` 绑定事件
  - 后端: `AuthMiddleware.cs` 读取并应用
- 预估: 中 ~ 大（1 ~ 2 天）

#### 8.11 AI 性能概览卡接入真实统计

- 目标: 从后端获取 token 消耗、调用次数等替代 `Fake Donut Chart`。
- 预估: 中等（1 天）

---

## 9. 审计结论

当前"系统配置"页属于**混合成熟度**状态:

1. `AI / 相机 / 用户管理` 已具备完整的真实功能闭环，安全设计合理。
2. `PLC 通讯配置` 为 UI 壳层，实质控制分散在流程算子中。
3. `常规 / 存储 / 运行` 存在大量"可保存但不生效"项，需从"配置展示"升级到"配置驱动"。
4. **第一梯队的 4 项高风险修复已全部通过质量核查**，当前系统最高风险已降至 🟡 中风险。
5. 下一步应聚焦 **P0（运行时消费接入）** 和 **P1（UI 占位清理）**，使配置真正驱动系统行为。

