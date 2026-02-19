// VisionValueObjects.cs
// 图像数据值对象 - 封装图像数据及其元数据
// 作者：蘅芜君

using Acme.Product.Core.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Acme.Product.Core.ValueObjects;

/// <summary>
/// 位置值对象 - 表示二维坐标位置
/// </summary>
public class Position : ValueObject
{
    public double X { get; private set; }
    public double Y { get; private set; }

    // EF Core 需要的无参构造函数
    private Position()
    {
    }

    public Position(double x, double y)
    {
        X = x;
        Y = y;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return X;
        yield return Y;
    }

    public override string ToString()
    {
        return $"({X}, {Y})";
    }
}

/// <summary>
/// 端口值对象 - 算子的输入/输出端口
/// </summary>
public class Port : ValueObject
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PortDirection Direction { get; private set; }
    public PortDataType DataType { get; private set; }
    public bool IsRequired { get; private set; }

    // EF Core 需要的无参构造函数
    private Port()
    {
    }

    public Port(Guid id, string name, PortDirection direction, PortDataType dataType, bool isRequired)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Direction = direction;
        DataType = dataType;
        IsRequired = isRequired;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
    }
}

/// <summary>
/// 参数选项（用于下拉列表）
/// </summary>
public class ParameterOption
{
    /// <summary>
    /// 显示标签
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 实际数值
    /// </summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 参数值对象 - 算子参数配置
/// </summary>
public class Parameter : ValueObject
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string DataType { get; private set; } = string.Empty; // int, double, bool, string, enum, etc.
    [NotMapped]
    public string? DefaultValueJson { get; private set; }
    [NotMapped]
    public string? ValueJson { get; private set; }
    [NotMapped]
    public string? MinValueJson { get; private set; }
    [NotMapped]
    public string? MaxValueJson { get; private set; }
    [NotMapped]
    public string? OptionsJson { get; private set; }
    public bool IsRequired { get; private set; }

    // EF Core 需要的无参构造函数
    private Parameter()
    {
    }

    public Parameter(
        Guid id,
        string name,
        string displayName,
        string description,
        string dataType,
        object? defaultValue = null,
        object? minValue = null,
        object? maxValue = null,
        bool isRequired = true,
        List<ParameterOption>? options = null)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? name;
        Description = description ?? string.Empty;
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        DefaultValueJson = SerializeValue(defaultValue);
        ValueJson = SerializeValue(defaultValue);
        MinValueJson = SerializeValue(minValue);
        MaxValueJson = SerializeValue(maxValue);
        OptionsJson = SerializeValue(options);
        IsRequired = isRequired;
    }

    private static string? SerializeValue(object? value)
    {
        return value == null ? null : System.Text.Json.JsonSerializer.Serialize(value);
    }

    private static object? DeserializeValue(string? json)
    {
        return string.IsNullOrEmpty(json) ? null : System.Text.Json.JsonSerializer.Deserialize<object>(json);
    }

    [NotMapped]
    public object? DefaultValue => DeserializeValue(DefaultValueJson);
    [NotMapped]
    public object? Value => DeserializeValue(ValueJson);
    [NotMapped]
    public object? MinValue => DeserializeValue(MinValueJson);
    [NotMapped]
    public object? MaxValue => DeserializeValue(MaxValueJson);
    [NotMapped]
    public List<ParameterOption>? Options => string.IsNullOrEmpty(OptionsJson)
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<List<ParameterOption>>(OptionsJson);

    public void SetValue(object? value)
    {
        // 这里可以添加类型验证逻辑
        ValueJson = SerializeValue(value);
    }

    public object? GetValue()
    {
        return Value ?? DefaultValue;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
    }
}

/// <summary>
/// 算子连接值对象 - 表示算子之间的数据连接
/// </summary>
public class OperatorConnection : ValueObject
{
    public Guid Id { get; private set; }
    public Guid SourceOperatorId { get; private set; }
    public Guid SourcePortId { get; private set; }
    public Guid TargetOperatorId { get; private set; }
    public Guid TargetPortId { get; private set; }

    // EF Core 需要的无参构造函数
    private OperatorConnection()
    {
    }

    public OperatorConnection(
        Guid sourceOperatorId,
        Guid sourcePortId,
        Guid targetOperatorId,
        Guid targetPortId)
    {
        Id = Guid.NewGuid();
        SourceOperatorId = sourceOperatorId;
        SourcePortId = sourcePortId;
        TargetOperatorId = targetOperatorId;
        TargetPortId = targetPortId;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
    }
}

