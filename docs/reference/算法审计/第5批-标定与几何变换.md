# 算法深度审计报告 - 第 5 批：标定、坐标变换与图像几何变换

**审计时间**：2026-02-26（勘误修订版）
**算子数量**：10（相机标定、畸变校正、平移旋转标定、N点标定、仿射、透视、极坐标、标定加载等）
**主要职责**：手眼标定、坐标转换、镜头畸变校正、图像空间变换。

## 总体评价
标定是工业视觉的"核心血液"。这一批算子实现了非常丰富的工业功能：不仅有基础的"棋盘格标定"，还包含了高端视觉软件中常见的"N点标定（仿射/透视）"、"手眼平移旋转标定（SVD法/最小二乘法）"。这部分数学底子**相当扎实**。

但在 **数据契约（JSON 序列化协同）** 与 **参数 UI 设计的一致性** 上，存在严重的断层（P1级缺陷），直接导致最核心的"畸变校正"功能名存实亡（退化）。

---

## 核心发现与建议 (按严重程度分类)

### 🔴 P1 级缺陷：核心标定流程断裂与算子命名误导
#### 1. `UndistortOperator` 与 `CameraCalibrationOperator` 间相机矩阵数据结构不兼容（失效）
- **现状**：
  - `CameraCalibrationOperator` 保存的标定文件中，`CameraMatrix` 被定义为二维数组 `double[,] CameraMatrix = new double[3, 3]`（第 392 行）。
  - `UndistortOperator` 解析标定文件时，用于反序列化的类 `CalibrationInfo` 中，定义的是一维数组 `public double[]? CameraMatrix { get; set; }`（第 131 行）。
- **问题**：`System.Text.Json` 反序列化时，JSON 中的二维数组 `[[a,b,c],[d,e,f],[g,h,i]]` 无法匹配到 `double[]` 类型，导致 `CameraMatrix` 属性被赋值为 `null`（而非抛出整体异常，因为 JSON 顶层结构仍可解析）。随后代码在第 94 行判断 `calInfo.CameraMatrix != null` 为 false，跳过标定数据，**启用了第 84-89 行写死的"假透视矩阵"**（主点在图像完全中心，焦距为宽度 0.8 倍）进行畸变校正。
- **补充说明**：第 72-76 行的 `catch {}` 仅在整个 JSON 无法解析时触发（此时第 78-80 行会正确返回失败）。真正危险的场景是 JSON 整体可解析但 `CameraMatrix` 字段类型不匹配——此时不抛异常、不返回失败、也不输出警告，直接静默降级使用伪造矩阵。
- **后果**：用户辛辛苦苦做了标定，导入畸变校正节点后却发现画面变形极其怪异，**且没有任何报错提示，用户完全无法定位问题**。
- **NotebookLM 建议原则**：全局统一核心几何数据结构对象的 JSON 数据契约。均改为 `double[][]` 或扁平的 `List<double>`。严禁在未成功取得参数时"无感降级并使用假矩阵代替"。

#### 2. `CameraCalibrationOperator` 中"单图检测"模式不标定
- **现状**：算子存在一个 `"SingleImage"` 模式，但代码逻辑中仅仅调用了 `FindChessboardCorners` 后就直接返回了。并没有调用 `Cv2.CalibrateCamera`。
- **问题**：算名叫"相机标定"，但单图模式实际上只是"寻找标定板角点"。用户如果在单图模式下期待获取标定文件，结果什么也得不到。
- **NotebookLM 建议原则**：改名或拆分功能。单张图原则上无法精确解算无约束的内外参，应该将单图模式命名为 `FindCalibrationBoard` 算子，或者在明确约束（假设焦距固定等）下调用真正的标定。

