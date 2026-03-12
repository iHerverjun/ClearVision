# ClearVision 当前 Bug / 架构隐患复核（2026-03-12）

> 复核基线：结合 `docs/guides/guide-codebase-deep-dive.md` 对主链路逐项核对  
> 复核方式：源码静态审查 + 构建 + .NET 测试 + Playwright E2E  
> 结论口径：区分“当前主流程已阻断的问题”和“潜伏的坏链路/架构隐患”

---

## 1. 本轮复核结论

### 1.1 当前主流程状态

本轮复核中，以下主路径已经通过验证：

1. `dotnet build Acme.Product/Acme.Product.sln -c Debug`：通过
2. `dotnet test Acme.Product/tests/Acme.Product.Tests/Acme.Product.Tests.csproj -c Debug --no-build`：通过（`711` 通过，`4` 跳过）
3. `dotnet test Acme.Product/tests/Acme.Product.Desktop.Tests/Acme.Product.Desktop.Tests.csproj -c Debug --no-build`：通过（`8` 通过）
4. `npm test` in `Acme.Product/tests/Acme.Product.UI.Tests`：通过（Playwright `4/4`）
5. `dotnet build Acme.OperatorLibrary/Acme.OperatorLibrary.sln -c Debug`：通过

**结论**：

- 当前“工程打开 / 流程保存 / 单次检测 / 实时检测 / 设置页 / 基础 E2E”主 Happy Path 没有发现新的阻断性故障。
- 但代码里仍然存在若干**潜伏 bug、破损的宿主桥链路，以及架构层面的长期隐患**。

---

## 2. 发现总览

### 2.1 严重度统计

- `P1`：3
- `P2`：4
- `P3`：2
- 合计：9

### 2.2 分类概览

| 编号 | 类型 | 级别 | 摘要 |
|---|---|---:|---|
| `CV-20260312-01` | Bug | P1 | `WebMessageHandler` 执行单算子命令时把 `Guid` 当 `OperatorType` 解析 |
| `CV-20260312-02` | Bug | P1 | `WebMessageHandler` 的 `UpdateFlow` / `StopInspection` 是空实现或近空实现 |
| `CV-20260312-03` | Bug | P1 | `MqttPublishOperator` 既是占位实现，又存在输入/输出/参数名不一致 |
| `CV-20260312-04` | Bug / 设计缺陷 | P2 | `FlowCanvas` 内部重复定义 `handleContextMenu()` / `clear()`，后定义覆盖前定义 |
| `CV-20260312-05` | 架构隐患 | P2 | Web 服务端口范围与前端自动发现范围不一致 |
| `CV-20260312-06` | 安全隐患 | P2 | 默认管理员账号硬编码且登录页直接暴露默认密码 |
| `CV-20260312-07` | 架构隐患 | P2 | 数据库初始化使用 `EnsureCreated()`，缺少迁移链路 |
| `CV-20260312-08` | 架构隐患 | P3 | 前端存在两套默认算子目录，元数据易漂移 |
| `CV-20260312-09` | 架构隐患 | P3 | 多个关键文件超大、关键宿主桥路径缺少自动化覆盖 |

---

## 3. 详细问题清单

## CV-20260312-01（P1）

- 标题：`WebMessageHandler` 执行单算子命令时把 `Guid` 错当成 `OperatorType`
- 影响模块：桌面宿主消息桥 / 单算子执行
- 影响范围：凡是通过 `ExecuteOperatorCommand` 走宿主桥执行单算子的链路，都会在创建算子时失配
- 代码证据：
  - 合同中 `OperatorId` 是 `Guid`：`Acme.Product/src/Acme.Product.Contracts/Messages/WebMessages.cs:61`
  - 处理时错误地 `Enum.Parse<OperatorType>(command.OperatorId.ToString())`：`Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs:229`
- 根因：
  - 消息合同里传的是“节点 ID”，而不是“算子类型枚举”；处理器却直接按 `OperatorType` 枚举解析。
