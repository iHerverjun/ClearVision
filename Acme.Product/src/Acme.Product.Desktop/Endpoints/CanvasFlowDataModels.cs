namespace Acme.Product.Desktop.Endpoints;

public class CanvasFlowDataDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "CanvasFlow";
    public List<CanvasOperatorDataDto> Operators { get; set; } = new();
    public List<CanvasConnectionDataDto> Connections { get; set; } = new();
}

public class CanvasOperatorDataDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public List<CanvasPortDataDto>? InputPorts { get; set; }
    public List<CanvasPortDataDto>? OutputPorts { get; set; }
}

public class CanvasPortDataDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}

public class CanvasConnectionDataDto
{
    public Guid Id { get; set; }
    public Guid SourceOperatorId { get; set; }
    public Guid SourcePortId { get; set; }
    public Guid TargetOperatorId { get; set; }
    public Guid TargetPortId { get; set; }
}
