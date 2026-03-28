# MedianBlur 技术笔记

> **对应算子**: `MedianBlurOperator` / `OperatorType.MedianBlur`  
> **OperatorType**: `MedianBlur`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/MedianBlurOperator.cs`  
> **相关算子**: [GaussianBlur](./01-GaussianBlur-技术笔记.md)、[BilateralFilter](./03-BilateralFilter-技术笔记.md)、[Threshold](./04-Threshold-技术笔记.md)  
> **阅读前置**: 邻域滤波、噪声类型的基本概念  
> **核心来源**: OpenCV Median Blur 文档、Gonzalez & Woods《Digital Image Processing》、Learning OpenCV

## 1. 一句话先理解这个算子

MedianBlur 不是平均邻域，而是取邻域里的“中位数”，所以它对椒盐噪声特别有效，也更不容易像均值或高斯那样把孤立黑白点扩散成一片灰。

## 2. 先说清当前实现口径

ClearVision 当前实现非常直接: 读取 `KernelSize`，必要时修正为奇数，再调用 OpenCV 的 `MedianBlur`。源码默认 `KernelSize=5`。这说明当前算子重点就是“实用稳定”，而不是做复杂的噪声建模。

和 [GaussianBlur](./01-GaussianBlur-技术笔记.md) 不同，它没有 `Sigma` 参数，因为中值滤波不是基于连续权重分布，而是基于排序。

## 3. 算法原理

对每个像素，取其邻域窗口内所有像素值，排序后选择中间那个值作为新像素。这个过程对离群点特别鲁棒，因为极端亮点或暗点通常不会成为中位数。

直观理解:

- 高斯滤波像“加权平均”
- 中值滤波像“把最离谱的点忽略掉”

所以在二值图、灰度图、带椒盐噪声的图上，它常常比高斯滤波更稳。

## 4. 参数说明

### 4.1 输入输出端口怎么理解

- 输入: `Image`
- 输出: `Image`

### 4.2 关键参数怎么调

| 参数 | 当前默认值 | 怎么理解 |
|------|------|------|
| `KernelSize` | `5` | 邻域窗口大小，必须是奇数；越大越能去掉孤立噪声，但也更容易损失细节 |

### 4.3 与教材口径不同的实现细节

- 教材里常强调中值滤波对椒盐噪声有效，仓库实现层面则更加朴素: 它没有自动判断噪声类型，全靠你根据场景选择。
- 这个算子当前没有输出“噪声统计”或“被替换像素比例”，所以调参主要还是靠图像观察和下游效果。

## 5. 推荐使用链路与调参建议

- 典型链路 1: `MedianBlur -> Threshold -> Morphology -> BlobDetection`
- 典型链路 2: `MedianBlur -> CannyEdge -> FindContours`
- 如果图里是零星亮点、黑点、毛刺，优先试中值滤波
- 如果图里的噪声更像连续波动或传感器颗粒感，先试 [GaussianBlur](./01-GaussianBlur-技术笔记.md)

## 6. 这个算子的边界

- 它对连续高斯噪声的抑制通常不如高斯滤波自然。
- 过大的核会让窄边缘、细线和小孔消失。
- 它不具备显式“保边建模”，只是因为中位数机制看起来比均值更抗离群点。

## 7. 失败案例与常见误区

### 案例1: 小圆孔被滤没了

这是因为目标尺寸已经接近滤波窗口。对窗口来说，它看起来和孤立噪点差不多。

### 案例2: 连续噪声看起来没明显改善

这类噪声不一定适合中值滤波，可能要换成 [GaussianBlur](./01-GaussianBlur-技术笔记.md) 或 [BilateralFilter](./03-BilateralFilter-技术笔记.md)。

### 常见误区

- “中值滤波一定比高斯滤波好”: 错，它们适合的噪声模型不同。
- “核越大越稳”: 错，大核会直接改变目标几何外观。

## 8. 专业来源与延伸阅读

- OpenCV Documentation: `medianBlur`
- Rafael C. Gonzalez and Richard E. Woods, *Digital Image Processing*, nonlinear filtering 相关章节
- Bradski and Kaehler, *Learning OpenCV*, smoothing filters 相关章节

## 9. 一句话总结

MedianBlur 最适合做“去掉离谱小噪点，但尽量别把边缘拖成灰”的场景，尤其适合椒盐噪声和二值前处理。