- 当前状态判断：
  - **当前主前端未走这条链路**，所以主 Happy Path 没被打断。
  - 但这条能力本身处于**确定性损坏**状态，属于潜伏 bug。
- 建议：
  1. 明确消息契约，是传 `OperatorType` 还是 `OperatorId`。
  2. 若传 `OperatorId`，应从当前流程上下文反查节点类型，而不是枚举解析。
  3. 补一条 `WebMessageHandler` 单测覆盖该分支。

---

## CV-20260312-02（P1）

- 标题：`WebMessageHandler` 中 `UpdateFlow` 与 `StopInspection` 仍为未完成实现
- 影响模块：桌面宿主消息桥
- 影响范围：任何继续沿用 WebMessage 方式保存流程或停止检测的调用方都会出现“看似成功、实际未执行”的假成功风险
- 代码证据：
  - `HandleUpdateFlowCommand()` 只打日志、`await Task.CompletedTask`，未真正落库：`Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs:266`
  - 文件内注释直接写明“实际实现需要注入 IProjectRepository 并调用 UpdateFlowAsync”：`Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs:277`
  - `HandleStopInspectionCommand()` 也仅日志 + `Task.CompletedTask`：`Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs:329`
- 当前状态判断：
  - 当前前端主流程已经改为 HTTP 路径保存流程：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/project/projectManager.js:124`
  - 当前前端主流程已经改为 HTTP 路径停止实时检测：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/inspection/inspectionController.js:219`
  - 所以这是**旧桥接链路破损 / 未收口**，而不是当前 UI 主链立即爆炸。
- 风险：
  - 后续如果有桌面插件、AI 或宿主扩展重走 WebMessage 旧接口，会出现“接口存在但不生效”的隐性故障。
- 建议：
  1. 彻底删掉这两条旧桥接能力，避免误用；或者
  2. 把它们补成真正可用的桥接实现；或者
  3. 至少改为显式抛错，禁止“假成功”。

---

## CV-20260312-03（P1）

- 标题：`MqttPublishOperator` 为占位实现，且输入口/输出口/参数名与实际执行不一致
- 影响模块：通信算子 / MQTT 发布
- 影响范围：算子库中该算子已对外暴露，但实际不会执行真实 MQTT 发布，而且运行时键名还错位
- 代码证据：
  - 算子已注册并出现在文档/元数据中：
    - `Acme.Product/src/Acme.Product.Desktop/DependencyInjection.cs:158`
    - `docs/operators/CATALOG.md` 中已有该算子条目
  - 输入端口定义为 `Payload`：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:38`
  - 执行时却优先读取 `inputs["Message"]`：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:73`
  - 输出端口定义为 `IsSuccess`：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:39`
  - 实际输出字典写的是 `Success`：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:96`
  - 参数定义名是 `Qos`：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:43`
  - 实际读取却是 `QoS`，而 `GetParam()` 按名称大小写精确匹配：
    - 读取处：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:60`
    - 精确匹配处：`Acme.Product/src/Acme.Product.Infrastructure/Operators/OperatorBase.cs:175`
  - 实际发布逻辑是占位模拟，`TODO` 未完成：`Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs:133`
- 根因：
  - 这是一个“已经上架到产品能力，但实现仍处于 Demo/Stub 状态”的典型问题。
- 当前状态判断：
  - 该算子如果被流程实际使用，结果是不可信的。
  - 属于**明确功能缺陷**，不是单纯“待优化”。
- 建议：
  1. 若近期不支持 MQTT，直接从算子库隐藏并在文档标注未实现。
  2. 若要支持，先统一端口名/输出键/参数名，再接入 MQTTnet。
  3. 为该算子补接口级集成测试。

---

## CV-20260312-04（P2）

