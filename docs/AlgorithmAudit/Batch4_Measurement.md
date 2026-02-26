# 算法深度审计报告 - 第 4 批：Blob 分析、轮廓与几何测量

**审计时间**：2026-02-26
**算子数量**：14（Blob检测、Blob标注、宽度、间隙、角度、点线距、线线距、轮廓测量、几何公差、坐标转换、统计等）
**主要职责**：工业测量、缺陷区域提取、坐标系标定基础。

## 总体评价
本批次算子完成了基本的业务场景覆盖（测宽、距、角度、抓缺陷），**封装较为完整**。但在**几何异常处理（退化情形）**、**子像素精度缺失**、以及**架构模块化解耦**方面，存在一些显著的不足，需要专门的优化才能满足高要求的高端制造标准。

本批次暂未发现绝对的“死锁”和 P0 级严重异常，但发现若干 P1（数值稳定性漏洞）及 P2（架构冗余）问题，需要集中治理。

---

## 核心发现与建议 (按严重程度分类)

### 🔴 P1 级缺陷：数值稳定性与退化情形处理漏洞
#### 1. `GapMeasurementOperator` 均值与方差计算容易产生高溢出或被噪声支配
- **现状**：在基于图像投影（`ComputeGapsFromImage`）计算间隙时，仅仅计算了 1D 投影分布并在 `FindFeaturePositions` 中用基础均值+0.5倍标准差卡阈值。
- **问题**：在工业打光不匀或背景有复杂划痕时，全图均值和标准差极易收到极端离群点（Outliers）影响，导致卡出的特征位置错乱，后续量出的 Gap 完全不可用。
- **NotebookLM 建议原则**：采用“中值绝对偏差”（MAD）或设定自适应局部阈值替代全局统计量进行鲁棒性特征提取。

#### 2. `LineLineDistanceOperator` 中平行度容差及交点异常
- **现状**：算子在算线段夹角时 `Math.Acos(cosTheta)`，若在极端精度下舍入误差使 `cosTheta > 1`（虽然写了 `Math.Clamp`），但后续交点计算 `SolveIntersection` 虽然判定了平行线，但直接将交点置为中点 `new Position((l1.MidX...))`。这在逻辑语义上“平行线强行造一个交点”是不合常理的。
- **NotebookLM 建议原则**：在 `IsParallel == true` 且有交点需求时，应当返回 null、NaN、或设置对应的标志位，而非造假数据送给下游。

#### 3. `GeometricToleranceOperator` 对于平行度/垂直度的严谨性不足
- **现状**：当前计算两线平行度时，仅仅计算了两条线段夹角。
- **问题**：在真正的 GD&T（几何尺寸与公差）标准中，平行度（Parallelism）不仅是角度，而是线段端点到基准线的“最大距离跳动公差带”。目前的实现严格来说只是“计算两线夹角”，在功能命名上有误导。
- **NotebookLM 建议原则**：重命名为基于角度的“简单夹角宽容度”，或真正实现包络区域公差。

### 🟡 P2 级缺陷：缺乏亚像素精度支撑与架构耦合
#### 1. 均缺乏基于梯度的亚像素插值
- **现状**：无论是 `WidthMeasurementOperator` 中的 `ProjectPointToLine` 然后做线段裁剪，还是 `GapMeasurementOperator` 找投影波峰，完全依赖整像素坐标（`int` 累加）。
- **问题**：在精密检测中，1 像素可能对应 10微米，不使用抛物线插值/高斯拟合找峰值（如 `CaliperToolOperator` 中做的那样），直接使测量精度锁死在整像素，失去了高精度视觉的意义。
- **NotebookLM 建议原则**：将 `CaliperTool` 中的一维边缘提取与亚像素拟合能力抽象为一个公共模块（`EdgeLocatorService`），让所有测量算子（Width, Gap）复用。

#### 2. `ContourMeasurementOperator` 算子职能与 `FindContoursOperator` 重复
- **现状**：`ContourMeasurementOperator` 内部包含了完整的二值化、找轮廓、然后算面积周长的逻辑。
- **问题**：架构耦合度高。上游如果已经做了形态学或者高级寻找轮廓，结果无法传给它。这违反了“数据对象（Contour）在流水线中流动，而非重新计算”的原则。
- **NotebookLM 建议原则**：应当利用上游 `FindContoursOperator` 的输出，本算子退化为只负责遍历 Contour 数组计算特征（属性），而不是从头读图像。

