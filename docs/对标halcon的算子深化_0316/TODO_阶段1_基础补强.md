# 阶段1 TODO：基础补强（Vibe Coding版 V2.1）

> **阶段目标**: 核心测量能力达到工业可用，Halcon 60%水平
> **时间周期**: 第0-5周（新增W0基础设施周，原1-4周调整为1-5周）
> **阶段编号**: Phase-1-Foundation-Vibe-Enhanced
> **核心理念**: AI生成代码 + 快速验证 + 每周有成果 + 性能可控

---

## 零、第0周：基础设施搭建（新增，必做）

> **为什么先做这个？** 没有测试数据和性能监控，后续开发将是盲目的

### W0任务卡片

```markdown
【W0-任务1】性能分析工具（2小时）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⏱️ 预计时间: 2小时
🎯 目标: 可记录任意算子执行时间，生成性能报告

【任务】
实现算子性能分析工具

【要求】
1. 封装Stopwatch计时器
2. 记录每个算子的执行时间（支持多次运行统计）
3. 生成性能报告（CSV/JSON格式）
4. 计算平均值、最大值、最小值、标准差

【输出文件】
- PerformanceProfiler.cs

【使用示例】
```csharp
using (var profiler = new PerformanceProfiler("CaliperTool"))
{
    // 算子执行代码
    var result = caliperOperator.Execute(input);
}
// 自动记录到 performance_log.csv
```

【验证标准】
□ 编译通过
□ 测试100次运行，能正确统计平均值、标准差
□ 生成的CSV文件格式正确
```

```markdown
【W0-任务2】内存泄漏检测设置（1小时）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⏱️ 预计时间: 1小时
🎯 目标: 配置内存监控工具

【任务】
配置.NET内存分析工具

【要求】
1. 编写内存泄漏测试脚本（循环调用算子1000次）
2. 记录初始内存和最终内存
3. 设置告警阈值（增长>50MB则失败）

【输出文件】
- MemoryLeakTest.cs（测试脚本）
- memory_test_report.md（测试报告模板）

【验证标准】
□ 能检测到故意制造的内存泄漏
□ 正常算子通过测试
```

```markdown
【W0-任务3】合成测试数据生成器（3小时）⭐ 核心任务
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⏱️ 预计时间: 3小时
🎯 目标: 生成10种标准测试图

【任务】
实现标准测试图生成工具

【要求】
1. 生成ISO 12233斜边图（用于亚像素精度测试）
2. 生成几何形状图（圆、矩形、三角形、椭圆）
3. 生成带噪声版本（高斯噪声SNR=20dB、椒盐噪声）
4. 生成3D点云（平面、球体、圆柱体，带噪声）
5. 保存到 tests/TestData/ 目录

【输出文件】
- TestDataGenerator.cs
- 生成的测试图（至少10张）

【AI Prompt】
```markdown
【任务】实现测试数据生成器

【要求】
1. 生成斜边图：
   - 尺寸512x512
   - 斜边角度5度
   - 黑白对比度255

2. 生成几何形状：
   - 圆形（半径50-100像素，圆度>0.99）
   - 矩形（宽高比1:2，矩形度>0.95）
   - 三角形

3. 添加噪声：
   - 高斯噪声：mean=0, sigma=10
   - 椒盐噪声：density=0.05

4. 生成点云：
   - 平面：z=0, 1000点，添加±0.5mm噪声
   - 球体：半径50mm, 2000点
   - 圆柱体：半径30mm, 高100mm, 1500点

【参考】
- OpenCV: Cv2.Line, Cv2.Circle, Cv2.Rectangle
- 噪声: Cv2.Randn (高斯), 手动实现椒盐
- 点云: 使用参数方程生成
```

【验证标准】
□ 生成10种测试图
□ 斜边图角度误差<0.1度
□ 圆形圆度>0.99
□ 点云文件可用Open3D加载
```

```markdown
【W0-任务4】现有算子基准测试（2小时）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⏱️ 预计时间: 2小时
🎯 目标: 建立性能基线

【任务】
为现有118个算子建立性能基线

【要求】
1. 选择10个代表性算子（预处理、测量、匹配各选几个）
2. 使用W0-3生成的测试图运行
3. 记录执行时间
4. 生成基准报告

【输出文件】
- baseline_performance.json

【验证标准】
□ 至少测试10个算子
□ 记录平均执行时间
□ 生成JSON报告
```

### W0验收标准

