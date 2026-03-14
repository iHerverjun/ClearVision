# 去畸变 / Undistort

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `UndistortOperator` |
| 枚举值 (Enum) | `OperatorType.Undistort` |
| 分类 (Category) | Calibration |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子根据相机标定结果中的内参矩阵 `CameraMatrix` 和畸变系数 `DistCoeffs`，调用 OpenCV 的 `Cv2.Undistort(...)` 对输入图像做镜头畸变校正。

当前实现并不在算子内部重新估计新相机矩阵，也不做鱼眼模型专门处理，而是直接使用提供的标定数据执行标准 pinhole 模型去畸变。

> English: The operator directly uses the provided calibration matrix and distortion coefficients to run OpenCV undistortion on the input image.

## 实现策略 / Implementation Strategy
当前实现重点放在**标定数据解析兼容性**上：

- **标定数据来源双通道**：优先读取输入端口 `CalibrationData`，若不存在则退回参数 `CalibrationFile`。
- **相机矩阵双格式兼容**：`CameraMatrix` 同时支持扁平一维数组长度 `9`，也支持标准 `3×3` 二维数组。
- **畸变系数扁平化**：`DistCoeffs` 既支持扁平数组，也支持嵌套数组；源码会统一拉平成一维 `double[]`。
- **失败即明确报错**：如果没有标定数据、没有 `CameraMatrix` 或 JSON 结构不合法，算子会直接返回框架级失败。
- **执行路径简洁**：解析成功后直接构造 OpenCV `Mat`，调用 `Cv2.Undistort(...)` 并返回结果图。

> English: The implementation is intentionally simple in computation and more careful in parsing calibration JSON from different upstream formats.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `TryResolveCalibrationData(...)`
   - 输入端口 `CalibrationData`
   - 或参数 `CalibrationFile`
3. `TryParseCalibrationData(...)`
   - `TryParseCameraMatrix(...)`
   - `TryParseDistCoeffs(...)`
4. 构造 `cameraMat` / `distMat`
5. `Cv2.Undistort(src, dst, cameraMat, distMat)`
6. `CreateImageOutput(dst, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `CalibrationFile` | `file` | `""` | 文件路径 | 当输入端口没有提供 `CalibrationData` 时，从该 JSON 文件读取标定数据。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Input Image | `Image` | Yes | 待去畸变图像。 |
| `CalibrationData` | Calibration Data | `String` | No | 标定 JSON 字符串。若提供，将优先于 `CalibrationFile` 使用。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Undistorted Image | `Image` | 去畸变后的图像结果。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `Applied` | `Boolean` | 当前实现是否已执行去畸变。成功路径中固定为 `true`。 |
| `Message` | `String` | 成功说明文字。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 主要由 `Cv2.Undistort(...)` 决定，通常近似随图像像素数线性增长。 |
| 典型耗时 (Typical Latency) | 仓库中未提供固定 benchmark；主要取决于图像分辨率和 OpenCV 去畸变开销。 |
| 内存特征 (Memory Profile) | 需要分配输出图 `dst`，以及相机矩阵/畸变系数的临时 `Mat`。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：使用 `CameraCalibration` 生成标定数据后，对现场图像做标准镜头去畸变。
- **适合 (Suitable)**：在尺寸测量、坐标换算、拼接等流程前先统一几何畸变。
- **适合 (Suitable)**：上游标定 JSON 可能存在一维/二维矩阵格式差异的场景。
- **不适合 (Not Suitable)**：需要鱼眼模型、广角特殊模型或更复杂重映射控制的任务。
- **不适合 (Not Suitable)**：缺少可靠 `CameraMatrix` 的输入。
- **不适合 (Not Suitable)**：希望同时优化视场裁剪、新相机矩阵或 ROI 的高级畸变校正流程。

## 已知限制 / Known Limitations
1. 当前实现直接调用 `Cv2.Undistort(...)`，没有暴露 `alpha`、`newCameraMatrix`、ROI 裁剪或重映射缓存等高级控制参数。
2. 虽然会解析 `ImageWidth` / `ImageHeight` 之外的 JSON 内容，但去畸变时并不会校验当前图像尺寸是否与标定时尺寸一致。
3. 当前实现支持标准 pinhole 模型参数，不是鱼眼专用去畸变算子。
4. 参数校验 `ValidateParameters(...)` 当前直接返回有效，更多合法性检查发生在执行阶段。
5. 若 `DistCoeffs` 缺失，源码会按空数组处理并继续执行；这在工程上有兼容性，但也意味着上游缺参时不一定会显式失败。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充标定 JSON 兼容格式、数据来源优先级和当前去畸变能力边界 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

