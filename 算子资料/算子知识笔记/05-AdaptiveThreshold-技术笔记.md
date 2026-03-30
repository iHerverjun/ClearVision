# AdaptiveThreshold 技术笔记

> **对应算子**: `AdaptiveThresholdOperator` / `OperatorType.AdaptiveThreshold`  
> **OperatorType**: `AdaptiveThreshold`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/AdaptiveThresholdOperator.cs`  
> **相关算子**: [Threshold](./04-Threshold-技术笔记.md)、[ClaheEnhancement](./09-ClaheEnhancement-技术笔记.md)、[Morphology](./10-Morphology-技术笔记.md)  
> **阅读前置**: 全局阈值、局部窗口、亮度不均匀的概念  
> **核心来源**: OpenCV Adaptive Threshold 文档、Gonzalez & Woods《Digital Image Processing》、Learning OpenCV

## 1. 一句话先理解这个算子

AdaptiveThreshold 不是给整张图只设一个阈值，而是让每个局部区域自己算阈值，所以它特别适合背景亮度不均匀的图像。

## 2. 先说清当前实现口径

ClearVision 当前实现的核心参数是 `MaxValue`、`AdaptiveMethod`、`ThresholdType`、`BlockSize`、`C`，默认分别是 `255.0`、`Gaussian`、`Binary`、`11`、`2.0`。这说明仓库默认更偏向高斯加权的局部统计，而不是简单均值。

这里还有一个工程上很重要的限制: `BlockSize` 必须是奇数且要大于等于 3。你如果把局部窗口设得太小，阈值会跟着局部噪声乱跳；设得太大，又会慢慢退化成接近全局阈值。

## 3. 算法原理

AdaptiveThreshold 的核心思想是:

```text
T(x, y) = 局部邻域统计值 - C
```

局部统计值常见有两类:

- Mean: 局部均值
- Gaussian: 局部高斯加权均值

最终每个像素会和它自己的局部阈值比较。这样即使图像左上角偏亮、右下角偏暗，也仍有机会把目标提出来。

## 4. 参数说明

### 4.1 输入输出端口怎么理解

- 输入: `Image`
- 输出: `Image`

### 4.2 关键参数怎么调

| 参数 | 当前默认值 | 怎么理解 |
|------|------|------|
| `AdaptiveMethod` | `Gaussian` | 局部阈值的统计方式 |
| `ThresholdType` | `Binary` | 二值化方向 |
| `BlockSize` | `11` | 局部窗口大小，必须是奇数 |
| `C` | `2.0` | 从局部统计值里减去的修正项 |
| `MaxValue` | `255.0` | 前景像素赋值 |

### 4.3 与教材口径不同的实现细节

- 教材常把它当成“局部阈值的一类方法”，仓库实现则把方法枚举和二值化方向都暴露成了可配参数。
- 当前默认用的是 `Gaussian`，这比单纯均值更重视中心邻域。

## 5. 推荐使用链路与调参建议

- 典型链路 1: `ColorConversion -> AdaptiveThreshold -> Morphology -> BlobDetection`
- 典型链路 2: `ClaheEnhancement -> AdaptiveThreshold -> FindContours`
- `BlockSize` 先从 `11` 或 `15` 试起
- `C` 太小容易把背景纹理也拉进前景，太大则可能吃掉弱目标
- 如果图像本来已经很均匀，别急着用它，先试更简单的 [Threshold](./04-Threshold-技术笔记.md)

## 6. 这个算子的边界

- 它能解决亮度不均匀，但不能解决目标和背景本身灰度几乎重叠的问题。
- 局部阈值更容易把背景纹理、纸张纹路、表面颗粒误当成前景。
- 它输出的二值图通常更“碎”，经常需要接 [Morphology](./10-Morphology-技术笔记.md) 做清理。

## 7. 失败案例与常见误区

### 案例1: 亮度不均解决了，但噪点变多了

这是局部阈值的典型副作用。它对局部变化很敏感，所以常要配合开运算、闭运算使用。

### 案例2: 调来调去还是不稳

这可能说明你真正的问题不只是亮度不均，而是对比度太低、边缘太弱，应该先看 [ClaheEnhancement](./09-ClaheEnhancement-技术笔记.md)。

### 常见误区

- “自适应阈值一定比全局阈值高级”: 不一定。它更灵活，但也更容易把背景细节带出来。
- “局部窗口越小越精准”: 错，小窗口更容易跟着噪声波动。

## 8. 专业来源与延伸阅读

- OpenCV Documentation: `adaptiveThreshold`
- Rafael C. Gonzalez and Richard E. Woods, *Digital Image Processing*, adaptive thresholding 相关内容
- Bradski and Kaehler, *Learning OpenCV*, thresholding and local adaptation 相关章节

## 9. 一句话总结

AdaptiveThreshold 的优势在于能“按局部亮度条件分割”，但它的代价是更敏感、更碎、更依赖后续形态学清理。