- ✅ 性能分析工具可用
- ✅ 内存泄漏检测脚本可用
- ✅ 生成至少10种测试图
- ✅ 建立性能基线

---

## 一、本周能做什么？（立即可启动）

### 本周任务卡片（复制即用）

```markdown
【W1-任务1】实现灰度重心法亚像素边缘检测（0316-1202）✅ 已完成
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⏱️ 预计时间: 2小时 | 实际时间: 1.5小时
🎯 目标: 精度<0.1像素 | 实际精度: <0.01像素
✅ 验证: 15个单元测试全部通过

【完成状态】
- ✅ SubPixelEdgeDetector.cs - 灰度重心法实现
- ✅ SubPixelEdgeDetectorTests.cs - 15个单元测试
- ✅ 编译通过
- ✅ 理想边缘/噪声边缘/弱边缘测试通过
- ✅ 与OpenCV cornerSubPix误差<5%

【Prompt】
基于OpenCvSharp实现灰度重心法亚像素边缘检测。
公式: position = Σ(i * gray[i]) / Σ(gray[i])
输入: Mat lineProfile (1xN灰度行)
输出: float subPixelPosition
要求: 包含3个单元测试（理想边缘/噪声边缘/弱边缘）

【AI 编程专属上下文】
- **定位**: 此类为纯算法辅助类 `SubPixelEdgeDetector`，不需要继承 `OperatorBase`。
- **输入**: `Mat lineProfile` 必定为 1xN 大小的单通道8位灰度图像，可使用 `lineProfile.At<byte>(0, i)` 提取像素进行计算。
- **返回**: 成功算出边缘重心则返回 float 坐标值；失败场景下可直接返回 -1。

【测试数据】
- 测试1: 理想阶梯边缘 [0,0,0,255,255,255] → 期望2.5
- 测试2: 斜边 [0,64,128,192,255] → 重心位置
- 测试3: 带噪声边缘（加高斯噪声）→ 稳定性验证

【通过标准】
□ 编译通过
□ 3个测试通过
□ 与OpenCV cornerSubPix误差<5%
```

---

## 二、W1-W4任务清单（AI Prompt就绪）

### Week 1：亚像素基础 + Blob特征

#### 任务W1-1：灰度重心法（2小时）

```markdown
【上下文】
项目: Acme.Product.Infrastructure
命名空间: Acme.Product.Infrastructure.ImageProcessing
现有依赖: OpenCvSharp4

【生成代码】
文件: SubPixelEdgeDetector.cs
```csharp
public class SubPixelEdgeDetector
{
    /// <summary>
    /// 灰度重心法亚像素边缘定位
    /// </summary>
    /// <param name="lineProfile">1xN灰度轮廓线</param>
    /// <param name="threshold">边缘阈值</param>
    /// <returns>亚像素位置，失败返回-1</returns>
    public float DetectCentroid(Mat lineProfile, byte threshold)
    {
        // AI实现
    }
}
```

【单元测试要求】
文件: SubPixelEdgeDetectorTests.cs
- Test_Centroid_PerfectEdge: 理想边缘，验证公式正确性
- Test_Centroid_NoisyEdge: SNR=20dB噪声，验证稳定性
- Test_Centroid_WeakEdge: 低对比度边缘，验证鲁棒性
```

#### 任务W1-2：Zernike矩法（3小时）

```markdown
【参考资料】
- 论文: Ghosal & Mehrotra, "Orthogonal Moment Operators for Subpixel Edge Detection" (1993)
- 简化公式: 
  - Z00 = Σf(x,y) （像素和）
  - Z11 = Σf(x,y) * (x - j0) / r （一阶矩）
  - 边缘位置 l = Z11 / Z00 * 2.0

【AI 编程专属上下文】
- **扩展要求**: 在已有的纯算法类 `SubPixelEdgeDetector` 中补充此新方法。
- **OpenCV矩阵加速**: 避免逐个像素写 for 循环算矩。建议通过初始化一组 Zernike 核特征值 `Mat`，再使用 `Cv2.Filter2D` 一次性完成与底层 ROI 区域的二维滤波（空间卷积），能让性能符合 <100ms 要求。

【生成代码】
文件: SubPixelEdgeDetector.cs（添加方法）
```csharp
public float DetectZernike(Mat roi, int maskSize = 5)
{
    // 使用预计算的Zernike核
    // 返回亚像素边缘位置
}
```

【测试要求】
- 精度测试: 斜边图误差<0.05像素
- 性能测试: 1000次调用<100ms
```

