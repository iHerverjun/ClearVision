编写面向 opencode 的详细开发指导 markdown 文档# ClearVision Phase 1 — 关键能力补齐 开发指导手册

> **适用于**: opencode / AI 编码助手  
> **目标**: 补齐 5 个工业视觉检测核心算子  
> **审计版本**: v2.0 (2026-02-13)

---

## 一、项目关键文件索引

开发任何新算子都会涉及以下文件，**按修改顺序排列**：

| # | 文件 | 绝对路径 | 作用 |
|:-:|------|---------|------|
| 1 | [OperatorEnums.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Core/Enums/OperatorEnums.cs) | `src\Acme.Product.Core\Enums\` | 添加枚举值 |
| 2 | **新算子.cs** | `src\Acme.Product.Infrastructure\Operators\` | 实现算子 |
| 3 | [OperatorFactory.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Services/OperatorFactory.cs) | `src\Acme.Product.Infrastructure\Services\` | 注册元数据 |
| 4 | [DependencyInjection.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Desktop/DependencyInjection.cs) | `src\Acme.Product.Desktop\` | DI 注册 |
| 5 | **新测试.cs** | `tests\Acme.Product.Tests\Operators\` | 单元测试 |

**参考文件**（必须先阅读再开发）：

| 文件 | 作用 | 为何重要 |
|------|------|---------|
| [OperatorBase.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Operators/OperatorBase.cs) (494行) | 算子抽象基类 | 所有 `GetParam`/`TryGetInputImage`/`CreateImageOutput` 方法都在这里 |
| [ImageWrapper.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Operators/ImageWrapper.cs) (259行) | 零拷贝图像传递 | **构造器会 Clone() Mat**，理解此行为是避免内存泄漏的关键 |
| [FlowExecutionService.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Services/FlowExecutionService.cs) (619行) | 流程执行引擎 | 理解 `PrepareOperatorInputs` 如何路由数据 |

---

## 二、强制性开发规范

### 2.1 算子骨架模板 (必须严格遵循)

```csharp
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Core.Operators;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Acme.Product.Infrastructure.Operators;

/// <summary>
/// {算子中文名} - {一句话描述}
/// </summary>
public class {算子名}Operator : OperatorBase
{
    public override OperatorType OperatorType => OperatorType.{枚举值};

    public {算子名}Operator(ILogger<{算子名}Operator> logger) : base(logger)
    {
    }

    protected override Task<OperatorExecutionOutput> ExecuteCoreAsync(
        Operator @operator,
        Dictionary<string, object>? inputs,
        CancellationToken cancellationToken)
    {
        // 1. 获取图像输入 (图像类算子必须)
        if (!TryGetInputImage(inputs, "Image", out var imageWrapper) || imageWrapper == null)
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("未提供输入图像"));
        }

        // 2. 获取参数 (始终使用 GetXxxParam 系列方法)
        var param1 = GetDoubleParam(@operator, "ParamName", defaultValue, min: 0, max: 100);
        var param2 = GetStringParam(@operator, "Method", "Default");

        // 3. 获取 Mat (使用 using 确保释放)
        using var src = imageWrapper.GetMat();
        if (src.Empty())
        {
            return Task.FromResult(OperatorExecutionOutput.Failure("无法解码输入图像"));
        }

        // 4. 核心算法 (所有临时 Mat 用 using)
        using var resultImage = src.Clone();
        using var gray = new Mat();
        // ... 算法实现 ...

        // 5. 构建输出 (必须使用 CreateImageOutput)
        var additionalData = new Dictionary<string, object>
        {
            { "ResultKey", resultValue }
        };
        return Task.FromResult(
            OperatorExecutionOutput.Success(CreateImageOutput(resultImage, additionalData)));
    }

    public override ValidationResult ValidateParameters(Operator @operator)
    {
        // 每个参数都要做边界校验
        var param = GetDoubleParam(@operator, "ParamName", 50.0);
        if (param < 0 || param > 100)
            return ValidationResult.Invalid("参数必须在 0-100 之间");

        return ValidationResult.Valid();
    }
}
```

### 2.2 关键规则 (违反将导致运行时错误)

> [!CAUTION]
> 以下规则从项目实际代码中总结得出，**违反任何一条都会导致问题**。

| # | 规则 | 原因 |
|:-:|------|------|
| 1 | **所有临时 `Mat` 必须 `using`** | OpenCvSharp 的 Mat 使用非托管内存，不 Dispose 会泄漏 |
| 2 | **`CreateImageOutput(mat)` 内部做了 `mat.Clone()`** | 通过 `ImageWrapper` 构造器，所以传入 `using var` 的 Mat 是安全的 |
| 3 | **参数获取必须用 `GetXxxParam` 方法** | JSON 反序列化后值可能是 `JsonElement` 类型，直接强转会崩 |
| 4 | **输出端口名称必须与工厂元数据中定义的一致** | `FlowExecutionService.PrepareOperatorInputs` 按端口名匹配数据 |
| 5 | **构造函数必须接受 `ILogger<具体算子类型>`** | DI 容器按泛型类型解析 Logger |
| 6 | **枚举值不能重复，用项目中最大值+1** | 当前最大值为 `HistogramEqualization = 40`，新值从 41 开始 |
| 7 | **非图像输入用 `TryGetInputValue<T>`** | 见 `OperatorBase.cs` L330-354 |
| 8 | **DI 注册固定为 `AddSingleton`** | 算子为无状态执行器，Singleton 匹配 `FlowExecutionService` 中 `_executors` 字典的生命周期 |

### 2.3 数据流关键机制

```
算子A.OutputData["Image"] (ImageWrapper)
        │
        ▼
