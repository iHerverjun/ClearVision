# 深度学习 / DeepLearning

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `DeepLearningOperator` |
| 枚举值 (Enum) | `OperatorType.DeepLearning` |
| 分类 (Category) | AI检测 |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
> 中文：基于 ONNX Runtime 执行 YOLO 推理，先进行 letterbox 预处理与张量归一化，再按输出张量形状适配 YOLOv5/v6 或 YOLOv8/v11 后处理，最终通过 NMS 输出目标框。
> English: Runs YOLO inference via ONNX Runtime with letterbox preprocessing, version-aware postprocessing (v5/v6 or v8/v11), and NMS to produce final detections.

## 实现策略 / Implementation Strategy
> 中文：采用模型会话缓存（含 GPU 配置维度）减少重复加载；支持 `ModelVersion=Auto` 自动识别输出格式；支持按类别过滤与标签文件覆盖，输出 `DetectionList` 供下游标准化使用。
> English: Uses cached inference sessions (including GPU settings), auto-detects model output layout, supports class filtering/custom labels, and emits normalized `DetectionList` outputs.

## 核心 API 调用链 / Core API Call Chain
- `LoadModel`：`InferenceSession` 缓存 + CUDA 优先（可回退 CPU）
- `PreprocessImage`：`Cv2.Resize` + letterbox padding + CHW/RGB tensor
- `session.Run`（ONNX 推理）
- `PostprocessYoloV5V6` / `PostprocessYoloV8V11`（版本化解码）
- `ApplyNMS`（同类框 IoU 抑制）
- `DrawResults`：`Cv2.Rectangle` / `Cv2.PutText`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ModelPath` | `file` | "" | - | - |
| `Confidence` | `double` | 0.5 | [0, 1] | - |
| `ModelVersion` | `enum` | Auto | - | - |
| `InputSize` | `int` | 640 | [320, 1280] | - |
| `TargetClasses` | `string` | "" | - | 检测目标类别（逗号分隔，如 person,car），为空则检测所有类别 |
| `LabelFile` | `file` | "" | - | 自定义标签文件路径（每行一个标签），为空则使用COCO 80类或自动查找模型目录下的labels.txt |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | - |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | - |
| `Defects` | 缺陷列表 | `DetectionList` | - |
| `DefectCount` | 缺陷数量 | `Integer` | - |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 后处理近似 `O(A*C + N^2)`（`A` anchor 数，`C` 类别数，`N` 候选框数） |
| 典型耗时 (Typical Latency) | CPU 约 `20-220 ms`；GPU 约 `5-60 ms`（取决于模型规模与输入尺寸） |
| 内存特征 (Memory Profile) | 常驻模型会话 + 输入/输出张量 + 候选框列表，内存占用与模型大小强相关 |

## 适用场景 / Use Cases
- 适合 (Suitable)：通用目标/缺陷检测、需要多模型版本兼容的产线 AI 检测链路。
- 不适合 (Not Suitable)：超低时延硬实时场景（未做批推理与算子级流水化）或无可用模型文件场景。

## 已知限制 / Known Limitations
1. `ModelPath` 必须可访问且与输出格式匹配，否则推理或后处理会失败。
2. 当前 NMS 为算子内逐类贪心实现，候选框很多时后处理开销明显增加。
3. 代码读取了 `UseGpu/GpuDeviceId` 等参数，但参数元数据未完整暴露在文档参数表中。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 0.1.0 | 2026-02-26 | 自动生成文档骨架 / Generated skeleton |

| 0.2.0 | 2026-02-26 | 完成 Phase 2.3 P2 文档补全 / Completed Phase 2.3 P2 documentation enrichment |