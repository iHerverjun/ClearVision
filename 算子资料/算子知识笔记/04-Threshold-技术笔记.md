# Threshold 技术笔记

> **对应算子**: `ThresholdOperator` / `OperatorType.Thresholding`  
> **OperatorType**: `Thresholding`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/ThresholdOperator.cs`  
> **相关算子**: [AdaptiveThreshold](./05-AdaptiveThreshold-技术笔记.md)、[Morphology](./10-Morphology-技术笔记.md)、[BlobDetection](./11-BlobDetection-技术笔记.md)  
> **阅读前置**: 灰度图、直方图、前景与背景  
> **核心来源**: OpenCV Threshold 文档、Otsu 1979、Gonzalez & Woods《Digital Image Processing》

## 1. 一句话先理解这个算子

Threshold 的本质是把连续灰度切成两类或多种区域，它是很多视觉链路里“从图像走向区域分析”的第一步。

## 2. 先说清当前实现口径

ClearVision 当前实现支持 `Threshold`、`MaxValue`、`Type`、`UseOtsu` 四个核心参数，默认值分别为 `127.0`、`255.0`、`0`、`false`。也就是说，默认行为是一个最普通的全局二值化，而不是自适应阈值，也不是默认启用 Otsu。

当前实现还会输出 `ActualThreshold`。这在 `UseOtsu=true` 时尤其有意义，因为真正使用的阈值不再是你手填的数，而是算法从直方图估出来的值。

## 3. 算法原理

最简单的二值化可以写成:

```text
if gray(x, y) > T then 255 else 0
```

其中 `T` 就是阈值。它的前提是假设前景和背景在灰度上能被一个全局阈值大致分开。Otsu 方法则是在全局阈值家族里自动寻找一个类间方差较大的分割点。

Threshold 是 [BlobDetection](./11-BlobDetection-技术笔记.md)、[FindContours](./12-FindContours-技术笔记.md) 等区域算子的经典上游。

## 4. 参数说明

### 4.1 输入输出端口怎么理解

- 输入: `Image`
- 输出: `Image`
- 运行时附加字段: `ActualThreshold`

### 4.2 关键参数怎么调

| 参数 | 当前默认值 | 怎么理解 |
|------|------|------|
| `Threshold` | `127.0` | 手动阈值 |
| `MaxValue` | `255.0` | 满足条件时赋给前景的值 |
| `Type` | `0` | 阈值模式，如 `Binary`、`BinaryInv`、`Trunc` 等 |
| `UseOtsu` | `false` | 是否自动估计全局阈值 |

### 4.3 与教材口径不同的实现细节

- 教材里常说 Otsu 是“自动阈值”，但当前实现里它仍然是可选开关，不是默认行为。
- 当前实现强调工程输出，除了图像结果，还会保留 `ActualThreshold` 方便调试。
- 当前实现会把单通道二值结果再转成 `BGR` 三通道输出，主要是为了兼容前端显示链路，而不是因为阈值算法本身需要 3 通道。

## 5. 推荐使用链路与调参建议

- 典型链路 1: `ColorConversion -> Threshold -> Morphology -> BlobDetection`
- 典型链路 2: `ClaheEnhancement -> Threshold -> FindContours`
- 光照均匀、对比清晰时，先试全局阈值
- 如果阈值特别难选，先试 `UseOtsu=true`
- 如果不同区域亮度差很大，直接转去看 [AdaptiveThreshold](./05-AdaptiveThreshold-技术笔记.md)

## 6. 这个算子的边界

- 全局阈值假设整张图的亮度条件大致一致，阴影、热点、渐变背景都会破坏这个前提。
- 它只能按亮度切，不理解语义。
- 阈值分割一旦做错，下游 [BlobDetection](./11-BlobDetection-技术笔记.md) 往往会整串失真。

## 7. 失败案例与常见误区

### 案例1: 左边分得对，右边全错

这通常是光照不均匀，说明全局阈值不适合。应考虑 [AdaptiveThreshold](./05-AdaptiveThreshold-技术笔记.md) 或先做 [ClaheEnhancement](./09-ClaheEnhancement-技术笔记.md)。

### 案例2: Otsu 给出的结果也不好

Otsu 不是万能的。它依赖直方图结构，如果前景和背景分布本来就严重重叠，自动阈值也会无能为力。

### 常见误区

- “阈值就是一个固定数”: 工程上并不总是。自动阈值和自适应阈值都说明阈值可以来自算法估计。
- “只要二值化成功，就完成识别了”: 远远没有。二值化只是把任务交给了下一步区域分析。

## 8. 专业来源与延伸阅读

- OpenCV Documentation: `threshold`
- Nobuyuki Otsu, “A Threshold Selection Method from Gray-Level Histograms”, 1979
- Rafael C. Gonzalez and Richard E. Woods, *Digital Image Processing*, thresholding 章节

## 9. 一句话总结

Threshold 是视觉链路里最朴素也最关键的“分界线”，它简单、快，但对光照和对比度条件非常敏感。
