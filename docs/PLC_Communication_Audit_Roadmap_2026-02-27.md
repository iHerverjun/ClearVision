# Acme.PlcComm 审计报告与开发路线图（不扩品牌）

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-02
- 完成状态：未完成
- 任务统计：总计 16，已完成 0，未完成 16，待办关键词命中 0
- 判定依据：任务清单尚未开始勾选
<!-- DOC_AUDIT_STATUS_END -->

> 日期：2026-02-27  
> 适用仓库：`ClearVision`  
> 适用模块：`Acme.Product/src/Acme.PlcComm` 及其在算子层的集成  
> 当前决策：**暂不扩展 PLC 品牌支持**，仅聚焦欧姆龙（FINS）、西门子（S7）、三菱（MC）三类协议的稳定性与工程质量提升

---

## 1. 文档目标

本文件用于指导 `Acme.PlcComm` 下一阶段开发，目标如下：

1. 明确当前实现与 HslCommunication、典型商用工业通信平台（Kepware/Softing）之间的差距。
2. 给出基于现有代码事实的风险清单与优先级。
3. 在“不扩品牌”的边界下，制定可执行的修复路线、验收标准和测试策略。

---

## 2. 审计范围与边界

### 2.1 代码范围

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

### 2.2 本次不包含

- 新增协议品牌（如 AB、Delta、Keyence、Modbus 扩展、OPC UA 新驱动等）。
- UI/交互层视觉设计调整。
- 现场 PLC 实机联调（本报告为静态代码审计 + 构建验证 + 外部资料对标）。

---

## 3. 外部对标基准（资料来源）

> 访问日期：2026-02-27  
> 说明：以下用于能力对标，不代表已在本项目中引入这些产品。

1. HslCommunication 社区版仓库：  
   `https://github.com/HslCommunication-Community/HslCommunication-Community`
2. PTC Kepware 产品页：  
   `https://www.ptc.com/en/store/kepware`
3. PTC Intelligent Industrial Connectivity：  
   `https://www.ptc.com/en/products/kepware/intelligent-industrial-connectivity`
4. Softing dataFEED OPC Suite：  
   `https://industrial.softing.com/products/docker/datafeed-opc-suite.html`
5. Softing 安全增强说明（证书/审计相关）：  
   `https://industrial.softing.com/products/docker/news/article/secure-communication-with-datafeed-opc-suite-version-v550.html`

---

## 4. 执行摘要（当前状态判断）

### 4.1 总体判断

`Acme.PlcComm` 已具备“可用的统一抽象 + 三协议接入”的基础框架，但与成熟商用品质相比，当前主要短板在：

1. 协议细节的正确性一致性（端序、长度语义、位读写语义）。
2. 可靠性细节（断连清理、半包处理、重连机制精度）。
3. 诊断/错误模型（错误码可操作性、事件体系落地）。
4. 自动化验证缺口（PLC 核心与三协议算子测试不足）。

### 4.2 当前策略（已确认）

1. **冻结品牌扩展范围**：不新增 PLC 品牌，先把 S7/MC/FINS 做稳。
2. **优先修 P0 正确性问题**：先修会导致错误读写或误判离线的问题。
3. **再补 P1/P2 工程能力**：可靠性、可观测性、测试体系。

---

## 5. 当前实现概览

### 5.1 支持协议

- 西门子 S7（基于 `S7NetPlus` 封装）
- 三菱 MC（原生 3E 帧）
- 欧姆龙 FINS/TCP（原生帧 + 握手）

### 5.2 基础抽象

- 统一客户端接口：`IPlcClient`
- 统一结果对象：`OperateResult` / `OperateResult<T>`
- 基类：`PlcBaseClient`（连接管理、重连、锁、日志）
- 协议地址解析器：S7 / MC / FINS 各自 `AddressParser`
- 上层算子复用连接池与心跳巡检：`PlcCommunicationOperatorBase`

---

## 6. 核心问题清单（按优先级）

> 说明：以下“证据”均来自当前仓库代码文件与行号，供修复时直接定位。

## 6.1 P0（必须优先修复）

