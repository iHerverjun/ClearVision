# ClearVision - 工业视觉检测软件开发任务清单

<!-- DOC_AUDIT_STATUS_START -->
## 文档审计状态（自动更新）
- 审计日期：2026-03-06
- 完成状态：部分完成（历史修复已闭环，发布尾项保留）
- 任务统计：阶段 6 的 P0/P1 问题已闭环；S6-P2 与流程编辑器增强项已迁入 backlog；阶段 7 仅保留最小发布清单
- 判定依据：已完成实现与文档勾选已回填，旧增强项不再挂在历史总表
<!-- DOC_AUDIT_STATUS_END -->

> 基于《.NET 8 + WebView2 桌面应用开发计划：代码结构最佳实践》
> 业务领域：工业视觉检测软件（VisionMaster 简化版）
> 核心特性：算子流水线、图像处理、缺陷检测
> 创建时间：2026-01-31
> 项目名称：ClearVision
> **最后更新时间：2026-03-06**

---

## 项目概述

**ClearVision** 是一款简化版工业视觉检测软件，参考海康 VisionMaster 设计，支持：
- 🔄 **算子流水线** - 可视化图像处理流程编排
- 🔍 **缺陷检测** - 基于机器学习的质量检测
- 📊 **结果分析** - 检测数据统计与报表
- 🎥 **多相机支持** - 工业相机图像采集

### 核心功能模块
1. **项目管理** - 工程文件的创建、保存、加载
2. **算子库** - 12+ 常用图像处理算子
3. **流程编辑器** - 拖拽式算子流程设计
4. **图像显示** - 实时预览与结果标注
5. **相机管理** - 工业相机配置与采集
6. **检测结果** - NG/OK 判定与数据记录

---

## 进度总览

| 阶段 | 状态 | 完成度 |
|------|------|--------|
| 阶段1：项目初始化与架构搭建 | 🟢 已完成 | 100% |
| 阶段2：后端核心实现 | 🟢 已完成 | 100% |
| 阶段3：前端基础架构 | 🟢 已完成 | 100% |
| 阶段4：前后端集成 | 🟢 已完成 | 100% |
| 阶段5：质量保证与优化 | 🟢 已完成 | 100% |
| 阶段6：功能完善与问题修复 | 🟢 已完成 | 100% |
| 阶段7：发布与部署 | 🟡 待最小闭环 | 20% |

---

## ✅ 已实现功能汇总

### 🎯 Sprint 4 已完成功能（2026-02-02）

| ID | 功能模块 | 状态 | 关键实现 |
|----|---------|------|---------|
| S4-001 | **ImageViewerComponent** | ✅ 完成 | 图像加载、缩放平移、缺陷标注 |
| S4-002 | **OperatorLibraryPanel** | ✅ 完成 | 算子分类树、拖拽、搜索过滤 |
| S4-003 | **WebSocket实时通信** | ✅ 完成 | 进度通知、错误同步、图像流推送 |
| S4-004 | **OperatorService** | ✅ 完成 | 算子CRUD、元数据查询 |
| S4-005 | **ImageAcquisitionService** | ✅ 完成 | 文件选择、图像预处理 |
| S4-006 | **端到端流程集成** | ✅ 完成 | app.js主应用集成 |
| S4-007 | **ImageData值对象** | ✅ 完成 | 图像数据封装、ROI管理 |
| S4-008 | **ResultAnalysisService** | ✅ 完成 | 统计报表、数据导出 |
| S4-009 | **集成测试补充** | ✅ 完成 | 22个新测试用例 |

### 🎯 Sprint 5 已完成功能（2026-02-03 ~ 2026-02-04）

| ID | 功能模块 | 状态 | 关键实现 |
|----|---------|------|---------|
| S5-001 | **Serilog结构化日志** | ✅ 完成 | 文件输出、扩展方法、异常丰富 |
| S5-002 | **集成测试完善** | ✅ 完成 | 14个测试用例、边界值测试 |
| S5-003 | **实时数据流推送** | ✅ 完成 | Base64压缩、WebSocket连接管理 |
| S5-004 | **图像处理性能优化** | ✅ 完成 | Mat对象池、避免GC |
| S5-005 | **算子管道并行优化** | ✅ 完成 | 并行输入数据准备 |
| S5-006 | **LRU图像缓存** | ✅ 完成 | 100MB限制、命中率统计 |
| S5-007 | **UI测试框架** | ✅ 完成 | Playwright基础配置 |
| S5-008 | **CI/CD初始化** | ✅ 完成 | GitHub Actions工作流 |
| S5-009 | **异常处理中间件** | ✅ 完成 | 全局捕获、ProblemDetails |
| S5-010 | **代码覆盖率报告** | ✅ 完成 | Coverlet配置、HTML报告 |