#### 任务W1-3：增强BlobDetectionOperator（2小时）⭐ 修正

```markdown
【任务】增强现有BlobDetectionOperator，添加更多几何特征

【现有实现】
- 已有算子：BlobDetectionOperator.cs（对应OperatorType.BlobAnalysis）
- 已有特征：MinCircularity, MinConvexity, MinInertiaRatio
- 已有输出：Blobs（轮廓数据）, BlobCount

【增强目标】
在现有BlobDetectionOperator中添加更多工业级特征：
- Rectangularity（矩形度）= Area/(Width*Height)
- Eccentricity（离心率）
- EulerNumber（欧拉数，拓扑特征）
- MeanGray, GrayStdDev（灰度特征）

【AI Prompt】
```markdown
【任务】增强BlobDetectionOperator，添加高级几何特征

【上下文】
- 文件：BlobDetectionOperator.cs
- 当前使用SimpleBlobDetector检测Blob
- 需要在现有基础上添加更多特征计算

【要求】
1. 保持现有接口不变（向后兼容）
2. 添加新的输出端口：
   - BlobFeatures（包含所有特征的结构化数据）
3. 添加新的参数：
   - MinRectangularity（最小矩形度，默认0.0）
   - MinEccentricity（最小离心率，默认0.0）
   - OutputDetailedFeatures（是否输出详细特征，默认false）

【实现步骤】
1. 在检测到Blob后，对每个Blob计算：
   - Rectangularity = Area / (BoundingRect.Width * BoundingRect.Height)
   - Eccentricity = 使用Cv2.FitEllipse计算椭圆，eccentricity = sqrt(1 - (minorAxis/majorAxis)^2)
   - EulerNumber = 1 - 孔洞数（使用轮廓层次结构）
   - MeanGray, GrayStdDev = 在原图上计算Blob区域的灰度统计

2. 创建BlobFeature结构：
```csharp
public class BlobFeature
{
    public int Label { get; set; }
    public int Area { get; set; }
    public double Circularity { get; set; }
    public double Convexity { get; set; }
    public double Rectangularity { get; set; }
    public double Eccentricity { get; set; }
    public int EulerNumber { get; set; }
    public float MeanGray { get; set; }
    public float GrayStdDev { get; set; }
}
```

3. 根据新参数过滤Blob

【AI 编程专属上下文】
- **修改现有类**：在BlobDetectionOperator.cs中添加代码，不要创建新类
- **参数获取**：使用现有方法 `GetDoubleParam(@operator, "MinRectangularity", 0.0)`
- **输出格式**：使用现有的CreateImageOutput方法，添加BlobFeatures到字典
- **向后兼容**：确保现有的Blobs和BlobCount输出不变

【测试数据】
- 圆形：Circularity>0.99, Rectangularity~0.785
- 正方形：Rectangularity>0.95, Circularity~0.785
- 带孔洞形状：EulerNumber=1-孔洞数
```

【验收标准】
□ 现有功能不受影响
□ 新增特征计算正确
□ 圆形Circularity>0.99
□ 矩形Rectangularity>0.95
□ 性能<30ms（512x512图，10个Blob）
```

---

#### 任务W1-4：增强CaliperToolOperator（3小时）⭐ 修正

```markdown
【任务】增强现有CaliperToolOperator，集成亚像素边缘检测

【现有实现】
- 已有算子：CaliperToolOperator.cs（对应OperatorType.CaliperTool）
- 已有参数：SubpixelAccuracy（bool类型）
- 当前状态：SubpixelAccuracy参数存在但可能未完全实现

【增强目标】
将W1-1和W1-2开发的SubPixelEdgeDetector集成到CaliperToolOperator中：
- 当SubpixelAccuracy=true时，使用亚像素检测
- 支持选择算法：gradient_centroid（灰度重心法）或 zernike（Zernike矩法）

【AI Prompt】
```markdown
【任务】增强CaliperToolOperator，集成亚像素边缘检测

【上下文】
- 文件：CaliperToolOperator.cs
- 已有SubpixelAccuracy参数（bool类型）
- 需要集成SubPixelEdgeDetector类

【要求】
1. 添加新参数：
   - SubPixelMode（enum，默认"gradient_centroid"）
   - 选项：["gradient_centroid", "zernike"]

