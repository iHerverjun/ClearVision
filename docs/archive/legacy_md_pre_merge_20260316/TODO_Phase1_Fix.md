# Phase 1 修复指令

Phase 1 的 4 个算子文件和枚举已创建，但缺少 DI 注册、工厂元数据、CameraCalibration 改进和单元测试。请按以下步骤补全。

---

## 1. DI 注册 (DependencyInjection.cs)

在 `src\Acme.Product.Desktop\DependencyInjection.cs` 的 `// Phase 3 新增算子` 注释之前添加：

```csharp
// Phase 1 关键能力补齐
services.AddSingleton<IOperatorExecutor, GeometricFittingOperator>();
services.AddSingleton<IOperatorExecutor, RoiManagerOperator>();
services.AddSingleton<IOperatorExecutor, ShapeMatchingOperator>();
services.AddSingleton<IOperatorExecutor, SubpixelEdgeDetectionOperator>();
```

---

## 2. 工厂元数据 (OperatorFactory.cs)

在 `src\Acme.Product.Infrastructure\Services\OperatorFactory.cs` 的 `InitializeDefaultOperators()` 方法末尾添加以下 4 段元数据。注意：必须放在方法内部、大括号关闭之前。

### 2.1 几何拟合

```csharp
_metadata[OperatorType.GeometricFitting] = new OperatorMetadata
{
    Type = OperatorType.GeometricFitting,
    DisplayName = "几何拟合",
    Description = "从轮廓点拟合直线、圆或椭圆",
    Category = "测量",
    IconName = "fit",
    InputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
    },
    OutputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
        new() { Name = "FitResult", DisplayName = "拟合结果", DataType = PortDataType.Any }
    },
    Parameters = new List<ParameterDefinition>
    {
        new() { Name = "FitType", DisplayName = "拟合类型", DataType = "enum", DefaultValue = "Circle",
            Options = new List<ParameterOption>
            {
                new() { Label = "直线", Value = "Line" },
                new() { Label = "圆", Value = "Circle" },
                new() { Label = "椭圆", Value = "Ellipse" }
            }
        },
        new() { Name = "Threshold", DisplayName = "二值化阈值", DataType = "double",
            DefaultValue = 127.0, MinValue = 0.0, MaxValue = 255.0 },
        new() { Name = "MinArea", DisplayName = "最小轮廓面积", DataType = "int",
            DefaultValue = 100, MinValue = 0 },
        new() { Name = "MinPoints", DisplayName = "最少拟合点数", DataType = "int",
            DefaultValue = 5, MinValue = 3, MaxValue = 10000 }
    }
};
```

### 2.2 ROI管理器

```csharp
_metadata[OperatorType.RoiManager] = new OperatorMetadata
{
    Type = OperatorType.RoiManager,
    DisplayName = "ROI管理器",
    Description = "矩形/圆形/多边形区域选择",
    Category = "辅助",
    IconName = "roi",
    InputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
    },
    OutputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "ROI图像", DataType = PortDataType.Image },
        new() { Name = "Mask", DisplayName = "掩膜", DataType = PortDataType.Image }
    },
    Parameters = new List<ParameterDefinition>
    {
        new() { Name = "Shape", DisplayName = "形状", DataType = "enum", DefaultValue = "Rectangle",
            Options = new List<ParameterOption>
            {
                new() { Label = "矩形", Value = "Rectangle" },
                new() { Label = "圆形", Value = "Circle" },
                new() { Label = "多边形", Value = "Polygon" }
            }
        },
        new() { Name = "Operation", DisplayName = "操作", DataType = "enum", DefaultValue = "Crop",
            Options = new List<ParameterOption>
            {
                new() { Label = "裁剪", Value = "Crop" },
                new() { Label = "掩膜", Value = "Mask" }
            }
        },
        new() { Name = "X", DisplayName = "X", DataType = "int", DefaultValue = 0, MinValue = 0 },
        new() { Name = "Y", DisplayName = "Y", DataType = "int", DefaultValue = 0, MinValue = 0 },
        new() { Name = "Width", DisplayName = "宽度", DataType = "int", DefaultValue = 200, MinValue = 1 },
        new() { Name = "Height", DisplayName = "高度", DataType = "int", DefaultValue = 200, MinValue = 1 },
        new() { Name = "CenterX", DisplayName = "圆心X", DataType = "int", DefaultValue = 100 },
        new() { Name = "CenterY", DisplayName = "圆心Y", DataType = "int", DefaultValue = 100 },
        new() { Name = "Radius", DisplayName = "半径", DataType = "int", DefaultValue = 50, MinValue = 1 },
        new() { Name = "PolygonPoints", DisplayName = "多边形顶点(JSON)", DataType = "string",
            DefaultValue = "[[10,10],[200,10],[200,200],[10,200]]" }
    }
};
```

### 2.3 形状匹配

