# BlobDetection 技术笔记

> **对应算子**: `BlobDetectionOperator` / `OperatorType.BlobAnalysis`  
> **代码依据**: `Acme.Product/src/Acme.Product.Infrastructure/Operators/BlobDetectionOperator.cs`  
> **相关算子**: `BlobLabelingOperator`、`FindContoursOperator`  
> **用途**: 面试准备，重点覆盖原理、参数、边界、失败案例，以及仓库当前实现的真实口径。

---

## 1. 先说清当前实现口径

面试里不要把这个算子简单说成“基于 `findContours`”或者“基于 `SimpleBlobDetector`”。

**当前主执行路径**是：

1. 输入图像转灰度
2. 二值化得到前景/背景
3. `ConnectedComponentsWithStats` 做 8 邻域连通域标记
4. 对每个 label 提取局部 mask
5. 再用 `FindContours(..., RetrievalModes.CComp, ...)` 提取该 blob 的外轮廓和孔洞层次
6. 计算面积、周长、圆度、凸度、矩形度、离心率、欧拉数、灰度统计等特征
7. 依据参数和表达式过滤，输出 `Blobs`、`BlobCount` 和可选 `BlobFeatures`

**仓库里确实还保留了 `ExecuteCoreAsync_Legacy` 旧分支**，那条路径用的是 `SimpleBlobDetector`。但它不是当前执行入口，面试时应以当前主路径为准。

---

## 2. 算法原理

### 2.1 核心思路

BlobDetection 的本质不是“识别某个具体类别”，而是把图像中满足连通性的前景区域提取出来，再对每个区域计算几何和灰度特征，供后续筛选、计数或判定使用。

在 ClearVision 当前实现中，算子本质上做了两层工作：

1. **连通域提取**
   把前景像素按 8 邻域聚成若干个 connected component。
2. **区域特征分析**
   对每个 component 计算面积、周长、圆度、凸度、矩形度、离心率、孔洞数、灰度均值等特征，再按阈值过滤。

### 2.2 当前代码里的处理流程

```text
Image
  -> 灰度化
  -> 二值化
  -> 可选黑白反转
  -> 可选 HSV 颜色预过滤
  -> ConnectedComponentsWithStats
  -> 对每个 label 生成局部 mask
  -> FindContours(CComp)
  -> 计算特征
  -> 参数过滤 / FeatureFilter 过滤
  -> 绘制轮廓和中心点
  -> 输出 BlobCount / Blobs / BlobFeatures
```

### 2.3 关键特征是怎么计算的

#### 面积

- 来自 `ConnectedComponentsWithStats` 的 `Area`
- 表示该连通域的前景像素数

#### 周长

- 来自外轮廓的 `Cv2.ArcLength(contour, true)`

#### 圆度 `Circularity`

公式：

```text
Circularity = 4πA / P²
```

其中：

- `A` 是轮廓面积
- `P` 是轮廓周长

当前实现里为了降低栅格化带来的锯齿影响，会先对轮廓做一次 `ApproxPolyDP`，再用近似后的周长计算圆度，因此圆形的圆度更接近理论值。

#### 凸度 `Convexity`

公式：

```text
Convexity = ContourArea / ConvexHullArea
```

如果轮廓接近凸形，凸度会接近 1；有明显凹陷时会下降。

#### 矩形度 `Rectangularity`

公式：

```text
Rectangularity = ContourArea / (BoundingRect.Width * BoundingRect.Height)
```

注意这里用的是**轴对齐外接矩形**，不是最小旋转外接矩形，因此旋转目标的矩形度会被低估。

#### 离心率 `Eccentricity` 与惯性比 `InertiaRatio`

当前实现不是直接 `FitEllipse`，而是基于中心矩求二阶矩阵特征值：

- `lambda1` 为主轴方向特征值
- `lambda2` 为副轴方向特征值
- `InertiaRatio = lambda2 / lambda1`
- `Eccentricity = sqrt(1 - InertiaRatio)`

所以：

- 越接近圆，`InertiaRatio` 越接近 1，`Eccentricity` 越接近 0
- 越细长，`InertiaRatio` 越小，`Eccentricity` 越接近 1

#### 孔洞数与欧拉数 `EulerNumber`

这是面试里最容易被追问的点。

当前实现做法是：

1. 先对单个 blob 的 mask 做 `FindContours(..., RetrievalModes.CComp, ...)`
2. 找到外轮廓
3. 统计它的子轮廓个数作为孔洞数 `HoleCount`
4. 计算：

```text
EulerNumber = 1 - HoleCount
```

因此：

- **一个带孔的目标，当前实现仍然算 1 个 blob**
- 孔不会被拆成多个 blob
- 但会通过 `HoleCount` 和 `EulerNumber` 反映其拓扑特征

这也是回答“螺丝中间有孔会算一个还是多个 blob”时最关键的口径。