### 🟡 P2 级缺陷：架构不一致、性能与体验问题
#### 1. `PerspectiveTransformOperator` 的入参设计过于繁琐
- **现状**：为了输入四边形的 4 个源顶点和 4 个目标顶点，算子竟然定义了 `SrcX1, SrcY1... DstY4` 共计 **16 个 `[OperatorParam]`**！
- **问题**：界面被撑爆，毫无可用性可言。而旁边的 `AffineTransformOperator` （仿射变换）和 `NPointCalibrationOperator` 已经做出了正确的榜样，使用了 JSON 列表字符串 `[[x1,y1],[x2,y2]...]` 来作为统一参数。
- **NotebookLM 建议原则**：清理繁琐的 16 个点坐标参数。使用点集合结构体/JSON Array输入，并在前端实现 ROI 四边形交互控件。

#### 2. `PolarUnwrapOperator`（极坐标展开）造轮子导致性能隐患
- **现状**：开发者为了把圆环展开为矩形，用双层 C# `for` 循环配合 `Math.Cos / Math.Sin` 密集计算生成 `MapX/MapY`，最后调用 `Remap`。
- **问题**：如果在 4K 分辨率或 2000 万像素相机下，C# 的 managed loop 以及浮点三角函数计算极其耗时。
- **NotebookLM 建议原则**：OpenCV 已经内置了高度优化过（且支持多线程/SIMD的） `Cv2.WarpPolar` 或直接 `LinearPolar` 算子，应直接复用底层原生能力。

#### 3. 几何变换算子的插值边界过于写死
- **现状**：`AffineTransformOperator` 等所有空间变换算子底层，写死了 `InterpolationFlags.Linear` 以及黑边填充 `BorderTypes.Constant, Scalar.Black`。
- **问题**：在工业字符识别、高精度测量或边缘保护中，经常需要最近邻 (`Nearest`) 或双三次插值 (`Cubic`)，有时需要边缘复制填补边界。没有暴露这些选项限制了算子灵活度。

---

## 逐一算子审查档案

### 1. `CalibrationLoaderOperator`
- **评价**：支持 JSON/XML/YAML 格式读取矩阵，解析逻辑写的很不错，利用字符串匹配和容错处理兼容了多种旧版格式。成熟稳定。

### 2. `TranslationRotationCalibrationOperator`
- **评价**：算法非常专业，内置 `LeastSquares`（仿射逼近）和 `SVD`（绝对刚体求解平移+旋转）。代码正确处理了质心坐标系并解算了最佳逼近矩阵。优秀的工业算子。

### 3. `NPointCalibrationOperator`
- **评价**：N 点标定同时支持了仿射（至少3点）和透视（至少4点）矩阵求解，也对投影误差（Reprojection Error）做了完美的评估反馈。成熟稳定。

### 4. `CameraCalibrationOperator`
- **评价**：存在 P1 未输出单图矩阵和 P1 数据结构不兼容隐患。需要打通上下游对象契约。

### 5. `UndistortOperator`
- **评价**：P1，反序列化 `double[]` 与上游 `double[,]` 不兼容导致 CameraMatrix 为 null，但不报错不返回失败，直接使用硬编码伪造矩阵。必须重构。

### 6. `PerspectiveTransformOperator`
- **评价**：P2，参数暴露过剩。功能使用正常。

### 7. `PolarUnwrapOperator`
- **评价**：P2，生成 Map 时有性能瓶颈，建议使用原生 `Cv2.WarpPolar` 取代手动计算。

### 8. `CoordinateTransformOperator` (第4批已接触)
- **评价**：处理了点坐标的简单乘法仿射与平移。

### 9. `AffineTransformOperator`
- **评价**：三点求仿射与 RotateScaleTranslate 并存设计很好。缺少插值选项。

### 10. `ImageRotateOperator`
- **评价**：旋转代码支持自动扩展画布 (`AutoResize` 为 true 时通过三角函数重算外接矩形大小)。写得非常好，考虑到了裁剪问题。

---

## 总结与后续动作
- **优先处理（P1）**：立刻统一标定矩阵存储时的模型，确保 `double[][]` 一致性。修复 `UndistortOperator` 中使用硬编码（src.Width*0.8）的逃生逻辑；修复 `CameraCalibrationOperator` 的单图执行逻辑。
- **重构建议（P2）**：修改透视变换节点的参数臃肿问题；重写极坐标展开。