| 编号 | 问题 | 证据 | 影响 | 修复方向 |
|---|---|---|---|---|
| P0-1 | `Dispose` 先置 `_disposed=true`，导致 `DisconnectAsync` 直接返回，底层资源清理被跳过 | `Core/PlcBaseClient.cs:131,133,433,434` | 连接/流对象可能残留，导致资源泄漏与后续连接异常 | 调整生命周期顺序：先执行断连清理，再置 `_disposed`；或为 `Dispose` 提供独立清理分支 |
| P0-2 | 算子层值转换统一使用 `BitConverter`（主机端序），与 S7/FINS 大端语义冲突 | `Infrastructure/Operators/PlcCommunicationOperatorBase.cs:268,290` | S7/FINS 读写可能出现值错误（MC 可能偶然正确） | 将算子层转换改为使用客户端 `ByteTransform` 或协议感知转换器 |
| P0-3 | 长度语义多层不一致，存在“字节数再次乘类型长度”的过读风险 | `Infrastructure/Operators/PlcCommunicationOperatorBase.cs:247` + `Infrastructure/Operators/SiemensS7CommunicationOperator.cs:124` + `Siemens/SiemensS7Client.cs:114,220` | 读范围异常、返回数据长度不符合预期 | 统一 `ReadAsync(address, length)` 的长度定义（建议：协议“点数/元素个数”），并全链路对齐 |
| P0-4 | S7 心跳地址 `MW0` 与解析器正则不兼容，可能误判离线 | `Siemens/SiemensS7Client.cs:180` + `Siemens/S7AddressParser.cs:19` | 心跳失败触发上层断连/重连抖动 | 扩展解析器支持 `MWx`/`MBx`/`MDx` 语法，或心跳改用解析器已支持地址 |
| P0-5 | S7 位地址虽解析了 `BitOffset`，但读写路径未使用位偏移 | `Siemens/S7AddressParser.cs:93` + `Siemens/SiemensS7Client.cs:113,155` | `DBXx.y` 等位级语义可能失真 | 在 S7 读写实现中显式处理 bit access（读后掩码/写前读改写） |
| P0-6 | FINS 写入长度固定按 `data.Length/2`，位写入可能变成 0 长度 | `Omron/FinsFrameBuilder.cs:175` + `Omron/OmronFinsClient.cs:174` | 位写入报文非法或行为不可预测 | 按“位/字访问模式”分别计算 length，并在参数层强校验 |

## 6.2 P1（高优先级）

| 编号 | 问题 | 证据 | 影响 | 修复方向 |
|---|---|---|---|---|
| P1-1 | TCP 读取响应未实现“读满循环”，单次 `ReadAsync` 易受半包影响 | `Mitsubishi/MitsubishiMcClient.cs:68,83` + `Omron/OmronFinsClient.cs:120,135` | 偶发报文不完整、随机失败 | 抽象 `ReadExactAsync(stream, buffer, offset, count, ct)` 公共方法 |
| P1-2 | 泛型重连模板 `ExecuteWithReconnectAsync<T>` 存在重复执行业务操作风险 | `Core/PlcBaseClient.cs:337,351` | 写操作可能重复发送，带来副作用 | 重构为“单次执行返回真实结果”的模板，禁止二次执行 |
| P1-3 | `ReconnectPolicy.MaxRetryInterval` 已定义但未实际生效 | `Core/PlcAddress.cs:104` + `Core/PlcBaseClient.cs:298,321` | 重连退避上限不可控 | 退避 delay 应 `Min(计算值, MaxRetryInterval)` |
| P1-4 | `ErrorOccurred` 事件已声明未触发 | `Core/PlcBaseClient.cs:61`（且 build 警告 CS0067） | 可观测性不足，调用方无法订阅错误事件 | 在连接失败、读写失败、重连失败路径统一触发 |
| P1-5 | 工厂创建 URI 参数解析使用 `int.Parse`，缺乏鲁棒性 | `PlcClientFactory.cs:98,99` | 配置输入异常时直接抛错，容错差 | 改为 `TryParse` + 明确错误信息 |

## 6.3 P2（重要但可后置）