### 🔧 近期关键Bug修复（2026-02-04）

| 问题 | 严重程度 | 状态 | 修复内容 |
|------|---------|------|---------|
| **算子拖拽失效** | 🔴 P0 | ✅ 修复 | 事件委托方案，防止TreeView重绘丢失事件 |
| **新建工程对话框位置错误** | 🔴 P0 | ✅ 修复 | CSS类名统一（dialog→cv-modal） |
| **WebView2通讯失效** | 🔴 P0 | ✅ 修复 | 消息格式统一（messageType字段） |
| **工程数据跨工程同步** | 🔴 P0 | ✅ 修复 | 保存逻辑使用正确API（/projects/{id}/flow） |
| **工程保存失效** | 🔴 P0 | ✅ 修复 | 分别调用基本信息和流程数据接口 |
| **CSS缓存问题** | 🟡 P1 | ✅ 修复 | 三重防护：DEBUG清除、动态版本号、强制刷新菜单 |
| **消息类型不一致** | 🟡 P1 | ✅ 修复 | 警告日志使用正确变量 |
| **版本号冲突风险** | 🟡 P1 | ✅ 修复 | 对象展开顺序调整 |
| **CSS选择器缺失** | 🟡 P1 | ✅ 修复 | 添加.viewer-canvas-container定义 |

### 🎨 UI升级已完成

| 功能 | 状态 | 说明 |
|------|------|------|
| **UI 2.0 数字水墨设计** | ✅ 完成 | 黛蓝墨韵主色调、朱砂霓虹强调色、国风科技风格 |
| **亮色/暗色模式切换** | ✅ 完成 | 支持主题持久化、自动切换 |
| **响应式布局** | ✅ 完成 | 支持1440px/1024px/768px断点 |
| **无障碍支持** | ✅ 完成 | 焦点可见、减少动效、高对比度 |
| **CSS缓存解决方案** | ✅ 完成 | DEBUG自动清除、动态版本号、强制刷新功能 |

---

## 📋 各阶段详细进度

### 阶段1：项目初始化与架构搭建 ✅ 100%

#### 1.1 技术栈准备 ✅
- [x] 安装 .NET 8 SDK
- [x] 配置开发环境
- [x] 安装 WebView2 Runtime
- [x] 创建 Git 仓库
- [x] 安装 OpenCvSharp4

#### 1.2 解决方案结构 ✅
- [x] 创建解决方案 `Acme.Product.sln`
- [x] `Acme.Product.Core` - 领域层
- [x] `Acme.Product.Application` - 应用层
- [x] `Acme.Product.Infrastructure` - 基础设施层
- [x] `Acme.Product.Contracts` - 共享契约
- [x] `Acme.Product.Desktop` - 宿主应用
- [x] `Acme.Product.Tests` - 测试项目

#### 1.3 基础配置 ✅
- [x] 配置 `.editorconfig`
- [x] 配置 `.gitignore`
- [x] 初始化 Git 仓库
- [x] 配置代码分析规则
- [x] 创建 README.md

---

### 阶段2：后端核心实现 ✅ 100%

#### 2.1 领域层 ✅
- [x] 基础实体基类（Entity, IAggregateRoot）
- [x] 领域事件基础设施
- [x] 值对象基类（ValueObject）
- [x] 异常体系（9个异常类）
- [x] **Project** - 视觉检测工程
- [x] **Operator** - 算子实体
- [x] **OperatorFlow** - 算子流程
- [x] **ImageData** - 图像数据值对象
- [x] **InspectionResult** - 检测结果
- [x] **Defect** - 缺陷实体
- [x] 12种算子类型枚举
- [x] IInspectionService、IFlowExecutionService、IOperatorFactory