- 标题：`FlowCanvas` 内部重复定义 `handleContextMenu()` / `clear()`，后定义覆盖前定义
- 影响模块：前端流程画布
- 影响范围：右键逻辑与清空逻辑可读性差，且前定义功能可能被静默覆盖
- 代码证据：
  - 第一处 `clear()`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js:1648`
  - 第二处 `clear()`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js:1934`
  - 第一处 `handleContextMenu()`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js:1726`
  - 第二处 `handleContextMenu()`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js:1815`
- 根因：
  - 类体内发生重复方法定义，后定义会覆盖前定义。
- 具体风险：
  - 第一版右键逻辑支持“右键删除连接线/节点”。
  - 第二版右键逻辑改成“只在节点上显示上下文菜单”。
  - 由于后者覆盖前者，前者逻辑**实际上已经失效**，但源码里仍保留，极易误导维护者。
- 当前状态判断：
  - 这更像**前端可维护性缺陷 + 潜在交互回归**。
  - 它未必影响现有 4 条 E2E，但会影响后续扩展和问题定位。
- 建议：
  1. 合并为单一 `handleContextMenu()`，明确节点/连接/空白区域分支。
  2. 删除重复 `clear()` 实现。
  3. 增加画布右键行为的前端单测或 E2E。

---

## CV-20260312-05（P2）

- 标题：后端端口选择范围与前端自动发现范围不一致
- 影响模块：桌面宿主启动 / 前后端连接发现
- 影响范围：在 API Base URL 注入失败或本地缓存丢失时，前端存在找不到后端端口的风险
- 代码证据：
  - 后端端口范围：`FindAvailablePort(5000, 6000)`：`Acme.Product/src/Acme.Product.Desktop/Program.cs:118`
  - 前端自动发现仅探测 `5000~5005`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/messaging/httpClient.js:70`
  - 登录校验回退直接使用 `savedPort || 5000`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/auth/auth.js:141`
- 根因：
  - 宿主端口策略扩大后，前端探测逻辑没同步更新。
- 当前状态判断：
  - 当前在 WebView2 正常注入 `window.__API_BASE_URL__` 时通常没问题。
  - 但它属于**典型“兜底链路与主链路不一致”隐患**。
- 建议：
  1. 把前端探测范围与后端统一配置化。
  2. 最好只保留注入式发现，不要保留分散的端口猜测逻辑。

---

## CV-20260312-06（P2）

- 标题：默认管理员账号硬编码且登录页直接展示默认密码
- 影响模块：认证与发布安全
- 影响范围：任何未做二次处理的安装包都带默认高权限口令
- 代码证据：
  - 自动创建默认管理员：`Acme.Product/src/Acme.Product.Desktop/Program.cs:327`
  - 登录页直接展示 `admin / admin123`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/login.html:90`
  - 前端令牌保存在 `localStorage`：
    - `Acme.Product/src/Acme.Product.Desktop/wwwroot/login.html:178`
    - `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/messaging/httpClient.js:21`
  - 后端会话仅保存在内存中，重启即失效：`Acme.Product/src/Acme.Product.Application/Services/AuthService.cs:15`
- 当前状态判断：
  - 这是**安全设计风险**，不是功能性回归。
- 风险拆解：
  1. 发布包若直连产线环境，存在默认口令风险。
  2. 前端持久化令牌与后端内存会话模型不一致，重启后会出现“本地仍有 token，但服务端会话消失”的状态漂移。
- 建议：
  1. 首次安装改为“必须初始化管理员密码”。
  2. 至少加“首次登录强制改密”。
  3. 评估改成持久化会话或短时 token + 刷新机制。

---

## CV-20260312-07（P2）

- 标题：数据库初始化使用 `EnsureCreated()`，缺少迁移管理链路
- 影响模块：数据库演进 / 发布升级
- 影响范围：后续模型调整时，历史用户库升级风险高
- 代码证据：
  - `dbContext.Database.EnsureCreated()`：`Acme.Product/src/Acme.Product.Desktop/Program.cs:157`
  - 当前仓库未发现 `Migrations/` 目录
- 根因：
  - 当前更偏“本地自举式”初始化，而不是正式的 schema 迁移模式。
- 当前状态判断：
  - 在单机开发/测试阶段可以工作。
  - 但只要实体结构继续演进，就会进入“新版本打不开老库 / 只能删库重建”的风险区间。
- 建议：
  1. 引入 EF Core Migrations。
  2. 明确 `vision.db` 的版本升级策略。
  3. 给配置/工程/用户数据准备升级脚本或兼容方案。

---

## CV-20260312-08（P3）

- 标题：前端存在两套默认算子目录，容易发生元数据漂移
- 影响模块：算子库前端展示
- 影响范围：后端算子库接口失败时，前端回退数据可能与真实算子元数据不一致
- 代码证据：
  - `app.js` 内置一套 `getDefaultOperators()`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js:828`
  - `operatorLibrary.js` 里又内置一套 `getDefaultOperators()`：`Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js:293`
