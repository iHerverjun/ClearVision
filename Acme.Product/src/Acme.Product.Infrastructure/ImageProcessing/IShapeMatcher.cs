// IShapeMatcher.cs
// 形状匹配器接口 - 统一模板匹配和形状描述符匹配的契约
// 作者：蘅芜君

using OpenCvSharp;

namespace Acme.Product.Infrastructure.ImageProcessing;

/// <summary>
/// 形状匹配结果
/// </summary>
public class ShapeMatchResult
{
    /// <summary>是否有效匹配</summary>
    public bool IsValid { get; set; }
    
    /// <summary>匹配位置（中心点）</summary>
    public Point Position { get; set; }
    
    /// <summary>匹配分数 (0-100)</summary>
    public float Score { get; set; }
    
    /// <summary>旋转角度（度）</summary>
    public float Angle { get; set; }
    
    /// <summary>尺度因子</summary>
    public float Scale { get; set; } = 1.0f;
    
    /// <summary>模板宽度</summary>
    public int TemplateWidth { get; set; }
    
    /// <summary>模板高度</summary>
    public int TemplateHeight { get; set; }
    
    /// <summary>匹配时间（ms）</summary>
    public long MatchTimeMs { get; set; }
    
    /// <summary>额外信息</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// 形状匹配器接口
/// </summary>
public interface IShapeMatcher : IDisposable
{
    /// <summary>
    /// 匹配器名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 训练模板
    /// </summary>
    /// <param name="template">模板图像</param>
    /// <param name="roi">感兴趣区域（null表示全图）</param>
    /// <returns>是否训练成功</returns>
    bool Train(Mat template, Rect? roi = null);
    
    /// <summary>
    /// 在搜索图像中执行匹配
    /// </summary>
    /// <param name="searchImage">搜索图像</param>
    /// <param name="minScore">最小匹配分数 (0-1)</param>
    /// <param name="maxMatches">最大返回匹配数</param>
    /// <returns>匹配结果列表</returns>
    List<ShapeMatchResult> Match(Mat searchImage, float minScore, int maxMatches);
    
    /// <summary>
    /// 获取匹配器配置信息
    /// </summary>
    Dictionary<string, object> GetConfig();
}