2. 修改边缘检测逻辑：
```csharp
if (subpixel)
{
    var detector = new SubPixelEdgeDetector();
    if (subPixelMode == "zernike")
    {
        edgePosition = detector.DetectZernike(lineProfile);
    }
    else
    {
        edgePosition = detector.DetectCentroid(lineProfile);
    }
}
else
{
    // 保持现有的整像素检测逻辑
}
```

3. 保持现有接口不变（向后兼容）

【AI 编程专属上下文】
- **修改现有类**：在CaliperToolOperator.cs中修改，不要创建新类
- **参数获取**：`var subPixelMode = GetStringParam(@operator, "SubPixelMode", "gradient_centroid");`
- **依赖注入**：SubPixelEdgeDetector是纯算法类，直接new即可
- **性能要求**：单次卡尺测量<5ms

【测试场景】
- 整像素模式：与现有功能一致
- 亚像素模式（重心法）：精度<0.1px
- 亚像素模式（Zernike）：精度<0.05px
```

【验收标准】
□ 现有功能不受影响（SubpixelAccuracy=false时）
□ 亚像素模式精度<0.1px
□ 性能<50ms（512x512图，10个卡尺）
□ 向后兼容
```

---

#### ~~任务W1-5：镜头畸变校正（3小时）~~ ❌ 已删除

```markdown
【删除原因】
- UndistortOperator.cs已存在并实现基础去畸变功能
- 无需重复开发

【现有实现】
- 文件：UndistortOperator.cs
- 功能：使用Cv2.Undistort进行镜头畸变校正
- 输入：Image + CalibrationData（相机内参和畸变系数）
- 输出：Undistorted Image

【如需增强】
可在后续阶段（第4阶段）考虑：
- 查找表加速（InitUndistortRectifyMap）
- 鱼眼镜头支持（Fisheye模型）
- 实时去畸变优化

【时间节省】
删除此任务节省3小时，W1总时间从11小时减少到8小时
```

---

### Week 2：ROI自动跟踪 + 卡尺完善

【AI 编程专属上下文】
- **定位**: 必须完整继承 `OperatorBase` 并附带上方的所有 Attribute。
- **矩形属性注入**: ROI 区域可通过 `TryParseSearchRect` 的方式从 `inputs` 的 `RoiRectangle` 获取。
- **卡尺遍历调用**: 利用在上一个任务写好的静态库 `SubPixelEdgeDetector` 去多次计算得到亚像素点。
- **结果输出格式**: 必须把边缘点封装进 `List<Position>` 提供给 `EdgePositions` 端口，通过 `CreateImageOutput` 包裹输出。

【实现要点】
1. 沿ROI长边等距布置卡尺工具
2. 每个卡尺提取垂直于边缘的灰度线
3. 调用SubPixelEdgeDetector获取亚像素位置
4. 可选：边缘点拟合直线/圆
```

---

#### 任务W1-5：镜头畸变校正（3小时）⭐ 新增关键任务

```markdown
【任务】实现镜头畸变校正算子

【为什么重要】
- 对精确测量至关重要
- 阻塞所有后续测量算子
- 工业计量应用的必备功能

【要求】
1. 基于相机标定参数实现去畸变
2. 支持径向畸变和切向畸变校正
3. 支持查找表加速（可选）

【AI Prompt】
```markdown
【任务】实现镜头畸变校正算子

【上下文】
- 项目已有CameraCalibration算子输出标定参数
- 需要使用标定结果对图像去畸变

【要求】
1. 继承OperatorBase
2. 输入：
   - Image（待校正图像）
   - CameraMatrix（相机内参矩阵）
   - DistCoeffs（畸变系数）
3. 输出：
   - UndistortedImage（校正后图像）

【实现】
使用OpenCV的Cv2.Undistort或Cv2.Remap

【AI 编程专属上下文】
- **定位**: 继承 `OperatorBase`
- **参数获取**:
  ```csharp
  var cameraMatrix = GetMatrixParam(@operator, "CameraMatrix");
  var distCoeffs = GetMatrixParam(@operator, "DistCoeffs");
  ```
- **性能优化**: 可选实现查找表缓存（InitUndistortRectifyMap）

【测试】
- 使用棋盘格标定图测试
- 验证直线在校正后保持直线
- 边缘位置误差<0.5像素
```

