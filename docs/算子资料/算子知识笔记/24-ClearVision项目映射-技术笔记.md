# ClearVision 项目映射技术笔记

> 这篇笔记的目标，是把前面三篇“通用原理”真正落回 ClearVision。你在面试里不需要像讲教材那样讲 PLC，而是要能回答：`这些原理在你的项目里分别对应什么模块、什么算子、什么链路。`

---

## 1. 一句话先理解这篇笔记

ClearVision 里和工业现场系统观最相关的，不只是通信算子本身，而是：

> `图像采集 + 触发组织 + 业务判定 + 结果回写 + 运行时协同`

把这几件事讲成一条链，你的回答就会更像做过系统，而不是只背过几个协议名词。

---

## 2. 先把系统角色映射到项目模块

## 2.1 现场角色 -> ClearVision 映射表

| 现场角色 | ClearVision 中更接近的模块 / 算子 | 面试里怎么讲 |
|----------|-----------------------------------|--------------|
| **上位机** | `Acme.Product.Desktop`、`FlowExecutionService`、`InspectionRuntimeCoordinator`、`GenerateFlowMessageHandler` | 负责界面、流程编排、运行时组织、结果展示 |
| **相机** | `CameraManager`、`ImageAcquisition` | 负责图像采集和相机绑定 |
| **PLC / 控制器通信** | `MitsubishiMcCommunication`、`ModbusCommunication`、`SiemensS7Communication`、`OmronFinsCommunication`、`TcpCommunication` | 负责读状态、写结果、同步配方或工艺参数 |
| **业务结果门控** | `ResultJudgment`、必要时再接 `ConditionalBranch` | 负责把算法输出收口成现场能消费的 OK/NG 或数值结果 |
| **触发节拍组织** | `TriggerModule`、`ImageAcquisition.triggerMode` | 负责软件触发、定时触发、外部信号触发等节拍组织 |
| **AI 场景生成** | `PromptBuilder`、`AiFlowGenerationService`、`AiFlowValidator`、`DryRunService` | 负责把自然语言需求收敛成结构化流程草案 |

### 2.2 一个很重要的说法

在 ClearVision 里，工业通信并不是项目的唯一主线。  
你更稳的讲法应该是：

> 我的主线仍然是“视觉软件 + LLM 编排 + 模板化收缩”，工业通信和现场系统观是我把这个项目讲得更像真实工业系统的重要补强。

---

## 3. 先看第一个典型链路

## 3.1 链路 A：PLC 触发拍照 -> 视觉检测 -> OK/NG 回写

这是工业视觉里最经典的一条链。

### 面向现场的表达

```text
PLC 发起本次节拍
   ->
相机采图
   ->
上位机做视觉检测
   ->
ResultJudgment 收口业务结果
   ->
通信算子把 OK/NG 或数值结果回写给 PLC
   ->
PLC 决定放行、剔除或报警
```

### 映射到 ClearVision 的表达

```text
[ImageAcquisition]
      ->
[视觉处理链]
      ->
[ResultJudgment]
      ->
[MitsubishiMcCommunication / ModbusCommunication]
```

### 为什么这条链适合你讲

- 它能把视觉和现场控制串起来
- 它不要求你假装自己会 PLC 梯形图
- 它能自然带出 `triggerMode`、`PollingMode`、`OkOutputValue / NgOutputValue` 这些项目里真实存在的概念

---

## 3.2 链路 A 里的两个常见实现版本

### 版本 1：PLC 直接硬件触发相机

```text
PLC -> 外部触发信号 -> 相机
                      |
                      v
              ImageAcquisition(triggerMode=Hardware)
                      |
                      v
                   视觉链
                      |
                      v
               ResultJudgment
                      |
                      v
         MitsubishiMcCommunication / ModbusCommunication
```

#### 这个版本的优点

- 相机采图节拍更稳
- 上位机不容易因为轮询延迟错过拍照时机
- 更适合节拍高的工位

#### 面试里怎么说

> 如果现场强调节拍稳定，我会更倾向让 PLC 通过 IO 直接硬件触发相机，上位机负责检测和回写结果，而不是所有动作都靠软件轮询去追信号。

### 版本 2：PLC 通过通信告诉上位机“可以开始”

```text
MitsubishiMcCommunication / ModbusCommunication 读取触发状态
                    |
                    v
               TriggerModule
                    |
                    v
      ImageAcquisition(triggerMode=Software)
                    |
                    v
                 视觉链
                    |
                    v
              ResultJudgment
                    |
                    v
         通信算子回写结果和完成状态
```

#### 这个版本更适合什么

- 调试和演示
- 节拍要求没那么极致
- 现场以“状态位协调”为主

#### 这个版本要注意什么

- 轮询间隔
- 触发重复消费
- Done / Ack 握手
- 不要把“读到值”说成“时序已经可靠”

---

## 3.3 链路 A 里各算子扮演什么角色

### `ImageAcquisition`

- 负责采图
- 支持 `sourceType=camera`
- 在算子手册里存在 `triggerMode` 概念，可区分软件触发和硬件触发

### `TriggerModule`

- 适合讲“节拍组织、信号整形、触发模式”
- 不要把它讲成图像算法

### `ResultJudgment`

- 负责把上游视觉结果收口成现场更能消费的业务结果
- 比如：
  - 是否 OK
  - 判定值是什么
  - 细节说明是什么