#### 灰度均值与灰度波动

如果提供了 `SourceImage`，当前实现会在原图灰度图上、用该 blob 的 mask 统计：

- `MeanGray`
- `GrayDeviation`

这样可以把“检测用二值 mask”和“测量用原图”分开，避免直接在二值图上做灰度统计。

---

## 3. 参数说明

### 3.1 基础筛选参数

| 参数 | 作用 | 面试里怎么解释 |
|------|------|------|
| `MinArea` | 最小面积阈值 | 先把噪点和微小碎片过滤掉，是第一道筛选 |
| `MaxArea` | 最大面积阈值 | 防止整片背景或大块误分割被当成目标 |
| `Color` | 目标前景颜色，`White` 或 `Black` | 不是颜色识别，而是决定二值图里前景是白还是黑；`Black` 本质上是对二值结果做反转 |

### 3.2 形状筛选参数

| 参数 | 作用 | 典型用途 |
|------|------|------|
| `MinCircularity` | 最小圆度 | 保留近圆形目标，过滤毛刺和不规则噪点 |
| `MinConvexity` | 最小凸度 | 去掉有明显凹陷、破损、裂口的区域 |
| `MinInertiaRatio` | 最小惯性比 | 过滤过细长的目标 |
| `MinRectangularity` | 最小矩形度 | 保留接近矩形的区域 |
| `MinEccentricity` | 最小离心率 | 反向保留细长目标，过滤过圆目标 |

### 3.3 输出控制参数

| 参数 | 作用 | 注意点 |
|------|------|------|
| `OutputDetailedFeatures` | 是否输出 `BlobFeatures` | 需要更完整特征时打开，否则只看 `Blobs` 即可 |
| `FeatureFilter` | 用表达式做二次筛选 | 支持类似 `Area > 500 AND Circularity < 0.8`，表达式非法会直接报错失败 |

### 3.4 颜色预过滤参数

| 参数 | 作用 | 注意点 |
|------|------|------|
| `EnableColorFilter` | 是否启用 HSV 预过滤 | 对彩色场景有用，对灰度图无效 |
| `HueLow` / `HueHigh` | 色相范围 | OpenCV 的 H 通常是 0~180 |
| `SatLow` / `SatHigh` | 饱和度范围 | 太宽会失去过滤意义 |
| `ValLow` / `ValHigh` | 明度范围 | 受现场光照影响很大 |

### 3.5 端口理解

| 端口 | 用途 |
|------|------|
| `Image` | 主输入，通常应该是已经做过阈值化或分割后的图像 |
| `SourceImage` | 可选原图，用于绘制结果和计算灰度统计 |
| `Blobs` | 每个 blob 的特征字典列表 |
| `BlobFeatures` | 打开 `OutputDetailedFeatures` 时输出详细特征 |
| `BlobCount` | 通过筛选后的 blob 数量 |

---

## 4. 这个算子的边界

### 4.1 最大的边界：它严重依赖前景分割质量

当前实现里，主路径的二值化是：

```text
Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary)
```

这意味着：

- 灰度值 `> 0` 的像素都会被当成前景
- 如果输入不是已经接近二值的图，而是普通灰度图，极容易“整张图几乎全白”

所以这个算子**更适合接在 Threshold / AdaptiveThreshold / Morphology 之后**，而不是直接吃原始工业图像。

### 4.2 它是像素级区域算子，不是子像素测量算子

- 它能做区域提取、计数和粗几何特征判断
- 但不适合直接承担高精度边缘定位、尺寸计量
- 真正的高精度测量应交给 `CaliperTool`、`SubpixelEdgeDetection`、几何拟合类算子

### 4.3 它默认按 8 邻域合并连通区域

这意味着：

- 两个目标如果仅通过细桥、噪声或粘连像素连在一起，可能会被算成一个 blob
- 这对缺陷团聚、脏污、毛刺粘连场景会有影响

### 4.4 旋转目标的矩形度会失真

当前矩形度基于轴对齐 `BoundingRect`，因此：

- 正放矩形矩形度高
- 旋转矩形即便本质上很规整，矩形度也会下降

如果面试官问到这一点，应该主动承认这是当前实现的简化。

### 4.5 颜色过滤不是“识别某颜色的 blob”

`Color` 参数和 `EnableColorFilter` 容易混淆：

- `Color=White/Black` 只是定义二值前景极性
- 真正的颜色范围过滤来自 `EnableColorFilter + HSV 范围`

因此不能把 `Color` 说成“支持颜色 blob 检测”。

### 4.6 这个算子更像“Blob 分析”，不是完整缺陷检测方案

它只能回答：

- 有多少个连通区域
- 每个区域多大、形状怎么样、灰度统计如何

它不能单独回答：