FlowExecutionService.PrepareOperatorInputs()
  → 将 sourceOutputs 的所有 KV 合并到 inputs
        │
        ▼
算子B.ExecuteCoreAsync(inputs)
  → TryGetInputImage(inputs, "Image", out var imageWrapper)
  → TryGetInputValue<T>(inputs, "Points", out var points)
```

**关键**: 上游算子 `OutputData` 字典中的 **所有键值对** 都会被传递到下游的 `inputs` 中。所以：
- 输出键名要有意义且不冲突
- 如果需要传递非图像数据（如点集、拟合结果），直接放在 `additionalData` 中
- 下游算子通过 `TryGetInputValue<T>` 按键名取值

---

## 三、5 个算子详细开发指引

### 任务 1: 几何拟合 (GeometricFitting) — 最简单，先做

> **枚举**: `GeometricFitting = 41`  
> **文件**: `GeometricFittingOperator.cs`  
> **难度**: ⭐⭐ 低

#### 1.1 枚举定义

```csharp
// OperatorEnums.cs - 在 HistogramEqualization = 40 之后添加
/// <summary>
/// 几何拟合 - 直线/圆/椭圆拟合
/// </summary>
GeometricFitting = 41,
```

#### 1.2 工厂元数据

```csharp
// OperatorFactory.cs - InitializeDefaultOperators() 方法末尾添加
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

#### 1.3 算子核心逻辑伪代码

```
1. 获取图像 → CvtColor 灰度 → Threshold 二值化 → FindContours
2. 过滤面积 < MinArea 的轮廓
3. 合并所有轮廓点为 Point[] allPoints
4. 根据 FitType:
   - "Line":  Cv2.FitLine(allPoints, ...) → 得到 (vx, vy, x0, y0) → 画直线
   - "Circle": 实现 Kasa 最小二乘圆拟合 (见下方算法)，或用 Cv2.MinEnclosingCircle 作为初始版本
   - "Ellipse": Cv2.FitEllipse(allPoints) → 得到 RotatedRect → 画椭圆
5. 输出拟合参数到 additionalData
```

**最小二乘圆拟合 (Kasa 方法) C# 实现**:

