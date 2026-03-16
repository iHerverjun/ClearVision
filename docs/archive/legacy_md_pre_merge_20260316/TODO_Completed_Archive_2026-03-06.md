# TODO 已完成归档（2026-03-06）

## 归档目的

将“时间较早、已实际完成、但原文状态不一致”的 TODO 文档统一归档，便于后续只追踪真实待办。

归档策略：

1. 保留原文件，不删除，确保可追溯。
2. 本文件只记录“已完成归档”的结论与证据索引。
3. 证据以代码文件存在、测试通过、构建通过为准。

---

## A. Phase 1 能力补齐（已完成归档）

### 来源文档

- `docs/completed_phases/TODO_Phase1_DevGuide.md`
- `docs/completed_phases/TODO_Phase1_Fix.md`

### 归档结论

- 归档状态：`已完成（归档）`

### 完成依据（节选）

- Phase1 涉及的核心算子实现已存在：
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/GeometricFittingOperator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/RoiManagerOperator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/ShapeMatchingOperator.cs`
  - `Acme.Product/src/Acme.Product.Infrastructure/Operators/SubpixelEdgeDetectionOperator.cs`
- 对应测试文件已存在：
  - `Acme.Product/tests/Acme.Product.Tests/Operators/GeometricFittingOperatorTests.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Operators/RoiManagerOperatorTests.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Operators/ShapeMatchingOperatorTests.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Operators/SubpixelEdgeDetectionOperatorTests.cs`

---

## B. Phase 2 测试基础设施（已完成归档）

### 来源文档

- `docs/completed_phases/TODO_Phase2_DevGuide.md`

### 归档结论

- 归档状态：`已完成（归档）`

### 完成依据（节选）

- 文档列出的关键缺失测试已补齐：
  - `MedianBlurOperatorTests.cs`
  - `BilateralFilterOperatorTests.cs`
  - `CoordinateTransformOperatorTests.cs`
  - `ModbusCommunicationOperatorTests.cs`
  - `TcpCommunicationOperatorTests.cs`
  - `DatabaseWriteOperatorTests.cs`
  - `Integration/BasicFlowIntegrationTests.cs`
  - `Integration/ColorDetectionIntegrationTests.cs`
- 全量测试（2026-03-06）：`658` 通过 / `5` 跳过 / `0` 失败。

---

## C. Phase 3 算法与遗留修复（已完成归档）

### 来源文档

- `docs/completed_phases/TODO_Phase3_DevGuide.md`

### 归档结论

- 归档状态：`已完成（归档）`

### 完成依据（节选）

- `CreateProjectRequest` 类型在 `ProjectDto.cs` 中存在，构建基线正常。
- `CameraCalibrationOperator` 已存在并支持文档中提到的关键参数：
  - `Mode`
  - `ImageFolder`
  - `CalibrationOutputPath`
- `CodeRecognitionOperator` 已实现并可参与全量测试。

---

## D. Phase 4 生产就绪增强（已完成归档）

### 来源文档

- `docs/completed_phases/TODO_Phase4_DevGuide.md`

### 归档结论

- 归档状态：`已完成（归档）`

### 完成依据（节选）

- 连接池/重连相关实现已在位：
  - `PlcCommunicationOperatorBase.cs`
  - `ModbusCommunicationOperator.cs`
  - `TcpCommunicationOperator.cs`
- 协议层健壮性代码在位：
  - `Acme.PlcComm/Core/PlcBaseClient.cs`（`ReadExactAsync`、重连策略上限）
  - `Acme.PlcComm/Siemens/S7AddressParser.cs`（支持 `MW`/`MB`/`MD` 风格）
  - `Acme.PlcComm/Siemens/SiemensS7Client.cs`（位读写与 `MW0` 心跳路径）
  - `Acme.PlcComm/Omron/FinsFrameBuilder.cs`（位写长度语义分支）
- PLC 子集测试（2026-03-06）：`18` 通过 / `0` 失败。

---

## E. Phase 5 前端增强与端到端（已完成归档）

### 来源文档

- `docs/completed_phases/TODO_Phase5_DevGuide.md`

### 归档结论

- 归档状态：`已完成（归档）`

### 完成依据（节选）

- 前端属性面板存在 `file` 参数支持：
  - `Acme.Product/src/Acme.Product.Desktop/wwwroot/src/features/flow-editor/propertyPanel.js`（`case 'file'`）
  - `PickFileCommand` 调用链路已在位。
- 流程集成测试在位：
  - `Acme.Product/tests/Acme.Product.Tests/Integration/BasicFlowIntegrationTests.cs`
  - `Acme.Product/tests/Acme.Product.Tests/Integration/ColorDetectionIntegrationTests.cs`
- 算子图标与前端映射已扩展：
  - `app.js`
  - `features/operator-library/operatorLibrary.js`

---

## F. 算子库深度管理（已完成归档）

### 来源文档

- `docs/TODO_OperatorLibrary_Management.md`

### 归档结论

- 归档状态：`已完成（归档）`
- 文档自身统计：`72/72` 已完成。

### 完成依据（节选）

- `docs/operators` 文档体系已建立并维护。
- `Acme.OperatorLibrary` 打包与模块目录存在：
  - `Acme.OperatorLibrary/src/Acme.OperatorLibrary.Modules/OperatorModuleCatalog.cs`

---

## G. 算法深度审计计划（已完成归档）

### 来源文档

- `docs/TODO_OperatorLibrary_AlgorithmAudit.md`

### 归档结论

- 归档状态：`已完成（归档）`

### 完成依据（节选）

- 文档中 7 批审计与勘误均标注完成：
  - `docs/AlgorithmAudit/Batch1_Preprocessing.md`
  - `docs/AlgorithmAudit/Batch2_EdgeDetection.md`
  - `docs/AlgorithmAudit/Batch3_Matching.md`
  - `docs/AlgorithmAudit/Batch4_Measurement.md`
  - `docs/AlgorithmAudit/Batch5_Calibration.md`
  - `docs/AlgorithmAudit/Batch6_DeepLearning.md`
  - `docs/AlgorithmAudit/Batch7_General.md`
  - `docs/AlgorithmAudit/Errata_CrossVerification.md`
- 与当前代码基线一致性较高（多项历史问题已闭环）。

---

## 全局验证记录（归档统一证据）

- `dotnet build Acme.Product/Acme.Product.sln -c Release`
  - 成功，`0 warning / 0 error`
- `dotnet test Acme.Product/tests/Acme.Product.Tests/Acme.Product.Tests.csproj -c Release`
  - 通过 `658`，跳过 `5`，失败 `0`

---

## 备注

本归档文件完成后，原 TODO 文档可继续保留为“历史开发指导”，后续建议仅在一个主索引中维护“当前开放待办”。