| 编号 | 问题 | 证据 | 影响 | 修复方向 |
|---|---|---|---|---|
| P2-1 | `ReadBatchAsync` 当前为串行逐条读取，无聚合优化 | `Core/PlcBaseClient.cs:235` | 高频多点位吞吐受限 | 同协议连续地址聚合读、分组策略、批读 API |
| P2-2 | 字符串读写长度策略简单（写入按 `value.Length`） | `Core/PlcBaseClient.cs:257,259` | 固定长度字符串场景兼容性一般 | 提供 `WriteString(address, value, fixedLength, encoding)` |
| P2-3 | FINS 算子暴露了轮询参数，但执行路径未使用轮询逻辑 | `Infrastructure/Operators/OmronFinsCommunicationOperator.cs:33-37,70-73` | 参数语义与行为不一致 | 要么实现轮询，要么移除参数并更新文档 |
| P2-4 | PLC 相关自动化测试覆盖不足 | 代码检索结果（tests 中未发现 S7/MC/FINS 客户端与算子专项测试） | 回归风险高 | 建立协议单测 + 契约测试 + 故障注入测试 |

---

## 7. 与 HslCommunication/商用品对比（当前阶段）

## 7.1 维度对比

| 维度 | Acme.PlcComm（当前） | HslCommunication（社区生态） | 商用品（Kepware/Softing 等） |
|---|---|---|---|
| 协议覆盖 | 当前聚焦 S7/MC/FINS | 覆盖范围更广（按其仓库描述） | 通常更广，且有持续商业驱动维护 |
| 架构抽象 | 有统一 `IPlcClient` 抽象 | 有成熟 API 与大量现成示例 | 以平台化接入、集中配置与统一管理为主 |
| 协议正确性保障 | 有基础实现，但存在 P0 级语义缺陷 | 生态沉淀更久 | 通常有更强 QA 与长期现场验证 |
| 可靠性机制 | 有连接池/心跳/重连基础 | 生态较成熟 | 冗余、容灾、诊断链路更完整 |
| 可观测与运维 | 日志+结果对象，事件体系未完整落地 | 视接入方式而定 | 常见 Web 管理、安全策略、审计日志 |
| 安全能力 | 当前较薄 | 依赖具体部署方式 | 通常含证书、权限与审计机制 |

## 7.2 关键结论

1. 现阶段不扩品牌是合理策略。当前瓶颈不是“协议数量”，而是“现有三协议的正确性与稳定性”。
2. 在修复 P0/P1 之前，新增品牌只会放大维护复杂度与故障面。
3. 目标应从“能连通”转向“可长期稳定运行、可诊断、可回归”。

---

## 8. 已确定开发方针（执行约束）

1. **品牌冻结**：S7、MC、FINS 之外不新增协议品牌。
2. **先正确性后扩展性**：先完成 P0/P1 再进入增强项。
3. **先测试后重构**：先补最小可行回归测试，再做结构重构，降低回归风险。
4. **协议语义一致化**：统一长度、端序、位访问语义定义并文档化。

---

## 9. 分阶段路线图（建议 3 阶段）

## 阶段 A：Correctness 修复（P0，建议 1-2 周）

### 目标

消除会导致“错误值、误掉线、资源泄漏”的核心风险。

### 任务清单

- [ ] 修复 `Dispose`/`DisconnectAsync` 生命周期顺序问题（P0-1）
- [ ] 统一端序转换路径，移除算子层 `BitConverter` 直转（P0-2）
- [ ] 统一长度语义并修复 S7 过读风险（P0-3）
- [ ] 修复 S7 心跳地址兼容（P0-4）
- [ ] 实现 S7 位读写语义（P0-5）
- [ ] 修复 FINS 位写长度计算（P0-6）

### 阶段验收标准

1. S7/MC/FINS 的 Bool/Word/DWord/Float 在回归测试中一致通过。
2. 心跳线程不再因地址语法问题误触发掉线重连。
3. 连接对象在 `Dispose` 后被正确释放（无残留活动 socket）。

## 阶段 B：Reliability 与可观测性（P1，建议 1 周）

### 任务清单

- [ ] 实现 `ReadExactAsync`，替换所有响应读取点（P1-1）
- [ ] 重构 `ExecuteWithReconnectAsync<T>`，确保单次业务执行（P1-2）
- [ ] 让 `MaxRetryInterval` 生效（P1-3）
- [ ] 打通 `ErrorOccurred` 事件触发链路（P1-4）
- [ ] 工厂 URI 参数改 `TryParse` 并增强错误信息（P1-5）

### 阶段验收标准

1. 人工注入半包/抖动场景下不出现随机“读响应不完整”失败。
2. 写操作回放日志中不存在重复发送同一业务写请求。
3. 错误事件可被上层订阅并看到分类错误码。

