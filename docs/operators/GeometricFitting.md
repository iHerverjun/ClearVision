# 几何拟合 / GeometricFitting

## 基本信息 / Basic Info
| 项目 (Field) | 值 (Value) |
|------|------|
| 类名 (Class) | `GeometricFittingOperator` |
| 枚举值 (Enum) | `OperatorType.GeometricFitting` |
| 分类 (Category) | Measurement |
| 成熟度 (Maturity) | 稳定 Stable |
| 作者 (Author) | 蘅芜君 |

## 算法原理 / Algorithm Principle
当前实现的几何拟合流程并不是直接接收点集输入，而是：

1. 把输入图像灰度化；
2. 用固定阈值做二值化；
3. 提取外轮廓；
4. 按 `MinArea` 过滤掉过小轮廓；
5. 将**所有有效轮廓的点合并为一个总点集**；
6. 对这个总点集执行直线、圆或椭圆拟合。

因此该算子的拟合对象是“阈值分割后所有有效轮廓点的联合点集”，而不是默认只对最大轮廓或某个 ROI 内单独轮廓拟合。

拟合分支如下：

- `Line`：`Cv2.FitLine(...)`
- `Circle`：自定义最小二乘圆拟合 `FitCircleLeastSquares(...)`
- `Ellipse`：`Cv2.FitEllipse(...)`

当 `RobustMethod = Ransac` 时：

- 直线和圆会先用 RANSAC 估计内点，再对内点做最终拟合；
- 椭圆**不走 RANSAC**，仍直接调用 `FitEllipse(...)`。

> English: The operator thresholds the input image, merges all valid contour points into one point set, and then fits a line, circle, or ellipse, with optional RANSAC only for line and circle fitting.

## 实现策略 / Implementation Strategy
当前实现更偏向“从图像自动提点再拟合”的通用流程，而不是精密点集拟合器：

- **固定阈值前处理**：没有自适应阈值、边缘子像素提取或 ROI 专用采样逻辑，全部基于固定二值阈值起步。
- **多轮廓合并**：只要轮廓面积达到 `MinArea`，其点都会被并入总点集参与拟合。
- **失败结果多数仍走成功输出**：当没有有效轮廓、点数不足或某个拟合分支失败时，算子通常返回成功执行结果，但 `FitResult.Success=false` 且附带 `Message`。
- **输出结构因拟合类型而异**：直线、圆、椭圆各自返回不同键值，如 `LineVx/LineVy`、`Radius`、`MajorAxis/MinorAxis` 等。
- **可视化优先**：结果图中会绘制原始轮廓和最终拟合图形，便于人工核查拟合是否合理。

> English: The implementation is a practical image-to-geometry pipeline, not a pure point-set fitting API, and the returned fit payload varies by the selected geometry type.

## 核心 API 调用链 / Core API Call Chain
1. `TryGetInputImage(inputs, "Image")`
2. `GetStringParam / GetIntParam / GetDoubleParam`
3. `Cv2.CvtColor(..., BGR2GRAY)`
4. `Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary)`
5. `Cv2.FindContours(binary, ..., RetrievalModes.External, ApproxSimple)`
6. 面积过滤并合并所有轮廓点
7. 分支拟合：
   - `Cv2.FitLine(...)`
   - 自定义 `FitCircleLeastSquares(...)`
   - `Cv2.FitEllipse(...)`
8. 可选 RANSAC：
   - `TryEstimateLineInliersRansac(...)`
   - `TryEstimateCircleInliersRansac(...)`
9. `Cv2.DrawContours(...)` / `Cv2.Line(...)` / `Cv2.Circle(...)` / `Cv2.Ellipse(...)`
10. `CreateImageOutput(resultImage, additionalData)`

## 参数说明 / Parameters
| 参数名 (Name) | 类型 (Type) | 默认值 (Default) | 范围 (Range) | 说明 (Description) |
|--------|------|--------|------|------|
| `FitType` | `enum` | `"Circle"` | `Line` / `Circle` / `Ellipse` | 拟合类型。决定最终拟合模型及输出字段结构。 |
| `Threshold` | `double` | `127.0` | `[0.0, 255.0]` | 固定二值阈值，用于先把图像分割成轮廓点集。 |
| `MinArea` | `int` | `100` | `[0, +∞)` | 最小轮廓面积。面积低于此值的轮廓不会参与拟合。 |
| `MinPoints` | `int` | `5` | `[3, 10000]` | 合并后点集的最小点数门槛。 |
| `RobustMethod` | `enum` | `"LeastSquares"` | `LeastSquares` / `Ransac` | 鲁棒方法选择。当前只有直线和圆会真正使用 RANSAC 分支。 |
| `RansacIterations` | `int` | `200` | `[10, 5000]` | RANSAC 迭代次数。 |
| `RansacInlierThreshold` | `double` | `2.0` | `(0, 100]` | 点到模型距离小于该阈值时视为内点。 |