#### 2.2 应用层 ✅
- [x] 配置 MediatR、FluentValidation、AutoMapper
- [x] DTO定义（Project/Operator/Flow/Image/Result）
- [x] ProjectService、OperatorService、InspectionService
- [x] Command和Query处理器

#### 2.3 基础设施层 ✅
- [x] Entity Framework Core 8配置
- [x] 仓储实现（Project/Operator/InspectionResult/ImageCache）
- [x] **9个算子完整实现**：
  - [x] ImageAcquisitionOperator
  - [x] GaussianBlurOperator
  - [x] CannyEdgeOperator
  - [x] ThresholdOperator
  - [x] MorphologyOperator
  - [x] BlobDetectionOperator
  - [x] TemplateMatchOperator
  - [x] FindContoursOperator
  - [x] MeasureDistanceOperator
- [x] ICamera/ICameraManager接口
- [x] MockCamera、FileCamera实现

#### 2.4 表现层 ✅
- [x] Minimal APIs配置
- [x] 17个REST API端点
- [x] 健康检查、工程管理、算子库、图像、检测API

---

### 阶段3：前端基础架构 ✅ 100%

#### 3.1 前端项目结构 ✅
```
wwwroot/
├── index.html
├── src/
│   ├── core/              # 核心基础设施 ✅
│   │   ├── messaging/     # WebMessage/HTTP客户端
│   │   ├── state/         # Signal状态管理
│   │   └── canvas/        # FlowCanvas/ImageCanvas
│   ├── features/          # 功能模块 ✅
│   │   ├── project/       # 工程管理
│   │   ├── flow-editor/   # 流程编辑器
│   │   ├── operator-library/  # 算子库
│   │   ├── image-viewer/  # 图像查看器
│   │   ├── inspection/    # 检测控制
│   │   └── results/       # 结果展示
│   └── shared/            # 共享资源 ✅
│       ├── components/    # UI组件
│       └── styles/        # 主题样式
```

#### 3.2 核心基础设施 ✅
- [x] **WebMessageBridge** - 消息通信封装
- [x] **HttpClient** - HTTP API客户端
- [x] **Signal-based Store** - 响应式状态管理
- [x] **FlowCanvas** - 流程编辑器画布（节点、连线、拖拽、缩放）
- [x] **ImageCanvas** - 图像画布（显示、缩放、标注）

#### 3.3 UI组件系统 ✅
- [x] Button、Input、Dialog、Toast、Loading
- [x] TreeView - 算子分类树
- [x] SplitPanel - 分割面板
- [x] PropertyPanel - 属性面板
- [x] ResultPanel - 结果面板

#### 3.4 核心功能模块 ✅
- [x] **ProjectManager** - 工程管理（列表、搜索、打开、保存、删除）
- [x] **OperatorLibraryPanel** - 算子库（树形、搜索、拖拽）
- [x] **InspectionController** - 检测控制（单次/实时/历史）
- [x] **工程视图** - 工程列表、搜索、卡片展示
- [x] **数显功能** - OK/NG统计、缺陷列表、历史记录、CSV导出

---

### 阶段4：前后端集成 ✅ 100%

#### 4.1 通信机制 ✅
- [x] WebMessage通信（ExecuteOperator/UpdateFlow/ImageAcquired/InspectionCompleted）
- [x] HTTP API通信（工程/算子/图像/结果）
- [x] 实时数据流（图像推送、结果推送、日志推送）

#### 4.2 WebView2集成 ✅
- [x] WebView2Host类（364行完整实现）
- [x] 异步初始化、消息收发、资源释放
- [x] 本地Kestrel服务器（端口动态分配）
- [x] **CSS缓存三重防护方案**：
  - [x] DEBUG模式自动清除缓存
  - [x] 动态版本号生成
  - [x] 强制刷新菜单（Ctrl+F5）

---

### 阶段5：质量保证与优化 ✅ 100%

#### 5.1 测试 ✅
- [x] **85+ 单元测试**（100%通过率）
  - 算子测试（16个）
  - 领域实体测试（23个）
  - 流程执行测试（14个）
  - 图像采集测试（6个）
  - 结果分析测试（11个）
  - Demo工程测试（5个）