- 当前状态判断：
  - 当前主链以 `/api/operators/library` 为准，所以平时不一定出错。
  - 但一旦进入回退路径，展示、参数、分类就可能与实际执行器脱节。
- 建议：
  1. 只保留一套前端回退元数据。
  2. 更进一步，直接移除前端回退目录，统一以后端元数据为唯一事实源。

---

## CV-20260312-09（P3）

- 标题：关键文件体量过大，且最脆弱的宿主桥路径缺少测试覆盖
- 影响模块：整体可维护性
- 证据位置：
  - `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/settings/settingsView.js`：约 `2086` 行
  - `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js`：约 `1946` 行
  - `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js`：约 `1707` 行
  - `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs`：约 `745` 行
  - 当前测试中未检索到 `WebMessageHandler` / `MqttPublishOperator` / `FlowCanvas` 的直接覆盖
- 当前状态判断：
  - 这是长期可维护性风险，而不是立即可复现的生产故障。
- 风险：
  - 代码继续迭代时，非常容易出现“局部修复影响别的分支”的隐式回归。
  - 本次发现的几个 latent bug，恰好都集中在这些未被覆盖的复杂文件中。
- 建议：
  1. 将 `settingsView.js` / `flowCanvas.js` 拆模块。
  2. 补宿主桥单测。
  3. 补关键算子契约测试。

---

## 4. 建议优先级

### 第一优先级：建议尽快修

1. `CV-20260312-01`：修复 `ExecuteOperatorCommand` 的 `Guid -> OperatorType` 错配
2. `CV-20260312-02`：处理 WebMessage 里的假实现（删或补）
3. `CV-20260312-03`：处理 `MqttPublishOperator`（隐藏或修正）

### 第二优先级：建议本迭代收口

4. `CV-20260312-04`：清理 `FlowCanvas` 重复方法定义
5. `CV-20260312-05`：统一端口发现策略
6. `CV-20260312-06`：收紧默认管理员与 token 策略
7. `CV-20260312-07`：规划迁移链路

### 第三优先级：建议作为重构债务纳入路线图

8. `CV-20260312-08`：统一前端回退算子目录
9. `CV-20260312-09`：拆分超大文件并补测试

---

## 5. 最终结论

### 5.1 正向结论

- 当前主产品已经比 2026-03-04 的状态稳定很多。
- 核心 Happy Path 构建、单元测试、桌面测试、E2E 都能通过。
- 说明“工程/检测/设置/基础前端交互”主链已经进入可持续迭代状态。

### 5.2 风险结论

- 当前最主要的问题，不再是“系统已经大面积坏掉”，而是：
  - 存在若干**未被主流程覆盖到的破损旧链路**；
  - 存在**已经对外暴露但实际仍是半成品的能力**；
  - 存在**安全与演进层面的结构性隐患**。

换句话说：

> **当前项目不是“不稳定”，而是“主链可用，但边缘链路和长期架构债务还没清干净”。**

