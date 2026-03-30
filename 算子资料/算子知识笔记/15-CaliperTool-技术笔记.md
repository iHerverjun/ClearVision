# CaliperTool 技术笔记

> **对应算子**: `CaliperToolOperator`  
> **OperatorType**: `OperatorType.CaliperTool`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/CaliperToolOperator.cs`  
> **相关算子**: [SubpixelEdgeDetection](./07-SubpixelEdgeDetection-技术笔记.md)、[CircleMeasurement](./16-CircleMeasurement-技术笔记.md)、[LineMeasurement](./17-LineMeasurement-技术笔记.md)  
> **阅读前置**: 先理解边缘、扫描线、宽度测量  
> **核心来源**: ClearVision 当前实现、工业视觉卡尺测量思想、亚像素边缘定位文献

---

## 1. 一句话先理解这个算子

`CaliperTool` 更像工业视觉里的一把“软件卡尺”：先沿着一条扫描线找边，再根据边对之间的距离去量宽度。

---

## 2. 先说清当前实现口径

当前实现不是整幅图全局测量，而是明确的**扫描线边缘对检测**：

1. 把输入转灰度
2. 解析 `SearchRegion`
3. 在区域内构建一条水平、垂直或自定义角度扫描线
4. 沿扫描线双线性采样灰度
5. 根据极性和阈值找边缘候选
6. 可选做亚像素细化
7. 按 `edge_pairs` 或 `single_edge` 组织结果

还要特别记住一个工程细节：

- 输出字典里的 `Width` 会被测量宽度覆盖，所以它不是“图像宽度”的意思，而是测量值

---

## 3. 算法原理

### 3.1 为什么卡尺工具常比整图检测更稳

因为它不是在整图里到处找，而是在**已知大致位置**上沿一条方向明确的线去找边。

这种思路有两个好处：

- 先缩小问题范围
- 再按边缘极性和距离关系做结构化判断

### 3.2 当前实现里边缘是怎么找的

它先沿扫描线采样强度曲线，再找强度变化明显的位置。  
如果开了 `SubpixelAccuracy`，还会继续细化边缘位置。

### 3.3 为什么它很适合宽度、间距测量

因为宽度本来就可以表达成“两条边之间的距离”。  
只要边找得稳、配对规则合理，卡尺测量会比整图盲搜更可控。

---

## 4. 参数说明

### 4.1 输入输出端口怎么理解

| 端口 | 作用 |
|------|------|
| `Image` 输入 | 原图 |
| `SearchRegion` 输入 | 可选搜索区域 |
| `Image` 输出 | 画了扫描线和边缘的结果图 |
| `Width` 输出 | 平均测得距离或宽度 |
| `EdgePairs` 输出 | 边缘点或边缘对 |
| `PairCount` 输出 | 有效边对数量 |
| `PairDistances` 输出 | 各边对距离 |

### 4.2 关键参数怎么调

| 参数 | 默认值 | 怎么理解 |
|------|------|------|
| `Direction` | `Horizontal` | 扫描方向 |
| `Angle` | `0.0` | 自定义方向时的角度 |
| `Polarity` | `Both` | 边缘极性要求 |
| `EdgeThreshold` | `18.0` | 灰度变化门槛 |
| `ExpectedCount` | `1` | 期望找到多少组边 |
| `MeasureMode` | `edge_pairs` | 是量边对，还是只看单边 |
| `PairDirection` | `any` | 边对极性顺序要求 |
| `SubpixelAccuracy` | `false` | 是否继续做亚像素细化 |

### 4.3 和教材口径不完全一样的地方

- 当前实现是“单扫描线 + 区域约束”的工业实用版本
- 它支持边缘极性和边对方向组合，这是工程上很有价值的约束
- 亚像素模式不是总是开启的，而是显式可选

---

## 5. 推荐使用链路与调参建议

### 5.1 什么时候优先用它

- 已知测量区域比较稳定
- 想量宽度、间距、边距
- 更关心少量高质量边，而不是全图大范围检测

### 5.2 调参建议

- 先把 `SearchRegion` 收紧
- 再明确 `Direction` 和 `Polarity`
- 最后再考虑要不要开亚像素

这通常比一上来全开参数更容易稳定。

---

## 6. 这个算子的边界

### 6.1 它依赖 ROI 和方向先验

如果目标位置漂得很厉害、方向也不稳定，卡尺工具就很难直接上手。

### 6.2 它本质是局部测量，不是全图搜索

别把它当成“万能检测器”。它擅长的是在已知区域里精确地量。

### 6.3 输入对比度差时很容易失稳

如果边缘本来就弱、反光又重，再精细的边对逻辑也会受影响。

---

## 7. 失败案例与常见误区

### 案例 1：搜索区域太大，边缘对乱配

这通常说明问题不是边缘阈值，而是 ROI 没有限好。

### 案例 2：方向设错了，结果边明明在图上却量不出来

卡尺本来就是“沿指定方向找边”，方向先验错了，后面全都白搭。

### 常见误区

- 误区一：卡尺工具就是一条直线版的边缘检测  
  它更强调边缘对、配对规则和测量结果。
- 误区二：只要开亚像素就一定更准  
  边缘候选本身不稳时，亚像素只会把不稳细化得更漂亮。

---

## 8. 专业来源与延伸阅读

- ClearVision 本地实现: `../../Acme.Product/src/Acme.Product.Infrastructure/Operators/CaliperToolOperator.cs`
- ClearVision 本地资料: `../算子手册.md`、`../算子名片/CaliperTool.md`
- 工业视觉测量常见卡尺思想与 scan-line edge methods
- Steger related subpixel edge literature
- OpenCV Documentation: interpolation, gradients, ROI handling

---

## 9. 一句话总结

`CaliperTool` 的价值不在“找更多边”，而在“沿正确的方向，在正确的区域，只量你真正关心的那几条边”。
