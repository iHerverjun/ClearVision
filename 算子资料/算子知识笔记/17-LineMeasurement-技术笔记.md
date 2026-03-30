# LineMeasurement 技术笔记

> **对应算子**: `LineMeasurementOperator`  
> **OperatorType**: `OperatorType.LineMeasurement`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/LineMeasurementOperator.cs`  
> **相关算子**: [06-CannyEdge-技术笔记](./06-CannyEdge-技术笔记.md)、[07-SubpixelEdgeDetection-技术笔记](./07-SubpixelEdgeDetection-技术笔记.md)、[15-CaliperTool-技术笔记](./15-CaliperTool-技术笔记.md)  
> **阅读前置**: 霍夫直线、线段长度、角度  
> **核心来源**: OpenCV `HoughLines` / `HoughLinesP` 文档、Szeliski《Computer Vision》、本仓库当前实现

---

## 1. 一句话先理解这个算子

`LineMeasurement` 的目标是从图像里找出直线或线段，再输出角度、长度等基础几何信息。

## 2. 先说清当前实现口径

当前实现先统一做：

- 图像转灰度
- `Cv2.Canny(gray, edges, 50, 150)`

然后根据 `Method` 分支：

- `HoughLine`: 使用标准霍夫直线，强调“无穷长直线”的参数表示。
- `FitLine` 这个名字在参数里存在，但当前代码实际走的是 `Cv2.HoughLinesP`，更接近概率霍夫线段检测，而不是严格意义上的 `Cv2.FitLine`。

这是这篇笔记里最重要的仓库口径之一。

## 3. 算法原理

霍夫直线的核心思想是让边缘点在参数空间里投票，找出最可能的直线。  
标准霍夫更偏向全局直线表示；概率霍夫更偏向直接输出有限线段。

因此它适合：

- 找主方向
- 粗量角度
- 找长直边

但不适合直接承担亚像素级尺寸测量。

## 4. 参数说明

### 4.1 输入输出怎么理解

- 输入 `Image`: 待测图像。
- 输出 `Angle`: 第一条结果线的角度。
- 输出 `Length`: 当走线段路径时，可得到长度。
- 输出 `LineCount`: 检出的线数量。

### 4.2 关键参数怎么调

- `Method`: 默认 `HoughLine`。
- `Threshold`: 默认 `100`。霍夫累加阈值。
- `MinLength`: 默认 `50.0`。在线段路径下限制最小长度。
- `MaxGap`: 默认 `10.0`。在线段路径下允许的断裂间隙。

### 4.3 与教材口径的差别

- 当前 `FitLine` 参数名和代码行为并不完全一致，实际走的是 `HoughLinesP`。
- 当前实现固定先做一次 `Canny(50, 150)`，这会影响线检测稳定性。

## 5. 推荐使用链路与调参建议

推荐链路：

```text
GaussianBlur / CannyEdge / SubpixelEdgeDetection
  -> LineMeasurement
  -> 几何判断
```

调参建议：

- 如果你只是需要主方向，标准霍夫就够了。
- 如果你更关心线段长度和端点，线段路径更实用。
- 真正的精细边距测量，优先看 [15-CaliperTool-技术笔记](./15-CaliperTool-技术笔记.md)。

## 6. 这个算子的边界

- 它强依赖边缘先被稳定提取出来。
- 对短线、弱线、断裂线和纹理背景比较敏感。
- 霍夫结果更偏“检测”，不是高精度拟合。

## 7. 失败案例与常见误区

### 案例1：图里有线，但检测结果很多乱线

通常是前面边缘太脏，霍夫只是忠实地把这些边缘都投票出来了。

### 案例2：把 `FitLine` 理解成最小二乘直线拟合

当前代码层面并不是那条路径，这一点必须和教材口径分开。

### 案例3：直接量原图

线测量稳定性通常依赖更好的边缘和更受控的 ROI。

## 8. 专业来源与延伸阅读

- 本仓库: `LineMeasurementOperator.cs`
- OpenCV 官方文档: `HoughLines`、`HoughLinesP`
- Richard Szeliski, *Computer Vision: Algorithms and Applications*
- 与 [15-CaliperTool-技术笔记](./15-CaliperTool-技术笔记.md) 配合阅读

## 9. 一句话总结

`LineMeasurement` 适合找直线和粗量角度，但高精度测量通常不能只停在霍夫结果上。