```csharp
private (double cx, double cy, double r) FitCircleLeastSquares(Point2f[] points)
{
    int n = points.Length;
    double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0, sumXY = 0;
    double sumX3 = 0, sumY3 = 0, sumX2Y = 0, sumXY2 = 0;

    for (int i = 0; i < n; i++)
    {
        double x = points[i].X, y = points[i].Y;
        sumX += x; sumY += y;
        sumX2 += x * x; sumY2 += y * y; sumXY += x * y;
        sumX3 += x * x * x; sumY3 += y * y * y;
        sumX2Y += x * x * y; sumXY2 += x * y * y;
    }

    double A = n * sumX2 - sumX * sumX;
    double B = n * sumXY - sumX * sumY;
    double C = n * sumY2 - sumY * sumY;
    double D = 0.5 * (n * sumX3 + n * sumXY2 - sumX * sumX2 - sumX * sumY2);
    double E = 0.5 * (n * sumX2Y + n * sumY3 - sumY * sumX2 - sumY * sumY2);

    double det = A * C - B * B;
    if (Math.Abs(det) < 1e-10) return (0, 0, 0);

    double cx = (D * C - B * E) / det;
    double cy = (A * E - B * D) / det;
    double r = Math.Sqrt(sumX2 / n - 2 * cx * sumX / n + cx * cx
                       + sumY2 / n - 2 * cy * sumY / n + cy * cy);
    return (cx, cy, r);
}
```

#### 1.4 DI 注册

```csharp
// DependencyInjection.cs - 在 Phase 3 注释块之前添加
// Phase 1 关键能力补齐
services.AddSingleton<IOperatorExecutor, GeometricFittingOperator>();
```

---

### 任务 2: ROI 管理器 (RoiManager)

> **枚举**: `RoiManager = 42`  
> **文件**: `RoiManagerOperator.cs`  
> **难度**: ⭐⭐ 低

#### 2.1 枚举定义

```csharp
/// <summary>
/// ROI管理器 - 区域裁剪与掩膜
/// </summary>
RoiManager = 42,
```

#### 2.2 工厂元数据

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

#### 2.3 核心逻辑伪代码

```
1. 获取图像 + 参数
2. 根据 Shape:
   - "Rectangle":
     - 边界检查: x/y/w/h 不超出图像尺寸
     - Crop: new Mat(src, new Rect(x, y, w, h))
     - Mask: 创建全黑 Mat → Cv2.Rectangle(mask, rect, Scalar.All(255), -1) → Cv2.BitwiseAnd(src, src, dst, mask)
   - "Circle":
     - Crop: 计算外接矩形裁剪 + 圆形 mask
     - Mask: 创建全黑 Mat → Cv2.Circle(mask, center, radius, Scalar.All(255), -1) → BitwiseAnd
   - "Polygon":
     - 解析 JSON 为 Point[] → Cv2.FillPoly(mask, new[]{points}, Scalar.All(255))
3. 输出: CreateImageOutput(result, additionalData) + 单独输出 Mask 为 ImageWrapper
```

> [!IMPORTANT]
> 输出 Mask 时也用 `ImageWrapper`：`additionalData["Mask"] = new ImageWrapper(maskMat);`  
> 下游算子可通过 `TryGetInputImage(inputs, "Mask", out var mask)` 获取。

---

### 任务 3: 形状匹配 (ShapeMatching) — 核心难点

> **枚举**: `ShapeMatching = 43`  
> **文件**: `ShapeMatchingOperator.cs`  
> **难度**: ⭐⭐⭐⭐ 高

#### 3.1 枚举与元数据

```csharp
/// <summary>
/// 形状匹配 - 旋转/缩放不变模板匹配
/// </summary>
ShapeMatching = 43,
```

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

#### 3.2 算法设计 — 基于金字塔的多角度模板匹配

```
=== 阶段一: 准备 ===
1. 获取模板: 优先从 Template 输入端口，否则从 TemplatePath 参数加载文件
2. 转灰度: src_gray, tmpl_gray

=== 阶段二: 构建旋转模板集 ===
3. angles = [AngleStart, AngleStart+AngleStep, ..., AngleStart+AngleExtent]
4. 对每个 angle:
   - rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0)
   - rotatedTmpl = Cv2.WarpAffine(tmpl_gray, rotMatrix, size)
   - 存入 Dictionary<double, Mat>

=== 阶段三: 金字塔粗搜索 ===
5. 在最顶层金字塔（最小分辨率）上对每个角度做 MatchTemplate
6. 初筛得分 > MinScore * 0.8 的候选 (angle, x, y)

=== 阶段四: 精搜索 ===
7. 在原图上对候选区域做 MatchTemplate 精定位
8. NMS 去除重叠匹配（IoU > 0.5 的保留得分最高者）
9. 取前 MaxMatches 个结果

=== 阶段五: 输出 ===
10. 在结果图上绘制匹配区域旋转矩形 + 分数
11. 输出 matchResults List<Dictionary> 含 X, Y, Angle, Score, CenterX, CenterY
```