## 输入/输出端口 / Input/Output Ports
### 输入 / Inputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 必填 (Required) | 说明 (Description) |
|------|------|------|------|------|
| `Image` | Input Image | `Image` | Yes | 输入图像。所有拟合都从图像分割轮廓点开始，而不是直接从点集输入。 |

### 输出 / Outputs
| 名称 (Name) | 显示名 (DisplayName) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|------|
| `Image` | Result Image | `Image` | 结果图，会绘制轮廓和拟合图形。 |
| `FitResult` | Fit Result | `Any` | 拟合结果字典。不同拟合类型字段不同。 |

### 运行时附加输出 / Runtime Additional Outputs
| 名称 (Name) | 数据类型 (DataType) | 说明 (Description) |
|------|------|------|
| `Width` | `Integer` | 输出图像宽度。 |
| `Height` | `Integer` | 输出图像高度。 |
| `FitResult` | `Dictionary` | 拟合结果主体字典。 |
| `FitType` | `String` | 当前拟合类型。 |
| `PointCount` | `Integer` | 参与拟合的总点数（合并后的点集大小）。 |
| `ContourCount` | `Integer` | 参与拟合的有效轮廓数量。 |

### `FitResult` 典型字段 / Typical `FitResult` Keys
- **通用字段**：`Success`、`RobustMethod`、可选 `Message`
- **直线拟合**：`LineVx`、`LineVy`、`LineX0`、`LineY0`、`Angle`
- **圆拟合**：`CenterX`、`CenterY`、`Radius`
- **椭圆拟合**：`CenterX`、`CenterY`、`MajorAxis`、`MinorAxis`、`Angle`
- **RANSAC 辅助字段**：`InlierCount`、`InlierRatio`

## 性能特征 / Performance
| 指标 (Metric) | 值 (Value) |
|------|------|
| 时间复杂度 (Time Complexity) | 预处理与轮廓提取近似随像素数线性增长；RANSAC 模式额外与 `RansacIterations × PointCount` 相关。 |
| 典型耗时 (Typical Latency) | 固定阈值和轮廓提取通常较快；开启 RANSAC 后，点集越大成本越明显。 |
| 内存特征 (Memory Profile) | 需要分配灰度图、二值图、轮廓点集和结果图；多轮廓场景下点集内存会随轮廓总点数增长。 |

## 适用场景 / Use Cases
- **适合 (Suitable)**：目标在阈值分割后轮廓清晰、需要拟合直线/圆/椭圆参数的场景。
- **适合 (Suitable)**：希望快速从图像直接得到几何模型参数，而不是单独准备点集输入的流程。
- **适合 (Suitable)**：存在少量异常点时，线/圆拟合可借助 RANSAC 提升鲁棒性。
- **不适合 (Not Suitable)**：多个无关目标同时存在且会被并入同一总点集的图像。
- **不适合 (Not Suitable)**：需要亚像素边缘采样、卡尺测量或专门测量 ROI 约束的高精度场景。
- **不适合 (Not Suitable)**：低对比度或固定阈值难以稳定分割的复杂背景。

## 已知限制 / Known Limitations
1. 当前实现会把**所有有效轮廓点合并后再拟合**，如果图中存在多个独立目标，可能得到混合后的错误几何模型。
2. 预处理仅使用固定阈值 `ThresholdTypes.Binary`，没有自适应阈值、极性选择或形态学清理分支。
3. `RobustMethod=Ransac` 当前只对直线和圆有效，椭圆拟合仍直接使用 `Cv2.FitEllipse(...)`。
4. 没有有效轮廓或点数不足时，算子通常返回成功执行结果，并把失败状态放进 `FitResult.Success` 与 `Message`，流程编排时不能只看执行状态。
5. `FitResult` 字段结构会随 `FitType` 改变，下游消费前应根据拟合类型判断字段是否存在。

## 变更记录 / Changelog
| 版本 (Version) | 日期 (Date) | 变更内容 (Changes) |
|------|------|----------|
| 1.0.2 | 2026-03-14 | 第二轮基于源码补充轮廓合并逻辑、RANSAC 适用范围、FitResult 结构与执行语义说明 |
| 1.0.1 | 2026-03-14 | 基于源码补充算法原理、调用链、参数语义、适用场景与已知限制 |
| 1.0.0 | 2026-03-03 | 自动生成文档骨架 / Generated skeleton |

