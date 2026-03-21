# 算法深度审计报告 - 第 2 批：边缘检测与亚像素处理

## 审计概述
- **审计算子数量**：10 个
- **涉及范围**：Canny 边缘、亚像素边缘（Steger/拟合）、拉普拉斯锐化、角点检测（Harris/Shi-Tomasi）、边线交点、边缘对缺陷（卡尺变体）、卡尺工具、平行线查找、直线测量（Hough）、形态学操作。
- **审计基准**：NotebookLM 工业机器视觉原理最佳实践（高精度测量环节）。

---

## 算子详细审计

### 1. `CannyEdgeOperator` (Canny边缘检测)
- **科学性**：4/5。标准 OpenCV Canny 实现。符合 NotebookLM 建议的“预先高斯模糊”流程。
- **性能效率**：4/5。
- **参数合理性**：4/5。暴露了 L2Gradient 选项，以及孔径大小。
- **鲁棒性**：5/5。兼容多通道输入，自动转灰度。
- **工程质量**：5/5。
- **改进建议**：缺乏自动计算上下阈值的机制（如基于中值或平均值的 Otsu/自适应 Canny），目前依然依赖人工硬编码 `Threshold1` 和 `Threshold2`。建议引入自适应阈值计算以减少工厂环境光照变化带来的影响。

### 2. `SubpixelEdgeDetectionOperator` (亚像素边缘提取)
- **科学性**：4/5。提供了Steger、GradientInterp（梯度插值）、GaussianFit（高斯拟合）三种亚像素方法，这在工业测量中属于顶级配置要求。
- **性能效率**：3/5。如果配置为 Steger 模式，调用了外部自定义的 `StegerSubpixelEdgeDetector`（需审查该类的具体 C# 实现是否高效）。如果是传统方法，存在大量 C# 层面的逐像素遍历 `BilinearInterpolate`。
- **参数合理性**：5/5。提供了 Sigma 和边缘强度的控制。
- **鲁棒性**：4/5。边界检查 `x <= 0 || x >= gray.Width - 1` 有点粗糙，但基本安全。
- **工程质量**：3/5。内部 `BilinearInterpolate` 双线性插值在 C# 层执行效率极低。
- **改进建议**：
  1. 传统方法的 `BilinearInterpolate` 应采用 OpenCV 原生 `Remap` 或多通道合并运算来提升速度。
  2. NotebookLM 中提到，Zernike 矩方法在应对高噪声时的亚像素精度更稳定，目前体系中缺失 Zernike 矩亚像素算子，建议未来补充。

### 3. `LaplacianSharpenOperator` (拉普拉斯锐化)
- **科学性**：4/5。标准的 Laplacian 叠加 `src + strength * laplacian` 锐化滤波。
- **性能效率**：4/5。
- **参数合理性**：4/5。
- **鲁棒性**：3/5。直接叠加没有进行边界截断（Clamp）保护，虽然 OpenCV 的 `AddWeighted` 可能底层做了 saturate_cast，但在某些特定数据类型下需验证是否会溢出。
- **工程质量**：4/5。
- **改进建议**：在工业中，Laplacian 极易放大高频噪声。建议在文档和界面上提醒操作人员“先去噪，后锐化”。

### 4. `CornerDetectionOperator` (角点检测)
- **科学性**：5/5。同时支持 Harris 和 Shi-Tomasi，并自带 `CornerSubPix` 亚像素级角点精化，科学性极佳。
- **性能效率**：5/5。
- **参数合理性**：5/5。
- **鲁棒性**：4/5。
- **工程质量**：5/5。

### 5. `EdgeIntersectionOperator` (边线交点)
- **科学性**：5/5。标准的向量代数相交计算，包含共线与平行的判定。
- **性能效率**：5/5。纯数学运算，O(1)。
- **参数合理性**：5/5。
- **鲁棒性**：5/5。分母极小值校验 `Math.Abs(denominator) > 1e-9` 规范。
- **工程质量**：5/5。

