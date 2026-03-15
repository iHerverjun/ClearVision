// ShapeDescriptorMatcher.cs
// 形状描述符匹配器 - 基于轮廓几何特征的匹配
// 支持 Hu 矩和傅里叶描述符
// 作者：蘅芜君

using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// 形状描述符配置
/// </summary>
public class ShapeDescriptorConfig
{
    /// <summary>是否使用 Hu 矩</summary>
    public bool UseHuMoments { get; set; } = true;
    
    /// <summary>是否使用傅里叶描述符</summary>
    public bool UseFourierDescriptors { get; set; } = true;
    
    /// <summary>傅里叶描述符系数数量</summary>
    public int FourierDescriptorCount { get; set; } = 10;
    
    /// <summary>面积预筛选容差（0-1）</summary>
    public double AreaTolerance { get; set; } = 0.3;
    
    /// <summary>周长预筛选容差（0-1）</summary>
    public double PerimeterTolerance { get; set; } = 0.3;
    
    /// <summary>最小轮廓点数</summary>
    public int MinContourPoints { get; set; } = 10;
    
    /// <summary>Canny 低阈值</summary>
    public double CannyThreshold1 { get; set; } = 50;
    
    /// <summary>Canny 高阈值</summary>
    public double CannyThreshold2 { get; set; } = 150;
}

/// <summary>
/// 轮廓描述符数据
/// </summary>
public class ContourDescriptor
{
    /// <summary>轮廓点集</summary>
    public Point[] Contour { get; set; } = Array.Empty<Point>();
    
    /// <summary>Hu 矩（7个不变矩）</summary>
    public double[] HuMoments { get; set; } = new double[7];
    
    /// <summary>傅里叶描述符（归一化）</summary>
    public double[] FourierDescriptors { get; set; } = Array.Empty<double>();
    
    /// <summary>面积</summary>
    public double Area { get; set; }
    
    /// <summary>周长</summary>
    public double Perimeter { get; set; }
    
    /// <summary>圆度 (4π*Area/Perimeter²)</summary>
    public double Circularity { get; set; }
    
    /// <summary>矩形度</summary>
    public double Rectangularity { get; set; }
    
    /// <summary>中心点</summary>
    public Point2f Centroid { get; set; }
    
    /// <summary>边界框</summary>
    public Rect BoundingBox { get; set; }
}

/// <summary>
/// 形状描述符匹配器
/// 基于轮廓几何特征（Hu矩、傅里叶描述符）进行形状匹配
/// </summary>
public class ShapeDescriptorMatcher : IShapeMatcher
{
    private readonly ShapeDescriptorConfig _config;
    private ContourDescriptor? _templateDescriptor;
    private bool _isTrained = false;
    
    public string Name => "ShapeDescriptorMatcher";
    
    /// <summary>
    /// 创建形状描述符匹配器
    /// </summary>
    public ShapeDescriptorMatcher(ShapeDescriptorConfig? config = null)
    {
        _config = config ?? new ShapeDescriptorConfig();
    }
    
    /// <summary>
    /// 训练模板 - 提取模板轮廓的描述符
    /// </summary>
    public bool Train(Mat template, Rect? roi = null)
    {
        if (template.Empty())
            return false;
        
        using var roiMat = roi.HasValue 
            ? new Mat(template, roi.Value) 
            : template.Clone();
        
        // 提取模板的主要轮廓
        var contours = ExtractContours(roiMat);
        if (contours.Count == 0)
            return false;
        
        // 选择最大的轮廓作为模板轮廓
        var mainContour = contours.OrderByDescending(c => c.Area).First();
        
        _templateDescriptor = mainContour;
        _isTrained = true;
        
        return true;
    }
    