/// <summary>
/// ROI区域值对象 - 表示图像中的感兴趣区域
/// </summary>
public class RegionOfInterest : ValueObject
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public double X { get; private set; }
    public double Y { get; private set; }
    public double Width { get; private set; }
    public double Height { get; private set; }
    public string Color { get; private set; } = "#FF0000";
    public int Thickness { get; private set; }

    // EF Core 需要的无参构造函数
    private RegionOfInterest()
    {
    }

    public RegionOfInterest(
        string name,
        double x,
        double y,
        double width,
        double height,
        string color = "#FF0000",
        int thickness = 2)
    {
        Id = Guid.NewGuid();
        Name = name ?? "ROI";
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Color = color;
        Thickness = thickness;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
    }
}

/// <summary>
/// 图像数据值对象 - 封装图像数据及其元数据
/// </summary>
public class ImageData : ValueObject
{
    public Guid Id { get; private set; }
    public byte[] Data { get; private set; } = Array.Empty<byte>();
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Channels { get; private set; }
    public string Format { get; private set; } = "PNG";
    public DateTime Timestamp { get; private set; }
    public List<RegionOfInterest> Regions { get; private set; } = new();
    public string? Source { get; private set; }

    // EF Core 需要的无参构造函数
    private ImageData()
    {
    }

    public ImageData(
        byte[] data,
        int width,
        int height,
        int channels,
        string format = "PNG",
        string? source = null)
    {
        Id = Guid.NewGuid();
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Width = width;
        Height = height;
        Channels = channels;
        Format = format ?? "PNG";
        Timestamp = DateTime.UtcNow;
        Regions = new List<RegionOfInterest>();
        Source = source;
    }

    public void AddRegion(RegionOfInterest region)
    {
        if (region == null)
            throw new ArgumentNullException(nameof(region));
        Regions.Add(region);
    }

    public bool RemoveRegion(Guid regionId)
    {
        var region = Regions.FirstOrDefault(r => r.Id == regionId);
        if (region != null)
        {
            return Regions.Remove(region);
        }
        return false;
    }

    public void ClearRegions()
    {
        Regions.Clear();
    }

    public string ToBase64()
    {
        return Convert.ToBase64String(Data);
    }

    public static ImageData FromBase64(string base64, int width, int height, int channels, string format = "PNG")
    {
        if (string.IsNullOrEmpty(base64))
            throw new ArgumentNullException(nameof(base64));

        var data = Convert.FromBase64String(base64);
        return new ImageData(data, width, height, channels, format);
    }

    public long SizeBytes => Data.Length;
    public double ResolutionMP => (Width * Height) / 1000000.0;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Id;
    }

    public override string ToString()
    {
        return $"ImageData[{Width}x{Height}, {Channels}ch, {Format}, {Regions.Count} ROIs]";
    }
}

#region Sprint 1 Task 1.2: 端口类型扩展值对象

/// <summary>
/// 检测结果值对象 - 表示单个检测目标（YOLO等深度学习输出）
/// </summary>
public class DetectionResult : ValueObject
{
    /// <summary>类别标签</summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>置信度 (0-1)</summary>
    public float Confidence { get; set; }
    
    /// <summary>边界框 X 坐标</summary>
    public float X { get; set; }
    
    /// <summary>边界框 Y 坐标</summary>
    public float Y { get; set; }
    
    /// <summary>边界框宽度</summary>
    public float Width { get; set; }
    
    /// <summary>边界框高度</summary>
    public float Height { get; set; }
    
    /// <summary>中心点 X</summary>
    public float CenterX => X + Width / 2;
    
    /// <summary>中心点 Y</summary>
    public float CenterY => Y + Height / 2;
    
    /// <summary>面积</summary>
    public float Area => Width * Height;

    public DetectionResult()
    {
    }

    public DetectionResult(string label, float confidence, float x, float y, float width, float height)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Confidence = confidence;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Label;
        yield return Confidence;
        yield return X;
        yield return Y;
        yield return Width;
        yield return Height;
    }

    public override string ToString()
    {
        return $"Detection[{Label}, {Confidence:F2}, ({X:F1},{Y:F1},{Width:F1},{Height:F1})]";
    }
}

