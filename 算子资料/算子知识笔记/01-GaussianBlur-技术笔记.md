# GaussianBlur 技术笔记

> **对应算子**: `GaussianBlurOperator` / `OperatorType.Filtering`  
> **OperatorType**: `Filtering`（仓库枚举里同时存在 `GaussianBlur`，但当前类返回的是 `Filtering`）  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/GaussianBlurOperator.cs`  
> **相关算子**: [MedianBlur](./02-MedianBlur-技术笔记.md)、[BilateralFilter](./03-BilateralFilter-技术笔记.md)、[CannyEdge](./06-CannyEdge-技术笔记.md)  
> **阅读前置**: 灰度图、卷积、噪声的基本概念  
> **核心来源**: OpenCV Gaussian Blur 文档、Szeliski《Computer Vision》、Bradski & Kaehler《Learning OpenCV》

## 1. 一句话先理解这个算子

GaussianBlur 就是用“越靠近中心权重越大”的方式做平滑，它的核心价值不是把图像变糊，而是把高频噪声压下去，让后面的边缘、阈值和模板匹配更稳定。

## 2. 先说清当前实现口径

ClearVision 当前实现直接调用 OpenCV 的 `Cv2.GaussianBlur`，关键参数是 `KernelSize`、`SigmaX`、`SigmaY`、`BorderType`。源码默认值是 `KernelSize=5`、`SigmaX=1.0`、`SigmaY=0.0`、`BorderType=4`。其中 `SigmaY=0.0` 表示沿 Y 方向自动继承 `SigmaX`，这是 OpenCV 的常见写法。

一个容易忽略的点是: 这个算子当前 `OperatorType` 返回的是 `Filtering`，而不是 `GaussianBlur`。也就是说，在仓库的实现层里，它更像“滤波大类下的一个具体实现”，而不是完全独立的一套运行语义。

## 3. 算法原理

二维高斯核的核心思想是让中心像素影响最大，离中心越远权重越小。形式上可以写成:

```text
G(x, y) = 1 / (2πσ²) * exp(-(x² + y²) / (2σ²))
```

把这个核和图像做卷积后，随机噪声会被平均掉，但边缘也会被一定程度地抹平。所以它很适合放在 [CannyEdge](./06-CannyEdge-技术笔记.md) 前面，也常放在 [Threshold](./04-Threshold-技术笔记.md) 前面做轻度预平滑。

## 4. 参数说明

### 4.1 输入输出端口怎么理解

- 输入: `Image`
- 输出: `Image`

它本身不输出额外几何结果，只负责给下游提供更平滑的图像。

### 4.2 关键参数怎么调

| 参数 | 当前默认值 | 怎么理解 |
|------|------|------|
| `KernelSize` | `5` | 卷积窗口大小，越大越平滑，但也越容易把细节抹掉 |
| `SigmaX` | `1.0` | X 方向高斯标准差，控制权重衰减速度 |
| `SigmaY` | `0.0` | 设为 0 时通常等于 `SigmaX` |
| `BorderType` | `4` | 边界像素怎样补齐，避免卷积时越界 |

### 4.3 与教材口径不同的实现细节

- 教材常先讲“高斯核公式”，但工程里真正影响结果的，往往是 `KernelSize` 与 `Sigma` 的搭配。
- 当前实现没有额外做颜色空间转换，意味着彩色图会直接在各通道上做平滑；如果你只关心灰度链路，通常会先接 [ColorConversion](./08-ColorConversion-技术笔记.md)。

## 5. 推荐使用链路与调参建议

- 典型链路 1: `GaussianBlur -> CannyEdge -> FindContours`
- 典型链路 2: `GaussianBlur -> Threshold -> Morphology -> BlobDetection`
- 如果噪声是轻微高斯噪声，先试 `KernelSize=5`
- 如果边缘已经比较弱，不要盲目增大核，先看是否该改用 [MedianBlur](./02-MedianBlur-技术笔记.md) 或 [ClaheEnhancement](./09-ClaheEnhancement-技术笔记.md)

## 6. 这个算子的边界

- 它不能区分“噪声”和“真实细节”，所以细小划痕、细线边缘也可能被一起抹掉。
- 它不擅长处理椒盐噪声，这类离散突刺更适合 [MedianBlur](./02-MedianBlur-技术笔记.md)。
- 它也不是“保边滤波”的代表，如果既想去噪又想尽量保边，更该考虑 [BilateralFilter](./03-BilateralFilter-技术笔记.md)。

## 7. 失败案例与常见误区

### 案例1: 核一变大，边缘全没了

这通常不是高斯滤波“失效”，而是你把它当成了越大越好的降噪器。对于细边、细孔、细裂纹，过大的核会直接吃掉有效结构。

### 案例2: 椒盐噪声怎么滤都不干净

高斯滤波更适合连续型噪声，不适合强离群点。这里通常要改用 [MedianBlur](./02-MedianBlur-技术笔记.md)。

### 常见误区

- “高斯滤波就是让图更清楚”: 错。它的本质是平滑，不是增强。
- “只要做了高斯滤波，Canny 一定更好”: 不一定。边缘本来就弱时，过度平滑反而会让双阈值更难选。

## 8. 专业来源与延伸阅读

- OpenCV Documentation: `GaussianBlur`
- Richard Szeliski, *Computer Vision: Algorithms and Applications*, smoothing and scale-space 相关章节
- Bradski and Kaehler, *Learning OpenCV*, image filtering 章节

## 9. 一句话总结

GaussianBlur 是视觉链路里最常见的“降噪起手式”，但它的代价是边缘变软，所以它永远是在“平滑”和“保细节”之间做取舍。