- 这是不是某个语义缺陷
- 这个区域是否满足工艺逻辑
- 多个 blob 之间的复杂关系是否合理

这类判断通常要靠：

- 上游更稳定的前景提取
- 下游规则判定
- 或深度学习 / 模板匹配 / 逻辑门组合

---

## 5. 失败案例

### 案例1：原图直接送入，整图被误检成一个大 blob

**现象**:

- `BlobCount` 变成 1
- 这个 blob 面积特别大
- 几乎覆盖整张图

**原因**:

- 当前主路径阈值是 `Threshold(..., 0, 255, Binary)`
- 普通灰度图里只要不是纯黑，基本都会变成前景

**怎么回答 / 怎么修**:

- 这是前景分割没做好，不是 blob 特征计算本身的问题
- 上游应先做阈值化、背景扣除、ROI 裁剪、形态学清理

### 案例2：两个相邻缺陷被当成一个 blob

**现象**:

- 明明有两个独立缺陷，结果只输出一个 blob

**原因**:

- 二者在二值图里已经连通
- 8 邻域连通域会把它们并成一个区域

**怎么回答 / 怎么修**:

- 这是连通域定义带来的天然边界
- 可通过腐蚀、距离变换、分水岭或更细的 ROI 分割来拆开

### 案例3：有孔的目标被问“算一个还是多个”

**现象**:

- 比如垫圈、螺母、带孔金属件

**当前实现口径**:

- **算一个 blob**
- 孔洞通过 `HoleCount` 和 `EulerNumber` 描述，而不是拆成多个 blob

**面试回答建议**:

- 我们先用连通域定义“一个前景区域算一个目标”
- 再用 `FindContours(CComp)` 统计孔洞层次
- 所以“带孔”是一个 blob，但它的拓扑特征会变化

### 案例4：旋转矩形被误判为“矩形度不够”

**现象**:

- 目标肉眼看是标准矩形
- 但 `Rectangularity` 低于预期

**原因**:

- 当前实现用的是轴对齐外接框
- 目标一旦旋转，外接框面积被放大，矩形度下降

**怎么回答 / 怎么修**:

- 这是当前实现的几何近似边界
- 若业务依赖旋转不变性，应改用最小外接旋转矩形或主方向归一化

### 案例5：灰度图开启颜色过滤没有效果

**现象**:

- 打开 `EnableColorFilter` 后，结果几乎没变化

**原因**:

- 当前 `ApplyColorFilter` 只对 3 通道彩色图有效
- 灰度图会直接返回 `null`，等价于没有做颜色预过滤

**怎么回答 / 怎么修**:

- 颜色过滤只适用于彩色源图
- 如果输入已经是灰度或二值图，这个功能不生效是预期行为

### 案例6：`FeatureFilter` 写错导致算子直接失败

**现象**:

- 执行时报 `FeatureFilter invalid`

**原因**:

- 当前实现通过 `DataTable.Compute` 解析表达式
- 字段名、逻辑运算符或语法写错会直接报错

**怎么回答 / 怎么修**:

- 表达式过滤功能很灵活，但也带来运行时语法风险
- 更稳妥的做法是把高频过滤条件做成结构化参数，而不是全靠字符串表达式

---

## 6. 面试里最容易被问的几个点

### 6.1 你们是基于 `findContours` 还是自己实现的连通域分析？

建议回答：

> 当前主路径是 `ConnectedComponentsWithStats` 做连通域标记，再对单个 blob 的 mask 用 `FindContours(CComp)` 计算轮廓和孔洞层次。也就是说，连通域提取和轮廓特征提取两步都用了，但主入口是连通域分析，不是单纯全图 `findContours`。

### 6.2 带孔的 blob 算一个还是多个？

建议回答：

> 当前实现算一个。因为我们先按前景连通性定义 blob，孔洞是背景，不会拆成多个目标；但我们会通过 `HoleCount` 和 `EulerNumber` 把这个拓扑信息保留下来。

### 6.3 圆度、矩形度、离心率分别适合干什么？

建议回答：

> 圆度适合筛近圆目标，矩形度适合筛规整矩形目标，离心率适合区分细长目标和接近圆的目标。三者组合起来，能比单纯面积阈值更稳定地过滤噪点和异形区域。

### 6.4 这个算子最大的工程风险是什么？

建议回答：

> 最大风险不是特征公式本身，而是前景分割质量。当前实现默认阈值非常保守，如果把原图直接喂进来，很容易整图变成一个大 blob。所以它必须放在合适的阈值化、ROI 和形态学清理后面用。

---

## 7. 一句话总结

`BlobDetection` 在 ClearVision 当前实现里，本质上是一个**基于连通域的区域分析算子**：先提区域，再算形状和灰度特征，再做筛选。它适合做缺陷区域计数、形状筛选和规则判定前置，不适合单独承担高精度测量或语义级识别。
