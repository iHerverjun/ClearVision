# TemplateMatch 技术笔记

> **对应算子**: `TemplateMatchOperator`  
> **OperatorType**: `OperatorType.TemplateMatching`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/TemplateMatchOperator.cs`  
> **相关算子**: [ShapeMatching](./14-ShapeMatching-技术笔记.md)、[CaliperTool](./15-CaliperTool-技术笔记.md)、[ColorConversion](./08-ColorConversion-技术笔记.md)  
> **阅读前置**: 先理解“模板就是一块小图”  
> **核心来源**: ClearVision 当前实现、OpenCV `matchTemplate`、经典模板匹配教材

---

## 1. 一句话先理解这个算子

`TemplateMatch` 做的事可以理解成：拿一张小模板，在大图上滑来滑去，看哪里最像。

---

## 2. 先说清当前实现口径

ClearVision 当前实现是标准灰度模板匹配路线：

1. 输入图和模板图都必须提供
2. 两者都会先转灰度
3. 调用 `Cv2.MatchTemplate`
4. 根据匹配方法解释分数
5. 通过反复 `MinMaxLoc + 局部抑制` 提取多个匹配

这里最值得记住的实现细节有：

- 当前匹配是在灰度上做的，不是彩色相关
- 如果方法是 `SqDiff / SqDiffNormed`，代码会把分数转成 `1 - minVal`
- 多目标输出不是复杂 NMS，而是较轻量的局部抑制窗口策略

---

## 3. 算法原理

### 3.1 模板匹配到底在比什么

它比较的是搜索图局部窗口与模板图在某种相似度度量下的接近程度。  
常见方法包括相关、归一化相关、平方差等。

### 3.2 为什么它简单但好用

如果目标外观稳定、角度不怎么变、尺度也差不多，模板匹配非常直观，而且部署成本低。

### 3.3 它和形状匹配的区别

- `TemplateMatch` 看的是灰度模板相似性
- [ShapeMatching](./14-ShapeMatching-技术笔记.md) 更强调旋转和尺度鲁棒

如果目标角度和大小变化明显，模板匹配会更快暴露短板。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 搜索图像 |
| `Template` 输入 | 模板小图 |
| `Image` 输出 | 绘制匹配框后的结果图 |
| `Position` 输出 | 最佳匹配中心点 |
| `Score` 输出 | 最佳匹配分数 |
| `Matches` 输出 | 匹配列表 |
| `MatchCount` 输出 | 匹配数量 |

### 4.2 关键参数怎么调

| 参数 | 默认值 | 怎么理解 |
|------|------|------|
| `Method` | `CCoeffNormed` | 相似度计算方式 |
| `Threshold` | `0.8` | 保留匹配的最低分数 |
| `MaxMatches` | `1` | 最多输出多少个匹配 |

### 4.3 和教材口径不完全一样的地方

- 当前实现自动转灰度，简化了多通道问题
- 多匹配提取用了轻量抑制区域，而不是单独独立的 NMS 算子
- 输出里除了最佳匹配，还会带模板宽高等附加信息

---

## 5. 推荐使用链路与调参建议

### 5.1 适合什么任务

- 固定外观目标定位
- 目标角度变化小、尺度变化小
- 规则件找位、找标记、找局部图案

### 5.2 常见链路

```text
Gray / Normalize
  -> TemplateMatch
  -> Position / Score / Rule Check
```

如果目标旋转和缩放变化明显，就要尽快考虑 [ShapeMatching](./14-ShapeMatching-技术笔记.md)。

---

## 6. 这个算子的边界

### 6.1 它天然怕旋转和缩放

当前实现没有帮你做旋转、尺度搜索，这是模板匹配最典型的边界。

### 6.2 它依赖亮度和纹理稳定

因为当前是灰度匹配，光照变化、局部反光、模板过时都会直接影响分数。

### 6.3 它不擅长严重形变

目标如果外形已经变形，模板匹配往往会直接失败。

---

## 7. 失败案例与常见误区

### 案例 1：同一个目标转了几度，分数就明显掉

这不是算法出 bug，而是模板匹配本来就不天然旋转不变。

### 案例 2：模板图比源图还大

当前实现会直接判定失败，不会自动缩放模板。

### 常见误区

- 误区一：模板匹配就是“万用定位”  
  它很适合稳定模板，不适合大变形大旋转。
- 误区二：分数低一定是阈值设错了  
  很多时候是模板本身已经不适合当前现场。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/TemplateMatchOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/TemplateMatching.md`
- OpenCV Documentation: *matchTemplate*
- Bradski & Kaehler, *Learning OpenCV*, template matching chapters
- Szeliski, *Computer Vision: Algorithms and Applications*, correlation and matching sections

---

## 9. 一句话总结

`TemplateMatch` 非常适合做“外观稳定的小图找位”，但一旦目标开始旋转、缩放、变形，它就应该让位给更鲁棒的匹配或测量方法。