    /// <summary>
    /// 在搜索图像中执行形状匹配
    /// </summary>
    public List<ShapeMatchResult> Match(Mat searchImage, float minScore, int maxMatches)
    {
        var results = new List<ShapeMatchResult>();
        
        if (!_isTrained || _templateDescriptor == null || searchImage.Empty())
            return results;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // 提取搜索图像中的所有轮廓
        var searchContours = ExtractContours(searchImage);
        if (searchContours.Count == 0)
            return results;
        
        // 预筛选：面积和周长相似的轮廓
        var candidates = PreFilterContours(searchContours, _templateDescriptor);
        
        // 计算描述符距离并排序
        var scoredMatches = new List<(ContourDescriptor contour, double score)>();
        
        foreach (var candidate in candidates)
        {
            double distance = CalculateDescriptorDistance(_templateDescriptor, candidate);
            // 距离转分数 (距离越小分数越高)
            double score = Math.Max(0, 100 - distance * 100);
            
            if (score >= minScore)
            {
                scoredMatches.Add((candidate, score));
            }
        }
        
        // 按分数排序并取前 N 个
        var topMatches = scoredMatches
            .OrderByDescending(m => m.score)
            .Take(maxMatches)
            .ToList();
        
        stopwatch.Stop();
        
        // 转换为结果格式
        foreach (var (contour, score) in topMatches)
        {
            results.Add(new ShapeMatchResult
            {
                IsValid = true,
                Position = new Point((int)contour.Centroid.X, (int)contour.Centroid.Y),
                Score = (float)score,
                Angle = 0,  // 形状描述符不直接提供角度，可通过主轴方向计算
                Scale = (float)Math.Sqrt(contour.Area / _templateDescriptor.Area),
                TemplateWidth = _templateDescriptor.BoundingBox.Width,
                TemplateHeight = _templateDescriptor.BoundingBox.Height,
                MatchTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    { "MatchMode", "ShapeDescriptor" },
                    { "Area", contour.Area },
                    { "Circularity", contour.Circularity },
                    { "HuDistance", CalculateHuDistance(_templateDescriptor, contour) },
                    { "FourierDistance", CalculateFourierDistance(_templateDescriptor, contour) }
                }
            });
        }
        
