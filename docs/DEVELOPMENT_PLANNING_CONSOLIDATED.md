# ClearVision 开发规划综合文档

> **文档合并日期**: 2026-03-16  
> **合并范围**: 用户系统规划、AI 模型集成、PLC 通信、算子实现等各类规划文档  
> **原始文件**: 已归档至 `docs/archive/legacy_md_pre_merge_20260316/`

---

## 目录

1. [用户系统开发计划](#一用户系统开发计划)
2. [AI 模型集成重构方案](#二ai-模型集成重构方案)
3. [PLC 通信审计与路线图](#三plc-通信审计与路线图)
4. [算子实现收敛计划](#四算子实现收敛计划)
5. [清霜 V3 迁移规划](#五清霜-v3-迁移规划)
6. [综合开发路线图](#六综合开发路线图)

7. [开放待办分类快照归档](#七开放待办分类快照归档2026-03-16)
---

## 一、用户系统开发计划

### 1.1 目标概述

为 ClearVision 工业视觉软件添加**三级角色权限管理系统**，控制不同用户对系统功能的访问权限。

| 角色 | 使用者 | 权限范围 |
|------|--------|---------|
| **Admin（管理员）** | IT / 管理人员 | 全部权限 + 用户管理 |
| **Engineer（工程师）** | 视觉工程师 | 项目编辑 + 运行 + 调试 |
| **Operator（操作员）** | 产线作业人员 | 运行已有项目 + 查看结果（可只读浏览流程） |

### 1.2 设计决策

| 项目 | 决策 |
|------|------|
| 认证方案 | **简单 Token（内存管理）**，应用重启需重新登录 |
| 密码策略 | 最小 6 位，**无强制改密码**，**无错误锁定** |
| 操作员模式 | **宽松** — 可只读浏览项目流程，但不可修改 |
| 审计日志 | **不需要** |

### 1.3 技术架构

遵循现有 DDD Lite 分层架构，各层新增内容如下：

```
Acme.Product.Core         → 实体 User、枚举 UserRole、接口 IUserRepository
Acme.Product.Infrastructure → UserRepository、PasswordHasher、DbContext 扩展
Acme.Product.Application   → AuthService、UserManagementService
Acme.Product.Desktop       → AuthEndpoints、UserEndpoints、AuthMiddleware
Frontend (wwwroot)         → 登录页面、用户管理页面、权限感知 UI
```

#### 数据模型 — Users 表

| 字段 | 类型 | 说明 |
|------|------|------|
| `Id` | GUID (PK) | 继承自 Entity 基类 |
| `Username` | VARCHAR(100), UNIQUE | 登录用户名 |
| `PasswordHash` | VARCHAR(256) | bcrypt 哈希 |
| `DisplayName` | VARCHAR(200) | 显示名称 |
| `Role` | INT | 0=Admin, 1=Engineer, 2=Operator |
| `IsActive` | BOOL | 是否启用 |
| `LastLoginAt` | DATETIME? | 最后登录时间 |

### 1.4 分阶段实施计划

#### Phase A — Core 层（领域模型）

| 操作 | 文件 | 说明 |
|------|------|------|
| **[NEW]** | `Core/Enums/UserRole.cs` | `Admin=0, Engineer=1, Operator=2` 枚举 |
| **[NEW]** | `Core/Entities/User.cs` | 继承 `Entity`，包含 Username、PasswordHash、Role 等属性 |
| **[NEW]** | `Core/Interfaces/IUserRepository.cs` | 继承 `IRepository<User>`，添加查询方法 |

#### Phase B — Infrastructure 层（数据实现）

| 操作 | 文件 | 说明 |
|------|------|------|
| **[MODIFY]** | `Infrastructure/Data/VisionDbContext.cs` | 添加 `DbSet<User>`，配置实体映射和唯一索引 |
| **[NEW]** | `Infrastructure/Repositories/UserRepository.cs` | 实现 `IUserRepository`，CRUD 操作 |
| **[NEW]** | `Infrastructure/Services/PasswordHasher.cs` | 使用 `BCrypt.Net-Next` 进行密码哈希/验证 |

#### Phase C — Application 层（业务服务）

| 操作 | 文件 | 说明 |
|------|------|------|
| **[NEW]** | `Application/Services/IAuthService.cs` | 接口定义: `LoginAsync()`, `LogoutAsync()`, `ValidateTokenAsync()` |
| **[NEW]** | `Application/Services/AuthService.cs` | 实现认证逻辑，内存 Token 管理 |
| **[NEW]** | `Application/Services/UserManagementService.cs` | 用户 CRUD 服务（含角色校验：仅 Admin 可调用） |

#### Phase D — Desktop 层（API + 中间件）

| 操作 | 文件 | 说明 |
|------|------|------|
| **[NEW]** | `Desktop/Endpoints/AuthEndpoints.cs` | 登录/登出/获取当前用户/改密码 API |
| **[NEW]** | `Desktop/Endpoints/UserEndpoints.cs` | 用户管理 CRUD API（Admin 专用） |
| **[NEW]** | `Desktop/Middleware/AuthMiddleware.cs` | Token 验证 + 角色注入中间件 |

#### API 端点设计

**认证端点:**

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| POST | `/api/auth/login` | 登录 | 公开 |
| POST | `/api/auth/logout` | 登出 | 已登录 |
| GET | `/api/auth/me` | 获取当前用户 | 已登录 |
| POST | `/api/auth/change-password` | 修改密码 | 已登录 |

**用户管理端点:**

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| GET | `/api/users` | 用户列表 | Admin |
| POST | `/api/users` | 创建用户 | Admin |
| PUT | `/api/users/{id}` | 修改用户 | Admin |
| DELETE | `/api/users/{id}` | 删除用户 | Admin |

#### Phase E — 前端实现

| 操作 | 文件 | 说明 |
|------|------|------|
| **[NEW]** | `wwwroot/login.html` | 全屏登录界面（品牌 Logo + 用户名密码） |
| **[NEW]** | `wwwroot/src/features/auth/auth.js` | 登录/登出逻辑、Token 管理、权限检查工具函数 |
| **[MODIFY]** | `wwwroot/src/app.js` | 启动时检查登录状态，未登录则跳转 |

#### 权限控制矩阵

| UI 功能 | Admin | Engineer | Operator |
|---------|:-----:|:--------:|:--------:|
| 运行检测 | ✅ | ✅ | ✅ |
| 查看结果/图像 | ✅ | ✅ | ✅ |
| 浏览项目流程（只读） | ✅ | ✅ | ✅ |
| 创建/编辑/删除项目 | ✅ | ✅ | ❌ |
| 编辑算子流程/参数 | ✅ | ✅ | ❌ |
| 系统设置 | ✅ | ⚠️部分 | ❌ |
| 用户管理 | ✅ | ❌ | ❌ |

### 1.5 工作量估算

| 阶段 | 新增 | 修改 | 预估 |
|------|:----:|:----:|:----:|
| A — Core 层 | 3 文件 | 0 | ~0.5 天 |
| B — Infrastructure 层 | 2 文件 | 2 文件 | ~1 天 |
| C — Application 层 | 4 文件 | 0 | ~1 天 |
| D — Desktop 层 | 3 文件 | 2 文件 | ~1.5 天 |
| E — 前端 | 5 文件 | 3 文件 | ~2-3 天 |
| **合计** | **~17 文件** | **~7 文件** | **~6-7 天** |

---

## 二、AI 模型集成重构方案

### 2.1 决策约束

本方案基于以下边界执行：

- **暂不处理**密钥下线/轮换/加密存储治理（设计期先保证调试效率）
- 优先重构"**多平台模型接入能力**"与"**统一架构**"
- 采用**增量迁移**，避免一次性大改导致生成功能停摆

### 2.2 当前状态摘要

#### 2.2.1 线上主链路

当前真实生成链路是：

```
WebMessageHandler → GenerateFlowMessageHandler → AiFlowGenerationService → AiApiClient → 各 Provider HTTP
```

关键文件：
- `Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/GenerateFlowMessageHandler.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/AiFlowGenerationService.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/AI/AiApiClient.cs`

#### 2.2.2 并存的第二套接入架构

项目里还存在一套 `ILLMConnector + Factory + Store` 架构（OpenAI/Azure/Ollama），但不在主链路中承担生成流量。

#### 2.2.3 主要问题

- 双轨并存，维护成本高，行为不一致风险高
- Provider 能力靠字符串启发式，缺少显式 capability 声明
- 模型配置表达力不足（缺 headers/query/body 扩展与认证模式声明）
- 测试连接逻辑对 keyless 本地模型不友好

### 2.3 目标架构

目标是收敛为一条统一数据面：

```
GenerateFlow -> AiGenerationOrchestrator -> IAiConnector -> Provider Adapter
```

并保留配置控制面：

```
Settings API/UI -> AiModelRegistry -> ai_models.json
```

### 2.4 核心抽象（新增）

1. **`IAiConnector`**
   - 统一聊天补全接口（普通 + 流式）
   - 输入为结构化消息（文本 + 图片），输出统一 `AiCompletionResult`

2. **`IAiConnectorFactory`**
   - 按模型配置的 `Protocol`/`ProviderKind` 返回对应 connector

3. **`IAiModelRegistry`**
   - 统一模型读取、激活、更新、能力读取与选择
   - 兼容当前 `AiConfigStore` 的 JSON 持久化

4. **`IAiModelSelector`**
   - 按任务角色与能力选择模型
   - 支持主模型/小模型/推理模型路由

5. **`AiGenerationOrchestrator`**
   - 负责重试、降级、fallback、附件策略、遥测采集
   - `AiFlowGenerationService` 只做业务编排，不直接耦合 provider 细节

### 2.5 模型配置标准化（增强）

在现有 `AiModelConfig` 基础上扩展：

- `Protocol`: `openai_compatible | anthropic | azure_openai | ollama_native`
- `AuthMode`: `bearer | header_key | none`
- `AuthHeaderName`: 如 `Authorization`、`x-api-key`
- `ChatPath`: 可配置补全路径（默认自动推导）
- `ExtraHeaders`: `Dictionary<string,string>`
- `ExtraQuery`: `Dictionary<string,string>`
- `ExtraBody`: `Dictionary<string,object>`
- `Capabilities`: 显式能力声明
- `RoleBindings`: `generation | reasoning | fallback | validation`
- `Priority`: fallback 顺序

### 2.6 能力声明（Capability Matrix）

新增 `AiModelCapabilities`：

- `SupportsStreaming`
- `SupportsVisionInput`
- `SupportsReasoningStream`
- `SupportsJsonMode`
- `SupportsToolCall`
- `SupportsSystemPrompt`
- `MaxImageCount`
- `MaxImageBytes`

规则：

- 附件策略不再靠 `model.Contains("reasoner")`
- 按 capability 进行前置拦截与降级

### 2.7 分阶段实施

#### 阶段 A：统一抽象与兼容适配（不改外部行为）

目标：先把主链路挂到统一接口，行为保持不变。

1. **新增抽象与 orchestrator 框架**
   - 新建目录：`Acme.Product/src/Acme.Product.Infrastructure/AI/Runtime/`
   - 文件：`IAiConnector.cs`, `IAiConnectorFactory.cs`, `IAiModelRegistry.cs`, `IAiModelSelector.cs`, `AiGenerationOrchestrator.cs`

2. **新增过渡适配器**
   - `AiApiClientAdapterConnector.cs`（内部调用当前 `AiApiClient`）
   - 作用：先把主流程挂上新抽象，不立即重写 provider 细节

3. **调整服务注入**
   - `AiGenerationServiceExtensions.cs` 改为注入 orchestrator
   - `AiFlowGenerationService` 不再直接持有 `AiApiClient`

**完成定义（DoD）**：
- 现有功能行为不变
- 现有测试通过（尤其 AI 相关测试）
- 主链路已经只依赖 `IAiConnector`

**预计工期**：`3-4` 人天

#### 阶段 B：Provider 适配统一化（收敛双轨）

目标：把旧 `Connectors/*` 与 `AiApiClient` 逻辑统一到同一实现体系。

1. **Provider Adapter 拆分**
   - `OpenAiCompatibleConnector`（覆盖 OpenAI/OpenAI 兼容厂商）
   - `AnthropicConnector`
   - `AzureOpenAiConnectorV2`（可复用现有类的 HTTP 结构）
   - `OllamaNativeConnector`（支持 keyless）

2. **工厂统一**
   - `AiConnectorFactory` 基于 `Protocol` 构建 connector

3. **退役旧路径**
   - `AiApiClient` 标记为过渡层，逐步只保留工具函数或彻底下线
   - `AIWorkflowService`/`DynamicLLMConnector` 复用同一 connector 工厂

**预计工期**：`5-7` 人天

#### 阶段 C：能力驱动路由 + 降级策略

目标：让"模型支持什么"可声明、可路由、可降级。

1. **能力模型落地**
   - `AiModelCapabilities` 加入 `AiModelConfig`
   - 新增默认能力映射与回填逻辑

2. **选择器落地**
   - `AiModelSelector` 按 `RoleBindings + Capabilities + Priority` 选择模型

3. **生成策略升级**
   - `AiGenerationOrchestrator` 内实现：
     - 图片输入前置能力检查
     - `400/429/5xx` 分类处理
     - fallback 切换（主 -> 备）

**预计工期**：`3-4` 人天

#### 阶段 D：设置页与 API 契约升级（保兼容）

目标：配置可表达复杂 provider 接入，不破坏现有配置。

1. **后端 API 升级**
   - 文件：`Acme.Product/src/Acme.Product.Desktop/Endpoints/SettingsEndpoints.cs`
   - 新增/扩展字段透传：protocol, authMode, authHeaderName, extraHeaders, extraQuery, extraBody, capabilities, roleBindings, priority

2. **前端设置页升级**
   - 表单新增高级配置折叠区
   - Provider 下拉改为 `Protocol` 选项（保留旧值自动映射）

3. **配置迁移**
   - `ai_models.json` 自动迁移旧字段

**预计工期**：`3-4` 人天

### 2.8 验收标准

1. 主链路只有一套 connector 调用路径
2. OpenAI、Anthropic、OpenAI-compatible、Ollama 全部在新链路可用
3. 多模态支持走 capability 判断，不再靠模型名启发式
4. 设置页可配置高级 provider 参数并成功连通测试
5. 回归测试通过，且具备开关回退能力

---

## 三、PLC 通信审计与路线图

### 3.1 审计范围与边界

#### 代码范围

- `Acme.Product/src/Acme.PlcComm`
  - `Core/`
  - `Interfaces/`
  - `Siemens/`
  - `Mitsubishi/`
  - `Omron/`
- `Acme.Product/src/Acme.Product.Infrastructure/Operators/`
  - `PlcCommunicationOperatorBase.cs`
  - `SiemensS7CommunicationOperator.cs`
  - `MitsubishiMcCommunicationOperator.cs`
  - `OmronFinsCommunicationOperator.cs`

#### 本次不包含

- 新增协议品牌（如 AB、Delta、Keyence、Modbus 扩展、OPC UA 新驱动等）
- UI/交互层视觉设计调整
- 现场 PLC 实机联调

### 3.2 执行摘要（当前状态判断）

`Acme.PlcComm` 已具备"可用的统一抽象 + 三协议接入"的基础框架，但与成熟商用品质相比，当前主要短板在：

1. 协议细节的正确性一致性（端序、长度语义、位读写语义）
2. 可靠性细节（断连清理、半包处理、重连机制精度）
3. 诊断/错误模型（错误码可操作性、事件体系落地）
4. 自动化验证缺口（PLC 核心与三协议算子测试不足）

### 3.3 已确定开发方针（执行约束）

1. **品牌冻结**：S7、MC、FINS 之外不新增协议品牌
2. **先正确性后扩展性**：先完成 P0/P1 再进入增强项
3. **先测试后重构**：先补最小可行回归测试，再做结构重构
4. **协议语义一致化**：统一长度、端序、位访问语义定义并文档化

### 3.4 核心问题清单（按优先级）

#### P0（必须优先修复）

| 编号 | 问题 | 影响 | 修复方向 |
|------|------|------|---------|
| P0-1 | `Dispose` 先置 `_disposed=true`，导致 `DisconnectAsync` 直接返回，底层资源清理被跳过 | 连接/流对象可能残留 | 调整生命周期顺序 |
| P0-2 | 算子层值转换统一使用 `BitConverter`（主机端序），与 S7/FINS 大端语义冲突 | S7/FINS 读写可能出现值错误 | 使用客户端 `ByteTransform` |
| P0-3 | 长度语义多层不一致，存在"字节数再次乘类型长度"的过读风险 | 读范围异常 | 统一 `ReadAsync(address, length)` 的长度定义 |
| P0-4 | S7 心跳地址 `MW0` 与解析器正则不兼容，可能误判离线 | 心跳失败触发上层断连/重连抖动 | 扩展解析器支持 `MWx`/`MBx`/`MDx` 语法 |
| P0-5 | S7 位地址虽解析了 `BitOffset`，但读写路径未使用位偏移 | 位级语义可能失真 | 显式处理 bit access |
| P0-6 | FINS 写入长度固定按 `data.Length/2`，位写入可能变成 0 长度 | 位写入报文非法 | 按"位/字访问模式"分别计算 length |

#### P1（高优先级）

| 编号 | 问题 | 影响 | 修复方向 |
|------|------|------|---------|
| P1-1 | TCP 读取响应未实现"读满循环"，单次 `ReadAsync` 易受半包影响 | 偶发报文不完整 | 抽象 `ReadExactAsync` 公共方法 |
| P1-2 | 泛型重连模板 `ExecuteWithReconnectAsync<T>` 存在重复执行业务操作风险 | 写操作可能重复发送 | 重构为"单次执行返回真实结果"的模板 |
| P1-3 | `ReconnectPolicy.MaxRetryInterval` 已定义但未实际生效 | 重连退避上限不可控 | 退避 delay 应 `Min(计算值, MaxRetryInterval)` |
| P1-4 | `ErrorOccurred` 事件已声明未触发 | 可观测性不足 | 在失败路径统一触发 |
| P1-5 | 工厂创建 URI 参数解析使用 `int.Parse`，缺乏鲁棒性 | 配置输入异常时直接抛错 | 改为 `TryParse` + 明确错误信息 |

#### P2（重要但可后置）

| 编号 | 问题 | 影响 | 修复方向 |
|------|------|------|---------|
| P2-1 | `ReadBatchAsync` 当前为串行逐条读取，无聚合优化 | 高频多点位吞吐受限 | 同协议连续地址聚合读 |
| P2-2 | 字符串读写长度策略简单 | 固定长度字符串场景兼容性一般 | 提供 `WriteString(address, value, fixedLength, encoding)` |
| P2-3 | FINS 算子暴露了轮询参数，但执行路径未使用轮询逻辑 | 参数语义与行为不一致 | 要么实现轮询，要么移除参数 |
| P2-4 | PLC 相关自动化测试仍不完整 | 回归风险 | 补齐 S7 算子级集成、客户端契约测试 |

### 3.5 核心类型定义

#### `OperateResult<T>` — 统一操作结果

```csharp
public class OperateResult
{
    public bool IsSuccess { get; set; }
    public int ErrorCode { get; set; }
    public string Message { get; set; } = string.Empty;

    public static OperateResult Success() => new() { IsSuccess = true };
    public static OperateResult Failure(int code, string msg) => 
        new() { IsSuccess = false, ErrorCode = code, Message = msg };
}

public class OperateResult<T> : OperateResult
{
    public T? Content { get; set; }

    public static OperateResult<T> Success(T content) => 
        new() { IsSuccess = true, Content = content };
    public static new OperateResult<T> Failure(int code, string msg) => 
        new() { IsSuccess = false, ErrorCode = code, Message = msg };
}
```

#### `IPlcClient` — 统一客户端接口

```csharp
public interface IPlcClient : IDisposable
{
    string IpAddress { get; }
    int Port { get; }
    bool IsConnected { get; }
    
    // 三级超时控制（单位：毫秒）
    int ConnectTimeout { get; set; }   // 默认 10000
    int ReadTimeout { get; set; }      // 默认 5000
    int WriteTimeout { get; set; }     // 默认 5000
    
    // 重连策略
    ReconnectPolicy ReconnectPolicy { get; set; }

    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    
    Task<OperateResult<byte[]>> ReadAsync(string address, ushort length, CancellationToken ct = default);
    Task<OperateResult> WriteAsync(string address, byte[] value, CancellationToken ct = default);
    
    Task<OperateResult<T>> ReadAsync<T>(string address, CancellationToken ct = default) where T : struct;
    Task<OperateResult> WriteAsync<T>(string address, T value, CancellationToken ct = default) where T : struct;
}
```

### 3.6 分阶段路线图

#### 阶段 A：Correctness 修复（P0）

**目标**：消除会导致"错误值、误掉线、资源泄漏"的核心风险。

**任务清单**：
- [x] 修复 `Dispose`/`DisconnectAsync` 生命周期顺序问题（P0-1）
- [x] 统一端序转换路径，移除算子层 `BitConverter` 直转（P0-2）
- [x] 统一长度语义并修复 S7 过读风险（P0-3）
- [x] 修复 S7 心跳地址兼容（P0-4）
- [x] 实现 S7 位读写语义（P0-5）
- [x] 修复 FINS 位写长度计算（P0-6）

**阶段验收状态（2026-03-07）**：
- [x] S7/MC/FINS 的 Bool/Word/DWord/Float 在回归测试中一致通过
- [x] 心跳线程不再因地址语法问题误触发掉线重连
- [x] 连接对象在 `Dispose` 后被正确释放（无残留活动 socket）

#### 阶段 B：Reliability 与可观测性（P1）

**任务清单**：
- [x] 实现 `ReadExactAsync`，替换所有响应读取点（P1-1）
- [x] 重构 `ExecuteWithReconnectAsync<T>`，确保单次业务执行（P1-2）
- [x] 让 `MaxRetryInterval` 生效（P1-3）
- [x] 打通 `ErrorOccurred` 事件触发链路（P1-4）
- [x] 工厂 URI 参数改 `TryParse` 并增强错误信息（P1-5）

**阶段验收状态（2026-03-07）**：
- [x] 人工注入半包/抖动场景下不出现随机"读响应不完整"失败
- [x] 写操作回放日志中不存在重复发送同一业务写请求
- [x] 错误事件可被上层订阅并看到分类错误码

#### 阶段 C：工程化补齐（P2）

**任务清单**：
- [ ] 增加 PLC 协议单元测试（地址解析、帧编解码、类型转换）
- [ ] 增加协议客户端契约测试（含断连、超时、重连）
- [x] 增加 MC / FINS 算子级集成测试（Read / Write 端到端流程）
- [ ] 补齐 S7 算子级集成测试（需模拟器或可注入连接缝）
- [ ] 处理 FINS 轮询参数"暴露但未执行"问题（实现或收敛）
- [ ] 评估批读聚合策略（按连续地址分组）

---

## 四、算子实现收敛计划

### 4.1 文档目的

本文档用于统一整理当前算子库中三类问题：

1. **契约不一致**：元数据、UI 参数、输入输出端口、运行时行为不一致
2. **实现欠账**：隐藏参数、未生效参数、隐藏输入、输出键漂移、失败语义不统一
3. **算法边界**：当前实现可用，但能力边界与用户预期存在偏差

### 4.2 排查范围

- 代码范围：`Acme.Product/src/Acme.Product.Infrastructure/Operators/**/*.cs`
- 文档范围：`docs/operators/*.md`
- 交叉参考：`docs/AlgorithmAudit/*.md`
- 覆盖算子总数：**118**

### 4.3 P0 优先级清单

P0 定义：直接影响调参、运行结果理解、下游集成，或容易导致用户以为「算子效果差/算子坏了」。

| 算子 | 问题类型 | 高置信度问题 | 建议动作 |
|------|----------|-------------|---------|
| `TemplateMatching` | 契约 + 实现 | `MaxMatches` 已声明但未生效；`Method` 元数据只暴露部分选项 | 统一 `Method` 枚举；实现多目标循环匹配 |
| `WidthMeasurement` | 契约 + 算法边界 | `Direction` 未生效；手动/自动模式差异 | 实现 `Direction` 与 `CustomAngle`；增加亚像素检测骨架 |
| `GradientShapeMatch` | 契约 + 实现 | `_matcherCache` 键碰撞风险；`Position` 缺失 | 修缓存键；显式输出 `Position` |
| `OrbFeatureMatch` | 契约 + 实现 | `EnableSymmetryTest` / `MinMatchCount` 隐藏；失败语义不统一 | 暴露隐藏参数；统一输出 `Position` 结构 |
| `DeepLearning` | 契约 + 实现 | `UseGpu` / `GpuDeviceId` 隐藏；`DetectionList` 端口未声明 | 暴露 GPU 参数；显式声明 `DetectionList` 端口 |
| `ImageAcquisition` | 契约 + 实现 | `exposureTime`、`gain`、`triggerMode` 参数预览与元数据不同步 | 在元数据中稳定声明采集参数 |
| `TypeConvert` | 契约 | 声明输入 `Input` 与源码 `Value` 不一致；输出端口漂移 | 统一输入键为 `Input`，移除 `Value` 读取 |
| `TriggerModule` | 契约 | 未声明 `Signal` 端口，隐藏路径读取 `Trigger` | 正式声明 `Signal` 输入端口 |

### 4.4 收敛原则（决策 checklist）

1. 已声明端口必须真实存在：声明了 `Position` 就真正输出 `Position`
2. 隐藏参数必须二选一：要么正式暴露，要么删除运行时读取
3. 已声明参数必须三选一：要么实现、要么废弃、要么从元数据移除
4. 输出端口必须名实一致：声明了 `Position` 就真正输出 `Position`
5. 业务失败语义统一：明确哪些算子使用「执行成功但业务 NG」，哪些算子使用框架级 `Failure`
6. 算法升级与契约收敛分批进行，先修可解释性，再修性能和精度增强

### 4.5 建议实施顺序

#### Batch A：契约止血（先做）

目标：先解决「UI 看起来是 A，运行时是 B」的问题。

建议覆盖：
- `TemplateMatching`, `WidthMeasurement`, `OrbFeatureMatch`, `GradientShapeMatch`
- `ImageAcquisition`, `ContourDetection`, `TypeConvert`, `TriggerModule`

#### Batch B：输出结构统一

目标：统一 `Position / X / Y / CenterX / CenterY / FitResult / ColorInfo / DetectionList` 等结果模型。

建议覆盖：
- `CircleMeasurement`, `ColorDetection`, `GeometricFitting`, `DeepLearning`

#### Batch C：算法能力补齐

目标：修真正会影响「效果」的能力缺口。

建议覆盖：
- `TemplateMatching` 的多匹配支持
- `WidthMeasurement` 的方向/法向策略与亚像素选项
- `ShapeMatching` 的尺度搜索或能力重命名
- `GeometricFitting` 的单轮廓/最大轮廓选择

#### Batch D：低风险机械收敛

目标：清理隐藏参数、未用参数、隐藏输入、输出声明噪声。

建议方式：
1. 一次只做一种问题类型
2. 每批附带自动化扫描和契约测试
3. 不与算法升级混在同一个 PR 中

---

## 五、清霜 V3 迁移规划

### 5.1 总体约束（所有Phase通用）

#### 架构约束

- **保持 DDD 洋葱架构不变**：`Core` 层定义接口/枚举/实体，`Infrastructure` 层实现具体逻辑
- **前端 UI 样式不做任何修改**，仅允许新增前端元数据注册
- 命名空间严格遵循已有约定，不创建新的顶级命名空间

#### 算子注册清单（每新增一个算子必须完成的4步）

添加新算子时，**必须依次修改以下4个文件**，漏掉任何一个都会导致算子不可用：

| 步骤 | 文件路径 | 操作 |
|-----:|------|------|
| ① | `Acme.Product.Core/Enums/OperatorEnums.cs` | 在 `OperatorType` 枚举中添加新值（当前最大值 = 84，从 **90** 开始分配） |
| ② | `Acme.Product.Infrastructure/Operators/` | 创建算子类，继承 `OperatorBase`，实现 `ExecuteCoreAsync` 和 `ValidateParameters` |
| ③ | `Acme.Product.Infrastructure/Services/OperatorFactory.cs` | 在 `InitializeDefaultOperators()` 方法末尾添加 `_metadata[OperatorType.Xxx] = new OperatorMetadata{...}` |
| ④ | `Acme.Product.Desktop/DependencyInjection.cs` | 添加 `services.AddSingleton<IOperatorExecutor, XxxOperator>();` |

### 5.2 第一阶段：核心底层能力移植

#### 5.2.1 相机SDK深度集成

**目标架构**：

```
Acme.Product.Core/Cameras/
├── ICamera.cs                  （已有，保持不变）
├── IIndustrialCamera.cs        【新增】扩展接口
├── ICameraManager.cs           【新增】管理器接口
└── ICameraProvider.cs          【新增】发现/创建抽象

Acme.Product.Infrastructure/Cameras/
├── MockCamera.cs               （已有，保持不变）
├── HikvisionCamera.cs          【新增】移植自清霜V3
├── MindVisionCamera.cs         【新增】移植自清霜V3
├── CameraProviderFactory.cs    【新增】工厂类
└── CameraManager.cs            【新增】统一管理器
```

**关键实现要点**：

1. **SDK依赖引入**
   - 华睿相机SDK：`MVSDK.dll`
   - 海康相机SDK：`MvCameraControl.Net.dll`

2. **定义扩展接口**
   - `IIndustrialCamera`：扩展 `ICamera`，增加 `SetTriggerModeAsync`, `ExecuteSoftwareTriggerAsync`, `FrameReceived` 事件
   - `ICameraManager`：`DiscoverAllCamerasAsync`, `GetOrCreateCameraAsync`, `GetConnectedCameras`, `DisconnectAllAsync`

3. **移植相机适配器**
   - `HikvisionCamera.cs`：实现 `IIndustrialCamera` 接口，保留 `_hDevice` 句柄管理
   - `MindVisionCamera.cs`：确保 `MVSDK.CameraInit()` / `MVSDK.CameraUnInit()` 正确配对调用

4. **改造图像采集算子**
   - 构造函数增加 `ICameraManager cameraManager` 注入参数
   - 当 `sourceType == "camera"` 时，通过 `cameraManager` 获取相机实例

#### 5.2.2 传统视觉算法移植

**枚举值分配表**：

| 枚举值 | 枚举名 | 说明 |
|-----:|--------|------|
| 90 | `AkazeFeatureMatch` | AKAZE特征匹配 |
| 91 | `OrbFeatureMatch` | ORB特征匹配 |
| 92 | `GradientShapeMatch` | 梯度形状匹配 |
| 93 | `PyramidShapeMatch` | 金字塔形状匹配 |

> **重要**：从 **90** 开始分配，给现有枚举（最大84）留出扩展空间。严禁与已有枚举值冲突。

**文件结构**：

```
Acme.Product.Infrastructure/Operators/Features/      【新建目录】
├── FeatureMatchOperatorBase.cs                       【新增】特征匹配泛型基类
├── AkazeFeatureMatchOperator.cs                      【新增】
├── OrbFeatureMatchOperator.cs                        【新增】
├── GradientShapeMatchOperator.cs                     【新增】
└── PyramidShapeMatchOperator.cs                      【新增】
```

**移植要点**：

| 清霜V3原始写法 | ClearVision适配写法 |
|---------------|-------------------|
| `Parameters["Threshold"]` | `GetDoubleParam(@operator, "Threshold", 0.7)` |
| `Execute(Mat input)` | `ExecuteCoreAsync(Operator @operator, Dictionary<string, object>? inputs, CancellationToken ct)` |
| `return Result` | `return Task.FromResult(OperatorExecutionOutput.Success(CreateImageOutput(resultMat, data)))` |
| `_logger.Info(...)` | `Logger.LogInformation(...)` |

**核心逻辑保留**：
- `MatchWithSymmetryTest()` 对称测试匹配
- `ComputeHomography()` 单应矩阵计算
- 模板缓存（用 `ConcurrentDictionary<string, (KeyPoint[], Mat)>` 缓存模板特征点）

#### 5.2.3 PLC通信优化

**现状评估**：

ClearVision的 `Acme.PlcComm` 库**已经非常完善**：
- `IPlcClient` 接口拥有 `ReadAsync<T>` / `WriteAsync<T>` 泛型读写、`PingAsync` 心跳检测、`ReconnectPolicy` 重连策略
- `PlcClientFactory` 已支持 `S7://` / `MC://` / `FINS://` 连接字符串
- 三个具体实现 `SiemensS7Client` / `MitsubishiMcClient` / `OmronFinsClient`

**结论**：ClearVision的PLC通信库在接口设计上**优于**清霜V3的 `PlcAdapters`。**不需要引入清霜V3的 `IPlcDevice` 接口**，而是应该保持现有架构不变。

**可从清霜V3借鉴的改进**：

| 改进项 | 来源 | 实施方式 |
|--------|------|---------|
| 串口Modbus RTU | `PlcAdapters.cs` 中的 `ModbusRtuAdapter` | 在Acme.PlcComm中新增 `ModbusRtuClient` 实现 `IPlcClient` |
| 连接字符串扩展 | - | 在 `PlcClientFactory.CreateFromConnectionString` 中增加 `"MODBUSRTU://"` 分支 |

> **重要**：**不要**重构现有的 `IPlcClient` 接口或 `PlcClientFactory`。它们的设计已经足够好。只需要**新增**实现类和工厂方法。

### 5.3 第二阶段：高级功能与智能化

#### 5.3.1 流水线调试预览

**架构设计**：

`FlowExecutionService.cs` 当前已有 `ExecuteFlowSequentialAsync` 和 `ExecuteFlowParallelAsync`，核心方法 `ExecuteOperatorInternalAsync` 已记录每个算子的执行时间。

**改造思路**：在现有执行流程上叠加调试模式，而非重写。

**扩展执行上下文**：

```csharp
// Acme.Product.Core/Services/DebugOptions.cs
public class DebugOptions
{
    /// <summary>设置了断点的算子ID列表</summary>
    public HashSet<Guid> Breakpoints { get; set; } = new();
    /// <summary>是否仅执行到下一个断点</summary>
    public bool StepMode { get; set; } = false;
    /// <summary>保存中间结果的图像格式</summary>
    public string ImageFormat { get; set; } = ".png";
}
```

**后端API**：
- `POST /api/flow/{flowId}/debug/start` — 启动调试
- `POST /api/flow/{flowId}/debug/step` — 单步执行
- `GET /api/flow/{flowId}/debug/preview/{operatorId}` — 获取中间图像

#### 5.3.2 智能检测机制

**核心概念**：

| 机制 | 来源 | 说明 |
|------|------|------|
| 自适应重拍 | `DetectionService.cs` | 根据图像亮度自动调整曝光并重试 |
| 双模态投票 | `DetectionService.cs` | YOLO + 传统算法同时检测，取交集判定 |
| NG重试 | `DetectionService.cs` | 首次NG后自动重拍确认 |

**智能检测服务**：

```csharp
// Acme.Product.Infrastructure/Services/IntelligentDetectionService.cs
public async Task<DetectionResult> ExecuteWithRetryAsync(
    ICameraManager cameraManager,
    string cameraId,
    IFlowExecutionService flowService,
    Flow flow,
    RetryPolicy policy,
    CancellationToken ct)
{
    for (int attempt = 0; attempt <= policy.MaxRetries; attempt++)
    {
        var result = await flowService.ExecuteFlowAsync(flow, ct);
        if (result.IsSuccess && result.Confidence >= policy.MinConfidence)
            return result;

        // 自适应曝光调整
        if (attempt < policy.MaxRetries)
        {
            var camera = await cameraManager.GetOrCreateCameraAsync(cameraId);
            var currentExposure = camera.GetParameters().ExposureTime;
            var brightness = CalculateImageBrightness(result.Image);
            var newExposure = AdjustExposure(currentExposure, brightness);
            await camera.SetExposureTimeAsync(newExposure);
        }
    }
    return DetectionResult.Failed("超过最大重试次数");
}
```

**双模态投票算子**：

- **枚举**: `DualModalVoting = 94`
- **输入端口**：`DLResult` (深度学习结果), `TraditionalResult` (传统算法结果)
- **输出端口**：`FinalResult` (最终判定)
- **参数**：`VotingStrategy` (enum: "Unanimous" / "Majority" / "WeightedAverage")

---

## 六、综合开发路线图

### 6.1 演进路线总览

```
Sprint 1              Sprint 2              Sprint 3              Sprint 4              Sprint 5
[RC+CoW+内存池]     [ForEach IoMode]      [算子全面扩充]        [AI 安全沙盒]         [AI 编排接入]
     固底                 强筋                  扩能               安全围栏               收割
   (2~3 周)             (2~3 周)              (3~4 周)            (1~2 周)              (1~2 周)
```

**各阶段 AI 编排能力变化**：

| 阶段 | 稳定性 | 可覆盖场景 | 安全性 | AI 编排综合成功率 |
|------|--------|-----------|--------|-----------------|
| 现状 | ⚠️ 数小时 OOM | 简单线性流程 | ❌ 无保护 | ~60% |
| Sprint 1 后 | ✅ 无 OOM、无碎片抖动、无数据竞争 | + 多目标类型流转 | ❌ 无保护 | ~68% |
| Sprint 2 后 | ✅ 稳定 | + 多目标遍历（纯计算并行 / 通信串行） | ❌ 无保护 | ~78% |
| Sprint 3 后 | ✅ 稳定 | + 公差/逻辑/OCR/MES 融合 | ❌ 无保护 | ~92% |
| Sprint 4 后 | ✅ 稳定 | 同上 | ✅ Linter + 双向仿真双保险 | ~92%（有安全保障） |
| Sprint 5 后 | ✅ 稳定 | 全场景一键生成 | ✅ 可投产 | ~92%（可投产） |

### 6.2 Sprint 1：内存安全与类型系统重构（2~3 周）

> **目标**: 解决 7×24 长期运行的三大稳定性根基：精确生命周期（引用计数）、并发数据隔离（写时复制）、恒定节拍内存（分桶内存池）。零新功能，专注"把现有的做对"。

#### Task 1.1 — 引用计数 + 写时复制 + 分桶内存池（RC + CoW + Pool）✅ 已完成

**优先级**: P0 — 阻断性  
**预估工时**: 7~10 天

**三层问题，三层方案**：

| 层次 | 问题 | 方案 |
|------|------|------|
| 释放时机 | 统一延迟释放导致内存峰值爆炸 | 引用计数（RC） |
| 数据安全 | Mat 浅拷贝在并发扇出时数据互相污染 | 写时复制（CoW） |
| 分配性能 | CoW 触发 `Clone()` 高频申请大块非托管内存，导致碎片化与帧耗时抖动 | 分桶内存池（Pool） |

**分桶内存池的核心逻辑**：

不同算子产生的图像尺寸不同（原图、ROI 裁剪图、缩放图...），因此不能用单一队列，必须按 `(width, height, channels, type)` 组合分桶管理：

```csharp
public sealed class MatPool : IDisposable
{
    private readonly ConcurrentDictionary<MatSpec, ConcurrentBag<Mat>> _buckets = new();
    private readonly int _maxPerBucket;
    private readonly long _maxTotalBytes;
    private long _currentTotalBytes = 0;
    private bool _disposed = false;

    public static readonly MatPool Shared = new(maxPerBucket: 8, maxTotalGb: 2.0);

    public Mat Rent(int width, int height, MatType type)
    {
        var spec = new MatSpec(width, height, type);
        if (_buckets.TryGetValue(spec, out var bag) && bag.TryTake(out var mat))
        {
            Interlocked.Add(ref _currentTotalBytes, -spec.ByteSize);
            return mat; // 复用已有缓冲块，零 malloc
        }
        return new Mat(height, width, type); // 池中无缓存，新建
    }

    public void Return(Mat mat)
    {
        // 归还到池中，或 Dispose
    }
    
    private readonly record struct MatSpec(int Width, int Height, MatType Type)
    {
        public long ByteSize => (long)Width * Height * Type.Channels * Type.CV_ELEM_SIZE1();
    }
}
```

#### Task 1.2 — 端口类型系统扩展 ✅ 已完成

**优先级**: P1  
**预估工时**: 4~5 天

新增端口类型：`PointList(8)`、`DetectionResult(9)`、`DetectionList(10)`、`CircleData(11)`、`LineData(12)`

**需同步修改**：
- `DeepLearning` 的 `Defects` 端口升级为 `DetectionList(10)`
- `CircleMeasurement` 新增 `Circle(11)` 输出端口
- 前端 `flowCanvas.js` 更新配色与兼容矩阵

### 6.3 Sprint 2：执行引擎并发化改造（2~3 周）

> **目标**: 引入 ForEach 子图机制，以"显式声明 IoMode"替代"一刀切禁止"，在保持业务表达力的同时，通过透明的引擎层调度保护硬件连接。

#### Task 2.1 — ForEach 子图执行机制（IoMode 双模式）✅ 已完成

**优先级**: P0  
**预估工时**: 9~13 天

**核心设计：IoMode 参数显式声明执行策略**

```
ForEach.IoMode = Parallel    → 纯计算子图，Parallel.ForEachAsync 并行执行
                               Linter SAFETY_002（Warning）：子图含通信算子时警告

ForEach.IoMode = Sequential  → 含 I/O 的串行子图，退化为顺序 foreach 执行
                               子图中的通信算子逐条串行执行，保护硬件连接
```

**参数规格**：

| 参数名 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| `IoMode` | enum | `Parallel` | `Parallel`=并行纯计算；`Sequential`=串行含 I/O |
| `MaxParallelism` | int | CPU 核心数 | 并行线程数上限 |
| `OrderResults` | bool | true | 是否按输入顺序重排结果 |
| `FailFast` | bool | false | 是否在第一个子图失败时提前终止 |
| `TimeoutMs` | int | 30000 | 单个子图执行超时毫秒数 |

**Linter SAFETY_002 降级为 Warning**：

```csharp
if (ioMode == "Parallel")
{
    foreach (var op in commOps)
        yield return new LintIssue(
            Code: "SAFETY_002",
            Severity: LintSeverity.Warning,   // ← V4 降级为 Warning（V3 是 Error）
            Message: $"ForEach（IoMode=Parallel）子图中检测到通信算子...",
            Suggestion: "如果业务上需要逐条通信，请将 ForEach.IoMode 改为 Sequential");
}
```

#### Task 2.2 — ArrayIndexer 与 JsonExtractor ✅ 已完成

**优先级**: P1（依赖 Task 1.2）  
**预估工时**: 2 天

- `ArrayIndexer`：从 `DetectionList` 按索引或条件（MaxConfidence / MinArea / MaxArea）提取单个 `DetectionResult`
- `JsonExtractor`：按 JSONPath 从 String 中提取字段值，输出 `Any`、`String`、`Float`、`Found(Boolean)`

### 6.4 Sprint 3：算子全面扩充（3~4 周）

> **目标**: 补齐高频场景算子，手眼标定以独立 UI 向导替代 DAG 算子，新增现代工业通信接口。

| Task | 算子 | 预估工时 | 状态 |
|------|------|----------|------|
| 3.1 | MathOperation（数值计算） | 2 天 | ✅ 已完成 |
| 3.2 | LogicGate（逻辑门） | 1 天 | ✅ 已完成 |
| 3.3 | TypeConvert（类型转换） | 1 天 | ✅ 已完成 |
| 3.4 | 手眼标定向导（独立 UI 模块） | 5~7 天 | ✅ 已完成 |
| 3.5a | HttpRequest | 2 天 | ✅ 已完成 |
| 3.5b | MqttPublish | 2 天 | ✅ 已完成 |
| 3.6a | StringFormat | 1 天 | ✅ 已完成 |
| 3.6b | ImageSave | 0.5 天 | ✅ 已完成 |
| 3.6c | OcrRecognition（PaddleOCRSharp） | 5~7 天 | ✅ 已完成 |
| 3.6d | ImageDiff（图像差异检测） | 3 天 | ✅ 已完成 |
| 3.6e | Statistics / CPK | 2 天 | ✅ 已完成 |

### 6.5 Sprint 4：AI 安全沙盒（1~2 周）

> **目标**: 建立不可绕过的安全隔离层，重点修复仿真模式的"状态孤岛"问题——离线仿真必须能激活异常分支和条件跳转。

#### Task 4.1 — 工程静态检查器（Flow Linter）✅ 已完成

**优先级**: P0  
**预估工时**: 3~4 天

三层检查规则：

**第一层：结构合法性**
- 算子类型合法、端口 ID 存在、类型兼容、无环路

**第二层：语义安全**
```
SAFETY_001（Error）：通信类算子上游必须有 ConditionalBranch 或 ResultJudgment
SAFETY_002（Warning）：ForEach(Parallel) 子图含通信算子时警告，建议改为 Sequential
SAFETY_003（Error）：CoordinateTransform(HandEye) 的 CalibrationFile 不能为空
```

**第三层：参数值合理性**
```
PARAM_001（Error）：CoordinateTransform.PixelSize 超出 (0, 10.0] mm
PARAM_002（Warning）：任意数值参数超出 minValue~maxValue
PARAM_003（Error）：DeepLearning.Confidence 超出 (0, 1]
PARAM_004（Warning）：MathOperation.Divide 且无上游保证 ValueB ≠ 0
```

#### Task 4.2 — 深度双向仿真模式（Deep Bidirectional Dry-Run）✅ 已完成

**优先级**: P1  
**预估工时**: 5~7 天

**V3 的缺陷**：单向拦截导致状态孤岛

V3 的 DryRun 对通信算子统一返回 `{ ValidationPassed=true, DryRun_Intercepted }`。当下游有 `ConditionalBranch` 依赖 PLC 返回的具体状态字时，统一的 Mock 成功状态让仿真永远只能走一条路径。

**V4 方案：Stub Registry（双向数据挡板）**

```csharp
public class DryRunStubRegistry
{
    private readonly Dictionary<StubKey, Queue<StubResponse>> _stubs = new();

    public DryRunStubRegistry Register(
        string deviceAddress,    // 如 "192.168.1.10:502"
        string targetAddress,    // 如 "40001"（Modbus寄存器）
        params StubResponse[] responses)
    {
        var key = new StubKey(deviceAddress, targetAddress);
        var queue = new Queue<StubResponse>(responses);
        _stubs[key] = queue;
        return this;
    }

    public StubResponse GetNextResponse(string deviceAddress, string targetAddress)
    {
        // 返回下一个预设响应，支持响应序列
    }
}

public record StubResponse(
    bool IsSuccess,
    string Payload,               // 原始响应报文
    int DelayMs = 0,              // 模拟网络延迟
    string? ErrorMessage = null)
{
    public static StubResponse DefaultSuccess =>
        new(true, "{\"status\":\"OK\"}", DelayMs: 5);
    public static StubResponse Timeout =>
        new(false, "", DelayMs: 30000, ErrorMessage: "Connection timed out");
}
```

#### Task 4.3 — 前端安全提示层 ✅ 已完成

**预估工时**: 1 天

1. `file` 类型参数的算子：`⚠️` 图标 + 橙色边框
2. 通信算子：红色边框 + "请在仿真通过后再部署"
3. Linter 结果面板：Error 存在时"部署"按钮置灰
4. 仿真通过后绿色横幅，显示分支覆盖率

### 6.6 Sprint 5：AI 编排接入（1~2 周）

**进入 Sprint 5 的 Gate Review 检查清单**：

- [x] Task 1.1：`MatPool.Rent()` 命中率 ≥ 90%，P99 帧耗时 ≤ P50 × 3
- [x] Task 1.2：DetectionList / CircleData 端口类型已上线
- [x] Task 2.1：ForEach IoMode 双模式可用，SAFETY_002 为 Warning 已降级
- [x] Task 3.1~3.3：MathOperation / LogicGate / TypeConvert 可用
- [x] Task 3.4：手眼标定后端 `HandEyeCalibrationService` 已完成
- [x] Task 4.1：FlowLinter 全部三层规则已激活
- [x] Task 4.2：Stub Registry 可用，仿真能通过预设响应激活异常分支
- [x] Task 4.3：前端安全提示层已上线

**AI 接入完整流水线**：

```
用户输入自然语言描述
         ↓
AiPromptBuilder（含最新算子库，ForEach 双模式说明）
         ↓
LLM 生成 JSON（最多重试 2 次）
         ↓
FlowLinter 三层检查
  Error → 追加错误到 Prompt，要求 AI 修正
  通过 → 渲染到画布
         ↓
自动构建默认 StubRegistry（为检测到的通信算子生成默认 Stub）
运行 Deep Bidirectional Dry-Run（5 张测试图 × 多场景 Stub）
         ↓
展示：执行路径、分支覆盖率、通信报文诊断
未覆盖分支 → Warning 提示用户手动配置更多 Stub 场景
         ↓
用户确认，解锁真实部署
```

### 6.7 优先级与工时全量汇总

| 编号 | Sprint | 优先级 | 内容 | 工时 | 状态 |
|------|--------|--------|------|------|------|
| 1.1 | S1 | 🔴 P0 | RC + CoW + **分桶内存池** | **7~10 天** | ✅ 已完成 |
| 1.2 | S1 | 🟠 P1 | 端口类型扩展 | 4~5 天 | ✅ 已完成 |
| 2.1 | S2 | 🔴 P0 | ForEach **IoMode 双模式** + SAFETY_002 降级 | **9~13 天** | ✅ 已完成 |
| 2.2 | S2 | 🟠 P1 | ArrayIndexer / JsonExtractor | 2 天 | ✅ 已完成 |
| 3.1 | S3 | 🟠 P1 | MathOperation | 2 天 | ✅ 已完成 |
| 3.2 | S3 | 🟠 P1 | LogicGate | 1 天 | ✅ 已完成 |
| 3.3 | S3 | 🟠 P1 | TypeConvert | 1 天 | ✅ 已完成 |
| 3.4 | S3 | 🟠 P1 | 手眼标定向导 | 5~7 天 | ✅ 已完成 |
| 3.5a | S3 | 🟡 P2 | HttpRequest | 2 天 | ✅ 已完成 |
| 3.5b | S3 | 🟡 P2 | MqttPublish | 2 天 | ✅ 已完成 |
| 3.6a | S3 | 🟡 P2 | StringFormat | 1 天 | ✅ 已完成 |
| 3.6b | S3 | 🟡 P2 | ImageSave | 0.5 天 | ✅ 已完成 |
| 3.6c | S3 | 🟡 P2 | OcrRecognition | 5~7 天 | ✅ 已完成 |
| 3.6d | S3 | 🟡 P2 | ImageDiff | 3 天 | ✅ 已完成 |
| 3.6e | S3 | 🟢 P3 | Statistics / CPK | 2 天 | ✅ 已完成 |
| 4.1 | S4 | 🔴 P0 | FlowLinter 完整三层规则 | 3~4 天 | ✅ 已完成 |
| 4.2 | S4 | 🟠 P1 | **Deep Bidirectional Dry-Run + Stub Registry** | **5~7 天** | ✅ 已完成 |
| 4.3 | S4 | 🟡 P2 | 前端安全提示层 | 1 天 | ✅ 已完成 |
| AI | S5 | — | AI 编排接入 | 5~7 天 | ✅ 已完成 |

**总计预估工时**: 63~85 天（约 13~17 工作周）

---
## 七、开放待办分类快照归档（2026-03-16）

> 来源文档：`TODO_Open_Categorized_2026-03-06.md`

该文档是一次“开放事项分类快照”，主要作用是把当时的材料分为三类：

| 分类 | 含义 | 当前处理方式 |
|---|---|---|
| `TYPE-A` | 真实未完成 | 转移到当前有效审计或专题文档继续跟踪 |
| `TYPE-B` | 主要已实现，但文档未更新 | 已在本轮汇总中回填吸收 |
| `TYPE-C` | 研究/规划型文档 | 继续保留在 `plans/`、`reference/`、`AlgorithmAudit/` 等目录 |

### 7.1 快照中的保留价值

- 它明确指出 `PLC`、`AI`、`用户系统` 等条线里存在大量“实现已落地但文档未闭环”的 `TYPE-B` 项。
- 它把 `BUG_AUDIT_2026-03-04.md`、`performance_audit_2026-02-28.md`、`SYSTEM_CONFIG_AUDIT_2026-02-27.md` 识别为仍需继续追踪的 `TYPE-A` 项。

### 7.2 当前归档结论

- 该快照文档已完成使命，不再单独保留。
- 其中仍有效的 `TYPE-A` 问题，现在统一以下列文档为准：
  - [CURRENT_BUG_ARCH_AUDIT_2026-03-12](c:/Users/11234/Desktop/ClearVision/docs/CURRENT_BUG_ARCH_AUDIT_2026-03-12.md)
  - [SYSTEM_CONFIG_AUDIT_2026-02-27](c:/Users/11234/Desktop/ClearVision/docs/audits/SYSTEM_CONFIG_AUDIT_2026-02-27.md)
  - `docs/reports/` 下相关专题报告

---


## 附录：原始文档索引

本文档由以下原始规划文档合并而成，原始文件已归档至 `docs/archive/legacy_md_pre_merge_20260316/`：

| 原始文件 | 合并章节 | 主要内容 |
|---------|---------|---------|
| `UserSystem_Plan.md` | 第一章 | 三级角色权限管理系统设计 |
| `AI_Model_Integration_Refactor_Plan_2026-02-28.md` | 第二章 | AI 接入架构重构方案 |
| `PLC_Communication_Audit_Roadmap_2026-02-27.md` | 第三章 | PLC 通信审计与修复路线图 |
| `plans/plan-plc-communication.md` | 第三章 | PLC 通信详细设计规范 |
| `plans/plan-operator-implementation-reconciliation.md` | 第四章 | 算子契约收敛计划 |
| `plans/plan-operator-remaining-work.md` | 第四章 | 算子实现剩余工作 |
| `plans/plan-operator-enhancement.md` | 第四章 | 算子算法增强规划 |
| `plans/plan-v3-migration.md` | 第五章 | 清霜 V3 迁移详细指导 |
| `plans/plan-ocr-native-dependency.md` | 第六章 | OCR 测试环境依赖修复 |
| `roadmaps/roadmap-main.md` | 第六章 | 综合开发路线图 V4 |
| `TODO_Open_Categorized_2026-03-06.md` | 第七章 | 开放待办分类快照与 TYPE-A/B/C 划分 |
| `roadmaps/roadmap-sprint6.md` | 第六章 | Sprint 6 LLM 生态规划 |

---

*文档维护：ClearVision 开发团队*  
*最后更新：2026-03-16（文档合并完成）*