- [x] 集成测试框架
- [x] 代码覆盖率报告

#### 5.2 性能优化 ✅
- [x] **Mat对象池** - 避免频繁GC
- [x] **LRU图像缓存** - 100MB限制、命中率统计
- [x] **算子管道并行** - 可并行执行优化
- [x] **CSS缓存优化** - 开发实时生效、生产利用缓存

#### 5.3 可观测性 ✅
- [x] **Serilog结构化日志**
  - 文件输出（按日滚动，保留30天）
  - 控制台输出
  - 扩展方法（算子/检测/流程日志）
- [x] 全局异常处理中间件

#### 5.4 技术债务清理 ✅
- [x] 31项技术债务 → 9项（85%完成）
- [x] 修复8个NotImplementedException
- [x] 修复7处TODO注释
- [x] 修复6处参数验证缺失
- [x] 修复前端3个Canvas内存泄漏

---

## 🚀 下一步开发计划（完善现有功能）

## 🚨 阶段6：问题修复与稳定性提升（进行中）

### 📋 排查发现的问题汇总（2026-02-08）

| 优先级 | 问题数 | 状态 |
|--------|--------|------|
| 🔴 P0 - 严重 | 0 | ✅ 已修复 |
| 🟡 P1 - 中等 | 0 | ✅ 已修复 |
| 🟢 P2 - 轻微 | 0 | ↗ 已迁入 backlog |

#### 🔴 P0 - 阻塞性问题（已修复）

| ID | 任务 | 描述 | 位置 | 预估工时 | 状态 |
|----|------|------|------|---------|------|
| S6-P0-001 | **流程执行取消机制不完整** | `CancelExecutionAsync` 已串联 CTS 传播，状态保留到查询窗口，并补齐取消回归测试 | `FlowExecutionService.cs` / `FlowExecutionServiceTests.cs` | 4h | ✅ 已修复并回填 |

#### 🟡 P1 - 功能完善（已修复）

| ID | 任务 | 描述 | 位置 | 预估工时 | 状态 |
|----|------|------|------|---------|------|
| S6-P1-001 | **ImageAcquisitionService日志不规范** | 已统一使用 `ILogger<ImageAcquisitionService>`，移除 `Debug.WriteLine` | `ImageAcquisitionService.cs` | 1h | ✅ 已修复并回填 |
| S6-P1-002 | **ProjectRepository调试代码残留** | 调试输出已清理，历史状态同步完成 | `ProjectRepository.cs` | 0.5h | ✅ 已修复并回填 |
| S6-P1-003 | **WebMessage消息路由为空实现** | 路由逻辑已实现，历史勾选已补齐 | `WebView2Host.cs` | 2h | ✅ 已修复并回填 |

#### 🟢 已迁出 backlog（不再挂在历史修复总表）

| ID | 任务 | 描述 | 位置 | 预估工时 | 状态 |
|----|------|------|------|---------|------|
| S6-P2-001 | **InvokeRequired模式可优化** | 代码优化类任务，迁入开放 backlog 管理 | `WebView2Host.cs:297-304` | 1h | ↗ backlog |
| S6-P2-002 | **ImageAcquisitionService锁策略优化** | 并发优化类任务，迁入开放 backlog 管理 | `ImageAcquisitionService.cs` | 2h | ↗ backlog |

### 📋 Sprint 6：功能完善与稳定性提升

**时间范围**：2026-02-05 ~ 2026-02-12
**2026-03-06 回填结论**：Sprint 6 的问题修复部分已闭环，历史增强项与编辑器演进项已迁入开放 backlog。

#### 修复执行计划

**Phase 1: P0 严重问题修复（已完成）**
- [x] **S6-P0-001**: 完善流程执行取消机制
  - [x] 添加 `_executionCancellations` 字典
  - [x] 修改 `ExecuteFlowAsync` 支持 `CancellationToken`
  - [x] 修改 `ExecuteOperatorInternalAsync` 传递 `CancellationToken`
  - [x] 完善 `CancelExecutionAsync` 方法
  - [x] 编写回归测试验证取消功能

