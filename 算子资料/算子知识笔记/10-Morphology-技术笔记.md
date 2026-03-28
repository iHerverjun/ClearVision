# Morphology 技术笔记

> **对应算子**: `MorphologyOperator`（兼容旧流程） / `MorphologicalOperationOperator`（新流程推荐）  
> **OperatorType**: `OperatorType.Morphology` / `OperatorType.MorphologicalOperation`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/MorphologyOperator.cs`、`Acme.Product/src/Acme.Product.Infrastructure/Operators/MorphologicalOperationOperator.cs`  
> **相关算子**: [Threshold](./04-Threshold-技术笔记.md)、[AdaptiveThreshold](./05-AdaptiveThreshold-技术笔记.md)、[BlobDetection](./11-BlobDetection-技术笔记.md)  
> **阅读前置**: 先理解前景/背景二值图，会更容易理解腐蚀和膨胀  
> **核心来源**: ClearVision 当前实现、OpenCV morphology、Gonzalez & Woods《Digital Image Processing》

---

## 1. 一句话先理解这个算子

形态学不是在“改亮度”，而是在“按结构元素重塑前景区域的形状”。

---

## 2. 先说清当前实现口径

这里最重要的不是某个公式，而是当前仓库里有**两套口径**：

1. `MorphologyOperator`  
   旧节点，源码里明确标注为 **Legacy**，新流程不推荐优先使用
2. `MorphologicalOperationOperator`  
   新流程更推荐的形态学节点，参数更完整，支持独立宽高

`MorphologyOperator` 当前实现的特点：

- `KernelSize` 只有一个值，本质上是方形核
- 输出里会回传 `LegacyCompatible=true`
- 代码里会记录一次 legacy 使用警告

所以如果你在读旧流程，看到 `Morphology` 很正常；但如果你在搭新流程，更应该知道平台已经把它视为兼容保留节点。

---

## 3. 算法原理

### 3.1 最核心的两个操作

形态学里最基础的是：

- **腐蚀 Erode**：前景缩小，能去掉细小突出和小噪点
- **膨胀 Dilate**：前景扩张，能补小缺口、连近邻点

在这两者之上，常见组合有：

- **开运算 Open** = 先腐蚀再膨胀
- **闭运算 Close** = 先膨胀再腐蚀
- **Gradient** = 膨胀减腐蚀，强调边界
- **TopHat / BlackHat** = 用于提取局部亮结构或暗结构

### 3.2 为什么它经常出现在二值化后面

因为 [Threshold](./04-Threshold-技术笔记.md) 或 [AdaptiveThreshold](./05-AdaptiveThreshold-技术笔记.md) 往往会留下：

- 毛刺
- 小洞
- 小碎片
- 粘连

形态学就是处理这些“区域形状问题”的常用工具。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 通常是二值图或已分割好的图像 |
| `Image` 输出 | 形态学处理后的图像 |

### 4.2 关键参数怎么调

旧 `MorphologyOperator` 的常见参数：

| 参数 | 默认值 | 说明 |
|------|------|------|
| `Operation` | `Erode` | 腐蚀、膨胀、开闭等操作类型 |
| `KernelSize` | `3` | 方形核边长 |
| `KernelShape` | `Rect` | 结构元素形状 |
| `Iterations` | `1` | 重复执行次数 |
| `AnchorX/Y` | `-1` | 锚点，`-1` 表示用核中心 |

### 4.3 和教材口径不完全一样的地方

- 平台里存在 legacy 与新节点并行的兼容现实
- 旧节点的核大小是单值，新节点允许宽高分开
- 这里更强调“工程里的链路位置”，而不是只谈数学定义

---

## 5. 推荐使用链路与调参建议

### 5.1 最常见链路

```text
Threshold / AdaptiveThreshold
  -> Morphology
  -> BlobDetection / FindContours
```

### 5.2 调参建议

- 去小白点、小毛刺：优先试开运算
- 补小裂口、小黑洞：优先试闭运算
- 如果两个目标被粘连，盲目膨胀通常只会更糟
- 核大小不要脱离目标尺寸去设，小结构前景很容易被大核吃掉

---

## 6. 这个算子的边界

### 6.1 它只会按结构元素改形状

形态学并不理解“这是缺陷还是目标”，它只是按核和规则去改前景区域。

### 6.2 它不能弥补错误分割

如果前景背景一开始就分得很差，形态学最多是补救，不会把错误世界变成正确世界。

### 6.3 过度使用会把真实结构改坏

尤其是细线、细孔、细裂纹，很容易在腐蚀或大核开运算中丢失。

---

## 7. 失败案例与常见误区

### 案例 1：为了去噪开运算，结果把小缺陷也一起去掉

这说明你的结构元素尺寸已经接近甚至超过目标本身。

### 案例 2：两个相邻目标本来分开，闭运算后粘成一片

闭运算的本质就是补缝、连近邻，出现这种结果并不奇怪。

### 常见误区

- 误区一：形态学只是“后处理小修小补”  
  实际上它经常决定后面 Blob 和轮廓结果稳不稳。
- 误区二：开闭运算是固定最佳实践  
  什么时候开、什么时候闭，要看你是想去掉小突起，还是想补小断裂。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/MorphologyOperator.cs`
- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/MorphologicalOperationOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/Morphology.md`
- OpenCV Documentation: morphological transformations
- Gonzalez & Woods, *Digital Image Processing*, morphology chapters

---

## 9. 一句话总结

`Morphology` 的真正价值，是把二值分割之后那些“形状上不干净”的前景区域修到更适合后续分析，但它必须建立在你知道自己到底想保留什么、去掉什么的前提上。
