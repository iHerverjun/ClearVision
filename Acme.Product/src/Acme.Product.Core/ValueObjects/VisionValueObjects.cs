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
