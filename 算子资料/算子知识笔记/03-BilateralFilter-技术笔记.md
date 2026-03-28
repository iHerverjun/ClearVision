# BilateralFilter 技术笔记

> **对应算子**: `BilateralFilterOperator` / `OperatorType.BilateralFilter`  
> **OperatorType**: `BilateralFilter`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/BilateralFilterOperator.cs`  
> **相关算子**: [GaussianBlur](./01-GaussianBlur-技术笔记.md)、[MedianBlur](./02-MedianBlur-技术笔记.md)、[CannyEdge](./06-CannyEdge-技术笔记.md)  
> **阅读前置**: 高斯滤波、像素邻域、边缘的概念  
> **核心来源**: Tomasi and Manduchi 1998、OpenCV Bilateral Filter 文档、Szeliski《Computer Vision》

## 1. 一句话先理解这个算子

BilateralFilter 的目标是“既要去噪，又不想把边缘抹掉”，它同时考虑空间距离和灰度差异，所以不是所有邻居都能平等参与平均。

## 2. 先说清当前实现口径

ClearVision 当前实现读取 `Diameter`、`SigmaColor`、`SigmaSpace` 三个参数，默认值分别是 `9`、`75.0`、`75.0`，然后直接调用 OpenCV 的双边滤波。它没有额外的自适应策略，也没有针对性能做金字塔近似，所以要把它看成“效果优先、开销较高”的保边滤波器。

## 3. 算法原理

双边滤波的权重来自两部分:

```text
总权重 = 空间权重 × 灰度权重
```

- 空间权重: 离中心越近，影响越大
- 灰度权重: 灰度越接近中心像素，影响越大

这意味着:

- 同一块平坦区域内，噪声会被平滑
- 跨越强边缘时，由于像素差异大，边缘两侧不会被强行平均

这也是它和 [GaussianBlur](./01-GaussianBlur-技术笔记.md) 最大的不同。

## 4. 参数说明

### 4.1 输入输出端口怎么理解

- 输入: `Image`
- 输出: `Image`

### 4.2 关键参数怎么调

| 参数 | 当前默认值 | 怎么理解 |
|------|------|------|
| `Diameter` | `9` | 参与滤波的邻域直径 |
| `SigmaColor` | `75.0` | 对灰度差异的容忍度，越大越容易跨灰度差异平均 |
| `SigmaSpace` | `75.0` | 对空间距离的容忍度，越大越容易引入更远邻域 |

### 4.3 与教材口径不同的实现细节

- 教材里常把双边滤波讲得很“优雅”，但工程上它最大的现实问题是慢。
- 当前实现没有做速度优化，所以在大图或实时链路里要谨慎使用。

## 5. 推荐使用链路与调参建议

- 典型链路 1: `BilateralFilter -> CannyEdge -> FindContours`
- 典型链路 2: `BilateralFilter -> Threshold -> Morphology`
- 如果边缘很重要、噪声又明显，可以先试它
- 如果实时性要求高，先比较它和 [GaussianBlur](./01-GaussianBlur-技术笔记.md) 的收益是否值得

## 6. 这个算子的边界

- 它不是万能保边滤波。纹理复杂时，参数一大就会把局部纹理也当成“可保留结构”。
- 运行代价高，分辨率越高越明显。
- 对非常强的离群点噪声，它未必像 [MedianBlur](./02-MedianBlur-技术笔记.md) 那样干脆。

## 7. 失败案例与常见误区

### 案例1: 参数一大，图像变得“油画感”

这通常是 `SigmaColor` 和 `SigmaSpace` 同时过大导致的。它虽然保住了主边缘，但把纹理和层次也抹成了块。

### 案例2: 实时链路明显变慢

双边滤波本来就比普通卷积重，不能拿它的耗时和 [GaussianBlur](./01-GaussianBlur-技术笔记.md) 同等看待。

### 常见误区

- “双边滤波就是更高级的高斯滤波”: 不完全对。它的目标更偏保边，但代价也更高。
- “保边就一定更适合测量”: 不一定。很多测量更依赖稳定边缘链路，未必必须先用双边滤波。

## 8. 专业来源与延伸阅读

- Carlo Tomasi and Roberto Manduchi, “Bilateral Filtering for Gray and Color Images”, ICCV 1998
- OpenCV Documentation: `bilateralFilter`
- Richard Szeliski, *Computer Vision: Algorithms and Applications*, edge-preserving smoothing 相关章节

## 9. 一句话总结

BilateralFilter 的价值在于“平滑区域、尽量保边”，但它的代价是更慢、更依赖参数，也更容易被误用成一把通吃所有噪声的锤子。
