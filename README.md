<div align="center">

# 🔬 ClearVision

**高性能工业视觉检测平台**

基于 .NET 8 · OpenCvSharp · ONNX Runtime · WebView2

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![OpenCV](https://img.shields.io/badge/OpenCV-4.9-5C3EE8?style=flat-square&logo=opencv)](https://opencv.org/)
[![ONNX](https://img.shields.io/badge/ONNX-Runtime-005CED?style=flat-square)](https://onnxruntime.ai/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D4?style=flat-square&logo=windows)](https://www.microsoft.com/windows)

[快速开始](#-快速开始) · [功能特性](#-功能特性) · [算子一览](#-算子能力一览46个) · [架构设计](#-架构设计) · [部署指南](DEPLOYMENT_GUIDE.md)

</div>

---

## 📖 项目简介

ClearVision 是一个现代化的**工业视觉检测平台**，提供从图像采集、预处理、检测分析到结果输出的**完整流程编排能力**。用户可以通过直观的拖拽式界面，零代码搭建复杂的视觉检测流程，适用于工业质检、缺陷检测、尺寸测量、条码识别等场景。

### ✨ 核心亮点

| 特性 | 说明 |
|:---:|------|
| 🧩 **46 个内置算子** | 覆盖图像处理、检测测量、识别标定、工业通信全流程 |
| 🖱️ **拖拽式流程编排** | 零代码搭建检测流程，所见即所得 |
| 🤖 **深度学习推理** | 集成 ONNX Runtime，支持 YOLO 系列模型和自定义标签 |
| 🏭 **工业通信** | 内置 Modbus TCP、TCP/IP Socket、串口、数据库连接池 |
| 📊 **结果可视化** | 实时检测结果展示、趋势分析、历史查询与导出 |
| 🏗️ **插件式架构** | 基于 DDD 洋葱架构，扩展新算子仅需实现接口即可 |

---

## 🚀 快速开始

### 环境要求

- **操作系统**：Windows 10/11 (x64)
- **SDK**：[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- **IDE**：Visual Studio 2022 / VS Code / Rider

### 构建与运行

```bash
# 1. 克隆仓库
git clone https://github.com/HerverJun/ClearVision.git
cd ClearVision

# 2. 编译项目
cd Acme.Product
dotnet build

# 3. 运行应用
dotnet run --project src/Acme.Product.Desktop

# 4. 运行测试
dotnet test
```

### 发布部署

```bash
# 使用发布脚本一键打包（自包含单文件 + ZIP）
cd Acme.Product/scripts
.\publish.bat
# 输出: publish/ClearVision-1.0.0-win-x64.zip
```

> 详细的发布方式（ZIP / MSIX / CI/CD）请参考 [部署指南](DEPLOYMENT_GUIDE.md)。

---

## 🧩 功能特性

### 流程编辑器

- **拖拽式画布**：从算子库拖拽算子到画布，通过端口连线构建检测流程
- **撤销/重做**：`Ctrl+Z` / `Ctrl+Y` 支持完整的操作历史
- **复制/粘贴**：快速复制算子节点
- **框选与多选**：Shift/Ctrl + 点击或拖拽框选
- **自动保存**：每 5 分钟自动备份当前工程

### 检测引擎

- **异步流执行**：基于拓扑排序的算子调度，支持并发执行
- **超时保护**：30 秒全局检测超时，防止死锁
- **取消机制**：支持随时取消正在执行的检测流程
- **结构化日志**：完整的执行链路追踪

### 结果管理

- **实时结果面板**：OK/NG 状态高亮、缺陷标注、置信度展示
- **历史查询**：支持按状态、时间范围筛选，分页浏览
- **趋势分析**：最近 100 次检测的良率趋势图
- **数据导出**：支持 CSV / JSON 格式导出

### 工程管理

- **多工程支持**：创建、切换、删除工程
- **导入/导出**：`.cvproj.json` 格式的工程文件互传
- **搜索功能**：快速定位目标工程

---

## 📦 算子能力一览（46个）

### 图像处理（14个）

| 算子 | 功能 | 关键参数 |
|------|------|----------|
| `ImageAcquisition` | 图像采集（文件/相机） | 源类型、文件路径 |
| `GaussianBlur` | 高斯模糊降噪 | 核大小、Sigma |
| `MedianBlur` | 中值滤波 | 核大小 |
| `BilateralFilter` | 双边滤波 | 直径、SigmaColor |
| `Threshold` | 全局/自适应二值化 | 阈值、类型 |
| `CannyEdge` | Canny 边缘检测 | 低阈值、高阈值 |
| `SubPixelEdge` | 亚像素边缘检测 | 精度 |
| `ImageResize` | 图像缩放 | 宽、高、插值方法 |
| `ImageCrop` | 图像裁剪 | ROI 区域 |
| `ImageRotate` | 图像旋转 | 角度、中心点 |
| `PerspectiveTransform` | 透视变换 | 四点映射 |
| `Morphology` | 形态学操作 | 操作类型、核大小 |
| `ColorConvert` | 颜色空间转换 | 转换模式 |
| `HistogramEqualize` | 直方图均衡化 | — |

### 检测与测量（14个）

| 算子 | 功能 | 关键参数 |
|------|------|----------|
| `BlobDetection` | Blob 分析 | 面积范围、圆度 |
| `FindContours` | 轮廓检测 | 检索模式、近似方法 |
| `TemplateMatch` | 模板匹配 | 模板图像、匹配方法 |
| `ShapeMatch` | 形状匹配 | 参考形状、相似度 |
| `MeasureDistance` | 距离测量 | 起点、终点 |
| `CircleMeasurement` | 圆测量 | 圆心、半径范围 |
| `LineMeasurement` | 直线测量 | 起点、终点 |
| `ContourMeasurement` | 轮廓测量 | 测量模式 |
| `AngleMeasurement` | 角度测量 | 三点定义 |
| `GeometricTolerance` | 几何公差 | 公差类型、上下限 |
| `GeometricFit` | 几何拟合 | 拟合类型 |
| `ColorDetection` | 颜色检测 | HSV 范围 |
| `ROIManagement` | ROI 区域管理 | 形状、位置 |
| `ResultOutput` | 结果输出 | 输出格式 |

### 识别（2个）

| 算子 | 功能 | 关键参数 |
|------|------|----------|
| `CodeRecognition` | 一维码/二维码识别 | 码制类型、多码模式 |
| `DeepLearning` | YOLO 深度学习推理 | ONNX 模型路径、置信度、自定义标签 |

### 标定（3个）

| 算子 | 功能 | 关键参数 |
|------|------|----------|
| `CameraCalibration` | 相机标定 | 棋盘格尺寸、单图/文件夹 |
| `Undistort` | 畸变校正 | 相机矩阵、畸变系数 |
| `CoordinateTransform` | 坐标变换 | 变换矩阵 |

### 工业通信（4个）

| 算子 | 功能 | 关键参数 |
|------|------|----------|
| `ModbusCommunication` | Modbus TCP/RTU 通信 | IP、端口、寄存器地址（连接池） |
| `TcpCommunication` | TCP/IP Socket 通信 | IP、端口（连接池） |
| `SerialCommunication` | 串口通信 RS-232/485 | 端口、波特率 |
| `DatabaseWrite` | 数据库写入 | 连接字符串（SQLite/SQL Server/MySQL） |

### 流程控制（2个）

| 算子 | 功能 | 关键参数 |
|------|------|----------|
| `ConditionalBranch` | 条件分支 | 条件表达式、True/False 路径 |
| `ResultOutput` | 结果输出汇总 | OK/NG 判定逻辑 |

---

## 🏗️ 架构设计

### 分层架构（DDD 洋葱模型）

```
Acme.Product/
├── src/
│   ├── Acme.Product.Core/              # 🔵 领域核心层
│   │   ├── Entities/                   #    领域实体（Project, OperatorFlow）
│   │   ├── Operators/                  #    算子接口（IOperatorExecutor）
│   │   ├── Services/                   #    领域服务接口
│   │   ├── ValueObjects/               #    值对象
│   │   └── Events/                     #    领域事件
│   │
│   ├── Acme.Product.Application/       # 🟢 应用服务层
│   │   ├── DTOs/                       #    数据传输对象
│   │   └── Services/                   #    应用服务（流程编排、检测调度）
│   │
│   ├── Acme.Product.Contracts/         # 🟡 契约层
│   │   └── Messages/                   #    WebView2 消息定义
│   │
│   ├── Acme.Product.Infrastructure/    # 🟠 基础设施层
│   │   ├── Operators/                  #    46 个算子实现
│   │   ├── Services/                   #    服务实现（EF Core, 文件存储）
│   │   └── Persistence/               #    数据持久化（SQLite）
│   │
│   ├── Acme.Product.Desktop/           # 🔴 桌面宿主层
│   │   ├── wwwroot/                    #    前端资源（HTML/JS/CSS）
│   │   │   ├── src/
│   │   │   │   ├── core/              #    核心模块（Canvas, Router）
│   │   │   │   ├── features/          #    功能模块（Project, Image, Results）
│   │   │   │   └── shared/            #    共享样式与组件
│   │   │   └── index.html
│   │   └── Program.cs                  #    WinForms + WebView2 入口
│   │
│   └── Acme.Product.Desktop.Package/   # 📦 MSIX 打包项目
│
├── tests/
│   └── Acme.Product.Tests/             # ✅ 单元测试 + 集成测试
│
└── docs/
    └── completed_phases/               # 📚 各阶段开发文档归档
```

### 技术选型

| 层次 | 技术 | 用途 |
|------|------|------|
| **运行时** | .NET 8.0, C# 12 | 后端核心 |
| **图像处理** | OpenCvSharp 4.9 | 传统视觉算法 |
| **深度学习** | ONNX Runtime | YOLO 模型推理 |
| **前端** | HTML5, JavaScript, CSS3 | Web UI |
| **宿主** | WinForms + WebView2 | 桌面应用容器 |
| **数据库** | Entity Framework Core + SQLite | 数据持久化 |
| **通信** | NModbus, System.IO.Ports | 工业协议 |
| **测试** | xUnit, FluentAssertions | 自动化测试 |
| **DI** | Microsoft.Extensions.DependencyInjection | 依赖注入 |

### 核心流程

```
用户拖拽构建流程
        │
        ▼
┌──────────────────┐     WebView2      ┌──────────────────┐
│   前端 (JS/HTML)  │ ◄───消息通信───► │  后端 (.NET 8)    │
│                  │                    │                  │
│  · 流程画布       │                    │  · 流程解析       │
│  · 属性面板       │                    │  · 拓扑排序       │
│  · 结果展示       │                    │  · 算子调度       │
│  · ROI 交互      │                    │  · 图像处理       │
└──────────────────┘                    └──────────────────┘
                                               │
                                               ▼
                                    ┌──────────────────┐
                                    │   算子执行链       │
                                    │                  │
                                    │  图像采集         │
                                    │    ↓              │
                                    │  预处理           │
                                    │    ↓              │
                                    │  检测/测量        │
                                    │    ↓              │
                                    │  通信/输出        │
                                    └──────────────────┘
```

---

## ⌨️ 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+S` | 保存工程 |
| `Ctrl+Z` / `Ctrl+Y` | 撤销 / 重做 |
| `Ctrl+C` / `Ctrl+V` | 复制 / 粘贴节点 |
| `Ctrl+A` | 全选节点 |
| `Delete` | 删除选中节点 |
| `F5` | 运行检测 |
| `Escape` | 取消选择 |

---

## 📊 项目统计

| 指标 | 数据 |
|------|------|
| **核心源代码** | ~355 个文件, ~96,000 行 |
| **C# 后端** | 174 文件, ~19,000 行 |
| **JavaScript 前端** | 95 文件, ~38,000 行 |
| **CSS 样式** | 45 文件, ~16,000 行 |
| **HTML 页面** | 25 文件, ~5,000 行 |
| **测试代码** | xUnit 单元测试 + 集成测试 |

---

## 🛣️ 路线图

### ✅ V1.0 — 已完成

- [x] **Phase 1-2**：核心架构 + 基础算子（图像处理、检测测量）
- [x] **Phase 3**：算法深度提升（颜色检测、串口通信、深度学习自定义标签）
- [x] **Phase 4**：生产就绪性（Modbus/TCP 连接池、超时保护）
- [x] **Phase 5**：前端 UI 增强（算子图标扩展、颜色检测集成测试）
- [x] **Phase 6**：代码质量打磨（NuGet 升级、端到端集成测试）

### 🚧 V2.0 — 规划中

- [ ] **Phase 7**：硬件生态集成（海康/Basler 工业相机 SDK、IO 卡）
- [ ] **Phase 8**：脚本与扩展性（C# 脚本算子、Python 脚本算子）
- [ ] **Phase 9**：高性能计算（GPU 加速 TensorRT/CUDA、SIMD 优化）
- [ ] **Phase 10**：产品化发行（安装包、加密授权、多语言、权限管理）

---

## 📄 文档导航

| 文档 | 说明 |
|------|------|
| [用户指南](USER_GUIDE.md) | 安装使用、功能详解、快捷键、故障排除 |
| [部署指南](DEPLOYMENT_GUIDE.md) | ZIP / MSIX / CI/CD 三种发布方式详解 |
| [开发规则](Acme.Product/DEVELOPMENT_RULES.md) | 代码规范、提交规范、文档同步规则 |
| [演进路线图](Project_Roadmap_v2.0.md) | V1.0 评估 + V2.0 规划 |

---

## 🤝 贡献

欢迎通过 [Issues](https://github.com/HerverJun/ClearVision/issues) 提交问题或建议。

---

## 📃 许可证

本项目基于 [MIT License](LICENSE) 开源。

Copyright © 2026 ClearVision. All rights reserved.