> [!TIP]
> 使用 `Parallel.For` 并行化阶段二和阶段三的循环，显著加速大角度范围搜索。  
> 注意: `Cv2.MatchTemplate` 本身是线程安全的，但结果 `Mat` 不是，每个线程需要自己的 result Mat。

#### 3.3 模板获取逻辑

```csharp
// 优先从输入端口获取模板（前一个算子传来的图像）
Mat? templateMat = null;

if (TryGetInputImage(inputs, "Template", out var templateWrapper) && templateWrapper != null)
{
    templateMat = templateWrapper.GetMat();
}
else
{
    // 从文件路径参数加载
    var templatePath = GetStringParam(@operator, "TemplatePath", "");
    if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
    {
        templateMat = Cv2.ImRead(templatePath, ImreadModes.Color);
    }
}

if (templateMat == null || templateMat.Empty())
{
    return Task.FromResult(OperatorExecutionOutput.Failure("未提供模板图像，请连接模板输入或设置模板文件路径"));
}
```

---

### 任务 4: 亚像素边缘提取 (SubpixelEdgeDetection)

> **枚举**: `SubpixelEdgeDetection = 44`  
> **文件**: `SubpixelEdgeDetectionOperator.cs`  
> **难度**: ⭐⭐⭐ 中

#### 4.1 枚举与元数据

```csharp
/// <summary>
/// 亚像素边缘提取 - 高精度边缘定位
/// </summary>
SubpixelEdgeDetection = 44,
```

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

#### 4.2 算法设计 — 基于梯度法向插值的亚像素定位

```
1. 灰度 → GaussianBlur(sigma) → Canny(low, high) → FindContours
2. 对每个轮廓中的每个像素级边缘点 P(x,y):
   a. 计算梯度方向: dx = Sobel_X(x,y), dy = Sobel_Y(x,y)
   b. 沿梯度方向取 3 个采样值:
      g_neg = 双线性插值(x - dx_norm, y - dy_norm)
      g_cur = gray(x, y)
      g_pos = 双线性插值(x + dx_norm, y + dy_norm)
   c. 抛物线拟合求亚像素偏移 offset:
      offset = 0.5 * (g_neg - g_pos) / (g_neg - 2*g_cur + g_pos)
   d. 亚像素坐标:
      subpixel_x = x + offset * dx_norm
      subpixel_y = y + offset * dy_norm
3. 输出 Point2f[] 坐标数组
```

> [!NOTE]
> 输出数据格式建议: `List<Dictionary<string, object>>` 每个元素含 `{ "X": float, "Y": float, "Strength": float }`  
> 这样下游的几何拟合算子可以直接通过 `TryGetInputValue<object>(inputs, "Edges", out var edges)` 获取。

---

### 任务 5: 相机标定改进 (CameraCalibration — 修改现有算子)

> **文件**: [CameraCalibrationOperator.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/src/Acme.Product.Infrastructure/Operators/CameraCalibrationOperator.cs)  
> **类型**: 修改现有算子（枚举值 `CameraCalibration = 24` 保持不变）  
> **难度**: ⭐⭐⭐ 中

#### 5.1 改进点

| 现状问题 | 改进方案 |
|---------|---------|
| 只做 `FindChessboardCorners`，不做标定 | 添加 `Cv2.CalibrateCamera` 调用 |
| 单图输入 | 新增 `ImageFolder` 参数，支持文件夹批量读取 |
| 标定结果不持久化 | 保存为 JSON 文件到 `CalibrationOutputPath` |
| 无矫正功能 | 已有独立的 `UndistortOperator`(枚举25)，无需重复 |

#### 5.2 新增参数

在 `OperatorFactory.cs` 中 `CameraCalibration` 的元数据里补充：

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

#### 5.3 文件夹标定核心逻辑