【验证标准】
□ 编译通过
□ 单元测试通过
□ 棋盘格直线校正后保持直线
□ 性能<10ms（512x512图像）
```

---

### Week 2：ROI自动跟踪 + 卡尺完善

#### 任务W2-1：ROI坐标变换器（2小时）

```markdown
【需求】
上游形状匹配输出位姿（位置+角度+缩放）
测量ROI需要跟随目标移动

【AI 编程专属上下文】
- **定位**: `RoiTracker.cs` 是测量模块的辅助逻辑类。
- **实现建议**: 需要使用 `OpenCvSharp.Cv2.GetRotationMatrix2D` 生成仿射变换矩阵，再使用 `Cv2.Transform` 变换被跟踪 ROI 矩形的四个顶点，最后通过取 `Cv2.BoundingRect()` 输出新的包裹外接矩形。

【生成代码】
文件: RoiTracker.cs
```csharp
public class RoiTracker
{
    public Rect TransformRoi(
        Rect baseRoi,           // 基准ROI（匹配坐标系）
        Point2f matchPosition,  // 匹配位置
        float matchAngle,       // 匹配角度（度）
        float matchScale        // 匹配缩放
    )
    {
        // 构建变换矩阵：平移+旋转+缩放
        // 应用变换到baseRoi的四个角点
        // 返回新的外接矩形
    }
}
```

【测试用例】
- 纯平移: baseRoi移动(100,50)，验证结果
- 旋转90度: baseRoi旋转，验证宽高互换
- 组合变换: 平移+旋转+缩放
```

#### 任务W2-2：卡尺边缘对测量（3小时）

```markdown
【Halcon参考】
measure_pairs: 测量一对边缘（明暗+暗明）
输出: 两个边缘位置 + 它们之间的距离

【AI 编程专属上下文】
- **框架定位**: 需通过扩展 `CaliperToolOperator` 代码去实现。
- **参数读取**: `var measureMode = GetStringParam(@operator, "MeasureMode", "single_edge");`
- **统计学计算**: 需要在算法块内过滤出所有有效的匹配边对，随后调用 `.Average()` 算出宽度的均值，并手写均方差(StdDev)计算。
- **端口透传**: 通过 `OperatorExecutionOutput` 增加返回 `AverageDistance` 与 `DistanceStdDev`。

【生成代码】
扩展 CaliperToolOperator
```csharp
[Parameter("MeasureMode", "single_edge", 
    Options = new[]{"single_edge", "edge_pairs"})]
[Parameter("PairDirection", "any",
    Options = new[]{"positive_to_negative", "negative_to_positive", "any"})]

[OutputPort("PairDistances", PortDataType.Float)]  // 边缘对距离
[OutputPort("AverageDistance", PortDataType.Float)]
[OutputPort("DistanceStdDev", PortDataType.Float)]
```

【算法逻辑】
1. 检测所有边缘（正负都检测）
2. 按方向筛选配对
3. 计算配对边缘的距离
4. 统计：平均距离、标准差
```

---

### Week 3-4：鲁棒形状匹配

#### 任务W3-1：模板边缘提取（2小时）

```markdown
【输入】模板图像
【输出】边缘点列表（位置+梯度方向）

【AI 编程专属上下文】
- **实现手段**: 使用 `OpenCvSharp.Cv2.Sobel()` 分别求 dx 和 dy。
- **浮点处理**: 梯度通常保存为 32F 单精度浮点矩阵，通过 `Cv2.Magnitude` 与 `Cv2.Phase`（或者直接 `Math.Atan2` 遍历）获取梯度方向。
- **过滤阈值**: 强度低于 `gradientThreshold` 的被淘汰，不生成 `EdgePoint`。

【生成代码】
```csharp
public class TemplateEdgeExtractor
{
    public List<EdgePoint> Extract(
        Mat template, 
        double gradientThreshold = 50
    )
    {
        // 1. Sobel计算梯度
        // 2. 阈值筛选边缘点
        // 3. 计算梯度方向 atan2(dy, dx)
        // 4. 返回EdgePoint列表
    }
}

public struct EdgePoint
{
    public Point Position;
    public float GradientDirection;  // -PI to PI
    public float GradientMagnitude;
}
```
```

#### 任务W3-2：金字塔构建（2小时）

```markdown
【需求】
构建3-4层图像金字塔，每层保存边缘点
用于分层搜索加速

