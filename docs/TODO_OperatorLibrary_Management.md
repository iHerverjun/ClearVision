# 算子库深度管理计划

> **作者**：蘅芜君  
> **创建日期**：2026-02-25  
> **最后更新**：2026-02-26  
> **目标**：在不破坏 ClearVision 项目现有功能的前提下，建立算子元数据注册系统、算法实现文档化体系，并生成可被其他项目复用的 NuGet 包。

---

## 决策记录

| 决策项 | 结论 | 日期 |
|--------|------|------|
| NuGet 包策略 | **统一单包** `Acme.OperatorLibrary`，不按分类拆分 | 2026-02-25 |
| 算法文档语言 | **中英双语**（中文为主体，关键术语附英文对照） | 2026-02-25 |
| 算子源码处理 | **不抽离**，通过 MSBuild 文件链接共享编译 | 2026-02-25 |
| 对主项目影响 | **零影响**，所有改动不得改变现有运行时行为 | 2026-02-25 |

---

## 核心原则

1. **零侵入、零影响**：ClearVision 项目继续使用原生算子实现，不引入额外抽象层开销。**任何阶段的改动都不得影响主项目的编译、运行和现有行为**
2. **源码不抽离**：算子源码留在 `Acme.Product.Infrastructure/Operators/` 原位
3. **NuGet 统一单包**：通过独立的打包项目读取算子代码，输出单一 `Acme.OperatorLibrary` NuGet 包供其他项目引用
4. **渐进式改造**：每个阶段可独立交付，不阻塞后续阶段
5. **安全保障**：每个 Phase 完成后必须执行完整的回归验证（编译 + 单元测试 + 前端功能验证），确认主项目无任何回退

---

## Phase 1：算子元数据 Attribute 注册系统

> **目标**：用 C# Attribute 标注替代 `OperatorFactory.cs` 中 3500+ 行的硬编码元数据注册，使每个算子"自描述"。

### 1.1 设计元数据 Attribute 体系

- [x] 在 `Acme.Product.Core` 中新建 `Attributes/` 目录
- [x] 设计并实现以下 Attribute 类：

| Attribute | 标注位置 | 用途 |
|-----------|----------|------|
| `[OperatorMeta]` | 类 | 基本信息：DisplayName、Description、Category、IconName、Keywords |
| `[InputPort]` | 类（可多次） | 输入端口定义：Name、DisplayName、DataType、IsRequired |
| `[OutputPort]` | 类（可多次） | 输出端口定义：Name、DisplayName、DataType |
| `[OperatorParam]` | 类（可多次） | 参数定义：Name、DisplayName、DataType、DefaultValue、Min/Max、Options |
| `[AlgorithmInfo]` | 类（可选） | 算法信息：算法名称、时间复杂度、空间复杂度、核心依赖库 |

- [x] Attribute 示例设计（以 `GaussianBlurOperator` 为例）：

```csharp
[OperatorMeta(
    DisplayName = "高斯模糊",
    Description = "对图像应用高斯滤波，消除噪声",
    Category = "滤波",
    IconName = "filter",
    Keywords = new[] { "高斯", "模糊", "滤波", "降噪", "Gaussian", "Blur" }
)]
[InputPort("Image", "图像", PortDataType.Image, IsRequired = true)]
[OutputPort("Image", "图像", PortDataType.Image)]
[OperatorParam("KernelSize", "核大小", "int", DefaultValue = 5, Min = 1, Max = 31)]
[OperatorParam("SigmaX", "X方向标准差", "double", DefaultValue = 1.0)]
[OperatorParam("SigmaY", "Y方向标准差", "double", DefaultValue = 0.0)]
[AlgorithmInfo(
    Name = "Gaussian Blur (OpenCV)",
    CoreApi = "Cv2.GaussianBlur",
    TimeComplexity = "O(W×H×K²)",
    Dependencies = new[] { "OpenCvSharp" }
)]
public class GaussianBlurOperator : OperatorBase { ... }
```

### 1.2 实现 Attribute 自动发现引擎

- [x] 在 `Acme.Product.Infrastructure/Services/` 新建 `OperatorMetadataScanner.cs`
- [x] 实现反射扫描逻辑：扫描程序集中所有继承 `OperatorBase` 且标注了 `[OperatorMeta]` 的类
- [x] 将扫描结果转化为现有的 `OperatorMetadata` 对象
- [x] 输出完整的 `List<OperatorMetadata>` 供工厂消费

### 1.3 改造 OperatorFactory（兼容模式）

