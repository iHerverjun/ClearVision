// TemplateMatcher.cs
// 模板匹配器 - 基于 LINEMOD 的适配器实现
// 包装现有的 LineModShapeMatcher，提供统一的 IShapeMatcher 接口
// 作者：蘅芜君

using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// 模板匹配器（LINEMOD 适配器）
/// 基于梯度方向量化和响应图的快速模板匹配
/// </summary>
public class TemplateMatcher : IShapeMatcher
{
    private readonly LineModShapeMatcher _lineModMatcher;
    private readonly int _pyramidLevels;
    private readonly int _angleRange;
    private readonly int _angleStep;
    private Mat? _trainedTemplate;
    private bool _isTrained = false;
    
    public string Name => "TemplateMatcher_LINEMOD";
    
    /// <summary>
    /// 创建模板匹配器
    /// </summary>
    /// <param name="pyramidLevels">金字塔层数（1-5）</param>
    /// <param name="angleRange">角度范围（±度）</param>
    /// <param name="angleStep">角度步长</param>
    public TemplateMatcher(int pyramidLevels = 3, int angleRange = 180, int angleStep = 5)
    {
        _pyramidLevels = Math.Clamp(pyramidLevels, 1, 5);
        _angleRange = Math.Clamp(angleRange, 0, 180);
        _angleStep = Math.Clamp(angleStep, 1, 45);
        
        _lineModMatcher = new LineModShapeMatcher
        {
            PyramidLevels = _pyramidLevels,
            // LINEMOD 内部参数使用默认值，可通过属性暴露
        };
    }
    
    /// <summary>
    /// 训练模板（多角度旋转生成模板金字塔）
    /// </summary>
    public bool Train(Mat template, Rect? roi = null)
    {
        if (template.Empty())
            return false;
        
        _trainedTemplate?.Dispose();
        _trainedTemplate = roi.HasValue 
            ? new Mat(template, roi.Value).Clone() 
            : template.Clone();
        
        try
        {
            // 使用 LINEMOD 训练多角度模板
            var templates = _lineModMatcher.Train(
                _trainedTemplate, 
                null, 
                _angleRange, 
                _angleStep
            );
            
            _isTrained = templates.Count > 0;
            return _isTrained;
        }
        catch
        {
            _isTrained = false;
            return false;
        }
    }
    
    /// <summary>
    /// 执行模板匹配
    /// </summary>
    public List<ShapeMatchResult> Match(Mat searchImage, float minScore, int maxMatches)
    {
        var results = new List<ShapeMatchResult>();
        
        if (!_isTrained || searchImage.Empty())
            return results;
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // 调用 LINEMOD 匹配
            var lineModResults = _lineModMatcher.Match(
                searchImage, 
                minScore / 100.0f,  // 转换为 0-1 范围
                maxMatches
            );
            
            stopwatch.Stop();
            
            // 转换为统一的 ShapeMatchResult
            foreach (var match in lineModResults.Where(m => m.IsValid))
            {
                results.Add(new ShapeMatchResult
                {
                    IsValid = true,
                    Position = match.Position,
                    Score = (float)(match.Score * 100),  // 转换为百分比
                    Angle = (float)match.Angle,
                    Scale = 1.0f,  // LINEMOD 内部处理尺度
                    TemplateWidth = _trainedTemplate?.Width ?? 0,
                    TemplateHeight = _trainedTemplate?.Height ?? 0,
                    MatchTimeMs = stopwatch.ElapsedMilliseconds,
                    Metadata = new Dictionary<string, object>
                    {
                        { "MatchMode", "Template" },
                        { "Algorithm", "LINEMOD" },
                        { "PyramidLevels", _pyramidLevels }
                    }
                });
            }
        }
        catch
        {
            // 匹配失败返回空列表
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
            { "Mode", "Template" },
            { "PyramidLevels", _pyramidLevels },
            { "AngleRange", _angleRange },
            { "AngleStep", _angleStep },
            { "IsTrained", _isTrained }
        };
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _trainedTemplate?.Dispose();
        _lineModMatcher?.Dispose();
    }
}
