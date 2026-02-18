// DemoProjectService.cs
// 演示步骤DTO
// 作者：蘅芜君

using Acme.Product.Application.DTOs;
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Interfaces;
using Acme.Product.Core.ValueObjects;

namespace Acme.Product.Application.Services;

/// <summary>
/// 演示工程服务 - 创建示例工程用于演示
/// </summary>
public class DemoProjectService
{
    private readonly IProjectRepository _projectRepository;

    public DemoProjectService(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    /// <summary>
    /// 创建一个演示工程 - PCB缺陷检测流程
    /// </summary>
    public async Task<ProjectDto> CreateDemoProjectAsync()
    {
        // 创建工程
        var project = new Project("PCB缺陷检测演示", "用于演示工业视觉检测的示例工程");

        // 添加全局设置
        project.SetGlobalSetting("InspectionMode", "Single");
        project.SetGlobalSetting("SaveResults", "true");
        project.SetGlobalSetting("DefectThreshold", "0.75");

        // 获取流程并设置名称
        var flow = project.Flow;

        // 1. 图像采集算子
        var acquisitionOp = new Core.Entities.Operator(
            "图像采集",
            Core.Enums.OperatorType.ImageAcquisition,
            100, 100);
        acquisitionOp.AddInputPort("input", PortDataType.Image, false);
        acquisitionOp.AddOutputPort("output", PortDataType.Image);
        acquisitionOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Source", "图像来源", "选择图像来源类型", "enum", "File", null, null, true));
        acquisitionOp.AddParameter(new Parameter(
            Guid.NewGuid(), "FilePath", "文件路径", "图像文件路径", "string", "", null, null, false));
        acquisitionOp.AddParameter(new Parameter(
            Guid.NewGuid(), "CameraId", "相机ID", "相机设备ID", "string", "camera-001", null, null, false));
        flow.AddOperator(acquisitionOp);

        // 2. 高斯滤波算子
        var blurOp = new Core.Entities.Operator(
            "高斯滤波",
            Core.Enums.OperatorType.Preprocessing,
            300, 100);
        blurOp.AddInputPort("input", PortDataType.Image, true);
        blurOp.AddOutputPort("output", PortDataType.Image);
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "KernelSize", "核大小", "高斯核大小（奇数）", "int", 5, 1, 51, true));
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "SigmaX", "X方向标准差", "X方向高斯标准差", "double", 1.5, 0.1, 10.0, true));
        blurOp.AddParameter(new Parameter(
            Guid.NewGuid(), "SigmaY", "Y方向标准差", "Y方向高斯标准差", "double", 1.5, 0.1, 10.0, true));
        flow.AddOperator(blurOp);

        // 创建端口并连接：图像采集 -> 高斯滤波
        var acqOutputPort = acquisitionOp.OutputPorts.First();
        var blurInputPort = blurOp.InputPorts.First();
        flow.AddConnection(new OperatorConnection(
            acquisitionOp.Id, acqOutputPort.Id,
            blurOp.Id, blurInputPort.Id));

        // 3. Canny边缘检测算子
        var cannyOp = new Core.Entities.Operator(
            "边缘检测",
            Core.Enums.OperatorType.EdgeDetection,
            500, 100);
        cannyOp.AddInputPort("input", PortDataType.Image, true);
        cannyOp.AddOutputPort("output", PortDataType.Image);
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold1", "低阈值", "Canny低阈值", "int", 50, 0, 255, true));
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold2", "高阈值", "Canny高阈值", "int", 150, 0, 255, true));
        cannyOp.AddParameter(new Parameter(
            Guid.NewGuid(), "ApertureSize", " Sobel核大小", "Sobel算子核大小", "int", 3, 3, 7, true));
        flow.AddOperator(cannyOp);

        // 连接：高斯滤波 -> Canny边缘检测
        var blurOutputPort = blurOp.OutputPorts.First();
        var cannyInputPort = cannyOp.InputPorts.First();
        flow.AddConnection(new OperatorConnection(
            blurOp.Id, blurOutputPort.Id,
            cannyOp.Id, cannyInputPort.Id));

        // 4. 二值化算子
        var thresholdOp = new Core.Entities.Operator(
            "二值化",
            Core.Enums.OperatorType.Thresholding,
            700, 100);
        thresholdOp.AddInputPort("input", PortDataType.Image, true);
        thresholdOp.AddOutputPort("output", PortDataType.Image);
        thresholdOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Threshold", "阈值", "二值化阈值", "int", 127, 0, 255, true));
        thresholdOp.AddParameter(new Parameter(
            Guid.NewGuid(), "MaxValue", "最大值", "最大值（白色）", "int", 255, 0, 255, true));
        thresholdOp.AddParameter(new Parameter(
            Guid.NewGuid(), "ThresholdType", "阈值类型", "阈值处理方式", "enum", "Binary", null, null, true));
        flow.AddOperator(thresholdOp);

        // 连接：Canny边缘检测 -> 二值化
        var cannyOutputPort = cannyOp.OutputPorts.First();
        var thresholdInputPort = thresholdOp.InputPorts.First();
        flow.AddConnection(new OperatorConnection(
            cannyOp.Id, cannyOutputPort.Id,
            thresholdOp.Id, thresholdInputPort.Id));

        // 5. 轮廓查找算子
        var contourOp = new Core.Entities.Operator(
            "轮廓查找",
            Core.Enums.OperatorType.ContourDetection,
            900, 100);
        contourOp.AddInputPort("input", PortDataType.Image, true);
        contourOp.AddOutputPort("contours", PortDataType.Contour);
        contourOp.AddOutputPort("output", PortDataType.Image);
        contourOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Mode", "检索模式", "轮廓检索模式", "enum", "External", null, null, true));
        contourOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Method", "近似方法", "轮廓近似方法", "enum", "Simple", null, null, true));
        contourOp.AddParameter(new Parameter(
            Guid.NewGuid(), "MinArea", "最小面积", "最小轮廓面积", "int", 100, 1, 100000, true));
        contourOp.AddParameter(new Parameter(
            Guid.NewGuid(), "MaxArea", "最大面积", "最大轮廓面积", "int", 10000, 1, 1000000, true));
        flow.AddOperator(contourOp);

        // 连接：二值化 -> 轮廓查找
        var thresholdOutputPort = thresholdOp.OutputPorts.First();
        var contourInputPort = contourOp.InputPorts.First();
        flow.AddConnection(new OperatorConnection(
            thresholdOp.Id, thresholdOutputPort.Id,
            contourOp.Id, contourInputPort.Id));

        // 6. 结果输出算子
        var outputOp = new Core.Entities.Operator(
            "结果输出",
            Core.Enums.OperatorType.ResultOutput,
            1100, 100);
        outputOp.AddInputPort("image", PortDataType.Image, false);
        outputOp.AddInputPort("contours", PortDataType.Contour, false);
        outputOp.AddOutputPort("result", PortDataType.String);
        outputOp.AddParameter(new Parameter(
            Guid.NewGuid(), "SaveImage", "保存图像", "是否保存结果图像", "bool", true, null, null, true));
        outputOp.AddParameter(new Parameter(
            Guid.NewGuid(), "OutputPath", "输出路径", "结果保存路径", "string", "./results", null, null, true));
        flow.AddOperator(outputOp);

        // 连接：轮廓查找 -> 结果输出
        var contourOutputPort = contourOp.OutputPorts.First(p => p.Name == "contours");
        var outputInputPort = outputOp.InputPorts.First(p => p.Name == "contours");
        flow.AddConnection(new OperatorConnection(
            contourOp.Id, contourOutputPort.Id,
            outputOp.Id, outputInputPort.Id));

        // 保存工程
        await _projectRepository.AddAsync(project);

        return MapProjectToDto(project);
    }

    /// <summary>
    /// 创建简单演示工程 - 只有图像采集和结果输出
    /// </summary>
    public async Task<ProjectDto> CreateSimpleDemoProjectAsync()
    {
        var project = new Project("简单检测演示", "最简化的检测流程示例");

        var flow = project.Flow;

        // 图像采集
        var acquisitionOp = new Core.Entities.Operator(
            "图像采集",
            Core.Enums.OperatorType.ImageAcquisition,
            150, 150);
        acquisitionOp.AddInputPort("input", PortDataType.Image, false);
        acquisitionOp.AddOutputPort("output", PortDataType.Image);
        acquisitionOp.AddParameter(new Parameter(
            Guid.NewGuid(), "Source", "图像来源", "选择图像来源类型", "enum", "File", null, null, true));
        flow.AddOperator(acquisitionOp);

        // 结果输出
        var outputOp = new Core.Entities.Operator(
            "结果输出",
            Core.Enums.OperatorType.ResultOutput,
            450, 150);
        outputOp.AddInputPort("input", PortDataType.Image, true);
        outputOp.AddOutputPort("result", PortDataType.String);
        outputOp.AddParameter(new Parameter(
            Guid.NewGuid(), "SaveImage", "保存图像", "是否保存结果图像", "bool", false, null, null, true));
        flow.AddOperator(outputOp);

        // 连接
        var acqPort = acquisitionOp.OutputPorts.First();
        var outputPort = outputOp.InputPorts.First();
        flow.AddConnection(new OperatorConnection(
            acquisitionOp.Id, acqPort.Id,
            outputOp.Id, outputPort.Id));

        await _projectRepository.AddAsync(project);

        return MapProjectToDto(project);
    }

    /// <summary>
    /// 获取演示说明
    /// </summary>
    public DemoGuideDto GetDemoGuide()
    {
        return new DemoGuideDto
        {
            Title = "ClearVision 演示指南",
            Description = "本演示展示了一个完整的PCB缺陷检测流程",
            Steps = new List<DemoStepDto>
            {
                new DemoStepDto
                {
                    Step = 1,
                    Title = "选择图像",
                    Description = "点击工具栏的'打开图像'按钮，选择一张PCB图像文件（支持JPG、PNG、BMP格式）",
                    ExpectedResult = "图像显示在主窗口中"
                },
                new DemoStepDto
                {
                    Step = 2,
                    Title = "配置算子",
                    Description = "在流程编辑器中，点击各个算子节点，在右侧属性面板中调整参数",
                    ExpectedResult = "算子参数更新"
                },
                new DemoStepDto
                {
                    Step = 3,
                    Title = "运行检测",
                    Description = "点击工具栏的'运行'按钮（▶）执行完整检测流程",
                    ExpectedResult = "检测进度显示，完成后显示结果"
                },
                new DemoStepDto
                {
                    Step = 4,
                    Title = "查看结果",
                    Description = "切换到'结果'视图，查看检测统计和缺陷列表",
                    ExpectedResult = "显示OK/NG判定、缺陷数量和位置"
                }
            },
            Tips = new List<string>
            {
                "可以通过拖拽算子库中的算子到画布来添加新算子",
                "在算子之间拖动连线来建立数据流",
                "使用鼠标滚轮缩放画布，按住空格拖动平移",
                "检测结果会实时显示在图像查看器中，缺陷用红色方框标注"
            }
        };
    }

    private ProjectDto MapProjectToDto(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            Version = project.Version,
            CreatedAt = project.CreatedAt,
            ModifiedAt = project.ModifiedAt,
            LastOpenedAt = project.LastOpenedAt,
            GlobalSettings = project.GlobalSettings,
            Flow = new OperatorFlowDto
            {
                Id = project.Flow.Id,
                Name = project.Flow.Name,
                Operators = project.Flow.Operators.Select(op => new OperatorDto
                {
                    Id = op.Id,
                    Name = op.Name,
                    Type = op.Type,
                    X = op.Position.X,
                    Y = op.Position.Y,
                    IsEnabled = op.IsEnabled,
                    ExecutionStatus = op.ExecutionStatus,
                    ExecutionTimeMs = op.ExecutionTimeMs,
                    ErrorMessage = op.ErrorMessage,
                    InputPorts = op.InputPorts.Select(p => new PortDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Direction = p.Direction,
                        DataType = p.DataType,
                        IsRequired = p.IsRequired
                    }).ToList(),
                    OutputPorts = op.OutputPorts.Select(p => new PortDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Direction = p.Direction,
                        DataType = p.DataType,
                        IsRequired = p.IsRequired
                    }).ToList(),
                    Parameters = op.Parameters.Select(p => new ParameterDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        DisplayName = p.DisplayName,
                        Description = p.Description,
                        DataType = p.DataType,
                        Value = p.GetValue(),
                        DefaultValue = p.DefaultValue,
                        MinValue = p.MinValue,
                        MaxValue = p.MaxValue,
                        IsRequired = p.IsRequired
                    }).ToList()
                }).ToList(),
                Connections = project.Flow.Connections.Select(conn => new OperatorConnectionDto
                {
                    Id = conn.Id,
                    SourceOperatorId = conn.SourceOperatorId,
                    SourcePortId = conn.SourcePortId,
                    TargetOperatorId = conn.TargetOperatorId,
                    TargetPortId = conn.TargetPortId
                }).ToList()
            }
        };
    }
}

/// <summary>
/// 演示指南DTO
/// </summary>
public class DemoGuideDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DemoStepDto> Steps { get; set; } = new();
    public List<string> Tips { get; set; } = new();
}

/// <summary>
/// 演示步骤DTO
/// </summary>
public class DemoStepDto
{
    public int Step { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
}
