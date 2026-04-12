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
当前实现是一个基于 ONNX Runtime 的 YOLO 推理算子，支持：

- `YOLOv5`
- `YOLOv6`
- `YOLOv8`
- `YOLOv11`
- `Auto` 自动检测版本

整体流程可概括为：

1. 对输入图像做保持宽高比的 letterbox 预处理；
2. 将 BGR 图像转换为模型期望的 RGB `CHW` 张量；
3. 调用 ONNX Runtime 执行推理；
4. 根据输出张量形状自动判断 YOLO 版本；
5. 走对应的后处理分支解析检测框；
6. 仅对**同类别框**执行 NMS（IoU 阈值写死为 `0.45`）；
7. 根据 `DetectionMode` 把检测结果解释为“缺陷列表”或“目标列表”。

当前预处理中的 letterbox 细节为：

- 缩放时保持原始宽高比；
- 填充底色使用 `Scalar(114,114,114)`；
- 输出张量形状为 `[1, 3, InputSize, InputSize]`。

> English: The operator runs YOLO models via ONNX Runtime, performs aspect-ratio-preserving letterbox preprocessing, auto-detects YOLO output format when requested, then applies version-specific postprocessing and same-class NMS.

## 实现策略 / Implementation Strategy
这不是“单纯加载模型然后推理”的最小实现，源码里有几项很关键的工程策略：

- **模型缓存**：使用静态 `ConcurrentDictionary<string, InferenceSession>` 缓存模型会话，缓存键包含 `modelPath + gpu/cpu + deviceId`。
- **并发安全加载**：通过 `SemaphoreSlim` 避免同一模型在并发流程下被重复加载。
- **LRU 驱逐**：模型缓存数量上限为 `3`，超出后会按访问顺序驱逐最久未使用模型。
- **GPU 支持**：代码里实际读取 `UseGpu` 和 `GpuDeviceId` 参数，并尝试启用 CUDA；若 GPU 初始化失败会回退到 CPU。
- **标签加载优先级**：`LabelFile` > 模型目录下 `labels.txt` > 默认 COCO80 标签。
- **输出模式切换**：在 `DetectionMode=Object` 时输出 `Objects/ObjectCount`；默认 `Defect` 模式输出 `Defects/DefectCount`。