**Phase 2: P1 中等问题修复（已完成）**
- [x] **S6-P1-001**: 修复 `ImageAcquisitionService` 日志
  - [x] 添加 `ILogger<ImageAcquisitionService>` 依赖
  - [x] 替换 `Debug.WriteLine` 为 `_logger.LogWarning`
  - [x] 日志进入统一日志链路

- [x] **S6-P1-002**: 清理 `ProjectRepository` 调试代码
  - [x] 删除临时调试输出
  - [x] 统一回到正式日志/异常处理路径

- [x] **S6-P1-003**: 实现 WebMessage 消息路由
  - [x] 构造函数接收消息处理器
  - [x] `HandleMessageAsync` 委托给路由处理器
  - [x] 前端消息链路具备响应能力

**Phase 3: 历史优化项（已迁出）**
- [x] **S6-P2-001**: UI 线程调用优化已迁入 backlog
- [x] **S6-P2-002**: 并发锁策略优化已迁入 backlog

#### 流程编辑器增强待办（由历史 Sprint 文档迁出）

以下 5 项不再挂在历史结项文档中，统一迁入 `docs/TODO_Open_Categorized_2026-03-06.md` 的 backlog 明细：

- `S7-005` 撤销/重做
- `S7-006` 复制/粘贴节点
- `S7-007` 框选多节点
- `S7-008` 自动保存
- `S7-009` 工程导入/导出

#### 阶段 7：发布与部署最小闭环（保留项）

- [x] Release 构建基线复核：`Acme.Product.sln` 与 `Acme.OperatorLibrary.sln` Release 构建通过
- [x] 关键稳定性回归：流程执行 / AI 配置隔离 / 桌面测试冒烟通过
- [x] UI E2E 链路完成一轮冒烟验证（登录前置链路已补）
- [x] 发布说明与索引文档同步到最新状态

---

## 📊 项目统计

### 代码规模
- **C# 源代码**：12,389 行（111 文件）
- **JavaScript**：5,535 行（34 文件）
- **CSS**：6 文件
- **单元测试**：85+ 个（100% 通过率）

### 功能模块覆盖
- ✅ 工程管理：100%
- ✅ 算子库：100%
- ✅ 流程编辑器：95%（需完善撤销/重做）
- ✅ 图像查看器：100%
- ✅ 检测控制：100%
- ✅ 结果展示：100%

### 问题统计（2026-03-06）
- 🔴 P0 严重问题：0 个（历史修复已闭环）
- 🟡 P1 中等问题：0 个（历史修复已闭环）
- 🟢 Backlog 优化项：7 个（已迁入开放 backlog，单独排期）

---

## 🎯 关键决策确认

| 决策项 | 选择 | 说明 |
|--------|------|------|
| 业务领域 | 工业视觉检测 | VisionMaster 简化版 |
| 前端框架 | 原生 ES6 模块 | 轻量、无框架依赖 |
| 图像处理 | OpenCvSharp4 | 功能完整、性能优秀 |
| 数据库 | SQLite | 轻量、无需安装 |
| 算子流程图 | 自建 Canvas | 不依赖第三方库 |
| UI 风格 | 数字水墨 | 国风科技风格 |

---

## 📚 相关文档

| 文档名称 | 说明 | 最后更新 |
|----------|------|----------|
| [USER_GUIDE.md](./USER_GUIDE.md) | 用户使用指南 | 2026-02-04 |
| [DEVELOPMENT_RULES.md](./Acme.Product/DEVELOPMENT_RULES.md) | 开发规则与规范 | 2026-01-31 |
| [项目年度总结报告-管理层版.md](./项目年度总结报告-管理层版.md) | 管理层汇报文档 | 2026-02-06 |
| [项目核心创新点总结.md](./项目核心创新点总结.md) | 技术创新总结 | 2026-02-06 |
| [逻辑问题排查与修复计划.md](./逻辑问题排查与修复计划.md) | 问题修复计划 | 2026-02-08 |
| [代码实践指导.md](./代码实践指导.md) | 代码规范指导 | - |

---

## 📝 变更日志

