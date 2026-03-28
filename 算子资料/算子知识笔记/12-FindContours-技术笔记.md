# FindContours 技术笔记

> **对应算子**: `FindContoursOperator`  
> **OperatorType**: `OperatorType.ContourDetection`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/FindContoursOperator.cs`  
> **相关算子**: [Threshold](./04-Threshold-技术笔记.md)、[BlobDetection](./11-BlobDetection-技术笔记.md)、[ShapeMatching](./14-ShapeMatching-技术笔记.md)  
> **阅读前置**: 先知道轮廓是前景边界点集  
> **核心来源**: ClearVision 当前实现、OpenCV `findContours`、经典轮廓与矩方法教材

---

## 1. 一句话先理解这个算子

`FindContours` 的目标是把前景区域的边界提出来，让后面可以做面积、周长、外接框、形状拟合等分析。

---

## 2. 先说清当前实现口径

当前实现不是“输入已经是二值图就直接找轮廓”的纯粹模式，而是：

1. 先把输入转灰度
2. 再按算子参数做一次二值化
3. 调用 `Cv2.FindContours`
4. 按面积过滤
5. 可选绘制轮廓和中心点

这意味着：

- 即使你输入的是已经看起来像黑白图的三通道图，它仍会先走一遍灰度化和阈值化
- 输出的 `Contours` 不是原始点数组，而是当前实现整理过的一组轮廓摘要字典

---

## 3. 算法原理

### 3.1 轮廓是什么

轮廓可以理解成前景区域的边界点集。  
一旦有了轮廓，你就可以算：

- 面积
- 周长
- 外接矩形
- 质心
- 形状近似

### 3.2 检索模式为什么重要

当前算子主要暴露了：

- `External`
- `List`
- `Tree`

如果你只关心最外层边界，`External` 更简单。  
如果你想保留层级关系，比如外轮廓与内孔洞，`Tree` 会更合适。

### 3.3 它和 BlobDetection 的区别

- `FindContours` 更偏边界表示
- [BlobDetection](./11-BlobDetection-技术笔记.md) 更偏连通区域及特征筛选

工程里两者经常相关，但不是同义词。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 原图或待二值化图像 |
| `Image` 输出 | 绘制了轮廓的结果图 |
| `Contours` 输出 | 当前实现整理后的轮廓信息列表 |
| `ContourCount` 输出 | 过滤后轮廓数量 |

### 4.2 关键参数怎么调

| 参数 | 默认值 | 怎么理解 |
|------|------|------|
| `Mode` | `External` | 轮廓检索层级 |
| `Method` | `Simple` | 点集压缩方式 |
| `MinArea` | `100` | 最小面积过滤 |
| `MaxArea` | `100000` | 最大面积过滤 |
| `Threshold` | `127.0` | 如果输入不是标准二值图，决定分割线 |
| `ThresholdType` | `Binary` | 前景极性 |
| `DrawContours` | `true` | 是否绘制结果图 |

### 4.3 和教材口径不完全一样的地方

- 当前算子内部自带灰度化和阈值化
- 输出轮廓信息经过摘要化，不是直接暴露原始 OpenCV 点集
- 实现里支持 `flooded` 检索模式分支，但参数面板并未主推

---

## 5. 推荐使用链路与调参建议

### 5.1 常见链路

```text
Threshold / Morphology
  -> FindContours
  -> ShapeMatching / Measurement / Rule Filter
```

### 5.2 调参建议

- 如果只关心目标外边界，先用 `External`
- 如果需要研究孔洞层次或嵌套结构，再考虑 `Tree`
- 面积过滤永远是高性价比的第一步

---

## 6. 这个算子的边界

### 6.1 它依赖二值质量

轮廓不是从语义里长出来的，而是从前景边界里长出来的。二值化糟糕，轮廓自然也糟糕。

### 6.2 它更适合边界表达，不是完整判定方案

找到了轮廓，不等于你已经知道它是不是良品、是不是缺陷。

### 6.3 小毛刺会放大边界复杂度

当前输入如果有很多碎边、毛刺、锯齿，轮廓点数和边界统计都会被污染。

---

## 7. 失败案例与常见误区

### 案例 1：输入已经分割得不错，结果还想当然再加一次错误阈值

当前算子内部确实会再阈值化，所以如果参数不合适，反而可能破坏原来的前景。

### 案例 2：只想数目标数量，却死磕轮廓层次

很多计数场景直接用 [BlobDetection](./11-BlobDetection-技术笔记.md) 更自然。

### 常见误区

- 误区一：轮廓就是目标  
  轮廓只是目标边界的一种表示。
- 误区二：点越多越精确  
  更多点也可能只是更多噪声和毛刺。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/FindContoursOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/ContourDetection.md`
- OpenCV Documentation: *findContours*, contour approximation, moments
- Bradski & Kaehler, *Learning OpenCV*, contour analysis
- Szeliski, *Computer Vision: Algorithms and Applications*, shape and boundary analysis

---

## 9. 一句话总结

`FindContours` 擅长把前景区域翻译成“可分析的边界”，但它从来不独立承担分割质量和业务语义判断。