> English: The implementation includes practical production concerns such as model caching, concurrency-safe loading, limited-size LRU eviction, optional GPU acceleration, and flexible label loading.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs)`
2. `GetStringParam / GetIntParam / GetFloatParam / GetBoolParam`
3. `LoadLabels(labelFile, modelPath)`
4. `LoadModel(modelPath, useGpu, gpuDeviceId)`
   - `SessionOptions`
   - `AppendExecutionProvider_CUDA(...)`（可用时）
   - `InferenceSession(...)`
5. `PreprocessImage(src, inputSize)`
   - `Cv2.Resize(...)`
   - letterbox 填充
   - BGR → RGB CHW
6. `RunInference(session, inputTensor)`
   - `NamedOnnxValue.CreateFromTensor(...)`
7. `DetectYoloVersion(outputTensor)`（当 `ModelVersion=Auto`）
8. `PostprocessYoloV5V6(...)` 或 `PostprocessYoloV8V11(...)`
9. `ApplyNMS(detections, 0.45f)`
10. `DrawResults(src, detections)`
11. `CreateImageOutput(outputImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `ModelPath` | `file` | `""` | 文件路径 | ONNX 模型路径。为空或不存在都会直接执行失败。 |
| `Confidence` | `double` | `0.5` | `[0.0, 1.0]` | 置信度阈值。后处理时低于该阈值的候选会被过滤。 |
| `ModelVersion` | `enum` | `"Auto"` | `Auto` / `YOLOv5` / `YOLOv6` / `YOLOv8` / `YOLOv11` | YOLO 版本。`Auto` 时根据输出张量维度自动判断。 |
| `InputSize` | `int` | `640` | `[320, 1280]` | 模型输入尺寸，影响预处理和推理成本。 |
| `TargetClasses` | `string` | `""` | 逗号分隔类别字符串 | 只保留指定类别；为空表示不过滤类别。 |
| `LabelFile` | `file` | `""` | 文件路径 | 自定义标签文件路径，优先级高于自动发现和默认 COCO 标签。 |
| `DetectionMode` | `enum` | `"Defect"` | `Defect` / `Object` | 输出语义模式：检出目标视为缺陷，或视为正常目标。 |

### 源码隐含参数 / Runtime-Used But Undeclared Parameters
以下参数在源码中被实际读取，但未通过 `OperatorParam` 对外声明：

| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `UseGpu` | `bool` | `true` | `true` / `false` | 是否尝试启用 GPU 推理。 |
| `GpuDeviceId` | `int` | `0` | `[0, 15]` | GPU 设备编号。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | 输入图像 | `Image` | Yes | 待推理图像。 |

### 输出 / Declared Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | 结果图像 | `Image` | 检测结果图，会绘制框、标签和统计文字。 |
| `Defects` | 缺陷列表 | `DetectionList` | 缺陷检测模式下使用。 |
| `DefectCount` | 缺陷数量 | `Integer` | 缺陷检测模式下使用。 |
| `Objects` | 目标列表 | `DetectionList` | 目标检测模式下使用。 |
| `ObjectCount` | 目标数量 | `Integer` | 目标检测模式下使用。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `DetectionList` | `DetectionList` | 统一的检测结果列表字段，无论缺陷模式还是目标模式都会带出。 |
| `Defects` | `DetectionList` | `DetectionMode=Defect` 时输出。 |
| `DefectCount` | `Integer` | `DetectionMode=Defect` 时输出。 |
| `Objects` | `DetectionList` | `DetectionMode=Object` 时输出。 |
| `ObjectCount` | `Integer` | `DetectionMode=Object` 时输出。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 主要由模型推理复杂度主导，预处理和后处理成本次之。 |
| 典型耗时 (Typical Latency) | 与模型大小、`InputSize`、CPU/GPU 环境、标签数和检测框数量强相关；命中模型缓存时会明显快于首次加载。 |
| 内存特征 (Memory Profile) | 除图像和输出图外，还包含输入张量、输出张量、静态模型缓存以及检测结果列表。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：外观复杂、规则算法难以稳定覆盖的检测、识别和缺陷筛查任务。
- **适合 (Suitable)**：需要同一算子支持多种 YOLO 版本和标签文件切换的流程。
- **适合 (Suitable)**：对模型切换频繁但希望复用推理会话的工程场景。
- **不适合 (Not Suitable)**：模型文件、标签文件和版本配置不明确的流程。
- **不适合 (Not Suitable)**：把 `DetectionMode` 只当展示选项，而不理解其会改变输出字段集合的场景。
- **不适合 (Not Suitable)**：对严格实时性和显存可预测性要求很高，但又频繁切换大量模型的场景。

## 已知限制 / Known Limitations
1. `UseGpu` 和 `GpuDeviceId` 在源码中实际生效，但未通过元数据声明；如果只看参数面板，容易误以为不支持 GPU 开关。
2. `ModelVersion=Auto` 的版本识别基于输出张量维度启发式判断，适合常见 YOLO 导出格式，但不保证覆盖所有非标准模型。
3. `DetectionMode` 会改变运行时实际输出字段：对象模式输出 `Objects/ObjectCount`，缺陷模式输出 `Defects/DefectCount`；集成时不能假定两组字段总是同时存在。
4. 当前 NMS 的 IoU 阈值固定为 `0.45`，且只对同类别框抑制，没有暴露为可调参数。
5. 文档审计勘误已确认此前关于本算子“ArrayPool 张量踩踏”和“ONNX 张量泄漏”的两个严重结论属于误报；但这不代表模型推理的性能和稳定性可脱离现场环境单独保证。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充 YOLO 版本判别、模型缓存/GPU 隐含参数、输出模式切换与预处理细节 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

