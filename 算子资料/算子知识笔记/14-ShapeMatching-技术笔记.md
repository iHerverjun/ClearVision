# ShapeMatching 技术笔记

> **对应算子**: `ShapeMatchingOperator`  
> **OperatorType**: `OperatorType.ShapeMatching`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/ShapeMatchingOperator.cs`  
> **相关算子**: [TemplateMatch](./13-TemplateMatch-技术笔记.md)、[FindContours](./12-FindContours-技术笔记.md)、[PerspectiveTransform](./18-PerspectiveTransform-技术笔记.md)  
> **阅读前置**: 先知道模板匹配怕旋转和尺度变化  
> **核心来源**: ClearVision 当前实现、金字塔 coarse-to-fine 搜索思想、经典模板匹配与几何搜索文献

---

## 1. 一句话先理解这个算子

`ShapeMatching` 可以先理解成“允许模板在角度和尺度上变化后再去找”的匹配。

---

## 2. 先说清当前实现口径

名字叫 `ShapeMatching`，但当前实现口径并不是 OpenCV 那种简单 `matchShapes` 轮廓距离比较。

当前实现更接近：

1. 模板来自输入端口或 `TemplatePath`
2. 搜索图和模板图都转灰度
3. 建立图像金字塔
4. 在不同层级上做角度与尺度搜索
5. 粗到细逐层收缩搜索范围
6. 最后做非极大值抑制，输出匹配列表

所以它更像一个**旋转尺度鲁棒的粗到细模板搜索器**，而不是狭义的“轮廓相似度评分器”。

---

## 3. 算法原理

### 3.1 为什么要做角度和尺度搜索

因为现实中目标很少永远保持同一个朝向和大小。  
如果直接用固定模板，会像 [TemplateMatch](./13-TemplateMatch-技术笔记.md) 那样在旋转或缩放变化时迅速失分。

### 3.2 为什么要做金字塔

直接在原图上把所有角度和尺度都搜一遍，成本很高。  
金字塔 coarse-to-fine 的好处是：

- 先在低分辨率上粗找
- 再在高分辨率上细化
- 这样比全量暴力搜索更实用

### 3.3 它和模板匹配的差异定位

- `TemplateMatch`: 固定模板快，但怕旋转缩放
- `ShapeMatching`: 更鲁棒，但计算更重、调参更复杂

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 搜索图 |
| `Template` 输入 | 模板图，可选 |
| `Image` 输出 | 画出匹配结果的图 |
| `Matches` 输出 | 匹配列表 |

### 4.2 关键参数怎么调

| 参数 | 默认值 | 怎么理解 |
|------|------|------|
| `MinScore` | `0.7` | 最低接受分数 |
| `MaxMatches` | `1` | 最多输出多少个匹配 |
| `AngleStart` | `-30` | 角度搜索起点 |
| `AngleExtent` | `60` | 角度搜索范围 |
| `AngleStep` | `1.0` | 角度步长 |
| `ScaleMin` | `1.0` | 最小尺度 |
| `ScaleMax` | `1.0` | 最大尺度 |
| `ScaleStep` | `0.1` | 尺度步长 |
| `NumLevels` | `3` | 金字塔层数 |

### 4.3 和教材口径不完全一样的地方

- 当前实现强调工程搜索流程，不是单一闭式“形状相似度公式”
- 最终结果会经过非极大值抑制，而不是简单取最高分
- 模板可以来自输入端口，也可以来自文件路径

---

## 5. 推荐使用链路与调参建议

### 5.1 适合什么任务

- 目标会旋转
- 目标会有一定尺度变化
- 外形相对稳定，但灰度纹理未必稳定

### 5.2 调参建议

- 角度范围不要无上限开大，先按现场允许姿态设范围
- `AngleStep` 越小越细，但计算量越大
- 金字塔层数不是越多越好，模板太小时层数太多反而没意义

---

## 6. 这个算子的边界

### 6.1 计算成本比普通模板匹配高

因为它本来就在做更大搜索空间。

### 6.2 不是任意形变都能抗

它能更好处理旋转和尺度变化，但对严重非刚性形变仍然有限。

### 6.3 模板质量依然重要

模板本身如果裁得不好、背景带太多、目标定义不清，鲁棒搜索也会受影响。

---

## 7. 失败案例与常见误区

### 案例 1：角度范围开到 360 度，结果速度明显掉下来

这是搜索空间变大的直接结果。

### 案例 2：觉得它叫 shape matching，就默认不看灰度模板质量

当前实现本质仍然是基于图像模板做旋转尺度搜索，不是纯轮廓数学距离。

### 常见误区

- 误区一：ShapeMatching 完全替代 TemplateMatch  
  如果目标本来很稳定，普通模板匹配更简单更快。
- 误区二：鲁棒就意味着不需要前处理  
  成像质量差时，任何匹配都会难受。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/ShapeMatchingOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/ShapeMatching.md`
- OpenCV Documentation: image pyramids, geometric transforms, template matching
- Szeliski, *Computer Vision: Algorithms and Applications*, coarse-to-fine search
- Bradski & Kaehler, *Learning OpenCV*, pyramids and matching

---

## 9. 一句话总结

`ShapeMatching` 的核心价值，是把“固定模板找位”推进到“允许旋转和尺度变化的搜索”，但代价是更高的计算成本和更复杂的参数空间。
