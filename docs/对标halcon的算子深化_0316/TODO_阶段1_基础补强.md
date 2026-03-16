# 阶段1 TODO：基础补强（Vibe Coding版）

> **阶段目标**: 核心测量能力达到工业可用，Halcon 60%水平  
> **时间周期**: 第1-4周（建议每周3-5个原子任务）  
> **阶段编号**: Phase-1-Foundation-Vibe  
> **核心理念**: AI生成代码 + 快速验证 + 每周有成果

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

#### 任务W1-3：Blob特征计算（2小时）

```markdown
【基于现有代码】
现有: ConnectedComponents算子已有基础连通域
扩展: AdvancedBlobOperator添加特征输出

【AI 编程专属上下文】
- **框架定位**: 需继承 `OperatorBase`，复写 `protected override Task<OperatorExecutionOutput> ExecuteCoreAsync`。
- **参数获取**: `var minArea = GetFloatParam(@operator, "MinArea", 100f, min: 0);`。
- **零拷贝输出**: 必须调用基类 `CreateImageOutput` 方法，将结果包裹：
  ```csharp
  var output = CreateImageOutput(colorSrc, new Dictionary<string, object>
  {
      { "BlobCount", keypoints.Length },
      { "Blobs", featuresList }
  });
  return Task.FromResult(OperatorExecutionOutput.Success(output));
  ```

【生成代码】
```csharp
public class BlobFeature
{
    public int Label { get; set; }
    public int Area { get; set; }
    public double Circularity { get; set; }  // 4*PI*Area/Perimeter^2
    public double Convexity { get; set; }    // Area/ConvexHullArea
    public double Rectangularity { get; set; } // Area/(Width*Height)
    public double Eccentricity { get; set; }
    public int EulerNumber { get; set; }
    public float MeanGray { get; set; }
    public float GrayStdDev { get; set; }
}

public class AdvancedBlobOperator : OperatorBase
{
    // AI生成Execute实现
}
```

【测试数据生成】
```csharp
// 测试图1: 完美圆形 (圆度>0.99)
// 测试图2: 正方形 (矩形度>0.95, 圆度~0.785)
// 测试图3: 带孔洞形状 (欧拉数=0)
```
```

#### 任务W1-4：AdvancedCaliperOperator框架（3小时）

```markdown
【算子规格】
```csharp
[OperatorMeta(
    Name = "高级卡尺",
    Category = "测量",
    Description = "亚像素边缘测量工具"
)]
public class AdvancedCaliperOperator : OperatorBase
{
    [InputPort("Image", PortDataType.Image)]
    [InputPort("RoiRectangle", PortDataType.Rectangle)]
    
    [Parameter("SubPixelMode", "gradient_centroid", 
        Options = new[]{"gradient_centroid", "zernike"})]
    [Parameter("CaliperCount", 10)]
    [Parameter("MeasureWidth", 20)]
    
    [OutputPort("EdgePositions", PortDataType.PointList)]
    [OutputPort("EdgeStrengths", PortDataType.Float)]
}
```

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
- **框架定位**: 需通过扩展 `AdvancedCaliperOperator` 代码去实现。
- **参数读取**: `var measureMode = GetStringParam(@operator, "MeasureMode", "single_edge");`
- **统计学计算**: 需要在算法块内过滤出所有有效的匹配边对，随后调用 `.Average()` 算出宽度的均值，并手写均方差(StdDev)计算。
- **端口透传**: 通过 `OperatorExecutionOutput` 增加返回 `AverageDistance` 与 `DistanceStdDev`。

【生成代码】
扩展 AdvancedCaliperOperator
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

## 三、验证集（每周测试用）

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

## 六、本周行动项

### 今天就开始

1. **复制W1-1 Prompt** → AI生成灰度重心法代码
2. **运行测试** → 验证3个测试用例
3. **提交代码** → Git commit
4. **记录耗时** → 更新实际时间

### 本周目标

> 完成W1所有任务，亚像素卡尺精度<0.05px，Blob特征完整可用

---

*本文档每周更新，记录实际Prompt效果和踩坑点*