## 阶段 C：工程化补齐（P2，建议 1-2 周）

### 任务清单

- [ ] 增加 PLC 协议单元测试（地址解析、帧编解码、类型转换）
- [ ] 增加协议客户端契约测试（含断连、超时、重连）
- [ ] 增加算子级集成测试（S7/MC/FINS 读写流程）
- [ ] 处理 FINS 轮询参数“暴露但未执行”问题（实现或收敛）
- [ ] 评估批读聚合策略（按连续地址分组）

### 阶段验收标准

1. `Acme.PlcComm` 至少具备协议层 + 客户端层 + 算子层三层自动化回归。
2. 关键缺陷（P0/P1）有对应测试用例防回归。
3. 文档与行为一致，无“参数存在但无效”的对外语义偏差。

---

## 10. 测试策略（建议最小落地集）

## 10.1 单元测试（纯内存）

1. 地址解析
   - S7：`DB1.DBX10.3`、`M100`、`MW0`（新增后）
   - MC：`D100`、`X10`、`B1F`
   - FINS：`DM100`、`CIO10.3`、`EM1 100`（如支持格式）
2. 帧编解码
   - MC 3E 读/写请求与响应解析
   - FINS 握手与读写响应解析
3. 端序转换
   - BigEndian/LittleEndian 的 Int16/Int32/Float 双向一致性

## 10.2 集成测试（协议模拟）

1. 使用本地 TCP 模拟服务器回放协议帧。
2. 注入异常场景：半包、断连、超时、错误码响应。
3. 验证重连后首包读写成功率与时延范围。

## 10.3 回归门禁（CI）

1. PR 必须通过 PLC 相关测试集。
2. 若改动 `Core/PlcBaseClient.cs` 或任一协议客户端，必须新增/更新对应测试。

---

## 11. 开发实施注意事项

1. 不要在算子层绕过协议端序模型，避免再次引入 `BitConverter` 端序隐患。
2. 长度定义需要在接口文档中“一次性讲清楚”，避免同名参数不同语义。
3. 对外错误信息保持“可诊断”：错误码 + 协议上下文 + 地址/操作类型。
4. 在重连流程中，避免任何可能导致业务操作“重复执行”的路径。

---

## 12. 里程碑完成定义（Definition of Done）

当满足以下条件时，认为本轮“稳态提升”完成：

1. P0 全部关闭，且有测试覆盖。
2. P1 至少完成 80%，并无已知高风险未处理项。
3. 线上/预发布环境连续运行观察期（建议 >= 7 天）无严重通信事故。
4. 运维可通过日志/事件快速定位通信异常根因。

---

## 13. 当前基线记录

1. 构建验证：`dotnet build Acme.Product/src/Acme.PlcComm/Acme.PlcComm.csproj` 成功。
2. 当前警告：`PlcBaseClient.ErrorOccurred` 未使用（CS0067）。
3. 测试现状：仓库中未发现针对 S7/MC/FINS 客户端与算子的专项自动化测试集。

---

## 14. 建议的下一步执行顺序（可直接开工）

1. 先开一个“P0 修复总任务”分支，按 P0-1 ~ P0-6 顺序提交小步 PR。
2. 每修一个 P0 项，立即补对应测试，确保后续重构不回退。
3. P0 全部完成后，再进入 P1 的可靠性重构与可观测性完善。

---

## 附录 A：关键代码定位索引

- `Acme.Product/src/Acme.PlcComm/Core/PlcBaseClient.cs`
- `Acme.Product/src/Acme.PlcComm/Core/PlcAddress.cs`
- `Acme.Product/src/Acme.PlcComm/Siemens/S7AddressParser.cs`
- `Acme.Product/src/Acme.PlcComm/Siemens/SiemensS7Client.cs`
- `Acme.Product/src/Acme.PlcComm/Mitsubishi/MitsubishiMcClient.cs`
- `Acme.Product/src/Acme.PlcComm/Omron/FinsFrameBuilder.cs`
- `Acme.Product/src/Acme.PlcComm/Omron/OmronFinsClient.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/Operators/PlcCommunicationOperatorBase.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/Operators/SiemensS7CommunicationOperator.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/Operators/MitsubishiMcCommunicationOperator.cs`
- `Acme.Product/src/Acme.Product.Infrastructure/Operators/OmronFinsCommunicationOperator.cs`