#### 3. `StatisticsOperator` 的历史缓存机制在无状态工作流中有引发内存泄漏的隐患
- **现状**：`private readonly List<double> _historyValues = new();` 和 `lock (_historyValues)`，这保留了一个最大容量 1000 的数组。
- **问题**：如果用户频繁拖拽画布、重启测绘循环、或者工作流处于并行多实例状态（例如跑 4 个相机），这个单例/静态级别的 History 会导致数据串线，且无法正确 Reset。
- **NotebookLM 建议原则**：统计信息的累加应托管给统一的 `ResultMemoryManager`，当前算子只做纯函数计算，不要在 Operator 类型内部维持有状态的大数组。

---

## 逐一算子审查档案

### 1. `BlobDetectionOperator` (Blob 连通域提取)
- **发现**：直接使用了 OpenCV 的 `SimpleBlobDetector`。速度不错。但是其 `Blobs` 输出重新用字典封装了。
- **建议**：符合标准。但在小面积滤波下容易受到噪点干扰，可建议用户在前面加入 `MorphologyOperator`。

### 2. `BlobLabelingOperator` (Blob 标注)
- **发现**：代码很不错，有 Otsu 二值化、有轮廓特征（圆形度、长宽比）计算分类。
- **建议**：颜色生成使用 `hash % 180`，非常巧妙。是一个成熟的展示级算子。保留即可。

### 3. `WidthMeasurementOperator` (宽度测量)
- **发现**：通过法线切片和霍夫线提取平行线。但自定义的平行判断 `AngleDiffDeg(a.Angle, b.Angle) > 10` 太粗糙，且后续采样的端点容易在复杂边缘跳动。
- **建议**：提升找边的亚像素精度能力。

### 4. `GapMeasurementOperator` (间隙测量)
- **发现**：`ComputeGapsFromImage` 利用 1D 平均投影找极值。P1 级稳健性问题。
- **建议**：引入更高级的一维平滑（如 1D 高斯）再提取波峰；在点表集合测间距时，应该支持曲线按弧长求间距，而不仅仅是 XY 投影。

### 5. `AngleMeasurementOperator` (三点测角)
- **发现**：纯数学公式算子 `Math.Atan2`。
- **建议**：无问题，实现轻量清晰。

### 6. `MeasureDistanceOperator` & `PointLineDistanceOperator` & `LineLineDistanceOperator`
- **发现**：测量基础库。包含点到点、点到线、线到线。代码严密（检查了 `len < 1e-9` 防止除零异常）。
- **建议**：`LineLineDistance` 将平行线距直接取中点到线距，这是正确的。无需大修。

### 7. `CoordinateTransformOperator` (像素到物理坐标系)
- **发现**：支持简单的缩放与平移（$x_p = origin * scale_p$）。
- **建议**：不够工业化。缺少齐次变换矩阵支持，且不支持相机的内参畸变矫正支持（Distortion）。这留待之后在“标定（Calibration）”类别里深入。当前算简单仿射。

### 8. `GeoMeasurementOperator` (综合几何距)
- **发现**：一个全能解析器，实现了点/线/圆两两组合的所有距离与交点。包括了复杂的直线求交、线圆求交和圆圆求交公式，解析数学功底过硬。
- **建议**：极其出色的数学工具类，建议作为底层 `MathUtils` 单独抽离，让其他算子复用。

### 9. `GeometricToleranceOperator` (几何公差)
- **发现**：实现仅为夹角计算，非真实公差带分布。
- **建议**：参考 P1 建议重命名或深化算法。

### 10. `StatisticsOperator` (统计运算 CPK 等)
- **发现**：P2 级带状态（Stateful）的安全隐患。
- **建议**：必须转移状态数据到专门的执行上下文（Context）中，避免多线程/复位的严重 Bug。

---

## 总结与后续动作
- **优先处理（P1）**：修正 `GapMeasurementOperator` 中的 1D 投影阈值提取算法抗噪能力，并处理好 `LineLineDistance` 退化几何实体的返回规范；`GeometricToleranceOperator` 应在界面上明确标出仅为“角度公差”。
- **重构建议（P2）**：抽离 `GeoMeasurementOperator` 里的解析基础几何函数至 `ClearVision.Core.Math` 模块。消除 `StatisticsOperator` 中的类内部有状态数组。
- **下一阶段**：完成审计后统一实施修复。继续推进第 5 批：标定与其他高级成像增强算子（10 个算子）。