### 2026-02-08 更新 12（文件选择闪退深度修复）
- 🐛 **深度修复文件选择闪退** - 全面重构消息处理机制
  - ✅ **根因1 - 异步上下文冲突**：原 `SafeFireAndForget` + `async void` 导致线程问题
  - ✅ **根因2 - WebView2 COM 冲突**：WebView2 作为 ShowDialog 父窗口导致闪退
  - ✅ **根因3 - 消息解析不一致**：前端发送 `parameterName`，后端解析不匹配
  - ✅ **修复方案1**：移除 `SafeFireAndForget`，改为同步处理文件选择命令
  - ✅ **修复方案2**：使用 `FindForm()` 获取 MainForm 作为对话框父窗口
  - ✅ **修复方案3**：添加 `HandleAsync` 公共方法供 WebView2Host 调用
  - ✅ **修复方案4**：统一消息格式，确保前后端字段一致
  - ✅ **修复方案5**：完善异常处理和日志记录
- 📁 完全重写 WebMessageHandler.cs - 简化为同步+异步混合模式
- 📊 **进度更新**：阶段6 - 45%（深度稳定性修复）

### 2026-02-08 更新 11（文件选择闪退问题修复 - 初次尝试）
- 🐛 **初次修复文件选择对话框闪退**
- ⚠️ 发现深层问题：异步上下文冲突、消息格式不一致
- 📊 **进度更新**：阶段6 - 40%

### 2026-02-08 更新 10（YOLO 多版本支持完成）
- ⭐ **扩展 DeepLearningOperator** - 支持多种 YOLO 版本
  - ✅ **YOLOv8** - [1, 84, 8400] 格式
  - ✅ **YOLOv11** - [1, 84, 8400] 格式（同 v8）
  - ✅ **YOLOv6** - [1, 25200, 85] 格式
  - ✅ **YOLOv5** - [1, 25200, 85] 格式（同 v6）
  - ✅ **自动检测** - 根据输出张量形状自动识别版本
  - ✅ **手动选择** - 支持 Auto/YOLOv5/YOLOv6/YOLOv8/YOLOv11
  - ✅ 不同版本的后处理逻辑（坐标解析、置信度计算）
- 🔧 添加 YoloVersion 枚举定义
- 🔧 添加 DetectYoloVersion 自动检测方法
- 🔧 更新算子元数据（新增 ModelVersion、InputSize 参数）
- 📊 **进度更新**：阶段6 - 35%（YOLO 多版本支持完成）

### 2026-02-08 更新 9（深度学习算子实现完成）
- ⭐ **新增 DeepLearningOperator** - 完整的 ONNX 深度学习推理算子
  - ✅ 支持 ONNX 模型加载和缓存
  - ✅ 图像预处理（Resize、Normalize、HWC→CHW）
  - ✅ ONNX Runtime 推理执行
  - ✅ YOLOv8 格式后处理（解析检测框、类别、置信度）
  - ✅ NMS 非极大值抑制
  - ✅ 结果绘制（缺陷框、标签、统计信息）
  - ✅ 依赖注入自动注册
- 🔧 添加 Microsoft.ML.OnnxRuntime 1.17.0 NuGet 包
- 📝 添加 DeepLearningOperator 单元测试
- 📊 **进度更新**：阶段6 - 30%（深度学习算子完成）

### 2026-02-08 更新 8（逻辑问题排查与修复计划发布）
- 🔧 **P0严重问题**：发现流程执行取消机制不完整（1个）
- 🔧 **P1中等问题**：发现日志使用不规范、调试代码残留、WebMessage路由为空（3个）
- 🔧 **P2轻微问题**：发现代码风格可优化项（2个）
- 📝 **文档**：新增《逻辑问题排查与修复计划.md》
- 📊 **进度更新**：阶段6 - 15%（问题排查完成，修复待进行）

### 2026-02-04 更新 7（Sprint 5 完成 & Bug修复）
- ✅ 修复算子拖拽失效问题（事件委托方案）
- ✅ 修复新建工程对话框样式问题（CSS类名统一）
- ✅ 修复WebView2通讯问题（消息格式修复）
- ✅ 修复工程保存失效问题（正确API调用）
- ✅ 修复工程跨工程同步问题（数据隔离）
- ✅ 实施CSS缓存三重防护方案
- ✅ 修复代码审计发现的P0/P1问题
- ✅ 更新TODO.md项目状态

---

*文档由 AI 助手维护，定期更新项目进度*  
*项目仓库：https://github.com/HerverJun/ClearVision.git*