> [!CAUTION]
> 此步骤直接接触主项目运行时代码，必须采用**纯增量**策略，不得删除或修改任何现有硬编码逻辑。

- [x] 修改 `OperatorFactory.InitializeDefaultOperators()` 逻辑：
  - 保留全部现有硬编码注册（**不删除任何一行**）
  - 在硬编码之后追加 Attribute 扫描结果，仅补充硬编码中**缺失**的算子
  - 若 Attribute 与硬编码同时存在，**以硬编码为准**（确保零行为变更）
- [x] 添加仅在 `DEBUG` 模式下的日志：记录每个算子的元数据来源（Attribute / Hardcode）
- [x] 添加启动时校验（仅告警不拦截）：检测 Attribute 定义与硬编码是否存在字段差异

### 1.4 逐步为 101 个算子添加 Attribute

> [!NOTE]
> Attribute 标注是**纯增量操作**——仅在算子类上添加 Attribute，不修改任何方法体或逻辑代码。对主项目运行时**零影响**。

- [x] **批次 1（核心算子 × 20）**：图像采集、预处理、滤波、边缘检测等基础算子
- [x] **批次 2（测量算子 × 15）**：卡尺、宽度、角度、距离、间距测量等
- [x] **批次 3（标定算子 × 8）**：相机标定、N点标定、畸变校正等
- [x] **批次 4（通信算子 × 8）**：Modbus、TCP、串口、PLC、MQTT、HTTP等
- [x] **批次 5（流程控制 × 12）**：条件分支、循环、异常、变量、脚本等
- [x] **批次 6（AI + 后处理 × 10）**：深度学习推理、NMS、Box过滤、缺陷检测等
- [x] **批次 7（剩余算子）**：数据处理、格式转换、结果判定等
- [x] 全部 7 批次标注完成后，验证通过（2026-02-26：118/118 算子已标注，覆盖率 100%）

### 1.5 验证（不清理硬编码）

> [!IMPORTANT]
> 本阶段**不移除** `InitializeDefaultOperators()` 中的硬编码。硬编码将长期保留作为 fallback，仅在未来明确不需要时才考虑移除。

- [x] 运行全部单元测试，确保行为一致（2026-02-26：593 总数，588 通过 / 5 跳过 / 0 失败）
- [x] 验证前端算子库展示无变化
- [x] 编译主项目 Release 模式，确认无警告（2026-02-26：`0 warning / 0 error`）

---

## Phase 2：算子算法实现文档化

> **目标**：为每个算子建立结构化的算法实现文档，形成"算法知识库"。

### 2.1 设计文档模板

- [x] 在项目根目录创建 `docs/operators/` 目录
- [x] 设计统一的算子文档模板 `docs/operators/_TEMPLATE.md`，结构如下：

```markdown
# {算子中文名} / {Operator English Name}

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | XxxOperator |
| 枚举值 (Enum) | OperatorType.Xxx |
| 分类 (Category) | 测量 Measurement / 滤波 Filtering / ... |
| 成熟度 (Maturity) | 🟢稳定 Stable / 🟡实验性 Experimental / 🔴弃用 Deprecated |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：简述核心算法思想、数学公式、信号处理原理等
> English: Brief description of core algorithm, mathematical formulas, signal processing principles.

## 实现策略 / Implementation Strategy
> 中文：为什么选择这种实现方式？与同类方案（如 Halcon、VisionPro）的差异
> English: Why this approach? Comparison with alternatives (e.g., Halcon, VisionPro).

## 核心 API 调用链 / Core API Call Chain
> 列出关键的 OpenCV / 自研 API 调用序列
> List the key OpenCV / custom API call sequence.

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
### 输出 / Outputs

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | O(?) |
| 典型耗时 (Typical Latency) | ~?ms (1920×1080) |
| 内存特征 (Memory Profile) | ? |

## 适用场景 / Use Cases
- ✅ 适合 (Suitable)：...
- ❌ 不适合 (Not suitable)：...

## 已知限制 / Known Limitations
1. ...

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
```

### 2.2 自动生成文档骨架

- [x] 编写文档生成脚本（C# 控制台工具 或 PowerShell），从 Attribute 元数据自动生成每个算子的文档骨架（`scripts/OperatorDocGenerator`）
- [x] 自动填充：基本信息、参数表、端口表
- [x] 人工补充：算法原理、实现策略、性能特征、适用场景

### 2.3 分批编写算法文档

- [x] **优先级 P0（核心测量算子 × 8）**：
  - CaliperTool、WidthMeasurement、AngleMeasurement、MeasureDistance
  - GapMeasurement、CircleMeasurement、ContourMeasurement、LineMeasurement