        return results;
    }
    
    /// <summary>
    /// 获取配置信息
    /// </summary>
    public Dictionary<string, object> GetConfig()
    {
        return new Dictionary<string, object>
        {
            { "Name", Name },
            { "Mode", "ShapeDescriptor" },
            { "UseHuMoments", _config.UseHuMoments },
            { "UseFourierDescriptors", _config.UseFourierDescriptors },
            { "FourierDescriptorCount", _config.FourierDescriptorCount },
            { "AreaTolerance", _config.AreaTolerance },
            { "IsTrained", _isTrained }
        };
    }
    
    /// <summary>
    /// 提取图像中的轮廓并计算描述符
    /// </summary>
    private List<ContourDescriptor> ExtractContours(Mat image)
    {
        var descriptors = new List<ContourDescriptor>();
        
        using var gray = image.Channels() == 1 
            ? image.Clone() 
            : image.CvtColor(ColorConversionCodes.BGR2GRAY);
        
        // Canny 边缘检测
        using var edges = new Mat();
        Cv2.Canny(gray, edges, _config.CannyThreshold1, _config.CannyThreshold2);
        
        // 查找轮廓
        Cv2.FindContours(
            edges, 
            out var contours, 
            out _, 
            RetrievalModes.External, 
            ContourApproximationModes.ApproxSimple
        );
        
        foreach (var contour in contours)
        {
            if (contour.Length < _config.MinContourPoints)
                continue;
            
            var descriptor = ComputeContourDescriptor(contour);
            if (descriptor != null)
                descriptors.Add(descriptor);
        }
        
        return descriptors;
    }
    
    /// <summary>
    /// 计算单个轮廓的描述符
    /// </summary>
    private ContourDescriptor? ComputeContourDescriptor(Point[] contour)
    {
        try
        {
            var descriptor = new ContourDescriptor
            {
                Contour = contour,
                Area = Cv2.ContourArea(contour),
                Perimeter = Cv2.ArcLength(contour, true),
                BoundingBox = Cv2.BoundingRect(contour)
            };
            
            // 计算中心点
            var moments = Cv2.Moments(contour);
            if (moments.M00 > 0)
            {
                descriptor.Centroid = new Point2f(
                    (float)(moments.M10 / moments.M00),
                    (float)(moments.M01 / moments.M00)
                );
            }
            
            // 计算圆度
            if (descriptor.Perimeter > 0)
            {
                descriptor.Circularity = 4 * Math.PI * descriptor.Area / 
                    (descriptor.Perimeter * descriptor.Perimeter);
            }
            
            // 计算矩形度
            double bboxArea = descriptor.BoundingBox.Width * descriptor.BoundingBox.Height;
            if (bboxArea > 0)
            {
                descriptor.Rectangularity = descriptor.Area / bboxArea;
            }
            
            // 计算 Hu 矩
            if (_config.UseHuMoments)
            {
                // OpenCvSharp 中 HuMoments 返回 double[7]
                var huMoments = moments.HuMoments();
                for (int i = 0; i < 7; i++)
                {
                    // 对数变换压缩数值范围
                    huMoments[i] = Math.Sign(huMoments[i]) * Math.Log10(Math.Abs(huMoments[i]) + 1e-10);
                }
                descriptor.HuMoments = huMoments;
            }
            
            // 计算傅里叶描述符
            if (_config.UseFourierDescriptors)
            {
                descriptor.FourierDescriptors = ComputeFourierDescriptors(contour);
            }
            
            return descriptor;
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// 计算傅里叶描述符
    /// </summary>
    private double[] ComputeFourierDescriptors(Point[] contour)
    {
        int n = contour.Length;
        int descriptorCount = Math.Min(_config.FourierDescriptorCount, n / 2);
        
        // 将轮廓转换为复数形式 (x + iy)
        var complexInput = new System.Numerics.Complex[n];
        for (int i = 0; i < n; i++)
        {
            complexInput[i] = new System.Numerics.Complex(contour[i].X, contour[i].Y);
        }
        
        // 执行 FFT
        // 注：为简化依赖，使用手动实现的 DFT 或简单近似
        // 实际项目中可使用 MathNet.Numerics
        var descriptors = new double[descriptorCount];
        
        // 简化的傅里叶描述符计算（DFT）
        for (int k = 0; k < descriptorCount; k++)
        {
            var sum = System.Numerics.Complex.Zero;
            for (int i = 0; i < n; i++)
            {
                double angle = -2 * Math.PI * k * i / n;
                var exp = new System.Numerics.Complex(Math.Cos(angle), Math.Sin(angle));
                sum += complexInput[i] * exp;
            }
            descriptors[k] = sum.Magnitude / n;  // 归一化
        }
        
        // 归一化：使描述符对平移、旋转、尺度不变
        // 除以第一个非零系数
        if (descriptors.Length > 0 && descriptors[0] > 1e-10)
        {
            double first = descriptors[0];
            for (int i = 0; i < descriptors.Length; i++)
            {
                descriptors[i] /= first;
            }
        }
        
        return descriptors;
    }
    
    /// <summary>
    /// 预筛选轮廓（面积、周长相近）
    /// </summary>
    private List<ContourDescriptor> PreFilterContours(
        List<ContourDescriptor> candidates, 
        ContourDescriptor template)
    {
        var filtered = new List<ContourDescriptor>();
        
        double minArea = template.Area * (1 - _config.AreaTolerance);
        double maxArea = template.Area * (1 + _config.AreaTolerance);
        double minPerimeter = template.Perimeter * (1 - _config.PerimeterTolerance);
        double maxPerimeter = template.Perimeter * (1 + _config.PerimeterTolerance);
        
        foreach (var candidate in candidates)
        {
            if (candidate.Area >= minArea && candidate.Area <= maxArea &&
                candidate.Perimeter >= minPerimeter && candidate.Perimeter <= maxPerimeter)
            {
                filtered.Add(candidate);
            }
        }
        
        return filtered.Count > 0 ? filtered : candidates;  // 如果筛选后为空，返回原始列表
    }
    
    /// <summary>
    /// 计算两个轮廓描述符之间的距离
    /// </summary>
    private double CalculateDescriptorDistance(ContourDescriptor d1, ContourDescriptor d2)
    {
        double totalDistance = 0;
        int componentCount = 0;
        
        // Hu 矩距离
        if (_config.UseHuMoments)
        {
            double huDist = CalculateHuDistance(d1, d2);
            totalDistance += huDist * 0.5;  // 权重 0.5
            componentCount++;
        }
        
        // 傅里叶描述符距离
        if (_config.UseFourierDescriptors)
        {
            double fourierDist = CalculateFourierDistance(d1, d2);
            totalDistance += fourierDist * 0.5;  // 权重 0.5
            componentCount++;
        }
        
        return componentCount > 0 ? totalDistance : double.MaxValue;
    }
    
    /// <summary>
    /// 计算 Hu 矩的欧氏距离
    /// </summary>
    private double CalculateHuDistance(ContourDescriptor d1, ContourDescriptor d2)
    {
        double sum = 0;
        for (int i = 0; i < 7; i++)
        {
            double diff = d1.HuMoments[i] - d2.HuMoments[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
    
    /// <summary>
    /// 计算傅里叶描述符的欧氏距离
    /// </summary>
    private double CalculateFourierDistance(ContourDescriptor d1, ContourDescriptor d2)
    {
        int n = Math.Min(d1.FourierDescriptors.Length, d2.FourierDescriptors.Length);
        if (n == 0) return 0;
        
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double diff = d1.FourierDescriptors[i] - d2.FourierDescriptors[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum / n);  // 归一化
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _templateDescriptor = null;
        _isTrained = false;
    }
}