【生成代码】
```csharp
public class PyramidTemplate
{
    public List<List<EdgePoint>> EdgePyramid { get; }  // 从粗到细
    public List<Mat> ImagePyramid { get; }
    
    public void Build(Mat template, int levels)
    {
        // 1. 原图作为最细层
        // 2. 逐层PyrDown下采样
        // 3. 每层提取边缘点
    }
}
```
```

#### 任务W3-3：梯度匹配评分（3小时）

```markdown
【核心算法】
模板边缘点在搜索图中寻找最佳匹配位置
评分基于梯度方向差异（不是灰度）

【公式】
score = Σ(1 - |Δgradient|/π) / N
其中 Δgradient = template_grad - search_grad

【生成代码】
```csharp
public class GradientMatcher
{
    public double ComputeScore(
        List<EdgePoint> templateEdges,
        Mat searchImage,
        Point offset
    )
    {
        // 在每个模板边缘点位置，搜索图中对应位置的梯度
        // 计算方向差异
        // 返回平均分
    }
}
```

【优化】
- 使用查找表加速方向差异计算
- SIMD优化（后期再做）
```

#### 任务W4-1：金字塔分层搜索（4小时）

```markdown
【算法流程】
1. 最粗层：遍历所有可能位置，快速筛选候选
2. 中间层：验证候选，进一步筛选
3. 最细层：精修位置，最小二乘拟合

【AI 编程专属上下文】
- **最终落地封装**: 该匹配核心模块完成后，务必将其向上挂载到一个继承于 `OperatorBase` 的包装类 `PyramidShapeMatchOperator` 内。
- **位姿返回规范**: 使用 `operator` 输出接口返回 `Position` 以及相应的 `AngleDeg` 角度、`Scale` 缩放，最好附带一个画上检测框边界的 `ImageWrapper` 显示图。

【生成代码】
```csharp
public class ShapeMatcher
{
    public List<MatchResult> Search(
        Mat searchImage,
        double minScore = 0.5,
        int numMatches = 1
    )
    {
        // 1. 在最粗层获取候选位置列表
        var candidates = CoarseSearch(searchImage, minScore * 0.8);
        
        // 2. 逐层精修
        foreach (var level in pyramidLevels.Skip(1))
        {
            candidates = RefineAtLevel(candidates, level);
        }
        
        // 3. 返回最佳匹配
        return candidates.OrderByDescending(c => c.Score)
                        .Take(numMatches)
                        .ToList();
    }
}
```

【测试场景】
- 无遮挡：匹配分数>0.9
- 30%遮挡：匹配分数>0.6
- 光照变化：±50%亮度，匹配稳定
```

---

#### 任务W4-2：形状匹配鲁棒性增强（4小时）⭐ 新增任务

```markdown
【任务】增强形状匹配的遮挡处理能力

【为什么重要】
- 工业场景常有部分遮挡（零件堆叠、夹具遮挡）
- Halcon的核心竞争力之一
- 当前W4-1实现可能对遮挡不够鲁棒

【要求】
1. 实现离群值剔除机制
2. 增加最小匹配百分比参数
3. 测试50%遮挡场景

【AI Prompt】
```markdown
【任务】增强ShapeMatcher的遮挡鲁棒性

【上下文】
- 已有W4-1的基础ShapeMatcher实现
- 需要增强对部分遮挡的鲁棒性

【要求】
1. 在ComputeScore中增加离群值检测：
   - 计算每个边缘点的匹配分数
   - 使用中位数绝对偏差（MAD）检测离群值
   - 剔除分数过低的点（可能是遮挡区域）

2. 增加参数：
   - MinMatchPercentage（最小匹配百分比，默认0.6）
   - 如果有效匹配点<总点数×MinMatchPercentage，则拒绝该候选

3. 修改评分公式：
   ```
   score = Σ(valid_points_score) / valid_points_count
   occlusion_ratio = 1 - (valid_points_count / total_points)
   final_score = score * (1 - occlusion_ratio * 0.5)
   ```

【AI 编程专属上下文】
- **扩展现有类**: 在ShapeMatcher中添加方法
- **参数传递**: 通过PyramidShapeMatchOperator的Parameter传入
- **输出增强**: 返回遮挡率估计

【测试场景】
- 30%遮挡：匹配分数>0.6，正确定位
- 50%遮挡：匹配分数>0.5，正确定位
- 70%遮挡：拒绝匹配（分数<MinScore）
```

