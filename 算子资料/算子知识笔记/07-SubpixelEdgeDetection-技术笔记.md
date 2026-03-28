# SubpixelEdgeDetection 技术笔记

> **对应算子**: `SubpixelEdgeDetectionOperator`  
> **OperatorType**: `OperatorType.SubpixelEdgeDetection`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/SubpixelEdgeDetectionOperator.cs`  
> **相关算子**: [CannyEdge](./06-CannyEdge-技术笔记.md)、[CaliperTool](./15-CaliperTool-技术笔记.md)、[LineMeasurement](./17-LineMeasurement-技术笔记.md)  
> **阅读前置**: 先理解像素级边缘和梯度方向，再看这篇会更轻松  
> **核心来源**: ClearVision 当前实现、Steger 方法、OpenCV 梯度与亚像素插值相关文献

---

## 1. 一句话先理解这个算子

`SubpixelEdgeDetection` 的重点不是“有没有边缘”，而是“边缘到底落在像素格子的哪一小段位置上”。

---

## 2. 先说清当前实现口径

ClearVision 当前实现不是单一路径，而是两大分支：

1. `Method=Steger`  
   使用 `StegerSubpixelEdgeDetector`
2. `Method=GradientInterp / GaussianFit`  
   先做灰度化、GaussianBlur、Canny、FindContours、Sobel，再沿梯度方向做一维细化

所以这个算子不能简单理解成“更高级的 Canny”。  
它的真实口径是：**先得到候选边缘，再把边缘点往亚像素位置细化**。

---

## 3. 算法原理

### 3.1 为什么像素级边缘不够

一条真实边缘在成像时通常不会刚好落在整数像素中心。  
如果后面要做宽度、圆心、直线角度等更细的测量，只知道“边在第 120 列”通常不够。

### 3.2 当前实现的两类思路

#### Steger 路径

Steger 方法常见于细线、脊线、亚像素线中心提取。它依赖局部导数信息和模型约束，目标是直接估计更细的位置。

#### 传统插值路径

当前实现的非 Steger 路径大致是：

```text
Gray
  -> GaussianBlur
  -> Canny
  -> FindContours
  -> Sobel 求梯度
  -> 沿梯度方向前后采样
  -> 用局部拟合估计亚像素偏移
```

如果是 `GaussianFit`，偏移会再乘一个高斯型权重修正；如果是 `GradientInterp`，则更接近抛物线式局部插值。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 原图或灰度图 |
| `Image` 输出 | 可视化后的结果图，画出边缘点和法向 |
| `Edges` 输出 | 亚像素边缘点列表，包含位置、法向、强度等 |

### 4.2 关键参数怎么调

| 参数 | 当前实现默认值 | 怎么理解 |
|------|------|------|
| `LowThreshold` | `50.0` | 候选边缘检测低阈值 |
| `HighThreshold` | `150.0` | 候选边缘检测高阈值 |
| `Sigma` | `1.0` | 传统路径高斯平滑强度 |
| `Method` | `GradientInterp` | 选择细化策略 |
| `EdgeThreshold` | `10.0` | Steger 路径的边缘强度阈值 |

### 4.3 和教材口径不完全一样的地方

- 当前实现把 `Canny + Sobel + 梯度方向采样` 组合成了一条传统亚像素细化链
- `Steger` 只在指定方法时使用，不是所有路径都走 Steger
- 输出不仅给图，还给结构化边缘点列表，便于后续测量使用

---

## 5. 推荐使用链路与调参建议

### 5.1 它通常放在哪里

```text
原图
  -> 轻量预处理
  -> SubpixelEdgeDetection
  -> CaliperTool / LineMeasurement / CircleMeasurement
```

### 5.2 调参建议

- 先确保边缘本身清楚，再谈亚像素；边缘候选都不稳，亚像素只会放大不稳定
- 如果你只是想做粗轮廓提取，先用 [CannyEdge](./06-CannyEdge-技术笔记.md) 就够了
- 如果要做尺寸或位置测量，再考虑亚像素路径

---

## 6. 这个算子的边界

### 6.1 它不是魔法

亚像素不能凭空创造信息。模糊、低对比、反光严重时，细化出来的位置也不会可靠。

### 6.2 它依赖梯度质量

当前实现的传统路径显著依赖 Sobel 梯度和局部采样质量，所以前处理和照明条件非常关键。

### 6.3 它更适合测量，不适合大面积语义检测

这个算子更像测量链的一环，而不是缺陷语义识别或目标分类主力。

---

## 7. 失败案例与常见误区

### 案例 1：边缘本来就虚，亚像素结果还飘

这不是算子“失灵”，而是上游输入不满足高精度定位前提。

### 案例 2：把它当成“比 Canny 更强”的通用边缘算子

它更细，但也更贵、更依赖输入质量，不适合作为所有场景的默认替代。

### 常见误区

- 误区一：亚像素一定更准  
  只有在成像质量和梯度结构足够好时才成立。
- 误区二：方法名只是不同实现细节  
  实际上 `Steger` 和 `GradientInterp / GaussianFit` 的数学假设并不一样。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/SubpixelEdgeDetectionOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/SubpixelEdgeDetection.md`
- Carsten Steger, *An Unbiased Detector of Curvilinear Structures*, IEEE TPAMI, 1998
- OpenCV Documentation: Sobel, Canny, contour extraction
- Szeliski, *Computer Vision: Algorithms and Applications*, subpixel localization related sections

---

## 9. 一句话总结

`SubpixelEdgeDetection` 的价值在于把“边大概在这里”推进到“边更精确地在这里”，它属于测量链的精细化工具，而不是普通边缘提取的简单升级版。
