# 算子 A / B / C 分级清单

> 用途：明确哪些算子能深聊，哪些只讲用途与边界，哪些不主动展开。
> 原则：面试不是比谁背得多，而是比谁知道自己最强的 20% 在哪里。

---

## 1. 分级标准

- `A 级`：能连续深聊 8-10 分钟，能讲原理、参数、边界、失败案例、排查思路。
- `B 级`：能讲清用途、适用场景、上下游关系和主要边界，但不主动进入细算法实现。
- `C 级`：知道它在系统里的角色，但当前不主动展开，除非面试岗位强相关。

---

## 2. 当前建议分级

| 等级 | 算子 | 当前口径 |
|---|---|---|
| A | `BlobDetection` | 主打。可讲当前实现、连通域、孔洞/Euler 数、筛选参数、失败案例。 |
| A | `TemplateMatch` | 主打。可讲用途、相似度思路、模板定位、适用边界、与深度学习的关系。 |
| A | `CannyEdge` | 主打。可讲梯度、NMS、双阈值、调参与光照边界。 |
| B | `Threshold` | 能讲全局阈值、二值化作用、与 Otsu/手动阈值的关系。 |
| B | `AdaptiveThreshold` | 能讲局部光照不均时的意义和参数边界。 |
| B | `Morphology` | 能讲开闭运算、去噪/补洞、与分割链路关系。 |
| B | `FindContours` | 能讲轮廓提取、层级、与 Blob 的差异。 |
| B | `SubpixelEdgeDetection` | 可作为备选。能讲“为什么需要子像素”和噪声敏感性。 |
| B | `PerspectiveTransform` | 能讲几何矫正、点对、常见失败条件。 |
| B | `CameraCalibration` | 能讲棋盘格、内参、重投影误差，但不夸大掌控度。 |
| B | `Undistort` | 能讲标定结果如何在运行时使用。 |
| B | `CaliperTool` | 能讲边缘搜索、测量思路、与 ROI/边缘质量关系。 |
| B | `LineMeasurement` | 能讲测量链路和输入先验。 |
| B | `CircleMeasurement` | 能讲圆拟合、边缘点质量、结果边界。 |
| C | `GaussianBlur` | 作为预处理知识点，不主动深挖实现。 |
| C | `MedianBlur` | 知道椒盐噪声场景即可。 |
| C | `BilateralFilter` | 知道保边去噪即可，不主动展开推导。 |
| C | `ColorConversion` | 知道空间切换意义即可。 |
| C | `ClaheEnhancement` | 知道对比度增强即可。 |
| C | `ShapeMatching` | 可讲用途，但不建议当前作为主打。 |
| C | `HandEyeCalibration` | 只有补到真实掌控度后再升级。当前不主讲。 |

---

## 3. 面试策略

### 3.1 主打顺序

1. 优先把 `BlobDetection` 讲稳。
2. 再把 `TemplateMatch` 和 `CannyEdge` 讲稳。
3. 如果对方继续追传统视觉，再补 `Threshold / Morphology / FindContours`。

### 3.2 不主动展开的内容

- 不主动把 `HandEyeCalibration` 当主打。
- 不主动展示“我会很多算子”，而是展示“我知道哪几类算子组成了业务链路”。
- 不用“广度”去掩盖“深度”不足。

---

## 4. 对外稳定说法

> 我不会把所有算子都当成自己的深聊点。当前我主打 `BlobDetection / TemplateMatch / CannyEdge` 三个点，因为这三个点既能覆盖传统视觉里的区域、匹配、边缘三条主线，也更容易和我的工程链路、失败案例、调参经验对上。