【验证标准】
□ 30%遮挡测试通过
□ 50%遮挡测试通过
□ 无遮挡性能不下降
□ 输出遮挡率估计
```

---

## 三、验证集（每周测试用）

### W0验证集

| 测试项 | 方法 | 通过标准 |
|--------|------|----------|
| 测试数据生成 | 运行TestDataGenerator | 生成10种测试图 |
| 性能分析工具 | 测试100次运行 | 正确统计平均值、标准差 |
| 内存泄漏检测 | 1000次循环 | 内存增长<50MB |

### W1验证集

| 测试项 | 方法 | 通过标准 |
|--------|------|----------|
| 亚像素精度 | ISO斜边图 | 误差<0.05px |
| Blob圆度 | 合成圆图 | >0.99 |
| Blob矩形度 | 合成矩形 | >0.95 |

### W2验证集

| 测试项 | 方法 | 通过标准 |
|--------|------|----------|
| ROI跟踪 | 匹配+测量联动 | ROI跟随无延迟 |
| 边缘对测量 | 标准量块 | 宽度误差<0.1px |

### W4验证集

| 测试项 | 方法 | 通过标准 |
|--------|------|----------|
| 形状匹配精度 | 标准形状 | <0.1px |
| 遮挡鲁棒性 | 人工遮挡30% | 分数>0.6 |
| 光照鲁棒性 | 亮度±50% | 分数变化<0.1 |

---

## 四、每日Vibe Coding节奏

```
☀️ 早上（30分钟）
├── 选择今天的1-2个任务
├── 复制对应Prompt
└── AI生成代码框架

🌤️ 上午（1-2小时）
├── 填入项目代码
├── 解决编译问题
└── 运行单元测试

🌞 下午（1-2小时）
├── 调试测试失败项
├── 优化边界情况
└── 提交Git

🌙 晚上（可选30分钟）
├── 复盘今日任务
├── 调整明日计划
└── 更新Prompt模板（踩坑记录）
```

---

## 五、Prompt模板速查

### 图像处理类任务Prompt模板

```markdown
【任务】实现[算子名]算子
【类型】图像处理/测量/几何变换
【输入】[InputPort列表]
【输出】[OutputPort列表]
【算法】[核心算法简述或公式]
【参考】[OpenCV函数或论文]
【测试】[测试场景和预期结果]

