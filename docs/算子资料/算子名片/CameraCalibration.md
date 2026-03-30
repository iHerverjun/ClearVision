# 相机标定 / CameraCalibration

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `CameraCalibrationOperator` |
| 枚举值 (Enum) | `OperatorType.CameraCalibration` |
| 分类 (Category) | Calibration |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
该算子基于标定板角点/圆点，估计相机内参矩阵 `CameraMatrix` 和畸变参数 `DistCoeffs`。

当前实现支持两种标定板：

- `Chessboard`：棋盘格内角点
- `CircleGrid`：对称圆点阵列

支持两种执行模式：

- **SingleImage**：使用单张图像检测角点并直接调用 `Cv2.CalibrateCamera(...)`
- **FolderCalibration**：从文件夹读取多张标定图，累计多组角点后再标定

物点构建方式为标准平面棋盘模型：

`(x × SquareSize, y × SquareSize, 0)`

即默认标定板位于 `Z=0` 平面，`SquareSize` 用于定义相邻角点的物理间距。

> English: The operator estimates camera intrinsics and distortion coefficients from chessboard or circle-grid observations, using either a single image or a folder of calibration images.

## 实现策略 / Implementation Strategy
当前实现兼顾了交互性和可落地性：

- **单图模式快速反馈**：单图模式下，如果找到标定点，会直接执行一次标定并把 `CameraMatrix`、`DistCoeffs`、角点和物点一起打包进 JSON 返回。
- **多图模式更实用**：文件夹模式会遍历目录中的 `png/jpg/jpeg/bmp`，只把成功检测到角点的图像纳入标定。
- **棋盘格额外细化**：当 `PatternType = Chessboard` 时，会用 `CornerSubPix` 对角点做亚像素细化；圆点阵模式则不走该分支。
- **失败文件会被记录**：文件夹模式会统计未能读取或未能检测角点的文件名，并写入 `FailedFiles`。
- **结果可落盘**：文件夹模式会尝试把标定 JSON 保存到 `CalibrationOutputPath`；即便保存失败，算子仍会返回内存中的 `CalibrationData`。

> English: The operator is designed both for interactive debugging and for practical dataset-based calibration workflows, with reusable JSON output and optional file persistence.

## 核心 API 调用链 / Core API Call Chain
1. `GetStringParam / GetIntParam / GetDoubleParam`
2. `TryGetInputImage(inputs, "Image")`（单图模式）或 `Directory.GetFiles(...)`（文件夹模式）
3. `Cv2.CvtColor(..., BGR2GRAY)` / `Cv2.ImRead(..., Grayscale)`
4. `TryFindCalibrationCorners(...)`
   - `Cv2.FindChessboardCorners(...)`
   - 或 `Cv2.FindCirclesGrid(...)`
5. `Cv2.CornerSubPix(...)`（仅棋盘格）
6. `CreateObjectPoints(patternSize, squareSize)`
7. `Cv2.CalibrateCamera(...)`
8. `JsonSerializer.Serialize(payload)`
9. `File.WriteAllText(outputPath, json)`（文件夹模式，尽力而为）
10. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `PatternType` | `enum` | `"Chessboard"` | `Chessboard` / `CircleGrid` | 标定板类型。棋盘格会额外做角点亚像素细化。 |
| `BoardWidth` | `int` | `9` | `[2, 30]` | 标定板内点列数。 |
| `BoardHeight` | `int` | `6` | `[2, 30]` | 标定板内点行数。 |
| `SquareSize` | `double` | `25.0` | `[0.1, 1000.0]` | 相邻标定点的物理间距，通常单位为毫米。它决定物点尺度。 |
| `Mode` | `enum` | `"SingleImage"` | `SingleImage` / `FolderCalibration` | 执行模式：单图快速标定或文件夹批量标定。 |
| `ImageFolder` | `string` | `""` | 路径字符串 | 文件夹模式下的标定图目录。 |
| `CalibrationOutputPath` | `string` | `"calibration_result.json"` | 路径字符串 | 文件夹模式下尝试写出的标定 JSON 路径。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Input Image | `Image` | Yes | 单图模式下的输入图像。文件夹模式虽然端口仍声明必填，但实际标定主要依赖 `ImageFolder` 参数。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Result Image | `Image` | 结果图。单图模式会绘制检测到的角点；文件夹模式会在预览图上叠加标定统计信息。 |
| `CalibrationData` | Calibration Data | `String` | 标定结果 JSON 字符串。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 结果图宽度。 |
| `Height` | `Integer` | 结果图高度。 |
| `CalibrationData` | `String` | 标定结果 JSON。当前 JSON 中使用 `double[][] CameraMatrix` 和 `double[] DistCoeffs`。 |
| `Found` | `Boolean` | 单图模式中是否成功检测到标定板。 |
| `ReprojectionError` | `Double` | 重投影误差。 |
| `Corners` | `Integer` | 单图模式中检测到的角点数。 |
| `PatternType` | `String` | 当前标定板类型。 |
| `ImageCount` | `Integer` | 参与标定的有效图像数。 |
| `TotalImages` | `Integer` | 文件夹模式下扫描到的总图像数。 |
| `OutputPath` | `String` | 文件夹模式下尝试保存的输出路径。 |
| `Message` | `String` | 运行结果说明。 |

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 单图模式主要与角点检测和一次 `CalibrateCamera` 调用相关；文件夹模式近似随图像数量线性增长，并叠加最终一次全局标定开销。 |
| 典型耗时 (Typical Latency) | 单图模式通常较快；文件夹模式耗时主要取决于图像数量、分辨率和角点检测成功率。 |
| 内存特征 (Memory Profile) | 文件夹模式会临时保存多组 `objectPoints` / `imagePoints`，峰值内存随有效样本数增长。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：相机内参标定、镜头畸变校正前的数据准备。
- **适合 (Suitable)**：需要生成后续 `Undistort`、坐标变换或测量换算所需标定 JSON 的流程。
- **适合 (Suitable)**：现场调试时先用单图模式快速验证“标定板是否可检测”。
- **不适合 (Not Suitable)**：把单图模式当成高可靠最终标定结果直接投入高精度测量生产。
- **不适合 (Not Suitable)**：混合不同分辨率、不同拍摄条件但未做一致性管理的数据集。
- **不适合 (Not Suitable)**：标定板尺寸、点数配置与实际图样不一致的输入。

## 已知限制 / Known Limitations
1. 单图模式当前确实会执行 `CalibrateCamera(...)` 并输出 `CameraMatrix` / `DistCoeffs`，但从标定理论上讲，单张图通常不足以支撑稳定的高质量内参估计，结果更适合作为调试参考而不是最终生产标定。
2. 文件夹模式要求至少 `3` 张有效图像，否则直接失败。
3. 文件夹模式没有显式检查所有图像尺寸是否一致，而 `imageSize` 取自第一张成功读取的图像。
4. `CalibrationOutputPath` 的文件写入失败只会记录日志警告，不会导致算子整体失败；下游若依赖落盘文件，需要自行确认文件是否真的写出。
5. 当前输出 JSON 使用 `double[][] CameraMatrix` 与 `double[] DistCoeffs`，文档和下游解析应以这一结构为准。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充单图/文件夹模式、输出 JSON 结构、失败文件统计与当前实现限制 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

