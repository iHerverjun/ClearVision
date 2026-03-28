# BlobDetection 技术笔记

> **对应算子**: `BlobDetectionOperator` / `OperatorType.BlobAnalysis`  
> **OperatorType**: `OperatorType.BlobAnalysis`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/BlobDetectionOperator.cs`  
> **相关算子**: [Threshold](./04-Threshold-技术笔记.md)、[Morphology](./10-Morphology-技术笔记.md)、[FindContours](./12-FindContours-技术笔记.md)  
> **阅读前置**: 先知道前景/背景、连通域、轮廓是什么  
> **核心来源**: ClearVision 当前实现、OpenCV connected components / contours、经典形状特征教材

---

## 1. 一句话先理解这个算子

`BlobDetection` 的本质不是“识别某类物体”，而是把前景区域一个个分出来，再给每个区域算面积、圆度、凸度、孔洞数等特征。

---

## 2. 先说清当前实现口径

在 ClearVision 当前实现里，它的主路径不是“直接全图 `findContours`”，也不是单纯的 `SimpleBlobDetector`。

当前主执行路径更接近：

```text
图像
  -> 灰度化
  -> 二值化
  -> 可选黑白反转
  -> 可选 HSV 颜色预过滤
  -> ConnectedComponentsWithStats
  -> 单个连通域 mask
  -> FindContours(CComp)
  -> 计算形状与灰度特征
  -> 过滤
  -> 输出 Blobs / BlobCount / BlobFeatures
```

仓库里虽然还保留了 legacy 分支，但学习和使用当前系统时，应以这条主路径为准。

---

## 3. 算法原理

### 3.1 什么是 blob

在这里，blob 更接近“一个连通的前景区域”。  
只要前景像素通过连通关系连成一片，就会被视为一个候选区域。

### 3.2 当前实现做了两层工作

1. **连通域提取**  
   先把前景区域切成一个个独立 component
2. **区域特征分析**  
   再给每个区域算面积、周长、圆度、凸度、矩形度、惯性比、离心率、孔洞数等

### 3.3 为什么它常接在阈值和形态学后面

因为它很依赖输入前景质量。  
如果前面 [Threshold](./04-Threshold-技术笔记.md) 或 [Morphology](./10-Morphology-技术笔记.md) 做得不好，blob 结果就会直接失真。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 通常是已经分割过、接近前景/背景清晰的图像 |
| `SourceImage` 输入 | 可选原图，用于灰度统计和结果绘制 |
| `Blobs` 输出 | blob 特征列表 |
| `BlobFeatures` 输出 | 打开详细特征后得到更完整的特征集 |
| `BlobCount` 输出 | 过滤后的 blob 数量 |

### 4.2 关键参数怎么调

| 参数 | 默认值 | 作用 |
|------|------|------|
| `MinArea` | `100` | 过滤掉太小的噪点 |
| `MaxArea` | `100000` | 防止整块背景被当成目标 |
| `Color` | `White` | 定义前景极性，必要时反转二值图 |
| `MinCircularity` | `0.0` | 保留更接近圆形的目标 |
| `MinConvexity` | `0.0` | 过滤有明显凹陷的区域 |
| `MinInertiaRatio` | `0.0` | 过滤过细长区域 |
| `MinRectangularity` | `0.0` | 保留更像矩形的区域 |
| `MinEccentricity` | `0.0` | 倾向保留细长目标 |
| `OutputDetailedFeatures` | `false` | 是否输出更全特征 |
| `FeatureFilter` | `""` | 用表达式做二次筛选 |

### 4.3 当前实现里最值得记住的几点

- 带孔目标默认仍算 **1 个 blob**
- 孔洞会通过 `HoleCount` 和 `EulerNumber` 反映
- `Rectangularity` 基于轴对齐外接矩形，所以旋转矩形会被低估

---

## 5. 推荐使用链路与调参建议

### 5.1 常见链路

```text
Gray / Color Filter
  -> Threshold / AdaptiveThreshold
  -> Morphology
  -> BlobDetection
```

### 5.2 调参建议

- 面积阈值永远是第一层筛选
- 如果目标形状比较稳定，再叠加圆度、凸度、矩形度
- 如果输入是原始灰度图，不要直接把它想象成“Blob 算子会自动帮你分割好”

---

## 6. 这个算子的边界

### 6.1 它严重依赖前景分割质量

当前主路径的阈值策略比较保守，如果上游没有先把前景/背景整理清楚，很容易出现整图误检。

### 6.2 它是区域分析，不是子像素测量

做区域计数、形状筛选很合适；做高精度边缘定位和尺寸测量，就要考虑 [CaliperTool](./15-CaliperTool-技术笔记.md) 或 [SubpixelEdgeDetection](./07-SubpixelEdgeDetection-技术笔记.md)。

### 6.3 连通定义会影响“算几个目标”

如果两个缺陷在二值图里已经粘到一起，连通域就会把它们算成一个。

---

## 7. 失败案例与常见误区

### 案例 1：原图直接送入，整张图成了一个大 blob

这通常不是 blob 特征公式的问题，而是前景分割没做好。

### 案例 2：两个近邻缺陷被算成一个

根因通常是二值图里已经连通。

### 常见误区

- 误区一：BlobDetection 就是轮廓检测  
  它更偏向“连通域 + 区域特征”，轮廓只是其中一环。
- 误区二：带孔目标会被拆成多个 blob  
  当前实现不是这样。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/BlobDetectionOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/BlobAnalysis.md`
- OpenCV Documentation: connected components, contours, moments
- Gonzalez & Woods, *Digital Image Processing*, connected component labeling and morphology
- Szeliski, *Computer Vision: Algorithms and Applications*, region analysis sections

---

## 9. 一句话总结

`BlobDetection` 更像一个“基于连通域的区域分析器”：先把前景区域分出来，再用形状和灰度特征做筛选，而不是直接完成语义级识别。