- [x] **优先级 P0（核心检测算子 × 5）**：
  - TemplateMatch、ShapeMatching、BlobDetection、SubpixelEdgeDetection、ColorDetection
- [x] **优先级 P1（标定与变换 × 6）**：
  - CameraCalibration、NPointCalibration、CoordinateTransform
  - AffineTransform、PerspectiveTransform、Undistort
- [x] **优先级 P1（图像预处理 × 10）**：
  - GaussianBlur、MedianBlur、BilateralFilter、MorphologicalOperation
  - Threshold、AdaptiveThreshold、CannyEdge、HistogramEqualization
  - ClaheEnhancement、ShadingCorrection
- [x] **优先级 P2（AI + 后处理 × 5）**：
  - DeepLearning、BoxNms、BoxFilter、SurfaceDefectDetection、EdgePairDefect
- [x] **优先级 P2（通信与流程 × 20）**：累计 20 个核心算子已补全（首批 12 + 收口 8）

### 2.4 阶段复审（2026-02-26）

- [x] 阶段复审报告已归档：`docs/OperatorLibrary_StageReview_2026-02-26.md`
- [x] 复审结论：Phase 2.3 P0/P1/P2 共 54 份文档补全通过（13 + 16 + 25）
- [x] 清理结果：已补全文档 `TODO/O(?)/~?ms` 占位符清零
- [x] 剩余盘点：当前仍有 64 份文档待补全，进入下一批次计划

### 2.5 剩余文档补全计划（已完成，2026-02-26）

> [!NOTE]
> 本批次已完成收口：64 份文档占位符清零，`docs/operators`（除 `_TEMPLATE.md` 模板外）已全部补全。

- [x] **批次 A（预处理 + 检测 × 18）**：11 预处理 + 7 检测，高存量优先清理
- [x] **批次 B（定位 + 数据处理 × 14）**：7 定位 + 7 数据处理
- [x] **批次 C（匹配定位 + 图像处理 + 逻辑工具 × 10）**：4 匹配定位 + 3 图像处理 + 3 逻辑工具
- [x] **批次 D（剩余散项 × ~22）**：识别、其余各类别各 1-2 个
- [x] 统一 `Category` 命名规范（解决中英文混用问题，如 `预处理` vs `Preprocessing`）
- [x] 回归盘点：占位符剩余 `0`（排除 `_TEMPLATE.md`）

---

## Phase 3：NuGet 包打包项目

> **目标**：创建独立的打包项目，将算子编译为 NuGet 包，不影响 ClearVision 主项目。

### 3.1 创建打包项目

- [x] 在解决方案中新建 `Acme.OperatorLibrary/` 项目（Class Library）
- [x] 项目结构设计：

```
Acme.OperatorLibrary/
├── Acme.OperatorLibrary.csproj       # 打包配置
├── Acme.OperatorLibrary.Core/        # 共享抽象（轻量接口、Attribute、枚举）
│   ├── Attributes/                   # 复用 Phase 1 的 Attribute 定义
│   ├── Interfaces/                   # IOperatorExecutor 等接口
│   └── Enums/                        # OperatorType 枚举
└── Acme.OperatorLibrary.Operators/   # 算子实现包
    └── (通过项目引用或文件链接关联源码)
```

### 3.2 源码共享策略（不复制、不抽离）

- [x] 用 MSBuild 的 `<Compile Include="..." Link="..." />` 文件链接方式，将 `Acme.Product.Infrastructure/Operators/` 中的源码链接到打包项目中
- [ ] 或使用 `Directory.Build.props` 实现共享编译
- [x] 确保 ClearVision 主项目和打包项目编译同一份源码
- [x] 示例 `.csproj` 配置：

```xml
<ItemGroup>
  <!-- 通过文件链接共享算子源码 -->
  <Compile Include="..\Acme.Product\src\Acme.Product.Infrastructure\Operators\*.cs"
           Link="Operators\%(Filename)%(Extension)"
           Exclude="**\ImageWrapper.cs;**\OperatorBase.cs" />
</ItemGroup>
```

### 3.3 处理依赖差异

- [x] 分析算子对 ClearVision 项目特有类型的依赖：
  - `OperatorBase`（基类） → 在 NuGet 包中提供轻量替代基类
  - `ImageWrapper`（内存管理） → 在 NuGet 包中提供简化版本
  - `Operator` / `OperatorExecutionOutput`（实体） → 在 Core 包中定义接口
  - `OperatorMetadata` / `PortDefinition`（元数据） → 在 Core 包中定义
