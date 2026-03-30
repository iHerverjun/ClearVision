# CircleMeasurement 技术笔记

> **对应算子**: `CircleMeasurementOperator`  
> **OperatorType**: `OperatorType.CircleMeasurement`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/CircleMeasurementOperator.cs`  
> **相关算子**: [07-SubpixelEdgeDetection-技术笔记](./07-SubpixelEdgeDetection-技术笔记.md)、[15-CaliperTool-技术笔记](./15-CaliperTool-技术笔记.md)、[17-LineMeasurement-技术笔记](./17-LineMeasurement-技术笔记.md)  
> **阅读前置**: 圆、半径、霍夫变换、圆度  
> **核心来源**: OpenCV `HoughCircles` 文档、Gonzalez & Woods《Digital Image Processing》、本仓库当前实现

---

## 1. 一句话先理解这个算子

`CircleMeasurement` 的目标是从图像里找出圆，并给出圆心、半径以及一些简单的形状质量指标。

## 2. 先说清当前实现口径

虽然参数里有 `Method = HoughCircle / FitEllipse` 两个选项，但当前主实现实际走的是：

- 图像转灰度
- 高斯模糊
- `Cv2.HoughCircles`
- 对检测到的圆做绘制和结果输出

也就是说，当前实现核心还是霍夫圆检测路径，而不是完整的椭圆拟合双分支实现。  
此外，代码里还会额外计算一个 `Circularity`，但这个圆度是通过 ROI 内再次做边缘与轮廓分析估计出来的，并不是 Hough 直接给出的原生结果。

## 3. 算法原理

霍夫圆检测的核心思想是：  
每个边缘点都对可能的圆心和半径投票，最终在参数空间中找出最可能的圆。

因此它适合：

- 圆孔检测
- 圆形零件定位
- 圆心和半径的粗测

但如果你要的是高精度边界测量，通常还需要更受控的边缘或卡尺链路。

## 4. 参数说明

### 4.1 输入输出怎么理解

- 输入 `Image`: 待测图像。
- 输出 `Radius`: 第一条检测圆的半径。
- 输出 `Center`: 第一条检测圆的圆心。
- 输出 `CircleCount`: 检出的圆数量。
- 输出 `CircleDataList`: 所有圆的结构化结果。

### 4.2 关键参数怎么调

- `MinRadius` / `MaxRadius`: 默认 `10 / 200`。先把搜索范围限制到业务尺度附近。
- `Dp`: 默认 `1.0`。累加器分辨率与原图分辨率之比。
- `MinDist`: 默认 `50.0`。限制多个圆心之间的最小距离。
- `Param1`: 默认 `100.0`。Canny 高阈值。
- `Param2`: 默认 `30.0`。霍夫累加器阈值，越高越严格。

### 4.3 与教材口径的差别

- 当前实现虽然暴露了 `FitEllipse` 选项，但主代码主体仍围绕 `HoughCircles` 组织。
- 当前输出的 `Circularity` 是后处理估计值，不要把它理解成霍夫算法自带的标准量。

## 5. 推荐使用链路与调参建议

推荐链路：

```text
GaussianBlur / SubpixelEdgeDetection / ROI
  -> CircleMeasurement
  -> 结果筛选
```

调参建议：

- 圆尺寸范围明确时，优先缩小半径搜索区间。
- 如果假圆很多，先提高 `Param2`，再检查上游边缘。
- 如果你只是要量一个明确边界的圆孔，卡尺或亚像素边缘有时会更稳。

## 6. 这个算子的边界

- 它依赖清楚、闭合或近闭合的圆边缘。
- 对强反光、缺边、椭圆透视变形不够稳。
- 它更适合“找圆”和“粗测半径”，不是所有场景下的最终精测工具。

## 7. 失败案例与常见误区

### 案例1：模板里是圆，结果没检出

常见原因是边缘断裂、反光或半径范围设置错了。

### 案例2：一个圆报出多个圆

往往和 `MinDist` 太小、边缘噪声太重有关。

### 案例3：直接拿霍夫圆结果做高精度计量

在很多工业测量场景里，这样的精度和稳定性都未必够。

## 8. 专业来源与延伸阅读

- 本仓库: `CircleMeasurementOperator.cs`
- OpenCV 官方文档: `imgproc::HoughCircles`
- Rafael C. Gonzalez, Richard E. Woods, *Digital Image Processing*
- 与 [15-CaliperTool-技术笔记](./15-CaliperTool-技术笔记.md) 和 [07-SubpixelEdgeDetection-技术笔记](./07-SubpixelEdgeDetection-技术笔记.md) 对照阅读

## 9. 一句话总结

`CircleMeasurement` 更像“圆目标的检测与粗测入口”，真正要稳量，常常还得靠更可控的边缘链路。