```csharp
_metadata[OperatorType.ShapeMatching] = new OperatorMetadata
{
    Type = OperatorType.ShapeMatching,
    DisplayName = "形状匹配",
    Description = "旋转/缩放不变的高级模板匹配",
    Category = "匹配定位",
    IconName = "shape-match",
    InputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "搜索图像", DataType = PortDataType.Image, IsRequired = true },
        new() { Name = "Template", DisplayName = "模板图像", DataType = PortDataType.Image, IsRequired = false }
    },
    OutputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
        new() { Name = "Matches", DisplayName = "匹配结果", DataType = PortDataType.Any }
    },
    Parameters = new List<ParameterDefinition>
    {
        new() { Name = "TemplatePath", DisplayName = "模板文件路径", DataType = "file", DefaultValue = "" },
        new() { Name = "MinScore", DisplayName = "最小匹配分数", DataType = "double",
            DefaultValue = 0.7, MinValue = 0.1, MaxValue = 1.0 },
        new() { Name = "MaxMatches", DisplayName = "最大匹配数", DataType = "int",
            DefaultValue = 1, MinValue = 1, MaxValue = 50 },
        new() { Name = "AngleStart", DisplayName = "起始角度", DataType = "double",
            DefaultValue = -30.0, MinValue = -180.0, MaxValue = 180.0 },
        new() { Name = "AngleExtent", DisplayName = "角度范围", DataType = "double",
            DefaultValue = 60.0, MinValue = 0.0, MaxValue = 360.0 },
        new() { Name = "AngleStep", DisplayName = "角度步长", DataType = "double",
            DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
        new() { Name = "NumLevels", DisplayName = "金字塔层数", DataType = "int",
            DefaultValue = 3, MinValue = 1, MaxValue = 6 }
    }
};
```

### 2.4 亚像素边缘

```csharp
_metadata[OperatorType.SubpixelEdgeDetection] = new OperatorMetadata
{
    Type = OperatorType.SubpixelEdgeDetection,
    DisplayName = "亚像素边缘",
    Description = "高精度亚像素级边缘提取",
    Category = "特征提取",
    IconName = "edge-subpixel",
    InputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "输入图像", DataType = PortDataType.Image, IsRequired = true }
    },
    OutputPorts = new List<PortDefinition>
    {
        new() { Name = "Image", DisplayName = "结果图像", DataType = PortDataType.Image },
        new() { Name = "Edges", DisplayName = "边缘点集", DataType = PortDataType.Any }
    },
    Parameters = new List<ParameterDefinition>
    {
        new() { Name = "LowThreshold", DisplayName = "低阈值", DataType = "double",
            DefaultValue = 50.0, MinValue = 0.0, MaxValue = 255.0 },
        new() { Name = "HighThreshold", DisplayName = "高阈值", DataType = "double",
            DefaultValue = 150.0, MinValue = 0.0, MaxValue = 255.0 },
        new() { Name = "Sigma", DisplayName = "高斯Sigma", DataType = "double",
            DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0 },
        new() { Name = "Method", DisplayName = "亚像素方法", DataType = "enum", DefaultValue = "GradientInterp",
            Options = new List<ParameterOption>
            {
                new() { Label = "梯度插值", Value = "GradientInterp" },
                new() { Label = "高斯拟合", Value = "GaussianFit" }
            }
        }
    }
};
```

---

## 3. CameraCalibration 改进 (CameraCalibrationOperator.cs)

修改 `src\Acme.Product.Infrastructure\Operators\CameraCalibrationOperator.cs`：

1. 在 `OperatorFactory.cs` 中 `CameraCalibration` (枚举值24) 的元数据 Parameters 列表里追加 3 个参数：

```csharp
new() { Name = "Mode", DisplayName = "模式", DataType = "enum", DefaultValue = "SingleImage",
    Options = new List<ParameterOption>
    {
        new() { Label = "单图检测", Value = "SingleImage" },
        new() { Label = "文件夹标定", Value = "FolderCalibration" }
    }
},
new() { Name = "ImageFolder", DisplayName = "标定图片文件夹", DataType = "string", DefaultValue = "" },
new() { Name = "CalibrationOutputPath", DisplayName = "标定结果保存路径", DataType = "string",
    DefaultValue = "calibration_result.json" },
```

2. 在 `CameraCalibrationOperator.cs` 的 `ExecuteCoreAsync` 中，根据 `Mode` 参数分两个分支：
   - `SingleImage`: 保留现有逻辑（FindChessboardCorners）
   - `FolderCalibration`: 新增逻辑 — 从 `ImageFolder` 读取所有 png/jpg → 对每张图片 FindChessboardCorners + CornerSubPix → 收集 objectPoints 和 imagePoints → Cv2.CalibrateCamera → 将 CameraMatrix 和 DistCoeffs 序列化为 JSON 保存到 CalibrationOutputPath

---

## 4. 单元测试

在 `tests\Acme.Product.Tests\Operators\` 下创建 4 个测试文件，每个至少包含 3 个测试。模板如下（替换类名和枚举值）：

```csharp
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acme.Product.Tests.Operators;

public class {算子名}OperatorTests
{
    private readonly {算子名}Operator _operator;

    public {算子名}OperatorTests()
    {
        var logger = new Mock<ILogger<{算子名}Operator>>();
        _operator = new {算子名}Operator(logger.Object);
    }

    [Fact]
    public void OperatorType_ShouldBe{枚举值}()
    {
        _operator.OperatorType.Should().Be(OperatorType.{枚举值});
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.{枚举值}, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试", OperatorType.{枚举值}, 0, 0);
        var result = await _operator.ExecuteAsync(op, new Dictionary<string, object>());
        result.IsSuccess.Should().BeFalse();
    }
}
```

需要创建的测试文件：
- `GeometricFittingOperatorTests.cs` (枚举: GeometricFitting)
- `RoiManagerOperatorTests.cs` (枚举: RoiManager)
- `ShapeMatchingOperatorTests.cs` (枚举: ShapeMatching)
- `SubpixelEdgeDetectionOperatorTests.cs` (枚举: SubpixelEdgeDetection)

---

## 5. 验证

全部修改完成后运行：

```powershell
cd c:\Users\11234\Desktop\ClearVision\Acme.Product
dotnet build
dotnet test
```

确保 0 error，所有测试通过。