- [x] 为 NuGet 包创建适配层/抽象层，使算子代码无需修改即可在两个上下文编译
- [x] 使用 `#if` 条件编译或接口隔离有分歧的依赖

> 交付物（2026-02-26）：
> - 依赖分析脚本：`Acme.OperatorLibrary/analyze-deps.ps1`
> - 分析报告：`Acme.OperatorLibrary/analysis/dependency-report.md`、`Acme.OperatorLibrary/analysis/dependency-report.json`
> - 抽象与适配层：`Acme.OperatorLibrary/src/Acme.OperatorLibrary.Abstractions/`

### 3.4 NuGet 统一单包配置

- [x] 配置 `.csproj` 的 NuGet 打包属性（**统一单包 `Acme.OperatorLibrary`**）：

```xml
<PropertyGroup>
  <PackageId>Acme.OperatorLibrary</PackageId>
  <Version>1.0.0</Version>
  <Authors>蘅芜君</Authors>
  <Description>工业视觉算子库 - 包含 100+ 图像处理、测量、标定、通信算子 / Industrial Vision Operator Library</Description>
  <PackageTags>opencv;machine-vision;operator;image-processing;measurement;calibration</PackageTags>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
</PropertyGroup>
```

- [x] 在包内通过命名空间区分模块：
  - `Acme.OperatorLibrary.ImageProcessing` — 图像处理算子
  - `Acme.OperatorLibrary.Measurement` — 测量算子
  - `Acme.OperatorLibrary.Calibration` — 标定算子
  - `Acme.OperatorLibrary.Communication` — 通信算子
  - `Acme.OperatorLibrary.FlowControl` — 流程控制算子
  - `Acme.OperatorLibrary.AI` — AI/深度学习算子
  - 已通过模块索引入口完成命名空间分层，算子实现源码保持原命名空间以确保主项目零行为变更
- [x] 编写包的 `README.md`（中英双语），包含快速使用示例

### 3.5 本地 NuGet 源配置

- [x] 在解决方案中配置本地 NuGet 源（`nuget.config`）
- [x] 编写打包脚本：`dotnet pack` → 输出到 `./nupkg/`
- [x] 编写集成测试：在独立测试项目中引用 NuGet 包，验证算子可正常实例化和执行

### 3.6 主项目隔离验证

> [!CAUTION]
> NuGet 打包项目**不得**加入 ClearVision 主项目的构建依赖链。确保 `dotnet build` 主项目时不会触发 NuGet 打包。

- [x] 确认 `Acme.OperatorLibrary.csproj` 不在主解决方案的默认构建配置中（或放在独立 `.sln` 中）
- [x] 验证主项目 `dotnet build` / `dotnet run` 行为完全不变
- [x] 验证主项目的 CI/CD 流水线不受打包项目影响

---

## Phase 4：算子目录自动生成

> **目标**：从 Attribute 元数据自动生成机器可读和人类可读的算子目录。

### 4.1 JSON 目录生成

- [x] 编写工具/脚本，扫描所有算子 Attribute，生成 `docs/operators/catalog.json`
- [x] 格式示例：

```json
{
  "generatedAt": "2026-02-25T15:00:00+08:00",
  "totalCount": 101,
  "categories": {
    "滤波": { "count": 8, "operators": [...] },
    "测量": { "count": 15, "operators": [...] }
  },
  "operators": [
    {
      "id": "GaussianBlur",
      "type": 1,
      "displayName": "高斯模糊",
      "category": "滤波",
      "algorithm": "Gaussian Blur (OpenCV)",
      "inputPorts": [...],
      "outputPorts": [...],
      "parameters": [...],
      "docPath": "docs/operators/GaussianBlur.md"
    }
  ]
}
```

### 4.2 Markdown 目录生成

- [x] 自动生成 `docs/operators/CATALOG.md`，包含：
  - 按分类分组的算子列表（含端口数、参数数）
  - 各分类统计图表
  - 链接到每个算子的详细文档

### 4.3 CI 集成

- [x] 在 CI/CD 流水线中加入目录生成步骤
- [x] 提交前钩子（可选）：检测算子变更时自动重新生成目录

---

## Phase 5：质量与维护体系

> **目标**：建立算子质量评分和持续维护机制。

### 5.1 算子质量评分

- [x] 为每个算子定义质量维度：
  - 文档完整性（Attribute 标注 + 算法文档）
  - 测试覆盖率
  - 参数校验完整性（`ValidateParameters` 实现质量）
  - 错误处理健壮性
- [x] 在 `catalog.json` 中为每个算子输出质量评分

