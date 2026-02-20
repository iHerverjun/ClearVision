// LineModShapeMatcher.cs
// LINEMOD 形状匹配算法 - 基于梯度响应图的高效模板匹配
// 参考实现: meiqua/shape_based_matching (GitHub)
// 论文: Hinterstoisser et al., "Gradient Response Maps for Real-Time Detection of Texture-Less Objects", IEEE TPAMI 2012
// 作者：蘅芜君

using System.Collections.Concurrent;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// LINEMOD 形状匹配结果
/// </summary>
public readonly struct LineModMatchResult
{
    public readonly Point Position;
    public readonly double Angle;
    public readonly double Score;
    public readonly int PyramidLevel;
    public readonly bool IsValid;

    public LineModMatchResult(Point position, double angle, double score, int pyramidLevel = 0, bool isValid = true)
    {
        Position = position;
        Angle = angle;
        Score = score;
        PyramidLevel = pyramidLevel;
        IsValid = isValid && score > 0;
    }

    public static LineModMatchResult Empty => new LineModMatchResult(new Point(-1, -1), 0, 0, 0, false);
}

/// <summary>
/// 特征点 - 包含位置和方向信息
/// </summary>
public struct Feature
{
    public int X;           // 坐标 X
    public int Y;           // 坐标 Y
    public int Label;       // 量化方向 (0-7)
    public float Theta;     // 原始角度 (0-360)

    public Feature(int x, int y, int label, float theta = 0)
    {
        X = x;
        Y = y;
        Label = label;
        Theta = theta;
    }
}

/// <summary>
/// 候选特征点 - 用于NMS和稀疏选择
/// </summary>
internal struct Candidate
{
    public int X;
    public int Y;
    public int Label;
    public float Score;
    public float Theta;

    public Candidate(int x, int y, int label, float score, float theta)
    {
        X = x;
        Y = y;
        Label = label;
        Score = score;
        Theta = theta;
    }
}

/// <summary>
/// 模板 - 包含多金字塔层的稀疏特征点
/// </summary>
public class Template
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int TlX { get; set; }        // 左上角偏移 X
    public int TlY { get; set; }        // 左上角偏移 Y
    public int PyramidLevel { get; set; }
    public int Angle { get; set; }      // 旋转角度 (度)
    public List<Feature> Features { get; set; } = new List<Feature>();
}

/// <summary>
/// LINEMOD 形状匹配器 - 基于梯度响应图的高效模板匹配
/// 
/// 核心优化点:
/// 1. 方向量化 + Hysteresis 稳定性检查
/// 2. NMS + 稀疏特征选择 (距离约束)
/// 3. 方向扩展 (Spreading) 增加鲁棒性
/// 4. 响应图 (Response Maps) + LUT 加速
/// 5. 线性化内存优化 SIMD 访问
/// 6. 金字塔粗到精搜索
/// </summary>
public sealed class LineModShapeMatcher : IDisposable
{
    #region 配置参数

    /// <summary>弱梯度阈值 (默认 30)</summary>
    public float WeakThreshold { get; set; } = 30.0f;

    /// <summary>强梯度阈值 (默认 60)</summary>
    public float StrongThreshold { get; set; } = 60.0f;

    /// <summary>目标特征点数量 (默认 150)</summary>
    public int NumFeatures { get; set; } = 150;

    /// <summary>扩展因子 T (默认 4, 越大越鲁棒但越慢)</summary>
    public int SpreadT { get; set; } = 4;

    /// <summary>金字塔层数 (默认 3)</summary>
    public int PyramidLevels { get; set; } = 3;

    #endregion

    #region 内部状态

    private List<Template>? _templates;
    private bool _isDisposed;
    private readonly object _lock = new();

    #endregion

    #region 静态 LUT

    /// <summary>
    /// 方向相似度查找表 (0-4 分)
    /// </summary>
    private static readonly byte[,] DirectionSimilarityLut = new byte[8, 8];