### `MitsubishiMcCommunication / ModbusCommunication`

- 负责把结果写回 PLC
- 也可用于读 Ready / Trigger / 状态码
- 不要只说“它能读写”，要说“它在时序闭环里负责哪一步”

---

## 4. 再看第二个典型链路

## 4.1 链路 B：上位机下发参数 / 配方 -> PLC 执行 -> 上位机读取状态

这条链更能体现“上位机不只是判定结果，还负责工程管理和配方管理”。

### 面向现场的表达

```text
工程师在上位机选择配方
   ->
上位机把配方参数写给 PLC / 控制系统
   ->
PLC 按当前配方驱动设备运行
   ->
上位机持续读取状态、结果或计数
   ->
界面显示当前工位状态和检测结果
```

### 映射到 ClearVision 的表达

```text
[上位机配置界面 / 模板配置]
          ->
[TcpCommunication / ModbusCommunication / MitsubishiMcCommunication]
          ->
[PLC / 自定义控制器]
          ->
[通信算子读取状态]
          ->
[界面显示 / 运行时协调]
```

### 为什么这条链适合补强你的面试

因为它能说明：

- 上位机不只会“算一个结果”
- 还会做配方、状态展示、参数同步
- 这和纯视觉算法岗相比，更接近真实工业软件场景

---

## 4.2 链路 B 里 `TcpCommunication` 怎么讲

很多面试官一听“PLC 通信”，脑子里默认只有 Modbus。  
你如果能自然带出 `TcpCommunication`，会显得系统视角更完整。

### 一个很稳的讲法

> 在 ClearVision 里，我会把 `TcpCommunication` 理解成对接自定义控制系统或上层服务的通用网络接口。它不一定是 PLC 原生协议，但适合传更灵活的业务报文，比如任务号、批次信息、配方摘要或结构化状态。

### 它和 Modbus 的区别怎么讲

- `Modbus` 更像标准化寄存器读写
- `TcpCommunication` 更像自定义报文通道

所以：

- 写简单状态、结果、计数，优先想标准协议
- 传结构化业务消息，自定义 TCP 更自然

---

## 5. ClearVision 项目里哪些细节最适合拿来当证据

## 5.1 `ImageAcquisition.triggerMode`

在算子手册和历史收敛文档里，`triggerMode` 都是一个真实存在的重要概念。  
这很适合你用来回答：

- 软件触发和硬件触发的区别
- 相机触发方式为什么会影响现场节拍

## 5.2 通信算子具备轮询等待语义

像 `MitsubishiMcCommunication` 这类算子已有：

- `PollingMode`
- `PollingCondition`
- `PollingValue`
- `PollingTimeout`
- `PollingInterval`

这意味着你不是在空谈“轮询”，而是项目里真的有这套语义。

## 5.3 `ResultJudgment` 的 OK / NG 输出语义

`ResultJudgment` 里已经有：

- `OkOutputValue`
- `NgOutputValue`

这很适合你解释：

> 为什么业务结果最好先收口，再传给 PLC，而不是让下游自己猜算法原始输出。

## 5.4 `SAFETY_001`

历史文档里明确提出：

```text
通信类算子上游必须有 ConditionalBranch 或 ResultJudgment
```

这条规则特别适合在面试里拿来证明：

> 你对“通信成功不等于业务成功”这件事是有系统意识的。

---

## 6. 面试时怎么把这四个笔记串成一条线

### 6.1 起手先讲角色分工

先说：

> 上位机负责视觉、界面、配方和历史记录；PLC 负责稳定控制和现场动作。

### 6.2 再讲协议选择

再说：

> 如果现场明确厂牌，我优先考虑原生协议；如果是多品牌统一接入，更适合 Modbus；如果是自定义控制系统，则考虑 TCP。

### 6.3 再讲可靠性

再说：

> 真正难点不只是连通，而是触发、采图、判定、回写这条时序怎么稳定闭环。

### 6.4 最后落回项目

最后说：

> 在 ClearVision 里，这套思路能映射到 `ImageAcquisition + TriggerModule + ResultJudgment + 通信算子 + 运行时协调` 这条链上。

---

## 7. 一段可直接口播的项目化回答

> 如果面试官问我 ClearVision 里工业通信和现场系统观是怎么落的，我会这样讲：  
> ClearVision 里我不会把 PLC 通信理解成单独读写几个寄存器，而是把它放在完整视觉闭环里看。上位机侧负责界面、流程编排、视觉算法和结果展示；相机由 `ImageAcquisition` 承接；现场触发可以通过 `triggerMode` 和 `TriggerModule` 组织；视觉链跑完以后，`ResultJudgment` 先把业务结果收口，再通过 `MitsubishiMcCommunication`、`ModbusCommunication` 或 `TcpCommunication` 回写给 PLC 或控制系统。  
>  
> 这样讲的重点不是“我知道几个协议名”，而是我知道这些协议和算子在现场闭环里分别承担什么职责，也知道通信成功不等于业务成功，真正关键的是触发、判定、回写和确认这一整条时序是否可靠。

---

## 8. 一句话总结

> 把工业现场系统观映射回 ClearVision，关键不是背算子目录，而是能把 `采图、触发、判定、回写、运行时协同` 讲成一条完整的项目链路。
