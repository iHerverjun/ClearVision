# ClearVision Bug 审计核查结论 & 修复实施计划

> 核查日期：2026-03-12
> 核查方式：源码静态审查逐项比对

---

## 一、核查结论汇总

**结论：审计报告中列出的全部 9 项问题（3 P1 + 4 P2 + 2 P3）均经源码验证属实。**

| 编号 | 核查结果 | 核查证据 |
|---|---|---|
| CV-01 (P1) | ✅ **属实** | [WebMessageHandler.cs:229](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L229) 确实将 `Guid` 类型的 `OperatorId` 传入 `Enum.Parse<OperatorType>()`，必然抛出 `ArgumentException` |
| CV-02 (P1) | ✅ **属实** | [WebMessageHandler.cs:266-284](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L266-L284) `HandleUpdateFlowCommand` 仅打日志+`Task.CompletedTask`；[WebMessageHandler.cs:329-333](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs#L329-L333) `HandleStopInspectionCommand` 同理 |
| CV-03 (P1) | ✅ **属实** | 端口名不一致：定义 `Payload`(L38) vs 读取 `Message`(L70)；输出 `IsSuccess`(L39) vs 写入 `Success`(L97)；参数 `Qos`(L43) vs 读取 `QoS`(L59)。[PublishAsync](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs#L133) 是 `Task.Delay(10)` 占位 |
| CV-04 (P2) | ✅ **属实** | `clear()` 重复：[L1649](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js#L1649) 和 [L1935](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js#L1935)；`handleContextMenu()` 重复：[L1727](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js#L1727)（支持删除连接线+节点）和 [L1816](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js#L1816)（只弹出节点菜单）。后者覆盖前者，**导致右键删除连接线功能失效** |
| CV-05 (P2) | ✅ **属实** | 后端 `FindAvailablePort(5000, 6000)` [Program.cs:118](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Program.cs#L118)；前端探测仅到 `5005` [httpClient.js:73](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/messaging/httpClient.js#L73) |
| CV-06 (P2) | ✅ **属实** | 默认密码 `admin/admin123` 硬编码 [Program.cs:327](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Program.cs#L327)；登录页明文展示 [login.html:90](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/login.html#L90)；会话纯内存 [AuthService.cs:21](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Application/Services/AuthService.cs#L21) |
| CV-07 (P2) | ✅ **属实** | `EnsureCreated()` [Program.cs:157](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Program.cs#L157)，无 `Migrations/` 目录 |
| CV-08 (P3) | ✅ **属实** | 两套 `getDefaultOperators()`：[app.js:828](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/app.js#L828) 和 [operatorLibrary.js:293](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/operator-library/operatorLibrary.js#L293) |
| CV-09 (P3) | ✅ **属实** | `flowCanvas.js` 2147 行、`settingsView.js` ~2086 行、`app.js` ~1707 行。测试目录中无 `WebMessageHandler` / `MqttPublishOperator` / `FlowCanvas` 专项测试 |

---

## 二、修复实施计划

### 第一阶段：P1 修复（建议紧急处理）

---

#### CV-01：修复 `ExecuteOperatorCommand` 的 Guid→OperatorType 错配

##### 修改文件

###### [MODIFY] [WebMessageHandler.cs](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs)

**方案**：`ExecuteOperatorCommand` 的语义是按 `OperatorId`（节点 ID）执行，应从当前流程上下文中反查节点的类型。

修改 `HandleExecuteOperatorCommand()` 方法（L211-L261）：

```diff
- var op = _operatorFactory.CreateOperator(
-     Enum.Parse<Core.Enums.OperatorType>(command.OperatorId.ToString()),
-     "TempOperator",
-     0, 0);
+ // 从消息中获取算子类型（如果携带），否则从流程上下文反查
+ // 短期方案：在 ExecuteOperatorCommand 消息中新增 OperatorType 字段
+ if (!command.Inputs?.TryGetValue("__operatorType", out var typeObj) == true
+     || !Enum.TryParse<Core.Enums.OperatorType>(typeObj?.ToString(), out var operatorType))
+ {
+     throw new InvalidOperationException(
+         $"无法确定算子类型。OperatorId={command.OperatorId}，请在消息中提供 __operatorType 字段。");
+ }
+ var op = _operatorFactory.CreateOperator(operatorType, "TempOperator", 0, 0);
```

**更彻底的方案**（推荐）：在 `ExecuteOperatorCommand` 消息合同中新增 `OperatorType` 字段：

###### [MODIFY] [WebMessages.cs](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Contracts/Messages/WebMessages.cs)

```diff
 public class ExecuteOperatorCommand : CommandBase
 {
     public Guid OperatorId { get; set; }
+    /// <summary>
+    /// 算子类型（用于创建执行器实例）
+    /// </summary>
+    public string? OperatorType { get; set; }
     public Dictionary<string, object>? Inputs { get; set; }
 }
```

然后 Handler 中优先使用 `command.OperatorType`，回退时抛出明确错误。

---

#### CV-02：处理 WebMessage 旧桥接空实现

##### 修改文件

###### [MODIFY] [WebMessageHandler.cs](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/Handlers/WebMessageHandler.cs)

**方案**（推荐方案 3：显式拒绝"假成功"）：

```diff
 private async Task HandleUpdateFlowCommand(string messageJson)
 {
-    // ... 仅打日志 + Task.CompletedTask ...
+    _logger.LogWarning("[WebMessageHandler] UpdateFlowCommand 已弃用。请使用 HTTP API: PUT /api/projects/{id}/flow");
+    throw new NotSupportedException(
+        "UpdateFlowCommand 已弃用，请改用 HTTP API (PUT /api/projects/{id}/flow)。");
 }

 private async Task HandleStopInspectionCommand()
 {
-    _logger.LogInformation("[WebMessageHandler] 检测已停止");
-    await Task.CompletedTask;
+    _logger.LogWarning("[WebMessageHandler] StopInspectionCommand 已弃用。请使用 HTTP API: POST /api/inspection/stop");
+    throw new NotSupportedException(
+        "StopInspectionCommand 已弃用，请改用 HTTP API (POST /api/inspection/stop)。");
 }
```

> 这样未来任何调用方走旧桥接，会立即看到清晰的错误提示，而不是"看似成功实际无效"。

---

#### CV-03：修正 MqttPublishOperator 端口名/参数名不一致

##### 修改文件

###### [MODIFY] [MqttPublishOperator.cs](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Operators/MqttPublishOperator.cs)

统一端口名和输出键：

```diff
 // 输入读取 (L70)
- if (inputs != null && inputs.TryGetValue("Message", out var msgObj) && msgObj != null)
+ if (inputs != null && inputs.TryGetValue("Payload", out var msgObj) && msgObj != null)

 // 输出字典 (L95-L103)
- { "Success", true },
+ { "IsSuccess", true },

 // 参数读取 (L59)
- var qos = GetIntParam(@operator, "QoS", 0, 0, 2);
+ var qos = GetIntParam(@operator, "Qos", 0, 0, 2);

 // ValidateParameters 中 (L165)
- var qos = GetIntParam(@operator, "QoS", 0);
+ var qos = GetIntParam(@operator, "Qos", 0);
```

同时在类注释和文档中标注 MQTT 发布为**实验性功能**（Experimental），直到 MQTTnet 真正接入。

---

### 第二阶段：P2 修复（建议本迭代收口）

---

#### CV-04：合并 FlowCanvas 重复方法

##### 修改文件

###### [MODIFY] [flowCanvas.js](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/canvas/flowCanvas.js)

1. **合并两个 `handleContextMenu()`**：保留第二版（节点上下文菜单 L1816），并将第一版的"连接线删除"逻辑并入，形成统一分支：
   - 右键连接线 → 确认删除
   - 右键节点 → 弹出上下文菜单（运行/复制/删除/禁用/帮助）
   - 右键空白 → 不操作
2. **删除第一个 `clear()`（L1649-1654）**，保留第二个更完整的版本（L1935-1943，包含 `draggedNode`/`selectedConnection` 重置）

---

#### CV-05：统一前后端端口发现范围

##### 修改文件

###### [MODIFY] [httpClient.js](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/src/core/messaging/httpClient.js)

将探测范围从 `[5000..5005]` 扩大为与后端一致的 `[5000..5020]`（保守范围，避免探测 1000 个端口的性能问题）。同时增加**超时并行探测**：

```diff
- const testPorts = [5000, 5001, 5002, 5003, 5004, 5005];
+ // 与后端 FindAvailablePort(5000, 6000) 保持一致
+ // 实际分配端口通常在前 20 个以内
+ const testPorts = Array.from({length: 20}, (_, i) => 5000 + i);
```

---

#### CV-06：消除默认密码明文展示

##### 修改文件

###### [MODIFY] [login.html](file:///C:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/wwwroot/login.html)

```diff
- <p>默认管理员: admin / admin123</p>
+ <!-- 不再明文展示默认凭据 -->
```

> [!IMPORTANT]
> "首次登录强制改密"和"持久化会话"属于更大的安全改造范围，建议作为独立迭代处理。本次先消除最低限度的安全敞口。

---

#### CV-07：规划数据库迁移链路

短期方案：先不动 `EnsureCreated()`，但建立 EF Core Migrations 基线：

```bash
# 在 Infrastructure 项目下生成初始迁移
dotnet ef migrations add InitialCreate --project src/Acme.Product.Infrastructure --startup-project src/Acme.Product.Desktop
```

然后将 `Program.cs` 中的 `EnsureCreated()` 替换为 `dbContext.Database.Migrate()`。

> [!WARNING]
> 此项变更影响已有用户数据库。需要先验证迁移脚本是否正确匹配现有 schema，再提交。建议单独分支处理。

---

### 第三阶段：P3 重构债务（纳入路线图）

---

#### CV-08：统一前端回退算子目录

- 删除 `app.js` 中的 `getDefaultOperators()`（L828+），统一使用 `operatorLibrary.js` 中的版本
- `app.js:780` 处改为引用 `operatorLibrary` 的方法

#### CV-09：拆分超大文件 & 补充测试

此项属于长期重构，需要独立规划：

- `flowCanvas.js` 拆分为 `flowCanvas.core.js`、`flowCanvas.render.js`、`flowCanvas.interaction.js` 等
- `settingsView.js` 按功能区（通用设置/相机设置/AI 设置）拆分
- 补充 `WebMessageHandler` 单元测试
- 补充 `MqttPublishOperator` 契约测试

---

## 三、验证计划

### 自动化测试

```bash
# 1. 后端构建验证
dotnet build Acme.Product/Acme.Product.sln -c Debug

# 2. 后端单元测试
dotnet test Acme.Product/tests/Acme.Product.Tests/Acme.Product.Tests.csproj -c Debug --no-build

# 3. 桌面测试
dotnet test Acme.Product/tests/Acme.Product.Desktop.Tests/Acme.Product.Desktop.Tests.csproj -c Debug --no-build

# 4. 前端 E2E 测试
cd Acme.Product/tests/Acme.Product.UI.Tests && npm test
```

### 手动验证

1. **CV-01 验证**：构建通过即可初步验证；如有 WebMessage 测试工具，发送 `ExecuteOperatorCommand` 消息观察是否返回正确错误
2. **CV-02 验证**：发送 `UpdateFlowCommand` / `StopInspectionCommand` 消息，应收到 `NotSupportedException` 错误响应
3. **CV-04 验证**：在流程画布中右键空白区域、右键节点、右键连接线，各分支行为符合预期
4. **CV-06 验证**：打开 `login.html` 页面，确认不再显示默认密码