基于以上信息，生成：
1. Operator实现类（继承OperatorBase）
2. 单元测试类（3个测试用例）
3. 测试数据生成代码
```

### 使用示例

复制上述模板，填入：
- 算子名：高斯拉普拉斯边缘检测
- 输入：Image
- 输出：Edges
- 算法：LoG = ∇²(Gσ * I) = 先高斯平滑再拉普拉斯
- 参考：cv2.Laplacian, cv2.GaussianBlur
- 测试：阶梯边缘检测，噪声抑制

---

## 六、性能预算检查清单

> **重要**：每个算子完成后必须验证性能是否满足预算

### 性能验证流程

```markdown
【性能验证步骤】
1. 使用W0-1性能分析工具测试
2. 运行100次取平均值
3. 对比性能预算表
4. 如果超标，分析瓶颈并优化
5. 记录到性能报告
```

### 阶段1算子性能预算

| 算子 | 目标延迟 | 内存限制 | 验证方法 |
|------|---------|---------|---------|
| SubPixelEdgeDetector | <1ms | 1MB | 1xN灰度线，N=100 |
| BlobDetectionOperator | <30ms | 50MB | 512x512二值图，10个Blob |
| CaliperToolOperator | <50ms | 50MB | 512x512图，10个卡尺 |
| LensDistortionCorrection | <10ms | 100MB | 512x512图 |
| RoiTracker | <1ms | 1MB | 单个ROI变换 |
| ShapeMatcher (无遮挡) | <200ms | 200MB | 512x512搜索图，64x64模板 |
| ShapeMatcher (50%遮挡) | <250ms | 200MB | 同上 |

### 性能优化建议

如果性能不达标：
1. **使用Profiler定位瓶颈**（W0-1工具）
2. **常见优化手段**：
   - 减少内存分配（复用Mat对象）
   - 使用OpenCV加速函数（避免逐像素循环）
   - 缓存重复计算（如查找表）
   - 多线程并行（Parallel.For）
3. **记录优化前后对比**

---

## 七、错误处理标准

> **重要**：所有算子必须遵循统一的错误处理规范

### 错误处理原则

```csharp
// 1. 输入验证
protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(...)
{
    // 验证输入
    if (inputImage == null || inputImage.Empty())
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "InputImageEmpty",
            "输入图像为空或无效",
            "请检查上游算子是否正确输出图像"
        ));
    }

    // 验证参数范围
    var threshold = GetFloatParam(@operator, "Threshold", 128f, min: 0, max: 255);
    if (threshold < 0 || threshold > 255)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "InvalidParameter",
            $"阈值参数超出范围: {threshold}",
            "阈值必须在0-255之间"
        ));
    }

    try
    {
        // 算法执行
        var result = ProcessImage(inputImage, threshold);

        // 验证输出
        if (result == null || result.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure(
                "ProcessingFailed",
                "算法执行失败，输出为空",
                "请检查输入图像质量或调整参数"
            ));
        }

        return Task.FromResult(OperatorExecutionOutput.Success(
            CreateImageOutput(result, new Dictionary<string, object>
            {
                { "ProcessedPixels", result.Width * result.Height }
            })
        ));
    }
    catch (OpenCVException ex)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "OpenCVError",
            $"OpenCV异常: {ex.Message}",
            "请检查图像格式或参数设置"
        ));
    }
    catch (Exception ex)
    {
        return Task.FromResult(OperatorExecutionOutput.Failure(
            "UnexpectedError",
            $"未预期的错误: {ex.Message}",
            "请联系技术支持"
        ));
    }
}
```

### 错误代码规范

| 错误代码 | 含义 | 恢复建议 |
|---------|------|---------|
| InputImageEmpty | 输入图像为空 | 检查上游算子 |
| InvalidParameter | 参数超出范围 | 调整参数值 |
| ProcessingFailed | 算法执行失败 | 检查输入质量 |
| InsufficientFeatures | 特征点不足 | 调整检测阈值 |
| MatchNotFound | 未找到匹配 | 降低匹配分数阈值 |
| MemoryAllocationFailed | 内存分配失败 | 减小图像尺寸 |
| OpenCVError | OpenCV库错误 | 检查图像格式 |
| UnexpectedError | 未预期错误 | 联系技术支持 |

### 日志记录规范

```csharp
// 使用结构化日志
_logger.LogInformation(
    "算子执行成功: {OperatorName}, 耗时: {ElapsedMs}ms, 输出: {OutputCount}",
    "CaliperToolOperator",
    elapsed.TotalMilliseconds,
    edgePositions.Count
);

_logger.LogWarning(
    "算子性能告警: {OperatorName}, 耗时: {ElapsedMs}ms, 超出预算: {BudgetMs}ms",
    "ShapeMatcher",
    elapsed.TotalMilliseconds,
    200
);

_logger.LogError(
    ex,
    "算子执行失败: {OperatorName}, 错误: {ErrorCode}",
    "BlobAnalysis",
    "ProcessingFailed"
);
```

---

## 八、本周行动项

### 今天就开始

**推荐路径**：
1. **第0周优先**：运行W0-3生成测试数据（3小时）
2. **选择第一个任务**：W1-1灰度重心法（已完成✅）或W1-3 Blob特征扩展
3. **复制Prompt** → AI生成代码
4. **性能验证** → 使用W0-1工具确认满足预算
5. **错误处理** → 按照第七节标准实现
6. **提交代码** → Git commit

### 本周目标

> **W0完成**：测试数据生成器可用，性能基线建立
> **W1完成**：亚像素卡尺 + Blob特征 + 镜头畸变校正，精度达到Halcon 60%水平

### 阶段1总体目标（第0-5周）

| 周次 | 核心任务 | 新增内容 | 验收标准 |
|------|---------|---------|---------|
| W0 | 基础设施搭建 | 性能分析、测试数据生成 | 10种测试图生成 |
| W1 | 亚像素基础 + Blob | +W1-5镜头畸变校正 | 精度<0.05px |
| W2 | ROI跟踪 + 卡尺完善 | - | ROI跟随无延迟 |
| W3 | 形状匹配基础 | - | 无遮挡匹配>0.9 |
| W4 | 形状匹配增强 | +W4-2遮挡处理 | 50%遮挡匹配>0.5 |
| W5 | 集成测试与优化 | 性能优化、文档完善 | 所有算子满足预算 |

---

*本文档每周更新，记录实际Prompt效果和踩坑点*

**版本记录**：
- V1.0 (2026-03-16)：初始版本
- V2.1 (2026-03-16)：增加W0基础设施、W1-5镜头畸变、W4-2遮挡处理、性能预算、错误处理标准