### 6. `EdgePairDefectOperator` (边缘对缺陷判断)
- **科学性**：5/5。用于检测两线平齐度/豁口缺陷的工业实用算法。通过参数空间的均匀采样点测距，判定 deviation。
- **性能效率**：5/5。
- **参数合理性**：5/5。包含期望宽度、容差等。
- **鲁棒性**：4/5。HoughLinesP 找平行线逻辑偶尔可能不稳定，依赖前面的边缘检测质量。
- **工程质量**：4/5。自动降级匹配逻辑（Canny/Sobel -> HoughP）比较完整。

### 7. `CaliperToolOperator` (卡尺工具)
- **科学性**：5/5。非常典型的工业视觉卡尺：一维像素投影、数值微分找梯度极值、亚像素插值（抛物线拟合）。高度契合 NotebookLM 中提及的 1D 亚像素测量最佳实践。
- **性能效率**：4/5。由于是 1D 采样，即便使用 C# 直接读取像素 `gray.At<byte>` 也非常快。
- **参数合理性**：5/5。提供了黑到白（DarkToLight）、白到黑（LightToDark）的极性筛选，极其专业。
- **鲁棒性**：4/5。
- **工程质量**：5/5。`RefineSubpixel` 中的 `0.5 * (g0 + g1) / denom` 是经典的一维二次插值。

### 8. `ParallelLineFindOperator` (平行线查找)
- **科学性**：4/5。通过 Hough 变换找到多条线段，然后计算两两夹角与垂直距离进行评分。
- **性能效率**：4/5。O(N^2) 的遍历比较，但 N（检测到的线段数）通常很小。
- **参数合理性**：5/5。
- **鲁棒性**：4/5。
- **工程质量**：4/5。

### 9. `LineMeasurementOperator` (直线测量)
- **科学性**：5/5。HoughLines (标准霍夫变换，极坐标) 和 HoughLinesP (概率霍夫变换，线段)。
- **性能效率**：5/5。
- **参数合理性**：4/5。
- **鲁棒性**：5/5。自动转换灰度和提取 Canny 边缘。
- **工程质量**：5/5。

### 10. `MorphologicalOperationOperator` & `MorphologyOperator` (形态学系列)
- **⚠️ 严重偏离（架构冗余）**
- **问题描述**：项目中存在两个职责几乎 100% 重叠的算子：`MorphologicalOperationOperator` 和 `MorphologyOperator`。两者都实现了 Erode/Dilate/Open/Close 等操作，参数定义（KernelShape, Iterations, Anchor）均高度一致。仅仅是参数字典的声明略有微小差异（如形参数据类型为 string vs enum）。
- **改进建议**：这是极其糟糕的工程实践，会导致用户迷惑。必须**废弃其中一个**（建议保留支持 Enum 下拉菜单体验更好的 `MorphologicalOperationOperator`，将原 `MorphologyOperator` 标记为 `[Obsolete]` 或彻底移除，并在兼容层做好别名映射）。
- **科学性**：代码逻辑调用 `Cv2.MorphologyEx` 科学性无问题。

---

## 结论与行动项 (Action Items)

1. **架构精简（P0）**：严重重复造轮子。合并并移除 `MorphologyOperator`，全面保留 `MorphologicalOperationOperator`，对外展现统一的形态学算子 UI。
2. **性能优化（P1）**：重构 `SubpixelEdgeDetectionOperator` 中传统方法的 C# `BilinearInterpolate` 循环插值，改为使用 OpenCV 向量化运算（如 `Remap`）。
3. **算法增强（P2）**：在 `CannyEdgeOperator` 中增加“AutoThreshold”属性，基于图像中值自动计算 Canny 阈值，降低人工调参成本。
4. **技术预研（P3）**：针对更高精度的玻璃屏检测等需求，未来可参考 NotebookLM 的建议，在 `SubpixelEdgeDetectionOperator` 引入 Zernike 矩亚像素计算。