> 交付物（2026-02-26）：
> - 生成器质量评分实现：`scripts/OperatorDocGenerator/Program.cs`
> - 输出字段：`catalog.json -> operators[*].quality`
> - 评分可视化：`CATALOG.md` 增加 `质量评分 / Quality Score` 与每行 `质量 (Q)` 列

### 5.2 算子分类标签体系

- [x] 在 `[OperatorMeta]` 的 `Category` 之外增加 `Tags` 字段
- [x] 定义标签维度：
  - 按功能域：测量 / 检测 / 定位 / 标定 / 通信 / 流程 / AI
  - 按算法类型：基于OpenCV / 自研 / 第三方SDK
  - 按成熟度：🟢稳定 / 🟡实验 / 🔴弃用

> 交付物（2026-02-26）：
> - 元数据字段扩展：`OperatorMetaAttribute.Tags`、`OperatorMetadata.Tags`
> - 扫描与比对链路：`OperatorMetadataScanner` / `OperatorFactoryMetadataMerge`
> - 目录输出：`catalog.json -> operators[*].tags`（按“功能域 + 算法类型 + 成熟度”统一输出）

### 5.3 变更追踪

- [x] 在 `[OperatorMeta]` 中增加 `Version` 字段（semver）
- [x] 算子源码变更时要求同步更新版本号
- [x] 生成 CHANGELOG：从算子版本历史自动汇总

> 交付物（2026-02-26）：
> - 元数据字段扩展：`OperatorMetaAttribute.Version`、`OperatorMetadata.Version`
> - 扫描与比对链路：`OperatorMetadataScanner` / `OperatorFactoryMetadataMerge` 已纳入 `Version`
> - 目录输出：`catalog.json -> operators[*].version`
> - 版本历史与变更汇总：`docs/operators/version-history.json`、`docs/operators/CHANGELOG.md`
> - 版本门禁：`OperatorDocGenerator --enforce-version-bump`（检测“源码变更但版本未升级”并失败）

---

## 实施优先级总览

```
Phase 1（元数据 Attribute）──┐
                            ├── 可并行 ── Phase 2（算法文档化）
Phase 3（NuGet 打包）────────┘
                    ↓
             Phase 4（目录生成）
                    ↓
             Phase 5（质量体系）
```

| 阶段 | 预估工作量 | 优先级 | 前置依赖 | 当前状态 |
|------|-----------|--------|----------|----------|
| Phase 1 | 3-5 天 | 🔴 最高 | 无 | ✅ 已完成（118/118 算子标注） |
| Phase 2 | 5-8 天（持续） | 🔴 最高 | 无（可与 Phase 1 并行） | ✅ 已完成（118/118 文档补全，100%） |
| Phase 3 | 2-3 天 | 🟡 高 | Phase 1（Attribute + Core 抽象） | ✅ 已完成（3.2 为备选 `Directory.Build.props` 方案，未采用不影响交付） |
| Phase 4 | 1-2 天 | 🟡 中 | Phase 1 | ✅ 已完成（编目 + CI 目录生成 + 可选 pre-commit 自动刷新） |
| Phase 5 | 2-3 天 | 🟢 低 | Phase 1 + Phase 2 | ✅ 已完成（5.1 质量评分 + 5.2 标签体系 + 5.3 版本追踪） |

---

## 风险与注意事项

> [!CAUTION]
> **主项目运行安全**（最高优先级）：所有 Phase 的改动必须满足以下安全红线：
> - Phase 1 的 Attribute 标注是**纯增量注解**，不修改任何方法体
> - Phase 1.3 的工厂改造采用**硬编码优先 + Attribute 补充**策略，不删不改现有逻辑
> - Phase 3 的打包项目是**完全独立项目**，不加入主项目构建链
> - 每个 Phase 结束必须通过完整回归测试（编译 + 全量单元测试 + 前端验证）

> [!CAUTION]
> **NuGet 包依赖隔离**：算子代码深度依赖 `ImageWrapper`（引用计数 + MatPool）、`OperatorBase`（生命周期管理）等 ClearVision 特有基础设施。NuGet 包必须提供等价的轻量替代实现，否则外部项目无法使用。

> [!IMPORTANT]
> **Attribute 的 Options 支持**：`[OperatorParam]` 需要支持枚举参数的 Options 列表定义，这是当前元数据结构中最复杂的部分，需仔细设计。

> [!NOTE]
> **Phase 2 文档可渐进式完成**：算法文档不需要一次性全部完成。建议从 P0 核心测量和检测算子开始，每次实现新功能时同步补充文档。