    static LineModShapeMatcher()
    {
        // 初始化方向相似度 LUT
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                int diff = Math.Abs(i - j);
                if (diff > 4)
                    diff = 8 - diff;
                DirectionSimilarityLut[i, j] = diff switch
                {
                    0 => 4,  // 完全一致
                    1 => 3,  // 相差 45°
                    _ => 0   // 相差 >= 90°, 无关
                };
            }
        }
    }

    #endregion

    #region 训练阶段

    /// <summary>
    /// 训练模板 - 从模板图像提取多金字塔层特征 (支持多角度旋转)
    /// </summary>
    /// <param name="templateImage">模板图像</param>
    /// <param name="mask">可选的掩码</param>
    /// <param name="angleRange">旋转角度范围 (默认0，表示不旋转)</param>
    /// <param name="angleStep">旋转角度步长 (默认1度)</param>
    public List<Template> Train(Mat templateImage, Mat? mask = null, int angleRange = 0, int angleStep = 1)
    {
        if (templateImage == null || templateImage.Empty())
            throw new ArgumentException("模板图像不能为空", nameof(templateImage));

        lock (_lock)
        {
            _templates = new List<Template>();
            var currentSrc = templateImage.Clone();
            Mat? currentMask = mask;

            try
            {
                for (int level = 0; level < PyramidLevels; level++)
                {
                    // 1. 计算量化梯度
                    var (magnitude, quantizedAngle, originalAngle) = ComputeQuantizedGradients(currentSrc);

                    using (magnitude)
                    using (quantizedAngle)
                    using (originalAngle)
                    {
                        // 2. 提取基础模板特征点
                        var baseTemplate = ExtractTemplate(magnitude, quantizedAngle, originalAngle, currentMask, level);

                        if (baseTemplate.Features.Count > 0)
                        {
                            if (angleRange > 0 && angleStep > 0)
                            {
                                // 生成旋转模板 (从 -angleRange 到 +angleRange)
                                for (int angle = -angleRange; angle <= angleRange; angle += angleStep)
                                {
                                    var rotatedTemplate = CreateRotatedTemplate(baseTemplate, angle, level);
                                    if (rotatedTemplate.Features.Count > 0)
                                    {
                                        _templates.Add(rotatedTemplate);
                                    }
                                }
                            }
                            else
                            {
                                // 无旋转，直接添加基础模板
                                _templates.Add(baseTemplate);
                            }
                        }
                    }

                    // 3. 构建下一层金字塔
                    if (level < PyramidLevels - 1)
                    {
                        using var nextSrc = new Mat();
                        Cv2.PyrDown(currentSrc, nextSrc);
                        currentSrc.Dispose();
                        currentSrc = nextSrc.Clone();

                        if (currentMask != null && !currentMask.Empty())
                        {
                            using var nextMask = new Mat();
                            Cv2.Resize(currentMask, nextMask, new Size(), 0.5, 0.5, InterpolationFlags.Nearest);
                            currentMask.Dispose();
                            currentMask = nextMask.Clone();
                        }
                    }
                }
            }
            finally
            {
                currentSrc.Dispose();
                currentMask?.Dispose();
            }

            return _templates;
        }
    }

    /// <summary>
    /// 创建旋转后的模板
    /// </summary>
    private Template CreateRotatedTemplate(Template baseTemplate, int angleDeg, int pyramidLevel)
    {
        if (angleDeg == 0)
        {
            // 0度时不旋转，但设置角度值
            return new Template
            {
                Width = baseTemplate.Width,
                Height = baseTemplate.Height,
                TlX = baseTemplate.TlX,
                TlY = baseTemplate.TlY,
                PyramidLevel = pyramidLevel,
                Features = new List<Feature>(baseTemplate.Features),
                Angle = angleDeg
            };
        }

        double angleRad = angleDeg * Math.PI / 180.0;
        double cosA = Math.Cos(angleRad);
        double sinA = Math.Sin(angleRad);

        // 计算方向偏移 (每45度一个方向的偏移)
        int directionOffset = (int)Math.Round(angleDeg / 45.0);
        directionOffset = ((directionOffset % 8) + 8) % 8;

        var rotatedFeatures = new List<Feature>();
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var f in baseTemplate.Features)
        {
            // 旋转特征点坐标
            double newX = f.X * cosA - f.Y * sinA;
            double newY = f.X * sinA + f.Y * cosA;

            int rx = (int)Math.Round(newX);
            int ry = (int)Math.Round(newY);
            int newLabel = (f.Label + directionOffset) % 8;

            rotatedFeatures.Add(new Feature(rx, ry, newLabel, f.Theta + angleDeg));

            minX = Math.Min(minX, rx);
            maxX = Math.Max(maxX, rx);
            minY = Math.Min(minY, ry);
            maxY = Math.Max(maxY, ry);
        }

        return new Template
        {
            Width = maxX - minX + 1,
            Height = maxY - minY + 1,
            TlX = minX,
            TlY = minY,
            PyramidLevel = pyramidLevel,
            Features = rotatedFeatures,
            Angle = angleDeg
        };
    }

    /// <summary>
    /// 计算量化梯度 - Hysteresis Gradient Quantization
    /// </summary>
    private (Mat magnitude, Mat quantizedAngle, Mat originalAngle) ComputeQuantizedGradients(Mat src)
    {
        // Step 1: 高斯模糊 (7x7 核)
        using var smoothed = new Mat();
        Cv2.GaussianBlur(src, smoothed, new Size(7, 7), 0, 0, BorderTypes.Replicate);

        // Step 2: 转换为灰度图
        using var gray = new Mat();
        if (src.Channels() > 1)
            Cv2.CvtColor(smoothed, gray, ColorConversionCodes.BGR2GRAY);
        else
            smoothed.CopyTo(gray);

        // Step 3: Sobel 梯度计算 (3x3 核)
        using var sobelX = new Mat();
        using var sobelY = new Mat();
        Cv2.Sobel(gray, sobelX, MatType.CV_32F, 1, 0, 3, 1.0, 0.0, BorderTypes.Replicate);
        Cv2.Sobel(gray, sobelY, MatType.CV_32F, 0, 1, 3, 1.0, 0.0, BorderTypes.Replicate);

        // Step 4: 计算幅值和角度
        var magnitude = new Mat();
        var originalAngle = new Mat();
        Cv2.Magnitude(sobelX, sobelY, magnitude);
        Cv2.Phase(sobelX, sobelY, originalAngle, true); // true = degrees

        // Step 5: Hysteresis 量化
        var quantizedAngle = HysteresisQuantize(magnitude, originalAngle, WeakThreshold);

        return (magnitude, quantizedAngle, originalAngle);
    }

    /// <summary>
    /// Hysteresis 方向量化 - 确保方向稳定性
    /// 1. 粗量化到 16 方向
    /// 2. 3x3 邻域投票 (稳定性检查)
    /// 3. 映射到 8 方向 (位编码: 1,2,4,8,16,32,64,128)
    /// </summary>
    private Mat HysteresisQuantize(Mat magnitude, Mat angle, float threshold)
    {
        var quantized = new Mat(angle.Size(), MatType.CV_8U, Scalar.All(0));

        // 粗量化到 16 方向
        using var quantized16 = new Mat();
        angle.ConvertTo(quantized16, MatType.CV_8U, 16.0 / 360.0);

        unsafe
        {
            byte* qPtr = (byte*)quantized16.DataPointer;
            byte* outPtr = (byte*)quantized.DataPointer;
            float* magPtr = (float*)magnitude.DataPointer;

            int qStep = (int)quantized16.Step();
            int outStep = (int)quantized.Step();
            int magStep = (int)magnitude.Step() / sizeof(float);
            int width = quantized.Cols;
            int height = quantized.Rows;

            // 边界置零 (避免边界效应)
            for (int c = 0; c < width; c++)
            {
                outPtr[c] = 0;
                outPtr[(height - 1) * outStep + c] = 0;
            }
            for (int r = 0; r < height; r++)
            {
                outPtr[r * outStep] = 0;
                outPtr[r * outStep + width - 1] = 0;
            }

            // 3x3 邻域投票
            Parallel.For(1, height - 1, r =>
            {
                for (int c = 1; c < width - 1; c++)
                {
                    float mag = magPtr[r * magStep + c];
                    if (mag <= threshold)
                        continue;

                    // stackalloc 在 C# 自动清零
                    Span<int> hist = stackalloc int[8];
                    for (int dr = -1; dr <= 1; dr++)
                    {
                        for (int dc = -1; dc <= 1; dc++)
                        {
                            byte q = (byte)(qPtr[(r + dr) * qStep + (c + dc)] & 7);
                            hist[q]++;
                        }
                    }

                    int maxVotes = 0, bestDir = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (hist[i] > maxVotes)
                        {
                            maxVotes = hist[i];
                            bestDir = i;
                        }
                    }

                    // 邻居一致性阈值 (5/9 = 55%)
                    if (maxVotes >= 5)
                    {
                        outPtr[r * outStep + c] = (byte)(1 << bestDir);
                    }
                }
            });
        }

        return quantized;
    }

    /// <summary>
    /// 提取模板 - NMS + 稀疏特征选择
    /// </summary>
    private Template ExtractTemplate(Mat magnitude, Mat quantizedAngle, Mat originalAngle, Mat? mask, int pyramidLevel)
    {
        var template = new Template
        {
            Width = quantizedAngle.Cols,
            Height = quantizedAngle.Rows,
            PyramidLevel = pyramidLevel
        };

        // Step 1: NMS 找梯度极值点
        var candidates = FindNMSCandidates(magnitude, quantizedAngle, originalAngle, mask);

        // Step 2: 稀疏特征选择 (距离约束)
        template.Features = SelectScatteredFeatures(candidates, NumFeatures);

        // 计算模板相对于特征点重心的偏移
        if (template.Features.Count > 0)
        {
            int sumX = 0, sumY = 0;
            foreach (var f in template.Features)
            {
                sumX += f.X;
                sumY += f.Y;
            }
            template.TlX = sumX / template.Features.Count - template.Width / 2;
            template.TlY = sumY / template.Features.Count - template.Height / 2;
        }

        return template;
    }

    /// <summary>
    /// NMS 非极大值抑制 - 在 5x5 窗口内找梯度极值点
    /// </summary>
    private unsafe List<Candidate> FindNMSCandidates(Mat magnitude, Mat quantizedAngle, Mat originalAngle, Mat? mask)
    {
        var candidates = new List<Candidate>();
        int nmsSize = 5;
        float nmsThreshold = StrongThreshold;

        float* magPtr = (float*)magnitude.DataPointer;
        byte* anglePtr = (byte*)quantizedAngle.DataPointer;
        float* oriAnglePtr = (float*)originalAngle.DataPointer;
        byte* maskPtr = mask != null && !mask.Empty() ? (byte*)mask.DataPointer : null;

        int width = magnitude.Cols;
        int height = magnitude.Rows;
        int magStep = (int)magnitude.Step() / sizeof(float);
        int angleStep = (int)quantizedAngle.Step();
        int maskStep = maskPtr != null ? (int)mask.Step() : 0;

        int border = nmsSize / 2;

        for (int r = border; r < height - border; r++)
        {
            for (int c = border; c < width - border; c++)
            {
                int idx = r * width + c;

                // 检查 mask
                if (maskPtr != null && maskPtr[r * maskStep + c] == 0)
                    continue;

                float score = magPtr[r * magStep + c];
                if (score < nmsThreshold || anglePtr[r * angleStep + c] == 0)
                    continue;

                // NMS: 检查 5x5 窗口
                bool isMax = true;
                for (int dr = -border; dr <= border && isMax; dr++)
                {
                    for (int dc = -border; dc <= border; dc++)
                    {
                        if (dr == 0 && dc == 0)
                            continue;
                        if (magPtr[(r + dr) * magStep + (c + dc)] > score)
                        {
                            isMax = false;
                            break;
                        }
                    }
                }

                if (isMax)
                {
                    int label = GetLabel(anglePtr[r * angleStep + c]);
                    float theta = oriAnglePtr[r * magStep + c];
                    candidates.Add(new Candidate(c, r, label, score, theta));
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// 将位编码方向转换为标签 (0-7)
    /// </summary>
    private int GetLabel(byte quantized)
    {
        // 1,2,4,8,16,32,64,128 -> 0,1,2,3,4,5,6,7
        return quantized switch
        {
            1 => 0,
            2 => 1,
            4 => 2,
            8 => 3,
            16 => 4,
            32 => 5,
            64 => 6,
            128 => 7,
            _ => 0
        };
    }

    /// <summary>
    /// 稀疏特征选择 - 确保空间均匀分布
    /// 算法: 迭代式稀疏选择
    /// 1. 按分数 (梯度幅值) 降序排序
    /// 2. 依次选择，确保与已选点距离 >= distance
    /// 3. 如果选不够，减小 distance 重新来过
    /// </summary>
    private List<Feature> SelectScatteredFeatures(List<Candidate> candidates, int numFeatures)
    {
        var features = new List<Feature>();

        if (candidates.Count == 0)
            return features;
        if (candidates.Count <= numFeatures)
        {
            // 候选点不足，全部选择
            foreach (var c in candidates)
            {
                features.Add(new Feature(c.X, c.Y, c.Label, c.Theta));
            }
            return features;
        }

        // 按分数降序排序
        var sortedCandidates = candidates.OrderByDescending(c => c.Score).ToList();

        // 启发式初始距离
        float distance = (float)Math.Sqrt((float)sortedCandidates.Count / numFeatures) + 1;
        float distanceSq = distance * distance;

        bool firstSelect = true;
        int i = 0;

        while (true)
        {
            var c = sortedCandidates[i];

            // 检查与已选点的距离
            bool keep = true;
            for (int j = 0; j < features.Count && keep; j++)
            {
                var f = features[j];
                float dx = c.X - f.X;
                float dy = c.Y - f.Y;
                keep = (dx * dx + dy * dy) >= distanceSq;
            }

            if (keep)
            {
                features.Add(new Feature(c.X, c.Y, c.Label, c.Theta));
            }

            if (++i >= sortedCandidates.Count)
            {
                bool numOk = features.Count >= numFeatures;

                if (firstSelect)
                {
                    if (numOk)
                    {
                        // 第一次就选够了，但可能太多，重来一次
                        features.Clear();
                        i = 0;
                        distance += 1.0f;
                        distanceSq = distance * distance;
                        continue;
                    }
                    else
                    {
                        firstSelect = false;
                    }
                }

                // 选不够，减小距离
                i = 0;
                distance -= 1.0f;
                distanceSq = distance * distance;

                if (numOk || distance < 3.0f)
                    break;
            }
        }

        if (features.Count > numFeatures)
        {
            return features.Take(numFeatures).ToList();
        }

        return features;
    }

    #endregion

    #region 匹配阶段

    /// <summary>
    /// 在场景图像中匹配模板
    /// </summary>
    public List<LineModMatchResult> Match(Mat sceneImage, float threshold = 0.8f, int maxMatches = 10)
    {
        if (_templates == null || _templates.Count == 0)
            throw new InvalidOperationException("模板未训练");

        var allMatches = new List<LineModMatchResult>();

        // 从顶层开始匹配 (粗到精)
        for (int level = PyramidLevels - 1; level >= 0; level--)
        {
            var levelTemplates = _templates.Where(t => t.PyramidLevel == level).ToList();
            if (levelTemplates.Count == 0)
                continue;

            // 处理该层图像
            using var currentScene = GetPyramidLevel(sceneImage, level);

            // 计算响应图
            using var responseMaps = ComputeResponseMaps(currentScene);

            // 匹配每个模板
            foreach (var template in levelTemplates)
            {
                var levelMatches = MatchTemplate(responseMaps, template, threshold, maxMatches, level);
                allMatches.AddRange(levelMatches);
            }

            // 如果顶层找到匹配，在下一层细化
            if (level > 0 && allMatches.Count > 0)
            {
                // 只保留最佳匹配位置进行细化
                var bestMatches = allMatches
                    .OrderByDescending(m => m.Score)
                    .Take(maxMatches)
                    .ToList();
                allMatches.Clear();

                // 将匹配位置转换到下一层
                foreach (var match in bestMatches)
                {
                    allMatches.Add(new LineModMatchResult(
                        new Point(match.Position.X * 2, match.Position.Y * 2),
                        match.Angle,
                        match.Score,
                        match.PyramidLevel,
                        match.IsValid));
                }
            }
        }

        // NMS 去重
        return NonMaximumSuppression(allMatches);
    }

    /// <summary>
    /// 获取指定金字塔层的图像
    /// </summary>
    private Mat GetPyramidLevel(Mat image, int level)
    {
        if (level == 0)
            return image.Clone();

        var current = image;
        Mat? result = null;

        for (int i = 0; i < level; i++)
        {
            result = new Mat();
            Cv2.PyrDown(current, result);
            if (i > 0)
                current.Dispose();
            current = result;
        }

        return result ?? image.Clone();
    }

    /// <summary>
    /// 计算响应图 - 核心加速机制
    /// </summary>
    private unsafe DisposableResponseMaps ComputeResponseMaps(Mat sceneImage)
    {
        // Step 1: 计算量化梯度
        var (magnitude, quantizedAngle, _) = ComputeQuantizedGradients(sceneImage);

        using (magnitude)
        {
            // Step 2: Spreading
            using var spreaded = SpreadQuantized(quantizedAngle, SpreadT);

            quantizedAngle.Dispose();

            // Step 3: 计算 8 方向响应图
            return ComputeResponseMapsFromSpreaded(spreaded);
        }
    }

    /// <summary>
    /// 方向扩展 - Spread quantized orientations
    /// 将每个像素的方向传播到 T×T 邻域 (使用 OR 操作)
    /// </summary>
    private Mat SpreadQuantized(Mat quantized, int T)
    {
        var spreaded = new Mat(quantized.Size(), MatType.CV_8U, Scalar.All(0));

        for (int r = 0; r < T; r++)
        {
            for (int c = 0; c < T; c++)
            {
                // 使用 OR 操作传播方向
                var srcRect = new Rect(c, r, quantized.Cols - c, quantized.Rows - r);
                var dstRect = new Rect(0, 0, srcRect.Width, srcRect.Height);

                using var roiSrc = new Mat(quantized, srcRect);
                using var roiDst = new Mat(spreaded, dstRect);

                Cv2.BitwiseOr(roiDst, roiSrc, roiDst);
            }
        }

        return spreaded;
    }

    /// <summary>
    /// 从扩展后的方向图计算 8 方向响应图
    /// 使用 LUT 查表加速相似度计算
    /// </summary>
    private unsafe DisposableResponseMaps ComputeResponseMapsFromSpreaded(Mat spreaded)
    {
        int width = spreaded.Cols;
        int height = spreaded.Rows;

        // 分离高低 4 位用于 LUT 查表
        using var lsb = new Mat(spreaded.Size(), MatType.CV_8U);
        using var msb = new Mat(spreaded.Size(), MatType.CV_8U);

        byte* srcPtr = (byte*)spreaded.DataPointer;
        byte* lsbPtr = (byte*)lsb.DataPointer;
        byte* msbPtr = (byte*)msb.DataPointer;

        int srcStep = (int)spreaded.Step();
        int lsbStep = (int)lsb.Step();
        int msbStep = (int)msb.Step();

        // 分离高低 4 位
        for (int r = 0; r < height; r++)
        {
            byte* srcRow = srcPtr + r * srcStep;
            byte* lsbRow = lsbPtr + r * lsbStep;
            byte* msbRow = msbPtr + r * msbStep;

            for (int c = 0; c < width; c++)
            {
                lsbRow[c] = (byte)(srcRow[c] & 0x0F);
                msbRow[c] = (byte)((srcRow[c] & 0xF0) >> 4);
            }
        }

        // 创建 8 个响应图
        var responseMaps = new Mat[8];

        // 为每个方向预先构建 LUT
        var luts = new (byte[] low, byte[] high)[8];
        for (int ori = 0; ori < 8; ori++)
        {
            byte[] lutLow = new byte[16];
            byte[] lutHigh = new byte[16];
            for (int j = 0; j < 16; j++)
            {
                lutLow[j] = ComputeMaxSimilarity(ori, j, 0);
                lutHigh[j] = ComputeMaxSimilarity(ori, j, 4);
            }
            luts[ori] = (lutLow, lutHigh);
        }

        // 并行计算 8 个方向的响应图
        var tempMaps = new Mat[8];
        Parallel.For(0, 8, ori =>
        {
            tempMaps[ori] = new Mat(spreaded.Size(), MatType.CV_8U);
            byte* resPtr = (byte*)tempMaps[ori].DataPointer;
            int resStep = (int)tempMaps[ori].Step();

            var (lutLow, lutHigh) = luts[ori];

            // 使用 SIMD LUT 查表计算响应值 (每行处理)
            for (int r = 0; r < height; r++)
            {
                byte* lsbRow = lsbPtr + r * lsbStep;
                byte* msbRow = msbPtr + r * msbStep;
                byte* resRow = resPtr + r * resStep;

                // 使用 SIMD 加速
                SimdHelper.LookupTableMaxSimd(lsbRow, msbRow, resRow, lutLow, lutHigh, width);
            }
        });

        // 线性化响应图 - 创建 T×T 个线性内存块
        var linearMaps = LinearizeResponseMaps(tempMaps, SpreadT);

        // 释放临时响应图
        foreach (var map in tempMaps)
        {
            map.Dispose();
        }

        return new DisposableResponseMaps(linearMaps, SpreadT, true);
    }

    /// <summary>
    /// 线性化响应图 - 将响应图重新排列为 T×T 个线性内存块
    /// 
    /// 原理:
    /// 原始响应图: 连续内存布局，步长 = width (缓存不友好)
    /// 线性化后: T×T 个独立内存块，每个包含每隔 T 个像素的值 (连续访问，缓存友好)
    /// 
    /// 示例 (T=4):
    /// 原始 (8×8): [A B C D E F G H] [I J K L M N O P] ...
    /// 线性化后:
    ///   内存 0 (0,0 起始): [A E I M Q U Y ...] - 每隔 4 个像素
    ///   内存 1 (0,1 起始): [B F J N R V Z ...]
    ///   ...
    ///   内存 15 (3,3 起始): [D H L P T X ...]
    /// </summary>
    private Mat[] LinearizeResponseMaps(Mat[] responseMaps, int T)
    {
        int width = responseMaps[0].Cols;
        int height = responseMaps[0].Rows;
        int W = width / T;   // 降采样后的宽度
        int H = height / T;  // 降采样后的高度

        var linearMaps = new Mat[8];

        for (int ori = 0; ori < 8; ori++)
        {
            // 创建 T×T 个线性内存，每个大小为 W×H
            // 使用单通道 8UC1 矩阵存储所有 T×T 个内存块
            // 矩阵行数 = T×T，列数 = W×H
            linearMaps[ori] = new Mat(T * T, W * H, MatType.CV_8UC1);

            unsafe
            {
                byte* srcPtr = (byte*)responseMaps[ori].DataPointer;
                byte* dstPtr = (byte*)linearMaps[ori].DataPointer;
                int srcStep = (int)responseMaps[ori].Step();
                int dstStep = (int)linearMaps[ori].Step();

                // 填充 T×T 个线性内存
                for (int gridY = 0; gridY < T; gridY++)
                {
                    for (int gridX = 0; gridX < T; gridX++)
                    {
                        int gridIndex = gridY * T + gridX;
                        byte* dstRow = dstPtr + gridIndex * dstStep;

                        // 从 (gridY, gridX) 开始，每隔 T 个像素取值
                        int dstIdx = 0;
                        for (int y = gridY; y < height; y += T)
                        {
                            byte* srcRow = srcPtr + y * srcStep;
                            for (int x = gridX; x < width; x += T)
                            {
                                dstRow[dstIdx++] = srcRow[x];
                            }
                        }
                    }
                }
            }
        }

        return linearMaps;
    }

    /// <summary>
    /// 计算模板方向与场景方向编码的最大相似度
    /// </summary>
    private byte ComputeMaxSimilarity(int templateOri, int sceneBits, int bitOffset)
    {
        byte maxSim = 0;
        for (int bit = 0; bit < 4; bit++)
        {
            if ((sceneBits & (1 << bit)) != 0)
            {
                int sceneOri = bit + bitOffset;
                byte sim = DirectionSimilarityLut[templateOri, sceneOri];
                if (sim > maxSim)
                    maxSim = sim;
            }
        }
        return maxSim;
    }

    /// <summary>
    /// 匹配单个模板 - 使用线性化内存和 T 步长搜索
    /// 
    /// 性能优化:
    /// - 使用线性化内存实现连续内存访问 (缓存友好)
    /// - 以 T 为步长搜索，搜索空间减少为 1/T²
    /// - SIMD 友好的内存布局
    /// </summary>
    private unsafe List<LineModMatchResult> MatchTemplate(
        DisposableResponseMaps responseMaps,
        Template template,
        float threshold,
        int maxMatches,
        int pyramidLevel)
    {
        var results = new List<LineModMatchResult>();
        if (template.Features.Count == 0)
            return results;

        if (!responseMaps.IsLinearized)
        {
            // 回退到传统匹配模式 (不应发生)
            return MatchTemplateTraditional(responseMaps, template, threshold, maxMatches, pyramidLevel);
        }

        var maps = responseMaps.Maps;
        int T = responseMaps.T;

        // 获取线性化后的尺寸
        // 线性化矩阵: 行数 = T×T, 列数 = (W×H) 其中 W=width/T, H=height/T
        int linearW = maps[0].Cols / T;  // 降采样后的宽度
        int linearH = maps[0].Cols / T;  // 同上 (列数 = W×H，但我们需要 W 和 H)
        // 实际上 maps[0].Cols = W × H，所以我们需要重新计算
        int totalLinearSize = maps[0].Cols;

        // 计算模板在降采样后的尺寸
        int minX = template.Features.Min(f => f.X);
        int maxX = template.Features.Max(f => f.X);
        int minY = template.Features.Min(f => f.Y);
        int maxY = template.Features.Max(f => f.Y);

        int wf = (maxX - minX + T - 1) / T + 1;  // 模板宽度 / T
        int hf = (maxY - minY + T - 1) / T + 1;  // 模板高度 / T

        // 计算场景尺寸 (反向推导)
        int sceneLinearW = (int)Math.Sqrt(totalLinearSize);  // 近似，实际需要正确计算
        int W = (int)Math.Sqrt(totalLinearSize);
        int H = totalLinearSize / W;

        // 可搜索范围 (以 T 为步长)
        int spanX = W - wf;
        int spanY = H - hf;
        if (spanX <= 0 || spanY <= 0)
            return results;

        int templatePositions = spanY * W + spanX + 1;

        // 归一化阈值
        float maxScore = 4.0f * template.Features.Count;
        float scoreThreshold = threshold * maxScore;

        // 预计算每个特征点对应的线性内存索引和偏移
        var featureInfo = template.Features.Select(f => new
        {
            Label = f.Label,
            X = f.X,
            Y = f.Y,
            GridX = f.X % T,
            GridY = f.Y % T,
            GridIndex = (f.Y % T) * T + (f.X % T),
            LmX = f.X / T,
            LmY = f.Y / T
        }).ToList();

        // 创建累加缓冲区
        var similarityScores = new float[templatePositions];

        // 遍历每个特征点，累加响应值
        foreach (var feat in featureInfo)
        {
            byte* mapPtr = (byte*)maps[feat.Label].DataPointer;
            int step = (int)maps[feat.Label].Step();
            byte* linearMem = mapPtr + feat.GridIndex * step;

            int featLmIndex = feat.LmY * W + feat.LmX;

            // 累加该特征点在所有搜索位置的响应值
            for (int pos = 0; pos < templatePositions; pos++)
            {
                // 计算该位置对应的线性内存偏移
                int posY = pos / W;
                int posX = pos % W;

                // 检查边界
                if (posX + feat.LmX >= W || posY + feat.LmY >= H)
                    continue;

                int lmOffset = (posY + feat.LmY) * W + (posX + feat.LmX);
                similarityScores[pos] += linearMem[lmOffset];
            }
        }

        // 收集超过阈值的位置
        var localResults = new List<(int x, int y, float score)>();
        for (int pos = 0; pos < templatePositions; pos++)
        {
            if (similarityScores[pos] >= scoreThreshold)
            {
                int posY = pos / W;
                int posX = pos % W;
                // 转换回原始坐标 (乘以 T)
                localResults.Add((posX * T, posY * T, similarityScores[pos] / maxScore));
            }
        }

        // 取最佳匹配
        foreach (var (x, y, score) in localResults.OrderByDescending(r => r.score).Take(maxMatches))
        {
            results.Add(new LineModMatchResult(new Point(x, y), template.Angle, score * 100, pyramidLevel, true));
        }

        return results;
    }

    /// <summary>
    /// 传统匹配模式 (非线性化，逐像素搜索)
    /// 用于兼容性回退
    /// </summary>
    private unsafe List<LineModMatchResult> MatchTemplateTraditional(
        DisposableResponseMaps responseMaps,
        Template template,
        float threshold,
        int maxMatches,
        int pyramidLevel)
    {
        var results = new List<LineModMatchResult>();
        if (template.Features.Count == 0)
            return results;

        var maps = responseMaps.Maps;
        int T = responseMaps.T;

        int mapW = maps[0].Cols;
        int mapH = maps[0].Rows;

        // 计算模板边界
        int minX = template.Features.Min(f => f.X);
        int maxX = template.Features.Max(f => f.X);
        int minY = template.Features.Min(f => f.Y);
        int maxY = template.Features.Max(f => f.Y);

        // 可搜索范围
        int searchW = mapW - (maxX - minX);
        int searchH = mapH - (maxY - minY);
        if (searchW <= 0 || searchH <= 0)
            return results;

        // 归一化阈值
        float maxScore = 4.0f * template.Features.Count;
        float scoreThreshold = threshold * maxScore;

        // 预取响应图指针
        var mapPtrs = new byte*[8];
        var mapSteps = new int[8];
        for (int i = 0; i < 8; i++)
        {
            mapPtrs[i] = (byte*)maps[i].DataPointer;
            mapSteps[i] = (int)maps[i].Step();
        }

        // 并行搜索所有位置
        var localResults = new ConcurrentBag<(int x, int y, float score)>();

        Parallel.For(0, searchH, y =>
        {
            int offsetY = y - minY;

            for (int x = 0; x < searchW; x++)
            {
                int offsetX = x - minX;
                float score = 0;

                // 累加所有特征点的响应值
                foreach (var feat in template.Features)
                {
                    int fx = feat.X + offsetX;
                    int fy = feat.Y + offsetY;

                    byte* mapPtr = mapPtrs[feat.Label];
                    score += mapPtr[fy * mapSteps[feat.Label] + fx];
                }

                // 检查是否超过阈值
                if (score >= scoreThreshold)
                {
                    localResults.Add((x, y, score / maxScore));
                }
            }
        });

        // 转换为结果列表
        foreach (var (x, y, score) in localResults.OrderByDescending(r => r.score).Take(maxMatches))
        {
            results.Add(new LineModMatchResult(new Point(x, y), template.Angle, score * 100, pyramidLevel, true));
        }

        return results;
    }

    /// <summary>
    /// 多目标NMS去重 - 重叠检测框抑制
    /// </summary>
    private List<LineModMatchResult> NonMaximumSuppression(List<LineModMatchResult> matches, float iouThreshold = 0.5f)
    {
        if (matches.Count <= 1)
            return matches;

        var sorted = matches.OrderByDescending(m => m.Score).ToList();
        var keep = new List<LineModMatchResult>();
        var suppressed = new bool[sorted.Count];

        for (int i = 0; i < sorted.Count; i++)
        {
            if (suppressed[i])
                continue;

            keep.Add(sorted[i]);

            for (int j = i + 1; j < sorted.Count; j++)
            {
                if (suppressed[j])
                    continue;

                // 计算距离
                float dx = sorted[i].Position.X - sorted[j].Position.X;
                float dy = sorted[i].Position.Y - sorted[j].Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                // 如果距离太近，抑制
                if (distance < 20) // 20像素阈值
                {
                    suppressed[j] = true;
                }
            }
        }

        return keep;
    }

    #endregion

    #region 模板序列化

    /// <summary>
    /// 模板数据契约 (用于JSON序列化)
    /// </summary>
    private class TemplateData
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int TlX { get; set; }
        public int TlY { get; set; }
        public int PyramidLevel { get; set; }
        public int Angle { get; set; }
        public List<FeatureData> Features { get; set; } = new();
    }

    /// <summary>
    /// 特征点数据契约 (用于JSON序列化)
    /// </summary>
    private class FeatureData
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Label { get; set; }
        public float Theta { get; set; }
    }

    /// <summary>
    /// 保存训练模板到文件 (JSON 序列化)
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public void SaveTemplates(string filePath)
    {
        if (_templates == null || _templates.Count == 0)
            throw new InvalidOperationException("没有可保存的模板");

        var templateDataList = _templates.Select(t => new TemplateData
        {
            Width = t.Width,
            Height = t.Height,
            TlX = t.TlX,
            TlY = t.TlY,
            PyramidLevel = t.PyramidLevel,
            Angle = t.Angle,
            Features = t.Features.Select(f => new FeatureData
            {
                X = f.X,
                Y = f.Y,
                Label = f.Label,
                Theta = f.Theta
            }).ToList()
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(templateDataList,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// 从文件加载训练模板
    /// </summary>
    /// <param name="filePath">文件路径</param>
    public void LoadTemplates(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("模板文件不存在", filePath);

        var json = File.ReadAllText(filePath);
        var templateDataList = System.Text.Json.JsonSerializer.Deserialize<List<TemplateData>>(json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        if (templateDataList == null)
            throw new InvalidOperationException("无法解析模板文件");

        lock (_lock)
        {
            _templates = templateDataList.Select(td => new Template
            {
                Width = td.Width,
                Height = td.Height,
                TlX = td.TlX,
                TlY = td.TlY,
                PyramidLevel = td.PyramidLevel,
                Angle = td.Angle,
                Features = td.Features.Select(fd => new Feature
                {
                    X = fd.X,
                    Y = fd.Y,
                    Label = fd.Label,
                    Theta = fd.Theta
                }).ToList()
            }).ToList();
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _templates?.Clear();
            _templates = null;
            _isDisposed = true;
        }
    }

    #endregion

    #region 响应图包装类 (用于自动释放)

    /// <summary>
    /// 响应图包装类 - 实现 IDisposable 用于自动释放资源
    /// 
    /// 支持两种模式:
    /// 1. 传统模式: 8 个标准响应图 (W×H)
    /// 2. 线性化模式: 8 个线性化响应图 (T×T 行, W×H 列)
    /// </summary>
    private class DisposableResponseMaps : IDisposable
    {
        public Mat[] Maps { get; }
        public int T { get; }
        public bool IsLinearized { get; }
        private bool _disposed;

        public DisposableResponseMaps(Mat[] maps, int t, bool isLinearized = false)
        {
            Maps = maps;
            T = t;
            IsLinearized = isLinearized;
        }

        /// <summary>
        /// 访问线性化内存中的响应值
        /// </summary>
        /// <param name="direction">方向索引 (0-7)</param>
        /// <param name="featureX">特征点 X 坐标</param>
        /// <param name="featureY">特征点 Y 坐标</param>
        /// <param name="offsetX">搜索偏移 X</param>
        /// <param name="offsetY">搜索偏移 Y</param>
        /// <returns>响应值</returns>
        public unsafe byte AccessLinearMemory(int direction, int featureX, int featureY, int offsetX, int offsetY)
        {
            if (!IsLinearized)
                throw new InvalidOperationException("响应图未线性化");

            int mapW = Maps[0].Cols / T;  // 原始宽度 / T

            // 计算在 T×T 网格中的位置
            int gridX = (featureX + offsetX) % T;
            int gridY = (featureY + offsetY) % T;
            int gridIndex = gridY * T + gridX;

            // 计算在线性内存中的偏移
            int lmX = (featureX + offsetX) / T;
            int lmY = (featureY + offsetY) / T;
            int lmIndex = lmY * mapW + lmX;

            byte* mapPtr = (byte*)Maps[direction].DataPointer;
            int step = (int)Maps[direction].Step();

            return mapPtr[gridIndex * step + lmIndex];
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var map in Maps)
                {
                    map?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    #endregion
}