```csharp
if (mode == "FolderCalibration")
{
    var imageFiles = Directory.GetFiles(imageFolder, "*.png")
        .Concat(Directory.GetFiles(imageFolder, "*.jpg")).ToArray();

    var objectPoints = new List<Mat>();  // 世界坐标系
    var imagePoints = new List<Mat>();   // 图像坐标系
    Size imageSize = default;

    foreach (var file in imageFiles)
    {
        using var img = Cv2.ImRead(file, ImreadModes.Grayscale);
        if (imageSize == default) imageSize = img.Size();

        if (Cv2.FindChessboardCorners(img, patternSize, out var corners))
        {
            Cv2.CornerSubPix(img, corners, new Size(11, 11), new Size(-1, -1),
                new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001));

            imagePoints.Add(Mat.FromArray(corners));

            // 构建世界坐标 (棋盘格)
            var objPts = new Point3f[patternSize.Width * patternSize.Height];
            for (int j = 0; j < patternSize.Height; j++)
                for (int i = 0; i < patternSize.Width; i++)
                    objPts[j * patternSize.Width + i] = new Point3f(i * gridSize, j * gridSize, 0);
            objectPoints.Add(Mat.FromArray(objPts));
        }
    }

    // 标定
    var cameraMatrix = new Mat();
    var distCoeffs = new Mat();
    Cv2.CalibrateCamera(objectPoints, imagePoints, imageSize,
        cameraMatrix, distCoeffs, out _, out _);

    // 保存 JSON
    var calibData = new { CameraMatrix = MatToArray(cameraMatrix), DistCoeffs = MatToArray(distCoeffs) };
    File.WriteAllText(outputPath, JsonSerializer.Serialize(calibData, new JsonSerializerOptions { WriteIndented = true }));
}
```

---

## 四、单元测试模板

每个算子至少需要以下 3 类测试。参考现有 [OperatorTests.cs](file:///c:/Users/11234/Desktop/ClearVision/Acme.Product/tests/Acme.Product.Tests/Operators/OperatorTests.cs)：

```csharp
using Acme.Product.Core.Entities;
using Acme.Product.Core.Enums;
using Acme.Product.Infrastructure.Operators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Acme.Product.Tests.Operators;

public class GeometricFittingOperatorTests
{
    private readonly GeometricFittingOperator _operator;

    public GeometricFittingOperatorTests()
    {
        var logger = new Mock<ILogger<GeometricFittingOperator>>();
        _operator = new GeometricFittingOperator(logger.Object);
    }

    [Fact]
    public void OperatorType_ShouldBeGeometricFitting()
    {
        _operator.OperatorType.Should().Be(OperatorType.GeometricFitting);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullInputs_ShouldReturnFailure()
    {
        var op = new Operator("测试拟合", OperatorType.GeometricFitting, 0, 0);
        var result = await _operator.ExecuteAsync(op, null);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ValidateParameters_WithInvalidMinPoints_ShouldReturnInvalid()
    {
        var op = new Operator("测试拟合", OperatorType.GeometricFitting, 0, 0);
        op.AddParameter(new Parameter(Guid.NewGuid(), "MinPoints", "最少点数", "", "int", 1));
        var result = _operator.ValidateParameters(op);
        result.IsValid.Should().BeFalse();
    }
}
```

> [!IMPORTANT]
> 注意构造函数需要 Mock `ILogger`，因为 `OperatorBase` 构造函数强制要求非 null 的 Logger。

---

## 五、构建验证

每完成一个算子后运行：

```powershell
# 编译验证
cd c:\Users\11234\Desktop\ClearVision\Acme.Product
dotnet build src\Acme.Product.Infrastructure\Acme.Product.Infrastructure.csproj

# 运行单元测试
dotnet test tests\Acme.Product.Tests\Acme.Product.Tests.csproj --filter "FullyQualifiedName~GeometricFitting"

# 全量构建 (所有算子完成后)
dotnet build
dotnet test
```

---

## 六、开发顺序总结

| 顺序 | 算子 | 枚举值 | 理由 |
|:----:|------|:------:|------|
| 1 | GeometricFitting | 41 | 最独立，OpenCV API 直接可用 |
| 2 | RoiManager | 42 | 基础工具，逻辑简单 |
| 3 | ShapeMatching | 43 | 核心价值最大，需要多角度匹配算法 |
| 4 | SubpixelEdgeDetection | 44 | 算法较难，但接口先行 |
| 5 | CameraCalibration 改进 | 24(已有) | 修改现有代码，放最后 |