/// <summary>
/// 检测结果列表值对象 - 包含多个 DetectionResult
/// </summary>
public class DetectionList : ValueObject
{
    public List<DetectionResult> Detections { get; set; } = new();
    
    /// <summary>检测数量</summary>
    public int Count => Detections.Count;
    
    /// <summary>平均置信度</summary>
    public float AverageConfidence => Detections.Count > 0 ? Detections.Average(d => d.Confidence) : 0;

    public DetectionList()
    {
    }

    public DetectionList(IEnumerable<DetectionResult> detections)
    {
        Detections = detections?.ToList() ?? new List<DetectionResult>();
    }

    public void Add(DetectionResult detection)
    {
        Detections.Add(detection);
    }

    public DetectionResult? GetBestByConfidence()
    {
        return Detections.OrderByDescending(d => d.Confidence).FirstOrDefault();
    }

    public DetectionResult? GetByLabel(string label)
    {
        return Detections.FirstOrDefault(d => d.Label == label);
    }

    public DetectionResult? GetMaxArea()
    {
        return Detections.OrderByDescending(d => d.Area).FirstOrDefault();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Detections.Count;
    }

    public override string ToString()
    {
        return $"DetectionList[{Count} items]";
    }
}

/// <summary>
/// 圆数据值对象 - 表示检测到的圆
/// </summary>
public class CircleData : ValueObject
{
    /// <summary>圆心 X</summary>
    public float CenterX { get; set; }
    
    /// <summary>圆心 Y</summary>
    public float CenterY { get; set; }
    
    /// <summary>半径（像素）</summary>
    public float Radius { get; set; }
    
    /// <summary>直径</summary>
    public float Diameter => Radius * 2;
    
    /// <summary>面积</summary>
    public float Area => (float)(Math.PI * Radius * Radius);
    
    /// <summary>周长</summary>
    public float Circumference => (float)(2 * Math.PI * Radius);

    public CircleData()
    {
    }

    public CircleData(float centerX, float centerY, float radius)
    {
        CenterX = centerX;
        CenterY = centerY;
        Radius = radius;
    }

    /// <summary>
    /// 计算两个圆心之间的距离
    /// </summary>
    public float DistanceTo(CircleData other)
    {
        float dx = CenterX - other.CenterX;
        float dy = CenterY - other.CenterY;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CenterX;
        yield return CenterY;
        yield return Radius;
    }

    public override string ToString()
    {
        return $"Circle[({CenterX:F2}, {CenterY:F2}), R={Radius:F2}]";
    }
}

/// <summary>
/// 直线数据值对象 - 表示检测到的直线
/// </summary>
public class LineData : ValueObject
{
    /// <summary>起点 X</summary>
    public float StartX { get; set; }
    
    /// <summary>起点 Y</summary>
    public float StartY { get; set; }
    
    /// <summary>终点 X</summary>
    public float EndX { get; set; }
    
    /// <summary>终点 Y</summary>
    public float EndY { get; set; }
    
    /// <summary>线段长度</summary>
    public float Length => (float)Math.Sqrt((EndX - StartX) * (EndX - StartX) + (EndY - StartY) * (EndY - StartY));
    
    /// <summary>中点 X</summary>
    public float MidX => (StartX + EndX) / 2;
    
    /// <summary>中点 Y</summary>
    public float MidY => (StartY + EndY) / 2;
    
    /// <summary>角度（相对于水平线，度数）</summary>
    public float Angle => (float)(Math.Atan2(EndY - StartY, EndX - StartX) * 180 / Math.PI);

    public LineData()
    {
    }

    public LineData(float startX, float startY, float endX, float endY)
    {
        StartX = startX;
        StartY = startY;
        EndX = endX;
        EndY = endY;
    }

    /// <summary>
    /// 计算点到直线的距离
    /// </summary>
    public float DistanceToPoint(float x, float y)
    {
        float A = EndY - StartY;
        float B = StartX - EndX;
        float C = EndX * StartY - StartX * EndY;
        return Math.Abs(A * x + B * y + C) / (float)Math.Sqrt(A * A + B * B);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return StartX;
        yield return StartY;
        yield return EndX;
        yield return EndY;
    }

    public override string ToString()
    {
        return $"Line[({StartX:F2}, {StartY:F2}) -> ({EndX:F2}, {EndY:F2}), L={Length:F2}]";
    }
}

#endregion
